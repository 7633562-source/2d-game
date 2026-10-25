"""export_dog.py - day 3: export a dog trot PPO actor for the Unity runner (route a), with its frozen VecNormalize
statistics folded in, as (1) ONNX and (2) plain JSON weights + every controller constant the runner needs, and a numpy
forward (the C# MLP shape). Checks all three against SB3's predict() on observations from the policy's own rollouts in
the twin, run with the Box2D-safe gain set (rlenv_dog.GAINS["box2d"]) that Unity will use.
Writes policy_dog_<name>.onnx, policy_dog_<name>.json, onnx_check_dog_<name>.json here.
usage: env -u PYTHONPATH <venv>/python.exe export_dog.py [runs/d5_tail35/last.zip] [name d5] [gains box2d]"""
import json
import math
import os
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.dont_write_bytecode = True
os.environ.setdefault("OMP_NUM_THREADS", "1")

import torch  # noqa: E402

torch.set_num_threads(1)
import onnx  # noqa: E402
import onnxruntime as ort  # noqa: E402
from stable_baselines3 import PPO  # noqa: E402
from stable_baselines3.common.vec_env import DummyVecEnv, VecNormalize  # noqa: E402

import build_dog as B  # noqa: E402
import eval_dog  # noqa: E402
import rlenv_dog as E  # noqa: E402

CKPT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "runs", "d5_tail35", "last.zip")
NAME = sys.argv[2] if len(sys.argv) > 2 else "d5"
GAINS = sys.argv[3] if len(sys.argv) > 3 else "box2d"
RV = 2
BKW = dict(tail_len=0.35, jaw_hinge=True)
# export rollouts: steady 0.8 m/s; slow 0.6 then a stop (d5 never trained a stop: reported); 1.0 m/s
PLANS = [[(0.0, 1.0), (0.8, 1e9)], [(0.0, 1.0), (0.6, 7.0), (0.0, 1e9)], [(0.0, 1.0), (1.0, 1e9)]]
T_EP = 14.0


class Actor(torch.nn.Module):
    """obs (raw, 53) -> clip((obs-mean)/sqrt(var+eps), -clip, clip) -> MLP tanh 256x256 -> 13 -> clip [-1,1]."""

    def __init__(self, policy, venv):
        super().__init__()
        self.mean = torch.tensor(venv.obs_rms.mean, dtype=torch.float32)
        self.std = torch.tensor(np.sqrt(venv.obs_rms.var + venv.epsilon), dtype=torch.float32)
        self.clip = float(venv.clip_obs)
        self.net = policy.mlp_extractor.policy_net
        self.head = policy.action_net

    def forward(self, obs):
        x = torch.clamp((obs - self.mean) / self.std, -self.clip, self.clip)
        return torch.clamp(self.head(self.net(x)), -1.0, 1.0)


def collect():
    model = PPO.load(CKPT, device="cpu")
    venv = VecNormalize.load(eval_dog.vecnorm_path(CKPT), DummyVecEnv([lambda: E.DogTrot(rv=RV)]))
    venv.training = False
    env = E.DogTrot(rv=RV, max_t=T_EP, seed=5000, bkw=BKW, gains=GAINS)
    obs, stats, trace = [], [], None
    for ep, plan in enumerate(PLANS):
        env.fixed_plan = plan
        o, _ = env.reset(seed=5000 + ep)
        done, info = False, {}
        rows = []
        while not done:
            obs.append(o.copy())
            a, _ = model.predict(venv.normalize_obs(o[None]), deterministic=True)
            if ep == 0 and len(rows) < 100:
                rows.append(dict(k=env.k, obs=[round(float(v), 6) for v in o], act=[round(float(v), 6) for v in np.clip(a[0], -1, 1)]))
            o, r, te, tr, info = env.step(a[0])
            done = te or tr
        if ep == 0:
            trace = rows
        vl = np.array(env.ep["vlog"])
        stats.append(dict(plan=[list(p) for p in plan], t=round(env.t, 2), fell=bool(te), dist_m=round(env.ep["dist"], 2),
                          v_last2s=round(float((vl[-1, 3] - vl[-100, 3]) / (vl[-1, 0] - vl[-100, 0])), 3),
                          assist_pct=round(100 * info.get("assist_bw", 0.0), 2)))
    return model, venv, np.array(obs, dtype=np.float32), stats, trace


def np_forward(W, obs):
    x = np.clip((obs - np.array(W["obs_mean"], np.float32)) / np.array(W["obs_std"], np.float32), -W["obs_clip"], W["obs_clip"])
    for layer in W["layers"]:
        x = x @ np.array(layer["W"], dtype=np.float32).T + np.array(layer["b"], dtype=np.float32)
        if layer["act"] == "tanh":
            x = np.tanh(x)
    return np.clip(x, -1, 1)


def controller_spec(env):
    """every constant the Unity runner needs, in the twin's units (rad, N m, s); ACT_J order unless noted."""
    import json as _j
    RF = _j.load(open(os.path.join(HERE, "ref_trot.json")))
    G = E.GAINS[GAINS]
    gear = {E.jtype(j): float(env.gear0[env.mot[i]]) for i, j in enumerate(E.ACT_J)}
    return dict(
        act_joints=E.ACT_J, passive_joints=E.PD_J,
        dt=E.DT, frame_skip=E.FRAME_SKIP, policy_hz=1.0 / (E.DT * E.FRAME_SKIP),
        a_lowpass_alpha=env.R["a_lp"], slew_per_substep=E.SLEW,
        stand_q_deg={k: v for k, v in E.STAND.items()}, span_deg={k: v * env.R["res_scale"] for k, v in E.SPAN.items()},
        gains=GAINS, kp=G["kp"], kd=G["kd"], passive_k=dict(zip(E.PD_J, G["pk"])), passive_c=dict(zip(E.PD_J, G["pc"])),
        gear_nm=gear, paw_flexor_scale=7.0 / 17.0,
        joint_range_deg={j: [round(math.degrees(env.lo[i]), 2), round(math.degrees(env.hi[i]), 2)] for i, j in enumerate(E.ACT_J)},
        ref=dict(period_s=env.R["period"], blend_full_at_v=0.8, offsets=dict(fr=0.0, fl=RF["offset_fl"], rr=RF["offset_rr"], rl=RF["offset_rl"]),
                 duty={k: RF[k]["duty"] for k in ("fr", "fl", "rr", "rl")},
                 profiles_deg={j: (RF["lumbar_vs_fr"] if j == "lumbar" else RF[("f" if E.jtype(j) in ("shoulder", "elbow") else "r") + j[-1]][E.jtype(j)])
                               for j in E.ACT_J if not j.endswith("paw_r") and not j.endswith("paw_l")},
                 profile_leg={j: ("fr" if j == "lumbar" else ("f" if E.jtype(j) in ("shoulder", "elbow") else "r") + j[-1])
                              for j in E.ACT_J if not j.endswith("paw_r") and not j.endswith("paw_l")}),
        v_cmd=dict(ramp_m_s2=env.R["acc"], trained_targets=list(env.v_choices), facing="+X only"),
        assist=dict(mode=1, dead_deg=4.0, kp_nm_per_deg=1.0, kd_nm_per_deg_s=0.08, cap_nm=4.9),
        jaw=dict(law="game DriveJaw (DogStanceController.cs:8189), jaw_open 0"),
        tail=dict(tail0="passive PD to q 0 (passive_k/c), gear 3", tail1_4="game TreeSpring 2 N m/rad, cap 2 (Dog.cs:736-746)",
                  tail_len_m=BKW["tail_len"]),
    )


def main():
    t0 = time.time()
    model, venv, obs, stats, trace = collect()
    print("collected %d obs (%.1f s)" % (len(obs), time.time() - t0), json.dumps(stats), flush=True)
    actor = Actor(model.policy, venv).eval()
    ref, _ = model.predict(venv.normalize_obs(obs), deterministic=True)
    ref = np.clip(ref, -1, 1).astype(np.float32)
    with torch.no_grad():
        tor = actor(torch.tensor(obs)).numpy()
    onnx_path = os.path.join(HERE, "policy_dog_%s.onnx" % NAME)
    torch.onnx.export(actor, torch.zeros(1, obs.shape[1]), onnx_path, input_names=["obs"], output_names=["action"],
                      dynamic_axes={"obs": {0: "n"}, "action": {0: "n"}}, opset_version=17, dynamo=False)
    onnx.checker.check_model(onnx.load(onnx_path))
    sess = ort.InferenceSession(onnx_path, providers=["CPUExecutionProvider"])
    out_onnx = sess.run(None, {"obs": obs})[0]
    out_onnx_1 = np.concatenate([sess.run(None, {"obs": obs[i:i + 1]})[0] for i in range(0, len(obs), 97)])
    t1 = time.time()
    for i in range(500):
        sess.run(None, {"obs": obs[i:i + 1]})
    lat_us = (time.time() - t1) / 500 * 1e6
    layers = []
    mods = [m for m in actor.net if isinstance(m, torch.nn.Linear)] + [actor.head]
    for i, lin in enumerate(mods):
        layers.append(dict(W=lin.weight.detach().numpy().tolist(), b=lin.bias.detach().numpy().tolist(),
                           act="tanh" if i < len(mods) - 1 else "linear"))
    env = E.DogTrot(rv=RV, bkw=BKW, gains=GAINS)
    W = dict(obs_mean=actor.mean.numpy().tolist(), obs_std=actor.std.numpy().tolist(), obs_clip=actor.clip,
             obs_dim=int(obs.shape[1]), act_dim=len(layers[-1]["b"]),
             obs_layout="13 q (ACT_J order, rad, q = jointAngle), 13 qd*0.1 (rad/s), chest world angle, pelvis world angle "
                        "(rad, CCW +), chest angvel*0.1, pelvis angvel*0.1 (rad/s), chest centre height above ground + 0.02 skin (m), "
                        "pelvis centre vx, vz (m/s), paw contact fr fl rr rl (0/1), previous RAW action 13 (clipped, before the "
                        "low-pass), v_cmd (m/s, ramped 0.5 m/s^2, 0 while standing), sin(2 pi tc/T), cos(2 pi tc/T) (tc held at 0 "
                        "while v_cmd == 0); see unity-transfer-dog.md section 1",
             act_layout="13 target offsets in [-1,1] (ACT_J order): q_target = base + a_filtered * span; base = stand pose blended "
                        "into the game-trot reference; paws: base keeps the paw flat; see unity-transfer-dog.md section 2",
             controller=controller_spec(env), layers=layers)
    out_np = np_forward(W, obs).astype(np.float32)
    json_path = os.path.join(HERE, "policy_dog_%s.json" % NAME)
    json.dump(W, open(json_path, "w"))

    def mx(a, b):
        return float(np.max(np.abs(a - b)))

    n_params = sum(int(np.prod(np.array(l["W"]).shape)) + len(l["b"]) for l in layers)
    chk = dict(ckpt=CKPT, gains=GAINS, n_obs=int(len(obs)), obs_dim=int(obs.shape[1]), act_dim=int(ref.shape[1]),
               arch=[list(np.array(l["W"]).shape) for l in layers], n_params=n_params,
               max_abs_diff=dict(torch_wrapper_vs_sb3=mx(tor, ref), onnx_batch_vs_sb3=mx(out_onnx, ref),
                                 onnx_single_vs_sb3=mx(out_onnx_1, ref[::97]), numpy_json_vs_sb3=mx(out_np, ref)),
               action_abs_mean=float(np.mean(np.abs(ref))), frac_saturated=float(np.mean(np.abs(ref) >= 1 - 1e-6)),
               onnx_single_call_us=round(lat_us, 1), onnx_file_kb=round(os.path.getsize(onnx_path) / 1024, 1),
               json_file_kb=round(os.path.getsize(json_path) / 1024, 1), rollouts=stats, wall_s=round(time.time() - t0, 1),
               first_100_ticks_of_rollout_0=trace)
    json.dump(chk, open(os.path.join(HERE, "onnx_check_dog_%s.json" % NAME), "w"), indent=1)
    print(json.dumps({k: v for k, v in chk.items() if k != "first_100_ticks_of_rollout_0"}, indent=1))


if __name__ == "__main__":
    main()
