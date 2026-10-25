"""tree-1019 experiments -> results.json, proto_arrays.npz, proto.log (run with numpy).
E0 validation vs game takes | E1 design table + modes | E2 stability at dt 0.005 (Box2D-style solver)
E3 60 s gust runs x 3 seeds + spectra | E4 crow landings / take-offs (step vs kick vs kick+mass)
E5 aerodynamic damping | E6 game-solver vs reference pluck."""
import json, math, os, sys, time
import numpy as np
from tree_core import *
from tree_designs import *

HERE = os.path.dirname(os.path.abspath(__file__))
T0 = time.time()
R = {}
ARR = {}


def log(*a):
    print("[%5.0fs]" % (time.time() - T0), *a, flush=True)


def welch(x, fs, seg_s=20.0):
    x = np.asarray(x, float) - np.mean(x)
    n = int(seg_s * fs); n = min(n, len(x))
    step = n // 2
    win = np.hanning(n)
    acc = None; cnt = 0
    for s in range(0, len(x) - n + 1, step):
        X = np.fft.rfft(x[s:s + n] * win)
        P = (np.abs(X) ** 2) / (fs * np.sum(win ** 2))
        acc = P if acc is None else acc + P; cnt += 1
    f = np.fft.rfftfreq(n, 1 / fs)
    P = acc / cnt; P[1:-1] *= 2
    return f, P


def band_peak(f, P, lo, hi):
    m = (f >= lo) & (f <= hi)
    return float(f[m][np.argmax(P[m])])


def centroid(f, P, lo=0.05, hi=20.0):
    m = (f >= lo) & (f <= hi)
    return float(np.sum(f[m] * P[m]) / np.sum(P[m]))


# ---------------- designs ----------------
head = build_head(); r12 = build_r12()
E_P = tune_bole_E(build_p18)
p18 = build_p18(E_P)
seg = build_seg(E_P, rule="pair", amax=0.5)
log("E tuned (min eig K+H >= 25):", round(E_P, 1), "MPa")

# ---------------- E0 validation ----------------
R["E0"] = {}
s = static_load(r12, 14, 1.0, 0.45)
R["E0"]["r12_crow_b14"] = dict(model_J11=float(-s["dq_deg"][11]), model_J12=float(-s["dq_deg"][12]),
                               take_J11=0.68, take_J12=0.39, drop_m=s["drop"])
for nm, tr, js, take in (("HEAD", head, (0, 7, 4, 11), (1.59, 0.95, 0.36, 0.27)), ("r12", r12, (4, 11), (0.14, None))):
    out = simulate(tr, 25.0, wind=SineWind(cap=3), wood_drag="abs", record_every=2)
    m = out["t"] > 5.0
    pk = {("J%02d" % j): float(np.ptp(np.degrees(out["q"][m, j]))) for j in js}
    R["E0"][nm + "_sine_pkpk_deg"] = dict(model=pk, take=dict(zip(["J%02d" % j for j in js], take)))
    log("E0", nm, pk)
R["E0"]["r12_b2sim"] = b2sim(r12, r12.I, seconds=2.0)
R["E0"]["head_b2sim"] = b2sim(head, head.I, seconds=2.0)
log("E0 crow", R["E0"]["r12_crow_b14"])


# ---------------- E1 design table ----------------
def order_freqs(md, tr):
    out = {}
    for o in range(4):
        fs = [m["f"] for m in md["modes"] if m["order"] == o]
        zs = [m["zeta"] for m in md["modes"] if m["order"] == o]
        if fs:
            out[o] = dict(f_min=round(min(fs), 3), f_max=round(max(fs), 3), zeta_first=round(zs[int(np.argmin(fs))], 3))
    return out


R["E1"] = {}
for tr in (head, r12, p18, seg):
    md = modal(tr)
    R["E1"][tr.name] = dict(bodies=int(tr.n), foliage_pts=len(tr.pts), mass=float(tr.M.sum() + sum(p[2] for p in tr.pts)),
                            sumI=float(tr.I.sum()), wdt_exact=md["wdt_max"], mineigKH=md["mineigKH"],
                            modes=[dict(f=round(m["f"], 3), zeta=round(m["zeta"], 3), order=m["order"], joint=m["joint"])
                                   for m in md["modes"][:14]],
                            by_order=order_freqs(md, tr), table=describe(tr))
    log("E1", tr.name, "n", tr.n, "f", [m["f"] for m in R["E1"][tr.name]["modes"][:8]], "wdt", round(md["wdt_max"], 2))
# crow statics on the designs (0.45 kg)
R["E1"]["static_crow"] = {}
for tr, picks in ((p18, {"twig": (6, 0.9), "branch": (5, 0.85), "limb": (4, 0.8)}),
                  (seg, {"twig": (8, 0.9), "branch": (7, 0.7), "limb": (5, 0.6)})):
    R["E1"]["static_crow"][tr.name] = {k: round(static_load(tr, b, f, 0.45)["drop"] * 100, 2) for k, (b, f) in picks.items()}
log("E1 static crow drop cm", R["E1"]["static_crow"])

# ---------------- E2 stability at the game step ----------------
RULES = {"true": dict(rule="true"), "r12": dict(rule="r12"), "pair1": dict(rule="pair", amax=1.0),
         "pair0.5": dict(rule="pair", amax=0.5), "pair0.25": dict(rule="pair", amax=0.25)}
R["E2"] = {}
for tr in (p18, seg):
    R["E2"][tr.name] = {}
    for nm, kw in RULES.items():
        I = inertia_rule(tr, **kw)
        md = modal(tr, Ivec=I)
        row = dict(sumI=float(I.sum()), wdt_exact=md["wdt_max"], f=[round(m["f"], 3) for m in md["modes"][:6]], runs={})
        for dmp in ("game", "implicit"):
            for km in (1, 2, 4):
                r = b2sim(tr, I, seconds=2.0, kmul=km, damper=dmp)
                row["runs"]["%s_k%d" % (dmp, km)] = dict(unstable=r["unstable"], maxrate=round(r["maxrate"], 3))
        R["E2"][tr.name][nm] = row
        log("E2", tr.name, nm, "sumI %.2f wdt %.2f" % (row["sumI"], row["wdt_exact"]),
            {k: ("X" if v["unstable"] else "ok") for k, v in row["runs"].items()})

# ---------------- E6 game solver vs reference: pluck ----------------
pl = np.zeros(seg.n); pl[4] = 0.4; pl[14] = -0.3; pl[0] = 0.05
ref = simulate(seg, 3.0, pluck=pl, record_every=2)
# b2sim with the same pluck: pass through pluck_deg_s=0 and set through seed? -> emulate by a custom pluck
def b2_pluck(tr, I, qd, damper):
    import tree_core
    rng_save = np.random.default_rng
    class FakeRng:
        def __init__(self, *a, **k): pass
        def uniform(self, lo, hi, n): return qd / math.radians(20.0)
    tree_core.np.random.default_rng = FakeRng
    try:
        return b2sim(tr, I, seconds=3.0, damper=damper, record=True)
    finally:
        tree_core.np.random.default_rng = rng_save
crown_tip = int(np.argmax(seg.rest[1][:, 1] + seg.rest[2][:, 1]))
R["E6"] = {}
for dmp in ("game", "implicit"):
    r = b2_pluck(seg, seg.I, pl, dmp)
    tt = np.array([x[0] for x in r["trace"]]); ang = np.array([x[1] for x in r["trace"]])
    ARR["e6_t_" + dmp] = tt; ARR["e6_q4_" + dmp] = ang[:, 4]
    R["E6"][dmp] = dict(unstable=r["unstable"], maxrate=r["maxrate"])
ARR["e6_t_ref"] = ref["t"]; ARR["e6_q4_ref"] = ref["q"][:, 4]
log("E6", R["E6"])

# ---------------- E3 gust runs ----------------
R["E3"] = {}
fs = 1.0 / (DT * 4)
probe = {}
for tr in (p18, seg):
    ph, base, e = tr.rest
    tips = base + e
    pr_ = dict(
        crown=int(np.argmax(tips[:, 1])),
        bole=3,
        limb=[b for b in range(tr.n) if tr.ORD[b] == 1 and tr.children[b] and tr.ORD[tr.children[b][0]] == 2][0],
        branch=[b for b in range(tr.n) if tr.ORD[b] == 2 and tr.children[b] and tr.ORD[tr.children[b][0]] == 3][0],
        twig=[b for b in range(tr.n) if tr.ORD[b] == 3][0])
    for suf in ("", "noturb", "abs", "fronts", "calm", "E44"):
        probe[tr.name + suf] = pr_
TURB = dict(sigTurb=0.6, tauTurb=0.1)
WCFG = {"P18": {}, "SEG24": TURB, "SEG24noturb": {}, "SEG24abs": TURB, "P18fronts": dict(fronts=True),
        "SEG24calm": dict(uMean=1.3, sigSlow=0.45, sigFast=0.25, **TURB), "P18E44": {}}
runs = [("P18", p18, "abs", s_) for s_ in (1, 2, 3)] + [("SEG24", seg, "rel", s_) for s_ in (1, 2, 3)] + \
       [("SEG24noturb", seg, "rel", 1), ("SEG24abs", seg, "abs", 1), ("P18fronts", p18, "abs", 1),
        ("SEG24calm", seg, "rel", 11), ("P18E44", build_p18(44.0), "abs", 1)]
for nm, tr, drag, sd in runs:
    wind = WindField(sd, seconds=62.0, **WCFG[nm])
    out = simulate(tr, 60.0, wind=wind, wood_drag=drag, record_every=4)
    pr = probe[nm]
    m = out["t"] >= 5.0
    tx = out["tipx"][m]
    res = dict(seed=sd, drag=drag, fronts=[round(f[0], 1) for f in wind.fr],
               crown_pkpk_cm=float(np.ptp(tx[:, pr["crown"]]) * 100),
               crown_rms_cm=float(np.std(tx[:, pr["crown"]]) * 100),
               bole_pkpk_mm=float(np.ptp(tx[:, pr["bole"]]) * 1000),
               pinned_max=float(out["pinned_frac"].max()),
               maxdev_deg={o: float(np.max(np.abs(np.degrees(out["q"][:, tr.ORD == o])))) for o in range(4)})
    spec = {}
    for key, sig in (("bole", out["tipx"][m, pr["bole"]]), ("crown", out["tipx"][m, pr["crown"]]),
                     ("limb_q", out["q"][m, pr["limb"]]), ("branch_q", out["q"][m, pr["branch"]]),
                     ("twig_q", out["q"][m, pr["twig"]])):
        f, P = welch(sig, fs)
        hb = (f >= 1.0) & (f <= 12.0)
        df = f[1] - f[0]
        spec[key] = dict(peak=band_peak(f, P, 0.1, 15.0), centroid=centroid(f, P, 0.1, 15.0),
                         peak_hi=band_peak(f, P, 1.0, 12.0),
                         rms_hi=float(math.sqrt(np.sum(P[hb]) * df)) * (180 / math.pi if key.endswith("_q") else 1000),
                         rms_all=float(math.sqrt(np.sum(P[f > 0.02]) * df)) * (180 / math.pi if key.endswith("_q") else 1000))
        if sd == 1:
            ARR["psd_%s_%s" % (nm, key)] = P; ARR["psd_f"] = f
    fu, Pu = welch(np.array(out["u"])[m], fs)
    if sd == 1:
        ARR["psd_%s_wind" % nm] = Pu
        ARR["gust_t_%s" % nm] = out["t"]; ARR["gust_q_%s" % nm] = out["q"]; ARR["gust_u_%s" % nm] = out["u"]
    res["spec"] = spec
    R["E3"]["%s_s%d" % (nm, sd)] = res
    log("E3", nm, sd, "crown pk-pk %.1f cm, bole %.1f mm, pinned %.3f" % (res["crown_pkpk_cm"], res["bole_pkpk_mm"],
        res["pinned_max"]), {k: (round(v["peak"], 2), round(v["peak_hi"], 2), round(v["rms_hi"], 3), round(v["rms_all"], 2))
                             for k, v in spec.items()})

# ---------------- E4 crows ----------------
CAND = {"P18": {"twig": ([b for b in range(p18.n) if p18.ORD[b] == 3], 0.9),
                "branch": ([b for b in range(p18.n) if p18.ORD[b] == 2], 0.85),
                "limb": ([b for b in range(p18.n) if p18.ORD[b] == 1], 0.8)},
        "SEG24": {"twig": ([b for b in range(seg.n) if seg.ORD[b] == 3], 0.9),
                  "branch": ([b for b in range(seg.n) if seg.ORD[b] == 2 and seg.TAG[b] == "crown1"], 0.7),
                  "limb": ([b for b in range(seg.n) if seg.ORD[b] == 1 and seg.TAG[b] == "crown1"], 0.6)}}
T_LAND, T_LEAVE, T_END = 0.5, 6.5, 9.0


def crow_metrics(t, y, Tp=0.08):
    y0 = np.mean(y[(t >= 0.3) & (t < T_LAND)])
    ys = np.mean(y[(t >= T_LEAVE - 0.4) & (t < T_LEAVE - Tp - 0.02)])
    dip_fast = y0 - np.min(y[(t >= T_LAND) & (t < T_LAND + 0.3)])
    to_dip = ys - np.min(y[(t >= T_LEAVE - Tp - 0.02) & (t < T_LEAVE + 0.3)])
    vel = np.gradient(y, t)
    vland = -np.min(vel[(t >= T_LAND) & (t < T_LAND + 0.3)])
    static = y0 - np.mean(y[(t >= T_LEAVE - 1.0) & (t < T_LEAVE)])
    dip = y0 - np.min(y[(t >= T_LAND) & (t < T_LAND + 1.5)])
    after = (t >= T_LEAVE) & (t < T_END)
    recoil = np.max(y[after]) - y0
    dev = np.abs(y - y0)
    bad = np.where(after & (dev > 0.002))[0]
    settle = (t[bad[-1]] - T_LEAVE) if len(bad) else 0.0
    return dict(static_cm=float(static * 100), dip_cm=float(dip * 100), overshoot=float(dip / max(static, 1e-6) - 1),
                dip_fast_cm=float(dip_fast * 100), takeoff_dip_cm=float(to_dip * 100), v_land_cms=float(vland * 100),
                recoil_cm=float(recoil * 100), settle_s=float(settle))


R["E4"] = {}
modes = [("P18", p18, "step"), ("SEG24", seg, "step"), ("SEG24", seg, "kick"), ("SEG24", seg, "kicks"),
         ("SEG24", seg, "kickmass"), ("P18", p18, "kicks")]


def sub_mass(tr, b):
    sb = tr.subtree(b)
    return float(tr.M[sb].sum() + sum(p[2] for p in tr.pts if p[0] in sb))
NEV = 8
for dname, tr, mode in modes:
    key = "%s_%s" % (dname, mode)
    R["E4"][key] = {}
    for ptype in ("twig", "branch", "limb"):
        bodies, frac = CAND[dname][ptype]
        rows = []
        for i in range(NEV):
            rng = np.random.default_rng(1000 + i)
            b = bodies[i % len(bodies)]
            ev = crow_event(rng, T_LAND, T_LEAVE, b, frac, mode)
            ms_ = sub_mass(tr, b); ev["scale"] = ms_ / (ms_ + ev["m"])
            out = simulate(tr, T_END, crow=ev, record_every=1, m_every=5)
            y = out["perch"][:, 1]
            rows.append(crow_metrics(out["t"], y, ev["Tp"]))
            rows[-1]["static_lin_cm"] = static_load(tr, b, frac, ev["m"])["drop"] * 100
            if i == 0:
                ARR["crow_t"] = out["t"]; ARR["crow_y_%s_%s" % (key, ptype)] = y - np.mean(y[(out["t"] >= 0.3) & (out["t"] < T_LAND)])
        agg = {k: dict(mean=float(np.mean([r[k] for r in rows])), min=float(np.min([r[k] for r in rows])),
                       max=float(np.max([r[k] for r in rows]))) for k in rows[0]}
        R["E4"][key][ptype] = agg
        log("E4", key, ptype, {k: round(v["mean"], 3) for k, v in agg.items()})

# ---------------- E5 aerodynamic damping ----------------
def aero_C(tr, u=2.6):
    ph, base, e = tr.rest
    com = base + 0.5 * e
    Jc = tr.jac(com, np.arange(tr.n), base)
    C = np.einsum("p,pn,pm->nm", 2 * Q_DYN * u * tr.AREA * (1 + 0.22 * np.abs(np.sin(ph))), Jc[:, 0, :], Jc[:, 0, :])
    if tr.pts:
        fb = np.array([p[0] for p in tr.pts]); ff = np.array([p[1] for p in tr.pts]); fa = np.array([p[3] for p in tr.pts])
        P = base[fb] + ff[:, None] * e[fb]
        Jf = tr.jac(P, fb, base)
        C += np.einsum("p,pn,pm->nm", 2 * Q_DYN * u * fa / math.sqrt(1 + (u / 5) ** 2), Jf[:, 0, :], Jf[:, 0, :])
    return C


R["E5"] = {}
for tr in (p18, seg):
    md0 = modal(tr)
    Ca = aero_C(tr)
    # modal() takes a vector; use the full matrix through a temporary diagonal-free path
    pts = tr.pts
    Mm = tr.mass_matrix(np.zeros(tr.n), pts); K = np.diag(tr.K) + tr.hessian(pts)
    n = tr.n; Mi = np.linalg.inv(Mm)
    zs = []
    for Cm in (np.diag(tr.C), np.diag(tr.C) + Ca):
        A = np.block([[np.zeros((n, n)), np.eye(n)], [-Mi @ K, -Mi @ Cm]])
        lam = np.linalg.eigvals(A); lam = lam[lam.imag > 1e-6]
        o = np.argsort(np.abs(lam)); lam = lam[o]
        zs.append([(round(float(abs(l) / 2 / math.pi), 3), round(float(-l.real / abs(l)), 4)) for l in lam[:5]])
    R["E5"][tr.name] = dict(wood_only=zs[0], with_aero=zs[1])
    log("E5", tr.name, zs)

# ---------------- frames for the picture ----------------
qg = ARR["gust_q_SEG24"]; tg = ARR["gust_t_SEG24"]
pr = probe["SEG24"]
tipx = np.array([seg.kin(q)[1][pr["crown"]][0] + seg.kin(q)[2][pr["crown"]][0] for q in qg[::5]])
i_max = int(np.argmax(np.abs(tipx - tipx[0]))) * 5
R["frames"] = dict(t_peak=float(tg[i_max]))
np.savez_compressed(os.path.join(HERE, "proto_arrays.npz"), **ARR)
with open(os.path.join(HERE, "results.json"), "w", encoding="utf-8") as fh:
    json.dump(R, fh, indent=1, default=lambda o: o.tolist() if hasattr(o, "tolist") else str(o))
log("done")
