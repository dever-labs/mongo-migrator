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
        services.AddMongoMigrations(m => m
            .UseDatabase(FakeDatabase())
            .ScanAssembly(typeof(ServiceCollectionExtensionsTests).Assembly));

        var provider = services.BuildServiceProvider();
        provider.GetService<IMigrationRunner>().Should().NotBeNull();
    }

    [Fact]
    public void AddMongoMigrations_WithoutAutoMigrate_DoesNotRegisterHostedService()
    {
        var services = new ServiceCollection();
        services.AddMongoMigrations(m => m
            .UseDatabase(FakeDatabase())
            .ScanAssembly(typeof(ServiceCollectionExtensionsTests).Assembly));

        services.Should().NotContain(sd => sd.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddMongoMigrations_WithAutoMigrate_RegistersHostedService()
    {
        var services = new ServiceCollection();
        services.AddMongoMigrations(m => m
            .UseDatabase(FakeDatabase())
            .ScanAssembly(typeof(ServiceCollectionExtensionsTests).Assembly)
            .AutoMigrate());

        services.Should().Contain(sd => sd.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddMongoMigrations_WithoutUseDatabase_ThrowsOnBuild()
    {
        var services = new ServiceCollection();

        var act = () => services.AddMongoMigrations(m => m
            .ScanAssembly(typeof(ServiceCollectionExtensionsTests).Assembly));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*UseDatabase*");
    }

    [Fact]
    public void AddMongoMigrations_WithoutScanAssembly_ThrowsOnBuild()
    {
        var services = new ServiceCollection();

        var act = () => services.AddMongoMigrations(m => m
            .UseDatabase(FakeDatabase()));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ScanAssembly*");
    }

    [Fact]
    public void AddMongoMigrations_WithNullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddMongoMigrations(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configure");
    }
}

