# ScreenRecorderLib Technical Spike

This project is the Phase 0 experiment harness for QuietCapture.

It is intentionally isolated from the production projects. Spike code may call third-party or Windows APIs directly so that the experiment measures their real behavior before QuietCapture freezes production abstractions.

## G0 gates

| Gate | Question | Status |
| --- | --- | --- |
| G0-1 | Can the selected backend reliably capture an arbitrary single-monitor area to MP4? | NOT RUN |
| G0-2 | Can it reliably record system audio? | NOT RUN |
| G0-3 | Can system audio and microphone be recorded together with acceptable sync? | NOT RUN |
| G0-4 | Which MP4 mode gives acceptable recovery and compatibility after interruption? | NOT RUN |
| G0-5 | Are StatusWindow and RecordingBorder excluded from all required capture paths? | NOT RUN |
| G0-6 | Is 30–60 minute recording stable, and what are observed Stop latency distributions? | NOT RUN |
| G0-7 | What are the verified window, monitor, resize, minimize, and DPI behaviors? | NOT RUN |

Record evidence and conclusions in docs/technical-spike.md.

## Generated files

Place local recordings and transient measurements under TestData/. They are ignored by Git.
