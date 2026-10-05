# QuietCapture Technical Spike

## Status

Phase 0 is active. This document is the authoritative record of G0 evidence, results, and architecture consequences.

Allowed gate states: NOT RUN, PASS, PASS WITH WORKAROUND, FAIL.

## Environment

| Item | Value |
| --- | --- |
| Date | 2026-10-05 |
| Windows version/build | TBD on test machine |
| GPU / driver | TBD on test machine |
| Display topology / DPI | TBD on test machine |
| .NET SDK | .NET 8 |
| ScreenRecorderLib version | 7.0.1 |
| Audio devices | TBD on test machine |

## G0-1 — Area capture
**Status:** NOT RUN

The G0-1 harness is in `spikes/QuietCapture.ScreenRecorderLibSpike`. It uses one `DisplayRecordingSource` with `SourceRect`, fixed output dimensions matching the rectangle, 30 FPS fixed-framerate H.264, audio disabled, and per-run ScreenRecorderLib plus JSON evidence.

### Evidence

Harness prepared and CI-compilable. Runtime evidence is pending Windows execution using the matrix in `Gates/G01AreaCapture/README.md`.

### Result

TBD after runtime validation.

### Architecture consequence

Do not define the production area-capture backend contract until the test establishes ScreenRect coordinate semantics, boundary behavior, output dimensions, cursor behavior, and Stop reliability.

## G0-2 — System audio
**Status:** NOT RUN

The G0-2 harness is prepared using the ScreenRecorderLib 7.0.1 audio-source model. It enumerates loopback devices with `Recorder.GetSystemAudioLoopbackDevices()`, binds the selected concrete `DeviceName` through `LoopbackAudioSource`, records full-display video plus system audio, and enables audio-packet preview for packet/byte evidence.

### Evidence

Harness prepared and CI-compilable. Each run will write MP4 output, a ScreenRecorderLib debug log, and a `*.g0-2.json` report containing the selected device ID, device enumeration snapshot, recorder state changes, and audio packet/byte counts.

Runtime evidence is pending Windows execution using the matrix in `Gates/G02SystemAudio/README.md`.

### Result

TBD after runtime validation.

### Architecture consequence

The candidate production policy is to resolve a user preference to one concrete output-device ID before Start and keep that ID fixed for the session. Do not freeze the Core audio port or device-loss policy until G0-2/G0-3 runtime evidence verifies how ScreenRecorderLib behaves.

## G0-3 — System audio + microphone
**Status:** NOT RUN

The G0-3 harness enumerates both loopback output devices and capture/microphone devices, binds one concrete ID for each before Start, creates one `LoopbackAudioSource` and one `CaptureAudioSource`, and records them together in one audio track. Per-source ScreenRecorderLib IDs are retained so audio packet evidence can be attributed to loopback, microphone, or unknown sources.

### Evidence

Harness prepared and CI-compilable. Each run will write MP4 output, a ScreenRecorderLib debug log, and a `*.g0-3.json` report containing both device IDs, both source IDs, enumeration snapshots, state changes, mixed packet/byte counts, and source-specific packet/byte counts.

A/V sync is not inferred from callback arrival times. Runtime validation must inspect the resulting media with an audible/visible reference event or media-analysis tooling.

Runtime evidence is pending Windows execution using the matrix in `Gates/G03SystemAudioMicrophone/README.md`.

### Result

TBD after runtime validation.

### Architecture consequence

The candidate session model resolves both system-audio and microphone preferences to concrete IDs before Start and keeps those IDs fixed for the session. Do not freeze device-loss handling, source-mix assumptions, or A/V sync guarantees until G0-3 runtime evidence is reviewed.

## G0-4 — Media and crash recovery
**Status:** NOT RUN

The G0-4 harness uses a separate Controller and Recorder Worker process. The Controller selects `IsFragmentedMp4Enabled`, `IsFixedFramerate`, run duration, and Normal Stop versus Kill. The Worker records the main display to `recording.partial.mp4`; Kill runs terminate the Worker from the Controller after the Worker has reached `RecorderStatus.Recording`.

### Evidence

Harness prepared. Every run keeps the raw MP4 untouched and records both `worker-manifest.json` and `controller-manifest.json`, plus the ScreenRecorderLib log and a recording-ready marker. The Controller records Worker PID/exit code, whether it performed the Kill, and post-exit output existence/size.

The planned matrix covers conventional/fragmented MP4 × fixed/non-fixed framerate × Normal Stop/Kill, repeated for static, dynamic, and static→dynamic desktop content.

Runtime media analysis will later record playback, seeking, duration, frame counts, PTS/sample duration, `r_frame_rate`, `avg_frame_rate`, frame-interval regularity, editor import/seek/export, and killed-process survivability.

### Result

TBD after runtime validation.

### Architecture consequence

Do not freeze fragmented-MP4 policy, fixed-framerate policy, remux/finalization requirements, or crash-recovery media behavior until G0-4 runtime evidence is reviewed. Raw interrupted files must remain untouched during initial analysis.

## G0-5 — Capture exclusion
**Status:** NOT RUN

Validate real StatusWindow and RecordingBorder window shapes with WDA_EXCLUDEFROMCAPTURE across required area, window, and monitor capture paths.

### Evidence
TBD

### Result
TBD

### Architecture consequence
TBD

## G0-6 — Stability and Stop latency
**Status:** NOT RUN

Run 30–60 minute sessions and measure CPU, GPU, memory, output growth, A/V sync, and Stop duration. Record Stop P50 and P95.

### Evidence
TBD

### Result
TBD

### Architecture consequence
TBD

## G0-7 — Window, monitor, and DPI behavior
**Status:** NOT RUN

Validate move, resize, minimize, restore, destroy, multi-monitor coordinates, negative coordinates, and mixed-DPI behavior.

### Evidence
TBD

### Result
TBD

### Architecture consequence
TBD

## Architecture decisions produced by Phase 0
TBD

## Open questions

- Are DisplayRecordingSource.SourceRect X/Y values source-local or virtual-desktop coordinates for every display/API combination?
- Are there alignment constraints beyond even output width/height?
- Does normal Stop remain reliable across tested rectangles and display configurations?
- Does a selected loopback device remain bound if the Windows default output device changes during an active recording?
- How does ScreenRecorderLib report an unavailable or removed loopback device?
- Does a selected microphone remain bound if the Windows default input device changes during an active recording?
- How does ScreenRecorderLib report microphone removal or privacy/access failure during an active recording?
- Does the library consistently mix loopback + microphone into one track with acceptable A/V sync?
- Which fragmented/fixed-framerate combination gives acceptable normal MP4 compatibility?
- Which configuration leaves the most useful raw media after abrupt process termination?
- Is a post-normal-stop remux/finalize step required for editor compatibility?
