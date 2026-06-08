using System.IO;
using Microsoft.EntityFrameworkCore;
using rrs_pay.Data;

namespace rrs_pay.Services;

public class DatabaseInitializer
{
    private readonly Func<AppDbContext> _contextFactory;

    public DatabaseInitializer() : this(AppDbContext.CreateDefault)
    {
    }

    public DatabaseInitializer(Func<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task InitializeAsync(bool useMigrationsIfAvailable = false, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(AppDbContext.DefaultDatabasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var context = _contextFactory();

        if (useMigrationsIfAvailable && context.Database.GetMigrations().Any())
        {
            await context.Database.MigrateAsync(cancellationToken);
            return;
        }

        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        return await context.Database.CanConnectAsync(cancellationToken);
    }
}
