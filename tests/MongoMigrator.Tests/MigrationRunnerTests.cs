using DeverLabs.MongoMigrator;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MongoMigrator.Tests;

public sealed class MigrationRunnerTests
{
    private static IMigration MockMigration(long version)
    {
        var m = Substitute.For<IMigration>();
        m.Version.Returns(version);
        m.MigrateAsync().Returns(Task.CompletedTask);
        m.RollbackAsync().Returns(Task.CompletedTask);
        return m;
    }

    [Fact]
    public async Task MigrateAsync_RunsMigrationsInAscendingVersionOrder()
    {
        var callOrder = new List<long>();

        var m1 = MockMigration(300);
        var m2 = MockMigration(100);
        var m3 = MockMigration(200);

        m1.MigrateAsync().Returns(_ => { callOrder.Add(300); return Task.CompletedTask; });
        m2.MigrateAsync().Returns(_ => { callOrder.Add(100); return Task.CompletedTask; });
        m3.MigrateAsync().Returns(_ => { callOrder.Add(200); return Task.CompletedTask; });

        var runner = new TestMigrationRunner([m1, m2, m3]);

        await runner.MigrateAsync();

        callOrder.Should().ContainInOrder(100L, 200L, 300L);
    }

    [Fact]
    public async Task MigrateAsync_CallsMigrateOnAllRegisteredMigrations()
    {
        var m1 = MockMigration(1);
        var m2 = MockMigration(2);

        var runner = new TestMigrationRunner([m1, m2]);
        await runner.MigrateAsync();

        await m1.Received(1).MigrateAsync();
        await m2.Received(1).MigrateAsync();
    }

    [Fact]
    public async Task MigrateAsync_WhenCancelled_ThrowsAndStopsProcessing()
    {
        using var cts = new CancellationTokenSource();
        var m1 = MockMigration(1);
        var m2 = MockMigration(2);

        m1.MigrateAsync().Returns(async _ =>
        {
            await cts.CancelAsync();
        });

        var runner = new TestMigrationRunner([m1, m2]);

        await runner.Invoking(r => r.MigrateAsync(cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();

        await m2.DidNotReceive().MigrateAsync();
    }

    [Fact]
    public async Task MigrateAsync_WhenMigrationThrows_PropagatesException()
    {
        var m1 = MockMigration(1);
        m1.MigrateAsync().ThrowsAsync(new InvalidOperationException("DB error"));

        var runner = new TestMigrationRunner([m1]);

        await runner.Invoking(r => r.MigrateAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("DB error");
    }

    [Fact]
    public async Task RollbackAsync_CallsRollbackOnCorrectVersion()
    {
        var m1 = MockMigration(1);
        var m2 = MockMigration(2);

        var runner = new TestMigrationRunner([m1, m2]);
        await runner.RollbackAsync(version: 2);

        await m1.DidNotReceive().RollbackAsync();
        await m2.Received(1).RollbackAsync();
    }

    [Fact]
    public async Task RollbackAsync_WhenVersionNotFound_DoesNotCallAnyRollback()
    {
        var m1 = MockMigration(1);
        var runner = new TestMigrationRunner([m1]);

        await runner.RollbackAsync(version: 999);

        await m1.DidNotReceive().RollbackAsync();
    }

    [Fact]
    public async Task RollbackAsync_WhenCancelled_ThrowsOperationCancelledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var m1 = MockMigration(1);
        var runner = new TestMigrationRunner([m1]);

        await runner.Invoking(r => r.RollbackAsync(1, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task MigrateAsync_WithNoMigrations_CompletesWithoutError()
    {
        var runner = new TestMigrationRunner([]);
        await runner.Invoking(r => r.MigrateAsync())
            .Should().NotThrowAsync();
    }

    /// <summary>Thin subclass that exposes the internal MigrationRunner via the public interface.</summary>
    private sealed class TestMigrationRunner(IEnumerable<IMigration> migrations) : IMigrationRunner
    {
        private readonly DeverLabs.MongoMigrator.MigrationRunner _inner =
            new(migrations, NullLogger<DeverLabs.MongoMigrator.MigrationRunner>.Instance);

        public Task MigrateAsync(CancellationToken cancellationToken = default) =>
            _inner.MigrateAsync(cancellationToken);

        public Task RollbackAsync(long version, CancellationToken cancellationToken = default) =>
            _inner.RollbackAsync(version, cancellationToken);
    }
}
