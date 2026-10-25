# req-draft — TREES: tapered 2-piece limbs and branches, distributed foliage, stable inertia rule (default off)

Status: DRAFT by Claude lab (tree-1019), not numbered — re-list `requests/` before assigning req-NNN (fleet workers write req-NNN too).
Type: DELEGATION (code change + takes). Lane: main (trees). Two rounds, one Unity build per round. No commit without GOOD.
Base: req-287 BEST (round 13-14 landed: margin law, centred stops, TreeWindField, judge fixes). Do NOT start before req-287 has a verdict. req-274 (archetypes) can follow this one or merge its generator later.
**PROPOSAL — tree structure change:** `-treeBranchSeg 2` adds 6 joints to the oak (24 wood instead of 18) and removes the 10 leaf-cluster bodies under `-treeLeaves 2`. Lab/owner approval of this structure change is required before the fleet lands it (the flag stays default off either way).
Design + numbers: `D:\hermes-data\claude-lab\cache\tree-1019\tree-audit.md` §4-6, model `tree_designs.py` (`build_seg`), `tree_core.py` (`inertia_rule(..., "pair")`, `b2sim`), picture `D:\hermes-data\claude-lab\frames\tree-1019\tree.png`.

## Why

After req-287 every limb and branch is still one rigid stick bending only at its root; twigs at k 59 sit at 8.4 Hz with zeta 0.4 (never visibly move); the 10 leaf clusters are bodies whose centre of mass is on their own hinge (inert, `MakeLeafCluster` in req-269-11.diff l.539) and their wind ignores the gust (`ApplyLeafWind` l.586 uses constant `windSpeed`). The lab model (SEG24) gives each order its own visible band: sway 0.30 Hz, limbs 2.1 / 3.5 Hz, branches 1.9 Hz, twigs 4.1 Hz, with the same limb-level static stiffness as req-287, and stays stable at dt 0.005 with a x2 stiffness margin once every joint obeys the pair inertia rule.

## Flags (all default OFF; with none of them the build is byte-identical)

| flag | meaning | default |
|---|---|---|
| `-treeBranchSeg <N>` | split every depth-1 (limb) and depth-2 (branch) stem into N tapered pieces on HingeJoint2D (N = 1 keeps req-287) | 1 |
| `-treeLeaves 2` | distributed foliage: leaf mass and leaf area as point loads on the wood (twig mid + tip, distal limb piece mid), NO leaf-cluster bodies; `-treeLeaves 1` keeps the req-269 clusters | as base |
| `-treeTwigLaw 1` | twigs k = max(1.9 G, G + 0.45 g reach / rad(6)) instead of the HEAD value | 0 |
| `-treeStableI <a>` | pair inertia rule on EVERY tree joint: raise the lighter body so k dt^2 (1/I_s + 1/I_p) <= a (0.25 <=> kOverStabilityBound <= 0.0625 counting both bodies) | 0 (off) |
| `-treeAeroDrag 1` | ApplyWind uses the relative speed (u - v_point) per stick and foliage point (aerodynamic damping) | 0 |
| `-treeImplicitDamp 1` | spare: tree JointFriction uses tau = -c w / (1 + c dt (1/I_s + 1/I_p)) instead of the stopTorque clamp | 0 |

Read once in the tree build path inside `if (HeadlessTrial.Active)` via `PlantTree.ReadPlantArg` (copy `-treeSeg` from req-269), list in the HeadlessTrial.cs flag list. Every hunk an early return or a guarded call. `-treeImplicitDamp` must be a per-instance bool on JointFriction set ONLY by PlantTree (JointFriction is shared with the human and the dog; their behaviour must not change).

## Do (round A: structure + laws)

1. `Grow(...)` (PlantTree.cs:2727): when `treeBranchSeg = N > 1` and depth is 1 or 2, build the stem as N pieces of length L/N; width tapers linearly from the stem's base width to its child width (twig width 0.03 for branches); piece mass ~ L_i w_i^2 normalised so the stem total equals the req-287 mass; each distal piece's heading turns 4 deg back toward vertical (natural sweep). Children (next depth) hinge on the LAST piece. Piece stops: +-(stop_depth / N + 4) deg centred on bind. Census cap = maxSegments + treeSeg - 1 + (N - 1) * (#limbs + #branches).
2. `AddWoodActuators(...)` (3888) for the pieces, **net-series law**: let k0, G0 be the req-287 spring and gravity gradient of the whole stem; per piece net_i = w_i^4 * sum_j(1/w_j^4) * (k0 - G0) (so sum 1/net_i = 1/(k0 - G0)); after the leaf/gravity gradient is computed at bind, k_i = G_i + net_i; floor k_i >= 1.5 G_i; maxTorque_i = k_i * rad(2 * stop_depth / N + 6).
3. `-treeTwigLaw 1`: twigs k = max(1.9 G, G + 0.45 g reach / rad(6)) (lab value about 13.5 N m/rad vs HEAD 59), maxTorque = k * rad(80).
4. Damping: crown pieces c_i = 2 zeta sqrt(k_i * Mjj_i), zeta 0.12 (limb, branch) / 0.15 (twig), with Mjj_i = moment of inertia of the piece's whole subtree (bodies + foliage points) about its joint (one tree walk at bind); bole unchanged (TuneBoleChain 2 * 0.08 * sqrt(k I)).
5. `-treeLeaves 2`: do not create cluster bodies; foliage 0.40 kg / 1.0 m^2 per tree split as point loads: each twig 0.02 kg + 0.05 m^2 at mid and at tip; each distal limb piece 0.04 kg + 0.10 m^2 at mid. Their weight goes into BindGravityHold / the gradient (the same way the r10 leaf weight does in req-269), and their drag is applied in ApplyWind at the point with the TreeWindField speed (so foliage now feels the gust). Leaf sprites stay where they are (cosmetic sway layer, `treeLeafSwayDegrees`), parented to the host wood.
6. `-treeStableI a`: after all inertias are set (after the `minSegmentInertia` floors, PlantTree.cs:3882 / 3974), iterate 6 passes over all tree joints: if a_j = k_j dt^2 (1/I_s + 1/I_p) > a, raise the lighter dynamic body to the inertia that makes a_j = a. Log sum I before/after and the max kOverStabilityBound (both bodies) in the build JSON.
7. Log per piece in the build JSON: depth, piece index, L, w, m, I, k, G, c, maxTorque, stops, kOverStabilityBound (both bodies).

## Do (round B: wind + spare)

8. `-treeAeroDrag 1`: in ApplyWind use rel = u(x, y, t) - v_point.x per stick (at the stick centre) and per foliage point; F = q rel |rel| A WindSail HeadingSailMul.
9. `-treeImplicitDamp 1`: tree JointFriction torque tau = -c w / (1 + c dt (1/I_s + 1/I_p)), no stopTorque clamp (still maxTorque clamp).

## Checks (in this order)

1. **S0 byte-identity**: std battery 11/11, md5 == c129a, with the new build and NO new flag.
2. **Off == base**: req-287 BEST gust take and seat take with the new build and no new flag: joints CSV byte-identical to the req-287 takes.
3. **Build JSON (flags `-treeBranchSeg 2 -treeLeaves 2 -treeTwigLaw 1 -treeStableI 0.25`)**: wood 24, leaf bodies 0, max kOverStabilityBound (both bodies) <= 0.0625, min eig(K+H) of the bole joints >= 25, sum I logged.
4. **Gust take** (req-287 recipe `-treeGust 1.4 -treeWindSpeed 2.6`, 60 s, seeds 1-3): crown tip pk-pk 12-28 cm (model 17-22), bole tip pk-pk inside the req-287 band (or <= 20 mm if req-287 settled on the wider band; model 16-20 mm at E 31, 10 mm at E 44), no joint pinned > 2 % of the time, spectrum of the bole tip peaks at 0.2-0.4 Hz, twig joint spectrum shows energy near 3-5 Hz (model 4.1).
5. **Stability margin**: one 10 s take with all stiffness x2 (`-treeStiffMul 2` if present, else a temporary build constant in the test build only): no blow-up (max |w| < 20 rad/s, no NaN). With `-treeImplicitDamp 1` also at x4.
6. **Seat take** (req-287 recipe, twig slot): landingLoadDropM within the judge band on the median twig; return < 5 mm after release; no stop hits.
7. **Round B**: `-treeAeroDrag 1` gust take: trunk-mode damping up (pluck decay or gust spectrum peak narrower), crown pk-pk within -10 % of check 4.
8. **Census / safety**: probe contacts == base, UnguardedSolid 0, wall-time per FixedUpdate within +15 % of req-287.

## Report format

```
VERDICT: GOOD | NO-KEEP | BLOCKED  (one line)
S0: 11/11 md5 <hash> (== c129a ?)
off==base: <yes/no>
build: wood <n> leafBodies <n> maxKOB <v> bole minEig <v> sumI before/after
gust s1-s3: crown pkpk cm / bole pkpk mm / pinned % / bole peak Hz / twig band energy
margin x2 (x4 implicit): ok/blow-up, max|w|
seat: drop cm / return mm / stops
aero (round B): trunk zeta or peak width, crown pkpk delta %
cost: ms per FixedUpdate base vs on
diff: <path to patch>, build hash
```
