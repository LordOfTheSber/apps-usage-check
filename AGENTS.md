# AGENTS

## Stack
- .NET 10, WPF, Windows-only.
- `Nullable` enabled, implicit usings enabled, warnings treated as errors.

## Projects
- `src/AppsUsageCheck.Core`: domain models, interfaces, tracking logic. Keep it dependency-light.
- `src/AppsUsageCheck.Infrastructure`: Win32 interop and persistence/integration code. Depends on `Core`.
- `src/AppsUsageCheck.App`: WPF app entry point and UI. Depends on `Core` and `Infrastructure`.
- `tests/AppsUsageCheck.Core.Tests`: unit tests for core behavior.

## Commands
- Build: `dotnet build AppsUsageCheck.sln`
- Test: `dotnet test tests/AppsUsageCheck.Core.Tests/AppsUsageCheck.Core.Tests.csproj`
- Run app: `dotnet run --project src/AppsUsageCheck.App/AppsUsageCheck.App.csproj`
- Run minimized: `dotnet run --project src/AppsUsageCheck.App/AppsUsageCheck.App.csproj -- --minimized`
- Add EF migration: `dotnet ef migrations add <Name> --project src/AppsUsageCheck.Infrastructure --startup-project src/AppsUsageCheck.App`

## Expectations
- Prefer putting business logic in `Core`.
- Keep Windows-specific code in `Infrastructure` or `App`, not `Core`.
- When changing behavior, update or add tests if the change is testable.
- PostgreSQL default DB name is `apps_usage_check`; app startup applies pending EF Core migrations automatically.
- Auto-start uses `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` and launches with `--minimized`.
- Settings changes for polling interval, flush interval, connection string, and log level are written to the app's `appsettings.json`; those changes take effect after restart.
