using Microsoft.Extensions.Logging;
using Npgsql;
using ThriveWellness.Services;

namespace ThriveWellness.Tests;

public class StartupSeedingTests
{
    // Records every log call so the tests can check what was (and wasn't)
    // written, without depending on a real logging provider.
    private class ListLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    private readonly ListLogger _logger = new();

    [Fact]
    public async Task RunWithRetryAsync_SeedSucceeds_RunsOnceAndLogsNothing()
    {
        var calls = 0;

        var result = await StartupSeeding.RunWithRetryAsync(() =>
        {
            calls++;
            return Task.CompletedTask;
        }, _logger, delayBetweenAttempts: TimeSpan.Zero);

        Assert.True(result);
        Assert.Equal(1, calls);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task RunWithRetryAsync_DatabaseError_IsCaughtAndLoggedAsAWarning()
    {
        var result = await StartupSeeding.RunWithRetryAsync(
            () => throw new NpgsqlException("Failed to connect to 127.0.0.1:5433"),
            _logger, delayBetweenAttempts: TimeSpan.Zero);

        Assert.False(result);
        var first = _logger.Entries.First();
        Assert.Equal(LogLevel.Warning, first.Level);
        Assert.Contains("NpgsqlException", first.Message);
        Assert.Contains("Failed to connect to 127.0.0.1:5433", first.Message);
        Assert.All(_logger.Entries, e => Assert.Equal(LogLevel.Warning, e.Level));
    }

    [Fact]
    public async Task RunWithRetryAsync_DatabaseKeepsFailing_StopsAfterFiveAttempts()
    {
        var calls = 0;

        var result = await StartupSeeding.RunWithRetryAsync(() =>
        {
            calls++;
            throw new NpgsqlException("Connection refused");
        }, _logger, delayBetweenAttempts: TimeSpan.Zero);

        Assert.False(result);
        Assert.Equal(5, calls);
        Assert.Equal(StartupSeeding.MaxAttempts, calls);
        // One warning per failed attempt, plus the final "gave up" one.
        Assert.Equal(6, _logger.Entries.Count);
    }

    [Fact]
    public async Task RunWithRetryAsync_DatabaseRecovers_StopsRetryingOnceItSucceeds()
    {
        var calls = 0;

        var result = await StartupSeeding.RunWithRetryAsync(() =>
        {
            calls++;
            if (calls < 3)
            {
                throw new TimeoutException("Timeout during connection attempt");
            }
            return Task.CompletedTask;
        }, _logger, delayBetweenAttempts: TimeSpan.Zero);

        Assert.True(result);
        Assert.Equal(3, calls);
        Assert.Equal(2, _logger.Entries.Count);
    }

    [Fact]
    public async Task RunWithRetryAsync_DatabaseErrorWrappedByExecutionStrategy_IsStillCaught()
    {
        // The shape Npgsql's default execution strategy actually throws when
        // the server refuses the connection.
        var wrapped = new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure.",
            new NpgsqlException("Failed to connect to 127.0.0.1:5433"));

        var result = await StartupSeeding.RunWithRetryAsync(
            () => throw wrapped, _logger, delayBetweenAttempts: TimeSpan.Zero);

        Assert.False(result);
        Assert.Contains("NpgsqlException: Failed to connect to 127.0.0.1:5433", _logger.Entries.First().Message);
    }

    [Fact]
    public async Task RunWithRetryAsync_NonDatabaseError_StillPropagates()
    {
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StartupSeeding.RunWithRetryAsync(() =>
            {
                calls++;
                throw new InvalidOperationException("A real bug, not an outage");
            }, _logger, delayBetweenAttempts: TimeSpan.Zero));

        Assert.Equal(1, calls);
    }
}
