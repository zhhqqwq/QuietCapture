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


## Phase 1 Recovery foundation

Recovery discovery is now a read-only Core operation.

`RecoveryService` merges two discovery sources by normalized Session working directory:

- entries from the small `recovery-index.json`;
- direct Session directories under known `.screenrecorder/sessions` roots.

Each discovered directory is classified as one of:

- `NoRecoveryRequired`;
- `Interrupted`;
- `StopFailed`;
- `Orphaned`.

Classification rules currently treat persisted `Created`, `Starting`, `Recording`, `Finalizing`, and `Interrupted` residuals as Interrupted recovery candidates. Persisted StopFailed remains StopFailed. Missing/corrupt/invalid metadata and identity mismatches are Orphaned. Completed/FailedToStart sessions need no recovery unless a non-empty partial file remains, which is treated as an orphaned inconsistency.

Recovery scanning never deletes or repairs media. In particular, non-empty `recording.partial.mp4` files are preserved byte-for-byte by discovery.

`SessionDocumentValidator` validates the versioned persistence document before classification, including Session identity/status, working/temp paths, capture-target shape, output geometry, frame rate, quality ID, and concrete audio-device IDs when enabled.

`RecoveryIndexJson` persists only a schema version plus Session ID / working-directory pairs.

Still deferred: writing/updating the recovery index from the application lifecycle, Windows filesystem implementation, user-facing recovery actions, media repair/remux, and cleanup policy for `NoRecoveryRequired` working directories.


## Phase 1 Windows Storage infrastructure

`QuietCapture.Infrastructure.Windows.Storage` now implements the Core filesystem contract without introducing any recording-backend dependency.

### Final-path reservation

`WindowsFileSystem.TryReserveFile` uses `FileMode.CreateNew`. The filesystem therefore decides the winner atomically when multiple sessions compete for the same final path; an existing path is never silently overwritten.

### Atomic text metadata

`AtomicTextFileWriter` writes UTF-8 metadata to a unique temporary file in the destination directory, flushes the file to disk, and then uses a same-directory Windows rename/replace with write-through semantics. A failed replace removes the temporary file while leaving the previous destination file intact.

This mechanism is intended for small durable metadata such as `session.json` and `recovery-index.json`, not for MP4 finalization.

### Volume identity

`VolumeInfoResolver` resolves the containing Windows volume from the nearest existing ancestor of a prospective path. It records:

- stable volume GUID when Windows exposes one, with mount-point/serial fallback;
- filesystem name;
- free bytes available to the current caller;
- read-only-volume state.

This allows Core same-volume planning to compare actual mounted volumes instead of only drive-letter strings.

### Safe cleanup and discovery

The Windows adapter implements direct-child directory/file enumeration and only deletes a reserved file when it is still zero bytes. Empty-directory cleanup is non-recursive.

Still deferred:

- partial-media → final-media move/rename policy;
- recovery-index lifecycle writes from application orchestration;
- cleanup of completed Session working directories;
- recording backend integration.


## Phase 1 Session Persistence lifecycle

Session lifecycle persistence now separates per-Session truth from the recovery discovery index.

### Ordering

Created Session registration is:

1. atomically write `session.json`;
2. atomically add the Session to `recovery-index.json`.

A crash between those writes leaves an unindexed Session directory, which direct recovery directory scanning can still discover.

State transitions are persisted as a proposed next-state document before the in-memory `SessionMetadata` advances. If the metadata write fails, the domain object remains in its previous state.

For safe-clean terminal states (`Completed` and `FailedToStart`), persistence order is:

1. atomically write the terminal `session.json`;
2. advance the in-memory Session;
3. atomically remove the recovery-index entry.

A crash or index-write failure after step 1 can leave a stale index entry, but recovery classification uses the terminal `session.json` as authoritative and reports `NoRecoveryRequired`.

Recovery-relevant terminal states (`StopFailed`, `Interrupted`, and `Orphaned`) remain in the recovery index.

### Stores

`SessionStore` owns versioned `session.json` persistence.

`RecoveryIndexStore` owns serialized, in-process synchronized read/modify/write of `recovery-index.json`.

`SessionPersistenceService` coordinates lifecycle ordering between those stores and the mutable domain Session.

`SessionManager` now registers a Created Session through `SessionPersistenceService`. If index registration fails after `session.json` is durable, the working directory and zero-byte final-path reservation are preserved for recovery rather than cleaned as if creation never happened.

Still deferred: application composition of the recovery-index path, multi-process coordination, final-media publication, and recording-backend lifecycle orchestration.


## Phase 1 Media Publication + Session Cleanup foundation

Successful media publication is now a backend-independent Core operation performed while a Session is `Finalizing`.

`MediaPublisher` requires all of the following before it moves media:

- non-empty `recording.partial.mp4`;
- an existing zero-byte final-path reservation;
- source and final paths on the same resolved volume.

If any prerequisite fails, the partial file is left in place. A filesystem move failure is reported as a failed publication and must preserve the partial media.

The filesystem contract now includes `MoveFileReplacingEmptyReservation`. The Windows implementation locks and revalidates the zero-byte reservation, consumes only that empty placeholder, then performs a same-volume `MoveFileEx(WRITE_THROUGH)` without replace-existing semantics. If the move fails, the source partial remains and the adapter best-effort recreates the empty reservation. If another file appears at the final path during the narrow reservation-to-rename window, the no-replace move fails rather than overwriting it. Cross-volume copy fallback is not enabled.

`SessionCleanupPolicy` only cleans safe terminal Sessions after recovery-index removal is complete:

- `Completed` requires the partial file to be absent and the final media to exist with non-zero length;
- `FailedToStart` may release only empty partial/final placeholders;
- `StopFailed`, `Interrupted`, and `Orphaned` are never automatically cleaned.

Cleanup deletes `session.json` and then attempts non-recursive working-directory deletion. Unknown files keep the directory present.

The intended successful finalization order is therefore:

1. Session is `Finalizing`;
2. publish partial media into the reserved final path;
3. atomically persist `Completed`;
4. remove the recovery-index entry;
5. clean Session metadata/empty working directory.

If recovery-index removal fails after `Completed` is durable, cleanup refuses to run until the stale index entry is resolved.

Still deferred: backend-specific finalization/remux before publication, cleanup scheduling/orchestration, and release-time media compatibility policy.
