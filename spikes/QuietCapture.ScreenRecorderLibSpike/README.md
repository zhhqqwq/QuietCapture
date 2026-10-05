# ScreenRecorderLib Technical Spike

This project is the Phase 0 experiment harness for QuietCapture.

It is intentionally isolated from the production projects. Spike code may call third-party or Windows APIs directly so that the experiment measures their real behavior before QuietCapture freezes production abstractions.

## Prepared harnesses

- `run-g0-1.ps1` — area capture using an explicit display and SourceRect.
- `run-g0-2.ps1` — full-display video plus one explicitly selected system-audio loopback device.
- `run-g0-3.ps1` — full-display video plus one explicitly selected loopback device and one explicitly selected microphone/capture device.

A prepared harness does not change a gate result. Runtime-dependent gates remain NOT RUN until reviewed Windows evidence exists.

## G0 gates

| Gate | Question | Status |
| --- | --- | --- |
| G0-1 | Can the selected backend reliably capture an arbitrary single-monitor area to MP4? | NOT RUN |
| G0-2 | Can it reliably record system audio from a fixed output device? | NOT RUN |
| G0-3 | Can fixed system-audio and microphone devices be recorded together with acceptable sync? | NOT RUN |
| G0-4 | Which MP4 mode gives acceptable recovery and compatibility after interruption? | NOT RUN |
| G0-5 | Are StatusWindow and RecordingBorder excluded from all required capture paths? | NOT RUN |
| G0-6 | Is 30–60 minute recording stable, and what are observed Stop latency distributions? | NOT RUN |
| G0-7 | What are the verified window, monitor, resize, minimize, and DPI behaviors? | NOT RUN |

Record reviewed evidence and conclusions in `docs/technical-spike.md`.

## Generated files

Local recordings, recorder logs, and per-run JSON evidence stay outside Git by default.
