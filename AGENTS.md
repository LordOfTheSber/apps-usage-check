# AGENTS

## Behavioral guidelines

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

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