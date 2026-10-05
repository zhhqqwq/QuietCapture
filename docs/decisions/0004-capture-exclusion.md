# ADR 0004 — Capture Exclusion

## Status

**Pending Phase 0 runtime evidence**

## Decision owner

Phase 0 Architecture Freeze

## Gates

Primary: G0-5

Cross-check: G0-7

## Context

QuietCapture must show recording state/border UI to the user without contaminating captured media.

The candidate implementation creates real top-level WPF StatusWindow and RecordingBorderWindow HWNDs, applies native overlay styles, calls `SetWindowDisplayAffinity(..., WDA_EXCLUDEFROMCAPTURE)`, verifies the affinity, and only then shows the windows. The actual requirement depends on the selected Area/Monitor/Window capture paths.

## Evidence Required

- [ ] E1 Area + WDA_NONE reviewed.
- [ ] E2 Area + WDA_EXCLUDEFROMCAPTURE reviewed.
- [ ] E3 Monitor + WDA_NONE reviewed.
- [ ] E4 Monitor + WDA_EXCLUDEFROMCAPTURE reviewed.
- [ ] E5 Window/WGC + WDA_NONE reviewed.
- [ ] E6 Window/WGC + WDA_EXCLUDEFROMCAPTURE reviewed.
- [ ] overlay visible-on-desktop evidence reviewed.
- [ ] affinity before Show reviewed.
- [ ] affinity after Show reviewed.
- [ ] first-frame contamination reviewed.
- [ ] G0-7 selected production capture APIs are known.

## Decision

**StatusWindow properties:** TBD

**RecordingBorderWindow properties:** TBD

**Native extended styles:** TBD

**Affinity application order:** TBD

**Area-path exclusion behavior:** TBD

**Monitor-path exclusion behavior:** TBD

**Window-path exclusion behavior:** TBD

**First-frame sequencing:** TBD

**Unsupported/fallback behavior:** TBD

## Alternatives

### WDA_EXCLUDEFROMCAPTURE on real overlay HWNDs

Candidate baseline behavior.

### Rely on capture API intrinsic window isolation

Accept only for a path where WDA_NONE controls prove the API itself excludes unrelated windows. Do not generalize that behavior to other paths.

### Hide overlays during recording

Does not satisfy the intended product behavior unless Phase 0 forces a product-scope change.

### Capture-path redesign

Use when a mandatory path cannot reliably exclude the recording UI.

## Consequences

### UI.Wpf

- window shape/properties: TBD
- HWND creation timing: TBD
- Show timing: TBD

### Infrastructure.Windows

- display-affinity Port behavior: TBD
- Win32 error handling: TBD

### Core lifecycle

- Start sequencing dependency: TBD
- failure to apply exclusion: TBD

## Regression requirements

- [ ] StatusWindow excluded on every relevant production path.
- [ ] RecordingBorderWindow excluded on every relevant production path.
- [ ] WDA_NONE control behavior retained in Spike/manual regression.
- [ ] affinity survives Show.
- [ ] no first-frame UI contamination.
- [ ] selected Windows versions covered.

## Evidence references

- G0-5 result: TBD
- G0-7 selected capture APIs: TBD
- GitHub issue: #5
- reviewed run IDs: TBD

## Decision date

TBD
