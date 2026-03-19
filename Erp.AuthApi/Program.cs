using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Domain.Entities;
using Erp.Infrastructure.Extensions;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

LoadDotEnvIfExists();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/", () => Results.Ok(new
{
    name = "Erp.AuthApi",
    status = "running"
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    utcNow = DateTime.UtcNow
}));

var authGroup = app.MapGroup("/auth");
var emailGroup = authGroup.MapGroup("/email");

emailGroup.MapPost("/send-code", async (
    SendEmailVerificationCodeRequest request,
    IEmailVerificationService emailVerificationService,
    CancellationToken cancellationToken) =>
{
    var result = await emailVerificationService.SendCodeAsync(request, cancellationToken);
    if (!result.Success)
    {
        return Results.BadRequest(result);
    }

    return Results.Ok(result);
});

emailGroup.MapPost("/verify-code", async (
    VerifyEmailVerificationCodeRequest request,
    IEmailVerificationService emailVerificationService,
    CancellationToken cancellationToken) =>
{
    var result = await emailVerificationService.VerifyCodeAsync(request, cancellationToken);
    if (!result.Success)
    {
        return Results.BadRequest(result);
    }

    return Results.Ok(result);
});

authGroup.MapPost("/signup", async (
    RegisterRequest request,
    IRegistrationService registrationService,
    IDbContextFactory<ErpDbContext> dbContextFactory,
    CancellationToken cancellationToken) =>
{
    var normalizedEmail = NormalizeEmail(request.Email);
    if (string.IsNullOrWhiteSpace(normalizedEmail))
    {
        return Results.BadRequest(RegisterResult.Failed("이메일은 필수입니다."));
    }

    await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

    var verification = await db.EmailVerificationCodes
        .Where(x =>
            x.Email == normalizedEmail &&
            x.Purpose == "signup" &&
            !x.IsRevoked &&
            x.VerifiedAtUtc.HasValue)
        .OrderByDescending(x => x.VerifiedAtUtc)
        .FirstOrDefaultAsync(cancellationToken);

    if (verification is null || verification.ExpiresAtUtc <= DateTime.UtcNow)
    {
        return Results.BadRequest(RegisterResult.Failed("이메일 인증을 먼저 완료하세요."));
    }

    var registerResult = await registrationService.RegisterAsync(
        new RegisterRequest(request.Username, request.Password, normalizedEmail),
        cancellationToken);

    if (!registerResult.Success)
    {
        return Results.BadRequest(registerResult);
    }

    var normalizedUsername = request.Username?.Trim();
    if (string.IsNullOrWhiteSpace(normalizedUsername))
    {
        return Results.BadRequest(RegisterResult.Failed("사용자명을 입력하세요."));
    }

    var user = await db.Users.FirstOrDefaultAsync(x => x.Username == normalizedUsername, cancellationToken);
    if (user is null)
    {
        return Results.BadRequest(RegisterResult.Failed("회원가입 처리 중 오류가 발생했습니다."));
    }

    if (user.Status != UserStatus.Active || !user.IsActive)
    {
        user.Approve();
    }

    const string defaultRoleName = "Staff";
    var staffRole = await db.Roles.FirstOrDefaultAsync(x => x.Name == defaultRoleName, cancellationToken);
    if (staffRole is null)
    {
        staffRole = new Role(defaultRoleName);
        db.Roles.Add(staffRole);
    }

    var hasRole = await db.UserRoles
        .AnyAsync(x => x.UserId == user.Id && x.RoleId == staffRole.Id, cancellationToken);
    if (!hasRole)
    {
        db.UserRoles.Add(new UserRole(user.Id, staffRole.Id));
    }

    verification.Revoke();

    db.AuditLogs.Add(new AuditLog(
        actorUserId: null,
        action: "User.RegisteredViaEmailVerification",
        target: user.Username,
        detailJson: null,
        ip: null));

    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(RegisterResult.Succeeded());
});

app.Run();

static string? NormalizeEmail(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    return value.Trim().ToLowerInvariant();
}

static void LoadDotEnvIfExists()
{
    foreach (var path in EnumerateDotEnvCandidates())
    {
        if (!File.Exists(path))
        {
            continue;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = trimmed[(separatorIndex + 1)..].Trim();
            if (value.Length >= 2 &&
                ((value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)) ||
                 (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal))))
            {
                value = value[1..^1];
            }

            Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
        }

        return;
    }
}

static IEnumerable<string> EnumerateDotEnvCandidates()
{
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var roots = new[]
    {
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory
    };

    foreach (var root in roots)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            continue;
        }

        var current = root;
        for (var depth = 0; depth < 8 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            var candidate = Path.Combine(current, ".env");
            if (seen.Add(candidate))
            {
                yield return candidate;
            }

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }
    }
}
