# dog-twin-1024 day 1: planar MuJoCo twin of the Unity dog + first PPO trot

Written 2026-10-25 00:26 (from `date`); sections marked "d4" were completed at the end of the pure-RL run.
Source (read-only): `E:/Games/2D Game/dog454`, branch land/dog-1024, HEAD 352ee30a. Unity was NOT run.

## 1. Build
- `skeleton.md` lists every body, joint, actuator, friction and contact parameter with `file:line` citations.
- `build_dog.py` writes `dog2d.xml`. The pelvis is the root (x/z slides, pitch hinge). It carries 22 bodies, 20 hinges
  (the jaw is welded), 16 muscle motors (tail 1-4 have none, only springs) and 20 joint-friction actuators. Mass is
  25.000 kg.
- Mapping rules (checked against take rows, see the header of build_dog.py):
  - q = Unity jointAngle, with q = 0 at the spawn (build) pose;
  - +signal = extensor = +jointAngle;
  - the floor sits at z = 0.02 (Box2D contact skin);
  - joint friction is modelled as an affine actuator with K_eff = min(K, Ieff/dt);
  - pair friction follows sqrt(mu1 mu2).
- Sanity (`twin_dog.py sanity`, `sanity.png`, `sanity.json`), soft PD inside the muscle ceilings:
  - stand 5 s: stands on 4 paws, chest pitch -7.9 deg (spawn -8);
  - drop 0.3 m: lands at 0.245 s on 4 paws, no body touch, no actuator saturation, stands;
  - with paw pair friction 0.4 and a pyramidal cone the stand creeps 12.8 cm in 5 s. The claw friction (paw 4.0 -> pair
    1.26, `-dogTrotClaw`) or an elliptic cone with impratio 10 stops the creep. The RL env uses the claw friction.

## 2. Match check vs Unity (existing takes, no new Unity run)
Method (`twin_dog.py replay`, `sweep_replay.py`):
- the logged joint angles (hip/knee/shoulder/elbow L+R, lumbar) of a trot take drive position servos;
- the paws are held flat (paw angles are not logged);
- the root is free, so height, pitch, contacts and travel come out of the physics;
- window 1.5-12 s;
- takes cl505_x_s07 and cl505_t1 (game trot door line, metrics/cl505-metrics.txt:97).

| quantity | cl505_x_s07 | cl505_t1 | verdict |
|---|---|---|---|
| chest height RMS / bias | 0.35 cm / +0.06 cm | 0.80 cm / +0.08 cm | match (skin 0 would give -2.4 cm bias) |
| chest pitch RMS | 0.96 deg | 2.44 deg | match |
| pelvis tilt RMS | 0.90 deg | 2.51 deg | match |
| paw contact flags agree | 81 % | 69 % | fair (paw angles unknown) |
| CoM speed Unity / MuJoCo | 0.695 / 0.821 m/s (+18 %) | 0.690 / 0.810 m/s (+17 %) | GAP |
| paw sole slip Unity / MuJoCo | ~0.16 / 0.076 m/s per paw | ~0.20 / 0.072 m/s per paw | GAP: Box2D paws skate 2-2.7x more |

- When the servos are capped at the game's muscle ceilings, speed drops to 0.71 m/s and slip rises to 0.17 m/s. But
  then tracking degrades to 14 deg RMS, so the speed match comes from lost tracking, not from correct physics.
- Claw friction lowers MuJoCo speed only to 0.74-0.81 m/s.
- Plot: `compare.png` (Russian labels).
- Geometry, mass, height and pitch match within 1 cm / 2.5 deg. The horizontal gap is +17-18 % travel for the same
  limb motion, caused by paw slip. It cannot be closed from the logs because paw angles and paw muscle activations
  are not logged.
- Closed loop: the game's own trot joint profiles (phase-averaged from cl505_x_s07, `ref_trot.json`) run through
  PD inside the muscle ceilings, with claws, 10 episodes (`eval_zero.jsonl`):
  - 0/10 falls at 1.04 m/s;
  - assist 2.9 % BW, against Unity's 2.85 % in the same take;
  - stride pattern intact.
  The twin trots on the game's law, about 35 % faster than Unity, which matches the slip gap above.

### Unity runs for a lane worker (headless player `E:/Games/2D Game/dog454/Build/Headless/BalanceTrial.exe`)
Same argv pattern as cache/rl-1024/run_unity.py: `-batchmode -trial -nographics -duration D -label L -out <dir>
-sourceFingerprint <manifest fp> -buildManifest <manifest> -fixedDelta 0.005`, plus:
1. U1 drop test (no code change): `-dog 1 -dogPawFriction 0.4 -startY -1.23419 -duration 5` (0.3 m above spawn
   -1.53419). Compare chestY(t), impact time and pitch with the twin drop in `sanity.json`.
2. U2 torque log (no code change): the trot door line + `-dogTwinProbe 1 -duration 12`
   `-dog 1 -dogWalk 1 -dogWalkTime 1 -dogTrot 1 -dogTrotClaw 1 -dogPawFriction 0.4 -dogSlipTrue 1 -dogAssist 1
   -dogAssistMode 1 -dogAssistDead 4 -dogAssistTorque 4.9 -dogTrotSeed 7 -dogTrotV 0.8 -dogTrotSwingGround 1
   -dogTrotProbe 2 -dogTrotSeek 0.6 -dogTrotTdContact 2 -dogTrotSwingCap 0.06 -dogTwinProbe 1`.
   It writes `<label>_twin.csv` (DogStanceController.Turn.cs:573-660): flexor/extensor torques of 8 leg joints +
   lumbar + neck, trot time 0.8-5 s. The twin can then replay torques instead of angles. Older req-450 twin files
   (cl454_*_twin.csv) exist but come from an older build.
3. U3 needs a small log patch (lane request, not done here):
   - add the 4 paw jointAngles and the paw muscle torques to the twin probe row;
   - widen TwinT0/TwinT1 (Turn.cs:573) to 0-12 s;
   - stop the main CSV from skipping 0.005-1.015 s.
   This is the data needed to close the slip gap: paw angle and paw torque per step.

## 3. First PPO trot (`rlenv_dog.py`, `train_dog.py`, `eval_dog.py`)
- Env: 200 Hz physics (RK4), 50 Hz policy, 13 actions (lumbar + shoulder/elbow/paw x2 + hip/knee/paw x2).
  - Each action is a joint-target offset. A fixed PD turns it into a muscle signal, then the game's slew is applied
    (0.1 per 5 ms) and the flexor/extensor ceilings (paw 17 / 7 N m).
  - Neck and head are held level by PD; the tail is passive.
  - The game's mode-1 pelvis assist is on, with its force logged as % BW.
  - Episode: stand 0.3-1.5 s, then the speed command ramps at 0.5 m/s^2 to 0.6/0.8/1.0 m/s.
  - Rewards: speed tracking, a diagonal-trot contact clock (period 0.775 s = Unity, per-leg offsets and duty from the
    take), upright, height, clearance, slip, energy and action rate.
  - Termination: any non-paw body on the floor, chest pitch beyond 45 deg, or chest below 0.25 m.
  - Domain randomisation as in the human run: mass/inertia x U(0.9, 1.1), gear +-10 %, joint friction +-20 %, paw
    pair friction U(0.7, 1.3), latency 0-1 step, chest pushes +-50 N x 0.2 s every 1-3 s.
  - PPO: SB3, pi/vf 256x256 tanh, 4 worker processes x 8 envs, ~2550 steps/s.
- Bugs met on the way, all fixed:
  - joint limits were double-converted to radians (runs moved aside: runs/d1_unstable_implicitfast, runs/d2_limits_bug);
  - implicitfast blew up under bang-bang actions, so the env uses RK4 plus an instability guard;
  - the paw PD gain was too high for the paw's 0.00075 kg m^2.
- Run d3 (rv 2 = residual policy over the game's own trot profiles, +-24 deg residual) ran 20 min / 3.1 M steps and
  was stopped once the target was met.
  - Eval (`eval_d3.jsonl`, deterministic, stand 1 s -> 0.8 m/s, 12.6 s; success = no fall and, in 2.6-12.6 s,
    speed 0.8 +- 0.12, diagonal pairs agree >= 70 %, >= 8 touchdowns per paw):

| checkpoint | physics | success | falls | speed | diagonal agree | stride | assist |
|---|---|---|---|---|---|---|---|
| 1.67 M | nominal | 10/10 | 0 | 0.822 | 0.865 | 0.78 s | 1.34 % BW |
| 1.67 M | DR | 10/10 | 0 | 0.832 | 0.858 | | 1.44 % BW |
| 3.13 M | nominal | 10/10 | 0 | 0.848 | 0.882 | 0.78 s | 0.58 % BW |
| 3.13 M | DR | 10/10 | 0 | 0.847 | 0.879 | | 0.82 % BW |
| 3.13 M | DR, assist OFF | 10/10 | 0 | 0.851 | 0.872 | | 0 |

  - Film: `best_dog.mp4` (3.13 M, nominal physics, Russian captions).
  - Paw slip 0.004-0.010 m/s per paw with claws (Unity ~0.16-0.20).
- Run d4 (rv 1 = pure policy, no game profile, same rewards): see section 3b.

### 3b. d4 pure RL (written 2026-10-25 01:55)
- The first d4 run was moved to runs/d4_reward_blowup. A finite but huge blow-up state (no MuJoCo warning) gave one
  episode a reward of -6.9e12 and spoiled the VecNormalize stats.
  - Fix in rlenv_dog.py: an episode now also ends as unstable when |qvel| > 300, and the per-step reward is clipped to
    +-10.
  - Normal trot peaks at 27 rad/s; random flailing reaches about 110 rad/s, so 60 was too strict.
- Run d4b: rv 1 = pure policy, no game profile, the same clock reward (period 0.775 s, per-pair offsets 0 / 0.5,
  duty 0.6). It ran 75 min / 14.7 M steps at 3270 steps/s, 4 workers. Progress:
  - the first ~3.5 M steps were short falls (1-2.5 s);
  - by 6 M steps episodes lasted 11 s;
  - from 8 M steps it survived the full 12 s in 96-100 % of training episodes.
- Eval (`eval_d4b.jsonl`, runs/d4b/last.zip):

| physics | success | falls | speed | diagonal agree | duty FR/FL/RR/RL | assist |
|---|---|---|---|---|---|---|
| nominal | 0/10 | 0 | 0.820 | 0.585 | 0.36 / 0.54 / 0.41 / 0.12 | 0.42 % BW |
| DR | 0/10 | 0 | 0.822 | 0.587 | 0.32 / 0.55 / 0.48 / 0.08 | 0.49 % BW |

- Verdict: the pure policy holds speed and never falls, but its gait is not a trot. It is lopsided: the rear-left paw
  is almost never down (duty 0.08-0.12) and the rear-right paw touches down about 3x per stride. Film:
  `pure_rl_d4b.mp4`.
- The trot result of the day is therefore d3 (residual over the game's trot). The pure line needs 2 more things:
  - symmetry (a mirrored-state loss or a left/right duty penalty);
  - a per-paw minimum stance time.
- Plot: `progress_dog.png` (d3 and d4b, Russian labels).

### 3c. Ready for req-531 (Unity data -> unity531/)
`unity531.py` reads the lane's takes as soon as they land:
- `list` classifies the files (U1/U2/U3 from the label/json) and prints the time spans and whether the paw block is
  present;
- `drop` overlays U1 chestY with the twin drop (u531_drop.png/json);
- `gate` runs the torque-replay gate (u531_gate.png/json):
  - seeds the twin from the Unity state every 0.5 s (joint and paw angles, pelvis tilt, CoM x/vx, chest height/vz);
  - applies the logged muscle torques -(tauF + tauE) open loop for 0.5 s;
  - recomputes the mode-1 pelvis assist and applies the logged BalanceAssist fx split by mass;
  - reports drift at 0.05-0.5 s;
  - proposed bar at 0.2 s: joints <= 5 deg RMS, pitch <= 2 deg, comX <= 1.5 cm in >= 80 % of windows;
- `slip` runs the calibration sweep (u531_slip.png/json):
  - angle replay that now includes the logged paw angles;
  - paw pair friction 0.25-1.26 x contact time constant 0.01/0.02/0.04 s;
  - fit to the Unity CoM speed (target gap < 5 %) and the Unity paw contact vx.
- The twin-probe time base is found by fitting chestPitch, so trot time or trial time both work. A non-numeric
  `phase` column is detected. An unexpected column count stops with a request for `--layout`.
- Smoke test on an old req-450 take with no paw columns (cl458r3_ta1_s7, copied to u531_smoke/, results u531_smoke/smoke_*.json/png):
  - the offset fit is exact (pitch RMS 0.0003 deg);
  - torque replay drifts 4.2 / 6.7 / 13.3 / 17 deg joint RMS at 0.05 / 0.1 / 0.2 / 0.3 s, while pitch stays within
    0.5-2.2 deg and comX within 0.1-1.1 cm. The paws are only PD-held flat there, so the paw columns are the missing
    piece;
  - angle replay with flat paws runs +58 % (pair 1.26) to +84 % (pair 0.4) faster than that take. This again points
    at the paws.
  - A first try on cl458_m2_s7 was invalid because that take turns around (TurnMirror2 mirrors the joint angles
    after the flip). Moved to old/.
- Run after the data lands: `env -u PYTHONPATH D:/hermes-data/rl-venv/Scripts/python.exe unity531.py all`
  (4 worker processes; the full slip sweep is 24 runs per take).

## 4. Next steps
1. U1/U2 in Unity (no code), then U3 (paw log patch). Torque-replay gate on U2, then slip calibration (paw friction /
   contact softness) until the speed gap is below 5 %.
2. Transfer: the rv-2 policy outputs target offsets over the game's profile. A Unity runner needs the same three lines
   (PD -> slew -> ceiling) plus the 53-value observation. Export the policy weights + VecNormalize stats to JSON as the
   human pilot plans.
3. Train with paw friction 0.4 (no claws) to match the current game paws, and narrow the speed to the measured game
   0.70-0.77 m/s.
