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
| Audio devices | Not used by G0-1 |

## G0-1 — Area capture
**Status:** NOT RUN

The G0-1 harness is now in spikes/QuietCapture.ScreenRecorderLibSpike. It uses one DisplayRecordingSource with SourceRect, fixed output dimensions matching the rectangle, 30 FPS fixed-framerate H.264, audio disabled, and per-run ScreenRecorderLib debug logging.

### Evidence

Harness prepared. Runtime evidence is pending Windows execution using the matrix in spikes/QuietCapture.ScreenRecorderLibSpike/Gates/G01AreaCapture/README.md.

### Result

TBD after runtime validation.

### Architecture consequence

Do not define the production area-capture backend contract until the test establishes ScreenRect coordinate semantics, boundary behavior, output dimensions, cursor behavior, and Stop reliability.

## G0-2 — System audio
**Status:** NOT RUN

Validate system-audio capture, silent periods, audio-track presence, and device-change behavior.

### Evidence
TBD

### Result
TBD

### Architecture consequence
TBD

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
