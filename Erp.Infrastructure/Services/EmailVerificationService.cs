using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

public sealed class EmailVerificationService : IEmailVerificationService
{
    private const string CodeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private readonly IDbContextFactory<ErpDbContext> _dbContextFactory;
    private readonly IEmailSender _emailSender;
    private readonly EmailVerificationOptions _options;

    public EmailVerificationService(
        IDbContextFactory<ErpDbContext> dbContextFactory,
        IEmailSender emailSender,
        EmailVerificationOptions options)
    {
        _dbContextFactory = dbContextFactory;
        _emailSender = emailSender;
        _options = options;
    }

    public async Task<SendEmailVerificationCodeResult> SendCodeAsync(
        SendEmailVerificationCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return SendEmailVerificationCodeResult.Failed("요청이 비어 있습니다.");
        }

        var email = NormalizeEmail(request.Email);
        if (!IsValidEmail(email))
        {
            return SendEmailVerificationCodeResult.Failed("이메일 형식이 올바르지 않습니다.");
        }

        var purpose = NormalizePurpose(request.Purpose);
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(_options.ExpiresInMinutes);
        var code = GenerateCode(_options.CodeLength);
        var codeHash = ComputeCodeHash(email, purpose, code);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var activeCodes = await db.EmailVerificationCodes
            .Where(x =>
                x.Email == email &&
                x.Purpose == purpose &&
                !x.IsRevoked &&
                !x.VerifiedAtUtc.HasValue)
            .ToListAsync(cancellationToken);

        foreach (var activeCode in activeCodes)
        {
            activeCode.Revoke();
        }

        var entity = new EmailVerificationCode(
            email,
            purpose,
            codeHash,
            expiresAtUtc,
            _options.MaxAttemptCount);

        db.EmailVerificationCodes.Add(entity);
        db.AuditLogs.Add(new AuditLog(
            actorUserId: null,
            action: "EmailVerification.CodeIssued",
            target: email,
            detailJson: SerializeDetail(new { purpose, expiresAtUtc }),
            ip: null));

        await db.SaveChangesAsync(cancellationToken);

        var textBody =
            $"Your ERP verification code is: {code}{Environment.NewLine}" +
            $"This code expires at {expiresAtUtc:O}.";

        try
        {
            await _emailSender.SendAsync(
                email,
                _options.Subject,
                textBody,
                htmlBody: null,
                cancellationToken);

            entity.MarkSent(now);
            db.AuditLogs.Add(new AuditLog(
                actorUserId: null,
                action: "EmailVerification.CodeSent",
                target: email,
                detailJson: SerializeDetail(new { purpose }),
                ip: null));

            await db.SaveChangesAsync(cancellationToken);

            return SendEmailVerificationCodeResult.Succeeded(expiresAtUtc);
        }
        catch (Exception ex)
        {
            entity.Revoke();
            db.AuditLogs.Add(new AuditLog(
                actorUserId: null,
                action: "EmailVerification.SendFailed",
                target: email,
                detailJson: SerializeDetail(new { purpose, reason = ex.Message }),
                ip: null));

            await db.SaveChangesAsync(cancellationToken);

            return SendEmailVerificationCodeResult.Failed("인증번호 메일 발송에 실패했습니다.");
        }
    }

    public async Task<VerifyEmailVerificationCodeResult> VerifyCodeAsync(
        VerifyEmailVerificationCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return VerifyEmailVerificationCodeResult.Failed("요청이 비어 있습니다.");
        }

        var email = NormalizeEmail(request.Email);
        if (!IsValidEmail(email))
        {
            return VerifyEmailVerificationCodeResult.Failed("이메일 형식이 올바르지 않습니다.");
        }

        var purpose = NormalizePurpose(request.Purpose);
        var code = request.Code?.Trim() ?? string.Empty;
        if (!IsValidCodeFormat(code))
        {
            return VerifyEmailVerificationCodeResult.Failed("인증번호 형식이 올바르지 않습니다.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.EmailVerificationCodes
            .Where(x =>
                x.Email == email &&
                x.Purpose == purpose &&
                !x.IsRevoked &&
                !x.VerifiedAtUtc.HasValue)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return VerifyEmailVerificationCodeResult.Failed("인증번호를 먼저 요청하세요.");
        }

        var now = DateTime.UtcNow;
        if (!entity.CanVerify(now))
        {
            var failureMessage = entity.ExpiresAtUtc <= now
                ? "인증번호가 만료되었습니다. 다시 요청하세요."
                : "인증 시도 횟수를 초과했습니다. 다시 요청하세요.";

            db.AuditLogs.Add(new AuditLog(
                actorUserId: null,
                action: "EmailVerification.VerifyFailed",
                target: email,
                detailJson: SerializeDetail(new { purpose, reason = "not_verifiable" }),
                ip: null));
            await db.SaveChangesAsync(cancellationToken);

            return VerifyEmailVerificationCodeResult.Failed(failureMessage, 0);
        }

        entity.RegisterAttempt(now);

        var inputHash = ComputeCodeHash(email, purpose, code);
        if (!FixedTimeEquals(inputHash, entity.CodeHash))
        {
            var remaining = Math.Max(0, entity.MaxAttemptCount - entity.AttemptCount);
            if (remaining == 0)
            {
                entity.Revoke();
            }

            db.AuditLogs.Add(new AuditLog(
                actorUserId: null,
                action: "EmailVerification.VerifyFailed",
                target: email,
                detailJson: SerializeDetail(new { purpose, reason = "invalid_code", remaining }),
                ip: null));
            await db.SaveChangesAsync(cancellationToken);

            return VerifyEmailVerificationCodeResult.Failed("인증번호가 올바르지 않습니다.", remaining);
        }

        entity.MarkVerified(now);
        db.AuditLogs.Add(new AuditLog(
            actorUserId: null,
            action: "EmailVerification.Verified",
            target: email,
            detailJson: SerializeDetail(new { purpose }),
            ip: null));

        await db.SaveChangesAsync(cancellationToken);

        return VerifyEmailVerificationCodeResult.Succeeded();
    }

    private static string ComputeCodeHash(string email, string purpose, string code)
    {
        var payload = $"{email}|{purpose}|{code}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        if (leftBytes.Length != rightBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private string GenerateCode(int length)
    {
        var value = new List<char>(length)
        {
            RandomUpper(),
            RandomLower(),
            RandomDigit()
        };

        while (value.Count < length)
        {
            var index = RandomNumberGenerator.GetInt32(CodeCharacters.Length);
            value.Add(CodeCharacters[index]);
        }

        for (var i = value.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (value[i], value[j]) = (value[j], value[i]);
        }

        return new string(value.ToArray());
    }

    private static string SerializeDetail(object detail)
    {
        return JsonSerializer.Serialize(detail);
    }

    private string NormalizePurpose(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? _options.DefaultPurpose
            : value.Trim();

        return normalized.ToLowerInvariant();
    }

    private static string NormalizeEmail(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static bool IsValidEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            _ = new MailAddress(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsValidCodeFormat(string value)
    {
        if (value.Length != _options.CodeLength)
        {
            return false;
        }

        var hasUpper = false;
        var hasLower = false;
        var hasDigit = false;

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (!char.IsAsciiLetterOrDigit(ch))
            {
                return false;
            }

            if (char.IsAsciiLetterUpper(ch))
            {
                hasUpper = true;
            }
            else if (char.IsAsciiLetterLower(ch))
            {
                hasLower = true;
            }
            else if (char.IsDigit(ch))
            {
                hasDigit = true;
            }
        }

        return hasUpper && hasLower && hasDigit;
    }

    private static char RandomUpper()
    {
        return (char)('A' + RandomNumberGenerator.GetInt32(26));
    }

    private static char RandomLower()
    {
        return (char)('a' + RandomNumberGenerator.GetInt32(26));
    }

    private static char RandomDigit()
    {
        return (char)('0' + RandomNumberGenerator.GetInt32(10));
    }
}
