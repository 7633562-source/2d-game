"""replay541.py - R1 / R2 replays of the rep-541 log-2 takes (unity_log2/<label>_twin2.csv, 200 Hz, after all writers).

R1  = exact state init (22 bodies: pelvis pose/velocity -> root, 21 hinges ang/spd -> q/qd) + the applied torques
      (tauM + tauFr + tauSp per hinge, the assist torque on the pelvis); the twin's own motors, joint friction and
      tail springs are off; the twin's paw contact stays on (the contact model under test).
R1f = R1 with the twin's own joint friction law (f_<joint>) instead of the logged friction torque (closed loop).
R2  = R1 with the paw-floor contact OFF and the logged paw contact forces (fn, ft at px, py) applied as external
      forces: the contact model is taken out of the test.
R2f = R2 with the twin's friction law.
Windows of 0.5 s every 0.5 s from 1.0 s; drift at 0.05/0.1/0.2/0.3/0.5 s. The headline is the rep-531 gate metric
(joint RMS over the 10 probe joints + 4 paws at 0.2 s, bar 5 deg on >= 80 % of the windows).
Signs (Unity source): q = Unity jointAngle (skeleton.md); Muscle / JointFriction / TreeSpring add +tau on the child,
-tau on the parent, and +CCW on the child lowers jointAngle -> generalised force on q = -(tauM + tauFr + tauSp)
(JointFriction.cs:79-82, TreeSpring.cs:101-103). Assist mode 1 = pelvisRb.AddTorque (DSC:7507) -> xfrc[pelvis, 4] = -tau.

usage: python replay541.py check  [label]           # state-seeding consistency, sign / paw-order / lag checks
       python replay541.py run    [label ...] [--lag 0|1] [--integrator Euler]  -> replay541.json / .png
"""
import argparse
import json
import math
import os
import sys

import mujoco
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build_dog as B  # noqa: E402
import twin_dog as T  # noqa: E402

DIR = os.path.join(HERE, "unity_log2")
DT = B.DT
SIDE = {"Right": "r", "Left": "l"}
JMAP = {"Chest": "lumbar", "Neck": "neck", "Head": "head", "Jaw": "jaw", "Tail": "tail0", "Tail2": "tail1",
        "Tail3": "tail2", "Tail4": "tail3", "Tail5": "tail4"}
BMAP = {"Chest": "chest", "Pelvis": "pelvis", "Neck": "neck", "Head": "head", "Jaw": "jaw", "Tail": "tail0",
        "Tail2": "tail1", "Tail3": "tail2", "Tail4": "tail3", "Tail5": "tail4"}
for _u, _s in SIDE.items():
    JMAP.update({"Front%sUpper" % _u: "shoulder_" + _s, "Front%sLower" % _u: "elbow_" + _s, "Front%sPaw" % _u: "fpaw_" + _s,
                 "Rear%sThigh" % _u: "hip_" + _s, "Rear%sShin" % _u: "knee_" + _s, "Rear%sPaw" % _u: "rpaw_" + _s})
    BMAP.update({"Front%sUpper" % _u: "upper_f" + _s, "Front%sLower" % _u: "lower_f" + _s, "Front%sPaw" % _u: "paw_f" + _s,
                 "Rear%sThigh" % _u: "thigh_r" + _s, "Rear%sShin" % _u: "shin_r" + _s, "Rear%sPaw" % _u: "paw_r" + _s})
GATE_J = ["shoulder_r", "shoulder_l", "hip_r", "hip_l", "elbow_r", "elbow_l", "knee_r", "knee_l", "lumbar", "neck",
          "fpaw_r", "fpaw_l", "rpaw_r", "rpaw_l"]          # the rep-531 gate set (10 probe joints + 4 paws)
PAW_BODIES = ["paw_fr", "paw_fl", "paw_rr", "paw_rl"]
HZ = [0.05, 0.1, 0.2, 0.3, 0.5]


def read2(label):
    p = os.path.join(DIR, label + "_twin2.csv")
    hdr = open(p).readline().strip().split(",")
    A = np.loadtxt(p, delimiter=",", skiprows=1)
    c = {k: i for i, k in enumerate(hdr)}
    g = lambda k: A[:, c[k]]
    meta = json.load(open(p.replace(".csv", ".json")))
    L = dict(t=g("t"), A=A, c=c)
    L["bodies"] = [b.split(".", 1)[1] for b in meta["bodies"]]
    L["joints"] = [b.split(".", 1)[1] for b in meta["joints"]]
    return L


def col(L, name):
    return L["A"][:, L["c"]["TrialDog." + name]] if ("TrialDog." + name) in L["c"] else L["A"][:, L["c"][name]]


PASSIVE = ["Tail", "Tail2", "Tail3", "Tail4", "Tail5", "Jaw"]   # Unity's tail chain chatters at the Nyquist rate
# (tail w +-5000 deg/s step to step, its linear velocities break the joint constraints by up to 2 m/s): with passive=True
# these joints keep the twin's own friction law + TreeSpring, start at the logged angle with zero relative speed, and
# only their logged muscle torque (tail0, jaw) is replayed.


def model(pair=1.2649, integrator="Euler", own_fric=False, paw_contact=True, passive=False):
    m, d, _ = T.load_model(paw_mu=pair * pair / B.GROUND_MU, integrator=integrator, jaw_hinge=True, tail_len=0.20,
                           tail_k=2.0)
    pas = {JMAP[u] for u in PASSIVE} if passive else set()
    for a in range(m.nu):
        n = mujoco.mj_id2name(m, mujoco.mjtObj.mjOBJ_ACTUATOR, a)
        if n.startswith("m_") or (n.startswith("f_") and not own_fric and n[2:] not in pas):
            m.actuator_gear[a, 0] = 0.0
    if not passive:
        m.jnt_stiffness[:] = 0.0          # tail TreeSprings come from the log
    if not paw_contact:
        for b in PAW_BODIES:
            g = mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_GEOM, b + "_g")
            m.geom_contype[g] = 0
            m.geom_conaffinity[g] = 0
    return m, d


class Map:
    def __init__(self, m, L):
        jid = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_JOINT, n)
        self.q = {u: m.jnt_qposadr[jid(n)] for u, n in JMAP.items()}
        self.v = {u: m.jnt_dofadr[jid(n)] for u, n in JMAP.items()}
        self.bid = {u: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_BODY, n) for u, n in BMAP.items()}
        self.gid = {u: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_GEOM, n + "_g") for u, n in BMAP.items()}
        assert min(self.q.values()) >= 0 and min(self.gid.values()) >= 0
        self.pel = self.bid["Pelvis"]


def seed(m, d, M, L, k, spd_sign=1.0, passive=False):
    """exact state of row k: hinges from ang/spd, root from the pelvis pose and velocity."""
    mujoco.mj_resetData(m, d)
    A, c = L["A"], L["c"]
    for u in JMAP:
        d.qpos[M.q[u]] = math.radians(A[k, c["TrialDog.%s_ang" % u]])
        d.qvel[M.v[u]] = 0.0 if (passive and u in PASSIVE) else spd_sign * math.radians(A[k, c["TrialDog.%s_spd" % u]])
    mujoco.mj_forward(m, d)
    a0 = T.world_angle(d, M.gid["Pelvis"])
    d.qpos[2] += math.radians(A[k, c["TrialDog.Pelvis_rot"]] - a0)
    mujoco.mj_forward(m, d)
    pc = d.geom_xpos[M.gid["Pelvis"]]
    d.qpos[0] += A[k, c["TrialDog.Pelvis_x"]] - pc[0]
    d.qpos[1] += A[k, c["TrialDog.Pelvis_y"]] + 2.0 - pc[2]
    d.qvel[2] = math.radians(A[k, c["TrialDog.Pelvis_w"]])
    mujoco.mj_forward(m, d)
    v6 = np.zeros(6)
    mujoco.mj_objectVelocity(m, d, mujoco.mjtObj.mjOBJ_GEOM, M.gid["Pelvis"], v6, 0)
    d.qvel[0] += A[k, c["TrialDog.Pelvis_vx"]] - v6[3]
    d.qvel[1] += A[k, c["TrialDog.Pelvis_vy"]] - v6[5]
    mujoco.mj_forward(m, d)


def body_err(m, d, M, L, k):
    """max position (cm) / angle (deg) / velocity (cm/s) error of the 22 seeded bodies vs the log row k."""
    A, c = L["A"], L["c"]
    ep, ea, ev = [], [], []
    v6 = np.zeros(6)
    for u in BMAP:
        g = M.gid[u]
        ep.append(100 * math.hypot(d.geom_xpos[g][0] - A[k, c["TrialDog.%s_x" % u]],
                                   d.geom_xpos[g][2] - 2.0 - A[k, c["TrialDog.%s_y" % u]]))
        da = (T.world_angle(d, g) - A[k, c["TrialDog.%s_rot" % u]] + 180) % 360 - 180
        ea.append(abs(da))
        mujoco.mj_objectVelocity(m, d, mujoco.mjtObj.mjOBJ_GEOM, g, v6, 0)
        ev.append(100 * math.hypot(v6[3] - A[k, c["TrialDog.%s_vx" % u]], v6[5] - A[k, c["TrialDog.%s_vy" % u]]))
    return max(ep), max(ea), max(ev)


def paw_order(L):
    """which paw slot k (paw<k>_px) belongs to which Unity paw body: nearest x while loaded."""
    names = ["FrontRightPaw", "FrontLeftPaw", "RearRightPaw", "RearLeftPaw"]
    out = {}
    for k in range(4):
        fn = col(L, "paw%d_fn" % k)
        px = col(L, "paw%d_px" % k)
        on = fn > 5.0
        e = {n: float(np.mean(np.abs(px[on] - col(L, n + "_x")[on]))) for n in names}
        out[k] = min(e, key=e.get), e
    return out


def apply_row(m, d, M, L, k, kf, paws, paw_slot):
    """generalised forces of row k (torques) and row kf (paw forces, R2 only)."""
    A, c = L["A"], L["c"]
    d.qfrc_applied[:] = 0.0
    d.xfrc_applied[:] = 0.0
    for u in JMAP:
        tau = A[k, c["TrialDog.%s_tauM" % u]]
        if paws.get("passive") and u in PASSIVE:
            d.qfrc_applied[M.v[u]] -= tau
            continue
        tau += A[k, c["TrialDog.%s_tauSp" % u]]
        if not paws.get("own_fric"):
            tau += A[k, c["TrialDog.%s_tauFr" % u]]
        d.qfrc_applied[M.v[u]] -= tau
    d.xfrc_applied[M.pel, 4] -= A[k, c["asTau"]]
    if paws.get("forces"):
        for slot, u in paw_slot.items():
            fn = A[kf, c["paw%d_fn" % slot]]
            if fn <= 0.0:
                continue
            F = np.array([A[kf, c["paw%d_ft" % slot]], 0.0, fn])
            P = np.array([A[kf, c["paw%d_px" % slot]], 0.0, A[kf, c["paw%d_py" % slot]] + 2.0])
            b = M.bid[u]
            d.xfrc_applied[b, :3] += F
            d.xfrc_applied[b, 3:] += np.cross(P - d.xipos[b], F)


def window(m, d, M, L, k0, n, variant, paw_slot, lag=1, passive=False):
    seed(m, d, M, L, k0, passive=passive)
    A, c = L["A"], L["c"]
    rec = {}
    paws = dict(forces=variant.startswith("R2"), own_fric=variant.endswith("f"), passive=passive)
    for i in range(n):
        k = k0 + i
        apply_row(m, d, M, L, k, min(k + lag, len(L["t"]) - 1), paws, paw_slot)
        mujoco.mj_step(m, d)
        te = (i + 1) * DT
        kk = k + 1
        for h in HZ:
            if h not in rec and te >= h - 1e-9:
                ej = {JMAP[u]: math.degrees(d.qpos[M.q[u]]) - A[kk, c["TrialDog.%s_ang" % u]] for u in JMAP}
                pe = (T.world_angle(d, M.gid["Chest"]) - A[kk, c["TrialDog.Chest_rot"]] + 180) % 360 - 180
                mujoco.mj_subtreeVel(m, d)
                mass = np.array([m.body_mass[M.bid[u]] for u in BMAP])
                cx_u = float(np.sum(mass * np.array([A[kk, c["TrialDog.%s_x" % u]] for u in BMAP])) / mass.sum())
                cx_t = float(np.sum(mass * np.array([d.geom_xpos[M.gid[u]][0] for u in BMAP])) / mass.sum())
                rec[h] = dict(per_joint=ej,
                              gate_rms=float(np.sqrt(np.mean([ej[j] ** 2 for j in GATE_J]))),
                              all_rms=float(np.sqrt(np.mean([v ** 2 for v in ej.values()]))),
                              pitch=float(pe), comx_cm=100 * (cx_t - cx_u),
                              chesty_cm=100 * (d.geom_xpos[M.gid["Chest"]][2] - 2.0 - A[kk, c["TrialDog.Chest_y"]]))
    return rec


def takes_found():
    return sorted(f[:-len("_twin2.csv")] for f in os.listdir(DIR) if f.endswith("_twin2.csv"))


def check(label):
    L = read2(label)
    m, d = model()
    M = Map(m, L)
    t = L["t"]
    out = {}
    # 1. jointSpeed sign: spd vs d(ang)/dt
    r = {}
    for u in JMAP:
        a = col(L, u + "_ang")
        s = col(L, u + "_spd")
        fd = np.gradient(a, t)
        ok = slice(10, len(t) - 10)
        r[u] = float(np.corrcoef(fd[ok], s[ok])[0, 1]) if np.std(s[ok]) > 0 else None
    out["spd_vs_dang_corr"] = r
    # 2. seeding consistency over the windows
    errs = [body_err(m, d, M, L, k) for k in range(200, len(t) - 101, 100) if (seed(m, d, M, L, k) or True)]
    out["seed_err_max"] = dict(pos_cm=max(e[0] for e in errs), ang_deg=max(e[1] for e in errs), vel_cms=max(e[2] for e in errs))
    out["seed_err_median"] = dict(pos_cm=float(np.median([e[0] for e in errs])), ang_deg=float(np.median([e[1] for e in errs])),
                                  vel_cms=float(np.median([e[2] for e in errs])))
    # 3. paw slot order
    po = paw_order(L)
    out["paw_order"] = {k: v[0] for k, v in po.items()}
    out["paw_order_err_m"] = {k: v[1] for k, v in po.items()}
    # 4. total vertical paw force vs weight, over the take
    fn = sum(col(L, "paw%d_fn" % k) for k in range(4))
    out["sum_fn_mean_N"] = float(np.mean(fn[200:]))
    out["mass_twin_kg"] = float(m.body_mass.sum())
    print(json.dumps(out, indent=1))
    return out


def run(labels, lag=1, integrator="Euler", variants=("R1", "R1f", "R2", "R2f"), out="replay541", t0=1.0, step=0.5, dur=0.5,
        passive=False):
    res = dict(meta=dict(lag=lag, integrator=integrator, dur=dur, step=step, passive=passive), takes={})
    n = int(round(dur / DT))
    for lab in labels:
        L = read2(lab)
        po = paw_order(L)
        paw_slot = {k: v[0] for k, v in po.items()}
        pair = 0.4 if "_u3_" in lab else 1.2649
        res["takes"][lab] = {"paw_slot": paw_slot, "pair": pair}
        tt = L["t"]
        starts = [int(round(ts / DT)) for ts in np.arange(t0, tt[-1] - dur - 0.02, step)]
        for var in variants:
            m, d = model(pair=pair, integrator=integrator, own_fric=var.endswith("f"), paw_contact=not var.startswith("R2"),
                         passive=passive)
            M = Map(m, L)
            W = []
            for k0 in starts:
                if k0 + n >= len(tt):
                    continue
                w = window(m, d, M, L, k0, n, var, paw_slot, lag=lag, passive=passive)
                W.append(dict(t_s=float(tt[k0]), **{"h%.2f" % h: v for h, v in w.items()}))
            s = {}
            for h in HZ:
                key = "h%.2f" % h
                v = [x[key] for x in W]
                s[key] = dict(gate_rms_med=float(np.median([x["gate_rms"] for x in v])),
                              all_rms_med=float(np.median([x["all_rms"] for x in v])),
                              pitch_med=float(np.median([abs(x["pitch"]) for x in v])),
                              comx_med_cm=float(np.median([abs(x["comx_cm"]) for x in v])),
                              chesty_med_cm=float(np.median([abs(x["chesty_cm"]) for x in v])),
                              per_joint_rms={j: float(np.sqrt(np.mean([x["per_joint"][j] ** 2 for x in v]))) for j in v[0]["per_joint"]})
            v = [x["h0.20"] for x in W]
            ok = [x["gate_rms"] <= 5 and abs(x["pitch"]) <= 2 and abs(x["comx_cm"]) <= 1.5 for x in v]
            s["windows"] = len(W)
            s["pass_frac_h0.20"] = float(np.mean(ok))
            s["PASS"] = bool(np.mean(ok) >= 0.8)
            res["takes"][lab][var] = dict(summary=s, windows=W)
            h = s["h0.20"]
            print("%-14s %-4s gate %.2f deg (all21 %.2f) pitch %.2f comx %.2f cm chestY %.2f cm pass %.0f %% | 0.5 s gate %.2f"
                  % (lab, var, h["gate_rms_med"], h["all_rms_med"], h["pitch_med"], h["comx_med_cm"], h["chesty_med_cm"],
                     100 * s["pass_frac_h0.20"], s["h0.50"]["gate_rms_med"]), flush=True)
    json.dump(res, open(os.path.join(HERE, out + ".json"), "w"), indent=1)
    return res


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["check", "run"])
    ap.add_argument("labels", nargs="*")
    ap.add_argument("--lag", type=int, default=1)
    ap.add_argument("--integrator", default="Euler")
    ap.add_argument("--variants", default="R1,R1f,R2,R2f")
    ap.add_argument("--out", default="replay541")
    ap.add_argument("--passive", type=int, default=1, help="tail chain + jaw on the twin's own passive law (see PASSIVE)")
    a = ap.parse_args()
    labs = a.labels or [l for l in takes_found() if "_u2_" in l or "_u3_" in l]
    if a.cmd == "check":
        check(labs[0])
    else:
        run(labs, lag=a.lag, integrator=a.integrator, variants=tuple(a.variants.split(",")), out=a.out, passive=bool(a.passive))


if __name__ == "__main__":
    main()
