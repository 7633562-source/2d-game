# Trees stream audit + segmented-branch prototype (tree-1019)

Claude lab subagent, 19.10 (night burn). Read-only on the game repo; no Unity runs. Everything below is from code, the build JSONs/joint CSVs of existing takes, the earlier lab prototypes and a new numpy model in this folder.

Files (all in `D:\hermes-data\claude-lab\cache\tree-1019\`):

| file | what |
|---|---|
| `tree_core.py` | planar articulated tree: kinematics, mass matrix, gravity Hessian, modal analysis, OU wind field (req-287 spec), crow landing/take-off impulse model, implicit reference integrator, Box2D-style sequential-impulse integrator (`b2sim`, dt 0.005, 8 velocity iterations, game TreeSpring + JointFriction laws) |
| `tree_designs.py` | HEAD crown, r12 (req-269 BEST), P18 (req-287 expected), SEG24 (proposal) built from the game's own build JSONs (`Trials/Runs/cl269_T0off.json`, `cl269_T4.json`) |
| `tree_proto.py` | experiments E0-E6 -> `results.json`, `proto_arrays.npz`, `proto.log` |
| `game_takes.py` / `game_takes.json` | spectra and crow seat steps of existing game takes (HEAD T0off, r12 T4/PR) |
| `tree_plot.py` | picture -> `D:\hermes-data\claude-lab\frames\tree-1019\tree.png` (Russian labels) |
| `req-draft-tree-crowkick.md` | small request: landing arrest + take-off push impulses on the perch (default off) |
| `req-draft-tree-branchseg.md` | larger request after req-287: tapered 2-piece limbs/branches, distributed foliage, stable inertia rule (default off; tree structure change marked for approval) |

## 1. What is in the game now

Source: `Assets/PlantTree.cs`, `TreeSpring.cs`, `JointFriction.cs`, `TreeDriver.cs`, `BirdTriggerPerch.cs`, `LevelGrove.cs`; physics step `Time.fixedDeltaTime = 0.005` (`GameProcess.cs:134`, `TimeManager.asset`).

**HEAD tree (default, `-tree 1`, rig Sway):** static stump + trunk folded into the stump (the trunk never bends), 14 dynamic crown boxes (limbs order 1, branches 2, twigs 3) on HingeJoint2D with limits centred on the bind angle. Per joint `TreeSpring` (hold torque + clamp(k err, +-maxTorque)), `JointFriction` (viscous, clamped by stopTorque = |w_rel| / ((1/I_s + 1/I_p) dt)). `minSegmentInertia 0.008` (`PlantTree.cs:108`). Wind `ApplyWind` (`PlantTree.cs:2034`): F = 0.5 rho u^2 A WindSail HeadingSailMul with **absolute** speed and a deterministic sinusoidal gust (windSpeed 2.2, windGust 1.1, period 4.5 s, windScale 0.12). Game take spectra (HEAD T0off): the whole crown follows the 0.218 Hz forcing metronome; pk-pk J00 1.59 deg, J07 0.95, J04 0.36, J11 0.27.

**Crow on the tree:** `BirdTriggerPerch.SeatOnRing` sets `tree.perchLoadKg = birdMass` (l.1134), `LeaveRing` sets 0 (l.1161). `ApplyPerchLoad` (`PlantTree.cs:2459-2499`) is a constant **force** m g at the bound pad point while seated - no landing arrest impulse, no take-off push, the bird body does not ride the wood. HEAD probe seat step: J08 +1.22 deg (peak 1.87, overshoot 53 %); r12 PR J11 +0.68 deg, J12 +0.39 (peak 0.81), release J11 -0.65.

**req-269 r12 (BEST, 28/35, not landed, `patches/req-269-11.diff`):** 4-piece bole (`-treeSeg`), leaf clusters (`-treeLeaves`), stiff crown. Facts that matter for the next step:
- leaf cluster rigidbody sits at the host tip with anchor 0 and no collider -> its centre of mass is ON its hinge -> the hinge is inert; the 10 cluster bodies act only as point masses on the host tip (cost of 10 bodies for no motion);
- `ApplyLeafWind` uses `float u = windSpeed;` (constant), relative speed rel = u - v.x, so leaf wind ignores the gust;
- bole tuned by kSeries > 3 sum G; bole kOverStabilityBound J00 0.0625, J01-J03 about 0.10 (the rule raises the bole inertia but ignores the parent body);
- result: crown sway 0.94 cm, trunk tip 0.53 mm (r12 game) - alive only on paper.

**req-287 (pending, rounds 13-14, `requests/req-287.md`):** margin rule k_i = max(k_HEAD, G_i + 0.45 g reach_i / rad(6)) on depths 1-2, centred stops +-6/18/35/40, census 18 wood + 10 clusters, bole by min eig(K+H) >= 25, TreeWindField OU gust (uMean 2.6, sigma 1.4/0.5, tau 3/0.5, uAdv 3) fed to ApplyWind and ApplyLeafWind, judge bands trunk 3-15 mm, landingLoadDropM 1-5 cm. **req-274** (seven archetypes, tree-1017b) waits on req-269/287.

Game gaps (ranked by what the owner would see):
1. Crow = weight step only: no dip on touch-down, no kick on take-off (the branch does not "answer" the bird).
2. Crown pieces are single rigid sticks (one hinge per limb/branch): a limb bends only at its root, no curvature.
3. r12/req-287 leaf clusters are inert bodies (com on hinge); foliage motion = only the cosmetic sprite sway.
4. Leaf wind ignores the gust (constant windSpeed); wood wind uses absolute speed (no aerodynamic damping).
5. HEAD wind is a deterministic sinusoid (the OU field is unwired until req-287 round 14).
6. JointFriction stopTorque clamp limits the damping that light crown pieces can get.

## 2. What the earlier lab prototypes found

| prototype | design | numbers | lesson |
|---|---|---|---|
| tree-1017 (`segment-tree-design.md`, `tree-sim-numbers.txt`) | 31 bodies (19 wood + 12 leaf clusters), 4-piece trunk, E_game auto-tune | E_game 21.9 MPa, trunk f1 0.62 Hz, limbs 3.1 Hz, min eig K 0.42 | explicit per-joint damping on a stiff chain diverged at dt 0.005; K = diag(k) - H_grav must be positive definite; series stiffness of a chain must beat the whole-chain gradient |
| tree-1017b (Fable, `numbers.txt`) | 7 seeded archetypes, Leonardo radii, crow floor k >= 0.45 g reach / 6 deg | oak f1 0.63 Hz, gust amp 15.5 cm, crow dip 2.7 cm; birch 0.47 Hz / 93 cm; spruce 0.73 / 8.6 cm; grove 175 bodies | the crow floor rule (6 deg per 0.45 kg at the farthest tip) is the right liveliness floor; gust fronts 3.5 m/s are storm-level |
| tree-1018 (`tree-diag.md`, `fleet-answer.md`) | 15-body model of r12, validated on r10/r12 takes | r10 limbs pinned (k/G 1.13, min eig 2.26, 0.10 Hz); r12 alive only 0.94 cm; margin law k/G 1.9; bole E 32-44 MPa expected | became req-287 rounds 13-14 |

Correction of an earlier claim (tree-1018 notes): "a hinge-aware inertia rule needs about 10x less raised inertia" is **wrong for chains**. The fastest bole mode is a zigzag mode dominated by the pieces' own inertia about their centres; with true inertia the joint-space w_max dt is 2.93 (unstable for any solver). The pair rule below is what works.

## 3. This run: model and validation

Planar chain of rigid sticks; joint-space dynamics with the exact mass matrix (bodies + foliage points), gravity Hessian, game spring law (hold + k err, maxTorque clamp), damping, hinge stops, wind on every stick and foliage point (req-287 OU field with spatial advection, game drag law). Two integrators: implicit reference and a Box2D-like sequential-impulse solver at dt 0.005 with the game's explicit TreeSpring/JointFriction (fidelity vs reference on a pluck: corr 0.93, amplitude ratio 1.24).

Validation against game takes:

| case | model | take |
|---|---|---|
| HEAD sine wind pk-pk J00 / J07 / J04 / J11 (deg) | 1.48 / 0.92 / 0.33 / 0.19 | 1.59 / 0.95 / 0.36 / 0.27 |
| r12 sine wind J04 pk-pk | 0.16 | 0.14 |
| r12 crow seat slot 14, J11 / J12 (deg) | 0.65 / 0.32 | 0.68 / 0.39 |

## 4. Designs compared

- **P18** = what req-287 is expected to produce: 4 bole + 14 crown (HEAD topology x1.121 mass, 28.4 kg), margin law, bole E 31.0 MPa by min eig(K+H) = 25, 10 leaf point masses (0.04 kg, 0.10 m^2) on twig/limb tips, r12 bole inertia rule.
- **SEG24** (proposal): P18 with every limb and branch split into 2 tapered pieces (24 wood, **0 leaf bodies**). Laws:
  - mass of a piece ~ L w^2 (taper from base width to child width), per limb total = P18;
  - **net-series stiffness**: sum 1/(k_i - G_i) = 1/(k_P18 - G_P18), weights w^4 (keeps the limb-level static stiffness net of gravity; a plain series rule made the crow drop 8x too small);
  - **twig law** k = max(1.9 G, G + 0.45 g reach / rad(6)) = 13.5 N m/rad instead of HEAD 59 (crow on the tip = 6 deg);
  - floor k_i >= 1.5 G_i; distal pieces curve 4 deg back toward vertical;
  - damping: bole 2 * 0.08 * sqrt(k I) (game TuneBoleChain law), crown c = 2 zeta sqrt(k Mjj) with zeta 0.12 / 0.12 / 0.15 by order;
  - **distributed foliage**: 0.40 kg / 1.0 m^2 as rigid points (twig mid + tip 0.02 kg / 0.05 m^2 each, distal limb mid 0.04 / 0.10), feeding mass, gravity hold and wind drag - no cluster bodies;
  - **pair inertia rule**: raise the lighter body of every joint so a = k dt^2 (1/I_s + 1/I_p) <= amax (amax 0.25 <=> kOverStabilityBound <= 0.0625 counted with BOTH bodies).

Modal results (zeta = damping ratio):

| design | 2 lowest sway modes (whole crown, bole tip moves) | limb modes | branches | twigs | bole internal | w_max dt (true I) |
|---|---|---|---|---|---|---|
| HEAD crown | 0.34 (zeta 0.24), 0.38 Hz | 4.1 (0.66) | 1.79 (0.31) | 9.5 (0.46) | static trunk | 0.52 |
| r12 | 0.70 (0.11), 1.03 Hz | 4.4-7.5 | 2.65 (0.24) | 8.1 (0.39) | >= 14 Hz | 0.84 |
| P18 | 0.235 (0.116), 0.48 Hz | 2.44, 3.8 | 1.58 (0.28) | 8.4 (0.40), effectively rigid | >= 11 Hz | 0.83 |
| SEG24 | 0.30 (0.056), 0.57 Hz | 2.08, 3.53 | 1.91 (0.08) | 4.07 (0.15) | >= 9.7 Hz | 0.88 |

(The modal classifier names the lowest modes "limb" because the limb roots carry most kinetic energy; they are the whole-tree sway modes and the bole-tip spectrum peaks there.)

SEG24 meets "trunk 0.2-1 Hz, branches faster" and gives every order its own visible band; P18 twigs at 8.4 Hz with zeta 0.4 never move visibly.
Note: SEG24 min eig(K+H) of the whole tree is 12.8 (the softest mode is a distal limb piece, not the bole) - the req-287 bole criterion must be evaluated on the bole joints only.

## 5. Stability at the game's physics step (dt 0.005, Box2D-style solver)

| inertia rule | P18 k x1 / x2 / x4 | SEG24 k x1 / x2 / x4 |
|---|---|---|
| true inertia | X / X / X | X / X / X |
| r12 (bole only, I >= k dt^2 / 0.25) | ok / ok / X | ok / X / X |
| pair a <= 1 | ok / X / X | X / X / X |
| pair a <= 0.5 | ok / ok / X | ok / X / X |
| pair a <= 0.25 (kOB <= 0.0625 both bodies) | ok / ok / ok | ok / ok / X (game damper); ok / ok / ok with implicit damper |

- SEG24 needs the pair rule a <= 0.25 on EVERY joint for a x2 stiffness margin: sum I 0.25 -> 2.04 kg m^2, frequencies change < 2 % (inertia is raised on light pieces whose motion is dominated by their parent's translation).
- The wind and crow runs in sections 6-7 used SEG24 with amax 0.5 (sum I 1.00, kOB max 0.125); modal frequencies differ < 2 % from amax 0.25 and the damping law c = 2 zeta sqrt(k Mjj) keeps the damping ratios, but for the game use 0.25 (margin x2).
- Implicit damper tau = -c w / (1 + c dt (1/I_s + 1/I_p)) buys the x4 margin; it is a one-line change in JointFriction and is offered as a spare flag.
- The P18 "r12 rule" already leaves a x2 margin, so req-287 itself is not at risk at dt 0.005.

## 6. Wind (60 s, req-287 OU field, 3 seeds; pinned fraction 0 in every run)

| run | crown tip pk-pk | bole tip pk-pk | note |
|---|---|---|---|
| P18 (E 31) | 23.5 / 27.3 / 28.3 cm | 21.1 / 24.9 / 25.2 mm | bole fails the req-287 judge band 3-15 mm |
| P18 E 44 | 14.4 cm | 10.2 mm | passes the band |
| SEG24 (E 31, relative drag + small eddies) | 17.8 / 16.6 / 21.7 cm | 16.9 / 15.7 / 20.4 mm | at the band edge |
| SEG24 calm (uMean 1.3) | 3.9 cm | 4.0 mm | |
| P18 + gust fronts 2-4 m/s | 44.1 cm | 39.9 mm | storm; keep fronts as an event, not the base field |

- Spectra: everything is dominated by the trunk mode (peak 0.20-0.30 Hz); the high band (1-12 Hz) twig RMS is 0.004 deg in P18 and 0.012 deg in SEG24 even with small-eddy turbulence (sigma 0.6 m/s, tau 0.1 s, 8 streams) - invisible. **Leaf flutter belongs to the sprite layer** (treeLeafSwayDegrees 8), not to physics; a turbulence flag is not worth it.
- Relative-velocity wood drag adds aerodynamic damping +0.016 to +0.021 zeta on the trunk mode (P18 0.116 -> 0.137, SEG24 0.056 -> 0.072), <= 0.01 elsewhere, sway -2 %. Cheap optional flag.
- Recommendation for req-287 judging: bole E about 44 MPa (or tune target min eig 40) to sit inside 3-15 mm, OR widen the band to 3-25 mm; the crown (23-28 cm) looks right for a 2.5 m tree in a 2.6 m/s breeze.

## 7. Crow landing and take-off (8 random events per case, crow 0.40-0.52 kg)

Event model (randomised per event): arrival 0.8-1.8 m/s at 20-60 deg down, arrest half-sine of impulse m v_in over Ta 0.03-0.07 s; weight while seated; push-off reaction -m v_out over Tp 0.06-0.10 s, v_out 1.8-2.8 m/s at 45-75 deg up, side random. Proposed in-game form "kicks": impulses scaled by s = m_sub / (m_sub + m_bird) (inelastic share; m_sub = perch body + descendants + foliage; s = 0.49 on twigs, 0.75-0.83 branches, 0.93-0.98 limbs/bole). Reference "kickmass": full impulses with the bird mass actually riding the wood.

Twig perch, means (cm, cm/s):

| case | fast dip 0.3 s | landing speed of perch | take-off dip | recoil after take-off |
|---|---|---|---|---|
| P18 step (game today) | - | 9.6 | none | - |
| SEG24 step | 3.17 | 18.6 | -0.38 (rises) | 3.16 |
| SEG24 full kick (no scaling) | - | 68 | 5.2 | overshoots |
| **SEG24 kicks (proposal)** | **3.55** | **38** | **1.92** | **3.21** |
| reference (bird mass riding) | 3.94 | 28 | 2.03 | 3.52 |
| P18 kicks | 2.72 | 18.4 | 1.82 | - |

- The scaled kick reproduces the physical reference within about 10-35 % on every metric, on both designs, and is a tiny code change (two half-sine pulses on the load's rising/falling edges). Full unscaled kicks overshoot 2.5x.
- Static drops: twig mean 7-10 cm over 8 twigs including 60 deg headings (outer twigs are above the judge band 1-5 cm; the median twig in the static check is 3.9 cm P18 / 2.7 cm SEG24); branch 1.8-3.5 cm; limb 0.2-0.5 cm. The judge band should be checked on the twig the seat take actually uses.

## 8. Recommendations (in order)

1. **req-287 first** (pending): suggest bole E about 44 MPa (or band 3-25 mm) - otherwise the trunk band fails by the model (21-25 mm at E 31).
2. **Crow kick** (`req-draft-tree-crowkick.md`, small, independent of segmentation): `-treePerchKick 1` adds the scaled arrest/push impulses on the perch-load edges. Biggest visible win per line of code.
3. **Segmented branches** (`req-draft-tree-branchseg.md`, after req-287): `-treeBranchSeg 2` + `-treeLeaves 2` (distributed foliage, drop the inert cluster bodies) + `-treeStableI 1` (pair rule kOB <= 0.0625 every joint), optional `-treeAeroDrag 1`, spare `-treeImplicitDamp 1`. More joints = tree structure change -> needs lab/owner approval before landing.
4. Do not spend physics on leaf flutter or small-eddy turbulence; keep that in the sprite layer. Keep gust fronts as rare level events (req-274), not the base field.
5. Living-world note: crow events and wind are seeded per tree / per event (random headings, speeds, masses), no scripted sway.
