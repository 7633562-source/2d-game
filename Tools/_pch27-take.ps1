# B-PERCH-27 take wrapper (bird lane, 27.09): the ground peck suite with the body-dip knob.
# The ground suite = the SOLO crow on the dirt (the phase-8 scene of B-PERCH-22), the same seed,
# so every take reproduces the same walk/peck schedule and the same strip panels.
# B-PERCH-27 adds -BodyDrop (Bird.PeckBodyDropKnob / -birdPeckBodyDrop, metres, the body's own
# lowering at the jab; dose 0.00 = the old hold exactly).
param(
    [string]$Label = "bPCH27a_ground",
    [double]$Duration = 40,
    [string]$BodyDrop = "",
    [double]$Margin = 0.6,
    [switch]$Rebuild
)
$ErrorActionPreference = "Stop"
$Extra = @(
    "-bird", "1", "-birdCount", "1", "-birdRig", "flock", "-birdDrive", "wander",
    "-birdDriveSeed", "1", "-birdNoSlide", "1",
    "-camAuto", "1", "-camAutoMargin", "0.6", "-shotRate", "20"
)
if ($BodyDrop -ne "") { $Extra += @("-birdPeckBodyDrop", $BodyDrop) }
& (Join-Path $PSScriptRoot "run-trial.ps1") -Label $Label -Duration $Duration -Visual -Rebuild:$Rebuild -Extra $Extra
