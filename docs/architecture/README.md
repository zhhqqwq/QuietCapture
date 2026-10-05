# Architecture

The current architecture boundary is frozen at project level while backend-facing method signatures remain open until Phase 0 evidence is complete.

## Project dependency rule

~~~text
QuietCapture.UI.Wpf → QuietCapture.Core ← QuietCapture.Infrastructure.Windows
          ↑                    ↑                    ↑
          └──────────────── QuietCapture.App ──────┘
~~~

Allowed references:

- QuietCapture.UI.Wpf → QuietCapture.Core
- QuietCapture.Infrastructure.Windows → QuietCapture.Core
- QuietCapture.App → Core, UI.Wpf, Infrastructure.Windows

Disallowed references:

- QuietCapture.Core → Windows infrastructure or UI
- QuietCapture.UI.Wpf → QuietCapture.Infrastructure.Windows

## Responsibilities

Core owns application state, recording/session policies, capture-target models, output planning, recovery policy, settings models, and platform port definitions.

Infrastructure.Windows owns recorder/capture integration, NAudio/WASAPI integration, Win32 calls, display/window queries, filesystem implementation, power requests, and system events.

UI.Wpf owns WPF views, view models, selection UI, control bar, status UI, recording border, tray UI, and settings surfaces.

App owns composition, startup, dependency registration, recovery startup scan, and coordinated application shutdown.

## Phase 0 rule

Do not freeze backend method signatures to match an assumed ScreenRecorderLib model. First measure the backend's real behavior in spikes/, then create production contracts from the resulting requirements.


## Architecture Freeze inputs

Phase 0 runtime evidence is converted into production architecture through:

- `docs/technical-spike.md`
- `docs/decisions/0001-recording-backend-route.md`
- `docs/decisions/0002-media-container-framerate.md`
- `docs/decisions/0003-audio-device-binding.md`
- `docs/decisions/0004-capture-exclusion.md`
- `docs/decisions/0005-stop-timeout.md`
- `docs/decisions/0006-window-monitor-dpi-policy.md`
- `docs/architecture-freeze-template.md`

Until those ADRs are finalized from reviewed runtime evidence, this file documents only the already-frozen project boundaries, not the final backend-facing contracts.
