# getup-rl-1024 - get-up from the back and the belly as an RL task in the MuJoCo twin

Written 2026-10-24 (Fable agent). Inputs: `cache/rl-1024/{skeleton.md, human2d.xml, rlenv.py, batchvec.py, train.py}`
(the twin, read-only), `cache/mujoco-1024/report.md` section 4 (the get-up plan), `cache/getup-1024f/{why.md, r3-plan.md,
statics_r3_out.txt}` (why 12 hand-built rounds ended 0/288), `reports/rep-525.md` + `frames/c525/obs-map.md` (the Unity
RlPolicy plumbing), the headless fall takes in `E:/Games/2D Game/My project/Trials/Runs/cl527*, cl520*` (read-only).
Everything new lives in this folder; `cache/rl-1024` is imported, not modified.

## 0. Why RL here

The hand ladders failed on geometry and on open-loop timing (why.md section 1): a 2D body with hip -90..45 / knee 0..120
/ ankle +-45 cannot plant a flat sole from a kneel, hands planted while the trunk lifts close a chain that folds the
body or somersaults it, and a stage that advanced by timeout started the next from a pose it was not written for.
A policy that searches the contact-rich state space with the real joint stops as hard constraints and a dense
"higher and more upright" reward does not need the route to be named. The walk pipeline (planar twin -> PPO -> JSON ->
Unity RlPolicy, 15/16 x 20 s in rep-525) is reused end to end.

## 1. Reset distribution

Three start families, mixed per episode (`p_mid`, `p_proc`, the rest from Unity by route weights 0.4/0.4/0.2):

1. **Unity fall end-states** (`extract_resets.py` -> `resets.json`). Every `cl527*/cl520*` take CSV is scanned for the
   first quiet 0.5 s after `fell = 1` (|d pelvisY| < 1.5 cm, |d pelvisTilt| < 8 deg, joints < 15 deg, pelvis centre
   within 0.45 m of the ground): the body at rest right after the fall = where the game's 1.5 s get-up dwell starts.
   Columns used: pelvisY (= the twin's pelvis body origin, z = pelvisY + 2.0), pelvisTilt, lumbar, neckAngle, headAngle,
   shoulder/elbow L/R, hip/knee/ankle L/R, toe L/R (MTP). Wrists are not logged -> 0 (passive spring anyway).
   Route by the PELVIS world angle: back (supine) > +45 deg, belly (prone) < -45 deg, else "other" = a floor sit
   (the backward falls n30/n45/n60 end sitting, folded forward, in 2D). The 12 fall kinds are deterministic, so the
   poses repeat across the flag cells; the env samples a KIND first, then a take, then adds +-0.08 rad joint noise,
   +-0.1 rad tilt noise, a random L/R mirror (legal in the plane), clamps to the twin's joint range and settles 0.4 s
   with zero muscle (springs on) so penetrations and the Box2D-vs-MuJoCo rest height resolve before the policy acts.
   Note: the game's get-up takes ran with widened stops (-humanHipMin -130, -humanKneeMax 150, lumbar 45 at arm); the
   policy is trained on the DEFAULT skeleton (hip -90..45, knee 0..120, lumbar +-20), so those poses are clamped.
   `wide=True` rebuilds the xml with the widened stops as a fallback variant (needs the same flags in Unity).
2. **Procedural lying** (`_proc_lying`): random limb angles (shoulders -1.2..1.0, elbows -1.6..0, hips -1.2..0.5,
   knees 0..1.6, ankles +-0.5 rad), pelvis tilt +-90 +- 20 deg, dropped from 5 cm, settled 0.6 s. Covers limb poses the
   12 fall kinds never produce.
3. **Curriculum poses** (`_mid_pose`): floor sit, all-fours/bear, deep squat, crouch, each with random angles, held by a
   PD during a 0.3 s settle. These are the late stages of every real get-up (why.md section 5: both routes end in the
   same deep squat), so the policy learns the rise first and the floor part last.

Curriculum: `p_mid` 0.5 -> 0.1 linearly over the first 20 M steps (trainer `set_attr`), `p_proc` 0.2 constant.
Eval (gate GU1) uses `fixed_route = "back" / "belly"` only, Unity poses, nominal physics, no mirror noise reduction.

## 2. Observation and action (frozen to the walk family)

62 floats exactly as `rlenv._obs` / obs-map.md: 17 q, 17 qd x0.1, torso/pelvis world angle, torso/pelvis angular
velocity x0.1, pelvis z/vx/vz, CoM x - support midpoint (grounded heel/toe origins, all four when airborne), heel/toe
flags, previous FILTERED action (13), v_cmd, sin/cos clock. Two slots change meaning, not layout:
- idx 59 `v_cmd` = 0.0 constant (the get-up is a zero-speed skill; Unity: `-rlVcmd 0`).
- idx 60/61 = sin/cos(2 pi t / 16 s), t = time since the policy started (Unity: `-rlPeriod 16`, clock reset at start).
  Over the 8 s episode the pair is monotone, so the policy knows how long it has been trying.
No new inputs: hand/knee/head contacts are not observed directly, but with 17 joint angles + pelvis angle + pelvis
height the whole body configuration is known kinematically, so the floor contacts are inferable (a hand at z <= 0 is
on the floor). Actions: 13 signals (ACT_J), v7 conventions (low-pass alpha 0.5 per 20 ms tick, 20 /s activation slew,
ankle -1 -> 22 N m, wrists/MTP passive springs with critical damping, neck/head springs; MTP outputs ignored).

## 3. Reward (per 20 ms policy step; `REWARD[rv]` in getupenv.py)

| term | v1 | meaning |
|---|---|---|
| level | 1.0 x clip((pelvis z - 0.15)/(0.95 - 0.15), 0, 1) | lying 0 -> standing 1, dense |
| head | 0.5 x clip((head z - 0.2)/(1.6 - 0.2), 0, 1) | rewards the trunk coming up, not only the hips |
| progress | 0.3 x clip(d pelvis z / dt, +-1) | velocity shaping for the first rise |
| upright | 0.5 x s(pelvis z) x exp(-(torso angle / 20 deg)^2), s = 0 at 0.4 m -> 1 at 0.8 m | upright torso only counts when high |
| CoM over support | 0.3 x exp(-((CoM x - mid)/0.1)^2) when a foot is down and pelvis > 0.4 m | the balance term |
| stand | +2.0 per step while STANDING (below) | the hold |
| success bonus | +30 when STANDING has lasted 3 s, then the episode ends | |
| energy | -0.001 x sum ctrl^2 (13 policy joints, per physics step mean) | |
| action rate / jerk | -0.05 x |da|^2, -0.02 x |d2a|^2 (on the filtered action) | |
| joint-limit hits | -0.05 x fraction of the 17 joints within 3 deg of a stop | |
| head on floor | -0.2 per step while the head box touches the floor | no face-planting |

STANDING = pelvis z >= 0.85 m AND |torso world angle| < 25 deg AND |pelvis angle| < 25 deg AND both feet down (heel or
toe) AND no other body part on the floor AND |pelvis velocity| < 0.4 m/s. (Standing pelvis z is 0.99 m; a kneel is
~0.5 m, so kneeling cannot score.)
**v1 result (9 M steps, 33 min, 4.5 k steps/s):** the policy found a local optimum from every start, including the
crouch: a folded A-frame (hips on the -90 deg stop, trunk hanging forward, hands at the floor, pelvis 0.78 m, 43 % of
joints at a stop, `frames/v1_8M_fold.png`). The level term paid 0.8 for it and the upright hold was never found.
**v2 (from this):** the level term is gated by the trunk angle once the pelvis is above 0.5 m (a fold earns 30 % of it),
head weight 0.8, joint-limit penalty 0.3 (x6), hands-on-floor penalty 0.3 when the pelvis is above 0.6 m, head-floor
0.5, a stand-still term 0.3 above 0.7 m, and the curriculum adds UPRIGHT STANDING starts (20 % of the mid poses:
q = U(-0.15, 0.15), tilt +-0.1) so the +2/step hold is discovered first and the value propagates down the ladder.
**v2 result (6 M steps, 20 min):** still 0 stands from any start. Diagnosis from upright starts (deterministic
policy): the body is STANDING at t = 0.5 s (4.2/step) and the policy leaves it within a second and parks in a
one-knee kneel (pelvis 0.53 m, shin on the floor, torso 52 deg, ~0.9/step) that is passively stable under the
exploration noise (sigma 0.6 on 13 joints = +-60-80 N m random torques); a stance never survived long enough for
its value to be learned. The fold/kneel attractors are a noise problem, not a weight problem.
**v3 (from this):** (1) a fixed 15 % share of upright standing starts (`p_stand`, outside the mid curriculum) and
FALL TERMINATION for those episodes only (pelvis < 0.7 m ends the episode, -1): the walk pilot's "falling loses the
future" signal, which is what taught balance there; (2) a penalty 0.5 for any non-sole/non-hand floor contact
while the pelvis is above 0.45 m (the kneel); (3) initial log_std -1.0 (sigma 0.37) instead of -0.5; p_mid 0.4 -> 0.1.
Lying starts keep no termination (the body is on the floor anyway).
**v3 result (4 M steps, 13 min):** succ_stand still 0: the policy falls from the stance in 1-2 s even
deterministically. Stand episodes are 15 % of starts but, being 50-100 steps long, ~3 % of the samples; the walk
pilot learned balance on 100 % standing data. **v3s = stage 1 "balance":** same v3 reward, starts 70 % upright
stance + 30 % mid poses, no lying, until the hold is reliable; **stage 2** resumes from it (`--init`) with the full
lying mixture (p_stand 0.15). Curriculum in time instead of in the mixture.
**v3s result (5 M steps, 17 min):** the policy stands 10 s but the HOLD never reached 3 s: the sole contact flags
flicker at 50 Hz on a quiet stance (a foot shows 0011 for a tick) and the hold reset to 0 on every flicker. Fix in
the stand test (all versions from here): a foot counts as down if its sole touched the floor within the last 0.1 s,
and a soft miss (pelvis still >= stand_h - 0.05, no other contact, |torso| < 40 deg) DECAYS the hold by 3 ticks
instead of resetting it; a real miss (lower, or another body part on the floor) still resets. Re-tested on the v3s
checkpoint: 1/8 stand starts now pass the 3 s hold (0/8 before). v3s2 resumes v3s with the fixed test.
**v3s2 result (13.7 M steps, 35 min):** stand hold 45-52 % in training rollouts (noise + DR), 16/24 = 67 %
deterministic; mid poses (sit/squat/fours/crouch) still 0/21: the balance exists, the rise does not yet.
**Stage 2 (v4 without grip, v4g with grip, in parallel, 3 threads each, 200 min):** resumed from v3s2's last
checkpoint with the full mixture: 15 % stand (with fall termination), 35 % -> 20 % mid poses, 10 % procedural
lying, the rest Unity lying poses (back/belly/other 0.4/0.4/0.2).

## 4. Termination

Success (3 s hold) -> terminal with bonus. Otherwise time cap only: 10 s in training (the gate is judged at 8 s), no
fall termination (the body starts on the floor; a fall during the rise is just a lower level reward). Safety: NaN or
|pelvis x| > 5 m ends the episode with -10.

## 5. Assist (owner's rule: external helper force <= 5 % BW MEAN per take, 10 % ceiling)

Modelled as `xfrc_applied` on the pelvis body, horizontal, a PD toward the support midpoint: F = -(600 e + 60 vx) N,
|F| <= 0.10 BW (69.5 N), active only while a foot is on the floor and the pelvis is above 0.4 m (the squat-to-stand
phase; why.md section 6: 34-69 N cannot lift a 350 N upper body, it can only steer a balanced body). Budgeted: a tank
of `assist x BW x episode length` N s per episode, so the per-take MEAN cannot exceed `assist` by construction; the
env reports `assist_mean` and `assist_peak` in BW for every episode. Default OFF (`assist = 0`): v1 trains without it
and the report states 0 / 0. It is switched on (0.05) only if a version stands but cannot hold. A policy-driven force
would add outputs to the 13-action layout and is not used.

## 5a. Hand-ground grip (owner 25.10, Telegram voice: «Когда человек встает, необходимо давать возможность создавать
## сцепление его кистей рук с землей, как будто бы он держится за землю или как-то упирается. В общем, эффект сцепки.»)

A policy-controlled grip per palm, a contact stand-in like the bird feet grip: it does NOT count against the 5 %
helper force, and it is reported separately (mean and peak in % BW, fraction of ticks held, number of breaks).
- **Channel (no layout change):** actions 8 and 12 are the MTP (toe) outputs, which the v7 layout ignores (passive
  toe springs). In grip mode they are the grip commands: right palm = action 8, left palm = action 12, thresholded at
  0 (> 0 = hold). The 62/13 layout, the obs and the Unity plumbing stay; `prev_a` already carries the commands.
- **Engage:** command > 0 AND the palm box touches the floor AND the palm moves slower than 0.5 m/s (a moving palm
  cannot grab; this also removes impulsive pins) AND no lock-out -> a `connect` equality constraint pins the palm to
  the world at the contact point (`_grip_engage`: anchor in the hand frame + the world point). The palm can still
  rotate about the point (a pin, not a weld) and cannot slide or lift.
- **Release:** command <= 0 -> released. Caps, checked every 5 ms substep on the solver's constraint force
  (`efc_force` rows of the equality): tangential |Fx| > 1.0 BW (695 N) per hand, or pull-off (the force needed to
  hold the palm DOWN) > 0.5 BW (347 N) per hand -> the constraint breaks, 0.1 s lock-out before it may re-engage.
  Pressing load is carried by the floor contact as before.
- **Report:** `grip_mean` = held force (|F| in the plane) averaged over the episode, `grip_peak` = max held force
  per tick, `grip_frac` = share of ticks with a grip on, `grip_breaks`. Random-policy smoke (grip always commanded):
  mean 0.13-0.16 BW, peaks 2-6 BW came from impulsive pins before the velocity gate; env 1.5 k policy steps/s (-25 %).
- **Gate:** GU1 is reported with and without the grip (two stage-2 runs from the same balance checkpoint).
- Training versions: v4 = stage 2 without grip, v4g = stage 2 with grip, same reward (v3), same mixture.

## 6. Domain randomisation (as the walk v7dr3)

Body mass x U(0.9, 1.1) per segment, motor gear x U(0.8, 1.2) per joint, joint friction x U(0.8, 1.2), sole friction
U(0.5, 0.9), palm friction U(0.55, 0.95) (nominal 0.748 = Box2D sqrt(1.4 x 0.4)), 0-1 tick action latency. Pushes are
not used (the body is on the floor). Eval runs the nominal twin.

## 7. Contacts

The twin's geoms carry the Box2D pair friction already (skeleton.md section 6/8, MuJoCo takes the max of the pair,
the floor is 0.4): soles 0.693 = sqrt(1.2 x 0.4), palms 0.748 = sqrt(1.4 x 0.4), every other segment 0.4. The game's
x3 grip swap (palm 4.2 in get-up, Human.cs:703-746) is NOT modelled: the policy must get up on the default palm
friction. Self-collision is off in both engines. Contacts are soft (solref 0.01), which on the 1 m drop test left the
heap 6 cm higher than Box2D (day1.md T2); the 0.4 s settle after every reset absorbs that at the start, the DR covers
the rest, and the Unity gate GU2 decides.

## 8. Training budget and gate

PPO, 256x256 tanh, gamma 0.995 (a 4 s credit horizon), GAE 0.95, lr 3e-4, 30 envs in 5 worker processes + 1 learner
thread (<= 6 of the 16 threads next to the walk day-4 run and the 6-copy battery), n_steps 1024 (30 k per rollout),
batch 7680, 4 epochs. Up to ~8 h wall across reward versions.
GU1 (MuJoCo, nominal physics, deterministic policy): stands and holds 3 s from >= 80 % of back starts and >= 80 % of
belly starts within 8 s, assist mean <= 5 % BW. Reported per version: success %, time to stand, assist mean / peak.
Films: `best_getup.mp4` («со спины», «с живота», «встал»); curves: `progress.png` (Russian labels).

**Stage 2 result (v4 / v4g, 25.10 02:05-03:16, from v3s2 12.0 M to 23.8 M / 24.0 M steps):** the episode reward stayed
flat at 100-300 and the mean max pelvis height at 0.55-0.65 m for 12 M steps in both runs; floor rises in the training
rollouts: 0. v4 died at 23.8 M (03:06, torch CPU allocator "not enough memory" under the shared-machine load), was
resumed as v4b from ckpt_22562816 and stopped together with v4g at 03:16 on the data below.
GU1, deterministic, nominal physics, 25 episodes per route, 8 s gate (`v4_22M.mp4`, `v4g_22M.mp4`, 3 episodes each):

| start | v4 (no grip, 22.6 M) | v4g (grip, 22.3 M) |
|---|---|---|
| back | 0/25, pelvis max 0.20 m | 0/25, pelvis max 0.17 m; grip on 85 % of ticks, mean 0.127 BW, peak 0.98 BW, 4 breaks/episode |
| belly | 0/25, pelvis max 0.68 m | 0/25, pelvis max 0.45 m; grip on 2 % of ticks |
| mid | 12 % (stand 60 %; crouch, fours, sit, squat 0) | 28 % (stand 100 %, crouch 29 %; fours, sit, squat 0); grip 19 % of ticks, mean 0.026 BW |

**GU1: FAIL in both runs.** What the films show: from the back the body sits up within 2 s (trunk upright, legs
straight ahead, hands planted behind the hips; with the grip it holds the floor behind it for 85 % of the episode,
pulling 0.13 BW) and then SITS for the remaining 8 s; from the belly it reaches a high kneel / all-fours (pelvis
0.45-0.68 m) and collapses (v4 rolls over at 10 s). The blocked rung is sit -> squat -> stand: the policy never pulls
the feet under the pelvis. With 27 % mid starts spread over five kinds each lower rung is ~5 % of the data, none of
them scores, so the value never propagates below the crouch. The grip gives a small gain on the rungs it can reach
(crouch 29 % vs 0, mid 28 % vs 12 %) and nothing from the floor; its force stays inside the caps (peak 0.98 BW
tangential cap 1.0, breaks happen on the pull-off cap) and it is not a helper force (section 5a).

**Stage 2b (v5 without grip from v4 ckpt 22.6 M, v5g with grip from v4g ckpt 24.0 M; started 25.10 03:17, 130 min
each, 2 x 7 envs):** reverse curriculum in pose space. New mid kind `ramp`: the upright stance interpolated toward
sit / fours / squat / crouch (0.3 / 0.2 / 0.3 / 0.2) in joint space at a depth U(0, depth); the trainer ramps `depth`
0.5 -> 1.0 over 15 M steps (`--ramp 0.7 --depth0 0.5 --depth1 1.0 --depth_steps 1.5e7`). Mixture: 10 % stand (fall
termination), 50 % mid (70 % of them ramp poses, 30 % the classic kinds), 5 % procedural lying, 35 % Unity lying
poses. Eval labels ramp0..ramp3 = quarter of the depth. Smoke: depth 1 puts the start pelvis at 0.49-0.97 m, env
1.76 k policy steps/s. The eval routes back / belly / mid are unchanged (ramp = 0 there), so the GU1 numbers stay
comparable. If v5/v5g climb the ramp but still stop at the sit, the next lever is a reward term for the feet under
the pelvis (feet x within 0.2 m of the pelvis x while the pelvis is below 0.5 m), not more steps.

**Stage 2b result (v5 / v5g, 25.10 03:17-05:56; v5 22.6 -> 45.5 M steps in 130 min at 2.9 k steps/s, v5g 24.0 ->
37.0 M in 120 min at 1.8 k, the grip pins cost a third of the throughput; written 25.10 06:00):** GU1 FAIL in both.
Deterministic eval, 25 episodes per route (`eval_getup.py`, new option `--ramp` for the ramp row):

| start | v5 (no grip, 45.5 M) | v5g (grip, 37.0 M) |
|---|---|---|
| back | 0/25, pelvis max 0.47 m (v4: 0.20) | 0/25, pelvis max 0.16 m; grip on 79 % of ticks, mean 0.106 BW, peak 0.50 BW, 3 breaks/episode |
| belly | 0/25, pelvis max 0.72 m (v4: 0.68) | 0/25, pelvis max 0.45 m; grip on 0.2 % of ticks |
| mid, classic kinds | 24 % (crouch 43 %, fours 20 %, stand 40 %; sit, squat 0) - v4: 12 % | 32 % (stand 80 %, crouch 57 %; fours, sit, squat 0) - v4g: 28 %; grip 21 % of ticks, mean 0.028 BW |
| ramp, 40 eps, depth U(0,1) | 35 %: ramp0 75 %, ramp1 44 %, ramp2 8 %, ramp3 27 %; t_stand median 0.6 s | 28 %: ramp0 50 %, ramp1 22 %, ramp2 17 %, ramp3 27 %; median 1.2 s |

Training, last 2 M steps of each run: v5 episode reward 497 (v4 248), succ_mid 11 %, succ_stand 23 %, pelvis max
0.74 m, energy 4.1; v5g 381 (v4g 182), succ_mid 14 %, succ_stand 33 %, grip mean 0.015 BW on 11 % of ticks. The reward
doubled because the ramp poses near the stance earn the hold bonus, not because the floor starts moved: succ_back,
succ_belly and succ_proc are 0 in every tabulated row of both runs.

Films (`v5_45M.mp4`, `v5g_37M.mp4`): v5 from the back rolls over into a plank / bear pose within 6 s (hands and feet
on the floor, pelvis 0.47 m) and holds it to the timeout; from the belly it tucks into a ball on the knees and
tumbles; from the crouch it stands and holds. v5g from the back still SITS as v4g did (hands planted behind the hips,
pulling the floor at 0.1 BW); from the belly a low kneel; from the stance it holds.

Reading: the ramp works where it is shallow (v5: ramp0 75 %, ramp1 44 %, poses that did not exist before stage 2b)
and the value does not reach the deep half (ramp2 8 %, ramp3 27 %: pelvis ~0.5-0.7 m, knees folded, feet behind the
pelvis) in 13-23 M steps; the floor starts lie below the deepest ramp pose, so they gain nothing. The blocked rung is
the one stage 2 named: nothing pulls the feet under the pelvis. The grip did not help the ramp (v5g below v5 at every
depth but ramp3) and keeps the back start in the sit; under this reward it is a sit-up aid, not a get-up aid.

Next lever, not more steps of the same: (1) a feet-under-pelvis term - credit for |x_feet - x_pelvis| < 0.2 m while
the pelvis is below 0.5 m, so the squat is rewarded before the hold; (2) the ramp depth window on the failing band,
U(0.4, 0.8), with the shallow share kept; (3) a longer run - the depth schedule alone used 15 M of the 13-23 M steps
available. Exports `policy_v5.json`, `policy_v5g.json` are written (numpy vs SB3 max error 1e-6); nothing goes to
Unity until a floor start stands.

**Stage 3 (v6 / v6k, started 25.10 06:22, 210 min each, 2 x 7 envs, both from v5 last 45.5 M, reward v4, NO grip; coordinator
GO 06:05 after the stage 2b report; written 25.10 06:24):** four levers.
1. Feet-under-pelvis term (shaped, no cliff): `w_feet 0.3 x gate(pel_h) / (1 + (dx / 0.2)^2)`, dx = mean foot x - pelvis
   x, gate 1 below 0.5 m and 0 above 0.8 m, x 0.25 when no sole touches the floor (a tuck in the air is not feet under
   the pelvis). Logged per episode as `feet_dx` (mean |dx|; lying ~0.9 m, standing 0.17).
2. Ramp depth on the failing band: U(0.4, 0.8) with a 25 % shallow share U(0, 0.4) (`--depth_lo 0.4 --shallow 0.25
   --depth0/1 0.8`); measured mix ramp0 16 %, ramp1 27 %, ramp2 50 %, ramp3 7 %. Mixture: 10 % stand, 40 % mid (70 %
   ramp), 5 % procedural lying, 45 % Unity lying poses.
3. 3.5 h wall per policy (~40 M steps each at 3.2 k steps/s).
4. v6k only: keyframe imitation for the FLOOR starts (back / belly / other / proc), `w_imit` 0.6 decaying linearly to 0
   over 28 M steps (pure task reward for the last ~12 M). Per step `w_imit x best match to any rung ahead of the last
   rung reached` + `w_imit x 1.0` once per rung advanced; a rung is reached at match >= 0.55; the rung reached at reset
   is not paid. Match = 1 - sqrt(D) / 3, D = mean over 11 joints ((q - q*) / 0.5 rad)^2 + ((tilt - tilt*) / 0.6)^2 +
   ((h - h*) / 0.2 m)^2, the better of a keyframe and its L/R mirror (a Gaussian match gave 0.00 from every lying
   pose; the linear basin gives 0.15 back, 0.16 belly, 0.33 floor sit toward the first rung). Logged as `kf_frac`
   (rungs reached / rungs) and `w_imit`.

   Ladders (joint targets in rad, twin conventions; h* = target pelvis height) were built from the twin's geometry
   (probed: thigh angle from vertical = -hip + tilt, the shin folds back by the knee, hip joint 0.08 m below the pelvis
   origin; the knee stop at 120 deg puts the lowest feet-flat squat at pelvis ~0.64 m, so a floor sit (0.13 m) to a
   squat is a 0.5 m lift via the hands, and hands-and-knees is reachable only from the belly in the plane) and from
   data (the sit = the Unity floor-sit resets, the crab = v5's own pose from the back, face up, hands behind, but with
   the heels pulled in from 0.83 m to 0.3 m). Picture: `keyframes.png`.

   | ladder | rung | hip | knee | ankle | shoulder | elbow | lumbar | tilt | h* |
   |---|---|---|---|---|---|---|---|---|---|
   | back, other | sit (tucked, hands at the hips) | -1.57 | 1.3 | 0 | 0.1 | -1.2 | 0.3 | +0.3 | 0.15 |
   | back, other | crab (hands behind, feet under the knees) | -0.32 | 2.05 | -0.5 | 1.57 | 0 | 0.3 | +1.25 | 0.54 |
   | belly | push-up (straight incline on the hands and toes) | 0 | 0.05 | 0.6 | -1.0 | -0.2 | -0.2 | -1.15 | 0.40 |
   | belly | all-fours (knees under the pelvis, elbows bent) | -1.5 | 1.57 | 0.78 | -1.5 | -0.9 | 0 | -1.5 | 0.55 |
   | belly | half-kneel (R foot forward flat, L shin down) | -1.57 / 0 | 1.15 / 1.57 | -0.42 / 0.78 | -0.3 | -0.3 | 0.2 | 0 | 0.62 |
   | both | squat (feet under, trunk forward, hands free) | -1.57 | 2.0 | -0.7 | -1.0 | -0.3 | 0.2 | -0.5 | 0.64 |
   | both | crouch | -1.0 | 1.2 | -0.5 | -0.3 | -0.3 | 0.1 | -0.3 | 0.84 |
   | both | stand | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.98 |

   Skipping rungs is free (reached = the highest rung matched), so the crab -> squat swing and all-fours -> squat (bear,
   feet walked in) are both open. The grip is off in both runs (v5g lost at every depth and cost a third of the
   throughput). Assist off. Trainer smoke (1 min from v5) and env smoke passed: terms finite on every start route,
   random actions reach rung 0-1 from the floor, r_imit mean 0.02-0.12, max 1.2 with the bonus.
