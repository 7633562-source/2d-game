# B-PERCH-24 take wrapper (bird lane, 27.09): the ground peck suite with the peck-pose probes.
# The ground suite = the SOLO crow on the dirt (the phase-8 scene of B-PERCH-22), the same seed,
# so every take reproduces the same walk/peck schedule and the same strip panels.
# B-PERCH-24 adds -LungeLegs (peckLungeLegs, the legs' own fold at the jab; 1 = the standing base).
param(
    [string]$Label = "bPCH24a_ground",
    [double]$Duration = 40,
    [string]$Dip = "",
    [string]$Reach = "",
    [string]$LungeLegs = "",
    [double]$Margin = 0.6,
    [switch]$Rebuild
)
$ErrorActionPreference = "Stop"
$Extra = @(
    "-bird", "1", "-birdCount", "1", "-birdRig", "flock", "-birdDrive", "wander",
    "-birdDriveSeed", "1", "-birdNoSlide", "1",
    "-camAuto", "1", "-camAutoMargin", "0.6", "-shotRate", "20"
)
if ($Dip -ne "") { $Extra += @("-peckDip", $Dip) }
if ($Reach -ne "") { $Extra += @("-peckReachAhead", $Reach) }
if ($LungeLegs -ne "") { $Extra += @("-peckLungeLegs", $LungeLegs) }
& (Join-Path $PSScriptRoot "run-trial.ps1") -Label $Label -Duration $Duration -Visual -Rebuild:$Rebuild -Extra $Extra
