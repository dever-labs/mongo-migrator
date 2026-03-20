using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DeverLabs.MongoMigrator;

/// <summary>
/// Represents a record of an applied migration stored in MongoDB.
/// </summary>
public sealed class MigrationHistory
{
    /// <summary>MongoDB document identifier.</summary>
    [BsonId]
    public ObjectId Id { get; set; }

    /// <summary>The version number of the applied migration.</summary>
    public required long Version { get; set; }

    /// <summary>The name of the migration class.</summary>
    public required string MigrationName { get; set; }

    /// <summary>UTC timestamp at which the migration was applied.</summary>
    public required DateTimeOffset AppliedAt { get; set; }
}
