# Document map

Document manager: agent `docs` (`.cursor/agents/docs.md`).
Live chat door: `GOAL.md` (the single statement of the current goal). `CURRENT.md` is pointers
only. This file is pointers, not the body journal.

**Language:** English only, always. Rule: `.cursor/rules/docs-english.mdc`.

Nightly check: Windows task `Unity-Docs-daily` at **02:00** local time,
script `Tools/run-docs-daily.ps1`, card `.cursor/tasks/DOCS-DAILY.md`.
Do not put this on Cursor cloud Automations: no git, the VM cannot see the
folder (same as GPU-10). Night report: `Trials/Runs/docs_daily/` and
`Trials/Runs/docs_daily_latest.md`.

## Where truth lives

| Question | File | Do not take it from |
|---|---|---|
| What the line is doing now | `GOAL.md` (checklist + evidence) | old chats, other rounds' briefs |
| What is open today | `CURRENT.md`, `.cursor/tasks/OPEN.md` | old chats |
| Engine channel / upgrade calls | `docs/ENGINE-6.6.md` | scout digests, chat |
| Body numbers and gains | `.cursor/rules/current-front.mdc` | `Trials/Baselines/current/` |
| Stance acceptance | `Trials/Baselines/stable-pd/` | the folder named `current/` |
| How to run the stand | `.cursor/rules/trial-protocol.mdc`, `Trials/README.md` | Play Mode |
| Ball hail into the standing human (line 2) | `Trials/Runs/ball-audit-summary-2026-09-28.md` (rows: `ball-restab-2026-09-27.md`) | an UpperArm `e_rel` as a material verdict — limb rows are kinematics, not the material |
| World frame cost | `Trials/Runs/perf_wave1_lab.md` (+ `.json`) | `renderAvgMs = 0`, `busyMain` |
| What each agent may optimize | `Trials/Runs/opt_agent_proposals.md` | cutting 200 Hz, retuning PD |
| Off-camera NPC rest | `.cursor/tasks/OPT-3-distant-rest.md`, `NpcRest` | sleeping the hero, `enabled = false` on bird sensors |
| Latest optimization verification | `Trials/Runs/opt_code_audit_31.md` | invalid Flat cycles 31/32 |
| Bird Flock capacity | `Trials/Runs/perf_bird_flock_01.md` | ragdoll birds, empty callback candidate |
| Bird code audit | `Trials/Runs/opt_bird_code_02.md` | cutting flap rate, ragdoll flock of 50 |
| Torque signs | `.cursor/rules/balance-actuators.mdc` | guessing from the angle sum |
| Agent roster | `.cursor/ROSTER.md`, `AGENTS.md` | college in `~/.cursor/agents/` |
| Brains | `.cursor/rules/ai.mdc` | forces, PD, `BirdFlight` |
| Life state (every brain) | `ai.mdc` + `LifeState` / `OrganismLife` (card `BIRD-10-life`) | `BirdMode`, Sit as Sleep |
| Factions | `ai.mdc` + `FactionMember` / `FactionTable` (card `FACTION-1`) | species as a side, stamp in `FixedUpdate` |
| Insect hunt | `ai.mdc` Hunt + `Insect` (card `BIRD-13-insect-hunt`) | swallow force, `BirdHunt` life root, insect crowd in World |
| Voice | `OrganismVoice` / `SoundBus` (card `BIRD-6-voice`) | mixer as ears |
| Beak | `Beak` segment + `BeakStrike` (card `BIRD-8-beak`) | chest / wing hit |
| Tree sit | `BirdPerch` pads, wood ghost (`BIRD-9-tree-ghost`) | landing on trunk colliders |
| Static grove | `Trials/Runs/plant_static_cheap_02.md` | Sway hinges in World, wood RB on grove |
| Garden flowers | `.cursor/rules/plant-physics.mdc`, card `PLANT-3-flowers` | `PlantTree`, perch, World bed |
| Dog stance | `.cursor/rules/dog-physics.mdc`, `Trials/Runs/dog_cycles.md` | human `BalanceController` |
| Dog look | `Assets/Resources/Art/Dog/`, card `ART-DOG` | dog sizes / stance gains |
| Bird look | `Art/Crow/`, `Art/Chicken/`, card `ART-BIRD` | `BirdFlight` / `BirdKind` sizes |
| Painted parts | `.cursor/rules/painted-parts.mdc` | judging crops instead of Play |

User-facing concept docs (Russian, edited by Yury): `docs/CONCEPT.md`,
`docs/AI.md`, `docs/ENGINE.md`, `docs/STATE.md`. Hermes reads these and
applies changes to the game; they are the interface layer above the
English `.cursor/rules/*.mdc`.

`current-front.mdc` is a journal. **Append** newly accepted numbers. Do not
scrub old paragraphs.

## Do not delete or rename

The folders below are trial memory. "Cleaning Runs" deletes proof.

| What | Why |
|---|---|
| `Trials/Baselines/*` | frozen baselines; name `current/` is **historical** |
| `Trials/Runs/*` | working summaries; the label is provenance |
| `.cursor/tasks/archive/` | closed waves |
| `E:\Games\Unity3D LOGS\trials` | CSV/JSON from before the repo copy |

Do not rename `Baselines/current/` for clarity: rules and old reports point
at that name. Read it as "ostrich knee".

Version history: git (since the 02.10.2026 reorg; branch `feat/dog-stance-10`,
checkpoints `8e9dd5d8` + `448eb3a9`; the offsite orphan `snapshot/2026-10-02`
holds the code without Trials captures - the branch itself is too heavy to
push, 3.2 GiB pack rejected by GitHub). `Trials/Runs` and the capture folders
are untracked by design. Deletion of tracked files is recoverable via git;
untracked captures are not.

## Baselines (`Trials/Baselines/`)

| Folder | Status | What is inside |
|---|---|---|
| `stable-pd/` | **acceptance** | SPD, runs `s_on` / `son_*` |
| `pelvis-world/` | history | pelvis to world upright, before SPD |
| `knee-flipped/` | history | knee `0…+120`, pelvis not yet world-controlled |
| `current/` | history | `curr_*`, ostrich knee; do not read the name as "current" |
| `legacy-no-provenance/` | history | `damp_*` with no fingerprint; do not backfill provenance |

A new baseline only if a card said so. Otherwise the anchor is `stable-pd/`,
fresh numbers from `Trials/Runs/<label>.json` → `applied`.

## Rules (`.cursor/rules/`)

Always in chat: `project-context`, `current-front`, `trial-protocol`,
`agent-handoff`, `ai`, `docs-map`, `docs-english`, `perf-budget`.
By layer: `balance-actuators`, `unity-2d-physics`, `level-generation`,
`lighting-2d`, `bird-physics`, `plant-physics`, `dog-physics`, `character-ai`.

## Known contradictions (do not erase; fix the pointer)

1. **Baseline.** Root `README` and `Trials/README` used to name
   `Baselines/current/` as canon. Since 2026-08-30 the pointer is
   `stable-pd/`. The `curr_*` files stayed on disk.
2. **Hand.** In `Human.cs` default `handSize.y = 0.32`. Card `ART-HAND`
   still asks for `0.19`. `current-front` records that `0.19` folded walk
   at ~25 s and the physical length was restored to 0.32. Stance `vis_hand`
   at `0.19` was accepted on its own. Do not close the card silently.
3. **CSV.** `stable-pd/` and `knee-flipped/` have JSON+CSV side by side.
   `current/`, `legacy-no-provenance/`, `pelvis-world/` are mostly JSON in
   the project. Full CSV lives in the external log folder.
4. **Walk gate Extra (17.09).** Two JSON floors, not the folder name
   `Baselines/current/`. Dead WalkLift: `p_now.json` / `p_base.json`
   (`applied.useCaptureWalker` 0) — fell 7.555 s, `walkSwap` 0, CSV
   `walkPhase` only −1 and 0, single support 0.40 %, both feet until
   the fold. Live CaptureWalk: `p_cap.json` (player 03:06 `ebfd67e1…`,
   Extra `-captureWalk 1`) — single support 34 %, both feet 62 %, first
   single support 5.22 s, 2–3 swaps, then fold ~5.8–6.29 s, travel
   1.37 m. QA card `Trials/Runs/walk_acceptance_checklist.md` now requires
   Extra `-captureWalk 1` and `applied.useCaptureWalker` 1; do not Extra
   WalkLift. `CURRENT.md` Walk front (16.09, build `c125adbd`) still
   says `-captureWalk 1` also folded 1.3 s after start with 0–1 swaps;
   JSON `p_cap` supersedes that door line. `current-front.mdc` still
   says VEST-2, not WalkLift (dispatcher appends; this profile does
   not). Until `walk_acc_walk` JSON exists after `-Rebuild`, 40 s /
   swap ≥ 6 / travel > 3 m is UNVERIFIED.
5. **CaptureWalk launch squat (17.09, Yury).** Pre-fall CSV, not JSON
   `pelvisDrop`. Walk start kneeL 6.5 / kneeR 3.4; 5.30 s 68.8 / 52.3;
   5.6 s both 120°; pelvis 0.456 m; comX +0.16 m. The launch bends both
   knees (a squat) instead of swing-flex / stance-extend. JSON
   `burn_walk` drop 0.8443 / torso 113.8° is the fold after that squat
   (`fell` true, 6.32 s, swap 1, travel 1.22 m, `useCaptureWalker` 1).
   `CURRENT.md` Walk front now has this squat note but still prints the
   c125adbd 0-swap / 100 % bothFeet table — that table is the dead
   WalkLift path. `current-front.mdc` still lists VEST-2 and
   `walk_mid1…5` (4 swap, drop 0.0019); dispatcher appends, this profile
   does not. Gate after `-Rebuild`: drop < 0.03 m on the launch, not
   the 0.84 m dump. Physics 04:20 claims a source fix (walking-chord
   stance ceiling, swing cap 70°, StandingRestore before Tick) in
   `BalanceControllerCaptureWalk.cs`; still UNVERIFIED — no
   `walk_acc_walk` JSON. Physics 04:26: JSON `fell` at 6.32 s is
   `RecoveryStep` `fallOffset` vs `fallCoMOffset` 0.30 m (mid-feet
   0.225 m, both soles down); the squat is the earlier 5.30–5.60 s
   window. `CURRENT.md` acceptance still says single support above
   20 %; this gate and `walk_acceptance_checklist.md` are > 25 %.
   Dispatcher owns CURRENT.md. Build OK 04:24. OPEN.md now indexes
   `WALK-2.md` as CaptureWalk canonical, FallCatch leftover.
6. **CaptureWalk freeze after launch gate (17.09 07:19).** Overlay /
   Yury: from `walkTime` 5 s the gait stays in phase 0 for 35 s, both
   feet 100 %, standing man. JSON `p_auto` (mtime 07:18, player
   `9a8020fa…` / 07:09, Extra 6/6 + `-captureWalk 1`): `fell` false,
   40.0043 s, `walkSwap` 0, `captureWalkStepCount` 0, `comTravelX`
   0.0668 m, SS 0 %, `pelvisDrop` 0.0141, `finalWalkPhase` −1 (not 0).
   Chat comX 0.069 m matches travel, not a new run. Before the launch
   was touched the same trial moved ~1.2 m: contradiction 5
   `burn_walk` / `p_cap` (~1.22–1.37 m, then fold). Do not drop the
   onset (lean, then lift ONE foot). Squat fix is swing knee only;
   stance knee < 15°, drop < 3 cm. `CURRENT.md` Walk front still
   prints both the dead 100 % bothFeet table and the 34 % SS capture
   step; dispatcher owns that file. No `walk_acc_*` yet. Gate: 40 s,
   swap ≥ 6, travel > 3 m, SS > 25 %.
   07:40: player on disk is `1fa5ac06…` (buildTimeUtc 04:32:58Z /
   07:33 local, guid `18824bfb…`). Still no `walk_acc_*` JSON on that
   fingerprint. Last walk JSON remains `p_auto` 07:18 (`9a8020fa…`).
   Physics 07:28 `walkGeoSwingLiftClearance` 0.12 and later
   RecoverStandReady source edits are UNVERIFIED. Orchestrator stamp:
   Build OK 07:33, no `-Rebuild`. `current-front.mdc` still lists
   `walk_mid1…5` (4 swap); dispatcher appends, this profile does not.
   08:03: last successful player is `db35285c…` (buildTimeUtc
   04:43:46Z / 07:43 local, guid `7d57e4d4…`). JSON `p_auto` on that
   fingerprint is the same freeze (Extra 6/6 + `-captureWalk 1`):
   fell false, 40.0043 s, swap 0, travel 0.0668 m, SS 0 %. Rebuild
   BROKEN 07:54 (Tundra interrupt). Physics 07:41 / 07:50 source
   still UNVERIFIED. No `walk_acc_*`. Orchestrator must `-Rebuild`.
   08:26 docs: last successful player is `f8b9108c…` (08:05, guid
   `2be27aa1…`). Orchestrator BROKEN 08:16 (sources after 08:05 STALE).
   Floor remains `p_auto` on `db35285c…` (Extra 6/6 freeze). Physics
   08:20 / 08:22 CaptureWalk source still UNVERIFIED. Gate Extra 8/8 +
   `-captureWalk 1`. Orchestrator must `-Rebuild`.
   10:11: control still phase 0 / 35 s / comX 0.069 m / swap 0 after
   Editor compile OK 10:08. Physics 10:08 source
   (`GaitStateMachine` / `BalanceControllerCaptureWalk`: first Dual
   → Swing after lean AND CoM `captureLaunchLead`) is UNVERIFIED —
   no `walk_acc_*` JSON. QA player 09:57 `b2ae9f9c…` is STALE vs
   those Assets. Floor remains `burn_walk`/`p_auto` Extra 6/6 freeze
   (40.0043 s, fell false, swap 0, travel 0.0668 m, SS 0 %).
   Orchestrator must `-Rebuild` Extra 8/8 + `-captureWalk 1`.
   Do not touch the onset.
   10:41: orchestrator Build OK (`build-manifest` fingerprint
   `50c55b7b…`, guid `bd1da1a7…`, buildTimeUtc 07:41:21Z). Control
   still: no `-walk 1` phase −1; with `-walk 1` phase 0 at 5.0 s,
   then DualSupport freeze 35 s / bothFeet 100 % / comX 0.069 m /
   swap 0. Physics 10:18 CoP and 10:31 `TryEnterCaptureFirstSwing`
   UNVERIFIED — no `walk_acc_*` JSON on this player. Floor remains
   `burn_walk` (`b1eac66f…`, Extra 6/6) / `p_auto` (`b2ae9f9c…`):
   40.0043 s, fell false, swap 0, travel 0.0668 m, SS 0 %,
   drop 0.0141. Gate Extra 8/8 + `-captureWalk 1`. Do not touch
   the onset. Run `walk_acc_walk` on the 10:41 player without a
   second `-Rebuild`. CURRENT.md Walk front still SS 20 % vs this
   gate and `walk_acceptance_checklist.md` > 25 %; dispatcher owns
   CURRENT.md. `current-front.mdc` still `walk_mid1…5` (4 swap).
7. **SinkCollapsed false-positive (17.09 12:56).** Quiet stance is not
   a 0.28 m fold. `StepPhaseDriver.SinkCollapsed` compares CoM y to
   `standComHeight`; the fallback is `GeometricStandingComY` (foot
   centre + pelvis/torso/2 + thigh + shin + foot/2 ≈ y −0.82). Live
   CoM sits ~0.28 m below that column, so drop > `sinkAbortDrop` 0.25
   and StandingRestore/Surrender keep `CaptureWalkActive` bailing.
   `TryCaptureSpawnStandHeight` still `Max(measured, geometric)`.
   Freeze JSON: `p_auto` / `p_sink05` — `walkOnsetCollapsed` True,
   posed False, swap 0, travel 0.0668 m, drop 0.0141. Probe `p_sink0`
   (`applied.sinkAbortDrop` 0.001): travel 0.9676 m, bothFeet 0.1304,
   `standLegBothFeetGroundedFraction` 0.0066, drop 0.881, swap 0. Do
   not ship that Extra as the fix. CURRENT.md Walk front still prints
   DualSupport freeze / launch squat / 1.3 s fold; dispatcher owns
   CURRENT.md. `current-front.mdc` still `walk_mid1…5`. No
   `walk_acc_*`. Physics must retarget the spawn CoM reference; then
   `-Rebuild` Extra 8/8 + `-captureWalk 1`.
8. **CaptureWalk skate after 40 s clock (17.09 16:23).** `fell` false
   is not a walk. Last labeled JSON `burn_walk` (`1b47e7ff…`, 16:14,
   Extra 6/6 + `-visual` + `-captureWalk 1`): 40.0043 s, travel
   1.6539 m, speed 0.0413, `walkSwap` 0, `captureWalkStepCount` 0,
   torso 82.8137°, `pelvisDrop` 0.8496, GETUP attempts 1 / success
   false / GettingUp 33.885 s / fall 6.14 s. Disk player 16:23 is
   `7e8437b2…` (guid `f626ee71…`, buildTimeUtc 13:23:13Z); hourly
   `p_auto.json` on that fingerprint (Extra 6/6, no `-visual`):
   travel 1.4218 m, tilt 89.29°, drop 0.8492, swap 0. Gate Extra
   8/8 `walk_acc_*` still missing. CURRENT.md Walk front still
   prints 1.3 s fold / 9 mm / bothFeet 100 % and SS 20 %; JSON
   supersedes. `current-front.mdc` still `walk_mid1…5` (4 swap);
   dispatcher appends. Physics HeelPin 16:28 UNVERIFIED. Do not
   treat clock survival as PASS.
   16:26: same `7e8437b2…` `p_auto` (Extra 6/6 + `-captureWalk 1`):
   first transfer is harder than DualSupport freeze. CSV first
   walk second: kneeL 110.10° at 5.995, pelvis sag 0.257 m, SS
   60.7 %, `walkPhase` stuck at 0 from 5.71. Fold `finalTorsoTilt`
   −52.90° by ~8 s; freeze t 15–40; bothFeet 1.0 after 8 s; swap
   0; travel 1.4218 m; `fell` false is the freeze. GETUP 5.71 /
   34.30 s / success false / locomotion 4. Disk player 16:45 is
   `78d5b753…` (guid `5724f7c5…`, buildTimeUtc 13:45:20Z). No
   `walk_acc_*` on that player. Gate Extra 8/8 + `-captureWalk 1`.
   Orchestrator: Build OK 16:45, no `-Rebuild` unless STALE.
   Physics GETUP tuck 16:44 UNVERIFIED. Target: swap ≥ 6, travel
   > 3 m, sag < 3 cm.
9. **CaptureWalk first second then GETUP freeze (17.09 18:13).**
   Number truth is JSON, not the folder name. Hourly `p_auto.json`
   mtime 18:06 (`b7e64e2b…`, player 18:01, Extra 6/6 +
   `-captureWalk 1`) **overwrote** the 17:32 probe (`053e2935…`,
   rebuild 17:29, CaptureWalk 17:24). First walk second recovered
   on both: 1 swap, SS 66.2 %, travel 1.43 m, knee ~56.5°, sag
   13.5 cm. Then the fold: 17:32 drop 0.75 m / pelvis 128° /
   GETUP 6.2 s / 33.8 s; 18:06 drop 0.8685 / pelvis 119° /
   GETUP 6.225 / 33.495 s / travel 1.5945 m. `finalWalkPhase` −1,
   `fell` false is freeze, not a hold. Disk player 18:13 is
   `c234b549…` (guid `79d2f670…`, buildTimeUtc 15:12:59Z). No
   `walk_acc_*` on that fingerprint. Physics 18:12 (keep first
   second past 6 s) is UNVERIFIED — do not read 18:06 as that
   test. `CURRENT.md` Walk front still prints 0-swap / 1.3 s /
   SS 20 %; JSON supersedes. `current-front.mdc` still
   `walk_mid1…5` (4 swap); dispatcher appends, this profile does
   not. Gate Extra 8/8 + `-captureWalk 1`. Target: swap ≥ 6,
   travel > 3 m, sag < 3 cm.
10. **CaptureWalk two-swap stall then GETUP (17.09 18:45).** Number
    truth is JSON, not chat. Last walk JSON `burn_walk` mtime 18:24
    (`1bd0ce94…`, Extra 6/6 + `-visual` + `-captureWalk 1`):
    `fell` false, 40.0043 s, `walkSwap` **2**, travel **1.314 m**,
    `bothFeet` **0.6624**, whole-run SS **8.27 %**, first-second SS
    **52.2 %**, `pelvisDrop` **0.8672**, GETUP 6.355 / 33.67 s,
    `walkOnsetCollapsed` false. Chat “SS 66 % / pelvis drop −0.135 m”
    is not those keys: 66 % is `bothFeetGroundedFraction`; −0.135 is
    an onset-log CoM margin, not `pelvisDrop`. Two swaps in 40 s is
    a **transfer orbit stall**, not the 17.09 launch squat. WalkLift
    stays dead (`applied.useCaptureWalker` 1). Disk player 18:45
    `9c966b55…` (guid `1cb7e7d1…`, buildTimeUtc 15:45:49Z). No
    `walk_acc_*` on that fingerprint. Physics 18:30 standing Plant
    and 18:35 `captureResumeOnset` CS0103 removal are UNVERIFIED.
    Assets after 18:45 (including `BalanceController` 18:49) make
    FreshnessOnly STALE — orchestrator must `-Rebuild` Extra 8/8 +
    `-captureWalk 1`. `CURRENT.md` Walk front still prints 0-swap /
    1.3 s / SS 20 %; JSON supersedes. `current-front.mdc` still
    `walk_mid1…5` (4 swap); dispatcher appends, this profile does
    not. Gate: swap ≥ 6, travel > 3 m, sag < 3 cm, SS > 25 %.

## Tools

Stand and measure scripts: `Tools/README.md`.
