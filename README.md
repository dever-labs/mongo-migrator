# DeverLabs.MongoMigrator

[![NuGet](https://img.shields.io/nuget/v/DeverLabs.MongoMigrator.svg)](https://www.nuget.org/packages/DeverLabs.MongoMigrator)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A simple, production-ready MongoDB migration library for .NET. Manage database schema changes with versioned, idempotent, rollback-capable migrations that run automatically on application startup.

## Features

- ✅ **Idempotent** — already-applied migrations are skipped automatically
- ✅ **Ordered** — migrations run in ascending version order every time
- ✅ **Rollback support** — revert a specific version via environment variable
- ✅ **Startup-safe** — migrations complete before the host starts accepting traffic (opt-in)
- ✅ **Manual control** — run migrations yourself via `IMigrationRunner` when you don't want auto-migrate
- ✅ **Auto-discovery** — all `IMigration` implementations in your assemblies are found automatically
- ✅ **Minimal boilerplate** — one `AddMongoMigrations()` call wires everything up

## Supported frameworks

| .NET | Status |
|------|--------|
| .NET 8 | ✅ LTS |
| .NET 9 | ✅ |
| .NET 10 | ✅ |
| .NET 11 | ✅ Preview |

Dependencies are scoped to a compatible version range per framework — they will never conflict with the versions your application already uses.

## Installation

```bash
dotnet add package DeverLabs.MongoMigrator
```

## Quick Start

### 1. Register in `Program.cs`

```csharp
// Simplest — pass connection string and database name directly
builder.Services.AddMongoMigrations(m => m
    .UseDatabase("mongodb://localhost:27017", "mydb")
    .ScanAssembly(typeof(Program).Assembly)
    .AutoMigrate());

// From configuration
builder.Services.AddMongoMigrations(m => m
    .UseDatabase(builder.Configuration.GetConnectionString("Mongo")!, "mydb")
    .ScanAssembly(typeof(Program).Assembly)
    .AutoMigrate());

// Or bring your own IMongoDatabase if you already have one
builder.Services.AddMongoMigrations(m => m
    .UseDatabase(existingMongoDatabase)
    .ScanAssembly(typeof(Program).Assembly)
    .AutoMigrate());
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
    // MigrationName defaults to the class name — override only if you want something else

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

To roll back a specific migration, set these environment variables **before** starting the application:

```
DB_MIGRATION_ROLLBACK=1
DB_MIGRATION_ROLLBACK_VERSION=20240101_120000
```

The application will call `RollbackMigrationAsync()` on the matching migration and remove it from `MigrationHistory`, then exit normally.

## Manual Migration Control

Omit `.AutoMigrate()` and inject `IMigrationRunner` wherever you need it — useful for CLI tools, admin endpoints, or custom deployment pipelines:

```csharp
builder.Services.AddMongoMigrations(m => m
    .UseDatabase("mongodb://localhost:27017", "mydb")
    .ScanAssembly(typeof(Program).Assembly));
// No .AutoMigrate() — you drive it
```

```csharp
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
builder.Services.AddMongoMigrations(m => m
    .UseDatabase("mongodb://localhost:27017", "mydb")
    .ScanAssembly(typeof(Program).Assembly)
    .ScanAssembly(typeof(SomeOtherMarker).Assembly)            // scan multiple assemblies
    .WithRollbackEnvironmentVariables(                         // custom env var names
        "MY_APP_ROLLBACK",
        "MY_APP_ROLLBACK_VERSION")
    .AutoMigrate());
```

## Migration Versioning Convention

Use a `long` that encodes a timestamp — naturally sortable and collision-free:

```csharp
// Format: YYYYMMdd_HHmmss → 20240115_093000L
public override long Version => 20240115_093000L;
```

Alternatively, use a Unix timestamp:

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

### Create a collection with an index

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
| `AddMongoMigrations(Action<MongoMigratorBuilder>)` | Single entry point — configure everything via the fluent builder. |

### `MongoMigratorBuilder`

| Method | Description |
|--------|-------------|
| `UseDatabase(connectionString, databaseName)` | **Required.** Creates a `MongoClient` internally — simplest setup. |
| `UseDatabase(settings, databaseName)` | **Required.** Uses `MongoClientSettings` for advanced client configuration. |
| `UseDatabase(IMongoDatabase)` | **Required.** Bring your own `IMongoDatabase` instance. |
| `ScanAssembly(params assemblies)` | **Required.** Assemblies to scan for `IMigration` implementations. Chainable. |
| `AutoMigrate()` | Registers a hosted service that runs migrations before the host accepts traffic. Omit for manual control. |
| `WithRollbackEnvironmentVariables(rollbackVar, versionVar)` | Override the env var names used in rollback mode (defaults: `DB_MIGRATION_ROLLBACK`, `DB_MIGRATION_ROLLBACK_VERSION`). |

### `MigrationBase`

| Member | Description |
|--------|-------------|
| `Version` | Abstract. Unique version number — determines execution order. |
| `MigrationName` | Virtual. Defaults to the concrete class name. Override to use a custom name. |
| `ApplyMigrationAsync()` | Abstract. Forward migration logic. |
| `RollbackMigrationAsync()` | Abstract. Reverse migration logic. |
| `Database` | The `IMongoDatabase` passed at construction. |
| `Logger` | The `ILogger` passed at construction. |
| `CollectionExistsAsync(name)` | Helper — returns `true` if the collection already exists. |

> **Custom history collection name:** Pass a third argument to the base constructor:
> ```csharp
> : MigrationBase(database, logger, "MyMigrationHistory")
> ```
> Or create a shared abstract base in your project that sets it for all your migrations.

## License

MIT — see [LICENSE](LICENSE).