using DeverLabs.MongoMigrator;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace MongoMigrator.Tests;

public sealed class MigrationServiceTests
{
    private static MigrationService CreateService(
        IMigrationRunner runner,
        Action<MigrationOptions>? configure = null)
    {
        var options = new MigrationOptions();
        configure?.Invoke(options);
        return new MigrationService(runner, Options.Create(options), NullLogger<MigrationService>.Instance);
    }

    [Fact]
    public async Task StartAsync_ByDefault_CallsMigrateAsync()
    {
        var runner = Substitute.For<IMigrationRunner>();
        var service = CreateService(runner);

        await service.StartAsync(CancellationToken.None);

        await runner.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
        await runner.DidNotReceive().RollbackAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenRollbackEnvVarSetTo1AndVersionProvided_CallsRollbackAsync()
    {
        const long targetVersion = 20240101_120000L;
        Environment.SetEnvironmentVariable("DB_MIGRATION_ROLLBACK", "1");
        Environment.SetEnvironmentVariable("DB_MIGRATION_ROLLBACK_VERSION", targetVersion.ToString());

        try
        {
            var runner = Substitute.For<IMigrationRunner>();
            var service = CreateService(runner);

            await service.StartAsync(CancellationToken.None);

            await runner.Received(1).RollbackAsync(targetVersion, Arg.Any<CancellationToken>());
            await runner.DidNotReceive().MigrateAsync(Arg.Any<CancellationToken>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DB_MIGRATION_ROLLBACK", null);
            Environment.SetEnvironmentVariable("DB_MIGRATION_ROLLBACK_VERSION", null);
        }
    }

    [Fact]
    public async Task StartAsync_WhenRollbackEnvVarSetButVersionMissing_CallsMigrateAsync()
    {
        Environment.SetEnvironmentVariable("DB_MIGRATION_ROLLBACK", "1");
        Environment.SetEnvironmentVariable("DB_MIGRATION_ROLLBACK_VERSION", null);

        try
        {
            var runner = Substitute.For<IMigrationRunner>();
            var service = CreateService(runner);

            await service.StartAsync(CancellationToken.None);

            await runner.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DB_MIGRATION_ROLLBACK", null);
        }
    }

    [Fact]
    public async Task StartAsync_WhenCustomEnvVarNamesConfigured_RespectsCustomNames()
    {
        const long targetVersion = 42L;
        Environment.SetEnvironmentVariable("CUSTOM_ROLLBACK", "1");
        Environment.SetEnvironmentVariable("CUSTOM_ROLLBACK_VERSION", targetVersion.ToString());

        try
        {
            var runner = Substitute.For<IMigrationRunner>();
            var service = CreateService(runner, opts =>
            {
                opts.RollbackEnvironmentVariable = "CUSTOM_ROLLBACK";
                opts.RollbackVersionEnvironmentVariable = "CUSTOM_ROLLBACK_VERSION";
            });

            await service.StartAsync(CancellationToken.None);

            await runner.Received(1).RollbackAsync(targetVersion, Arg.Any<CancellationToken>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("CUSTOM_ROLLBACK", null);
            Environment.SetEnvironmentVariable("CUSTOM_ROLLBACK_VERSION", null);
        }
    }

    [Fact]
    public async Task StopAsync_CompletesWithoutError()
    {
        var runner = Substitute.For<IMigrationRunner>();
        var service = CreateService(runner);

        await service.Invoking(s => s.StopAsync(CancellationToken.None))
            .Should().NotThrowAsync();
    }
}
