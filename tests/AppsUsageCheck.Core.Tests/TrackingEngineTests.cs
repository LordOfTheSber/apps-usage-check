using AppsUsageCheck.Core.Enums;
using AppsUsageCheck.Core.Interfaces;
using AppsUsageCheck.Core.Models;
using AppsUsageCheck.Core.Services;
using Xunit;

namespace AppsUsageCheck.Core.Tests;

public sealed class TrackingEngineTests
{
    [Fact]
    public async Task AddTrackedProcessAsync_NormalizesNameAndTrimsDisplayName()
    {
        var now = new DateTimeOffset(2026, 4, 14, 11, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var repository = new FakeUsageRepository(Array.Empty<TrackedProcess>());
        var processDetector = new FakeProcessDetector(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var foregroundDetector = new FakeForegroundDetector(null);

        await using var engine = new TrackingEngine(
            processDetector,
            foregroundDetector,
            repository,
            pollingInterval: TimeSpan.FromSeconds(1),
            flushInterval: TimeSpan.FromSeconds(30),
            timeProvider: timeProvider);

        await engine.StartAsync();

        var trackedProcess = await engine.AddTrackedProcessAsync("  Code.EXE  ", "  Visual Studio Code  ");

        Assert.Equal("code", trackedProcess.ProcessName);
        Assert.Equal("Visual Studio Code", trackedProcess.DisplayName);

        var status = Assert.Single(engine.GetAllStatuses());
        Assert.Equal("code", status.ProcessName);
        Assert.Equal("Visual Studio Code", status.DisplayName);

        var addedProcess = Assert.Single(repository.AddedTrackedProcesses);
        Assert.Equal("code", addedProcess.ProcessName);
        Assert.Equal("Visual Studio Code", addedProcess.DisplayName);
    }

    [Fact]
    public async Task AddTrackedProcessAsync_DuplicateNormalizedName_ThrowsInvalidOperationException()
    {
        var trackedProcess = new TrackedProcess
        {
            Id = Guid.NewGuid(),
            ProcessName = "code",
        };

        var repository = new FakeUsageRepository(new[] { trackedProcess });
        var processDetector = new FakeProcessDetector(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var foregroundDetector = new FakeForegroundDetector(null);

        await using var engine = new TrackingEngine(
            processDetector,
            foregroundDetector,
            repository,
            pollingInterval: TimeSpan.FromSeconds(1),
            flushInterval: TimeSpan.FromSeconds(30));

        await engine.StartAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.AddTrackedProcessAsync("CODE.exe"));

        Assert.Contains("already being tracked", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessTickAsync_RunningForegroundProcess_OpensSessionAndAccumulatesTime()
    {
        var now = new DateTimeOffset(2026, 4, 14, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var trackedProcess = new TrackedProcess
        {
            Id = Guid.NewGuid(),
            ProcessName = "code",
            DisplayName = "Visual Studio Code",
        };

        var repository = new FakeUsageRepository(
            new[] { trackedProcess },
            new Dictionary<Guid, long> { [trackedProcess.Id] = 120 },
            new Dictionary<Guid, long> { [trackedProcess.Id] = 30 });
        var processDetector = new FakeProcessDetector(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Code.exe" });
        var foregroundDetector = new FakeForegroundDetector("code.exe");

        await using var engine = new TrackingEngine(
            processDetector,
            foregroundDetector,
            repository,
            pollingInterval: TimeSpan.FromSeconds(1),
            flushInterval: TimeSpan.FromSeconds(30),
            timeProvider: timeProvider);

        await engine.StartAsync();
        await engine.ProcessTickAsync();

        var status = Assert.Single(engine.GetAllStatuses());
        Assert.Equal(TrackingState.Active, status.TrackingState);
        Assert.True(status.IsRunning);
        Assert.True(status.IsForeground);
        Assert.Equal(121, status.TotalRunningSeconds);
        Assert.Equal(31, status.ForegroundSeconds);
        Assert.Equal(1, status.CurrentSessionRunningSeconds);
        Assert.Equal(1, status.CurrentSessionForegroundSeconds);
        Assert.Equal(now, status.CurrentSessionStart);

        var createdSession = Assert.Single(repository.AddedSessions);
        Assert.Equal(trackedProcess.Id, createdSession.TrackedProcessId);
        Assert.Equal(now, createdSession.SessionStart);
        Assert.Null(createdSession.SessionEnd);
    }

    [Fact]
    public async Task ProcessTickAsync_ProcessStops_ClosesOpenSessionAndResetsCurrentSessionCounters()
    {
        var now = new DateTimeOffset(2026, 4, 14, 13, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var trackedProcess = new TrackedProcess
        {
            Id = Guid.NewGuid(),
            ProcessName = "notepad",
        };

        var repository = new FakeUsageRepository(new[] { trackedProcess });
        var processDetector = new FakeProcessDetector(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "notepad.exe" });
        var foregroundDetector = new FakeForegroundDetector("notepad");

        await using var engine = new TrackingEngine(
            processDetector,
            foregroundDetector,
            repository,
            pollingInterval: TimeSpan.FromSeconds(1),
            flushInterval: TimeSpan.FromSeconds(30),
            timeProvider: timeProvider);

        await engine.StartAsync();
        await engine.ProcessTickAsync();

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        processDetector.SetProcesses(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        foregroundDetector.SetForeground(null);

        await engine.ProcessTickAsync();

        var status = Assert.Single(engine.GetAllStatuses());
        Assert.Equal(TrackingState.Active, status.TrackingState);
        Assert.False(status.IsRunning);
        Assert.False(status.IsForeground);
        Assert.Equal(1, status.TotalRunningSeconds);
        Assert.Equal(1, status.ForegroundSeconds);
        Assert.Equal(0, status.CurrentSessionRunningSeconds);
        Assert.Equal(0, status.CurrentSessionForegroundSeconds);
        Assert.Null(status.CurrentSessionStart);

        var closedSession = Assert.Single(repository.UpdatedSessions);
        Assert.Equal(1, closedSession.TotalRunningSeconds);
        Assert.Equal(1, closedSession.ForegroundSeconds);
        Assert.Equal(now.AddSeconds(1), closedSession.SessionEnd);
    }

    [Fact]
    public async Task ProcessTickAsync_RunningBackgroundProcess_AccumulatesRunningWithoutForegroundTime()
    {
        var now = new DateTimeOffset(2026, 4, 14, 13, 30, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var trackedProcess = new TrackedProcess
        {
            Id = Guid.NewGuid(),
            ProcessName = "notepad",
        };

        var repository = new FakeUsageRepository(new[] { trackedProcess });
        var processDetector = new FakeProcessDetector(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "notepad.exe" });
        var foregroundDetector = new FakeForegroundDetector("code.exe");

        await using var engine = new TrackingEngine(
            processDetector,
            foregroundDetector,
            repository,
            pollingInterval: TimeSpan.FromSeconds(1),
            flushInterval: TimeSpan.FromSeconds(30),
            timeProvider: timeProvider);

        await engine.StartAsync();
        await engine.ProcessTickAsync();

        var status = Assert.Single(engine.GetAllStatuses());
        Assert.True(status.IsRunning);
        Assert.False(status.IsForeground);
        Assert.Equal(1, status.TotalRunningSeconds);
        Assert.Equal(0, status.ForegroundSeconds);
        Assert.Equal(1, status.CurrentSessionRunningSeconds);
        Assert.Equal(0, status.CurrentSessionForegroundSeconds);
    }

    [Fact]
    public async Task PauseAndResumeTrackingAsync_PausedProcessDoesNotAccumulateUntilResumed()
    {
        var now = new DateTimeOffset(2026, 4, 14, 14, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var trackedProcess = new TrackedProcess
        {
            Id = Guid.NewGuid(),
            ProcessName = "chrome",
        };

        var repository = new FakeUsageRepository(new[] { trackedProcess });
        var processDetector = new FakeProcessDetector(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "chrome.exe" });
        var foregroundDetector = new FakeForegroundDetector("chrome");

        await using var engine = new TrackingEngine(
            processDetector,
            foregroundDetector,
            repository,
            pollingInterval: TimeSpan.FromSeconds(1),
            flushInterval: TimeSpan.FromSeconds(30),
            timeProvider: timeProvider);

        await engine.StartAsync();
        await engine.ProcessTickAsync();

        await engine.PauseTrackingAsync(trackedProcess.Id);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await engine.ProcessTickAsync();

        var pausedStatus = Assert.Single(engine.GetAllStatuses());
        Assert.Equal(TrackingState.Paused, pausedStatus.TrackingState);
        Assert.False(pausedStatus.IsRunning);
        Assert.Equal(1, pausedStatus.TotalRunningSeconds);
        Assert.Equal(1, pausedStatus.ForegroundSeconds);

        await engine.ResumeTrackingAsync(trackedProcess.Id);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await engine.ProcessTickAsync();

        var resumedStatus = Assert.Single(engine.GetAllStatuses());
        Assert.Equal(TrackingState.Active, resumedStatus.TrackingState);
        Assert.True(resumedStatus.IsRunning);
        Assert.True(resumedStatus.IsForeground);
        Assert.Equal(2, resumedStatus.TotalRunningSeconds);
        Assert.Equal(2, resumedStatus.ForegroundSeconds);

        Assert.Equal(2, repository.AddedSessions.Count);
        Assert.Equal(2, repository.UpdatedTrackedProcesses.Count);
    }

    [Fact]
    public async Task PauseAllTrackingAsync_PausesOnlyActiveProcessesAndClosesOpenSessions()
    {
        var now = new DateTimeOffset(2026, 4, 14, 14, 30, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var activeProcess = new TrackedProcess
        {
            Id = Guid.NewGuid(),
            ProcessName = "chrome",
        };
        var alreadyPausedProcess = new TrackedProcess
        {
            Id = Guid.NewGuid(),
            ProcessName = "notepad",
            IsPaused = true,
        };

        var repository = new FakeUsageRepository(new[] { activeProcess, alreadyPausedProcess });
        var processDetector = new FakeProcessDetector(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "chrome.exe",
                "notepad.exe",
            });
        var foregroundDetector = new FakeForegroundDetector("chrome");

        await using var engine = new TrackingEngine(
            processDetector,
            foregroundDetector,
            repository,
            pollingInterval: TimeSpan.FromSeconds(1),
            flushInterval: TimeSpan.FromSeconds(30),
            timeProvider: timeProvider);

        await engine.StartAsync();
        await engine.ProcessTickAsync();
        await engine.PauseAllTrackingAsync();

        var statuses = engine.GetAllStatuses()
            .OrderBy(status => status.ProcessName, StringComparer.Ordinal)
            .ToArray();

        Assert.Collection(
            statuses,
            chromeStatus =>
            {
                Assert.Equal("chrome", chromeStatus.ProcessName);
                Assert.Equal(TrackingState.Paused, chromeStatus.TrackingState);
                Assert.False(chromeStatus.IsRunning);
                Assert.False(chromeStatus.IsForeground);
                Assert.Equal(1, chromeStatus.TotalRunningSeconds);
            },
            notepadStatus =>
            {
                Assert.Equal("notepad", notepadStatus.ProcessName);
                Assert.Equal(TrackingState.Paused, notepadStatus.TrackingState);
                Assert.False(notepadStatus.IsRunning);
                Assert.False(notepadStatus.IsForeground);
                Assert.Equal(0, notepadStatus.TotalRunningSeconds);
            });

        Assert.Single(repository.UpdatedTrackedProcesses);
        Assert.Equal(activeProcess.Id, repository.UpdatedTrackedProcesses[0].Id);

        var closedSession = Assert.Single(repository.UpdatedSessions);
        Assert.Equal(activeProcess.Id, closedSession.TrackedProcessId);
        Assert.Equal(now, closedSession.SessionEnd);
    }

    [Fact]
    public async Task ResumeAllTrackingAsync_ResumesOnlyPausedProcesses()
    {
        var trackedProcess = new TrackedProcess
        {
            Id = Guid.NewGuid(),
            ProcessName = "chrome",
            IsPaused = true,
        };
        var alreadyActiveProcess = new TrackedProcess
        {
            Id = Guid.NewGuid(),
            ProcessName = "code",
        };

        var repository = new FakeUsageRepository(new[] { trackedProcess, alreadyActiveProcess });
        var processDetector = new FakeProcessDetector(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var foregroundDetector = new FakeForegroundDetector(null);

        await using var engine = new TrackingEngine(
            processDetector,
            foregroundDetector,
            repository,
            pollingInterval: TimeSpan.FromSeconds(1),
            flushInterval: TimeSpan.FromSeconds(30));

        await engine.StartAsync();
        await engine.ResumeAllTrackingAsync();

        var statuses = engine.GetAllStatuses()
            .OrderBy(status => status.ProcessName, StringComparer.Ordinal)
            .ToArray();

        Assert.Collection(
            statuses,
            chromeStatus =>
            {
                Assert.Equal("chrome", chromeStatus.ProcessName);
                Assert.Equal(TrackingState.Active, chromeStatus.TrackingState);
            },
            codeStatus =>
            {
                Assert.Equal("code", codeStatus.ProcessName);
                Assert.Equal(TrackingState.Active, codeStatus.TrackingState);
            });

        Assert.Single(repository.UpdatedTrackedProcesses);
        Assert.Equal(trackedProcess.Id, repository.UpdatedTrackedProcesses[0].Id);
        Assert.False(repository.UpdatedTrackedProcesses[0].IsPaused);
    }

    [Fact]
    public async Task ProcessTickAsync_FlushIntervalReached_PersistsOpenSession()
    {
        var now = new DateTimeOffset(2026, 4, 14, 15, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var trackedProcess = new TrackedProcess
        {
            Id = Guid.NewGuid(),
            ProcessName = "chrome",
        };

        var repository = new FakeUsageRepository(new[] { trackedProcess });
        var processDetector = new FakeProcessDetector(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "chrome.exe" });
        var foregroundDetector = new FakeForegroundDetector("chrome");

        await using var engine = new TrackingEngine(
            processDetector,
            foregroundDetector,
            repository,
            pollingInterval: TimeSpan.FromSeconds(1),
            flushInterval: TimeSpan.FromSeconds(2),
            timeProvider: timeProvider);

        await engine.StartAsync();
        await engine.ProcessTickAsync();

        timeProvider.Advance(TimeSpan.FromSeconds(2));
        await engine.ProcessTickAsync();

        var persistedSession = Assert.Single(repository.UpdatedSessions);
        Assert.Null(persistedSession.SessionEnd);
        Assert.Equal(2, persistedSession.TotalRunningSeconds);
        Assert.Equal(2, persistedSession.ForegroundSeconds);
    }

    [Fact]
    public async Task StopAsync_WithOpenSession_ClosesSessionAtCurrentTime()
    {
        var now = new DateTimeOffset(2026, 4, 14, 16, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var trackedProcess = new TrackedProcess
        {
            Id = Guid.NewGuid(),
            ProcessName = "chrome",
        };

        var repository = new FakeUsageRepository(new[] { trackedProcess });
        var processDetector = new FakeProcessDetector(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "chrome.exe" });
        var foregroundDetector = new FakeForegroundDetector("chrome");

        await using var engine = new TrackingEngine(
            processDetector,
            foregroundDetector,
            repository,
            pollingInterval: TimeSpan.FromSeconds(1),
            flushInterval: TimeSpan.FromSeconds(30),
            timeProvider: timeProvider);

        await engine.StartAsync();
        await engine.ProcessTickAsync();

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await engine.StopAsync();

        var closedSession = Assert.Single(repository.UpdatedSessions);
        Assert.Equal(now.AddSeconds(5), closedSession.SessionEnd);
        Assert.Equal(1, closedSession.TotalRunningSeconds);
        Assert.Equal(1, closedSession.ForegroundSeconds);
    }

    [Fact]
    public async Task StartAsync_LoadsPausedTrackedProcess_AsPausedStatus()
    {
        var trackedProcess = new TrackedProcess
        {
            Id = Guid.NewGuid(),
            ProcessName = "chrome.exe",
            IsPaused = true,
        };

        var repository = new FakeUsageRepository(new[] { trackedProcess });
        var processDetector = new FakeProcessDetector(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "chrome.exe" });
        var foregroundDetector = new FakeForegroundDetector("chrome");

        await using var engine = new TrackingEngine(
            processDetector,
            foregroundDetector,
            repository,
            pollingInterval: TimeSpan.FromSeconds(1),
            flushInterval: TimeSpan.FromSeconds(30));

        await engine.StartAsync();
        await engine.ProcessTickAsync();

        var status = Assert.Single(engine.GetAllStatuses());
        Assert.Equal("chrome", status.ProcessName);
        Assert.Equal(TrackingState.Paused, status.TrackingState);
        Assert.False(status.IsRunning);
        Assert.False(status.IsForeground);
        Assert.Empty(repository.AddedSessions);
    }

    private sealed class FakeProcessDetector : IProcessDetector
    {
        private IReadOnlySet<string> _processes;

        public FakeProcessDetector(IReadOnlySet<string> processes)
        {
            _processes = processes;
        }

        public IReadOnlySet<string> GetRunningProcessNames() => _processes;

        public void SetProcesses(IReadOnlySet<string> processes)
        {
            _processes = processes;
        }
    }

    private sealed class FakeForegroundDetector : IForegroundDetector
    {
        private string? _foregroundProcessName;

        public FakeForegroundDetector(string? foregroundProcessName)
        {
            _foregroundProcessName = foregroundProcessName;
        }

        public string? GetForegroundProcessName() => _foregroundProcessName;

        public void SetForeground(string? foregroundProcessName)
        {
            _foregroundProcessName = foregroundProcessName;
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan by)
        {
            _utcNow = _utcNow.Add(by);
        }
    }

    private sealed class FakeUsageRepository : IUsageRepository
    {
        private readonly Dictionary<Guid, TrackedProcess> _trackedProcesses;
        private readonly Dictionary<Guid, long> _totalRunningSeconds;
        private readonly Dictionary<Guid, long> _totalForegroundSeconds;
        private readonly Dictionary<Guid, UsageSession> _sessionsById;

        public FakeUsageRepository(
            IEnumerable<TrackedProcess> trackedProcesses,
            IDictionary<Guid, long>? totalRunningSeconds = null,
            IDictionary<Guid, long>? totalForegroundSeconds = null,
            IEnumerable<UsageSession>? usageSessions = null)
        {
            _trackedProcesses = trackedProcesses.ToDictionary(process => process.Id, CloneTrackedProcess);
            _totalRunningSeconds = totalRunningSeconds is null
                ? []
                : new Dictionary<Guid, long>(totalRunningSeconds);
            _totalForegroundSeconds = totalForegroundSeconds is null
                ? []
                : new Dictionary<Guid, long>(totalForegroundSeconds);
            _sessionsById = usageSessions is null
                ? []
                : usageSessions.ToDictionary(session => session.Id, CloneUsageSession);
        }

        public List<TrackedProcess> AddedTrackedProcesses { get; } = [];

        public List<TrackedProcess> UpdatedTrackedProcesses { get; } = [];

        public List<UsageSession> AddedSessions { get; } = [];

        public List<UsageSession> UpdatedSessions { get; } = [];

        public Task<IReadOnlyList<TrackedProcess>> GetTrackedProcessesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TrackedProcess>>(_trackedProcesses.Values.Select(CloneTrackedProcess).ToArray());
        }

        public Task<TrackedProcess?> GetTrackedProcessByNameAsync(string normalizedProcessName, CancellationToken cancellationToken = default)
        {
            var trackedProcess = _trackedProcesses.Values.FirstOrDefault(process =>
                string.Equals(process.ProcessName, normalizedProcessName, StringComparison.Ordinal));

            return Task.FromResult(trackedProcess is null ? null : CloneTrackedProcess(trackedProcess));
        }

        public Task AddTrackedProcessAsync(TrackedProcess trackedProcess, CancellationToken cancellationToken = default)
        {
            _trackedProcesses[trackedProcess.Id] = CloneTrackedProcess(trackedProcess);
            AddedTrackedProcesses.Add(CloneTrackedProcess(trackedProcess));
            return Task.CompletedTask;
        }

        public Task UpdateTrackedProcessAsync(TrackedProcess trackedProcess, CancellationToken cancellationToken = default)
        {
            _trackedProcesses[trackedProcess.Id] = CloneTrackedProcess(trackedProcess);
            UpdatedTrackedProcesses.Add(CloneTrackedProcess(trackedProcess));
            return Task.CompletedTask;
        }

        public Task RemoveTrackedProcessAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
        {
            _trackedProcesses.Remove(trackedProcessId);
            return Task.CompletedTask;
        }

        public Task<UsageSession?> GetOpenSessionAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
        {
            var session = _sessionsById.Values.FirstOrDefault(candidate =>
                candidate.TrackedProcessId == trackedProcessId && candidate.SessionEnd is null);

            return Task.FromResult(session is null ? null : CloneUsageSession(session));
        }

        public Task AddUsageSessionAsync(UsageSession session, CancellationToken cancellationToken = default)
        {
            _sessionsById[session.Id] = CloneUsageSession(session);
            AddedSessions.Add(CloneUsageSession(session));
            return Task.CompletedTask;
        }

        public Task UpdateUsageSessionAsync(UsageSession session, CancellationToken cancellationToken = default)
        {
            _sessionsById[session.Id] = CloneUsageSession(session);
            UpdatedSessions.Add(CloneUsageSession(session));
            return Task.CompletedTask;
        }

        public Task AddTimeAdjustmentAsync(TimeAdjustment adjustment, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<long> GetTotalRunningSecondsAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_totalRunningSeconds.TryGetValue(trackedProcessId, out var value) ? value : 0L);
        }

        public Task<long> GetTotalForegroundSecondsAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_totalForegroundSeconds.TryGetValue(trackedProcessId, out var value) ? value : 0L);
        }

        private static TrackedProcess CloneTrackedProcess(TrackedProcess trackedProcess)
        {
            return new TrackedProcess
            {
                Id = trackedProcess.Id,
                ProcessName = trackedProcess.ProcessName,
                DisplayName = trackedProcess.DisplayName,
                IsPaused = trackedProcess.IsPaused,
                CreatedAt = trackedProcess.CreatedAt,
                UpdatedAt = trackedProcess.UpdatedAt,
            };
        }

        private static UsageSession CloneUsageSession(UsageSession session)
        {
            return new UsageSession
            {
                Id = session.Id,
                TrackedProcessId = session.TrackedProcessId,
                SessionStart = session.SessionStart,
                SessionEnd = session.SessionEnd,
                TotalRunningSeconds = session.TotalRunningSeconds,
                ForegroundSeconds = session.ForegroundSeconds,
                IsManualEdit = session.IsManualEdit,
                Notes = session.Notes,
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt,
            };
        }
    }
}
