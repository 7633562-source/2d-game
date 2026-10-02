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

## WALK — THE RUNWAY (cycle 56, 23.09 10:53) — the trial's own floor WAS the edge; the fix is NEW floor beyond it

Yury's complaint ("замля слишком мала... люди уже доходят до края") was the HEADLESS TRIAL's own
20 m ground — 10 m of runway ahead of the spawn — and it is CODE. **A ground-box RESIZE moves
recorded rows** (measured: the plain ground 20 -> 200 m, build `feca3719`, made the #92 row
`f2603b2e` ≠ `3f5625b8`, first difference frame 3319 = t 16.600 s, the POST-FALL phase; the gait and
the whole stance `a7e18eb9` stayed byte-identical), so the runway landed as **NEW floor ADDED beyond
the old edge** (`HeadlessTrial.cs` `BuildRunwayFields`: two dirt fields x ∈ [10, 100] and [−100, −10],
same layer/material/slab/grass, built LAST; the old 20 m box, `-groundFriction` and the spawn
geometry unmoved; the dog/bird/tree/apple/flower/impact trials keep their 20 m — the dog's own row
`bc7a1625` is byte-identical across the old build, the dog lane's own pre and this round's control).
The fence holds on build `5548d8c3` (`fa008fe8`): `c6w56pre_walk` = `3f5625b8`,
`c6w56pre_stance` = `a7e18eb9`, `w56_dog_ctl` = `bc7a1625`. THE NEW TRUTH: the accepted line
(gene531's 13 flags) on the runway = `c6w56_gene531_walk.csv` md5
`744f0b4577df135acdc8e5d7dcef4463` — **15.3435 m, 51 plants, 45 swaps, survived 40.0049 s, `fell`
False** (the record row: 10.31 m of gait at 28.43 s then 660 m of gravity), crossing x = +10 m at
t 28.060 s (the same frame 5611), pace 0.4511 m/s, max tilt 5.45°. THE KEEP, byte-kept:
`c6w54_gene531_walk.csv` `0a4e26bc` (the accepted row of record — the world changed, the line did
not), `c6w52_gene92_walk.csv` `3f5625b8` (previous package), `c6w52_gene92_stance.csv` `a7e18eb9`,
`c6w38n_walk.csv` `d76e2943`; plants `257aef4197b01803eb6def82001e8b5d`. The next stand's `pre`
EXPECT stays `0a4e26bc` + the 13 gene flags (the world's floor is not a row). Report
`Trials/Runs/walk-cycle56-the-runway.md`; clip `Trials/Runs/c6w56_gene531_cross.mp4` (25 fps, the
crossing + the continuation) with the stills `w56_edge_new_t30/t39.png` against
`w56_edge_old_t30.png` (the same line in free fall, before). RULE FOR LATER ROUNDS: extend the world
by ADDING floor, never by resizing an existing collider (the 2D clip's float rounding moves rows).
OPEN (flagged, not patched): the compile gate's `build.lock` has no stale-takeover path — a gate run
died mid-build at 10:26 and every later pass skipped on it (removed by hand this round; infra is
H_Manager's file). The c54 items stay open: the arena's fitness compares the fall's ABSOLUTE time
against the 15 s window (`fellInside` in `ArenaHumanAgent`), the age fix is the next arena round's
first move. The c54 landing's own record: the accepted package = the accepted line + the 13 arena
genes of `arena_fix2` genome 531, every bar of the battery passing, pace 0.44 m/s and `wSup` 693.8 N
stated as flags.

## Open

| Who | Task | Card |
|---|---|---|
| physics | **VEST-2**: per-foot quiet ankle coded (`quietAnklePerFoot` 1). Next: `pf23v2` +23 N·s. | `.cursor/tasks/VEST-2-balance.md` |
| physics | ONELEG-1 accepted (Yury): `oneleg_sym` / `oneleg_sym_l`, both 30 s, span 0.75° / 0.79°. | `.cursor/tasks/ONELEG-1-physics.md` |
| physics | ONELEG-2 **wall**: 90° pose holds; CoM ankle loop is bistable. Do not reopen. | `.cursor/tasks/ONELEG-2-march.md` |
| physics | WALK-2 closed 18.09 (`walk1_load4`, fp a53d483f): recorder complete — `check-owners.py` OK, 160 cols, no NaN, every joint owned; demanded-vs-applied answered: **hips = (b)** (applied ±100 in 190/274 frames at 69 % while the demand is ≤21 % of the ceiling and the angle target is pinned at +35.0°), **ankle = (b)** (extensor −65 pegged with a −10 N·m ask; the only real overshoot is the flexor demand 68.96 vs 22 N·m = 3.13x). One criterion for the project in `Assets/FallCriterion.cs` (human/dog/arena routed, local rules deleted) + a measured load meter (fzL/fzR/xCopMeas/lambda/dcm). Next after cycle 6k: **the solver is clean** (`captureSwingSolvedResidualX` exactly 0.0000 m on all 446 walk frames) and the 0.457 m lives in the DELIVERY - the solve frame is the PLAN frame (-3 deg) while the measured trunk is at -22 to -34 deg; see `Trials/Runs/walk-cycle6l-accept.md` (6l measured: the frame owns the plant gap - 0.5015 m of the 0.457 m, `SolveFrameAngle` -3.01 vs applied -37.82 deg, clamps unsaturated at 0.0024, tracking 0.0448 m), next brief `F:/hermes-data/ds-walk-cycle6m.md`. **6m closed the frame defect in substance, 6n on the letter** (`Trials/Runs/walk-cycle6m-accept.md`, `walk-cycle6n-accept.md`; 6n runs `c6n_walk`/`c6n_stance` on fp `917a1c8b`): `|solvedSoleX - appliedSoleX|` at the plant **0.0000 m on all 30 rows** (6m 0.0154 mean with 3 rows over the 0.05 m bar, 6l 0.5015 flat), residual exactly 0.0000, and `captureSwingAskY` + `captureSwingHipDemandDeg` published (473 non-zero rows, 0 sentinel, no NaN; 190->199 columns). **The walk row did not move**: fell 7.425 s, 3 swaps, 0.9277 m at the fall (WALK-2 wants swaps >= 6 and travel > 3 m at the fall), single support 71.4 %; rule zero holds (`c6n_stance` vs `c6m`/`c6l`: 232 keys, 9 differing, none physics). The next owner is measured at the STEP TARGET: `capturePlanTargetX` 0.5303 -> 0.7839 m at t 7.150 where the measured tilt is -37.6 deg, `pelvisTilt` -3.3 -> -58.2 deg, `planValid` 0 on 111 of 521 walk rows, `ctrlFallState` 1/2 on 203 of 521 from 6.150 s. Next brief `F:/hermes-data/ds-walk-cycle6o.md` (the step target's frame, or the unowned pitch if the target is already measured). **6o (19.09, fp `39a689fd`, runs `c6o_walk`/`c6o_stance`) made the ask's frame the measured one at the handover (freeze -57.10 vs a -58.73 deg body) and the row got one step fewer (2 swaps, fell 7.650 s, 1.072 m at the fall). 6p (20.09, fp `da8fa193` / guid `6ff2325c`, runs `c6p_walk`/`c6p_stance`/`c6p_squat`/`c6p_stance_reg`, report `Trials/Runs/walk-cycle6p-accept.md`) spent the posture error on the swing hip's command but its bound is the loaded-hip band - `captureSwingPostureBiasDeg` pegged at 3.0000 deg against a 20-35 deg drift - so the row is unchanged (fell 7.650 s, 2 swaps, 1.0135 m) while the split `pelvisTilt - capturePlanFrameTiltDeg` at t 7.00 fell to -0.57 deg (6o -35.89) and the plant held inside its bar. The failure is now located in the 0.46 m between `capturePlanTargetX` 0.64 m and the delivered `captureSwingAskX`/`captureSwingSoleX` 0.18 m with `fzL` 0 N from 6.95 s. **LEVEL 2 DECIDED (Hermes 20.09 03:00, from this document's own authorisation): level 1 is measured blocked after 6e..6p, brief `F:/hermes-data/ds-walk-cycle6q.md` starts the pose-driven path (pose library + per-joint PD, balance layer reduced to foot placement by the capture point and hip height) behind ONE switch, default OFF, capture walker kept as rollback.** Then targets/reference, never the ceilings. Arena `fallen` still 61/61 — its baseline window sits inside the spawn settle. | `Trials/Runs/walk1_load4-report.md`, `Assets/FallCriterion.cs` |
| physics | deep crouch WIP intentionally leaves code defaults 98/28 after `crouch_accept`; accepted journal remains 55/12 until the physics work closes | HumanPhysics continuation |
| artist | code hand is `0.06×0.16`; card still wants `0.19`; walk fold at 0.19 is recorded | `.cursor/tasks/ART-HAND.md` |
| artist | **TEXTURE RULE clarified 20.09 + gallery dispatch 21.09** (Hermes; H_Manager on Yury's word): the showcase is the gallery + the 10 species scenes — no code narrowing; campfire and ground/grass stay as they are (all four verdict points answered in `F:/hermes-data/manager/inbox/artrule-texture-rule/verdict.md`; rule recorded in `ART-1-gallery.md`). Next: close the gallery's six MISSING-ART tiles to "all objects with real art", dog first — the creatures row drawn as real part boxes like the human row. Brief `F:/hermes-data/ds-art-task.md`. | `.cursor/tasks/ART-1-gallery.md` |
| artist | **ART-0 29th pass 20.09 (tree leaf cards)**: the oak's leaf parts stop being crops of the `leaf` material. The rig's leaf parts are 0.002-0.5 m (`ART-1A-CHECK INFO oak-leaf`) and the ones the eye can see are 0.02-0.12 m, where a crop of the 0.5 m tile is a 41 px window of a field whose own leaves are 0.068-0.127 m long — `Tools/art0-leaf-window-probe.py`: 87 % ink coverage, a CUT leaf on 100 % of the window borders, i.e. a green plate edged by the part's own rectangle (three vision reads of `art0leaf28_tree.png`: "flat rectangular plates, hard stepped edges"; "flat green chips ... do not read as oak leaves"). Now a leaf part of 0.02-0.12 m draws the painted `Art/Tree/leaf_card.png` (128x128 RGBA ppu 1024, ink 0.329, one ovate leaf in the level's own tone ladder, midrib + 5 veins at partial alpha, transparent margin), `Sliced` in the part's own box; a part >= 0.12 m keeps the material (a window holding 9+ leaves IS the mass a crown is made of). One square canvas because the rig's leaf boxes are square at the median (0.75/1.00/1.00 quartiles in both bands a card is drawn in). `ArtParts.TreeLeafCardKey/Min/Max` + one branch in `DressAll`, tree family only. Gates: `art1-gate.sh` `failures=0` with 6 new assertions in `CheckTreeLeafCards` (`cards=268 mass=1173 wrongCanvas=0`, no localScale moved, no body added, `real=false` puts all 1829 renderers back), `art0-import-gate.sh` failures 0, `art0-meta-audit.py` `problems=0` (`OK Art/Tree/leaf_card.png 128x128 ppu=1024 readable=1 wrap=1/1 physShape=0`). Frame `art0leaf29`: tree 23658 px (1.14 %, box x 589-1320 y 31-253), apple 2305 px (0.11 %), human/fire/flower/flowerbox/level/materials byte-identical, so no physics number can have moved; oak ink 103527 -> 94257 px (the card inks 33 % of its box) and trunk rows 300-1000 0.0 % changed. 4 blind pairwise reads for the new bytes (seed 300 swapped the sides): "separate oval leaves with veins and gaps" vs "solid plates with stepped edges"; absolute read of the new still: crown reads as foliage, NO hard rectangles or repeating planks in it. **Still OPEN, measured, not guessed**: the green chips ON the trunk are NOT the leaf cards — the whole frame difference is confined to y 31-253 and the trunk rows are 0.0 % changed — so their owner is a leaf-material part at or above 0.12 m overlapping the trunk; next step is an editor probe that NAMES those parts before anything moves. Also recorded: one canvas serves the card aspects 0.33-2.59 (a 2.2 box shows the leaf 2.2x wide), the 1171 leaf parts under 0.02 m keep the material (under 8 px at the closest lens), the trunk's module seams stay open. Report `Trials/Runs/art-0-leaf-card-29th-report.md` | `ART-0-inventory.md` item 6c |
| artist | **ART-0 28th pass 20.09 (leaf material)**: `Art/leaf.png`'s LAYOUT stops repeating — 44 Poisson-scattered leaves -> one leaf per cell of a 7×7 lattice over the 0.5 m tile (`gen-level-textures.py`: `LEAF_CELLS`/`LEAF_JITTER`), so no void is larger than a 7.1 cm cell. The tile's deep void 50.5 -> 66.7 (mean 112), band 99.5 -> 79.4 at a 12.5 cm window, mean colour held `(66.8,135.8,4.5) -> (66.6,136.8,4.5)`; wrap still an ordinary neighbour pair (50.3 rd percentile). 5 blind pairwise reads for the new bytes (3 with the sides swapped), incl. the `tree` crown and the `materials` swatch. Frame diff `art0hand27 -> art0leaf28`: leaf surfaces only (materials/tree/apple/level/photo), `human`/`fire`/`flower`/`flowerbox` identical. Recorded against it: the autocorrelation at the cell period 0.33 -> 0.46 and at the half period 0.079 -> 0.313, the accepted probe's dark-blob count 14 -> 32 — the reads, not the metric, decided. REFUSED with numbers: the other nine human parts' ink anchoring (a zero-sum move of the same slack between two joints). Gates: `art0-import-gate.sh` failures 0, `art1-gate.sh` `ART-1A-CHECK end failures=0` (119 OK). One PNG changed, no `.cs`/`.meta`/collider. Report `Trials/Runs/art-0-leaf-cover-28th-report.md` | `ART-0-inventory.md` item 6c |
| docs | pointers and baselines; do not clean Runs | `DOCS.md` |
| optimization | P1 + P2/P3 + PERF-5 closed. Yard 04.09: off-camera dogs/birds rest (`NpcRest`). PERF-1 is pre-grove and pre-yard. Do not cut 200 Hz | `OPT-3-distant-rest.md`; `opt_agent_proposals.md` |
| level | Test1 yard: campfire at x=32.5 (24 tongues + ParticleSystem sparks/smoke/embers; no sprite smoke boxes); lab scene `Campfire`; gate Static trees + World flock 6 / hens 10 / dogs 3; `treeCount` 0 | no new card |
| level | **CLEARING-1 dispatched 21.09 00:0x** (Hermes, H_Manager order "first real task on scenes/rooms"): the first real game location — `Assets/Scenes/Clearing.unity`, the concept's forest clearing as ONE walkable scene (yard tape + Static grove + campfire + strawberries + flower bed + day/night; no creatures in the first slice; rule zero: no physics files, `treeCount` stays 0 on World/Level1). Card `.cursor/tasks/CLEARING-1.md`, brief `F:/hermes-data/ds-level-task.md`; the level cron is resumed. | `.cursor/tasks/CLEARING-1.md` |
| level | **BALANCE-ARENA-1 DONE 19.09** (level worker; fp `1d86f80e`, buildGuid `5794f0ef`): `Assets/Scenes/BalanceArena.unity` exists (Main Camera + `ImpactArena` host, runtime world like `WalkArena`) and runs in a player (`GalleryPreview.exe -scene BalanceArena` -> its own `impact_play-impacts.json`). One real `Rigidbody2D` per throw (`Assets/ImpactThrower.cs`), per-throw record (`Assets/ImpactRecorder.cs`), harness hooks `-impact 1` + `-impactMass/-impactShape/-impactSpeed/-impactAim/-impactSide/-impactAt/-impactCount/-impactSpacing/-impactSeed/-shotInterval`. Acceptance: `-impactCount 0` (40 s) is the stand **to the last digit** (pelvisDrop 0.0148, rmsShoulder 2.6542, rmsElbow 7.5647, armSwingFraction 0.0, 0 physics diffs vs `burn_stance`); same-seed pair `imp_s1a`/`imp_s1b` bit-identical (CSVs byte-identical); in-plane asserted (`outOfPlaneCount 0`). First throws: 32 N·s at the torso -> down in **0.605 s**, CoM travels 1.98 m, **0 steps taken**; 72 N·s at the head -> 0.565 s; 180 N·s at a leg -> peak fzTotal 4442 N (6.4x weight). **L1: latency = 5 ms (one frame) -> FAILS the card's 40-150 ms**: the joint PD answers the measured joint state with zero proprioceptive delay (raw frames in the report); the reflex delay belongs to the `BalanceController*` stream. Video 1080p: `Trials/Runs/impact_imp_v1_circle_0_25_m_4_kg_8_m_s_1080p.mp4` (24.05 s) + `impact_three_throws.mp4` (54.15 s) + stills. **LABELS 19.09** (level worker; fp `bfcb3a69`, buildGuid `9f9782ea`, runs `imp_v1L`/`imp_v2L`/`imp_v3L`/`imp_v5`): `Assets/ImpactCaption.cs` (new) draws the parameters and the live per-throw banner on the frame - what was thrown, what it hit and when, reference vs delivered impulse, CoM excursion, min support margin, steps after the impact, outcome - plus a cross on the contact point for 1 s. The recorder composes the text it measures and builds the caption itself, so both entry points (the scene and the headless harness) get it and `HeadlessTrial.cs` was not touched; `GalleryLabels.AddPlate` is now public. Rule zero re-verified on this build: `imp_quiet_cap2` (`-nographics`, the card's protocol) is **bit-for-bit** `imp_quiet40` - 719 shared keys, every difference is label/buildGUID/time/fingerprint/`wallPerSim` - so `pelvisDrop 0.0148`, `rmsShoulder 2.6542`, `rmsElbow 7.5647`, `armSwingFraction 0.0`, `sensorTick 8002`, `maxAbsPelvisAccY 14.2838` all unchanged. The banner's final line equals the published rows to the last digit (30.228/1.984/-0.925/0.605 s; 57.567; 171.834/1.28/-0.887/0.480 s) and all eight `throws[0]` columns are identical to `imp_v1`/`imp_v2`/`imp_v3`. Labelled video: `impact_three_throws_labelled.mp4` (54.15 s, 1080p) + `impact_imp_v1L/v2L/v3L_..._1080p.mp4`; the unlabelled cut is kept as `impact_three_throws_pre-labels.mp4`. Report `Trials/Runs/impact-labels-report.md`. Note: only `-nographics` reproduces bit-for-bit, so each video is one take. **GRID 19.09** (level worker; fp `4681f70b`, buildGuid `42e3c16c`, ONE build, seed 1, `-nographics`, 12 runs `grid_<shape>_<mass>_<speed>`, no `.cs` touched): the card's §7 item 2 — the ends of the card's own ranges, never run before (0.5 kg / 15 kg × 4 m/s / 20 m/s × circle 0.25 / square 0.30 / rect 0.60×0.30). Verdicts: **3 of 12 classes stand, 9 go down, and the split IS the sign of the support margin** (STANDS `minSupportMarginM` +0.139, DOWN −0.896…−1.031, `wentOutsideSupport` matching in 12/12, `clairvoyanceFlag` false in 12/12); `stepsAfterImpact` **0 in 12/12**; the survival boundary for a torso hit sits between 2 and 10 N·s (all three shapes); time to fall follows the momentum, not the speed (10 N·s light-fast 1.325-1.340 s, 48-54 N·s heavy-slow 0.445-0.455 s, 236-281 N·s 0.210-0.220 s); delivered fraction 98-106 % at 2-10 N·s, 79-89 % at 60 N·s, 79-94 % at 300 N·s; in-plane everywhere (`maxAbsZMeters` 0, `projectileGravityScale` 0). Rule zero re-verified on this build: `imp_quiet_grid` vs `imp_quiet40` = 700 shared keys, **11 differences, all identity/clock** (label, buildGuid, manifest build guid/time, fingerprint, `wallPerSim`, `wallSeconds`, `commandArgs`), the card's four numbers unchanged (0.0148 / 2.6542 / 7.5647 / 0.0). FIVE instrument findings, none of them physics — the next cycle's single change: (a) **L1 passive AND active latency 5.000 ms in 12/12** = exactly one fixed step, the collision itself crosses both bands, so this metric cannot resolve the card's 40-150 ms window (it measures when the blow ARRIVES); (b) **no active-reaction-off switch** among the 14 impact switches (`-impact…` list), so L3 has no run and the divergence-based L1 cannot be built; (c) `pushedBack` fires at contact+0…+5 ms in 12/12 with the peak fz/W arriving +5…+125 ms, and the above-weight impulse is 3-13 frames in the falling classes vs **468-473** in the standing ones (the stand's own 200 Hz fz oscillation) — the column is not comparable across classes and does not separate collision from push-back; (d) `closingSpeedMps` = `collision.relativeVelocity.magnitude` is **0.059 m/s in `grid_rect_15_4`** against its own 3.940 m/s launch and its 1.271 m/s post-hit track (85 % of its reference impulse delivered) — the caption prints that wrong number, 11 of 12 classes report closing == launch (1.000); (e) the throw row's `fell` is the shared `FallCriterion` alone, the controller's own fall state (available as `ctrlFallState*` in the walk harness) is not wired in. Evidence: `Trials/Runs/impact_grid_ends.mp4` (**54.15 s, 1083 frames, 1920×1080**) + three 24.05 s takes (`impact_imp_g1L_circle_0_25_m_0_5_kg_20_m_s_1080p.mp4`, `imp_g2L_square_15_kg_20`, `imp_g3L_rect_15_kg_4`) + 6 stills, every frame captioned by `ImpactCaption.cs`; `-visual` vs `-nographics` on the same class differs in the last digits only (worst `minSupportMarginM` 1.4 mm). Runners/audits: `Tools/impact-grid.sh`, `impact-grid-video.sh`, `impact-grid-table.py`, `impact-grid-verdict.py`, `impact-pushback-audit.py`, `impact-ground-audit.py`, `impact-closing-audit.py`, `impact-closing-check.py`, `impact-projectile-track.py`, `impact-json-diff.py`, `impact-throw-diff.py`. Report `Trials/Runs/impact-grid-report.md`. **L2 NO-CLAIRVOYANCE 19.09** (level worker; fp `3c4dfc53`, buildGuid `59b8b311`, 5 runs `l2_quiet`/`l2_hit_s1`/`l2_hit_s1b`/`l2_hit_s2`/`l2_hit_s3`, 12 s, `-nographics`, ONE build, no `.cs` touched): the card's §L2 measured instead of assumed. Static: the 17 files of the human's control path (`BalanceController*`, `GaitStateMachine`, `StepPhaseDriver`, `PoseDriveController`, `ArmDrive`, `Human`, `HumanSegment`, `VestibularSystem`, `BodyStateEstimator`, …) hold **0** references to `ImpactThrower`/`ImpactProjectile`/`Projectile`/`-impact`, and every physics query they can make is masked to `Ground` (layer 6) while the thrown body keeps Default (layer 0) and the throw lane writes no layer at all — so the solver's own contact response is the only channel that can reach him. Behavioural: the **1621 pre-contact frames** of a scheduled 8 s throw are bit-identical to the quiet stand (0 differing rows, 0 differing body columns, differing columns: none) — no bracing. Seed: 1/2/3 move time (7.805372/7.952081/8.098790 s), side (+1/+1/−1) and aim (Torso/Torso/Pelvis) while the ordered 32 N·s impulse is identical in all three; the same seed twice is byte-identical (`csv md5 03f44d42a043d3c53f50720926ec7219`). FINDING: `System.Random`'s small seeds are correlated — the three plan times sit exactly 0.146709 s apart, inside the 0.25 s jitter contract but **a sequential-seed sweep is not an independent sample** (scatter the seeds for any distribution claim); and the 5.0001 ms latency of item (a) is confirmed to be ARRIVAL, not an answer, because the body provably does nothing before the blow. L2 PASSES; L3 still has no run (it needs an active-reaction-off switch inside the physics lane's controller). **CURRENT-BUILD EVIDENCE 19.09** (level worker; player `7ebbad89`, fp `559e2e50`, gitCommit `fca0a4a8`, ZERO source edits, 7 runs `imp_cb*`): the card's evidence clause (the delivered run's `sourceFingerprint` == `Build/Headless/build-manifest.json`) was satisfied by no run in the folder (imp_v1 11:56, grid 12:0x-15:1x, L2 18:03), so the accepted protocol was re-run on the player on disk and the video cut from those runs. Identity **MATCH on all 7**; rule zero bit-for-bit (7/7 card numbers, and `imp_cb_quiet` vs the 11:56 `imp_quiet40`: 700 shared keys, 11 differences, all identity/clock). The same class on three builds of the day: contact 8.110043 s, reference 32 N·s, delivered 30.22801 N·s, `latencyActiveMs` 5.000114, down 0.605014 s identical on all three, while the post-fall extrema move (`peakComExcursionM` 1.984342 → 1.911077 → 1.868298, `minSupportMarginM` -0.925346 → -0.925359 → -0.901962, `peakComDropM` 0.810421 → 0.810422 → 0.827085) — and `Tools/impact-cb-diverge.py` locates the move: **1937 of 2400 CSV rows bit-identical**, the first difference at t = 9.690079 s = contact + 1.580 s, body already down (tilt -46.54031 → -46.48721, bodyAngVel -219.5161 → -208.6491), so the response to the blow did not move, the post-fall settling did (candidate, not a bisect: the 18:56-19:11 arm/leg-lane edits in the 19:24 player). Evidence: `impact_current_build.mp4` (**54.15 s, 1083 frames, 1920×1080**, three takes cut from t = 6 s) + three per-class 24.05 s cuts (481 f each) + 12 stills (contact / criterion fall / ctrl fall / window end per take), all captioned by `ImpactCaption.cs` with this build's own identity. Tools `Tools/impact-cb-runs.sh`, `impact-cb-table.py`, `impact-cb-diverge.py`; report `Trials/Runs/impact-cb-report.md`. **CURRENT-BUILD EVIDENCE #2 19.09 22:34 (level worker; player `bf191843300941a48cab37f1a579f44e`, fp `b0d57623fc56565db7529e5210e3b3063d5e90f0b45e430037b9ff1e5585ee86`, 632 files, `buildTimeUtc 2026-09-19T19:32:28Z`, ZERO source edits, 7 runs `imp_cb*` + the same-seed repeat `imp_cb1r`):** the player moved twice after the 19:24 delivery (the arm lane's `ArmDrive.cs` 21:50, the walk lane's `BalanceControllerCaptureWalk.cs` 22:24), so no run in the folder named the player on disk - the accepted protocol was re-run and the video cut from those runs, and the superseded 19:24 evidence (1487 files) is KEPT in `Trials/Runs/impact-pre-bf191843/`. Identity MATCH on 7/7; rule zero bit-for-bit (the card's seven numbers 0.0148 / 2.6542 / 7.5647 / 0.0 / 8002 / 14.2838 / 694.9567; `imp_cb_quiet` vs `burn_stance` = 700 shared keys, 11 differences all identity/clock, and this pair is a free CROSS-BUILD control because `burn_stance.json` was itself re-run at 22:31 on `186813e9`); reproducibility re-proved on THIS player (`imp_cb1` vs `imp_cb1r`: per-frame CSV **byte-identical**, md5 `d6b61d96d0902743bae66c190b01167c`, JSON differs in 4 keys = label ×2 + `wallPerSim`/`wallSeconds`). The same class on the FOURTH build of the day: contact 8.110043 s, reference 32 → delivered 30.22801 N·s, `closingSpeedMps` 7.879361, `latencyActiveMs` 5.000114, down 0.605014 s all identical; `impact-throw-diff` prints **4 moved keys on `cb1`** (all in the 4th digit) and **zero on `cb2`/`cb3`**, and `impact-cb-diverge` locates the `cb1` move: **1262 of 3200 rows differ, first at row 1938 (t = 9.695079 s = contact + 1.585 s)**, i.e. the blow and the response are unchanged and only the downed body's settling moves. Quotable classes now also record the contact PART: the thigh-aimed rect first touches the **LeftArmHand** (and is the only class with a positive `backInsideSupportSeconds`, 0.835021) - identical on the 19:24 player, so it is what the harness has always measured. Evidence: `impact_current_build.mp4` (54.15 s, 1083 frames, 1920x1080) + three 24.05 s cuts + 12 stills, every frame captioned with this build's own identity (banner read back off the rendered frame). New/changed tools: `Tools/impact-cb-rows.py` (the three quotable classes per run, with the manifest check per row) and `Tools/impact-cb-table.py` (`TRACE` now carries the folder each row was read from, so a reused label cannot silently name the wrong player). Report `Trials/Runs/impact-cb2-report.md`. **CURRENT-BUILD EVIDENCE #3 20.09 00:10 (level worker; player `a12521af`, fp `917a1c8b`, gitCommit `cd882312`, buildTimeUtc `2026-09-19T20:33:21Z`, ZERO source edits, no rebuild, 8 runs `imp_cb*` + the repeat `imp_cb1r`):** `Build/Headless` was rebuilt at 23:33 by another lane, so after 22:34 no run in the folder satisfied the card's clause (`record-build-id.py`: every `imp_cb*` named `bf191843`, the manifest names `a12521af`, the tree is `dfdf5920` now). Before the re-run the drift was located and REFUTED as a physics change: **new `Tools/impact-drift-audit.py`** prints `dll_vs_sources` = the player's `Assembly-CSharp.dll` (23:32:32) is newer than every `.cs` under `Assets/` (newest `DogStanceController.cs` 23:32:00) and `drift_is_art_only: YES` — 27 files written after the manifest, all `.png`/`.meta` under `Resources/` plus the 5 render/`ProjectSettings` assets the artist's own preview build rewrote (`DynamicsManager.asset`/`Physics2DSettings.asset` untouched), and `trial-manifest.py --check-list` says the file set did not move (632 files, added=0 removed=0), so the fingerprint difference is content in those pictures only. Identity **MATCH 8/8**; rule zero is now a **same-build** control (`burn_stance.json` was itself re-run 23:34 on this player): 707 shared keys, **5 differences, all identity/clock** (`commandArgs`, both labels, `wallPerSim`, `wallSeconds`), the card's seven numbers unchanged (0.0148 / 2.6542 / 7.5647 / 0.0 / 8002 / 14.2838 / 694.9567); reproducibility byte-identical again (CSV md5 `d6b61d96…`, the same md5 the 22:32 pair printed); and against the 22:32 archive (`Trials/Runs/impact-pre-a12521af/`, 1491 files kept) **all three classes are identical — `throw[0]` 79/79 keys and `cb1`'s 3200 CSV rows on 31 columns** — so the arm lane's `ArmDrive.cs` (23:28) and the walk lane's capture-walk edit (23:22) did not move this scene at all (the 19:24→22:32 pair had moved four keys in the 4th digit). Evidence: `impact_current_build.mp4` / `impact_three_throws.mp4` (**54.15 s, 1083 frames, 1920×1080**) + three 24.05 s cuts + 12 stills (contact / fall / ctrl-fall / window-end per take), each captioned with this build's own identity (banner read off the rendered frame: `build a12521afdadd | source 917a1c8bb74e`); **new `Tools/impact-evidence-check.py`** reads every still's own IHDR (all 1920×1080), its mtime against the manifest (all after 23:33:23) and ffmpeg PSNR against the archive (32.0937xx dB — the caption's header block is what moved). Report `Trials/Runs/impact-cb3-report.md`. **CURRENT-BUILD EVIDENCE #4 20.09 00:40 (level worker; player `39a689fd`, fp `9d21b051`, 632 files, `buildTimeUtc 2026-09-19T21:35:32Z`, gitCommit `5b6c2e16`, ZERO source edits, no rebuild, 8 runs `imp_cb*` + the repeat `imp_cb1r`):** the dog lane rebuilt `Build/Headless` four times inside five minutes (manifests read at 00:31:04 `c9cd3387`/`8de03490`, 00:32:32 `433d0859`/`90773645`, ~00:33 `9d21b051`/`8868d6a6`, 00:35:34 `9d21b051`/`39a689fd`), so no run in the folder named the player on disk; the accepted protocol was re-run and the video cut from those runs, and the superseded `a12521af` set (1492 files) is KEPT in `Trials/Runs/impact-pre-8868d6a6/`. **FINDING: a fingerprint MATCH is not a build match** — the fingerprint is computed over the manifest's file NAMES (`trial-manifest.py --check-list`), so `9d21b051` is the fingerprint of both `8868d6a6` and `39a689fd`; the first pass of seven labels stamped THREE players (fp `433d0859` / `dca6f4bd` / `9d21b051`, buildGuids `90773645` / `7310f8eb` / `39a689fd`) while the card's clause read `MATCH` on all of them, so `impact-cb-table.py` now prints a `guid?` column (same/OTHER) and two labels were re-run for reading `MATCH / OTHER`. Identity now **7/7 MATCH and 7/7 the manifest's own buildGuid** (the same-seed repeat 8/8), and the DLL is bit-identical before and after the takes (`cc204c9af0f80af4…`). Rule zero: 707 shared keys, **11 differences, all identity/clock**, the card's seven numbers unchanged (0.0148 / 2.6542 / 7.5647 / 0.0 / 8002 / 14.2838 / 694.9567). Reproducibility: `imp_cb1` and `imp_cb1r` per-frame CSV **byte-identical**, md5 `d6b61d96…` — the same md5 the 22:32 and 23:33 cycles printed, so the class is byte-identical on three accepted builds. The other lanes' edits did not move this scene: against the archive, `impact-cb-diverge` = 31 columns x 3200 rows identical and `impact-throw-diff` = 79/79 keys identical. Evidence: `impact_current_build.mp4` / `impact_three_throws.mp4` (**54.15 s, 1083 frames, 1920×1080**) + three 24.05 s cuts + 12 stills, `IMPACT-EVIDENCE-CHECK OK stills=12 problems=0`, banner read off the rendered frame (`build 39a689fd9eca | source 9d21b0514b81`; 30.228 N·s / 1.868 m / -0.902 m / down 0.605 s / steps 0). New tool `Tools/impact-archive-set.py` (move a superseded set aside by label or `--build`, never delete) with a recorded mistake: called with no label list it moved EVERY impact label (6758 files, incl. `imp_v1`/`imp_s1a`/`l2_hit_s1`/the grid sets); 5266 files were restored inside the cycle and the tool has no "everything" default now. Report `Trials/Runs/impact-cb4-report.md`. **Checked and left for the next cycle: the gallery/species frames** — `Art/skin.png`/`fabric.png` on disk are the artist's final `after-bytes` (`a5f78f7e…`/`87706428…`) and their own `art0wrap17` catalogue was shot with them (`GALLERY-REFRESH-CHECK OK stills_bad=0`), while the level lane's `gallery6_*` and the ten species stills are one art revision behind. **Still open in this lane: L1** (5.000114 ms = exactly one fixed step = the ARRIVAL of the blow, so the metric cannot resolve the card's 40-150 ms band) **and L3** (no switch disables the active reaction; building one means editing `Assets/BalanceController.cs`, the balance stream's file). | `.cursor/tasks/BALANCE-ARENA-1.md` |
| level | ARENA check 19.09 (Hermes; fingerprint `b63dda80`, buildGuid `36aa1bba`; runs `arena_chk1`/`arena_chk1b`/`arena_chk_video`, seed 7, 180 s): runs, writes JSON + CSV, appends to the append-only `arena-param-log.csv` (331 rows); interval 0.5 s assert OK; alive cap 60 never crossed (361 spawns, 301 culled); `ArenaHuman` x `ArenaHuman` off, x ground on (58 overlapping bodies, no push); determinism 361/361 identical genomes and distances; inheritance 185/331 children from the top quartile (chance 0.25), zero-mutation-delta children 0/166, no gene's spread at 0; the shared `FallCriterion` discriminates (7/331 upright at 15 s; before it `fallen` was 61/61). **HEAD NUMBER FIXED 19.09** (level worker; buildGuid `19547550`, fp `4914add9`, runs `arena_num1`/`arena_num2`, seed 7): the digit is now sized from the HEAD's own collider box (measured 0.230 m) and the requested `ArenaRunner.numberHeight` 0.34 m acts as a ceiling - drawn **0.127 m = 0.55x the head** (correction's ceiling 0.60), anchored `MiddleCenter` INSIDE the skull, and read back from the rendered mesh (`measured` 24/24, `insideHead` 24/24); 7 whole bodies with 31 px digits in one 1080p frame (`Trials/Runs/photo_arena_num1_00.png`, t=6 s). Rule zero: two runs differing only in `-arenaNumber` give 81/81 identical human rows. Report `Trials/Runs/arena-number-report.md`. The genome is still the 13 C1 genes - the wide 155-gene box is a spec only (`arena-wide-genome.md`). Elite copies 65..81 drift 0.346 m from their reference at the same age (path 1.46 m) while the back-to-back pair 57/58 is exactly 0.000 m. `arena-report.py` crashed on an unbound `video` (fixed 19.09) | `.cursor/tasks/ARENA-1-param-sweep.md` |
| level | **SPECIES SCENES 19.09** (level worker; buildGuid `2c54685e`): `Assets/Scenes/TreeOak.unity` + `TreePine.unity` are clones of the accepted `TreeApple.unity` whose ONLY difference is the three fields that name the species (`Tools/species-scenes.py` writes them, `--check` re-verifies, both also in the preview player's scene list). Each scene holds exactly one tree - oak/pine run `PlantTree 1 / AppleTree 0`, every other spawn counter 0 - and each still is 1920x1080 with the whole tree in frame (`Trials/Runs/treeoak_photo.png`, `treepine_photo.png`; the control `treeapple_ctl_photo.png` reproduces the 11:02 apple numbers to the digit: height 3.256089, ortho 1.790849). The framing law `ortho = 1.1 * max(h/2, w/2/aspect)` matches all three measured boxes to 1.5e-5 m and gives 1.100 for a 2 m tree, 2.750 for a 5 m one. Measured transient: oak and pine each have ONE frame outside the frame (t = 0.020 s, 1.47 m / 0.92 m out) on the frame after the real art lands; `missFramesAfterOneSecond` 0 in all three. **SPECIES SET COMPLETE 19.09** (level worker; fp `54b94b65`, one build): the remaining seven (willow, bush, poplar, lilac, hazel, cedar, plum) are in as the same clones, so all nine `PlantTree.ParseKind` kinds have a scene and `Tools/species-scenes.py` now fails if a parsable kind has none (`COVERAGE parseKind=10 scenes=10 missing=none`); ten runs (apple control + nine) each report `treeCount 1` (apple `AppleTree 1/PlantTree 0`, the rest `PlantTree 1/AppleTree 0`), `fits true`, `missFramesAfterOneSecond 0`, and the framing law holds on all ten to 9.8e-5 m. Findings: the **cedar's drawn box is 33.5 x 34.7 m** because 189 of its 746 parts are over 5 m (biggest leaf fan 19.40 x 16.06 m on a lossy scale of 1338) while its 12 colliders stay <= 0.48 m - a picture-only rig defect for the plant lane. Gallery re-shot on the same build (`gallery3`, 7/7 fits, 28 real/13 missing, pan 165 f = 16.5 s, real-vs-placeholder PSNR 39.33 dB). Report `Trials/Runs/tree-species-all-report.md`. **SPECIES STILLS REFRESH 19.09 (level worker, no source edit, no build):** the ten stills on disk were captured 14:40-14:42 on `buildGuid 54b94b65`, but `Art/leaf.png` (19:50), `Level/ground_cap.png` (19:22) and `Level/sign_finish.png` (17:21) were written AFTER that capture and Resources bake into a player at build time - so Yury would have judged the previous art. All ten re-run on the player already on disk (`buildGuid 0114b556a23e4529aa49c9331438d87b`, the same id the newest gallery capture `art0flame1` carries, `resources.assets` 20:28): `SPECIES-RUN-ALL scenes=10 failed=0`, each `treeCount 1`, `fits true`, 1920x1080. `Tools/species-refresh-check.py Trials/Runs/species-pre-art0leaf` (new tool; the outgoing stills are kept in that folder): `acceptance-failures=0 pictures-repainted=10 pictures-unchanged=0` - all ten pictures move (ffmpeg PSNR 35.4-52.9 dB vs the outgoing still) and every acceptance field comes back (three lengths to 1e-5 relative; worst cedar 1.4e-4 m = 4.3e-6 rel, apple/pine exactly 0). The cedar's 1.45e-4 m is the live player's own wobble, measured: a third run reads 33.49763488769531 against 33.49775695800781 -> 33.49761199951172. No file under `Assets/` was touched. Report `Trials/Runs/tree-species-refresh-report.md`. **RE-SHOT 19.09 23:00 (level worker, preview player `641f6567`, no `Assets/` change left, `git status --porcelain Assets/Resources/Level/` empty):** the ten stills were re-shot on the newest preview player (`SPECIES-RUN-ALL scenes=10 failed=0`; every scene `treeCount 1`, `fits true`, `missFramesAfterOneSecond 0`, `dressOutsideMeters 0.000`, 1920x1080, apple `AppleTree 1 / PlantTree 0` and the nine `PlantTree 1 / AppleTree 0`) and the hypothesis behind the re-shoot - the 22:23 `Level/ground_cap.png` making them stale - was REFUTED twice. (a) CODE: `Assets/Scenes/TreeApple.unity` carries exactly `GameProcess` + `GroundBuilder` + `SpeciesScene` and no `LevelGenerator`, and `GroundBuilder.BuildGround/BuildGrass` draw `ArtLibrary.Tiled(ArtLibrary.Dirt|Grass)` (i.e. `Resources/Art/dirt.png` / `grass.png`), `BuildSky` draws a flat colour - so `LevelArt.Cap()`, the sign and `Level/sky.png` are NEVER drawn in a species scene; the only art of that scene newer than the 20:34 capture is `Art/leaf.png` (19:50), which is what justified the earlier refresh (that report's cap/sign lines are wrong for these scenes - folded here so the premise is not repeated). (b) A/B: `Tools/species-cap-ab.py` swaps only the cap canvas with both byte sequences proved (OLD `67f407c7c7122aece81fb239440709daea` 1519 B, repainted through the generator's own painter; NEW `8d5f842ec5e85750000720a72e355e89` 1746 B) and two preview builds (`6f4a3ff5` old cap, `641f6567` shipped) shot through the same protocol: `SPECIES-REFRESH-CHECK species=10 acceptance-failures=0 pictures-repainted=10 pictures-unchanged=0`, every acceptance field back (pine 0 drift, apple 2.4e-07 m, oak 1.6e-05 m, cedar 1.0e-04 m). FOUND, and it bounds every future art A/B on this rig: the species camera is re-fitted from the live swaying box every frame, so two runs are never pixel-aligned and pixel comparison has NO resolving power here - the same-build control (plum r3 vs r4) differs in 10.705 % of the frame and 44.26 % of the ground band, the oak cap pair 26.792 %, and the one pair that did align (apple, 0.078 % = 1616 px) shows no cap band at all (the artist's cap bands are 2-58 px tall x 1920 = 3.3k-35k px) - `Tools/species-cap-frame-diff.py`, new, prints that per-row profile with the control. The plum's 4th acceptance row is the live player's own wobble, not a change: six runs on three players (14:42 set, delivered, r2, r3, r4, old-cap set) spread 7.25e-05 m = 1.8e-05 relative against the checker's 1e-5 relative band, so its row flips between `identical` and `MOVED` with the pair compared; `Tools/species-run-diff.py` (new) prints two runs field by field. Own mistake, stated: the first snapshot loop wrote `"tree$s_photo.png"` (bash reads `$s_photo`, unset) so only the JSONs were archived and the outgoing 20:34 PNGs were overwritten - the byte-proved A/B replaces that lost md5 column. No `.cs` touched, `Build/Headless/` not rebuilt. Report `Trials/Runs/species-refresh-2260-report.md` | `Tools/species-scenes.py`, `Tools/species-run-all.sh`, `Tools/species-refresh-check.py`, `Tools/species-cap-ab.py`, `Tools/species-cap-frame-diff.py`, `Tools/species-run-diff.py` |
| level | **ART-1 item 2 measured 19.09** (level worker; no build, no `Assets/` write, no trial): `Tools/art1-fit-audit.py` (+ `Tools/art1-fit-audit-crosscheck.py`, 10/10 identical to Pillow) reads the shipped PNGs, `Human.cs`, `GalleryScene.cs` and `LevelTest1.cs` and reports the drawn-vs-art aspect per part — the finish sign fits exactly (scale 0.28/0.28), and **10 of 10 human parts are drawn 1.05x (head) to 2.67x (hand) off their own aspect** wherever a part picture is `Sliced` onto its collider box, which is what the gallery does and what the rig does (`HumanSegment` draws the collider box). The hand is the worst row in the catalogue although `ART-HAND` exempts it in the rig, and `ART-FOOT`'s ×2.42 is stale (its `VisualSize` growth no longer exists in `HumanSegment.cs`; the current number is 1.69x). **PADDED 19.09** (level worker; fp `5ee9ecae`, buildGuid `dc09a872`, preview `PREVIEW_BUILD_OK`): the fix the report above recommended is in - `pad_to_aspect` in `Tools/import-part-art.py` adds a centred transparent margin so the canvas aspect equals the shipping collider box from `Human.cs`, and the `.meta` ppu makes the canvas' own world size that box (checked to 0.0015 m or the run fails), so the picture is scaled by ONE factor instead of two; `ART1-FIT parts=11 distorted=0` (was distorted=10, hand 2.67x). Consequence measured: the body keeps its proportions and fills 95 % (head) down to **33 % (hand)** of its box, torso 57 %, foot 58 % - so the remaining item-2 rows are a decision, not a mechanical fix (artist repaint in the box aspect, or the hand box in `Human.cs`). Only the ten `Human/*.png` + `.meta` under `Assets/Resources` moved, no `.cs`, and `Build/Headless` was not rebuilt. Frames `gallery5_photo/_human/_tree/_apple/_materials/_level/_fire/_placeholder` + `gallery5_pan.mp4` (165 f, 16.5 s, 1080p), A/B pair `gallery4b_human.png` vs `gallery5_human.png`. Report `Trials/Runs/art1-pad-report.md`. **GALLERY RE-SHOT 19.09 23:02** (level worker, ART-1c/1d evidence; no `Assets/` write, no build, zero-touch cycle): the accepted frames were `gallery5` on player `dc09a872` (22:05), but three inputs the catalogue draws moved after it - `Level/ground_cap.png` 22:45:59, `Campfire.cs` 22:51:17, `GalleryScene.cs` (the assembled FIRE row) 22:58:32 - while the preview player on disk is 22:58:57, i.e. **after all of them and after every other `Assets/` file**, so the catalogue was re-photographed on it with the same protocol (label `gallery6`, `buildGuid` `6e2836b7`, tree fp `fb6d0b21`, 632 files): 8 stills, all 1920x1080 and all written after the build, covering the card's list (whole gallery / human close / tree close / apple / materials / ground+sky+sign / fire / placeholder branch), **8/8 `fits`**; `gallery6_pan.mp4` **165 f = 16.500000 s at 1920x1080** (>= the card's 15 s); `realItems 29` / `missingItems 11` / `dressedRenderers 5567` and `rowBoxes` 23 rows **identical** to `gallery5`; the card's item-5 inventory printed in full (36 named items + the four level files' on-disk/meta status). What moved is exactly what the artist changed: the FIRE row is now the ASSEMBLED stack (inventory entry `campfire flame core` -> `campfire (assembled)`, 29 pieces 22 lit, painted fraction 0.8979705 -> 0.9016172) and every still differs from its predecessor (ffmpeg PSNR: fire 24.11 dB, human 34.23, tree 37.06, apple 39.11, materials 36.80, level 41.96, whole gallery 44.70); within one build the real-vs-placeholder pair is 39.33 dB and the pre-pad human close 31.71 dB. Rule zero: the cycle wrote nothing under `Assets/` (only `Trials/Runs/gallery6*`, `Trials/Runs/shot_gallery6_*.png`, `Logs/art1-gallery6.log`, `Tools/gallery-refresh-check.py`) and `Build/Headless/` was not rebuilt; `gallery5_*` and `gallery4b_human.png` are kept, nothing overwritten. New tool `Tools/gallery-refresh-check.py` (still pixel size from the PNG's own IHDR + mtime-vs-build verdict + field-by-field diff of two capture labels, list entries matched by name) - `GALLERY-REFRESH-CHECK OK stills_bad=0`. Report `Trials/Runs/gallery6-report.md`. **EVIDENCE REFRESH 20.09 00:40** (level worker, ART-1c/1d + Yury's species order; no `Assets/` write, no build, zero-touch): the ten species stills (`641f6567`, captured 22:46-22:48) and `gallery6` (`6e2836b7`, 23:02) were each baked BEFORE the artist's `Resources/Art/{bark,leaf,dirt,grass}.png` writes (23:53:56-58) and `Level/ground_fill.png` (00:24:24), while the preview player on disk (`ad2baf22`, `globalgamemanagers` 00:24:38) is after all of them - so both were re-shot on it. Species: `SPECIES-RUN-ALL scenes=10 failed=0`, every scene `treeCount 1` (apple `AppleTree 1 / PlantTree 0`, the nine clones `PlantTree 1 / AppleTree 0`), `fits true`, `missFramesAfterOneSecond 0`, 1920x1080; `species-refresh-check.py` = acceptance fields identical on 9 of 10, the plum row moving 1.6e-05 relative - CLOSED as the live player's own wobble because a same-build pair (`treeplum` vs `treeplum_r3`) disagrees 3x more (1.6e-05) than the archive pair does (1.0e-06); all ten stills repainted (new `Tools/capture-still-psnr.py`: 22.93-42.43 dB); the outgoing stills+JSONs are KEPT in `Trials/Runs/species-pre-ad2baf22/` (20 files). Gallery: label `gallery7` on `ad2baf22`, 8 stills + a **165-frame 16.500000 s 1920x1080** pan (`gallery7_pan.mp4`), `GALLERY-REFRESH-CHECK OK stills_bad=0`, `realItems 29 / missingItems 11 / dressedRenderers 5567 / rowBoxes 23` identical to `gallery6`, all 8 stills repainted (tree 24.157 ... placeholder 61.438 dB), `gallery6*` kept. Report `Trials/Runs/species-refresh-2010-report.md` | `ART-1-gallery.md` item 2 / 3 / 5 |
| level | **ART-1 CATALOGUE RE-SHOT 20.09 01:02** (level worker; preview `d24a5bcd785d46f181aa76e719b7635e`, `bash Tools/gallery-preview-build.sh level-gallery8` -> `PREVIEW_BUILD_OK scenes=12 errors=0 bytes=150256013`, data 01:02:01): the delivered `gallery7` (00:43, player `ad2baf22` data 00:24:38) predated the artist's nineteenth pass (`Assets/Campfire.cs` 00:55:06, `Assets/GalleryScene.cs` 00:55:33, `Assets/Editor/Art1Check.cs` 00:55:56, `Assets/Resources/Art/Campfire/{core,log_a,log_b,log_c}.png` 00:57:35), so the art player was rebuilt from bash and the catalogue re-shot as `gallery8`: 8 stills 1920x1080 (01:02:21-43, all after the build data; pan frames `shot_gallery8_00..164` 01:02:24-43; video 01:02:56), pan **165 f = 16.500000 s 1920x1080** (`gallery8_pan.mp4` == `gallery8.mp4`, md5 `5e1b54bab0757a9c7558b5f01f202815`), `realItems 29 / missingItems 11 / dressedRenderers 5567 / rowBoxes 23 / fits 8 / inventory 36` identical to `gallery7` (`GALLERY-REFRESH-CHECK OK stills_bad=0 fields_moved=7` = label, buildGuid, capturedUtc, 8 still names, the FIRE row's `texture`+`note`), stills repainted exactly where the write set says (`capture-still-psnr`: fire 40.542, apple 72.437, photo 70.858, placeholder 70.858 dB; human/level/materials/tree `inf`). **FINDING - the fire's log canvases ARE in the frame while the catalogue's own counter says they are not**: `gallery8-gallery.json` prints "logs dressed 0/3 by Campfire.DressLog", but the pile window (x 845..1075, y 735..845 = 0.68 x 0.32 m at the row lens) loses BOTH flat `LogWood` blends to exactly 0 px (`[106,73,65]` 7319 -> 0, `[117,68,45]` 2469 -> 0) and gains three textured bark browns (1837 / 1198 / 869 px), modal fraction 0.2893 -> 0.1463, distinct colours 5024 -> 7204 - the canvases are drawn. The counter can never read anything but 0: `HumanSegment.ApplyPicture` leaves a pixel-snapped white body sprite (`CreateBodySprite` 256 px/m, 64-px minimum side) so `ArtParts.BoxOf(sr) == piece.size` is false for all three logs by 0.21 / 1.04 / 0.75 mm (`Tools/level-logs-dress-counter.py`, no Unity: `counter_can_hold=0`) - the line's test is too strict, not the art. Proposed next cycle (one change, needs a build): count `Campfire.DressLog`'s own return value, or compare `ArtParts.WorldBoxOf(sr)` after a frame, and re-shoot. The ten species stills were NOT re-shot and that is proved, not assumed: the artist's writes are campfire-only, no species scene carries a campfire or a `LevelGenerator`, and the control `treeapple_ctl3` on the new build returns `trees=1`, `fits True`, `missAfter1s=0`, apple `AppleTree 1 / PlantTree 0`, with all 12 acceptance fields `d=0.000e+00` against the still on disk and 94.577 dB PSNR. Rule zero: no `Assets/` file written by this cycle (the 17 `git status` entries are the other lanes' in-flight files plus the build's own URP reserialize), `Build/Headless/build-manifest.json` untouched (00:35:34), no trial run - the walk lane's `c6o_*` runs are theirs. New tools: `Tools/level-gallery8-read.py`, `Tools/level-fire-band-map.py`, `Tools/level-fire-window-flat.py`, `Tools/level-logs-dress-counter.py`. Report `Trials/Runs/level-gallery8-report.md` | `ART-1-gallery.md` evidence + acceptance 1-4 |
| level | **GALLERY COUNTER FIX 20.09 01:45** (level worker; preview `f27c21abad7e483ca735b21948d95a19`, `bash Tools/gallery-preview-build.sh level-gallery9` -> `PREVIEW_BUILD_OK scenes=12 errors=0 bytes=150256525`, data 01:34:46): the 01:02 finding is closed - `GalleryScene.BuildFireRow` counted a log as dressed on `ArtParts.BoxOf(sr) == piece.size`, which `HumanSegment.ApplyPicture`'s pixel-snapped white body sprite makes false by construction, so the note read `0/3` on a frame that showed the canvases; it now counts off `Campfire.DressLog`'s own verdict (it returns `ArtParts.Dress`'s), `/3` became `/logSeen`, and a refused dress is named in the note. Catalogue re-shot as `gallery9`: 8 stills 1920x1080 (01:35, after the build data), pan **165 f = 16.500000 s 1920x1080** (`gallery9_pan.mp4` == `gallery9.mp4`, 1582970 B), `gallery9-gallery.json` carries `buildGuid f27c21ab`, the fire note **"logs dressed 3/3 by Campfire.DressLog"**, and `realItems 29 / missingItems 11 / dressedRenderers 5567 / rowBoxes 23 / fits 8 / inventory 36` identical to `gallery8` (`GALLERY-REFRESH-CHECK OK stills_bad=0 fields_moved=7` = label, buildGuid, capturedUtc, the 8 still names, the fire note). **The fix is a line, not a picture, and that is measured**: `gallery8_fire.png` and `gallery9_fire.png` are byte-identical (md5 `5b7667785b19157fb809eab7b6a9adfb`) although the note moved; 7 of 8 stills `inf` PSNR, the apple row 69.879 dB = the re-shoot's own noise floor (8 px in box x 1115..1409 y 298..423; the same row moved 72.437 dB between 7 and 8, where only the campfire changed); 134 of 165 pan frames byte-identical (ffmpeg framemd5) with video mean PSNR 60.62 / min 56.46 dB; the pile window on the new build still loses both flat `LogWood` blends to exactly 0 px (`gallery7` -> `gallery9`: `[106,73,65]` 7319 -> 0, `[117,68,45]` 2469 -> 0, distinct 5024 -> 7204). `Build/Headless` untouched (manifest 01:32:38, the dog lane's), no trial run, only the art player's `Gallery` scene opened. New tool `Tools/level-gallery-inventory.py`. Report `Trials/Runs/level-gallery9-report.md` | `ART-1-gallery.md` evidence + acceptance 1-4 |
| level | **GALLERY `gallery10` 20.09 02:44** (level worker; zero-touch, no build, no `Assets/` write): `gallery9` (01:35, player `f27c21ab`) predated the artist's flower pass (`Art/Flower/*.png` 02:31:08, `ArtParts.cs` 02:28:44, `GalleryScene.cs` 02:34:23) and the art player rebuilt at **02:34:52**, so the catalogue was re-shot on the player already on disk (newer than every RUNTIME input; the only later write is the Editor-only `Art1Check.cs` 02:37:04, not shipped): label `gallery10`, `buildGuid 55be038d16df4c08b4b26d140d9de0a8`, captured 23:44:11Z, **10 stills 1920x1080** (01:43:49-52, after the player data) incl. the new `gallery10_flower.png` + `gallery10_flowerbox.png`, pan **165 f = 16.500000 s 1920x1080** (ffprobe), `realItems 29 -> 30`, `rowBoxes 23 -> 24` (the FLOWER row), `dressedRenderers 5634`, `fits` on all 8 named targets, `GALLERY-REFRESH-CHECK OK stills_bad=0 fields_moved=10` (label, buildGuid, capturedUtc, still names, `realItems`, `missingItems`, `rowBoxes`, inventory entry). What moved is the flower pass only: FIRE/human/level/materials/tree stills byte-identical (`inf` PSNR), the whole-gallery pair 41.30/41.22 dB and the apple close 57.63 dB because the layout gained a row, and the inventory changed in exactly one entry (`flower` -> `flower bed (rose, daisy, tulip, sunflower)` with the five canvases) - the last object without art at all now has art. **FINDING (next cycle's single change)**: `missingItems` reads **7** while the printed inventory says **not real 6** - `Assets/GalleryScene.cs` increments ONE counter for two things (line 743, a MISSING-ART tile actually drawn; line 790, a row in the inventory), so 6 rows + 1 drawn tile = 7 and a reader cannot tell which; the fix is to split it and name the drawn tiles (needs a preview build + re-shoot). Rule zero: no `Assets/` file written, `Build/Headless` untouched, no trial run, only the art player's `Gallery` scene opened; `gallery9*` and every earlier catalogue kept. New tool `Tools/level-gallery-lists.py`. Report `Trials/Runs/level-gallery10-report.md` | `ART-1-gallery.md` evidence + acceptance 1-4 |
| level | **ONE-SPECIES SCENES RE-SHOT 20.09 02:47** (level worker; zero-touch, no build, no `Assets/` write): the ten species stills (00:40-01:05, player `ad2baf22`) predate the art player rebuilt at **02:34:52** (`55be038d`), and the species path draws through `Assets/ArtParts.cs` (rewritten 02:28:44 by the artist's flower pass), so the claim "the tree art did not move" was measured instead of assumed: `bash Tools/species-run-all.sh` -> **`SPECIES-RUN-ALL scenes=10 failed=0`**, every scene `treeCount 1` (apple `PlantTree 0 / AppleTree 1`, the nine clones `PlantTree 1 / AppleTree 0`), `fits True`, `missAfter1s 0`, `dressOutside 0.000 m` (cedar 67.392), 1920x1080, each scene framed at its own height (ortho 1.503 for the 2.243 m bush up to 18.424 for the 33.498 m cedar) - Yury's order (one species per scene, apple first, whole tree in frame, count exactly 1) holds on the current player. `species-refresh-check.py`: **10/10 acceptance fields identical (max drift 4.6e-05 m = 1.4e-06 rel), 10/10 pictures repainted**; PSNR of every pair 33.20-84.93 dB; the outgoing `ad2baf22` set KEPT in `Trials/Runs/species-pre-55be038d/` (20 files). Why pictures move while the scene does not, measured on the oak pair: 13 of 44 keys differ - `buildGuid`, `capturedUtc`, `observedFrames 17134 -> 17932`, `heightMeters 3.1868665 -> 3.1868720` (5.5e-06 m), `orthoSize`, `paintedFraction` - i.e. the live player's frame-count wobble on time-driven swaying plants, not a scene change. Report `Trials/Runs/species-refresh-2048-report.md` | Yury 18.09 species order; `ART-1-gallery.md` item 3 |
| level | **BALANCE-ARENA-1 current-build evidence #5 20.09 02:41** (level worker; player `6ff2325c`, fp `da8fa193`, 649 files, buildTimeUtc 23:35:56Z, dll 02:35:53, ZERO `Assets/` edits, no build, 8 runs `imp_cb*` + the repeat `imp_cb1r`): the delivered set named `39a689fd` and the player was rebuilt twice inside eight minutes (02:11 `8c4be56d` 638 files, 02:35 `6ff2325c` 649 files), so the accepted protocol was re-run on the player on disk; the first half of the cycle measured `8c4be56d` and was superseded two minutes later - both sets KEPT (`Trials/Runs/impact-pre-8c4be56d/`, `impact-pre-6ff2325c/`, 1492 files each). Identity **MATCH + guid same on 8/8**; the card's rule zero is now a **same-build** control (`burn_stance.json` re-run 02:37 on this player): 707 shared keys, **5 differences, all identity/clock**, the seven numbers 0.0148 / 2.6542 / 7.5647 / 0.0 / 8002 / 14.2838 / 694.9567 unchanged; reproducibility byte-identical (`imp_cb1`/`imp_cb1r` CSV md5 `a858fd68e32df0dd1eb7a31dc77c686c`). **What the two rebuilds did to this scene is measured**: `Tools/impact-trace-diff.py` (new) on the full frame trace - `6ff2325c` vs `8c4be56d` **byte-identical on all eight labels (201 columns, 3200 rows)**; vs `39a689fd` the recorder gained one column (`captureSwingPostureBiasDeg`, 200 -> 201) and three of the arm lane's OWN window columns now move (`armWindowRiseL/R 0.0000 -> 0.0012` at t = 9.8351 s = contact + 1.725 s, body already down; `imp_cb3` at contact + 0.315 s), with **no body column moving anywhere** and the quiet stand's 8001 rows identical on all 200 common columns. Acceptance 3 now has an instrument: `Tools/impact-inplane-check.py` + `Tools/impact-inplane-selftest.py` (new) - `IMPACT-INPLANE-CHECK OK runs=8 bad=0` (per-throw `outOfPlane false`, `maxAbsZMeters 0`, `projectileGravityScale 0`, `referenceImpulseNs == mass*speed`) with **four mutations of a real accepted run all CAUGHT** - written because my own first two versions printed `OK runs=8 bad=0` while checking nothing (wrong file, then wrong level). `Tools/impact-drift-audit.py` now separates runtime from `Assets/**/Editor/**` scripts (an Editor-only file made it print STALE PLAYER; exit 1 only on a runtime drift). Evidence all written after the 02:35:58 manifest: `impact_current_build.mp4` == `impact_three_throws.mp4` (**54.15 s, 1083 f, 1920x1080**, md5 `de1e71644d3be50dc555aad1104fcdce`), three 24.05 s per-class cuts (481 f each), **12 stills** 1920x1080, `IMPACT-EVIDENCE-CHECK OK stills=12 problems=0` (PSNR 33.3762 dB vs the superseded set), the caption read off the rendered frame `build 6ff2325c52b2 | source da8fa193675b` with the banner equal to the published rows. Quotable rows unchanged on the seventh accepted build (torso 32 -> 30.22801 N*s, down 0.605014 s; head 72 -> 57.56658, 0.565013 s; thigh 180 -> 171.6157, 0.490011 s; `stepsAfterImpact 0`, `latencyActiveMs 5.000114`). Still open in this lane: L1 (the 5 ms is ARRIVAL, the metric cannot resolve the card's 40-150 ms band) and L3 (no active-reaction-off switch) - both in `Assets/BalanceController.cs`, the balance stream's file. Report `Trials/Runs/impact-cb5-report.md` | `BALANCE-ARENA-1.md` evidence + acceptance 2/3/4 |
| level | **BALANCE-ARENA-1 current-build evidence #6 20.09 06:25** (level worker; player `1073b8df`, fp `a6b51152`, 661 files, buildTimeUtc 02:42:36Z = 05:42 local, dll 05:42:31, gitCommit `644a0d59`, ZERO `Assets/` edits, no build, 8 runs `imp_cb*` + the repeat `imp_cb1r`): the delivered set named `6ff2325c`, and `Tools/impact-evidence-check.py` opened the cycle with **FAIL stills=12 problems=12** (every still older than the 05:42:38 manifest), so the accepted protocol was re-run on the player on disk (two commits the old set never saw: `697d8472` ARMS CYCLE 11, `644a0d59` ART-0 25th) and the `6ff2325c` set archived as `Trials/Runs/impact-pre-1073b8df/` (1492 files, nothing deleted). Identity **MATCH + guid same on 8/8**; rule zero (`imp_cb_quiet` vs `burn_stance.json`) 707 shared keys, **5 differences, all identity/clock**, the seven card numbers to the digit (0.0148 / 2.6542 / 7.5647 / 0.0 / 8002 / 14.2838 / 694.9567), and the quiet frame trace **identical on all 201 common columns over 8001 rows**; reproducibility byte-identical (`imp_cb1`/`imp_cb1r` CSV md5 `699c8e407d490363ff0e1badb5f97f07`). **What the two rebuilds did to this scene is measured, not assumed** (`Tools/impact-trace-diff.py` against the archived set): the quiet stand and the head throw are identical on all 201 common columns / 3200 rows, the torso throw diverges only after the fall (first difference t 9.6951 s, arm columns), and the **only throw whose published row moved is the one that lands on the ARM** (rect 12 kg 15 m/s: `timeToFall` 0.490011 -> 0.480011, `peakComExcursionM` 1.292102 -> 1.318134, `minSupportMarginM` -0.824011 -> -0.855388, `peakFzTotalN` 1950.536 -> 1861.74, push-back +18 f unchanged) - owner `697d8472`, the only commit that touched the arms between the two builds; this lane changed nothing. Acceptance 3: `IMPACT-INPLANE-CHECK OK runs=8 bad=0` + selftest 4/4 CAUGHT. Evidence all written after the manifest: `impact_current_build.mp4` == `impact_three_throws.mp4` (md5 `c47cc3f234f9d097b2dbd9c7b8b01576`, 54.15 s, 1083 f, 1920x1080), three 24.05 s 1080p takes, 12 stills, `IMPACT-EVIDENCE-CHECK OK stills=12 problems=0`, caption read off the rendered frame (`build 1073b8df5d33 \| source a6b51152780c`) equal to the published rows. **Two of my own instruments were lying and are fixed**: `Tools/impact-cb-table.py`'s newest trace row read `RUNS` and silently became whatever set was newest (the `8c4be56d` row had been reading `6ff2325c` since 02:38 - invisible because those two sets are byte-identical); it now reads each set's archive folder and prints nine builds. `Tools/level-freshness-audit.py` read `Build/Headless/Headless_Data/...` where the player's data folder is `BalanceTrial_Data`, so its headless line printed `-` about the very player it audits. Still open in this lane, unchanged: L1 (5.000114 ms = arrival, the card wants 40-150 ms) and L3 (no active-off switch), both inside `Assets/BalanceController.cs`. Report `Trials/Runs/impact-cb6-report.md` | `.cursor/tasks/BALANCE-ARENA-1.md` |
| bird | BIRD-12: inter-bird contact is torso+head (`BirdCore`); limbs/perch `BirdLimb`. Walk = `walkThrust` on the body. Opt 02.09 flock 50 still the 200 Hz edge; no `GetContacts` | `BIRD-12-core-contact.md`; `opt_bird_code_02.md` |
| plant | TREES T1 DONE 18.09 (oak build f9d594f0, apple 8000f531, instrumentation only): `<label>_joints.csv` per-step per-joint trace, run block = measurements only vs `build:` = constants + rig hash, `tree-const-assert.py` flags constants hiding in a run block; oak bit-identical to pre-T1 (17/17 keys). **T1 apple gap closed 19.09** (build cae2483a): `TreeBase.BindGravityHold` fills `gravityGradient`, so the apple no longer reports 0 on all 15 joints — apple `apple_t1gap_*` vs the pre-edit player `apple_t1g0_*` moves 28/30 run keys nowhere but `wallPerSim`+`sourceFingerprint`, and only the 19 gradient fields move anywhere; oak `tree_t1gap_*` vs accepted `tree_t1_*` build 93/93. Report `Trials/Runs/apple-t1gap-report.md`. **T1-sway 19.09** (build `3f93bfc4`, instrumentation only): the apple's two doubted numbers are now honest — one tip segment chosen once and followed, a settled window (t>=4 s), a per-joint PINNED TIME fraction with the overshoot past the stop, and a WIND-OFF run (`PlantBase.windOffSeconds` + `TreeBase.FixedUpdate` gate + `-appleWindUntil <s>`, 0 = old behaviour). 28 of 28 physics run keys bit-identical to the pre-edit player on hold AND wind 0.12 (`apple_t1sw_*` vs `apple_t1sw_base_*`), oak reproduces the canon. Findings: `swayAmpM` 0.1465 m was a startup transient (settled sway 0.0222 m, below the 0.10-0.30 band); the pinned joint is J08, 68.3 % of the hold samples, 0.674 deg OUTSIDE its own stop, overrun up to 2.066 deg under wind (soft stops); wind off at 4 s -> residual 0.0220 deg from the hold pose, crown tip back within 0.0007 m. Next: T2 diagnosis. Report `Trials/Runs/apple-t1sw-report.md`. **T1-oak 19.09** (build `8e9ed340`, instrumentation only): the same honest windows are now on the OAK (`PlantTrialRecorder` + `PlantTree.windOffSeconds`/`ApplyWind` gate + `-treeWindUntil <s>`, 11 added run keys, 4 added build keys, nothing renamed). `_t1oak-accept.py`: oak base vs post PASS on hold AND wind 0.12 (no run key moved, build block only moves the wind config), apple controls reproduce the accepted T1-sway values to the digit, oak canon bit-identical (0.00103 / 34.17341 / 0.2553246 / pinned 0.20). Gate proof `_t1oak-gate.py`: wind-off vs wind-on identical to 0.000000 deg until the switch opens at t=3.990 s, then they separate. Findings: `swayAmpM` 1.287312 is a transient (stable-tip whole run 1.473969, settled 4-8 s 0.179467 m, inside T3's [0.05H,0.15H] for H=2.332039 m); pinning is 3 of 15 joints (J00 66.1 %, J08 85.2 %, J01 7.5 %), overrun 0.151758 deg; wind off at 4 s -> the motion decays (mean |dAngle|/step 0.00311 -> 0.00070) but the POSE does not return: residual 32.72058 deg, tip 1.368073 m — the trunk sits on its +-16 deg stop and the 180 N.m/rad spring cannot lift the 259.7 N.m/rad weight gradient (T3/T4 input; T3's RETURN TEST fails on the oak today). Report `Trials/Runs/tree-t1oak-report.md`. **T1 close-out + THE T2 FRAME FIX STOOD AND COMMITTED 19.09** (Hermes; commit `c9129bc1`, ONE file `Assets/TreeBase.cs`; player `cb7e4ef6`, gitCommit `9a43a74e`; runs `tree_t1c2_hold/_wind012/_windoff`, `apple_t1c2_hold/_wind012/_windoff` + a repeat): the oak canon `tree_t1oak_*` and the apple's post-edit pair `apple_t1sw_hold/_wind012` are BIT-IDENTICAL to the fresh reruns (run block and build block, and 0 of 1600 `joints.csv` rows differ on each pair), while the pre-T2 `apple_t1_*` pair correctly moves 23 run keys (that pair is the "before", not a target). Stand criteria all met: born-behind 4/15 -> 0/15, `springSatFraction` 0.06620833 -> 0 on every joint (per-joint CSV, J08 0.993125 of 1600 rows -> 0.000000), fingerprint == manifest, rigs unchanged (`dcc15b908146504e` / `b242a8ddd29b0b28`). FOUND this cycle: `apple_t1sw_windoff` does NOT reproduce — it diverges from its own sibling wind-on run (same fp, same player) at row 2, t = 0.01 s, 1599 of 1600 rows — so wind-off evidence is now `apple_t1c2_windoff` (release peak 6.448067 deg, 0.11 deg past the wind-on peak 6.335289; residual 0.022004 deg from the hold pose, crown tip 0.000672 m); the oak's wind-off label is fine. `tree-const-assert.py` fires on the apple pair for three DISCRETE counts (J08 and J01 are the only joints that ever enter the 0.2 deg band, in either run). Report `Trials/Runs/apple-t1c2-report.md`. **T1 RE-VERIFIED ON A NEW PLAYER 19.09** (cycle `T1`, zero source edits, 11 runs, fp `4681f70b`, gitCommit `ac2299ea`, gate "OK at 15:12"): the oak canon `tree_t1d_*` and the apple's accepted pair `apple_t1d_nv_*` reproduce BIT-IDENTICAL on the player the other four streams rebuilt — run block, build block and **0 of 1600 `joints.csv` rows** (oak hold/wind/wind-off vs `tree_t1oak_*`; apple hold/wind vs `apple_t1sw_*`, apple wind-off vs `apple_t1c2_windoff`; a repeat is bit-identical to its sibling), while the PRE-T2 pair moves 23 run keys and the STALE `apple_t1sw_windoff` moves 12 — its fingerprint does not name the player that made it, confirmed a second time. **FOUND: `-visual` is not a neutral flag** — it shifts the recorded trajectory from row 2 (t = 0.01 s) deterministically on BOTH rigs and on BOTH players (apple 1599 of 1600 rows differ, 1.725 N·m in `J00_springTorque` at that row; oak 1231 rows, 5.95e-03 deg, but the oak CONVERGES back to a 0.000e+00 last-row delta while the apple keeps 7.7e-04 deg; the old player's `-visual` label `apple_t1c2_photo_hold` is bit-identical to today's `apple_t1d_hold`), so **a physics label must never carry `-visual`** — compare only within the same switch shape. T2's frame fix still stands (born-behind 0/15 on every run today, no joint at its cap; PRE-T2 4/15 with J08 at the 40 N·m cap in 0.993125 of 1600 rows). Wind off at 4 s: release peak 6.448067 vs the wind-on peak 6.335289, residual 0.022004 deg, crown tip 0.000672 m. Still missed by the apple: pinned 0.06666667, settled sway 0.02222349 m (below the 0.10-0.30 band) and `crownDrop` 0.00667548 m — the T2 fix RAISED it from 0.00265265. Picture `Trials/Runs/apple_t1d_windoff.mp4` (82 frames, 1080p) + `apple_t1d_windoff_photo.png`; report `Trials/Runs/apple-t1d-report.md`; helpers `_t1d-accept.py`, `_t1d-visual.py`, `_t1d-firstdiff.py`. **T1 re-verified 19.09** (apple worker, player `3c4dfc53`, gitCommit `8ce38675`, seven runs, ZERO source edits): every accepted label is bit-identical again — apple `apple_t1e_*` == `apple_t1sw_*` and `apple_t1c2_windoff`, oak `tree_t1e_*` == `tree_t1oak_*`, run block AND build block AND 0 of 1600 CSV rows on every pair; the canon now reproduces across THREE players, the PRE-T2 pair and the STALE label still move (negative controls), const-assert fires on the same discrete keys, and the `-visual` picture `apple_t1e_photo_windoff` is byte-identical (md5) to `apple_t1d_windoff` (frames, mp4, still). The apple brief's job 2 was already landed by the level worker: `Assets/Scenes/TreeApple.unity` plus `GameProcess.cs:500-519` routing `AppleTree` by `treeSpecies`, so it must not be asked for again. Report `Trials/Runs/apple-t1e-report.md`; helpers `_t1e-runs.sh`, `_t1e-accept.py`, `_t1e-photo.sh`, `_t1e-dump.py`. **T1 re-verified on a FOURTH player 19.09** (apple worker, cycle `T1f`, player `fc6471dd`, gitCommit `35c7fbbd`, gate "OK at 19:01", eight labels, ZERO source edits): 11 of 11 pairs bit-identical again — apple `apple_t1f_*` == `apple_t1sw_hold`/`_wind012`/`apple_t1c2_windoff` and == the previous cycle's `apple_t1e_*`, the wind-off repeat == its own sibling, oak `tree_t1f_*` == `tree_t1oak_*` — run block AND build block AND 0 of 1600 `joints.csv` rows on every pair, so the canon now reproduces across FOUR players; the negative controls still move (PRE-T2 pair 23 run keys + CSV from row 1, STALE `apple_t1sw_windoff` 12 keys + CSV from row 2) and `tree-const-assert.py` fires on the same discrete sets (apple 3, oak 1). Picture `apple_t1f_photo_windoff` (41 frames, mp4 865 646 B, still 725 666 B, one whole apple tree, nothing else in frame — checked on the image) is byte-identical by `md5` to the two previous cycles' pictures. FOUND, read-only: `GetHingePinnedFraction` (`Assets/TreeBase.cs:640-655`) counts `|angle - limit| <= 0.2` deg and keeps the MAX instant, so a joint sitting 0.7 deg OUTSIDE its stop is not counted — the trace's per-joint time fractions are hold J01 0.7781 (overshoot 0.6740 deg, outside) and J08 0.6831, wind 0.12 J01 0.8244 / 2.0664 and J08 0.7625 / 1.1959, and no other joint ever enters the band, so "pinned 0.00" can be met while the crown rests on a soft stop; read `maxHingeOverrunDeg` beside it (T4). Wind-off gate proven on the trace: row-identical to its own wind-on sibling through row 799, first differing at row 800 (t = 4.004949 s); motion in the last second 0.004357 -> 0.000923 mean |dAngle|/step over 15 joints (ratio 0.212), residual 0.022004 deg, tip back within 0.000672 m. Report `Trials/Runs/apple-t1f-report.md`; helpers `_t1f-runs.sh`, `_t1f-accept.py`, `_t1f-detail.py`, `_t1f-dump.py`, `_t1f-photo.sh`. **T1g 19.09** (apple worker, zero source edits, player fp `0fd4f288…` gitCommit `73e5f3bd`, gate REBUILT "OK at 20:07"): T1 had no open work, so the cycle is its own acceptance test again — **11 of 11 pairs bit-identical** (run block AND build block AND 0 of 1600 `joints.csv` rows) for `apple_t1g_hold`/`_wind012`/`_windoff` against the canon `apple_t1sw_*` / `apple_t1c2_windoff`, the wind-off repeat against its own sibling, and `tree_t1g_*` against `tree_t1oak_*`; the apple canon now reproduces across **five** players (`cb7e4ef6` -> `4681f70b` -> `3c4dfc53` -> `fc6471dd` -> `0fd4f288`), negative controls move as they must (PRE-T2 pair 23 run keys / 11 added, CSV from row 1; STALE `apple_t1sw_windoff` 12 keys, CSV from row 2), `tree-const-assert.py` FIRES on the same 3 discrete keys, born-behind 0/15 and no joint at its cap. Picture `apple_t1g_photo_windoff` (41 frames, 1080p) is **byte-identical by `md5`, frames included**, to the three previous cycles' pictures (mp4 `d10ec27d…` 865 646 B, still `347e44ce…` 725 666 B), one whole tree, nothing else in frame. The three unmet acceptance numbers are T4/T5 (pinned 0.06666667, settled sway 0.022223 m, `crownDrop` 0.00667548 m). Report `Trials/Runs/apple-t1g-report.md`; helpers `_t1g-state.py`, `_t1g-runs.sh`, `_t1g-accept.py`, `_t1g-photo.sh`. **T1h 19.09** (apple worker, zero source edits, seven physics labels + one picture on the player fp `a4b8dbbf…` gitCommit `2779a3bb`, built `18:12:46Z`, 632 files, buildGuid `d75635ef6b224ed7b03190612f123abf`, gate REBUILT and **"OK at 21:12"** because the player on disk `01d56460` at 20:49:52 was older than `Assets/ArmDrive.cs` at 21:00:25 — another lane's edit — 0 `error CS`, no editor, no lock, plant sources clean against HEAD): T1 still had no open work, so the cycle is its own acceptance test — **11 of 11 positive pairs bit-identical** (run block AND build block AND **0 of 1600 `joints.csv` rows**) for `apple_t1h_hold`/`_wind012`/`_windoff` against the canon `apple_t1sw_hold`/`_wind012` / `apple_t1c2_windoff`, the wind-off repeat against its own sibling, the same three against the previous cycle's `apple_t1g_*`, and `tree_t1h_*` against `tree_t1oak_*` / `tree_t1g_wind012`; the apple canon now reproduces across **six** players (`cb7e4ef6` -> `4681f70b` -> `3c4dfc53` -> `fc6471dd` -> `0fd4f288` -> `a4b8dbbf`), rigs unchanged (`b242a8ddd29b0b28` / `dcc15b908146504e`), `jointColumnCount` 15, born-behind 0/15 (4/15 on the PRE-T2 pair), no joint at its cap in any row — the negative controls move as they must (PRE-T2 pair 23 run keys / 11 added, CSV from row 1; STALE `apple_t1sw_windoff` 12 keys, CSV from row 2, fourth confirmation) and `tree-const-assert.py` FIRES on the same discrete sets (apple 3, oak 1). Picture `apple_t1h_photo_windoff` (41 frames, 5 fps, 1080p) is **byte-identical by `md5`, frames included, to the four previous cycles' pictures** (mp4 `d10ec27dd1a265eba217397e748d24a6` 865 646 B, still `347e44cefeb516f93d60adf2504ab3c3` 725 666 B, sampled frames `00`/`01`/`02`/`40` hash-identical), one whole apple tree centred and fully inside the frame, nothing else in scene (checked on the image). Wind off at 4 s proven again on the trace: row-identical to its own wind-on sibling through row 800, first differing at row 801 (t = 4.004949), release peak 6.448067 past the wind-on 6.335289, residual 0.022004 deg from the hold pose, crown tip back within 0.000672 m, mean |dAngle|/step 0.076571 startup -> 0.000811 (first second after the gate) -> 0.000061 (last second) against 0.000289 wind-on and 0.000021 for the no-wind hold. The three unmet acceptance numbers are unchanged and belong to T4/T5 (pinned 0.06666667 with J08 0.674024 deg outside its own SOFT stop in still air and overrun 2.066446 deg under wind, settled sway 0.02222349 m against 0.10-0.30, `crownDrop` 0.00667548 m against 2 mm; lean 1.166476, base slide 0). Report `Trials/Runs/apple-t1h-report.md`; helpers `_t1h-runs.sh`, `_t1h-accept.py`, `_t1h-photo.sh`, `_t1h-detail.py`, `_t1h-decay.py`, `_t1h-dump.py`. **T3 oak 21.09** (build `40826f66`, ONE change in `Assets/PlantTree.cs`, 68+/11-, one file): order 0 is folded into the static stump — the bole is a picture-only segment whose box is a SECOND COLLIDER on the static body (no joint, no spring, no Rigidbody2D), its children hinge on the static base at the crown base, `NormalizeDynamicMass` keeps the bole in the denominator so the crown keeps its canon masses, `TallyBodies` counts one body fewer and one box per wood piece. Joints 15 -> 14, bodies 16 -> 15, dynamic 15 -> 14, wood boxes 16 -> 16, joints.csv 151 -> 141 columns (1600 rows). `tree_t3oak_hold/_wind012/_wind036/_windoff`: `maxTrunkLeanDeg` 0.000 at wind 0.12 AND 0.36 (was 16.04749) — the bole is provably static; RETURN TEST PASSES — wind off at 4 s leaves 0.1234 deg and 0.003132 m (was 32.72058 deg / 1.368073 m) against 3 deg / 0.046641 m, so T3's stop rule did not apply; `springSatFraction` 0, `baseSlideM` 0, `maxAbsJointAngle` 1.25655 (was 34.17341) / 3.66038 at 0.36, `crownDrop` 0.00180435 (was 0.2553246), `maxAbsSegmentWorldDevDeg` 1.575226 (was 67.55936), `maxAbsAppliedTorqueNm` 1.479578 (was 50.41466). Two acceptance rows open and named: `hingePinnedFraction` 0.07142857 (1 of 14 — J07, the order-1 limb grown 0.7626 deg under its +-26.752 deg stop, clipping its own bend: 21.3 % of the 0.12 samples, 38.4 % at 0.36, overrun <= 0.0029 deg; the apple's T2 stop-ORIGIN class, the oak's stops still come from the grow pose) and the settled `swayAmpM` 0.024689 m (0.0106H) at 0.12 / 0.078261 m (0.0336H) at 0.36 — BELOW the [0.05H, 0.15H] band, since the pre-fold 0.179467 m was the bole's own fold and the bole carried 27 % of the sail area — T4/T5 sizing. Apple canon untouched: `apple_t1sw_hold`, `apple_t1sw_wind012`, `apple_t1c2_windoff` vs `apple_t3_*` BIT-IDENTICAL (run block AND build block, 0 of 1600 joints.csv rows, header identical); `tree-const-assert.py` FIRED on five zero keys, all zero BY the fold (no order-0 joint exists any more): `maxTrunkLeanDeg`, `residualTrunkLeanDeg`, `treeOrder0MaxAngleDeg`, `trunkJointAngleDeg`, `springSatFraction`. Report `Trials/Runs/tree-t3oak-report.md`; helpers `_t3-runs.sh`, `_t3-rows.py`, `_t3-accept.py`, `_t3-pin.py`, `_t3-csv-shape.py`, `_t3-photo.sh`. **T3 apple 21.09** (ONE change, ONE file: `Assets/TreeBase.cs` 96+/17-; the oak's T3 fold lives in `PlantTree.Grow` and `PlantTree` is NOT on the apple's path — `AppleTree : TreeBase` / `TreeBase : PlantBase` are two builders, and `TreeBase.cs` was clean against HEAD — so the apple's cycle was the PORT): `Grow()` `:227` folds `depth == 0` — the bole is created picture-only (`CreateWood(..., livePhysics: false)` -> `InitializeVisualOnly`: no `Rigidbody2D`, no hinge, no spring), its box becomes a SECOND COLLIDER on the static stump (`:261` re-parent with the world pose kept), its children hinge on the static base with the pivot at the crown base (`:277`) while the stop stays in the frame of the wood it hangs from (`:281`); the one judgement call is `NormalizeDynamicMass` `:418` keeping the folded trunk in the denominator (`n.dynamic || n.depth == 0`) — without it the crown is restamped to `totalMass` 28 kg and every gradient passes its stiffness. Counters: segments 15 -> 15 (rig hash **`b242a8ddd29b0b28`** unchanged — the geometry did not move, quoted on purpose), dynamic 15 -> 14, `TreeRigidbodyCount` 16 -> 15, wood boxes 16 -> 16, fruits 20 -> 20, leaf plates 54 -> 54, `jointColumnCount` 15 -> 14, `joints.csv` 151 -> 141 columns. Labels: pre `apple_t3pre_hold/_wind012/_wind036/_windoff/_windoff_b` (NO `-visual`) — another lane rebuilt the player mid-set and `_windoff` died on the swap, so it was re-run on the new player next to `_windoff_b`; the pre set is BIT-IDENTICAL to the canon (`apple_t1sw_hold`, `apple_t1sw_wind012`, `apple_t1c2_windoff`, run block AND build block, 0 of 1600 CSV rows on the wind-off pair). Post: `apple_t3post_*` (five) + oak control `tree_t3post_hold/_wind012` + picture `apple_t3post_photo_windoff` (`-visual`, the picture label only), player fp `6ec67c83…` buildGuid `aad87f54…` built `21:53:26Z`, **every quoted run `sourceFingerprint == build-manifest.json`**. Accepted rows: `maxTrunkLeanDeg` **0** at 0.12 AND 0.36 (was 1.166936/1.166476); `hingePinnedFraction` **0** with `hingePinnedJointCount` 0 and `maxHingeOverrunDeg` **0** (was 0.06666667 / 2 / 2.066446 — J08's soft-stop class is gone); hold `crownDrop` **0** (was 0.00667548 — the 2 mm target now MET); `holdResidualNm` 0.00015342 (was 18.8835); `maxAbsJointAngle` 0.00013638 hold / 0.6035438 wind 0.12 / 1.812001 wind 0.36 (was 5.674024 / 7.066446 / 8.651575); `maxAbsSegmentWorldDevDeg` 0.00010777 / 0.8340149 (was 4.906799 / 6.424713); `maxAbsAppliedTorqueNm` 0.00015604 / 1.755638 (was 20.36688 / 20.55546); `crownComDxM` 0.00721205 (was 0.03471757); `springSatFraction` 0; `baseSlideM` 0. RETURN TEST **PASSES** — wind off at T/2 = 4 s leaves `residualTipDxM` **0.00932419 m** (limit 0.02H = 0.05492) and `residualAbsJointAngleDeg` **0.2963557** (limit 3), was 0.1035979 m / 5.693279 deg; `windOnMaxAbsJointAngle` 6.335289 -> 0.6035438 (a decay toward rest, not an overshoot). Still open: settled `swayAmpM` 0.02079147 m at 0.12 (0.0075H) / 0.06270987 m at 0.36 (0.0228H), BELOW the [0.05H, 0.15H] band (H = 2.762232 m) — the fold removed the bole's own whip, the crown's own sway is what is left: T4/T5 sizing, the oak's reading. The oak did not move: `tree_t3post_hold/_wind012` vs `tree_t3oak_hold/_wind012` BIT-IDENTICAL (run block AND build block, 0 of 1600 CSV rows, rig `dcc15b908146504e`), its `PlantTree.cs` edit untouched. Build block: `build.joints[]` 15 -> 14 rows, the order-0 row gone, and matched by IDENTITY (order + bind) every crown row keeps every number except `kOverStabilityBound` on the two order-1 joints (0.00750651 -> 0.00649 — the pair's other side is static now). `tree-const-assert.py` FIRED on nine zero keys, all nine zero BY the fold (no order-0 joint exists any more, the static bole cannot lean): `maxTrunkLeanDeg`, `residualTrunkLeanDeg`, `treeOrder0MaxAngleDeg`, `trunkJointAngleDeg`, `hingePinnedFraction`, `hingePinnedJointCount`, `maxHingeOverrunDeg`, `worstJointPinnedTimeFraction`, `springSatFraction` — with 24 of 36 run keys still moving with the wind. Not touched: `PlantTree.cs`, `PlantTrialRecorder.cs`, `TreeJointTrace.cs`, `HeadlessTrial.cs`, `AppleTree.cs`, every recipe value, human/birds/dog/yard. No commit. Report `Trials/Runs/apple-t3-report.md`, verifier output `apple-t3-accept-output.txt`, picture `apple_t3post_photo_windoff.mp4` (41 frames) + still; helpers `_t3pre-runs.sh`, `_t3pre-build.sh`, `_t3post-runs.sh`, `_t3post-photo.sh`, `_t3-apple-accept.py`, `_t3-counters.py`. | `.cursor/tasks/TREES-plan.md`; `plant-physics.mdc` |
| artist | bark/leaf placeholder; physics sizes stay | `.cursor/tasks/ART-TREE.md` |
| artist | dog parts in `Art/Dog` (chest…paw); sizes unchanged | `.cursor/tasks/ART-DOG.md` |
| artist | ART-DOG **box look is live**: `DogArt.BoxLook` draws the dog the way `HumanSegment` draws the human — one flat translucent rectangle per bone (head, neck, chest, pelvis, 5 tail, 4 × upper/lower/paw), one dark joint marker, far side dimmed; `-dogCoatLook` rolls back to the plates, `-dogBoxAlpha <f>` tunes the fill (0.5, the human's `Human.cs` alpha; was 0.65 in an earlier pass), `visualSideOffset` 0 like the human with `-dogSideOffset <f>` for the A/B (`HipBridge` / `ShoulderBridge` cover the two joints whose anchor sits off the box edge). `Art/Dog/Palette.cs`, `Dog.cs`. Frames: `Trials/Runs/dog_art1*.png` | `.cursor/agents/artist.md` |
| artist | crow / chicken parts in `Art/Crow` and `Art/Chicken` | `.cursor/tasks/ART-BIRD.md` |
| ai | Insect hunt concept: faction Insects, Forage→Pursue, eat with beak | `BIRD-13-insect-hunt.md` |
| dog | **GET-UP CYCLE 40 dispatched 21.09 19:56 (Hermes) - THE ARRIVAL (arrive slower, or step)**: c39 ran TWO measured rows, both empty of the hold and rich in mechanism: (1) the phases kept + the reach live + the lift faded -> the pose (f2643-f2650, 8 frames, 0.035 s) unmoved, the lift's fade took the card's line 16 frames EARLIER (worse); (2) the four limb hinges' D x4 -> the saturation 0.983 -> 0.934 (the brake acts) and nothing else moved. Conclusion: the 8 pose frames are the tail of the rise's own extension stroke - the CoM arrives +0.095 m rising 2.4 m/s (1.67 m/s up over the push's last 14 frames) and 1.0 m/s forward, the capture margin +0.107 -> -0.046; stiffening saturated channels cannot hold it - it needs to arrive slower or to step. Also found: the hold's release keyed to the plant's clock never releases on a lost pose (handOver 1.000 on 2350 frames, both rows) - any next hold needs a pose-loss exit. The c39's kept items stay in the tree (the hold signal, the phase survival, the reach live, the brake); the lift fade is NOT in the tree. Rows: c39 0931087e (fp 105ffb24), c39b 92de5f44 (fp c3fe3e16, the tree's state, row of record), the quiet 0355abc2 in all three. -> c40 = THE ARRIVAL (the stroke's terminal phase / the front paws step; the c39's own fork). Pre-controls c40: quiet 0355abc2 == c39b_q04, walk 92de5f44 == c39b_w04; law a9b9d4b9 / rec f827ebfb / Dog.cs (the c35 claws draw). Walk lane inert for the dog rows; DOG-4: `DogDrive` writes Walk; no play keys | `.cursor/tasks/DOG-3-walk.md`; `DOG-5-getup.md` |

## THE BIRD AI SHEET FILED (Yury's sheet #2, 23.09) - "B-AI-1" (23:12)

Yury's handwritten sheet ("Задача для профиля по птицам", photo
`F:/hermes-data/bird-tz-ai-20260923.jpg`): the class tree = птицы -> ВОРОН | КУРИЦА (ПЕТУХ) |
ГОЛУБЬ; the crow's states: check for enemies, enemy = a living object of a hostile faction, if
seen - assess strength: a stronger human = keep distance, a WOUNDED human = attack; plus a
note: a common "уровень угрозы" parameter on ALL living objects, to be worked out in detail
later. FILED as `ds-bird-ai-order.md` (binding for the bird lane's AI track): the pigeon is the
week's species target (the earlier sparrow/magpie candidates drop behind the sheet); B-AI-1
lands after B-CLASS-4 (the tree), because the ruler comes first. No brief touched - the order
rides the file + the next round's brief.

## THE HUMAN'S TEXTURES ORDERED (Yury's sheet, 23.09 ~21:05) — ART-H1

Yury's handwritten TZ (photo `F:/hermes-data/art-tz-human-textures-20260923.jpg`, transcription in
`ds-art-human-textures-order.md`): mannequin segment textures as separate elements that CONNECT
in game, overlap accounted; the shin-foot joint must show NO GAPS at any angle; all scenes stay
simple with semi-transparent rectangles; real textures ONLY in scene #1 "Level 1"
(`Level1.unity`); and the campfire must be worked up and graded by Gemini >= 8/10.
DISPATCHED: the art stream's brief rewritten (ART-H1 = the mannequin + the joint overlap; old
brief archived `.bak-2309`); QUEUE: ART-H1 -> ART-H2 (campfire + the Gemini gate) -> ART-H3
(Level1-only real textures). Evidence = frames (the leg through its flexion range; no gap at the
shin-foot joint) + the Gemini score. Lane: view-only, rows bit-identical.

## THE DOG GET-UP CONCEPT ORDERED (Yury, 23.09 ~20:55) - CYCLE 71 (20:35)

Yury: "Собаки совершенно не умеют вставать. Они просто дергаются и катаются на спине.
Необходимо реализовывать концепцию, чтобы они сгибались и лапами цеплялись за землю."
DISPATCHED: dog cycle 71 "the get-up: bend and grip" - manager/dispatch-dogc71.txt (the writer
archives c70's brief then rewrites ds-dog-task.md), wrapper manager/run-dogc71.sh FIRED (waits
for c70's own `done` marker first - two writers never touch the brief at once). QUEUE: c70
(platform edge, in flight) -> c71 (the get-up) -> then the queued c68 7.2/7.3 rears + recorder
columns, then the arena orders. ANCHORS in the round: the 18.09 inverted-heap finding (the tuck
alone cannot right it), the 21.09 grip sweep (nine-times grip made it WORSE - the motion was
paddling; the mechanism is PLANT+PULL), c42's plant 0.79 m behind the CoM = the geometry defect
to fix FIRST. BARS: >=4/5 falls, stance <=3 s, per-paw slip budget, roll 0 after the coil, the
get-up family stays default-OFF + the ablation row in the report; film = 25 fps cinema opening
at the get-up start (the action is sub-second). Requirements file updated the same hour.

RE-FIRE NOTE (H_Manager 23.09 22:09): the first c71 wrapper died at 20:57 in the provider
outage before the brief was written (rc1 brief_not_updated); H_Manager re-fired it - the
writer session is live. Do not re-fire in parallel.

## THE BIRD STREAM IS LIVE - PROFILE + WEEK RUN (20:34, Yury's order)

Yury: "На птиц заводить профиль. Хочу на ближайшую неделю запустить постоянный поток для работы с
их физикой, искусственным интеллектом, работой стаи. Также разнообразие птиц надо увеличивать."
DONE: profile `h_2dgame_bird` created (clone of the fleet base, deepseek-flash, smoke test passed
20:30); lane `bird` added to the runner's case (atomic swap deferred while a dog cycle holds
ds-cycle.sh; the wrapper pins DS_PROFILE=h_2dgame_bird meanwhile, swap waiter running); wrapper
`deepseek-bird-cycle.sh`; cron `deepseek-bird-15m` (job 749c3d547ab0, `15 * * * *`, no_agent,
deliver origin - walk :00, dog :30, bird :15); brief `ds-bird-task.md` = current round B-NAT-0
"the ruler"; requirements `bird-requirements.txt` (binding; carries the diversity track: seeded
per-individual jitter first, new species - sparrow/magpie/pigeon - with the class split); plan
`ds-bird-strategy-order.md`; video focus gained the token "bird"; FIRST FIRE IS RUNNING (worker
`--profile h_2dgame_bird` pid 1716, ~20:33). Sunset: the week ends ~30.09 - re-decide then.

### B-NAT-0 STATUS - FIRST FIRE DIED IN THE OUTAGE (H_Manager, 23.09 22:15)

The 20:33 first fire ran 24 min (hit the 200-turn budget) and its wrap-up died in the
20:57-21:30 provider outage: the recorder edits landed (BirdFlockDrive / BirdTrialRecorder
+ the HeadlessTrial hooks, 20:45-20:46), no report was written, the brief stamp is consumed
(1790184699) so the stream sleeps. NEEDED: re-run B-NAT-0 or advance to B-LAND-1 per the
strategy order; nothing else blocks the lane.

## THE BIRDS' STRATEGY IN - OPUS REVIEWED THE CLASS (23.09 ~20:25)

Yury granted one Opus run for the birds ("пусть проанализирует ии наших птиц и их физику и
предложит стратегию... цель - максимально естественное поведение"). RUN DONE (session
20260923_201425_3e794b, 6m59s; packet opus-birds-packet.md; report filed at
F:/hermes-data/opus-birds-review.md; quota line 23.09 20:14 - the day's single slot used). HEAD
FINDINGS (Hermes-verified where checkable): landing = a DROP (Sit written 0.5-2 m up;
glideFraction 0.026; landedUpright false); the FLOCK NEVER LANDS (bird4 landCount 0/20 s); the
wander is a visible METRONOME (Sit->Walk->Peck->Sit->Fly every ~5.8 s); the bird3 film is 66%
dead frames (100/151 identical - verified); species logic lives as if-branches in the 8636-line
controller (chicken 56 / woodpecker 43 / crow 98 mentions - verified); the hunt's snaps are the
debt; blink via GetHashCode + Time.time brains are latent determinism risks. THE PLAN: rounds
B-NAT-0 (the per-member ruler + the bird onset rule + film framing), B-LAND-1 (real
approach/flare/touchdown), B-TAKEOFF-2 (leap + demand-driven cadence, kill the sinusoids),
B-TURN-3 (bank-arcs + flock separation), B-CLASS-4 (species out, urges replace clocks); the
evolutionary arena LAST (jitter first). ORDER: `F:/hermes-data/ds-bird-strategy-order.md`
(binding; the bird lane's next rounds, B-NAT-0 first). The доклад went to Yury same evening.

## OPUS PLAN - TODAY NONE, TOMORROW ONE (Yury 23.09 ~19:55)

Verbatim: "сег нет, завтра один прогон опусом." Plan file: `F:/hermes-data/_cost/opus-plan.md`.
Today no Opus (none ran since the five sessions of 22.09 - walk2/walk3/getup-fall/arena + one
earlier; the arena one is the review whose findings are already landed in the concept and the
arena rounds). Tomorrow ONE run via `scripts/opus-guard.sh` (the guard writes the quota line; no
hand-written entries in `_cost/opus-quota.log` - the counter would block its own run). The packet
to prepare: the arena's bars round (walk c58) - the new selection reviewed - unless Yury names a
different subject. Opus stays: <=1/day, on his word, paid from his wallet.

## THE ONSET RULE - THE FIRST SECONDS DON'T COUNT (Yury 23.09 ~18:25)

Yury, verbatim: "Если мы выполняем эволюцию для походки, и первые пять секунд наши люди стоят, то
я считаю, что первые пять секунд должны не учитываться." ADOPTED FLEET-WIDE as of now: the walk
protocol's quiet stand (ArenaRunner.WalkOnsetSeconds = 5 s in the arena; the walk trial's own
walkOnsetSeconds, 3 s on the current line) is excluded from (a) the arena's fitness window - it
starts at the walk onset, and a body that never starts moving gets no distance credit; (b) the
acceptance arithmetic of the walk rounds; (c) ALL delivered films - every film is cut from the
onset, never from the spawn (this also explains the films that read "человек просто стоит": they
opened on the protocol's stand). Written into: `F:/hermes-data/manager/dispatch-c58.txt` (the live
arena round) and `F:/hermes-data/ds-walk-take-order.md` (the film pipeline). If the shortened
post-onset window is too short for the gait, the measure extends so the post-onset window is a
full 15 s - with the same-seed rerun repeated.

## THE ARENA'S BARS ORDERED - "ЛЮДИ ПРОСТО СТОЯТ НА МЕСТЕ" (Yury ~18:10)

Yury, after running the human arena himself: "почему их ходьба намного хуже чем у нашего
эталонного человека?" then «На видео люди просто стоят на месте.» One root, named with his own
session's numbers (arena_play_s1, 2627 bodies): median 2.15 m per 15 s (~0.14 m/s stand-drift);
median lift 0.179 vs the accepted 0.20; placement 0.035 vs 0.018; the arena's film reads 1/10 to
the reviewers. The raw material exists (body 675 already matched the accepted cycle's own numbers)
- the RANKING is what fails: the fitness still pays distance first. ROUND FIRED: walk c58 "THE
ARENA'S BARS" (dispatch-c58.txt written by Hermes; run-dispatch-c58.sh mirrors the manager's
wrapper; started 18:1x, waits for the c57 worker that took the lane at 18:05 - the c57 worker has
already absorbed ds-walk-take-order.md per its own log). The round: the line's bars as the arena's
fitness (time-to-fall primary, the walk census WITH a lift clause so a stand-shuffle cannot rank,
the anti-back filter, distance as last tiebreak); numbers + HUD over the people for Yury's runs
(the dog arena's c69 pattern); films for Yury from the accepted line only; evidence = a >=180 s
run (arena_c58) + same-seed rerun + the walk-share before/after + a film with the HUD. The rank
still only proposes; the line decides. NOTE for the record: dog_c69vis.mp4 was posted to Yury
FIVE times (14:57..16:01) despite the md5 rule, and c6w57_gene531_walk_cin twice (16:49, 17:05) -
the reporter's dedupe needs a second look (the make-preview re-encode changes bytes).

## THE FILM SHOWED A STANDING MAN - THE SUBJECT, NOT THE BUILD (Yury 23.09 ~17:35)

Yury: "на видео человек просто стоит." Both delivered films had real causes, both now named:
(1) `c6w57_gene531_walk_cin.mp4` (posted 16:49 + 17:05) = the round's take of the CROSS candidate
gene531: survives 40 s, never falls, 45 swaps - by STAND-SHUFFLING (swingFootGrounded 0.73, no
lift; reviewers Grok 3/10 / MiMo 5/10; my frame check of the delivered cut reads standing). The
take's subject is not a walk. (2) THE AUTO FILM FLOW: walk-video-pulse rendered the line WITHOUT
the accepted parameter set - `F:/hermes-data/walk-landed-line.txt` still carried only the c38-era
tail flags; the c52 gene landing never reached it. Controlled pair on the current build: without
the set 6.635 s / 5 swaps; with it 9.36 s / 9 swaps (== c6w57pre). THE FILE NOW CARRIES THE FULL
ACCEPTED LINE (Hermes, 17:40). A corrected film for Yury was cut from a fresh accepted-line render
(c6w_g92vis, 9.36 s / 9 swaps, 805 frames, cut n90..225 at 20 fps = 5.55 s; delivered). ORDER for
the walk lane: `F:/hermes-data/ds-walk-take-order.md` (films for Yury come from the accepted body;
the census gets a LIFT clause so a shuffle cannot count as walking; the fence re-base must be
stated; the cinema fps must be derived from the real frame count). The same "standing body wins
the bar" disease the arena showed today - named once, fixed at both ends.

## C69 LANDED + VERIFIED LIVE - THE DOG GENOME IS 41 GENES (16:22 done; ~16:35 checked)

The round Yury asked for closed rc=0 (started 14:25:45, done 16:22:32; session
20260923_160338_cc2fcc, 18m51s, "Expand dog genome from 15 to 41 genes"). Report:
Trials/Runs/dog-cycle69-genome-expansion.md. FACTS: the genome 15 -> 41 genes in 6 families
(posture, limbServo, flex, gait, trunk, pad; THE FRONT LIMB'S OWN P AND D in the box - c63's named
object), boxes = the law's own clamps, reference scales FROZEN, law md5 unchanged; the shipped
champion sits inside every box with 4 named edge positions (lumbarRestOn at the switch, stance
knee/elbow at the cap, swingLiftFrac at the floor). THE CEILING BROKE FIRST TIME SINCE c64: best
travel 11.766260 m (body 376) vs the 7.636 m baseline (+4.13 m), pre-control `dog_c69pre`
re-confirmed 7.636246; determinism proof (same-seed rerun, best=417@16.19558, same rows/ref-trace
bytes); effective population watched (best-quartile mean 61 bodies, but only 2 distinct root
ancestors - the lineage collapse is named as the next lever). VISIBILITY for Yury: number over
each dog + a HUD line. VERIFIED BY MY OWN LIVE RUN (dog_playt2, windowed, 90 s): DOG_ARENA_BEGIN
box=OK, frame check at t44 (labels over the dogs; HUD top-left, readable: "t 44.0s spawn 87 meas
58 best 6.30m id 27"), DOG_ARENA_DONE best=82@14.03607, spawned 179/measured 149, lost=0/0, clean
exit; the test's shots were removed. HIS LAUNCHER NEEDS NOTHING - the Desktop "Арена собак.cmd"
reads the new build fingerprint at every run (manifest now 1f1657f077a8ac99...). NEXT LEVERS per
the round: the lineage collapse, then the recorder columns; the rank still only proposes.

## WHY THE ARENA'S HUMANS WALK WORSE THAN THE CHAMPION (Yury's question, 23.09 ~14:45)

Yury: "почему я только что погонял арену с людьми а их ходьба намного хуже чем у нашего
эталонного человека?" - ANSWERED from his own run's rows (arena_play_s1, 2627 measured bodies):
the MEDIAN body walks 2.15 m with lift 0.179 / landShare 0.583 / placement 0.035 / lead 0.213 -
against the accepted cycle's 0.20 / 0.63 / 0.018 / 0.229 (placement twice as loose); the field is
the average draft, not the champion. THE REASONS, measured: (1) the arena's fitness pays
distance in a 15 s window with a small survival bonus - the softness terms (jerk, foot-touch
speed, knee damping, variability) are NOT in it; the rounds that build them (54/55) are in
flight, so selection steers to "who travelled farther", not to a pretty gait; (2) the champion
was accepted by the LINE's strict bars, not the arena's; the arena only proposes - and it already
matched the champion's gait numbers once (body 675: lift 0.2034 / landShare 0.6293 / lead 0.2286 /
placement 0.01759, yet 9.4 s and backwards on the line); (3) the reference body (the verified
genome) spawns FIRST in every run (id 1, ReferenceChildKind 3) - the rest of the field is
drafts at various ages. The fix already ordered: line bars as the arena's fitness + the genome
expansion (humans and dogs). Watch the arena by its BEST bodies; the champion you see in videos
is the top of the line, not the population.

## THE C69 ROUND FIRED - THE DOG GENOME EXPANDS (14:25, by Hermes on Yury's word)

Yury asked (msg ~14:2x): "собак можно запустить с новыми параметрами расширенными они?" - the
answer at that moment was NO: DogArenaGenome.cs still carried the 15-gene list (md5 73a28512f506);
c68 (THE LAYOUT) had closed at 13:35 rc=0. So the expansion round was FIRED, not waited for:
`F:/hermes-data/manager/dispatch-dogc69.txt` written (the round = ds-dog-genome-order.md made
operational: the ~30-40-gene genome with its box audit, one-family mutations, clamp-pressure log,
FROZEN scales, the c65 v2 fitness; plus the labels+HUD for Yury's runs; the evidence bar names the
7.636 m baseline), `run-dogc69.sh` adapted from c68's wrapper, started marker 14:25:45
(`dogc69.started`), the brief-writer session in flight (dogc69-brief.out live), then the standard
dog cycle runs (ds-cycle.sh dog; its own lock + the brief-seen guard). The manager's hourly dog
fires remain the safety net; the recorder's `dog_play` label rows (Yury's own runs, 90 measured,
best 7.636 m) are his session data - read-only for the lanes. When c69 lands, Yury's Desktop
launcher picks the new genome up at spawn-time - nothing for him to change.

## THE C69 ROUND CLOSED — THE 41-GENE GENOME BROKE THE CEILING (dog lane, 23.09 ~15:2x)

THE GENOME EXPANSION LANDED: `Assets/DogArenaGenome.cs` 15 -> 41 genes (6 families: posture /
limbServo / flex / gait / trunk / pad, plus the legacy `paw` pair; every box = the law's own
`-dog*` accept range read out of `DogStanceController.cs`; the champion-inside-box audit rides the
run JSON - 5 violations, all switches or law caps at a box edge, disclosed; the two D-gain floors
repaired 0.05 -> 0.0 so the shipped 0.30 sits at 0.150). `Assets/DogArenaRunner.cs`: one family per
child p=0.7 / whole genome 0.3 (measured 192/96 of 288 children), the `*P`/`*D` pairs move as one
unit, coarse -> fine jitter schedule (0.05-0.10 -> 0.03-0.05 when the best-quartile mean rises
< 0.05 m over 4 checkpoints; the run entered FINE), per-gene clamp-pressure log (244 of 5249
component draws clamped; the hot four: `pawComVelDeadzone` 0.421, `pelvisPGain` 0.328,
`pelvisTargetTilt` 0.321, `lumbarRestOn` 0.280), effective-population counter, and THE VISIBILITY
for Yury: a number over every dog + a HUD line (`t / spawn / meas / best id`), TextMesh only,
built only when a camera exists. THE RUN that measured it - `dog_c69arena`, the launcher's own
protocol (seed 1, 240 s, nographics, spawn 0.5 / measure 15 / cull 30): 474 spawned / 444
measured; **best travel 11.766260 m (body 376) against the 7.636 m ceiling = +4.13 m** (the c64 /
c65 / c66 / `dog_play` ceiling is broken; the pre-control re-read 7.636246 m on the entry build);
best FITNESS 16.195580 (body 417, travels 10.11 m, stands). The winner's own genome: tall and stiff
(`heightP` 159.7 of 180), the front limb's servo driven deep (`kneePGain` 10.7 of 12, `hipPGain`
4.39, `walkKneeP` 6.70), stride envelopes wide open (`walkStanceSweepDeg` 68.4 of 70,
`walkKneeFlex` 57.2, `walkHipFlex` 12.3), the trunk coupling ON (`lumbarPelvisP` 2.42,
`lumbarRestOn` 0.95), the D gains crushed (`chestDGain` 0.0073, `hipDGain` 0.080), ground hold
long (0.072 s), rear lag 0.127. PROOFS: the same-seed rerun is byte-identical (rows md5
`5781cb13e0e0692d1d2c68ad6c332064`, ref-trace `90c4011e4358e5ce9a930ebb8d621697`); the identity
holds across FIVE rebuilt players of the round (the visual-layer fixes are provably visual-only);
body 1 reproduces the
ENTRY build's pre-control BIT FOR BIT (`90c4011e…` both, 6001 rows) - the expansion is provably
neutral for the shipped champion; the shared `dog-arena-param-log.csv` is append-only (the 15-gene
snapshot 353 249 B is a prefix of the current file; its header stays the 32-field 15-gene header
while rows now carry 58 - readers key by NAME, `impact-json-lint.py` reports the log BROKEN on its
header==row rule, expected and disclosed). VISIBILITY CHECKED ON THE FRAME: 60 labels + the HUD on
`dog_c69vis` (30 s, 376 frames 1080p, `Trials/Runs/dog_c69vis.mp4` md5 `9582c6f3a0bda5c0bc10517f6adda14a`, still
`shot_dog_c69vis_100.png`), whose ref-trace is IDENTICAL to the nographics run - the visual layer
cannot move physics and the determinism survives it; FOUR takes were spent learning the visual
layer's own traps (the TextMesh anchor is not the mesh's corner; the batchmode screen's aspect is
not the frame's; the view's corner is the CAMERA's position +- halfH; and THE ARENA'S ROOT OBJECT
STAYS AT THE SPAWN POINT, so a label attached once piled into the spawn column - measured as a
static cream blob at x ~ 670-708 in every frame), now fixed by `camera.aspect = 16/9` in batchmode
+ HUD placement from its DRAWN bounds + each label re-anchored from its own chest every tick. The
number was read off the frame (a pale-yellow id above a walking dog's back) and the pixel census
counts 1336-2463 label pixels in 10-19 clusters per frame, each riding a body. LAW UNTOUCHED
(md5 `ca4104613fe431c9ab175ae99735d189` / `cb58f580dce338c1b9ab3b0dff64b0f1` /
`f827ebfbfaba5c1c0645f12a42d9e9f6`, open == close). Yury's Desktop `Арена собак.cmd` reads the
genome at spawn-time: he re-runs it, nothing to change. Report
`Trials/Runs/dog-cycle69-genome-expansion.md`. NOT landed: promoting the arena's crown into the
shipped defaults (it would move reference rows - its own landed KEEP). NEXT LEVERS, named by this
run's numbers: the lineage collapse (the best quartile's 61 bodies descend from 2 root ancestors -
add blood) and the four high-clamp genes whose optima sit ON the box edge.

## THE HUMAN LAUNCHER + THE DOG GENOME EXPANSION (Yury, 23.09 ~15:00)

Yury: "собак прогнал, результата особо не было, увеличь перечень изменяемых параметров собак.
Сделай такой же запуск для сцены с людьми, удобно." THREE ITEMS, all moving:
* THE HUMAN ARENA LAUNCHER DONE + TESTED: `C:/Users/Admin/Desktop/Арена людей.cmd` - the same
  windowed live pattern as the dog's (`-arena 1 -arenaSeed 1 -arenaSeconds <SECONDS>
  -arenaInterval 0.5 -arenaWallLimit 7200 -visual -shotRate 0.2`, fingerprint read + empty-FP
  guard, log grok-burn/arena_play.log; his sessions append label `arena_play` to
  Trials/Runs/arena-param-log.csv). Tested with a 50 s run (`arena_launchtest`: ARENA_CSV written,
  51 shots, clean exit; test shots removed). SECONDS=3600 editable at the top of the .cmd.
* THE DOG GENOME EXPANSION ORDERED (`F:/hermes-data/ds-dog-genome-order.md`): the numbers back
  Yury's eye - his own run `dog_play_s1` peaked at 7.636 m, the SAME ceiling as c64 (7.483) and
  c65/c66 (7.636); the 15-gene genome is exhausted. Order: grow `DogArenaGenome.cs` to the dog's
  full lever set (~30-40 genes: the walk clock/stride, the stance/posture, the trunk fold, THE
  FRONT LIMB'S OWN P/D (c63's named next object), the pad/dwell levers; the reference scales stay
  FROZEN; the law files read-only), carrying the human concept's rules (boxes from the clamps,
  the champion-inside-box audit, one-family mutation p=0.7, clamp-pressure logging, the c65 v2
  fitness) + VISIBILITY for Yury's runs: number labels over the dogs (ArenaNumberTarget pattern)
  and a HUD (time/spawned/measured/best) - visual only, determinism preserved. Runs after c68.
* His launcher `Арена собак.cmd` picks the expanded genome up automatically (no new file).

## THE SELF-RUN DOG ARENA FOR YURY (23.09 ~13:45) - THE DESKTOP LAUNCHER

Yury: "подготовь эвалюционный стенд с собакой, хочу погонять сам." DONE + TESTED: the Desktop
file `C:/Users/Admin/Desktop/Арена собак.cmd` launches the dog arena LIVE IN A WINDOW (windowed
player = no `-batchmode`/`-nographics`, keeping `-visual -shotRate 0.2`; fingerprint read from
`Build/Headless/build-manifest.json` via powershell in the .cmd; log
`grok-burn/dog_play.log`). Parameters at the top of the .cmd (SECONDS=3600, INTERVAL=0.5,
MEASURE=15, CULL=30). Verified by a real test run `dog_playtest` (windowed, D3D12/RTX 3060,
DOG_ARENA_BEGIN ... box=OK, 100 sim-seconds, clean shutdown, 101 shots - shots deleted after the
test; its 198 rows stay in the shared append-only log tagged dog_playtest). HIS sessions write
label `dog_play` and append to `Trials/Runs/dog-arena-param-log.csv` - treat them like the human
side's `arena_play_s1` (the owner's own play data; the lanes read, never rewrite). NOTE: the
launcher does NOT take `trial.lock` - if a round is running, both may coexist (his is visual
play; a collision stretches the lanes' wall time only). FIX 14:20: the first .cmd shipped with LF endings + a fragile for/f-powershell fingerprint read -> the player exited on TRIAL_PROVENANCE_FAILED (Yury: "не запустилась арена"). Rebuilt with CRLF + `set /p FP=<%TEMP%\dogfp.txt` + an empty-FP guard; verified end-to-end (DOG_ARENA_DONE label=dog_play spawn=0.5 best=19@13.84196, wall 61 s, clean exit). OPTIONAL polish named, not done: number
labels over the dogs (the human arena has them; the dog arena draws none) - add on Yury's word.

## YURY'S EYE: THE ELBOWS + THE HEAD (23.09 ~09:30) - THE FIRST NAMED GAIT FAULTS

Watching the accepted walk take (`c6w52_gene92_walk_cin`), Yury named two faults: the elbows
barely flex ("руки почти не сгибаются в локтях") and the neck jerks fore-aft with the head
flopping ("шея очень активно дергается вперед и назад, голова болтается"). Recorded in
`F:/hermes-data/ds-walk-cycle55.md` as the instrument's FIRST two named joint clauses: the ELBOW
(the angle's flexion range over the walk + its phase vs the same-side hip; suspects to read:
`-poseArmSwing` 5.06 deg in the current genome, ArmDrive's `walkElbowMid` 12 /`walkElbowAmp` 16,
the `-walkArmShoulder/-walkArmElbow` pair - name which path owns the elbows) and the NECK/HEAD
(the neck's relative angular velocity/jerk + the head's fore-aft displacement relative to the
chest; suspect: the vestibular upright loop for Neck/Head, PoseDriveController ~1997-2018).
Report-only in the instrument round; then the FIRST two changes (an elbow walk cycle with phase;
a damped head stabilization), one at a time, judged by Yury's eye on the next take.

MEASURED 23.09 ~12:15 (the c55 addendum pass; `Trials/Runs/walk-cycle55-the-softness-instrument.md`
§1d, grids `c55-out-c54_grid.csv` et al, scratch): the ELBOW'S WRITER IS THE ARMDRIVE'S WALK SWING
(`armSwingLevel` 1.0000 on 100 % of the accepted window; the joints track its emitted targets at
corr 0.9869/0.9975, 10-20 ms); the flexion range is 39.9 / 61.2 deg, and the elbow is its own
shoulder's SLAVE (corr +0.998/+1.000, slope 0.73 deg/deg — one rigid pendulum, no cycle of its own).
The pose path's own walk-arm block (PoseDriveController.cs:1796-1799, its whole box [-32,-20];
`-poseArmSwing` 7.742455 on this row) is NOT the writer — named and cleared by range. The NECK/HEAD:
the loop in force IS the pose path's (ApplyWorldUpright, P 1.5 / D 0.4, target 0, 15 N·m) and it does
NOT hold: the neck's WORLD tilt spans ±31 deg (the torso's own sd is 2.08), the joint sits within
0.5 deg of a ±30 stop on 11.86 % of the window's frames (16 holds >= 50 ms, worst 0.30 s), the head's
fore-aft excursion over the chest is 183 mm, headY 104 mm — step-locked at 1.27 Hz. No source edit,
no trial; the two changes above are the next round's, in that order.

## THE GOAL ORDER: SMOOTHNESS NOW, SPEED LATER (Yury, 23.09 ~09:00)

Yury: "сейчас цель - добиться плавности а потом уже будем стремится к целевой скорости." THE
PRIORITY IS SET: the live objective = a smooth, soft, natural walk (the c55 softness instrument
and the changes it judges); the TARGET SPEED chase is parked - no round quotes speed/distance
gains as progress until the softness bar holds. Queue: c54 (in flight) -> c55 (the instrument) ->
the first smoothness change (the staircase/jerk lever). The arena's fitness keeps its current
shape; the softness terms join it only after the shape is right. The dog gait order follows the
same logic (naturalness first, cadence later).

## WALK RENDERS = SIMPLE SCENE ONLY (Yury, 23.09 ~08:55)

Yury: "ролика ходьбы гоняй в симпл сцене, не не нужны сейчас лишние элементы в игре." THE RULE
(standing, all lanes): every video/evidence take of the WALK (and the arena's runs) is rendered
in the SIMPLE scene - the trial's own ground + sky + the human, nothing else. No decorations
(trees/grass/campfire/night/day cycle/clearing elements) in walking videos, and no new game
elements added for evidence's sake. The walk lane's own vis path already complies (the trial's
`-visual -walk 1 ...` scene); the offending take was the level designer's `grnd_after_walk.mp4`
from the decorated clearing capture path - that path stays for SCENE evidence, never for the
walk's. The reporter keeps sending only simple-scene takes; the designer's future ground
evidence = the simple walk path (the trial's own scene) or stills of the scene itself.

## THE GROUND AUDIT'S FINDING + THE RUNWAY ORDER (23.09 ~08:40) - THE REAL EDGE IS CODE

The level designer finished (report `Trials/Runs/level-ground-ext-report.md`, session
20260923_075825_3a26aa; evidence `Trials/Runs/grnd_before_*` / `grnd_after_*` - 5+5 stills and two
24.0 s 1080p walk takes, verified on disk; scenes md5 6f64b33e -> 05557d10 = BalanceArena only;
ONE build PREVIEW_BUILD_OK scenes=13 errors=0). RESULT: in the scenes the walker reaches the
CAMERA's frame, not the ground's end - and the ground the body ACTUALLY reaches is the headless
trial's own floor (20 m, ~10 m of runway), which is CODE: the accepted c54 row walked off it and
free-fell to -660 m. Scene-side fix landed inside the lane's rules: BalanceArena groundMeters
40 -> 400 (collider + slab + grass + sky grew together; Clearing/WalkArena untouched).

THE RUNWAY ORDER (`F:/hermes-data/ds-walk-runway-order.md`) rides the game line's queue: extend
the trial's own floor (20 -> 200 m, the arena's house scale; the dog scene's deliberate 20 m
stays unless the dog's own rows reach its edge), audit which recorded rows ever touched the old
10 m edge (c54's free-fall row did), prove the never-reaching rows byte-identical by md5 (the
primary regression fence), re-measure the edge-reaching rows as walks on the new ground, and widen
the EVIDENCE camera (ImpactArena.FrameCamera - capture only, never the comparators) for a 20-30 s
take of a walker crossing the old edge and continuing. The lane's KEEP discipline applies.

## THE GROUND EXTENDS (Yury, 23.09 ~08:15) - THE LEVEL DESIGNER'S ORDER

Yury: "замля слишком мала пусть дизайнер уровней расширит землю, люди уже доходят до края." Order
on disk: `F:/hermes-data/ds-level-ground-order.md`; the level designer (h_2dgame_leveldesigner)
kicked 08:20 (proc_390c9a69ab4c, the run log grok-burn/level-ground-run.log). Scope: (1) audit
EVERY place a human walks - each ground's current size, the walker's reach, whether the edge is
actually reached (BalanceArena first; the yard's 400 m path as the house scale); (2) extend
scene-side only - NO code changes (the trials' ground sizes are code-driven and the rows of record
stay bit-identical; the dog arena's 20 m is deliberate - a bigger ground re-clips the contact
manifold at f1765); (3) evidence: audit table, before/after stills, a >= 20 s 1080p walk video,
the buildGuid + the scenes' md5s. If a reached edge is the CAMERA's frame (walkers exit the
view), the lane reports and proposes rather than inventing a camera system. The lane's rule zero
holds (no physics/control files; build from bash only, never while Yury's editor is open).

## THE DOG'S GAIT GETS THE HUMAN'S STEP CYCLE (Yury, 23.09 ~08:00) - THE MECHANICS REVIEW ORDERED

Yury: "я доволен эвалюционным подходом, у человека прогресс очень явный, над собаками работай,
может стоит пересмотреть механику пока выглядит не естественно, движение ног и циклы шагов
можно подсмотреть у человека и использовать по возможности и у собак." Order on disk:
`F:/hermes-data/ds-dog-gait-order.md` - the dog lane's next mechanics round (after c66): measure
the dog's own clock (walkPeriod 0.40, walkDuty 0.38, the diagonal trot, per-limb duty/swing/lift/
landing offsets) against the human's accepted cycle's SHAPE (the c6w52_gene92_walk surface: land
share 0.63, comLeadSwap 0.229, placementMargin 0.018, the soft plant -14/0.09, the living
variability), build the mismatch table, then ONE change at a time through the dog's full KEEP
(candidates named: the duty/phase layout - a 4-beat lateral sequence at duty >= 0.6 vs the 2-beat
0.38 trot; the swing's descent shape; the landing ahead of the CoM; the soft paw plant; the
variability last). The look is judged by the softness instrument's three laws adapted to the
dog's limbs (report-only first); the 25 fps cinema take is the eye's witness; the gait's params
join the dog arena's genome after the shape is right. Borrow the SHAPE, never the human's timings.

## THE SOFTNESS BAR (Yury, 23.09 ~06:30) - THE LINE'S NEW QUALITY GOAL

Yury: "человек уже ходит и это результат, однако есть куда двигаться, мы хотим добиться плавной
мягкой походки человека, более естественной." The line's bars now include HOW the walk looks:
smooth, soft, natural - not only fall-time/travel. WALK CYCLE 55 written and queued after the c54
(the arena's fitness number, in flight): the SOFTNESS INSTRUMENT - the accepted row's own
jerk (per joint, with the reversal frames and the clamp-staircase count), the contact's softness
(the touchdown speed, the knee's 100-ms absorb, the CoM's drop rate) and the cycle's naturalness
(the step variability, the arm counter-swing's phase, the pelvis bounce, the lean variance) -
report-only, no controller change, the numbers first so the next rounds argue about the law, not
the adjectives. Then: one change at a time judged by the instrument (a smooth command filter,
the plant's absorb, the arm phase), and finally the softness terms join the arena's fitness as a
calibrated penalty - after the c53/c54 selection work shows the generations turning over.

## OPUS ON THE EVOLUTIONARY CONCEPT (22.09 ~23:00) - THE PLATEAU IS THE SELECTION'S FAULT, IN THE CODE

Opus (h_2dgame_opus, 3m14s, read the concept + ArenaGenome.cs + ArenaRunner.cs + ArenaHumanAgent.cs
+ the log's head; its own naming "Ревью эволюции арены"). THE FINDINGS, file-anchored:
* (a) "Defaults = box centre" is FALSE, refuted by the code itself: ArenaGenome.cs:74 Defaults[8]
  = -0.45 = Min[8] - the SHIPPED value sits ON the edge; jitter is one-sided, half the steps clamp
  back. The rule must be: default INSIDE the box, (default-min)/(max-min) in [0.15, 0.85]; today
  gene 8 (stepMinMeters - the very gene 6r widened for a reason) violates it.
* (b) The plateau is NOT box exhaustion - it is selection-signal exhaustion, three code causes:
  the tournament runs against the CUMULATIVE measured pool (ArenaRunner.cs:479-486), ~580 bodies
  by t=300, quartile 145, TournamentSize=3 -> near-zero selection pressure; the elite comes from
  the whole pool (BestMeasured, 461), so the 45-s champion rules to the end; EliteEverySpawns=8 +
  the twin copy (324-336) eats 2 of every 8 spawns = 25% budget on the determinism proof, plus 25%
  fresh blood = HALF the population never searches.
* (c) The six tiers are not the right boundary: T2/T3 are mutually exclusive by construction while
  PoseDriveController writes the joints alone (ArenaRunner.cs:70-73) - T3's capture genes are dead
  in this configuration; T6's are off by default = run config flags, not genes. The true partition:
  ACTIVE (mutate: today's 13 + the pose law's balance gains + the walk-lift family + the arm
  balance gains = ~35 max that 600 bodies in 300 s can chew) vs FROZEN (log, don't mutate: all T3,
  all T6, the reference-scale calibrations errorReferenceDegrees/speedReferenceDegPerSec/
  maxBalanceSignal - mutating scales breaks every other gain's meaning). Drop from ACTIVE:
  armSwingDeg (visual swing, the arm balance gains already cover the lever).
* (3) THE FITNESS must measure what the line judges: fitness = distance_at_min(15, fallTime) +
  0.4*min(fallTime,15) - 3.0*[fallTime < 15]; plus a hard pre-filter: distance15 < 0.3 m or a
  non-monotone trajectory (ArenaHumanAgent.cs:209-216, x rising on >=70% of samples) is out of the
  quartile entirely. fourPawFraction does not belong in the human formula (two formulas, explicit).
* (4) Mutation policy: mutate exactly ONE family per child at p=0.7 / the whole genome at p=0.3
  (at 35 genes uniform jitter everywhere = a random walk in 35-D, inheritance unmeasurable); the
  schedule's trigger must be the best-quartile mean (the total mean CANNOT rise while 50% of the
  population is blood+elites); drop the 15% dominance cap - the real problem is the opposite
  (25% random) - measure EFFECTIVE POPULATION instead: distinct ancestors in the best quartile,
  need blood if <5, selection broken if >30; LOG clamp pressure (ArenaGenome.Clamp 167-176 eats
  steps silently - today one cannot tell "gene at optimum" from "gene stuck against a wall").
* (5) THE ONE CHANGE (its pick): the selection pool as a SLIDING WINDOW (~last 120 measured),
  tournament 5 of the best THIRD, elite copies 1 per 24 spawns instead of 8 - returns ~20% of the
  budget and turns the 900-body blob into ~7 generations; the plateau at 200-300 s is exactly the
  moment the cumulative pool stops giving a gradient. Second: the time-to-fall fitness. Third: the
  ACTIVE expansion to ~35 - only AFTER the first two (more genes on weak selection = worse).
* (6) Numbers: run 600 s not 300 (300 s ends where the interesting part starts); cull 18 s not 30
  (15 s of physics per already-measured body = half the run's cost); fresh blood 25% -> 12% after
  the first 100 s; elite 1/24 keeping the twin (determinism is real value).
* Work order: (1) fix Defaults[8] + the "default inside the box" audit + clamp logging
  (ArenaGenome.cs); (2) the sliding window + tournament 5-of-third + elite 1/24 + cull 18
  (ArenaRunner.cs:54-57, 479-505); (3) the time-to-fall fitness + sign/monotony filter
  (ArenaHumanAgent.cs:116); (4) one-family mutation + the best-quartile schedule trigger;
  (5) THEN the ACTIVE expansion to ~35 (T3/T6 logged as frozen); (6) concept edits: remove
  "defaults = centre", fourPawFraction from the human formula, the 15% dominance block.
* NOT DONE (its own flag, iteration budget 8/8): the numerical heredity check (parent->child corr
  by distance) and the actual mutation step in box fractions - the script is ready, one run needed;
  corr > 0.3 would move its priority toward the fitness instead. NOTHING IN THE CODE WAS CHANGED;
  the arena's rank stays a proposal, landing only through the line's bars (c6w38n_walk stands,
  stance md5 a7e18eb9).

## CONCEPT-RUNS FOR THE PACKETS (22.09 ~22:55) - the walk's arena at 90 s, seeds 1 and 7

`arena_concept1b` (seed 1) / `arena_concept2b` (seed 7), 90 s, 181 spawns each, fingerprint
c96db7e9: distance_15s max 5.327 / 5.086 m; distance_30s max **6.939** / 4.531 m (the 1b's best
is the day's arena best on the 30 s window; first-row identity across seeds = 2.93781 m holds -
the mutations diverge after spawn 1). These runs back the reviewers' packets together with the
transplant trio (#92 11.325 s / #317 9.44 / #675 9.37 backwards) and the play-session's plateau.

## GROK 4.7 ON THE EVOLUTIONARY CONCEPT (22.09 ~22:45) - THE TRANSFER INVERSION IS THE STEERING ERROR

Grok 4.7 (x-ai/grok-4.7 via gpt-chat.by, 61.6 s, 4510/4578 tokens) reviewed `arena-evolution-concept.md`
and went harder than the concept deserves, in the right direction. THE ONE CHANGE: **select parents
from LINE acceptance, not arena distance** - all three transplants inverted arena rank, so `distance15`
is not a noisy objective but a DIFFERENT EPISODE; breed on the line's own bars (transplant top-K
through the same flags every N spawns; the elite set = line-passers only). Also: do NOT open T2-T6
until line-passes per hour rise (900 jitters did not cover a 13-D box; the plateau is the surrogate
saturating, not the box being finished); no single-rollout elites (contact noise + one 15 s trial =
luck, re-score before promotion); coherence only on REAL PAIRS (`*P`/`*D`, `stepMin`/`stepMax`,
land/stance lengths, swing/stance timing) at high probability - a tier-wide jump is an unattributable
macro-mutation; schedule step size + fresh blood on transfer DISAGREEMENT, not arena-mean plateau.
REMOVALS: the concept's "the 13-gene box is explored" sentence (steering error); the 15% dominance cap
(undefined under cull+elite+blood, fights selection); tier-level family mutation; arena-mean plateau
as the shrink trigger; the "accepted values = box centre" centre-lock (keep only the audit: shipped
champion inside every clamp); one formula for human and dog (fourPawFraction does not belong here;
the 1.0/0.5/0.5 weights are falsified by #92>#317>#675 and #675's backwards travel - penalty retuning
cannot fix a transfer inversion); dormant genes logged as searched; end-of-session harvest as the only
acceptance gate. Full text: `F:/hermes-data/grok-concept-review.md`. The Opus's parallel review is
still cooking (proc_30c6aaca2ac4).

## THE DOG ARENA ORDERED (Yury, 22.09 ~23:05) - the evolutionary flow for the dog lane

Yury: "эволюционный поток возьми как основу на будущее, реализуй его для собак." The dog-arena
order is on disk: `F:/hermes-data/ds-dog-arena-order.md` - the required elements (the dog genome
of 12-15 genes from DogStanceController/Dog.cs/the walk's clock; the spawn/evolution reusing the
human arena's pattern without forking its box; the fitness carrying the dog's own card clauses,
not distance alone; the spawns carrying TODAY'S dog law - the c50 lesson; the transplant passing
the dog's full KEEP before any landing; the 30 s video for Yury). It rides the dog lane's next
free round after the c62 (which owns ds-dog-task.md right now). THE HUMAN SIDE'S EVIDENCE for the
method: #92's transplant = +2.73 s on the walk; the arena's own best #317 = a candidate only.

## THE EVOLUTIONARY FLOW IS NOW A BASIS (Yury, 22.09 ~23:05) - and the c317 disqualifier

Yury: "эволюционный поток возьми как основу на будущее, реализуй его для собак." ACCEPTED as a
standing process decision: the arena/evolution is a first-class lane instrument, not a side demo.
The same message's law is already proven twice: the arena's fitness is NOT the walk's fitness -
the arena's best #317 (dist15 6.115 m, better than #92's 5.967) transplanted onto the walk gives
**9.44 s / 3.72 m / 8 swaps** against #92's **11.325 s / 5.93 m / 16 swaps**. The transplant
(cycle 52) keeps the #92 genome; the arena's best is a CANDIDATE, never a transplant by rank. The
dog line's implementation is ordered (the dog-arena task file).

## THE ARENA'S GENOME MOVES THE WALK (22.09 ~22:30) - THE TRANSPLANT, +2.73 s MEASURED

Yury: "200 ые уже идут не плохо, мне кажется ты недооцениваешь арену с эволюционным подходом."
He is right. From his own arena session (arena_play_s1, 1153 rows) the best genome #92 (dist15
5.967 m) transplanted onto the walk's accepted line (the flags -poseSwingT/-poseStanceT/-poseLift/
-poseLandShare/-poseHeightGain/-poseHeightDamp/-posePlaceMargin/-poseLeadSwap/-poseStepMin/
-poseStepMax/-poseArmSwing/-poseAnkleP/-poseAnkleD): **fallTime 8.595 -> 11.325 s, travel 3.44 ->
5.93 m, swaps 11 -> 16** (row arena_gene92c_walk, fp c96db7e9, a single deterministic run; the
first attempt with the genome's own gene names was ignored by the player - the mapping above is
from the code's own GetFloat list). **WALK CYCLE 52 dispatched** (the transplant's full
acceptance: the reproducibility md5, the transfer/price/census bars, the stance a7e18eb9, the
per-plant story, the videos; brief ds-walk-cycle52.md, stand scripts/run-c6w52.sh, probe
tmp_w52). This is the largest single-day gain on the walk and it came from the arena; the box
(ArenaGenome) is now a standing tool for the walk's constants.

## ARENA - WHY THEY FALL (Yury's question 22.09 ~17:30) - measured, and the fix dispatched

Yury: "Почему они на видео падают? У нас же сейчас уже человек проходит несколько шагов." THE
MEASURED ANSWER: the arena's spawned bodies run the walk's DEFAULT law (the prefab's shipped
initialisers); today's accepted walk needs its accepted switches (`-captureWalk 1 -poseTailHold
0.010 -poseAnkleCarry -14 -poseAnkleRamp 0.09`), which the spawn path never passes. PROOF: the
re-run with exactly those flags = the SAME row (81 spawned / 51 measured / best distance15 -0.91
/ 0 walkers > 0.5 m) - the flags reach nothing in the arena. The walk itself walks (8.595 s, 11
swaps, 3.44 m). FIX: **WALK CYCLE 50 (side round) dispatched** - the spawn wired to today's walk
(brief `F:/hermes-data/ds-walk-cycle50.md`; acceptance: the default genome's distance15 >= 3.0 m
+ a 30 s video for Yury; rides the lane's next free slot after the c46-c49 queue). Then the
arena becomes the mass-sweeper for the new families' constants.

## ARENA RUN for Yury (22.09 ~17:20) - the evolutionary stand, 30 s video delivered

Yury asked for the arena stand with its automatic spawning (the evolutionary approach) and a 30 s
video. Ran `arena-run.sh arena_evo_vis 1 40 -visual -shotRate 5`: 81 humans spawned (one every
0.5 s, 13-gene genomes), 51 measured, the evolution live (37 fresh / 37 children / 7 elite,
parents tracked); wall 43.5 s on the gate's build 7d36ab75. Artifacts: `Trials/Runs/arena_evo_vis.
{json,csv}`, `shot_arena_evo_vis_*.png` (201), video `arena_evo30.mp4` (30.0 s, sent to Yury).
THE HONEST DATUM: the best measured distance15 = -0.91 m, most bodies fallen - the genome box (13
core pose-walk genes, defaults = the 6r-era verified values) does NOT carry the current walk's
extra families (the capture walker's switches, the carry/ramp/tail), so the as-is arena races the
old walk. TOOL VALUE (Yury's hint, and it holds): the box already carries `ankleBalanceP/D` -
the very gains the Opus W2 servo will tune - so the arena is the lane's mass-sweeper for dose
work (the zone the reviews called dead at cycle 39). FOLLOW-UP (not dispatched): extend the
genome with the current law's families as genes 14+ so the arena races today's walk.

## OPUS REVIEW - THE GROUND INTERFACE (22.09, Hermes; the second of Yury's two granted calls) - THE PITCH, ONE LEVER

opus-review-ground.md (5m09s, 16 tool calls). THE READ: both walls are ONE place - the body has
no owner of trunk pitch once it leaves the walk's support. The fall: 130 ms palm-to-floor with
the pitch unarrested; the get-up: the apex made with zero contact (fz 0) on a body ballistic and
inverted (-181.4 unwrapped); the tuck's 0.79 m-behind grounding = the same fact seen from the
feet. ONE LEVER: the timing of support acquisition relative to pitch ("two rooms, one house, one
lever"). THE PLAN: R1 (the fall: the guard arms on fs>=Critical AND pelvisVelY<=-0.20 held
0.10 s + the wrist palm-parallel-to-slab; acceptance: the walk byte-identical, the contact order
hand->head >= +0.10 s on EVERY row, minHeadY >= -1.882, |accY| <= 29.7 "not worse"; stop:
head-first / the walk moves / |accY| > 35 => OFF + the wrist hunk withdrawn) -> R2 (the Tuck's
bound exit to Plant when |unwrapped| > 120 deg, no new writer; the Rise entered with |unwrapped|
<= 120 and pelvisVelY >= -1.0; best CoM short <= 0.1455; stop: worse CoM / a Plant-Tuck
livelock) -> R3 (the re-plant: the tear gate gains load>0; >=1 re-plant engage in attempt 2/3;
the c42's 91+12 tuck frames kept; stop: the retries' palm fraction toward c43's 0/402). RANK:
(1) the entry, (2) the re-plant, (3) the 0.79 band (a consequence - do not spend a round until
R2's hand-off is honest). THE HONEST: no absorption promise (the arm chord 0.67 m vs the
shoulder 1.2-1.3 m; c45 measured the arms at 2.4-4.2x the baseline's peak); the get-up's card
needs the TRUNK's owner (BalanceController.lumbarTargetTilt - a scope request, not a lane tune);
"arms that come out before the face arrives read as fear - that is the whole of 'естественно'".
THE FLAG: the video the owner has seen (c6w40's dense) = the default-hold row whose walk moved
(fall 8.185) - landed rounds shoot their OWN row's video before the owner sees anything. THE ONE
NUMBER: the CoM short at the apex (0.1455 m today; < 0.10 => the supported apex next).

**WALK CYCLE 46 dispatched (Hermes 22.09 ~17:01): THE FALL, ARMED ON DESCENT, PALM FLAT** = the
reviewer's R1 verbatim (brief `F:/hermes-data/ds-walk-cycle46.md`; stand
`F:/hermes-data/scripts/run-c6w46.sh`; probe `tmp_w46/probe_w46fall2.py`). Goes FIRST (the
rhythm); the walk's c47 next; c48 = R2, c49 = R3 (both cited in the c46 brief).

## OPUS WALK REVIEW #3 (22.09 16:55, Hermes; Yury's word) - R1 UNCONDITIONAL, W2/W3 after; the honest: the rig may be the wall

Opus #3 (opus-review-walk3.md, 2m16s, 16 tool calls) on its own plan one day later: **Q1** R1 =
run as written + the free column (log planStepT & landBlendU@plant: if landBlendU < 1 on every
plant, R2's second guard is justified free; else R2 shrinks); the cause series (chord@plant,
hipY@plant, lead@plant) is measured NOWHERE in the tree - the fall window is the consequence.
**Q2** the ninth step's census gap = a DIFFERENT mechanism (the planner asks a plant the reach
cannot deliver); it waits until R3's row exists (its sign flips with it) - do NOT open it as
round 4. **Q3** the fall guard: keep the isolation (off in the walk's rows); a companion
guard-ON row = not yet. **Q4** the order: W1 (the instrument: the recorder + the plant latch
captureStanceAnchor, NO writer; acceptance = the row bit-identical + non-NaN columns k4..k9;
stop: any fall movement => withdraw, restore first) -> W2 (the support-height servo, the
morning's acceptance) -> W3 (the speed reference: copVDes clamped to CaptureDesiredSpeed*dir;
comVelX <= 0.9 over [k4,k9]; lead@plant >= 0 on 2 of the last 3 plants). THE RHYTHM (binding):
alternate by ROUND, the ground line first each pair; the walk never skips two consecutive
rounds. **Q5 the honest:** R2+R3 give a decisive number, not 40 s; 3 of the last 6 rounds
landing nothing = the normal rate; IF W2+W3 land clean and the fall still < 10 s => the PLANT
(the rig's ceilings/links) is the wall - "an owner decision, and I would bring it to him as
one". The two geometry notes: the arms (0.67 m chord vs a 1.25 m shoulder - a geometry verdict)
and the walk's own reach bound. ONE NUMBER: fallTime on W3's row - > 12 s = the frame right,
40 s is an engineering grind; 8.6-9.5 s with comVelX <= 0.9 and lead >= 0 = the rig, the next
month.

**WALK CYCLE 47 dispatched (Hermes 22.09 ~17:45): THE PER-PLANT INSTRUMENT** = the reviewer's W1
verbatim (brief `F:/hermes-data/ds-walk-cycle47.md`; stand `F:/hermes-data/scripts/run-c6w47.sh`
pre|walk|full|stance|dense; probe `F:/hermes-data/tmp_w47/probe_w47plants.py`). QUEUE: after the
ground line's own round (the reviewer's rhythm) - do not run before it. THE GATE: lead@plant
trending more negative across k4..k9 + hipY@plant non-flat (> +-0.01 m) => RATCHET CONFIRMED =>
W2 as written; else SINK READ REFUTED => skip W2, W3 next.

## WALK CYCLE 45 dispatched (Hermes 22.09 ~15:55, Yury's word) - THE FALL ONTO THE HANDS

Yury: the fall is a STATE; the body must act in it - the arms come out forward/down, the hands
take the ground first ("падать на руки, а не головой об землю"), the impact is absorbed, and it
must look natural ("сейчас человек падает неестественно"). Cycle 45 = the fall guard: the
controller's own Falling state triggers the arms (the arm-plant machinery the get-up round just
exercised), the hands contact first, the elbows flex to absorb; switch `-poseFallGuard 1`,
default OFF; acceptance = the contact order (a hand >= 0.1 s before head/chest), the head's
clearance above its `-poseFallGuard 0` baseline, the absorbed impact vs the ablation, the
pre-fall prefix byte-identical, the stance a7e18eb9, the 5 fps video + a dense take of the fall.
Brief `F:/hermes-data/ds-walk-cycle45.md`; stand `F:/hermes-data/scripts/run-c6w45.sh`
(pre|walk|full|stance|dense); probe path `F:/hermes-data/tmp_w45/probe_w45fall.py`. QUEUE: after
the lane's own chain (41-43, the Opus review's R1/R2 line) - do not run before it.

## WALK CYCLE 40 dispatched (Hermes 22.09 09:55, Yury's word) - THE HAND GRIP (the human's own claws)

The human never tries to get up after the fall (measured on the current player: he topples
forward at ~8.9 s and lies for the rest of the 40 s run - the `GETUP-1-walk` card's own
opening state still true). Yury's word, verbatim: add a hand grip - the palms raise their
friction when he presses them to the ground, "как будто бы он цепляется руками за землю",
"это может помочь для его вставания". Cycle 40 = the get-up behaviour (card
`.cursor/tasks/GETUP-1-walk.md`) with the grip as its instrument, built on the dog's claw
pattern (`DogStanceController`: a press, a friction material swap, a hard point, the lever),
plus the visible grip mark (the dog c35 lesson). Switches `-poseGetUp 1`, `-poseHandGrip 1`,
`-poseHandGripMu <f>`, `-poseHandGripHold <s>` - both families DEFAULT OFF so every old
command line keeps its row. Brief `F:/hermes-data/ds-walk-cycle40.md`; stand
`F:/hermes-data/scripts/run-c6w40.sh` (pre | walk | full | stance | dense). Row of record at
dispatch: `c6w38n_walk` d76e2943 (law 6e418545); cycle 39's probe round was still running.

**OPUS WALK REVIEW #2 (22.09, Hermes; Yury's word): the sink, not the topple.** The expert
(packet `F:/hermes-data/opus-walk2-packet.md`, report `F:/hermes-data/opus-review-walk2.md`,
verification + razbor `F:/hermes-data/opus-walk2-verification.md`) read the capture walker's own
code and the fresh records: the fall at 8.595 is a SINK - the pelvis crosses the bar at tilt only
-12.5 deg while the CoM runs at ~1.5 m/s (2.7x v_des); the speed is open loop (single support
`copVDes = fade*comVelX`, "do not fully brake"); the plant lands 0.4-0.5 m behind the mass (no
restoring moment, no leg length) and no writer restores the height (LegLengthKnee = fixed angle,
HoldKnee = zero-error, LandingKnee's blend truncated by the 0.205 s step). Its plan, kept:
**R1 instrument-only** (per-plant snapshot columns; acceptance = the row of record bit-identical;
gate: if lead@plant/hipY@plant are flat across k4..k9, skip R2) -> **R2 the support-height servo**
(`DriveCaptureSupportHeight`, the loaded stance knee only, the chord closed on the measured, two
guards, the landing blend normalized by the actual step T) -> **R3 the speed reference**
(copVDes clamped toward CaptureDesiredSpeed*dir; the swing floor = the landing blend's time).
Dispatch R1 after the get-up round (cycle 40) closes. All load-bearing claims verified against
source/records; one overstatement noted ("hip height measured nowhere" - the planner reads a hipY
locally, the recorder has no per-plant column).
**Quota (Yury 22.09): 2 more Opus runs approved for this line, fired by Hermes without asking.**
Trigger 1: R1's gate refutes the sink read (lead@plant not trending negative / hipY@plant flat
across k4..k9). Trigger 2: two rounds in a row fail to move the fall past 8.610 with the census
kept. Before firing: regenerate the packet with fresh numbers (same skeleton).

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

**Cycles 6f + 1 acceptance MEASURED 19.09 13:06 (Hermes orchestrator; build fp `bfcb3a69`
613 files, guid `9f9782ea`; runs `c6f_walk` / `c6f_stance` / `squat_c1` / `c1_stance_reg`,
no rebuild - the 12:51 build already carried both edits; reports
`Trials/Runs/walk-cycle6f-accept.md`, `Trials/Runs/walk-cycle1-accept.md`).**
Cycle 6f (swing-hip ask written in the plan's walking pelvis tilt instead of the live measured one)
removed the trunk fold's own engine: `torsoTilt` at 7.1 s **-13.7 vs -56.7** in 6e, `rmsTorsoAngVel`
55.6 vs 67.7, CoM at the fall **1.146 m vs 0.592** (`comVelX` 1.688 vs 0.750), `comTravelX` 2.804
vs 2.167. A1-A4 still FAIL (2 swaps, fall **7.380 s** vs 7.145, no third placement); B bit-for-bit
(221 numeric keys, only `wallSeconds`/`wallPerSim` differ); C passes (`ikSaturatedFraction` 0.0,
`captureWalkStepCount` 2). The remaining defect is a **placement, not a fold**: the swing sole
rides 0.018 -> 0.158 m UP, `fzL` 0 N from 6.94 s, `dcm` 1.440, `supportMargin` -0.266 while
`comVelX` grows monotonically 0 -> 1.51 m/s; the first transfer does work (left single support
0.305 s vs 0.100 s), the second never completes. Cycle 1's vertical channel is measured at last
(`squat_c1`, the label the cycle-1 report asked for): the pose IS throttled (52.5 % of the
5.0-7.5 s descent inside 0.90-1.15 W) but the protocol still ends in a fall (5.955 s) with peak
`fzTotal` **3.38 W vs the <= 1.5 W bar** and `maxAbsPelvisAccY` 48.2 vs <= 6 - the 0.25 m fall bar
and the load band stay in tension, exactly as that report's point 3 predicted; quiet stance
`c1_stance_reg` bit-for-bit. Next: cycle 6g brief `F:/hermes-data/ds-walk-cycle6g.md` (attribute
the missing third placement before any further edit).

**Cycles 6g-6k, one line each (Hermes; reports `Trials/Runs/walk-cycle6*-accept.md`).** 6g/6h/6i
closed the ask-vs-delivery split in stages; 6j published `captureSwingAskX` / `captureSwingSoleX` /
`captureSwingTrackAge` / `capturePlanTargetX` and showed the plan floor and the chord clamp are NOT
the owner (the ask and the sway die in the DELIVERY, 0.457 m at the plant, with the leg at the pose
the solver asked for within 1-3 deg). **6k (accepted 19.09 20:55, player fp `01d56460`, buildGuid
`90a2f449`, runs `c6k_walk` / `c6k_stance`, report `Trials/Runs/walk-cycle6k-accept.md`) closes the
solver branch: `captureSwingSolvedResidualX` is EXACTLY 0.0000 on all 446 walk frames and
`solvedSoleX` carries the ask's own range to the digit — the solver's returned angles land the ankle
where it was asked, so the input-to-solution kinematics are CLEAN and REFUTED as the owner. The loss
is delivery only (`|solved-sole|` mean 0.2047, max 0.7150 m); the new discriminator is the frame:
`captureSwingSolveFrameAngleDeg` is -3.00 deg at the plant and frozen while the MEASURED trunk tilt
is -22.4 to -34.6 deg, i.e. the solve runs in the PLAN frame up to ~19-30 deg off the body, and the
delivered sole pins at 0.209 m for 0.145 s with a live ask. Rule zero: the walk window 5.0-7.4 s is
bit-identical to `c6j_walk` (only `tapAge`, a frame counter); everything that moved starts at
7.375 s, after the fall, and belongs to the arms lane's `Human.cs` (19:53) / `ArmDrive.cs` (20:06)
edits — the 6k fields have no reader outside the recorder. Walk still FAILS its bar (fall 7.365 s,
3 swaps, `comTravelX` 4.62 m). Next: cycle 6l brief `F:/hermes-data/ds-walk-cycle6l.md` — publish the
applied-pose FK under the MEASURED parent so the delivery loss splits into clamp/tracking vs
parent-frame mismatch.

**Cycles 6l-6n, one line each (Hermes; reports `Trials/Runs/walk-cycle6*-accept.md`).** 6l measured
the frame as the owner of the plant gap (0.5015 m of the 0.457 m, `SolveFrameAngle` -3.01 against an
applied -37.82 deg). 6m made the frame the ask is solved in and the frame the returned angles are
applied in one measured frame, and published `captureSwingAppliedFrameAngleDeg`: the plant gap fell
to 0.0154 m mean with 3 of 30 rows over the 0.05 m bar (the ask's own ramp in the last 15 ms).
**6n (accepted 19.09 23:37, player fp `917a1c8b`, buildGuid `a12521af`, runs `c6n_walk` / `c6n_stance`
on the dog lane's 23:32 build, runner `F:/hermes-data/scripts/run-c6n.sh`)** re-derived the flexion
allowance in the measured frame and published `captureSwingAskY` + `captureSwingHipDemandDeg` (the
pre-bound demand): the plant window is now **0.0000 m on all 30 rows** with the residual exactly
0.0000, so the frame defect is closed on the letter too. The walk is unchanged (fell 7.425 s, 3 swaps,
0.9277 m at the fall) and the run of the same CSV locates the next owner at the step target:
`capturePlanTargetX` 0.5303 -> 0.7839 m at t 7.150 with the measured tilt at -37.6 deg, `pelvisTilt`
running to -58.2 deg, `planValid` 0 on 111 of 521 walk rows, `ctrlFallState` 1/2 from 6.150 s.
Next: cycle 6o brief `F:/hermes-data/ds-walk-cycle6o.md` (the step target's frame, or the unowned
pitch, whichever the code shows).

**Cycles 6o-6p, and the LEVEL-2 DECISION 20.09 (Hermes; reports `Trials/Runs/walk-cycle6o-accept.md`,
`walk-cycle6p-accept.md`).** 6o (player fp `9d21b051` / guid `39a689fd`, runs `c6o_walk`/`c6o_stance`)
showed the step target IS built on the plan frame and made the delivered frame the measured one: the
freeze in force carries -57.10 deg against a -58.73 deg body, the plant holds (`|solved-applied|`
0.0000 on 30/30, residual exactly 0) — and the walk got **one step fewer** (2 swaps, fell 7.650 s,
travel at the fall 1.0719 m, `comVelX` at the fall 1.917 m/s), so the drift it bounded was charged to
the swing leg's flexion. 6p (player fp `da8fa193` / guid `6ff2325c`, runs `c6p_walk`/`c6p_stance`, the
same build also carries the arms lane's `ArmDrive.cs` 01:55 and the level lane's 02:28-02:34 edits)
answered the posture error on the swing hip's **command** (`CaptureSwingPostureBiasDeg`, line 4207)
bounded by the walker's own 45 N·m loaded-hip band expressed in degrees = **3.0 deg**. Measured: the
bias is **pegged at 3.0000 deg for most of the walk** (377 non-zero rows, t 5.15-7.30, no NaN, 0
sentinel outside) against a drift of 20-35 deg, the split `pelvisTilt − capturePlanFrameTiltDeg` at
t 7.00 is **-0.57 deg** (6o -35.89) and the card row is **unchanged**: fell 7.650 s, 2 swaps (edges
6.455 / 6.855), travel at the fall 1.0135 m, single support 70.4 %, `pelvisDrop` 0.8747, plant mean
0.038163 / max 0.038400 m (inside the 0.05 m bar, exactly the bound the worker predicted), rule zero
PASS (`c6p_stance` 201 cols, the card's four numbers to the digit). The failure is now located
downstream of nothing: the swing sole **stalls** — `capturePlanTargetX` **0.64 m** against a delivered
`captureSwingAskX`/`captureSwingSoleX` **0.18 m** at t 7.015-7.275, `fzL` 0 N from 6.95 s, `pelvisTilt`
-31 → -75 deg, `ctrlFallState` 2 at 7.275 s — i.e. 0.46 m of plan never becomes a placement. Vertical
channel re-measured on the same player: `c6p_squat` still misses the c1 bar to the digit (fell
5.955 s, `maxAbsPelvisAccY` 48.1553, peak `fzTotal` 2319.794 N = 3.3377 W), `c6p_stance_reg` = the
accepted quiet stand. **DECISION**: level 1 is measured blocked (6e..6p: the card row pinned at 2
swaps / ~1 m of travel while solver, delivery, plant, frame and posture channels are all instrumented
and bounded), so the Level-2 authorisation above is used: brief `F:/hermes-data/ds-walk-cycle6q.md`
builds the pose-driven path — a pose library and per-joint PD torques, the balance layer reduced to
foot placement by the capture point and holding the hip height — **behind ONE switch, default OFF**,
the capture walker kept as the rollback, rule zero = bit-identical while off, one recorder column
naming the path. NOT this lane, flagged for arms: after the fall the body now creeps **backwards**
(`comX` 1.77 m at t 12 s → 0.073 m at 40 s where `c6o_walk` slid forward 2.98 → 4.909 m,
`armPressWindowTravelX` 4.33 → 1.50, `armAimBranchL` 0 → 1).

**Cycle 6s ACCEPTED 20.09 05:35 (Hermes dispatcher; player fp `631f238b` / guid `ef25e815`, 657 files,
`gitCommit 697d8472`, gitDirty; built by the compile gate at 05:20 with `TRIAL_BUILD_OK`; runs `c6s_walk` /
`c6s_stance` / `c6s_quiet` / `c6s_walkstand` + the control `c6s_quiet_arm0`; report
`Trials/Runs/walk-cycle6s-accept.md`, dump `Trials/Runs/c6s-analysis.txt`).** The walk lane's publish-the-
asks edit (`PoseDriveController.cs` 01:58 + `TrialRecorder.cs` 01:59, uncommitted) is **recording-only,
proven bit-for-bit**: 202 -> **217** columns with the 15 new names appended last in the documented order,
`c6s_walk` == `c6r2_walk` on all 202 shared columns in 5.0-7.2 s with the row identical to the digit
(fell **7.255** s, swaps 43, travel -0.2415), `c6s_stance` == `c6r2_stance` over the whole run, and
`-poseDrive 0` reproduces `c6q_walk` exactly **once the arms lane's own switch `-armDamperBase 0` is set**
(`c6s_quiet_arm0`, 0 differing columns) — so the level-1 window that moved (fell 7.65 -> 7.645, 93 columns,
`armHaveR`/`armRestR` up to 356, `fzR` 35.6 N) belongs to commit `697d8472` (ARMS CYCLE 11), **not** to the
walk; from now on rule zero against `c6q_walk` must be taken with `-armDamperBase 0` or against a same-
player control. The new columns read their own values (no non-finite cell in 4 x 8001 rows; quiet all 0,
stance drive 1, walk drive 2 on 6991 rows). **The asked question is answered**: swaps 1 (5.705) and 2 (6.315)
fired on **lead** (`comVelX` +0.220 / +0.787); then the placement gate `maySwap = swingPlanted || stepClock >=
halfCycle + 0.15` is **shut 6.425 -> 6.945 (0.52 s)** with the swing foot airborne (`fzL` exactly 0), the CoM
peaks **0.6015 m at 6.70** and turns round (`comVelX` +0.515 -> -0.080 at 6.75), the sole touches ~0.63 s
after its own swap against a half cycle of ~0.61 s, and the third swap at 7.125 fired on **the clock** with
`comVelX` -0.888 and the swing unloaded -> fall 7.255 s; the run's reasons are **28 planted timeout / 13
clock / 2 lead**. The ask during the reversal is a static stand (swing knee target **12 deg** while the foot
is off the floor, pelvis tilt target -1.0 against a measured **+22.0**, hip height request 0.985 vs live
0.869 = **-0.116 m**). The post-fall runaway now has its column (`down` = 1 with `capturePoseDriveState` = 2
on every row to 39 s, 42 further swaps). Walk-key-stand unchanged (`c6s_walkstand` 6.72 s). Card bar still
FAILED. Next: brief `F:/hermes-data/ds-walk-cycle6t.md` — the landing must follow the CoM (capture point),
not `swingPhase`, so the sole is placed before the clock expires.

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
- ONE PROFILE = ONE KIND OF WORK (Yury via H_Manager, 20.09: 614M tokens/day and 84 sessions
  in a single profile was the trigger). Routing lives in the `case` of `ds-cycle.sh` and
  nowhere else: `measure` (MEASURE STREAM, dog metrics, any numbers-only run) ->
  **`h_2dgame_measure`** (read-only trials + static analysis, evidence in `Trials/Runs/`);
  `level` (rooms, scenes, level geometry, framing) -> **`h_2dgame_leveldesigner`**;
  `walk` / `arms` / `dog` / programmer cycle (code, build, physics edits) ->
  `h_2dgame_programmer`. A non-code stream never borrows the programmer profile by default;
  a MISSING profile falls back to it and prints the substitution. Check a route with no side
  effects: `DS_DRY=1 bash scripts/ds-cycle.sh <stream>`.
- Unity executor: **`h_2dgame_programmer`** (Yury renamed `programmer` -> `H_2Dgame_programmer`
  on 20.09). The 18.09 name `h_2dgame_unity` was never created, so `ds-cycle.sh` now resolves
  the worker profile to whichever of `h_2dgame_programmer` / `programmer` / `h_2dgame_unity`
  exists - a half-finished rename can no longer point a stream at a profile that is not there.
  Every script that named `--profile programmer` was repointed in the same pass; the obsolete
  one-shot rename helpers are kept as `*.obsolete`. `h_rev_*` stays Revit.
- The DEFAULT profile's display name is **`H_2Dgame_BOT`** (Yury, 20.09). Its canonical id
  stays `default`: every cron origin, the gateway and the scripts point at it.
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

## Dog trial step clock - the run-protocol artifact is fixed, and the "regression" is a 4-step onset shift (dog worker, 19.09 ~13:45)

The bisect of 12:45 (`dog-art1-bisect-report.md`) answered "no source file and no art file moved
the physics - the loop's clock is nominal over an unrecorded number of executed steps". That loop
is now step-driven: `HeadlessTrial.FixedUpdate` owns the dog trial's clock, its scheduled
`Walk`/`LeapAttack`/push and the recorder sample, and the run ends on an exact step budget
(`duration / fixedDeltaTime`), so `samples == executedSteps` (published in the JSON beside
`samples`) and `simulatedSeconds == executedSteps * fixedDeltaTime`.

Build `5da13d45` / guid `bf95ae39` (gate `TRIAL_BUILD_OK` 13:36). Two files: `HeadlessTrial.cs`
(the dog loop + the new `FixedUpdate`), `DogTrialRecorder.cs` (`MarkExecutedSteps`,
`executedSteps`). No control law, threshold, scenario flag or physics constant touched.

- five identical runs `dog_det1..5` (25 s, `-dog 1 -dogWalk 1 -dogWalkTime 1 -fixedDelta 0.005`):
  one csv md5 `cf35e1dda4`, comTravelX 2.60954762, comSpeedX 0.108731307, 21 cycles,
  executedSteps 5001, simulatedSeconds 25.0049648.
- `-visual` run `dog_det_vis1` = the same bytes: the dog has no render axis any more.
- the first 5001 steps of the 40 s run `dog_gu14_cyc51` (8001 steps) are byte-identical to
  `dog_det1`: the trace is a function of the step index, not of the run's length.
- **the 18.09 "regression" is a 4-step walk-onset shift**: `-dogWalkTime 1.02` (step 204)
  reproduces the accepted 5.5232029 / 0.230325714 / 35 cycles / `dogDownFraction 0.232153565`
  (identical to `dog_art1`), `-dogWalkTime 1.005` (step 201) reproduces the "regressed"
  -1.75025594 / 8 cycles / 0.743251324. The old loop fired the walk intent off the nominal clock,
  so the onset step was a free parameter of the harness, not of the dog.
- noise floor over 10 adjacent onsets (same build, 25 s): travel min -1.750256, max 5.523203,
  mean 1.674107, stdev 2.011625, **range 7.273459 m** while the 5 km/h target is 34.7 m in 25 s.
  A single-trajectory A/B on this dog resolves nothing below ~7 m: dog acceptance must name the
  onset step or be an ensemble, and past single-trajectory verdicts need re-measuring.
- cycle-1 instruments re-verified on the step clock (`dog-cycle1-check.py dog_det1 dog_gu14_cyc51`
  -> RESULT OK, 39 columns, NaN 0): controller-vs-recorder trunkDrop gap max **0.3514 m**
  (frame 1454) - the decisive column has a number; metresPerCycle 0.124264173 m vs nominal
  0.279358923 m.
- retired: the `dog_art1` triple and `dog_gu14` 4.81013966; the 40 s protocol on the step clock is
  `dog_gu14_cyc51` 7.02897739 / 0.180230439 / 44 cycles / fell 4.52999353 s.
- NEXT (harness, before any physics cycle): the same fix for the other six loops -
  `RunImpactTrial` is the measured case (`balance-arena1-report.md`: only `-nographics`
  reproducible). Then cycle 2 (cosmetic split) can use the new detector.
- Runners/checkers: `Tools/dog-det-run.sh`, `.../scripts/dog-determinism-check.py`,
  `.../scripts/dog-onset-ensemble.py`. Full numbers: `Trials/Runs/dog-stepclock-report.md`.

## Build-trust audit - a stale player, two unswitched walker knobs, and the gate's blind stale test (orchestrator, 19.09 14:26)

Step 1 of the cycle read `build-status.txt`: "OK at 14:10". That OK was honest for the top-level
sources of that minute (newest top-level `.cs` was Campfire 13:53, player 13:55) but NOT for the
tree - three changes landed after the gate's last tick and were never compiled:
`Assets/Settings/URP-2D.asset` + `UniversalRenderPipelineGlobalSettings.asset` (14:09),
`Assets/BalanceControllerCaptureWalk.cs` (14:18, the WALK CYCLE 6d step-time block),
`Assets/Resources/Level/ground_cap.png` (14:22). The 13:55 player did not carry the 6d code, so a
trial in that window would have measured the pre-6d walker while quoting a 6d source. Rebuilt
14:26 (`TRIAL_BUILD_OK`): the player is now newer than every source under `Assets`. The art and
settings edits are render-only - `LevelArt.UseTiledFill` / `StaticPlatform` touch
`SpriteRenderer.drawMode` and the cap PNG is a cap strip; no collider and no physics path, so no
reference number moves (verified by diff, not assumed).

Two control knobs were live in the default path with no switch. Every other capture knob has one:

- `captureComLandingScale = 0.37` is ON by default and its own Tooltip promises "0 = off: the
  frozen plan target and the old fixed catch lead only (behaviour before this field, to the bit)" -
  but `HeadlessTrial` had no `-captureComLanding`, so no run could set it to 0 or sweep it: the
  number was unvalidated AND unreproducible, and the c6c_walk placement failure could not be A/B'd
  against the path it replaced.
- WALK CYCLE 6d (`CaptureStepDurationForSpeed`, the speed-adaptive step time) was unconditional.
- FIX (behaviour-preserving, both defaults unchanged): a `captureStepSpeedAdapt = 1` gate in
  `BalanceControllerCaptureWalk.cs` plus two args in `HeadlessTrial` - `-captureComLanding`
  (0 = the pre-field path) and `-captureStepAdapt 0` (the constant T of every cycle before 6d).
  The applied argv is already echoed into the trial JSON (`commandArgs`), so a run now names which
  walker it ran. Only one rebuild was spent: the tree was already stale, so the two switches rode
  the rebuild the stale player needed anyway.

Gate fix - the stale test was blind to subdirectories. `newest=$(ls -t "$PROJ"/Assets/*.cs |
head -1)` never looked inside `Assets/Editor`, so "status OK hides an edit" had a second door even
when the gate did run. It now takes the newest `.cs` under all of `Assets`
(`find ... -printf '%T@ %p\n' | sort -rn | head -1`), old `ls` kept as the fallback for a `find`
without `-printf`. Proven by reproduction on a synthetic tree (top-level source older than the
player, an `Assets/Editor` edit newer): the old expression prints OK, the new one prints STALE.
`bash -n` clean.

OPEN, recorded not edited: the cycle-1 vertical channel is switchless too.
`BalanceController.CrouchLoadShapedLevel` gates both the fold and the rise on the measured load
ratio (`crouchLoadFoldFloor 0.90` / `crouchLoadPushCeiling 1.15` / `crouchLoadFilterTau 0.02`) and
nothing in `HeadlessTrial` can turn that shaping off - the only escape is `CrouchLoadRatio() < 0`
when the meter cannot resolve. So the pre-cycle-1 crouch cannot be reproduced for an A/B either,
and `-crouch` runs are silently the throttled ones. It is the same one-line wiring as above; it was
left out deliberately because the rebuild was already running and a third file would have
invalidated it. The cycle-1 owner should take it before the next `-crouch` / `-squatRise`
measurement.

## Walk A/B on the 14:26 build - the landing scale is the measured knob, 6d is an exact null (orchestrator, 19.09 14:35)

The autopilot change detector fired: p_auto moved from 7.38 s / 2 swaps / 2.878 m to 7.24 s /
3 swaps / 2.236 m. The 14:26 rebuild was the first player to carry the 14:18 walk-cycle 6d block
and the two new control switches, so this is the first window in which they could be measured.
Four probes on that one player, cb7e4ef6fb1f, 29 s each:

- p_auto, defaults: fell 7.24 s, 3 swaps, travel 2.2362, single support in the 5-6 s window
  5.30 percent, bothFeet 81.6 percent
- -captureStepAdapt 0, run twice: bit identical to p_auto, 7.24 / 3 / 2.2362
- -captureComLanding 0, run twice: 7.36 s, 3 swaps, travel 2.9708, single 5.01 percent
- -captureStepT 0.30: 7.30 / 3 / 2.27; -captureStepT 0.20: 7.28 / 2 / 2.26 with single support
  20.92 percent and bothFeet 36.2 percent

What this settles:
- the arg plumbing reaches the controller: both -captureStepT values change the run, and the step
  duration is the field the 6d block writes, so the exact null of -captureStepAdapt 0 is a property
  of the walk, not of the harness;
- 6d never engages here. Its own rule returns captureStepDuration whenever the speed is at or below
  CaptureDesiredSpeed, 0.55 m/s in this mode, and the captured walker runs at 0.077 m/s average.
  The block is behaviourally dead on this failure, so it can neither be credited nor blamed for the
  2.878 to 2.236 move;
- the capture-placement scale is worth 0.73 m of travel by itself: 0.37 gives 2.2362 m, 0 gives
  2.9708 m, reproducible twice each. It barely moves the fall, 7.24 against 7.36 s, so the default
  is not the third-plant cause either - it is forward travel being eaten.

OPEN for the walk owner: what changed between the 14:10 player ddb38bf88d, travel 3.088, and the
14:26 player cb7e4ef6fb1f, travel 2.236, if 6d is inert. Same-build travel noise measured so far is
0.08 m, 2.8042 against 2.878 on the 8400c build, so this move is about ten times that. Re-diff the
14:18 and 14:25 edits to BalanceControllerWalk before the next cycle credits them. Every arm above
is a single trajectory: the 0.37-to-0 pair is reproducible but must not be turned into a verdict -
sweep 0 / 0.1 / 0.2 / 0.37.


## Dog cycle 2 - the cosmetic layer is out of the physical file (dog worker, 19.09 ~16:25)

One change: the expression / idle-flavour family moved out of `DogStanceController.cs` into
`Assets/DogCosmetics.cs` as a second **part** of the same class (pure move, verified line by line).
`DogStanceController.cs` 10 279 -> **5 792 lines** (md5 `40cfc2ac` -> `1363db98`), new
`DogCosmetics.cs` 4 502 lines = 15-line header + **4 486 moved lines, 107 methods** (old lines
2343-6828: `FacingSign`/`SetFacing`, turn-hop notes, the `Walk*` expression family, the whole
`Sit*` library, `WalkHunt*`/`LeapHunt*`). Head/middle/tail of the old file are byte-identical to the
two new files; only `partial` and the header comment were added. No gain, threshold, scenario flag,
control law or amplitude touched.

Acceptance MEASURED (build fp `d4055c0109978b8c67c01ef1eb5d4d07f32dd0d166af656c37d14cc4b5b59ec4`,
guid `c49c83d3b6d64723aba4a1c018a3d761`, `Assembly-CSharp.dll` md5 `38e628747b2a82f126ce8162ef52ab56`,
player newer than every source, `TRIAL_BUILD_OK`): `bash Tools/dog-det-run.sh dog_c2 5 25` ->
**5/5 md5 `cf35e1dda4c69d6c433d825575bcf943`**, `cmp` clean against the accepted
`dog_art1_md51..5` of the 15:22 build `4681f70b`; 102 of 106 JSON keys identical (the four that move
are `label`, `sourceFingerprint`, `wallSeconds`, `wallPerSim`); `dog-cycle1-check.py dog_c21 dog_c25`
-> RESULT OK, 39 columns, non-finite 0, `cycle1NanCount 0`; the `-visual` run `dog_c2_vis1` (no
`-nographics`) is the same md5 and **all 126 frames are byte-identical** to the accepted
`dog_art1_vis7` frames. Video `Trials/Runs/dog_c2_vis1.mp4`.

VOID bar, measured: the review's "`DogStanceController.cs` < 1500 lines" is unreachable by moving
cosmetics - its line map ("2305 onward cosmetic only") is wrong below the block: the cosmetic region
is 2343-6828, and 6829-10279 (**3 451 lines**) is the walk/get-up/leap machinery (`Walk`,
`DriveWalk` 203, `WalkLimbTargets` 143, `AdvanceWalkClock` 106, `DriveLeap` 107, `LogWalkDiag` 100,
`ApplyWalkPropulsion` 73, paw/IK helpers), with 2 342 lines above the block (475-field knob surface +
Bind/Sense/Drive/DriveGetUp). Reaching 1500 would need ~4 300 further lines, i.e. the walk
controller itself.

NOT done (its own cycle): the review's structure - `DogCosmetics` as a separate MonoBehaviour with a
read-only state struct. Today the split is a FILE boundary, so the compiler still cannot stop a
cosmetic routine from writing a physical field. Surfaces for that step: the moved block touches
183 controller members and 19 controller methods are called from the physical side (`FacingSign` x16
+ 18 Sit*/Walk*/LeapHunt* tail/head routines). Report: `Trials/Runs/dog-cycle2-cosmetic-split.md`.

### DOG cycle 3 - the capture-point sensor (signal only), 19.09 17:35

ONE change, no control law: the extrapolated CoM (`xi = x_com + v_x/omega`, `omega = sqrt(g/h)`,
`h` = live CoM height over the slab top `walkGroundTopY`, floored at 0.10 m) is computed in
`DogStanceController.SampleCycle1Instruments` from the SAME support interval the projection margin
uses, published as `capturePointX`/`capturePointHeight`/`capturePointMargin`/`hasSupportInterval`
(read-only), and logged as three append-only columns `captureXi`/`captureHeight`/`captureMargin`
(39 -> 42) plus 12 JSON keys. `Sense()` now sets `hasSupportInterval` in both branches of the
support block (the else branch leaves `supportMinX/MaxX` stale). Nothing reads the new fields.

Build fp `cd88a4b5721ac9c0c0b4407b1c52a3c30bdd3eb20263df3ab401ad4db8fb7139` / guid
`2e033a0821d04f6ca427d49c801161b4`, `TRIAL_BUILD_OK`, `executedSteps == samples`, `cycle1NanCount 0`.
**A1 PASS - physics bit-identical**: 5/5 md5 `34f4df76259d153197d61fdf896fa8bb` and **0 of 5001 rows
differ in the 39 pre-existing columns** vs the accepted `dog_art1_r6*` (fp `336a0296`); 12 JSON keys
added, 0 removed, only `label`/`sourceFingerprint`/`wallSeconds`/`wallPerSim` move. **A3 PASS**:
quiet `dog_gu15` keeps `captureMargin > 0` in 5000/5001 frames (99.98 %; 4998/4998 excluding the
spawn frames) - one negative frame named: frame 1, t 0.010 s, -3.0793 m, the spawn CoM jump
(finite difference of -17.687 m/s), not an excursion. **A4**: the walk runs carry the sustained
excursion and it is the CAPTURE POINT's magnitude - `dog_c3_1` 63.69 % negative, p05 -0.3256, min
(excl. frames 0-2) **-0.6485**; `dog_gu14c3` 59.74 %, p05 -0.3404, same min; the old projection
column reaches only -0.4220 m on the same runs. The brief's "observed -0.70 m" is that capture
number, not a projection number. Cycle-4 input: first crossing same frame (t 1.155 s, no lead), but
under the rule `margin < -0.05 m held 0.15 s` the capture point fires at 1.425 s vs the
projection's 1.965 s (**+0.540 s lead**, 3.105 s before the controller's own fall declaration at
4.530 s); 8.82 % of frames flip from projection-safe to lost; at the fall instant -0.2302 vs
-0.1481 m. Warning, measured: the margin is below -0.05 m in 53.56 % of walk frames and the bare
rule fires 107/153 times, so cycle 4's stance-phase gate is the binding constraint.
Report: `Trials/Runs/dog-cycle3-capture-sensor.md`. Stand `Tools/dog-stand-cycle3.sh`, checker
`hermes/scripts/dog-cycle3-check.py`, visual stand `Tools/dog-cycle3-visual.sh` (`-visual` without
`-nographics`, 126 frames, all byte-identical to the accepted `shot_dog_c2_vis1_*`).
NOTE: `Tools/impact-build.sh` writes `build-info.json` only - `Build/Headless/build-manifest.json`
must be refreshed afterwards (`hermes/scripts/dog-write-manifest.sh`) or the player aborts with
`TRIAL_PROVENANCE_FAILED`.

## BALANCE-ARENA-1 push-back: the blow and the body's answer are one column no more (level worker, 19.09 ~16:40)

The card's own column - *"whether the rig pushed back at all (vertical force above body weight after
the impact, with the frame number where it happens)"* - was measured broken by the grid audit
(`Trials/Runs/impact-grid-report.md` 5c): `pushedBack` was true in **12/12** classes at contact
+0..5 ms, i.e. it was the collision's own ground load. One change, `Assets/ImpactRecorder.cs` only:
the first 50 ms after the contact are the *passive window* (the card's spinal-reflex floor - no
living controller can answer inside it), the measured contact duration is now its own column, and
`pushedBack` is a 40 ms sliding mean of `fzTotal` reaching 1.05 x body weight **after** that window.

Acceptance MEASURED on build fp `d4055c0109978b8c67c01ef1eb5d4d07f32dd0d166af656c37d14cc4b5b59ec4`,
guid `c49c83d3b6d64723aba4a1c018a3d761`: rule zero `python Tools/impact-json-diff.py
Trials/Runs/imp_quiet40.json Trials/Runs/pb_quiet.json` -> the card's four numbers unchanged
(0.0148 / 2.6542 / 7.5647 / 0.0), 700 shared keys with 11 differences, all label/clock/build
identity; the twelve classes of `bash Tools/impact-pushback-grid.sh` are **identical to the accepted
build in reference impulse, delivered impulse, contact time and fall verdict in 12/12** (e.g.
50.916 N*s and contact 8.360 s on rect 15 kg / 4 m/s), so the change moved a measure, not physics.

The new column: a push-back in **6/12** classes, and the six are exactly the six 0.5 kg classes.
It arrives **+125..+145 ms** after the contact and lasts 25..65 ms (the card's long-loop band);
nothing appears before +50 ms in any class. The answer is what separates the outcomes: 3.24 / 3.24
/ 4.07 N*s buys a stand against a 2 N*s hit, 1.00 / 1.19 / 1.17 N*s does not buy one against 10 N*s
(he is down 1.32-1.34 s later), and the six 60-300 N*s classes never answer at all (best peak 40 ms
mean 1.025 W, below the 5 % threshold). `contactMs` turned out to need its own column: 10 ms for the
0.60 m plate against 375 ms for the 0.25 m circle, which is why a boundary drawn at "the contact
ended" called the same body answer a push in one shape and not in the other.

Evidence: two 24.05 s 1920x1080 takes with the new line drawn on the frame
(`impact_pb_v1L_*` = push, `impact_pb_v2L_*` = no answer), the 36.1 s cut
`Trials/Runs/impact_pushback_two_takes.mp4`, four stills, and `Tools/impact-pushback-table.py` /
`-probe.py` / `-video.sh`. Report: `Trials/Runs/impact-pushback-report.md`.

NOT this lane, still open: L1 latency is still keyed on the collision (5.000 ms in 12/12) - the
honest divergence definition needs an active-reaction-off run, i.e. a mode in
`Assets/BalanceController.cs`, the walk stream's file.

## BALANCE-ARENA-1 verdict cycle: the throw record's `fell` is the criterion AND the controller (level worker, 19.09 ~17:00)

The card asks the per-throw record for *"`fell` (the shared `FallCriterion` AND the controller's own
state)"*, and the row carried the criterion alone - the grid audit's finding 5e. One change,
`Assets/ImpactRecorder.cs` only: `BalanceController.GeometricFallState` (the `BodySupportGeometry`
ladder - 0 supported / 1 critical = the capture point has left the hull / 2 fallen = the CoM itself
has / 3 no support = flight) is now read once per fixed step and published per throw window as
`ctrlStateAtContact`, `ctrlStatePeakAfterContact`, `ctrlCriticalAfterContactSeconds`,
`ctrlFallenAfterContactSeconds`, `ctrlFell`, `ctrlCriticalFraction`, `ctrlFallenFraction`, and one
word `fallVerdict` (`both | criterion-only | controller-only | neither`); `totals` gained
`ctrlFallenCount` / `verdictMismatchCount`, the per-frame CSV gained `ctrlState` as column 31, and
the frame's banner prints the second verdict next to the first. `NoSupport` is flight, never counted
as a fall.

Measured on build fp `78ea6d5f66e9e18e6dcb4a671693e933d57c52988244a284e3d6e1e1b25fa67c`, guid
`38b4d9287ab9470f9b406d77e43b5e37` (gitCommit `78325dc8`, gitDirty true), 13 `-nographics` runs +
2 `-visual` takes, all carrying that pair:

1. **Rule zero**: `cv_quiet` (`-impactCount 0`, 40 s) vs the published `imp_quiet40` = 700 shared
   keys, 11 differing, every one identity/clock; the card's four numbers unchanged (0.0148 / 2.6542 /
   7.5647 / 0.0) with `sensorTick` 8002 and `maxAbsPelvisAccY` 14.2838.
2. **Publication rule zero**: 14 published columns x 12 classes of the accepted `pb_grid_*` build
   c49c83d3 vs this build's `cv_grid_*` - **0 columns moved**.
3. **The new column**: the two verdicts agree on WHICH windows are falls in **12/12** (0 mismatches in
   13 runs; 9 classes down, 3 stand under both), but the **controller's ladder fires first in 9 of 9
   falling classes, by 160-380 ms (mean 262 ms)** - structural, because the criterion needs 0.25 m of
   drop or 60 deg of tilt held 0.15 s. The gap grows as the blow weakens (160-170 ms at 300 N*s,
   235-245 ms at 60 N*s, 380 ms at 10 N*s). A `controller-only` reading is geometry, not clairvoyance:
   the blow shoves the CoM in the same frame, so the ladder flips 5-10 ms after the contact while
   `clairvoyanceFlag` (a commanded-torque test) stays false. The ladder's three arms are separable in
   the CSV: for rect 15 kg / 20 m/s, state 1 at +0.010 s, state 2 at +0.050 s, state 3 (flight)
   at +0.145 s.
4. Evidence: `impact_verdict_two_takes.mp4` (36.10 s, 722 f, 1080p) + two 24.05 s takes
   `impact_cv_v1L_circle_0_25_m_0_5_kg_4_m_s_1080p.mp4` (stands) / `impact_cv_v2L_square_0_30_m_15_kg_4_m_s_1080p.mp4`
   (down) + 7 stills including the frame at the **controller's** own fall time; the window-end still
   reads `down 0.455 s after contact | controller 0 at contact, fallen +0.210 s -> both`, equal to the
   `-nographics` row. Tools: `Tools/impact-verdict-grid.sh`, `impact-verdict-table.py`,
   `impact-verdict-video.sh`, `impact-verdict-stills.py`. Report:
   `Trials/Runs/impact-verdict-report.md`.

NOT this lane, still open: L1 latency is keyed on the collision (5.000 ms in 12/12) and the honest
divergence definition needs an active-reaction-off mode in `Assets/BalanceController.cs` (walk
stream's file); `closingSpeedMps` still prints a wrong number on `grid_rect_15_4` (finding 5d).

## BALANCE-ARENA-1 closing-speed cycle: the metric is measured, not asked of the engine (level worker, 19.09 ~17:12)

The grid audit's last open item in this lane (§5d: `grid_rect_15_4` reported a closing speed of
**0.059 m/s** for an object launched at **3.940 m/s**, while the same row's delivered impulse
50.916 N·s said otherwise). One change, `Assets/ImpactThrower.cs` + `Assets/ImpactRecorder.cs`:
`Collision2D.relativeVelocity` is read inside the step the contact was solved in, so it collapses to
the post-hit residual exactly when the man and the object leave that step moving together. The
projectile now caches its own velocity and the velocities of the human's own segment bodies at the
**start** of every fixed step (`FixedUpdate` runs before the solver and before the callbacks of the
same step) and reports `ClosingSpeedMps = |PreStepVelocity − PreStepOtherVelocity|`; the engine's
reading is kept as the append-only witness `engineRelativeSpeedMps` and printed beside it in the
on-frame banner. Read-only: no force, no velocity write, no transform.

Measured on build fp `336a0296c07c7892f6c76f41e9fc11ac26d85e78e6b9632b53d1ce4d0f96ba5d`,
buildGuid `a308d8f65d754305b571183370905d3c`, `TRIAL_BUILD_OK`, player newer than every source.
Rule zero: `close_quiet` (`-impactCount 0`, 40 s) reproduces the card's four numbers to the digit
(0.0148 / 2.6542 / 7.5647 / 0.0) plus `sensorTick` 8002 and `maxAbsPelvisAccY` 14.2838; 700 shared
keys, 11 differing, all identity/clock/switch-set. The 12-class grid against the **previous build**
(`close_*` vs `cv_grid_*`, only this change between them): **11 of 12 classes with 0 keys moved**,
and the three rect classes moved **exactly `closingSpeedMps` and nothing else** — the lying class
`0.059292 → 3.93968` (= launch 3.939681, gravityScale 0), the other two in the last printed digit
(19.69841 → 19.6984, the contacted part's own ≈1e-5 m/s pre-step motion). Strictly 2D in 12/12
(`outOfPlane false`, `maxAbsZ 0`). Evidence: two rendered takes (rect 0.6 m/15 kg/4 m/s = the fixed
class; circle 0.25 m/0.5 kg/20 m/s = he stands), 24.05 s 1920×1080 each, the 36.10 s cut
`Trials/Runs/impact_closing_two_takes.mp4`, stills incl. the window-end frame whose banner now reads
`closing 3.94 m/s (engine 0.059)`. Tools: `Tools/impact-build.sh` (bash build with the gate's lock and
an explicit OK/FAILED line), `impact-closing-grid.sh`, `impact-closing-table.py`,
`impact-closing-accept.sh`. Report `Trials/Runs/impact-closing-report.md`. **This lane's grid-audit
list (5a-5f) is now empty**: L1 (latency keyed on the collision) and L3 (no active-reaction-off
switch) remain the walk stream's, in `Assets/BalanceController.cs`.

## ARENA genome box could not express the step band it was searching (programmer cycle, 20.09 ~07:50)

`build-status.txt` read `OK at 06:45` while `Assets/PoseDriveController.cs` (06:51/07:03) and
`Assets/TrialRecorder.cs` were newer than the line that certified them - the stale-OK class this
lane closed on 20.09 06:43. A gate run was forced after the edits below (Unity closed, no
`trial.lock`) and wrote `OK at 07:50` off a real compile: `CompileScripts: 5696.900ms`, **0**
`error CS` lines, `TRIAL_BUILD_OK .../Build/Headless/BalanceTrial.exe`, with both edited sources
(`ArenaGenome.cs` 07:49:03, `PoseDriveController.cs` 07:48:57) OLDER than the run's own start
(07:49:52). The player now carries the edited compiler output, not the 06:45 one.

Found in the two Assets files changed in the hour, and it is the failure class this lane flagged on
19.09: **the arena genome's box and its apply path could not express the walk band the tree
ships.** `Tools/arena-genome-box-audit.py` (the offline contract audit) read **BROKEN - 2 breaks**
on the live tree:

1. `stepMinMeters` shipped **-0.45** (`PoseDriveController.cs` field, WALK CYCLE 6r: the band is
   one step either side of the stance foot "so the law can put the pivot behind a CoM that is
   falling backwards") against `ArenaGenome.Min[8] = 0f` - the shipped value was OUTSIDE the box.
2. `ApplyArenaGenome` read `stepMinMeters = Mathf.Max(0f, g[8])`, the ONE clamp `ReadSwitches` does
   not have (`-poseStepMin` passes its value through bare). The clamp BINDS the shipped value, so
   through the genome path **every negative sample landed on 0**, the gene read as a constant, and
   no row can show it: `Trials/Runs/arena-param-log.csv` stores the gene it SAMPLED, not the value
   the body received. Half the band was unreachable and the log would have called it a null.

Fix, three numbers and a doc: `ArenaGenome.Min[8] 0f -> -0.45f`, `Defaults[8] 0.02f -> -0.45f` (the
reference genome now reproduces the walk the tree ships, not the pre-6r +0.02 band), the apply floor
removed so the genome's clamp IS the switches' clamp (the header promise), and the box now carries
the invariant in-line (every shipped initialiser lies inside `[Min[i], Max[i]]`).

The audit tool itself was lying about the fix: its statement splitter cut on `;` without stripping
comments, so the semicolon in the new comment's prose split the assignment in half and the field
read as "ApplyArenaGenome never assigns it (gene order broken?)" on a tree that assigns it plainly.
Splitter now strips `//` comments and string literals before splitting. Its `--selftest` was also
leans-on-the-tree (it asserted the live tree is BROKEN), so after the fix it would have printed
FAILED for the wrong reason; it now rebuilds each break synthetically from the live text and passes
**4/4** - `live tree passes`, `box Min[8] = 0 is caught`, `apply floor at 0 is caught`, `the fixed
box still accepts the +0.02 arm`. Audit live: `OK - every shipped default is inside its box; 0
latent clamp drift(s)`.

Rule-zero on the walk lane's in-flight 6s/6t/6w publication (the reason the two files were new):
the new `capturePose*` columns are **live**, not dead - 15 of 15 move on `c6w_walk.csv` (217
columns, header == row per `Tools/impact-json-lint.py --selftest`, `OK` at cycle start), and read 0
only where the sentinel means it (stand runs carry no pose ask). The 6w height datum is a real
latch, not the 6v mirror: the request is CONSTANT within each step (0.9582 / 0.9463 / 0.9499 /
0.2758 m) while the live hip moves, bit-equal on **6 of 604** driving rows - the latch frame - with
`max|request - live| = 0.6679 m` (step 3 held the ask at 0.9499 while the hip sank to 0.2820 and the
CoM rolled back to -0.7272 m). Swap reasons exercised: 2 (CoM led) for steps 1-2, 1 (clock) for
step 3, 3 (planted timeout) for step 4, `driveState` 2 -> 0 as the gait relaxed - the lead rule
holds twice and the clock then takes over the step that sinks.

## BALANCE-ARENA-1 evidence #9 on `82fbb1ec`, and the vertical CoM was in the record's own trace (level worker, 20.09 ~09:12)

The #8 evidence set (07:56) named `9a47f594`; the player on disk moved at **09:00:06** to `82fbb1ec`
/ fp `8ba620b8` / `796629c9`, carrying runtime files #8 could not measure (`ArmDrive.cs` 08:34 =
**ARMS CYCLE 13**, `PoseDriveController.cs` 08:58, `TrialRecorder.cs` 08:58). The accepted protocol
was therefore re-run on the player on disk and the `9a47f594` set archived first
(`Tools/impact-archive-set.py impact-pre-82fbb1ec --build 9a47f594`, 1492 files moved, nothing
deleted). **No `Assets/` file on the trial path was touched and no build was requested.**

Identity: 8 of 8 runs name fp `8ba620b8` / guid `82fbb1ec`, manifest re-read after the last run
(09:07:14) unchanged. Rule zero reproduces the tester's stand to the digit (pelvisDrop 0.0148,
rmsShoulder 2.6542, rmsElbow 7.5647, armSwingFraction 0.0; 707 shared keys, 11 differing = identity
and wall clock). Reproducibility: `imp_cb1`/`imp_cb1r` md5-identical, 223/223 columns, 3200 rows,
0 differing. The three quotable classes (circle 0.25/4 kg/8 -> torso, square 0.30/6/12 -> head, rect
0.60/12/15 -> arm) differ from the #8 rows in **three keys only** (`buildGuid`,
`sourceFingerprint`, `wallSeconds`) - no published quantity moved between the two builds; all three
still fall (0.605 / 0.565 / 0.475 s), `stepsAfterImpact 0`, push-back at frames 27/24/18.

**Correction of #8 section 6.** That section read the missing vertical CoM off the 217-column
`TrialRecorder` trace and named "publish the vertical CoM" as the next instrument. The impact run
writes a second trace, `<label>-impacts.csv` (`ImpactRecorder.cs`, 31 columns), which **already
carries `comX` and `comY`** - the pair `Accumulate()` turns into the excursion and the drop. New
tool `Tools/impact-com-axes-check.py` recomputes both peaks from that trace: over **12 throws of 6
labels** the record equals the trace to the CSV's own 1e-6 m quantisation, contact frame found at
`dt 0.000 ms` (including throws 2 and 3 of multi-throw takes) ->
`IMPACT-COM-AXES-CHECK OK problems=0 labels=7`. So no recorder needs a new column; a drift audit of
the CoM pair must diff `-impacts.csv`, and it now does.

That diff is where the two builds differ, and it names the owner: quiet stand, head class and arm
class are trace-identical; the torso class first differs at t **12.425 s** (the run's own fall was
at 8.715 s), and the earliest movement anywhere is `armPlantFoldL/R` `-102.4963 -> -18.8712` at
t 9.690 s - the arm lane's ARMS CYCLE 13. Post-fall arm behaviour, recorded not judged, no card key
moved.

Evidence: `impact_current_build.mp4` == `impact_three_throws.mp4` (54.15 s, 1083 f, 1920x1080, md5
`93ddd0278919397bcaa23ce1332b44a5`), three 24.05 s takes, 12 stills (4 per take), check
`IMPACT-EVIDENCE-CHECK OK stills=12 problems=0` (every still repainted vs the archive, PSNR
30.2-32.0 dB), in-plane `OK runs=8 bad=0`, folder lint `OK`; caption read off the rendered frame
says `build 82fbb1ecf3c8 | source 8ba620b883d9`. Gallery-card rule zero intact
(`walk1_load4.json` md5 `3ad0a04a`, `dog_art1.json` md5 `4141622a`). Report
`Trials/Runs/impact-cb9-report.md`; dumps `Logs/ld9-checks.txt` / `-checks2.txt` / `-rows.txt` /
`-evidence.txt` / `level-cb9-runs.txt`.

Still open, NOT this lane: L1 `latencyActiveMs 5.000114` = one fixed step (the metric reads when the
blow ARRIVES; `BalanceController*`, walk stream) and L3 has no active-reaction-off switch, so the
passive/active split still has no run.

## PROGRAMMER CYCLE 20.09 ~12:20: a RENDERED take is a different run on the human trial, and the walk's 3 m bar moves 0.38 m with the camera (programmer)

`build-status.txt` was OK (12:08) and the player carries the uncommitted crouch edit (dll 10:18 >
source 10:06; `c6zc_squat.json`'s own `applied` block reads `crouchKneeFlex 109.0` /
`crouchPelvisTilt 54.0`), so this cycle went to the Assets-changed case. It reproduced the 6zc
headline to the digit (deepest pelvis at `crouchLevel >= 0.99` **-1.3243 m**, drop **0.3336 m**
against `c6y_squat` 0.2933 -> **+0.0403 m**), reproduced the squat rule zero, and passed
`impact-json-lint.py` OK at cycle start.

**COLUMN TRAPS on the newest instrument.** The walk 6x/6y block is live, not dead: on `c6y_walk`
(`-poseDrive 1 -poseWalk 1`) `capturePosePlaceXM / SwingSoleXM / StanceSoleXM / PlaceLeadM /
ReachXM / HipXM` publish on 498 of 8001 frames with the reach ceiling a positive 0.10 .. 0.86 m.
They read 0 on all 8001 rows of `burn_walk` and `p_auto` because those labels are launched
WITHOUT `-poseDrive 1` **by design** (`grok-trial-run.sh:117`, `walk-probe.sh:13` = the level-1
rule-zero rows; `captureWalkPath` 0 on every row). A reader who tests the new columns there will
call them dead.

**THE FINDING — one flag apart, a different walk.** Same player (fp `951769c3`, guid `f4927b60`),
verbatim `walk-probe.sh` flags, fresh labels (`pc_vischeck_head` / `pc_vischeck_vis`), the second
run adding only `-visual`: the fall clock (7.645 s), the step count (2) and the walk onset
(2.995 s) are identical, while **comTravelX 2.7377 -> 3.1158 m** (comX span from the traces
2.7431 -> 3.1206), `armPressStrokesL/R` 5 -> 3, `maxAbsPelvisAccY` 520.8 -> 361.4,
`wallPerSim` 0.6187 -> 1.0671. 91 of 238 shared keys differ; 4 are identity/clock and **87 are
physics**. Both are individually reproducible: the headless run reproduces `p_auto` to the digit
and the rendered run reproduces `burn_walk` to the digit. `-visual` touches no physics (its only
consumers are sprites, camera, skeleton bars, art; the collider stays) — the owner is the harness
clock: the human loop samples once per coroutine iteration on `WaitForFixedUpdate`
(`HeadlessTrial.cs:3434-3476`, `FixedStep` at `:113`), i.e. **once per FRAME**, while Unity runs a
variable number of `FixedUpdate` steps per frame — the exact class the dog trial documented and
fixed with an executed-step counter (`HeadlessTrial.cs:397-405` + `dogSteps`), which the human
loop has not adopted.

**Cost.** The walk target is `>3 m`: `burn_walk` (rendered) reads 3.1158, the headless probe of the
same protocol `p_auto` reads 2.7377 on every fire since 10:09 — and the video Yury is shown is the
rendered take, not the run the row is graded on. **Correction of `walk-cycle6zc-accept.md`:** its
"rendered vs headless ... every one identity/clock or last-digit" is wrong — 86 differ but only 5
are identity/clock and 81 are physics keys (`pelvisDrop` 0.6897 -> **0.8706 m**, `minPelvisY`
-1.6800 -> -1.8608, `armPressLiftMaxMeters` 0.1329 -> **0.3708 m**,
`armPressShoulderMaxDegL` 63.83 -> **41.13 deg**). What survives is the narrower statement that
card needed: the **crouch hold's** own quantity is identical on both takes (deepest pelvis
-1.3243 at t 6.015, 71 rows at full crouch) — the takes diverge after the release, not during the
hold. New tool `Tools/pc-visual-clock-ab.py A B` separates identity/clock from physics on any
headless/rendered pair; report `Trials/Runs/programmer-cycle-20.09-visual-clock.md`.

Also: `p_auto` is a reused label rewritten in place every ~31 min (`walk-history.jsonl`), so any
number quoted from it has a ~30 min shelf life; and the 6zc acceptance is stamped 12:07:25 while
carrying a section headed "12:15-12:25" — the queue fix it claims there IS in place (6z mtime
12:07:14 / stamp 1789895234 now beats 6zc's 1789887125). No source edit, no build, no physics or
parameter change was made by this cycle.

