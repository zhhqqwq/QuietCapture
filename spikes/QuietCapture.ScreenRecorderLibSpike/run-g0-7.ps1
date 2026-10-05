$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "QuietCapture.ScreenRecorderLibSpike.csproj"

Write-Host "Restoring G0-7 spike (x64)..."
dotnet restore $project -p:Platform=x64

Write-Host "Running G0-7 Window + Monitor + DPI controller (x64)..."
dotnet run --project $project --configuration Release -p:Platform=x64 --no-restore -- --gate=g0-7
