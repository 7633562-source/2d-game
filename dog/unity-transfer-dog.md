# unity-transfer-dog: what Unity must reproduce for the dog policy d5 (route a, beyond the MLP)

Written 2026-10-25 (day 3 of the dog twin). The template is `cache/mujoco-1024/unity-transfer-v7.md` (the human).
- Policy: `runs/d5_tail35/last.zip` with its VecNormalize, exported to `policy_dog_d5.json` / `policy_dog_d5.onnx`.
  The obs normalisation is folded in, so the net takes the RAW 53-vector. Export script: `export_dog.py`.
- Net check `onnx_check_dog_d5.json`: 2,100 obs from 3 rollouts. Max |diff| vs SB3 predict is 9.5e-7 for ONNX
  batch/single and for the numpy JSON forward. ONNX single call takes 24 us.
- Controller unit test `ctl_trace_dog_d5.json` (`tests/ctl_trace.py`), see section 7.
- Training env: `rlenv_dog.DogTrot(rv=2, dr=1, assist=1, bkw tail_len 0.35, jaw_hinge 1)`; `runs/d5_tail35/train.log` line 1.
- **Unity gains are NOT the training gains.** Use the `box2d` set (section 3). The trained gains limit-cycle in Box2D
  (pitfall 1). d5 runs zero-shot under box2d in the twin:

  | run | success | falls | speed (target 0.8 m/s) | assist |
  |---|---|---|---|---|
  | nominal | 10/10 | 0 | 0.876 m/s | 0.35 % BW |
  | DR | 9/10 | 0 | 0.877 m/s | 0.48 % BW |

  Source: `law_check.json`.
- File refs: `rlenv:N` = `cache/dog-twin-1024/rlenv_dog.py`.
  - `DSC:N` = `Assets/DogStanceController.cs`, `Trot:N` = `Assets/DogStanceController.Trot.cs`, `Dog:N` = `Assets/Dog.cs`.
  - All game refs are at dog454 HEAD 352ee30a.
- Units: SI and radians in the policy; the game is in degrees, so convert with `Mathf.Deg2Rad`.
- Sign conventions:
  - q = `HingeJoint2D.jointAngle` = connectedBody rotation − own body rotation − reference.
  - World angles are CCW +, as `Rigidbody2D.rotation`.
  - The dog faces +X only.

## 1. Timing
| item | value | ref |
|---|---|---|
| physics step | 0.005 s (Time.fixedDeltaTime) | rlenv:36 |
| policy tick | every 4 FixedUpdates (50 Hz); the action is held for the 4 substeps | rlenv:37, 443 |
| PD + slew | every FixedUpdate (substep), on the current q, qd | rlenv:445-469 |
| slew | 0.1 activation per substep on ONE signed activation per joint (20 /s) | rlenv:38, 468 |
| tick bookkeeping | after the 4th substep, before the next obs (section 2, last rows) | rlenv:487-560 |

## 2. Observation: 53 floats, raw, in this order (rlenv:281-293)
| idx | quantity | game source |
|---|---|---|
| 0-12 | q of the 13 policy joints, rad | `joint.jointAngle * Deg2Rad`. Order: lumbar, shoulder_r, elbow_r, fpaw_r, shoulder_l, elbow_l, fpaw_l, hip_r, knee_r, rpaw_r, hip_l, knee_l, rpaw_l. The joint fields are DSC:1170-1181, bound by segment name at DSC:2319-2335 (Chest, FrontRightUpper, FrontRightLower, FrontRightPaw, ..., RearLeftPaw). Every joint sits on the CHILD segment. |
| 13-25 | qd × 0.1, rad/s | `(joint.connectedBody.angularVelocity - rb.angularVelocity) * Deg2Rad * 0.1`, where rb is the joint's own body. This is −TrotJointOmega (Trot:333-339). `joint.jointSpeed * Deg2Rad * 0.1` should give the same value: the game uses jointSpeed as d jointAngle/dt in ControlJointStable (DSC:8808). |
| 26 | chest world angle, rad | `Mathf.DeltaAngle(0, chestRb.rotation) * Deg2Rad` (chestRb DSC:931). Not `chestPitch` (pitfall 6). |
| 27 | pelvis world angle, rad | `Mathf.DeltaAngle(0, pelvisRb.rotation) * Deg2Rad` (pelvisRb DSC:952). |
| 28 | chest angvel × 0.1, rad/s | `chestRb.angularVelocity * Deg2Rad * 0.1` |
| 29 | pelvis angvel × 0.1, rad/s | `pelvisRb.angularVelocity * Deg2Rad * 0.1` |
| 30 | chest height, m | `chestRb.position.y - groundTopY - 0.02`. groundTopY = −2 (Dog:307). The 0.02 is the twin's floor skin: two Box2D contact offsets of 0.01. The twin stands at 0.40-0.46. |
| 31, 32 | pelvis centre vx, vy, m/s | `pelvisRb.GetPointVelocity(pelvisTf.position)`. The twin's root is the pelvis box centre. Not `comVelocity` (DSC:2466, a whole-body finite difference). |
| 33-36 | paw contact fr, fl, rr, rl (0/1) | `pawCol.GetContacts(groundContactFilter, buf) > 0` on the paw colliders (DSC:1191; filter built in ProbePaw DSC:8280-8293). Use the RAW per-tick value, not the latched `frontRightGrounded` etc. (DSC:1060, LatchPawGrounded DSC:8491). |
| 37-49 | previous RAW action: the 13 clipped net outputs from the last tick, BEFORE the low-pass | runner state; 0 at the start. rlenv:548 |
| 50 | v_cmd, m/s | runner state: ramped 0.5 m/s² toward the commanded speed (section 6) |
| 51, 52 | sin, cos(2π tc / 0.775) | runner state: tc grows 0.02 per tick only while v_cmd > 0, and goes back to 0 when v_cmd returns to 0 |

Tick bookkeeping, done after the 4th substep and before the next obs, in exactly this order (rlenv:487-560):
1. If v_cmd > 0, add 0.02 to tc.
2. Set prev_a to this tick's clipped action.
3. Move v_cmd toward the target by at most 0.5 × 0.02.
4. If v_cmd ≤ 1e-9, set v_cmd = 0 and tc = 0.

## 3. Action path (rlenv:420-469)
**Per tick:**
1. Compute `a = clip(net(obs), -1, 1)`, then `a_f += 0.5 * (a - a_f)` (a_f = 0 at the start).
2. Compute the target base `base`, 13 values (rlenv:317-328):
   - **Non-paw joints:** `base = b0 + s * (ref(tc) - b0)`, with `s = min(1, v_cmd / 0.8)`.
     - b0 = the joint's q at the handover (section 6). Route A: b0 = the trained stand pose, not the spawn jointAngle (section 6, 2026-10-25 06:45).
     - ref = the game-trot profile in `policy_dog_d5.json` `controller.ref`.
     - Lookup: `u = (tc / 0.775 - offset[leg]) mod 1`, then 50 samples linearly interpolated with wrap-around, in degrees.
     - The lumbar follows fr (`profile_leg`), rlenv:295-315.
   - **Paws:** `base = q_paw + paw world angle`, where the world angle is `DeltaAngle(0, pawRb.rotation) * Deg2Rad`.
     This target puts the paw flat.
3. Compute `q_target = clip(base + a_f * span, lo, hi)`.
   - span = lumbar 6°, shoulder 24°, elbow 20°, hip 24°, knee 20°, paw 16°.
   - lo/hi = `controller.joint_range_deg`.

**Per FixedUpdate (substep), for each of the 13 joints:**
```
s   = clamp((kp * (q_target - q) - kd * qd) / gear, -1, 1)     // q, qd as in section 2, read THIS substep
act += clamp(s - act, -0.1, 0.1)                                // ONE signed activation per joint
if (act >= 0) { extensor.activation = act;  flexor.activation = 0; }
else          { extensor.activation = 0;    flexor.activation = -act; }
```
- **box2d gains:**

  | joint | kp N m/rad | kd N m s/rad | gear = game muscle maxTorque |
  |---|---|---|---|
  | lumbar | 120 | 1.0 | 80 |
  | shoulder | 80 | 0.25 | 35 |
  | elbow | 50 | 0.06 | 40 |
  | hip | 80 | 0.5 | 40 |
  | knee | 60 | 0.15 | 45 |
  | paw | 4 | 0.015 | 17 = extensor |

  The maxTorque values are at Dog:136-146.
- The paw flexor's maxTorque of 7 already gives the twin's 7/17 on the negative side. Do NOT scale the activation.
- Sign: +act = extensor (Muscle.direction −1) = +jointAngle. Muscle.Apply puts `activation * maxTorque * direction`
  on the child and the opposite on the parent (Muscle.cs:45-52).
- Write the activations directly, as TrotSignal does (Trot:346-362). Do NOT use ApplySignal/UpdateMuscle
  (DSC:8843, 8868): its MoveTowards slews each muscle separately, which doubles the slew at a sign change (pitfall 8).
- The runner must be the ONLY writer of these 13 pairs and of neck, head and tail0.
  - Put the runner inside DogStanceController.FixedUpdate (order 0, DSC:4) in place of `Drive()` (DSC:2438-2443).
    Muscles and ActuatorDriver run at order 50, so they apply the write in the same step.
  - The Falling → RelaxAll branch (DSC:2964-2969) must not run while the policy owns the joints.

## 4. Passive joints (rlenv:453-476)
| joint | law per substep (signal s, same slew 0.1, same split into flexor/extensor) | game parts |
|---|---|---|
| neck | `s = clamp((5 * wn - 0.1 * qd) / 8, -1, 1)`, where wn = `DeltaAngle(0, neckRb.rotation) * Deg2Rad` (WORLD angle; holds the neck at world 0) | neck muscles maxTorque 8 (Dog:137, 438). The trot does not drive the neck (Trot:944-950), so the runner must. |
| head | the same with headRb, K 5, C 0.1, gear 8 | Dog:456 |
| tail0 (the "Tail" hinge) | `s = clamp((-0.5 * q - 0.005 * qd) / 3, -1, 1)` | tail muscles 3 (Dog:139). The game's trot drives the tail itself (Trot:950); turn that off under the policy. |
| tail1-4 | game TreeSpring, K = `tailSpringStiffness` 2 N m/rad, cap 2 (Dog:105-106, 736-746) | nothing to do. If req-535 lands with `-dogTailK 4`: d5 in the twin with tail K 4 gives 10/10 nominal and 9/10 DR, 0 falls, 0.876 m/s (`tailk_check.json`), the same as K 2. |
| jaw | the game's own `DriveJaw()` (DSC:8189-8223) with jawOpen 0: closed, ControlJointStable at jawPGain 2.5 | call it every FixedUpdate from the runner (Drive() is skipped). First set `activationSpeedThisStep = muscleActivationSpeed`, as Drive() does at DSC:2962, because DriveJaw's muscle write goes through UpdateMuscle. The twin has the same law. |

The trained passive gains (neck 20/0.3, head 10/0.15) blow up in Box2D at ≥ 10° error (law_check.json `passive`).
5/0.1 is stable from 3-40°.

## 5. Assist: the game's own mode 1, pelvis pitch hand (DSC:7484-7510, flags HeadlessTrial.cs:3109-3113)
- Flags: `-dogAssist 1 -dogAssistMode 1 -dogAssistDead 4 -dogAssistTorque 4.9`.
  - Kp 1 N m/deg and Kd 0.08 are the defaults (DSC:941-942).
  - Leave `DogTrotAssistCap` at 0: it would override the 4.9 N m cap.
- `posePelvisTilt0` = pelvisTilt at the handover tick. The runner sets it; the game sets it only on its pose-walk entry (DSC:2614).
- The twin's law is identical: torque = clamp(−(1·err_deg + 0.08·ω_deg/s), ±4.9) on the pelvis when |err| > 4°
  (rlenv:478-484). Twin mean is 0.35 % BW.

## 6. Start state, handover, speed command (rlenv:356-418)
- **Twin reset:**
  - Spawn stand pose: shoulder −20, elbow 24, hip −12, knee 24, lumbar 0, chest −8 world. These are the same numbers as ApplySpawnPoseToStance, Dog:1002-1013.
  - Each joint gets noise of ±2.9° (0.05 rad); paws are flat; pelvis tilt noise ±1.7°.
  - The dog is dropped 1 mm above the floor with zero velocity and ZERO activation.
  - b0 = that pose; tilt0 = that pelvis tilt.
- **Under the policy at v_cmd 0, d5 settles in ~0.5 s into its OWN deeper crouch:**

  | joint | angle |
  |---|---|
  | shoulder | −35° |
  | elbow | 45° |
  | hip | −35° |
  | knee | 50° |

  Chest height drops from 0.457 to 0.404, chest −6.6°. This holds for both gain sets.
- **Preferred handover = the spawn itself (route A).** Engage the policy at the first FixedUpdate after
  ApplySpawnPoseToStance, before the game's stand writes anything. That is the twin's reset; the human lesson is
  rep-530/533 (pitfall 3).
  - **2026-10-25 06:45 correction (rep-542, day4.md section 1):** the game does NOT spawn in the stand pose.
    - Every hinge spawns at jointAngle 0 (the build pose); ApplySpawnPoseToStance only sets the stand's targets.
    - So "b0 = jointAngle at engage" gives b0 = ~0 on all 9 non-paw joints. rep-542's stand chest of 0.437 (vs ~0.40)
      and DR06's 0.708 m/s / chest 0.410 are that b0. The twin started from the game's row 0 with the same b0 gives
      0.445 and 0.711 m/s / 0.413; with b0 = the stand pose it gives 0.409 and 0.666 / 0.401.
    - Use b0 = `controller.stand_q_deg` of the policy json (shoulder −20, elbow 24, hip −12, knee 24, lumbar 0), the
      training b0. Under req-543's reset noise, use b0 = stand_q_deg + U(±0.05 rad) per joint, drawn independently of
      the body noise.
    - b0 only acts below v_cmd 0.8 (s < 1): the stand, the ramp, 0.6 m/s and after a stop. Trot at 0.8 / 1.0 is the
      same either way.
    - Both are safe in the twin (game-like spawn q = 0 ± 0.05 rad, pelvis ± 0.03; D8 and DS, nominal and DR, 10 eps
      each). b0 = spawn: 0 falls in 40, stand 0.443. b0 = stand_q_deg ± 0.05: 0 falls in 40, stand 0.406 (`spawn_b0_check.json`).
      So this is a fidelity fix, not a safety one.
- **Live handover from the game's stand (route B).** Allowed only if the 13 q are within STAND ± 3° and the joint speeds are < 20 deg/s.
  - b0 = clamp(q_now, STAND ± 2.9°). Paws need no b0: their base is recomputed each tick.
  - act = extensor.activation − flexor.activation at the switch, so there is no 50 ms sag.
  - a_f = prev_a = 0; tc = 0; v_cmd = 0; posePelvisTilt0 = pelvisTilt.
- **Speed:**
  - Hold v_cmd = 0 for ≥ 1 s (training stood 0.3-1.5 s).
  - Then ramp to the target at 0.5 m/s². Trained targets: 0.6, 0.8 and 1.0 m/s.
  - To stop, set the target to 0: the ramp goes down at 0.5 m/s², then tc resets and the dog stands.
  - Facing: +X only. ApplyLookFacing (Dog:1288) only mirrors the picture; a −X walk is out of distribution.
- **Termination in training:** any non-paw collider touching the ground, chest height < 0.25 m, or chest world angle
  outside −8 ± 45° (rlenv:565). Treat these as a fall in Unity and hand the joints back to the game.

## 7. Unit tests for the runner
1. **Net:** `onnx_check_dog_d5.json` `first_100_ticks_of_rollout_0`: obs → act. The runner's MLP must match to 1e-5.
2. **Controller:** `ctl_trace_dog_d5.json` holds 150 ticks of rollout 0 (stand 1 s, then 0.8 m/s, box2d gains).
   - Per tick: obs, act, a_filtered, base, q_target, paw world angles.
   - Substep 0: q, qd, neck/head world angles, the 16 clipped signals, act before/after the slew, MuJoCo ctrl.
   - Feeding the same obs and paw angles, the C# code must give the same base/q_target/signal/act to 1e-5.
   - It was checked against the env's own slew: max |diff| 6e-17.
   - **2026-10-25 06:45: the trace is REGENERATED (md5 4894a8e6; the old 3223c4ab is void).**
     - The old file was recorded with the trained neck/head passive gains (20/0.3, 10/0.15): an init-order bug in
       `rlenv_dog.DogTrot(gains=...)`, now fixed.
     - That is why its passive rows looked like gears 2/4/3: 8·5/20 = 2 and 8·5/10 = 4.
     - Section 4 was right all along: neck / head / tail0 = (5 wn − 0.1 qd)/8, (5 wn − 0.1 qd)/8, (−0.5 q − 0.005 qd)/3.
       The new trace matches that law to 7.5e-7 (6-decimal rounding).
     - In the runner self-test, drop TWIN_PGEAR (2/4/3) and compare the passive rows directly (8/8/3).
     - Re-run the 13 active rows against the new file too: the rollout itself changed.
     - `onnx_check_dog_d5.json` and `policy_dog_d5.json` are unchanged.
3. **Live:** a 12 s take (stand 1 s, then 0.8) should give speed 0.85-0.9 m/s, a stand chest height of about 0.40 at
   1 s, contact pattern trot, assist < 1 % BW.
   - 2026-10-25 06:45: the 0.40 holds with b0 = stand_q_deg (section 6). With b0 = jointAngle ≈ 0 it is 0.44 (rep-542:
     0.437 game, 0.445 twin).
   - The trot chest is 0.37-0.375 either way (rep-542 0.371, twin 0.375).

## 8. Not for Unity (training randomisation only)
- Mass and inertia ±10 %, muscle gear ±10 %, paw friction 0.7-1.3 ×, joint friction ±20 %.
- 0/1-tick action latency, chest pushes of ±50 N.
- Pose noise at reset.

## 9. Pitfalls
1. **Explicit damping and the trained gains limit-cycle in Box2D** (`tests/t_law.py`, `law_check.json`).
   - At the game's two-body EffectiveInertia (DSC:8831) with JointFriction's dead-beat cap (JointFriction.cs:55-80), a 1-DOF Box2D-like test of the trained gains limit-cycles:

     | joint | limit cycle |
     |---|---|
     | lumbar | 70 deg/s |
     | shoulder | 170 deg/s |
     | elbow | 460-500 deg/s |
     | hip | 116 deg/s |
     | knee | 320 deg/s |

   - Stability needs a + 2b < 2 and b < 1 (a = kp dt²/Ieff, b = kd dt/Ieff), plus the slew's large-error limit. The human failed the same way (transfer-v7-diag.md, c·dt/Ieff > 2).
   - The box2d set is quiet from 3° to 40° (hip unstable only from 60°). Do not "restore" the trained KP/KD.
2. **Flag-order bug (human req-533).** A new flag that implies another must be parsed AFTER that flag's own line, or the old line resets it.
   - Print every runner flag (policy file, gains name, assist set, dead band, tail K) in one init log line.
   - Check that line in the first take.
3. **Start-pose mismatch / handover sink (human rep-530, rep-533).** The human net died 1-2 s after a live handover because the game's stand was another operating point: hips/knees 60-100° apart, pelvis 2 cm low.
   - For the dog, the game spawn pose IS the twin's start, so route A is in distribution. (2026-10-25 06:45: not quite.
     The game spawns the joints at jointAngle 0, not in the stand pose, so b0 must come from stand_q_deg (section 6).
     The body pose at spawn is fine: d5 handles it, with 0 falls in rep-542 and 0/80 in the twin.)
   - Route B only inside the window of section 6. Log the 13 q, chest height and pelvis tilt at the switch.
4. **Activation ceiling.** Any other writer that clips or rescales activations (game flag sets, quiet-plant
   writers, ActuatorMixer) changes the plant. The policy must own all 16 joints with the plain 0-1 activation, and the jaw goes through DriveJaw only.
5. **Gains are not the training gains.** The export JSON carries `controller.gains = "box2d"`. A runner that reads KP/KD from the code comments would get the unstable set.
6. **Vestibular noise.** `chestPitch` / `pelvisTilt` come from VestibularSystem (DSC:2514-2515, VestibularSystem.cs:51-79), which adds `tiltNoiseDegrees` if set (default 0).
   - Read rb.rotation for the obs.
   - Keep the noise at 0 for the assist dead band.
7. **Latched contacts.** The game's `*Grounded` flags are latched with hysteresis (DSC:8491). The policy saw raw per-tick contact.
8. **Slew per muscle vs signed.** UpdateMuscle slews each muscle at 20 /s on its own. A sign flip then moves the net
   torque at up to 0.2 per step and can co-contract for a few steps. The twin has one signed activation. Keep a
   signed `act` per joint in the runner.
9. **qd sign.** TrotJointOmega is child − parent (= −qd). Check qd ≈ Δq/dt on the first ticks of the first take.
10. **Paw world angle.** The paw base uses the paw segment's world rotation. It is 0 when the sole is flat at build;
    check this on the spawn frame. A constant offset here tilts every stance paw.

## 10. Pending game changes (not in 352ee30a; re-check when they land)
- 2026-10-25 05:12, lane status (coordinator): req-542 = this package, route A, base 352ee30a + req-535-1, tail 35 K4 on every take, the json unit tests to 1e-5 before any live take. req-541 = the log-2 request. The spec is unchanged since 04:20: d5_tail35 policy, box2d gains, 53 obs, 13 actions. The day-3 fast-gait runs (g2-g5) use the same obs, actions and gains, but none of them replaces d5 here unless a dated line says so.
- 2026-10-25 06:11, rep-541 log-2 replays (day3.md section 5): the spec is UNCHANGED.
  - Confirmed: Unity's joint-friction law and body inertias equal the twin's (gain 1.00 on all 15 joints). This supports `gains = "box2d"` as specified. The leg-dynamics gap is the paw contact (known since day 2; the training contact already matches travel within +1..+5 %).
  - New watch item for req-542: Unity's jointSpeed alternates step to step (Nyquist, from the explicit dead-beat friction). In the game trot the rms alternation is 15-20 % of qd on lumbar/legs, as large as qd itself on the paws (~460 deg/s), and ~60 deg/s on the neck. The twin has none.
  - Read qd exactly as section 2 says. If D8 falls or jitters, log the obs qd (13-25) on the `_dogrl.csv` ticks; a twin check of this is being run (result to follow here).
- 2026-10-25 06:17, the twin check promised above: d5 TOLERATES the chatter; the spec stays UNCHANGED.
  - Unity-form (explicit, pre-step) joint friction in the twin: 10/10 nominal at 0.881 m/s, DR 9/10, 0 falls (implicit: 10/10 at 0.876, 9/10). It does not recreate Unity's chatter (paws 65-81 vs ~460 deg/s), so that is Box2D solver/contact, not the friction law (`tests/t_explfric.py`, `explfric_check.json`).
  - Unity's measured qd noise injected into the obs qd (13-25), AR(1) per joint at the control rate with Unity's rms (paws 290-320, legs 40-62, lumbar 36 deg/s) and lag-1 correlation: x1 10/10 nominal, DR 9/10; x2 10/10, DR 9/10; 0 falls in all (`tests/t_obschatter.py`, `obschatter_check.json`).
  - Not covered: the chatter inside the game PD's kd*qd term. If D8 jitters, the first suspect is the PD/paw loop, not the policy input; the obs-qd log line above stays the diagnosis.
- 2026-10-25 06:45, rep-542 answers the obs-qd watch: in the game, d5 sees LESS qd noise than the twin was tested with.
  - The obs qd residual over a 50 Hz central difference of q, D8 trot 4-12 s: legs 39-74 deg/s and paws 98-136, vs the
    twin's clean 30-62 and 80-99. The excess over the twin is legs 27-51 (model 40-62) and paws 50-105 (model 290-320).
  - d5 passed x1 and x2 of the model. The model came from the game's OWN trot controller (rep-541 log 2); under the
    runner's PD the paws chatter 3-6x less.
  - `tests/t_obsqd_game.py`, `obsqd_game.json`, day4.md section 3. No spec change.
- 2026-10-25 06:45, plan clock (cosmetic): the runner's v_cmd plan switches one tick (20 ms) after the twin's.
  - Cause: it sums `(float)dt * frameSkip` = 0.0199999996 per tick and tests `>`, so 50 ticks fall just short of 1.0 s.
  - The policy sees its own tc, so it is harmless. But a log compared against the twin by t shows a 1-tick phase lag.
  - Optional fix: accumulate in double, or test `>= dur - 1e-6`.
- **Paw grip (main lane):**
  - rep-537: `-dogTrotClawAt 1` (contact-point tear), pad 0.8, 4 cm budget. The loaded slide goes 5.3 → 2.7 cm per stance.
  - req-538: a claw pin, i.e. a FrictionJoint2D to the world while the claw holds, maxForce 1.26 × Fn.
  - 2026-10-25 05:12: the claw pin is refuted (rep-538) and dropped. Only the rep-537 claw switch stays a pending game change. The rest of this item applies to it unchanged (one live d5 take, slide and speed vs the twin).
  - The twin trains with the pair friction 1.26 (paw material 4.0, day2.md slip calibration), so the pin should already be close to the twin's contact.
  - If it lands: run one live d5 take and compare the paw slide per stance and the speed with the twin (twin d5 at 0.8 m/s: 0.876 m/s).
  - The obs contact flag is unaffected. The pin must not count as a ground contact in `GetContacts`, because a joint is not a contact.
- **Tail K (rep-535, candidate 35 cm at K 4, waiting for the owner's "+"):** d5 is insensitive (section 4). Mirror it with `build_dog.build(tail_k=4)`.
- **Hip anchor (owner's sheet `hip_options.png`, option A recommended; the game edit is on hold until he picks):** the thigh gets longer and the anchor moves inside the pelvis.
  - d5 zero-shot on A in the twin: 5/5 at 0.80 m/s.
  - Still, retrain or fine-tune d5 on the A skeleton before any Unity take on A.
