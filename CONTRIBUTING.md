# Contributing to QuietCapture

QuietCapture is currently in Phase 0. Contributions should preserve the project's minimal interaction model and keep experimental code separate from production architecture.

## Before changing code

- Read docs/engineering-baseline.md.
- Read docs/technical-spike.md for the current state of G0 validation.
- For behavior that depends on Windows capture APIs or third-party recorder behavior, add evidence before turning the behavior into a production contract.

## Repository boundaries

- src/QuietCapture.Core must not reference WPF, Win32 UI types, ScreenRecorderLib, NAudio, or Windows capture implementations.
- src/QuietCapture.UI.Wpf may reference QuietCapture.Core, but not QuietCapture.Infrastructure.Windows.
- src/QuietCapture.Infrastructure.Windows implements platform concerns behind Core-owned contracts.
- src/QuietCapture.App is the composition root.
- spikes/ is experimental code and does not define production architecture.

## Development workflow

1. Create a focused branch from main.
2. Keep each change scoped to one problem or decision.
3. Add or update tests for production behavior when applicable.
4. Update docs/technical-spike.md when a G0 experiment changes a technical conclusion.
5. Open a pull request with the problem, evidence, decision, and validation steps.

## Commit messages

Use short imperative messages. Conventional Commit prefixes are preferred when they improve history readability, for example:

~~~text
chore: bootstrap repository
spike: verify area capture
test: cover pending stop behavior
fix: preserve interrupted session metadata
~~~

## Build and test

QuietCapture currently uses an x64 solution platform because the Phase 0 ScreenRecorderLib package rejects Any CPU builds.

~~~powershell
dotnet restore QuietCapture.sln -p:Platform=x64
dotnet build QuietCapture.sln --configuration Release -p:Platform=x64 --no-restore
dotnet test QuietCapture.sln --configuration Release -p:Platform=x64 --no-build
~~~

## Scope changes

Feature requests should describe the user problem and the expected workflow. New controls or settings need a clear reason to exist in a product whose primary goal is minimal recording friction.
