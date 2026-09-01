# History — ostrich knee (`curr_*`)

**Not the acceptance canon.** The folder name `current/` is historical.
See `../stable-pd/`. Do not delete the files or rename the folder.

# What is here (with provenance)

Seven summaries were taken on the final portable build after experimental
layer edits (`HeadlessTrial`, `TrialRecorder`, `HeadlessBuild`, the stand).
The physical algorithm `Muscle` / `JointFriction` / `BalanceController` /
`Human` was left alone on purpose. Numbers differ from the old `damp_*` —
this is a new reproducible attractor after rebuild, **not** a proven
controller improvement.

Fingerprint: `5d6a30dce4daac3352512269ddbcfd57a56721634f694fd8d7a12627910cebd2`
`buildGuid`: `aa6a9b20ca874c5aa52e5856acee055f`
Unity 6000.5.8f1, `fixedDelta` 0.005. Actual Rigidbody2D mass sum:
70.84 kg at `totalMass` 70.

Old `damp_*` without fingerprint live in
`Trials/Baselines/legacy-no-provenance/`. They were not overwritten.
Historical CSV: `E:\Games\Unity3D LOGS\trials`.

`buildManifestPath` and `commandArgs` contain machine-absolute paths:
they are not in the fingerprint and will differ after a move.

## Thresholds

| Label | What it checks | fell | survivedSeconds | maxAbsTorsoTilt | rmsComOffset | rmsTorsoAngVel | muscleSaturationFraction |
|---|---|---|---|---|---|---|---|
| `curr_stand` | stance 30 s | false | 30.0029 | 3.3802 | 0.0070 | 26.6462 | 0.0015 |
| `curr_act40` | `activationSpeed 40` | false | 30.0029 | 6.3800 | 0.0327 | 82.9746 | 0.2326 |
| `curr_act80` | `activationSpeed 80` | true | 7.0650 | 93.5207 | 0.5483 | 62.1328 | 0.1568 |
| `curr_pf22` | push **+22** N·s | false | 20.0046 | 5.1036 | 0.0194 | 32.7054 | 0.0080 |
| `curr_pf23` | push **+23** N·s | true | 6.7800 | 90.7622 | 0.7078 | 40.6393 | 0.0090 |
| `curr_pb23` | push **−23** N·s | false | 20.0046 | 3.3802 | 0.0283 | 31.0803 | 0.0050 |
| `curr_pb24` | push **−24** N·s | true | 6.7700 | 91.3402 | 0.7246 | 42.9462 | 0.0022 |

Forward holds 22, falls at 23. Backward holds 23, falls at 24.
Quiet stance `bothFeetGroundedFraction` = 1.
This is not the solid-torso baseline from `trial-protocol.mdc`.
