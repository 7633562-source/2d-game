# Regression for the walk-travel work: every new lever defaults to neutral
# (walkComLeadX 0, stanceComTrigger 0, walkSwingScissorLevel 0), so the stand
# must reproduce the accepted numbers. comTravelX is a new metric only.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$runTrial = Join-Path $PSScriptRoot 'run-trial.ps1'
$runs = Join-Path $root 'Trials\Runs'

$cases = [ordered]@{
    'wt_reg_walk'  = @{ D = 40; E = @('-walk', '1', '-walkTime', '5', '-walkDuration', '35',
                                      '-walkStance', '8', '-walkTransfer', '8', '-walkFirst', 'right') }
    'wt_reg_stand' = @{ D = 30; E = @() }
    'wt_reg_pf24'  = @{ D = 20; E = @('-pushImpulse', '24', '-pushTime', '5') }
    'wt_reg_pb23'  = @{ D = 20; E = @('-pushImpulse', '-23', '-pushTime', '5') }
    'wt_reg_onegl' = @{ D = 20; E = @('-standLeg', 'left', '-standLegTime', '5', '-standLegHold', '20') }
}

foreach ($name in $cases.Keys) {
    $c = $cases[$name]
    Write-Host "=== $name ===" -ForegroundColor Cyan
    & $runTrial -Duration $c.D -Label $name -Extra $c.E | Out-Null
    $json = Join-Path $runs "$name.json"
    if (-not (Test-Path $json)) { Write-Host "$name : NO SUMMARY" -ForegroundColor Red; continue }
    $s = Get-Content $json -Raw | ConvertFrom-Json
    'RES {0}: fell={1} tilt={2:N2} rms={3:N4} drop={4:N4} sat={5:N4} feet={6:N4} swing={7:N4} swaps={8} travel={9:N3}' -f `
        $name, $s.fell, $s.maxAbsTorsoTilt, $s.rmsComOffset, $s.pelvisDrop,
        $s.muscleSaturationFraction, $s.bothFeetGroundedFraction,
        $s.swingFootGroundedFraction, $s.walkSwapCount, $s.comTravelX
}
