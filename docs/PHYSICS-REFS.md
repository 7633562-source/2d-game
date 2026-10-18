# External physics references (GitHub recon 2026-09-30)

Reading list for the segmented human rig (joints, balance, walk) and the tilemap/art path. Reading
external code is free; copying code is bound by the license column (the game repo has no LICENSE file,
so nothing here is ours to relicense). Nothing on this list is installed or vendored - it is a reading
list to use when a task needs it. Facts below were checked against the GitHub API on 2026-09-30.

## Segmented human / active ragdoll - our core problem (pose + balance + walk on joints)
| repo | stars | last push | license | what to take from it |
|---|---|---|---|---|
| `ashleve/ActiveRagdoll` | 267 | 2023-03-05 | MIT | canonical from-scratch active ragdoll: PID-controlled pose/balance over a physically simulated body. Read for the balancing loop and the joint-target formulation. |
| `sergioabreu-g/active-ragdolls` | 364 | 2021-02-02 | Apache-2.0 | joint-drive control blended with keyframed animation - the same blend our walk does between `ReferenceWalkLine` and physics. |
| `hairibar/Hairibar.Ragdoll` | 268 | 2021-04-21 | MIT | UPM package for animating ragdolls through keyframed animations: partial physics blending and hit reactions without full ragdollization; closest published analogue of our mix. |
| `EZhex1991/EZSoftBone` | 511 | 2023-11-08 | check before copy | kinetic soft-chain simulator (hair / tail / skirt) - secondary motion for later, not now. |
| `nocaptheorem/SuperHeroFlightAndGrapplingRobot` | 4 | 2026-08-05 | check | a Godot project, but the algorithms (bipedal stabilization, subtree-COM gravity compensation) are engine-agnostic - read for the COM idea only. |
| `Saviourcoder/DynamicEngine3D` | 27 | 2026-09-26 | check | soft-body physics engine, actively developed; 3D soft bodies are not our lane - noted, not adopted. |

## Official Unity references
| repo | stars | last push | license | what to take |
|---|---|---|---|---|
| `Unity-Technologies/PhysicsExamples2D` | 737 | 2026-09-29 | Unity Companion License (`LICENSE.md`, SPDX NOASSERTION) | official samples: Rigidbody2D, joints, effectors, contacts. Read before reinventing joint tuning; copying requires the license terms. |
| `Unity-Technologies/2d-extras` | 1630 | 2026-01-16 | Unity Companion License | Rule Tiles, animated tiles, tilemap brushes - the tilemap level path (ART-1). |

## Declined, with the reason (so they are not re-proposed)
- `IvanMurzak/Unity-MCP` - editor automation over MCP; our live-editor lane is the official `unity-cli`,
  no second infrastructure to maintain.
- `Seanba/SuperTiled2Unity` - does not compile on 6000.6.2f1 (CS0619, no upstream fix); we hold no `.tmx`.
- `cjddmut/Unity-2D-Platformer-Controller` - platformer motor; our character is a physics biped, not a
  platformer controller.
- `justcoredev/UnityTilemapMask` - SpriteMask-like masking for tilemaps; no current need (we hold no
  SpriteMask usage either).

## Second recon round (2026-10-01)

Added to the reading list (all licenses re-checked through the GitHub API):
- `Habrador/Ten-Minute-Physics-Unity` (363*, MIT, pushed 2026-04-20) - C# ports of Ten Minute Physics:
  verlet chains, cloth, soft bodies. Read for chain patterns if hair/tail/rope secondary motion appears;
  the rig itself stays on Rigidbody2D joints.
- `nocaptheorem/PoseMimicRobot` (2*, Apache-2.0, C#, updated 2026-09-24) - small but actively developed
  active ragdoll: VMC + closed-loop PD pose matching, torque projection, balance without kinematic
  pinning. Closest thing to our pose + balance control; read the control loop (same author as
  SuperHeroFlightAndGrapplingRobot).
- `juniorrojas/algovivo` (374*, MIT, JS, 2026-09-23) - energy-based formulation for soft-bodied creatures
  with muscle actuation. Read for the math of actuated articulated bodies, not for code.
- `Unity-Technologies/2d-techdemos` (1015*, MIT, 2026-03-13) - official 2D feature demos (tilemap, sprite
  animation, sorting, physics); canonical companion to 2d-extras for the ART-1 tilemap path.
- `FinlayTheBerry/Unity2DPixelPerfectCollider` (89*, MIT, 2026-01-25) - auto-generates pixel-perfect
  colliders from sprites; optional if a shape ever needs more than our box colliders (painted-parts rule).
- `TLabAltoh/Unity-SDF-UI-Toolkit` (247*, MIT, updated 2026-10-01) - SDF UI rendering, third sighting;
  still only relevant if the HUD grows past the current minimum.

Declined, with the reason:
- `bhubbard/active-ragdoll-rs` (0*) - Rust port of ashleve/ActiveRagdoll; a second interpretation of the
  same design, no new ground.
- `erayzesen/QuarkPhysics` (295*, C++) - a separate 2D engine; our solver is Unity's, only its joint and
  soft-contact ideas would transfer.
- `appsinacup/godot-softbody2d` (384*, Godot) - deformable soft bodies; squishy segments are not our
  mechanic.
- `OndrejNepozitek/Edgar-Unity` (913*, MIT) and `AndreaEnei/UnityDungeonBSP` (0*) - procedural level
  generation; our world is hand-authored scenes and nobody asked for random levels. Revisit only if
  "playthrough variability" becomes a goal.

## Extracted control patterns (free-web analysis, 2026-10-01 - advice, NOT yet verified in our rig)

Produced by a free channel (deepseek web, answered in 24 s) from the three ragdoll references above.
Treat as a hypothesis list to test against trials, not as facts about our project.

- Joint targeting: never command the target angle directly. Per hinge, per FixedUpdate:
  `err = DeltaAngle(current, target)`, `velRef = clamp(kp*err - kd*omega, -vMax, vMax)`, then
  `joint.motor = { useMotor = true, motorSpeed = velRef, maxMotorTorque = tauMax }`. Gain rule of thumb
  `kp ~ 4*pi^2*f_n^2*I_eff` with `f_n = 1.5-3 Hz`, `kd = 2*zeta*sqrt(kp*I_eff)`, `zeta 0.7-1.0`,
  `I_eff = 1/(1/I_child + 1/I_parent)`. One writer per joint (no MovePosition/MoveRotation, no second PD),
  adjacent mass ratios <= 5:1 (10:1 hard limit), torque feedforward `m*g*r` for stance joints.
- Balance priority: ankle strategy first (`tau = -kA*(comX - supportX) - dA*comVelX`, clamped to the joint
  limit), hip strategy when the ankle saturates, capture-point step when `xi = comX + comVelX*sqrt(h/g)`
  leaves the support polygon - with one small foot collider the ankle alone saturates at ~5 deg of CoM
  offset, so the step carries the base. Torque projection `tau = J^T F_com`, subtree COM feedforward.
- Traps: tremor = kp too high for joint inertia, motor max torque too high, too few solver iterations,
  or PD run from Update; foot slide = friction below the contact angle, motor past the friction cone,
  mass ratios > 10:1, restitution > 0. Their suggested ranges: `velocityIterations >= 20`,
  `positionIterations >= 8`, `fixedDeltaTime 0.005-0.01`, contact offset >= 0.1x smallest collider,
  friction 0.6-1.0 (Average combine), restitution 0.
- Metrics that separate "improved" from "stiffer" (headless): CoM_x std and 95p jerk falling while ankle
  torque RMS stays flat or drops = improvement; CoM_x std falling while ankle torque RMS and tremor power
  in the 20-50 Hz band rise = stiffening. Walk: stride/cadence/contact-time variance, foot slip, support
  margin. Report them together - a single number lies.
- Their verdict on the references: skip Hairibar.Ragdoll for a planar 2D biped (1 DOF per joint gives the
  blend nowhere to hide residual solver error -> visible tremor); use the closed-loop PD + torque
  projection pattern (PoseMimicRobot) as the base.

### Our numbers against those ranges (checked 2026-10-01)
| setting | ours | their range | note |
|---|---|---|---|
| `Fixed Timestep` | 0.005 (200 Hz) | 0.005-0.01 | inside |
| `m_VelocityIterations` | 8 | >= 20 | below - cheapest single experiment, but solver cost rises on WebGL |
| `m_PositionIterations` | 3 | >= 8 | below - same trade-off |
| `m_DefaultContactOffset` | 0.01 | >= 0.1x smallest collider | check against the foot collider size |
| friction | no `PhysicsMaterial2D` in the project -> engine default (0.4) | 0.6-1.0, Average, restitution 0 | feet/torso contact is un-tuned; candidate for the slide problem |
| simulation mode | 0 = FixedUpdate | FixedUpdate | correct |

## Third recon round (2026-10-02)

Added (licenses checked through the API; star counts are honest - two of these are days old):
- `Josh-Prints/Equilibria` (0*, created 2026-10-01, **no license declared** - study only, never embed) - a
  2D side-on active ragdoll: box rig on Planck.js (Box2D port) at 240 Hz, PD on every joint with gains
  derived from body inertia, hand-written ankle/hip balance and a stepping reflex, plus a small NN
  correction trained by evolution strategies. Closest fresh reference to our own problem shape; read the
  reflex state machine and the gain derivation. Cross-check: its design matches the free-web advice in the
  section above (inertia-derived gains; ankle -> hip -> step priority) - two independent sources agreeing
  is the cheapest confidence we can get before burning trial time.
- `opaopa6969/xpbd-body` (0*, MIT, created 2026-06-25) - tiny XPBD active ragdoll that physically tracks a
  target pose through compliant "muscle" motors; deterministic (fixed substeps, no randomness) and unit
  tested headless in Node. Relevant because our trial harness is headless and deterministic too; an XPBD
  drive is the alternative to the PD/motor drive we use today.
- `indenturestoreria106/GenoView-InverseKinematics` (0*, MIT, C, updated 2026-10-02) - skeletal-animation
  viewer that detects foot sliding. Port material for the automatic foot-slide metric we still lack.
- `h8man/NavMeshPlus` (2355*, MIT, active since 2018) - NavMesh pathfinding for 2D (bakes from tilemap or
  collider geometry). Not needed today (no NPC navigation), noted for when the dog or any agent has to
  path around the world.

Declined, with the reason:
- `Fire-Aalt/com.firealt.mosaic` (58*, MIT) - ECS tilemap autotiling; our tilemap path is classic Tilemap
  + 2d-extras, and there is no ECS in the project.
- `NotNull92/hera-agent-unity` (29*, Apache-2.0) - agent-driven live editor CLI; the official `unity-cli`
  already covers that lane, and a second driver is a cost.
- `GabrielBigardi/Unity-2D-Sprite-Animator` (96*, MIT) - lightweight sprite animator; our human is
  physics-driven and the animals are not animated yet, so it stays out until an art task asks for it.

## How to use this file
When a physics task touches balance, joint drive or the reference-line/physics blend, read the top three
repos first and write the finding into the task's notes; when a tilemap task starts, read 2d-extras.
