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

The G0-5 harness uses real top-level WPF StatusWindow and RecordingBorderWindow instances with `WindowStyle=None`, `AllowsTransparency=True`, `Topmost=True`, no taskbar entry, and native click-through/no-activate/tool-window extended styles.

For each run the harness calls `WindowInteropHelper.EnsureHandle()`, applies either `WDA_EXCLUDEFROMCAPTURE` or the `WDA_NONE` control value, verifies the value with `GetWindowDisplayAffinity`, starts ScreenRecorderLib, waits for `RecorderStatus.Recording`, and only then shows both overlay windows. After `Show()`, affinity is read again without reapplying it.

### Evidence

Harness prepared for three routes:

- Area — primary-display `DisplayRecordingSource` with explicit `SourceRect`;
- Monitor — primary-display `DisplayRecordingSource`;
- Window — a deterministic non-excluded in-process target captured with `WindowRecordingSource` / Windows Graphics Capture.

The E1–E6 matrix pairs `WDA_NONE` control runs with `WDA_EXCLUDEFROMCAPTURE` runs. Each run produces an MP4, ScreenRecorderLib log, and JSON manifest containing both overlay HWNDs, requested/verified affinity before Show, affinity re-read after Show, native extended styles, recorder lifecycle timestamps, capture target identity, and output size/existence. An optional external reference screenshot path is reserved in the manifest.

Runtime evidence is pending Windows execution using `Gates/G05CaptureExclusion/README.md`.

### Result

TBD after runtime validation.

### Architecture consequence

Do not freeze the final overlay implementation until G0-5 proves the actual StatusWindow and RecordingBorderWindow shapes stay visible to the user but absent from every production capture route that could otherwise include them. Control runs must separate display-affinity behavior from capture APIs that intrinsically ignore overlapping windows.

## G0-6 — Stability and Stop latency
**Status:** NOT RUN

The G0-6 harness uses a separate Controller and Recorder Worker process. Each Worker records the main monitor with fixed 30 FPS H.264, hardware encoding, ordinary MP4, and audio disabled so the stability experiment does not absorb G0-2/G0-3/G0-4 variables.

The Controller runs multiple Workers sequentially. After each Worker reaches `RecorderStatus.Recording`, it periodically samples the Worker process from outside:

- Working Set;
- Private Bytes;
- cumulative `Process.TotalProcessorTime`;
- CPU utilization derived from processor-time/wall-time deltas and normalized across logical processors;
- current output-file size;
- recording elapsed time.

Every sample is flushed immediately to a batch-level CSV. Per-run summaries record start/final/max memory, Private Bytes growth, an ordinary least-squares Private Bytes slope in MiB/hour, average/max CPU, final output size, Worker status, and Stop latency.

### Evidence

The Worker measures Stop latency with `Stopwatch.GetTimestamp()` immediately before `Recorder.Stop()` and again at entry to `OnRecordingComplete`. The Controller aggregates successful measured runs and calculates P50/P95 using linear interpolation.

A batch produces `batch-manifest.json`, `samples.csv`, and `summary.md`; each run directory retains its MP4, ScreenRecorderLib log, Worker manifest, ready marker, and stdout/stderr logs.

The planned runtime sequence is 3+ rounds × 30 minutes, 3+ rounds × 60 minutes, followed later by the 8-hour acceptance run using the same evidence format. GPU behavior and A/V sync remain explicit external runtime observations rather than inferred counters.

Runtime evidence is pending Windows execution using `Gates/G06StabilityStopLatency/README.md`.

### Result

TBD after runtime validation.

### Architecture consequence

Do not freeze the Core Stop timeout from the provisional 30-second value. Use observed Stop P50/P95 plus failure/timeout behavior from G0-6. Long-session acceptance must also satisfy the project memory-growth and finalization requirements before the ScreenRecorderLib route is frozen.

## G0-7 — Window, monitor, and DPI behavior
**Status:** NOT RUN

The G0-7 harness combines three diagnostic routes:

- Window — a deterministic `G07TargetWindow` captured by `WindowRecordingSource(HWND)`, which ScreenRecorderLib 7.0.1 fixes to Windows Graphics Capture;
- Monitor — selected `DisplayRecordingSource` with explicit `RecorderApi.DesktopDuplication` or `RecorderApi.WindowsGraphicsCapture`;
- Area — the same selectable display API with source-local physical-pixel `SourceRect`.

### Evidence

Every run captures monitor topology before and after recording. Each display snapshot records HMONITOR, GDI device name, display-device interface identity when available, virtual-screen monitor/work rectangles, primary-monitor status, effective DPI/scaling, and whether the monitor begins at a negative virtual X/Y coordinate.

Window mode supports timed monotonic scripts beginning when the recorder reaches `RecorderStatus.Recording`:

- Lifecycle — Move → Resize → Minimize → Restore → Destroy;
- CrossMonitor — move the same HWND to another monitor and then back;
- NoActions — fixed target baseline.

Each scripted action records scheduled/actual time, window HWND validity/minimized state, physical-pixel window rectangle, current monitor device, and `GetDpiForWindow` before/after evidence.

Area runs record both the source-local SourceRect and the expected virtual-desktop rectangle computed as selected-monitor virtual origin + local SourceRect. This isolates coordinate-transform questions on monitors with negative virtual coordinates.

Monitor and Area runs can switch ScreenRecorderLib's display `RecorderApi` between Desktop Duplication and Windows Graphics Capture so Phase 0 can select the production route from evidence.

Runtime evidence is pending Windows execution using `Gates/G07WindowMonitorDpi/README.md`.

### Result

TBD after runtime validation.

### Architecture consequence

Do not freeze Window resize/minimize/destroy/cross-monitor policy, Monitor/Area capture API, Area coordinate transform, or display-topology-change behavior until G0-7 evidence is reviewed. The final Core model remains physical-pixel based and the app manifest remains PerMonitorV2.

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
- Does WDA_EXCLUDEFROMCAPTURE remain applied after real transparent/topmost WPF overlays are shown?
- Which Area/Monitor capture path actually honors display affinity for these overlay HWNDs?
- Does the Window/WGC route exclude unrelated overlapping windows even in the WDA_NONE control run?
- What Stop timeout follows from observed G0-6 P50/P95 plus bounded failure margin?
- Does Private Bytes stabilize after warmup, and what is the measured MiB/hour slope in 30-minute, 60-minute, and 8-hour runs?
- Does WindowRecordingSource follow the same HWND reliably through move/resize and cross-monitor DPI changes?
- What raw behavior occurs on minimize and HWND destruction, and should Core safe-stop before/at those transitions?
- Which display RecorderApi should production retain for Monitor and Area capture?
- Are Area SourceRect values consistently source-local physical pixels, including on negative-origin monitors?
