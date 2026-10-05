# G0-7 Results

Gate status: **NOT RUN**

## Environment

| Item | Value |
| --- | --- |
| Windows version/build | TBD |
| GPU / driver | TBD |
| Monitor count | TBD |
| Virtual desktop bounds | TBD |
| ScreenRecorderLib | 7.0.1 |
| Process DPI awareness | PerMonitorV2 |
| Process architecture | x64 |

## Display snapshot

| Device | Interface ID | Bounds | Work area | DPI | Scale | Primary | Negative origin |
| --- | --- | --- | --- | --- | --- | --- | --- |
| TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD |

## Window lifecycle

| Case | Action | Capture behavior | Canvas/output behavior | Recorder behavior | DPI/monitor evidence | Result |
| --- | --- | --- | --- | --- | --- | --- |
| F1 | Move | TBD | TBD | TBD | TBD | NOT RUN |
| F2 | Resize | TBD | TBD | TBD | TBD | NOT RUN |
| F3 | Minimize | TBD | TBD | TBD | TBD | NOT RUN |
| F4 | Restore | TBD | TBD | TBD | TBD | NOT RUN |
| F5 | Destroy | TBD | TBD | TBD | TBD | NOT RUN |
| F6 | Cross-monitor move | TBD | TBD | TBD | TBD | NOT RUN |

## Monitor capture APIs

| Case | Monitor | API | Cursor | Stability | Geometry | Result |
| --- | --- | --- | --- | --- | --- | --- |
| F7 | primary | Desktop Duplication | TBD | TBD | TBD | NOT RUN |
| F8 | primary | Windows Graphics Capture | TBD | TBD | TBD | NOT RUN |
| F9 | secondary / negative origin | both | TBD | TBD | TBD | NOT RUN |
| F10 | topology/resolution change | chosen candidate | TBD | TBD | TBD | NOT RUN |

## Area / coordinates

| Case | Monitor origin | SourceRect local | Expected virtual rect | API | Captured region | Result |
| --- | --- | --- | --- | --- | --- | --- |
| F11 | primary | TBD | TBD | Desktop Duplication | TBD | NOT RUN |
| F12 | primary | same rect | same | WGC vs DD | TBD | NOT RUN |
| F13 | negative if available | TBD | TBD | both | TBD | NOT RUN |
| F14 | boundary | TBD | TBD | chosen candidate | TBD | NOT RUN |

## DPI matrix

| Case | Source monitor scale | Destination scale | Physical window rect preserved? | GetDpiForWindow change | Capture geometry | Result |
| --- | ---: | ---: | --- | --- | --- | --- |
| 100% → 100% | 100% | 100% | TBD | TBD | TBD | NOT RUN |
| 100% → 150% | 100% | 150% | TBD | TBD | TBD | NOT RUN |
| 150% → 100% | 150% | 100% | TBD | TBD | TBD | NOT RUN |
| mixed-DPI Area | mixed | n/a | n/a | n/a | TBD | NOT RUN |

## Architecture decisions to freeze after G0-7

| Decision | Result |
| --- | --- |
| Window resize policy | TBD |
| Window minimize policy | TBD |
| Window destroy policy | TBD |
| Cross-monitor Window policy | TBD |
| Monitor capture API | TBD |
| Area capture API | TBD |
| Area coordinate transform | TBD |
| Topology-change policy | TBD |

## Findings

TBD after Windows execution.

## Gate decision

TBD.
