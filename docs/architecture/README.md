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


## Phase 1 Core foundation scaffold

The following backend-independent Core rules are now implemented before backend Architecture Freeze:

- physical-pixel `PixelRect` / `PixelSize` models and explicit even-dimension normalization;
- Area / Window / Monitor capture-target domain models;
- resolved per-session `RecordingOptions` with concrete audio-device IDs when audio is enabled;
- `AppStateMachine` as the public application-flow state source;
- Starting-time pending Stop represented without a direct `Starting → Stopping` transition;
- persistent Session status/lifecycle rules with no public status setter;
- same-output-tree working/final path planning models;
- timestamp filename collision policy with `_001`, `_002`, ... suffixes.

Not implemented yet:

- ScreenRecorderLib-facing recording Ports or method signatures;
- production SessionManager/RecorderService orchestration;
- filesystem atomic reservation/write Ports;
- backend Stop timeout value;
- CFR/fragmented-MP4 backend configuration;
- Window/Monitor/DPI policies that still depend on Phase 0 runtime evidence.


## Phase 1 Session + Storage foundation

The Core storage boundary now defines `IFileSystem` for volume queries, atomic final-path reservation, atomic small-metadata writes, and safe cleanup of unused placeholders.

`OutputPlanner` rejects non-writable output volumes and FAT32, verifies that the derived Session working directory resolves to the same volume identity as the final output, and reserves the first available timestamp filename atomically.

A successful final-path reservation is a zero-byte placeholder. Later finalize code must replace that placeholder through the filesystem abstraction rather than selecting a new name after recording has started.

`SessionManager.CreateSession` now performs backend-independent creation in this order:

1. reserve final output path;
2. create same-volume Session working directory;
3. create `SessionMetadata` in `Created`;
4. atomically write `session.json`.

If creation fails, only an unused empty final reservation is released. Non-empty `recording.partial.mp4` media is preserved.

`SessionMetadataDocument` is a versioned primitive persistence shape. `RecoveryIndexEntry` stores only Session ID and working directory.

Still deferred: Windows filesystem implementation, final media move/replace, recovery scanning, and backend Start/Stop orchestration.
