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

Harness prepared. Each run will write MP4 output, a ScreenRecorderLib debug log, and a `*.g0-2.json` report containing the selected device ID, device enumeration snapshot, recorder state changes, and audio packet/byte counts.

Runtime evidence is pending Windows execution using the matrix in `Gates/G02SystemAudio/README.md`.

### Result

TBD after runtime validation.

### Architecture consequence

The candidate production policy is to resolve a user preference to one concrete output-device ID before Start and keep that ID fixed for the session. Do not freeze the Core audio port or device-loss policy until G0-2/G0-3 runtime evidence verifies how ScreenRecorderLib behaves.

## G0-3 — System audio + microphone
**Status:** NOT RUN

Validate simultaneous system audio and microphone capture, selected device identity, and A/V sync.

### Evidence
TBD

### Result
TBD

### Architecture consequence
TBD

## G0-4 — Media and crash recovery
**Status:** NOT RUN

Compare normal Stop and interrupted-process output, including conventional MP4 versus fragmented MP4 and CFR/VFR behavior where supported.

### Evidence
TBD

### Result
TBD

### Architecture consequence
TBD

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
