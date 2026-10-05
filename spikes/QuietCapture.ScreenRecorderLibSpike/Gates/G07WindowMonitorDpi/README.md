# G0-7 — Window + Monitor + DPI

## Question

What are the verified Window, Monitor, Area-coordinate, multi-monitor, negative-coordinate, and mixed-DPI behaviors of the candidate ScreenRecorderLib path?

**Gate status:** NOT RUN

## Display snapshot

Every run records a display snapshot before and after capture. Each monitor entry includes:

- HMONITOR
- GDI device name, such as `\\.\DISPLAY1`
- ScreenRecorderLib friendly name when it can be matched
- display-device interface ID/string/key when available
- monitor bounds in virtual-screen physical pixels
- work-area bounds in virtual-screen physical pixels
- primary-monitor flag
- effective DPI X/Y
- scale percentage relative to 96 DPI
- whether the monitor begins at a negative X or Y coordinate

The harness obtains monitor/work rectangles from Win32 monitor enumeration. Target-window DPI is recorded separately after every scripted action.

## Capture modes

### Window

Uses a deterministic `G07TargetWindow` and `WindowRecordingSource(HWND)`. ScreenRecorderLib fixes this source to Windows Graphics Capture.

Available scripts:

- `NoActions`
- `Lifecycle`
- `CrossMonitor`

The Lifecycle script runs:

```text
Move
↓
Resize
↓
Minimize
↓
Restore
↓
Destroy
```

The CrossMonitor script moves the same HWND to another available monitor and then back to the selected monitor.

Each action has a scheduled offset, actual offset, before/after HWND snapshot, window physical-pixel rectangle, minimized/valid state, current monitor device, window DPI, and scale percentage.

### Monitor

Uses `DisplayRecordingSource` for the selected display.

The controller can select:

- `RecorderApi.DesktopDuplication`
- `RecorderApi.WindowsGraphicsCapture`

This allows G0-7 evidence to decide whether one display API should be retained for the production Monitor route.

### Area

Uses the same selectable display API plus `DisplayRecordingSource.SourceRect`.

The entered Area X/Y/Width/Height are recorded as selected-source-local physical pixels. The manifest also stores:

```text
ExpectedVirtualAreaRect =
    selected monitor virtual origin
    + source-local SourceRect
```

This is specifically useful on monitors whose virtual desktop origin is negative.

## Timed action timeline

Window actions use a monotonic `Stopwatch` starting when ScreenRecorderLib reaches `RecorderStatus.Recording`.

The manifest records:

- Recorder status events
- action scheduled offset
- action actual offset
- window snapshot before the action
- window snapshot after the action
- Stop request
- completion/failure
- final display snapshot

## Runtime matrix

### Window lifecycle

- [ ] **F1** — Move within the same monitor
- [ ] **F2** — Resize while recording
- [ ] **F3** — Minimize
- [ ] **F4** — Restore
- [ ] **F5** — Destroy target HWND
- [ ] **F6** — Move between monitors and back

Record whether capture follows the HWND, keeps a fixed or changing canvas, letterboxes/scales/crops, freezes, fails, or finalizes.

### Monitor route

Run on every available monitor where practical:

- [ ] **F7** — Desktop Duplication
- [ ] **F8** — Windows Graphics Capture
- [ ] **F9** — secondary/negative-coordinate monitor where available
- [ ] **F10** — display topology/resolution change during recording

### Area / coordinate diagnostics

- [ ] **F11** — primary monitor local SourceRect
- [ ] **F12** — WGC versus Desktop Duplication for the same SourceRect
- [ ] **F13** — local SourceRect on a monitor whose virtual origin is negative
- [ ] **F14** — boundary crop near right/bottom edge

### DPI

Repeat representative Window/Area cases with:

- [ ] 100% scaling
- [ ] 150% scaling
- [ ] mixed-DPI monitors
- [ ] window moved from one DPI monitor to another

Check that selection/capture geometry is explained by physical pixels and that target-window DPI changes follow monitor movement.

## Evidence per run

Default root:

`%TEMP%\QuietCaptureSpike\G0-7\<run-id>\`

Each run writes:

- `capture.mp4`
- `recorder.log`
- `run-manifest.json`

The manifest contains the full display snapshots, selected capture API, Area coordinate diagnostics, target HWND, action observations, recorder timeline, final status, and output size.

## Gate decision

PASS requires a documented production policy for Window, Monitor, and Area behavior across required Windows/DPI/topology cases.

PASS WITH WORKAROUND requires a bounded behavior such as safe-stop-on-resize/topology change, fixed output canvas with documented scaling, or one selected display API.

FAIL means the candidate route cannot support the v1 target semantics reliably.

Summarize reviewed evidence in `RESULTS.md` and `docs/technical-spike.md`.
