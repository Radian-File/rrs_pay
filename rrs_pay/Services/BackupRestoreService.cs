using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using rrs_pay.Data;

namespace rrs_pay.Services;

public sealed class BackupRestoreService
{
    private const string BackupAction = "Backup Database";
    private const string RestoreAction = "Restore Database";

    private readonly AuditLogService _auditLogService;
    private readonly Func<AppDbContext> _contextFactory;

    public BackupRestoreService()
        : this(new AuditLogService(), AppDbContext.CreateDefault)
    {
    }

    public BackupRestoreService(AuditLogService auditLogService, Func<AppDbContext> contextFactory)
    {
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public static string BackupsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "RRS Pay",
        "Backups");

    public async Task<BackupResult> BackupAsync(CancellationToken cancellationToken = default)
    {
        var databasePath = AppDbContext.DefaultDatabasePath;
        EnsureDatabaseIsAvailable(databasePath);

        Directory.CreateDirectory(BackupsFolder);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(BackupsFolder, $"rrs_pay-backup-{timestamp}.db");

        await CreateSqliteBackupAsync(databasePath, backupPath, cancellationToken);
        await ValidateDatabaseAsync(backupPath, cancellationToken);

        await _auditLogService.LogAsync(
            BackupAction,
            "Database",
            Path.GetFileName(backupPath),
            $"Database backup created at '{backupPath}'.",
            cancellationToken: cancellationToken);

        return new BackupResult(backupPath, DateTime.Now, "Backup completed successfully.");
    }

    public async Task<RestoreResult> RestoreAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            throw new InvalidOperationException("Choose a backup database file before restoring.");
        }

        var sourcePath = Path.GetFullPath(backupPath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The selected backup file could not be found.", sourcePath);
        }

        await ValidateDatabaseAsync(sourcePath, cancellationToken);

        var currentDatabasePath = AppDbContext.DefaultDatabasePath;
        var databaseDirectory = Path.GetDirectoryName(currentDatabasePath);
        if (string.IsNullOrWhiteSpace(databaseDirectory))
        {
            throw new InvalidOperationException("RRS Pay could not resolve the local database folder.");
        }

        Directory.CreateDirectory(databaseDirectory);
        Directory.CreateDirectory(BackupsFolder);

        BackupResult? safetyBackup = null;
        if (File.Exists(currentDatabasePath))
        {
            safetyBackup = await BackupAsync(cancellationToken);
        }

        SqliteConnection.ClearAllPools();
        await MoveCurrentCompanionFilesAsync(currentDatabasePath, cancellationToken);

        try
        {
            File.Copy(sourcePath, currentDatabasePath, overwrite: true);
            await CopyCompanionFileIfPresentAsync(sourcePath, currentDatabasePath, "-wal", cancellationToken);
            await CopyCompanionFileIfPresentAsync(sourcePath, currentDatabasePath, "-shm", cancellationToken);
            await ValidateDatabaseAsync(currentDatabasePath, cancellationToken);

            await _auditLogService.LogAsync(
                RestoreAction,
                "Database",
                Path.GetFileName(sourcePath),
                safetyBackup is null
                    ? $"Database restored from '{sourcePath}'. No previous database was present."
                    : $"Database restored from '{sourcePath}'. Safety backup: '{safetyBackup.BackupPath}'.",
                cancellationToken: cancellationToken);

            return new RestoreResult(
                sourcePath,
                safetyBackup?.BackupPath,
                DateTime.Now,
                "Restore completed successfully. Restart RRS Pay if any page still shows old data.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "RRS Pay could not restore the selected backup. Close other app windows that may be using the database and try again. " +
                $"Your safety backup remains in '{BackupsFolder}'. Details: {ex.Message}",
                ex);
        }
    }

    private static void EnsureDatabaseIsAvailable(string databasePath)
    {
        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException(
                "The local RRS Pay database does not exist yet. Open the app once so it can initialize the database, then try again.",
                databasePath);
        }
    }

    private static async Task CreateSqliteBackupAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            using var sourceConnection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = sourcePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());

            using var destinationConnection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = destinationPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ToString());

            sourceConnection.Open();
            destinationConnection.Open();
            sourceConnection.BackupDatabase(destinationConnection);
        }, cancellationToken);
    }

    private static async Task ValidateDatabaseAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());

        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException("The selected file is not a readable RRS Pay SQLite database.", ex);
        }

        await using (var quickCheckCommand = connection.CreateCommand())
        {
            quickCheckCommand.CommandText = "PRAGMA quick_check;";
            var quickCheck = (await quickCheckCommand.ExecuteScalarAsync(cancellationToken))?.ToString();
            if (!string.Equals(quickCheck, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SQLite integrity validation failed: {quickCheck ?? "no result"}.");
            }
        }

        var requiredTables = new[]
        {
            "Users",
            "Employees",
            "Departments",
            "Positions",
            "Attendances",
            "Payrolls",
            "PayrollDetails",
            "PayrollSettings",
            "AuditLogs"
        };

        foreach (var table in requiredTables)
        {
            await using var tableCommand = connection.CreateCommand();
            tableCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            tableCommand.Parameters.AddWithValue("$name", table);
            var exists = Convert.ToInt32(await tableCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

            if (exists == 0)
            {
                throw new InvalidOperationException($"The selected database is missing the required '{table}' table.");
            }
        }
    }

    private static async Task CopyCompanionFileIfPresentAsync(string sourceDatabasePath, string targetDatabasePath, string suffix, CancellationToken cancellationToken)
    {
        var sourceCompanionPath = sourceDatabasePath + suffix;
        if (!File.Exists(sourceCompanionPath))
        {
            return;
        }

        await using var source = File.Open(sourceCompanionPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var target = File.Open(targetDatabasePath + suffix, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken);
    }

    private static async Task MoveCurrentCompanionFilesAsync(string databasePath, CancellationToken cancellationToken)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var currentCompanionPath = databasePath + suffix;
            if (!File.Exists(currentCompanionPath))
            {
                continue;
            }

            var archivedPath = Path.Combine(BackupsFolder, $"rrs_pay-pre-restore-{timestamp}{suffix}");
            await Task.Run(() => File.Move(currentCompanionPath, archivedPath, overwrite: true), cancellationToken);
        }
    }
}

public sealed record BackupResult(string BackupPath, DateTime CreatedAt, string Message);

public sealed record RestoreResult(string RestoredFromPath, string? SafetyBackupPath, DateTime RestoredAt, string Message);
