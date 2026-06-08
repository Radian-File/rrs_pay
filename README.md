# RRS Pay

RRS Pay is a desktop payroll MVP built with WPF and SQLite. It demonstrates an HR/payroll workflow for maintaining employees, departments, positions, attendance, payroll runs, reports, audit history, and local backup/restore utilities.

## Stack

- **UI:** WPF on .NET 10 (`net10.0-windows`)
- **Data:** Entity Framework Core with SQLite
- **Reports/exports:** ClosedXML for spreadsheet exports when available, CSV/text fallback for audit activity, QuestPDF for PDF payslip/report workflows
- **Storage model:** Local-first desktop database

## Feature overview

- Dashboard and navigation shell for the payroll workspace
- Employee, department, and position maintenance
- Attendance review and payroll preparation screens
- Payroll settings persisted through `PayrollSetting` records
- Audit logging for sensitive activities such as login, settings updates, backup, and restore
- Audit log filtering and CSV export
- SQLite database backup and restore from the Settings page
- Portfolio publish script for creating a distributable build folder

## Run locally

1. Install the .NET SDK that supports `net10.0-windows`.
2. From the repository root, restore and build:
   ```powershell
   dotnet restore .\rrs_pay.slnx
   dotnet build .\rrs_pay.slnx
   ```
3. Run the desktop app:
   ```powershell
   dotnet run --project .\rrs_pay\rrs_pay.csproj
   ```

The app creates the local SQLite database on first launch.

## Demo seed credentials

Seeded users share the same demo password:

- `admin` / `admin123`
- `payroll.manager` / `admin123`
- `hr.officer` / `admin123`

> These credentials are for local MVP/demo use only. Change the authentication model before production use.

## Local data locations

RRS Pay intentionally writes runtime data outside the git repository:

- **SQLite database:** `%LOCALAPPDATA%\RRS Pay\Data\rrs_pay.db`
- **Backups:** `%USERPROFILE%\Documents\RRS Pay\Backups\rrs_pay-backup-yyyyMMdd-HHmmss.db`
- **Exports:** `%USERPROFILE%\Documents\RRS Pay\Exports\`

Restore creates a safety backup of the current database before replacing it. If SQLite WAL/SHM companion files are present, restore attempts to archive current companions and copy backup companions where available.

## Portfolio publish

Use the helper script from the repository root:

```powershell
.\scripts\publish-portfolio.ps1
```

By default it publishes the WPF project to `artifacts\portfolio-publish`, which is ignored by git.

## Screenshots

Screenshot placeholders and capture guidance live in [docs/screenshots/README.md](docs/screenshots/README.md). Actual screenshots should be captured intentionally from a polished build and added only if the team wants them committed.

## Known limitations

- This is an MVP desktop app, not a production payroll compliance system.
- Local SQLite storage is single-user and should not be treated as a shared multi-user database.
- Authentication uses seeded/demo users and SHA-256 password hashing for MVP purposes.
- Backup/restore should be run when no other RRS Pay windows or external tools are using the database.
- Payroll calculations, statutory rules, and report formats should be validated against the target jurisdiction before real use.
- Some UI flows are portfolio/demo oriented and may need additional validation, permissions, and test coverage before production.
