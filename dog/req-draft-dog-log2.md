# req-draft: DOG twin log 2 - full state, applied torques after all writers, contact force per paw for the replay (log-only flag)

DRAFT for the coordinator (written 2026-10-25, dog twin day 3). This is not a filed request: give it a number when filing (re-list requests/ first).
Type: DELEGATION. Log-only code behind a default-off flag, plus takes. Patch only: no land, no commit.
Worktree dog454 at 352ee30a, the same tree as req-531/535.

## Why
The torque replay of rep-531 (day2.md, U2/U3 gate, u531_gate.json) drifts **6.0-7.5° joint RMS at 0.2 s**. The bar is 5°, and only 19-24 % of the 0.5 s windows pass.

The logs cannot say where the gap comes from. There are three blind spots:
1. **Window start state is reconstructed.** The twin probe has joint angles, comX/comVx and two trunk angles. It has no
   root position/velocity, no joint speeds, and no neck/head/jaw/tail angles. Every window therefore starts from finite
   differences and a guessed root.
2. **The torque that reaches the joint is not logged.**
   - tauF/tauE are logged inside the controller, before later writers and before the muscle reads them.
   - Joint friction (JointFriction.cs:55-83), the tail TreeSprings and the head/jaw/tail muscles are not logged at all.
   - The assist columns fy/tau are hard-coded 0, and fx is 0 throughout.
3. **The paw contact force is not in the twin probe.** The paws drift most: front-left with claws is 21° RMS,
   without claws 8-10°. Paw SLIP itself is out of scope here: it is the main lane's line (rep-537 paw log,
   req-538 claw pin). This request only needs the force per paw, so that the replay can apply it.

With the fields below, two replays become decidable:
- **(R1)** exact state init + applied-torque replay;
- **(R2)** R1 + logged paw contact forces applied as external forces. This takes the contact model out of the test.

How to read the result:
- R2 ≤ 2° and R1 ≫ R2: the gap is the contact model.
- R2 also off: it is the multibody/actuator model.

## Do
1. **Flag `-dogTwinLog2 1`.** With every flag absent, the build is byte-identical.
   - Write `<label>_twin2.csv` at EVERY FixedUpdate (200 Hz, no decimation), from t = 0.
   - Columns: `t` (Time.fixedTime) and `step` (a counter).
   - Write it from ActuatorDriver.FixedUpdate AFTER its Apply loops (ActuatorDriver.cs:58-75, order 50). That way every value is the one actually applied this step.
2. **Bodies.** For every dog Rigidbody2D (pelvis, chest, neck, head, jaw, Tail..tail4, and per leg upper/lower/paw or thigh/shin/paw), log 6 values:
   - `x`, `y`;
   - `rot` (deg, raw rb.rotation);
   - `vx`, `vy` (rb.linearVelocity);
   - `w` (rb.angularVelocity, deg/s).

   Name the columns `<segment>_<field>`, and print the segment order once in the json.
3. **Hinges.** For every HingeJoint2D of the dog (21: lumbar, neck, head, jaw, tail0-4, 4 × 3 leg joints), log:
   - `ang`: jointAngle;
   - `spd`: jointSpeed;
   - `tauM`: the net muscle torque on the child = Σ activation × maxTorque × direction, read at Apply time (Muscle.cs:45-52);
   - `tauFr`: JointFriction.lastFrictionTorque (JointFriction.cs:24, 81);
   - `tauSp`: the TreeSpring torque, tail1-4 only, 0 elsewhere;
   - `react`: joint.reactionTorque. This shows limit hits.
   - Also `actF`, `actE`: the two activations after all writers.
4. **Paws (4): the contact force for R2 only. No slip fields:** slip is covered by the rep-537 `_paw.csv` (t, paw, mode, stanceClock, load, fn, fx, vt, slide, claw, torn).
   - Turn that existing paw log on in the same takes.
   - Add only what R2 needs and the paw log lacks. Read the paw collider's ground contacts with the same filter and loop as ReadClawContact (DSC:4642-4686), and log:
     - `fn` (Σ normalImpulse / dt);
     - `ft` (Σ tangentImpulse / dt, SIGNED along +x);
     - `px`, `py`: the impulse-weighted contact point.
   - If the req-538 claw pin (FrictionJoint2D) is in the build, add its `reactionForce` x/y to the paw force: it is a contact stand-in.
5. **External forces.**
   - `asTau`, `asFx`: the assist torque/force actually added. These are the AddTorque/AddForce arguments at DSC:7507, 7528, 7558-7560; 0 when not called.
   - `flipTau`: DSC:4290-4292.
   - `push`: HeadlessTrial.cs:6140.
   - `bodyGnd`: 0/1, set if any non-paw dog collider touches the ground.
6. **Takes.** These are the same lines as rep-531, so the day-2 analysis carries over:
   - U2: trot door line + `-dogTwinProbe 1 -dogTwinT0 0 -dogTwinT1 12 -dogTwinLog2 1 -duration 12`, seeds 7, 3, 11.
   - U3: U2 with `-dogTrotClaw 0`, seed 7.
   - U4: a 3 s stand, with no trot flags and `-dogTwinLog2 1`, seed 7.

   Copy the main CSV, `_twin.csv`, `_twin2.csv` and the json to `D:/hermes-data/claude-lab/cache/dog-twin-1024/unity_log2/`.

## Gates
- The dog std is byte-identical with every new flag absent (rep-505 C1 std 11/11 == c129a).
- T1 with `-dogTwinLog2 1` added: the main CSV md5 is the same as without it (the log must not change the physics).
- 0 CS errors.
- Sanity rows in the report:
  - Σ tauM over the lumbar (two bodies) matches the twin probe's lumbar tauE − tauF within 1e-4 when no other writer acts.
  - Σ fn over the 4 paws during the stand ≈ body weight (25 kg × 9.81 = 245 N) ± 2 %.

## Claude side (after the report)
- `unity531.py` gets a `_twin2` reader.
- R1/R2 replay: windows of 0.5 s started from the logged full state, with joint drift at 0.2 s.
- Bar: R1 ≤ 5° on ≥ 80 % of the windows. Otherwise R2 names the culprit.
