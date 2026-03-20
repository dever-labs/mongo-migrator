# DeverLabs.MongoMigrator

[![NuGet](https://img.shields.io/nuget/v/DeverLabs.MongoMigrator.svg)](https://www.nuget.org/packages/DeverLabs.MongoMigrator)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A simple, production-ready MongoDB migration library for .NET 8+. Manage database schema changes with versioned, idempotent, rollback-capable migrations that run automatically on application startup.

## Features

- ✅ **Idempotent** — already-applied migrations are skipped automatically
- ✅ **Ordered** — migrations run in ascending version order every time
- ✅ **Rollback support** — revert a specific version via environment variable
- ✅ **Startup-safe** — migrations complete before the host starts accepting traffic
- ✅ **Auto-discovery** — all `IMigration` implementations in your assemblies are found automatically
- ✅ **Minimal boilerplate** — one `AddMongoMigrations()` call wires everything up

## Installation

```bash
dotnet add package DeverLabs.MongoMigrator
```

## Quick Start

### 1. Register in `Program.cs`

```csharp
var mongoClient = new MongoClient(connectionString);
var database = mongoClient.GetDatabase("mydb");

builder.Services.AddMongoMigrations(database, typeof(Program).Assembly);
```

### 2. Write a migration

```csharp
public sealed class AddUserEmailIndexMigration(
    IMongoDatabase database,
    ILogger<AddUserEmailIndexMigration> logger)
    : MigrationBase(database, logger)
{
    // Use a sortable long — a compact timestamp works well: YYYYMMdd_HHmmss
    public override long Version => 20240101_120000L;
    protected override string MigrationName => nameof(AddUserEmailIndexMigration);

    protected override async Task ApplyMigrationAsync()
    {
        var collection = Database.GetCollection<BsonDocument>("users");
        var key = Builders<BsonDocument>.IndexKeys.Ascending("email");
        var model = new CreateIndexModel<BsonDocument>(key, new() { Unique = true });
        await collection.Indexes.CreateOneAsync(model);
    }

    protected override async Task RollbackMigrationAsync()
    {
        var collection = Database.GetCollection<BsonDocument>("users");
        await collection.Indexes.DropOneAsync("email_1");
    }
}
```

That's it — the migration runs automatically when your application starts.

## How It Works

```
Application starts
      │
      ▼
MigrationService.StartAsync()          ← blocks host startup until done
      │
      ▼
MigrationRunner orders by Version ──► for each migration:
                                         • Already in MigrationHistory?  → skip
                                         • Not applied?                  → ApplyMigrationAsync()
                                                                           RecordMigrationAsync()
      │
      ▼
Host starts accepting traffic ✓
```

Applied migrations are recorded in a `MigrationHistory` collection in your database.

## Rollback

To roll back a specific migration set these environment variables **before** starting the application:

```
DB_MIGRATION_ROLLBACK=1
DB_MIGRATION_ROLLBACK_VERSION=20240101_120000
```

The application will call `RollbackMigrationAsync()` on the matching migration and remove it from `MigrationHistory`, then exit normally.

## Manual Migration Control

Set `RunOnStartup = false` to skip the hosted service and drive migrations yourself via `IMigrationRunner`:

```csharp
builder.Services.AddMongoMigrations(
    database,
    options => options.RunOnStartup = false,
    typeof(Program).Assembly);
```

Then inject `IMigrationRunner` wherever you need it:

```csharp
// e.g. a minimal API endpoint, a CLI command, or a custom background job
app.MapPost("/admin/migrate", async (IMigrationRunner runner) =>
{
    await runner.MigrateAsync();
    return Results.Ok();
});

app.MapPost("/admin/rollback/{version:long}", async (long version, IMigrationRunner runner) =>
{
    await runner.RollbackAsync(version);
    return Results.Ok();
});
```

## Advanced Configuration

```csharp
builder.Services.AddMongoMigrations(
    database,
    options =>
    {
        // Custom collection name for migration history
        options.HistoryCollectionName = "MyMigrationHistory";

        // Custom environment variable names for rollback control
        options.RollbackEnvironmentVariable = "MY_APP_ROLLBACK";
        options.RollbackVersionEnvironmentVariable = "MY_APP_ROLLBACK_VERSION";
    },
    typeof(Program).Assembly,
    typeof(SomeOtherMarker).Assembly   // scan multiple assemblies
);
```

## Migration Versioning Convention

Use a long that encodes a timestamp so versions are naturally sortable and unique:

```csharp
// Format: YYYYMMdd_HHmmss → 20240115_093000L
public override long Version => 20240115_093000L;
```

Alternatively, use Unix timestamps:

```csharp
// DateTimeOffset.UtcNow.ToUnixTimeSeconds() at time of writing
public override long Version => 1705312200L;
```

## Example Migration Patterns

### Add a field with a default value

```csharp
protected override async Task ApplyMigrationAsync()
{
    var collection = Database.GetCollection<BsonDocument>("users");
    var filter = Builders<BsonDocument>.Filter.Exists("isActive", false);
    var update = Builders<BsonDocument>.Update.Set("isActive", true);
    await collection.UpdateManyAsync(filter, update);
}

protected override async Task RollbackMigrationAsync()
{
    var collection = Database.GetCollection<BsonDocument>("users");
    var update = Builders<BsonDocument>.Update.Unset("isActive");
    await collection.UpdateManyAsync(FilterDefinition<BsonDocument>.Empty, update);
}
```

### Rename a field

```csharp
protected override async Task ApplyMigrationAsync()
{
    var collection = Database.GetCollection<BsonDocument>("users");
    var update = Builders<BsonDocument>.Update.Rename("name", "fullName");
    await collection.UpdateManyAsync(FilterDefinition<BsonDocument>.Empty, update);
}

protected override async Task RollbackMigrationAsync()
{
    var collection = Database.GetCollection<BsonDocument>("users");
    var update = Builders<BsonDocument>.Update.Rename("fullName", "name");
    await collection.UpdateManyAsync(FilterDefinition<BsonDocument>.Empty, update);
}
```

### Create a new collection with an index

```csharp
protected override async Task ApplyMigrationAsync()
{
    if (await CollectionExistsAsync("audit_logs"))
        return;

    await Database.CreateCollectionAsync("audit_logs");
    var collection = Database.GetCollection<BsonDocument>("audit_logs");
    var key = Builders<BsonDocument>.IndexKeys.Descending("createdAt");
    await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(key));
}

protected override async Task RollbackMigrationAsync()
{
    if (await CollectionExistsAsync("audit_logs"))
        await Database.DropCollectionAsync("audit_logs");
}
```

## API Reference

### `ServiceCollectionExtensions`

| Method | Description |
|--------|-------------|
| `AddMongoMigrations(database, params assemblies)` | Register migrations, scanning the specified assemblies. |
| `AddMongoMigrations(database, configure, params assemblies)` | Register with custom `MigrationOptions`. |

### `MigrationBase`

| Member | Description |
|--------|-------------|
| `Version` | Abstract. Unique version number — determines execution order. |
| `MigrationName` | Abstract. Human-readable name used in logs and history. |
| `ApplyMigrationAsync()` | Abstract. Forward migration logic. |
| `RollbackMigrationAsync()` | Abstract. Reverse migration logic. |
| `Database` | The `IMongoDatabase` passed at construction. |
| `Logger` | The `ILogger` passed at construction. |
| `CollectionExistsAsync(name)` | Helper — returns `true` if the collection already exists. |

### `MigrationOptions`

| Property | Default | Description |
|----------|---------|-------------|
| `HistoryCollectionName` | `"MigrationHistory"` | MongoDB collection for tracking applied migrations. |
| `RollbackEnvironmentVariable` | `"DB_MIGRATION_ROLLBACK"` | Env var that activates rollback mode when set to `"1"`. |
| `RollbackVersionEnvironmentVariable` | `"DB_MIGRATION_ROLLBACK_VERSION"` | Env var containing the version to roll back. |
| `RunOnStartup` | `true` | When `false`, no hosted service is registered. Use `IMigrationRunner` directly. |

## License

MIT — see [LICENSE](LICENSE).
