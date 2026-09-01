# Запуск headless-прогона баланса.
#
# Примеры (из корня проекта или из Tools):
#   .\run-trial.ps1 -Rebuild                       # собрать плеер и прогнать 10 секунд
#   .\run-trial.ps1 -Duration 20 -Label strongHip -Extra @('-hipP','80')
#   .\run-trial.ps1 -Label hz200 -Extra @('-fixedDelta','0.005','-activationSpeed','10')
#
# Из внешней консоли вызывать через -Command, а не -File: при -File массив
# в -Extra приходит строкой и переопределения молча игнорируются.
#   powershell -Command ".\run-trial.ps1 -Label test -Extra @('-muscle','0.1')"
#
# Перед пересборкой Unity Editor должен быть закрыт: редактор держит папку Library.
# Уже собранный плеер можно запускать при открытом редакторе.

param(
    [double]$Duration = 10,
    [string]$Label = "base",
    [switch]$Rebuild,
    [string[]]$Extra = @(),
    [string]$UnityPath,
    [string]$BuildFolder,
    [string]$TrialFolder,
    [string]$ProjectPath,
    [switch]$FreshnessOnly
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
    Write-Host "Билд свежий. fingerprint=$($check.Fingerprint)" -ForegroundColor Green
    $check.Manifest | ConvertTo-Json -Depth 4
    return
}

if ($mustRebuild) {
    Write-Host "Собираю плеер..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $paths.BuildFolder | Out-Null

    $unityArgs = @(
        '-batchmode', '-nographics',
        '-projectPath', $paths.ProjectPath,
        '-executeMethod', 'HeadlessBuild.BuildTrialPlayer',
        '-buildOut', $paths.BuildFolder,
        '-logFile', '-'
    )
    & $paths.UnityPath @unityArgs | Tee-Object -Variable buildLog | Out-Null

    if ($LASTEXITCODE -ne 0) {
        $buildLog | Select-String -Pattern "error CS|TRIAL_BUILD_FAILED" | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        throw "Сборка не удалась (код $LASTEXITCODE)."
    }

    if (-not (Test-Path -LiteralPath $paths.ExePath)) {
        throw "Сборка завершилась без плеера: $($paths.ExePath)"
    }

    $fingerprint = Get-TrialSourceFingerprint -ProjectPath $paths.ProjectPath
    Write-TrialBuildManifest -Paths $paths -Fingerprint $fingerprint
    Write-Host "Плеер собран: $($paths.ExePath)" -ForegroundColor Green
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

Write-Host "Прогон '$Label' на $Duration с..." -ForegroundColor Cyan

$jsonPath = Join-Path $paths.TrialFolder ($Label + ".json")
if (Test-Path -LiteralPath $jsonPath) { Remove-Item -LiteralPath $jsonPath }

# Путь берём в кавычки: Start-Process не экранирует пробелы сам.
$durationText = [string]::Format([cultureinfo]::InvariantCulture, "{0}", $Duration)
$playerArgs = @(
    "-batchmode", "-nographics", "-trial",
    "-duration", $durationText,
    "-label", $Label,
    "-out", "`"$($paths.TrialFolder)`"",
    "-sourceFingerprint", $builtFingerprint,
    "-buildManifest", "`"$($paths.ManifestPath)`""
)

if (-not (Test-ExtraHasSwitch -Extra $Extra -Name '-fixedDelta')) {
    $playerArgs += @('-fixedDelta', '0.005')
}

if ($Extra) {
    $playerArgs += $Extra
}

# Start-Process -Wait обязателен: плеер в batchmode отвязывается от консоли,
# и обычный вызов возвращает управление до конца прогона.
$player = Start-Process -FilePath $paths.ExePath -ArgumentList $playerArgs -Wait -NoNewWindow -PassThru
if ($player.ExitCode -ne 0) {
    throw "Плеер завершился с кодом $($player.ExitCode). Сводку не принимаю, даже если JSON появился."
}

if (-not (Test-Path -LiteralPath $jsonPath)) {
    throw "Прогон '$Label' не записал сводку: $jsonPath"
}

Get-Content -LiteralPath $jsonPath -Raw
Write-Host "CSV: $(Join-Path $paths.TrialFolder ($Label + '.csv'))" -ForegroundColor Green
