# ADR 0002 — Media Container and Framerate Policy

## Status

**Pending Phase 0 runtime evidence**

## Decision owner

Phase 0 Architecture Freeze

## Gates

Primary: G0-4

Cross-check: G0-6

## Context

QuietCapture must produce media that finalizes reliably, remains compatible with expected players/editors, and preserves useful evidence after interruption. The production choice must resolve conventional versus fragmented MP4, fixed versus non-fixed framerate, and any normal-stop remux/finalization step.

Raw interrupted files must never be silently deleted.

## Evidence Required

- [ ] G0-4 D1–D8 reviewed.
- [ ] static content runs reviewed.
- [ ] dynamic content runs reviewed.
- [ ] static → dynamic → static runs reviewed.
- [ ] normal Stop playback/seek reviewed.
- [ ] interrupted-process survivability reviewed.
- [ ] duration/frame count/PTS reviewed.
- [ ] `r_frame_rate` and `avg_frame_rate` reviewed.
- [ ] target editor import/seek/export reviewed.
- [ ] any remux candidate tested against the original raw output.
- [ ] G0-6 confirms selected normal-stop path remains stable.

## Decision

**MP4 mode:** TBD

**Framerate policy:** TBD

**Target FPS semantics:** TBD

**Normal Stop finalization/remux:** TBD

**Interrupted-file treatment:** TBD

**Final-file compatibility requirement:** TBD

## Alternatives

### Conventional MP4 + fixed framerate

TBD from evidence.

### Conventional MP4 + non-fixed framerate

TBD from evidence.

### Fragmented MP4 + fixed framerate

TBD from evidence.

### Fragmented MP4 + non-fixed framerate

TBD from evidence.

### Normal-stop remux/finalize step

TBD from evidence. Must not be added solely to make interrupted media look repaired.

## Consequences

### Positive

TBD

### Negative

TBD

### Recovery implications

- partial file preservation: TBD
- recoverable metadata expectation: TBD
- remux ownership/layer: TBD
- player/editor limitations: TBD

### Storage/session implications

- working filename: TBD
- final rename/finalize sequence: TBD
- same-volume requirement: retained unless evidence changes it
- auto-delete policy: prohibited for non-empty interrupted media

## Regression requirements

- [ ] normal Stop media compatibility.
- [ ] interrupted-process preservation.
- [ ] frame/timestamp behavior.
- [ ] editor import/seek/export.
- [ ] finalization/remux behavior when selected.
- [ ] no silent deletion of raw interrupted media.

## Evidence references

- G0-4 result: TBD
- G0-6 cross-check: TBD
- GitHub issue: #4
- reviewed run IDs: TBD
- ffprobe/MediaInfo summaries: TBD
- player/editor observations: TBD

## Decision date

TBD
