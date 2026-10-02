# Generate seamless fills into Assets/Resources/Art.
# Pictures only: fabric, skin, denim, dirt, grass. The sky stays in code.
#
# Pollinations needs no key; limit ~1 request / 15 s. Parallel calls get 429.
# Retries must be sequential. Commit the PNGs; the game stays offline.
#
# From the project root:
#   powershell -File .\Tools\gen-art.ps1

$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent
$OutDir = Join-Path $Root "Assets\Resources\Art"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$Jobs = @(
    @{ Name = "fabric"; Seed = 11; Prompt = "seamless tileable pale cotton fabric weave, close-up cloth only, even studio lighting, no clothing, no people, no text, no watermark, photoreal, square" }
    @{ Name = "skin";   Seed = 12; Prompt = "seamless tileable pale human skin texture, close-up skin only, subtle pores, even studio lighting, no face, no body parts, no text, no watermark, photoreal, square" }
    @{ Name = "denim";  Seed = 13; Prompt = "seamless tileable light denim twill weave, close-up fabric only, even studio lighting, no clothing, no people, no text, no watermark, photoreal, square" }
    @{ Name = "dirt";   Seed = 14; Prompt = "seamless tileable light brown dirt soil, top-down dry earth, even lighting, no plants, no text, no watermark, photoreal, square" }
    @{ Name = "grass";  Seed = 15; Prompt = "seamless tileable short green grass lawn, top-down, even daylight, no flowers, no text, no watermark, photoreal, square" }
)

function Get-PollinationsUrl([string]$Prompt, [int]$Seed) {
    $enc = [uri]::EscapeDataString($Prompt)
    return "https://image.pollinations.ai/prompt/$enc`?width=512&height=512&seed=$Seed&nologo=true&model=flux"
}

foreach ($job in $Jobs) {
    $dest = Join-Path $OutDir ($job.Name + ".png")
    $url = Get-PollinationsUrl $job.Prompt $job.Seed
    Write-Host "GET $($job.Name) ..."
    $ok = $false
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        try {
            Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing -TimeoutSec 120
            $bytes = [IO.File]::ReadAllBytes($dest)
            if ($bytes.Length -lt 1000) { throw "too small ($($bytes.Length))" }
            if ($bytes[0] -ne 0x89 -or $bytes[1] -ne 0x50) {
                throw "not PNG (got $('{0:X2} {1:X2}' -f $bytes[0], $bytes[1]))"
            }
            Write-Host "  OK $($bytes.Length) bytes"
            $ok = $true
            break
        }
        catch {
            Write-Host "  attempt $attempt failed: $($_.Exception.Message)"
            if (Test-Path $dest) { Remove-Item $dest -Force }
            if ($attempt -lt 4) { Start-Sleep -Seconds 16 }
        }
    }
    if (-not $ok) { throw "failed: $($job.Name)" }
    Start-Sleep -Seconds 16
}

Write-Host "done: $OutDir"
