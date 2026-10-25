# Dog twin, day 3

Started 2026-10-25 (times from `date`, RTZ). Order: hip remark (priority) -> transfer package -> log request -> gallop.

## 1. Hip anchor options (owner's remark on still_v2), 2026-10-25 03:50

Owner: «задние лапы должны крепится к торсу, ты просто дорисовал странный квадрат как вырост на теле».

Sheet: `hip_options.png` (Russian labels, side view after a 3 s stand settle, same scale, NO bridge drawn).
Data: `hip_opt.json`; scripts `tests/t_hipopt.py`, `tests/plot_hipopt.py`; options in `build_dog.py` `build(hip_opt=...)`
(default build is byte-identical to day 2).

Confirmed in the game (dog454 @ 352ee30a, Dog.cs:585-600): `hipRootY = soleY + dropR`,
`hipInPelvisY = hipRootY - pelvisY`; the anchor lands 4.33 cm below the pelvis box bottom (box h 0.13 m);
Dog.cs:621 / 624-660 `AddHipBridge` only draws a sprite over the gap.

Target for A/B/C: anchor 1/6 of the box height above the bottom face (the middle of the lower third; the hip joint
sits low in the rump depth; this is an anatomical estimate, not a measured ratio).

| option | geometry change | stand 10 s, 5x (zero action) | trot zero-shot d5, 5 ep, 0.8 m/s | joint limits |
|---|---|---|---|---|
| 0 now | anchor 4.3 cm below the box | 0/5 falls, pelvis -0.6, chest -8.0 deg | 5/5, 0.83 m/s, diag 0.92 | - |
| A hip inside pelvis, longer thigh | anchor +6.5 cm up, 2.9 cm back (along the thigh's stand axis, 24 deg), 2.2 cm inside the box; thigh 0.170 -> 0.241 m; mass kept (inertia +83 %) | 0/5, pelvis -0.7, chest -8.0 | 5/5, 0.80 m/s, diag 0.92 | none |
| B pelvis box extended down | box h 0.13 -> 0.195 m, centre 3.25 cm lower, same mass | 0/5, pelvis -0.6, chest -8.0 | 5/5, 0.83 m/s | none |
| C lower pelvis (less chest pitch) | chest pitch -8 -> +3.95 deg | 0/5, pelvis -1.5, chest +3.1 | 0/5 success (no falls, diag 0.67, 0.78 m/s) | stand pose/targets need redoing |

Verdict: **A**, as the coordinator expected. Anatomical, same stand and paws, trot works zero-shot, no limit change.
B has the same physics but draws the rump as a tall block deeper than the chest. C fails the zero-shot trot and changes
the whole posture.

Game-side edit for A (only after the owner picks it):
- Dog.cs:86 `rearThighSize.y` 0.17 -> 0.241.
- Dog.cs:92 `hipLocalX` -0.04 -> -0.069.
- Thigh mass is a fraction (`CreateSegment(..., 0.080f, ...)`), so it stays the same automatically.
- The game's `dropR` lengthens the thigh along its build angle (12 deg), so the anchor lands ~2.6 cm inside the box and
  the stand is ~0.5 cm lower than in the twin.
- Drop `AddHipBridge` (Dog.cs:621).
- Then re-run the stand/trot batteries.

## 2. Transfer package, route a (2026-10-25 04:20)

Doc: `unity-transfer-dog.md`: obs sources, action-to-muscle mapping, timing, passive joints, assist, start and handover, file:line refs at 352ee30a, and 10 pitfalls.

**Key finding: the trained PD gains are not Box2D-safe.** This was checked with `tests/t_law.py` → `law_check.json`.
- Method: a 1-DOF test at the game's two-body EffectiveInertia (DSC:8831) with the game's joint friction, which is a dead-beat cap (b_f = 1 on every leg, lumbar and tail joint).
- Under the trained KP/KD every leg joint and the lumbar limit-cycle at 70-500 deg/s. The human v7 failed in Unity the same way.
- The new set `GAINS["box2d"]` in `rlenv_dog.py` is quiet from 3-40° error (the hip is unstable only at 60°). Neck and head move to 5/0.1 (the trained 20/0.3 blows up at ≥ 10°).

  | joint | kp | kd |
  |---|---|---|
  | lumbar | 120 | 1.0 |
  | shoulder | 80 | 0.25 |
  | elbow | 50 | 0.06 |
  | hip | 80 | 0.5 |
  | knee | 60 | 0.15 |
  | paw | 4 | 0.015 |

- The SPD (implicit) form is only explicit PD with different gains: under SPD, d5 zero-shot fails 10/10.
- **d5 runs zero-shot under box2d, so no retrain is needed:**

  | run | success | falls | speed | assist |
  |---|---|---|---|---|
  | nominal | 10/10 | 0 | 0.876 m/s | 0.35 % BW |
  | DR | 9/10 | 0 | 0.877 m/s | 0.48 % BW |

  The stop works too.

Export: `export_dog.py`.
- **Files:** `policy_dog_d5.onnx` (326 KB, opset 17) and `policy_dog_d5.json` (1.8 MB). The JSON has the weights with VecNormalize folded in, plus a `controller` block with every runner constant and the box2d gains.
- **Net check** (`onnx_check_dog_d5.json`):
  - Max |diff| vs SB3 is ≤ 9.5e-7 for ONNX batch, ONNX single and numpy JSON. One call takes 24 us.
  - Rollouts under box2d: 0.879 m/s; 0.6 → stop → stands; 1.026 m/s.
- **Controller unit test:** `ctl_trace_dog_d5.json` (`tests/ctl_trace.py`) holds 150 ticks of obs → base → q_target → signal → slewed activation. Its self-check is 6e-17.

Observation for the handover: at v 0, d5 settles into its own crouch (shoulder -35, elbow 45, hip -35, knee 50, chest height 0.40). It starts from the game spawn pose (chest height 0.46). The preferred handover is therefore at spawn (route A in the doc). A live handover from the game stand is allowed only inside the window of doc section 6.

Tail K (rep-535 is done; the candidate is 35 cm at K 4, and the land waits for the owner's "+"):
- `build_dog.build(tail_k=...)` mirrors `-dogTailK`. The default stays 2, the game's HEAD.
- d5 with box2d and tail K 4: 10/10 nominal, 9/10 DR, 0 falls, 0.876 m/s (`tailk_check.json`). This is the same as K 2.
- Switch the default once the land happens.

## 3. Log request for the failed torque-replay gate (2026-10-25 04:20)

Draft: `req-draft-dog-log2.md`. The coordinator numbers it when filing. Contents:
- Flag `-dogTwinLog2 1` writes a 200 Hz CSV from ActuatorDriver after its Apply loops.
- Full rigid-body state of all dog bodies.
- Per hinge (21): angle, speed, the applied muscle torque after all writers, friction torque, TreeSpring torque, reaction torque, and both activations.
- Per paw: only fn, signed ft and the contact point, for the force replay. Paw slip is left out because the main lane covers it (rep-537 `_paw.csv`, req-538 claw pin, coordinator 04:22).
- The assist, flip and push forces actually applied, and a body-ground flag.
- Takes: U2 ×3, U3, and a 3 s stand.
- This makes two replays possible: R1 (exact state + applied torque) and R2 (R1 + logged contact forces). Together they say whether the 6-7.5° gap is the contact model or the multibody/actuator model.

## 4. Gallop / fast gait (status 2026-10-25 06:18; first written 05:27)

Target: hold 2.5 m/s after the 0.5 m/s^2 ramp (eval: `eval_gallop.py`, 3 nominal eps per speed, box2d gains, judge
watcher `tests/watch_g.sh TAG RV`, results `runs/TAG_evals.jsonl`).

Runs (all box2d gains, assist on, DR on, tail 35, jaw):
- g1: rv 3 from d5; died on a memory error (ArrayMemoryError).
- g2: rv 3 (morph trot -> gallop over 1.5-2.0 m/s); 0/3 at 2.0 and 2.5 for both checkpoints, falls at 3.3-5 s. The
  half-way gait in 1.5-2.0 m/s is a wall.
- g3: rv 4 (gait SWITCH at 1.8 m/s); curriculum stuck at 1.7-1.8 m/s for 20 min, 0/3 everywhere; killed 05:03.
- g4: rv 5 (fast TROT at every speed, period/duty/amp from MORPH) from g2 ckpt_10156064, lr 2e-4, 150 min.
- g5: rv 5 from g4 ckpt_11096096, lr 1e-4, 120 min (the 3 envs freed by g3).

| ckpt | 2.0 m/s | 2.5 m/s |
|---|---|---|
| g4 10623008 | 0/3, 3 falls | 0/3 |
| g4 11096096 | 2/3, hold 2.13, flight 0.26 | 0/3 |
| g4 11550752 | **3/3**, hold 2.10, 1.05 % BW, flying trot (flight 0.21) | 0/3 |
| g4 11999264 | **3/3**, hold 1.98, 1.41 % BW, trot | 0/3 |
| g5 11550752 | 1/3 | 0/3 |
| g5 11986976 | **3/3**, hold 2.07, 1.30 % BW | 0/3 |
| g4 12435488 | 3/3, hold 1.97, 1.06 % BW; **2.2 m/s: 3/3**, hold 2.29, 1.35 % BW, flight 0.20 | 0/3 |
| g5 12429344 | 3/3, hold 1.97, 0.95 % BW; **2.2 m/s: 3/3**, hold 2.27, 1.24 % BW, flight 0.20 | 0/3 |
| g4 12859424 | 3/3, 1.28 % BW; **2.3 m/s: 3/3**, hold 2.30, 1.92 % BW, flight 0.17 | 0/3 |
| g5 12871712 | 3/3, 0.94 % BW; **2.3 m/s: 3/3**, hold 2.37, 1.10 % BW, flight 0.17, trot 3/3 | 0/3 |
| g4 13283360 | 3/3, 1.67 % BW; **2.4 m/s: 3/3**, hold 2.40, 2.04 % BW, trot 3/3 | **1/3**, hold 2.57, 1.81 % BW, flight 0.24 |
| g5 13271072 | 3/3, 0.79 % BW; **2.4 m/s: 3/3**, hold 2.47, 1.41 % BW, trot 3/3 | **1/3**, hold 2.70, 1.51 % BW, flight 0.26 |
| **g4 13854752** | 3/3, 1.07 % BW; 2.7 m/s: 2/3, hold 2.64, 1.71 % BW | **3/3**, hold 2.49, 1.30 % BW, flying trot (flight 0.24) |
| g5 13891616 | 3/3, 1.15 % BW; 2.7 m/s: 0/3 | 0/3 |
| **g4 14536736** | 2.7 m/s: **3/3**, hold 2.62, 1.24 % BW, flight 0.26 | **3/3**, hold 2.50, 1.16 % BW, flight 0.26, trot 3/3 |
| g5 14555168 | 2.7 m/s: 0/3, 3 falls | **3/3**, hold 2.51, 1.53 % BW, flight 0.20, trot 2/3 |

So 2.0 m/s is solved (fast trot with a flight phase, assist 1.0-1.4 % BW). 2.5 m/s is not trained yet: the curriculum
(vmax_c per env) creeps about +0.04 m/s per 5 min, g4 mean 2.04 / max 2.2 at minute 45, g5 mean 1.96 / max 2.1.

**06:18: 2.5 m/s reached.** g4 ckpt_14536736 (minute ~90 of 150):
- 2.5 m/s, 10 nominal eps: **10/10**, hold 2.50 m/s, assist 1.24 % BW, flying trot (flight 0.26, duty 0.31-0.32,
  stride 0.42 s), stops on 4 paws at the end.
- 2.5 m/s, 10 DR eps: 7/10, 3 falls at 5.4-9.2 s (hold 2.57-2.74, i.e. DR overshoots the speed).
- 2.7 m/s: 3/3 nominal (hold 2.62).
- The gait is a fast trot, not a gallop (rv 5 by design: the trot -> gallop morph of g2/g3 was a wall).
- Film: `gallop_g4_25.mp4` (one nominal ep at 2.5 m/s, box2d gains, Russian title).
- g4 and g5 keep training (g4 to ~07:10, g5 to ~07:05); the DR margin at 2.5 is the open item.

Why it falls at 2.5 (`tests/t_fastfall.py`, `runs/g4_fastfall.jsonl`, g4 ckpt_11550752):
- Falls at 4.0-5.6 s, right after reaching 2.5. The first non-paw contact is a front lower leg (lower_fr / lower_fl):
  the front legs fold or trip.
- Saturation in the hold window (|act| > 0.98): shoulder 44-74 %, hip 14-31 %, everything else < 7 %. At 2.0 m/s the
  shoulder saturates 0.3-1.7 % only.
- What-if on the same policy, gear only in the twin: shoulder x1.43 drops shoulder saturation to 13-31 % but moves it to
  the hip (36-70 %); shoulder x1.43 + hip x1.5 takes both to <= 36 % / <= 4 %, and it still falls 3/3 (4.2-7.9 s).
  So torque is near the limit at 2.5, but the policy was never trained there: no torque change is proposed (it would also
  be a skeleton/actuator change for the owner). Next step is more curriculum time.

## 5. R1 / R2 replays of the rep-541 log 2 (2026-10-25 06:10)

Data: `unity_log2/` (rep-541), takes u2_s03/s07/s11 (claws) and u3_s07 (no claws), 200 Hz rows after all writers.
Code: `replay541.py` (check / run) -> `replay541.json`; one-step test `tests/t_r541_onestep.py` -> `r541_onestep_<label>.json`.

Set-up:
- **Exact state init**: pelvis pose/velocity -> root, 21 hinges ang/spd -> q/qd. Seeding check: every non-tail body lands
  within 0.01 cm / 5e-5 deg of the log, velocities within ~0.1 m/s (front legs).
- **Applied torques**: per hinge q-force = -(tauM + tauFr + tauSp) (Muscle / JointFriction.cs:79-82 / TreeSpring.cs:101-103 all
  add +tau on the child), assist mode 1 = -asTau on the pelvis (DSC:7510). Row k's torques drive step k -> k+1 (shift
  -1/+1 flips the sign of the one-step correlation, so the alignment is unambiguous).
- Paw slots: paw0..3 = FL, FR, RL, RR (nearest paw body x while loaded, 3 cm vs 13-46 cm for the others).
- Paw force lag: the contact impulse of step k is in row k+1 (lag 1 beats lag 0 on every leg joint).
- **Unity's tail chain chatters at the Nyquist rate.** Tail w alternates by +-5000 deg/s step to step, and the tail
  linear velocities break the joint constraints by up to 2 m/s. The cause is explicit dead-beat friction on 0.11 kg links.
  - Replayed open loop, it blows the twin up.
  - The tail chain + jaw are therefore "passive" in every run below: the twin's own friction law and spring, seeded at
    the logged angle with zero relative speed, plus only their logged muscle torque.
  - The integrator is implicitfast. Euler with the twin's friction actuators is unstable for the same reason.

Window replay (0.5 s windows every 0.5 s from 1 s, 20-21 per take; median joint RMS over the rep-531 gate set of 10
joints + 4 paws at 0.2 s):

| take | R1: logged friction | R1f: twin friction law | R2: logged paw forces + logged friction | R2f: logged paw forces + twin friction | rep-531 gate |
|---|---|---|---|---|---|
| u2_s03 | 14.6 | 7.2 (29 % pass) | 20.4 | 9.0 (19 %) | 7.2-7.5 |
| u2_s07 | 14.6 | 6.8 (24 %) | 28.1 | 8.8 (19 %) | |
| u2_s11 | 15.6 | 7.2 (24 %) | 21.5 | 8.0 (19 %) | |
| u3_s07 | 15.2 | 6.4 (33 %) | 23.8 | 9.5 (10 %) | 6.0 |

In R2/R2f, comX error falls from 0.5 to 0.17 cm and pitch stays at ~1 deg.

One-step test (seed the exact state of row k, apply row k, one step; error = rms(twin - Unity change of joint speed) /
rms(Unity's change), 4 takes, ~700 rows each):

| joint | twin contact | logged paw forces (contact out) |
|---|---|---|
| hip | 0.51-0.72 | **0.15-0.16** |
| knee | 0.80-1.00 | **0.12-0.13** |
| rear paw | 0.33-0.46 | **0.09-0.25** |
| elbow | 0.79-0.90 | 0.15-0.58 |
| shoulder | 0.58-0.70 | 0.29-0.43 |
| lumbar | 0.28-0.31 | 0.22-0.24 (twin response x0.89) |
| front paw | 0.40-0.55 | 0.61-0.85 (worse, U3 as well) |
| neck | 1.30-1.43 | 1.18-1.31 (twin response x1.21-1.28) |
| head | 1.98-2.31 | 1.93-2.25 |

Joint-friction law: the twin law -min(K, Ieff/dt) qd, capped, evaluated at Unity's state, reproduces the logged tauFr
with gain 1.00 / corr 1.00 on all 15 non-tail joints in all 4 takes. So the friction law AND Unity's body inertias
(its Ieff) equal the twin's.

**Verdict: the leg part of the 6-7.5 deg gap is the contact model, not the multibody/actuator model.**
- Actuator side: exact. The friction law and inertias match, and muscles are replayed as logged.
- Exact state + applied torques leave the window drift where the old gate had it (R1f 6.4-7.2 deg vs 6.0-7.5). The
  reconstructed state and probe torques of rep-531 were therefore not the cause.
- Per step, putting Unity's own paw forces in cuts the leg joints' error 3-7x (hip, knee, rear paw, elbow).
- The window form of R2 cannot show it. Open-loop contact forces do not follow the drift: a paw that is 1 mm off keeps
  receiving the logged force. R2/R2f are therefore not better over 0.2 s, and the request's "R2 <= 2 deg" form of the
  test is not decidable this way. The one-step test decides it.
- **Residuals that are NOT contact:**
  - Neck/head: the error is larger than the signal in every mode. No other AddTorque writer exists in the game code
    (only flip DSC:4293-4295 and assist DSC:7510/7563, both logged). Suspect: the jaw hinge, which chatters in Unity and
    is passive here.
  - Front paws: worse with the logged forces, and without claws too. Likely the force's application point on a
    3.4e-4 kg m^2 paw (1 mm of lever = 58 deg/s per step).
  - Lumbar/hip: the twin responds 10-11 % weaker. Small; C1 (lumbar tauM vs probe) points to a lumbar writer the probe
    does not see.
- No twin change is made. The contact difference is the one day 2 already found (Box2D's rigid contact sticks the paw
  at touchdown; MuJoCo's soft contact lets it slide), and the training pair 1.26 / tc 0.01 already closes the travel
  gap to +1..+5 %.

**For the transfer (dated line in unity-transfer-dog.md):** Unity's joint speeds alternate step to step. Rms
alternation vs the 20 ms mean speed in the game trot:
- legs 60-77 vs 320-460 deg/s, i.e. 15-20 %;
- paws 450-480 vs 465-555 deg/s, i.e. as large as the signal;
- neck 55-60 vs 40.

The twin's implicit friction has no such alternation. The obs qd (13-25) and the PD's kd*qd see it in the game.

**Chatter robustness of d5 (2026-10-25 06:17)**, 10 nominal + 10 DR eps each, box2d gains:

| twin variant | nominal | DR | falls |
|---|---|---|---|
| baseline (implicit friction) | 10/10, 0.876 m/s | 9/10 | 0 |
| explicit Unity-form friction (lumbar, neck, head, 12 leg joints) | 10/10, 0.881 m/s | 9/10 | 0 |
| obs qd + Unity noise x1 | 10/10, 0.883 m/s | 9/10 | 0 |
| obs qd + Unity noise x2 | 10/10, 0.899 m/s, assist 0.48 % BW | 9/10 | 0 |

- The explicit form barely raises the twin's alternation (paws 52-59 -> 65-81 deg/s, legs 18-37 -> 22-41; Unity: paws
  ~460, legs 60-77). Unity's chatter is therefore Box2D's solver/contact, not the friction law.
- The obs noise is Unity's own (u2_s07, spd minus its 4-step centred mean, sampled at the control rate): rms paws 290-320,
  elbow/knee 55-62, shoulder/hip 40-44, lumbar 36 deg/s; lag-1 correlation 0.1-0.6. Injected as AR(1) per joint.
- So d5 does not care about the chatter on its input, even at twice Unity's level. The chatter inside the game PD's
  kd*qd is not tested in the twin. Files: `tests/t_explfric.py`, `tests/t_obschatter.py`, `explfric_check.json`,
  `obschatter_check.json`.
