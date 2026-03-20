using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeverLabs.MongoMigrator;

/// <summary>
/// Hosted service that runs migrations (or a rollback) when the application starts.
/// The host will not accept traffic until migrations complete.
/// </summary>
/// <remarks>
/// <para>To trigger <b>rollback mode</b> set these environment variables before starting:</para>
/// <list type="bullet">
///   <item><c>DB_MIGRATION_ROLLBACK=1</c></item>
///   <item><c>DB_MIGRATION_ROLLBACK_VERSION=&lt;version&gt;</c></item>
/// </list>
/// The environment variable names can be customised via <see cref="MigrationOptions"/>.
/// </remarks>
internal sealed class MigrationService(
    IMigrationRunner migrationRunner,
    IOptions<MigrationOptions> options,
    ILogger<MigrationService> logger) : IHostedService
{
    private readonly MigrationOptions _options = options.Value;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var rollbackFlag = Environment.GetEnvironmentVariable(_options.RollbackEnvironmentVariable);
        var rollbackVersionRaw = Environment.GetEnvironmentVariable(_options.RollbackVersionEnvironmentVariable);

        if (rollbackFlag == "1" && long.TryParse(rollbackVersionRaw, out var rollbackVersion))
        {
            logger.LogInformation(
                "Rollback mode active — rolling back to version {Version}", rollbackVersion);
            return migrationRunner.RollbackAsync(rollbackVersion, cancellationToken);
        }

        return migrationRunner.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
