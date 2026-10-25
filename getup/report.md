# getup-rl-1024 - stage report after stage 2b (v5 / v5g), 25.10 06:00

## 1. Answer first

GU1 (stand and hold 3 s from >= 80 % of back and belly starts, assist mean <= 5 % BW) is NOT passed by any run so far:
v3s2 / v4 / v4g / v5 / v5g all give 0/25 from the back and 0/25 from the belly in the deterministic MuJoCo eval.
What stage 2b (reverse curriculum in pose space, `ramp` starts) bought: the policy now stands from the near-stance
ramp poses (v5: 75 % at depth 0-0.25, 44 % at 0.25-0.5) and from the crouch (43 %) and all-fours (20 %) - all were 0
after stage 2 - and from the back it reaches a plank / bear pose (pelvis 0.47 m vs 0.20 m). It still never pulls the
feet under the pelvis, so the deep ramp band (8-27 %) and every floor start stay on the ground. The hand-ground grip
(v5g) does not help: it is below the no-grip run at every ramp depth and keeps the back start in a sit. Assist force
is 0 in every eval (the assist term is off at rv 3); grip force stays inside its caps (peak 0.50 BW, mean 0.106 BW on
the back route, 0.028 BW on mid).

## 2. Numbers (deterministic policy, nominal physics, 25 episodes per route; ramp row 40 episodes)

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

## 3. Training curves

`progress.png` (Russian labels) has v3s2, v4, v4b, v4g, v5, v5g; `runs/<tag>/progress.csv` the raw rows. v5 ran
22.6 -> 45.5 M steps (130 min, 2 x 7 envs, 2.9 k policy steps/s), v5g 24.0 -> 37.0 M (120 min, 1.8 k steps/s: the
grip pins add contacts). The depth schedule 0.5 -> 1.0 took 15 M steps, so v5g spent almost its whole run inside the
schedule.

## 4. Files

- `design.md` - sections 0-8 plus the stage 2 and 2b result paragraphs (this report repeats 2b).
- `v5_45M.mp4`, `v5g_37M.mp4` - best clip per route («со спины», «с живота», «промежуточная»); `v4_22M.mp4`,
  `v4g_22M.mp4` the stage 2 films.
- `progress.png` - curves of all six runs.
- `policy_v5.json`, `policy_v5g.json` (+ `_check.json`) - Unity RlPolicy format, VecNormalize folded in, numpy vs SB3
  max error 1e-6; not transferred (section 1).
- `runs/v5/last.zip`, `runs/v5g/last.zip` + `vecnorm_last.pkl` - the checkpoints; `eval_getup.py --ramp 1.0` the
  ramp-depth diagnostic.
