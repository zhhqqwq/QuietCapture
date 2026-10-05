# Architecture Decision Records

This directory is the canonical ADR location for QuietCapture architecture decisions.

Phase 0 ADRs are created before runtime validation as decision templates, but remain **Pending Phase 0 runtime evidence** until their Evidence Required sections are satisfied.

## Status flow

```text
Pending Phase 0 runtime evidence
        ↓
Accepted / Accepted with constraints / Rejected
        ↓
Superseded by ADR NNNN   (only when a later decision replaces it)
```

Do not treat a template's candidate direction as a decision.

## Phase 0 ADR set

| ADR | Decision | Primary gates |
| --- | --- | --- |
| 0001 | Recording backend route | G0-1, G0-2, G0-3, G0-6, G0-7 |
| 0002 | Media container + framerate | G0-4 |
| 0003 | Audio device binding | G0-2, G0-3 |
| 0004 | Capture exclusion | G0-5, G0-7 |
| 0005 | Stop timeout | G0-1, G0-4, G0-6 |
| 0006 | Window / Monitor / DPI policy | G0-7 |

See `docs/architecture-freeze-template.md` for the complete freeze checklist.
