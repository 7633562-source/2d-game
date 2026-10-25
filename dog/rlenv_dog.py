"""rlenv_dog.py - gymnasium env: planar dog trot in the MuJoCo twin (dog-twin-1024).

- physics 200 Hz (dog2d.xml via build_dog.build), policy 50 Hz (frame skip 4);
- action 13 in [-1, 1]: joint TARGET offsets around the stand pose for lumbar, shoulder/elbow/paw (front r, l),
  hip/knee/paw (rear r, l). The env turns them into muscle signals with a fixed PD
      s = clip((kp (q_stand + a * span - q) - kd qd) / gear, -1, 1)
  then the game's activation slew 0.1 per 5 ms (20 /s, DogStanceController.cs:1037) and the flexor/extensor
  ceilings (paw +1 -> 17 N m extensor, -1 -> 7 N m flexor). The Unity runner reproduces the same three lines.
  Neck/head: PD hold world 0 (the game's SPD target), tail0 PD to 0, tail 1-4 passive springs.
- observation 53: 13 q, 13 qd/10, chest + pelvis world angle and rates (4), chest height, vx, vz (3), paw contacts (4),
  previous action (13), commanded speed (1), clock sin/cos (2). VecNormalize outside.
- episode = a speed plan: stand 0.3-1.5 s, then the command ramps at 0.5 m/s^2 to v (0.8 m/s = the game's -dogTrotV);
  the trot clock (period T, diagonal pairs FR+RL at 0, FL+RR at 0.5, duty D) runs only while v_cmd > 0.
- termination: any non-paw geom on the floor, chest pitch off the stand by > 45 deg, chest centre < 0.25 m.
- domain randomisation (dr=True): mass x U(0.9, 1.1) per body, gear +-10 %, joint friction +-20 %, paw pair friction
  U(0.7, 1.3), action latency 0-1 policy step, chest pushes +-50 N x 0.2 s every 1-3 s.
- jaw (day 2, bkw jaw_hinge=True): not a policy joint. The game's DriveJaw hold (DogStanceController.cs:8190-8223,
  ControlJointStable 8802-8830): target = 35 deg x jaw_open (0 = closed), P 2.5 / D 0.4, error ref 10 deg, speed ref
  200 deg/s, divided by (1 + D-gain x 2.5 N m x 57.3 / 200 x dt / Ieff); muscle 2.5 N m each way (Dog.cs:138), same
  activation slew. CLI -dogWalk leaves WalkPant 0, so the game's trot keeps jaw_open 0. env.jaw_open drives it.
- assist (assist=True): the game's mode-1 pelvis hand (DogStanceController.cs:7497-7508): tau = clamp(-(1 N m/deg err +
  0.08 w), +-4.9 N m) when |pelvis tilt - tilt0| > 4 deg; logged as |tau| / 0.40 m in body weights.
"""
import math
import os
import sys

import gymnasium as gym
import mujoco
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build_dog as B  # noqa: E402

DT = 0.005
FRAME_SKIP = 4
SLEW = 0.1
FLOOR_Z = 0.02
ACT_J = ["lumbar", "shoulder_r", "elbow_r", "fpaw_r", "shoulder_l", "elbow_l", "fpaw_l",
         "hip_r", "knee_r", "rpaw_r", "hip_l", "knee_l", "rpaw_l"]
PD_J = ["neck", "head", "tail0"]
# the game's stand pose (referenced jointAngle, ApplySpawnPoseToStance Dog.cs:1002-1013: hip -12, knee 24, shoulder -20,
# elbow 24; lumbar 0 = chest -8 world with pelvis level); paws: flat (q = elbow/knee fold, set at reset)
STAND = {"lumbar": 0.0, "shoulder": -20.0, "elbow": 24.0, "hip": -12.0, "knee": 24.0, "paw": 0.0}
SPAN = {"lumbar": 15.0, "shoulder": 60.0, "elbow": 50.0, "hip": 60.0, "knee": 50.0, "paw": 40.0}
KP = {"lumbar": 300.0, "shoulder": 80.0, "elbow": 80.0, "hip": 80.0, "knee": 80.0, "paw": 4.0}   # N m/rad (paw: M_jj 0.00075 kg m2, explicit PD stable only at kp dt2/M << 1)
KD = {"lumbar": 3.0, "shoulder": 1.0, "elbow": 0.6, "hip": 1.0, "knee": 0.6, "paw": 0.015}       # N m s/rad
PAWS = ["fpaw_r", "fpaw_l", "rpaw_r", "rpaw_l"]
DIAG = {"fpaw_r": 0, "rpaw_l": 0, "fpaw_l": 1, "rpaw_r": 1}   # pair A = FR + RL, pair B = FL + RR

REWARD = {
    1: dict(w_speed=1.0, sig=0.25, w_prog=0.5, alive=0.1, w_up=0.3, w_pel=0.2, w_h=0.2, w_clock=0.4, w_diag=0.2,
            w_clear=0.2, clear_h=0.04, w_slip=2.0, w_energy=0.002, w_rate=0.1, w_stand=1.0, period=0.775, duty=0.6,
            a_lp=0.5, acc=0.5),
    # rv 2: residual policy on top of the game's own trot (phase-averaged Unity joint profiles, ref_trot.json from
    # take cl505_x_s07, per-leg touchdown offsets and duty from the same take, period 0.775 s)
    2: dict(w_speed=1.0, sig=0.25, w_prog=0.5, alive=0.1, w_up=0.3, w_pel=0.2, w_h=0.2, w_clock=0.4, w_diag=0.2,
            w_clear=0.2, clear_h=0.04, w_slip=2.0, w_energy=0.002, w_rate=0.1, w_stand=1.0, period=0.775, duty=0.6,
            a_lp=0.5, acc=0.5, ref=True, w_imit=0.5, imit_sig=20.0, res_scale=0.4),
    # rv 3 (day 3): the rv 2 residual, with the gait morphing by commanded speed (MORPH below): the clock period, duty
    # and joint amplitude follow v_cmd; above 1.5 m/s the leg phase offsets slide from the game trot to a bound-like
    # gallop (fores together, hinds together, half a cycle apart). Episodes: stand, ramp at 1 m/s^2, hold, often a
    # stop back to the stand. Same obs (53) and actions (13) as rv 2; the clock is the integrated phase.
    3: dict(w_speed=1.0, sig=0.25, w_prog=0.5, alive=0.1, w_up=0.3, w_pel=0.2, w_h=0.2, w_clock=0.4, w_diag=0.2,
            w_clear=0.2, clear_h=0.04, w_slip=2.0, w_energy=0.002, w_rate=0.1, w_stand=1.0, period=0.775, duty=0.6,
            a_lp=0.5, acc=1.0, ref=True, w_imit=0.5, imit_sig=20.0, res_scale=0.4, morph=True),
}
# day 3, g3/g4: the rv 3 morph puts a half-way gait at every speed in 1.5-2.0 m/s (the offsets slide with v), and every
# policy so far hits a wall there (falls from 1.6 m/s, trained and box2d gains alike). rv 4 = a gait SWITCH at
# 1.8 m/s (trot below, gallop offsets above, k moves 0 <-> 1 in 0.4 s, never parked half-way at a steady speed); rv 5 = a fast
# trot (trot offsets at every speed, period/duty/amp still from MORPH). Both: an adaptive speed curriculum per env
# (vmax_c grows 0.1 after <= 1 fall in the last >= 4 episodes near vmax_c, drops 0.1 after >= 3 falls).
REWARD[4] = dict(REWARD[3], gait_switch=True, curr_adapt=True)
REWARD[5] = dict(REWARD[3], trot_only=True, curr_adapt=True)
# speed -> gait table for rv 3 (v in m/s). PER = clock period s, DUTYF = factor on each leg's trot duty, AMP = factor on
# the trot profile's swing about its mean. k_gallop ramps 0 -> 1 over 1.5-2.0 m/s and slides the phase offsets.
MORPH = dict(v=[0.0, 0.8, 1.5, 2.0, 2.5, 3.0], per=[0.775, 0.775, 0.60, 0.48, 0.42, 0.40],
             dutyf=[1.0, 1.0, 0.80, 0.68, 0.61, 0.56], amp=[1.0, 1.0, 1.10, 1.20, 1.25, 1.30])
GALLOP_OFF = {"fr": 0.0, "fl": 0.92, "rr": 0.50, "rl": 0.58}   # bound-like: fores ~together, hinds ~0.5 later
LEG_OF = {"fpaw_r": "fr", "fpaw_l": "fl", "rpaw_r": "rr", "rpaw_l": "rl"}
# day 3, transfer: "box2d" = gains that stay stable in Box2D at the game's two-body effective inertia WITH the game's
# dead-beat joint friction on top (tests/t_law.py, law_check.json: regulation from 3-40 deg, no limit cycle).
# The trained KP/KD limit-cycle there (70-500 deg/s). pk/pc = neck, head, tail0.
GAINS = {
    "trained": dict(kp=dict(KP), kd=dict(KD), pk=[20.0, 10.0, 0.5], pc=[0.3, 0.15, 0.005]),
    "box2d": dict(kp={"lumbar": 120.0, "shoulder": 80.0, "elbow": 50.0, "hip": 80.0, "knee": 60.0, "paw": 4.0},
                  kd={"lumbar": 1.0, "shoulder": 0.25, "elbow": 0.06, "hip": 0.5, "knee": 0.15, "paw": 0.015},
                  pk=[5.0, 5.0, 0.5], pc=[0.1, 0.1, 0.005]),
}
# the game's EffectiveInertia (DogStanceController.cs:8829) = 1 / (1/I_child + 1/I_parent), rb.inertia about each CoM
# (skeleton.md masses/sizes, I = m (w^2 + h^2) / 12), kg m^2
GAME_IEFF = {"lumbar": 0.016330, "shoulder_r": 0.002966, "shoulder_l": 0.002966, "elbow_r": 0.001243,
             "elbow_l": 0.001243, "fpaw_r": 0.000294, "fpaw_l": 0.000294, "hip_r": 0.004929, "hip_l": 0.004929,
             "knee_r": 0.002034, "knee_l": 0.002034, "rpaw_r": 0.000308, "rpaw_l": 0.000308,
             "neck": 0.001107, "head": 0.000908, "tail0": 0.0000369}


def jtype(jn):
    return B.joint_of(B.BODY_OF_J[jn])


class DogTrot(gym.Env):
    JAW_P = 2.5  # DogStanceController.jawPGain; a what-if knob for the droop test (tests/t_jawgain.py)
    metadata = {"render_modes": ["rgb_array"], "render_fps": 50}

    def __init__(self, v_target=0.8, v_choices=(0.6, 0.8, 0.8, 1.0), rv=1, max_t=12.0, dr=False, assist=True,
                 seed=None, paw_mu=4.0, pose_noise=0.05, stand_range=(0.3, 1.5), render_mode=None, bkw=None, gains=None,
                 curr_eps=0, curr_log=None):
        bk = dict(integrator="RK4")  # bang-bang actions, full joint range: implicit 108/340 episodes blew up, RK4 4/274 (guarded)
        bk.update(bkw or {})
        xml, self.start_y = B.build(paw_mu=paw_mu, **bk)
        self.m = mujoco.MjModel.from_xml_string(xml)
        self.d = mujoco.MjData(self.m)
        m = self.m
        self.R = REWARD[rv]
        self.v_target = v_target
        self.v_choices = v_choices
        self.max_steps = int(round(max_t / (DT * FRAME_SKIP)))
        self.dr = dr
        self.assist = assist
        self.pose_noise = pose_noise
        self.stand_range = stand_range
        self.render_mode = render_mode
        self.fixed_plan = None
        jid = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_JOINT, n)
        aid = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_ACTUATOR, n)
        gid = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_GEOM, n)
        bid = lambda n: mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_BODY, n)
        allj = ACT_J + PD_J
        self.qa = np.array([m.jnt_qposadr[jid(j)] for j in allj])
        self.va = np.array([m.jnt_dofadr[jid(j)] for j in allj])
        self.mot = np.array([aid("m_" + j) for j in allj])
        self.na = len(ACT_J)
        self.lo = np.array([m.jnt_range[jid(j)][0] for j in allj])  # compiled model: already rad
        self.hi = np.array([m.jnt_range[jid(j)][1] for j in allj])
        self.gear0 = m.actuator_gear[:, 0].copy()
        self.kp = np.array([KP[jtype(j)] for j in ACT_J])
        self.kd = np.array([KD[jtype(j)] for j in ACT_J])
        self.span = np.radians(np.array([SPAN[jtype(j)] for j in ACT_J]))
        self.stand = np.radians(np.array([STAND[jtype(j)] for j in ACT_J]))
        # paw flat in the stand: paw world = lower world - q_paw - ref; the fold adds elbow - shoulder etc.
        # front: upper world = chest - sh_q - ref..., computed numerically at reset
        self.neg_scale = np.ones(len(allj))
        for j in ("fpaw_r", "fpaw_l", "rpaw_r", "rpaw_l"):
            self.neg_scale[allj.index(j)] = 7.0 / 17.0
        self.fric_act = np.array([i for i in range(m.nu) if mujoco.mj_id2name(m, mujoco.mjtObj.mjOBJ_ACTUATOR, i).startswith("f_")])
        self.fric_base = m.actuator_biasprm[self.fric_act, 2].copy()
        self.mass0 = m.body_mass.copy()
        self.inert0 = m.body_inertia.copy()
        self.g_floor = gid("floor")
        self.g_paw = {p: gid(B.BODY_OF_J[p] + "_g") for p in PAWS}
        self.paw_of_g = {g: p for p, g in self.g_paw.items()}
        self.b_paw = {p: bid(B.BODY_OF_J[p]) for p in PAWS}
        self.paw_fric0 = m.geom_friction[list(self.g_paw.values()), 0].copy()
        self.g_chest = gid("chest_g")
        self.g_pelvis = gid("pelvis_g")
        self.g_neck = gid("neck_g")
        self.g_head = gid("head_g")
        self.b_chest = bid("chest")
        self.b_pelvis = bid("pelvis")
        self.bw = float(m.body_mass.sum() * 9.81)
        self.jaw = None
        self.jaw_open = 0.0
        if jid("jaw") >= 0:
            self.jaw = dict(q=int(m.jnt_qposadr[jid("jaw")]), v=int(m.jnt_dofadr[jid("jaw")]), a=aid("m_jaw"),
                            b=bid("jaw"), bh=bid("head"))
        self.observation_space = gym.spaces.Box(-np.inf, np.inf, shape=(53,), dtype=np.float32)
        self.action_space = gym.spaces.Box(-1.0, 1.0, shape=(self.na,), dtype=np.float32)
        self.rng = np.random.default_rng(seed)
        self.renderer = None
        self.ref = None
        if self.R.get("ref"):
            import json
            RF = json.load(open(os.path.join(HERE, "ref_trot.json")))
            self.off = {"fr": 0.0, "fl": RF["offset_fl"], "rr": RF["offset_rr"], "rl": RF["offset_rl"]}
            self.duty = {k: RF[k]["duty"] for k in ("fr", "fl", "rr", "rl")}
            prof = []  # (row in ACT_J, leg, profile deg[50])
            for i, j in enumerate(ACT_J):
                if j == "lumbar":
                    prof.append((i, "fr", RF["lumbar_vs_fr"]))
                elif not j.endswith("paw_r") and not j.endswith("paw_l"):
                    leg = ("f" if jtype(j) in ("shoulder", "elbow") else "r") + j[-1]
                    prof.append((i, leg, RF[leg][jtype(j)]))
            self.ref = [(i, leg, np.radians(np.array(pr))) for i, leg, pr in prof]
            self.span = self.span * self.R["res_scale"]
            self.off0, self.duty0 = dict(self.off), dict(self.duty)
            self.pmean = {i: float(np.mean(pr)) for i, _, pr in self.ref}
        self.morph = bool(self.R.get("morph"))
        # rv 3 speed curriculum (day 3, g2): the top commanded speed grows 1.2 -> 3.0 m/s over curr_eps resets of this env
        self.curr_eps = int(curr_eps)
        self.n_reset = 0
        self.k_gait, self.gait_tgt = 0.0, 0
        self.vmax_c, self.c_hist, self.curr_log, self.ep_v = 1.5, [], curr_log, 0.0
        self.phase = 0.0
        self.jacp = np.zeros((3, m.nv))
        # day 3, transfer: the PD law. "explicit" (training default) = kp (qt - q) - kd qd. "spd" = the Box2D-safe
        # implicit form with the GAME's two-body effective inertia (GAME_IEFF; DogStanceController.cs:8829):
        #   tau = (kp (qt - q - qd dt) - kd qd) / (1 + (kd + kp dt) dt / Ieff); "spd_all" also for neck/head/tail0.
        self.law = "explicit"
        # passive PD (neck, head: world 0; tail0: q 0): K N m/rad, C N m s/rad. Training default 20/0.3, 10/0.15, 0.5/0.005.
        self.pk = np.array([20.0, 10.0, 0.5])
        self.pc = np.array([0.3, 0.15, 0.005])
        self.ieff = np.array([GAME_IEFF[j] for j in ACT_J + PD_J])
        # day 4 fix (2026-10-25): the gains kwarg is applied AFTER the passive defaults above. Before, it was applied
        # first and pk/pc were reset to the training values, so DogTrot(gains="box2d") ran box2d leg gains with the
        # TRAINED neck/head/tail0 gains (20/0.3, 10/0.15). Hit: ctl_trace.py, export_dog.py rollouts, eval_gallop.py,
        # t_fastfall.py, train_dog.py --gains (g2-g5). env_hook / set_gains() users were right.
        if gains:
            self.set_gains(gains)
        self._stand_q = None

    def set_gains(self, name):
        """day 3: PD gain set by name (GAINS); 'trained' = KP/KD + the passive defaults."""
        G = GAINS[name]
        self.gains = name
        self.kp = np.array([G["kp"][jtype(j)] for j in ACT_J])
        self.kd = np.array([G["kd"][jtype(j)] for j in ACT_J])
        self.pk = np.array(G["pk"], dtype=float)
        self.pc = np.array(G["pc"], dtype=float)

    # ---------------- helpers
    def _gait(self, v=None):
        """rv 3: (period s, per-leg duty, per-leg phase offset, amplitude factor, k_gallop) at speed v (default v_cmd)."""
        if not self.morph:
            return self.R["period"], getattr(self, "duty", None), getattr(self, "off", None), 1.0, 0.0
        v = self.v_cmd if v is None else v
        M = MORPH
        per = float(np.interp(v, M["v"], M["per"]))
        df = float(np.interp(v, M["v"], M["dutyf"]))
        amp = float(np.interp(v, M["v"], M["amp"]))
        if self.R.get("trot_only"):
            k = 0.0
        elif self.R.get("gait_switch"):
            k = self.k_gait
        else:
            k = min(1.0, max(0.0, (v - 1.5) / 0.5))
        off = {}
        for lg in ("fr", "fl", "rr", "rl"):
            d0 = self.off0[lg]
            dd = ((GALLOP_OFF[lg] - d0 + 0.5) % 1.0) - 0.5   # shortest way round the cycle
            off[lg] = (d0 + k * dd) % 1.0
        duty = {lg: self.duty0[lg] * df for lg in self.duty0}
        return per, duty, off, amp, k

    def _phase01(self):
        return (self.phase % 1.0) if self.morph else ((self.tc / self.R["period"]) % 1.0)

    def _wang(self, g):
        R = self.d.geom_xmat[g].reshape(3, 3)
        return math.atan2(R[2, 0], R[0, 0])  # CCW +, rad

    def _contacts(self):
        d = self.d
        on = {p: False for p in PAWS}
        bad = False
        for i in range(d.ncon):
            c = d.contact[i]
            if c.dist > 0.0:
                continue
            g = c.geom1 if c.geom2 == self.g_floor else c.geom2
            p = self.paw_of_g.get(g)
            if p is not None:
                on[p] = True
            else:
                bad = True
        return on, bad

    def _paw_low(self, p):
        m, d = self.m, self.d
        g = self.g_paw[p]
        hs = m.geom_size[g]
        R = d.geom_xmat[g].reshape(3, 3)
        z = 9.0
        for sx in (-1, 1):
            for sz in (-1, 1):
                z = min(z, d.geom_xpos[g][2] + R[2, 0] * sx * hs[0] + R[2, 2] * sz * hs[2])
        return z - FLOOR_Z

    def _slip(self):
        """sum over paws of the mean |horizontal velocity| of their floor contact points, m/s."""
        m, d = self.m, self.d
        acc = {}
        for i in range(d.ncon):
            c = d.contact[i]
            if c.dist > 0.0:
                continue
            g = c.geom1 if c.geom2 == self.g_floor else c.geom2
            p = self.paw_of_g.get(g)
            if p is None:
                continue
            mujoco.mj_jac(m, d, self.jacp, None, c.pos, self.b_paw[p])
            acc.setdefault(p, []).append(abs(float(self.jacp[0] @ d.qvel)))
        return sum(float(np.mean(v)) for v in acc.values())

    def _chest_h(self):
        return self.d.geom_xpos[self.g_chest][2] - FLOOR_Z

    def _obs(self):
        d = self.d
        q = d.qpos[self.qa[:self.na]]
        qd = d.qvel[self.va[:self.na]]
        th_c = self._wang(self.g_chest)
        th_p = d.qpos[2]
        w_p = d.qvel[2]
        w_c = w_p - d.qvel[self.va[0]]
        ph = 2 * math.pi * (self.phase if self.morph else self.tc / self.R["period"])
        on = self.cont
        o = np.concatenate([q, qd * 0.1, [th_c, th_p, w_c * 0.1, w_p * 0.1], [self._chest_h(), d.qvel[0], d.qvel[1]],
                            [float(on[p]) for p in PAWS], self.prev_a, [self.v_cmd], [math.sin(ph), math.cos(ph)]])
        return o.astype(np.float32)

    def _ref_q(self):
        """game-trot joint targets at the current clock (rad, ACT_J order; paws / unset = NaN)."""
        out = np.full(self.na, np.nan)
        ph = self._phase01()
        if self.morph:
            _, duty, off, amp, _ = self._gait()
        for i, leg, pr in self.ref:
            if not self.morph:
                u = (ph - self.off[leg]) % 1.0
            else:   # time-warp: the new gait's stance [0, duty) onto the trot's stance (lumbar follows fr)
                u = (ph - off[leg]) % 1.0
                d0, d1 = self.duty0[leg], duty[leg]
                u = u / d1 * d0 if u < d1 else d0 + (u - d1) / (1.0 - d1) * (1.0 - d0)
            x = u * len(pr)
            k0 = int(x) % len(pr)
            f = x - int(x)
            val = pr[k0] * (1 - f) + pr[(k0 + 1) % len(pr)] * f
            if self.morph:
                val = self.pmean[i] + amp * (val - self.pmean[i])
            out[i] = val
        return out

    def _ref_base(self):
        """stand pose blended into the game trot by s = v_cmd / 0.8; paws: the target that holds them flat."""
        s = min(1.0, self.v_cmd / 0.8)
        b = self.base.copy()
        if s > 0:
            rq = self._ref_q()
            m = ~np.isnan(rq)
            b[m] = self.base[m] + s * (rq[m] - self.base[m])
        for p in PAWS:
            i = ACT_J.index(p)
            b[i] = self.d.qpos[self.qa[i]] + self._wang(self.g_paw[p])
        return b

    def _imit(self):
        if self.ref is None or self.v_cmd <= 0:
            return 0.0
        b = self._ref_base()
        q = self.d.qpos[self.qa[:self.na]]
        idx = [i for i, _, _ in self.ref]
        e = np.degrees(q[idx] - b[idx])
        return float(np.exp(-np.mean(e ** 2) / (2 * self.R["imit_sig"] ** 2)))

    def _make_plan(self):
        rng = self.rng
        if self.R.get("curr_adapt"):   # adaptive curriculum: v = vmax_c (50 %) or U(0.8, vmax_c)
            v = self.vmax_c if rng.random() < 0.5 else float(rng.uniform(0.8, self.vmax_c))
            plan = [(0.0, float(rng.uniform(*self.stand_range))), (v, v / self.R["acc"] + float(rng.uniform(3.0, 6.0)))]
            plan.append((0.0, 1e9) if rng.random() < 0.7 else (v, 1e9))
            return plan
        if self.morph and self.curr_eps > 0:   # curriculum: v = vmax (40 %) or U(0.8, vmax)
            vmax = 1.2 + 1.8 * min(1.0, self.n_reset / self.curr_eps)
            v = vmax if rng.random() < 0.4 else float(rng.uniform(0.8, vmax))
            plan = [(0.0, float(rng.uniform(*self.stand_range))), (v, v / self.R["acc"] + float(rng.uniform(3.0, 6.0)))]
            plan.append((0.0, 1e9) if rng.random() < 0.7 else (v, 1e9))
            return plan
        if self.morph:   # stand, go to v (1 m/s^2), hold 3-6 s, then 70 %: stop back to the stand
            v = float(rng.choice([0.8, 1.2, 1.5, 2.0, 2.5, 2.5, 3.0, 3.0]))
            plan = [(0.0, float(rng.uniform(*self.stand_range))), (v, v / self.R["acc"] + float(rng.uniform(3.0, 6.0)))]
            plan.append((0.0, 1e9) if rng.random() < 0.7 else (v, 1e9))
            return plan
        v = float(rng.choice(self.v_choices)) if self.v_choices else self.v_target
        return [(0.0, float(rng.uniform(*self.stand_range))), (v, 1e9)]

    # ---------------- gym API
    def reset(self, seed=None, options=None):
        if seed is not None:
            self.rng = np.random.default_rng(seed)
        self.n_reset += 1
        m, d = self.m, self.d
        mujoco.mj_resetData(m, d)
        fs = 1.0 + self.rng.uniform(-0.2, 0.2)
        m.actuator_biasprm[self.fric_act, 2] = self.fric_base * fs
        if self.dr:
            ms = self.rng.uniform(0.9, 1.1, m.nbody)
            ms[0] = 1.0
            m.body_mass[:] = self.mass0 * ms
            m.body_inertia[:] = self.inert0 * ms[:, None]
            m.actuator_gear[self.mot, 0] = self.gear0[self.mot] * self.rng.uniform(0.9, 1.1, len(self.mot))
            m.geom_friction[list(self.g_paw.values()), 0] = self.rng.uniform(0.7, 1.3)
            self.latency = int(self.rng.integers(0, 2))
            self.push_next = self.rng.uniform(1.0, 3.0)
        else:
            m.geom_friction[list(self.g_paw.values()), 0] = self.paw_fric0
            self.latency = 0
            self.push_next = 1e9
        self.push_left = 0
        self.push_f = 0.0
        d.xfrc_applied[:] = 0.0
        # stand pose + noise
        q0 = self.stand + self.rng.uniform(-self.pose_noise, self.pose_noise, self.na)
        d.qpos[self.qa[:self.na]] = q0
        mujoco.mj_forward(m, d)
        for p in PAWS:  # paws flat
            i = ACT_J.index(p)
            d.qpos[self.qa[i]] += self._wang(self.g_paw[p])
        d.qpos[self.qa[:self.na]] = np.clip(d.qpos[self.qa[:self.na]], self.lo[:self.na], self.hi[:self.na])
        d.qpos[2] += self.rng.uniform(-0.03, 0.03)
        mujoco.mj_forward(m, d)
        minz = min(self._paw_low(p) for p in PAWS)
        d.qpos[1] -= minz - 0.001
        mujoco.mj_forward(m, d)
        self.base = d.qpos[self.qa[:self.na]].copy()  # target centre = this stand pose (paws flat)
        self.act = np.zeros(len(self.mot))
        self.prev_a = np.zeros(self.na)
        self.a_f = np.zeros(self.na)
        self.a_hold = np.zeros(self.na)
        self.plan = list(self.fixed_plan) if self.fixed_plan is not None else self._make_plan()
        self.seg_i, self.seg_t = 0, 0.0
        self.ep_v = max(p[0] for p in self.plan)
        self.k_gait, self.gait_tgt = 0.0, 0
        self.v_tgt = self.plan[0][0]
        self.v_cmd = 0.0
        self.tc = 0.0
        self.phase = 0.0
        self.t = 0.0
        self.k = 0
        self.tilt0 = float(d.qpos[2])
        self.cont, _ = self._contacts()
        self.h0 = self._chest_h()
        self.ep = dict(dist=0.0, assist_ns=0.0, slip=0.0, td={p: [] for p in PAWS}, vlog=[], flags=[], pushes=0,
                       jaw=[])
        if self.jaw is not None:
            J = self.jaw
            ia, ib = m.body_inertia[J["b"]][1], m.body_inertia[J["bh"]][1]
            ie = 1.0 / (1.0 / ia + 1.0 / ib)   # EffectiveInertia (rb.inertia about the CoM), cached at first use
            self.jaw_damp = 0.4 * m.actuator_gear[J["a"], 0] * math.degrees(1.0) / 200.0 * DT / ie
            self.jaw_act = 0.0
        self.was = dict(self.cont)
        return self._obs(), {}

    def step(self, a):
        m, d, R = self.m, self.d, self.R
        a = np.clip(np.asarray(a, dtype=float), -1, 1)
        self.a_f = self.a_f + R["a_lp"] * (a - self.a_f)
        a_in = a
        a_use = self.a_f
        if self.latency:
            a_use, self.a_hold = self.a_hold, self.a_f.copy()
        base = self._ref_base() if self.ref is not None else self.base
        qt = np.clip(base + a_use * self.span, self.lo[:self.na], self.hi[:self.na])
        gear = m.actuator_gear[self.mot, 0]
        if self.dr:
            if self.push_left <= 0 and self.t >= self.push_next:
                self.push_f = self.rng.uniform(-50.0, 50.0)
                self.push_left = 10
                self.push_next = self.t + 0.2 + self.rng.uniform(1.0, 3.0)
                self.ep["pushes"] += 1
            d.xfrc_applied[self.b_chest, 0] = self.push_f if self.push_left > 0 else 0.0
            self.push_left -= 1
        x0 = d.subtree_com[1][0]
        nwarn0 = int(d.warning[mujoco.mjtWarning.mjWARN_BADQACC].number)
        tau2 = 0.0
        assist_abs = 0.0
        for _ in range(FRAME_SKIP):
            q = d.qpos[self.qa]
            qd = d.qvel[self.va]
            tgt = np.zeros(len(self.mot))
            if self.law == "explicit":
                tgt[:self.na] = (self.kp * (qt - q[:self.na]) - self.kd * qd[:self.na]) / gear[:self.na]
            else:
                ie = self.ieff[:self.na]
                tgt[:self.na] = ((self.kp * (qt - q[:self.na] - qd[:self.na] * DT) - self.kd * qd[:self.na])
                                 / (1.0 + (self.kd + self.kp * DT) * DT / ie) / gear[:self.na])
            # neck / head: world 0; tail0: 0
            wn = self._wang(self.g_neck)
            wh = self._wang(self.g_head)
            pk, pc = self.pk, self.pc
            tgt[self.na + 0] = (-pk[0] * (-wn) - pc[0] * qd[self.na]) / gear[self.na]
            tgt[self.na + 1] = (-pk[1] * (-wh) - pc[1] * qd[self.na + 1]) / gear[self.na + 1]
            tgt[self.na + 2] = (-pk[2] * q[self.na + 2] - pc[2] * qd[self.na + 2]) / gear[self.na + 2]
            if self.law == "spd_all":   # same laws, implicit form; neck/head world rates from the geoms' bodies
                na = self.na
                wnd = d.qvel[2] - d.qvel[self.va[0]] - qd[na]          # neck world rate = chest rate - neck qd
                whd = wnd - qd[na + 1]
                for k_, K, C, e in ((0, pk[0], pc[0], wn + wnd * DT), (1, pk[1], pc[1], wh + whd * DT),
                                    (2, pk[2], pc[2], -(q[na + 2] + qd[na + 2] * DT))):
                    tgt[na + k_] = (K * e - C * qd[na + k_]) / (1.0 + (C + K * DT) * DT / self.ieff[na + k_]) / gear[na + k_]
            tgt = np.clip(tgt, -1, 1)
            self.act += np.clip(tgt - self.act, -SLEW, SLEW)
            d.ctrl[self.mot] = np.where(self.act < 0, self.act * self.neg_scale, self.act)
            if self.jaw is not None:
                J = self.jaw
                qj, wj = d.qpos[J["q"]], d.qvel[J["v"]]
                err = 35.0 * min(max(self.jaw_open, 0.0), 1.0) - math.degrees(qj + wj * DT)
                raw = self.JAW_P * err / 10.0 - 0.4 * math.degrees(wj) / 200.0  # JAW_P = jawPGain (2.5 in the game)
                sj = min(max(raw / (1.0 + self.jaw_damp), -1.0), 1.0)
                self.jaw_act += min(max(sj - self.jaw_act, -SLEW), SLEW)
                d.ctrl[J["a"]] = self.jaw_act
            if self.assist:
                err = math.degrees(d.qpos[2] - self.tilt0)
                tau = 0.0
                if abs(err) > 4.0:
                    tau = float(np.clip(-(1.0 * err + 0.08 * math.degrees(d.qvel[2])), -4.9, 4.9))
                d.xfrc_applied[self.b_pelvis, 4] = -tau
                assist_abs += abs(tau)
            mujoco.mj_step(m, d)
            tau2 += float(np.sum(d.ctrl[self.mot[:self.na]] ** 2))
        self.t += DT * FRAME_SKIP
        self.k += 1
        if self.v_cmd > 0:
            self.tc += DT * FRAME_SKIP
            if self.morph:
                self.phase += DT * FRAME_SKIP / self._gait()[0]
        unstable = (int(d.warning[mujoco.mjtWarning.mjWARN_BADQACC].number) > nwarn0
                    or not np.isfinite(d.qvel).all() or float(np.abs(d.qvel).max()) > 300.0)
        on, bad = self._contacts()
        self.cont = on
        vx = (d.subtree_com[1][0] - x0) / (DT * FRAME_SKIP)
        th_c = self._wang(self.g_chest)
        th_p = d.qpos[2]
        h = self._chest_h()
        gate = min(1.0, max(0.0, (self.v_cmd - 0.05) / 0.25))
        _, g_duty, g_off, _, kg = self._gait()
        sig = max(R["sig"], 0.12 * self.v_cmd) if self.morph else R["sig"]
        # --- reward
        r_speed = math.exp(-((vx - self.v_cmd) / sig) ** 2)
        r_prog = min(max(vx / max(self.v_cmd, 0.3), -1.0), 1.2) * gate
        r_up = math.exp(-((th_c - math.radians(-8.0)) / math.radians(10.0 + 5.0 * kg)) ** 2)
        r_pel = math.exp(-((th_p - self.tilt0) / math.radians(10.0 + 5.0 * kg)) ** 2)
        r_h = math.exp(-((h - self.h0) / (0.05 + 0.03 * kg)) ** 2)
        ph = self._phase01()
        want = {}
        for p in PAWS:
            if self.ref is not None:
                lg = LEG_OF[p]
                want[p] = ((ph - g_off[lg]) % 1.0) < g_duty[lg]
            else:
                pp = (ph - 0.5 * DIAG[p]) % 1.0
                want[p] = pp < R["duty"]
        r_clock = sum((1.0 if on[p] == want[p] else -1.0) for p in PAWS) / 4.0 * gate
        r_diag = 0.5 * ((on["fpaw_r"] == on["rpaw_l"]) + (on["fpaw_l"] == on["rpaw_r"])) * gate * (1.0 - kg)
        r_clear = 0.0
        nsw = 0
        for p in PAWS:
            if not want[p]:
                nsw += 1
                r_clear += min(self._paw_low(p) / R["clear_h"], 1.0)
        r_clear = (r_clear / max(nsw, 1)) * gate
        slip = self._slip()
        e_rate = float(np.sum((a_in - self.prev_a) ** 2))
        r_stand = 0.0
        if self.v_tgt <= 0.0:
            r_stand = (1.0 - gate) * (0.5 * math.exp(-(vx / 0.1) ** 2) + 0.5 * float(all(on.values())))
        rew = (R["w_speed"] * r_speed + R["w_prog"] * r_prog + R["alive"] + R["w_up"] * r_up + R["w_pel"] * r_pel
               + R["w_h"] * r_h + R["w_clock"] * r_clock + R["w_diag"] * r_diag + R["w_clear"] * r_clear
               + R["w_stand"] * r_stand + R.get("w_imit", 0.0) * (1.0 - 0.5 * kg) * self._imit() - R["w_slip"] * slip - R["w_energy"] * tau2 / FRAME_SKIP - R["w_rate"] * e_rate)
        # bookkeeping
        for p in PAWS:
            if on[p] and not self.was[p]:
                self.ep["td"][p].append(self.t)
        self.was = dict(on)
        self.ep["dist"] += vx * DT * FRAME_SKIP
        self.ep["assist_ns"] += assist_abs / 0.40 * DT
        self.ep["slip"] += slip * DT * FRAME_SKIP
        self.ep["vlog"].append((self.t, self.v_cmd, vx, d.subtree_com[1][0]))
        self.ep["flags"].append(tuple(int(on[p]) for p in PAWS))
        if self.jaw is not None:
            self.ep["jaw"].append(math.degrees(d.qpos[self.jaw["q"]]))
        self.prev_a = a_in
        # advance the plan
        self.seg_t += DT * FRAME_SKIP
        while self.seg_i < len(self.plan) - 1 and self.seg_t >= self.plan[self.seg_i][1]:
            self.seg_t -= self.plan[self.seg_i][1]
            self.seg_i += 1
        self.v_tgt = self.plan[self.seg_i][0]
        dv = R["acc"] * DT * FRAME_SKIP
        self.v_cmd = float(self.v_cmd + min(dv, max(-dv, self.v_tgt - self.v_cmd)))
        if self.v_cmd <= 1e-9:
            self.v_cmd = 0.0
            self.tc = 0.0
            self.phase = 0.0
        if self.R.get("gait_switch"):   # step switch at 1.8 m/s (no hysteresis: the obs has v_cmd, not k), k ramps in 0.4 s
            self.gait_tgt = 1 if self.v_cmd >= 1.8 else 0
            dk = DT * FRAME_SKIP / 0.4
            self.k_gait += min(dk, max(-dk, self.gait_tgt - self.k_gait))
        if unstable:
            rew = 0.0
            self.ep['unstable'] = 1
        rew = float(np.clip(rew, -10.0, 10.0))
        term = bool(unstable or bad or h < 0.25 or abs(th_c - math.radians(-8.0)) > math.radians(45) or not np.isfinite(d.qpos).all())
        trunc = self.k >= self.max_steps
        info = {}
        if term or trunc:
            if self.R.get("curr_adapt") and self.fixed_plan is None and self.ep_v >= self.vmax_c - 0.15:
                self.c_hist.append(int(term))
                last = self.c_hist[-6:]
                old = self.vmax_c
                if len(last) >= 4 and sum(last) <= 1:
                    self.vmax_c = min(3.0, self.vmax_c + 0.1)
                elif sum(last) >= 3:
                    self.vmax_c = max(1.4, self.vmax_c - 0.1)
                if self.vmax_c != old:
                    self.c_hist = []
                    if self.curr_log:
                        import time as _t
                        with open(self.curr_log, "a") as f:
                            f.write("%.0f %d %.2f\n" % (_t.time(), self.n_reset, self.vmax_c))
            info["vmax_c"] = self.vmax_c
            info["ep_t"] = self.t
            info["ep_dist"] = self.ep["dist"]
            info["fell"] = term
            info["assist_bw"] = self.ep["assist_ns"] / max(self.t, 1e-6) / self.bw
            info["unstable"] = self.ep.get("unstable", 0)
            if self.ep["jaw"]:
                info["jaw_max_deg"] = float(np.max(self.ep["jaw"]))
                info["jaw_p95_deg"] = float(np.percentile(self.ep["jaw"], 95))
        return self._obs(), float(rew), term, trunc, info

    def render(self):
        if self.renderer is None:
            self.renderer = mujoco.Renderer(self.m, 480, 800)
            self.cam = mujoco.MjvCamera()
            self.cam.type = mujoco.mjtCamera.mjCAMERA_FREE
            self.cam.azimuth = 90
            self.cam.elevation = -5
            self.cam.distance = getattr(self, "cam_dist", 1.9)
        self.cam.lookat[:] = [self.d.subtree_com[1][0] + getattr(self, "cam_dx", 0.0), 0, getattr(self, "cam_z", 0.3)]
        self.renderer.update_scene(self.d, self.cam)
        return self.renderer.render()


def make_env(rank, **kw):
    def _f():
        import torch
        torch.set_num_threads(1)
        return DogTrot(seed=1000 + rank, **kw)
    return _f
