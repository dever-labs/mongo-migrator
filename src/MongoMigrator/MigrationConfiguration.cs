namespace DeverLabs.MongoMigrator;

/// <summary>Internal configuration captured at registration time — no IOptions indirection.</summary>
internal sealed class MigrationConfiguration
{
    public required string RollbackEnvironmentVariable { get; init; }
    public required string RollbackVersionEnvironmentVariable { get; init; }
}
