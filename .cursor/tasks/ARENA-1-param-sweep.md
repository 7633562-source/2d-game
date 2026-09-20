# ARENA-1 - parameter sweep arena (Yury, 18.09.2026)

New scene, additive: it does not cancel the walk, dog or plant work.

## What Yury asked for
- A separate scene with JUST GROUND, nothing else.
- Spawn a new human every 500 ms, each with different values of the parameters we are
  currently tuning for walking.
- Each human carries its NUMBER on its head, readable in the frame.
- Every human starts walking immediately on spawn.
- 15 s after a human spawns, measure how far it walked, in metres.
- Parameters of the humans that walked further must appear MORE OFTEN in the humans
  spawned afterwards.
- 30 s after spawn, delete the human (and so on), so RAM does not grow.

## Deliverable
Scene `Assets/Scenes/WalkArena.unity` (ground only, wide framing) AND headless support:
`-arena 1` runs the same arena in the trial player so the run can be measured and filmed.
New files only; do not rewrite the human rig.

## Genome - exactly these fields of PoseDriveController (private floats today)
swingDuration, stanceDuration, swingLiftMeters, swingLandShare, heightGainDegPerMeter,
heightDampDegPerMeterPerSecond, placementMarginMeters, comLeadSwapMeters, stepMinMeters,
stepMaxMeters, armSwingDeg, ankleBalanceP, ankleBalanceD.
Constraints: stepMaxMeters >= stepMinMeters; every value clamped to its range, ranges
written in the code and printed in the summary.
NOT in the genome (bodies must stay comparable): rig geometry, masses, contact materials,
muscle ceilings, gravity, fixedDelta. The ceiling question (knee 130 / hip 100 N*m
saturation) is a SEPARATE experiment, not this search.

## Spawn and lifecycle
- Spawn interval 0.5 s (assert the real interval from the recorded spawn times).
- ONE SPAWN POINT: every human appears at the SAME x (Yury's correction 18.09 - no lanes).
  Distance is measured from that shared spawn x.
- HUMANS MUST NOT COLLIDE WITH EACH OTHER. Put every arena human's rigidbodies on a
  dedicated layer (for example `ArenaHuman`) and switch that layer's pair OFF in
  ProjectSettings/Physics2DSettings so ArenaHuman x ArenaHuman does not collide, while
  ArenaHuman x ground still does. Humans then overlap and pass through one another instead
  of pushing or blocking. Do not touch the human rig's own joints.
  PROOF, not assertion: the elite genome is re-spawned unchanged once the log has 20+
  measured humans - the two copies (same genome, same spawn point, different ages) must
  produce the same distance and the same early trajectory; any deflection of the younger
  copy means a human-human collision is still live. Report that comparison, and also send
  one frame in which several humans visibly overlap without pushing each other.
- Each new human starts walking the moment it spawns (no stand phase window that blocks it).
- Alive cap: about 60 (30 s / 0.5 s). At 30 s the oldest is destroyed and its body count
  released. The alive count must oscillate, never grow.

## Number INSIDE the head (Yury's second correction, 18.09)

The label is drawn INSIDE the head sprite, centred on the head segment - not floating above it.
- Position: centred on the head segment centre; the old `numberHeightMeters = 0.34` above-the-head
  offset is removed.
- Size: digit height <= 60% of the head's visible height so the glyphs fit inside the outline.
- Contrast: readable against the head's own fill in a 1080p frame - opposite luminance, plus a thin
  outline/halo if the fill varies.
- No collider, no rigidbody, no physics influence, and scale-independent: digits must be the same
  size on every human regardless of segment scale.
- Must stay readable at 1x when humans overlap in a pile; if the digits become unreadable in a pile,
  say so instead of claiming readability.
- Acceptance: a 1080p frame from the run where the heads of at least three different humans are
  readable with their own numbers, attached to the report.

## Measurement (15 s)
- Distance = current x minus that human's own spawn x, in metres, sampled exactly 15 s
  after ITS spawn (one frame tolerance stated in the output).
- Upright / fallen from the RECORDER'S PHYSICAL CRITERION (|torso tilt| > 60 deg OR pelvis
  more than 0.25 m below that human's standing value, sustained 0.1 s). The trial `fell`
  flag is known to lie (measured 6.035 s vs 8.415 s) - never use it here.
- A fallen human still reports its distance; do not drop the row.

## Inheritance (the point of the experiment)
At each spawn: with probability 0.75 take a PARENT from the humans that already reached
their 15 s measurement, chosen by tournament among the best quartile (fitness = distance,
a fall penalty applied), copy its genome, then jitter each gene by a small amount
(about 5-10 pct of its range). Otherwise draw a fresh random genome.
Every child records: its parent id, the mutation seed, and its own genome, so inheritance
is PROVABLE from the log rather than claimed.

## Evidence and acceptance
1. One JSON row per human: id, spawn time, genome, parent id, distance at 15 s, upright,
   destroyed at 30 s. Plus a summary: best genome so far, population mean per gene over
   time, alive-count curve, and the seed.
2. A 60-90 s video and a photo where the numbers on the heads are readable.
3. Inheritance measured, not asserted: report the fraction of children whose parent came
   from the top quartile, against the fraction expected by chance.
4. Alive count never exceeds the cap and returns down after 30 s; report max Rigidbody2D
   count and the frame time so the memory claim is visible.
5. Same seed reproduces the same genome sequence - prove it with two runs and say the seed.
6. Deterministic: seeded RNG only, never an unseeded Random.

## Forbids
No change to the human rig, the dog, birds or plants. No new textures or PNG sprites
(project rule: flat procedural shapes). No localScale for sizing. Never build from
PowerShell. Never leave the tree uncompilable: commit a checkpoint before the big edit.

## Parameter-influence LOG (Yury's addition 18.09) - the point of the run
Write an APPEND-ONLY log, never overwritten between runs:
  <project>/Trials/Runs/arena-param-log.csv
Header, fixed contract (the analyzer depends on it; add columns only at the END):
  run_id,seed,human_id,parent_id,child_kind,spawn_t,distance_m,upright,fallen,destroyed_t,
  <then one column per genome parameter>
One row per human that reached its 15 s measurement. Written even if the human fell.
Append across cycles and builds so the influence keeps accumulating; keep `run_id` and
`seed` so rows of one run can be grouped and reproduced.

Then the influence analysis: for every parameter, the relation between its value and the
distance walked (Spearman rank correlation, plus mean distance in the top and bottom third),
with the sample count, and "no signal yet" when n is too small. Publish it in Russian context
but English text at F:/hermes-data/arena-param-influence.md, and run the shared reader
  python F:/hermes-data/scripts/arena-param-influence.py
which already implements exactly that and prints the best genomes too. Correlation is NOT
causation: parameters mutate together, so a strong rho is a hypothesis for the next
generation, not a conclusion. Say so in the report.

## Evolution principle - MUTATION NEVER STOPS (Yury's correction 18.09)
Yury: "the mutations must run all the time; that is the criterion of evolution".
- EVERY new human is mutated. There are NO exact copies, ever - not even of the best genome.
  Elitism means the best genome is chosen as a PARENT more often (a selection bonus), and its
  child is still jittered. A child whose genome is bit-identical to its parent is a defect and
  must be visible as such in the log.
- Fresh blood stays permanent: 25 pct of spawns are fully random, in every generation, not
  only at the start.
- Mutation pressure must not decay: the jitter amplitude is a constant floor (never annealed
  to zero). If the population's spread for a gene falls below a floor (about 2 pct of that
  gene's range), RAISE the jitter for that gene for the following spawns until the spread
  recovers. State this rule in the code comments.
- Selection: tournament among the best quartile of the measured humans (fitness = distance
  minus a fall penalty), with the elite parent bonus above. Parent_id and the mutation seed
  are logged for every child, and so is the per-gene delta, so that "mutation happened" is
  provable from the log.
- Acceptance for this rule: over a run of at least 3 minutes, the fraction of children with a
  zero mutation delta is 0.000, and the per-gene population spread never sits at 0. Report
  both numbers, plus the spread curve, as evidence.
- COLLISION CALIBRATION (kept separate from breeding): the first TWO humans of a run are
  spawned with a deliberately identical genome so the no-human-collision layer can be proven
  (same genome, same spawn point, different age => same distance and early trajectory). This
  is a calibration pair, not the breeding scheme.


## WIDE genome - Yury's requirement 18.09: "almost every parameter that can influence walking"

The first build sampled 13 continuum parameters; the wide set below lists 155 that exist in the human stack.
Verified spread of that first build (run arena1e_sweep_s31): 149 distinct genomes out of
181 humans, every parameter spanning its full declared range - so VALUES are varied, but the
SCOPE was narrow. Widen it to the groups below.

### C - control layer (continuum, sampled every spawn)
- C1 pose drive (PoseDriveController): the 13 already sampled + standCaptureSeconds, standTiltLimitDeg
- C2 capture walk (BalanceControllerCaptureWalk): capture* continuum only, NOT the *First flags:
  captureStepDuration, captureSwingXShare, captureTargetSpeed, captureRunSpeedScale, captureSpeedGain,
  captureLookahead, captureStepMin, captureStepMax, captureSwingApex, captureReachMargin, captureStepBackMax,
  captureFootLandLift, captureFootPress, captureFootPitchDeg, captureDoubleSupportMin, captureDoubleSupportMax,
  captureDoubleSupportShare, captureTouchdownBlendSeconds, captureDoubleSupportHoldSeconds, captureLandKneeFlex,
  captureStanceLegLength, captureLandLegLength, captureEntryRampSeconds, captureLegPushCap,
  captureSupportKneeExtendRate, captureStanceHipTorqueCap, captureStanceHipTorqueCapForward,
  captureTrunkLeanDeg, capturePelvisLeanDeg, captureCopLead, captureCopScale, captureCopTravelBias,
  captureLaunchSeconds, captureLaunchAnkleCap, captureLaunchSpeed, captureLaunchLead, captureCatchPast,
  captureRecoveryMargin
- C3 walk lift (BalanceControllerWalkLift): walkLiftKneeFlex, walkLiftClearHeight, walkLiftStepLength,
  walkLiftStepMax, walkLiftAdvanceSeconds, walkLiftHipRateDeg, walkLiftCoupleHipShare, walkLiftCoupleAnkleShare,
  walkLiftCoupleHipMax, walkLiftReactionCap, walkLiftArmGain, walkLiftClearance, walkLiftStancePdScale,
  walkLiftUnloadSeconds, walkLiftClearSeconds, walkLiftRestoreSeconds, walkLiftLoadRatio, walkLiftIdleSeconds,
  walkLiftAbortDrop, walkLiftAbortTilt
- C4 step phase / gating (StepPhaseDriver): stanceComTrigger, stanceComMinAge, stanceComTriggerPerSpeed,
  stanceHeelStrikeMinAge, stanceHeelAirMin, walkCadenceStanceGain, walkCadenceMinStance, walkTiltAbortDeg,
  walkTiltRecoverDeg, fallComLeadTrigger, fallPelvisLean, fallForwardLeanScale, reachKneeFlex, reachKneeLandDeg,
  reachExtendStart, catchKneeFlex, catchMinDuration, catchMaxDuration, catchFootLead, catchFootLeadMin,
  catchLeadCaptureMargin, catchComLead, fallKneeRampSeconds
- C5 arm drive (ArmDrive): walkShoulderSwing, walkElbowMid, walkElbowAmp, walkRampSeconds, walkHipSpeedDegPerSec,
  walkPGainScale, walkDriveSlewPerSecond, walkHoldSeconds, crossWeight, ipsWeight, hipStatsTau, minHipVarianceDeg2
- C6 balance loop (BalanceController): comProportionalGain, comDerivativeGain, maxBalanceSignal, hipBalanceGain,
  hipBaseAngle, kneeBaseAngle, kneeRecoveryFlex, comOffsetReference, comVelocityReference, tiltSpeedReference,
  swingHipFlex, swingKneeFlex, swingKneeGroundedFraction, swingHipUnloadBias, forwardSwingHipUnloadScale,
  forwardSwingKneeGroundScale, backSwingHipUnloadScale, backSwingKneeGroundScale

### B - body layer (also sampled, logged as group B so we can tell strength from control)
- B1 muscle ceilings (Human): hipMuscleTorque, kneeMuscleTorque, ankleExtensorTorque, ankleFlexorTorque,
  toeMuscleTorque, lumbarMuscleTorque, shoulderMuscleTorque, elbowMuscleTorque
- B2 actuator (ActuatorMixer): activationSpeed, speedReference
- B3 contact (Human): footFriction, handFriction, hipFriction, kneeFriction, ankleFriction, toeFriction
- B4 geometry/mass (Human): totalMass, muscleMultiplier, frictionMultiplier, footHeelShare, ankleHeelOffset,
  hipHalfSpacing, shoulderHalfSpacing, hipLimitMin, hipLimitMax, kneeLimitMin, kneeLimitMax, toeLimitMin, toeLimitMax

### Ranges and rules
- Gains and ceilings: 0.25x to 4x the shipped default, clamped to physically sane values (torque > 0).
- Timings and durations: 0.5x to 2x default. Lengths/margins: 0.5x to 2x default, never negative.
- Frictions: 0.25x to 4x default. Masses/multipliers: 0.7x to 1.4x default (no super-heavy bodies).
- Limits: keep ordered (min < max) and inside the shipped hardware limits; enforce by construction.
- HARD invariants: stepMaxMeters >= stepMinMeters, hipLimitMin < hipLimitMax, every torque > 0,
  every duration > 0. Violations are repaired at sample time, and the repair is logged.
- Bounds must be handled by REFLECTION, not clamping: in the 13-parameter build 26.5% of
  heightDampDegPerMeterPerSecond and 23.8% of ankleBalanceD sat exactly on a bound, which wastes samples.
- Every sampled parameter is written as its own column in arena-param-log.csv (same schema, wide header),
  plus a group tag so the influence analysis can aggregate per layer.

### Excluded on purpose (do not randomise)
- The hundreds of 0/1 `*First` toggles in BalanceController.cs from earlier patch work: randomising
  toggles is noise, not search. They must be inventoried and de-duplicated separately (tech-debt task).
- Ground/gravity and the fixed timestep: changing them changes the world, not the walker.

### Budget
A ~155-dimension space needs more samples than 151 per 75 s: run the sweep 5-10 minutes
(600-1200 humans), first phase pure random (fresh 100%) to map the space, then evolution.
Report max alive, wall per sim second, and distinct-genome count so thin sampling is visible.
