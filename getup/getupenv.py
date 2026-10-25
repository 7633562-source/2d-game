"""getupenv.py - gymnasium env: the planar human (rl-1024 twin) gets up from lying on the back / belly.

Imports the twin (human2d.xml, constants) from cache/rl-1024 read-only; nothing there is modified.
Observation: the SAME 62-float family as the walk (rlenv._obs, frames/c525/obs-map.md) so the Unity RlPolicy
plumbing runs it: 17 q, 17 qd*0.1, torso/pelvis angle, torso/pelvis angvel*0.1, pelvis z/vx/vz, comx - support mid,
heel/toe flags, prev (filtered) action 13, v_cmd (= 0 here), sin/cos of the clock (period T_CLOCK = 16 s, so the
pair encodes the time since the get-up started over the 8 s episode).
Action: 13 signals as the walk (ACT_J), v7 conventions: low-pass alpha 0.5 per tick, 20 /s activation slew,
wrists/MTP passive springs, neck/head springs. MTP outputs ignored.
Reward (REWARD[rv], every version documented): height level + progress, head height, upright torso when high,
CoM over the support, stand-and-hold bonus; penalties energy, action rate, jerk, joint-limit hits, head on floor.
Termination: success = standing (pelvis >= 0.85 m, |torso|,|pelvis| < 25 deg, both feet down, nothing else on the
floor, calm) held 3 s -> bonus; else time cap. No fall termination (the body starts on the floor).
Assist (optional, off by default): horizontal PD on the pelvis toward the support midpoint, |F| <= cap BW,
drawn from a per-episode budget tank of `assist` BW x episode length (mean <= assist by construction), active only
when a foot is down and the pelvis is above 0.4 m. Logged as mean / peak in BW.
Resets: 'unity' (lying poses from the real fall takes, resets.json, clamped to the twin's joint range, L/R mirror,
noise, 0.4 s passive settle), 'proc' (random limb poses dropped supine / prone), 'mid' (curriculum: sit, all-fours,
squat, crouch; held by a PD during a short settle). Mix set by p_mid / p_proc (set_attr from the trainer).
"""
import json
import math
import os
import sys

import gymnasium as gym
import mujoco
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
RL = os.path.normpath(os.path.join(HERE, "..", "rl-1024"))
if RL not in sys.path:
    sys.path.insert(0, RL)
import rlenv  # noqa: E402  (read-only use of the twin constants and xml path)

JN, ACT_J, PD_J, GEAR = rlenv.JN, rlenv.ACT_J, rlenv.PD_J, rlenv.GEAR
FLOOR_Z, DT, FRAME_SKIP, SLEW = rlenv.FLOOR_Z, rlenv.DT, rlenv.FRAME_SKIP, rlenv.SLEW
XML = rlenv.XML
T_CLOCK = 16.0
BW = 70.84 * 9.81
RESETS = os.path.join(HERE, "resets.json")

REWARD = {
    # v1: dense level terms + hold bonus; see design.md section 3
    1: dict(w_h=1.0, w_head=0.5, w_prog=0.3, w_up=0.5, w_com=0.3, w_stand=2.0, bonus=30.0, w_e=0.001, w_rate=0.05,
            w_jerk=0.02, w_lim=0.05, w_headc=0.2, hold_s=3.0, stand_h=0.85, a_lp=0.5),
}
# v2 (after v1, 9 M steps / 33 min): v1 rose to a folded A-frame (hips at the -90 stop, trunk hanging forward, hands
# near the floor, pelvis ~0.78 m, 43 % of joints at a stop) from EVERY start, including the crouch, and never found the
# upright hold. Changes: the level term is gated by the trunk angle when high (a fold earns 30 % of it), head level
# weight up, joint-limit penalty x6, hands-on-floor penalty when the pelvis is above 0.6 m, head-on-floor penalty up,
# and the curriculum gets upright standing starts (the hold must be learned somewhere).
REWARD[2] = dict(REWARD[1], w_head=0.8, w_lim=0.3, w_hands=0.3, hands_h=0.6, w_headc=0.5, up_gate=0.3, w_still=0.3)
# v3 (after v2, 6 M steps): with v2 the policy LEAVES an upright start within 0.5 s and parks in a one-knee kneel
# (pelvis 0.53 m, shin on the floor, ~0.9/step, passively stable under the exploration noise); standing (4.6/step)
# was never held long enough under sigma 0.6 to be valued. Changes: (1) a fixed share of upright standing starts
# (trainer --p_stand) with FALL TERMINATION for those episodes (pelvis < 0.7 m ends it, -1): the walk pilot's
# signal; (2) a penalty for any non-sole/non-hand floor contact when the pelvis is above 0.45 m (the kneel);
# (3) the trainer starts with log_std -1.0 (sigma 0.37) so a stance survives the noise.
REWARD[3] = dict(REWARD[2], fall_term=True, fall_h=0.7, w_other=0.5, other_h=0.45)
# v4 (stage 3, 25.10, after v5/v5g: the ramp stands from the shallow poses, nothing pulls the feet under the pelvis):
# (1) feet-under-pelvis term, shaped: w_feet x gate(pel_h) / (1 + (dx / feet_w)^2), dx = mean foot x - pelvis x; the gate
#     is 1 below feet_h0, 0 above feet_h1 (no cliff), x feet_air when no sole touches the floor (a tuck in the air is not
#     feet under the pelvis). (2) keyframe imitation for the FLOOR starts (KF ladders below; coordinator's lever, a motion
#     prior, not a controller): per step w_imit(t) x best match to any keyframe ahead of the last rung reached, plus
#     imit_bonus once per rung advanced; a rung counts as reached at match >= imit_thr; w_imit is set by the trainer and
#     decays to 0 over the run. Match = 1 - sqrt(D) / imit_r, D = mean_j((q_j - q*_j)/imit_sq)^2 + ((tilt - tilt*)/
#     imit_st)^2 + ((h - h*)/imit_sh)^2, the better of the keyframe and its L/R mirror (a linear basin of radius
#     imit_r sigmas: a lying pose still sees ~0.2 toward the first rung). The ramp depth band (depth_lo, p_shallow) is an
#     env attribute, not a reward item.
REWARD[4] = dict(REWARD[3], w_feet=0.3, feet_w=0.2, feet_h0=0.5, feet_h1=0.8, feet_air=0.25, imit_bonus=1.0, imit_thr=0.55,
                 imit_sq=0.5, imit_st=0.6, imit_sh=0.2, imit_r=3.0)

ROUTE_OF = {"back": "back", "belly": "belly", "other": "other"}
MIRROR_PAIRS = (("shoulder_r", "shoulder_l"), ("elbow_r", "elbow_l"), ("wrist_r", "wrist_l"), ("hip_r", "hip_l"),
                ("knee_r", "knee_l"), ("ankle_r", "ankle_l"), ("mtp_r", "mtp_l"))
KF_JOINTS = ["lumbar", "shoulder_r", "elbow_r", "shoulder_l", "elbow_l", "hip_r", "knee_r", "ankle_r", "hip_l", "knee_l", "ankle_l"]


def _kf(name, hip, knee, ankle, sh, el, lumbar, tilt, hip2=None, knee2=None, ankle2=None, h=None):
    """a keyframe: joint targets (rad, twin conventions: hip < 0 = flexion, knee > 0 = flexion, shoulder < 0 = arm
    forward, tilt < 0 = pelvis pitched forward, prone = -pi/2, supine = +pi/2); hip2/knee2/ankle2 = the left leg when
    the pose is asymmetric (half-kneel); h = target pelvis height (m), else from posing the model (_build_kf)."""
    q = np.zeros(17)
    I = JN.index
    q[I("lumbar")] = lumbar
    for s in ("r", "l"):
        q[I("shoulder_" + s)], q[I("elbow_" + s)] = sh, el
    q[I("hip_r")], q[I("knee_r")], q[I("ankle_r")] = hip, knee, ankle
    q[I("hip_l")] = hip if hip2 is None else hip2
    q[I("knee_l")] = knee if knee2 is None else knee2
    q[I("ankle_l")] = ankle if ankle2 is None else ankle2
    return dict(name=name, q=q, tilt=tilt, h=h)


# Ladders built from the twin's geometry (probed 25.10: thigh angle from vertical = -hip + tilt, the shin folds back by
# the knee, the knee stops at 120 deg, so a feet-flat squat cannot put the pelvis below ~0.5 m and a floor sit (0.13 m)
# to a squat is a 0.35 m lift via the hands) and from data (the sit = the Unity floor-sit resets; the crab = v5's own
# pose from the back: face up, hands behind, pelvis 0.5 m, but with the heels pulled in from 0.83 m to ~0.3 m).
# Skipping rungs is free: "reached" is the highest rung matched, the reward pulls toward the best rung ahead.
# Back: tucked sit -> crab (feet pulled under) -> squat (trunk swung forward over the feet, hands free) -> crouch -> stand.
# Belly: push-up -> all-fours (knees under the pelvis) -> half-kneel (one foot forward, flat) -> squat -> crouch -> stand.
# 'other' (the floor sit after a backward fall) uses the back ladder.
# Heights are explicit targets (posing with "lowest point on the floor" is fragile when a pose is a little off); the
# hip joint sits ~0.08 m below the pelvis origin, the arm (0.6 m) is longer than thigh + offset, so all-fours needs
# bent elbows and a kneel rests on the shin with the toes pointed back (ankle +45 deg, the twin's limit).
KF_SQUAT = _kf("squat", -1.57, 2.0, -0.7, -1.0, -0.3, 0.2, -0.5, h=0.64)
KF_CROUCH = _kf("crouch", -1.0, 1.2, -0.5, -0.3, -0.3, 0.1, -0.3, h=0.84)
KF_STAND = _kf("stand", 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, h=0.98)
KF = {"back": [_kf("sit", -1.57, 1.3, 0.0, 0.1, -1.2, 0.3, 0.3, h=0.15),
               _kf("crab", -0.32, 2.05, -0.5, 1.57, 0.0, 0.3, 1.25, h=0.54),
               KF_SQUAT, KF_CROUCH, KF_STAND],
      "belly": [_kf("pushup", 0.0, 0.05, 0.6, -1.0, -0.2, -0.2, -1.15, h=0.40),
                _kf("fours", -1.5, 1.57, 0.78, -1.5, -0.9, 0.0, -1.5, h=0.55),
                _kf("halfkneel", -1.57, 1.15, -0.42, -0.3, -0.3, 0.2, 0.0, hip2=0.0, knee2=1.57, ankle2=0.78, h=0.62),
                KF_SQUAT, KF_CROUCH, KF_STAND]}
KF["other"] = KF["back"]


def _wide_xml(xml):
    """owner-flag variant: hip -130..45, knee 0..150, lumbar +-45 (the game's -humanHipMin/-humanKneeMax/-humanLumbarFwd)."""
    xml = xml.replace('range="-90 45"', 'range="-130 45"').replace('range="0 120"', 'range="0 150"')
    return xml.replace('name="lumbar" type="hinge" axis="0 1 0" range="-20 20"', 'name="lumbar" type="hinge" axis="0 1 0" range="-45 45"')


def _grip_xml(xml):
    """hand-ground grip (owner 25.10: the palms may hold on to the ground): one inactive connect constraint per palm to
    the world. The env sets the anchor at the palm contact point and activates it while the policy's grip channel is > 0."""
    eq = ('  <equality>\n    <connect name="grip_r" body1="hand_r" body2="world" anchor="0 0 -0.16" active="false"/>\n'
          '    <connect name="grip_l" body1="hand_l" body2="world" anchor="0 0 -0.16" active="false"/>\n  </equality>\n')
    return xml.replace("</mujoco>", eq + "</mujoco>")


class HumanGetUp(gym.Env):
    metadata = {"render_modes": ["rgb_array"], "render_fps": 50}

    def __init__(self, rv=1, max_t=10.0, seed=None, dr=False, dr_gear=0.2, assist=0.0, assist_cap=0.10, wide=False,
                 p_mid=0.3, p_proc=0.2, p_stand=0.0, routes=("back", "belly", "other"), route_w=(0.4, 0.4, 0.2), fixed_route=None,
                 settle_s=0.4, pose_noise=0.08, render_mode=None, grip=False, grip_cap_t=1.0, grip_cap_n=0.5, ramp=0.0,
                 depth_lo=0.0, p_shallow=0.0):
        xml = open(XML, encoding="utf-8").read()
        if wide:
            xml = _wide_xml(xml)
        if grip:
            xml = _grip_xml(xml)
        self.grip = bool(grip)
        self.grip_cap_t, self.grip_cap_n = grip_cap_t * BW, grip_cap_n * BW  # per hand, N
        self.wide = wide
        self.m = mujoco.MjModel.from_xml_string(xml)
        self.d = mujoco.MjData(self.m)
        self.R = REWARD[rv]
        self.rv = rv
        self.max_steps = int(round(max_t / (DT * FRAME_SKIP)))
        self.dr, self.dr_gear = dr, dr_gear
        self.assist, self.assist_cap = assist, assist_cap
        self.p_mid, self.p_proc, self.p_stand = p_mid, p_proc, p_stand
        self.ramp, self.depth = ramp, 1.0  # share of mid starts that are ramp poses; max depth stand->pose (trainer sets it)
        # stage 3 ramp band: depth_lo > 0 -> depth U(depth_lo, depth) except a p_shallow share at U(0, depth_lo)
        self.depth_lo, self.p_shallow = depth_lo, p_shallow
        self.w_imit = 0.0  # keyframe-imitation weight (trainer sets it, decays to 0); 0 = off
        self.kf_lad, self.kf_reached = None, -1
        self.routes, self.route_w = list(routes), np.array(route_w, dtype=float) / sum(route_w)
        self.fixed_route = fixed_route  # eval: 'back' / 'belly' / 'other' / 'mid' / 'proc_back' / 'proc_belly'
        self.settle_s = settle_s
        self.pose_noise = pose_noise
        self.render_mode = render_mode
        m = self.m
        jid = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_JOINT, n)
        gid = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_GEOM, n)
        bid = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_BODY, n)
        aid = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_ACTUATOR, n)
        self.qa = np.array([m.jnt_qposadr[jid(j)] for j in JN])
        self.va = np.array([m.jnt_dofadr[jid(j)] for j in JN])
        self.jrange = m.jnt_range[[jid(j) for j in JN]].copy()  # already radians (compiler converts)
        self.mot = np.array([aid("m_" + j) for j in JN])
        self.act_idx = np.array([JN.index(j) for j in ACT_J])
        self.fric_act = np.array([aid("f_" + j) for j in JN])
        self.fric_base = m.actuator_biasprm[self.fric_act, 2].copy()
        self.neg_scale = np.ones(17)
        for j in ("ankle_r", "ankle_l"):
            self.neg_scale[JN.index(j)] = 22.0 / 65.0
        self.g_heel = [gid("heel_r_g"), gid("heel_l_g")]
        self.g_toe = [gid("toe_r_g"), gid("toe_l_g")]
        self.g_sole = self.g_heel + self.g_toe
        self.g_hand = [gid("hand_r_g"), gid("hand_l_g")]
        self.g_knee = [gid("shin_r_g"), gid("shin_l_g"), gid("thigh_r_g"), gid("thigh_l_g")]
        self.g_head = gid("head_g")
        self.g_floor = gid("floor")
        self.b_heel = [bid("heel_r"), bid("heel_l")]
        self.b_toe = [bid("toe_r"), bid("toe_l")]
        self.b_torso, self.b_pelvis, self.b_head = bid("torso"), bid("pelvis"), bid("head")
        self.b_hand = [bid("hand_r"), bid("hand_l")]
        self.eq_grip = [mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_EQUALITY, n) for n in ("grip_r", "grip_l")] if grip else None
        self.grip_on, self.grip_lock, self.hand_pos = [False, False], [0.0, 0.0], [None, None]
        self.i_lumbar = JN.index("lumbar")
        self.i_wr = [JN.index("wrist_r"), JN.index("wrist_l")]
        self.i_mtp = [JN.index("mtp_r"), JN.index("mtp_l")]
        self.i_knee = [JN.index("knee_r"), JN.index("knee_l")]
        # v7 "calm" passive joints: wrist spring 20 N m/rad + damping 0.55, MTP spring 12 + damping 0.19, neck/head spring
        for j in self.i_wr:
            m.dof_damping[self.va[j]] = 0.55
        for j in self.i_mtp:
            m.dof_damping[self.va[j]] = 0.19
        self.pd_idx = np.array([JN.index(j) for j in PD_J + ["mtp_r", "mtp_l"]])
        self.pd_kp = np.array([2.0, 2.0, 2.0, 2.0, 1.0, 1.0])
        self.slew_vec = np.full(17, SLEW)
        self.slew_vec[self.pd_idx] = 1.0
        self.observation_space = gym.spaces.Box(-np.inf, np.inf, shape=(62,), dtype=np.float32)
        self.action_space = gym.spaces.Box(-1.0, 1.0, shape=(13,), dtype=np.float32)
        self.rng = np.random.default_rng(seed)
        self.renderer = None
        self.mass0 = m.body_mass.copy()
        self.inert0 = m.body_inertia.copy()
        self.gear0 = m.actuator_gear[:, 0].copy()
        self.fric_sole0 = m.geom_friction[self.g_sole, 0].copy()
        self.fric_hand0 = m.geom_friction[self.g_hand, 0].copy()
        self.body_geoms = [g for g in range(m.ngeom) if m.geom_bodyid[g] != 0]
        self.resets = self._load_resets()
        self.v_cmd = 0.0
        self._build_kf()

    # ---------- keyframe ladders (stage 3)
    def _build_kf(self):
        """per ladder: Q (K x 2 x 11: keyframe and its L/R mirror on KF_JOINTS), T (K tilts), H (K pelvis heights from
        posing the model with its lowest point on the floor, so h* follows the twin's geometry)."""
        self.kf_sel = np.array([JN.index(j) for j in KF_JOINTS])
        self.kf_arr = {}
        for route in ("back", "belly"):
            Q, T, H = [], [], []
            for kf in KF[route]:
                self._set_pose(kf["q"], kf["tilt"])
                H.append(kf["h"] if kf.get("h") is not None else self.d.qpos[1] + 1.01 - FLOOR_Z)
                qm = kf["q"].copy()
                for a_, b_ in MIRROR_PAIRS:
                    qm[JN.index(a_)], qm[JN.index(b_)] = kf["q"][JN.index(b_)], kf["q"][JN.index(a_)]
                Q.append(np.stack([kf["q"][self.kf_sel], qm[self.kf_sel]]))
                T.append(kf["tilt"])
            self.kf_arr[route] = (np.array(Q), np.array(T), np.array(H))
        self.kf_arr["other"] = self.kf_arr["back"]
        mujoco.mj_resetData(self.m, self.d)

    def _kf_match(self):
        """match in [0, 1] of the current pose to every keyframe of the episode's ladder: a linear basin
        1 - sqrt(D) / imit_r, D = mean_j((q_j - q*_j)/sq)^2 + ((tilt - tilt*)/st)^2 + ((h - h*)/sh)^2 (the better of the
        keyframe and its mirror), so a lying pose still sees a gradient toward the first rungs (a Gaussian gave ~0)."""
        R = self.R
        Q, T, H = self.kf_arr[self.kf_lad]
        q = self.d.qpos[self.qa][self.kf_sel]
        dq = np.mean(((Q - q) / R.get("imit_sq", 0.5)) ** 2, axis=2).min(axis=1)
        dt = ((T - self.d.qpos[2]) / R.get("imit_st", 0.6)) ** 2
        dh = ((H - (self.d.qpos[1] + 1.01 - FLOOR_Z)) / R.get("imit_sh", 0.2)) ** 2
        return np.maximum(0.0, 1.0 - np.sqrt(dq + dt + dh) / R.get("imit_r", 3.0))

    # ---------- resets
    def _load_resets(self):
        out = {"back": [], "belly": [], "other": []}
        if os.path.exists(RESETS):
            for r in json.load(open(RESETS, encoding="utf-8")):
                out[r["route"]].append(r)
        return out

    def _lowest(self):
        m, d = self.m, self.d
        z = 9.0
        for g in self.body_geoms:
            hs = m.geom_size[g]
            R = d.geom_xmat[g].reshape(3, 3)
            for sx in (-1, 1):
                for sz in (-1, 1):
                    z = min(z, d.geom_xpos[g][2] + R[2, 0] * sx * hs[0] + R[2, 2] * sz * hs[2])
        return z

    def _set_pose(self, q, tilt, z=None, mirror=False):
        m, d = self.m, self.d
        q = np.array(q, dtype=float)
        if mirror:  # swap r/l (same plane: a legal relabelling)
            qm = q.copy()
            for a_, b_ in (("shoulder_r", "shoulder_l"), ("elbow_r", "elbow_l"), ("wrist_r", "wrist_l"), ("hip_r", "hip_l"),
                           ("knee_r", "knee_l"), ("ankle_r", "ankle_l"), ("mtp_r", "mtp_l")):
                qm[JN.index(a_)], qm[JN.index(b_)] = q[JN.index(b_)], q[JN.index(a_)]
            q = qm
        q = np.clip(q, self.jrange[:, 0] + 0.01, self.jrange[:, 1] - 0.01)
        d.qpos[:] = 0.0
        d.qvel[:] = 0.0
        d.qpos[self.qa] = q
        d.qpos[2] = tilt
        d.qpos[1] = (z - 1.01) if z is not None else 0.0
        mujoco.mj_forward(m, d)
        low = self._lowest()
        if z is None or low < FLOOR_Z + 0.005:
            d.qpos[1] += FLOOR_Z + 0.01 - low
        mujoco.mj_forward(m, d)

    def _settle(self, seconds, hold_q=None, kp=3.0):
        """passive settle (zero muscle, springs on) or a PD hold toward hold_q."""
        m, d = self.m, self.d
        n = int(round(seconds / DT))
        for _ in range(n):
            tgt = np.zeros(17)
            qp = d.qpos[self.qa]
            if hold_q is not None:
                tgt = np.clip(kp * (hold_q - qp), -1, 1)
            tgt[self.pd_idx] = np.clip(-self.pd_kp * d.qpos[self.qa[self.pd_idx]], -1, 1)
            d.ctrl[self.mot] = np.where(tgt < 0, tgt * self.neg_scale, tgt)
            mujoco.mj_step(m, d)
        d.ctrl[:] = 0.0

    def _proc_lying(self, route):
        rng = self.rng
        q = np.zeros(17)
        for j in ("shoulder_r", "shoulder_l"):
            q[JN.index(j)] = rng.uniform(-1.2, 1.0)
        for j in ("elbow_r", "elbow_l"):
            q[JN.index(j)] = rng.uniform(-1.6, 0.0)
        for j in ("hip_r", "hip_l"):
            q[JN.index(j)] = rng.uniform(-1.2, 0.5)
        for j in ("knee_r", "knee_l"):
            q[JN.index(j)] = rng.uniform(0.0, 1.6)
        for j in ("ankle_r", "ankle_l"):
            q[JN.index(j)] = rng.uniform(-0.5, 0.5)
        q[JN.index("lumbar")] = rng.uniform(-0.3, 0.3)
        tilt = (math.pi / 2 if route == "back" else -math.pi / 2) + rng.uniform(-0.35, 0.35)
        self._set_pose(q, tilt)
        self.d.qpos[1] += 0.05
        mujoco.mj_forward(self.m, self.d)
        self._settle(0.6)

    def _mid_pose(self, kind=None):
        """curriculum poses, held by a PD during a 0.3 s settle: sit / all-fours / squat / crouch / stand, or 'ramp':
        a pose interpolated between the upright stance and one of sit/fours/squat/crouch at a depth U(0, self.depth)
        (reverse curriculum: the trainer raises depth from ~0.5 to 1 as the rise is learned)."""
        rng = self.rng
        if kind is None:
            if self.ramp > 0.0 and rng.uniform() < self.ramp:
                kind = "ramp"
            else:
                kind = str(rng.choice(["sit", "fours", "squat", "crouch", "stand"], p=[0.2, 0.2, 0.25, 0.15, 0.2] if self.R.get("up_gate") else [0.25, 0.25, 0.3, 0.2, 0.0]))
        if kind == "ramp":
            target = str(rng.choice(["sit", "fours", "squat", "crouch"], p=[0.3, 0.2, 0.3, 0.2]))
            qk, tk = self._kind_pose(target)
            qs, ts = self._kind_pose("stand")
            if self.depth_lo > 0.0 and rng.uniform() >= self.p_shallow:
                dep = rng.uniform(self.depth_lo, max(self.depth, self.depth_lo + 0.05))  # the failing band
            else:
                dep = rng.uniform(0.0, max(self.depth_lo if self.depth_lo > 0.0 else self.depth, 0.05))
            q, tilt = qs + dep * (qk - qs), ts + dep * (tk - ts)
            self._set_pose(q, tilt)
            self._settle(0.3, hold_q=self.d.qpos[self.qa].copy())
            return "ramp%d" % int(min(dep, 0.999) * 4)  # ramp0 (near stand) .. ramp3 (near the target pose)
        q, tilt = self._kind_pose(kind)
        self._set_pose(q, tilt)
        self._settle(0.3, hold_q=self.d.qpos[self.qa].copy())
        return kind

    def _kind_pose(self, kind):
        rng = self.rng
        q = np.zeros(17)
        I = JN.index
        if kind == "sit":  # floor sit, legs bent in front, trunk upright, hands beside the hips
            for s in ("r", "l"):
                q[I("hip_" + s)] = rng.uniform(-1.5, -1.0)
                q[I("knee_" + s)] = rng.uniform(0.8, 1.9)
                q[I("shoulder_" + s)] = rng.uniform(-0.3, 0.6)
                q[I("elbow_" + s)] = rng.uniform(-0.6, 0.0)
            tilt = rng.uniform(-0.3, 0.4)
        elif kind == "fours":  # all-fours / bear: trunk pitched down, hands planted ahead, knees bent
            for s in ("r", "l"):
                q[I("hip_" + s)] = rng.uniform(-1.5, -0.8)
                q[I("knee_" + s)] = rng.uniform(1.0, 2.0)
                q[I("shoulder_" + s)] = rng.uniform(-1.6, -1.0)
                q[I("elbow_" + s)] = rng.uniform(-0.4, 0.0)
                q[I("ankle_" + s)] = rng.uniform(-0.6, 0.3)
            tilt = rng.uniform(-1.4, -0.7)
        elif kind == "squat":  # deep squat, hands forward near the floor
            for s in ("r", "l"):
                q[I("hip_" + s)] = rng.uniform(-1.5, -1.1)
                q[I("knee_" + s)] = rng.uniform(1.6, 2.05)
                q[I("ankle_" + s)] = rng.uniform(0.2, 0.7)
                q[I("shoulder_" + s)] = rng.uniform(-1.3, -0.6)
            tilt = rng.uniform(-0.7, -0.2)
        elif kind == "stand":  # upright stance with pose noise: where the hold is learned
            q = rng.uniform(-0.15, 0.15, 17)
            tilt = rng.uniform(-0.1, 0.1)
        else:  # crouch: half-bent legs, trunk a little forward
            for s in ("r", "l"):
                q[I("hip_" + s)] = rng.uniform(-1.0, -0.4)
                q[I("knee_" + s)] = rng.uniform(0.6, 1.4)
                q[I("ankle_" + s)] = rng.uniform(0.1, 0.5)
                q[I("shoulder_" + s)] = rng.uniform(-0.6, 0.2)
            tilt = rng.uniform(-0.4, 0.0)
        return q, tilt

    def _unity_pose(self, route):
        rs = self.resets.get(route) or self.resets["belly"]
        kinds = sorted({r["kind"] for r in rs})
        kind = str(self.rng.choice(kinds))
        pool = [x for x in rs if x["kind"] == kind]
        r = pool[int(self.rng.integers(len(pool)))]
        n = self.pose_noise
        q = np.array(r["q"]) + self.rng.uniform(-n, n, 17)
        tilt = r["tilt"] + self.rng.uniform(-0.1, 0.1)
        self._set_pose(q, tilt, z=r["z"] + 0.02, mirror=bool(self.rng.integers(2)))
        self._settle(self.settle_s)
        return kind

    def _contacts(self):
        d = self.d
        heel = [False, False]
        toe = [False, False]
        hand = [False, False]
        other = False
        head = False
        knee = False
        self.hand_pos = [None, None]
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
            elif g == self.g_hand[0]:
                hand[0] = True
                self.hand_pos[0] = c.pos.copy()
            elif g == self.g_hand[1]:
                hand[1] = True
                self.hand_pos[1] = c.pos.copy()
            else:
                other = True
                if g == self.g_head:
                    head = True
                if g in self.g_knee:
                    knee = True
        return heel, toe, hand, other, head, knee

    def _torso_angle(self):
        return self.d.qpos[2] - self.d.qpos[self.qa[self.i_lumbar]]

    def _mid(self):
        d = self.d
        heel, toe = self.cont[0], self.cont[1]
        xs = []
        for s in range(2):
            if heel[s]:
                xs.append(d.xpos[self.b_heel[s]][0])
            if toe[s]:
                xs.append(d.xpos[self.b_toe[s]][0])
        if not xs:
            xs = [d.xpos[b][0] for b in self.b_heel + self.b_toe]
        return 0.5 * (min(xs) + max(xs))

    def _obs(self):
        d = self.d
        q = d.qpos[self.qa]
        qd = d.qvel[self.va]
        heel, toe = self.cont[0], self.cont[1]
        th_p, w_p = d.qpos[2], d.qvel[2]
        th_t = self._torso_angle()
        w_t = d.qvel[2] - d.qvel[self.va[self.i_lumbar]]
        comx = d.subtree_com[1][0]
        ph = 2 * math.pi * self.t / T_CLOCK
        o = np.concatenate([q, qd * 0.1, [th_t, th_p, w_t * 0.1, w_p * 0.1],
                            [d.qpos[1] + 1.01 - FLOOR_Z, d.qvel[0], d.qvel[1]], [comx - self._mid()],
                            [heel[0], toe[0], heel[1], toe[1]], self.prev_a, [self.v_cmd],
                            [math.sin(ph), math.cos(ph)]])
        return o.astype(np.float32)

    # ---------- gym API
    def reset(self, seed=None, options=None):
        if seed is not None:
            self.rng = np.random.default_rng(seed)
        m, d = self.m, self.d
        mujoco.mj_resetData(m, d)
        rng = self.rng
        fs = 1.0 + rng.uniform(-0.2, 0.2)
        m.actuator_biasprm[self.fric_act, 2] = self.fric_base * fs
        if self.dr:
            ms = rng.uniform(0.9, 1.1, m.nbody)
            m.body_mass[:] = self.mass0 * ms
            m.body_inertia[:] = self.inert0 * ms[:, None]
            m.actuator_gear[self.mot, 0] = self.gear0[self.mot] * rng.uniform(1 - self.dr_gear, 1 + self.dr_gear, 17)
            m.geom_friction[self.g_sole, 0] = rng.uniform(0.5, 0.9)
            m.geom_friction[self.g_hand, 0] = rng.uniform(0.55, 0.95)
            self.latency = int(rng.integers(0, 2))
        else:
            m.body_mass[:] = self.mass0
            m.body_inertia[:] = self.inert0
            m.actuator_gear[self.mot, 0] = self.gear0[self.mot]
            m.geom_friction[self.g_sole, 0] = self.fric_sole0
            m.geom_friction[self.g_hand, 0] = self.fric_hand0
            self.latency = 0
        # start distribution
        fr = self.fixed_route
        if fr is None:
            u = rng.uniform()
            if u < self.p_stand:
                fr = "stand"
            elif u < self.p_stand + self.p_mid:
                fr = "mid"
            elif u < self.p_stand + self.p_mid + self.p_proc:
                fr = "proc_" + str(rng.choice(["back", "belly"]))
            else:
                fr = str(rng.choice(self.routes, p=self.route_w))
        self.start = fr
        if fr == "stand":
            self.start_kind = self._mid_pose(kind="stand")
        elif fr == "mid":
            self.start_kind = self._mid_pose()
        elif fr.startswith("proc_"):
            self.start_kind = fr
            self._proc_lying(fr[5:])
        else:
            self.start_kind = self._unity_pose(fr)
        d.qvel[:] = 0.0
        d.xfrc_applied[:] = 0.0
        if self.grip:
            d.eq_active[self.eq_grip] = 0
        self.grip_on, self.grip_lock = [False, False], [0.0, 0.0]
        mujoco.mj_forward(m, d)
        self.t = 0.0
        self.k = 0
        self.act = np.zeros(17)
        self.a_f = np.zeros(13)
        self.a_hold = np.zeros(13)
        self.prev_a = np.zeros(13)
        self.prev_a2 = np.zeros(13)
        self.cont = self._contacts()
        self.foot_last = [-1.0, -1.0]
        self.pel_prev = d.qpos[1] + 1.01 - FLOOR_Z
        self.hold = 0.0
        self.budget = self.assist * BW * self.max_steps * DT * FRAME_SKIP  # N s available this episode
        self.ep = dict(assist_sum=0.0, assist_peak=0.0, grip_sum=0.0, grip_peak=0.0, grip_ticks=0, pel_max=0.0, t_stand=None, success=False, head_floor=0,
                       start=self.start, kind=self.start_kind, lim=0.0, e=0.0, pel0=self.pel_prev, tilt0=float(d.qpos[2]),
                       feet_dx=0.0)
        # keyframe ladder for the floor starts; rungs already matched at the start are not paid for
        self.kf_lad, self.kf_reached = None, -1
        route = fr[5:] if fr.startswith("proc_") else fr
        if route in self.kf_arr:
            self.kf_lad = route
            hit = np.nonzero(self._kf_match() >= self.R.get("imit_thr", 0.5))[0]
            self.kf_reached = int(hit.max()) if len(hit) else -1
        self.ep["kf0"] = self.kf_reached
        return self._obs(), {}

    def _grip_engage(self, i, p_world):
        """pin palm i to the world at p_world: connect anchor in the hand frame (eq_data[0:3]) and in the world (3:6)."""
        m, d = self.m, self.d
        b = self.b_hand[i]
        R = d.xmat[b].reshape(3, 3)
        m.eq_data[self.eq_grip[i], 0:3] = R.T @ (p_world - d.xpos[b])
        m.eq_data[self.eq_grip[i], 3:6] = p_world
        d.eq_active[self.eq_grip[i]] = 1
        self.grip_on[i] = True

    def _eq_force(self, eid):
        """constraint force (N, world x/y/z rows) of an active connect equality, from the solver's efc arrays."""
        d = self.d
        idx = np.nonzero((d.efc_type == mujoco.mjtConstraint.mjCNSTR_EQUALITY) & (d.efc_id == eid))[0]
        if len(idx) < 3:
            return np.zeros(3)
        return np.array(d.efc_force[idx[:3]])

    def _standing(self, heel, toe, other, th_t, pel_h):
        R = self.R
        # debounced feet: a foot counts as down if its sole touched the floor within the last 0.1 s (contact flags
        # flicker at 50 Hz on a quiet stance; a hold that resets on one flicker can never reach 3 s)
        both = (self.t - self.foot_last[0] <= 0.1) and (self.t - self.foot_last[1] <= 0.1)
        calm = abs(self.d.qvel[0]) < 0.4 and abs(self.d.qvel[1]) < 0.4
        head_ok = True
        if R.get("head_min"):  # head box centre above the floor (standing: ~1.63 m)
            head_ok = (self.d.geom_xpos[self.g_head][2] - FLOOR_Z) >= R["head_min"]
        return bool(pel_h >= R["stand_h"] and abs(th_t) < math.radians(25) and abs(self.d.qpos[2]) < math.radians(25)
                    and both and not other and calm and head_ok)

    def step(self, a):
        m, d, R = self.m, self.d, self.R
        a = np.clip(np.asarray(a, dtype=float), -1, 1)
        self.a_f = self.a_f + R["a_lp"] * (a - self.a_f)
        a_in = self.a_f.copy()
        a_use = a_in
        if self.latency:
            a_use, self.a_hold = self.a_hold.copy(), a_in.copy()
        target = np.zeros(17)
        target[self.act_idx] = a_use
        # assist: horizontal PD on the pelvis toward the support midpoint, budgeted
        d.xfrc_applied[self.b_pelvis, :] = 0.0
        F = 0.0
        heel, toe = self.cont[0], self.cont[1]
        pel_h0 = d.qpos[1] + 1.01 - FLOOR_Z
        if self.assist > 0 and self.budget > 0 and (any(heel) or any(toe)) and pel_h0 > 0.4:
            e = d.subtree_com[1][0] - self._mid()
            F = -(600.0 * e + 60.0 * d.subtree_com[1][0] * 0 + 60.0 * d.qvel[0])
            F = float(np.clip(F, -self.assist_cap * BW, self.assist_cap * BW))
            F = float(np.clip(F, -self.budget / (DT * FRAME_SKIP), self.budget / (DT * FRAME_SKIP)))
            d.xfrc_applied[self.b_pelvis, 0] = F
            self.budget -= abs(F) * DT * FRAME_SKIP
        self.ep["assist_sum"] += abs(F)
        self.ep["assist_peak"] = max(self.ep["assist_peak"], abs(F))
        # hand-ground grip: the policy's MTP channels (actions 8 = right, 12 = left; the toes are passive springs in the
        # v7 layout, so these outputs are free) command a grip: > 0 and the palm on the floor -> the palm is pinned to
        # the contact point (connect constraint); <= 0 or a cap exceeded -> released (0.1 s lock-out after a break)
        gF = 0.0
        if self.grip:
            hand = self.cont[2]
            for i, ai in ((0, 8), (1, 12)):
                want = a_use[ai] > 0.0
                if self.grip_on[i] and not want:
                    d.eq_active[self.eq_grip[i]] = 0
                    self.grip_on[i] = False
                elif (want and not self.grip_on[i] and hand[i] and self.hand_pos[i] is not None and self.t >= self.grip_lock[i]
                      and np.linalg.norm(d.cvel[self.b_hand[i]][3:6]) < 0.5):  # a moving palm cannot grab (no impulsive pins)
                    self._grip_engage(i, self.hand_pos[i])
        tau2 = 0.0
        for _ in range(FRAME_SKIP):
            target[self.pd_idx] = np.clip(-self.pd_kp * d.qpos[self.qa[self.pd_idx]], -1, 1)
            self.act += np.clip(target - self.act, -self.slew_vec, self.slew_vec)
            d.ctrl[self.mot] = np.where(self.act < 0, self.act * self.neg_scale, self.act)
            mujoco.mj_step(m, d)
            tau2 += float(np.sum(d.ctrl[self.mot[self.act_idx]] ** 2))
            if self.grip and any(self.grip_on):
                for i in (0, 1):
                    if self.grip_on[i]:
                        f = self._eq_force(self.eq_grip[i])
                        if abs(f[0]) > self.grip_cap_t or -f[2] > self.grip_cap_n:  # tangential cap / pull-off cap
                            d.eq_active[self.eq_grip[i]] = 0
                            self.grip_on[i] = False
                            self.grip_lock[i] = self.t + 0.1
                            self.ep["grip_breaks"] = self.ep.get("grip_breaks", 0) + 1
                        else:
                            gF += math.hypot(f[0], f[2]) / FRAME_SKIP  # held force only (a break is not a held load)
        self.ep["grip_sum"] += gF
        self.ep["grip_peak"] = max(self.ep["grip_peak"], gF)
        if any(self.grip_on):
            self.ep["grip_ticks"] += 1
        self.t += DT * FRAME_SKIP
        self.k += 1
        heel, toe, hand, other, headc, kneec = self.cont = self._contacts()
        for i in (0, 1):
            if heel[i] or toe[i]:
                self.foot_last[i] = self.t
        th_t = self._torso_angle()
        pel_h = d.qpos[1] + 1.01 - FLOOR_Z
        head_h = d.geom_xpos[self.g_head][2] - FLOOR_Z
        # --- reward
        lvl = min(max((pel_h - 0.15) / (0.95 - 0.15), 0.0), 1.0)
        r_h = R["w_h"] * lvl
        if R.get("up_gate"):  # when high, the level term needs an upright trunk (a forward fold earns up_gate of it)
            hi0 = min(max((pel_h - 0.5) / 0.3, 0.0), 1.0)
            r_h *= 1.0 - hi0 * (1.0 - R["up_gate"]) * (1.0 - math.exp(-(th_t / math.radians(30)) ** 2))
        r_head = R["w_head"] * min(max((head_h - 0.2) / (1.6 - 0.2), 0.0), 1.0)
        r_prog = R["w_prog"] * float(np.clip((pel_h - self.pel_prev) / (DT * FRAME_SKIP), -1.0, 1.0))
        hi = min(max((pel_h - 0.4) / 0.4, 0.0), 1.0)
        r_up = R["w_up"] * hi * math.exp(-(th_t / math.radians(20)) ** 2)
        r_com = 0.0
        if (any(heel) or any(toe)) and pel_h > 0.4:
            r_com = R["w_com"] * math.exp(-((d.subtree_com[1][0] - self._mid()) / 0.1) ** 2)
        standing = self._standing(heel, toe, other, th_t, pel_h)
        r_stand = R["w_stand"] if standing else 0.0
        r_still = 0.0
        if R.get("w_still") and pel_h > 0.7:
            r_still = R["w_still"] * math.exp(-((d.qvel[0] ** 2 + d.qvel[1] ** 2) / 0.3 ** 2))
        pen_knee = R.get("w_knee", 0.0) * (1.0 if (kneec and pel_h > 0.45) else 0.0)
        pen_hands = R.get("w_hands", 0.0) * (1.0 if (any(hand) and pel_h > R.get("hands_h", 0.7)) else 0.0)
        pen_other = R.get("w_other", 0.0) * (1.0 if (other and pel_h > R.get("other_h", 0.45)) else 0.0)
        q = d.qpos[self.qa]
        near = np.minimum(q - self.jrange[:, 0], self.jrange[:, 1] - q) < math.radians(3)
        pen_lim = R["w_lim"] * float(np.mean(near))
        e_rate = float(np.sum((a_in - self.prev_a) ** 2))
        e_jerk = float(np.sum((a_in - 2 * self.prev_a + self.prev_a2) ** 2))
        pen_head = R["w_headc"] * (1.0 if headc else 0.0)
        # stage 3: feet under the pelvis (shaped) and the keyframe ladder (floor starts, weight decays to 0)
        feet_x = float(np.mean([d.xpos[b][0] for b in self.b_heel + self.b_toe]))
        dx = feet_x - float(d.xpos[self.b_pelvis][0])
        self.ep["feet_dx"] += abs(dx)
        r_feet = 0.0
        if R.get("w_feet"):
            g = min(max((R["feet_h1"] - pel_h) / (R["feet_h1"] - R["feet_h0"]), 0.0), 1.0)
            if not (any(heel) or any(toe)):
                g *= R["feet_air"]
            r_feet = R["w_feet"] * g / (1.0 + (dx / R["feet_w"]) ** 2)
        r_imit = 0.0
        if self.w_imit > 0.0 and self.kf_lad is not None:
            ms = self._kf_match()
            hit = np.nonzero(ms >= R.get("imit_thr", 0.5))[0]
            jn = int(hit.max()) if len(hit) else -1
            adv = max(0, jn - self.kf_reached)
            self.kf_reached = max(self.kf_reached, jn)
            nxt = ms[self.kf_reached + 1:]
            r_imit = self.w_imit * ((float(nxt.max()) if len(nxt) else 1.0) + R.get("imit_bonus", 1.0) * adv)
        self.r_terms = (r_feet, r_imit, dx)  # diagnostics (smoke / eval)
        rew = (r_h + r_head + r_prog + r_up + r_com + r_stand + r_still + r_feet + r_imit - pen_knee - pen_hands - pen_other
               - R["w_e"] * tau2 / FRAME_SKIP - R["w_rate"] * e_rate - R["w_jerk"] * e_jerk - pen_lim - pen_head)
        self.prev_a2 = self.prev_a
        self.prev_a = a_in
        self.pel_prev = pel_h
        self.ep["pel_max"] = max(self.ep["pel_max"], pel_h)
        self.ep["lim"] += float(np.mean(near))
        self.ep["e"] += tau2 / FRAME_SKIP
        if headc:
            self.ep["head_floor"] += 1
        term = False
        if standing:
            if self.hold == 0.0:
                self.ep["t_stand"] = self.t
            self.hold += DT * FRAME_SKIP
            if self.hold >= R["hold_s"] - 1e-9:
                rew += R["bonus"]
                term = True
                self.ep["success"] = True
        elif pel_h >= R["stand_h"] - 0.05 and not other and abs(th_t) < math.radians(40):
            self.hold = max(0.0, self.hold - 3 * DT * FRAME_SKIP)  # soft miss (a flicker, a sway): the hold decays
            if self.hold == 0.0:
                self.ep["t_stand"] = None
        else:
            self.hold = 0.0
            self.ep["t_stand"] = None
        if not np.isfinite(d.qpos).all() or abs(d.qpos[0]) > 5.0:
            term = True
            rew -= 10.0
        if R.get("fall_term") and self.start == "stand" and pel_h < R["fall_h"] and not term:
            term = True  # upright-start episodes end on a fall (the walk pilot's signal)
            rew -= 1.0
        trunc = self.k >= self.max_steps and not term
        info = {}
        if term or trunc:
            n = max(self.k, 1)
            info.update(grip_mean=self.ep["grip_sum"] / n / BW, grip_peak=self.ep["grip_peak"] / BW,
                        grip_frac=self.ep["grip_ticks"] / n, grip_breaks=self.ep.get("grip_breaks", 0))
            info.update(ep_t=self.t, success=self.ep["success"], t_stand=self.ep["t_stand"], pel_max=self.ep["pel_max"],
                        assist_mean=self.ep["assist_sum"] / n / BW, assist_peak=self.ep["assist_peak"] / BW,
                        start=self.ep["start"], kind=self.ep["kind"], lim_frac=self.ep["lim"] / n, energy=self.ep["e"] / n,
                        head_floor=self.ep["head_floor"] / n, feet_dx=self.ep["feet_dx"] / n,
                        kf_frac=((self.kf_reached + 1) / len(KF[self.kf_lad])) if self.kf_lad is not None else np.nan)
        return self._obs(), float(rew), bool(term), bool(trunc), info

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
        return HumanGetUp(seed=2000 + rank, **kw)
    return _f
