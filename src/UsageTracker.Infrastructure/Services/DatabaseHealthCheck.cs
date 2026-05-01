using System.Data.Common;
using UsageTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace UsageTracker.Infrastructure.Services;

public sealed class DatabaseHealthCheck : IDatabaseHealthCheck, IAsyncDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<DatabaseHealthCheck> _logger;
    private readonly Lock _stateLock = new();
    private readonly Lock _lifecycleLock = new();

    private CancellationTokenSource? _loopCancellationTokenSource;
    private Task? _loopTask;
    private DatabaseConnectionState _connectionState = DatabaseConnectionState.Unknown;
    private string _statusText = "Database status pending...";
    private DateTimeOffset? _lastCheckedAtUtc;

    public DatabaseHealthCheck(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<DatabaseHealthCheck> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public DatabaseConnectionState ConnectionState
    {
        get
        {
            lock (_stateLock)
            {
                return _connectionState;
            }
        }
    }

    public bool IsConnected => ConnectionState == DatabaseConnectionState.Connected;

    public string StatusText
    {
        get
        {
            lock (_stateLock)
            {
                return _statusText;
            }
        }
    }

    public DateTimeOffset? LastCheckedAtUtc
    {
        get
        {
            lock (_stateLock)
            {
                return _lastCheckedAtUtc;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lifecycleLock)
        {
            if (_loopTask is not null)
            {
                return Task.CompletedTask;
            }

            _loopCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loopTask = RunLoopAsync(_loopCancellationTokenSource.Token);
            return Task.CompletedTask;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? loopTask;
        CancellationTokenSource? cancellationTokenSource;

        lock (_lifecycleLock)
        {
            loopTask = _loopTask;
            cancellationTokenSource = _loopCancellationTokenSource;
            _loopTask = null;
            _loopCancellationTokenSource = null;
        }

        if (cancellationTokenSource is not null)
        {
            await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            cancellationTokenSource.Dispose();
        }

        if (loopTask is not null)
        {
            await loopTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        await CheckDatabaseAsync(cancellationToken).ConfigureAwait(false);

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await CheckDatabaseAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await dbContext.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT 1";
            _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            UpdateState(DatabaseConnectionState.Connected, "Database connected", checkedAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Database health check failed.");
            UpdateState(DatabaseConnectionState.Disconnected, "Database unavailable", checkedAt);
        }
    }

    private void UpdateState(
        DatabaseConnectionState newState,
        string newStatusText,
        DateTimeOffset checkedAtUtc)
    {
        DatabaseConnectionState previousState;
        string previousStatusText;

        lock (_stateLock)
        {
            previousState = _connectionState;
            previousStatusText = _statusText;
            _connectionState = newState;
            _statusText = newStatusText;
            _lastCheckedAtUtc = checkedAtUtc;
        }

        if (previousState != newState || !string.Equals(previousStatusText, newStatusText, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Database health state changed to {ConnectionState}: {StatusText}.",
                newState,
                newStatusText);
        }
    }
}
