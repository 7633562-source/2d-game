# GETUP-1 - get up from a fall, walk slot

Requirement: while the human is Fallen and the walk key is held, he must try
to get back on his feet, then walk again. The walk input is not ignored on the
ground.

Measured now: the walk key is held 5 to 40 s, the human falls at 8 s, then the
body is frozen for 32 s - torsoTilt -70.4 deg, comX 0.970 m, state 1. No attempt
at all.

States to tell apart: Standing, Walking, Falling, Fallen, Getting up.

Acceptance: within 10 s of the fall - torso tilt above -20 deg, CoM above
the standing threshold, both feet under the body; then 3 s of walking
without falling. Fall and get-up must be visible in the 5 fps video.

Files: BalanceController.cs, BalanceControllerCaptureWalk.cs,
StepPhaseDriver.cs. One writer: this slot.
