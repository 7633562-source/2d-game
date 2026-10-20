# Run a headless balance trial.
#
# Examples (from the project root or from Tools):
#   .\run-trial.ps1 -Rebuild                       # build the player and run 10 seconds
#   .\run-trial.ps1 -Duration 20 -Label strongHip -Extra @('-hipP','80')
#   .\run-trial.ps1 -Label hz200 -Extra @('-fixedDelta','0.005','-activationSpeed','10')
#   .\run-trial.ps1 -Rebuild -Duration 30 -Label dog_c1_stand -Extra @('-dog','1')
#
# From an outer console call via -Command, not -File: with -File the -Extra
# array arrives as one string and overrides are silently ignored.
#   powershell -Command ".\run-trial.ps1 -Label test -Extra @('-muscle','0.1')"
#
# Close the Unity Editor before -Rebuild: the editor holds Library.
# An already-built player may run while the editor is open.

param(
    [double]$Duration = 10,
    [string]$Label = "base",
    [switch]$Rebuild,
    [string[]]$Extra = @(),
    [string]$UnityPath,
    [string]$BuildFolder,
    [string]$TrialFolder,
    [string]$ProjectPath,
    [switch]$FreshnessOnly,
    [switch]$Visual
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'TrialEnv.ps1')

$paths = Resolve-TrialPaths -ProjectPath $ProjectPath -UnityPath $UnityPath -BuildFolder $BuildFolder -TrialFolder $TrialFolder -NeedUnity:$false

$exeExists = Test-Path -LiteralPath $paths.ExePath
$mustRebuild = [bool]$Rebuild -or (-not $exeExists)

if ($mustRebuild) {
    $paths = Resolve-TrialPaths -ProjectPath $ProjectPath -UnityPath $UnityPath -BuildFolder $BuildFolder -TrialFolder $TrialFolder -NeedUnity
    Assert-UnityEditorClosedForRebuild
}

if ($FreshnessOnly) {
    $check = Test-TrialBuildFreshness -Paths $paths
    Write-Host "Build is fresh. fingerprint=$($check.Fingerprint)" -ForegroundColor Green
    $check.Manifest | ConvertTo-Json -Depth 4
    return
}

if ($mustRebuild) {
    Write-Host "Building the player..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $paths.BuildFolder | Out-Null

    $unityArgs = @(
        '-batchmode', '-nographics',
        '-projectPath', $paths.ProjectPath,
        '-executeMethod', 'HeadlessBuild.BuildTrialPlayer',
        '-buildOut', $paths.BuildFolder,
        '-logFile', '-'
    )
    # Run the editor as a child process: a native command inside a PowerShell
    # pipeline is fragile here (it died with CantActivateDocumentInPipeline).
    # Quote arguments with spaces: Start-Process does not escape them itself.
    $buildOut = Join-Path $paths.BuildFolder 'unity-build.out.log'
    $buildErr = Join-Path $paths.BuildFolder 'unity-build.err.log'
    $quoted = @($unityArgs | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } })
    $proc = Start-Process -FilePath $paths.UnityPath -ArgumentList $quoted -NoNewWindow -PassThru -RedirectStandardOutput $buildOut -RedirectStandardError $buildErr
    # Never wait forever: a wedged editor blocked a whole cycle silently once.
    if (-not $proc.WaitForExit(900000)) {
        try { $proc.Kill() } catch { }
        throw "Build timed out after 15 min."
    }
    $buildLog = @()
    if (Test-Path -LiteralPath $buildOut) { $buildLog += @(Get-Content -LiteralPath $buildOut -ErrorAction SilentlyContinue) }
    if (Test-Path -LiteralPath $buildErr) { $buildLog += @(Get-Content -LiteralPath $buildErr -ErrorAction SilentlyContinue) }
    $buildExit = $proc.ExitCode
    if ($null -eq $buildExit -or "$buildExit" -eq "") {
        # Start-Process -PassThru sometimes reports an EMPTY exit code for the
        # Unity launcher here even when the build succeeded (the editor writes
        # TRIAL_BUILD_OK into the log; the exe check below is the second gate).
        if ($buildLog -match 'TRIAL_BUILD_OK') {
            Write-Host "Build log carries TRIAL_BUILD_OK (no exit code reported); accepting the build." -ForegroundColor Yellow
            $buildExit = 0
        }
    }
    if ($buildExit -ne 0) {
        $buildLog | Select-String -Pattern 'error CS|TRIAL_BUILD_FAILED' | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        throw "Build failed (exit $buildExit)."
    }

    if (-not (Test-Path -LiteralPath $paths.ExePath)) {
        throw "Build finished without a player: $($paths.ExePath)"
    }

    $fingerprint = Get-TrialSourceFingerprint -ProjectPath $paths.ProjectPath
    Write-TrialBuildManifest -Paths $paths -Fingerprint $fingerprint
    Write-Host "Player built: $($paths.ExePath)" -ForegroundColor Green
    Write-Host "fingerprint=$($fingerprint.Fingerprint)" -ForegroundColor Green
}
else {
    $fresh = Test-TrialBuildFreshness -Paths $paths
    $fingerprint = [pscustomobject]@{ Fingerprint = $fresh.Fingerprint }
}

$manifest = Read-TrialJsonFile -Path $paths.ManifestPath
$builtFingerprint = $null
if ($manifest) { $builtFingerprint = [string]$manifest.fingerprint }
if ([string]::IsNullOrWhiteSpace($builtFingerprint)) {
    throw "No fingerprint in build-manifest.json. Rebuild with -Rebuild; do not substitute the current source hash."
}

New-Item -ItemType Directory -Force -Path $paths.TrialFolder | Out-Null

Write-Host "Trial '$Label' for $Duration s..." -ForegroundColor Cyan

$jsonPath = Join-Path $paths.TrialFolder ($Label + ".json")
if (Test-Path -LiteralPath $jsonPath) { Remove-Item -LiteralPath $jsonPath }

# Quote the path: Start-Process does not escape spaces by itself.
$durationText = [string]::Format([cultureinfo]::InvariantCulture, "{0}", $Duration)
$playerArgs = @("-batchmode", "-trial")
if (-not $Visual) { $playerArgs += @("-nographics") }
$playerArgs += @(
    "-duration", $durationText,
    "-label", $Label,
    "-out", "`"$($paths.TrialFolder)`"",
    "-sourceFingerprint", $builtFingerprint,
    "-buildManifest", "`"$($paths.ManifestPath)`""
)
if ($Visual) { $playerArgs += @("-visual") }

if (-not (Test-ExtraHasSwitch -Extra $Extra -Name '-fixedDelta')) {
    $playerArgs += @('-fixedDelta', '0.005')
}

if ($Extra) {
    $playerArgs += $Extra
}

# Start-Process -Wait is required: the batchmode player detaches from the
# console, so a plain invoke returns before the trial ends.
$player = Start-Process -FilePath $paths.ExePath -ArgumentList $playerArgs -Wait -NoNewWindow -PassThru
if ($player.ExitCode -ne 0) {
    throw "Player exited with code $($player.ExitCode). Reject the summary even if a JSON file appeared."
}

if (-not (Test-Path -LiteralPath $jsonPath)) {
    throw "Trial '$Label' did not write a summary: $jsonPath"
}

Get-Content -LiteralPath $jsonPath -Raw
Write-Host "CSV: $(Join-Path $paths.TrialFolder ($Label + '.csv'))" -ForegroundColor Green
