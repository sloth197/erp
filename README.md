# ERP Desktop

## Prerequisites

- .NET SDK 8.0
- Docker Desktop (with Docker Compose)
- PostgreSQL port `5432` available on localhost

## Configuration

This repository does not store real credentials. Keep secrets in environment variables.

PowerShell example:

```powershell
$env:ERP_DB_NAME = "erp"
$env:ERP_DB_USER = "erp"
$env:ERP_DB_PASSWORD = "change_me"
$env:ERP_SEED_ADMIN_PASSWORD = "change_me_admin"
$env:ERP_SEED_STAFF_PASSWORD = "change_me_staff"
```

Required variables:

- `ERP_DB_NAME`
- `ERP_DB_USER`
- `ERP_DB_PASSWORD`
- `ERP_SEED_ADMIN_PASSWORD`
- `ERP_SEED_STAFF_PASSWORD`

`Erp.Desktop/appsettings.json` uses `${...}` placeholders and resolves them from environment variables at runtime.

## Run (Reproducible)

1. Start PostgreSQL:

   ```powershell
   docker compose up -d
   docker compose ps
   ```

   Ensure `postgres` is `healthy`.

2. Build solution:

   ```powershell
   dotnet build Erp.sln -c Debug
   ```

3. Start desktop app:

   ```powershell
   dotnet run --project Erp.Desktop
   ```

On startup, the app runs EF Core migrations and idempotent seed automatically.

## Signup Flow (Pending -> Active)

1. A new user signs up from the signup screen.
2. Account is created with `Pending` status.
3. `Pending` users cannot log in (`승인 대기 중입니다.` message).
4. Admin reviews pending users and approves the account.
5. Approved account becomes `Active` and can log in.

## Admin Approval Operation

Only users with `Master.Users.Write` can approve/reject/disable.

- Open `User & Access` screen and go to `승인 대기` tab.
- `Approve`: changes `Pending -> Active`, records approver/time, assigns default `Staff` role.
- `Reject`: marks user as rejected (optional reason).
- `Disable`: disables account (active users are blocked from login).

## Default Role Policy

- Seeded roles: `Admin`, `Staff`
- Approved signup users get `Staff` role by default.
- `Admin` has full permissions.
- `Staff` is read-focused baseline (for example item/partner/inventory read scopes).

## Security Policy

- Passwords are hashed with PBKDF2-SHA256 (`100,000` iterations + per-user salt).
- Plain-text passwords are never stored.
- Login lockout policy: 5 consecutive failures -> 5 minutes lockout.
- Audit logs are stored for authentication and approval actions (login success/failure, lockout, logout, approve/reject/disable).
