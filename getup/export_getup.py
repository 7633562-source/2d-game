"""export_getup.py - export a get-up PPO actor with its VecNormalize statistics folded in, as plain JSON weights in
the SAME format as cache/mujoco-1024/policy_v4strict.json (obs_mean/obs_std/obs_clip, layers [{W,b,act}], layouts),
and check the numpy forward against SB3 predict() on observations from the policy's own get-up rollouts.
usage: env -u PYTHONPATH <venv>/python.exe -I export_getup.py runs/v1/ckpt_N.zip policy_getup_v1.json [--rv 1]
"""
import argparse
import json
import os
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
RL = os.path.normpath(os.path.join(HERE, "..", "rl-1024"))
sys.path.insert(0, HERE)
sys.path.insert(0, RL)
os.environ.setdefault("OMP_NUM_THREADS", "1")

import torch  # noqa: E402

torch.set_num_threads(1)
from stable_baselines3 import PPO  # noqa: E402
from stable_baselines3.common.vec_env import DummyVecEnv, VecNormalize  # noqa: E402

import getupenv  # noqa: E402
from eval_getup import vecnorm_path  # noqa: E402


def np_forward(W, obs):
    x = np.clip((obs - W["obs_mean"]) / W["obs_std"], -W["obs_clip"], W["obs_clip"])
    for layer in W["layers"]:
        x = x @ np.array(layer["W"], dtype=np.float32).T + np.array(layer["b"], dtype=np.float32)
        if layer["act"] == "tanh":
            x = np.tanh(x)
    return np.clip(x, -1, 1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("ckpt")
    ap.add_argument("out")
    ap.add_argument("--rv", type=int, default=1)
    ap.add_argument("--grip", type=int, default=0)
    a = ap.parse_args()
    t0 = time.time()
    model = PPO.load(a.ckpt, device="cpu")
    vn = VecNormalize.load(vecnorm_path(a.ckpt), DummyVecEnv([lambda: getupenv.HumanGetUp(rv=a.rv)]))
    vn.training = False
    # observations along deterministic rollouts from both routes (the distribution Unity will feed)
    obs = []
    for route in ("back", "belly", "proc_back", "proc_belly"):
        env = getupenv.HumanGetUp(rv=a.rv, fixed_route=route, max_t=11.0, seed=9000, grip=bool(a.grip))
        for ep in range(2):
            o, _ = env.reset(seed=9000 + ep)
            done = False
            while not done:
                obs.append(o.copy())
                act, _ = model.predict(vn.normalize_obs(o[None]), deterministic=True)
                o, r, te, tr, info = env.step(act[0])
                done = te or tr
    obs = np.array(obs, dtype=np.float32)
    pol = model.policy
    mods = [m for m in pol.mlp_extractor.policy_net if isinstance(m, torch.nn.Linear)] + [pol.action_net]
    layers = [dict(W=lin.weight.detach().numpy().tolist(), b=lin.bias.detach().numpy().tolist(),
                   act="tanh" if i < len(mods) - 1 else "linear") for i, lin in enumerate(mods)]
    W = dict(obs_mean=np.asarray(vn.obs_rms.mean, np.float32).tolist(),
             obs_std=np.sqrt(np.asarray(vn.obs_rms.var, np.float32) + vn.epsilon).tolist(),
             obs_clip=float(vn.clip_obs), obs_dim=int(obs.shape[1]), act_dim=len(layers[-1]["b"]),
             obs_layout="17 q, 17 qd*0.1, torso ang, pelvis ang, torso angvel*0.1, pelvis angvel*0.1, pelvis z, vx, vz, "
                        "comx-midsole, heel_r toe_r heel_l toe_l, prev action 13, v_cmd (=0 for the get-up), "
                        "sin(ph), cos(ph) with ph = 2*pi*t/16 s, t = time since the get-up policy started (rlenv._obs layout)",
             act_layout="lumbar, sh_r, el_r, sh_l, el_l, hip_r, knee_r, ankle_r, mtp_r, hip_l, knee_l, ankle_l, mtp_l "
                        "(signal in [-1,1]; s>=0 extensor ceiling, s<0 flexor; ankle -1 -> 22 N m; v7 calm conventions: "
                        "action low-pass 0.5, wrists/MTP/neck/head passive springs, MTP outputs ignored)",
             skill="get-up from lying (back/belly) to standing; run with -rlPolicyV7 1 -rlVcmd 0 -rlPeriod 16",
             grip=("actions 8 (right) and 12 (left) are the hand-ground GRIP commands: > 0 and the palm on the floor -> pin the "
                   "palm to the contact point (joint to the ground), <= 0 or break force exceeded -> release; caps 1.0 BW "
                   "tangential and 0.5 BW pull-off per hand; the toes stay passive springs" if a.grip else "none"),
             source=os.path.relpath(a.ckpt, HERE).replace("\\", "/"), layers=layers)
    ref, _ = model.predict(vn.normalize_obs(obs), deterministic=True)
    ref = np.clip(ref, -1, 1).astype(np.float32)
    W2 = dict(W)
    W2["obs_mean"] = np.array(W["obs_mean"], np.float32)
    W2["obs_std"] = np.array(W["obs_std"], np.float32)
    out_np = np_forward(W2, obs)
    err = float(np.abs(out_np - ref).max())
    json.dump(W, open(a.out, "w"))
    chk = dict(ckpt=a.ckpt, out=a.out, n_obs=int(len(obs)), max_abs_err_numpy_vs_sb3=err,
               size_kb=round(os.path.getsize(a.out) / 1024, 1), layers=[len(l["b"]) for l in layers])
    print(json.dumps(chk))
    json.dump(chk, open(a.out.replace(".json", "_check.json"), "w"), indent=1)
    print("%.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
