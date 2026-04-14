# Apps Usage Check — C# WPF Process Tracker

## Context

Build a high-quality Windows desktop application from scratch that tracks how much time the user spends in selected processes. The app runs silently in the system tray, auto-starts with Windows, and persists all data to PostgreSQL. It tracks both **foreground (active window)** time and **total running** time separately.

## Prerequisites

1. **.NET 10 SDK** — must be installed (download from dotnet.microsoft.com if missing)
2. **PostgreSQL** server running locally (or remotely, connection string configurable)
3. **Visual Studio 2022** (17.8+) recommended for WPF designer support

## Local Commands

Run these from the repository root:

```powershell
dotnet restore AppsUsageCheck.sln
dotnet build AppsUsageCheck.sln
dotnet run --project src/AppsUsageCheck.App/AppsUsageCheck.App.csproj
dotnet clean AppsUsageCheck.sln
dotnet test
```

Build individual projects when needed:

```powershell
dotnet build src/AppsUsageCheck.Core/AppsUsageCheck.Core.csproj
dotnet build src/AppsUsageCheck.Infrastructure/AppsUsageCheck.Infrastructure.csproj
dotnet build src/AppsUsageCheck.App/AppsUsageCheck.App.csproj
```

If the .NET CLI hits first-run setup issues in PowerShell, use this session-only setup before building:

```powershell
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet build AppsUsageCheck.sln
```

## Solution Structure

```
apps-usage-check/
├── AppsUsageCheck.sln
├── Directory.Build.props
├── .gitignore
├── .editorconfig
├── src/
│   ├── AppsUsageCheck.Core/           # Domain models, interfaces, engine logic (no dependencies)
│   │   ├── Models/
│   │   ├── Interfaces/
│   │   ├── Services/
│   │   └── Enums/
│   ├── AppsUsageCheck.Infrastructure/ # Data access (EF Core + PostgreSQL), Win32 interop
│   │   ├── Data/
│   │   ├── Interop/
│   │   ├── Services/
│   │   └── Migrations/
│   └── AppsUsageCheck.App/           # WPF app — Views, ViewModels, DI, entry point
│       ├── Views/
│       ├── ViewModels/
│       ├── Converters/
│       ├── Services/
│       ├── Resources/
│       └── Assets/
└── tests/
    ├── AppsUsageCheck.Core.Tests/
    └── AppsUsageCheck.Infrastructure.Tests/
```

**Dependency direction**: `App → Core + Infrastructure`, `Infrastructure → Core`, `Core → nothing`.

## NuGet Packages

| Package | Project | Purpose |
|---------|---------|---------|
| `CommunityToolkit.Mvvm` >= 8.2 | Core | MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`) |
| `Npgsql.EntityFrameworkCore.PostgreSQL` >= 8.0 | Infrastructure | EF Core PostgreSQL provider |
| `Microsoft.EntityFrameworkCore.Design` >= 8.0 | Infrastructure | Migrations CLI |
| `EFCore.NamingConventions` >= 8.0 | Infrastructure | Automatic snake_case column mapping |
| `Polly` >= 8.0 | Infrastructure | Retry/resilience for DB operations |
| `Microsoft.Extensions.Hosting` >= 8.0 | App | Generic host, DI, config |
| `Serilog.Extensions.Hosting` + `.Sinks.File` + `.Sinks.Console` | App | Structured logging |
| `Hardcodet.NotifyIcon.Wpf` >= 1.1 | App | System tray icon |

## Database Schema (PostgreSQL)

```sql
CREATE TABLE tracked_processes (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    process_name    VARCHAR(256) NOT NULL UNIQUE,
    display_name    VARCHAR(512),
    is_paused       BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE usage_sessions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tracked_process_id  UUID NOT NULL REFERENCES tracked_processes(id) ON DELETE CASCADE,
    session_start       TIMESTAMPTZ NOT NULL,
    session_end         TIMESTAMPTZ,               -- NULL = still running
    total_running_secs  BIGINT NOT NULL DEFAULT 0,
    foreground_secs     BIGINT NOT NULL DEFAULT 0,
    is_manual_edit      BOOLEAN NOT NULL DEFAULT FALSE,
    notes               TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE time_adjustments (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tracked_process_id  UUID NOT NULL REFERENCES tracked_processes(id) ON DELETE CASCADE,
    adjustment_type     VARCHAR(20) NOT NULL,       -- 'running' or 'foreground'
    adjustment_secs     BIGINT NOT NULL,            -- positive=add, negative=subtract
    reason              TEXT,
    applied_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

**Total displayed time** = `SUM(session seconds) + SUM(adjustment seconds)`.

---

## Phase 1: Project Setup & Foundation

**Goal**: Solution skeleton, NuGet packages, DB schema, build configuration.

**Files to create**:
- `AppsUsageCheck.sln` (via `dotnet new sln`)
- `Directory.Build.props` — shared: `net10.0-windows`, nullable enable, implicit usings, warnings as errors
- `.gitignore`, `.editorconfig`
- `src/AppsUsageCheck.Core/AppsUsageCheck.Core.csproj` — class library
- `src/AppsUsageCheck.Infrastructure/AppsUsageCheck.Infrastructure.csproj` — class library
- `src/AppsUsageCheck.App/AppsUsageCheck.App.csproj` — WPF app (`<UseWPF>true</UseWPF>`, `<OutputType>WinExe</OutputType>`)
- `src/AppsUsageCheck.App/appsettings.json` — connection string, polling interval (1000ms), flush interval (30s)

**Key details**:
- App project: set `CopyToOutputDirectory = PreserveNewest` for appsettings.json
- Add `app.manifest` for DPI awareness
- All project references wired: App → Core + Infrastructure, Infrastructure → Core

---

## Phase 2: Core Tracking Engine

**Goal**: Background engine that detects running processes, identifies foreground window, accumulates time.

**Files to create**:
- `Core/Models/TrackedProcess.cs` — Id, ProcessName, DisplayName, IsPaused, timestamps
- `Core/Models/UsageSession.cs` — Id, TrackedProcessId, SessionStart/End, TotalRunningSeconds, ForegroundSeconds
- `Core/Models/TimeAdjustment.cs` — Id, TrackedProcessId, AdjustmentType, AdjustmentSeconds, Reason
- `Core/Models/ProcessStatus.cs` — Runtime state (not persisted): IsRunning, IsForeground, current + historic seconds
- `Core/Enums/TrackingState.cs` — Active, Paused
- `Core/Interfaces/IProcessDetector.cs` — `GetRunningProcessNames()` → `IReadOnlySet<string>`
- `Core/Interfaces/IForegroundDetector.cs` — `GetForegroundProcessName()` → `string?`
- `Core/Interfaces/IUsageRepository.cs` — CRUD for processes, sessions, adjustments, aggregate queries
- `Core/Interfaces/ITrackingEngine.cs` — StartAsync, StopAsync, GetAllStatuses, Add/Remove/Pause/Resume
- `Core/Services/TrackingEngine.cs` — **the heart of the app**
- `Infrastructure/Interop/NativeMethods.cs` — P/Invoke: `GetForegroundWindow`, `GetWindowThreadProcessId`
- `Infrastructure/Services/Win32ProcessDetector.cs` — Uses `Process.GetProcesses()`
- `Infrastructure/Services/Win32ForegroundDetector.cs` — Uses Win32 API to find foreground process name

**TrackingEngine design**:
- Uses `PeriodicTimer` (1-second tick) — no reentrancy issues
- Each tick: get running process names, get foreground process name, for each tracked (non-paused) process: increment running seconds if alive, increment foreground seconds if in focus
- Session lifecycle: process starts → new session; process stops → close session + flush; app shuts down → close all open sessions
- DB flush every 30 seconds (configurable) — in-memory counters are authoritative at runtime
- Process name normalization: lowercase, strip `.exe`
- The tick loop MUST never throw — wrap in try/catch, log errors, continue

---

## Phase 3: Data Layer (PostgreSQL + EF Core)

**Goal**: Persistence layer with resilience.

**Files to create**:
- `Infrastructure/Data/AppDbContext.cs` — DbContext with DbSets
- `Infrastructure/Data/Configurations/TrackedProcessConfiguration.cs` — Fluent API, snake_case, unique index
- `Infrastructure/Data/Configurations/UsageSessionConfiguration.cs` — FK, indexes
- `Infrastructure/Data/Configurations/TimeAdjustmentConfiguration.cs` — FK
- `Infrastructure/Data/UsageRepository.cs` — Implements IUsageRepository with Polly retry
- `Infrastructure/Data/DesignTimeDbContextFactory.cs` — For EF CLI migrations

**Key details**:
- Use `UseSnakeCaseNamingConvention()` from EFCore.NamingConventions
- Polly retry pipeline: 3 attempts, exponential backoff, handles `NpgsqlException` where `IsTransient`
- Offline write queue: if PostgreSQL is down, buffer writes in a `Channel<Func<Task>>` and drain when connectivity resumes
- Run `Database.MigrateAsync()` on app startup

---

## Phase 4: WPF UI (Main Window)

**Goal**: Main window with MVVM, showing tracked processes and controls.

**Files to create**:
- `App/App.xaml` + `App.xaml.cs` — Host builder, DI container, Serilog, migration, engine start, single-instance mutex
- `App/ViewModels/MainViewModel.cs` — ObservableCollection of processes, Add/Remove/Pause/Resume commands, 1-second UI refresh via DispatcherTimer
- `App/ViewModels/ProcessItemViewModel.cs` — Per-row: ProcessName, DisplayName, IsRunning, IsForeground, IsPaused, formatted times
- `App/ViewModels/AddProcessViewModel.cs` — Searchable list of running processes, manual entry option
- `App/Views/MainWindow.xaml` + `.cs` — Toolbar (Add, Settings), DataGrid (status dot, name, running time, foreground time, pause/resume, remove), status bar
- `App/Views/AddProcessDialog.xaml` + `.cs` — Pick from running processes or type name, optional display name
- `App/Converters/SecondsToTimeStringConverter.cs` — 3661 → "1h 1m 1s"
- `App/Converters/BoolToStatusColorConverter.cs` — Running=Green, Stopped=Gray, Paused=Yellow
- `App/Converters/BoolToVisibilityConverter.cs`
- `App/Resources/Styles.xaml` — Global styles, colors, DataGrid templates
- `App/Services/IDialogService.cs` + `DialogService.cs` — Show dialogs from ViewModels

**Key details**:
- Window closing → hide (don't close), for system tray behavior
- `ShutdownMode = OnExplicitShutdown`
- DataGrid virtualization enabled
- Uses CommunityToolkit.Mvvm source generators (`partial` classes)

---

## Phase 5: System Tray Integration

**Goal**: App lives in system tray, main window opens on demand.

**Files to create**:
- `App/Services/ITrayIconService.cs` + `TrayIconService.cs` — Manages `TaskbarIcon` from Hardcodet
- `App/Assets/tray-icon.ico` — Multi-resolution icon (16/32/48px)

**Tray context menu**: Open, Pause All, Resume All, separator, Exit.
**Double-click**: Show main window.
**Tooltip**: "Tracking N processes | M active".
**Startup**: Window hidden by default, tray icon only.

---

## Phase 6: Auto-start with Windows

**Goal**: Launch on Windows boot.

**Files to create**:
- `Core/Interfaces/IAutoStartService.cs`
- `Infrastructure/Services/AutoStartService.cs` — Registry: `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`

**Key details**:
- No admin privileges needed (HKCU, not HKLM)
- Passes `--minimized` flag on startup → tray-only mode
- Single-instance enforcement via named `Mutex` in App.xaml.cs
- UI toggle: "Start with Windows" checkbox in settings

---

## Phase 7: Pause/Resume & Manual Time Editing

**Goal**: Per-process pause/resume, manual time adjustment with audit trail.

**Files to create**:
- `App/ViewModels/EditTimeViewModel.cs` — Add/subtract hours/minutes/seconds, reason field, type (running/foreground)
- `App/Views/EditTimeDialog.xaml` + `.cs` — Shows current time, adjustment inputs, live preview of new total

**Key details**:
- Pause persists to DB (`is_paused` column) — survives app restart
- Manual adjustments create `time_adjustments` records (append-only audit trail), never modify sessions
- Validation: cannot subtract more than current total
- Context menu on each process row: Pause/Resume, Edit Time, Remove

---

## Phase 8: Polish & Hardening

**Goal**: Production-quality error handling, logging, settings UI, edge cases.

**Files to create**:
- `App/ViewModels/SettingsViewModel.cs` + `App/Views/SettingsWindow.xaml` — Polling interval, flush interval, auto-start toggle, connection string, log level
- `Core/Services/TimeFormatter.cs` — Centralized formatting
- `Infrastructure/Services/DatabaseHealthCheck.cs` — Periodic `SELECT 1`, exposes `IsConnected`
- `App/Converters/ConnectionStatusConverter.cs` — Green/red/yellow status dot

**Error handling**:
- Global exception handlers: `DispatcherUnhandledException`, `UnhandledException`, `UnobservedTaskException`
- TrackingEngine tick: never throws — catch + log + continue
- Process access errors: some system processes throw `AccessDeniedException` → catch silently
- DB status indicator in UI status bar

**Edge cases**:
- Screen lock/sleep: no foreground process → foreground time pauses, running time continues
- Crash recovery: on startup, find sessions with `session_end IS NULL` → close if process not running, continue if still running
- System processes: filter out SessionId==0 processes from "add" dialog
- Clock changes: use `DateTimeOffset` (UTC) everywhere
- Multiple instances: blocked by Mutex

**Logging** (Serilog):
- Info: start/stop, session created/closed, DB flush
- Warning: DB retry, process access error
- Error: DB write failure after retries
- Rolling file: `logs/app-{Date}.log`, 10 MB max, 7 days retention

---

## Phase Dependency Graph

```
Phase 1 (Setup)
  └─→ Phase 2 (Engine)
       └─→ Phase 3 (Data Layer)
            └─→ Phase 4 (WPF UI)
                 ├─→ Phase 5 (System Tray)
                 │    └─→ Phase 6 (Auto-start)
                 └─→ Phase 7 (Pause/Resume + Time Edit)
                      └─→ Phase 8 (Polish)
```

## Verification Plan

After each phase, verify:

1. **Phase 1**: `dotnet build` succeeds for all projects, solution compiles clean
2. **Phase 2**: Unit tests for TrackingEngine with mocked detectors — verify tick logic, session lifecycle, pause behavior
3. **Phase 3**: Integration tests against a real PostgreSQL instance — CRUD operations, migrations run, resilience pipeline handles transient failures
4. **Phase 4**: Launch app → main window renders, add a process, see it tracked in the list, times increment
5. **Phase 5**: Close window → app stays in tray, double-click tray → window reopens, Exit → app shuts down
6. **Phase 6**: Enable auto-start → reboot → app appears in tray, tracking resumes
7. **Phase 7**: Pause a process → time stops, resume → time continues; edit time → new total reflects adjustment
8. **Phase 8**: Kill PostgreSQL → app continues tracking with queue, restart PostgreSQL → data flushes; check logs for proper entries; crash and restart → sessions recovered

## Complete File Listing

```
AppsUsageCheck.sln
Directory.Build.props
.gitignore
.editorconfig

src/AppsUsageCheck.Core/
    AppsUsageCheck.Core.csproj
    Models/TrackedProcess.cs, UsageSession.cs, TimeAdjustment.cs, ProcessStatus.cs
    Enums/TrackingState.cs
    Interfaces/IProcessDetector.cs, IForegroundDetector.cs, IUsageRepository.cs, ITrackingEngine.cs, IAutoStartService.cs
    Services/TrackingEngine.cs, TimeFormatter.cs

src/AppsUsageCheck.Infrastructure/
    AppsUsageCheck.Infrastructure.csproj
    Data/AppDbContext.cs, DesignTimeDbContextFactory.cs, UsageRepository.cs
    Data/Configurations/TrackedProcessConfiguration.cs, UsageSessionConfiguration.cs, TimeAdjustmentConfiguration.cs
    Interop/NativeMethods.cs
    Services/Win32ProcessDetector.cs, Win32ForegroundDetector.cs, AutoStartService.cs, DatabaseHealthCheck.cs
    Migrations/ (EF-generated)

src/AppsUsageCheck.App/
    AppsUsageCheck.App.csproj
    App.xaml, App.xaml.cs, appsettings.json
    Assets/app-icon.ico, tray-icon.ico
    Views/MainWindow.xaml(.cs), AddProcessDialog.xaml(.cs), EditTimeDialog.xaml(.cs), SettingsWindow.xaml(.cs)
    ViewModels/MainViewModel.cs, ProcessItemViewModel.cs, AddProcessViewModel.cs, EditTimeViewModel.cs, SettingsViewModel.cs
    Converters/SecondsToTimeStringConverter.cs, BoolToVisibilityConverter.cs, BoolToStatusColorConverter.cs, ConnectionStatusConverter.cs
    Services/IDialogService.cs, DialogService.cs, ITrayIconService.cs, TrayIconService.cs
    Resources/Styles.xaml

tests/
    AppsUsageCheck.Core.Tests/TrackingEngineTests.cs, TimeFormatterTests.cs
    AppsUsageCheck.Infrastructure.Tests/UsageRepositoryTests.cs
```
