[CmdletBinding()]
param(
    [ValidateSet(
        "list",
        "g0-0",
        "g0-1",
        "g0-2",
        "g0-3",
        "g0-4",
        "g0-5",
        "g0-6",
        "g0-7",
        "all")]
    [string]$Gate = "list",

    [switch]$ContinueOnError
)

$ErrorActionPreference = "Stop"

$spikeRoot = $PSScriptRoot
$phase0Root = Join-Path $env:TEMP "QuietCaptureSpike\Phase0"
$prerequisitePath = Join-Path $phase0Root "g0-0-prerequisite.json"

$gateScripts = [ordered]@{
    "g0-1" = "run-g0-1.ps1"
    "g0-2" = "run-g0-2.ps1"
    "g0-3" = "run-g0-3.ps1"
    "g0-4" = "run-g0-4.ps1"
    "g0-5" = "run-g0-5.ps1"
    "g0-6" = "run-g0-6.ps1"
    "g0-7" = "run-g0-7.ps1"
}

function Get-FileSystemInfo {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $root = [System.IO.Path]::GetPathRoot([System.IO.Path]::GetFullPath($Path))
    $driveName = $root.TrimEnd("\").TrimEnd(":")

    try {
        $volume = Get-Volume -DriveLetter $driveName -ErrorAction Stop

        return [ordered]@{
            Root = $root
            FileSystem = [string]$volume.FileSystem
            SizeRemainingBytes = [int64]$volume.SizeRemaining
            SizeBytes = [int64]$volume.Size
        }
    }
    catch {
        $drive = [System.IO.DriveInfo]::new($root)

        return [ordered]@{
            Root = $root
            FileSystem = [string]$drive.DriveFormat
            SizeRemainingBytes = [int64]$drive.AvailableFreeSpace
            SizeBytes = [int64]$drive.TotalSize
        }
    }
}

function Test-G00Prerequisite {
    New-Item -ItemType Directory -Force -Path $phase0Root | Out-Null

    $checks = New-Object System.Collections.Generic.List[object]
    $isWindows = $env:OS -eq "Windows_NT"

    $osVersion = [Environment]::OSVersion.Version
    $windowsBuild = if ($isWindows) { [int]$osVersion.Build } else { 0 }

    $checks.Add([ordered]@{
        Name = "WindowsBuild"
        Passed = $isWindows -and $windowsBuild -ge 19041
        Required = "Windows build >= 19041"
        Actual = if ($isWindows) { $osVersion.ToString() } else { [Environment]::OSVersion.VersionString }
    })

    $checks.Add([ordered]@{
        Name = "X64OperatingSystem"
        Passed = [Environment]::Is64BitOperatingSystem
        Required = "x64 Windows"
        Actual = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    })

    $checks.Add([ordered]@{
        Name = "X64Process"
        Passed = [Environment]::Is64BitProcess
        Required = "x64 process"
        Actual = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    })

    $dotnetVersion = $null
    try {
        $dotnetVersion = (& dotnet --version 2>$null | Select-Object -First 1).Trim()
    }
    catch {
        $dotnetVersion = $null
    }

    $dotnetMajor = 0
    $parsedVersion = $null

    if ($dotnetVersion -and
        [Version]::TryParse(
            $dotnetVersion.Split("-")[0],
            [ref]$parsedVersion)) {
        $dotnetMajor = $parsedVersion.Major
    }

    $checks.Add([ordered]@{
        Name = "DotNetSdk"
        Passed = $dotnetMajor -ge 8
        Required = ".NET SDK 8+"
        Actual = if ($dotnetVersion) { $dotnetVersion } else { "not found" }
    })

    $system32 = Join-Path $env:WINDIR "System32"

    foreach ($runtimeFile in @("vcruntime140.dll", "msvcp140.dll")) {
        $path = Join-Path $system32 $runtimeFile

        $checks.Add([ordered]@{
            Name = "VisualCpp:$runtimeFile"
            Passed = Test-Path $path
            Required = "$runtimeFile present"
            Actual = if (Test-Path $path) { $path } else { "missing" }
        })
    }

    foreach ($mfFile in @("mf.dll", "mfplat.dll", "mfreadwrite.dll")) {
        $path = Join-Path $system32 $mfFile

        $checks.Add([ordered]@{
            Name = "MediaFoundation:$mfFile"
            Passed = Test-Path $path
            Required = "$mfFile present"
            Actual = if (Test-Path $path) { $path } else { "missing" }
        })
    }

    $writeProbe = Join-Path $phase0Root "write-probe.tmp"
    $writePassed = $false
    $writeActual = $null

    try {
        [System.IO.File]::WriteAllText($writeProbe, "QuietCapture Phase 0 write probe")
        Remove-Item -Force $writeProbe
        $writePassed = $true
        $writeActual = $phase0Root
    }
    catch {
        $writeActual = $_.Exception.Message
    }

    $checks.Add([ordered]@{
        Name = "EvidenceRootWritable"
        Passed = $writePassed
        Required = "writable Phase 0 evidence root"
        Actual = $writeActual
    })

    $storage = $null

    try {
        $storage = Get-FileSystemInfo -Path $phase0Root

        $checks.Add([ordered]@{
            Name = "EvidenceFileSystem"
            Passed = $storage.FileSystem -ne "FAT32"
            Required = "filesystem other than FAT32"
            Actual = $storage.FileSystem
        })
    }
    catch {
        $checks.Add([ordered]@{
            Name = "EvidenceFileSystem"
            Passed = $false
            Required = "readable filesystem information"
            Actual = $_.Exception.Message
        })
    }

    $blocked = @($checks | Where-Object { -not $_.Passed }).Count -gt 0

    $report = [ordered]@{
        Gate = "G0-0"
        Kind = "Prerequisite"
        Status = if ($blocked) { "BLOCKED" } else { "READY" }
        TimestampUtc = [DateTimeOffset]::UtcNow.ToString("O")
        MachineName = $env:COMPUTERNAME
        OsDescription = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        OsArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        ProcessArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        DotNetSdkVersion = $dotnetVersion
        EvidenceRoot = $phase0Root
        Storage = $storage
        Checks = $checks
        ManualGateSpecificChecks = [ordered]@{
            G02 = "working system-audio output device"
            G03 = "working system-audio output plus microphone/capture device"
            G05 = "supported Windows capture-exclusion environment"
            G07 = "multiple monitors required only for cross-monitor/mixed-DPI cases"
        }
    }

    $report |
        ConvertTo-Json -Depth 8 |
        Set-Content -Path $prerequisitePath -Encoding UTF8

    Write-Host ""
    Write-Host "G0-0 prerequisite: $($report.Status)"
    Write-Host "Report: $prerequisitePath"
    Write-Host ""

    foreach ($check in $checks) {
        $mark = if ($check.Passed) { "[PASS]" } else { "[FAIL]" }
        Write-Host "$mark $($check.Name) — $($check.Actual)"
    }

    return -not $blocked
}

function Invoke-Gate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$GateName
    )

    if (-not $gateScripts.Contains($GateName)) {
        throw "Unknown runtime gate: $GateName"
    }

    $scriptPath = Join-Path $spikeRoot $gateScripts[$GateName]

    if (-not (Test-Path $scriptPath)) {
        throw "Harness script not found: $scriptPath"
    }

    Write-Host ""
    Write-Host "============================================================"
    Write-Host "Starting $GateName via $scriptPath"
    Write-Host "Close the harness after completing the intended evidence run."
    Write-Host "============================================================"
    Write-Host ""

    & powershell -ExecutionPolicy Bypass -File $scriptPath

    if ($LASTEXITCODE -ne 0) {
        throw "$GateName harness exited with code $LASTEXITCODE."
    }
}

if ($Gate -eq "list") {
    Write-Host "QuietCapture Phase 0 launcher"
    Write-Host ""
    Write-Host "g0-0  Clean-machine prerequisite"
    foreach ($entry in $gateScripts.GetEnumerator()) {
        Write-Host ("{0,-5} {1}" -f $entry.Key, $entry.Value)
    }
    Write-Host ""
    Write-Host "all   G0-0 prerequisite, then G0-1 through G0-7 sequentially"
    exit 0
}

if ($Gate -eq "g0-0") {
    if (Test-G00Prerequisite) {
        exit 0
    }

    exit 2
}

if ($Gate -eq "all") {
    $ready = Test-G00Prerequisite

    if (-not $ready) {
        Write-Error "G0-0 is BLOCKED. Runtime gates were not started."
        exit 2
    }

    foreach ($gateName in $gateScripts.Keys) {
        try {
            Invoke-Gate -GateName $gateName
        }
        catch {
            Write-Error $_

            if (-not $ContinueOnError) {
                exit 3
            }
        }
    }

    Write-Host ""
    Write-Host "Phase 0 assisted sequence finished."
    Write-Host "No gate status was changed automatically."
    exit 0
}

Invoke-Gate -GateName $Gate
