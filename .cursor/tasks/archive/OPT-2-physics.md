# OPT-2 · experiment/physics-adjacent

This is not a controller edit. One sentence: the stand CSV must not allocate a string array 200 times per second.

Layer: `TrialRecorder` only. Do not open `BalanceController`, `Muscle`, `JointFriction`.

## Do

In the step write, replace `rows.AppendLine(string.Join(";", new[] { ... }))` with sequential `Append` into the same `StringBuilder` (`rows`), separator `;`, same fields and `F()`. Do not change column order.

## Stand (you alone occupy Unity)

From the project root, close the Editor only if the script asks for Rebuild:

```
powershell -Command ".\Tools\run-trial.ps1 -Duration 10 -Label opt2_rec"
```

In `applied` check that Extra is empty / fixedDelta 0.005. Reply with: fell, survivedSeconds, maxAbsTorsoTilt, pelvisDrop, rmsComOffset, muscleSaturationFraction.

## Forbids

Do not change balance formulas. Do not use `-fixedDelta 0.01`. Do not edit signs.

## Acceptance

Table + `Trials/Runs/opt2_physics.md`. No CSV dump.
