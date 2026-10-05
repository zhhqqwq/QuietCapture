$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "QuietCapture.ScreenRecorderLibSpike.csproj"

Write-Host "Restoring G0-5 spike (x64)..."
dotnet restore $project -p:Platform=x64

Write-Host "Running G0-5 capture exclusion controller (x64)..."
dotnet run --project $project --configuration Release -p:Platform=x64 --no-restore -- --gate=g0-5
