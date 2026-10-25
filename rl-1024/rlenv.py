"""rlenv.py - gymnasium env for the rl-1024 planar human walk (day 2).

Spec (plan.md "Days 2-4"):
- physics 200 Hz (human2d.xml, final_kw: joint friction x0.8, solref 0.01 1), policy 50 Hz (frame skip 4);
- action 13 signals in [-1, 1]: lumbar, shoulder r/l, elbow r/l, hip r/l, knee r/l, ankle r/l, MTP r/l.
  Signal -> activation slewed 0.1 per physics step (Unity 20 /s); s >= 0 extensor ceiling, s < 0 flexor ceiling
  (ankle: +1 -> 65 N m plantarflexion, -1 -> 22 N m dorsiflexion). Neck, head, wrists: fixed PD to 0.
- observation 62 dims: 17 joint angles, 17 joint velocities, torso/pelvis world angle + angular velocity (4),
  pelvis height / vx / vz (3), CoM x minus grounded-sole midpoint (1), heel/toe contact flags (4), previous
  action (13), commanded speed (1), phase clock sin/cos (2). (Normalised outside, VecNormalize.)
- reward terms and weights: REWARD dict below (versioned, `rv`).
- termination: pelvis < 0.55 m above the ground, torso tilt > 60 deg, head/neck/torso touches the ground; 20 s cap.
- randomisation: start pose noise, joint friction scale 0.8 * U(0.8, 1.2) per episode.
"""
import math
import os

import gymnasium as gym
import mujoco
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
XML = os.path.join(HERE, "human2d.xml")
JN = ["lumbar", "neck", "head", "shoulder_r", "elbow_r", "wrist_r", "shoulder_l", "elbow_l", "wrist_l",
      "hip_r", "knee_r", "ankle_r", "mtp_r", "hip_l", "knee_l", "ankle_l", "mtp_l"]
ACT_J = ["lumbar", "shoulder_r", "elbow_r", "shoulder_l", "elbow_l", "hip_r", "knee_r", "ankle_r", "mtp_r",
         "hip_l", "knee_l", "ankle_l", "mtp_l"]
PD_J = ["neck", "head", "wrist_r", "wrist_l"]
GEAR = {"lumbar": 90, "neck": 15, "head": 15, "shoulder": 40, "elbow": 30, "wrist": 10, "hip": 100, "knee": 130,
        "ankle": 65, "mtp": 12}
FLOOR_Z = 0.02
DT = 0.005
FRAME_SKIP = 4
SLEW = 0.1

# reference gait (Fukuchi 2018 walking dataset, CC BY 4.0, via cache/refgait-1020/curves_walk.csv; same joint
# conventions as ours: hip minus = flexion, knee plus = flexion, ankle plus = plantarflexion). Interpolated to V_REF.
REF_CSV = os.path.join(HERE, "..", "refgait-1020", "curves_walk.csv")
V_REF = 1.1


def _load_ref(V=V_REF):
    import csv
    rows = list(csv.DictReader(open(REF_CSV, encoding="utf-8")))
    sp = sorted({float(r["speed"]) for r in rows})
    V = min(max(V, sp[0]), sp[-1])
    lo = max(x for x in sp if x <= V)
    hi = min(x for x in sp if x >= V)
    w = 0.0 if hi == lo else (V - lo) / (hi - lo)
    out = {}
    for j in ("hip", "knee", "ankle"):
        a = np.zeros(101)
        b = np.zeros(101)
        for r in rows:
            if r["joint"] != j:
                continue
            p = int(r["phase_pct"])
            if p > 100:
                continue
            if float(r["speed"]) == lo:
                a[p] = float(r["mean_deg"])
            if float(r["speed"]) == hi:
                b[p] = float(r["mean_deg"])
        out[j] = np.radians((1 - w) * a + w * b)[:100]  # periodic, drop the 100 % duplicate
    return out


try:
    REF = _load_ref()
    # v8: reference tables by speed (0.5 .. 1.3 m/s every 0.05) for the speed-dependent pose target
    REF_V = {round(0.5 + 0.05 * i, 2): _load_ref(0.5 + 0.05 * i) for i in range(17)}
except Exception:  # file missing: v4 unavailable
    REF = None
    REF_V = None
REF_T = 1.076  # stride time at 1.1 m/s (RefGaitTable WalkStrideTime interpolated)
REF_DUTY = 0.618


ARM_GAIN = 0.8


def ref_pose(ph, arm_gain=None, v=None):
    """reference joint angles (rad) at the right-leg phase ph in [0,1): dict joint -> angle.
    v (v8): use the reference gait at that speed (nearest 0.05 m/s in 0.5-1.3); None = the 1.1 m/s table."""
    REFT = REF if v is None else REF_V[round(min(max(round(v / 0.05) * 0.05, 0.5), 1.3), 2)]
    def f(arr, p):
        x = (p % 1.0) * 100.0
        i = int(x)
        t = x - i
        return (1 - t) * arr[i % 100] + t * arr[(i + 1) % 100]
    hipm = float(np.mean(REFT["hip"]))
    out = {}
    for side, off in (("r", 0.0), ("l", 0.5)):
        p = ph + off
        out["hip_" + side] = f(REFT["hip"], p)
        out["knee_" + side] = f(REFT["knee"], p)
        out["ankle_" + side] = f(REFT["ankle"], p)
        # arms: no arm data; same-side anti-phase to the hip, 0.8 x the hip swing (about +-18 deg), elbow -25 deg
        out["shoulder_" + side] = -(ARM_GAIN if arm_gain is None else arm_gain) * (out["hip_" + side] - hipm)
        out["elbow_" + side] = math.radians(-25.0)
    out["lumbar"] = 0.0
    return out


POSE_W5 = {"hip_r": 1.0, "hip_l": 1.0, "knee_r": 1.0, "knee_l": 1.0, "ankle_r": 0.5, "ankle_l": 0.5,
           "shoulder_r": 1.0, "shoulder_l": 1.0, "elbow_r": 0.5, "elbow_l": 0.5, "lumbar": 0.3}
POSE_WD = {"hip_r": 1.5, "hip_l": 1.5, "knee_r": 1.0, "knee_l": 1.0, "ankle_r": 0.5, "ankle_l": 0.5,
           "shoulder_r": 1.0, "shoulder_l": 1.0, "elbow_r": 0.3, "elbow_l": 0.3, "lumbar": 0.3}
POSE_W = {"hip_r": 1.0, "hip_l": 1.0, "knee_r": 1.0, "knee_l": 1.0, "ankle_r": 0.5, "ankle_l": 0.5,
          "shoulder_r": 0.5, "shoulder_l": 0.5, "elbow_r": 0.2, "elbow_l": 0.2, "lumbar": 0.3}

# reward versions; every change is logged in day2.md
REWARD = {}
REWARD.update({
    1: dict(w_speed=1.0, alive=0.2, w_up=0.3, w_full=0.1, w_heel=0.3, w_slip=0.3, w_energy=0.002, w_rate=0.02,
            w_sym=1.0, w_arm=0.2, period=1.0),
    # v2: v1 walk run sat in the standing optimum (speed term ~0 below 0.6 m/s): wider speed kernel + linear progress
    2: dict(w_speed=1.0, sig=0.5, w_prog=0.5, alive=0.2, w_up=0.3, w_full=0.05, w_heel=0.3, w_slip=0.3,
            w_energy=0.002, w_rate=0.02, w_sym=1.0, w_arm=0.2, period=1.0),
    # v3: v2 still stood/shuffled at 0.15 m/s; stronger progress, smaller alive/upright, plus a periodic
    # contact-schedule term on the phase clock (right stance phase 0-0.6, left 0.5-1.1: 10 % double support)
    3: dict(w_speed=1.0, sig=0.4, w_prog=1.0, alive=0.1, w_up=0.2, w_full=0.05, w_heel=0.3, w_slip=0.3,
            w_energy=0.002, w_rate=0.02, w_sym=1.0, w_arm=0.2, period=1.0, w_clock=0.15),
    # v4: v3 learned a one-leg-lead gallop (right foot always behind). Imitation term (plan fallback a):
    # track the real-human reference gait on the clock (DeepMimic pose reward), reference state init (RSI),
    # clock period = the reference stride time, contact schedule on the reference duty
    4: dict(w_speed=1.0, sig=0.4, w_prog=0.5, alive=0.1, w_up=0.2, w_full=0.05, w_heel=0.3, w_slip=0.3,
            w_energy=0.002, w_rate=0.02, w_sym=1.0, w_arm=0.2, period=REF_T, w_clock=0.1, w_pose=1.0, rsi=0.75),
    # v5 (day 3; replaces the untrained day-2 draft): v4strict passed G3 but goose-steps (left thigh to
    # horizontal), steps 0.4/0.9 m (75 % L/R diff), arms to shoulder height, slides. The v4 pose kernel
    # exp(-2 sum err^2) is flat (~0) for such errors, so: per-joint dense pose kernel, hard caps (hip flexion
    # > 35 deg, swing-foot lowest point > 0.15 m, |shoulder| > 35 deg), touchdown step-length target
    # (L* = v* T / 2), stronger L/R term, real contact-point slip.
    5: dict(w_speed=1.0, sig=0.4, w_prog=0.5, alive=0.1, w_up=0.2, w_full=0.05, w_heel=0.3, w_slip=1.0,
            w_energy=0.002, w_rate=0.02, w_sym=1.0, w_arm=0.2, period=REF_T, w_clock=0.1, w_pose=1.5, rsi=0.75,
            real_td=True, back_step=1.0, dense_pose=True, pose_sig=0.25, w_cap=2.0, hip_cap=35.0, clear_cap=0.15,
            sh_cap=35.0, w_td=4.0, td_sig=0.10, sym_k=3.0, cp_slip=True, arm_gain=1.2),
    # v6: owner on the v4strict film: hands and toes jitter. Wrist PD was underdamped (zeta 0.36) and slew-limited
    # (lag -> limit cycle). Wrists: spring 20 N m/rad without slew + implicit joint damping at critical (0.55).
    # MTP: taken from the policy, passive spring 12 N m/rad + critical damping 0.19 (policy MTP outputs ignored).
    # All actions: action-rate 0.02 -> 0.1 plus a 2nd-difference term 0.05. Slip weight 1 -> 3 (v5 kept 4.4 cm/step).
})
REWARD_V5 = REWARD[5]
REWARD[6] = dict(REWARD_V5, calm=True, w_rate=0.1, w_jerk=0.05, w_slip=3.0)
# v7: v6 walks clean (step diff ~1-4 %, arms +-18-22 deg anti-phase) but the feet chatter on the ground (heel/toe
# contact flags flicker, foot world-angle jitter ~7 deg) and 2/10 rest starts fall back. First-order low-pass on
# all policy actions (alpha 0.5 per 20 ms step, ~5.5 Hz), ankle action-rate weight x4, contact-flicker penalty,
# more rest starts (RSI 0.75 -> 0.5).
REWARD[7] = dict(REWARD[6], a_lp=0.5, ankle_rate=4.0, w_flick=0.05, rsi=0.5)
# v8 (day 4): speed-command PLANS like the game's stop battery (idle / walk / idle / walk ...), v_cmd in the obs is
# ramped at <= acc m/s^2 toward the plan target (start ramp 0 -> v, stop ramp v -> 0); clock tc held at 0 while
# v_cmd == 0; gait terms (pose, clock, arms) faded in over v_cmd 0.05-0.3; a stand term when the target is 0;
# pose target from the reference gait at the commanded speed (0.6 m/s cell); slip weight 3 -> 6.
REWARD[8] = dict(REWARD[7], plan=True, acc=0.5, rsi=0.2, w_slip=6.0, w_stand=1.5, stand_step=0.3, ref_v=True)
# v8b (rv 9): v8 at 64 M learned standing, ramp starts and stops (4/4) but limps (steps 0.56/0.41 m at 1.1,
# 0.38/0.20 at 0.6) and walks 0.92 instead of 1.1: symmetry weight 1 -> 2.5, speed kernel 0.4 -> 0.25, slip 6 -> 9.
REWARD[9] = dict(REWARD[8], w_sym=2.5, sig=0.25, w_slip=9.0)
REWARD[10] = dict(REWARD[9], sym_cap=5.0, plan_vs=[0.6, 0.6, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.1, 1.2])  # v8c: slow-heavy mix, sym penalty uncapped to 5
REWARD[11] = dict(REWARD[10], back_step=5.0, sym_all=True)  # v8d: close the short-step loophole (a <0.1 m step cost only -1 and switched the next sym check off)
REWARD[12] = dict(REWARD[11], spd=True)  # v8e: springs in the Unity SPD form (rep-529); obs/actions unchanged
REWARD[13] = dict(REWARD[12], head_pd=(20.0, 2.0, 15.0), wrist_rest_deg=-12.0, w_elbow=0.8)  # v8f: chest-head spring-damper K 20 N m/rad, C 2 N m s/rad, cap 15 N m (joint c 0.4 tested: worse, day5.md); relaxed wrists; flexed elbows
REWARD[14] = dict(REWARD[13], foot1=True)  # v9: one-segment foot twin variant (owner approved the merge 24.10); policy layout unchanged
# v8f2 (rv 15): the rv 13 elbow kernel (sigma 0.15 rad around -25 deg) gave no gradient at the straight-arm stop
# (exp(-8.6)): add a wide kernel (sigma 0.5). Start robustness (rep-530): 30 % of the standing starts from the
# game's own stand (day5/gamestand_rows.json + noise), 20 % from the reset pose perturbed +-15 deg per joint with
# small joint speeds.
REWARD[15] = dict(REWARD[13], elbow_wide=0.5, gs_frac=0.3, pert_frac=0.2, pert_deg=15.0, clip_fix=True)
REWARD[16] = dict(REWARD[15], foot1=True)  # v9 on the v8f2 env
# v8f4 (rv 17): the elbow is a light free hinge (forearm + hand 1.56 kg, I ~0.1 kg m2) driven only by a 30 N m muscle,
# so a -25 deg hold needs a policy signal of about -0.04 held to +-0.01 (v8f2: straight at the stop; a -0.3 shift:
# folded to -165). Passive elbow tone like the wrist springs: tau = -k (q - q0) - c qd, k 8 N m/rad, c 0.8 N m s/rad,
# q0 -18 deg (hangs at about -12 deg standing); the policy flexes more while walking on top of it.
REWARD[17] = dict(REWARD[15], elbow_spring=(8.0, 0.8, -18.0))
REWARD[18] = dict(REWARD[17], foot1=True)  # v9 on the v8f4 env
# v8f final (rv 19), after rep-533 and the owner's "head wobbles, arms hang paralysed": chest-head pair K 60 C 4;
# head-steadiness reward; arm imitation (each shoulder tracks -G x its own hip, G sized for +-22 deg at the reference
# hip swing of the commanded speed, so the arm swings with the opposite leg); elbows -30 deg walking (+0.3 x shoulder:
# more flexion forward), -12 deg standing; stronger standing-arm terms; rep-533 handover starts (the game's engage
# states + synthetic whole-body leans -10..+6 deg with feet flat and forward speed up to 0.35 m/s).
REWARD[19] = dict(REWARD[17], head_pd=(60.0, 4.0, 15.0), w_head=0.3, arm_imit=0.6, arm_amp_deg=22.0,
                  elbow_walk_deg=-30.0, elbow_slope=0.3, w_stand_arm=0.6, gs_frac=0.15, pert_frac=0.15, hand_frac=0.25)
REWARD[20] = dict(REWARD[19], foot1=True)  # v9 on the v8f final env
# v8f8 (rv 21), after the v8f7 judge (day5.md 4): head and arms on target, but (a) every judge fall is in the first
# 1.4 s of a standing start, (b) the step difference grew to 6-7 % at 1.1 m/s and 31-41 % at 0.6 (v8e 0.7 / 10 %): the
# per-touchdown symmetry penalty (about -4/s at a 30 % limp) is tiny next to the per-tick arm, elbow and head terms (up to
# ~140/s), (c) standing elbows stay on the 0-deg stop (command +0.4/+0.5, std 0.17: no sample unsticks them), (d) the
# cl533 engage states are lost by every policy at 0.44 s (they are the product of the Unity writer bug, day5.md 3b).
# So: no hand-over starts, symmetry 2.5 x 3 (free band 5 %) -> 6 x 4 (free band 2 %) per touchdown, and a standing penalty on the elbow commands
# themselves (the elbow spring alone hangs the forearm at about -12 deg).
REWARD[21] = dict(REWARD[19], hand_frac=0.0, gs_frac=0.2, w_sym=6.0, sym_k=4.0, sym_thr=0.02, back_step=2.1,
                  w_stand_el=0.5)  # back_step 2.1 x 6 = the old 5 x 2.5 per shuffle step; free band 5 -> 2 %
REWARD[22] = dict(REWARD[21], foot1=True)  # v9 on the v8f8 env
# v8f9 (rv 23), rep-536: on the owner's gentle 0.1 m/s^2 ramp v8e drifts backward (-0.15 m/s) and never walks; the
# training ramp was always 0.5 m/s^2. Per-episode ramp rate (start and stop): 0.5 (50 %), 0.25 (20 %), 0.1 (30 %).
REWARD[23] = dict(REWARD[21], acc_choices=([0.5, 0.25, 0.1], [0.5, 0.2, 0.3]))
REWARD[24] = dict(REWARD[23], foot1=True)  # v9 on the v8f9 env
# v8f10 (rv 25), after the v8f9 judge: 1.1 m/s fixed (step difference 0.6-1.1 %, stills 18/20) but 0.6 m/s became a
# step-to gait (right +0.52 m, left -0.12 m: the left foot lands behind) and drifted that way through every v8f8/v8f9
# checkpoint. Cause: rv 21 raised the per-touchdown asymmetry charge (up to 6 x 5 = 30) but kept the short-step charge
# at 2.1 x 6 = 12.6, so a short or backward step became the cheap way out. Short step now 5 x 6 = 30, the same as the
# largest asymmetry charge; standing elbow-command penalty 0.5 -> 1.5 (v8f9: one elbow bent at the stand, one straight).
# Trained from v8f7 (before the step-to basin).
REWARD[25] = dict(REWARD[23], back_step=5.0, w_stand_el=1.5)
REWARD[26] = dict(REWARD[25], foot1=True)  # v9 on the v8f10 env
_GS_ROWS = None


_HAND_ROWS = None


def handover_rows():
    global _HAND_ROWS
    if _HAND_ROWS is None:
        import json as _json
        with open(os.path.join(HERE, "day5", "handover_rows.json")) as f:
            _HAND_ROWS = _json.load(f)["rows"]
    return _HAND_ROWS


# reference hip mean and half-range by speed (rad), for the rv 19 arm imitation gain
ARM_TAB = {k: (float(np.mean(v["hip"])), float(0.5 * (np.max(v["hip"]) - np.min(v["hip"])))) for k, v in REF_V.items()} if REF_V else {}


def gamestand_rows():
    global _GS_ROWS
    if _GS_ROWS is None:
        import json as _json
        with open(os.path.join(HERE, "day5", "gamestand_rows.json")) as f:
            _GS_ROWS = _json.load(f)["rows"]
    return _GS_ROWS


class HumanWalk(gym.Env):
    metadata = {"render_modes": ["rgb_array"], "render_fps": 50}

    def __init__(self, v_target=1.1, rv=1, max_t=20.0, fric_rand=0.2, pose_noise=0.05, seed=None,
                 v_choices=None, render_mode=None, v0_max=0.0, rsi_on=True, strict=True, pel_min=0.75, dr=False, locked=False, rest_frac=0.0, dr_gear=0.1):
        self.foot1 = bool(REWARD[rv].get("foot1"))  # v9: one rigid foot segment (human2d_foot1.xml, build_model.py --foot1)
        xml = open(os.path.join(HERE, "human2d_foot1.xml") if self.foot1 else XML, encoding="utf-8").read()
        if locked:  # owner variants: locked=True welds wrists + MTP; locked="mtp" welds only the toe to the heel
            # (one rigid foot: same masses, same sole outline and contact boxes, ankle unchanged; 17 -> 16 hinges)
            eq = "".join('<joint joint1="%s" polycoef="0 0 0 0 0" solref="0.004 1"/>' % j
                         for j in (("mtp_r", "mtp_l") if locked == "mtp" else ("wrist_r", "wrist_l", "mtp_r", "mtp_l")))
            xml = xml.replace("  <actuator>", "  <equality>" + eq + "</equality>\n  <actuator>", 1)
        self.locked = locked
        self.m = mujoco.MjModel.from_xml_string(xml)
        self.d = mujoco.MjData(self.m)
        self.v_target = v_target
        self.v_choices = v_choices
        self.R = REWARD[rv]
        self.rv = rv
        self.max_steps = int(round(max_t / (DT * FRAME_SKIP)))
        self.fric_rand = fric_rand
        self.pose_noise = pose_noise
        self.acc_fixed = None  # eval: pin the command ramp rate (m/s^2)
        self.acc = REWARD[rv].get("acc", 0.5)
        self.render_mode = render_mode
        self.v0_max = v0_max
        self.rsi_on = rsi_on
        self.strict = strict
        self.pel_min = pel_min
        self.dr = dr
        self.rest_frac = rest_frac
        self.force_start = None  # v8f2 eval: force the standing-start kind ("game" / "pert" / "plain")
        self.fixed_plan = None  # v8 eval: list of (v_target m/s, duration s); first v > 0 = moving (RSI) start
        self.plan_vs = [0.6, 0.6, 0.8, 1.0, 1.1, 1.1, 1.2, 1.3]
        self.dr_gear = dr_gear
        m = self.m
        jid = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_JOINT, n)
        gid = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_GEOM, n)
        bid = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_BODY, n)
        aid = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_ACTUATOR, n)
        self.qa = np.array([m.jnt_qposadr[jid(j)] for j in JN])
        self.va = np.array([m.jnt_dofadr[jid(j)] for j in JN])
        self.mot = np.array([aid("m_" + j) for j in JN])
        self.act_idx = np.array([JN.index(j) for j in ACT_J])
        self.pd_idx = np.array([JN.index(j) for j in PD_J])
        self.fric_act = np.array([aid("f_" + j) for j in JN])
        self.fric_base = m.actuator_biasprm[self.fric_act, 2].copy()
        # ankle negative side 22/65
        self.neg_scale = np.ones(17)
        for j in ("ankle_r", "ankle_l"):
            self.neg_scale[JN.index(j)] = 22.0 / 65.0
        self.gmax = np.array([GEAR[j.split("_")[0]] for j in JN], dtype=float)
        self.g_heel = [gid("heel_r_g"), gid("heel_l_g")]
        self.g_toe = [gid("toe_r_g"), gid("toe_l_g")]
        self.g_bad = {gid("head_g"), gid("neck_g"), gid("torso_g")}
        self.g_floor = gid("floor")
        self.b_heel = [bid("heel_r"), bid("heel_l")]
        self.b_toe = [bid("toe_r"), bid("toe_l")]
        self.b_torso = bid("torso")
        self.b_pelvis = bid("pelvis")
        self.i_hip = [JN.index("hip_r"), JN.index("hip_l")]
        self.i_knee = [JN.index("knee_r"), JN.index("knee_l")]
        self.i_ankle = [JN.index("ankle_r"), JN.index("ankle_l")]
        self.i_sh = [JN.index("shoulder_r"), JN.index("shoulder_l")]
        self.i_el = [JN.index("elbow_r"), JN.index("elbow_l")]
        self.i_lumbar = JN.index("lumbar")
        self.observation_space = gym.spaces.Box(-np.inf, np.inf, shape=(62,), dtype=np.float32)
        self.action_space = gym.spaces.Box(-1.0, 1.0, shape=(13,), dtype=np.float32)
        self.rng = np.random.default_rng(seed)
        self.renderer = None
        self.q0 = self.d.qpos.copy()
        self.mass0 = m.body_mass.copy()
        self.inert0 = m.body_inertia.copy()
        self.gear0 = m.actuator_gear[:, 0].copy()
        self.g_sole = self.g_heel + self.g_toe
        self.fric_sole0 = m.geom_friction[self.g_sole, 0].copy()
        self.b_foot_of = {self.g_heel[0]: (0, self.b_heel[0]), self.g_toe[0]: (0, self.b_toe[0]),
                          self.g_heel[1]: (1, self.b_heel[1]), self.g_toe[1]: (1, self.b_toe[1])}
        self.jacp = np.zeros((3, m.nv))
        self.i_wr = [JN.index("wrist_r"), JN.index("wrist_l")]
        self.i_mtp = [JN.index("mtp_r"), JN.index("mtp_l")]
        self.i_jit = self.i_wr + self.i_mtp
        self.b_end = [mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_BODY, b) for b in ("hand_r", "hand_l", "toe_r", "toe_l")]
        self.calm = bool(self.R.get("calm"))
        if self.calm:
            for j in self.i_wr:
                m.dof_damping[self.va[j]] = 0.55
            for j in self.i_mtp:
                m.dof_damping[self.va[j]] = 0.19
            self.pd_idx = np.array([JN.index(j) for j in PD_J + ["mtp_r", "mtp_l"]])
            self.pd_kp = np.array([2.0, 2.0, 2.0, 2.0, 1.0, 1.0])  # signal per rad (x gear: 30/30/20/20/12/12 N m/rad)
            self.slew_vec = np.full(17, SLEW)
            self.slew_vec[self.pd_idx] = 1.0  # passive springs act at once (no activation lag)
        self.spd = bool(self.R.get("spd"))
        if self.spd:  # v8e: the springs exactly as Unity runs them after rep-529 (implicit/SPD form, game runtime Ieff)
            for j in self.i_wr + self.i_mtp:
                m.dof_damping[self.va[j]] = 0.0
            self.spd_k = np.array([30.0, 30.0, 20.0, 20.0, 12.0, 12.0])  # N m/rad (neck, head, wrist_r, wrist_l, mtp_r, mtp_l)
            self.spd_c = np.array([0.0, 0.0, 0.55, 0.55, 0.19, 0.19])  # N m s/rad
            self.spd_g = np.array([15.0, 15.0, 10.0, 10.0, 12.0, 12.0])  # gear
            self.spd_I = np.array([1.567e-3, 1.567e-3, 9.531e-4, 9.531e-4, 2.964e-4, 2.964e-4])  # rep-529 runtime Ieff
            nc = self.R.get("neck_c", 0.0)  # v8f: neck/head damping (owner: the head wobbles)
            self.spd_c[0] = self.spd_c[1] = nc
            wr0 = math.radians(self.R.get("wrist_rest_deg", 0.0))  # v8f: relaxed-hand rest angle (game sign, flexion < 0)
            self.spd_q0 = np.array([0.0, 0.0, wr0, wr0, 0.0, 0.0])
        self.head_pd = self.R.get("head_pd")
        self.el_spring = self.R.get("elbow_spring")  # rv 17: passive elbow tone (k, c, q0 deg), applied as a joint torque
        self.b_head = mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_BODY, "head")

    # ---------- helpers
    def _contacts(self):
        d = self.d
        heel = [False, False]
        toe = [False, False]
        bad = False
        for i in range(d.ncon):
            c = d.contact[i]
            if c.dist > 0.0:
                continue
            g = c.geom1 if c.geom2 == self.g_floor else c.geom2
            if g == self.g_heel[0]:
                heel[0] = True
            elif g == self.g_heel[1]:
                heel[1] = True
            elif g == self.g_toe[0]:
                toe[0] = True
            elif g == self.g_toe[1]:
                toe[1] = True
            elif g in self.g_bad or self.strict:
                bad = True  # strict: ANY non-foot geom on the floor (knee/shin kneeling, hands, pelvis)
        return heel, toe, bad

    def _foot_low(self, s):
        """lowest point of foot s (heel + toe boxes) above the floor, m."""
        m, d = self.m, self.d
        z = 9.0
        for g in (self.g_heel[s], self.g_toe[s]):
            hs = m.geom_size[g]
            R = d.geom_xmat[g].reshape(3, 3)
            for sx in (-1, 1):
                for sz in (-1, 1):
                    z = min(z, d.geom_xpos[g][2] + R[2, 0] * sx * hs[0] + R[2, 2] * sz * hs[2])
        return z - FLOOR_Z

    def _cp_slip(self):
        """per foot: mean |horizontal velocity| of the sole contact points (true sliding speed), m/s."""
        m, d = self.m, self.d
        acc = [[], []]
        for i in range(d.ncon):
            c = d.contact[i]
            if c.dist > 0.0:
                continue
            g = c.geom1 if c.geom2 == self.g_floor else c.geom2
            if g not in self.b_foot_of:
                continue
            s, b = self.b_foot_of[g]
            mujoco.mj_jac(m, d, self.jacp, None, c.pos, b)
            acc[s].append(abs(float(self.jacp[0] @ d.qvel)))
        return [float(np.mean(x)) if x else 0.0 for x in acc]

    def _torso_angle(self):
        # world angle of the torso, CCW + (= pelvis angle minus lumbar jointAngle)
        return self.d.qpos[2] - self.d.qpos[self.qa[self.i_lumbar]]

    def _make_plan(self):
        """v8 training plan, shapes from the game's stop battery (req468/battery108.json: i<idle>,w<walk>,i..,w..):
        start idle 0.3-2 s (or a moving start with prob R rsi), then walk 2-10 s / idle 1-9 s alternating;
        15 % of the walks are taps of 0.1-0.5 s. Walk speeds from plan_vs (or v_choices)."""
        rng = self.rng
        vs = self.v_choices or self.R.get("plan_vs") or self.plan_vs
        segs = []
        if self.rsi_on and rng.uniform() < self.R["rsi"]:
            segs.append((float(rng.choice(vs)), float(rng.uniform(2.0, 10.0))))
        else:
            segs.append((0.0, float(rng.uniform(0.3, 2.0))))
        T = segs[0][1]
        while T < self.max_steps * DT * FRAME_SKIP + 1.0:
            if segs[-1][0] > 0:
                segs.append((0.0, float(rng.uniform(1.0, 9.0))))
            elif rng.uniform() < 0.15:
                segs.append((float(rng.choice(vs)), float(rng.uniform(0.1, 0.5))))
            else:
                segs.append((float(rng.choice(vs)), float(rng.uniform(2.0, 10.0))))
            T += segs[-1][1]
        return segs

    def _obs(self):
        d = self.d
        q = d.qpos[self.qa]
        qd = d.qvel[self.va]
        if self.foot1:  # v9: no MTP hinge on the man; the layout slots are fed 0
            q = q.copy()
            qd = qd.copy()
            q[self.i_mtp] = 0.0
            qd[self.i_mtp] = 0.0
        heel, toe, _ = self.cont
        th_p = d.qpos[2]
        w_p = d.qvel[2]
        th_t = self._torso_angle()
        w_t = d.qvel[2] - d.qvel[self.va[self.i_lumbar]]
        comx = d.subtree_com[1][0]
        mid = self._mid()
        ph = 2 * math.pi * self.tc / self.R["period"]
        o = np.concatenate([q, qd * 0.1, [th_t, th_p, w_t * 0.1, w_p * 0.1],
                            [d.qpos[1] + 1.01 - FLOOR_Z, d.qvel[0], d.qvel[1]], [comx - mid],
                            [heel[0], toe[0], heel[1], toe[1]], self.prev_a, [self.v_cmd],
                            [math.sin(ph), math.cos(ph)]])
        return o.astype(np.float32)

    def _mid(self):
        """support midpoint x: mid of the grounded heel/toe body origins (all four if airborne)."""
        d = self.d
        heel, toe, _ = self.cont
        xs = []
        for s in range(2):
            if heel[s]:
                xs.append(d.xpos[self.b_heel[s]][0])
            if toe[s]:
                xs.append(d.xpos[self.b_toe[s]][0])
        if not xs:
            xs = [d.xpos[b][0] for b in self.b_heel + self.b_toe]
        return 0.5 * (min(xs) + max(xs))

    # ---------- gym API
    def reset(self, seed=None, options=None):
        if seed is not None:
            self.rng = np.random.default_rng(seed)
        m, d = self.m, self.d
        mujoco.mj_resetData(m, d)
        # friction randomisation (base already x0.8 in the xml)
        fs = 1.0 + self.rng.uniform(-self.fric_rand, self.fric_rand)
        m.actuator_biasprm[self.fric_act, 2] = self.fric_base * fs
        self.fric_scale = fs
        if self.dr:  # plan.md day-3 domain randomisation
            ms = self.rng.uniform(0.9, 1.1, m.nbody)
            m.body_mass[:] = self.mass0 * ms
            m.body_inertia[:] = self.inert0 * ms[:, None]
            m.actuator_gear[self.mot, 0] = self.gear0[self.mot] * self.rng.uniform(1 - self.dr_gear, 1 + self.dr_gear, 17)
            m.geom_friction[self.g_sole, 0] = self.rng.uniform(0.5, 0.9)  # pair friction = max(sole, floor 0.4)
            self.latency = int(self.rng.integers(0, 2))
            self.push_next = self.rng.uniform(1.0, 3.0)
        else:
            self.latency = 0
            self.push_next = 1e9
        self.push_left = 0
        self.push_f = 0.0
        self.d.xfrc_applied[:] = 0.0
        self.a_hold = np.zeros(13)
        self.a_f = np.zeros(13)
        self.rate_w = np.ones(13)
        if self.R.get("ankle_rate"):
            self.rate_w[[ACT_J.index("ankle_r"), ACT_J.index("ankle_l")]] = self.R["ankle_rate"]
        self.flags_prev = None
        # start pose noise: joints, root tilt
        n = self.pose_noise
        d.qpos[self.qa] += self.rng.uniform(-n, n, 17)
        lo = m.jnt_range[[mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_JOINT, j) for j in JN]]
        # NOTE (day 5): m.jnt_range is already in radians, so the x pi/180 below clipped the start noise to about
        # +-1-3 deg on every joint since day 2; kept for rv <= 14 (regression), fixed from rv 15 (clip_fix)
        sc = 1.0 if self.R.get("clip_fix") else math.pi / 180
        d.qpos[self.qa] = np.clip(d.qpos[self.qa], lo[:, 0] * sc, lo[:, 1] * sc)
        d.qpos[2] += self.rng.uniform(-0.03, 0.03)
        self.tc = 0.0
        self.rsi_start = False
        rest = self.rest_frac > 0 and self.rng.uniform() < self.rest_frac  # a true standing start (eval-like)
        vt = self.v_target
        if self.R.get("plan"):  # v8: the episode is a speed plan; first segment v > 0 = moving (RSI) start
            self.plan = list(self.fixed_plan) if self.fixed_plan is not None else self._make_plan()
            vt = self.plan[0][0]
            rest = vt <= 0
        self.start_kind = "plain"
        fk = self.force_start  # eval: "game" / "pert" / "plain" forces the standing-start kind
        if self.R.get("plan") and rest and (self.R.get("gs_frac") or fk):
            if fk is None:
                u = self.rng.uniform()
                g1 = self.R["gs_frac"]
                g2 = g1 + self.R["pert_frac"]
                g3 = g2 + self.R.get("hand_frac", 0.0)
                fk = "game" if u < g1 else "pert" if u < g2 else "hand" if u < g3 else "plain"
            if fk == "game":  # rep-530: the game's own stand (+ noise)
                rows = gamestand_rows()
                g = rows[int(self.rng.integers(len(rows)))]
                d.qpos[self.qa] = np.radians(g["q"]) + self.rng.uniform(-0.05, 0.05, 17)
                d.qvel[self.va] = np.radians(g["qd"]) + self.rng.normal(0.0, 0.1, 17)
                d.qpos[2] = math.radians(g["pelvis"]) + self.rng.uniform(-0.03, 0.03)
            elif fk == "pert":  # the reset pose perturbed +-pert_deg per joint, small joint speeds
                p = math.radians(self.R.get("pert_deg", 15.0))
                d.qpos[self.qa] += self.rng.uniform(-p, p, 17)
                d.qvel[self.va] = self.rng.uniform(-0.3, 0.3, 17)
            elif fk == "hand":  # rep-533: the game hands over from a forward lean that is already moving
                if self.rng.uniform() < 0.5:  # the game's own engage states (cl533_g8r) + noise
                    rows = handover_rows()
                    g = rows[int(self.rng.integers(len(rows)))]
                    d.qpos[self.qa] = np.radians(g["q"]) + self.rng.uniform(-0.05, 0.05, 17)
                    d.qvel[self.va] = np.radians(g["qd"]) + self.rng.normal(0.0, 0.1, 17)
                    d.qpos[2] = math.radians(g["pelvis"]) + self.rng.uniform(-0.03, 0.03)
                    self.hand_v = (g["vx"] + self.rng.uniform(-0.05, 0.05), math.radians(g["pelvis_rate"]) + self.rng.uniform(-0.1, 0.1))
                else:  # synthetic: whole-body lean with the feet flat, forward speed up to 0.35 m/s
                    th = self.rng.uniform(math.radians(-10.0), math.radians(6.0))
                    d.qpos[self.qa] += self.rng.uniform(-0.05, 0.05, 17)
                    d.qpos[2] = th
                    for s_ in range(2):  # foot world angle = pelvis - hip - knee - ankle = 0
                        d.qpos[self.qa[self.i_ankle[s_]]] = th - d.qpos[self.qa[self.i_hip[s_]]] - d.qpos[self.qa[self.i_knee[s_]]]
                    d.qvel[self.va] = self.rng.normal(0.0, 0.1, 17)
                    self.hand_v = (self.rng.uniform(0.0, 0.35) if th < 0 else self.rng.uniform(0.0, 0.1), self.rng.uniform(-0.3, 0.3))
            d.qpos[self.qa] = np.clip(d.qpos[self.qa], lo[:, 0], lo[:, 1])  # jnt_range is in radians
            if self.foot1:
                d.qpos[self.qa[self.i_mtp]] = 0.0
                d.qvel[self.va[self.i_mtp]] = 0.0
            self.start_kind = fk
        if self.R.get("plan") and not rest and self.rsi_on or (
                not self.R.get("plan") and self.R.get("rsi", 0.0) and self.rsi_on and not rest and self.rng.uniform() < self.R["rsi"]):
            ph0 = self.rng.uniform()
            T = self.R["period"]
            rp = ref_pose(ph0)
            rp2 = ref_pose(ph0 + 0.01)
            for j, val in rp.items():
                d.qpos[self.qa[JN.index(j)]] = val + self.rng.uniform(-0.03, 0.03)
                d.qvel[self.va[JN.index(j)]] = (rp2[j] - val) / (0.01 * T)
            d.qvel[0] = vt * self.rng.uniform(0.7, 1.05)
            self.tc = ph0 * T
            self.rsi_start = True
        mujoco.mj_forward(m, d)
        # put the lowest foot corner on the floor
        minz = 9.0
        for g in self.g_heel + self.g_toe:
            hs = m.geom_size[g]
            R = d.geom_xmat[g].reshape(3, 3)
            for sx in (-1, 1):
                for sz in (-1, 1):
                    minz = min(minz, (d.geom_xpos[g] + R @ np.array([sx * hs[0], 0, sz * hs[2]]))[2])
        d.qpos[1] += FLOOR_Z - minz
        if self.start_kind == "hand":
            d.qvel[0], d.qvel[2] = self.hand_v
        if self.v0_max > 0 and not self.rsi_start and not rest:  # optional initial forward speed (training only; eval starts from rest)
            d.qvel[0] = self.rng.uniform(0.0, self.v0_max)
        mujoco.mj_forward(m, d)
        self.act = np.zeros(17)
        self.prev_a = np.zeros(13)
        self.v_cmd = float(self.rng.choice(self.v_choices)) if self.v_choices else self.v_target
        if self.R.get("plan"):
            self.seg_i, self.seg_t = 0, 0.0
            ac = self.R.get("acc_choices")  # rv 23: per-episode ramp rate (rep-536: v8e never walks on a 0.1 m/s^2 ramp)
            self.acc = (self.acc_fixed if self.acc_fixed is not None else
                        float(self.rng.choice(ac[0], p=ac[1])) if ac and self.fixed_plan is None else self.R["acc"])  # judge plans keep 0.5
            self.v_tgt = self.v_cmd = max(0.0, vt)
        self.t = 0.0
        self.k = 0
        self.cont = self._contacts()
        self.was_down = [self.cont[0][s] or self.cont[1][s] for s in range(2)]
        self.air_k = [0, 0]
        self.last_td_x = [None, None]
        self.step_len = [None, None]
        self.sym_err = 0.0
        self.swing_max = [0.0, 0.0]
        self.prev_a2 = np.zeros(13)
        self.ep = dict(flick=0, jw=[], jq=[], jit_acc2=np.zeros(4), jit_n=0, jit_da2=np.zeros(13), clear=[[], []], hipflex=[[], []], slip_d=0.0, pushes=0, vlog=[], steps_t=[[], []], dist=0.0, slip=0.0, nsteps=[0, 0], steps=[[], []], sh=[[], []], hip=[[], []], tau2=0.0)
        return self._obs(), {}

    def step(self, a):
        m, d, R = self.m, self.d, self.R
        a = np.clip(np.asarray(a, dtype=float), -1, 1)
        if R.get("a_lp"):
            self.a_f = self.a_f + R["a_lp"] * (a - self.a_f)
            a = self.a_f
        a_in = a
        if self.latency:
            a, self.a_hold = self.a_hold, a_in
        target = np.zeros(17)
        target[self.act_idx] = a
        if self.dr:  # random pushes on the torso: up to +-30 N s as 150 N x 0.2 s, every 1-3 s
            if self.push_left <= 0 and self.t >= self.push_next:
                self.push_f = self.rng.uniform(-150.0, 150.0)
                self.push_left = 10
                self.push_next = self.t + 0.2 + self.rng.uniform(1.0, 3.0)
                self.ep["pushes"] += 1
            d.xfrc_applied[self.b_torso, 0] = self.push_f if self.push_left > 0 else 0.0
            self.push_left -= 1
        x0 = d.subtree_com[1][0]
        tau2 = 0.0
        slip = 0.0
        for _ in range(FRAME_SKIP):
            # fixed PD for neck/head/wrists (stable gains for the small inertias)
            qp = d.qpos[self.qa[self.pd_idx]]
            qv = d.qvel[self.va[self.pd_idx]]
            if self.spd:  # Unity RlPolicy.Spring: tau = (-k(q+qd dt) - c qd) / (1 + (c + k dt) dt / Ieff)
                tq = (-self.spd_k * (qp - self.spd_q0 + qv * DT) - self.spd_c * qv) / (1.0 + (self.spd_c + self.spd_k * DT) * DT / self.spd_I)
                target[self.pd_idx] = np.clip(tq / self.spd_g, -1, 1)
                self.act += np.clip(target - self.act, -self.slew_vec, self.slew_vec)
                if self.head_pd is not None:  # v8f: chest->head spring-damper (torque pair on chest and head, skips the light neck body)
                    K, C, cap = self.head_pd
                    th = float(np.clip(-K * (qp[0] + qp[1]) - C * (qv[0] + qv[1]), -cap, cap))
                    d.xfrc_applied[self.b_head, 4] = th
                    d.xfrc_applied[self.b_torso, 4] = -th
            elif self.calm:  # spring only; damping is the implicit joint damping
                target[self.pd_idx] = np.clip(-self.pd_kp * qp, -1, 1)
                self.act += np.clip(target - self.act, -self.slew_vec, self.slew_vec)
            else:
                target[self.pd_idx] = np.clip(-2.0 * qp - 0.02 * qv, -1, 1)
                self.act += np.clip(target - self.act, -SLEW, SLEW)
            d.ctrl[self.mot] = np.where(self.act < 0, self.act * self.neg_scale, self.act)
            if self.el_spring is not None:  # Unity: torque pair on lower arm / upper arm (same form as the chest-head pair)
                ke, ce, q0e = self.el_spring
                ie = self.va[self.i_el]
                d.qfrc_applied[ie] = -ke * (d.qpos[self.qa[self.i_el]] - math.radians(q0e)) - ce * d.qvel[ie]
            mujoco.mj_step(m, d)
            self.ep["jit_acc2"] += d.qacc[self.va[self.i_jit]] ** 2
            self.ep["jit_n"] += 1
            tau2 += float(np.sum((d.ctrl[self.mot[self.act_idx]]) ** 2))
        self.t += DT * FRAME_SKIP
        self.tc += DT * FRAME_SKIP
        self.k += 1
        heel, toe, bad = self.cont = self._contacts()
        vx = (d.subtree_com[1][0] - x0) / (DT * FRAME_SKIP)
        th_t = self._torso_angle()
        # --- reward
        r_speed = math.exp(-((vx - self.v_cmd) / R.get("sig", 0.25)) ** 2)
        r_prog = min(max(vx / max(self.v_cmd, 0.3), -1.0), 1.2) if self.v_cmd > 0.3 else 0.0
        plan = R.get("plan")
        g_lo = 0.05 if plan else 0.3  # v8: gait terms fade in over v_cmd 0.05-0.3 (v7: on/off at 0.3)
        gate = min(1.0, max(0.0, (self.v_cmd - 0.05) / 0.25)) if plan else 1.0
        n_td = 0
        r_up = math.exp(-(th_t / math.radians(15)) ** 2)
        r_full = 0.0
        r_heel = 0.0
        r_sym = 0.0
        r_td = 0.0
        cps = self._cp_slip()
        if R.get("cp_slip"):
            slip = sum(cps)
        for s in range(2):
            down = heel[s] or toe[s]
            if heel[s] and toe[s]:
                r_full += 1.0
            # sole slip while in contact
            for b, c in ((self.b_heel[s], heel[s]), (self.b_toe[s], toe[s])):
                if c and not R.get("cp_slip"):
                    v6 = np.zeros(6)
                    mujoco.mj_objectVelocity(m, d, mujoco.mjtObj.mjOBJ_BODY, b, v6, 0)
                    slip += abs(v6[3])
            real = down and not self.was_down[s] and self.air_k[s] >= 4  # airborne >= 0.08 s before
            if down and not self.was_down[s] and (real or not R.get("real_td")):  # touchdown
                if heel[s] and not toe[s]:
                    r_heel += 1.0
                xh = d.xpos[self.b_heel[s]][0]
                xo = d.xpos[self.b_heel[1 - s]][0]
                L = xh - xo
                prev_other = self.step_len[1 - s]
                self.step_len[s] = L
                if real:
                    n_td += 1
                if R.get("w_td") and real and (not plan or self.v_cmd > 0.3):
                    Lt = self.v_cmd * R["period"] / 2.0
                    r_td += math.exp(-((L - Lt) / R["td_sig"]) ** 2) - 0.5
                if R.get("real_td"):
                    if L < 0.1 and self.v_cmd > 0.3:
                        r_sym = -R.get("back_step", 1.0)  # foot lands behind / beside the other one (gallop, shuffle)
                    elif prev_other is not None and prev_other > (0.0 if R.get("sym_all") else 0.1):
                        diff = abs(L - prev_other) / (0.5 * (L + prev_other))
                        r_sym = -min(R.get("sym_cap", 2.0), R.get("sym_k", 2.0) * max(0.0, diff - R.get("sym_thr", 0.05)))
                elif prev_other is not None and L > 0.05 and prev_other > 0.05:
                    mean = 0.5 * (L + prev_other)
                    diff = abs(L - prev_other) / mean
                    r_sym = -max(0.0, diff - 0.05)
                elif L <= 0.05 and self.v_cmd > 0.3:
                    r_sym = -0.2  # a non-advancing step (shuffle) at walking command
            if not down:
                self.swing_max[s] = max(self.swing_max[s], self._foot_low(s))
            elif real:
                self.ep["clear"][s].append(self.swing_max[s])
                self.swing_max[s] = 0.0
            if real:  # measurement: only real steps (re-plant chatter excluded)
                self.ep["steps"][s].append(d.xpos[self.b_heel[s]][0] - d.xpos[self.b_heel[1 - s]][0])
                self.ep["steps_t"][s].append(self.t)
            self.air_k[s] = 0 if down else self.air_k[s] + 1
            self.was_down[s] = down
        r_pose = 0.0
        if R.get("w_pose", 0.0) and self.v_cmd > g_lo:
            rp = ref_pose((self.tc / R["period"]) % 1.0, R.get("arm_gain"), max(self.v_cmd, 0.5) if R.get("ref_v") else None)
            if R.get("dense_pose"):
                sg = R["pose_sig"]
                r_pose = sum(w * math.exp(-((d.qpos[self.qa[JN.index(j)]] - rp[j]) / sg) ** 2)
                             for j, w in POSE_WD.items()) / sum(POSE_WD.values())
            else:
                PW = POSE_W5 if R.get("real_td") else POSE_W
                e2 = sum(w * (d.qpos[self.qa[JN.index(j)]] - rp[j]) ** 2 for j, w in PW.items())
                r_pose = math.exp(-2.0 * e2)
        r_cap = 0.0
        if R.get("w_cap"):
            for s in range(2):
                hf = -d.qpos[self.qa[self.i_hip[s]]]  # hip flexion, rad
                r_cap -= max(0.0, hf - math.radians(R["hip_cap"])) * 2.0
                if not (heel[s] or toe[s]):
                    r_cap -= max(0.0, self._foot_low(s) - R["clear_cap"]) * 5.0
                r_cap -= max(0.0, abs(d.qpos[self.qa[self.i_sh[s]]]) - math.radians(R["sh_cap"])) * 2.0
        r_clock = 0.0
        if R.get("w_clock", 0.0) and self.v_cmd > g_lo:
            ph = (self.tc / R["period"]) % 1.0
            duty = REF_DUTY if R.get("w_pose") else 0.6
            want = [ph < duty, ph >= 0.5 or ph < duty - 0.5]
            for s in range(2):
                down = heel[s] or toe[s]
                r_clock += 1.0 if down == want[s] else -1.0
        legdiff = d.qpos[self.qa[self.i_hip[0]]] - d.qpos[self.qa[self.i_hip[1]]]
        armdiff = d.qpos[self.qa[self.i_sh[0]]] - d.qpos[self.qa[self.i_sh[1]]]
        r_arm = float(np.clip(-armdiff * legdiff / (0.35 * 0.5), -1, 1)) if self.v_cmd > g_lo else 0.0
        r_pose *= gate
        r_clock *= gate
        r_arm *= gate
        r_elbow = 0.0
        if R.get("w_elbow"):  # v8f (owner: straight arms look unnatural): elbow flexed, more on the forward swing
            for s in range(2):
                qsh = d.qpos[self.qa[self.i_sh[s]]]  # > 0 = arm back
                if R.get("elbow_walk_deg") is not None:  # rv 19: -30 deg, more flexion on the forward swing, -50..-15 deg
                    walk_ref = min(-0.26, max(-0.87, math.radians(R["elbow_walk_deg"]) + R["elbow_slope"] * qsh))
                else:
                    walk_ref = min(-0.17, max(-0.79, math.radians(-25.0) + 0.25 * qsh))  # -25 deg, -6 deg per 25 deg forward swing
                ref = gate * walk_ref + (1.0 - gate) * math.radians(-12.0)
                qe = d.qpos[self.qa[self.i_el[s]]]
                r_elbow += 0.5 * math.exp(-((qe - ref) / 0.15) ** 2)
                if R.get("elbow_wide"):
                    r_elbow += 0.5 * math.exp(-((qe - ref) / R["elbow_wide"]) ** 2)
                if plan and self.v_tgt <= 0.0:  # standing: arms hang (shoulders near 0)
                    wsa = R.get("w_stand_arm", 0.25)
                    r_elbow += wsa * (1.0 - gate) * (math.exp(-(qsh / 0.15) ** 2) - 1.0)
                    if R.get("elbow_wide"):  # rv 15: the narrow kernel is flat at a 25 deg standing arm; add a wide one
                        r_elbow += wsa * (1.0 - gate) * (math.exp(-(qsh / R["elbow_wide"]) ** 2) - 1.0)
                    if R.get("w_stand_el"):  # rv 21: standing, pull the elbow command itself toward 0 (gradient even on the stop)
                        r_elbow -= R["w_stand_el"] * (1.0 - gate) * float(a_in[ACT_J.index(("elbow_r", "elbow_l")[s])] ** 2)
        r_swing = 0.0
        if R.get("arm_imit"):  # rv 19: each shoulder tracks -G x (its own hip - reference mean hip)
            hm, hr = ARM_TAB[round(min(max(round(max(self.v_cmd, 0.5) / 0.05) * 0.05, 0.5), 1.3), 2)]
            G = math.radians(R["arm_amp_deg"]) / hr
            for s in range(2):
                e = d.qpos[self.qa[self.i_sh[s]]] + G * (d.qpos[self.qa[self.i_hip[s]]] - hm)
                r_swing += 0.5 * (math.exp(-(e / 0.15) ** 2) + 0.5 * math.exp(-(e / 0.4) ** 2))
            r_swing *= gate
        r_head = 0.0
        if R.get("w_head"):  # rv 19: head steadiness (neck and head are passive; the policy acts through the torso)
            r_head = 0.5 * (math.exp(-(d.qpos[self.qa[1]] / 0.1) ** 2) + math.exp(-(d.qpos[self.qa[2]] / 0.1) ** 2))
        r_stand = 0.0
        if plan and self.v_tgt <= 0.0:  # stopping / stopped: stand still, both feet down, CoM over the support
            both = float((heel[0] or toe[0]) and (heel[1] or toe[1]))
            r_stand = (1.0 - gate) * (0.5 * math.exp(-(vx / 0.1) ** 2) + 0.25 * both
                                      + 0.25 * math.exp(-((d.subtree_com[1][0] - self._mid()) / 0.05) ** 2))
            if self.v_cmd <= 0.0:
                r_stand -= R.get("stand_step", 0.0) * n_td
        e_rate = float(np.sum(self.rate_w * (a_in - self.prev_a) ** 2))
        flags = (heel[0], toe[0], heel[1], toe[1])
        n_flick = 0 if self.flags_prev is None else sum(1 for x, y in zip(flags, self.flags_prev) if x != y)
        self.flags_prev = flags
        self.ep["flick"] += n_flick
        e_jerk = float(np.sum((a_in - 2 * self.prev_a + self.prev_a2) ** 2))
        self.ep["jit_da2"] += (a_in - self.prev_a) ** 2
        rew = (R["w_speed"] * r_speed + R.get("w_prog", 0.0) * r_prog + R["alive"] + R["w_up"] * r_up + R["w_full"] * r_full + R["w_heel"] * r_heel
               - R["w_slip"] * slip - R["w_energy"] * tau2 / FRAME_SKIP - R["w_rate"] * e_rate - R.get("w_jerk", 0.0) * e_jerk - R.get("w_flick", 0.0) * n_flick
               + R["w_sym"] * r_sym + R["w_arm"] * r_arm + R.get("w_clock", 0.0) * r_clock
               + R.get("w_pose", 0.0) * r_pose + R.get("w_cap", 0.0) * r_cap + R.get("w_td", 0.0) * r_td + R.get("w_stand", 0.0) * r_stand + R.get("w_elbow", 0.0) * r_elbow
               + R.get("arm_imit", 0.0) * r_swing + R.get("w_head", 0.0) * r_head)
        self.prev_a2 = self.prev_a
        self.prev_a = a_in
        if plan:  # advance the speed plan; the ramped command is what the NEXT observation shows
            self.seg_t += DT * FRAME_SKIP
            while self.seg_t >= self.plan[self.seg_i][1] and self.seg_i < len(self.plan) - 1:
                self.seg_t -= self.plan[self.seg_i][1]
                self.seg_i += 1
            self.v_tgt = self.plan[self.seg_i][0]
            dv = self.acc * DT * FRAME_SKIP
            self.v_cmd = float(self.v_cmd + min(dv, max(-dv, self.v_tgt - self.v_cmd)))
            if self.v_cmd <= 1e-9:
                self.v_cmd = 0.0
                self.tc = 0.0  # clock held at 0 while the command is zero; restarts from 0
            self.ep["vlog"].append((self.t, self.v_cmd, self.v_tgt, vx, d.subtree_com[1][0], sum(cps)))
        self.ep["slip_d"] += sum(cps) * DT * FRAME_SKIP
        self.ep["jq"].append(d.qpos[self.qa[self.i_jit]].copy())
        self.ep["jw"].append([math.atan2(d.xmat[b][2], d.xmat[b][0]) for b in self.b_end])  # world angle of the end links
        for s in range(2):
            self.ep["hipflex"][s].append(-d.qpos[self.qa[self.i_hip[s]]])
        self.ep["dist"] += vx * DT * FRAME_SKIP
        self.ep["slip"] += slip
        for s in range(2):
            self.ep["sh"][s].append(d.qpos[self.qa[self.i_sh[s]]])
            self.ep["hip"][s].append(d.qpos[self.qa[self.i_hip[s]]])
        pel_h = d.qpos[1] + 1.01 - FLOOR_Z
        term = bool(pel_h < self.pel_min or abs(th_t) > math.radians(60) or bad or not np.isfinite(d.qpos).all())
        trunc = self.k >= self.max_steps
        info = {}
        if term or trunc:
            info["ep_t"] = self.t
            info["ep_dist"] = self.ep["dist"]
            info["fell"] = term
        return self._obs(), float(rew), term, trunc, info

    def render(self):
        if self.renderer is None:
            self.renderer = mujoco.Renderer(self.m, 480, 640)
            self.cam = mujoco.MjvCamera()
            self.cam.type = mujoco.mjtCamera.mjCAMERA_FREE
            self.cam.azimuth = 90
            self.cam.elevation = -5
            self.cam.distance = 3.5
        self.cam.lookat[:] = [self.d.subtree_com[1][0], 0, 0.9]
        self.renderer.update_scene(self.d, self.cam)
        return self.renderer.render()


def make_env(rank, **kw):
    def _f():
        import torch
        torch.set_num_threads(1)
        e = HumanWalk(seed=1000 + rank, **kw)
        return e
    return _f
