using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeverLabs.MongoMigrator;

/// <summary>
/// Hosted service that runs migrations (or a rollback) when the application starts.
/// The host will not accept traffic until migrations complete.
/// Registered automatically when <see cref="MongoMigratorBuilder.AutoMigrate"/> is called.
/// </summary>
/// <remarks>
/// <para>To trigger <b>rollback mode</b> set these environment variables before starting:</para>
/// <list type="bullet">
///   <item><c>DB_MIGRATION_ROLLBACK=1</c></item>
///   <item><c>DB_MIGRATION_ROLLBACK_VERSION=&lt;version&gt;</c></item>
/// </list>
/// The variable names can be overridden via
/// <see cref="MongoMigratorBuilder.WithRollbackEnvironmentVariables"/>.
/// </remarks>
internal sealed class MigrationService(
    IMigrationRunner migrationRunner,
    MigrationConfiguration configuration,
    ILogger<MigrationService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var rollbackFlag = Environment.GetEnvironmentVariable(configuration.RollbackEnvironmentVariable);
        var rollbackVersionRaw = Environment.GetEnvironmentVariable(configuration.RollbackVersionEnvironmentVariable);

        if (rollbackFlag == "1" && long.TryParse(rollbackVersionRaw, out var rollbackVersion))
        {
            logger.LogInformation("Rollback mode active — rolling back to version {Version}", rollbackVersion);
            return migrationRunner.RollbackAsync(rollbackVersion, cancellationToken);
        }

        return migrationRunner.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

