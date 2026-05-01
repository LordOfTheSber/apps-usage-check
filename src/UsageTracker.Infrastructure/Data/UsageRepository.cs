using System.Threading.Channels;
using UsageTracker.Core.Interfaces;
using UsageTracker.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;

namespace UsageTracker.Infrastructure.Data;

public sealed class UsageRepository : IUsageRepository, IAsyncDisposable
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<UsageRepository> _logger;
    private readonly IAsyncPolicy _databaseRetryPolicy;
    private readonly Channel<QueuedWrite> _writeQueue;
    private readonly CancellationTokenSource _queueCancellationTokenSource = new();
    private readonly Task _queueDrainTask;

    public UsageRepository(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<UsageRepository> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _databaseRetryPolicy = Policy
            .Handle<NpgsqlException>(static exception => exception.IsTransient)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: static attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
                onRetryAsync: (exception, delay, retryAttempt, _) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retrying database operation in {DelaySeconds} second(s). Attempt {RetryAttempt}.",
                        delay.TotalSeconds,
                        retryAttempt);
                    return Task.CompletedTask;
                });

        _writeQueue = Channel.CreateUnbounded<QueuedWrite>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        _queueDrainTask = Task.Run(() => DrainWriteQueueAsync(_queueCancellationTokenSource.Token));
    }

    public Task<IReadOnlyList<TrackedProcess>> GetTrackedProcessesAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(
            static async (dbContext, ct) =>
            {
                var trackedProcesses = await dbContext.TrackedProcesses
                    .AsNoTracking()
                    .OrderBy(trackedProcess => trackedProcess.ProcessName)
                    .ToArrayAsync(ct)
                    .ConfigureAwait(false);

                return (IReadOnlyList<TrackedProcess>)trackedProcesses;
            },
            cancellationToken);
    }

    public Task<TrackedProcess?> GetTrackedProcessByNameAsync(string normalizedProcessName, CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(
            async (dbContext, ct) => await dbContext.TrackedProcesses
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    trackedProcess => trackedProcess.ProcessName == normalizedProcessName,
                    ct)
                .ConfigureAwait(false),
            cancellationToken);
    }

    public Task AddTrackedProcessAsync(TrackedProcess trackedProcess, CancellationToken cancellationToken = default)
    {
        var trackedProcessCopy = CloneTrackedProcess(trackedProcess);
        return ExecuteWriteAsync(
            "add tracked process",
            async (dbContext, ct) =>
            {
                await dbContext.TrackedProcesses.AddAsync(trackedProcessCopy, ct).ConfigureAwait(false);
            },
            onSucceeded: null,
            cancellationToken);
    }

    public Task UpdateTrackedProcessAsync(TrackedProcess trackedProcess, CancellationToken cancellationToken = default)
    {
        var trackedProcessCopy = CloneTrackedProcess(trackedProcess);
        return ExecuteWriteAsync(
            "update tracked process",
            (dbContext, _) =>
            {
                dbContext.TrackedProcesses.Update(trackedProcessCopy);
                return Task.CompletedTask;
            },
            onSucceeded: null,
            cancellationToken);
    }

    public Task RemoveTrackedProcessAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
    {
        return ExecuteWriteAsync(
            "remove tracked process",
            async (dbContext, ct) =>
            {
                var trackedProcess = await dbContext.TrackedProcesses
                    .SingleOrDefaultAsync(entity => entity.Id == trackedProcessId, ct)
                    .ConfigureAwait(false);

                if (trackedProcess is not null)
                {
                    dbContext.TrackedProcesses.Remove(trackedProcess);
                }
            },
            onSucceeded: null,
            cancellationToken);
    }

    public Task<IReadOnlyList<UsageSession>> GetOpenSessionsAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(
            static async (dbContext, ct) =>
            {
                var openSessions = await dbContext.UsageSessions
                    .AsNoTracking()
                    .Where(session => session.SessionEnd == null)
                    .OrderByDescending(session => session.SessionStart)
                    .ToArrayAsync(ct)
                    .ConfigureAwait(false);

                return (IReadOnlyList<UsageSession>)openSessions;
            },
            cancellationToken);
    }

    public Task<UsageSession?> GetOpenSessionAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(
            async (dbContext, ct) => await dbContext.UsageSessions
                .AsNoTracking()
                .Where(session => session.TrackedProcessId == trackedProcessId && session.SessionEnd == null)
                .OrderByDescending(session => session.SessionStart)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false),
            cancellationToken);
    }

    public Task AddUsageSessionAsync(UsageSession session, CancellationToken cancellationToken = default)
    {
        var sessionCopy = CloneUsageSession(session);
        return ExecuteWriteAsync(
            "add usage session",
            async (dbContext, ct) =>
            {
                await dbContext.UsageSessions.AddAsync(sessionCopy, ct).ConfigureAwait(false);
            },
            () => _logger.LogInformation(
                "Created usage session {SessionId} for tracked process {TrackedProcessId}.",
                sessionCopy.Id,
                sessionCopy.TrackedProcessId),
            cancellationToken);
    }

    public Task UpdateUsageSessionAsync(UsageSession session, CancellationToken cancellationToken = default)
    {
        var sessionCopy = CloneUsageSession(session);
        return ExecuteWriteAsync(
            "update usage session",
            (dbContext, _) =>
            {
                dbContext.UsageSessions.Update(sessionCopy);
                return Task.CompletedTask;
            },
            () =>
            {
                if (sessionCopy.SessionEnd.HasValue)
                {
                    _logger.LogInformation(
                        "Closed usage session {SessionId} for tracked process {TrackedProcessId}.",
                        sessionCopy.Id,
                        sessionCopy.TrackedProcessId);
                }
                else
                {
                    _logger.LogInformation(
                        "Flushed open usage session {SessionId} for tracked process {TrackedProcessId}.",
                        sessionCopy.Id,
                        sessionCopy.TrackedProcessId);
                }
            },
            cancellationToken);
    }

    public Task AddTimeAdjustmentAsync(TimeAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        var adjustmentCopy = CloneTimeAdjustment(adjustment);
        return ExecuteWriteAsync(
            "add time adjustment",
            async (dbContext, ct) =>
            {
                await dbContext.TimeAdjustments.AddAsync(adjustmentCopy, ct).ConfigureAwait(false);
            },
            onSucceeded: null,
            cancellationToken);
    }

    public Task<long> GetTotalRunningSecondsAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(
            async (dbContext, ct) =>
            {
                var sessionTotal = await dbContext.UsageSessions
                    .AsNoTracking()
                    .Where(session => session.TrackedProcessId == trackedProcessId)
                    .Select(session => (long?)session.TotalRunningSeconds)
                    .SumAsync(ct)
                    .ConfigureAwait(false);

                var adjustmentTotal = await dbContext.TimeAdjustments
                    .AsNoTracking()
                    .Where(adjustment =>
                        adjustment.TrackedProcessId == trackedProcessId &&
                        adjustment.AdjustmentType == TimeAdjustmentTypes.Running)
                    .Select(adjustment => (long?)adjustment.AdjustmentSeconds)
                    .SumAsync(ct)
                    .ConfigureAwait(false);

                return (sessionTotal ?? 0L) + (adjustmentTotal ?? 0L);
            },
            cancellationToken);
    }

    public Task<long> GetTotalForegroundSecondsAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(
            async (dbContext, ct) =>
            {
                var sessionTotal = await dbContext.UsageSessions
                    .AsNoTracking()
                    .Where(session => session.TrackedProcessId == trackedProcessId)
                    .Select(session => (long?)session.ForegroundSeconds)
                    .SumAsync(ct)
                    .ConfigureAwait(false);

                var adjustmentTotal = await dbContext.TimeAdjustments
                    .AsNoTracking()
                    .Where(adjustment =>
                        adjustment.TrackedProcessId == trackedProcessId &&
                        adjustment.AdjustmentType == TimeAdjustmentTypes.Foreground)
                    .Select(adjustment => (long?)adjustment.AdjustmentSeconds)
                    .SumAsync(ct)
                    .ConfigureAwait(false);

                return (sessionTotal ?? 0L) + (adjustmentTotal ?? 0L);
            },
            cancellationToken);
    }

    public Task<long> GetTotalRunningSecondsAsync(
        Guid trackedProcessId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        ValidateRange(from, to);

        return ExecuteReadAsync(
            async (dbContext, ct) =>
            {
                var sessionTotal = await dbContext.UsageSessions
                    .AsNoTracking()
                    .Where(session =>
                        session.TrackedProcessId == trackedProcessId &&
                        session.SessionStart >= from &&
                        session.SessionStart < to)
                    .Select(session => (long?)session.TotalRunningSeconds)
                    .SumAsync(ct)
                    .ConfigureAwait(false);

                var adjustmentTotal = await dbContext.TimeAdjustments
                    .AsNoTracking()
                    .Where(adjustment =>
                        adjustment.TrackedProcessId == trackedProcessId &&
                        adjustment.AdjustmentType == TimeAdjustmentTypes.Running &&
                        adjustment.AppliedAt >= from &&
                        adjustment.AppliedAt < to)
                    .Select(adjustment => (long?)adjustment.AdjustmentSeconds)
                    .SumAsync(ct)
                    .ConfigureAwait(false);

                return (sessionTotal ?? 0L) + (adjustmentTotal ?? 0L);
            },
            cancellationToken);
    }

    public Task<long> GetTotalForegroundSecondsAsync(
        Guid trackedProcessId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        ValidateRange(from, to);

        return ExecuteReadAsync(
            async (dbContext, ct) =>
            {
                var sessionTotal = await dbContext.UsageSessions
                    .AsNoTracking()
                    .Where(session =>
                        session.TrackedProcessId == trackedProcessId &&
                        session.SessionStart >= from &&
                        session.SessionStart < to)
                    .Select(session => (long?)session.ForegroundSeconds)
                    .SumAsync(ct)
                    .ConfigureAwait(false);

                var adjustmentTotal = await dbContext.TimeAdjustments
                    .AsNoTracking()
                    .Where(adjustment =>
                        adjustment.TrackedProcessId == trackedProcessId &&
                        adjustment.AdjustmentType == TimeAdjustmentTypes.Foreground &&
                        adjustment.AppliedAt >= from &&
                        adjustment.AppliedAt < to)
                    .Select(adjustment => (long?)adjustment.AdjustmentSeconds)
                    .SumAsync(ct)
                    .ConfigureAwait(false);

                return (sessionTotal ?? 0L) + (adjustmentTotal ?? 0L);
            },
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _writeQueue.Writer.TryComplete();
        await _queueCancellationTokenSource.CancelAsync().ConfigureAwait(false);

        try
        {
            await _queueDrainTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _queueCancellationTokenSource.Dispose();
    }

    private async Task<T> ExecuteReadAsync<T>(
        Func<AppDbContext, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        return await _databaseRetryPolicy.ExecuteAsync(
                async ct =>
                {
                    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
                    return await operation(dbContext, ct).ConfigureAwait(false);
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ExecuteWriteAsync(
        string operationName,
        Func<AppDbContext, CancellationToken, Task> operation,
        Action? onSucceeded,
        CancellationToken cancellationToken)
    {
        var queuedWrite = new QueuedWrite(operationName, operation, onSucceeded);

        try
        {
            await PersistWriteAsync(queuedWrite, cancellationToken).ConfigureAwait(false);
        }
        catch (NpgsqlException exception) when (exception.IsTransient)
        {
            _logger.LogError(exception, "Database write '{OperationName}' failed after retries and was queued.", operationName);
            await _writeQueue.Writer.WriteAsync(queuedWrite, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PersistWriteAsync(QueuedWrite queuedWrite, CancellationToken cancellationToken)
    {
        await _databaseRetryPolicy.ExecuteAsync(
                async ct =>
                {
                    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
                    await queuedWrite.ExecuteAsync(dbContext, ct).ConfigureAwait(false);
                    await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                    queuedWrite.OnSucceeded?.Invoke();
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task DrainWriteQueueAsync(CancellationToken cancellationToken)
    {
        while (await _writeQueue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (_writeQueue.Reader.TryRead(out var queuedWrite))
            {
                await PersistQueuedWriteUntilSuccessAsync(queuedWrite, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task PersistQueuedWriteUntilSuccessAsync(QueuedWrite queuedWrite, CancellationToken cancellationToken)
    {
        var retryAttempt = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await PersistWriteAsync(queuedWrite, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Queued database write '{OperationName}' flushed successfully.", queuedWrite.OperationName);
                return;
            }
            catch (NpgsqlException exception) when (exception.IsTransient)
            {
                retryAttempt++;
                _logger.LogWarning(
                    exception,
                    "Queued database write '{OperationName}' is still failing. Retry cycle {RetryAttempt}.",
                    queuedWrite.OperationName,
                    retryAttempt);
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, retryAttempt)));
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void ValidateRange(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from)
        {
            throw new ArgumentOutOfRangeException(nameof(to), "The time range end must be after the start.");
        }
    }

    private static TrackedProcess CloneTrackedProcess(TrackedProcess source)
    {
        return new TrackedProcess
        {
            Id = source.Id,
            ProcessName = source.ProcessName,
            DisplayName = source.DisplayName,
            IsPaused = source.IsPaused,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
        };
    }

    private static UsageSession CloneUsageSession(UsageSession source)
    {
        return new UsageSession
        {
            Id = source.Id,
            TrackedProcessId = source.TrackedProcessId,
            SessionStart = source.SessionStart,
            SessionEnd = source.SessionEnd,
            TotalRunningSeconds = source.TotalRunningSeconds,
            ForegroundSeconds = source.ForegroundSeconds,
            IsManualEdit = source.IsManualEdit,
            Notes = source.Notes,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
        };
    }

    private static TimeAdjustment CloneTimeAdjustment(TimeAdjustment source)
    {
        return new TimeAdjustment
        {
            Id = source.Id,
            TrackedProcessId = source.TrackedProcessId,
            AdjustmentType = source.AdjustmentType,
            AdjustmentSeconds = source.AdjustmentSeconds,
            Reason = source.Reason,
            AppliedAt = source.AppliedAt,
        };
    }

    private sealed record QueuedWrite(
        string OperationName,
        Func<AppDbContext, CancellationToken, Task> ExecuteAsync,
        Action? OnSucceeded);
}
