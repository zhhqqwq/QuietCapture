# Third-Party Notices

This file tracks third-party components that are shipped, linked, or materially used by QuietCapture.

No recording backend package is part of the product scaffold yet. Phase 0 will add and verify the candidate dependencies before release packaging decisions are frozen.

| Component | Version | Purpose | License / redistribution status |
| --- | --- | --- | --- |
| ScreenRecorderLib | TBD | Phase 0 recording backend candidate | Verify when package version is selected |
| NAudio | TBD | Planned Windows audio integration | Verify when package version is selected |
| xUnit | 2.8.1 | Test framework | Development-only dependency; verify before release audit |
| coverlet.collector | 6.0.2 | Test coverage collection | Development-only dependency; verify before release audit |

If FFmpeg becomes part of a later backend route, document the exact binary source, configuration, codec set, and LGPL/GPL obligations before distribution.
