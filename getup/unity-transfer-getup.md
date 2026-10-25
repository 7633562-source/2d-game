# unity-transfer-getup: what Unity must reproduce for the get-up policy (beyond the MLP)

Companion to `cache/mujoco-1024/unity-transfer-v7.md` (the walk): everything in its sections 1-4 (timing, the 62-obs
layout, the action path with the 0.5 low-pass + 20/s slew, the passive wrist/MTP/neck/head springs in the SPD form of the
2026-10-24 correction) applies UNCHANGED. The existing RlPolicy plumbing (`patches/req-525-1/2.diff`, flags
`-rlPolicy <json> -rlPolicyV7 1`) runs this policy; the differences are listed here. Policy file and gate numbers:
see section 7 (filled after training). File references: `getupenv.py:N` = `cache/getup-rl-1024/getupenv.py`.

## 0. Differences vs the walk policy (same 62/13 layout, no added inputs)
| slot | walk v7dr3 | get-up | Unity flag |
|---|---|---|---|
| obs 59 `v_cmd` | 1.0-1.3 m/s | **0.0 constant** | `-rlVcmd 0` |
| obs 60/61 clock | sin/cos(2 pi tc / 1.076 s), tc from the standing start | **sin/cos(2 pi t / 16 s)**, t = 0 when the get-up policy takes over, +0.02 per tick (getupenv.py `_obs`, T_CLOCK = 16) | `-rlPeriod 16` |
| start | standing, q = 0 | the body as it lies after the fall (section 3) | start detection, section 3 |
| end | never (walk) | when STANDING has been held 3 s (section 4) hand back to the walk/idle controller | hand-over, section 4 |
| assist | none | optional budgeted pelvis force (section 5); v1 trained with it OFF | `-rlAssist 0` (default) |
No new observation inputs. Hand / knee / head contacts are not observed; the policy infers them from the 17 joint angles,
the pelvis angle and the pelvis height (all in the obs). The 13 outputs and their conventions are the v7 ones: MTP
outputs (8, 12) are ignored (passive toe spring), ankle flexion side 22 N m, all other gears as the twin.

## 1. Observation
Identical to unity-transfer-v7.md section 2. Two reminders that matter more when lying than when walking:
- obs 41 "COM x - support mid": mid is the midpoint of the heel/toe body ORIGINS that touch the floor; when no sole touches
  the floor (lying, kneeling, all-fours) it is the midpoint over all four foot bodies. Same rule as the walk.
- obs 42-45 are SOLE geom contacts only (heel box / toe box touching the floor). A foot lying on its side or the shin on
  the floor does not set them.
- Pelvis height obs 38 = pelvis body origin above the floor = Unity pelvisY (centre) - groundY. Lying: 0.12-0.15 m,
  kneeling ~0.5, standing 0.99.

## 2. Action path
Identical to unity-transfer-v7.md section 3-4 (`-rlPolicyV7 1`): a_f = a_f + 0.5 (raw - a_f) once per 20 ms tick, 4
substeps of 5 ms with the 0.1/substep slew, springs on wrist (20 N m/rad, c 0.55), MTP (12, 0.19), neck/head (30).
At the take-over: a_f = 0, act (slewed activation) = 0, prev_a (obs 46-58) = 0, clock t = 0 (getupenv.py `reset`).
Joint limits: the policy was trained on the DEFAULT skeleton stops (hip -90..45, knee 0..120, lumbar +-20, ankle +-45,
MTP -35..50, neck/head +-30, shoulder -160..90, elbow -165..0, wrist +-60). Run Unity WITHOUT the widened get-up flags
(`-humanHipMin -130 -humanKneeMax 150 -humanLumbarFwd 45`): the ladder's widened stops are not part of this policy.
(If a `wide` variant is trained later, it will say so in section 7 and need exactly those flags.)

## 3. Start detection (when the get-up policy takes over)
Training resets (getupenv.py `_unity_pose`, `_proc_lying`): the body at rest after a fall, 0.4 s of passive settle,
zero velocity, and random variation. For Unity the matching trigger is the one the game already has for GetUp3:
`fell = 1` (or `down`) AND the body quiet for the dwell (speed < 0.15 m/s for 1.5 s), then switch the muscle driver to
RlPolicy with the get-up json. The policy is route-agnostic: supine (pelvisTilt ~ +90), prone (~-90), and the floor-sit
the backward falls end in (pelvisTilt ~ 0, pelvis ~0.15 m) were all in training (routes back/belly/other, 0.4/0.4/0.2,
plus 20 % procedural lying poses and the sit/all-fours/squat/crouch curriculum). Any joint pose within the default
limits is a valid start; joint velocities should be ~0 (the dwell guarantees it).
Not covered: starting while still falling, or from a pose with a limb at a widened stop (hip < -90, knee > 120):
clamp the game to the default limits before the take-over (the twin clamps the imported Unity poses the same way).

## 4. Hand-over (when the get-up is done)
STANDING in training (getupenv.py `_standing`): pelvis origin >= 0.85 m above the floor AND |torso world angle| < 25 deg
AND |pelvis world angle| < 25 deg AND both feet have a sole contact AND no other body part touches the floor AND
|pelvis velocity| < 0.4 m/s. The episode ended (success) when this held for 3.0 s; the policy keeps balancing
indefinitely otherwise (it was rewarded +2/step for standing, so leaving it running is safe). Suggested game rule: when
STANDING has held 1-3 s, blend to the idle/walk controller (the walk policy expects q ~ 0, upright, clock tc = 0; the
get-up ends in a slightly bent, upright stance, so a 0.5 s blend or the game's stand controller first).

## 5. Assist channel (owner rule: mean <= 5 % BW per take, 10 % ceiling)
Modelled in training as a horizontal force on the pelvis body (`xfrc_applied`, getupenv.py `step`):
F = -(600 (comx - mid) + 60 vx_pelvis) N, clipped to +-0.10 BW (69.5 N), applied only while a sole touches the floor
and the pelvis is above 0.4 m; a per-take tank of assist x BW x T_episode N s makes the MEAN <= assist by construction
(assist = 0.05 -> 34.7 N mean cap over a 10 s take). In Unity this is AddForce on the pelvis rigidbody, world x, same
law, same gates, same tank, logged as mean/peak in % BW per take (the [assist] line). Whether it is on for the shipped
policy is stated in section 7 (v1: OFF, mean 0 / peak 0).

## 5a. Hand-ground grip (owner 25.10: «эффект сцепки») - what Unity must have for the grip policy
Policy side: actions 8 (right palm) and 12 (left palm) - the MTP slots the v7 layout ignores - are the grip commands.
Nothing else in the layout changes. A non-grip policy leaves these slots unused as before (`grip: none` in the json).
Unity must provide a **palm anchor** per hand, switched by that output:
- **Create** when action > 0 AND the palm collider touches the ground AND the palm speed < 0.5 m/s AND no lock-out:
  a 2D joint from the palm rigidbody to the world (`HingeJoint2D` with `connectedBody = null`, or a `DistanceJoint2D`
  of distance 0) anchored at the palm-ground contact point. It is a PIN: the palm may rotate about the point, not
  slide or lift (the twin's MuJoCo `connect` constraint, getupenv.py `_grip_engage`).
- **Destroy** when action <= 0, or when the joint's reaction force exceeds the cap: tangential 1.0 BW (695 N) or
  pull-off (the force holding the palm down) 0.5 BW (347 N) per hand (`breakForce` on the joint or a reaction-force
  check in FixedUpdate; the twin checks every 5 ms substep). After a break: 0.1 s lock-out before re-creating.
- **Log** per take: held joint force mean and peak in % BW, share of ticks with a grip, number of breaks (the twin's
  `grip_mean / grip_peak / grip_frac / grip_breaks`). It is a contact stand-in like the bird feet grip and does not
  count against the 5 % helper force; the [assist] line stays separate.
- Palm friction otherwise unchanged (0.748 pair value); the x3 get-up grip swap stays OFF.

## 6. Not for Unity (training-only randomisation)
Body mass x U(0.9, 1.1), motor gear x U(0.8, 1.2), joint friction x U(0.8, 1.2), sole friction U(0.5, 0.9), palm friction
U(0.55, 0.95), 0-1 tick latency, +-0.08 rad pose noise, L/R mirror of the start pose. Unity runs the nominal values:
palm friction stays the DEFAULT Box2D pair (1.4 x 0.4 -> 0.748); the game's x3 get-up grip swap (Human.cs:703-746) must
be OFF for this policy.

## 7. Policy file, gate and the run (filled after training)
(see the end of this file)
