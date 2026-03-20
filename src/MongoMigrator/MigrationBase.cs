using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace DeverLabs.MongoMigrator;

/// <summary>
/// Base class for all MongoDB migrations. Inherit from this class and implement
/// <see cref="ApplyMigrationAsync"/> and <see cref="RollbackMigrationAsync"/>.
/// </summary>
/// <remarks>
/// Migrations are idempotent: <see cref="MigrateAsync"/> is a no-op if the migration
/// has already been applied, and <see cref="RollbackAsync"/> is a no-op if it has not.
/// </remarks>
/// <example>
/// <code>
/// public sealed class AddUserIndexMigration(IMongoDatabase database, ILogger&lt;AddUserIndexMigration&gt; logger)
///     : MigrationBase(database, logger)
/// {
///     public override long Version =&gt; 20240101_120000L;
///     // MigrationName defaults to "AddUserIndexMigration" — override only if you want something else
///
///     protected override async Task ApplyMigrationAsync()
///     {
///         var collection = Database.GetCollection&lt;BsonDocument&gt;("users");
///         var indexKeys = Builders&lt;BsonDocument&gt;.IndexKeys.Ascending("email");
///         await collection.Indexes.CreateOneAsync(new CreateIndexModel&lt;BsonDocument&gt;(indexKeys));
///     }
///
///     protected override async Task RollbackMigrationAsync()
///     {
///         var collection = Database.GetCollection&lt;BsonDocument&gt;("users");
///         await collection.Indexes.DropOneAsync("email_1");
///     }
/// }
/// </code>
/// </example>
public abstract class MigrationBase : IMigration
{
    private readonly IMongoCollection<MigrationHistory> _historyCollection;

    /// <summary>Initialises a new migration instance.</summary>
    /// <param name="database">The MongoDB database to operate on.</param>
    /// <param name="logger">Logger for migration lifecycle events.</param>
    /// <param name="historyCollectionName">
    /// Name of the collection used to track applied migrations.
    /// Defaults to <c>"MigrationHistory"</c> when not supplied.
    /// </param>
    protected MigrationBase(IMongoDatabase database, ILogger logger, string historyCollectionName = "MigrationHistory")
    {
        Database = database;
        Logger = logger;
        _historyCollection = database.GetCollection<MigrationHistory>(historyCollectionName);
    }

    /// <inheritdoc/>
    public abstract long Version { get; }

    /// <summary>
    /// A human-readable name for this migration, used in logs and history.
    /// Defaults to the concrete class name. Override to provide a custom name.
    /// </summary>
    protected virtual string MigrationName => GetType().Name;

    /// <summary>The MongoDB database instance available to subclasses.</summary>
    protected IMongoDatabase Database { get; }

    /// <summary>Logger instance available to subclasses.</summary>
    protected ILogger Logger { get; }

    /// <inheritdoc/>
    public async Task MigrateAsync()
    {
        if (await IsMigrationAppliedAsync())
        {
            Logger.LogInformation("Migration '{MigrationName}' (v{Version}) already applied — skipping", MigrationName, Version);
            return;
        }

        Logger.LogInformation("Applying migration '{MigrationName}' (v{Version})…", MigrationName, Version);
        await ApplyMigrationAsync();
        await RecordMigrationAsync();
        Logger.LogInformation("Migration '{MigrationName}' (v{Version}) applied successfully", MigrationName, Version);
    }

    /// <inheritdoc/>
    public async Task RollbackAsync()
    {
        if (!await IsMigrationAppliedAsync())
        {
            Logger.LogInformation("Migration '{MigrationName}' (v{Version}) not applied — rollback skipped", MigrationName, Version);
            return;
        }

        Logger.LogInformation("Rolling back migration '{MigrationName}' (v{Version})…", MigrationName, Version);
        await RollbackMigrationAsync();
        await RemoveMigrationRecordAsync();
        Logger.LogInformation("Migration '{MigrationName}' (v{Version}) rolled back successfully", MigrationName, Version);
    }

    /// <summary>
    /// Implement the forward migration logic here.
    /// This method is only called when the migration has not yet been applied.
    /// </summary>
    protected abstract Task ApplyMigrationAsync();

    /// <summary>
    /// Implement the rollback logic here.
    /// This method is only called when the migration has previously been applied.
    /// </summary>
    protected abstract Task RollbackMigrationAsync();

    /// <summary>
    /// Checks whether a collection with the given <paramref name="collectionName"/> already exists.
    /// </summary>
    protected async Task<bool> CollectionExistsAsync(string collectionName)
    {
        using var cursor = await Database.ListCollectionNamesAsync();
        var names = await cursor.ToListAsync();
        return names.Contains(collectionName);
    }

    private async Task<bool> IsMigrationAppliedAsync() =>
        await _historyCollection
            .Find(Builders<MigrationHistory>.Filter.Eq(m => m.Version, Version))
            .AnyAsync();

    private Task RecordMigrationAsync() =>
        _historyCollection.InsertOneAsync(new MigrationHistory
        {
            Version = Version,
            MigrationName = MigrationName,
            AppliedAt = DateTimeOffset.UtcNow
        });

    private Task RemoveMigrationRecordAsync() =>
        _historyCollection.DeleteOneAsync(
            Builders<MigrationHistory>.Filter.Eq(m => m.Version, Version));
}
