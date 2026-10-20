# B-PERCH-1 take wrapper (bird lane, 25.09): run-trial.ps1 with -Visual and the ring/perch flags,
# so the seat take carries shots. DyStr is kept a string so no culture turns '-0.12' into '-0,12'.
# Suite 'ring7' = the pinned slot-7 ring take; 'owner' = the owner's launcher flags as-is
# (no slot, no seat flag, wind as the scene default) to prove the code default seat.
param(
    [string]$Label = "bPCH1m2v_tree",
    [string]$DyStr = "-0.075",
    [double]$Duration = 70,
    [string]$Suite = "ring7",
    [string]$SettleStr = "",
    [string]$DropStr = "",
    [switch]$Rebuild
)
$ErrorActionPreference = "Stop"
if ($Suite -eq "owner") {
    $Extra = @(
        "-bird", "1", "-birdCount", "1", "-birdRig", "flock", "-birdDrive", "wander",
        "-tree", "1", "-treePerch", "1", "-treeTriggers", "1", "-treeTriggerCap", "16",
        "-camAuto", "1", "-camAutoMargin", "0.6",
        "-shotRate", "20"
    )
} elseif ($Suite -eq "owner7") {
    # B-PERCH-4: the owner suite with the bird-lane slot pin (the measured-seat
    # verification on slot 7 - the accepted contact read lives there).
    $Extra = @(
        "-bird", "1", "-birdCount", "1", "-birdRig", "flock", "-birdDrive", "wander",
        "-tree", "1", "-treePerch", "1", "-treeTriggers", "1", "-treeTriggerCap", "16",
        "-camAuto", "1", "-camAutoMargin", "0.6",
        "-perchSlot", "7",
        "-shotRate", "20"
    )
} elseif ($Suite -eq "owner7auto") {
    # B-PERCH-5 (26.09): the slot-7 pin with the measured seat on (the probe's
    # slot-7 verification take: seatMeasured must reproduce the accepted -0.037).
    $Extra = @(
        "-bird", "1", "-birdCount", "1", "-birdRig", "flock", "-birdDrive", "wander",
        "-tree", "1", "-treePerch", "1", "-treeTriggers", "1", "-treeTriggerCap", "16",
        "-camAuto", "1", "-camAutoMargin", "0.6",
        "-perchSlot", "7", "-perchSeatAuto", "1",
        "-shotRate", "20"
    )
} elseif ($Suite -eq "owner1auto") {
    # B-PERCH-6 (26.09): the slot-1 pin, the measured seat on - the verification
    # take for the support-face rule (the exposed fork the free pick lands on).
    $Extra = @(
        "-bird", "1", "-birdCount", "1", "-birdRig", "flock", "-birdDrive", "wander",
        "-tree", "1", "-treePerch", "1", "-treeTriggers", "1", "-treeTriggerCap", "16",
        "-camAuto", "1", "-camAutoMargin", "0.6",
        "-perchSlot", "1", "-perchSeatAuto", "1",
        "-shotRate", "20"
    )
} elseif ($Suite -eq "owner11auto") {
    # B-PERCH-18 (26.09): the slot-11 pin, the measured seat on - the round's
    # A/B take (the same open mark the base bPCH17a's free pick landed on, so
    # the pose change can be read on the identical spot).
    $Extra = @(
        "-bird", "1", "-birdCount", "1", "-birdRig", "flock", "-birdDrive", "wander",
        "-tree", "1", "-treePerch", "1", "-treeTriggers", "1", "-treeTriggerCap", "16",
        "-camAuto", "1", "-camAutoMargin", "0.6",
        "-perchSlot", "11", "-perchSeatAuto", "1",
        "-shotRate", "20"
    )
} elseif ($Suite -eq "ownera") {
    # B-PERCH-5 (26.09): the owner suite, free pick, the measured seat per slot.
    $Extra = @(
        "-bird", "1", "-birdCount", "1", "-birdRig", "flock", "-birdDrive", "wander",
        "-tree", "1", "-treePerch", "1", "-treeTriggers", "1", "-treeTriggerCap", "16",
        "-camAuto", "1", "-camAutoMargin", "0.6",
        "-perchSeatAuto", "1",
        "-shotRate", "20"
    )
} elseif ($Suite -eq "owner1seat") {
    # B-PERCH-14 (26.09): the owner suite + the slot-1 pin + an explicit seat
    # value - the A/B of the fork's own seat family (0.335 live / 0.365 / 0.393
    # the crest of the claw band's first faces).
    $Extra = @(
        "-bird", "1", "-birdCount", "1", "-birdRig", "flock", "-birdDrive", "wander",
        "-tree", "1", "-treePerch", "1", "-treeTriggers", "1", "-treeTriggerCap", "16",
        "-camAuto", "1", "-camAutoMargin", "0.6",
        "-perchSlot", "1", "-perchSeatDy", $DyStr,
        "-shotRate", "20"
    )
} elseif ($Suite -eq "ground") {
    # B-PERCH-22 (27.09): the phase-8 baseline film - the SOLO bird on the dirt
    # (the simple scene: no tree), the owner's ground law on (-birdNoSlide 1),
    # the wander drive as the owner scene runs it; shots for the cuts.
    $Extra = @(
        "-bird", "1", "-birdCount", "1", "-birdRig", "flock", "-birdDrive", "wander",
        "-birdDriveSeed", "1",
        "-birdNoSlide", "1",
        "-camAuto", "1", "-camAutoMargin", "0.6",
        "-shotRate", "20"
    )
    # B-PERCH-25 (27.09): the grounded settle's per-take dose (-birdGroundSettle);
    # empty = the code default. The lever: the plate hold draws a body that stopped
    # flying back down to the stance root (the drawn feet then meet the grass).
    if ($SettleStr -ne "") { $Extra += @("-birdGroundSettle", $SettleStr) }
    # B-PERCH-26 (27.09): the peck's own drop, metres (-birdPeckNeckDrop; the knob
    # Bird.PeckNeckDropKnob). Empty = the code default 0.08.
    if ($DropStr -ne "") { $Extra += @("-birdPeckNeckDrop", $DropStr) }
} else {
    $Extra = @(
        "-bird", "1", "-birdCount", "1", "-birdRig", "flock", "-birdDrive", "wander",
        "-tree", "1", "-treePerch", "1", "-treePerchSlot", "7",
        "-treeTriggers", "1", "-treeTriggerCap", "16", "-treeWind", "0",
        "-perchSeatDy", $DyStr, "-shotRate", "20"
    )
}
Write-Host ("EXTRA-ARGS: " + ($Extra -join " "))
& (Join-Path $PSScriptRoot "run-trial.ps1") -Label $Label -Duration $Duration -Visual -Rebuild:$Rebuild -Extra $Extra