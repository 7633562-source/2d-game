# 10 optimize/test cycles for human walk + run (WALK-1 / run lift-off).
# English comments. Dots in Extra. No reject-list levers from current-front.
# Usage: powershell -NoProfile -Command "& '.\Tools\run-wr10-opt.ps1'"

param([switch]$SkipRebuild)

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
    # Same process keeps -Extra as string[]. Unity may exit 128 after a good summary.
    # Parallel agents may touch Assets mid-batch (STALE_BUILD): rebuild once and retry.
    $trial = Join-Path $PSScriptRoot 'run-trial.ps1'
    $summary = Join-Path $root "Trials\Runs\$Label.json"
    $attempt = 0
    while ($attempt -lt 2) {
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
        if (-not $failed -and (Test-Path -LiteralPath $summary)) {
            return
        }
        if ($attempt -lt 2 -and ($failed -or -not (Test-Path -LiteralPath $summary))) {
            Write-Host "Retry $Label (stale or missing summary): $errMsg" -ForegroundColor Yellow
            continue
        }
        if ($failed) { throw $errMsg }
        throw "trial failed: $Label (no summary)"
    }
}

function Read-Metrics([string]$Label) {
    $p = Join-Path $root "Trials\Runs\$Label.json"
    $j = Get-Content $p -Raw | ConvertFrom-Json
    [pscustomobject]@{
        label = $Label
        fell = [bool]$j.fell
        survived = [double]$j.survivedSeconds
        swing = [double]$j.swingFootGroundedFraction
        drop = [double]$j.pelvisDrop
        swaps = [int]$j.walkSwapCount
        sat = [double]$j.muscleSaturationFraction
        torso = [double]$j.maxAbsTorsoTilt
        bothFeet = [double]$j.bothFeetGroundedFraction
        fp = [string]$j.sourceFingerprint
    }
}

function Test-WalkOk($m) {
    (-not $m.fell) -and ($m.drop -le 0.01) -and ($m.swaps -ge 4) -and ($m.survived -ge 39)
}

function Test-RunOk($m) {
    (-not $m.fell) -and ($m.drop -le 0.02) -and ($m.swaps -ge 4) -and ($m.survived -ge 29)
}

$walkBase = @('-walk','1','-walkTime','5','-walkDuration','35','-walkStance','8','-walkTransfer','8','-walkFirst','right')
$runBase = @('-run','1','-runTime','5','-runHold','20','-runStance','4')

$cycles = @(
    @{ n=0; note='baseline hipair + run defaults'; walk=@(); run=@() }
    @{ n=1; note='air hip flex 48'; walk=@('-swingHip','48'); run=@('-swingHip','48') }
    @{ n=2; note='air knee flex 65'; walk=@('-swingKnee','65'); run=@('-swingKnee','65') }
    @{ n=3; note='weightRamp 1.5'; walk=@('-walkWeightRamp','1.5'); run=@('-walkWeightRamp','1.5') }
    @{ n=4; note='liftUnload 0.04 (below fold 0.10)'; walk=@('-walkLiftUnload','0.04'); run=@('-walkLiftUnload','0.04') }
    @{ n=5; note='unloadBias 0.32'; walk=@('-swingHipUnload','0.32'); run=@('-swingHipUnload','0.32') }
    @{ n=6; note='dualSupportSwing 0.28'; walk=@('-dualSupportSwing','0.28'); run=@('-dualSupportSwing','0.28') }
    @{ n=7; note='liftDelay 5 + levelRate 1.15'; walk=@('-walkLiftDelay','5','-walkLiftLevelRate','1.15'); run=@('-runLiftDelay','2.2','-walkLiftLevelRate','1.15') }
    @{ n=8; note='kneeGround 0.30 + unload 0.30'; walk=@('-swingKneeGround','0.30','-swingHipUnload','0.30'); run=@('-swingKneeGround','0.30','-swingHipUnload','0.30') }
    @{ n=9; note='runWeightRamp 1.8 + walkWeightRamp 1.25'; walk=@('-walkWeightRamp','1.25'); run=@('-runWeightRamp','1.8','-walkWeightRamp','1.25') }
    @{ n=10; note='combo: hip48 + unload0.04 + weightRamp1.5'; walk=@('-swingHip','48','-walkLiftUnload','0.04','-walkWeightRamp','1.5'); run=@('-swingHip','48','-walkLiftUnload','0.04','-runWeightRamp','1.8') }
)

$results = New-Object System.Collections.Generic.List[object]
$bestWalk = $null
$bestRun = $null

if (-not $SkipRebuild) {
    Write-Host '=== REBUILD ==='
    Invoke-Trial -Rebuild -Duration 5 -Label 'wr10_rebuild_ping' -Extra @()
}

foreach ($c in $cycles) {
    $n = $c.n
    Write-Host "=== CYCLE $n : $($c.note) ==="
    $wLabel = "wr10_c${n}_walk"
    $rLabel = "wr10_c${n}_run"
    $wExtra = @($walkBase) + @($c.walk)
    $rExtra = @($runBase) + @($c.run)

    Invoke-Trial -Duration 40 -Label $wLabel -Extra $wExtra
    Invoke-Trial -Duration 30 -Label $rLabel -Extra $rExtra

    $wm = Read-Metrics $wLabel
    $rm = Read-Metrics $rLabel
    $wOk = Test-WalkOk $wm
    $rOk = Test-RunOk $rm

    $wVerdict = 'reject'
    if ($wOk) {
        if ($null -eq $bestWalk -or $wm.swing -lt ($bestWalk.swing - 0.005)) { $wVerdict = 'improve' }
        elseif ([math]::Abs($wm.swing - $bestWalk.swing) -le 0.005) { $wVerdict = 'plateau' }
        else { $wVerdict = 'worse' }
    }
    $rVerdict = 'reject'
    if ($rOk) {
        if ($null -eq $bestRun -or $rm.swing -lt ($bestRun.swing - 0.005)) { $rVerdict = 'improve' }
        elseif ([math]::Abs($rm.swing - $bestRun.swing) -le 0.005) { $rVerdict = 'plateau' }
        else { $rVerdict = 'worse' }
    }

    if ($wOk -and ($null -eq $bestWalk -or $wm.swing -lt $bestWalk.swing)) {
        $bestWalk = $wm | Select-Object *
        $bestWalk | Add-Member -NotePropertyName note -NotePropertyValue $c.note -Force
        $bestWalk | Add-Member -NotePropertyName extra -NotePropertyValue ($c.walk -join ' ') -Force
    }
    if ($rOk -and ($null -eq $bestRun -or $rm.swing -lt $bestRun.swing)) {
        $bestRun = $rm | Select-Object *
        $bestRun | Add-Member -NotePropertyName note -NotePropertyValue $c.note -Force
        $bestRun | Add-Member -NotePropertyName extra -NotePropertyValue ($c.run -join ' ') -Force
    }

    Write-Host ("  walk: fell={0} swing={1:n4} drop={2:n4} swaps={3} {4}" -f $wm.fell,$wm.swing,$wm.drop,$wm.swaps,$wVerdict)
    Write-Host ("  run:  fell={0} swing={1:n4} drop={2:n4} swaps={3} {4}" -f $rm.fell,$rm.swing,$rm.drop,$rm.swaps,$rVerdict)

    $results.Add([ordered]@{
        cycle = $n
        note = $c.note
        walkExtra = ($c.walk -join ' ')
        runExtra = ($c.run -join ' ')
        walk = $wm
        run = $rm
        walkOk = $wOk
        runOk = $rOk
        walkVerdict = $wVerdict
        runVerdict = $rVerdict
    }) | Out-Null
}

$baseW = ($results | Where-Object { $_.cycle -eq 0 }).walk.swing
$baseR = ($results | Where-Object { $_.cycle -eq 0 }).run.swing
$summary = [ordered]@{
    capturedUtc = (Get-Date).ToUniversalTime().ToString('o')
    goal = 'swingFoot < 0.35 without fold; >=4 swaps; walk 40s / run 30s'
    fingerprint = $results[0].walk.fp
    baseline = @{ walkSwing = $baseW; runSwing = $baseR }
    bestWalk = $bestWalk
    bestRun = $bestRun
    walkLiftOffClosed = (($null -ne $bestWalk) -and ($bestWalk.swing -lt 0.35))
    runLiftOffClosed = (($null -ne $bestRun) -and ($bestRun.swing -lt 0.35))
    rejectedLeversAvoided = @(
        'runStance 3/5', 'runWeightRamp 1', 'runLiftDelay 1.5',
        'walkSwingLift 1.1/1.2', 'walkLiftCom 0.15', 'walkLiftUnload 0.10',
        'toeOff', 'airHold', 'heelPlant', 'unlatchPlant', 'latchAir', 'airLevel', 'SIMBICON'
    )
    cycles = @($results)
}
$outPath = Join-Path $root 'Trials\Runs\opt_walk_run_10.json'
($summary | ConvertTo-Json -Depth 8) | Set-Content -Path $outPath -Encoding UTF8
Write-Host "Wrote $outPath"
Write-Host ("BEST walk swing={0:n4} ({1})" -f $bestWalk.swing, $bestWalk.note)
Write-Host ("BEST run  swing={0:n4} ({1})" -f $bestRun.swing, $bestRun.note)
Write-Host ("lift-off closed walk={0} run={1}" -f $summary.walkLiftOffClosed, $summary.runLiftOffClosed)
