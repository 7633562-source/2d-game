# Dog twin, day 2 (2026-10-25 03:25 RTZ)

Owner feedback on best_dog.mp4, all items done:
- tail 35 cm;
- rear legs looked detached;
- jaw as a real hinge;
- retrain;
- Unity notes;
- then the rep-531 checks and the paw-slip calibration.

No Unity runs. E:/Games was only read. Up to 4 worker processes.

Deliverables (all in cache/dog-twin-1024/):

| File | What it is |
|---|---|
| `best_dog_v2.mp4` | trot film, tail 35 cm, hinged jaw, hip bridge, Russian captions |
| `jaw_v2.mp4` + `jaw_test.json` | jaw test: 3 open/close cycles while standing, head inset, caption «челюсть: открыть / закрыть» |
| `still_v2.png` | side view at stand, day 1 vs day 2: tail, hip, jaw |
| `dog2d_tail35.xml` | new model. `dog2d.xml` stays byte-identical as the req-531 reference (md5 d87ef3a4) |
| `u531_drop.*`, `u531_gate.*`, `u531_slip.*`, `u531_slip2.json`, `u531_summary.png` | Unity comparison |

## 1. Build changes (build_dog.py)

`python build_dog.py [out.xml] [tail_len] [jaw_hinge 0/1] [visuals 0/1]`. With no arguments it rebuilds dog2d.xml unchanged.

- **Tail:** new option `tail_len`. At 0.35 the tail is 5 x 0.07 m.
  - Unchanged: the taper, the mass (0.1096 kg per segment, 2.0 % of the body), TreeSpring 2 N m/rad and the limits.
  - Inertia is recomputed: 6.76e-5 ... 4.94e-5 kg m² (was 3.75e-5 ... 1.93e-5).
  - Joint friction scales with it: K_eff 0.0135 ... 0.0051 (was 0.0075 ... 0.0021).
- **Hip and shoulder look:** visual-only boxes, contype 0 / conaffinity 0, so they have no contacts and no mass.
  - `hip_bridge_v` sits on the pelvis at the hip anchor, the game's AddHipBridge (Dog.cs:640-660).
  - `shoulder_bridge_v` sits on the chest at the shoulder anchor.
  - The jaw is drawn in the game's Jaw colour, 2 mm proud of the head.
  - Proof: tests/t_visuals.py → visuals_check.json. Max |qpos difference| is 0.0 in all 4 runs: 20 cm welded and 35 cm hinged, each nominal and with domain randomisation (DR).
  - Physics is bit-identical because the inertias are explicit (inertiafromgeom false).
  - The rear legs looked detached only in the twin's film. In the game the bridge already exists.

## 2. The jaw as a hinge

The game already has it. Dog.cs:488-514 AttachJaw:
- HingeJoint2D at the TMJ, head-local (-0.18·headW, -0.38·headH);
- limits 0..40°, friction 0.01, muscles 2.5 N m;
- the collider is a trigger;
- hits go through JawStrike.

So nothing is welded in Unity, and no `-dogJawHinge` flag is needed. The twin now copies it:
- joint `jaw` at the rear end of the jaw, range 0-40°;
- muscle `m_jaw` (gear 2.5 N m, the game's jawMuscleTorque);
- friction `f_jaw` (0.01);
- the policy does not see or drive it;
- the jaw geom has no contacts.

**Hold law:** the game's DriveJaw → ControlJointStable (DogStanceController.cs:8179-8213), in the env step:
- target = 35° x jawOpen;
- P 2.5 / D 0.4;
- error reference 10°, speed reference 200°/s;
- the 1 + damp divisor, damp = 7.41 with the effective inertia;
- activation slew 0.1 per step.

This gives about 4.3 N m/rad at ζ ≈ 0.5. I did not add a passive spring: the game has none, and a 1 N m/rad spring cut the held-open angle to 29.5°.

**Stop:** MuJoCo's soft limit let the jaw reach 44.7°. A jaw-only stiff stop (`solreflimit -40000 -400`) holds 39.8°, like Box2D's limit (tests/t_jawlimit.py).

**Jaw test** (jaw_v2.mp4, d5 policy standing):

| Quantity | Value |
|---|---|
| open peak | 39.8 / 39.8 / 39.8° |
| time to 90 % open | 0.04 s |
| time to 90 % closed | 0.04 s |
| closed droop at stand | 1.5° |
| torque peak | 1.10 N m (ceiling 2.5) |
| falls | none |

**Droop while trotting** (d5, 10 episodes each):

| Case | p95 of the jaw angle | Per-episode max |
|---|---|---|
| nominal | 2.86° | 3.1-3.4° |
| DR | 2.98° | 3.1-3.4° |

- The p95 meets "< 3°". Single footfall spikes reach 5.2° (nominal) and 6.2° (DR).
- What-if on the game gain (tests/t_jawgain.py):

| jawPGain | Trot max (nom / DR) | Open peak |
|---|---|---|
| 5 | 2.9° / 3.1° | 43.6° |
| 8 | 1.8° / 2.0° | 46° |

- A stiffer hold makes the open step slam past the 40° stop.
- Proposal, if the owner wants peaks < 3°: jawPGain 5 plus a rate-limited open target (35° over about 0.1 s). This is a controller number, not skeleton or assist. Not applied.

## 3. Retrain (runs/d5_tail35)

- d3 zero-shot on tail 35 + hinged jaw: 10/10 nominal and 10/10 DR. Assist 0.61 / 0.82 % BW, speed 0.847 m/s.
- d5 training:
  - continued from d3 ckpt_3129344 with `--tail_len 0.35 --jaw 1`, lr 1e-4, 4 x 8 envs, DR on, 30 min;
  - ran 3.13 M → 7.86 M steps;
  - every training episode ran the full 12 s, with 0 falls;
  - assist fell from 1.17 % to 0.41 % BW;
  - reward rose from 1609 to 1765.

**Gate, d5 last.zip (10 episodes each):**

| Case | Pass | Speed (m/s) | Diagonals together | Assist (% BW) |
|---|---|---|---|---|
| nominal | 10/10 | 0.836 | 0.913 | 0.28 |
| DR | 10/10 | 0.837 | 0.898 | 0.43 |
| assist off, nominal | 10/10 | 0.838 | | 0 |
| assist off, DR | 10/10 | 0.839 | | 0 |

Logs are in eval_d5.jsonl and old/ev_d5_*.txt.

**Tail droop** (tests/t_taildroop.py → tail_droop.json): the angle of the tip below horizontal, at stand, 2-8 s.

| Tail | Spring (N m/rad) | Mean | Swing range |
|---|---|---|---|
| 20 cm | 2 (today) | 23.6° | 7-41° |
| 35 cm | 2 (today) | 34.9° | 6-63° |
| 35 cm | 4 | 22° | |
| 35 cm | 6 | 16° | |

- Trotting: 20 cm 18.5°, 35 cm 32.3°.
- The longer tail hangs lower and swings more: the spring has no damper (ζ ≈ 0.025).
- I kept spring 2 as in the game. Changing it needs the owner's number.

## 4. Unity lane notes (code read at E:/Games/2D Game/My project; nothing edited)

- **U4, tail flag.** In HeadlessTrial.ApplyDogBuildOverrides (HeadlessTrial.cs:2961), add:
  `target.tailSize = new Vector2(GetFloatArg("-dogTailLen", target.tailSize.x), target.tailSize.y);`
  - It runs before BuildDog: at :2932 for the trial dog and at :2228 for the level-1 dog.
  - CreateTail (Dog.cs:700-708) derives the segment length, size and taper from tailSize.x, and the mass per segment from the 0.020 fraction. Nothing else changes.
  - After acceptance, change the Dog.cs:83 default from 0.20 to 0.35. The in-game dog is built in GameProcess.cs:795.
  - Check in Unity: tail droop at stand. The twin predicts a mean of about 35° below horizontal, swinging 6-63°.
- **U5, hip look.** Already in the game: AddHipBridge (Dog.cs:621, 640-660) is a picture only, with no collider. Unity needs no change. Only the twin film lacked it, and it is fixed there.
- **U6, jaw API for a behaviour AI.** Nothing to build for the hinge (section 2).
  - Controls today:
    - `DogStanceController.jawOpen` (0..1 → 0..35°, DogStanceController.cs:329);
    - `Bark()` (:5142, opens for 0.16 s and cries);
    - `Bite()` (:5150, opens for 0.10 s, slams shut until 0.36 s, arms JawStrike);
    - CLI `-jawOpen x`, `-dogBark 1`, `-dogBite 1` (HeadlessTrial.cs:3061-3065).
  - DriveJaw (:8179) also opens it by itself: pant 0.28, chew 0.62, hunt growl 0.20, leap crouch 0.28 / push 0.40.
  - Carrying a stick has no code yet. It would need:
    - a `Carry` state that holds jawOpen at about 0.3 while the stick sits between the jaws;
    - a grab joint (FixedJoint2D) from the jaw to the stick, because the jaw collider is a trigger and cannot pinch.
  - DogStanceController.cs has uncommitted lane edits (mtime 02:12), so the line numbers may move by about 1.

## 5. rep-531 checks (unity531.py, updated reader)

**Reader** (old copy: old/unity531_day1.py):
- The 118-column rep-531 layout is detected from the '#' header: twinT (trial clock), phase, trotT, state, 10 joints x 6, paws x 5, the 28-column paw block at 87, assist at 115.
- Main-CSV rows with the wrong field count are skipped: the glued line covering 0.005-1.010 s.
- Twin and main files are classified by content, so cl531_t1_twin.csv is read as a main CSV.
- Byte duplicates are listed but not run: cl531_t1* = u2_s07, and u1_s07/s11 = u1_s03.
- Time offset between twin and main: 0.000 s, pitch RMS 0.0004°.

**U1 drop** (u531_drop.png/json):
- Impact time is identical: 0.245 s in Unity and in the twin. Free fall matches within 1.5 cm.
- Unity drops 1.1 cm in the first 0.05 s, a spawn transient, then falls at g.
- Landing: Unity dips 4 cm below its final stand (-1.578 at 0.275 s) and recovers to -1.537. The twin, on its soft stand PD, does not dip and then sags to -1.575 with chest pitch -21.7° (Unity: -8.6°).
- The difference after impact is the stand controller (the twin has no copy of the game's landing controller), not the contact. No contact retune comes from U1.

**U2/U3 torque-replay gate** (u531_gate.png/json):
- Logged muscle torques, 21 windows of 0.5 s per take.
- Drift at 0.2 s:

| Takes | Joint RMS | Pitch error | CoM x error |
|---|---|---|---|
| U2 | 7.2-7.5° | 1.0-1.6° | 0.3-0.4 cm |
| U3 | 6.0° | 1.0° | 0.3 cm |

- Only 19-24 % of windows pass, so the gate does NOT pass the proposed bar (joints ≤ 5°).
- With the paw angles logged, drift at 0.2 s fell from 13.3° (day-1 smoke test, which had no paw data) to 6-7.5°.
- The paws drift most: up to 21° RMS on the front-left paw with claws, 8-10° without claws. Elbows and neck drift about 9-10°.
- So the remaining gap is the claw grip moment (not in the twin CSV; the main CSV has clawMomentNm / clawTfN for one paw) and the unlogged head torque.
- Next data request, if wanted: the per-paw claw moment and the head joint torque in the twin probe.

**Paw-slip calibration** (u531_slip.json/png full sweep, tests/t_slip2.py → u531_slip2.json, summary u531_summary.png).

Method:
- angle replay, now including the logged paw angles, 1.5-11.95 s;
- servo targets clipped to the joint range, because Box2D's soft limits overshoot by 2-3°;
- runs that blow up are cut at the last finite step and flagged: 19 of 96 in the full sweep, all on u2_s03, cut at 9.4-11.9 s. I did not trace the cause in that take.

Speed gap of MuJoCo against Unity (Unity travels 0.689-0.698 m/s):

| Setting | U2 claws (s03 / s07 / s11) | U3 no claws |
|---|---|---|
| day-1 condition: flat paws, pair 0.4 | +18..+20 % | +12 % |
| flat paws, pair 1.26 | +11.5..+13.5 % | +7.4 % |
| paws replayed, pair 0.4 | +16 % | +14 % |
| **paws replayed, pair 1.26 (material 4.0), tc 0.01** | **+2.4 / +5.0 / +2.7 %** | **+1.2 %** |
| paws replayed, pair 1.6 | +1.1 / +1.9 / +2.3 % | -2.5 % |
| paws replayed, pair 2.0 | +0.4 / +1.7 / +0.9 % | -3.1 % |
| pair 1.26, contact tc 0.02 | +11..+14 % | +8 % |
| pair 1.26, contact tc 0.04 | +22..+31 % | +12 % |
| noslip 10 | same as the grid | |
| elliptic cone, impratio 10 | same as the grid | |

Ankle |vx| while the paw is loaded, at the same point and gate Unity logs (pawVx = rigid-body point velocity at the paw-joint anchor, Trot.cs:370-376):

| | Unity | MuJoCo |
|---|---|---|
| U2 | 0.19 m/s | 0.22 m/s |
| U3 | 0.16 m/s | 0.19 m/s |

This barely moves with friction. Loaded fraction: Unity 0.60, MuJoCo 0.43-0.46. Contact flags agree 80-83 %.

**Verdict:** the +17-18 % travel gap is closed to within ±5 % (+1.2..+5.0 %) on all 4 takes, with no new parameter.
- About 6-7 points of the gap were the missing paw angles: the day-1 replay held the paws flat.
- The rest is friction. The twin needs the claw-level pair 1.26 even for the clawless U3 take.
  - Box2D's rigid contact sticks a paw at touchdown, where MuJoCo's soft contact lets it slide.
  - That is also why a softer contact makes the twin faster.
- Recommendation: keep the training settings, paw material 4.0 (pair 1.26) and contact time constant 0.01 s. These are exactly what d3/d5 trained with, so no retrain is needed.
- Pair 1.6 would trim U2 to +1..+2 % but takes U3 to -2.5 %. Not worth a change.
- The duty and ankle-speed differences point at the paw contact geometry, not friction. A possible next step: a rounded paw sole (capsule), like Box2D's polygon radius.

## Files changed today

| File | Change |
|---|---|
| build_dog.py | tail_len, jaw hinge, visuals, jaw stop |
| rlenv_dog.py | jaw hold law, JAW_P what-if knob, camera options |
| train_dog.py | --tail_len, --jaw |
| eval_dog.py | --tail_len, --jaw, --visuals, --cam_dist, --title, jaw stats |
| unity531.py | rep-531 reader, duplicates, ankle-speed measure, blow-up cut, per-joint gate |
| new scripts | jaw_film.py, still_v2.py, tests/t_visuals.py, t_jawlimit.py, t_taildroop.py, t_jawgain.py, t_slip2.py, plot_u531.py |

Day-1 code copies are in old/day1_code/.
