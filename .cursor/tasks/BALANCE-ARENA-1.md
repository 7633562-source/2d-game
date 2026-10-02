# BALANCE-ARENA-1 - the impact harness (Yury's order, 19.09.2026)

Source contract: `F:/hermes-data/ds-balance-scene-task.md` (read it whole - it is the brief).
Yury asked for the video of this scene on 19.09; it does not exist yet because the scene does
not exist yet. This card is the harness delivery, not the win.

## What Yury asked for
A separate scene where the human stands and objects are thrown at different parts of his
body; he must react automatically and keep his footing. Later this becomes the game's
impact / combat behaviour (enemies, attacks, interaction with the world).

## Scene
`Assets/Scenes/BalanceArena.unity`, built like WalkArena (the world is spawned at runtime;
Main Camera + a host component). STRICTLY 2D: the object flies in the X-Y plane, no
out-of-plane force, no fake depth.

## Thrower (every parameter from the command line, so a run is reproducible)
- `-impactMass <kg>`: 0.5-15. Mass and shape are INDEPENDENT parameters.
- `-impactShape circle|square|rect <size_m>`: the thrown body is a real `CircleCollider2D`
  or `BoxCollider2D` of that size. Round, square and rectangular are all legitimate -
  nothing here is a "ball" or a "sword"; it is whatever 2D body the switches describe.
- `-impactSpeed <m/s>`: 4-20
- `-impactAim head|torso|shoulder|thigh|shin`
- `-impactSide left|right` (in 2D: thrown from the left or from the right)
- `-impactAt <s>`: time of the first throw, default 8 s (after the stand settles)
- `-impactCount <n>`: default 3, spaced 6 s
- `-impactSeed <n>`: the aim/timing jitter comes from the seed, never from frame timing -
  the simulation is deterministic and must stay so.
- The thrown object is a real `Rigidbody2D` with a mass and a real collider: NEVER add a
  scripted force to the human. The impact must be a real collision or the measurement is
  worthless.

## Recorded per throw
`fell` (the shared `FallCriterion` AND the controller's own state), time to fall (s), peak
CoM excursion after the impact (m), minimum support margin after the impact (m), steps
taken after the impact, time to come back inside the support polygon (s), peak joint
torques for hip/knee/ankle (N*m) with the limit-hit fraction, peak `fzTotal` (N) and the
delivered vertical impulse (N*s), and whether the rig pushed back at all (vertical force
above body weight after the impact, with the frame number where it happens).

## No clairvoyance (Yury's rule)
The human must not know the mass, the speed, the aim point or the time of a throw. He
reacts AFTER the hit. The impulse (mass * speed) is OUR reference in the log, never an
input to his controller.
What a living body is allowed to sense, and nothing else: foot contact and load (his own
plantar pressure, which arrives only after the hit); proprioception (joint angles,
velocities, the torque he asks of each muscle); the vestibular analogue (head angular
velocity and linear acceleration).
Latency is part of the model, not an accident: 30-50 ms spinal reflex, 100-150 ms
long-loop, 150-250 ms voluntary. The first 40-150 ms after the hit must be PASSIVE -
compliance, inertia, joint limits and the physics of a real rigidbody absorbing the blow,
with no active answer in that window.
- L1 REACTION LATENCY: report the frame of first contact and the frame where the first
  measurable response starts (CoM acceleration above noise, or a joint torque change above
  noise). The gap must be 40-150 ms. A gap near 0 FAILS: it means the controller saw the
  future.
- L2 NO ANTICIPATION: the throw time, side and aim vary with the seed, and the human's
  code must not read the thrower's state at all (grep the arm/torso/leg code for any
  reference to the projectile and report it). Bracing before the hit is a defect.
- L3 PASSIVE vs ACTIVE SPLIT: run the same throw twice, once with the active reaction
  disabled (test mode) and once with it enabled; the passive run measures what the body
  geometry and inertia absorb for free. Report both numbers.

## Acceptance - the FIRST delivery is the harness, not the win
1. The scene runs and writes one CSV/JSON row-group per throw with the columns above.
2. Reproducible: two runs with the same seed are bit-identical.
3. The object is strictly in-plane and the impulse (mass * speed) is reported.
4. No regression of the quiet stand: with `-impactCount 0` the scene reproduces the stand
   numbers of `Trials/Runs/burn_stance.json` (`pelvisDrop` 0.0148, `rmsShoulder` 2.6542,
   `rmsElbow` 7.5647, `armSwingFraction` 0.0).
5. THEN report the first measured impacts. The human is EXPECTED to fail most of them
   today - that failure is the measurement that sizes the vertical force channel.

## Evidence for Yury
- a >= 15 s 1080p video of the first throws, plus >= 3 stills including the contact frame,
  all under `Trials/Runs/`;
- the run JSON/CSV with the run identity (label, seed, `sourceFingerprint` ==
  `Build/Headless/build-manifest.json`).

## Implementation order (each step must run before the next)
1. The scene + the thrower + the recording columns.
2. `-impactCount 0` reproduces the stand bit-for-bit.
3. Two runs, same seed, bit-identical.
4. First real throws REPORTED (impulse, latency L1, push-back or no push-back).
5. Only after 1-4: any change to how he RESPONDS (a step, a hip strategy) - one change,
   one build, one run, and the stand baseline must stay bit-for-bit.

## Rules
One cycle = one change. No new 0/1 toggles. Builds from bash only
(`F:/hermes-data/scripts/grok-trial-run.sh` is the recipe). All art stays placeholder in
this scene - it is about force, not looks. Never leave the tree uncompilable: commit a
checkpoint before the big edit.
