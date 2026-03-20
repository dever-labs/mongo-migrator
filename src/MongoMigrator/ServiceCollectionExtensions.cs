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
    /// builder.Services.AddMongoMigrations(m => m
    ///     .UseDatabase("mongodb://localhost:27017/mydb", "mydb")
    ///     .ScanAssembly(typeof(Program).Assembly)
    ///     .AutoMigrate());
    /// </code>
    /// </example>
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