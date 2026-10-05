# G0-5 — Capture Exclusion

## Question

Can QuietCapture's real StatusWindow and RecordingBorderWindow remain visible to the user while being absent from the required Area, Monitor, and Window capture paths?

**Gate status:** NOT RUN

## Overlay preparation order

The harness uses the production-intended order:

```text
construct WPF top-level window
    ↓
WindowInteropHelper.EnsureHandle()
    ↓
apply click-through / no-activate / tool-window extended styles
    ↓
SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)
    ↓
GetWindowDisplayAffinity() verification
    ↓
start Recorder
    ↓
wait for RecorderStatus.Recording
    ↓
Show StatusWindow + RecordingBorderWindow
    ↓
DwmFlush()
    ↓
read affinity again without reapplying it
```

The Off control group uses the same HWND/style flow but sets `WDA_NONE`.

## Overlay window shape

Both windows are real WPF top-level windows with:

- `WindowStyle=None`
- `AllowsTransparency=True`
- `Topmost=True`
- `ShowInTaskbar=False`
- `ShowActivated=False`
- native `WS_EX_TRANSPARENT`
- native `WS_EX_NOACTIVATE`
- native `WS_EX_TOOLWINDOW`

StatusWindow has an obvious magenta marker and running timer. RecordingBorderWindow has an obvious green border and label so inclusion is easy to detect in recorded evidence.

## Capture routes

G0-5 intentionally isolates capture-exclusion behavior from G0-7 topology/DPI work:

- **Area** — primary display + explicit SourceRect, default `0,0,1280,720`
- **Monitor** — primary display through `DisplayRecordingSource`
- **Window** — a deterministic in-process, non-excluded `G05CaptureTargetWindow` through `WindowRecordingSource`

The Window route uses ScreenRecorderLib's Windows Graphics Capture window source. The Area/Monitor routes use the display source selected by ScreenRecorderLib.

## Test matrix

Run control and exclusion pairs:

| Case | Capture path | Affinity | Expected evidence |
| --- | --- | --- | --- |
| E1 | Area | WDA_NONE | overlays should be visible if this path captures ordinary top-level overlays |
| E2 | Area | WDA_EXCLUDEFROMCAPTURE | overlays absent from recording while visible on screen |
| E3 | Monitor | WDA_NONE | overlays should be visible if this path captures ordinary top-level overlays |
| E4 | Monitor | WDA_EXCLUDEFROMCAPTURE | overlays absent from recording while visible on screen |
| E5 | Window / WGC | WDA_NONE | establishes whether unrelated overlapping windows appear in this capture path |
| E6 | Window / WGC | WDA_EXCLUDEFROMCAPTURE | overlays absent from final window capture |

The control run matters: if a capture API never includes overlapping windows even with `WDA_NONE`, that is a property of the capture path and must not be misreported as proof that display affinity caused the exclusion.

## Evidence per run

Default root:

`%TEMP%\QuietCaptureSpike\G0-5\<run-id>\`

Each run writes:

- `capture.mp4`
- `recorder.log`
- `run-manifest.json`
- optional external `reference-screen.png`

The manifest records:

- capture mode and SourceRect;
- primary display identity;
- target HWND for Window mode;
- StatusWindow and BorderWindow HWNDs;
- requested affinity;
- SetWindowDisplayAffinity return/error;
- GetWindowDisplayAffinity return/error and verified value before Show;
- extended window styles before/after preparation;
- affinity re-read after Show;
- Record/Recording/OverlayShow/Stop/Complete timestamps;
- output existence and size.

A reference screenshot is optional evidence showing that the overlays were visibly present on the user's desktop during the run. The recording itself determines whether those windows entered the captured media.

## Runtime validation

For every E1–E6 run inspect:

- overlays visible on the real desktop during recording;
- StatusWindow present/absent in recorded MP4;
- RecordingBorderWindow present/absent in recorded MP4;
- first captured frames for accidental overlay inclusion;
- normal Stop/finalization;
- manifest affinity values;
- ScreenRecorderLib log.

Repeat on the Windows versions and final overlay properties selected for release.

## Gate decision

PASS requires StatusWindow and RecordingBorderWindow to be absent from every production capture path that would otherwise include them, with affinity successfully applied before Show and retained after Show.

PASS WITH WORKAROUND requires a bounded capture-path-specific adjustment that preserves the product behavior.

FAIL means the selected capture route cannot reliably keep required recording UI out of captured media.

Summarize reviewed evidence in `RESULTS.md` and `docs/technical-spike.md`.
