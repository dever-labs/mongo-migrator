using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace DeverLabs.MongoMigrator;

/// <summary>
/// Extension methods for registering MongoMigrator with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers MongoMigrator and discovers all <see cref="IMigration"/> implementations
    /// in the provided <paramref name="assemblies"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="database">The MongoDB database to run migrations against.</param>
    /// <param name="assemblies">
    /// One or more assemblies to scan for concrete <see cref="IMigration"/> implementations.
    /// Typically <c>typeof(Program).Assembly</c>.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddMongoMigrations(
        this IServiceCollection services,
        IMongoDatabase database,
        params Assembly[] assemblies) =>
        services.AddMongoMigrations(database, _ => { }, assemblies);

    /// <summary>
    /// Registers MongoMigrator with custom options and discovers all <see cref="IMigration"/>
    /// implementations in the provided <paramref name="assemblies"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="database">The MongoDB database to run migrations against.</param>
    /// <param name="configure">A callback to customise <see cref="MigrationOptions"/>.</param>
    /// <param name="assemblies">
    /// One or more assemblies to scan for concrete <see cref="IMigration"/> implementations.
    /// Typically <c>typeof(Program).Assembly</c>.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddMongoMigrations(
        this IServiceCollection services,
        IMongoDatabase database,
        Action<MigrationOptions> configure,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(configure);

        if (assemblies.Length == 0)
            throw new ArgumentException("At least one assembly must be provided for migration discovery.", nameof(assemblies));

        services.Configure<MigrationOptions>(configure);

        services.AddSingleton(database);
        services.AddTransient<IMigrationRunner, MigrationRunner>();

        var migrationTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(IMigration).IsAssignableFrom(t))
            .Distinct();

        foreach (var type in migrationTypes)
            services.AddTransient(typeof(IMigration), type);

        services.AddHostedService<MigrationService>();

        return services;
    }
}
