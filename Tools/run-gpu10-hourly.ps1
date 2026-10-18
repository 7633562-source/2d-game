# Hourly local GPU-10 sample. Cloud Cursor automations cannot measure Unity GPU.
# Default: nvidia-smi only (does not start the editor).
# Full World graphics: -FullWorld, skipped if Unity Editor is already open.

param(
    [switch]$FullWorld,
    [int]$Cycle = 11,
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$runs = Join-Path $ProjectPath "Trials\Runs\gpu10_hourly"
New-Item -ItemType Directory -Force -Path $runs | Out-Null

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outJson = Join-Path $runs "$stamp.json"
$nvsmi = "C:\Windows\System32\nvidia-smi.exe"

$gpu = [ordered]@{
    source = "nvidia-smi utilization.gpu"
    available = $false
    avg = $null
    p95 = $null
    max = $null
    samples = 0
    note = $null
}

if (Test-Path -LiteralPath $nvsmi) {
    $vals = @()
    for ($i = 0; $i -lt 8; $i++) {
        $raw = & $nvsmi --query-gpu=utilization.gpu --format=csv,noheader,nounits 2>$null
        if ($LASTEXITCODE -eq 0 -and $raw) {
            $n = 0
            if ([int]::TryParse(("$raw".Trim()), [ref]$n)) { $vals += $n }
        }
        Start-Sleep -Milliseconds 250
    }
    if ($vals.Count -gt 0) {
        $sorted = @($vals | Sort-Object)
        $p95Index = [Math]::Min($sorted.Count - 1, [int][Math]::Floor(0.95 * ($sorted.Count - 1)))
        $gpu.available = $true
        $gpu.avg = [Math]::Round((($vals | Measure-Object -Average).Average), 1)
        $gpu.p95 = [int]$sorted[$p95Index]
        $gpu.max = [int]($vals | Measure-Object -Maximum).Maximum
        $gpu.samples = $vals.Count
        $gpu.note = "Driver GPU percent for the whole adapter, not frame share and not one human isolated."
    }
    else {
        $gpu.note = "nvidia-smi returned no utilization.gpu"
    }
}
else {
    $gpu.note = "nvidia-smi missing"
}

$unityEditor = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -like "*\Editor\Unity.exe"
})
$editorOpen = $unityEditor.Count -gt 0

$world = $null
$didWorld = $false
$skipWorld = $null

if ($FullWorld) {
    if ($editorOpen) {
        $skipWorld = "Unity Editor is open; skip full World to avoid Library lock."
    }
    else {
        $didWorld = $true
        & (Join-Path $PSScriptRoot "run-world-perf.ps1") -Graphics -Cycle $Cycle
        $summary = Join-Path $ProjectPath "Trials\Runs\world_perf_summary_cycle$Cycle.json"
        if (Test-Path -LiteralPath $summary) {
            $world = Get-Content -LiteralPath $summary -Raw | ConvertFrom-Json
        }
    }
}

$threshold = 10
$over = $false
if ($gpu.available -and $null -ne $gpu.max) {
    $over = [double]$gpu.max -gt $threshold
}

$verdict = if (-not $gpu.available) { "gpu_unmeasured" }
elseif ($over) { "over_10_percent_gpu" }
else { "below_10_percent_gpu" }

$report = [ordered]@{
    capturedUtc = (Get-Date).ToUniversalTime().ToString("o")
    card = "GPU-10"
    mode = $(if ($didWorld) { "hourly+world" } else { "hourly-smi" })
    gpuPercent = $gpu
    thresholdPercent = $threshold
    verdict = $verdict
    unityEditorOpen = $editorOpen
    fullWorldRequested = [bool]$FullWorld
    fullWorldSkipped = $skipWorld
    worldSummary = $world
    note = "10 percent GPU is not 10 percent of the frame. Do not retune PD or 200 Hz. Lights/bloom only if verdict is over_10_percent_gpu."
}

$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($outJson, ($report | ConvertTo-Json -Depth 8), $utf8)
Write-Host "GPU-10 $verdict avg=$($gpu.avg) max=$($gpu.max) -> $outJson"

$latest = Join-Path $ProjectPath "Trials\Runs\gpu10_hourly_latest.json"
Copy-Item -LiteralPath $outJson -Destination $latest -Force

if ($over) {
    $flag = Join-Path $ProjectPath "Trials\Runs\gpu10_OVER.flag"
    [System.IO.File]::WriteAllText($flag, $outJson, $utf8)
}

if (-not $gpu.available) { exit 2 }
if ($over) { exit 1 }
exit 0
