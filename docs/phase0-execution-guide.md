# Phase 0 Harness Freeze + Execution Guide

## Purpose

This document is the execution contract for QuietCapture Phase 0.

Phase 0 harness preparation is complete when G0-1 through G0-7 build in CI and this guide, the launcher, the gate-specific matrices, and the evidence conventions are present. That does **not** change any runtime gate result.

The runtime gates remain:

```text
G0-1 = NOT RUN
G0-2 = NOT RUN
G0-3 = NOT RUN
G0-4 = NOT RUN
G0-5 = NOT RUN
G0-6 = NOT RUN
G0-7 = NOT RUN
```

until reviewed Windows-machine evidence exists.

## G0-0 — Clean-machine prerequisite

G0-0 is a prerequisite, not an architecture gate. Its result is **READY** or **BLOCKED**.

Run it before the first runtime gate on every new test machine:

```powershell
.\spikes\QuietCapture.ScreenRecorderLibSpike\run-phase0.ps1 -Gate g0-0
```

The launcher checks and records:

- Windows OS and build;
- x64 OS/process;
- .NET SDK availability/version;
- Visual C++ runtime files required by the current native/C++ route;
- Media Foundation runtime files;
- evidence-root writability;
- evidence-volume filesystem and free space.

The generated report is stored under:

```text
%TEMP%\QuietCaptureSpike\Phase0\g0-0-prerequisite.json
```

Minimum execution prerequisite:

- Windows 10 version 2004 / build 19041 or later, or Windows 11;
- x64 Windows;
- .NET 8 SDK for source execution;
- required Visual C++ runtime present;
- Media Foundation present;
- writable evidence location;
- non-FAT32 evidence/output volume for long/media-safety tests.

Gate-specific hardware is checked manually before the relevant gate:

- G0-2: working playback/output device;
- G0-3: playback device plus microphone/capture device;
- G0-5: Windows version supporting the required capture-exclusion behavior;
- G0-7 mixed-DPI/cross-monitor cases: two or more displays when those cases are executed.

A BLOCKED G0-0 stops the test session until the failed prerequisite is corrected.

## Recommended execution order

Run the gates in this order:

| Order | Gate | Purpose | GitHub issue |
| ---: | --- | --- | --- |
| 0 | G0-0 | Clean-machine/runtime prerequisite | prerequisite; no gate issue |
| 1 | G0-1 | Area capture geometry and Stop baseline | #1 |
| 2 | G0-2 | Fixed system-audio device | #2 |
| 3 | G0-3 | Fixed system-audio + microphone devices and sync | #3 |
| 4 | G0-4 | MP4/framerate/crash-recovery behavior | #4 |
| 5 | G0-5 | Overlay capture exclusion | #5 |
| 6 | G0-6 | Long-run stability and Stop P50/P95 | #6 |
| 7 | G0-7 | Window/Monitor/Area/DPI/topology behavior | #7 |

Why this order:

1. G0-1 establishes the simplest video path.
2. G0-2 and G0-3 add audio one layer at a time.
3. G0-4 decides media/container behavior before recovery policy is frozen.
4. G0-5 validates the real recording overlays against capture paths.
5. G0-6 measures stability after the basic backend behavior is understood.
6. G0-7 closes the remaining Window, display-API, coordinate, and DPI decisions.

Do not freeze an architecture decision that depends on a later gate before that gate is reviewed.

## Launcher

List the available harnesses:

```powershell
.\spikes\QuietCapture.ScreenRecorderLibSpike\run-phase0.ps1 -Gate list
```

Run one gate:

```powershell
.\spikes\QuietCapture.ScreenRecorderLibSpike\run-phase0.ps1 -Gate g0-4
```

Run the full assisted sequence:

```powershell
.\spikes\QuietCapture.ScreenRecorderLibSpike\run-phase0.ps1 -Gate all
```

`all` runs G0-0 first and stops if it is BLOCKED. G0-1 through G0-7 then launch sequentially. GUI harnesses are interactive; close the current harness only after completing the intended matrix entries/evidence collection for that session.

The launcher does not assign PASS/FAIL and does not edit result documents.

## Environment capture checklist

Record the following once for each machine/session and repeat whenever a relevant component changes:

### Operating system

- Windows edition;
- version and build;
- architecture;
- clean VM / physical machine / existing development machine;
- pending Windows updates or reboot state when relevant.

### Graphics

- GPU model(s);
- driver version;
- hardware encoder availability/behavior;
- display count;
- monitor device names;
- monitor resolution;
- virtual desktop layout;
- negative X/Y coordinates;
- DPI/scaling per monitor.

### Runtime and dependencies

- .NET SDK version;
- ScreenRecorderLib version;
- Visual C++ runtime presence/version when available;
- Media Foundation presence;
- execution mode: source checkout / portable build / installer build when later packaging tests begin.

### Audio

For G0-2/G0-3 record:

- loopback/output friendly name and concrete device ID;
- whether it was Windows default at enumeration;
- microphone/capture friendly name and concrete device ID;
- whether it was Windows default at enumeration;
- privacy/access state;
- Bluetooth/USB/wired device type when relevant.

### Storage

- evidence/output path;
- filesystem;
- free space before long runs;
- removable/local/network status;
- same-volume relationship when recovery/session tests later use production paths.

## Evidence directory conventions

Runtime harnesses write evidence outside Git by default under:

```text
%TEMP%\QuietCaptureSpike\
├─ Phase0\
│  └─ g0-0-prerequisite.json
├─ G0-1\
├─ G0-2\
├─ G0-3\
├─ G0-4\
├─ G0-5\
├─ G0-6\
└─ G0-7\
```

Gate-specific harnesses define their exact file layout in their own README.

Evidence rules:

- never overwrite a prior run intentionally;
- preserve raw ScreenRecorderLib logs;
- preserve raw killed/interrupted G0-4 media before any repair/remux attempt;
- preserve generated JSON/CSV manifests with the media they describe;
- record external ffprobe/MediaInfo/player/editor observations against the exact run ID;
- do not commit large MP4/log evidence to the source repository;
- attach or link evidence in the matching GitHub issue when reviewing a gate;
- if evidence must be archived elsewhere, preserve the original run directory structure and run IDs.

## Gate decision rules

Only G0-1 through G0-7 use these states.

### NOT RUN

Use when:

- runtime execution has not occurred;
- execution happened but evidence has not been reviewed;
- only CI/build/API compilation has succeeded.

### PASS

Use only when all mandatory cases for that gate have reviewed evidence and the candidate behavior satisfies the product/engineering baseline without a production workaround that changes the architecture decision.

A PASS entry must state:

- environment;
- cases executed;
- evidence/run IDs;
- observed behavior;
- architecture consequence.

### PASS WITH WORKAROUND

Use only when the candidate route can satisfy v1 with a bounded, explicit workaround.

The workaround must have:

- a precise trigger/condition;
- an owner/layer;
- deterministic product behavior;
- a regression-test plan;
- no violation of the frozen architecture boundaries or media-safety rules.

Examples include:

- safe-stop on a specific target/topology transition;
- selecting one proven display capture API and rejecting the other;
- a bounded normal-stop remux/finalize step proven by G0-4.

Do not use PASS WITH WORKAROUND for an unexplained intermittent failure.

### FAIL

Use when the candidate route cannot satisfy the mandatory v1 behavior with a bounded acceptable workaround.

A FAIL must identify the architecture consequence, such as evaluating Route C or changing the production capture path.

## Issue mapping

| Gate | Issue | Primary result file |
| --- | --- | --- |
| G0-1 | [#1 — Area capture validation](https://github.com/zhhqqwq/QuietCapture/issues/1) | `Gates/G01AreaCapture/RESULTS.md` |
| G0-2 | [#2 — System audio validation](https://github.com/zhhqqwq/QuietCapture/issues/2) | `Gates/G02SystemAudio/RESULTS.md` |
| G0-3 | [#3 — System audio + microphone validation](https://github.com/zhhqqwq/QuietCapture/issues/3) | `Gates/G03SystemAudioMicrophone/RESULTS.md` |
| G0-4 | [#4 — Media + crash recovery validation](https://github.com/zhhqqwq/QuietCapture/issues/4) | `Gates/G04MediaCrashRecovery/RESULTS.md` |
| G0-5 | [#5 — Capture exclusion validation](https://github.com/zhhqqwq/QuietCapture/issues/5) | `Gates/G05CaptureExclusion/RESULTS.md` |
| G0-6 | [#6 — Stability + Stop latency validation](https://github.com/zhhqqwq/QuietCapture/issues/6) | `Gates/G06StabilityStopLatency/RESULTS.md` |
| G0-7 | [#7 — Window + monitor + DPI validation](https://github.com/zhhqqwq/QuietCapture/issues/7) | `Gates/G07WindowMonitorDpi/RESULTS.md` |

## Result update procedure

For each gate:

1. Run the required matrix and preserve all raw evidence.
2. Update the matching GitHub issue checklist with the environment and run IDs.
3. Review media/log/manifest evidence; do not decide from the harness UI alone.
4. Update the gate's `RESULTS.md` with per-case outcomes and findings.
5. Set the gate state in `RESULTS.md` to PASS, PASS WITH WORKAROUND, or FAIL only after review.
6. Update the matching section in `docs/technical-spike.md`:
   - Status;
   - Evidence;
   - Result;
   - Architecture consequence.
7. Update the mapped ADR under `docs/decisions/`; do not finalize its status until its Evidence Required checklist is satisfied.
8. Link the commit/ADR/result back to the GitHub issue.
9. Close the issue only when the gate decision and architecture consequence are recorded in the repository.
10. When all mapped ADRs are finalized, complete `docs/architecture-freeze-template.md` and then update `docs/architecture/README.md` to the frozen production architecture.

If new evidence invalidates an earlier decision, reopen the issue, return the gate to the appropriate unresolved state, and update the ADR rather than silently editing history.

## Architecture-freeze checklist

Do not start the production architecture freeze until all mandatory Phase 0 gates have reviewed decisions.

### Backend route

- [ ] G0-1 area capture candidate is viable.
- [ ] G0-2 system audio is viable.
- [ ] G0-3 system audio + microphone is viable.
- [ ] Route A / Route B / Route C decision is explicit.

### Backend contract

- [ ] final backend Start/Stop/failure semantics are supported by evidence;
- [ ] fresh backend-per-session assumption remains valid;
- [ ] device IDs are resolved to concrete session IDs before Start;
- [ ] backend failure/device-loss behavior is mapped to Core Stop reasons.

### Media

- [ ] fixed/non-fixed framerate policy frozen;
- [ ] fragmented/conventional MP4 policy frozen;
- [ ] normal-stop finalize/remux requirement frozen;
- [ ] interrupted-media preservation/recovery behavior frozen;
- [ ] player/editor compatibility evidence recorded.

### Overlay

- [ ] final StatusWindow properties proven;
- [ ] final RecordingBorderWindow properties proven;
- [ ] capture-exclusion implementation proven for all production capture paths;
- [ ] first-frame UI contamination behavior resolved.

### Stability and Stop

- [ ] 30-minute runs reviewed;
- [ ] 60-minute runs reviewed;
- [ ] Stop P50/P95 recorded;
- [ ] production soft Stop timeout selected from evidence;
- [ ] long-run memory behavior acceptable;
- [ ] 8-hour acceptance scheduled/completed before release freeze.

### Window / Monitor / DPI

- [ ] Window resize policy frozen;
- [ ] Window minimize policy frozen;
- [ ] Window destroy policy frozen;
- [ ] cross-monitor Window policy frozen;
- [ ] Monitor capture API selected;
- [ ] Area capture API selected;
- [ ] Area source-local/virtual coordinate transform frozen;
- [ ] topology/resolution/DPI-change policy frozen;
- [ ] mixed-DPI/negative-coordinate evidence reviewed.

### Freeze output

Before Phase 1 production implementation proceeds beyond scaffolding:

- [ ] `docs/technical-spike.md` contains all final gate decisions;
- [ ] required ADRs under `docs/decisions/` are finalized;
- [ ] unresolved open questions are either closed or explicitly deferred outside v1;
- [ ] `docs/architecture-freeze-template.md` is complete;
- [ ] architecture document matches the decided route;
- [ ] test plan contains regression coverage for every PASS WITH WORKAROUND;
- [ ] Phase 0 issues #1–#7 are linked to repository evidence.

## Phase 0 freeze definition

Harness Freeze means:

- G0-1 through G0-7 harnesses exist;
- CI builds/tests the repository successfully;
- this execution guide and launcher exist;
- gate matrices and result templates exist.

Architecture Freeze means:

- runtime evidence has been collected and reviewed;
- G0-1 through G0-7 have decisions;
- architecture consequences and ADRs are committed.

QuietCapture is currently at **Harness Freeze**, not Architecture Freeze.
