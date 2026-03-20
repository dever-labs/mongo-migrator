namespace DeverLabs.MongoMigrator;

/// <summary>
/// Represents a single versioned database migration.
/// </summary>
public interface IMigration
{
    /// <summary>
    /// Gets the unique version number for this migration.
    /// Use a timestamp-based long (e.g. <c>20240101_120000L</c>) to guarantee ordering.
    /// </summary>
    long Version { get; }

    /// <summary>Applies the migration forward.</summary>
    Task MigrateAsync();

    /// <summary>Reverts this migration.</summary>
    Task RollbackAsync();
}
