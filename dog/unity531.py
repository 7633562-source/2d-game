"""unity531.py - read the req-531 Unity takes (cache/dog-twin-1024/unity531/) and run three checks on the twin.

usage:
  python unity531.py list                         # classify the files found, print column counts / time spans
  python unity531.py drop  [label ...]            # U1: Unity drop vs the twin drop -> u531_drop.json/.png
  python unity531.py gate  [label ...]            # U2/U3: torque-replay gate     -> u531_gate.json/.png
  python unity531.py slip  [label ...]            # U2/U3: slip calibration sweep -> u531_slip.json/.png
  python unity531.py all                          # list + drop + gate + slip on everything found
  options: --dir <folder> (default unity531/), --workers N (<= 4), --layout layout.json (column override)

Files: <label>.csv (main trial CSV, comma), <label>_twin.csv (twin probe, ';'), <label>.json (trial summary).
Twin probe row layout (DogStanceController.Turn.cs:590-650 + req-531):
  t; comX; comVx; chestPitch; pelvisTilt;
  10 joints x (angle; target; tauF; tauE; dirF; dirE), joints = FR shoulder, FL shoulder, RR hip, RL hip (trotUpJ,
     Trot.cs:273), FR elbow, FL elbow, RR knee, RL knee (trotLoJ), lumbar, neck;
  4 paws x (x-comX; mode; load; behind; xlim), paws = FR, FL, RR, RL (trotPawJ, Trot.cs:275);
  assist fx; fy; tau                                                   -> 88 columns (the req-450 probe)
  req-531 block, 4 paws x (angle; target; tauF; tauE; contactX; contactVx; onGround) -> +28 columns
  optional non-numeric 'phase' column (if the lane logs by trial time before the trot): detected and kept apart.
If the lane writes a real header row (names), the reader prints it; when the count is not 88 / 116 (+phase) the run
stops and asks for --layout (a JSON {"paw_block": ["angle", "target", ...], "paw_block_at": 88}).

Signs: Muscle torque logged as activation * maxTorque * direction (TwinMuscleTau); the generalised torque on the
twin hinge q (= Unity jointAngle) is -(tauF + tauE) (extensor direction -1 raises jointAngle; skeleton.md).
"""
import argparse
import csv
import glob
import json
import math
import os
import sys
from multiprocessing import Pool

import mujoco
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build_dog as B  # noqa: E402
import twin_dog as T  # noqa: E402

DT = B.DT
LEG8 = ["shoulder_r", "shoulder_l", "hip_r", "hip_l", "elbow_r", "elbow_l", "knee_r", "knee_l"]
J10 = LEG8 + ["lumbar", "neck"]
PAWS = ["fpaw_r", "fpaw_l", "rpaw_r", "rpaw_l"]
PAW_BLOCK = ["ang", "tgt", "tauF", "tauE", "cx", "cvx", "on"]
DEFAULT_DIR = os.path.join(HERE, "unity531")


# ------------------------------------------------------------------ reading
def read_main(path):
    """main trial CSV (comma). Rows whose field count differs from the header are skipped (rep-531: a pre-trot
    NullReferenceException glued rows 0.005-1.010 s into one 21862-field line; the twin CSV covers that window)."""
    rd = csv.reader(open(path, encoding="utf-8"))
    hdr = next(rd)
    rows = [r for r in rd if len(r) == len(hdr)]
    out = {"_skipped_rows": 0}
    for i, k in enumerate(hdr):
        try:
            out[k] = np.array([float(r[i]) for r in rows])
        except (ValueError, TypeError):
            pass
    return out


def read_twin(path, layout=None):
    lines = [ln.rstrip("\n") for ln in open(path, encoding="utf-8") if ln.strip()]
    head = [ln for ln in lines if ln.startswith("#")]
    body = [ln for ln in lines if not ln.startswith("#")]
    names = None
    if body and not _num(body[0].split(";")[0]):
        names = body[0].split(";")
        body = body[1:]
    toks = [ln.split(";") for ln in body]
    n = len(toks[0])
    # non-numeric columns (e.g. phase): find from the first rows
    text_cols = [j for j in range(n) if not all(_num(r[j]) or r[j].lower() == "nan" for r in toks[:50])]
    phase = None
    if text_cols:
        phase = np.array([r[text_cols[0]] for r in toks])
    num_idx = [j for j in range(n) if j not in text_cols]
    A = np.array([[_f(r[j]) for j in num_idx] for r in toks if len(r) == n])
    nn = A.shape[1]
    lay = dict(paw_block=PAW_BLOCK, paw_block_at=88)
    if layout:
        lay.update(layout)
    if nn == 118 and any("twinT" in h for h in head):
        return _twin_531(A, names)
    if nn not in (88, 88 + 4 * len(lay["paw_block"])):
        print("twin %s: %d numeric columns (+%d text); header lines:" % (os.path.basename(path), nn, len(text_cols)))
        for h in head:
            print("  ", h[:400])
        if names:
            print("  names:", names)
        raise SystemExit("unknown twin layout: pass --layout")
    D = {"t": A[:, 0], "comX": A[:, 1], "comVx": A[:, 2], "chestPitch": A[:, 3], "pelvisTilt": A[:, 4]}
    c = 5
    for j in J10:
        for k in ("ang", "tgt", "tauF", "tauE", "dirF", "dirE"):
            D[j + "_" + k] = A[:, c]
            c += 1
    for p in PAWS:
        for k in ("xrel", "mode", "load", "behind", "xlim"):
            D[p + "_" + k] = A[:, c]
            c += 1
    D["assist_fx"] = A[:, c]
    c += 3
    D["has_paws"] = nn > 88
    if D["has_paws"]:
        c = lay["paw_block_at"]
        for p in PAWS:
            for k in lay["paw_block"]:
                D[p + "_" + k] = A[:, c]
                c += 1
    D["phase"] = phase
    D["names"] = names
    return D


def _twin_531(A, names):
    """rep-531 probe: twinT(trial clock); phase(0 stand,1 walk-in,2 trot); trotT; comX; comVx; chestPitch; pelvisTilt;
    10 joints x 6; 4 paws x (x-comX; mode; load; behind; xlim); 4 paws x (pawAngle; pawTgt; pawTauF; pawTauE;
    pawX absolute; pawVx; pawGnd); assist fx; fy; tau  -> 118 columns, paw block at 87, assist at 115."""
    D = {"t": A[:, 0], "phase": A[:, 1], "trotT": A[:, 2], "comX": A[:, 3], "comVx": A[:, 4], "chestPitch": A[:, 5],
         "pelvisTilt": A[:, 6]}
    c = 7
    for j in J10:
        for k in ("ang", "tgt", "tauF", "tauE", "dirF", "dirE"):
            D[j + "_" + k] = A[:, c]
            c += 1
    for p in PAWS:
        for k in ("xrel", "mode", "load", "behind", "xlim"):
            D[p + "_" + k] = A[:, c]
            c += 1
    assert c == 87
    for p in PAWS:
        for k in PAW_BLOCK:
            D[p + "_" + k] = A[:, c]
            c += 1
    D["assist_fx"], D["assist_fy"], D["assist_tau"] = A[:, c], A[:, c + 1], A[:, c + 2]
    D["has_paws"] = True
    D["names"] = names
    D["layout"] = "rep-531"
    return D


def _num(s):
    try:
        float(s)
        return True
    except ValueError:
        return False


def _f(s):
    try:
        return float(s)
    except ValueError:
        return float("nan")


def discover(folder):
    import hashlib
    takes = {}
    seen = {}
    for p in sorted(glob.glob(os.path.join(folder, "*.csv"))):
        b = os.path.basename(p)
        if "_" in b and b.rsplit("_", 1)[1] in ("swing.csv", "stance.csv", "tgt.csv"):
            continue
        first = open(p, encoding="utf-8").readline()
        is_twin = first.startswith("#") or (first.count(";") > first.count(","))
        lab = b[:-9] if (is_twin and b.endswith("_twin.csv")) else b[:-4]
        takes.setdefault(lab, {})["twin" if is_twin else "main"] = p
    for lab in sorted(takes, key=lambda x: ("_u" not in x, x)):  # canonical = the u-label
        key = tuple(hashlib.md5(open(takes[lab][k], "rb").read()).hexdigest() for k in ("main", "twin") if k in takes[lab])
        if key in seen:
            takes[lab]["dup_of"] = seen[key]
        else:
            seen[key] = lab
    for lab, d in takes.items():
        j = os.path.join(folder, lab + ".json")
        d["json"] = j if os.path.exists(j) else None
        txt = (open(j, encoding="utf-8").read() if d["json"] else "") + " " + lab
        low = txt.lower()
        ll = lab.lower()
        if "_u1" in ll:  # the label names the kind first (req-531 labels cl531_u1/u2/u3_sNN)
            d["kind"] = "U1"
        elif "_u3" in ll:
            d["kind"] = "U3"
        elif "_u2" in ll or "_t1" in ll:
            d["kind"] = "U2"
        elif "starty" in low and "-1.23" in low:
            d["kind"] = "U1"
        elif "dogtrotclaw 0" in low or '"-dogtrotclaw", "0"' in low or "noclaw" in low:
            d["kind"] = "U3"
        elif "twin" in d:
            d["kind"] = "U2"
        else:
            d["kind"] = "?"
        if d.get("dup_of"):
            d["kind"] = "dup:" + d["dup_of"]
    return takes


def time_offset(main, tw):
    """offset such that main time = twin t + off (twin t may be trot time); fit on chestPitch."""
    tm, pm = main["t"], main["chestPitch"]
    best = (1e9, 0.0)
    for off in np.arange(-3.0, 3.0001, 0.005):
        tt = tw["t"] + off
        ok = (tt > tm[0]) & (tt < tm[-1])
        if ok.sum() < 50:
            continue
        e = np.sqrt(np.mean((np.interp(tt[ok], tm, pm) - tw["chestPitch"][ok]) ** 2))
        if e < best[0]:
            best = (e, float(off))
    return best[1], best[0]


# ------------------------------------------------------------------ twin state seeding
def seed_state(m, d, ids, tw, main, off, t, head_level=True):
    """place the twin at the Unity state of twin-time t (angles, pelvis tilt, CoM x, chest height, velocities)."""
    mujoco.mj_resetData(m, d)
    h = 0.005
    ang = {}
    vel = {}
    for j in J10:
        ang[j] = math.radians(np.interp(t, tw["t"], tw[j + "_ang"]))
        vel[j] = math.radians((np.interp(t + h, tw["t"], tw[j + "_ang"]) - np.interp(t - h, tw["t"], tw[j + "_ang"])) / (2 * h))
    if tw["has_paws"]:
        for p in PAWS:
            ang[p] = math.radians(np.interp(t, tw["t"], tw[p + "_ang"]))
            vel[p] = math.radians((np.interp(t + h, tw["t"], tw[p + "_ang"]) - np.interp(t - h, tw["t"], tw[p + "_ang"])) / (2 * h))
    for j, v in ang.items():
        d.qpos[ids.q[j]] = v
        d.qvel[ids.v[j]] = vel[j]
    th = math.radians(np.interp(t, tw["t"], tw["pelvisTilt"]))
    d.qpos[2] = th
    d.qvel[2] = math.radians((np.interp(t + h, tw["t"], tw["pelvisTilt"]) - np.interp(t - h, tw["t"], tw["pelvisTilt"])) / (2 * h))
    mujoco.mj_forward(m, d)
    if not tw["has_paws"]:
        for p in PAWS:
            d.qpos[ids.q[p]] = T.paw_flat_target(d, ids, p)
    if head_level:
        for jn, v in T.set_neck_targets(d, ids).items():
            if jn == "head":
                d.qpos[ids.q[jn]] = v
    mujoco.mj_forward(m, d)
    # CoM x and chest height
    d.qpos[0] += np.interp(t, tw["t"], tw["comX"]) - d.subtree_com[1][0]
    cy = np.interp(t + off, main["t"], main["chestY"])
    d.qpos[1] += (cy + 2.0) - d.geom_xpos[ids.chest_g][2]
    mujoco.mj_forward(m, d)
    # velocities of the root: CoM vx, chest vz
    mujoco.mj_subtreeVel(m, d)
    d.qvel[0] += np.interp(t, tw["t"], tw["comVx"]) - d.subtree_linvel[1][0]
    vz = (np.interp(t + off + h, main["t"], main["chestY"]) - np.interp(t + off - h, main["t"], main["chestY"])) / (2 * h)
    v6 = np.zeros(6)
    mujoco.mj_objectVelocity(m, d, mujoco.mjtObj.mjOBJ_GEOM, ids.chest_g, v6, 0)
    d.qvel[1] += vz - v6[5]
    mujoco.mj_forward(m, d)


def mode1_assist(d, ids, pel, tilt0, dead=4.0, cap=4.9):
    th = T.world_angle(d, ids.pelvis_g)
    err = th - tilt0
    tau = 0.0
    if abs(err) > dead:
        tau = float(np.clip(-(1.0 * err + 0.08 * math.degrees(d.qvel[2])), -cap, cap))
    d.xfrc_applied[pel, 4] = -tau
    return tau


def apply_assist_fx(m, d, tw, t):
    """BalanceAssist's horizontal force, split by mass (BalanceAssist.cs:500-506); logged as assist fx."""
    fx = float(np.interp(t, tw["t"], tw["assist_fx"]))
    tot = m.body_mass[1:].sum()
    d.xfrc_applied[1:, 0] = fx * m.body_mass[1:] / tot
    return fx


def pair_to_material(pair):
    return pair * pair / B.GROUND_MU


# ------------------------------------------------------------------ U1 drop
def drop(takes, out="u531_drop"):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    res = {}
    fig, ax = plt.subplots(1, 1, figsize=(10, 4.5))
    for lab, d in takes.items():
        if d.get("kind") != "U1" or "main" not in d:
            continue
        M = read_main(d["main"])
        t, y = M["t"], M["chestY"]
        gap = bool(((t > 0.01) & (t < 1.0)).sum() < 10)
        imp = None
        for k in ("paws", "frG"):
            if k in M:
                on = M[k] > 0.5 if k == "frG" else M[k] > 0
                if on.any() and not on[0]:
                    imp = float(t[np.argmax(on)])
                break
        res[lab] = dict(rows=int(len(t)), first_t=float(t[0]), drop_window_logged=not gap, impact_s=imp,
                        chestY_start=float(y[0]), chestY_min=float(y.min()), chestY_end=float(y[-1]),
                        pitch_end=float(M["chestPitch"][-1]),
                        landing_dip_min=float(y[t <= 0.6].min()), landing_dip_t=float(t[t <= 0.6][np.argmin(y[t <= 0.6])]),
                        chestY_at=dict((str(tq), float(np.interp(tq, t, y))) for tq in (0.1, 0.2, 0.3, 0.5, 1.0, 2.0)))
        ax.plot(t, y, lw=1, label="Unity " + lab)
    # twin: the same drop on the soft stand PD (sanity.py case 'drop', start 0.3 m above the spawn)
    m, dd, sy = T.load_model()
    ids = T.Ids(m)
    kp, kv = T.gains(0.15)
    T.to_servo(m, kp, kv, cap=T.ceilings())
    mujoco.mj_resetData(m, dd)
    dd.qpos[1] += 0.30
    mujoco.mj_forward(m, dd)
    L = []
    imp = None
    for k in range(int(5.0 / DT)):
        tgt = {jn: 0.0 for jn in B.JOINTS_ORDER}
        for p in PAWS:
            tgt[p] = T.paw_flat_target(dd, ids, p)
        tgt.update(T.set_neck_targets(dd, ids))
        for jn, v in tgt.items():
            a = mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_ACTUATOR, "m_" + jn)
            if a >= 0:
                dd.ctrl[a] = v
        mujoco.mj_step(m, dd)
        on, _ = T.contacts(m, dd, ids)
        if imp is None and any(on.values()):
            imp = (k + 1) * DT
        s = T.unity_state(dd, ids, sy)
        L.append(((k + 1) * DT, s["chestY"], s["chestPitch"]))
    L = np.array(L)
    w6 = L[:, 0] <= 0.6
    res["twin"] = dict(impact_s=imp, chestY_start=float(L[0, 1]), chestY_min=float(L[:, 1].min()),
                       chestY_end=float(L[-1, 1]), pitch_end=float(L[-1, 2]),
                       landing_dip_min=float(L[w6, 1].min()), landing_dip_t=float(L[w6, 0][np.argmin(L[w6, 1])]),
                       chestY_at=dict((str(tq), float(np.interp(tq, L[:, 0], L[:, 1]))) for tq in (0.1, 0.2, 0.3, 0.5, 1.0, 2.0)),
                       note="twin holds the stand on a soft PD (kp scale 0.15), not the game's stand/landing controller")
    ax.plot(L[:, 0], L[:, 1], "k--", lw=1.5, label="MuJoCo-двойник (мягкий ПД стойки)")
    ax.set_xlabel("время, с")
    ax.set_ylabel("высота центра груди, м (Unity y)")
    ax.set_title("Падение собаки с 0,3 м: Unity против двойника")
    ax.grid(alpha=0.3)
    ax.legend(fontsize=8)
    fig.tight_layout()
    fig.savefig(os.path.join(HERE, out + ".png"), dpi=100)
    json.dump(res, open(os.path.join(HERE, out + ".json"), "w"), indent=1)
    print(json.dumps(res, indent=1))
    return res


# ------------------------------------------------------------------ U2/U3 torque-replay gate
def torque_window(args):
    lab, twp, mainp, off, t_s, dur, paw_pair, layout = args
    tw = read_twin(twp, layout)
    main = read_main(mainp)
    m, d, sy = T.load_model(paw_mu=pair_to_material(paw_pair), integrator="RK4")
    ids = T.Ids(m)
    pel = ids.body("pelvis")
    for a in range(m.nu):  # muscles come from the log: motors off
        if mujoco.mj_id2name(m, mujoco.mjtObj.mjOBJ_ACTUATOR, a).startswith("m_"):
            m.actuator_gear[a, 0] = 0.0
    seed_state(m, d, ids, tw, main, off, t_s)
    tilt0 = float(np.interp(1.0, main["t"], main["pelvisTilt"])) if main["t"][0] < 1.0 else float(main["pelvisTilt"][0])
    logj = J10 + (PAWS if tw["has_paws"] else [])
    hz = [0.05, 0.1, 0.2, 0.3, 0.5]
    rec = {h: None for h in hz if h <= dur + 1e-9}
    n = int(round(dur / DT))
    for k in range(n):
        t = t_s + k * DT
        d.qfrc_applied[:] = 0.0
        for j in logj:
            tq = np.interp(t, tw["t"], tw[j + "_tauF"]) + np.interp(t, tw["t"], tw[j + "_tauE"])
            d.qfrc_applied[ids.v[j]] = -tq
        # unlogged: head held level (PD), tail0 PD to 0, paws (old probe) held flat by PD
        wh = math.radians(T.world_angle(d, ids.geom("head_g")))
        d.qfrc_applied[ids.v["head"]] = float(np.clip(10.0 * wh - 0.15 * d.qvel[ids.v["head"]], -8, 8))
        d.qfrc_applied[ids.v["tail0"]] = float(np.clip(-0.5 * d.qpos[ids.q["tail0"]], -3, 3))
        if not tw["has_paws"]:
            for p in PAWS:
                e = T.paw_flat_target(d, ids, p) - d.qpos[ids.q[p]]
                d.qfrc_applied[ids.v[p]] = float(np.clip(4.0 * e - 0.015 * d.qvel[ids.v[p]], -7, 17))
        apply_assist_fx(m, d, tw, t)
        mode1_assist(d, ids, pel, tilt0)
        mujoco.mj_step(m, d)
        te = (k + 1) * DT
        for h in rec:
            if rec[h] is None and te >= h - 1e-9:
                tt = t_s + te
                ej = [math.degrees(d.qpos[ids.q[j]]) - np.interp(tt, tw["t"], tw[j + "_ang"]) for j in logj]
                rec[h] = dict(per_joint_deg={j: float(e) for j, e in zip(logj, ej)},
                              joint_rms_deg=float(np.sqrt(np.mean(np.square(ej)))),
                              pitch_err_deg=float(T.world_angle(d, ids.chest_g) - np.interp(tt, tw["t"], tw["chestPitch"])),
                              pelvis_err_deg=float(T.world_angle(d, ids.pelvis_g) - np.interp(tt, tw["t"], tw["pelvisTilt"])),
                              comX_err_cm=float(100 * (d.subtree_com[1][0] - np.interp(tt, tw["t"], tw["comX"]))),
                              chestY_err_cm=float(100 * (d.geom_xpos[ids.chest_g][2] - 2.0 - np.interp(tt + off, main["t"], main["chestY"]))))
    return dict(label=lab, t_s=t_s, **{"h%.2f" % h: v for h, v in rec.items()})


def gate(takes, workers=4, layout=None, out="u531_gate", step=0.5, dur=0.5):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    jobs = []
    meta = {}
    for lab, d in takes.items():
        if d.get("kind") not in ("U2", "U3") or "twin" not in d or "main" not in d:
            continue
        tw = read_twin(d["twin"], layout)
        main = read_main(d["main"])
        off, e = time_offset(main, tw)
        pair = 1.2649 if d["kind"] == "U2" else 0.4  # claws engaged -> 4.0 material; claws off -> 0.4
        meta[lab] = dict(kind=d["kind"], offset_s=off, offset_fit_pitch_rms=e, paw_pair=pair, paws_logged=bool(tw["has_paws"]),
                         twin_t=[float(tw["t"][0]), float(tw["t"][-1])])
        for t_s in np.arange(max(tw["t"][0] + 0.05, 1.0), tw["t"][-1] - dur - 0.01, step):
            jobs.append((lab, d["twin"], d["main"], off, float(t_s), dur, pair, layout))
    if not jobs:
        print("gate: no U2/U3 take with a twin CSV")
        return None
    with Pool(min(workers, 4)) as pool:
        R = pool.map(torque_window, jobs)
    summ = {}
    for lab in meta:
        rr = [r for r in R if r["label"] == lab]
        s = {}
        for h in ("h0.05", "h0.10", "h0.20", "h0.30", "h0.50"):
            v = [r[h] for r in rr if r.get(h)]
            if v:
                s[h] = {k: float(np.median([abs(x[k]) for x in v])) for k in v[0] if k != "per_joint_deg"}
                s[h]["per_joint_rms_deg"] = {j: float(np.sqrt(np.mean([x["per_joint_deg"][j] ** 2 for x in v])))
                                             for j in v[0]["per_joint_deg"]}
        # proposed bar: at 0.2 s, joint RMS <= 5 deg, |pitch| <= 2 deg, |comX| <= 1.5 cm in >= 80 % of windows
        v = [r["h0.20"] for r in rr if r.get("h0.20")]
        ok = [x["joint_rms_deg"] <= 5 and abs(x["pitch_err_deg"]) <= 2 and abs(x["comX_err_cm"]) <= 1.5 for x in v]
        s["windows"] = len(v)
        s["pass_frac_h0.20"] = float(np.mean(ok)) if ok else 0.0
        s["PASS"] = bool(ok) and float(np.mean(ok)) >= 0.8
        summ[lab] = dict(meta[lab], **s)
    json.dump(dict(summary=summ, windows=R), open(os.path.join(HERE, out + ".json"), "w"), indent=1)
    fig, ax = plt.subplots(1, 3, figsize=(14, 4))
    for lab, s in summ.items():
        hs = [h for h in ("h0.05", "h0.10", "h0.20", "h0.30", "h0.50") if h in s]
        x = [float(h[1:]) for h in hs]
        ax[0].plot(x, [s[h]["joint_rms_deg"] for h in hs], "o-", label=lab)
        ax[1].plot(x, [s[h]["pitch_err_deg"] for h in hs], "o-", label=lab)
        ax[2].plot(x, [s[h]["comX_err_cm"] for h in hs], "o-", label=lab)
    for a, tt, bar in zip(ax, ["суставы: ск. ошибка, °", "наклон груди: ошибка, °", "центр масс x: ошибка, см"], [5, 2, 1.5]):
        a.set_title(tt)
        a.axhline(bar, ls="--", c="gray")
        a.set_xlabel("время после старта окна, с")
        a.grid(alpha=0.3)
        a.legend(fontsize=8)
    fig.suptitle("Повтор моментов мышц Unity в двойнике (медиана по окнам; пунктир - порог проверки на 0,2 с)")
    fig.tight_layout()
    fig.savefig(os.path.join(HERE, out + ".png"), dpi=100)
    print(json.dumps(summ, indent=1))
    return summ


# ------------------------------------------------------------------ U2/U3 slip calibration (angle replay incl. paws)
def angle_run(args):
    lab, twp, mainp, off, t0, t1, pair, tc, layout = args[:9]
    opts = args[9] if len(args) > 9 else {}
    tw = read_twin(twp, layout)
    if opts.get("flat"):  # day-1 condition: paw angles ignored, paws PD-held flat
        tw["has_paws"] = False
    main = read_main(mainp)
    bk = dict(paw_mu=pair_to_material(pair), solref="%g 1" % tc)
    for k in ("noslip", "impratio", "cone", "integrator"):
        if k in opts:
            bk[k] = opts[k]
    m, d, sy = T.load_model(**bk)
    jid = {j: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_JOINT, j) for j in J10 + PAWS}
    lo = {j: (float(m.jnt_range[i][0]) if m.jnt_limited[i] else -1e9) for j, i in jid.items()}  # compiled: radians
    hi = {j: (float(m.jnt_range[i][1]) if m.jnt_limited[i] else 1e9) for j, i in jid.items()}
    ids = T.Ids(m)
    pel = ids.body("pelvis")
    kp, kv = T.gains(3.0)
    T.to_servo(m, kp, kv)
    act = {jn: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_ACTUATOR, "m_" + jn) for jn in B.JOINTS_ORDER}
    seed_state(m, d, ids, tw, main, off, t0)
    tilt0 = float(np.interp(1.0, main["t"], main["pelvisTilt"])) if main["t"][0] < 1.0 else float(main["pelvisTilt"][0])
    logj = J10 + (PAWS if tw["has_paws"] else [])
    x0 = d.subtree_com[1][0]
    slip = {p: [] for p in PAWS}
    ankle = {p: [] for p in PAWS}  # Unity pawVx = rb.GetPointVelocity(paw-joint anchor).x (Trot.cs:370-376): the ankle
    flags = []
    jac_a = np.zeros((3, m.nv))
    trace = []
    unstable_t = None
    x_last, t_last = float(x0), t0
    jac = np.zeros((3, m.nv))
    n = int(round((t1 - t0) / DT))
    for k in range(n):
        t = t0 + (k + 1) * DT
        for j in logj:
            q_t = np.interp(t, tw["t"], tw[j + "_ang"])
            if opts.get("clip", True) and np.isfinite(q_t):  # Box2D soft limits overshoot by 2-3 deg: servo inside the range
                q_t = min(max(math.radians(q_t), lo[j]), hi[j])
            else:
                q_t = math.radians(q_t)
            d.ctrl[act[j]] = q_t
        if not tw["has_paws"]:
            for p in PAWS:
                d.ctrl[act[p]] = T.paw_flat_target(d, ids, p)
        for jn, v in T.set_neck_targets(d, ids).items():
            if jn == "head":
                d.ctrl[act[jn]] = v
        d.ctrl[act["tail0"]] = 0.0
        d.xfrc_applied[:] = 0.0
        apply_assist_fx(m, d, tw, t)
        mode1_assist(d, ids, pel, tilt0)
        mujoco.mj_step(m, d)
        if abs(d.time - (k + 1) * DT) > 1e-6 or not np.isfinite(d.qvel).all() or np.abs(d.qvel).max() > 500.0:  # MuJoCo auto-reset or blow-up
            unstable_t = t
            break
        x_last, t_last = float(d.subtree_com[1][0]), t
        if opts.get("trace") and k % 10 == 0:
            trace.append((round(t, 3), float(d.subtree_com[1][0] - x0), float(np.interp(t, tw["t"], tw["comX"]) - np.interp(t0, tw["t"], tw["comX"])),
                          float(np.abs(d.qvel).max()), float(T.world_angle(d, ids.chest_g)), float(np.interp(t, tw["t"], tw["chestPitch"]))))
        on = {p: False for p in PAWS}
        for i in range(d.ncon):
            c = d.contact[i]
            if c.dist > 0.0:
                continue
            g = c.geom1 if c.geom2 == ids.floor else c.geom2
            for p in PAWS:
                if ids.paw_g[p] == g:
                    on[p] = True
                    mujoco.mj_jac(m, d, jac, None, c.pos, ids.paw_b[p])
                    slip[p].append(abs(float(jac[0] @ d.qvel)))
        for p in PAWS:
            if on[p]:
                mujoco.mj_jac(m, d, jac_a, None, d.xpos[ids.paw_b[p]], ids.paw_b[p])
                ankle[p].append(abs(float(jac_a[0] @ d.qvel)))
        if tw["has_paws"]:
            flags.append(np.mean([on[p] == (np.interp(t, tw["t"], tw[p + "_on"]) > 0.5) for p in PAWS]))
    t1 = t_last if t_last > t0 + 0.5 else t1  # an unstable run is measured up to its last finite step
    m_speed = (x_last - x0) / max(t1 - t0, 1e-6)
    w = (tw["t"] >= t0) & (tw["t"] <= t1)
    u_speed = (np.interp(t1, tw["t"], tw["comX"]) - np.interp(t0, tw["t"], tw["comX"])) / (t1 - t0)
    u_slip = None
    if tw["has_paws"]:
        u_slip = float(np.mean([np.mean(np.abs(tw[p + "_cvx"][w][tw[p + "_on"][w] > 0.5])) if (tw[p + "_on"][w] > 0.5).any() else 0.0
                                for p in PAWS]))
    m_slip = float(np.mean([np.mean(v) if v else 0.0 for v in slip.values()]))
    m_ankle = float(np.mean([np.mean(v) if v else 0.0 for v in ankle.values()]))
    m_duty = float(np.mean([len(v) for v in ankle.values()])) / max(n, 1)
    u_duty = float(np.mean([(tw[p + "_on"][w] > 0.5).mean() for p in PAWS])) if tw["has_paws"] else None
    # u_contact_slip = Unity ankle |vx| while the paw is loaded; compare with m_ankle_vx (same point, same gate)
    return dict(label=lab, pair=pair, solref_tc=tc, opts={k: v for k, v in opts.items()}, unstable_t=unstable_t, t1=float(t1),
                u_speed=float(u_speed), m_speed=float(m_speed),
                speed_gap_pct=float(100 * (m_speed - u_speed) / max(abs(u_speed), 1e-6)),
                u_contact_slip=u_slip, m_ankle_vx=m_ankle, m_contact_slip=m_slip, u_duty=u_duty, m_duty=m_duty,
                flag_agree=float(np.mean(flags)) if flags else None, **({"trace": trace} if trace else {}))


def slip(takes, workers=4, layout=None, out="u531_slip", pairs=(0.25, 0.32, 0.4, 0.5, 0.63, 0.8, 1.0, 1.26),
         tcs=(0.01, 0.02, 0.04)):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    jobs = []
    for lab, d in takes.items():
        if d.get("kind") not in ("U2", "U3") or "twin" not in d or "main" not in d:
            continue
        tw = read_twin(d["twin"], layout)
        main = read_main(d["main"])
        off, _ = time_offset(main, tw)
        t0 = max(1.5, float(tw["t"][0]) + 0.05)
        t1 = float(tw["t"][-1]) - 0.05
        for pr in pairs:
            for tc in tcs:
                jobs.append((lab, d["twin"], d["main"], off, t0, t1, pr, tc, layout))
    if not jobs:
        print("slip: no U2/U3 take with a twin CSV")
        return None
    with Pool(min(workers, 4)) as pool:
        R = pool.map(angle_run, jobs)
    best = {}
    for lab in sorted(set(r["label"] for r in R)):
        rr = [r for r in R if r["label"] == lab]

        def cost(r):
            c = abs(r["speed_gap_pct"]) / 5.0
            if r["u_contact_slip"]:
                c += abs(r["m_ankle_vx"] - r["u_contact_slip"]) / max(r["u_contact_slip"], 0.02)
            return c
        b = min(rr, key=cost)
        best[lab] = dict(b, cost=cost(b), gap_below_5pct=abs(b["speed_gap_pct"]) < 5.0)
    json.dump(dict(best=best, runs=R), open(os.path.join(HERE, out + ".json"), "w"), indent=1)
    fig, ax = plt.subplots(1, 2, figsize=(13, 4.5))
    for lab in best:
        for tc in tcs:
            rr = sorted([r for r in R if r["label"] == lab and r["solref_tc"] == tc], key=lambda r: r["pair"])
            ax[0].plot([r["pair"] for r in rr], [r["speed_gap_pct"] for r in rr], "o-", label="%s, жёсткость %.2f с" % (lab, tc))
            ax[1].plot([r["pair"] for r in rr], [r["m_ankle_vx"] for r in rr], "o-", label="%s, %.2f с" % (lab, tc))
        if best[lab]["u_contact_slip"]:
            ax[1].axhline(best[lab]["u_contact_slip"], ls="--", c="k", lw=0.8)
    ax[0].axhspan(-5, 5, color="green", alpha=0.1)
    ax[0].set_title("разница скорости MuJoCo - Unity, % (зелёное: цель ±5 %)")
    ax[1].set_title("скорость голеностопа лапы на земле |vx|, м/с (пунктир: Unity)")
    for a in ax:
        a.set_xlabel("трение лапа-земля (парное)")
        a.grid(alpha=0.3)
        a.legend(fontsize=7)
    fig.suptitle("Калибровка проскальзывания лап двойника по дублям Unity (повтор углов, включая лапы)")
    fig.tight_layout()
    fig.savefig(os.path.join(HERE, out + ".png"), dpi=100)
    print(json.dumps(best, indent=1))
    return best


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["list", "drop", "gate", "slip", "all"])
    ap.add_argument("labels", nargs="*")
    ap.add_argument("--dir", default=DEFAULT_DIR)
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--layout", default="")
    ap.add_argument("--kind", default="", help="force the kind (U1/U2/U3) of the given labels")
    ap.add_argument("--quick", type=int, default=0, help="slip: 2 pairs x 1 stiffness (smoke test)")
    a = ap.parse_args()
    layout = json.load(open(a.layout)) if a.layout else None
    takes = discover(a.dir)
    if a.labels:
        takes = {k: v for k, v in takes.items() if k in a.labels}
    if a.kind:
        for v in takes.values():
            v["kind"] = a.kind
    if a.cmd in ("list", "all"):
        for lab, d in takes.items():
            info = dict(kind=d["kind"], main=bool(d.get("main")), twin=bool(d.get("twin")))
            if d.get("twin"):
                tw = read_twin(d["twin"], layout)
                info.update(twin_rows=len(tw["t"]), twin_t=[float(tw["t"][0]), float(tw["t"][-1])], paws=bool(tw["has_paws"]),
                            phase=tw["phase"] is not None)
            if d.get("main"):
                M = read_main(d["main"])
                info.update(main_rows=len(M["t"]), main_first_t=[float(x) for x in M["t"][:2]])
            print(lab, json.dumps(info))
    if a.cmd in ("drop", "all"):
        drop(takes)
    if a.cmd in ("gate", "all"):
        gate(takes, a.workers, layout)
    if a.cmd in ("slip", "all"):
        if a.quick:
            slip(takes, a.workers, layout, out="u531_slip_quick", pairs=(0.4, 1.26), tcs=(0.01,))
        else:
            slip(takes, a.workers, layout)


if __name__ == "__main__":
    main()
