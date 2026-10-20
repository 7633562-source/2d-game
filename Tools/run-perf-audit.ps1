# Headless performance measure: wall time and balance metrics.
# The optimizer runs this between executor edit cycles.
#
#   powershell -Command ".\Tools\run-perf-audit.ps1 -Cycle 1 -Rebuild"

param(
    [int]$Cycle = 0,
    [switch]$Rebuild,
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$OutFile
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'TrialEnv.ps1')

$paths = Resolve-TrialPaths -ProjectPath $ProjectPath -NeedUnity:$Rebuild
$runTrial = Join-Path $PSScriptRoot 'run-trial.ps1'

if (-not $OutFile) {
    $suffix = if ($Cycle -gt 0) { "_cycle$Cycle" } else { "_$(Get-Date -Format 'yyyyMMdd_HHmmss')" }
    $OutFile = Join-Path $paths.TrialFolder "perf_audit$suffix.json"
}

function Invoke-PerfTrial {
    param(
        [string]$Label,
        [double]$Duration,
        [string[]]$Extra = @(),
        [switch]$DoRebuild
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $trialArgs = @{
        Duration    = $Duration
        Label       = $Label
        Extra       = $Extra
        ProjectPath = $ProjectPath
    }
    if ($DoRebuild) { $trialArgs.Rebuild = $true }

    & $runTrial @trialArgs
    if ($LASTEXITCODE -ne 0) { throw "Trial $Label failed exit=$LASTEXITCODE" }
    $sw.Stop()

    $jsonPath = Join-Path $paths.TrialFolder "$Label.json"
    $summary = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json

    $wallSec = if ($null -ne $summary.PSObject.Properties['wallSeconds']) {
        [double]$summary.wallSeconds
    } else {
        $sw.Elapsed.TotalSeconds
    }
    $sim = [double]$summary.simulatedSeconds
    $rtf = if ($wallSec -gt 0) { $sim / $wallSec } else { 0 }
    $lp = [Environment]::ProcessorCount

    [pscustomobject]@{
        label              = $Label
        durationRequested  = $Duration
        simulatedSeconds   = $sim
        wallSeconds        = [math]::Round($wallSec, 3)
        realtimeFactor     = [math]::Round($rtf, 3)
        exitCode           = 0
        fell               = $summary.fell
        maxAbsTorsoTilt    = $summary.maxAbsTorsoTilt
        pelvisDrop         = $summary.pelvisDrop
        rmsComOffset       = $summary.rmsComOffset
        muscleSaturation   = $summary.muscleSaturationFraction
        hostWallSeconds    = [math]::Round($sw.Elapsed.TotalSeconds, 3)
        avgMachinePct      = [math]::Round(($rtf * 100) / $lp, 2)
        extra              = ($Extra -join ' ')
    }
}

$manifestPath = Join-Path (Split-Path $paths.ExePath -Parent) 'build-manifest.json'
$fingerprint = $null
if (Test-Path -LiteralPath $manifestPath) {
    $fingerprint = (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json).fingerprint
}

Write-Host "Perf audit cycle=$Cycle fingerprint=$fingerprint" -ForegroundColor Cyan

$firstRebuild = [bool]$Rebuild
$trials = @(
    Invoke-PerfTrial -Label "perf_c${Cycle}_stand" -Duration 10 -DoRebuild:$firstRebuild
    Invoke-PerfTrial -Label "perf_c${Cycle}_walk" -Duration 15 -Extra @('-walk','1','-walkTime','5','-walkDuration','25')
)

$regression = Invoke-PerfTrial -Label "perf_c${Cycle}_reg" -Duration 30

$report = [ordered]@{
    capturedUtc         = (Get-Date).ToUniversalTime().ToString('o')
    cycle               = $Cycle
    logicalProcessors   = [Environment]::ProcessorCount
    cpuName             = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name
    ramGB               = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2)
    unityEditorRunning  = [bool](Get-Process Unity -ErrorAction SilentlyContinue)
    fingerprint         = $fingerprint
    trials              = $trials
    regression30s       = $regression
}

$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutFile -Encoding UTF8
Write-Host "Saved $OutFile" -ForegroundColor Green
$report | ConvertTo-Json -Depth 6
