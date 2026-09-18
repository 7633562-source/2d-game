# Now (door for a new chat)

Read this and `.cursor/rules/`. Do not copy chat history.

**Russian exists only in live chat with Yury.** Everything else is English,
always — existing and new. See `docs-english.mdc`.

## Object

The human is assembled in `Human.cs` (public fields). No prefabs. Scenes:
`SampleScene` (stand, do not touch), `World` (yard: flock 6 / hens 10 / dogs 3; `treeCount = 0`),
`Bird` (one crow + AI), `BirdFlock50` (flock AI), `Chicken` (hens),
`Tree` (plants), `Dog` (quadruped), `Campfire` (fire lab).

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
| physics | WALK-2: the walk never steps (single support 0 %, walkSwap 0, folds 1.3 s after the walk start). Vestibular first; switch surface frozen. | this file, Walk front |
| physics | deep crouch WIP intentionally leaves code defaults 98/28 after `crouch_accept`; accepted journal remains 55/12 until the physics work closes | HumanPhysics continuation |
| artist | code hand is `0.06×0.16`; card still wants `0.19`; walk fold at 0.19 is recorded | `.cursor/tasks/ART-HAND.md` |
| docs | pointers and baselines; do not clean Runs | `DOCS.md` |
| optimization | P1 + P2/P3 + PERF-5 closed. Yard 04.09: off-camera dogs/birds rest (`NpcRest`). PERF-1 is pre-grove and pre-yard. Do not cut 200 Hz | `OPT-3-distant-rest.md`; `opt_agent_proposals.md` |
| level | Test1 yard: campfire at x=32.5 (24 tongues + ParticleSystem sparks/smoke/embers; no sprite smoke boxes); lab scene `Campfire`; gate Static trees + World flock 6 / hens 10 / dogs 3; `treeCount` 0 | no new card |
| bird | BIRD-12: inter-bird contact is torso+head (`BirdCore`); limbs/perch `BirdLimb`. Walk = `walkThrust` on the body. Opt 02.09 flock 50 still the 200 Hz edge; no `GetContacts` | `BIRD-12-core-contact.md`; `opt_bird_code_02.md` |
| plant | PLANT-2 accepted. PLANT-3: 12 garden flowers. Four extra tree kinds in scene Tree (Lilac Hazel Cedar Plum); yard cycle still Oak…Poplar | `PLANT-3-flowers.md`; `plant-physics.mdc` |
| artist | bark/leaf placeholder; physics sizes stay | `.cursor/tasks/ART-TREE.md` |
| artist | dog parts in `Art/Dog` (chest…paw); sizes unchanged | `.cursor/tasks/ART-DOG.md` |
| artist | ART-DOG **box look is live**: `DogArt.BoxLook` draws the dog the way `HumanSegment` draws the human — one flat translucent rectangle per bone (head, neck, chest, pelvis, 5 tail, 4 × upper/lower/paw), one dark joint marker, far side dimmed; `-dogCoatLook` rolls back to the plates, `-dogBoxAlpha <f>` tunes the fill (0.65). `Art/Dog/Palette.cs`. Frames: `Trials/Runs/dog_art1*.png` | `.cursor/agents/artist.md` |
| artist | crow / chicken parts in `Art/Crow` and `Art/Chicken` | `.cursor/tasks/ART-BIRD.md` |
| ai | Insect hunt concept: faction Insects, Forage→Pursue, eat with beak | `BIRD-13-insect-hunt.md` |
| dog | DOG-3 leftover sit-trot **0.286 km/h** (`dog_w10`); 5 km/h open. DOG-4: `DogDrive` writes Walk; no play keys | `.cursor/tasks/DOG-3-walk.md`; `DOG-4-ai.md` |

## Walk front — WALK-2 (Hermes 16.09)

Yury 15.09: **vestibular first, not swing lift** — still the line.

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

## Rule: every collider gets an explicit friction material - Yury 18.09

No object with a collider may rely on the engine default (0.4). Box2D pairs friction as
sqrt(mu1*mu2), so an unset material silently loses grip. Ground 0.4, human sole 1.2,
dog NOTHING (its AddFriction is joint friction, not contact). Rule file:
.cursor/rules/collider-materials.mdc. Measured trap: on the dog, more grip made it
slower (ice 0.339 m/s, stock 0.129, sticky -0.015), so set materials explicit first,
raise mu only after the gait has a loaded push.
