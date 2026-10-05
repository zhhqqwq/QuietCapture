# ADR 0001 — Recording Backend Route

## Status

**Pending Phase 0 runtime evidence**

## Decision owner

Phase 0 Architecture Freeze

## Gates

Primary: G0-1, G0-2, G0-3

Cross-check: G0-4, G0-5, G0-6, G0-7

## Context

QuietCapture needs one production recording route that can support Area, Window, and Monitor capture; system audio and microphone; bounded Stop; recoverable media behavior; overlay exclusion; and Windows lifecycle requirements.

The engineering baseline defines three candidate routes:

- Route A — ScreenRecorderLib owns capture, encoding, audio, and MP4.
- Route B — ScreenRecorderLib remains primary with bounded Windows/platform adapters or workarounds.
- Route C — Windows Graphics Capture + FFmpeg fallback/replacement path.

Production Ports must be derived after the route is selected. Spike-specific third-party types must not cross into Core.

## Evidence Required

- [ ] G0-1 Area capture mandatory cases reviewed.
- [ ] G0-2 fixed system-audio device mandatory cases reviewed.
- [ ] G0-3 system + microphone mandatory cases reviewed.
- [ ] G0-4 selected media behavior is compatible with the route.
- [ ] G0-5 selected production capture paths support overlay requirements.
- [ ] G0-6 30/60-minute stability does not invalidate the route.
- [ ] G0-7 Area/Monitor/Window APIs and policies are compatible with the route.
- [ ] evidence includes test environment, run IDs, logs/manifests, and failure cases.

## Decision

**Selected route:** TBD

**Production backend technology:** TBD

**Capture APIs by target:**

| Target | API/backend |
| --- | --- |
| Area | TBD |
| Window | TBD |
| Monitor | TBD |

**Encoding/audio ownership:** TBD

**Fallback behavior:** TBD

## Alternatives

### Route A — ScreenRecorderLib end-to-end

Keep when the primary backend satisfies mandatory video/audio/media/stability behavior without architecture-changing workarounds.

### Route B — ScreenRecorderLib + bounded platform adaptations

Use when ScreenRecorderLib remains viable but one or more proven behaviors require a narrow Windows adapter, capture-path selection, or finalization constraint.

### Route C — WGC + FFmpeg

Use when the primary route fails a mandatory v1 requirement that cannot be corrected by a bounded workaround.

### Other alternative

TBD only if Phase 0 reveals an option not represented above.

## Consequences

### Positive

TBD

### Negative

TBD

### Constraints

TBD

### Core / Infrastructure impact

- Core Port behavior: TBD
- Infrastructure.Windows ownership: TBD
- App composition changes: TBD
- third-party dependency impact: TBD

## Failure and fallback policy

TBD

## Regression requirements

- [ ] Area capture regression.
- [ ] Window capture regression.
- [ ] Monitor capture regression.
- [ ] system-audio regression.
- [ ] microphone + system-audio regression.
- [ ] bounded Stop regression.
- [ ] selected capture API regression.
- [ ] any workaround-specific regression.

## Evidence references

- G0-1 result: TBD
- G0-2 result: TBD
- G0-3 result: TBD
- G0-4 result: TBD
- G0-5 result: TBD
- G0-6 result: TBD
- G0-7 result: TBD
- GitHub issues: #1, #2, #3, #4, #5, #6, #7
- reviewed run IDs: TBD

## Decision date

TBD
