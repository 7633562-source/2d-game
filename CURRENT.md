# Now (door for a new chat)

Read this and `.cursor/rules/`. Do not copy chat history.

**Russian exists only in live chat with Yury.** Everything else is English,
always — existing and new. See `docs-english.mdc`.

## Object

The human is assembled in `Human.cs` (public fields). No prefabs. Scenes:
`SampleScene` (stand, do not touch), `World` (yard: flock 6 / hens 10 / dogs 3; `treeCount = 0`),
`Bird` (one crow + AI), `BirdFlock50` (flock AI), `Chicken` (hens),
`Tree` (plants), `Dog` (quadruped), `Campfire` (fire lab), `BalanceArena` (impact lab: the human stands, objects are thrown at him; level worker 19.09), `Gallery` (art catalogue), `TreeApple` / `TreeOak` / `TreePine` (one species per scene, level worker 19.09).

Stance baseline: `Trials/Baselines/stable-pd/`. Folder `Baselines/current/`
is the old ostrich knee. Document map: `DOCS.md`. Do not delete
`Trials/Baselines` or `Trials/Runs`.

Trial proof: `Trials/Runs/<label>.json` → `applied`.

## Open

| Who | Task | Card |
|---|---|---|
| physics | **VEST-2**: per-foot quiet ankle coded (`quietAnklePerFoot` 1). Next: `pf23v2` +23 N·s. | `.cursor/tasks/VEST-2-balance.md` |
| physics | ONELEG-1 accepted (Yury): `oneleg_sym` / `oneleg_sym_l`, both 30 s, span 0.75° / 0.79°. | `.cursor/tasks/ONELEG-1-physics.md` |
| physics | ONELEG-2 **wall**: 90° pose holds; CoM ankle loop is bistable. Do not reopen. | `.cursor/tasks/ONELEG-2-march.md` |
| physics | WALK-2 closed 18.09 (`walk1_load4`, fp a53d483f): recorder complete — `check-owners.py` OK, 160 cols, no NaN, every joint owned; demanded-vs-applied answered: **hips = (b)** (applied ±100 in 190/274 frames at 69 % while the demand is ≤21 % of the ceiling and the angle target is pinned at +35.0°), **ankle = (b)** (extensor −65 pegged with a −10 N·m ask; the only real overshoot is the flexor demand 68.96 vs 22 N·m = 3.13x). One criterion for the project in `Assets/FallCriterion.cs` (human/dog/arena routed, local rules deleted) + a measured load meter (fzL/fzR/xCopMeas/lambda/dcm). Next: targets/reference, never the ceilings. Arena `fallen` still 61/61 — its baseline window sits inside the spawn settle. | `Trials/Runs/walk1_load4-report.md`, `Assets/FallCriterion.cs` |
| physics | deep crouch WIP intentionally leaves code defaults 98/28 after `crouch_accept`; accepted journal remains 55/12 until the physics work closes | HumanPhysics continuation |
| artist | code hand is `0.06×0.16`; card still wants `0.19`; walk fold at 0.19 is recorded | `.cursor/tasks/ART-HAND.md` |
| docs | pointers and baselines; do not clean Runs | `DOCS.md` |
| optimization | P1 + P2/P3 + PERF-5 closed. Yard 04.09: off-camera dogs/birds rest (`NpcRest`). PERF-1 is pre-grove and pre-yard. Do not cut 200 Hz | `OPT-3-distant-rest.md`; `opt_agent_proposals.md` |
| level | Test1 yard: campfire at x=32.5 (24 tongues + ParticleSystem sparks/smoke/embers; no sprite smoke boxes); lab scene `Campfire`; gate Static trees + World flock 6 / hens 10 / dogs 3; `treeCount` 0 | no new card |
| level | **BALANCE-ARENA-1 DONE 19.09** (level worker; fp `1d86f80e`, buildGuid `5794f0ef`): `Assets/Scenes/BalanceArena.unity` exists (Main Camera + `ImpactArena` host, runtime world like `WalkArena`) and runs in a player (`GalleryPreview.exe -scene BalanceArena` -> its own `impact_play-impacts.json`). One real `Rigidbody2D` per throw (`Assets/ImpactThrower.cs`), per-throw record (`Assets/ImpactRecorder.cs`), harness hooks `-impact 1` + `-impactMass/-impactShape/-impactSpeed/-impactAim/-impactSide/-impactAt/-impactCount/-impactSpacing/-impactSeed/-shotInterval`. Acceptance: `-impactCount 0` (40 s) is the stand **to the last digit** (pelvisDrop 0.0148, rmsShoulder 2.6542, rmsElbow 7.5647, armSwingFraction 0.0, 0 physics diffs vs `burn_stance`); same-seed pair `imp_s1a`/`imp_s1b` bit-identical (CSVs byte-identical); in-plane asserted (`outOfPlaneCount 0`). First throws: 32 N·s at the torso -> down in **0.605 s**, CoM travels 1.98 m, **0 steps taken**; 72 N·s at the head -> 0.565 s; 180 N·s at a leg -> peak fzTotal 4442 N (6.4x weight). **L1: latency = 5 ms (one frame) -> FAILS the card's 40-150 ms**: the joint PD answers the measured joint state with zero proprioceptive delay (raw frames in the report); the reflex delay belongs to the `BalanceController*` stream. Video 1080p: `Trials/Runs/impact_imp_v1_circle_0_25_m_4_kg_8_m_s_1080p.mp4` (24.05 s) + `impact_three_throws.mp4` (54.15 s) + stills. Report `Trials/Runs/balance-arena1-report.md` | `.cursor/tasks/BALANCE-ARENA-1.md` |
| level | ARENA check 19.09 (Hermes; fingerprint `b63dda80`, buildGuid `36aa1bba`; runs `arena_chk1`/`arena_chk1b`/`arena_chk_video`, seed 7, 180 s): runs, writes JSON + CSV, appends to the append-only `arena-param-log.csv` (331 rows); interval 0.5 s assert OK; alive cap 60 never crossed (361 spawns, 301 culled); `ArenaHuman` x `ArenaHuman` off, x ground on (58 overlapping bodies, no push); determinism 361/361 identical genomes and distances; inheritance 185/331 children from the top quartile (chance 0.25), zero-mutation-delta children 0/166, no gene's spread at 0; the shared `FallCriterion` discriminates (7/331 upright at 15 s; before it `fallen` was 61/61). **HEAD NUMBER FIXED 19.09** (level worker; buildGuid `19547550`, fp `4914add9`, runs `arena_num1`/`arena_num2`, seed 7): the digit is now sized from the HEAD's own collider box (measured 0.230 m) and the requested `ArenaRunner.numberHeight` 0.34 m acts as a ceiling - drawn **0.127 m = 0.55x the head** (correction's ceiling 0.60), anchored `MiddleCenter` INSIDE the skull, and read back from the rendered mesh (`measured` 24/24, `insideHead` 24/24); 7 whole bodies with 31 px digits in one 1080p frame (`Trials/Runs/photo_arena_num1_00.png`, t=6 s). Rule zero: two runs differing only in `-arenaNumber` give 81/81 identical human rows. Report `Trials/Runs/arena-number-report.md`. The genome is still the 13 C1 genes - the wide 155-gene box is a spec only (`arena-wide-genome.md`). Elite copies 65..81 drift 0.346 m from their reference at the same age (path 1.46 m) while the back-to-back pair 57/58 is exactly 0.000 m. `arena-report.py` crashed on an unbound `video` (fixed 19.09) | `.cursor/tasks/ARENA-1-param-sweep.md` |
| level | **SPECIES SCENES 19.09** (level worker; buildGuid `2c54685e`): `Assets/Scenes/TreeOak.unity` + `TreePine.unity` are clones of the accepted `TreeApple.unity` whose ONLY difference is the three fields that name the species (`Tools/species-scenes.py` writes them, `--check` re-verifies, both also in the preview player's scene list). Each scene holds exactly one tree - oak/pine run `PlantTree 1 / AppleTree 0`, every other spawn counter 0 - and each still is 1920x1080 with the whole tree in frame (`Trials/Runs/treeoak_photo.png`, `treepine_photo.png`; the control `treeapple_ctl_photo.png` reproduces the 11:02 apple numbers to the digit: height 3.256089, ortho 1.790849). The framing law `ortho = 1.1 * max(h/2, w/2/aspect)` matches all three measured boxes to 1.5e-5 m and gives 1.100 for a 2 m tree, 2.750 for a 5 m one. Measured transient: oak and pine each have ONE frame outside the frame (t = 0.020 s, 1.47 m / 0.92 m out) on the frame after the real art lands; `missFramesAfterOneSecond` 0 in all three. Remaining species: willow, bush, poplar, lilac, hazel, cedar, plum. Report `Trials/Runs/tree-species-report.md` | `Tools/species-scenes.py` |
| bird | BIRD-12: inter-bird contact is torso+head (`BirdCore`); limbs/perch `BirdLimb`. Walk = `walkThrust` on the body. Opt 02.09 flock 50 still the 200 Hz edge; no `GetContacts` | `BIRD-12-core-contact.md`; `opt_bird_code_02.md` |
| plant | TREES T1 DONE 18.09 (oak build f9d594f0, apple 8000f531, instrumentation only): `<label>_joints.csv` per-step per-joint trace, run block = measurements only vs `build:` = constants + rig hash, `tree-const-assert.py` flags constants hiding in a run block; oak bit-identical to pre-T1 (17/17 keys). **T1 apple gap closed 19.09** (build cae2483a): `TreeBase.BindGravityHold` fills `gravityGradient`, so the apple no longer reports 0 on all 15 joints — apple `apple_t1gap_*` vs the pre-edit player `apple_t1g0_*` moves 28/30 run keys nowhere but `wallPerSim`+`sourceFingerprint`, and only the 19 gradient fields move anywhere; oak `tree_t1gap_*` vs accepted `tree_t1_*` build 93/93. Report `Trials/Runs/apple-t1gap-report.md`. **T1-sway 19.09** (build `3f93bfc4`, instrumentation only): the apple's two doubted numbers are now honest — one tip segment chosen once and followed, a settled window (t>=4 s), a per-joint PINNED TIME fraction with the overshoot past the stop, and a WIND-OFF run (`PlantBase.windOffSeconds` + `TreeBase.FixedUpdate` gate + `-appleWindUntil <s>`, 0 = old behaviour). 28 of 28 physics run keys bit-identical to the pre-edit player on hold AND wind 0.12 (`apple_t1sw_*` vs `apple_t1sw_base_*`), oak reproduces the canon. Findings: `swayAmpM` 0.1465 m was a startup transient (settled sway 0.0222 m, below the 0.10-0.30 band); the pinned joint is J08, 68.3 % of the hold samples, 0.674 deg OUTSIDE its own stop, overrun up to 2.066 deg under wind (soft stops); wind off at 4 s -> residual 0.0220 deg from the hold pose, crown tip back within 0.0007 m. Next: T2 diagnosis. Report `Trials/Runs/apple-t1sw-report.md`. | `.cursor/tasks/TREES-plan.md`; `plant-physics.mdc` |
| artist | bark/leaf placeholder; physics sizes stay | `.cursor/tasks/ART-TREE.md` |
| artist | dog parts in `Art/Dog` (chest…paw); sizes unchanged | `.cursor/tasks/ART-DOG.md` |
| artist | ART-DOG **box look is live**: `DogArt.BoxLook` draws the dog the way `HumanSegment` draws the human — one flat translucent rectangle per bone (head, neck, chest, pelvis, 5 tail, 4 × upper/lower/paw), one dark joint marker, far side dimmed; `-dogCoatLook` rolls back to the plates, `-dogBoxAlpha <f>` tunes the fill (0.5, the human's `Human.cs` alpha; was 0.65 in an earlier pass), `visualSideOffset` 0 like the human with `-dogSideOffset <f>` for the A/B (`HipBridge` / `ShoulderBridge` cover the two joints whose anchor sits off the box edge). `Art/Dog/Palette.cs`, `Dog.cs`. Frames: `Trials/Runs/dog_art1*.png` | `.cursor/agents/artist.md` |
| artist | crow / chicken parts in `Art/Crow` and `Art/Chicken` | `.cursor/tasks/ART-BIRD.md` |
| ai | Insect hunt concept: faction Insects, Forage→Pursue, eat with beak | `BIRD-13-insect-hunt.md` |
| dog | DOG-3 leftover sit-trot **0.286 km/h** (`dog_w10`); 5 km/h open. DOG-4: `DogDrive` writes Walk; no play keys | `.cursor/tasks/DOG-3-walk.md`; `DOG-4-ai.md` |

## Walk front — WALK-2 (Hermes 16.09)

Yury 15.09: **vestibular first, not swing lift** — still the line.

**WALK-1 instrumentation cycle closed 18.09 (walk worker, build `33e423c3` == manifest,
run `walk1_own1`)**: `check-owners.py` and `walk-nan-audit.py` now report OK - every leg
joint on every row of the walk window has a finite request and a named owner, and the
pre-clamp demand is a recorded column. Physics unchanged (bit-identical to `walk1_tap4`,
fell 6.37 s, 2 swaps). The answer to the cycle's question: **the hips are (b) - the demand
itself is wrong** (in 5.00-5.60 s, where the collapse is created, the hip target tracks the
measured angle within 1.8° and asks 17-24 % of the muscle, 0/121 frames at the ceiling;
after the 5.915 s handover the request is an unreachable 35° against a measured hip
swinging -38 → +45°, demand 3.5-15x, pinned at ±100 N·m and flipping sign inside 100 ms;
in the last 12 walker frames the left hip asks 4.08x while the right asks 0.19x).
**The ankle is (a) - its ceiling binds** (full 22 N·m dorsiflexor in 19/121 frames of
5.00-5.60 against a 126 N·m gravity moment), but its raw demand is 5-69x the ceiling and
after 5.915 s the loop is switched off entirely. The ±100/±130 saturation the earlier
cycle reported is GET-UP saturation, not walk saturation. Details, tables and scripts:
`Trials/Runs/walk1_own1-report.md`.

**Two controllers, one of them dead (Hermes 17.09).** Measured in the window before the fall: the default path (WalkLift / Stance clock) never lifts a foot — single support 0.0 %, both feet grounded 100 %, first single support never. The capture walker (`-captureWalk 1`) does step: single support 34 %, both feet 62 %, first single support at 5.22 s, 2-3 swaps, fall at 5.8 s. Fresh data (Hermes 17.09, Yury's eye check): at the walk start kneeL 6.5 / kneeR 3.4 deg, by 5.6 s BOTH knees reach 120 deg, the pelvis drops 0.456 m and the body topples forward. The launch squats both legs instead of lifting one: the swing knee must flex, the stance knee must stay extended, pelvis drop under 3 cm. So `BalanceControllerCaptureWalk.cs` is the canonical walk now; the WalkLift families own the dead path and must not be tuned. The hourly trial runs `-captureWalk 1`.

Measured on build `c125adbd` (`burn_walk`, 40 s, walk starts at 5 s):

- During the walk phase both feet stay grounded **100 %**, single support
  **0 %**, `walkSwap` **0**, `comX` moves **9 mm**, knees stay 3–7° — then the
  body folds.
- Moving the walk start from 5 s to 12 s moves the fold from 6.305 s to
  13.335 s: the fold follows the walk start by **1.3 s**. The first transfer
  fails; the gait never runs.
- Quiet stance in the same build: 40 s, 2.1° tilt — the support loop is healthy.
- Variants tried: default, `-walkStance 8 -walkTransfer 8 -walkFirst right`,
  `-captureWalk 1` — all fold 1.3 s after the start, 0–1 swaps.
- Yury watched the video: "the step is weak". Measured: there is no step.

Hard rules for the walk scope:

1. **The switch surface is frozen.** 250 `-walk*` switches exist, 61 are read
   at most once, and 46 are synonyms of two ideas (`-walkPlantFallFoldGrow*`
   15, `-walkPlantFallLiftCapGrow*` 16). Never add a switch: reuse an existing
   one, or delete a dead sibling.
2. The step must come from the CoM leaving the support area — the CoM-triggered
   capture step — not from swing-lift or plant-fold timers.
3. Do not tune PD, do not touch the stance loop, do not start SIMBICON.
4. Unity 6 API: `GetInstanceID` is obsolete and stops the build (CS0619). Cache a `Transform` reference and compare it instead.
5. One writer only: the walk slot edits the physics files (`StepPhaseDriver.cs`, `BalanceController.cs`, `MotionIntent.cs`, `PlayerInputSource.cs`, `Human.cs`, `HumanSegment.cs`, `JointFriction.cs`). Other slots stay out of them.

Acceptance: 40 s no fall, `walkSwap` ≥ 6, `comTravelX` > 3 m, single support
above 20 % of the walk phase, swing foot grounded fraction < 0.35, left/right
step length within 15 %, stance still RMS ≤ 0.0055 and `pf23v2` +23 N·s holds.

## Closed — do not open

Forces in `Update`, 100/50 Hz in prod, merge pelvis+torso, amputate arms,
SIMBICON, retune PD for FPS. PERF-2: keep 200 Hz (`perf_wave2_timestep.json`).
Wood `Rigidbody2D` / wood collider on `TreeRig.Static` (PLANT-2).
`enabled = false` on driven `BirdSensors` / `BirdFlight` (PERF-5, 0%).

## Automations

Cloud slot `Human GPU 10%` **cannot** do that measurement: VM has no
checkout, no Unity, no GPU. The run writes "GPU percent cannot be measured".
More actions has only Duplicate / Copy as JSON / Delete — no off switch.
While Active, the ~13:58 GMT+3 slot burns another empty hour.

Working path is local: `Tools/run-gpu10-hourly.ps1` (task `Unity-GPU10-hourly`).
Lab already exists: `Trials/Runs/gpu10_lab.json`, driver GPU max **7%**,
avg **1.1%**, 10% threshold not crossed. Frame note for colleagues:
`Trials/Runs/perf_wave1_lab.md`. Do not open lights/bloom. Do not
touch PD or 200 Hz.

Documents: scheduler `Unity-Docs-daily` every day at **02:00**, script
`Tools/run-docs-daily.ps1`. Cloud cannot see this folder. Report:
`Trials/Runs/docs_daily_latest.md`.

## Dispatch

Telegram: Hermes manages the game. Generic recon is CLI `-Ask`. Named
work is **`-Profile physics`**: the wrapper **injects**
`.cursor/agents/physics.md`. On this PC headless `--print` has **no
Task** (Hermes 2026-09-06, 70K log). Label `generic+physics.md`, not
“physics Task”. This IDE chat is the **internal admin**.

## Roster

`.cursor/ROSTER.md` and `.cursor/agents/` (`physics`, `visual`, `level`,
`bird`, `plant`, `dog`, `optimization`, `ai`, `docs`). The five-model
college is not these profiles.
5. Unique local names: CS0136 shadowing blocked the build three times tonight (BirdTrialRecorder, PlantTree). Never reuse a local name inside a method.

6. Commit a checkpoint before a big change and never leave the tree uncompilable: the repo had no commit since 04.09 while BalanceController gained 12k lines, so a regression could not be traced or undone.

## Get up from a fall - Yury 17.09

The human has five states, and the trial must tell them apart:

1. Standing. 2. Walking. 3. Falling. 4. Fallen. 5. Getting up.

Rule: while the human is Fallen and the walk key is held, he must try
to get back on his feet. The walk input is not ignored on the ground.

Measured 17.09: after the fall at 8.0 s the body is frozen - torsoTilt
-70.4 deg, comX 0.970 m, state 1 - until the trial ends, even though the
walk window stays open to 40 s. There is no attempt at all.

Acceptance for the get-up case: the trial runs walk 1, walkTime 5,
walkDuration 35. The human falls, and while the walk key is still held
he must come back to his feet: torso tilt above -20 deg and CoM above
the standing threshold within 10 s of the fall, then walk again for 3 s.
The recorder must report getUpAttempts, getUpSuccess and seconds from
fall to stand.


## Arms are part of the vestibular apparatus - Yury 17.09

The arms are not decoration. They counter the legs while walking, they reach
when a fall is caught, and they push off the ground while getting up.
Measured 17.09: the walk window shoulder range is 42 deg but the counter phase
is weak, correlation -0.22 with the left hip and +0.55 with the right; after
the fall the arms are dead, shoulder 4.8 deg and elbow 0.03 deg over 33 s.
Acceptance: walking shoulder correlation below -0.5 with the opposite leg;
while Getting up the elbow range above 20 deg and a non zero arm command;
both must be visible in the video, not only in the numbers.

## Level 2 authorised - Yury 18.09

Yury allows more substantial changes to the human physics and a move to another
level of correction. Assistant decision: level 1 first, the pelvis support for
the walk. If level 1 stays blocked, move to pose driven control: a pose library
and PD joint torques, the balance layer only placing the feet by the capture
point and holding the hip height. Behind a new switch, old path kept for rollback.

## Get up mechanics corrected - Yury 18.09

The knees press to the torso (knee flexion at least 90 deg, thigh to the chest),
then the arms push off the ground: the hands press, the elbows extend and the
arms lift the body. Then the feet come under the centre of mass and the legs
stand him up. Without the knee tuck and the arm push it does not count, even if
he ends up standing. The hand colliders exist, so the push is physically
possible, it is simply never driven.

## Swing leg must fold - Yury 18.09

From a drawing of a walker: in the swing the leg folds under the body, the knee
bends 60 to 80 deg while the foot is off the ground, clearance 30 mm or more, the
foot passes under the pelvis instead of trailing behind, and the knee extends to
under 20 deg before landing. The stance leg stays extended and carries the weight.

## Dog has two modes - Yury 18.09

Walk first: 5 km/h = 1.389 m/s, trot with diagonal pairs and no suspension phase.
Then run as a SEPARATE gait: 15 km/h = 4.167 m/s, gallop or bound with a
suspension phase, stride 2.5 to 3.5 Hz, front and hind limbs moving as two units.
A separate mode switch, never a throttle: the two gaits stay intact side by side.

## Dog walk checkpoint (programmer slot, 18.09 ~09:55)

Checkpoint before dog_ds3. Change: the stance paw plant is wired
(`WalkStancePlantCommand` → `WalkPlantCommand`, dead code since DOG-3).
Baseline measured on this build, `dog_ds3_base`: `comSpeedX` 0.073 m/s
(0.264 km/h), `comTravelX` 1.762 m, `walkCycleCount` 65, `trunkDrop` 0.351,
`fourFeet` 0.084, `fell` false, 25 s.

## Dog lies 63 pct of the run and never tries to stand - measured 18.09

dog_gu5: dogDownSeconds 25.14 of 40 s, dogGetUpAttempts 0, getUpSuccess false while
the dog state machine claims true at 4.58 s. The quiet run dog_gu6 is clean (0 s down,
no false positives), so the recorder is trustworthy now.

## Dog get-up flag fixed - physical stand test, 18.09 15:50

`DogStanceController.GetUpIsStanding` read only `AllPawsGrounded() + pitch`, and in a
side view a prone dog has a level trunk too, so the flag was a state name. It now uses
the recorder's rule: the trunk back within `getUpStandDrop` 0.12 m of a measured
standing pose with at least `getUpStandMinPaws` 3 planted (held `getUpPlantSeconds`
0.20 s). The bar is `getUpStandChestY/PelvisY` (`TryCaptureGetUpStandTrunk`): the
highest trunk pose seen with all four paws down and the chest inside the upright band,
both ends from the same frame, never updated during a get-up, never cleared by a fall.
`getUpSuccess` is also cleared in `EnterFallingOrFallen` / `EnterGettingUp`, so a stale
true cannot outlive the stand it described.
Build 0faedd47509af633, label `dog_uf2` (40 s, `-dogWalk 1`): 0 of 3715 down frames
carry `upSuccess` 1 (the old flag was 1 in all of them from 4.58 s); the flip is at
11.84 s with a trunk drop of 0.011 m and 4 paws, at the frame the pitch comes back
-72 -> -12; the lying window 21.6-40 s (pelvis 0.32 m down) stays false. Quiet run
`dog_uf1` clean: `fourFeet` 1.0, `trunkDrop` 0.025, no fall, no flag.
Still open: the dog does not try - `dogGetUpAttempts` 0 (the automaton's own
`getUpAttempts` 2 is entry counting, not a push), so a rise from a physical heap is
still unproven. Next: the tuck-push sequence, then the loaded stance.

## Rule: every collider gets an explicit friction material - Yury 18.09

No object with a collider may rely on the engine default (0.4). Box2D pairs friction as
sqrt(mu1*mu2), so an unset material silently loses grip. Ground 0.4, human sole 1.2,
dog NOTHING (its AddFriction is joint friction, not contact). Rule file:
.cursor/rules/collider-materials.mdc. Measured trap: on the dog, more grip made it
slower (ice 0.339 m/s, stock 0.129, sticky -0.015), so set materials explicit first,
raise mu only after the gait has a loaded push.


## Expert audit 18.09 - the measured transfer window (read before any walk edit)

Verified on build f550de9831e6e2 / guid 1c2e0baa by an independent audit; all
records are in F:/hermes-data/expert-audit.

- 5.2 s swing foot lifts - the claim "the swing knee never bends" is WRONG.
- 5.4 s first role swap counted, both knee activations high.
- 5.6-5.8 s BOTH contact flags read airborne, pelvis rises first.
- 6.0 s both feet grounded, knees ~103/~69 deg, pelvis dropping.
- Independent detector: collapse onset 6.035 s. JSON `fell` only at 8.4151 s.
  The fall flag LIES - never quote it as acceptance on its own.
- 8 swaps counted, 6 after the collapse began. The swap counter is a timer/contact
  proxy, NOT completed steps.

Order of work from here: instrument (requested vs applied torque, clearance, load,
owner per joint, swap reason) with FROZEN gains -> quiet stand -> single support on
each leg -> ONE transfer with lift, placement, landing, load -> then sustained walk.
No tuning while instrumentation is missing.

REFUTED, do not retry: flipping the pose controller's height-damper sign. Measured
default 8.4151 s, zero damping 5.135 s, reversed 1.225 s (fails before the walk
starts). Also: the arm-swing parameter is NOT inert (0 vs 60 changed the trace).

Stale numbers are worse than no numbers: check `sourceFingerprint` in the JSON
against the build manifest before quoting anything, and never test a build older
than the sources.


## Walk instrumentation results - verified by independent CSV parse 18.09 (run walk1_tap4)

Run: Trials/Runs/walk1_tap4, fingerprint 6a1aeb82a547bfed == Build/Headless/build-manifest.json.
Check any run mechanically with: python F:/hermes-data/scripts/check-owners.py <label>
(it exits 1 with the offending columns and frame counts, 0 when every joint is owned and finite).

- The ankle IS driven: appTorqueAnkleL/R are finite in every walk-window frame. But the
  REQUEST is not recorded - reqAnkleL NaN in 6927 frames (from 5.205 s), reqAnkleR NaN in
  6978 - and ownerAnkleL reads 0 in 7845 of 8001 frames; copCmdL is NaN in 236 of 321
  walk-window frames. So: not "nobody commands support" (the earlier reading was wrong),
  but control runs with no recorded author.
- The demand SATURATES: appTorqueKneeL hits exactly +130/-130 N*m, hipL/R exactly +-100 N*m
  in 5.0-6.6 s while the body sinks.
- The body pitches forward while BOTH feet are still grounded: torsoTilt -25.8 deg at
  5.60 s, -74.6 deg at 5.96 s, walkSwap frozen at 2.
- Three builds (efc9d7f7, 0f59d4c7, 6a1aeb82) all returned fell 6.37 s / 2 swaps: the
  recent edits did not reach the behaviour. Treat an unmoved number after a landed edit as
  "the path is not executed", not as "the fix did not help".

Next cycle: complete the recorder (finite request and a named owner for every joint in the
walk window), then answer ONE question with that evidence - is the ceiling too small, or
is the demand itself wrong? No gain tuning until that is answered.


## ARENA-1 - parameter sweep arena (Yury's new order 18.09, additive)

A separate scene with ONLY ground: a human spawns every 500 ms, each with a different genome
of the pose-drive walk parameters, its number on its head, walking at once; 15 s after each
spawn its distance in metres is recorded from its own spawn x; the parameters of the further
walkers appear more often later (0.75 of spawns copy a parent from the best quartile, then
jitter, with the parent id and mutation seed logged); 30 s after spawn the human is destroyed
so RAM stays bounded (about 60 alive). Upright/fallen comes from the physical criterion, never
from the `fell` flag. Card: .cursor/tasks/ARENA-1-param-sweep.md. This does NOT cancel the
walk, dog, tree or apple work.

## Walk recorder complete + the demanded-vs-applied answer (walk worker, 18.09, run walk1_own1)

Full report: `Trials/Runs/walk1_own1-report.md`. Run `walk1_own1`, fingerprint
`33e423c31515fcf27aa9657ba5357f01421d5310d183136d7a881461cf6de789` ==
`Build/Headless/build-manifest.json`, buildGuid `6ea0e9d9...` == manifest.
Gains/ceilings/limits/masses/friction UNCHANGED: physics columns are bit-identical to
`walk1_tap4` (only `tapAge`, a real-frame counter, differs), `fell True`,
`survivedSeconds 6.37`, `walkSwapCount 2.0`, `comTravelX 2.0002` - same as before.

Recorder (recorder-only, nothing feeds the control math): every leg joint on every row
now has a finite request and a named owner - walker code while it wrote the joint, else
the layer that held it (16 StanceLayer, 17 StandingPlant, 18 GetUp, 19 Falling,
20 PoseDrive, 21 CaptureWalker, 15 IDLE = nobody). A joint commanded as a TORQUE has no
angle request: the cell holds the MEASURED angle and `reqFallbackMask` flags it. New
append-only columns: `legLayer`, `reqFallbackMask`, `demandHipL/R`, `demandKneeL/R`,
`demandAnkleL/R` (121 -> 145); `demand*` is the PRE-CLAMP demand in units of the muscle
ceiling. `check-owners.py walk1_own1` -> OK, `walk-nan-audit.py walk1_own1` -> OK
(`walk1_tap4` fails both: reqAnkleL NaN in 246 window frames, ownerAnkleL zero in 165).

The answer (5.0-6.6 s, owner split: walker 5.00-5.915, GetUp 5.915-6.60):

- **(b) the demand itself is wrong - the HIPS.** In 5.00-5.60 s, while the pelvis sinks
  80 mm and the trunk pitches to -25.8 deg, the hip target TRACKS the measured hip
  (median error 1.8 deg) and asks only 17-24 % of the 100 N*m muscle; 0/121 frames touch
  the ceiling. An infinite ceiling on a target equal to the current angle still yields no
  corrective torque - the binding limit is the controller's own loaded-hip cap (45/130
  N*m) plus the target law. Once the walker hands over, the request is an unreachable
  posture (35 deg constant against a measured hip swinging -38 -> +45 deg) with a demand
  of 3.5-15x the ceiling, applied pinned at +-100 N*m in 67-71 % of frames and flipping
  full scale inside 100 ms. In the last 12 walker frames the left hip asks 4.08x (408
  N*m) and sits on -100 N*m in all 12, while the right hip asks 0.19x (19 N*m) and never
  approaches the ceiling: a 21x left/right asymmetry, so the demand is not load-derived.
- **(a) the ceiling is too small - the ANKLE only.** The loop asks dorsiflexion
  (`copCmd < -0.5` in 41/121 frames of 5.00-5.60) to hold a CoM 0.077-0.184 m ahead of
  the origin and applies exactly the full 22 N*m dorsiflexor in 19/121 frames - never 65.
  The gravity moment there is m*g*comX = 70*9.81*0.184 = 126 N*m, i.e. 5.7x its muscle.
  Caveats: its raw demand is 5-69x the ceiling (a saturated bang-bang, magnitude carries
  no information), and after 5.915 s the loop is switched OFF entirely (0 demand, <= 4.5
  N*m applied while the trunk pitches another 40 cm). Once the CoM is past the toe no
  ankle torque can hold the body, so raising this ceiling alone cannot restore the
  support.
- The saturation quoted by the earlier cycle is GET-UP saturation, not walk saturation: in
  the walker phase the hip touches 100 N*m in 13 frames of one third of a second and the
  knee never reaches 130; the +-100/+-130 pins belong to the layer that takes the legs
  after the walker has surrendered (locomotion Walking -> GettingUp at 5.915 s).

**Next cycle: the targets/reference, not the ceilings.** The hips - the only joint with
the torque to arrest a forward pitch - are never asked for the load in the window where
the failure is created, and the layer that inherits them commands an unreachable pose and
oscillates at full scale. The undersized ankle dorsiflexor (22 vs 65 N*m) is a real but
separate defect, and the ankle loop's gate must be fixed before its ceiling matters.


## Delivery rule for the ARENA scene (Yury 18.09)

Yury checks the arena scene in Unity HIMSELF. When Assets/Scenes/WalkArena.unity is done:
1. verify it before announcing - the scene file exists, it is in the build scene list, the
   spawner runs, the head numbers are readable in a frame, and a fresh run produced the
   arena JSON + the append-only parameter log;
2. then tell him it is ready, with the exact scene path and what to click (open the scene,
   press Play), plus the run label and the video from the same build;
3. never announce "ready" from the worker's own summary - announce it from the verified
   artefacts.
While his editor is open the compile gate SKIPS (a live editor holds the project lock), so
automatic rebuilds pause until he closes it - that is expected, not a failure. Do not open,
close or kill his editor.


## Profile gates (18.09.2026, by Yury)

- `h_2dgame_opus` exists for expensive work but **starts ONLY on Yury's explicit approval**,
  per task. It is deliberately NOT wired into any cron, stream, lane or script. Use it for
  hard problems Flash failed twice and for physics help - not for routine edits.
- Vegetation has ONE owner: `h_2dgame_plant` (tree + apple). `h_2dgame_apple` is retired.
- Unity executor is `h_2dgame_unity` (renamed from `programmer`); `h_rev_*` stays Revit.
## Dog get-up: he tries and rises - tuck/push cycle, 18.09 (dog worker, build a6092d68)

Job order from the brief: (1) fix the success flag, (2) make him try. The flag fix
(commit e909399d, build 0faedd47) is in the current build and re-verified here, so this
cycle's ONE change is item 2.

Root cause measured (label `dog_gu12`, build 33e423c3, fingerprint == manifest, 40 s
`-dogWalk 1 -dogWalkTime 1`): from 21.4 s the dog lay with the pelvis 0.33 m down, pitch
+49, ALL FOUR PAWS OFF THE FLOOR, in `GettingUp` to the end of the run. `DriveGetUp`
gated its pike on `anyPaw`: with nothing grounded it held the quiet base pose, so the
body never moved (`dogGetUpAttempts` 0 in 3715 down frames). The tuck is the movement
that PUTS the paws on the floor, so it must be allowed to start without one.

Change (`DogStanceController.DriveGetUp`):
- the tuck latches on a HEAP, not on contact: trunk at least `getUpConnectDrop` 0.20 m
  below the measured stand bar (`GetUpTrunkDropBar`, the same pose `GetUpIsStanding`
  measures against), or |pitch| >= `getUpSquatAbsPitch`;
- the one-shot pike became a TUCK to PUSH cycle: `getUpTuckSeconds` 0.45 s folded under
  the trunk, then `getUpPushSeconds` 0.35 s extending against the floor, repeating while
  the trunk is still a heap. The old latch released only when |pitch| fell under the
  squat, which a pelvis-down heap never reaches - it froze in one pose;
- a push needs a floor: it is entered only with a paw planted and drops back to the tuck
  without one (measured `dog_gu13`: 16 pushes with 0 paws each spun the trunk to pitch
  +-180 and left the dog on its back for the last 19 s);
- recorder witness (read-only): CSV tail `hipL,hipR,kneeL,kneeR,shoL,shoR,elbL,elbR,
  trunkBar,push`, JSON `getUpTrunkBarFinal/Max`, `getUpHeapSamples`, `getUpPushSamples`.

Measured (40 s, walk from 1 s; `dog_gu12` is the same protocol on the previous build):

| label | build | down s | attempts | up success | getUp s | fourFeet | trunkDrop | m/s |
|---|---|---|---|---|---|---|---|---|
| dog_gu12 | 33e423c3 | 18.58 | 0 | false | - | 0.061 | 0.351 | 0.047 |
| dog_gu13 | 0af0550f | 17.36 | 13 | true | 7.54 | 0.055 | 0.414 | 0.023 |
| dog_gu14 | a6092d68 | 8.39 | 9 | true | 6.58 | 0.089 | 0.418 | 0.123 |

Flag proof (item 1): 0 of 3473 (`dog_gu13`) and 0 of 1679 (`dog_gu14`) down frames carry
`upSuccess` 1; the flip is physical - `dog_gu14` `getUpSuccessTime` 16.315 s,
`getUpSuccessTrunkDrop` 0.0092 m, `getUpSuccessPaws` 4. Quiet regression `dog_gu15`
(same build, no walk): fourFeet 1.0, trunkDrop 0.025, fell false, 40 s.

Still broken: the dog reaches a heap it cannot leave - on its back (pitch +-180, 0 paws)
from 33.6 s in `dog_gu14` and 20.6 s in `dog_gu13`, to the end of the run, so item 2's
run-level bar (fourFeet > 0.5, trunkDrop < 0.06) is NOT met. The walk is untouched:
comSpeedX 0.123 m/s = 0.444 km/h against 1.389 (5 km/h). Next: right the body from a
back-lying heap, then the loaded stance push (item 3). Video: `Trials/Runs/dog_gu14.gif`.

## Dog get-up: inverted heap (on its back) - the tuck cannot right it, 18.09 (dog worker, builds 3c363645 / 94dd356b)

Job order from the brief: item 1 (the flag) is in the tree and re-verified here (the
flip is physical: `upSuccess` at 16.315 / 22.774 / 30.633 s with `trunkBar` 0.015 /
0.036 / 0.027 m and 4 paws; 0 of 2670 down frames carry the flag from the floor), so
this cycle's ONE change is item 2, the last defect left in it: the dog ends the run on
its BACK. `dog_rd1` frame 190 (rendered) is a dog flat on its back, belly up, all four
paws in the air; `dog_gu14`/`dog_rd1`/`dog_rd2` lay inverted 6.00 / 4.03 / 4.49 s with
0 push frames before this change.

Change (`DogStanceController.DriveGetUp`): an `inverted` heap (|pitch| >=
`getUpInvertedAbsPitch` 100 deg, lines 176-185, 2049) bypasses the upright contact-age
gate that threw away every 1-2 frame contact (2055), and gets a righting cycle instead
of the tuck: fold ONE pair while the other reaches straight past its trunk end, swap
every `getUpInvertedSwapSeconds` 0.35 s, and a contact turns the half into the push at
once with the pair that HAS the floor extending (2084-2110, targets 2157-2180 and
2211-2235). Two pairs folding together cancel their own reaction - that is the
measured failure.

Measured (40 s, `-dogWalk 1 -dogWalkTime 1`, fingerprint == manifest):

| label | build | down s | attempts | fourFeet | trunkDrop | m/s | inv s | push inv | push+contact |
|---|---|---|---|---|---|---|---|---|---|
| dog_gu14 | a6092d68 | 8.39 | 9 | 0.089 | 0.418 | 0.123 | 6.00 | 0 | 0 |
| dog_rd1 | 3c363645 | 13.10 | 17 | 0.097 | 0.410 | 0.073 | 4.03 | 0 | 0 |
| dog_rd2 | 94dd356b | 13.35 | 17 | 0.090 | 0.417 | 0.068 | 4.49 | 566 | 77 |

Still broken: the escape from flat-on-back. The push now lands on the floor (0 -> 566
inverted push frames, 77 with a paw down, trunk rate -373 deg/s across those frames)
and the trunk still does not leave the back (ends at pitch 166.8, `trunkBar` 0.402;
net rotation inside the window 83.5 deg in 4.49 s, the same as before). From there the
paws touch only 9 % of the frames, so the righting has to happen BEFORE |pitch| passes
100 deg - catch the trunk in the side-lying band 55-100 deg, where the paws still reach
the floor and the extension really levers it. Walk (item 3) untouched: 0.068 m/s =
0.243 km/h against 1.389 m/s. Video: `Trials/Runs/dog_rd2.gif` / `.mp4`.

## Expensive lanes: opus AND kimi (Yury 18.09)
The gate above applies to BOTH `h_2dgame_opus` and `h_2dgame_kimi`: no cron, no ds-cycle stream,
no script may start them. They run only after Yury approves a specific task, and approval for one
task never carries to the next. Purpose: hard tasks, and help for the physics lanes.

## Dog cycle 1 - instruments only, landed and type-checked, stands NOT run (dog worker, 18.09 ~18:35)

Brief: the 18:20 status (cycle 1 = instrument columns only, no control change, the bit-identical
`dog_art1` triple as the regression detector). Edit is in the tree of build `94dd356b`; no player
was rebuilt and no trial was run - the status reserves the stands for the orchestrator, so the
acceptance numbers are NOT measured yet and are not claimed.

- `DogStanceController`: `SampleCycle1Instruments()` at the end of `Sense()` publishes
  `pawNormalImpulse[4]` (N*s, `ContactPoint2D.normalImpulse` per paw, order FR/FL/RR/RL),
  `pawWeightFraction` = sum(vertical Fn)/(m*g), and `walkStandDropBar` (the controller's own
  `WalkStandDrop()`, m). Read-only: no counter, gate or command touched.
- `DogTrialRecorder`: 8 append-only per-frame CSV columns after `push` (**31 -> 39**) -
  `trunkLift` m, `capturePointMargin` m (+ inside support), `pawNormalImpulseFR/FL/RR/RL` N*s,
  `sumFnOverWeight` (1.0 = the paws carry the weight), `controllerTrunkDrop` m (the controller's
  own bar beside the recorder's independent `trunkDrop`) - plus JSON summary, `metresPerCycle` vs
  `nominalStrideFront/Rear` from the Bind link geometry (`2*L*sin(sweep/2)`), and a
  `cycle1NanCount` that must be 0.
- Verified Unity-free: `bash F:/hermes-data/scripts/plant-csc-check.sh` -> `csc exit 0`, 0 errors.
  Checker: `python C:/Users/Admin/AppData/Local/hermes/scripts/dog-cycle1-check.py <label> ...`
  (already run on the pre-cycle CSVs: reports both as "columns absent", 31 columns).
- Stand commands, details and the acceptance table: `Trials/Runs/dog_cycles.md`, section
  "Cycle 1 - INSTRUMENTS ONLY". Acceptance still open: `dog_art1` bit-identical
  (comTravelX 5.53443003, comSpeedX 0.230613619, 35 cycles), `dog_gu14` comTravelX 4.81013966.
