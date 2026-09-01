# Now (door for a new chat)

Read this and `.cursor/rules/`. Do not copy chat history.

**Documents are English only, always.** See `docs-english.mdc`.

## Object

The human is assembled in `Human.cs` (public fields). No prefabs. Scenes:
`SampleScene` (stand, do not touch), `World` (game, `birdCount = 0`),
`Bird` (one crow + AI), `BirdFlock50` (flock AI), `Chicken` (hens),
`Tree` (plants), `Dog` (quadruped).

Stance baseline: `Trials/Baselines/stable-pd/`. Folder `Baselines/current/`
is the old ostrich knee. Document map: `DOCS.md`. Do not delete
`Trials/Baselines` or `Trials/Runs`.

Trial proof: `Trials/Runs/<label>.json` → `applied`.

## Open

| Who | Task | Card |
|---|---|---|
| physics | WALK-1: stance 8 holds; Play one-leg “freeze” = Stance timer; walk-only Transfer unstick (`walk_freeze_fix.md`); lift <0.35 open | `.cursor/tasks/WALK-1-physics.md` |
| physics | deep crouch WIP intentionally leaves code defaults 98/28 after `crouch_accept`; accepted journal remains 55/12 until the physics work closes | HumanPhysics continuation |
| visual | code hand is `0.06×0.32`; card still wants `0.19`; walk fold at 0.19 is recorded | `.cursor/tasks/VIS-HAND.md` |
| docs | pointers and baselines; do not clean Runs | `DOCS.md` |
| optimization | P1 + safe P2/P3 closed; World cycle 39 known work 0.414 ms. PERF-5 Flock 1/2/5/10/50 wallPerSim 0.51/0.55/0.73/0.96/1.00; empty callback candidate 0%, reverted | `.cursor/tasks/PERF-1-optimization.md`; archives `PERF-4-runtime-cache-cleanup.md`, `PERF-5-bird-flock.md` |
| level | Test1 Sol: tape to 96, curb faces, back fence picture; grove Xs unchanged | no new card |
| bird | BIRD-9 tree1 accepted: `perchLandCount` 5, `fell` false; wood ghost and `BirdPerch` pads; 50-bird shared-perch stress is not the one-bird acceptance | `BIRD-9-tree-ghost.md`; `perf_bird_flock_01.md` |
| plant | five kinds (oak canon); Tree scene row of 5; hold still oak | `.cursor/tasks/PLANT-1-tree.md` |
| visual | bark/leaf placeholder; physics sizes stay | `.cursor/tasks/VIS-TREE.md` |
| visual | dog parts in `Art/Dog` (chest…paw); sizes unchanged | `.cursor/tasks/VIS-DOG-textures.md` |
| visual | crow / chicken parts in `Art/Crow` and `Art/Chicken` | `.cursor/tasks/VIS-BIRD-textures.md` |
| ai | BIRD-9 perch pads + wood ghost; hen Peck / voice still stand | `BIRD-9-tree-ghost.md` |
| dog | 10 stand cycles done — wiring yes, stance no (`dog_cycles.md`); CPU×3 next | `.cursor/tasks/DOG-1-physics.md` |

## Closed — do not open

Forces in `Update`, 100/50 Hz in prod, merge pelvis+torso, amputate arms,
SIMBICON, retune PD for FPS. PERF-2: keep 200 Hz (`perf_wave2_timestep.json`).

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

## Roster

`.cursor/ROSTER.md` and `.cursor/agents/` (`physics`, `visual`, `level`,
`bird`, `optimization`, `ai`, `docs`). The five-model college is not these
profiles.
