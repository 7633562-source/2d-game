# Dog twin skeleton (dog-twin-1024)

Source (read-only): `E:/Games/2D Game/dog454/Assets/` on branch land/dog-1024, HEAD 352ee30a.
Model: `build_dog.py` -> `dog2d.xml` (planar: rootx / rootz slides + rooty hinge on the pelvis; every other joint a
hinge about y). Citations are `file:line` in that tree.

## World and solver
| item | Unity | twin | cite |
|---|---|---|---|
| step | 0.005 s, Box2D vel 8 / pos 3 it, Baumgarte 0.2 | dt 0.005, implicitfast, 50 it | HeadlessTrial.cs (human skeleton.md, same project settings) |
| gravity | -9.81 | -9.81 | |
| contact offset | 0.01 per polygon (skin) | floor plane at z = 0.02 | replay check: skin 0 -> chest -2.4 cm, skin 0.02 -> +0.06 cm |
| ground | y top -2, material mu 0.4 | z = 0 (z = Unity y + 2) | GroundBuilder |
| pair friction | sqrt(mu1 mu2) | per-geom pair value, floor gets the minimum (MuJoCo takes max) | |
| spawn | root y = StandingRootOffset = -1.53419 | same | Dog.cs:316-322, HeadlessTrial.cs:2937 |
| total mass | 25 kg, NormalizeMass (fractions sum 0.912 -> x1.09649) | same | Dog.cs:1244-1264 |

## Bodies (spawn pose, Unity root frame; centres in m, world angle deg; I = m(w^2+h^2)/12)
| body | size w x h | mass kg | Izz kg m^2 | centre | deg | cite (size; fraction) |
|---|---|---|---|---|---|---|
| pelvis | 0.22 x 0.13 | 3.8377 | 0.020884 | (-0.2882, 0.0251) | 0 | Dog.cs:78; 402 (0.140) |
| chest | 0.36 x 0.14 | 6.0307 | 0.074982 | (0, 0) | -8 | Dog.cs:77, 68; 408 (0.220) |
| neck | 0.10 x 0.08 | 0.8224 | 0.001124 | (0.2282, -0.0251) | 0 | Dog.cs:79; 423 (0.030) |
| head | 0.14 x 0.10 | 1.9189 | 0.004733 | (0.3482, -0.0251) | 0 | Dog.cs:80; 441 (0.070) |
| jaw (welded, trigger collider) | 0.10 x 0.032 | 0.2193 | 0.000201 | (0.3730, -0.0631) | 0 | Dog.cs:81; 497, 510-511 |
| tail0..4 | 0.04 x 0.05..0.0225 (taper to 0.45) | 0.1096 each | 3.7e-5..1.9e-5 | x -0.418..-0.578 | 0 | Dog.cs:83, 104, 697-746 (0.020 total) |
| upper (front leg) x2 | 0.07 x 0.17 | 1.0965 | 0.003088 | (0.1070, -0.1664) | 12 | Dog.cs:84; 545 (0.040) |
| lower x2 | 0.06 x 0.17 | 0.7675 | 0.002079 | (0.1070, -0.3327) | -12 | Dog.cs:85; 557 (0.028) |
| thigh x2 | 0.08 x 0.17 | 2.1930 | 0.006451 | (-0.3106, -0.1664) | 12 | Dog.cs:86; 596 (0.080) |
| shin x2 | 0.06 x 0.17 | 1.0965 | 0.002970 | (-0.3106, -0.3327) | -12 | Dog.cs:87; 608 (0.040) |
| paw x4 | 0.10 x 0.05, heel 0.025 behind the ankle | 0.3289 | 0.000343 | front (0.1143, -0.4408), rear (-0.3032, -0.4408) | 0 | Dog.cs:88, 92; PlacePaw 750-777 (0.012) |

Layout: chest centred at the root, pitched -8 (Dog.cs:68); pelvis butts the chest's caudal end; shoulder anchor 0.10
forward on the chest belly (Dog.cs:73), hip 0.04 behind the pelvis centre (Dog.cs:91); legs at the design angles
shoulder -20, elbow 24, hip -12, knee 24 (Dog.cs:60-63); both legs of a pair coincide (planar; left / right do not
collide with each other, DisableCollisions Dog.cs:1266-1274). Spawn: pelvis centre z 0.4909, CoM (-0.0884, 0.3503),
paws centre z 0.025.

## Joints (q = Unity jointAngle = parentRot - childRot - referenceAngle, reference = build pose, so q = 0 at spawn)
| joint | range deg | flexor / extensor ceiling N m | friction K (cap 15-20) -> K_eff = min(K, Ieff/dt) | cite |
|---|---|---|---|---|
| lumbar (pelvis->chest) | -20..20 | 80 / 80 | 8 -> 3.267 (cap 20) | Dog.cs:413-417, 108-118, 136-146 |
| neck | -30..30 | 8 / 8 | 0.01 -> 0.01 | Dog.cs:431-435 |
| head | -30..30 | 8 / 8 | 0.01 -> 0.01 | Dog.cs:449-453 |
| tail0..4 | -40..40 | 3 / 3 on tail0 only; TreeSpring 2 N m/rad to 0, cap 2 | 0.4 -> 0.0075..0.0021 | Dog.cs:721-746, 105-106 |
| shoulder | -90..90 | 35 / 35 | 3 -> 0.593 | Dog.cs:548-552 |
| elbow | 0..120 | 40 / 40 | 3 -> 0.249 | Dog.cs:560-564 |
| hip | -90..90 | 40 / 40 | 3 -> 0.986 | Dog.cs:599-603 |
| knee | 0..120 | 45 / 45 | 3 -> 0.407 | Dog.cs:611-615 |
| paw (ankle/wrist) | -45..45 | 7 / 17 | 1 -> 0.059-0.061 | Dog.cs:770-774 |

- Joint friction: tau = clamp(-K w, +-|w| Ieff/dt, +-cap) (JointFriction.cs:55-80) -> MuJoCo affine-bias actuator
  `f_<joint>` with gain -K_eff, forcerange +-cap.
- Muscles: Muscle.cs; flexor direction +1, extensor -1; torque = activation x ceiling on the child, minus on the parent
  (HumanSegment.ConnectTo :194-211). Positive signal = extensor = +jointAngle. Twin: `motor m_<joint>` gear =
  max(flexor, extensor), ctrl in [-1, 1]; paws scale the negative side by 7/17 in the env.
- Activation slew: muscleActivationSpeed 20 /s (DogStanceController.cs:1037, UpdateMuscle 8868-8874) -> 0.1 per step.
  The trot law writes activation directly (DogStanceController.Trot.cs:345-361).
- Stand targets (referenced): hip -12, knee 24, shoulder -20, elbow 24 (ApplySpawnPoseToStance Dog.cs:1001-1012).
- Segment damping 0 (HumanSegment.cs), paws Discrete collision.

## Contact / friction
- Body / limb material 0.4 (Dog.cs:1089-1166) -> pair with ground 0.4.
- Paws: `-dogPawFriction 0.4` in the trot door line -> pair 0.4; claws (`-dogTrotClaw 1`) raise the paw material to 4.0
  when engaged (Dog.cs:1131) -> pair 1.26. Paw contact factor 1.2 (Dog.cs:128). Twin default for RL: claw 4.0
  (owner: paws barely slip, high friction allowed); replay uses 0.4.
- Jaw collider is a trigger (Dog.cs:510-511) -> contype 0.

## Assist (game mode 1, the trot door line uses it)
Pelvis torque tau = clamp(-(1.0 err_deg + 0.08 w_deg/s), +-dogAssistTorque 4.9) when |pelvisTilt - tilt0| > dogAssistDead 4
(DogStanceController.cs:7486-7508); force equivalent |tau| / 0.40 m. Take cl505_x_s07: mean 2.85 % BW.
