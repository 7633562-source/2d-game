"""tree-1019 designs: HEAD crown, r12 (req-269 BEST), P18 (req-287 expected), SEG24 (proposal).
All geometry from the game's own build JSONs (Trials/Runs/cl269_T0off.json HEAD, cl269_T4.json r12)."""
import json, math
import numpy as np
from tree_core import Tree, G, DT, box_inertia, inertia_rule

RUNS = r"E:/Games/2D Game/My project/Trials/Runs/"
JH = json.load(open(RUNS + "cl269_T0off.json", encoding="utf-8-sig"))["build"]["joints"]
J12 = json.load(open(RUNS + "cl269_T4.json", encoding="utf-8-sig"))["build"]["joints"]
PAR_HEAD = [-1, 0, 1, 1, 0, 4, 4, -1, 7, 8, 8, 7, 11, 11]
PAR_R12 = [-1, 0, 1, 2, 3, 4, 5, 5, 4, 8, 8, 3, 11, 12, 12]
Y_STUMP = 0.22
Y_CROWN = 0.22 + 0.82
CENTRED = {0: 6.0, 1: 18.0, 2: 35.0, 3: 40.0}
E_R12 = 177.35            # MPa of the r12 bole (tree-1018 E4)
LEAF_M, LEAF_A = 0.04, 0.10
RAD6 = math.radians(6.0)


def stops_from_json(j):
    return j["bindDeg"] - j["limitMaxDeg"], j["bindDeg"] - j["limitMinDeg"]


def build_head():
    t = Tree("HEAD")
    for i, j in enumerate(JH):
        lo, hi = stops_from_json(j)
        t.add(PAR_HEAD[i], j["sizeY"], j["sizeX"], j["mass"], j["inertia"], j["buildHeadingDeg"], j["order"],
              j["stiffness"], j["frictionDamping"], j["maxTorque"], lo, hi, root=(0.0, Y_CROWN))
    return t.finish()


def build_r12():
    t = Tree("r12")
    for i, j in enumerate(J12):
        lo, hi = stops_from_json(j)
        t.add(PAR_R12[i], j["sizeY"], j["sizeX"], j["mass"], j["inertia"], j["buildHeadingDeg"], j["order"],
              j["stiffness"], j["frictionDamping"], j["maxTorque"], lo, hi, root=(0.0, Y_STUMP))
    t.pts = [(b, 1.0, LEAF_M, LEAF_A) for b in range(len(J12)) if J12[b]["order"] in (1, 3)]
    return t.finish()


def _bole(t, E):
    """4 bole pieces, r12 geometry/masses, beam stiffness scaled from r12 (k ~ E r^4 / L)."""
    for i in range(4):
        j = J12[i]
        k = j["stiffness"] * E / E_R12
        t.add(i - 1, j["sizeY"], j["sizeX"], j["mass"], box_inertia(j["mass"], j["sizeY"], j["sizeX"]),
              0.0, 0, k, 0.0, k * math.radians(12.0), -CENTRED[0], CENTRED[0], root=(0.0, Y_STUMP), tag="bole")
    return 3


def _set_bole_damping(t, Ivec, zeta=0.08):
    for b in range(t.n):
        if t.ORD[b] == 0:
            t.C[b] = 2 * zeta * math.sqrt(t.K[b] * Ivec[b])   # r12 TuneBoleChain law (about the centre)


def _crown_law(t, crown_idx, k_head):
    """req-287 round 13: depths 1-2 k = max(k_HEAD, G + 0.45 g reach / rad6); c ~ sqrt(k), maxT ~ k."""
    H = t.hessian(t.pts)
    for b in crown_idx:
        if t.ORD[b] in (1, 2):
            Gi = -H[b, b]
            knew = max(k_head[b], Gi + 0.45 * G * t.reach(b) / RAD6)
            r = knew / t.K[b]
            t.K[b] = knew; t.C[b] *= math.sqrt(r); t.MT[b] *= r


def tune_bole_E(builder, target=25.0, lo=5.0, hi=200.0, **kw):
    """Smallest E (MPa) with min eig(K + H) >= target (req-287 round 13 item 4)."""
    for _ in range(30):
        mid = 0.5 * (lo + hi)
        t = builder(mid, **kw)
        ev = float(np.min(np.linalg.eigvalsh(np.diag(t.K) + t.hessian(t.pts))))
        if ev >= target:
            hi = mid
        else:
            lo = mid
    return hi


def build_p18(E=38.0, bole_rule="r12"):
    """req-287 expected tree: 4 bole + full HEAD crown (14) = 18 wood, 10 leaf clusters (8 twig tips + 2 limb
    tips; in r12 the cluster com sits on its hinge, so it is a point mass on the host tip), centred stops,
    crown masses HEAD x 1.121 (28 kg total), game floor minSegmentInertia 0.008 on the crown."""
    t = Tree("P18")
    top = _bole(t, E)
    k_head = {}
    for i, j in enumerate(JH):
        m = j["mass"] * 1.121
        p = PAR_HEAD[i] + 4 if PAR_HEAD[i] >= 0 else top
        o = j["order"]
        b = t.add(p, j["sizeY"], j["sizeX"], m, max(box_inertia(m, j["sizeY"], j["sizeX"]), 0.008),
                  j["buildHeadingDeg"], o, j["stiffness"], j["frictionDamping"], j["maxTorque"],
                  -CENTRED[o], CENTRED[o], root=(0.0, Y_STUMP), tag="crown")
        k_head[b] = j["stiffness"]
    t.pts = [(b, 1.0, LEAF_M, LEAF_A) for b in range(4, 18) if t.ORD[b] in (1, 3)]
    t.finish()
    _crown_law(t, range(4, 18), k_head)
    Ivec = inertia_rule(t, bole_rule)
    _set_bole_damping(t, Ivec)
    t.I = Ivec
    t.hold = -t.grav_Q(np.zeros(t.n), t.pts)
    return t


def build_seg(E=38.0, nseg=2, twig_law=True, foliage="dist", zeta=(0.08, 0.12, 0.12, 0.15), rule="pair",
              wdt=0.5, curve_deg=4.0, amax=1.0):
    """Proposal SEG24: P18 with every limb and branch split into nseg tapered pieces.
    * mass of a piece ~ length * mean width^2 (taper base width -> child width), total per limb/branch = P18;
    * stiffness ~ w^4 / L with series compliance sum(1/k_i) = 1/k_P18 (same limb-level static stiffness);
      floor k_i >= 1.5 G_i (gravity margin);
    * twigs (twig_law): k = max(1.9 G, G + 0.45 g reach / rad6) instead of HEAD 59 (crow on the tip = 6 deg);
    * damping by modal target: c_i = 2 zeta_order sqrt(k_i * Mjj_i) (Mjj = joint-space inertia about the joint);
    * distal pieces curve up by curve_deg (natural sweep, not a straight stick);
    * foliage 'dist': 0.40 kg / 1.0 m^2 spread as rigid points (twig mid + tip, limb distal mid), no bodies;
      'tips': 10 point masses on the host tips (as P18)."""
    p18 = build_p18(E)
    G_P18 = -np.diag(p18.hessian(p18.pts))
    t = Tree("SEG%d" % (4 + 14 + 6 * (nseg - 1)))
    top = _bole(t, E)
    newidx = {}
    width_child = {1: JH[1]["sizeX"], 2: JH[2]["sizeX"], 3: 0.03}
    for i, j in enumerate(JH):
        o = j["order"]
        bP = i + 4
        p = newidx[PAR_HEAD[i]] if PAR_HEAD[i] >= 0 else top
        L, w0, m0, k0 = j["sizeY"], j["sizeX"], p18.M[bP], p18.K[bP]
        if o in (1, 2) and nseg > 1:
            w1 = width_child[o]
            fr = (np.arange(nseg) + 0.5) / nseg
            wm = w0 + (w1 - w0) * fr
            ms = m0 * wm ** 2 / np.sum(wm ** 2)
            kr = wm ** 4                                   # relative k_i (same length pieces)
            net0 = k0 - G_P18[bP]                          # P18 net stiffness (spring - gravity gradient)
            ks = kr * np.sum(1.0 / kr) * net0              # sum 1/net_i = 1/net0; G_i added after finish()
            sgn = 1.0 if j["buildHeadingDeg"] < 0 else -1.0  # curve the distal pieces back towards vertical
            for s in range(nseg):
                head = j["buildHeadingDeg"] + (sgn * curve_deg * s)
                lp = L / nseg
                p = t.add(p, lp, float(wm[s]), float(ms[s]), box_inertia(ms[s], lp, wm[s]), head, o,
                          float(ks[s]), 0.0, float(ks[s]) * math.radians(2 * CENTRED[o] / nseg + 6),
                          -CENTRED[o] / nseg - 4, CENTRED[o] / nseg + 4, root=(0.0, Y_STUMP), tag="crown%d" % s)
            newidx[i] = p
        else:
            newidx[i] = t.add(p, L, w0, m0, box_inertia(m0, L, w0), j["buildHeadingDeg"], o, k0, 0.0,
                              k0 * math.radians(2 * CENTRED[o]), -CENTRED[o], CENTRED[o], root=(0.0, Y_STUMP),
                              tag="crown")
    n = len(t.P)
    if foliage == "dist":
        pts = []
        for b in range(4, n):
            if t.ORD[b] == 3:
                pts += [(b, 0.5, 0.02, 0.05), (b, 1.0, 0.02, 0.05)]
            elif t.ORD[b] == 1 and t.TAG[b] == "crown%d" % (nseg - 1):
                pts += [(b, 0.5, 0.04, 0.10)]
        t.pts = pts
    else:
        t.pts = [(b, 1.0, LEAF_M, LEAF_A) for b in range(4, n) if t.ORD[b] == 3 or
                 (t.ORD[b] == 1 and t.TAG[b] in ("crown", "crown%d" % (nseg - 1)))]
    t.finish()
    H = t.hessian(t.pts)
    for b in range(4, n):
        Gi = -H[b, b]
        if t.TAG[b].startswith("crown") and t.TAG[b] != "crown":
            t.K[b] = t.K[b] + Gi                            # net -> spring: k_i = G_i + net_i
            t.MT[b] = t.K[b] * math.radians(2 * CENTRED[t.ORD[b]] / nseg + 6)
        if t.ORD[b] == 3 and twig_law:
            t.K[b] = max(1.9 * Gi, Gi + 0.45 * G * t.reach(b) / RAD6)
            t.MT[b] = t.K[b] * math.radians(2 * CENTRED[3])
        t.K[b] = max(t.K[b], 1.5 * Gi)
    Ivec = inertia_rule(t, rule, wdt=wdt, amax=amax)
    t.I = Ivec
    Mm = t.mass_matrix(np.zeros(n), t.pts, Ivec)
    for b in range(n):
        if t.ORD[b] == 0:
            t.C[b] = 2 * zeta[0] * math.sqrt(t.K[b] * Ivec[b])          # game bole law (r12 TuneBoleChain)
        else:
            t.C[b] = 2 * zeta[min(t.ORD[b], 3)] * math.sqrt(t.K[b] * Mm[b, b])
    t.hold = -t.grav_Q(np.zeros(n), t.pts)
    return t


def describe(t):
    rows = []
    for b in range(t.n):
        rows.append(dict(b=b, parent=int(t.P[b]), order=int(t.ORD[b]), tag=t.TAG[b], L=round(float(t.L[b]), 4),
                         W=round(float(t.W[b]), 4), m=round(float(t.M[b]), 4), I=round(float(t.I[b]), 5),
                         k=round(float(t.K[b]), 2), c=round(float(t.C[b]), 4), maxT=round(float(t.MT[b]), 2),
                         stop=[round(math.degrees(t.LO[b]), 1), round(math.degrees(t.HI[b]), 1)]))
    return rows
