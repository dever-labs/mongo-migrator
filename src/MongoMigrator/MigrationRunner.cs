using Microsoft.Extensions.Logging;

namespace DeverLabs.MongoMigrator;

/// <summary>
/// Orchestrates execution of all registered <see cref="IMigration"/> instances.
/// </summary>
internal sealed class MigrationRunner(
    IEnumerable<IMigration> migrations,
    ILogger<MigrationRunner> logger) : IMigrationRunner
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var ordered = migrations.OrderBy(m => m.Version).ToList();

        logger.LogInformation("Starting migrations — {Count} migration(s) registered", ordered.Count);

        try
        {
            foreach (var migration in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await migration.MigrateAsync();
            }

            logger.LogInformation("All migrations completed successfully");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Migrations cancelled");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Migration failed");
            throw;
        }
    }

    public async Task RollbackAsync(long version, CancellationToken cancellationToken = default)
    {
        var target = migrations.FirstOrDefault(m => m.Version == version);

        if (target is null)
        {
            logger.LogWarning("No migration found for version {Version} — rollback skipped", version);
            return;
        }

        logger.LogInformation("Starting rollback for version {Version}", version);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await target.RollbackAsync();
            logger.LogInformation("Rollback for version {Version} completed successfully", version);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Rollback cancelled");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rollback for version {Version} failed", version);
            throw;
        }
    }
}
