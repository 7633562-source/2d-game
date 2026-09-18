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
