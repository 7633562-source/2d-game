# Dog twin, day 4 (2026-10-25, started 06:27; status 06:46)

Items 1-3 are done; item 4 is trailing in a detached job. The spec lines are dated 2026-10-25 06:45 in
unity-transfer-dog.md: sections 3, 6, 7.2, 7.3, pitfall 3 and section 10.

Input: rep-542 (d5 in the owner's game, route A, box2d gains, tail 35 / K 4), runner logs
`E:/Games/2D Game/dog454/Trials/Runs/cl542_{d8,ds,dr06,dr10}_s07_dogrl.csv` (read only) and the coordinator's four items.

## 1. Game vs twin on D8 seed 7 (2026-10-25 06:40)

Twin rollouts (`tests/t_game_vs_twin.py`, d5, box2d gains, no DR, no noise, the game's plan; `game_vs_twin_<cell>.json`):
- **A**: the twin's own start (stand pose shoulder -20, elbow 24, hip -12, knee 24), b0 = that pose.
- **B**: the game's start (row-0 q / qd of the `_dogrl.csv`), b0 = the game's b0 (= row-0 q, what the runner does).
- **C**: the game's start, b0 = the stand pose (standQ).

Chest height (m) and speed (m/s), the same windows for all. The game's chest height is from the `_dogrl.csv`; its speed
is from comX of the main CSV:

| cell | window | game | twin B (game start + game b0) | twin A (twin start) | twin C (game start, b0 = stand) |
|---|---|---|---|---|---|
| D8 | stand 0-1 s | 0.437 | 0.445 | 0.406 | 0.409 |
| D8 | trot 4-12 s | 0.371, 0.889 m/s | 0.375, 0.880 | 0.375, 0.880 | 0.375, 0.880 |
| DR06 | trot 4-7 s | 0.410, 0.708 m/s | 0.413, 0.711 | 0.401, 0.665 | 0.401, 0.666 |
| DR10 | trot 4-7 s | 0.368, 1.045 m/s | 0.373, 1.052 | 0.373, 1.051 | 0.373, 1.053 |
| DS | stand after the stop, 10-12 s | 0.426 | 0.436 | 0.405 | 0.405 |
| D8 | whole take 0-12 s | 0.383 | 0.387 | 0.382 | 0.383 |

No fall in any twin run. With the game's start and b0, the twin reproduces the game in every cell: chest within
0.3-1.0 cm, speed within 0.01 m/s.

**Where the "3 cm" goes: there is no 3 cm gap in the trot.**
- In the trot, the game is 0.371 and the twin 0.375. The "~0.40" in req-542 was the twin's STAND height at 1 s, not its
  trot height. (The s07 log's own 0-12 s mean is 0.383; the rep's 0.373 is near the trot value.)
- The real difference is in the stand: 0.437 in the game vs 0.406 in the twin's own start. Its cause is b0.
  - The game spawns every hinge at jointAngle 0, the build pose. Row 0 of the runner log has all 13 q within 1.0 deg of
    0. Rep-541 log 2 (the game's own stand) shows the same: the stand drives the legs away from the build pose after
    the spawn.
  - ApplySpawnPoseToStance sets the stand's TARGETS; it does not pose the bodies.
  - So route A (b0 = jointAngle at the 2nd FixedUpdate) gives b0 = ~0 on all 9 non-paw joints, instead of the
    trained stand pose. That is my spec's error: section 6 said the game's spawn is the twin's reset.
- b0 only matters below 0.8 m/s. The base is b0 blended into the trot reference by s = min(1, v/0.8), and all 9
  non-paw joints have a reference.
  - So D8 / DR10 trot are untouched.
  - DR06 keeps 25 % of b0: taller (0.410) and 7 % faster than the twin's 0.665.
  - The stand before the start and after the stop is the straight b0 pose.
- Both b0 choices are safe (`tests/t_spawn_b0.py`, `spawn_b0_check.json`). Set-up: game-like spawn at q = 0 ± 0.05 rad,
  pelvis ± 0.03 rad (the req-543 noise), D8 and DS, nominal and DR, 10 eps each.
  - b0 = spawn: 0 falls in 40 eps, stand 0.443.
  - b0 = standQ ± 0.05: 0 falls in 40 eps, stand 0.406, still after the stop (|v| ≤ 0.002 m/s).
  - The trot is the same in both.

**First divergence (B vs game):** tick 1 (t = 0.02 s), the spawn drop. At tick 0, q is identical (B is seeded from
it); the contact flags differ only because the twin is dropped from 1 mm.
- Front-right paw: -11.7 deg (game minus twin); the other paws -3.7 to -4.8.
- Elbows and knees: the game folds 4.4-7.2 deg further in the first 20 ms (Box2D rigid contact vs MuJoCo soft, the
  day-3 finding). The differences hold at 6-8 deg through tick 5.
- It washes out by 1 s: chest 0.438 in the game vs 0.441 in the twin.

**Clock offset (cosmetic):** the game's speed plan runs one tick (20 ms) behind the twin's.
- The game's v_cmd first leaves 0 at row 51, the twin's at row 50.
- Cause: the runner adds `(float)dt * frameSkip` (0.0199999996) to the plan clock, so 50 ticks sum to just under 1.0 s.
  The twin switches at `>=`, the game at `>`.
- The policy sees its own tc, so it is harmless. But a raw t-aligned comparison shows a 1-tick phase lag on every
  joint: rms 7.0-8.8 deg on shoulders / elbows / hips / knees over 4-12 s, 4-7 on the paws, contact agreement 91 %.
- Aligned by one tick, the trot joints match to 1.5-3.1 deg rms (lumbar 0.7). The amplitude std is equal within 1.5 deg,
  mean offsets ≤ 1.8 deg (knee_l), contact agreement 96 %.
- Duty: game 0.57-0.62, twin 0.55-0.58.

Picture: `game_vs_twin_d8.png` (Russian labels; `tests/plot_game_vs_twin.py`). It shows shoulder / hip / knee over
6-8 s, the contact pattern, the speed and the chest height (game, twin B, twin A).

## 2. Passive rows of the ctl unit test (2026-10-25 06:40)

**The spec is right: neck / head / tail0 divide by 8 / 8 / 3 with K / C = 5 / 0.1, 5 / 0.1, 0.5 / 0.005. The trace was
wrong.**
- Bug: `rlenv_dog.DogTrot(gains="box2d")` applied the gains kwarg BEFORE `__init__` re-set the passive defaults.
  - The env ran box2d leg gains with the TRAINED passive gains (20 / 0.3, 10 / 0.15, 0.5 / 0.005).
  - `tests/ctl_trace.py` builds its env that way. Its passive rows are the same law at gear 8 with K 20 / 10: under the
    spec's K = 5 that looks like gear 8·5/20 = 2 and 8·5/10 = 4, exactly the 2 / 4 / 3 the lane solved.
  - Tick 0 matched because qd = 0. The damping terms differ (0.3/8 vs 0.1/2), hence the drift once things move.
- **Fixed**: `rlenv_dog.py` now applies the kwarg after the defaults (comment in `__init__`).
  - `ctl_trace_dog_d5.json` is regenerated: md5 4894a8e6 (was 3223c4ab; old copy in the session scratchpad), 150
    ticks, selfcheck 6e-17.
  - Its passive signals now equal the spec law (5 wn - 0.1 qd)/8, etc. to 7.5e-7 (6e-6 N m; 6-decimal rounding). The
    old trace was off by up to 0.73 in signal (5.8 N m) on the neck.
- **For the runner's self-test**: drop TWIN_PGEAR (2 / 4 / 3) and compare the 3 passive rows directly (gear 8 / 8 / 3,
  as the runner already drives them).
  - The whole trace changed (its rollout now has the right passive gains), so the 13 active rows must be re-run
    against the new file too.
  - `policy_dog_d5.json` and `onnx_check_dog_d5.json` are unchanged and still valid (the net test only needs obs -> act).
- **Who else was hit:**
  - Everything that passed `gains=` to the constructor: `export_dog.py` rollouts (cosmetic), `eval_gallop.py`,
    `tests/t_fastfall.py` and `train_dog.py --gains box2d`. So the gallop runs g2-g5 trained with the trained passive
    gains.
  - Everything that called `set_gains()` after construction (all d5 evals: transfer, chatter, tail K, explicit friction,
    this file's item 1) was right.
  - Check: g4 ckpt_14536736 at 2.5 m/s with the Box2D-safe passive gains is still 10/10 nominal (hold 2.49, 1.29 % BW).
  - `eval_gallop.py --ptrained 1` reproduces the old condition.

## 3. In-game obs qd vs the AR(1) chatter model (2026-10-25 06:40)

Estimator (`tests/t_obsqd_game.py`, `obsqd_game.json`), the same for game and twin: noise = logged qd minus the
central difference of logged q over ±20 ms, trot 4-12 s. The runner logs exactly the obs qd (rlQd, before the ×0.1).
The twin row is the clean B rollout, and the same rollout with the AR(1) model added.

Noise rms in deg/s; lag-1 correlation in parentheses:

| joint | game D8 | game DR10 | twin clean | twin + AR(1) x1 | AR(1) model |
|---|---|---|---|---|---|
| lumbar | 23 (0.53) | 26 | 18 | 36 | 36 (0.60) |
| shoulder r / l | 41 / 39 (0.45-0.48) | 45 / 42 | 31 / 37 | 48 / 53 | 40 (0.17-0.23) |
| elbow r / l | 74 / 73 (0.42-0.47) | 78 / 73 | 54 / 62 | 86 / 90 | 62 (0.12-0.18) |
| front paw r / l | 136 / 122 (0.28-0.67) | 138 / 141 | 88 / 80 | 340 / 316 | 317 (0.10-0.12) |
| hip r / l | 49 / 48 (0.61-0.65) | 52 / 53 | 30 / 36 | 54 / 57 | 44 (0.45-0.51) |
| knee r / l | 63 / 62 (0.43-0.48) | 74 / 75 | 50 / 61 | 78 / 84 | 55 (0.31-0.34) |
| rear paw r / l | 98 / 128 (0.15-0.50) | 151 / 180 | 83 / 99 | 324 / 330 | 289-316 (0.10-0.33) |

**d5 sees LESS chatter in the game than I injected.**
- The excess over the twin's own residual (quadrature) is:
  - legs 27-51 deg/s (model 40-62);
  - paws 50-105 (model 290-320);
  - lumbar 14 (model 36).
- So the game sits at about 0.2-0.4 of the model on the paws and 0.6-1.0 on the legs. d5 tolerated x1 and x2 of the
  model with 0 falls (day 3).
- Why the model is bigger: it came from rep-541 log 2, the GAME's own trot controller. Under the runner's PD (paw
  kp 4, kd 0.015) the paws chatter much less.
- On the legs the game's residual is more correlated tick to tick (lag-1 0.42-0.65 vs the model's 0.12-0.51), i.e. it
  is slower, closer to a bias. Part of that is the estimator itself (the twin's clean residual has -0.05 to 0.51).
- Caveat: a 50 Hz estimator cannot see a 200 Hz alternation inside a tick; it measures what the policy actually gets.
- Item closed: the obs-qd watch line in unity-transfer-dog.md is answered.

## 4. Gallop g4 / g5 final evaluation (trailing)

g4 ends ~07:13, g5 ~07:05.

An unattended job, `tests/g45_final.py` (detached, started 06:40), runs these steps:
1. Waits for both `last.zip` files.
2. Screens g4 ckpt_14536736 plus, for each run, the last 2 checkpoints and last.zip at 2.5 m/s (3 nominal eps).
3. Gives the top 2 (by success, then lower assist) 10 nominal + 10 DR eps, with the Box2D-safe passive gains.
4. Gives the winner 10 more DR eps with the trained passive gains (`--ptrained`).

Results go to `runs/g45_final.txt` (readable log, BEST line at the end) and `runs/g45_final.jsonl`. Nothing is
deleted; the best checkpoint stays where it is.

Known so far: g4 ckpt_14536736 at 2.5 m/s is 10/10 nominal with either passive set, and 7/10 DR with the trained
passive set (falls at 5.4-9.2 s, DR overshoots to 2.57-2.74 m/s). No transfer request and no torque change.
