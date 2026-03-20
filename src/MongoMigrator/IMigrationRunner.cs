namespace DeverLabs.MongoMigrator;

/// <summary>
/// Orchestrates the execution of registered migrations.
/// </summary>
public interface IMigrationRunner
{
    /// <summary>
    /// Applies all pending migrations in ascending version order.
    /// Already-applied migrations are skipped idempotently.
    /// </summary>
    Task MigrateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the migration with the specified <paramref name="version"/>.
    /// Does nothing if the version is not found or has not been applied.
    /// </summary>
    Task RollbackAsync(long version, CancellationToken cancellationToken = default);
}
