param(
    [string]$CapturesRoot = "Trials/Captures",
    [switch]$Zip,
    [switch]$Video
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path $CapturesRoot -ErrorAction Stop
$latest = Get-ChildItem -Path $root -Directory |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if (-not $latest) {
    Write-Host "No capture sessions found in $root"
    exit 1
}

Write-Host ("Latest capture: " + $latest.FullName)

$meta = Join-Path $latest.FullName "meta.txt"
if (Test-Path $meta) {
    Write-Host "Meta:"
    Get-Content $meta
}

if ($Zip) {
    $zipPath = Join-Path $latest.Parent.FullName ($latest.Name + ".zip")
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $latest.FullName "*") -DestinationPath $zipPath
    Write-Host ("ZIP: " + $zipPath)
}

if ($Video) {
    $ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if (-not $ffmpeg) {
        Write-Host "ffmpeg not found in PATH; skipping video."
        exit 0
    }

    $input = Join-Path $latest.FullName "frame_%04d_t*.png"
    $videoPath = Join-Path $latest.Parent.FullName ($latest.Name + ".mp4")
    & $ffmpeg.Source -y -framerate 8 -pattern_type glob -i (Join-Path $latest.FullName "frame_*.png") `
        -c:v libx264 -pix_fmt yuv420p $videoPath | Out-Null
    Write-Host ("Video: " + $videoPath)
}
