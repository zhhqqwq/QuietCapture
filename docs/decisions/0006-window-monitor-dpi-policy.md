# ADR 0006 — Window, Monitor, Area, and DPI Policy

## Status

**Pending Phase 0 runtime evidence**

## Decision owner

Phase 0 Architecture Freeze

## Gates

Primary: G0-7

## Context

QuietCapture's Core coordinate model is physical pixels and the application is PerMonitorV2. Production behavior must still be frozen for Window move/resize/minimize/destroy, cross-monitor movement, Area/Monitor capture API choice, negative virtual coordinates, mixed DPI, and display-topology changes.

Window capture uses a target HWND. Area is constrained to one monitor in v1.

## Evidence Required

- [ ] F1 Window Move reviewed.
- [ ] F2 Window Resize reviewed.
- [ ] F3 Window Minimize reviewed.
- [ ] F4 Window Restore reviewed.
- [ ] F5 Window Destroy reviewed.
- [ ] F6 cross-monitor Window move reviewed.
- [ ] F7 Desktop Duplication Monitor reviewed.
- [ ] F8 WGC Monitor reviewed.
- [ ] F9 secondary/negative-origin monitor reviewed where available.
- [ ] F10 topology/resolution change reviewed.
- [ ] F11 primary local SourceRect reviewed.
- [ ] F12 WGC vs Desktop Duplication Area reviewed.
- [ ] F13 negative-origin Area reviewed where available.
- [ ] F14 boundary crop reviewed.
- [ ] representative 100% DPI reviewed.
- [ ] representative 150% DPI reviewed.
- [ ] mixed-DPI movement reviewed where available.

## Decision

### Window

**Capture API:** TBD

**Move within monitor:** TBD

**Resize:** TBD

**Output canvas policy:** TBD

**Minimize:** TBD

**Restore:** TBD

**Destroy:** TBD

**Cross-monitor move:** TBD

**DPI change during move:** TBD

### Monitor

**Capture API:** TBD

**Resolution change:** TBD

**Disconnect/topology change:** TBD

### Area

**Capture API:** TBD

**SourceRect coordinate system:** TBD

**UI → source-local transform:** TBD

**negative virtual coordinates:** TBD

**boundary behavior:** TBD

**even-dimension normalization:** TBD

### System events

**DPI change:** TBD

**display topology change:** TBD

**target unavailable reason:** TBD

**target invalidated reason:** TBD

## Alternatives

### Monitor/Area — Desktop Duplication

TBD from G0-7.

### Monitor/Area — Windows Graphics Capture

TBD from G0-7.

### Window resize — fixed output canvas with scaling/padding

Candidate baseline behavior; accept/reject from evidence.

### Window resize — safe Stop

Use if resize behavior cannot remain predictable inside one session.

### Minimize/destroy/topology changes — safe Stop

Candidate v1 policy where capture continuity becomes undefined or unsafe.

## Consequences

### Core models

- target models: TBD
- physical-pixel transform rules: TBD
- Stop reasons: TBD

### Infrastructure.Windows

- display/window query APIs: TBD
- chosen capture APIs: TBD
- event detection: TBD

### UI.Wpf

- selector coordinate conversion: TBD
- one-monitor Area enforcement: retained unless evidence forces redesign
- resize/move status behavior: TBD

## Regression requirements

- [ ] same-monitor Window move.
- [ ] resize.
- [ ] minimize/restore.
- [ ] destroy.
- [ ] cross-monitor move.
- [ ] 100% DPI.
- [ ] 150% DPI.
- [ ] mixed DPI.
- [ ] negative-origin Area.
- [ ] Monitor capture on selected API.
- [ ] topology/resolution change.
- [ ] physical-pixel transform tests.

## Evidence references

- G0-7 result: TBD
- GitHub issue: #7
- reviewed run IDs: TBD

## Decision date

TBD
