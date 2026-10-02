# World Play Mode profile: Unity batchmode + WorldPerfBatch.Run (~35 s).
# Writes E:\Games\Unity3D LOGS\world_perf.txt via WorldPerformanceMonitor.
#
#   powershell -Command ".\Tools\run-world-perf.ps1 -Cycle 2"

param(
    [int]$Cycle = 0,
    [switch]$Graphics,
    [ValidateSet('', 'flat', 'test1', 'course', 'steps')]
    [string]$LevelProfile = '',
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$UnityPath,
    [string]$LogFile = "E:\Games\Unity3D LOGS\world_perf.txt"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'TrialEnv.ps1')
$paths = Resolve-TrialPaths -ProjectPath $ProjectPath -UnityPath $UnityPath -NeedUnity

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$unityLog = Join-Path $env:TEMP "unity_world_perf_c${Cycle}_$stamp.log"
$capturePath = Join-Path $paths.TrialFolder 'world_perf_capture.json'

if (Test-Path -LiteralPath $LogFile) {
    $archive = $LogFile -replace '\.txt$', "_cycle${Cycle}_$stamp.txt"
    Copy-Item -LiteralPath $LogFile -Destination $archive -Force
    Remove-Item -LiteralPath $LogFile -Force
}

$levelFlag = Join-Path $PSScriptRoot 'force-level.flag'
$wroteLevelFlag = $false
if ($LevelProfile) {
    Set-Content -LiteralPath $levelFlag -Value $LevelProfile.Trim().ToLowerInvariant() -Encoding ascii -NoNewline
    $wroteLevelFlag = $true
}

$editorAlreadyOpen = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -like '*\Editor\Unity.exe'
}).Count -gt 0
if ($editorAlreadyOpen) {
    if ($wroteLevelFlag -and (Test-Path -LiteralPath $levelFlag)) { Remove-Item -LiteralPath $levelFlag -Force }
    throw "Unity Editor is already open; World batch would lock Library. Close it or skip this run."
}

# A run owns this fixed-name capture. Remove it only after the Library lock check.
if (Test-Path -LiteralPath $capturePath) {
    Remove-Item -LiteralPath $capturePath -Force
}

Write-Host "World perf cycle=$Cycle profile=$(if ($LevelProfile) { $LevelProfile } else { 'scene' }) graphics=$Graphics ..." -ForegroundColor Cyan

try {
# A path with spaces cannot be passed to Start-Process as an array — Unity splits it.
$argLine = '-batchmode -projectPath "{0}" -executeMethod WorldPerfBatch.Run -logFile "{1}"' -f $paths.ProjectPath, $unityLog
if (-not $Graphics) { $argLine = '-nographics ' + $argLine }

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$runStartedUtc = (Get-Date).ToUniversalTime()
$timedOut = $false
Set-StrictMode -Off
$player = Start-Process -FilePath $paths.UnityPath -ArgumentList $argLine -PassThru
if (-not $player) { throw 'Unity.exe failed to start.' }

$goneDeadline = (Get-Date).AddSeconds(180)
do {
    Start-Sleep -Seconds 2
    try { $player.Refresh() } catch { break }
} while (-not $player.HasExited -and (Get-Date) -lt $goneDeadline)

if (-not $player.HasExited) {
    $timedOut = $true
    $exit = 2
    try {
        # Kill only the disposable batch process tree started by this run.
        $null = & taskkill.exe /PID $player.Id /T /F 2>&1
        $null = $player.WaitForExit(10000)
    }
    catch {
        Write-Warning "Failed to terminate timed-out Unity batch PID $($player.Id): $($_.Exception.Message)"
    }
}
else {
    try { $exit = $player.ExitCode } catch { $exit = 0 }
    if ($null -eq $exit) { $exit = 0 }
}
Set-StrictMode -Version Latest
$sw.Stop()
}
finally {
    if ($wroteLevelFlag -and (Test-Path -LiteralPath $levelFlag)) {
        Remove-Item -LiteralPath $levelFlag -Force
    }
}

Write-Host "Unity exit=$exit wall=$([math]::Round($sw.Elapsed.TotalSeconds,1))s log=$unityLog" -ForegroundColor $(if ($exit -eq 0) { 'Green' } else { 'Yellow' })

$summaryPath = Join-Path $paths.TrialFolder "world_perf_summary_cycle${Cycle}.json"

$inv = [System.Globalization.CultureInfo]::InvariantCulture
$lines = @()
if (Test-Path -LiteralPath $LogFile) {
    $lines = @(Get-Content -LiteralPath $LogFile | Where-Object { $_ -match '^WORLD_PERF ' })
}

$parsed = foreach ($line in $lines) {
    $row = [ordered]@{ raw = $line }
    foreach ($pair in ($line -replace '^WORLD_PERF ', '' -split ' ')) {
        if ($pair -match '^([^=]+)=(.+)$') {
            $row[$Matches[1]] = ($Matches[2] -replace ',', '.')
        }
    }
    [pscustomobject]$row
}

function Read-PerfDouble($rows, $name) {
    @($rows) | ForEach-Object { [double]::Parse([string]$_.$name, $inv) }
}

$avgFps = if (@($parsed).Count -gt 0) { (Read-PerfDouble $parsed 'fps' | Measure-Object -Average).Average } else { $null }
$maxFrame = if (@($parsed).Count -gt 0) { (Read-PerfDouble $parsed 'frameMs_max' | Measure-Object -Maximum).Maximum } else { $null }
$maxSteps = if (@($parsed).Count -gt 0) { (@($parsed) | ForEach-Object { [int]$_.fixedSteps_max } | Measure-Object -Maximum).Maximum } else { $null }
$avgOver = if (@($parsed).Count -gt 0) { (Read-PerfDouble $parsed 'overBudget16ms' | Measure-Object -Average).Average } else { $null }

$capture = $null
$captureAnnounced = $false
if (Test-Path -LiteralPath $unityLog) {
    $captureAnnounced = [bool](Select-String -LiteralPath $unityLog -Pattern 'WORLD_PERF_JSON ' -Quiet)
}
$captureFresh = $false
if (Test-Path -LiteralPath $capturePath) {
    $captureFresh = (Get-Item -LiteralPath $capturePath).LastWriteTimeUtc -ge $runStartedUtc
}

$valid = $exit -eq 0 -and @($parsed).Count -gt 0 -and $captureAnnounced -and $captureFresh
if ($valid) {
    try {
        $capture = Get-Content -LiteralPath $capturePath -Raw | ConvertFrom-Json
        $valid = $null -ne $capture -and [int]$capture.sampleFrames -gt 0
    }
    catch {
        $capture = $null
        $valid = $false
    }
}

if ($valid -and $LevelProfile) {
    $named = Join-Path $paths.TrialFolder "world_perf_capture_$LevelProfile.json"
    Copy-Item -LiteralPath $capturePath -Destination $named -Force
}
elseif (-not $valid) {
    $capture = $null
    if (Test-Path -LiteralPath $capturePath) {
        Remove-Item -LiteralPath $capturePath -Force
    }
}

$report = [ordered]@{
    capturedUtc      = (Get-Date).ToUniversalTime().ToString('o')
    cycle            = $Cycle
    levelProfile     = $(if ($LevelProfile) { $LevelProfile } else { 'scene' })
    graphics         = [bool]$Graphics
    valid            = [bool]$valid
    unityExitCode    = $exit
    timedOut         = [bool]$timedOut
    hostWallSeconds  = [math]::Round($sw.Elapsed.TotalSeconds, 1)
    logFile          = $LogFile
    unityLog         = $unityLog
    sampleCount      = @($parsed).Count
    captureAnnounced = [bool]$captureAnnounced
    captureFresh     = [bool]$captureFresh
    fps_avg          = if ($avgFps) { [math]::Round($avgFps, 1) } else { $null }
    frameMs_max      = if ($maxFrame) { [math]::Round($maxFrame, 2) } else { $null }
    fixedSteps_max   = $maxSteps
    overBudget16_avg = if ($avgOver) { [math]::Round($avgOver, 3) } else { $null }
    capture          = $capture
    samplesOmitted   = $true
}

$json = $report | ConvertTo-Json -Depth 6
Set-Content -LiteralPath $summaryPath -Value $json -Encoding UTF8
$captureP95 = if ($capture) { $capture.p95FrameMs } else { $null }
$capturePhysics = if ($capture) { $capture.physics2dAvgMs } else { $null }
Write-Host "Summary $summaryPath valid=$valid fps_avg=$($report.fps_avg) p95=$captureP95 phys=$capturePhysics" -ForegroundColor $(if ($valid) { 'Green' } else { 'Yellow' })

if (-not $valid) {
    $failureExit = if ($exit -ne 0) { $exit } else { 3 }
    exit $failureExit
}
