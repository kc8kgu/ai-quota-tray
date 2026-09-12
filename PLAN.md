# AIQuotaTray implementation plan

## Product contract

AIQuotaTray is a local-only Windows 10/11 x64 tray application for one active Codex subscription and one active Claude.ai Pro/Max subscription.

- Show every available usage window with remaining percentage emphasized, used percentage secondary, reset time, freshness, and provider health.
- Color the tray gauge using the lowest remaining allowance: green above 50%, amber from 20% through 50%, red below 20%, and gray when no trustworthy value is available.
- Keep providers independent so one failure never takes down the application.
- Do not store credentials, contact provider APIs directly, collect telemetry, retain history, or send threshold notifications in version one.
- Start with Windows, show setup on first run, start silently on later sign-ins, hide the window when closed, and exit only from the tray menu.

## Architecture

1. **Core model** - provider snapshots, usage windows, health/freshness rules, protocol parsers, and display formatting with no UI dependency.
2. **Codex provider** - manage `codex app-server --stdio`, initialize JSON-RPC, read `account/rateLimits/read`, listen for updates, reconcile every minute, reconnect, and launch managed ChatGPT sign-in when requested.
3. **Claude provider** - configure Claude Code's documented `statusLine` command to call AIQuotaTray in bridge mode. The bridge atomically saves the newest five-hour/seven-day snapshot and prints a compact remaining-usage line.
4. **Local state** - latest snapshots and settings only under `%LOCALAPPDATA%\AIQuotaTray`; no secrets or activity history.
5. **Windows shell** - WinForms compact window, dynamic gauge icon, tooltip, context menu, light/dark response, high-DPI layout, startup registration, and a single-instance guard.
6. **Packaging** - self-contained x64 publish plus a per-user Inno Setup definition. Automatic updates remain future work.

## Delivery checkpoints

- [x] Confirm scope and provider data sources.
- [x] Scaffold the solution and core model.
- [x] Implement and test Codex protocol parsing/client lifecycle.
- [x] Implement and test Claude bridge/configuration and atomic latest-wins cache.
- [x] Implement tray shell, compact window, startup, theme, and degraded states.
- [x] Add packaging and operator documentation.
- [ ] Build, run automated tests, publish, and perform a local smoke test.

## Deferred directions

- Configurable Windows notifications
- Usage history and charts
- Multiple accounts
- ARM64 builds
- Automatic updates
- Additional AI providers
