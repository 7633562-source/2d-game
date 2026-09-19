# TREES plan (expert review 18.09, verified against the source by Hermes)

## What was established, and how
Opus read TreeSpring.cs and TreeBase.cs and produced a mechanism. I then checked every load-bearing
claim against the source myself. CONFIRMED, exactly as stated:
- Bend limits per order: trunkBendLimit 1.5, limb 5, secondary 12, twig 28 -> 46.5 deg along one
  root-to-tip path, and nothing in the code bounds the SUM.
- `spring.maxTorque = order == 0 ? trunkSpringMaxTorque(600) : twigSpringMaxTorque(40)`
  (TreeBase.cs:266), later raised only to `max(|moment|*0.5 + 16)` (:390). A twig at 34 deg is asked
  for ~66 N.m (stiffness 22 * lengthScale ~5 = ~110 N.m/rad) and allowed ~20-40: past ~0.2-0.35 rad
  the joint stops being a spring, becomes a constant-torque element and then walks to its hinge stop.
  hingePinnedFraction 0.2 = ~3 of 14 hinges sitting on a limit.
- `spring.holdTorque = -moment` and `spring.restAngle = jointAngle` at build (:389,:391). Therefore the
  no-wind run is a TAUTOLOGY: residual torque is zero by construction, so maxTrunkLeanDeg 6.6e-05
  certifies the build, not the stiffness. Any rig, at any stiffness, passes it. It proves nothing
  about "the stem is stiff". (I earlier told Yury this run showed rigidity - that was wrong, withdrawn.)
- `TreeSpring.gravityGradient` (TreeSpring.cs:30) is declared and NEVER assigned and never read: dead
  field. Effective stiffness is k - sum(m_j g r_j) with nothing compensating.
- Wind has no zero mean: `gust = 1 + windGust*sin(...)` (TreeBase.cs:594-599) and the force is
  `sign(v)*q*v*|v|*area*...` (:606-611). With windGust < 1 the sign never flips, so the load is a
  one-sided STEP. A step load on a spring gives a permanent offset - that is "keeps leaning and stays
  leaned". Consequently `swayAmpM` has been measuring a lean, never an amplitude.
- `seed` feeds exactly two things: the fork scatter `rng.NextDouble()` (the ONLY use of rng,
  :444-445, +-forkJitter) and the wind phase (:598). forkAngle, leaderForkShare, decays, taper and
  height are identical for every tree. "Diverse trees" today = one tree with +-4 deg of noise.
- The stump is Static (:129) and the trunk's parent is the stump.

CORRECTED (I reject this part of the review):
- Opus estimated the explicit-integration bound with dt = 0.02 -> `4I/dt^2 = 80 N.m/rad`, and concluded
  the twig stiffness (~110) sits ABOVE it, so "the clamp is the only thing keeping the solver stable".
  ProjectSettings/TimeManager.asset says Fixed Timestep = 0.005, and no script overrides it. The real
  bound is 4*0.008/0.005^2 = 1280 N.m/rad, i.e. ~110 is comfortably INSIDE it. So that conclusion is
  void: the clamp is not carrying the solver. Keep the `kOverStabilityBound` check as a cheap guard,
  but do not build any design decision on it.

## T1 DONE — 18.09, build f9d594f0 (instrumentation only, verified)

- The oak is **16 bodies = static stump + 15 dynamic wood**, so the trace has
  **15 joint column groups, not 14** (0.2 pinned = 3 of 15 hinges). The plan's
  "14" was an assumption, not a measurement.
- Acceptance met: rerun oak wind 0.12 → **17 of 17 measured keys bit-identical**
  to build 7ed1ff4d (maxTrunkLeanDeg 16.04749, maxAbsJointAngle 34.17341,
  crownDrop 0.2553246, hingePinnedFraction 0.2, swayAmpM 1.287312, all four
  per-order maxima), hold run likewise (0.00102997 / 0). Same for the rig hash
  in both runs: dcc15b908146504e.
- The assertion fires exactly where the plan said: on the pre-T1 run block it
  named 84 keys, including `maxAbsHoldTorque` and `treeMaxAbsBranchHeading`;
  both now live in `build:` and the new run block carries measurements only.
- **T2 IS ALREADY ON ITS OWN STOP RULE**: `springSatFraction` = 0 on every
  joint at wind 0.12 (the order-3 twigs move 1.5 deg against a 40.08 N·m cap),
  so the clamp/saturation mechanism is NOT what eats the 34 deg. Re-brief T2
  before sizing anything from it. Note also `trunkJointAngleDeg` 16.03 against
  `trunkLimitDeg` 16.00 — the trunk sits on its stop, as the audit said.

### T1 for the APPLE — 18.09, build 8000f531 (same instrumentation, verified)

- `TreeJointTrace` now takes `IWoodSensorSource`, so ONE trace reads both trees:
  `TreeBase` hands out the same `WoodSensor` (order / bindDeg / buildHeadingDeg)
  and computes its own `RigHash` at build (fnv1a64, `b242a8ddd29b0b28` at seed 1).
  `PlantTree.WoodSensor` moved out of `PlantTree` into `TreeJointTrace` — one
  struct, one CSV layout, so one script set reads oak and apple.
- `AppleTrialRecorder` is sensor-pure: it no longer calls `GetMaxAbsJointAngle`
  / `GetTrunkLeanDeg` / `GetHingePinnedFraction` / `GetCrownY` / `GetCrownTip`
  / `GetHoldStats`. The apple JSON splits into a run block (25 keys, measurements
  only) and a `build:` block (42 keys + `joints[]`), and the apple run writes
  `<label>_joints.csv` — 1600 rows, `t` + 10 columns x 15 joints. No key was
  renamed; the constants that used to sit in the run block moved into `build:`
  (`tree-report.py` reads both blocks now).
- Acceptance met: `apple_t1_hold` / `apple_t1_wind012` (build 8000f531) against
  the pre-T1 `apple_t1base_hold` / `apple_t1base_wind012` (build f9d594f0) —
  **23 of 23 shared run-block keys bit-identical on BOTH runs**, and the oak
  re-run `tree_t1b_hold` / `tree_t1b_wind012` is bit-identical to the accepted
  `tree_t1_*` on both hold and wind. Instrumentation only: zero physics change.
- First apple diagnosis from the trace (T2 input, not yet a brief): the apple
  rig is NOT quiet. J00 (trunk) max|angle| 1.5094 against limit 1.50 — PINNED at
  its own stop; J08 (order 1) 23.87 deg against limit 5 with 1589 of 1600 samples
  at the cap; J12 (order 2) 31.87 deg against limit 12; J14 (twig) 28.56 against
  28. `maxAbsAppliedTorqueNm` = 40 on the nose (the twig cap) and
  `springSatFraction` 0.0662 — NONZERO here, unlike the oak (0), so on the apple
  the saturation mechanism the review named IS live. `holdResidualNm` 35.31 N·m.
- The two reported apple sway numbers are STILL untrustworthy (job order 1 of the
  apple brief is not done): `hingePinnedFraction` remains a maximum instantaneous
  fraction (0.40 hold / 0.27 wind) and `swayAmpM` is still 0.67 m with and without
  wind — a startup transient, with the measured crown COM drift only 0.096 m
  (`crownComDxM`). T1 did not add a settled window or a wind-off run: the plan
  allows one change per cycle and forbids new signals outside T4/T5. The CSV and
  `joints[]` now make both computable offline.

- **T1 GAP CLOSED 19.09** (build `cae2483a`): the apple's
  `build.joints[].gravityGradient` is no longer 0 on all 15 joints.
  `TreeBase.BindGravityHold` never wrote the field (PlantTree's did); it does now, and the
  only reader in the tree is PlantTree's ordered-stem law, which the apple path never runs
  — instrumentation only, no force. Labels `apple_t1gap_hold` / `apple_t1gap_wind012` and
  `tree_t1gap_hold` / `tree_t1gap_wind012` (fp `cae2483a…`). Apple against the pre-edit
  player `apple_t1g0_*` (fp `c0b27669…`): run block 28 of 30 keys bit-identical
  (`wallPerSim` + `sourceFingerprint` only) and the only fields that move anywhere in the
  file are the 19 gradient values (0 → 305.7497 / 52.95278 / 7.346342 / 0.6321625 by
  order). Oak against the accepted `tree_t1_*`: build block 93/93 identical, run block 27
  of 29 (same two meta keys) — the canon is untouched. Report
  `Trials/Runs/apple-t1gap-report.md`.
- Still open from T1: the apple brief's job 1 (settled window, stable tip identity,
  wind-off decay) is NOT done, so `hingePinnedFraction` and `swayAmpM` remain numbers that
  do not mean what they look like; and the uncommitted `CentreJointStopsOnBind()` in
  `Assets/TreeBase.cs` still sits in every plant build, so the apple is not bit-identical
  to the committed `apple_t1_*` (53 keys moved, re-measured 19.09).

### T1-sway — job 1 CLOSED, 19.09 (build `3f93bfc4`)

The bullet above is done for its first half, instrumentation only, one change:

- `PlantBase.windOffSeconds` (+ the gate in `TreeBase.FixedUpdate`, `-appleWindUntil <s>`):
  the run can switch the sail OFF mid-trial. At 0 the wind is issued on exactly the same
  steps as before — proven by bit-identity, not asserted.
- New run-block keys (all additions; no key renamed, no key redefined): `swayFixedTipAmpM`,
  `swaySettledAmpM`, `swaySettledPkPkM`, `worstJointPinnedTimeFraction`,
  `hingePinnedJointCount`, `maxHingeOverrunDeg`, `windOnMaxAbsJointAngle`,
  `windOffMaxAbsJointAngle`, `residualAbsJointAngleDeg`, `residualTrunkLeanDeg`,
  `residualTipDxM`. The tip SEGMENT is chosen once and followed; the settled window is
  t >= 4 s; the residual window is the last second. Windows + tip index live in `build:`.
- Verification: `apple_t1sw_*` vs `apple_t1sw_base_*` (pre-edit player, same sources) —
  28 of 28 physics run keys bit-identical on hold AND wind 0.12; oak `tree_t1sw_*`
  reproduces the accepted canon (0.00103 / 34.17341 / 0.2553246 / pinned 0.20). Only
  `wallPerSim` + `sourceFingerprint` move.
- What the honest windows say (T2/T4/T5 input): `swayAmpM` 0.1465 m at wind 0.12 was a
  STARTUP TRANSIENT — the settled sway is `swaySettledAmpM` 0.0222 m (pk-pk 0.0396), below
  the acceptance band 0.10-0.30 m; the pinned joint is J08, 68.3 % of the hold samples,
  0.674 deg OUTSIDE its own stop, with `maxHingeOverrunDeg` up to 2.066 deg under wind;
  wind off at 4 s → the residual is 0.0220 deg from the no-wind hold pose and the crown tip
  returns within 0.0007 m, with the motion envelope falling 10x in one second.
- The uncommitted `CentreJointStopsOnBind()` is still in the working tree (the apple is
  still not bit-identical to the committed `apple_t1_*`), so T1-sway compares baseline vs
  post on the SAME sources. Report `Trials/Runs/apple-t1sw-report.md`.


### T1-oak — the same windows on the oak, job 1 closed for BOTH trees, 19.09 (build `8e9ed340`)

`PlantTrialRecorder` now carries the apple's T1-sway block (same keys, same
windows): `swayFixedTipAmpM`, `swaySettledAmpM`, `swaySettledPkPkM`,
`worstJointPinnedTimeFraction`, `hingePinnedJointCount`, `maxHingeOverrunDeg`,
`windOnMaxAbsJointAngle`, `windOffMaxAbsJointAngle`, `residualAbsJointAngleDeg`,
`residualTrunkLeanDeg`, `residualTipDxM` (+ `windOffS`, `swaySettledWindowStartS`,
`residualWindowS`, `tipSegmentIndex` in `build:`). `PlantTree.windOffSeconds`
(default 0) gates `ApplyWind()` and `-treeWindUntil <s>` wires it; `PlantTree` is
not a `PlantBase`, so it is a field of its own. Instrumentation only, proven by
bit-identity against the pre-edit player `tree_t1oak_base_*` (fp `ca4ed1b6`):
no run key moved on hold or wind 0.12, 11 keys added, and the apple controls
(`apple_t1oak_*`) reproduce the accepted `apple_t1sw_*` to the digit.

The oak readings (T3/T4 input): `swayAmpM` 1.287312 m is a transient — stable tip
over the whole run 1.473969 m, settled (4-8 s) 0.179467 m, pk-pk the same, inside
T3's [0.05H, 0.15H] for H = 2.332039 m; pinning is 3 of 15 (J00 66.1 %, J08 85.2 %,
J01 7.5 %), worst overrun 0.151758 deg; wind off at 4 s → motion decays
(mean |dAngle|/step 0.00311 → 0.00070) but the POSE stays: residual 32.72058 deg,
trunk 16.0264 against its 16.0 stop, tip 1.368073 m. The 180 N·m/rad trunk spring
cannot lift the 259.7 N·m/rad weight gradient, so T3's RETURN TEST fails on the oak
today — by the rig, not by the measurement. Report `tree-t1oak-report.md`.

### T1 close-out + the T2 frame fix STOOD AND COMMITTED — 19.09, commit `c9129bc1`

Zero source edits this cycle; seven runs on the player `cb7e4ef6` (gitCommit `9a43a74e`):
`tree_t1c2_hold/_wind012/_windoff`, `apple_t1c2_hold/_wind012/_windoff` and one repeat.

- **Bit-identity:** the oak canon `tree_t1oak_*` is BIT-IDENTICAL on hold, wind 0.12 and
  wind off (run block and build block; 0 of 1600 `joints.csv` rows differ), and so is the
  apple's post-edit pair `apple_t1sw_hold/_wind012` (0 differing rows). Against the PRE-T2
  `apple_t1_*` pair the apple moves 23 run keys, 11 added, 0 lost — the intended physics
  change, so that pair is the "before", not a target.
- **The T2 frame fix met every stand criterion on the fresh runs:** born-behind 4/15
  (J01, J05, J08, J12) -> **0/15**; `springSatFraction` 0.06620833 -> **0** on every joint
  (per-joint CSV: J08 at its 40 N·m cap in 0.993125 of 1600 rows -> 0.000000); oak pair
  bit-identical; `sourceFingerprint` == `Build/Headless/build-manifest.json`; rigs unchanged
  (oak `dcc15b908146504e`, apple `b242a8ddd29b0b28`). Committed as `c9129bc1` — ONE file,
  `Assets/TreeBase.cs`, 41 insertions; nothing else staged (the rest of the dirty tree is
  the art/level/walk streams).
- **Open, found this cycle:** `apple_t1sw_windoff` does NOT reproduce — it diverges from its
  own sibling `apple_t1sw_wind012` (same fp, same player) at **row 2, t = 0.01 s**, 1599 of
  1600 rows, so that artifact was produced by a player its fingerprint does not name. Today's
  wind-off runs are byte-identical to each other and their first 800 rows equal the accepted
  wind-on label; wind-off evidence is now `apple_t1c2_windoff` (release peak 6.448067 deg,
  0.11 deg past the wind-on peak 6.335289; residual 0.022004 deg, crown tip 0.000672 m).
  The oak's wind-off label is fine (bit-identical to today's rerun). The apple's wind-off
  gate opens exactly at t = 4.004949 (first differing row against its own wind-on sibling).
- `tree-const-assert.py` fires on the apple pair for three DISCRETE counts
  (`springSatFraction` 0, `hingePinnedFraction` 0.06666667, `hingePinnedJointCount` 2). The
  CSV explains them: the same two joints (J08, J01) are the only ones that ever enter the
  0.2 deg band in either run. The oak's same keys differ between its runs, so the signal is
  live; J08's 0.674 deg overrun in still air is the SOFT stop, which is T4's.
- Report `Trials/Runs/apple-t1c2-report.md`; helpers `_t1c2-read.py`, `_t1c2-accept.py`
  (output `apple-t1c2-accept-output.txt`), `_t1c2-firstdiff.py`, `_t1c2-pinned.py`,
  `_t1c2-joints.py`.

### T1 re-verified on a NEW player, and `-visual` is not a neutral flag — 19.09 (cycle `T1`)

Zero source edits, eleven runs, the accepted labels re-run on the player that the other four
streams have since rebuilt: fp `4681f70b…`, gitCommit `ac2299ea`, built 15:11, gate "OK at 15:12",
every `.cs` older than the DLL, no editor, no lock. The accepted baselines were made on
`cb7e4ef6` (gitCommit `9a43a74e`).

- **T1 reproduces bit for bit.** Run block AND build block AND all 1600 CSV rows: oak
  `tree_t1d_hold/_wind012/_windoff` == `tree_t1oak_*`; apple `apple_t1d_nv_hold/_wind012/_windoff`
  == `apple_t1sw_hold/_wind012` and `apple_t1c2_windoff`; a repeat of the wind-off run is
  bit-identical to its sibling. The negative controls move as they must: the PRE-T2
  `apple_t1_hold` pair moves 23 run keys (11 added), and the STALE `apple_t1sw_windoff` moves 12
  — that artifact was made by a player its fingerprint does not name, confirmed a second time.
- **FOUND: `-visual` changes the recorded trajectory, deterministically, on BOTH rigs.** The apple
  diverges from row 2 (t = 0.01 s) with 1599 of 1600 rows differing and 1.725 N·m in
  `J00_springTorque` at that row; the oak diverges from row 2 with 1231 rows (5.95e-03 deg) but
  CONVERGES back (last-row delta 0.000e+00, it ends on its trunk stop) while the apple does not
  (7.7e-04 deg, J08 never leaves its soft stop). It is the harness, not a regression: the
  `-visual` apple run on the OLD player (`apple_t1c2_photo_hold`) is bit-identical to today's
  `-visual` run (`apple_t1d_hold`) and the no-`-visual` runs match the accepted pair, so both
  variants are deterministic and the flag simply chooses which. Therefore the apple's recorded
  physics depends on the shape of the run: **compare labels only within the same switch shape; a
  physics label must not carry `-visual`** — which is why the accepted apple pair is the
  `-visual`-free `apple_t1sw_*`/`apple_t1c2_*` and the picture labels are
  `apple_t1c2_photo_*`/`apple_t1d_*`. The oak's labels all carry `-visual` (tree-probe.sh always
  passes it) and are consistent with each other.
- T2's frame fix still stands on the new player: born-behind 4/15 (J01, J05, J08, J12) on the
  PRE-T2 pair, **0/15** on every today's-shape run; J08 sat at its 40 N·m cap in 0.993125 of 1600
  rows before the fix and in **0.000000** after, on every joint. Rigs unchanged
  (`dcc15b908146504e` / `b242a8ddd29b0b28`), `jointColumnCount` 15.
- Three acceptance numbers the apple still misses, unchanged and not instrumentation work:
  `hingePinnedFraction` 0.06666667 (2 of 15 — J08 0.674 deg outside its own stop in still air,
  overrun up to 2.066446 deg under wind: SOFT stops, T4); settled crown sway 0.02222349 m at wind
  0.12 is BELOW the 0.10-0.30 band (T5); `crownDrop` 0.00667548 m in the hold is above the 2 mm
  target — and the T2 frame fix RAISED it from 0.00265265 (pre-T2), because centring the stop band
  on the bind angle moved the joint that carries the crown by ~4 mm. New T2/T4 input, not a win.
- Wind off at 4 s (today, no `-visual`): release peak 6.448067 against the wind-on peak 6.335289
  (an overshoot, not a return), last-second residual 5.693279 against the no-wind hold's 5.671275
  = **0.022004 deg**, crown tip 0.1035979 against 0.1029263 = **0.000672 m**. T1-sway's reading
  stands to the last digit on the new player.
- Picture `Trials/Runs/apple_t1d_windoff.mp4` (865 646 bytes, 82 frames, 5 fps, 1080p) +
  `apple_t1d_windoff_photo.png`; one apple tree, whole and centred, nothing else in frame.
- Report `Trials/Runs/apple-t1d-report.md`; helpers `_t1d-accept.py`
  (output `apple-t1d-accept-output.txt`), `_t1d-visual.py`, `_t1d-firstdiff.py`.
- **Next: T2 (diagnosis), not started here.**

### T1 re-verified on the ART-0 player, zero edits — cycle `T1` (apple worker), 19.09

T1 had **no open work** (trace, run/build split, the constant-suspect assertion, the apple
`gravityGradient` gap, the settled window + stable tip + wind-off run, and the T2 frame fix
that had been sitting uncommitted are all landed and committed), so the cycle is the test
T1 itself names: a rerun in the accepted switch shape. Seven runs, **zero source edits**,
one picture, on the player `3c4dfc53…` (gitCommit `8ce38675`, built 14:54:08Z, 632 files,
gate "OK at 17:54", no editor, plant sources clean against HEAD).

- **Bit-identical, run block AND build block AND all 1600 CSV rows:** `apple_t1e_hold` ==
  `apple_t1sw_hold`, `apple_t1e_wind012` == `apple_t1sw_wind012`, `apple_t1e_windoff` ==
  `apple_t1c2_windoff`, `apple_t1e_windoff` == its own repeat `_b`, all three also == the
  previous cycle's `apple_t1d_nv_*`, and `tree_t1e_*` == `tree_t1oak_*`. Run `moved=0
  lost=0 added=0` on every pair; the only moving keys are the run-file meta and the wind
  config in `build:`. The negative controls move as they must: the PRE-T2 `apple_t1_hold`
  pair moves 23 run keys (11 added, build `joints` + the four gradients) and the STALE
  `apple_t1sw_windoff` moves 12, CSV from row 2.
- **New: the canon reproduces across THREE players** (`cb7e4ef6` -> `4681f70b` ->
  `3c4dfc53`, two rebuilds by other lanes): the plant trajectory is invariant to a rebuild,
  so the plant instrumentation reads nothing the other lanes can move.
- Const-assert FIRED with the same discrete sets as the close-out (apple 3:
  `hingePinnedFraction` 0.06666667, `hingePinnedJointCount` 2, `springSatFraction` 0; oak
  1: `springSatFraction`), and J08 stays the one pinned joint (0.683 of the hold samples,
  0.296 at wind 0.12, 2.066 deg outside its own SOFT stop; the oak's worst overrun is
  0.1518 deg = hard stops).
- The apple's three unmet acceptance numbers are unchanged and not instrumentation
  (`hingePinnedFraction` 0.0667, settled sway 0.0222 m against 0.10-0.30, `crownDrop`
  6.68 mm); the wind-off run proves decay (residual 0.022004 deg, tip back within
  0.000672 m).
- Picture `apple_t1e_photo_windoff` (41 frames, mp4 865 646 B, photo 725 666 B) is
  **byte-identical** (`md5`) to `apple_t1d_windoff`'s picture and frames, so the `-visual`
  variant is deterministic across players too.
- **The apple brief's job 2 is already landed by the level worker** (`Assets/Scenes/
  TreeApple.unity` + `GameProcess.cs:500-519` routing `AppleTree` via `treeSpecies`), so a
  future apple brief must not ask for it again.
- Report `Trials/Runs/apple-t1e-report.md`; helpers `_t1e-runs.sh`, `_t1e-accept.py`,
  `_t1e-photo.sh`, `_t1e-dump.py`.
- **Next: T2 (diagnosis) / T4 (the soft stop), not started here.**

## Task order (one change per cycle; distance/lean targets are NOT the acceptance here)
T1 - INSTRUMENTATION ONLY, zero physics change.
  Per-joint per-step CSV (row per FixedUpdate; per joint: jointAngle, limitMin, limitMax, springTorque,
  holdTorque, frictionTorque, rotation, inertia, mass); a separate `build:` block for build constants;
  a build hash (sorted segment positions + sizes); and a CONSTANT-SUSPECT assertion that flags any
  metric bit-identical across two runs with different wind. The recorder must read ONLY Unity
  primitives (rb.rotation/position/mass/inertia, joint.jointAngle/joint.limits) and NEVER a helper on
  TreeBase (GetTrunkLeanDeg / GetHoldStats / GetMaxAbsJointAngle / GetHingePinnedFraction) - a metric
  the fix can rewrite is not a measurement.
  New metrics: maxAbsAppliedTorqueNm (N.m), springSatFraction (0..1), holdResidualNm (N.m),
  kOverStabilityBound (dimensionless), maxAbsSegmentWorldDevDeg (deg), crownComDxM (m), and
  trunkJointAngleDeg logged as a PAIR with trunkLimitDeg.
  Acceptance: rerun oak wind 0.12 -> the physics numbers are BIT-IDENTICAL to today
  (maxTrunkLeanDeg 16.05, maxAbsJointAngle 34.17, crownDrop 0.255); 14 joint column groups present;
  the assertion fires on maxAbsHoldTorque and treeMaxAbsBranchHeading (both are build constants
  reported inside a run block - move them to `build:`).
  Stop rule: if ANY physics number moves, the instrumentation changed behaviour - revert, do not go on.
T2 - DIAGNOSIS ONLY, no rig change. From T1's CSV: springSatFraction per joint, the first step each
  joint reaches its limit, kOverStabilityBound, and the trunk load split into
  (children reactions | crown gravity moment | direct wind).
  Acceptance: a table naming which order eats the 34 deg and the crown-gravity share of trunk load.
  STOP RULE (falsifies the review itself): if the crown gravity moment is < 30 % of the trunk load, the
  mechanism is wrong - stop and re-brief before touching the rig. If springSatFraction = 0 everywhere,
  the saturation mechanism is wrong - same stop.
T3 - ship Variant B: fold order 0 into the static stump (trunk becomes a second collider on the static
  body, no joint/spring), first hinge at the crown base. Bodies 16 -> ~13, joints 14 -> ~12: a
  REDUCTION. Acceptance: maxTrunkLeanDeg <= 0.05 deg at wind 0.12 AND 0.36 (now provable, the trunk is
  static); hingePinnedFraction 0; swayAmpM in [0.05H, 0.15H]; RETURN TEST (wind off at T/2):
  |crownTipX(T) - crownTipX(0)| <= 0.02H and maxAbsJointAngle(T) <= 3 deg.
  Stop rule: if the return test fails, do NOT raise stiffness - go to T4.
T4 - caps become consequences: maxTorque = max(k*limitRad*1.2, |moment|*1.5 + 16); assign
  gravityGradient in BindGravityHold and apply it in TreeSpring.Apply. Acceptance: springSatFraction 0
  on every joint at wind 0.12 and 0.36; return test passes.
T5 - wind semantics: a zero-mean "breeze" preset (steady case renamed windLoad and honestly expected to
  lean). Acceptance: sum(f dt)/sum(|f| dt) <= 0.10 for breeze; the crown tip X crosses its start value
  >= 6 times in 10 s (that is what makes swayAmpM an amplitude again).
T6 - VARIETY (Yury's "natural and diverse"): wire the seed through the whole recipe with every range
  expressed as a multiple of H - target height 2-5 m, trunk width H*(0.035-0.060), trunk length
  H*(0.30-0.45), forkAngle 24-40 deg, leaderForkShare 0.22-0.45, lengthDecay 0.60-0.74, widthDecay
  0.56-0.68, side bias +-0.25, base heading +-4 deg, leader dominance 0..1. maxDepth stays 3,
  maxSegments stays 15.
  Acceptance - the 24-seed battery: silhouette rasterized 64x64 normalized by H, min pairwise distance
  >= 0.08, median in [0.15,0.35]; plausibility box for all 24 (H within +-5 % of target, crown/H
  0.45-0.95, |crownComDx| <= 0.08H, slenderness 18-60, taper monotone); std > 0 on EVERY feature
  (that is the check that catches "seed wired to one field", i.e. today); silhouette unchanged to 1e-3
  after 10 s at wind 0 (variety must come from the build, not from drooping). Plus one 24-tree contact
  sheet for Yury's eye - the numbers decide pass/fail, the eye decides whether the box is set right.

## Standing rules for this stream
- One change per cycle, one build, one trial, one report, then stop. Two failed acceptances on one
  cycle -> stop and report.
- NEVER add a `...First` 0/1 toggle. Add or delete a SIGNAL. The only new knobs this plan allows are
  the deflection budget signal (T5) and the derived cap (T4).
- No wind run is a NULL, not a pass. Label it so.
- A metric the fix can rewrite is not a measurement: sensors read Unity primitives only.
- If Yury's editor is open and the build gate reports SKIPPED/STALE: do not retry builds, do the code
  part, say so, stop.
