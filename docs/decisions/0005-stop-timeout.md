# ADR 0005 — Stop Timeout Policy

## Status

**Pending Phase 0 runtime evidence**

## Decision owner

Phase 0 Architecture Freeze

## Gates

Primary: G0-6

Cross-check: G0-1, G0-4

## Context

All Stop sources converge through the Core recording lifecycle. Stop must be idempotent, serialized, bounded, and media-safe.

The engineering baseline contains a provisional soft timeout, but the production value must be selected from observed Stop→RecordingComplete distributions and known failure behavior rather than frozen from an assumption.

## Evidence Required

- [ ] G0-1 normal Stop behavior reviewed.
- [ ] G0-4 normal Stop/finalization behavior reviewed.
- [ ] G0-6 30-minute rounds reviewed.
- [ ] G0-6 60-minute rounds reviewed.
- [ ] Stop P50 recorded.
- [ ] Stop P95 recorded.
- [ ] maximum observed successful Stop recorded.
- [ ] any long-tail/failure cases reviewed.
- [ ] cleanup/Dispose behavior after backend failure reviewed where observable.
- [ ] selected media policy from ADR 0002 does not invalidate timing evidence.

## Decision

**Soft Stop timeout:** TBD

**Measurement basis:** TBD

**Timeout margin/rationale:** TBD

**Behavior before timeout:** TBD

**Behavior at timeout:** TBD

**Cancellation behavior:** TBD

**Dispose bound:** TBD

**Idle vs Faulted decision after timeout:** TBD

**SessionStatus mapping:** TBD

## Alternatives

### Fixed provisional timeout

Reject unless runtime evidence independently justifies the same value.

### P95 + explicit bounded margin

Candidate evidence-driven policy.

### Backend-specific indefinite wait

Rejected by the product lifecycle requirement for bounded Stop.

## Consequences

### Core lifecycle

- Stopping behavior: TBD
- StopFailed behavior: TBD
- Faulted transition rule: TBD
- retry/restart eligibility: TBD

### Session metadata

- timeout metadata write: TBD
- partial-media preservation: TBD
- recovery index behavior: TBD

### UX

- stopping indication: TBD
- timeout error: TBD
- saved/recovery notification: TBD

## Regression requirements

- [ ] repeated normal Stop.
- [ ] Stop from every supported source.
- [ ] pending Stop during Starting.
- [ ] backend failure followed by Stop.
- [ ] timeout path using a controllable fake backend.
- [ ] metadata/media preservation after timeout.
- [ ] Faulted versus Idle behavior.

## Evidence references

- G0-1 result: TBD
- G0-4 result: TBD
- G0-6 result: TBD
- GitHub issues: #1, #4, #6
- P50: TBD
- P95: TBD
- maximum observed successful Stop: TBD
- reviewed run IDs: TBD

## Decision date

TBD
