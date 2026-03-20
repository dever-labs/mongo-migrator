using Microsoft.Extensions.DependencyInjection;

namespace DeverLabs.MongoMigrator;

/// <summary>
/// Extension methods for registering MongoMigrator with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers MongoMigrator using a fluent <see cref="MongoMigratorBuilder"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// // Manual control — inject IMigrationRunner yourself
    /// services.AddMongoMigrations(m => m
    ///     .UseDatabase(database)
    ///     .ScanAssembly(typeof(Program).Assembly));
    ///
    /// // Automatic — migrations run before the host accepts traffic
    /// services.AddMongoMigrations(m => m
    ///     .UseDatabase(database)
    ///     .ScanAssembly(typeof(Program).Assembly)
    ///     .AutoMigrate());
    /// </code>
    /// </example>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A callback that configures the <see cref="MongoMigratorBuilder"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddMongoMigrations(
        this IServiceCollection services,
        Action<MongoMigratorBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new MongoMigratorBuilder(services);
        configure(builder);
        builder.Build();

        return services;
    }
}

