using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace DeverLabs.MongoMigrator;

/// <summary>
/// Fluent builder for configuring MongoMigrator during DI registration.
/// Obtain an instance via <see cref="ServiceCollectionExtensions.AddMongoMigrations"/>.
/// </summary>
public sealed class MongoMigratorBuilder
{
    private IMongoDatabase? _database;
    private readonly List<Assembly> _assemblies = [];
    private bool _autoMigrate;
    private string _rollbackEnvVar = "DB_MIGRATION_ROLLBACK";
    private string _rollbackVersionEnvVar = "DB_MIGRATION_ROLLBACK_VERSION";

    internal IServiceCollection Services { get; }

    internal MongoMigratorBuilder(IServiceCollection services) => Services = services;

    /// <summary>
    /// Sets the MongoDB database that migrations will operate against. <b>Required.</b>
    /// </summary>
    public MongoMigratorBuilder UseDatabase(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
        return this;
    }

    /// <summary>
    /// Adds one or more assemblies to scan for <see cref="IMigration"/> implementations.
    /// Call this at least once. Multiple calls accumulate assemblies.
    /// </summary>
    public MongoMigratorBuilder ScanAssembly(params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
            throw new ArgumentException("At least one assembly must be provided.", nameof(assemblies));

        _assemblies.AddRange(assemblies);
        return this;
    }

    /// <summary>
    /// Registers a hosted service that automatically runs pending migrations on application startup,
    /// before the host begins accepting traffic.
    /// <para>
    /// Omit this call if you prefer to invoke <see cref="IMigrationRunner"/> manually —
    /// e.g. from a CLI command, a minimal API endpoint, or a custom background job.
    /// </para>
    /// </summary>
    public MongoMigratorBuilder AutoMigrate()
    {
        _autoMigrate = true;
        return this;
    }

    /// <summary>
    /// Overrides the environment variable names used to trigger rollback mode.
    /// Defaults are <c>DB_MIGRATION_ROLLBACK</c> and <c>DB_MIGRATION_ROLLBACK_VERSION</c>.
    /// Only relevant when <see cref="AutoMigrate"/> is also called.
    /// </summary>
    public MongoMigratorBuilder WithRollbackEnvironmentVariables(
        string rollbackVariable,
        string rollbackVersionVariable)
    {
        if (string.IsNullOrWhiteSpace(rollbackVariable))
            throw new ArgumentException("Must not be null or whitespace.", nameof(rollbackVariable));
        if (string.IsNullOrWhiteSpace(rollbackVersionVariable))
            throw new ArgumentException("Must not be null or whitespace.", nameof(rollbackVersionVariable));

        _rollbackEnvVar = rollbackVariable;
        _rollbackVersionEnvVar = rollbackVersionVariable;
        return this;
    }

    internal void Build()
    {
        if (_database is null)
            throw new InvalidOperationException(
                "MongoMigratorBuilder: UseDatabase() must be called before building.");

        if (_assemblies.Count == 0)
            throw new InvalidOperationException(
                "MongoMigratorBuilder: ScanAssembly() must be called with at least one assembly.");

        Services.AddSingleton(_database);
        Services.AddSingleton(new MigrationConfiguration
        {
            RollbackEnvironmentVariable = _rollbackEnvVar,
            RollbackVersionEnvironmentVariable = _rollbackVersionEnvVar
        });

        Services.AddTransient<IMigrationRunner, MigrationRunner>();

        var migrationTypes = _assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(IMigration).IsAssignableFrom(t))
            .Distinct();

        foreach (var type in migrationTypes)
            Services.AddTransient(typeof(IMigration), type);

        if (_autoMigrate)
            Services.AddHostedService<MigrationService>();
    }
}
