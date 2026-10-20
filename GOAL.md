# GOAL (2026-10-03 03:50) — the assisted human

Owner's decision, 03.10 ~03:40: balance from joint torques alone is no longer the game's path.
The human keeps its full physical body (muscles, joint limits, contacts, honest falls) and gets a
bounded, invisible balance assist (`Assets/BalanceAssist.cs`, switch `-assist <level>`).
The pure-physics version stays one switch away (`-assist` absent) and keeps its fences
byte-identical, so every result is compared with it in one click.

Code: Claude only (claude-lab cycles, `F:/hermes-data/claude-lab/PROTOCOL.md`). The line executes
requests and reports; no own hypotheses, no tuning, no code edits.

## Acceptance (numbers; the pure version runs alongside every assisted take)
- [ ] A1 Stand: 60 s quiet stance, no fall.
- [ ] A2 Push test (`-pushAt` ladder on the torso): pushes up to 20 N*s recovered, a fall at or
      below 70 N*s (not a marionette); the threshold repeats on 3 runs.
- [ ] A3 Door scene (desk plan `i1.5,w6,i2,w6,i2,s5,r2,w8`, 60 s): no fall,
      `door_squat_desk_reps` >= 1 (rises from the squat).
- [ ] A4 Squat law: `squat_clean_reps` = 3 on ctl and land.
- [ ] A5 Walk: s_walk and the door's walk segments without a fall; mean walk speed not below the
      pure version by more than 10 %.
- [ ] A6 Honesty caps: assist load mean <= 0.05 and p95 <= 0.15 body weight in quiet stance; zero
      while airborne; fades with the trunk past 45-75 deg (from `<label>_assist_summary.txt`).
- [ ] A7 Gets up after a fall (after A1-A6).

## Evidence — v14 assist (claude-034 KEEP, commit 7a29dd1b)
Met WITH KB; the launcher (`Scenes/SampleScene.cmd`) still passes K (no `-clStandLp`); `-clStandLp`
below 80 falls at the walk entry / final stop (40: 27.1 s); the push ladder still ends on the ground
(`cl033_push_KB`).
K = `-assist 1 -assistGain 1.5 -assistCenter 4 -assistHoldK 30 -assistCenterFree 1 -clAnkleCap 0.6
-assistAnkleCrouch 0.5 -assistWalkDir 1 -assistBudget 80 -clStanceClose 1 -clWalkEntry 2
-assistSettleWalk 1 -assistBrake 0.08 -assistBrakeFloor 0.45 -clStanceCloseRearm 8`; KB = K `-clStandLp 80`.

| goal row | take | md5 | numbers |
|---|---|---|---|
| door 60 s + long stand 600 s | cl033_door_K_lp80 | 82e88a04 | reps 1 clean 1, dpy +0.002, dAnk 0.079, 0 thumps / 0 airborne / buzz 0.000 |
| longest stand | cl033_door_KB_1200 | e9c1bc36 | stands sim 0-1152.8 s (wall-cut take), 0 / 0 / 0.000 |
| the game's flags + KB, 600 s | cl033_game_KB | d50d51ab | reps 1 clean 1, 0 / 0 / 0.000 |
| squat law under KB | cl033_ctl_KB / cl033_land_KB | 1c90db99 / d0e82464 | 3 clean reps each |
| pure physics untouched | cl033 standard set | 11/11 == c129a | default flags |
| video | Runs/cl033_door_KB_1200_vis_0-60.mp4, _90-120.mp4 | - | visual run, sim 0-554.8 s |

The claude-034 commit is code-identical to what rep-033 measured: the retakes byte-match cl033
(`cl034_door_K` = f49ab59c..., `cl034_door_KB` = 82e88a04..., `cl034_game_KB` = d50d51ab...),
standard set 11/11 == c129a at default flags (rep-034).

## Evidence — v15 assist cap (req-085 part 1 C2 KEEP, commit d3ddec1d)
`-assistFmax 0.5 -assistBudget 100000` now ride in `Scenes/SampleScene.cmd`; the rest of K is unchanged
(`-clStandLp 80` still in). cl085 battery (HEAD 625e2ef0, fp b700599f, PAR 7, 121 takes, 0 failed; map rows
carry `-walkPlanBrake 0`): map A 38/41 (bar == 38), held-out map B 38/41 (bar >= 35), restart W2a 11/12 +
W2d 9/12 = 20/24 (bar >= 18), ctl / land 3 clean reps each, s_stance 600 s STAND; std set 11/11 == c129a and
`cl085_K_w400 == cl059_K_w400`. loadBw mean 0.0132 BW (map A) / 0.0140 (map B) against the owner's 0.10
ceiling (ceiling, not a target); worst single row 0.1243 (map B), max == the 0.5 cap. Evidence:
`F:/hermes-data/claude-lab/reports/rep-085.md`, `metrics/cl085-judge.txt`, `Trials/Runs/cl085_*`.

## Evidence — v16 entry mask + straight-legs (req-086 KEEP, commit 33b6faa7)
`-clEntryMask 1 -clEntryStraight 25` ride at the end of K_FLAGS in `scripts/scene-cmd-update.py`
(patch req-082-2, audited; the switches stay default-off in the code). cl086 battery (HEAD 4d425695,
fp bca0f33d, PAR 7, 378 takes, 0 failed; map rows carry `-walkPlanBrake 0`): RL over FS 75 - restart
M0 43 vs M2 50, stop2 38/43 vs 44/50, entry kind ready/timeout 40/3 vs 48/2, wait mean 3.08 vs 3.04 s
(bar <= +0.5); the maps under M2 == the M0 maps in letters and fall times (82/82 pairs, 0 differ);
ctl / land 3 clean reps each, s_stance 600 s STAND; std set 11/11 == c129a and `cl086_K_w400 ==
cl059_K_w400`; S 34/35 next to map A 38/41. Landing: launcher map A == M0 map A (0 of 41 differ),
W2 a/d 19/24 (bar >= 18), S on the landed launcher 34/35. Evidence: `reports/rep-086.md`,
`metrics/cl086-judge.txt`, `metrics/cl086-landing.txt`, `Trials/Runs/cl086_*`.

## Evidence — v17 release ramp + press (req-095 landing via req-097, commit 8ba3e1e2)
`-clReleaseRamp 2.0 -clReleaseRampCatch 0.95 -clReleaseRampClose 0.05 -clReleaseRampPress 0.75
-clReleaseRampPressSpan 1` ride at the end of K_FLAGS in `scripts/scene-cmd-update.py` (patch
req-095-3; the switches stay default-off in the code). cl097s1 battery (HEAD 5ad569ee, fp cd1828bb,
PAR 7, 122 takes, 0 failed; plan rows carry `-walkPlanBrake 0`): the unseen door family DF
(`i1.5,w6.125-6.5,i1-3,wA,i2,s5,r2,w6`, -squatReps 1) - S (ramp + press) 16/20 vs K (ramp off) 6/20
(R1 = S >= K-2 holds, +10); rearm 3 lock probe: S3 + K3 28 vs S + K 22 (R2 bar +6, met at 28); the
two-stops-in-8-s chain unchanged: DA_w600 S75DA == 82ca2b0a, R6DA == 3d228c63 bytes (fingerprints);
cl095r3b bars B1 76/82, B3 48/56, B4 73/82, B5, B7 stand; std 11/11 == c129a, `cl097s1_K_w400 ==
cl059_K_w400`, `K2_w400 == cl086_K2_w400`. Evidence: `reports/rep-097.md`,
`metrics/cl097s1-judge.txt`, `metrics/cl097s1-metrics.txt`, `Trials/Runs/cl097s1_*`.

## Evidence — v18 L/R asymmetry screening flags, default off (req-284 Part B landing via req-322, commit 70704496)
`-poseAimHipAnchor 1` (the reach guard's hipX and poseIn.pelvisX come from the swing thigh hinge's live
connectedAnchor world x; the mode-2 hipDynAim term is suppressed while it is on), `-clReleasePhaseSide
0|1|2`, `-clEntrySwingFirst 0|1|2`, `-buildLegOrder 1`, `-shoulderCenterX 1` are in the code, every one
default off: flag absent == flag 0 == today's bytes. req-322 LAND (base HEAD 8a78256f, patch
`patches/req-284-1.diff` md5 f4eda2b1, 4 files +99/-17, fp 7a0564b7 dll 74def080, PAR 4, 11 std takes,
0 failed): std 11/11 == c129a and the build carries all five switches (new-symbol gate). The flags'
liveness was measured in req-284 (B cell 12/12 byte-identical to the unpatched main build; N1/N4/N5/N6
12/12, N2 10/12, N3 0/12, rep-284). No value is switched on: the A1/A2/N5 levers are skeleton /
geometry changes that need the owner's explicit yes with the picture, then 108 x 2. Evidence:
`reports/rep-322.md`, `metrics/cl322-metrics.txt`, `metrics/cl322_door_probe.txt`, `Trials/Runs/cl322_*`.

## Evidence — v19 player args audit, default off (req-303 landing via req-333, commit c3c58e15)
`-argsAudit 1` is in the code and the launcher lines stay untouched: when the switch is present every
`ReadArg` call site reports READ / DUP / UNREAD to `Trials/Runs/<label>_args.txt`; flag absent == today's
bytes (the 11 std takes write no `_args.txt` at all). req-333 LAND (base HEAD 7d7199b2, patch
`patches/req-303-2.diff` md5 2a7cbd80, 25 files +207/-72, fp 0c746911 dll 48136ac9, PAR 4, 13 takes,
0 failed): std 11/11 == c129a, and the audit on the two Owner launcher lines reads SampleScene
n=109 read=105 unread=4 dup=0 and Уровень 1 n=124 read=120 unread=4 dup=0, the four unread being the
Unity-owned `-batchmode`, `-screen-fullscreen`, `-window-mode`, `-logFile` only. No value is switched on;
CLAMP stays unmarked (no arg helper clamps a number in its own body). Evidence: `reports/rep-333.md`,
`metrics/cl333-metrics.txt`, `Trials/Runs/cl333_*`.

## Evidence — LEVEL 1 steady camera, default off (req-265 round 1 landing via req-282, commit c7a36653)
`-level1CamSteady 1` (needs `-level1Cam 1`) draws the story camera every LateUpdate and wins over
`-camAuto`; the drawn pose and the shot rows are the same pose, capped at 0.12 m/frame; flag absent ==
c129a bytes. req-282 LAND (base HEAD 1b796398, patch `patches/req-265-1.diff` md5 b509f995, 3 files,
fp 91ee351d5954 dll e9cdefcf8725, PAR 4, 18 takes, 0 failed): std 11/11 == c129a, K_w400 0680afa1f3a7,
L1 twin 2b7b8c7906f7, C7 61/61 shots on a drawn pose, C8 0.1200 m, C5 one hero md5 7ed36663c635 over
both arms and seeds. No value is switched on: the `-level1CamSteady 1` line into `Scenes/Уровень 1.cmd`
is Claude's to order. Evidence: `reports/rep-282.md`, `metrics/cl282-metrics.txt`, `Trials/Runs/cl282_*`.

## Rules of this goal
- The assist law has no windows, corridors or gates: one smooth function of the body state
  (capture point vs the soles, trunk tilt, ground contact) with few parameters.
- The body is frozen: Human.cs geometry, masses, limits and muscle ceilings do not change, except
  one deliberate simplification decided by Claude (e.g. a rigid foot) with the ceilings
  recalculated, after which it is frozen again.
- Code rules R1-R5 (order 04): files <= 1500 lines, no history in comments, no dead flags,
  byte-identical fences for refactors, one responsibility per file.

## Status
- req-003 (the first assisted build + the push test): superseded — the assisted line reached v14
  (claude-034 KEEP, evidence above).

## History
The pure-physics checklist and its evidence up to c129: `docs/archive/GOAL-pure-physics-2026-10-03.md`.
