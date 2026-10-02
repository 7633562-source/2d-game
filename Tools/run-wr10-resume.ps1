# Resume wr10 cycles from a start index. Requires fresh build or closed Editor for -Rebuild.
# powershell -NoProfile -Command "& '.\Tools\run-wr10-resume.ps1' -StartCycle 5"

param(
    [int]$StartCycle = 5,
    [switch]$SkipRebuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Invoke-Trial {
    param(
        [double]$Duration,
        [string]$Label,
        [string[]]$Extra,
        [switch]$Rebuild
    )
    $trial = Join-Path $PSScriptRoot 'run-trial.ps1'
    $summary = Join-Path $root "Trials\Runs\$Label.json"
    $attempt = 0
    while ($attempt -lt 3) {
        $attempt++
        if (Test-Path $summary) { Remove-Item -LiteralPath $summary -Force }
        $failed = $false
        $errMsg = ''
        try {
            if ($Rebuild -or $attempt -gt 1) {
                & $trial -Rebuild -Duration $Duration -Label $Label -Extra $Extra
            } else {
                & $trial -Duration $Duration -Label $Label -Extra $Extra
            }
        } catch {
            $failed = $true
            $errMsg = $_.Exception.Message
        }
        if (-not $failed -and (Test-Path -LiteralPath $summary)) { return }
        if ($attempt -lt 3) {
            Write-Host "Retry $Label attempt=$attempt : $errMsg" -ForegroundColor Yellow
            if ($errMsg -match 'Unity Editor') {
                $deadline = (Get-Date).AddMinutes(2)
                while ((Get-Process Unity -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
                    Write-Host 'waiting for Unity Editor...'
                    Start-Sleep 10
                }
            }
            continue
        }
        if ($failed) { throw $errMsg }
        throw "trial failed: $Label"
    }
}

function Read-Metrics([string]$Label) {
    $j = Get-Content (Join-Path $root "Trials\Runs\$Label.json") -Raw | ConvertFrom-Json
    [pscustomobject]@{
        label=$Label; fell=[bool]$j.fell; survived=[double]$j.survivedSeconds
        swing=[double]$j.swingFootGroundedFraction; drop=[double]$j.pelvisDrop
        swaps=[int]$j.walkSwapCount; sat=[double]$j.muscleSaturationFraction
        torso=[double]$j.maxAbsTorsoTilt; bothFeet=[double]$j.bothFeetGroundedFraction
        fp=[string]$j.sourceFingerprint
        unload=[double]$j.applied.swingHipUnloadBias
        hipFlex=[double]$j.applied.swingHipFlex
        kneeFlex=[double]$j.applied.swingKneeFlex
        weightRamp=[double]$j.applied.walkWeightRamp
        liftUnload=[double]$j.applied.walkLiftUnloadBias
    }
}

function Test-WalkOk($m) { (-not $m.fell) -and ($m.drop -le 0.01) -and ($m.swaps -ge 4) -and ($m.survived -ge 39) }
function Test-RunOk($m) { (-not $m.fell) -and ($m.drop -le 0.02) -and ($m.swaps -ge 4) -and ($m.survived -ge 29) }

$walkBase = @('-walk','1','-walkTime','5','-walkDuration','35','-walkStance','8','-walkTransfer','8','-walkFirst','right')
$runBase = @('-run','1','-runTime','5','-runHold','20','-runStance','4')

# After c0-c4: hip48/knee65/liftUnload0.04 fold or worse; unload 0.32 walk swing 0.3757 best.
$cycles = @(
    @{ n=5; note='unloadBias 0.32'; walk=@('-swingHipUnload','0.32'); run=@('-swingHipUnload','0.32') }
    @{ n=6; note='unloadBias 0.28'; walk=@('-swingHipUnload','0.28'); run=@('-swingHipUnload','0.28') }
    @{ n=7; note='unloadBias 0.35'; walk=@('-swingHipUnload','0.35'); run=@('-swingHipUnload','0.35') }
    @{ n=8; note='unload 0.32 + dualSwing 0.32'; walk=@('-swingHipUnload','0.32','-dualSupportSwing','0.32'); run=@('-swingHipUnload','0.32','-dualSupportSwing','0.32') }
    @{ n=9; note='unload 0.33 + kneeGround 0.28'; walk=@('-swingHipUnload','0.33','-swingKneeGround','0.28'); run=@('-swingHipUnload','0.33','-swingKneeGround','0.28') }
    @{ n=10; note='unload 0.32 + runWeightRamp 1.8'; walk=@('-swingHipUnload','0.32'); run=@('-swingHipUnload','0.32','-runWeightRamp','1.8') }
)

if (-not $SkipRebuild) {
    Write-Host '=== REBUILD ==='
    Invoke-Trial -Rebuild -Duration 5 -Label 'wr10_resume_ping' -Extra @()
}

foreach ($c in $cycles) {
    if ($c.n -lt $StartCycle) { continue }
    Write-Host "=== CYCLE $($c.n) : $($c.note) ==="
    $wLabel = "wr10_c$($c.n)_walk"
    $rLabel = "wr10_c$($c.n)_run"
    Invoke-Trial -Duration 40 -Label $wLabel -Extra ($walkBase + $c.walk)
    Invoke-Trial -Duration 30 -Label $rLabel -Extra ($runBase + $c.run)
    $wm = Read-Metrics $wLabel
    $rm = Read-Metrics $rLabel
    Write-Host ("  walk: fell={0} swing={1:n4} drop={2:n4} swaps={3}" -f $wm.fell,$wm.swing,$wm.drop,$wm.swaps)
    Write-Host ("  run:  fell={0} swing={1:n4} drop={2:n4} swaps={3}" -f $rm.fell,$rm.swing,$rm.drop,$rm.swaps)
}

Write-Host 'Resume segment done. Assemble with Tools/assemble-wr10-summary.ps1'
