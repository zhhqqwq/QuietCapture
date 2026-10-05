# G0-6 — Stability + Stop Latency

## Question

Can the candidate ScreenRecorderLib path record for long sessions without crash, hang, unbounded memory growth, or failed finalization, and what Stop timeout is supported by observed Stop→RecordingComplete latency?

**Gate status:** NOT RUN

## Process model

G0-6 separates measurement from recording:

```text
G0-6 Controller
    ↓ launches sequentially
Recorder Worker
    ↓
ScreenRecorderLib → recording.mp4
```

The Worker owns Recorder lifecycle. The Controller observes the Worker process from outside and records resource/file samples.

## Worker configuration

The initial stability harness fixes unrelated variables:

- Main monitor capture
- H.264 MP4
- 30 FPS
- fixed framerate enabled
- hardware encoding enabled
- fragmented MP4 disabled
- audio disabled

G0-2/G0-3 own audio-device validation. G0-4 owns MP4/framerate/crash configuration comparison.

## Periodic sampling

Sampling starts after the Worker creates `recording-ready.flag`, proving ScreenRecorderLib reached `RecorderStatus.Recording`.

Each sample records:

- UTC timestamp
- elapsed recording time
- Working Set bytes
- Private Bytes
- cumulative `Process.TotalProcessorTime`
- CPU utilization derived from processor-time/wall-time deltas and normalized by logical processor count
- current MP4 file size

The Controller writes every sample immediately to the batch-level `samples.csv`.

Private-memory growth is summarized both as start→final delta and an ordinary least-squares slope in MiB/hour.

## Stop latency

The Worker records two monotonic timestamps with `Stopwatch.GetTimestamp()`:

1. immediately before calling `Recorder.Stop()`
2. at entry to the `OnRecordingComplete` callback

The difference is the G0-6 Stop→Complete latency for that run.

The Controller aggregates successful measured runs and calculates P50 and P95 using linear interpolation across sorted values.

## Batch evidence

Default root:

`%TEMP%\QuietCaptureSpike\G0-6\<batch-id>\`

Batch files:

- `batch-manifest.json`
- `samples.csv`
- `summary.md`

Each `run-NNN\` directory contains:

- `recording.mp4`
- `recorder.log`
- `worker-manifest.json`
- `recording-ready.flag`
- `worker.stdout.log`
- `worker.stderr.log`

## Runtime plan

Initial execution:

- 3+ rounds × 30 minutes
- 3+ rounds × 60 minutes

Later acceptance:

- 8-hour run(s) using the same sampling format

During real-machine validation also record GPU utilization/encoder behavior and A/V sync using external tools or the later audio-enabled acceptance path. The in-process harness does not invent GPU metrics from unrelated counters.

## Baseline observations to evaluate

The engineering baseline currently calls for investigating/failing runs that show sustained unbounded Private Bytes growth after warmup, including:

- more than 1 GiB growth from hour 1 to hour 8; or
- more than 100 MiB/hour average during the last 4 hours.

G0-6 also records actual Stop latency so the final Core Stop timeout can be frozen from evidence rather than from the provisional timeout.

## Gate decision

PASS requires repeated long sessions without crash/hang/unhandled failure, acceptable memory/file growth, playable/finalized output, and Stop completion within the evidence-backed timeout.

PASS WITH WORKAROUND requires a bounded backend/platform workaround that preserves the v1 lifecycle.

FAIL means the candidate recording path cannot satisfy long-session or bounded-Stop requirements.

Summarize reviewed evidence in `RESULTS.md` and `docs/technical-spike.md`.
