using UsageTracker.Core.Enums;
using UsageTracker.Core.Interfaces;
using UsageTracker.Core.Models;

namespace UsageTracker.Core.Services;

public sealed class TrackingEngine : ITrackingEngine, IAsyncDisposable
{
    private readonly IProcessDetector _processDetector;
    private readonly IForegroundDetector _foregroundDetector;
    private readonly IUsageRepository _usageRepository;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _pollingInterval;
    private readonly TimeSpan _flushInterval;
    private readonly long _tickIncrementSeconds;
    private readonly Action<Exception>? _errorHandler;
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, TrackedProcessRuntime> _trackedProcesses = [];

    private CancellationTokenSource? _loopCancellationTokenSource;
    private Task? _loopTask;
    private bool _isStarted;
    private DateTimeOffset _lastFlushAt;

    public TrackingEngine(
        IProcessDetector processDetector,
        IForegroundDetector foregroundDetector,
        IUsageRepository usageRepository,
        TimeSpan pollingInterval,
        TimeSpan flushInterval,
        TimeProvider? timeProvider = null,
        Action<Exception>? errorHandler = null)
    {
        if (pollingInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollingInterval));
        }

        if (flushInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(flushInterval));
        }

        _processDetector = processDetector ?? throw new ArgumentNullException(nameof(processDetector));
        _foregroundDetector = foregroundDetector ?? throw new ArgumentNullException(nameof(foregroundDetector));
        _usageRepository = usageRepository ?? throw new ArgumentNullException(nameof(usageRepository));
        _pollingInterval = pollingInterval;
        _flushInterval = flushInterval;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _errorHandler = errorHandler;
        _tickIncrementSeconds = Math.Max(1L, (long)Math.Round(_pollingInterval.TotalSeconds, MidpointRounding.AwayFromZero));
        _lastFlushAt = _timeProvider.GetUtcNow();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource linkedCancellationTokenSource;

        lock (_syncRoot)
        {
            if (_isStarted)
            {
                return;
            }

            _isStarted = true;
            _loopCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCancellationTokenSource = _loopCancellationTokenSource;
        }

        try
        {
            await LoadTrackedProcessesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (_syncRoot)
            {
                _isStarted = false;
                _loopCancellationTokenSource?.Dispose();
                _loopCancellationTokenSource = null;
            }

            throw;
        }

        _lastFlushAt = _timeProvider.GetUtcNow();
        _loopTask = RunLoopAsync(linkedCancellationTokenSource.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? loopTask;

        lock (_syncRoot)
        {
            if (!_isStarted)
            {
                return;
            }

            _isStarted = false;
            _loopCancellationTokenSource?.Cancel();
            loopTask = _loopTask;
        }

        if (loopTask is not null)
        {
            try
            {
                await loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        await CloseAllOpenSessionsAsync(cancellationToken).ConfigureAwait(false);

        lock (_syncRoot)
        {
            _loopCancellationTokenSource?.Dispose();
            _loopCancellationTokenSource = null;
            _loopTask = null;
        }
    }

    public IReadOnlyList<ProcessStatus> GetAllStatuses()
    {
        lock (_syncRoot)
        {
            return _trackedProcesses.Values
                .Select(runtime => CloneStatus(runtime.Status))
                .OrderBy(status => status.ProcessName, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public async Task<TrackedProcess> AddTrackedProcessAsync(string processName, string? displayName = null, CancellationToken cancellationToken = default)
    {
        var normalizedProcessName = ProcessNameNormalizer.Normalize(processName);
        if (normalizedProcessName.Length == 0)
        {
            throw new ArgumentException("Process name cannot be empty.", nameof(processName));
        }

        lock (_syncRoot)
        {
            if (_trackedProcesses.Values.Any(runtime => string.Equals(runtime.Process.ProcessName, normalizedProcessName, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Process '{normalizedProcessName}' is already being tracked.");
            }
        }

        var existing = await _usageRepository.GetTrackedProcessByNameAsync(normalizedProcessName, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Process '{normalizedProcessName}' is already being tracked.");
        }

        var now = _timeProvider.GetUtcNow();
        var trackedProcess = new TrackedProcess
        {
            Id = Guid.NewGuid(),
            ProcessName = normalizedProcessName,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            IsPaused = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _usageRepository.AddTrackedProcessAsync(trackedProcess, cancellationToken).ConfigureAwait(false);

        var runtime = new TrackedProcessRuntime(
            trackedProcess,
            new ProcessStatus
            {
                TrackedProcessId = trackedProcess.Id,
                ProcessName = trackedProcess.ProcessName,
                DisplayName = trackedProcess.DisplayName,
                TrackingState = TrackingState.Active,
            });

        lock (_syncRoot)
        {
            _trackedProcesses[trackedProcess.Id] = runtime;
        }

        return CloneTrackedProcess(trackedProcess);
    }

    public async Task UpdateTrackedProcessDisplayNameAsync(Guid trackedProcessId, string? displayName, CancellationToken cancellationToken = default)
    {
        TrackedProcess trackedProcess;
        var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();

        lock (_syncRoot)
        {
            var runtime = GetRuntime(trackedProcessId);
            runtime.Process.DisplayName = normalizedDisplayName;
            runtime.Process.UpdatedAt = _timeProvider.GetUtcNow();
            runtime.Status.DisplayName = normalizedDisplayName;
            trackedProcess = CloneTrackedProcess(runtime.Process);
        }

        await _usageRepository.UpdateTrackedProcessAsync(trackedProcess, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveTrackedProcessAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
    {
        TrackedProcessRuntime runtime;

        lock (_syncRoot)
        {
            runtime = GetRuntime(trackedProcessId);
        }

        await CloseSessionAsync(runtime, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        await _usageRepository.RemoveTrackedProcessAsync(trackedProcessId, cancellationToken).ConfigureAwait(false);

        lock (_syncRoot)
        {
            _trackedProcesses.Remove(trackedProcessId);
        }
    }

    public async Task PauseTrackingAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
    {
        TrackedProcessRuntime runtime;
        var now = _timeProvider.GetUtcNow();

        lock (_syncRoot)
        {
            runtime = GetRuntime(trackedProcessId);
            if (runtime.Process.IsPaused)
            {
                return;
            }

            runtime.Process.IsPaused = true;
            runtime.Process.UpdatedAt = now;
            runtime.Status.TrackingState = TrackingState.Paused;
            runtime.Status.IsRunning = false;
            runtime.Status.IsForeground = false;
        }

        await CloseSessionAsync(runtime, now, cancellationToken).ConfigureAwait(false);
        await _usageRepository.UpdateTrackedProcessAsync(CloneTrackedProcess(runtime.Process), cancellationToken).ConfigureAwait(false);
    }

    public async Task ResumeTrackingAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
    {
        TrackedProcess trackedProcess;

        lock (_syncRoot)
        {
            var runtime = GetRuntime(trackedProcessId);
            if (!runtime.Process.IsPaused)
            {
                return;
            }

            runtime.Process.IsPaused = false;
            runtime.Process.UpdatedAt = _timeProvider.GetUtcNow();
            runtime.Status.TrackingState = TrackingState.Active;
            trackedProcess = CloneTrackedProcess(runtime.Process);
        }

        await _usageRepository.UpdateTrackedProcessAsync(trackedProcess, cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyTimeAdjustmentAsync(
        Guid trackedProcessId,
        TimeAdjustmentTarget target,
        long adjustmentSeconds,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (adjustmentSeconds == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(adjustmentSeconds), "Adjustment must be non-zero.");
        }

        TimeAdjustment adjustment;

        lock (_syncRoot)
        {
            var runtime = GetRuntime(trackedProcessId);
            var currentRunningSeconds = runtime.Status.TotalRunningSeconds;
            var currentForegroundSeconds = runtime.Status.ForegroundSeconds;
            var newRunningSeconds = currentRunningSeconds + (target == TimeAdjustmentTarget.Running ? adjustmentSeconds : 0);
            var newForegroundSeconds = currentForegroundSeconds + (target == TimeAdjustmentTarget.Foreground ? adjustmentSeconds : 0);

            if (newRunningSeconds < 0)
            {
                throw new InvalidOperationException("Running time cannot be reduced below zero.");
            }

            if (newForegroundSeconds < 0)
            {
                throw new InvalidOperationException("Foreground time cannot be reduced below zero.");
            }

            if (newForegroundSeconds > newRunningSeconds)
            {
                throw new InvalidOperationException("Foreground time cannot exceed running time.");
            }

            adjustment = new TimeAdjustment
            {
                Id = Guid.NewGuid(),
                TrackedProcessId = trackedProcessId,
                AdjustmentType = TimeAdjustmentTypes.ToStorageValue(target),
                AdjustmentSeconds = adjustmentSeconds,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                AppliedAt = _timeProvider.GetUtcNow(),
            };
        }

        await _usageRepository.AddTimeAdjustmentAsync(adjustment, cancellationToken).ConfigureAwait(false);

        lock (_syncRoot)
        {
            var runtime = GetRuntime(trackedProcessId);
            runtime.Status.TotalRunningSeconds += target == TimeAdjustmentTarget.Running ? adjustmentSeconds : 0;
            runtime.Status.ForegroundSeconds += target == TimeAdjustmentTarget.Foreground ? adjustmentSeconds : 0;
        }
    }

    public async Task PauseAllTrackingAsync(CancellationToken cancellationToken = default)
    {
        Guid[] trackedProcessIds;

        lock (_syncRoot)
        {
            trackedProcessIds = _trackedProcesses.Values
                .Where(runtime => !runtime.Process.IsPaused)
                .Select(runtime => runtime.Process.Id)
                .ToArray();
        }

        foreach (var trackedProcessId in trackedProcessIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PauseTrackingAsync(trackedProcessId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ResumeAllTrackingAsync(CancellationToken cancellationToken = default)
    {
        Guid[] trackedProcessIds;

        lock (_syncRoot)
        {
            trackedProcessIds = _trackedProcesses.Values
                .Where(runtime => runtime.Process.IsPaused)
                .Select(runtime => runtime.Process.Id)
                .ToArray();
        }

        foreach (var trackedProcessId in trackedProcessIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ResumeTrackingAsync(trackedProcessId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyDictionary<Guid, UsageTotals>> GetFilteredTotalsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        if (to <= from)
        {
            throw new ArgumentOutOfRangeException(nameof(to), "The time range end must be after the start.");
        }

        Guid[] trackedProcessIds;

        lock (_syncRoot)
        {
            trackedProcessIds = _trackedProcesses.Keys.ToArray();
        }

        var totals = await Task.WhenAll(
                trackedProcessIds.Select(
                    async trackedProcessId =>
                    {
                        var runningSeconds = await _usageRepository
                            .GetTotalRunningSecondsAsync(trackedProcessId, from, to, cancellationToken)
                            .ConfigureAwait(false);
                        var foregroundSeconds = await _usageRepository
                            .GetTotalForegroundSecondsAsync(trackedProcessId, from, to, cancellationToken)
                            .ConfigureAwait(false);

                        return new KeyValuePair<Guid, UsageTotals>(
                            trackedProcessId,
                            new UsageTotals(runningSeconds, foregroundSeconds));
                    }))
            .ConfigureAwait(false);

        return totals.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    internal Task ProcessTickAsync(CancellationToken cancellationToken = default)
    {
        return ProcessTickSafeAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task LoadTrackedProcessesAsync(CancellationToken cancellationToken)
    {
        var trackedProcesses = await _usageRepository.GetTrackedProcessesAsync(cancellationToken).ConfigureAwait(false);
        var openSessions = await _usageRepository.GetOpenSessionsAsync(cancellationToken).ConfigureAwait(false);
        var openSessionsByTrackedProcessId = openSessions
            .GroupBy(session => session.TrackedProcessId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(session => session.SessionStart)
                    .Select(CloneUsageSession)
                    .First());
        var activeTargetProcessNames = trackedProcesses
            .Where(trackedProcess => !trackedProcess.IsPaused)
            .Select(trackedProcess => ProcessNameNormalizer.Normalize(trackedProcess.ProcessName))
            .Where(processName => processName.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var runningProcessNames = activeTargetProcessNames.Length == 0
            ? []
            : NormalizeProcessNames(_processDetector.GetRunningTargetProcessNames(activeTargetProcessNames));
        var foregroundProcessName = runningProcessNames.Count == 0
            ? string.Empty
            : ProcessNameNormalizer.Normalize(_foregroundDetector.GetForegroundProcessName());
        var runtimes = new List<TrackedProcessRuntime>(trackedProcesses.Count);
        var now = _timeProvider.GetUtcNow();

        foreach (var trackedProcess in trackedProcesses)
        {
            var normalizedProcessName = ProcessNameNormalizer.Normalize(trackedProcess.ProcessName);
            var normalizedTrackedProcess = CloneTrackedProcess(trackedProcess);
            normalizedTrackedProcess.ProcessName = normalizedProcessName;
            var isRunning = !normalizedTrackedProcess.IsPaused && runningProcessNames.Contains(normalizedTrackedProcess.ProcessName);
            var isForeground = isRunning && string.Equals(normalizedTrackedProcess.ProcessName, foregroundProcessName, StringComparison.Ordinal);

            var totalRunningSeconds = await _usageRepository.GetTotalRunningSecondsAsync(normalizedTrackedProcess.Id, cancellationToken).ConfigureAwait(false);
            var foregroundSeconds = await _usageRepository.GetTotalForegroundSecondsAsync(normalizedTrackedProcess.Id, cancellationToken).ConfigureAwait(false);
            openSessionsByTrackedProcessId.TryGetValue(normalizedTrackedProcess.Id, out var openSession);

            if (openSession is not null
                && (normalizedTrackedProcess.IsPaused || !runningProcessNames.Contains(normalizedTrackedProcess.ProcessName)))
            {
                openSession.SessionEnd = now;
                openSession.UpdatedAt = now;
                await _usageRepository.UpdateUsageSessionAsync(CloneUsageSession(openSession), cancellationToken).ConfigureAwait(false);
                openSession = null;
            }

            var status = new ProcessStatus
            {
                TrackedProcessId = normalizedTrackedProcess.Id,
                ProcessName = normalizedTrackedProcess.ProcessName,
                DisplayName = normalizedTrackedProcess.DisplayName,
                TrackingState = normalizedTrackedProcess.IsPaused ? TrackingState.Paused : TrackingState.Active,
                IsRunning = isRunning,
                IsForeground = isForeground,
                TotalRunningSeconds = totalRunningSeconds,
                ForegroundSeconds = foregroundSeconds,
            };

            if (openSession is not null)
            {
                status.CurrentSessionStart = openSession.SessionStart;
                status.CurrentSessionRunningSeconds = openSession.TotalRunningSeconds;
                status.CurrentSessionForegroundSeconds = openSession.ForegroundSeconds;
            }

            runtimes.Add(new TrackedProcessRuntime(normalizedTrackedProcess, status, openSession));
        }

        lock (_syncRoot)
        {
            _trackedProcesses.Clear();
            foreach (var runtime in runtimes)
            {
                _trackedProcesses[runtime.Process.Id] = runtime;
            }
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_pollingInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await ProcessTickSafeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessTickSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ProcessTickCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _errorHandler?.Invoke(exception);
        }
    }

    private async Task ProcessTickCoreAsync(CancellationToken cancellationToken)
    {
        List<TrackedProcessRuntime> runtimes;
        lock (_syncRoot)
        {
            runtimes = _trackedProcesses.Values.ToList();
        }

        var activeTargetProcessNames = runtimes
            .Where(runtime => !runtime.Process.IsPaused)
            .Select(runtime => runtime.Process.ProcessName)
            .ToArray();
        var runningProcessNames = activeTargetProcessNames.Length == 0
            ? []
            : NormalizeProcessNames(_processDetector.GetRunningTargetProcessNames(activeTargetProcessNames));
        var foregroundProcessName = runningProcessNames.Count == 0
            ? string.Empty
            : ProcessNameNormalizer.Normalize(_foregroundDetector.GetForegroundProcessName());
        var now = _timeProvider.GetUtcNow();
        var shouldFlush = now - _lastFlushAt >= _flushInterval;

        foreach (var runtime in runtimes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (runtime.Process.IsPaused)
            {
                runtime.Status.TrackingState = TrackingState.Paused;
                runtime.Status.IsRunning = false;
                runtime.Status.IsForeground = false;
                continue;
            }

            runtime.Status.TrackingState = TrackingState.Active;
            var isRunning = runningProcessNames.Contains(runtime.Process.ProcessName);
            var isForeground = isRunning && string.Equals(runtime.Process.ProcessName, foregroundProcessName, StringComparison.Ordinal);

            runtime.Status.IsRunning = isRunning;
            runtime.Status.IsForeground = isForeground;

            if (isRunning)
            {
                if (runtime.OpenSession is null)
                {
                    await OpenSessionAsync(runtime, now, cancellationToken).ConfigureAwait(false);
                }

                runtime.OpenSession!.TotalRunningSeconds += _tickIncrementSeconds;
                runtime.OpenSession.ForegroundSeconds += isForeground ? _tickIncrementSeconds : 0;
                runtime.OpenSession.UpdatedAt = now;

                runtime.Status.TotalRunningSeconds += _tickIncrementSeconds;
                runtime.Status.ForegroundSeconds += isForeground ? _tickIncrementSeconds : 0;
                runtime.Status.CurrentSessionRunningSeconds += _tickIncrementSeconds;
                runtime.Status.CurrentSessionForegroundSeconds += isForeground ? _tickIncrementSeconds : 0;
                runtime.Status.CurrentSessionStart ??= runtime.OpenSession.SessionStart;
            }
            else if (runtime.OpenSession is not null)
            {
                await CloseSessionAsync(runtime, now, cancellationToken).ConfigureAwait(false);
            }

            if (shouldFlush && runtime.OpenSession is not null)
            {
                await _usageRepository.UpdateUsageSessionAsync(CloneUsageSession(runtime.OpenSession), cancellationToken).ConfigureAwait(false);
            }
        }

        if (shouldFlush)
        {
            _lastFlushAt = now;
        }
    }

    private async Task OpenSessionAsync(TrackedProcessRuntime runtime, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var session = new UsageSession
        {
            Id = Guid.NewGuid(),
            TrackedProcessId = runtime.Process.Id,
            SessionStart = now,
            SessionEnd = null,
            TotalRunningSeconds = 0,
            ForegroundSeconds = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };

        runtime.OpenSession = session;
        runtime.Status.CurrentSessionStart = session.SessionStart;
        runtime.Status.CurrentSessionRunningSeconds = 0;
        runtime.Status.CurrentSessionForegroundSeconds = 0;

        await _usageRepository.AddUsageSessionAsync(CloneUsageSession(session), cancellationToken).ConfigureAwait(false);
    }

    private async Task CloseSessionAsync(TrackedProcessRuntime runtime, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (runtime.OpenSession is null)
        {
            runtime.Status.CurrentSessionStart = null;
            runtime.Status.CurrentSessionRunningSeconds = 0;
            runtime.Status.CurrentSessionForegroundSeconds = 0;
            runtime.Status.IsRunning = false;
            runtime.Status.IsForeground = false;
            return;
        }

        runtime.OpenSession.SessionEnd = now;
        runtime.OpenSession.UpdatedAt = now;

        await _usageRepository.UpdateUsageSessionAsync(CloneUsageSession(runtime.OpenSession), cancellationToken).ConfigureAwait(false);

        runtime.OpenSession = null;
        runtime.Status.CurrentSessionStart = null;
        runtime.Status.CurrentSessionRunningSeconds = 0;
        runtime.Status.CurrentSessionForegroundSeconds = 0;
        runtime.Status.IsRunning = false;
        runtime.Status.IsForeground = false;
    }

    private async Task CloseAllOpenSessionsAsync(CancellationToken cancellationToken)
    {
        List<TrackedProcessRuntime> runtimes;
        lock (_syncRoot)
        {
            runtimes = _trackedProcesses.Values.ToList();
        }

        var now = _timeProvider.GetUtcNow();
        foreach (var runtime in runtimes)
        {
            if (runtime.OpenSession is null)
            {
                continue;
            }

            await CloseSessionAsync(runtime, now, cancellationToken).ConfigureAwait(false);
        }
    }

    private static HashSet<string> NormalizeProcessNames(IEnumerable<string> processNames)
    {
        var normalized = new HashSet<string>(StringComparer.Ordinal);

        foreach (var processName in processNames)
        {
            var name = ProcessNameNormalizer.Normalize(processName);
            if (name.Length > 0)
            {
                normalized.Add(name);
            }
        }

        return normalized;
    }

    private TrackedProcessRuntime GetRuntime(Guid trackedProcessId)
    {
        if (_trackedProcesses.TryGetValue(trackedProcessId, out var runtime))
        {
            return runtime;
        }

        throw new KeyNotFoundException($"Tracked process '{trackedProcessId}' was not found.");
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

    private static ProcessStatus CloneStatus(ProcessStatus source)
    {
        return new ProcessStatus
        {
            TrackedProcessId = source.TrackedProcessId,
            ProcessName = source.ProcessName,
            DisplayName = source.DisplayName,
            TrackingState = source.TrackingState,
            IsRunning = source.IsRunning,
            IsForeground = source.IsForeground,
            TotalRunningSeconds = source.TotalRunningSeconds,
            ForegroundSeconds = source.ForegroundSeconds,
            CurrentSessionRunningSeconds = source.CurrentSessionRunningSeconds,
            CurrentSessionForegroundSeconds = source.CurrentSessionForegroundSeconds,
            CurrentSessionStart = source.CurrentSessionStart,
        };
    }

    private sealed class TrackedProcessRuntime
    {
        public TrackedProcessRuntime(TrackedProcess process, ProcessStatus status, UsageSession? openSession = null)
        {
            Process = process;
            Status = status;
            OpenSession = openSession;
        }

        public TrackedProcess Process { get; }

        public ProcessStatus Status { get; }

        public UsageSession? OpenSession { get; set; }
    }
}
