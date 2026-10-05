# Third-Party Notices

This file tracks third-party components that are shipped, linked, or materially used by QuietCapture.

| Component | Version | Purpose | License / redistribution status |
| --- | --- | --- | --- |
| ScreenRecorderLib | 7.0.1 | Phase 0 recording backend candidate | MIT; native/runtime redistribution requirements still need release audit |
| NAudio | TBD | Planned Windows audio integration | Verify when package version is selected |
| xUnit | 2.8.1 | Test framework | Development-only dependency; verify before release audit |
| coverlet.collector | 6.0.2 | Test coverage collection | Development-only dependency; verify before release audit |

ScreenRecorderLib is currently referenced only by the isolated Phase 0 spike project. Product projects do not depend on it directly.

If FFmpeg becomes part of a later backend route, document the exact binary source, configuration, codec set, and LGPL/GPL obligations before distribution.
