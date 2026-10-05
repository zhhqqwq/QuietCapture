# QuietCapture Architecture Freeze Template

## Status

**Architecture Freeze: PENDING PHASE 0 RUNTIME EVIDENCE**

This document is the final decision checklist that converts reviewed Phase 0 evidence into the production architecture used by Phase 1 and later work.

Do not mark Architecture Freeze complete until every required gate decision and ADR below has reviewed evidence.

## Inputs

Architecture Freeze consumes:

- `docs/engineering-baseline.md`
- `docs/phase0-execution-guide.md`
- `docs/technical-spike.md`
- G0-1 through G0-7 `RESULTS.md`
- GitHub Issues #1 through #7
- raw run manifests/logs/media referenced by those results
- ADRs under `docs/decisions/`

G0-0 validates the execution environment. It does not produce an architecture decision.

## Gate → ADR mapping

| Gate | Decision area | Required ADR |
| --- | --- | --- |
| G0-1 | Area capture viability and backend-route input | 0001 recording backend route |
| G0-2 | System-audio viability and fixed device binding | 0001 recording backend route; 0003 audio device binding |
| G0-3 | System + microphone mixing, device binding, A/V sync | 0001 recording backend route; 0003 audio device binding |
| G0-4 | MP4 mode, framerate behavior, interrupted-media recovery | 0002 media container + framerate |
| G0-5 | Status/border exclusion implementation and capture-path constraints | 0004 capture exclusion |
| G0-6 | Stop latency distribution and production Stop timeout | 0005 Stop timeout |
| G0-7 | Window lifecycle, Monitor/Area API, coordinates, DPI/topology policy | 0006 Window/Monitor/DPI policy |
| G0-6 + release acceptance | long-run stability | 0001 recording backend route and release acceptance, when it changes the route decision |

## ADR status rules

All Phase 0 ADRs start as:

`Pending Phase 0 runtime evidence`

Allowed final statuses:

- **Accepted** — decision is frozen for production implementation.
- **Accepted with constraints** — decision is frozen with explicit bounded constraints/workarounds.
- **Rejected** — candidate decision/path is rejected; another documented alternative is selected.
- **Superseded by ADR NNNN** — a later reviewed decision replaces this one.

Do not change an ADR to Accepted/Accepted with constraints/Rejected unless its Evidence Required section is satisfied.

## Evidence quality rule

Every frozen decision must identify:

1. gate status;
2. test machine/environment;
3. mandatory matrix cases executed;
4. run IDs/evidence locations;
5. observed behavior;
6. known failure cases;
7. workaround, when applicable;
8. regression coverage required by the decision.

A build-only or CI-only result is not runtime evidence.

## Freeze decision summary

Fill only after evidence review.

| Decision | ADR | Status | Frozen value |
| --- | --- | --- | --- |
| Recording backend route | 0001 | Pending | TBD |
| Media container/framerate/finalization | 0002 | Pending | TBD |
| Audio device binding | 0003 | Pending | TBD |
| Capture exclusion | 0004 | Pending | TBD |
| Stop timeout | 0005 | Pending | TBD |
| Window/Monitor/DPI policy | 0006 | Pending | TBD |

## Route A / B / C decision

### Candidates

- **Route A** — ScreenRecorderLib owns capture, encoding, audio, and MP4.
- **Route B** — ScreenRecorderLib remains the primary backend with bounded Windows/platform adapters or workarounds.
- **Route C** — Windows Graphics Capture + FFmpeg fallback/replacement route.

### Evidence required

- [ ] G0-1 Area path reviewed.
- [ ] G0-2 system audio reviewed.
- [ ] G0-3 system + microphone reviewed.
- [ ] G0-4 media/finalization implications reviewed.
- [ ] G0-5 production capture-path exclusion implications reviewed.
- [ ] G0-6 stability does not invalidate the selected route.
- [ ] G0-7 selected Area/Monitor/Window APIs are implementable under the route.

### Frozen decision

**Selected route:** TBD

**Reason:** TBD

**Rejected alternatives:** TBD

## Backend contract freeze

Do not freeze signatures from the Spike API. Freeze production contracts from required behavior.

- [ ] backend is created fresh per recording session;
- [ ] Start inputs are concrete session-resolved values;
- [ ] target type/model frozen;
- [ ] audio device identifiers frozen;
- [ ] Start completion semantics frozen;
- [ ] failure callback/event semantics frozen;
- [ ] Stop semantics frozen;
- [ ] Dispose semantics frozen;
- [ ] device/target loss behavior mapped to Core reasons;
- [ ] third-party types do not cross Infrastructure boundary.

**Contract summary:** TBD

## Media freeze

- [ ] target FPS semantics frozen;
- [ ] fixed/non-fixed framerate policy frozen;
- [ ] conventional/fragmented MP4 policy frozen;
- [ ] normal Stop finalization behavior frozen;
- [ ] remux requirement frozen;
- [ ] interrupted-media preservation policy frozen;
- [ ] compatibility targets recorded;
- [ ] recovery limitations recorded.

**Media summary:** TBD

## Audio freeze

- [ ] Windows default preference resolution policy frozen;
- [ ] concrete output device ID binding frozen;
- [ ] concrete microphone device ID binding frozen;
- [ ] no-silent-switch session policy frozen;
- [ ] microphone preflight policy frozen;
- [ ] missing/lost device behavior frozen;
- [ ] one-track/mix behavior recorded;
- [ ] A/V sync evidence recorded.

**Audio summary:** TBD

## Capture-exclusion freeze

- [ ] final StatusWindow properties frozen;
- [ ] final RecordingBorderWindow properties frozen;
- [ ] EnsureHandle → affinity → Show order frozen;
- [ ] capture paths requiring affinity identified;
- [ ] capture paths intrinsically excluding unrelated windows identified;
- [ ] first-frame contamination behavior resolved;
- [ ] unsupported Windows behavior documented.

**Overlay summary:** TBD

## Stop freeze

- [ ] G0-6 successful-run distribution reviewed;
- [ ] P50 recorded;
- [ ] P95 recorded;
- [ ] maximum/long-tail observations recorded;
- [ ] soft timeout selected;
- [ ] post-timeout cleanup behavior defined;
- [ ] StopFailed/Faulted boundary frozen.

**Soft Stop timeout:** TBD

**Reason:** TBD

## Window / Monitor / DPI freeze

- [ ] Window move policy frozen;
- [ ] Window resize policy frozen;
- [ ] minimize policy frozen;
- [ ] destroy policy frozen;
- [ ] cross-monitor policy frozen;
- [ ] Area capture API frozen;
- [ ] Monitor capture API frozen;
- [ ] Window capture API frozen;
- [ ] physical-pixel coordinate transform frozen;
- [ ] negative-coordinate handling frozen;
- [ ] mixed-DPI behavior frozen;
- [ ] topology/resolution-change policy frozen.

**Window/Monitor/DPI summary:** TBD

## Core lifecycle consequences

Update production state/lifecycle only after the six ADRs are frozen.

- [ ] `IAppStateMachine` states still sufficient.
- [ ] Starting + pending Stop behavior still sufficient.
- [ ] all Stop reasons converge through Core lifecycle.
- [ ] backend failure maps to safe Stop/Faulted policy.
- [ ] target/device/topology failures have explicit Core reasons.
- [ ] Stop timeout behavior matches ADR 0005.
- [ ] recovery state mapping matches ADR 0002.

**Lifecycle changes required:** TBD

## Regression requirements

Every Accepted with constraints decision must create regression coverage.

| ADR | Constraint/workaround | Required automated/manual regression |
| --- | --- | --- |
| 0001 | TBD | TBD |
| 0002 | TBD | TBD |
| 0003 | TBD | TBD |
| 0004 | TBD | TBD |
| 0005 | TBD | TBD |
| 0006 | TBD | TBD |

## Open questions disposition

Before freeze, every question in `docs/technical-spike.md` must be:

- answered by evidence and linked to an ADR;
- explicitly deferred outside v1 with product impact stated; or
- converted into a blocking follow-up Gate if it prevents safe architecture freeze.

**Remaining unresolved blockers:** TBD

## Freeze checklist

Architecture Freeze may be marked COMPLETE only when all are true:

- [ ] G0-1 has a reviewed final status.
- [ ] G0-2 has a reviewed final status.
- [ ] G0-3 has a reviewed final status.
- [ ] G0-4 has a reviewed final status.
- [ ] G0-5 has a reviewed final status.
- [ ] G0-6 has a reviewed final status.
- [ ] G0-7 has a reviewed final status.
- [ ] ADR 0001 is finalized.
- [ ] ADR 0002 is finalized.
- [ ] ADR 0003 is finalized.
- [ ] ADR 0004 is finalized.
- [ ] ADR 0005 is finalized.
- [ ] ADR 0006 is finalized.
- [ ] `docs/technical-spike.md` matches all ADR decisions.
- [ ] `docs/architecture/README.md` is updated to the frozen production architecture.
- [ ] backend-facing production Port signatures are derived from the frozen behavior, not copied from Spike code.
- [ ] every workaround has an owner and regression test.
- [ ] no mandatory Phase 0 question remains unresolved.
- [ ] Phase 1 implementation plan is consistent with the selected route.

## Freeze record

**Architecture Freeze status:** PENDING PHASE 0 RUNTIME EVIDENCE

**Freeze date:** TBD

**Freeze commit:** TBD

**Reviewed Gate commits:** TBD

**Accepted ADRs:** TBD

**Approved production route:** TBD
