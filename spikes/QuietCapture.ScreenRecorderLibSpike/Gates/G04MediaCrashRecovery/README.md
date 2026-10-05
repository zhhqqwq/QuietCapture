# G0-4 — Media + Crash Recovery

## Question

Which combination of conventional/fragmented MP4 and fixed/non-fixed framerate gives QuietCapture acceptable normal-finalization behavior, interrupted-process survivability, timestamp behavior, and player/editor compatibility?

**Gate status:** NOT RUN

## Process model

G0-4 separates orchestration from recording:

```text
G0-4 Controller
    ↓ launches
Recorder Worker process
    ↓
ScreenRecorderLib → recording.partial.mp4
```

The Worker writes `worker-manifest.json` before and during recording. The Controller writes `controller-manifest.json` and owns the final observation after the Worker exits or is killed.

For Kill runs, the Controller waits until the Worker has entered ScreenRecorderLib `Recording` status and created `recording-ready.flag`. It then waits the configured recording interval and terminates the Worker process. The resulting MP4 is left untouched.

For Normal Stop runs, the Worker records for the configured interval, calls `Recorder.Stop()`, waits for completion, and exits normally.

## Matrix

Run every combination:

| Fragmented MP4 | Fixed framerate | Normal Stop | Kill |
| --- | --- | --- | --- |
| false | false | D1 | D2 |
| false | true | D3 | D4 |
| true | false | D5 | D6 |
| true | true | D7 | D8 |

Repeat the matrix with:

- mostly static desktop content;
- continuously changing/moving content;
- static → dynamic → static content.

The initial harness keeps audio disabled so container/framerate behavior is isolated from audio-device behavior. G0-2/G0-3 own audio-source validation.

## Evidence per run

Each run directory contains:

- `recording.partial.mp4` — raw media output; never auto-repaired or rewritten by the harness;
- `recorder.log` — ScreenRecorderLib debug log;
- `worker-manifest.json` — Worker lifecycle evidence written before and during recording;
- `controller-manifest.json` — launch/termination mode, Worker PID/exit code, and post-exit file existence/size;
- `recording-ready.flag` — proves the Worker reached RecorderStatus.Recording before the timed interval.

Default root:

`%TEMP%\QuietCaptureSpike\G0-4\`

## External media analysis to perform later

For every retained MP4, record at least:

- whether Windows playback succeeds;
- whether a second independent player succeeds;
- seek behavior;
- media duration;
- frame count when available;
- sample PTS/duration;
- `r_frame_rate`;
- `avg_frame_rate`;
- frame interval regularity;
- whether the target editor can import, seek, and export;
- whether the file remains usable after Kill.

ffprobe/MediaInfo may be used as development tools. Raw killed-process files must remain untouched so analysis does not accidentally measure a repaired copy.

## Gate decision

PASS requires a documented configuration whose normal-stop output is compatible with the target playback/editor workflow and whose interrupted-process behavior satisfies the project's recovery expectations.

PASS WITH WORKAROUND requires a bounded post-normal-stop finalize/remux workflow or another documented constraint.

FAIL means the candidate backend/container strategy cannot provide an acceptable basis for v1 media safety.

Summarize reviewed evidence in `RESULTS.md` and `docs/technical-spike.md`.
