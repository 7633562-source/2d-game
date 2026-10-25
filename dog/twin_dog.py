"""twin_dog.py - dog2d model helpers + sanity checks + the log-replay match check (dog-twin-1024).

usage:
  python twin_dog.py sanity            -> sanity.json, sanity.png  (static PD stand 5 s, drop 0.3 m)
  python twin_dog.py replay <csv> ...  -> replay_<label>.json/.csv  (kinematic replay of a Unity take)
Replay = the logged joint angles (lumbar, hip, knee, shoulder, elbow; L/R) are tracked by stiff position servos
(implicit damping), paws held flat in the world, neck/head at world 0, tail passive; the root is free.
The trunk motion (chestY, chestPitch, pelvisTilt, comX) and the paw contact flags are then the twin's own answer
to the same limb motion and are compared with the Unity columns."""
import csv
import json
import math
import os
import sys

import mujoco
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build_dog as B  # noqa: E402

FLOOR = 0.02
RUNS = "E:/Games/2D Game/dog454/Trials/Runs"
CSV_MAP = {"lumbar": "lumbar", "hip_r": "hipR", "hip_l": "hipL", "knee_r": "kneeR", "knee_l": "kneeL",
           "shoulder_r": "shoR", "shoulder_l": "shoL", "elbow_r": "elbR", "elbow_l": "elbL"}
PAWS = ["fpaw_r", "fpaw_l", "rpaw_r", "rpaw_l"]
PAW_FLAG = {"fpaw_r": "frG", "fpaw_l": "flG", "rpaw_r": "rrG", "rpaw_l": "rlG"}


def load_model(**kw):
    xml, start_y = B.build(**kw)
    m = mujoco.MjModel.from_xml_string(xml)
    return m, mujoco.MjData(m), start_y


class Ids:
    def __init__(self, m):
        self.m = m
        j = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_JOINT, n)
        self.q = {n: m.jnt_qposadr[j(n)] for n in B.JOINTS_ORDER}
        self.v = {n: m.jnt_dofadr[j(n)] for n in B.JOINTS_ORDER}
        self.body = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_BODY, n)
        self.geom = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_GEOM, n)
        self.floor = self.geom("floor")
        self.paw_g = {p: self.geom(B.BODY_OF_J[p] + "_g") for p in PAWS}
        self.paw_b = {p: self.body(B.BODY_OF_J[p]) for p in PAWS}
        self.chest_g = self.geom("chest_g")
        self.pelvis_g = self.geom("pelvis_g")


def world_angle(d, gid):
    """CCW + world angle of a geom in the x-z view, deg (Unity rotation)."""
    R = d.geom_xmat[gid].reshape(3, 3)
    return math.degrees(math.atan2(R[2, 0], R[0, 0]))


def contacts(m, d, ids):
    on = {p: False for p in PAWS}
    other = False
    for i in range(d.ncon):
        c = d.contact[i]
        if c.dist > 0.001:
            continue
        g = c.geom1 if c.geom2 == ids.floor else c.geom2
        hit = [p for p, gg in ids.paw_g.items() if gg == g]
        if hit:
            on[hit[0]] = True
        else:
            other = True
    return on, other


def to_servo(m, kp, kv, cap=None, ids=None):
    """turn every motor into a position servo: force = kp (ctrl - q) - kv qdot (kv as implicit joint damping)."""
    for a in range(m.nu):
        name = mujoco.mj_id2name(m, mujoco.mjtObj.mjOBJ_ACTUATOR, a)
        if not name.startswith("m_"):
            continue
        jn = name[2:]
        k = kp.get(jn, 0.0) if isinstance(kp, dict) else kp
        m.actuator_gear[a, 0] = 1.0
        m.actuator_gainprm[a, 0] = k
        m.actuator_biastype[a] = mujoco.mjtBias.mjBIAS_AFFINE
        m.actuator_biasprm[a, :3] = [0.0, -k, 0.0]
        m.actuator_ctrllimited[a] = 0
        if cap is not None and jn in cap:
            m.actuator_forcelimited[a] = 1
            m.actuator_forcerange[a] = [-cap[jn], cap[jn]]
        dof = m.jnt_dofadr[m.actuator_trnid[a, 0]]
        m.dof_damping[dof] = kv.get(jn, 0.0) if isinstance(kv, dict) else kv


def ceilings():
    c = {}
    for jn in B.JOINTS_ORDER:
        j = B.joint_of(B.BODY_OF_J[jn])
        c[jn] = max(B.JOINT[j][4], B.JOINT[j][5])
    return c


def gains(scale=1.0):
    """servo kp per joint ~ critically damped at ~12 Hz for the distal inertia (kp in N m/rad)."""
    kp, kv = {}, {}
    for jn in B.JOINTS_ORDER:
        if jn.startswith("tail") and jn != "tail0":
            continue
        j = B.joint_of(B.BODY_OF_J[jn])
        base = {"lumbar": 600, "neck": 40, "head": 20, "tail": 0.5, "shoulder": 300, "elbow": 200, "hip": 300,
                "knee": 200, "paw": 20}[j]
        kp[jn] = base * scale
        kv[jn] = 2 * math.sqrt(base * scale * {"lumbar": 0.05, "neck": 0.003, "head": 0.002, "tail": 1e-4,
                                               "shoulder": 0.02, "elbow": 0.006, "hip": 0.03, "knee": 0.008,
                                               "paw": 0.0006}[j])
    return kp, kv


def paw_flat_target(d, ids, p):
    """q such that the paw lies flat in the world: q_paw grows with the paw turning CW."""
    return d.qpos[ids.q[p]] + math.radians(world_angle(d, ids.paw_g[p]))


def unity_state(d, ids, start_y):
    ch = d.geom_xpos[ids.chest_g]
    return dict(chestY=ch[2] - 2.0, chestPitch=world_angle(d, ids.chest_g), pelvisTilt=world_angle(d, ids.pelvis_g),
                pelvisY=d.geom_xpos[ids.pelvis_g][2] - 2.0, comX=d.subtree_com[1][0], comY=d.subtree_com[1][2] - 2.0)


def set_neck_targets(d, ids):
    """neck / head SPD hold world 0 (DefaultNeckPitch 0): target q = current q + world angle (CW +)."""
    t = {}
    for jn, b in (("neck", "neck"), ("head", "head")):
        t[jn] = d.qpos[ids.q[jn]] + math.radians(world_angle(d, ids.geom(b + "_g")))
    return t


# ------------------------------------------------------------------ sanity
def sanity(out_json="sanity.json"):
    res = {}
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    fig, axs = plt.subplots(1, 2, figsize=(12, 4.2))
    for case, lift in (("stand", 0.0), ("drop", 0.30)):
        m, d, sy = load_model()
        ids = Ids(m)
        kp, kv = gains(0.15)  # a soft PD inside the muscle ceilings
        to_servo(m, kp, kv, cap=ceilings())
        mujoco.mj_resetData(m, d)
        d.qpos[1] += lift
        mujoco.mj_forward(m, d)
        T = int(5.0 / B.DT)
        log = []
        t_imp = None
        satur = 0
        for k in range(T):
            for a in range(m.nu):
                pass
            tgt = {jn: 0.0 for jn in B.JOINTS_ORDER}
            for p in PAWS:
                tgt[p] = paw_flat_target(d, ids, p)
            tgt.update(set_neck_targets(d, ids))
            for jn, v in tgt.items():
                aid = mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_ACTUATOR, "m_" + jn)
                if aid >= 0:
                    d.ctrl[aid] = v
            mujoco.mj_step(m, d)
            on, other = contacts(m, d, ids)
            if t_imp is None and any(on.values()):
                t_imp = (k + 1) * B.DT
            mot = [a for a in range(m.nu) if mujoco.mj_id2name(m, mujoco.mjtObj.mjOBJ_ACTUATOR, a).startswith("m_")]
            satur += int(np.any(np.abs(d.actuator_force[mot]) >= m.actuator_forcerange[mot, 1] * 0.999))
            s = unity_state(d, ids, sy)
            log.append(((k + 1) * B.DT, s["chestY"], s["chestPitch"], s["pelvisTilt"], s["comX"], sum(on.values()), other))
        L = np.array(log)
        res[case] = dict(lift_m=lift, impact_s=t_imp, chestY_start=float(L[0, 1]), chestY_end=float(L[-1, 1]),
                         chestY_min=float(L[:, 1].min()), drop_m=float(L[0, 1] - L[:, 1].min()),
                         pitch_maxabs=float(np.abs(L[:, 2]).max()), pitch_end=float(L[-1, 2]),
                         comX_drift_m=float(L[-1, 4] - L[0, 4]), paws_down_end=int(L[-1, 5]),
                         body_touch=bool(L[:, 6].any()), servo_saturated_frac=satur / T,
                         stands=bool(L[-1, 5] == 4 and not L[:, 6].any() and abs(L[-1, 2] + 8) < 15))
        ax = axs[0] if case == "stand" else axs[1]
        ax.plot(L[:, 0], L[:, 1], label="грудь, y (м)")
        ax2 = ax.twinx()
        ax2.plot(L[:, 0], L[:, 2], color="tab:orange", label="наклон груди (°)")
        ax.set_xlabel("время, с")
        ax.set_ylabel("высота центра груди, м (Unity y)")
        ax2.set_ylabel("наклон груди, °")
        ax.set_title({"stand": "Стойка на ПД-удержании, 5 с", "drop": "Падение с 0.3 м на ПД-удержании"}[case])
        ax.legend(loc="lower left")
        ax2.legend(loc="lower right")
    fig.suptitle("Двойник собаки в MuJoCo: проверка статики и динамики")
    fig.tight_layout()
    fig.savefig(os.path.join(HERE, "sanity.png"), dpi=110)
    json.dump(res, open(os.path.join(HERE, out_json), "w"), indent=1)
    print(json.dumps(res, indent=1))


# ------------------------------------------------------------------ replay
def read_take(path, t_max=None):
    rows = []
    with open(path, encoding="utf-8") as f:
        r = csv.DictReader(f)
        for row in r:
            t = float(row["t"])
            if t_max and t > t_max:
                break
            rows.append(row)
    return rows


def replay(path, label, t_max=12.0, kp_scale=1.0, capped=False, skin=FLOOR, paw_mu=0.4, assist=None, jfric=1.0, bkw=None):
    rows = read_take(path, t_max)
    m, d, sy = load_model(skin=skin, paw_mu=paw_mu, jfric_scale=jfric, **(bkw or {}))
    ids = Ids(m)
    kp, kv = gains(kp_scale)
    to_servo(m, kp, kv, cap=ceilings() if capped else None)
    mujoco.mj_resetData(m, d)
    mujoco.mj_forward(m, d)
    act = {jn: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_ACTUATOR, "m_" + jn) for jn in B.JOINTS_ORDER}
    ts = np.array([float(r["t"]) for r in rows])
    ang = {jn: np.radians(np.array([float(r[c]) for r in rows])) for jn, c in CSV_MAP.items()}
    out = []
    pel = ids.body("pelvis")
    n = int(round(ts[-1] / B.DT))
    k_row = 0
    slip = 0.0
    v6 = np.zeros(6)
    for k in range(n):
        t = (k + 1) * B.DT
        tgt = {jn: float(np.interp(t, ts, ang[jn])) for jn in CSV_MAP}
        for p in PAWS:
            tgt[p] = paw_flat_target(d, ids, p)
        tgt.update(set_neck_targets(d, ids))
        tgt["tail0"] = 0.0
        for jn, v in tgt.items():
            if act[jn] >= 0:
                d.ctrl[act[jn]] = v
        d.xfrc_applied[:] = 0
        if assist:  # the game's mode-1 pelvis hand (DogStanceController.cs:7497-7508), err vs walk-entry tilt
            th = world_angle(d, ids.pelvis_g)
            err = th - assist["tilt0"]
            if abs(err) > assist["dead"]:
                w = math.degrees(d.qvel[2])
                tau = float(np.clip(-(1.0 * err + 0.08 * w), -assist["cap"], assist["cap"]))
                d.xfrc_applied[pel, 4] = -tau  # MuJoCo torque about +y is CW in the x-z view
        mujoco.mj_step(m, d)
        on_s, _ = contacts(m, d, ids)
        for p in PAWS:
            if on_s[p]:
                mujoco.mj_objectVelocity(m, d, mujoco.mjtObj.mjOBJ_BODY, ids.paw_b[p], v6, 0)
                slip += abs(v6[3]) * B.DT
        if k_row < len(rows) and abs(float(rows[k_row]["t"]) - t) < 0.0026:
            s = unity_state(d, ids, sy)
            on, other = contacts(m, d, ids)
            u = rows[k_row]
            out.append([t, float(u["chestY"]), s["chestY"], float(u["chestPitch"]), s["chestPitch"],
                        float(u["pelvisTilt"]), s["pelvisTilt"], float(u["comX"]), s["comX"],
                        sum(int(float(u[PAW_FLAG[p]]) > 0.5) for p in PAWS), sum(on.values()),
                        np.mean([int(float(u[PAW_FLAG[p]]) > 0.5) == int(on[p]) for p in PAWS]), int(other)]
                       + [math.degrees(d.qpos[ids.q[jn]]) - float(u[CSV_MAP[jn]]) for jn in ("hip_r", "knee_r", "shoulder_r", "elbow_r")] + [slip])
            k_row += 1
            while k_row < len(rows) and float(rows[k_row]["t"]) < t + 0.0024:
                k_row += 1
    A = np.array(out)
    cols = ["t", "u_chestY", "m_chestY", "u_pitch", "m_pitch", "u_pelvis", "m_pelvis", "u_comX", "m_comX",
            "u_paws", "m_paws", "flag_agree", "m_body_touch", "e_hip_r", "e_knee_r", "e_sh_r", "e_el_r", "m_slip_sum4"]
    with open(os.path.join(HERE, "replay_%s.csv" % label), "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(cols)
        for r in A:
            w.writerow(["%.5f" % x for x in r])

    def seg(t0, t1):
        s = (A[:, 0] >= t0) & (A[:, 0] < t1)
        if not s.any():
            return None
        B_ = A[s]
        u_v = (B_[-1, 7] - B_[0, 7]) / (B_[-1, 0] - B_[0, 0])
        m_v = (B_[-1, 8] - B_[0, 8]) / (B_[-1, 0] - B_[0, 0])
        return dict(t0=t0, t1=t1,
                    chestY_rms_cm=float(100 * np.sqrt(np.mean((B_[:, 1] - B_[:, 2]) ** 2))),
                    chestY_bias_cm=float(100 * np.mean(B_[:, 2] - B_[:, 1])),
                    chestY_rms_cm_debiased=float(100 * np.std(B_[:, 2] - B_[:, 1])),
                    pitch_rms_deg=float(np.sqrt(np.mean((B_[:, 3] - B_[:, 4]) ** 2))),
                    pelvis_rms_deg=float(np.sqrt(np.mean((B_[:, 5] - B_[:, 6]) ** 2))),
                    u_speed=float(u_v), m_speed=float(m_v),
                    comX_gap_end_m=float((B_[-1, 8] - B_[0, 8]) - (B_[-1, 7] - B_[0, 7])),
                    paw_flag_agree=float(B_[:, 11].mean()), m_body_touch_frac=float(B_[:, 12].mean()),
                    track_err_deg_rms=float(np.sqrt(np.mean(B_[:, 13:17] ** 2))),
                    m_slip_per_paw_mps=float((B_[-1, 17] - B_[0, 17]) / 4 / (B_[-1, 0] - B_[0, 0])))
    tw = float(A[-1, 0])
    res = dict(take=os.path.basename(path), label=label, kp_scale=kp_scale, capped=capped, skin=skin, paw_mu=paw_mu,
               assist=assist, bkw=bkw, walk=seg(1.5, tw), all=seg(1.0, tw))
    json.dump(res, open(os.path.join(HERE, "replay_%s.json" % label), "w"), indent=1)
    return res


if __name__ == "__main__":
    if sys.argv[1] == "sanity":
        sanity()
    elif sys.argv[1] == "replay":
        p = sys.argv[2]
        lab = sys.argv[3]
        kw = json.loads(sys.argv[4]) if len(sys.argv) > 4 else {}
        print(json.dumps(replay(os.path.join(RUNS, p) if not os.path.isabs(p) else p, lab, **kw), indent=1))
