# Перебор множителя моментов мышц: ищем, при какой силе человек стоит дольше всего.
#
#   .\sweep-muscle.ps1
#   .\sweep-muscle.ps1 -Values 0.05,0.1,0.2 -Duration 20
#
# Плеер должен быть уже собран (см. run-trial.ps1 -Rebuild).

param(
    [double[]]$Values = @(0.05, 0.08, 0.10, 0.15, 0.20, 0.30, 0.50),
    [double]$Duration = 10,
    [string[]]$Extra = @(),
    [string]$UnityPath,
    [string]$BuildFolder,
    [string]$TrialFolder,
    [string]$ProjectPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'TrialEnv.ps1')

$paths = Resolve-TrialPaths -ProjectPath $ProjectPath -UnityPath $UnityPath `
    -BuildFolder $BuildFolder -TrialFolder $TrialFolder

$fresh = Test-TrialBuildFreshness -Paths $paths
$builtFingerprint = [string]$fresh.Fingerprint

if (-not (Test-Path -LiteralPath $paths.ExePath)) {
    throw "Плеер не собран: $($paths.ExePath). Запустите run-trial.ps1 -Rebuild."
}

New-Item -ItemType Directory -Force -Path $paths.TrialFolder | Out-Null
$results = @()

foreach ($value in $Values) {
    $text  = [string]::Format([cultureinfo]::InvariantCulture, "{0}", $value)
    $label = "muscle_" + ($text -replace "\.", "p")

    $args = @(
        "-batchmode", "-nographics", "-trial",
        "-duration", ([string]::Format([cultureinfo]::InvariantCulture, "{0}", $Duration)),
        "-label", $label,
        "-out", "`"$($paths.TrialFolder)`"",
        "-sourceFingerprint", $builtFingerprint,
        "-buildManifest", "`"$($paths.ManifestPath)`"",
        "-muscle", $text
    )
    if (-not (Test-ExtraHasSwitch -Extra $Extra -Name '-fixedDelta')) {
        $args += @('-fixedDelta', '0.005')
    }
    if ($Extra) { $args += $Extra }

    Write-Host "muscle = $text ..." -NoNewline
    Start-Process -FilePath $paths.ExePath -ArgumentList $args -Wait -NoNewWindow

    $jsonPath = Join-Path $paths.TrialFolder ($label + ".json")
    if (-not (Test-Path -LiteralPath $jsonPath)) {
        Write-Host " нет сводки" -ForegroundColor Red
        continue
    }

    $summary = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
    $results += [pscustomobject]@{
        Muscle    = $value
        Survived  = [math]::Round($summary.survivedSeconds, 2)
        Fell      = $summary.fell
        MaxTilt   = [math]::Round($summary.maxAbsTorsoTilt, 1)
        HeadDrop  = [math]::Round($summary.headDrop, 2)
        RmsCoM    = [math]::Round($summary.rmsComOffset, 3)
        Saturated = [math]::Round($summary.muscleSaturationFraction, 2)
    }
    Write-Host " простоял $([math]::Round($summary.survivedSeconds,2)) с" -ForegroundColor Cyan
}

$results | Sort-Object -Property Survived -Descending | Format-Table -AutoSize
