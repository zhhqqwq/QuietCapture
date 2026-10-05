# QuietCapture

QuietCapture is a minimal Windows screen recorder built around a short workflow: **select → audio → record**.

> Development status: **Phase 0 technical spike**. The repository currently contains the engineering scaffold and validation plan; recording behavior is not yet frozen for production use.

## Goals

- Keep the primary recording flow small and fast.
- Prefer reliable recording and recoverable output over feature count.
- Use Windows-native capture and platform integration behind explicit architecture boundaries.
- Validate uncertain capture behavior before freezing backend contracts.

## Planned v1 scope

- Area, window, and monitor capture.
- System audio and microphone recording.
- MP4 output with crash/recovery behavior validated during Phase 0.
- Recording status UI and border overlays excluded from capture where the Windows capture path supports it.
- Session metadata and recovery handling for interrupted recordings.

## Architecture

```text
QuietCapture.UI.Wpf → QuietCapture.Core ← QuietCapture.Infrastructure.Windows
          ↑                    ↑                    ↑
          └──────────────── QuietCapture.App ──────┘
```

`QuietCapture.Core` owns product state and policies. Windows APIs, ScreenRecorderLib, NAudio, and other platform dependencies belong in `QuietCapture.Infrastructure.Windows`. `QuietCapture.App` is the composition root.

See [`docs/engineering-baseline.md`](docs/engineering-baseline.md) for the current engineering baseline, [`docs/technical-spike.md`](docs/technical-spike.md) for Phase 0 results, and [`docs/phase0-execution-guide.md`](docs/phase0-execution-guide.md) for the frozen Phase 0 runtime procedure.

## Requirements

- Windows 10 version 2004 (build 19041) or later, or Windows 11.
- x64 Windows for the current Phase 0 ScreenRecorderLib route.
- .NET 8 SDK.
- A Windows development environment capable of building WPF projects.

## Build

```powershell
dotnet restore QuietCapture.sln -p:Platform=x64
dotnet build QuietCapture.sln --configuration Release -p:Platform=x64 --no-restore
dotnet test QuietCapture.sln --configuration Release -p:Platform=x64 --no-build
```

## Repository layout

```text
src/      Product code
spikes/   Phase 0 experiments used to resolve technical unknowns
tests/    Tests for production code
docs/     Engineering baseline, spike evidence, architecture, and ADRs
```

## Development roadmap

1. Repository bootstrap.
2. Phase 0 technical spike and G0 gates.
3. Architecture freeze based on spike evidence.
4. MVP implementation.
5. Stability, recovery, packaging, and release validation.

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md).

## Security

See [`SECURITY.md`](SECURITY.md).

## License

QuietCapture is licensed under the MIT License. See [`LICENSE`](LICENSE).
