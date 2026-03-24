using System.Text.Json;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

public sealed class RegistrationService : IRegistrationService
{
    private readonly IDbContextFactory<ErpDbContext> _dbContextFactory;
    private readonly IPasswordHasher _passwordHasher;

    public RegistrationService(
        IDbContextFactory<ErpDbContext> dbContextFactory,
        IPasswordHasher passwordHasher)
    {
        _dbContextFactory = dbContextFactory;
        _passwordHasher = passwordHasher;
    }

    public async Task<RegisterResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return RegisterResult.Failed("회원가입 요청이 비어 있습니다.");
        }

        var normalizedUsername = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return RegisterResult.Failed("사용자명을 입력하세요.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return RegisterResult.Failed("비밀번호는 8자 이상이어야 합니다.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedName = NormalizeOptional(request.Name);
        var normalizedPhoneNumber = NormalizePhoneNumber(request.PhoneNumber);
        var normalizedCompany = NormalizeOptional(request.Company);

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return RegisterResult.Failed("이름을 입력하세요.");
        }

        if (string.IsNullOrWhiteSpace(normalizedCompany))
        {
            return RegisterResult.Failed("회사를 입력하세요.");
        }

        if (string.IsNullOrWhiteSpace(normalizedPhoneNumber))
        {
            return RegisterResult.Failed("전화번호 형식이 올바르지 않습니다.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var usernameExists = await db.Users
            .AnyAsync(x => x.Username == normalizedUsername, cancellationToken);
        if (usernameExists)
        {
            return RegisterResult.Failed("동일한 사용자명이 이미 존재합니다.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            var emailExists = await db.Users
                .AnyAsync(x => x.Email == normalizedEmail, cancellationToken);
            if (emailExists)
            {
                return RegisterResult.Failed("동일한 이메일이 이미 존재합니다.");
            }
        }

        var user = new User(
            normalizedUsername,
            _passwordHasher.Hash(request.Password),
            normalizedEmail,
            normalizedName,
            normalizedPhoneNumber,
            normalizedCompany);

        db.Users.Add(user);
        db.AuditLogs.Add(new AuditLog(
            actorUserId: null,
            action: "User.Registered",
            target: user.Username,
            detailJson: SerializeDetail(new { user.Email, user.Name, user.PhoneNumber, user.Company, user.Status }),
            ip: null));

        await db.SaveChangesAsync(cancellationToken);

        return RegisterResult.Succeeded();
    }

    private static string? NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string? NormalizePhoneNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
        {
            return null;
        }

        return digits.Length switch
        {
            10 => $"{digits[..3]}-{digits.Substring(3, 3)}-{digits.Substring(6, 4)}",
            11 => $"{digits[..3]}-{digits.Substring(3, 4)}-{digits.Substring(7, 4)}",
            _ => null
        };
    }

    private static string SerializeDetail(object detail)
    {
        return JsonSerializer.Serialize(detail);
    }
}
