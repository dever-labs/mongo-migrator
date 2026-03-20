using DeverLabs.MongoMigrator;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using NSubstitute;

namespace MongoMigrator.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    private static IMongoDatabase FakeDatabase() => Substitute.For<IMongoDatabase>();

    [Fact]
    public void AddMongoMigrations_RegistersMigrationRunner()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMongoMigrations(FakeDatabase(), typeof(ServiceCollectionExtensionsTests).Assembly);

        var provider = services.BuildServiceProvider();
        provider.GetService<IMigrationRunner>().Should().NotBeNull();
    }

    [Fact]
    public void AddMongoMigrations_ByDefault_RegistersHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMongoMigrations(FakeDatabase(), typeof(ServiceCollectionExtensionsTests).Assembly);

        services.Should().Contain(sd => sd.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddMongoMigrations_WhenRunOnStartupFalse_DoesNotRegisterHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMongoMigrations(
            FakeDatabase(),
            options => options.RunOnStartup = false,
            typeof(ServiceCollectionExtensionsTests).Assembly);

        services.Should().NotContain(sd => sd.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddMongoMigrations_WithNoAssemblies_ThrowsArgumentException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddMongoMigrations(FakeDatabase());

        act.Should().Throw<ArgumentException>()
            .WithParameterName("assemblies");
    }

    [Fact]
    public void AddMongoMigrations_WithNullDatabase_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddMongoMigrations(null!, typeof(ServiceCollectionExtensionsTests).Assembly);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("database");
    }
}
