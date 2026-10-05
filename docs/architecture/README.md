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
