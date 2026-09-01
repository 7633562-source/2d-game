# Walk grid: walkComLeadX (how far ahead of the support the ankle target sits)
# x stanceComTrigger (CoM lead that ends Stance). Walking is controlled falling
# forward: the lead supplies the drive, the trigger puts the next foot down.
# Metric to watch is comTravelX — support swaps alone travelled 0.21 m in 35 s.
param(
    [switch]$Rebuild,
    # Each item is "lead:trigger", numbers as strings with a dot (invariant).
    [string[]]$Pairs = @('0.03:0.05', '0.03:0.08', '0.06:0.05', '0.06:0.08', '0.10:0.08'),
    [string]$Prefix = 'step',
    [string[]]$ExtraArgs = @()
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$runTrial = Join-Path $PSScriptRoot 'run-trial.ps1'
$runs = Join-Path $root 'Trials\Runs'

$walkArgs = @('-walk', '1', '-walkTime', '5', '-walkDuration', '35',
              '-walkStance', '8', '-walkTransfer', '8', '-walkFirst', 'right')

$first = $true
foreach ($pair in $Pairs) {
    $parts = $pair.Split(':')
    $lead = $parts[0]
    $trigger = $parts[1]
    $label = "{0}_l{1}_t{2}" -f $Prefix, ($lead -replace '\.', ''), ($trigger -replace '\.', '')
    $extra = $walkArgs + @('-walkComLead', $lead, '-walkComTrigger', $trigger) + $ExtraArgs
    $trialArgs = @{ Duration = 40; Label = $label; Extra = $extra }
    if ($first -and $Rebuild) { $trialArgs['Rebuild'] = $true }
    $first = $false

    Write-Host "=== $label (lead $lead, trigger $trigger) ===" -ForegroundColor Cyan
    & $runTrial @trialArgs | Out-Null

    $json = Join-Path $runs "$label.json"
    if (-not (Test-Path $json)) { Write-Host "$label : NO SUMMARY" -ForegroundColor Red; continue }
    $s = Get-Content $json -Raw | ConvertFrom-Json
    '{0}: lead={1} trig={2} fell={3} travel={4:N3} speed={5:N3} swaps={6} drop={7:N4} swing={8:N4} sat={9:N4} tilt={10:N2}' -f `
        $label, $s.applied.walkComLeadX, $s.applied.stanceComTrigger, $s.fell,
        $s.comTravelX, $s.comSpeedX, $s.walkSwapCount, $s.pelvisDrop,
        $s.swingFootGroundedFraction, $s.muscleSaturationFraction, $s.maxAbsTorsoTilt
}
