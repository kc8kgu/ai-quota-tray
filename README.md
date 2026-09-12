# AIQuotaTray

AIQuotaTray is a local-only Windows tray app that shows the remaining subscription allowance and reset times for Codex and Claude Code.

## Current scope

- Windows 10/11 x64
- One active ChatGPT-backed Codex account
- One active Claude.ai Pro/Max account
- Codex data from the official local App Server
- Claude data from the official Claude Code status-line payload
- No API billing, telemetry, alerts, or historical tracking

The tray icon uses the lowest trustworthy remaining value: green above 50%, amber from 20% through 50%, red below 20%, and gray when data is stale or unavailable.

## Build and test

Prerequisites: the .NET 8 SDK or newer on Windows.

```powershell
dotnet build .\AIQuotaTray.sln
dotnet run --project .\tests\AIQuotaTray.Tests\AIQuotaTray.Tests.csproj
```

Publish the self-contained x64 app:

```powershell
.\scripts\publish.ps1
```

The output is written to `artifacts\publish`. Compile `installer\AIQuotaTray.iss` with Inno Setup 6 to produce the per-user installer.

## Provider setup

AIQuotaTray starts and manages `codex app-server --stdio` itself. It uses Codex's managed ChatGPT authentication and never reads or stores Codex credentials.

On first run, AIQuotaTray adds a `statusLine` command to `%USERPROFILE%\.claude\settings.json`. Claude Code sends subscription-window data to that command after a normal model response. The bridge stores only the latest snapshot under `%LOCALAPPDATA%\AIQuotaTray` and prints a compact status line inside Claude Code. It does not make model requests or consume tokens.

If Claude's recorded reset passes without a new snapshot, AIQuotaTray marks the data stale instead of assuming the allowance returned to 100%. Use **Open Claude Code** and complete one normal response to refresh it.

## Local files and privacy

Settings and latest snapshots live under `%LOCALAPPDATA%\AIQuotaTray`. AIQuotaTray has no telemetry and makes no direct network requests. It keeps no usage history and stores no provider credentials.

The Claude settings file is backed up once as `settings.json.aiquotatray.backup`. Uninstall runs the app's removal mode, which removes AIQuotaTray's status line while preserving subsequent unrelated Claude settings changes.

See [PLAN.md](PLAN.md) for the agreed product contract and deferred directions.
