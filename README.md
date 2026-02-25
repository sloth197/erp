# ERP Desktop

## Security Notes

- This repository does not store real credentials.
- Copy `.env.example` to `.env` and set real values locally.
- Required env vars:
  - `ERP_DB_NAME`
  - `ERP_DB_USER`
  - `ERP_DB_PASSWORD`
  - `ERP_SEED_ADMIN_PASSWORD`
  - `ERP_SEED_STAFF_PASSWORD`
- `appsettings.json` uses env placeholders and should not contain real secrets.
