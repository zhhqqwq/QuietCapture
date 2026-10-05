# ADR 0003 — Audio Device Binding

## Status

**Pending Phase 0 runtime evidence**

## Decision owner

Phase 0 Architecture Freeze

## Gates

Primary: G0-2, G0-3

## Context

QuietCapture exposes simple system-audio and microphone toggles while Windows exposes mutable default devices and concrete endpoint IDs.

The candidate session model resolves user preferences to concrete device IDs before Start and keeps those IDs fixed for the recording session. Phase 0 must verify that the backend behavior supports that policy and define what happens when a selected device becomes unavailable.

ScreenRecorderLib performs recording; NAudio/WASAPI metering remains a separate concern.

## Evidence Required

- [ ] G0-2 baseline system-audio recording reviewed.
- [ ] G0-2 explicit non-default device binding reviewed where available.
- [ ] default output change during active recording reviewed.
- [ ] selected output-device loss behavior reviewed.
- [ ] G0-3 system + microphone baseline reviewed.
- [ ] source-isolation intervals reviewed.
- [ ] non-default microphone binding reviewed where available.
- [ ] default input change during active recording reviewed.
- [ ] selected microphone loss behavior reviewed.
- [ ] A/V sync reviewed from final media.
- [ ] packet/source evidence matches selected devices.

## Decision

**Default-device preference resolution:** TBD

**System-audio session binding:** TBD

**Microphone session binding:** TBD

**Silent device switching during session:** TBD

**Mic preflight behavior:** TBD

**Output-device loss behavior:** TBD

**Microphone loss behavior:** TBD

**Audio track/mix policy:** TBD

**Metering-device binding relationship:** TBD

## Alternatives

### Resolve default preference to concrete ID before Start

Candidate baseline policy; accept/reject from evidence.

### Follow Windows default dynamically

Reject unless Phase 0 produces a compelling product-safe reason and deterministic backend behavior.

### Backend-specific implicit default selection

Reject unless concrete ID resolution is impossible and the behavior remains testable/deterministic.

## Consequences

### Core model

- audio preference model: TBD
- resolved session options: TBD
- failure/Stop reason mapping: TBD

### Infrastructure

- device enumeration ownership: TBD
- device ID type/normalization: TBD
- ScreenRecorderLib source construction: TBD
- NAudio meter correlation: TBD

### UX

- missing device before Start: TBD
- lost device during recording: TBD
- default-device changes during recording: TBD

## Regression requirements

- [ ] explicit output-device binding.
- [ ] explicit microphone binding.
- [ ] default output change.
- [ ] default input change.
- [ ] output-device loss.
- [ ] microphone loss.
- [ ] dual-source recording.
- [ ] A/V sync.
- [ ] metering and recording use the same resolved device.

## Evidence references

- G0-2 result: TBD
- G0-3 result: TBD
- GitHub issues: #2, #3
- reviewed run IDs: TBD

## Decision date

TBD
