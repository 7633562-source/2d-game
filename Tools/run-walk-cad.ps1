# Walk cadence cases. The lift schedule (walkLiftDelay 6 s, ramp 2 s) and the
# unload ramp (standLegRate 1.5/s, cap 0.4) were sized for an 8 s stance. With a
# body-driven step of about a second standLegLevel peaks near 0.6, the swing foot
# never unloads, and the body face-plants over two planted feet.
# Each case: lead, trigger, liftDelay, liftRamp, weightRamp, transferMin,
# standLegRate, dualSupportCap.
param(
    [switch]$Rebuild,
    [string]$Only = '',
    [string]$Set = 'b'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$runTrial = Join-Path $PSScriptRoot 'run-trial.ps1'
$runs = Join-Path $root 'Trials\Runs'

$walkArgs = @('-walk', '1', '-walkTime', '5', '-walkDuration', '35',
              '-walkStance', '8', '-walkTransfer', '8', '-walkFirst', 'right')

$sets = @{
    'a' = [ordered]@{
        'cad_a' = @('0.03', '0.05', '0.15', '0.35', '0.35', '0.20', '1.5', '0.4')
        'cad_b' = @('0.03', '0.05', '0.30', '0.50', '0.50', '0.30', '1.5', '0.4')
        'cad_c' = @('0.05', '0.06', '0.15', '0.35', '0.35', '0.20', '1.5', '0.4')
        'cad_d' = @('0.03', '0.04', '0.10', '0.25', '0.25', '0.15', '1.5', '0.4')
        'cad_e' = @('0.06', '0.06', '0.20', '0.40', '0.40', '0.25', '1.5', '0.4')
    }
    # Scissor: swing hip goes forward while still grounded (index 9),
    # knee peel clears the toe (index 10). From cad_o.
    'd' = [ordered]@{
        'sci_a' = @('0.06', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3', '0.8', '0')
        'sci_b' = @('0.06', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3', '0.6', '0')
        'sci_c' = @('0.06', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3', '0.8', '0.5')
        'sci_d' = @('0.06', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3', '0.6', '0.5')
        'sci_e' = @('0.10', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3', '0.8', '0.5')
    }
    # Partial scissor (index 11): full grounded flex threw the pelvis back.
    'e' = [ordered]@{
        'scf_a' = @('0.06', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3', '0.8', '0.5', '0.5')
        'scf_b' = @('0.06', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3', '0.8', '0.5', '0.3')
        'scf_c' = @('0.10', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3', '0.8', '0.5', '0.5')
        'scf_d' = @('0.10', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.6', '0.8', '0.5', '0.3')
        'scf_e' = @('0.06', '0.06', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3', '0.9', '0.5', '0.4')
    }
    # Full scissor plus forward trunk lean: the swing thigh throws the pelvis
    # back (third law), the lean puts the mass back over the step.
    'f' = [ordered]@{
        'lean_a' = @('0.06', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3', '0.8', '0.5', '1')
        'lean_b' = @('0.03', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3', '0.8', '0.5', '1')
        'lean_c' = @('0.06', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3', '0.6', '0.5', '1')
        'lean_d' = @('0.10', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3', '0.8', '0.5', '1')
    }
    'c' = [ordered]@{
        # Around cad_i (stable 40 s, 8 swaps, upright) — add drive.
        'cad_k' = @('0.06', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0')
        'cad_l' = @('0.10', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0')
        'cad_m' = @('0.03', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3')
        'cad_n' = @('0.03', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.6')
        'cad_o' = @('0.06', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1', '0.3')
    }
    'b' = [ordered]@{
        'cad_f' = @('0.03', '0.05', '0.10', '0.20', '0.20', '0.15', '6', '1')
        'cad_g' = @('0.05', '0.06', '0.10', '0.20', '0.20', '0.15', '6', '1')
        'cad_h' = @('0.03', '0.05', '0.05', '0.15', '0.15', '0.10', '10', '1')
        'cad_i' = @('0.03', '0.08', '0.10', '0.20', '0.20', '0.15', '6', '1')
        'cad_j' = @('0.02', '0.04', '0.10', '0.20', '0.20', '0.15', '6', '1')
    }
}

$cases = $sets[$Set]
$first = $true
foreach ($name in $cases.Keys) {
    if ($Only -and $name -ne $Only) { continue }
    $c = $cases[$name]
    $extra = $walkArgs + @(
        '-walkComLead', $c[0],
        '-walkComTrigger', $c[1],
        '-walkLiftDelay', $c[2],
        '-walkLiftRamp', $c[3],
        '-walkWeightRamp', $c[4],
        '-walkTransferMin', $c[5],
        '-standLegRate', $c[6],
        '-dualSupportCap', $c[7])
    if ($c.Count -gt 8) { $extra += @('-walkStancePush', $c[8]) }
    if ($c.Count -gt 9) { $extra += @('-walkSwingScissor', $c[9]) }
    if ($c.Count -gt 10) { $extra += @('-walkKneePeel', $c[10]) }
    if ($c.Count -gt 11) { $extra += @('-walkSwingScissorFlex', $c[11]) }
    if ($Set -eq 'f') { $extra += @('-lean', '1', '-leanTime', '5', '-leanHold', '40') }
    $trialArgs = @{ Duration = 40; Label = $name; Extra = $extra }
    if ($first -and $Rebuild) { $trialArgs['Rebuild'] = $true }
    $first = $false

    Write-Host "=== $name (lead $($c[0]) trig $($c[1]) delay $($c[2]) rate $($c[6]) cap $($c[7])) ===" -ForegroundColor Cyan
    & $runTrial @trialArgs | Out-Null

    $json = Join-Path $runs "$name.json"
    if (-not (Test-Path $json)) { Write-Host "$name : NO SUMMARY" -ForegroundColor Red; continue }
    $s = Get-Content $json -Raw | ConvertFrom-Json
    'RES {0}: fell={1} travel={2:N3} speed={3:N3} swaps={4} drop={5:N4} swing={6:N4} sat={7:N4} tilt={8:N2} surv={9:N1}' -f `
        $name, $s.fell, $s.comTravelX, $s.comSpeedX, $s.walkSwapCount, $s.pelvisDrop,
        $s.swingFootGroundedFraction, $s.muscleSaturationFraction, $s.maxAbsTorsoTilt,
        $s.survivedSeconds
}
