namespace DeverLabs.MongoMigrator;

/// <summary>
/// Configuration options for MongoMigrator.
/// </summary>
public sealed class MigrationOptions
{
    /// <summary>
    /// The MongoDB collection name used to track applied migrations.
    /// Defaults to <c>"MigrationHistory"</c>.
    /// </summary>
    public string HistoryCollectionName { get; set; } = "MigrationHistory";

    /// <summary>
    /// The name of the environment variable that, when set to <c>"1"</c>, triggers rollback mode.
    /// Defaults to <c>"DB_MIGRATION_ROLLBACK"</c>.
    /// </summary>
    public string RollbackEnvironmentVariable { get; set; } = "DB_MIGRATION_ROLLBACK";

    /// <summary>
    /// The name of the environment variable containing the version number to roll back to.
    /// Defaults to <c>"DB_MIGRATION_ROLLBACK_VERSION"</c>.
    /// </summary>
    public string RollbackVersionEnvironmentVariable { get; set; } = "DB_MIGRATION_ROLLBACK_VERSION";

    /// <summary>
    /// When <c>true</c> (the default), a hosted service is registered that automatically runs
    /// migrations on application startup before the host begins accepting traffic.
    /// <para>
    /// Set to <c>false</c> if you want to control when migrations run yourself — e.g. invoke
    /// <see cref="IMigrationRunner"/> manually, run migrations in a separate tool, or integrate
    /// with a custom deployment pipeline.
    /// </para>
    /// </summary>
    public bool RunOnStartup { get; set; } = true;
}
