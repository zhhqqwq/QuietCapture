# QuietCapture Technical Spike

## Status

Phase 0 has not started. This document is the authoritative record of G0 evidence, results, and architecture consequences.

Allowed gate states: NOT RUN, PASS, PASS WITH WORKAROUND, FAIL.

## Environment

| Item | Value |
| --- | --- |
| Date | TBD |
| Windows version/build | TBD |
| GPU / driver | TBD |
| Display topology / DPI | TBD |
| .NET SDK | TBD |
| ScreenRecorderLib version | TBD |
| Audio devices | TBD |

## G0-1 — Area capture
**Status:** NOT RUN

Validate arbitrary single-monitor physical-pixel rectangles, even dimensions, cursor behavior, MP4 output, and basic frame pacing.

### Evidence
TBD

### Result
TBD

### Architecture consequence
TBD

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
TBD
