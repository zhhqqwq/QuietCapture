# ScreenRecorderLib Technical Spike

This project is the Phase 0 experiment harness for QuietCapture.

It is intentionally isolated from the production projects. Spike code may call third-party or Windows APIs directly so that the experiment measures their real behavior before QuietCapture freezes production abstractions.

## Phase 0 launcher

Use the unified launcher to inspect or run the frozen Phase 0 sequence:

```powershell
.\run-phase0.ps1 -Gate list
.\run-phase0.ps1 -Gate g0-0
.\run-phase0.ps1 -Gate g0-4
.\run-phase0.ps1 -Gate all
```

`g0-0` performs the clean-machine prerequisite check. `all` runs that prerequisite first, then launches G0-1 through G0-7 sequentially. No launcher mode changes a gate result automatically.

See `docs/phase0-execution-guide.md` for the evidence and decision procedure.

## Prepared harnesses

- `run-g0-1.ps1` — area capture using an explicit display and SourceRect.
- `run-g0-2.ps1` — full-display video plus one explicitly selected system-audio loopback device.
- `run-g0-3.ps1` — full-display video plus one explicitly selected loopback device and one explicitly selected microphone/capture device.
- `run-g0-4.ps1` — Controller/Worker media experiment for fragmented MP4, fixed-framerate behavior, Normal Stop, and killed-process output.
- `run-g0-5.ps1` — real StatusWindow/RecordingBorderWindow capture-exclusion experiment across Area, Monitor, and Window routes.
- `run-g0-6.ps1` — multi-round Controller/Worker stability sampling and Stop-latency aggregation.
- `run-g0-7.ps1` — Window lifecycle, display API, virtual-coordinate, multi-monitor, and DPI diagnostics.

A prepared harness does not change a gate result. Phase 0 Harness Freeze is complete; runtime-dependent gates remain NOT RUN until reviewed Windows evidence exists.

## G0 gates

| Gate | Question | Status |
| --- | --- | --- |
| G0-1 | Can the selected backend reliably capture an arbitrary single-monitor area to MP4? | NOT RUN |
| G0-2 | Can it reliably record system audio from a fixed output device? | NOT RUN |
| G0-3 | Can fixed system-audio and microphone devices be recorded together with acceptable sync? | NOT RUN |
| G0-4 | Which MP4/framerate configuration gives acceptable normal and interrupted-process media behavior? | NOT RUN |
| G0-5 | Are real StatusWindow and RecordingBorderWindow excluded from required Area, Monitor, and Window capture paths? | NOT RUN |
| G0-6 | Is 30–60 minute recording stable, how do memory/CPU/output metrics evolve, and what are Stop P50/P95? | NOT RUN |
| G0-7 | What are the verified Window lifecycle, Monitor/Area API, virtual-coordinate, and mixed-DPI behaviors? | NOT RUN |

Record reviewed evidence and conclusions in `docs/technical-spike.md`.

## Generated files

Local recordings, recorder logs, run manifests, and per-run evidence stay outside Git by default.
