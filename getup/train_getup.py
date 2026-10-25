"""train_getup.py - PPO (stable-baselines3) on getupenv.HumanGetUp. CPU budget: workers + threads <= 6.

usage: env -u PYTHONPATH <venv>/python.exe -I train_getup.py --tag v1 --rv 1 --steps 6e7 --minutes 120
       [--init runs/<tag>/ckpt_N.zip] [--workers 5 --per_worker 6 --threads 1] [--dr 1] [--assist 0.0]
       [--p_mid0 0.5 --p_mid1 0.1 --p_mid_steps 2e7] (curriculum: share of mid-pose starts, linear 0.5 -> 0.1)
Writes runs/<tag>/progress.csv (one row per rollout), ckpt_<steps>.zip + vecnorm_<steps>.pkl every 10 min / 4 M
steps, last.zip, train.log.
"""
import argparse
import csv
import os
import sys
import time

os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("MKL_NUM_THREADS", "1")
HERE = os.path.dirname(os.path.abspath(__file__))
RL = os.path.normpath(os.path.join(HERE, "..", "rl-1024"))
sys.path.insert(0, HERE)
sys.path.insert(0, RL)

import numpy as np  # noqa: E402
import torch  # noqa: E402
from stable_baselines3 import PPO  # noqa: E402
from stable_baselines3.common.callbacks import BaseCallback  # noqa: E402
from stable_baselines3.common.vec_env import VecNormalize, VecMonitor  # noqa: E402

import getupenv  # noqa: E402
from batchvec import BatchSubprocVecEnv  # noqa: E402  (rl-1024 helper, read-only import)

STARTS = ["back", "belly", "other", "mid", "proc", "stand"]


class Logger(BaseCallback):
    def __init__(self, out, a, ckpt_every_s=600, max_s=None):
        super().__init__()
        self.out, self.a, self.ckpt_every_s, self.max_s = out, a, ckpt_every_s, max_s
        self.eps = []

    def _on_training_start(self):
        self.t0 = time.time()
        self.last_ck = self.t0
        self.f = open(os.path.join(self.out, "progress.csv"), "a", newline="")
        self.w = csv.writer(self.f)
        if self.f.tell() == 0:
            self.w.writerow(["steps", "wall_s", "fps", "eps", "p_mid", "ep_rew_mean", "succ_all", "succ_back", "succ_belly",
                             "succ_other", "succ_mid", "succ_proc", "succ_stand", "t_stand_med", "pel_max_mean", "assist_mean", "assist_peak",
                             "lim_frac", "energy", "head_floor", "grip_mean", "grip_peak", "grip_frac", "kf_frac", "feet_dx", "w_imit"])
        self.n0 = self.num_timesteps
        self.w_imit = 0.0
        self.last_ck_n = self.num_timesteps // 4_000_000
        self._set_pmid()

    def _set_pmid(self):
        a = self.a
        f = min(1.0, max(0.0, (self.num_timesteps - a.p_mid_from) / max(a.p_mid_steps, 1.0)))
        self.p_mid = a.p_mid0 + f * (a.p_mid1 - a.p_mid0)
        self.model.get_env().set_attr("p_mid", self.p_mid)
        if a.ramp > 0.0:
            g = min(1.0, max(0.0, (self.num_timesteps - a.depth_from) / max(a.depth_steps, 1.0)))
            self.depth = a.depth0 + g * (a.depth1 - a.depth0)
            self.model.get_env().set_attr("depth", self.depth)
        if a.imit0 > 0.0:  # keyframe-imitation weight, linear decay to 0 over imit_steps from imit_from (default: the resume point)
            frm = a.imit_from if a.imit_from > 0 else self.n0
            h = min(1.0, max(0.0, (self.num_timesteps - frm) / max(a.imit_steps, 1.0)))
            self.w_imit = a.imit0 * (1.0 - h)
            self.model.get_env().set_attr("w_imit", self.w_imit)

    def _on_step(self):
        for info in self.locals["infos"]:
            if "success" in info:
                st = info["start"]
                st = "proc" if st.startswith("proc") else st
                self.eps.append((st, bool(info["success"]), info["t_stand"] if info["success"] else np.nan, info["pel_max"],
                                 info["assist_mean"], info["assist_peak"], info["lim_frac"], info["energy"], info["head_floor"],
                                 info.get("episode", {}).get("r", np.nan), info.get("grip_mean", 0.0), info.get("grip_peak", 0.0),
                                 info.get("grip_frac", 0.0), info.get("kf_frac", np.nan), info.get("feet_dx", np.nan)))
        if self.max_s and time.time() - self.t0 > self.max_s:
            return False
        return True

    def _on_rollout_end(self):
        now = time.time()
        E = self.eps
        self.eps = []
        if E:
            def succ(st):
                s = [e[1] for e in E if st == "all" or e[0] == st]
                return round(float(np.mean(s)), 3) if s else ""
            ts = [e[2] for e in E if e[1]]
            row = [self.num_timesteps, round(now - self.t0, 1), round((self.num_timesteps - self.n0) / max(now - self.t0, 1e-6)),
                   len(E), round(self.p_mid, 3), round(float(np.nanmean([e[9] for e in E])), 2), succ("all"), succ("back"), succ("belly"),
                   succ("other"), succ("mid"), succ("proc"), succ("stand"), round(float(np.median(ts)), 2) if ts else "",
                   round(float(np.mean([e[3] for e in E])), 3), round(float(np.mean([e[4] for e in E])), 4),
                   round(float(np.max([e[5] for e in E])), 4), round(float(np.mean([e[6] for e in E])), 3),
                   round(float(np.mean([e[7] for e in E])), 3), round(float(np.mean([e[8] for e in E])), 3),
                   round(float(np.mean([e[10] for e in E])), 4), round(float(np.max([e[11] for e in E])), 3),
                   round(float(np.mean([e[12] for e in E])), 3)]
            kf = [e[13] for e in E if np.isfinite(e[13])]
            fd = [e[14] for e in E if np.isfinite(e[14])]
            row += [round(float(np.mean(kf)), 3) if kf else "", round(float(np.mean(fd)), 3) if fd else "", round(self.w_imit, 3)]
            self.w.writerow(row)
            self.f.flush()
            print(" ".join(str(x) for x in row), flush=True)
        self._set_pmid()
        if now - self.last_ck > self.ckpt_every_s or self.num_timesteps // 4_000_000 > self.last_ck_n:
            self.last_ck_n = self.num_timesteps // 4_000_000
            self.save("ckpt_%d" % self.num_timesteps)
            self.last_ck = now

    def save(self, name):
        self.model.save(os.path.join(self.out, name + ".zip"))
        self.model.get_env().save(os.path.join(self.out, "vecnorm_" + name.replace("ckpt_", "") + ".pkl"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tag", required=True)
    ap.add_argument("--rv", type=int, default=1)
    ap.add_argument("--steps", type=float, default=6e7)
    ap.add_argument("--minutes", type=float, default=0)
    ap.add_argument("--init", type=str, default="")
    ap.add_argument("--workers", type=int, default=5)
    ap.add_argument("--per_worker", type=int, default=6)
    ap.add_argument("--threads", type=int, default=1)
    ap.add_argument("--n_steps", type=int, default=1024)
    ap.add_argument("--batch", type=int, default=7680)
    ap.add_argument("--epochs", type=int, default=4)
    ap.add_argument("--lr", type=float, default=3e-4)
    ap.add_argument("--gamma", type=float, default=0.995)
    ap.add_argument("--max_t", type=float, default=10.0)
    ap.add_argument("--log_std", type=float, default=-0.5)
    ap.add_argument("--ent", type=float, default=0.0)
    ap.add_argument("--dr", type=int, default=1)
    ap.add_argument("--dr_gear", type=float, default=0.2)
    ap.add_argument("--assist", type=float, default=0.0)
    ap.add_argument("--wide", type=int, default=0)
    ap.add_argument("--p_mid0", type=float, default=0.5)
    ap.add_argument("--p_mid1", type=float, default=0.1)
    ap.add_argument("--p_mid_steps", type=float, default=2e7)
    ap.add_argument("--p_mid_from", type=float, default=0.0)
    ap.add_argument("--p_proc", type=float, default=0.2)
    ap.add_argument("--p_stand", type=float, default=0.0)
    ap.add_argument("--grip", type=int, default=0)
    ap.add_argument("--grip_cap_t", type=float, default=1.0)
    ap.add_argument("--grip_cap_n", type=float, default=0.5)
    ap.add_argument("--route_w", type=str, default="0.4,0.4,0.2")
    ap.add_argument("--ramp", type=float, default=0.0, help="share of mid-pose starts that are stand->pose ramp poses (v5)")
    ap.add_argument("--depth0", type=float, default=0.5)
    ap.add_argument("--depth1", type=float, default=1.0)
    ap.add_argument("--depth_steps", type=float, default=1.5e7)
    ap.add_argument("--depth_from", type=float, default=0.0)
    ap.add_argument("--depth_lo", type=float, default=0.0, help="stage 3: ramp depth band U(depth_lo, depth), 0 = U(0, depth)")
    ap.add_argument("--shallow", type=float, default=0.0, help="stage 3: share of ramp starts kept shallow, U(0, depth_lo)")
    ap.add_argument("--imit0", type=float, default=0.0, help="stage 3: keyframe-imitation weight at the start, decays to 0")
    ap.add_argument("--imit_steps", type=float, default=3e7)
    ap.add_argument("--imit_from", type=float, default=0.0)
    a = ap.parse_args()
    torch.set_num_threads(a.threads)
    out = os.path.join(HERE, "runs", a.tag)
    os.makedirs(out, exist_ok=True)
    rw = [float(x) for x in a.route_w.split(",")]
    venv = BatchSubprocVecEnv([getupenv.make_env(i, rv=a.rv, max_t=a.max_t, dr=bool(a.dr), dr_gear=a.dr_gear, assist=a.assist,
                                                 wide=bool(a.wide), p_mid=a.p_mid0, p_proc=a.p_proc, p_stand=a.p_stand, route_w=rw, grip=bool(a.grip), grip_cap_t=a.grip_cap_t, grip_cap_n=a.grip_cap_n, ramp=a.ramp,
                                                 depth_lo=a.depth_lo, p_shallow=a.shallow)
                               for i in range(a.workers * a.per_worker)], a.workers)
    venv = VecMonitor(venv)
    if a.init:
        base = os.path.basename(a.init).replace(".zip", "").replace("ckpt_", "")
        venv = VecNormalize.load(os.path.join(os.path.dirname(a.init), "vecnorm_" + base + ".pkl"), venv)
        venv.training = True
        model = PPO.load(a.init, env=venv, device="cpu",
                         custom_objects=dict(learning_rate=a.lr, n_steps=a.n_steps, batch_size=a.batch, n_epochs=a.epochs,
                                             ent_coef=a.ent))
    else:
        venv = VecNormalize(venv, norm_obs=True, norm_reward=True, clip_obs=10.0, gamma=a.gamma)
        model = PPO("MlpPolicy", venv, n_steps=a.n_steps, batch_size=a.batch, n_epochs=a.epochs, learning_rate=a.lr,
                    gamma=a.gamma, gae_lambda=0.95, clip_range=0.2, ent_coef=a.ent, vf_coef=0.5, max_grad_norm=0.5,
                    policy_kwargs=dict(net_arch=dict(pi=[256, 256], vf=[256, 256]), activation_fn=torch.nn.Tanh,
                                       log_std_init=a.log_std),
                    device="cpu", verbose=0, seed=0)
    cb = Logger(out, a, max_s=a.minutes * 60 if a.minutes else None)
    print("start", a, flush=True)
    model.learn(total_timesteps=int(a.steps), callback=cb, reset_num_timesteps=not a.init)
    cb.save("last")
    print("done", model.num_timesteps, flush=True)
    venv.close()


if __name__ == "__main__":
    main()
