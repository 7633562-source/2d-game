"""train_dog.py - PPO (stable-baselines3) on rlenv_dog.DogTrot (dog-twin-1024), copied from rl-1024/train.py.

usage: python train_dog.py --tag d1 --minutes 150 [--dr 1] [--init runs/<tag>/ckpt_N.zip]
Writes runs/<tag>/: progress.csv (one row per rollout), ckpt_<steps>.zip + vecnorm_<steps>.pkl every ~10 min, last.*"""
import argparse
import csv
import os
import sys
import time

os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("MKL_NUM_THREADS", "1")

import numpy as np
import torch
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import BaseCallback
from stable_baselines3.common.vec_env import VecNormalize, VecMonitor

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rlenv_dog  # noqa: E402
from batchvec import BatchSubprocVecEnv  # noqa: E402


class Logger(BaseCallback):
    def __init__(self, out, ckpt_every_s=600, max_s=None):
        super().__init__()
        self.out, self.ckpt_every_s, self.max_s = out, ckpt_every_s, max_s
        self.eps = []

    def _on_training_start(self):
        self.t0 = time.time()
        self.last_ck = self.t0
        self.f = open(os.path.join(self.out, "progress.csv"), "a", newline="")
        self.w = csv.writer(self.f)
        if self.f.tell() == 0:
            self.w.writerow(["steps", "wall_s", "fps", "eps", "ep_t_mean", "ep_t_p20", "full_frac", "fell_frac",
                             "speed_mean", "assist_bw_mean", "ep_rew_mean"])
        self.n0 = self.num_timesteps

    def _on_step(self):
        for info in self.locals["infos"]:
            if "ep_t" in info:
                self.eps.append((info["ep_t"], info["ep_dist"], info["fell"], info.get("assist_bw", 0.0),
                                 info.get("episode", {}).get("r", np.nan)))
        if self.max_s and time.time() - self.t0 > self.max_s:
            return False
        return True

    def _on_rollout_end(self):
        now = time.time()
        rec = np.array(self.eps) if self.eps else np.zeros((0, 5))
        self.eps = []
        if len(rec):
            t = rec[:, 0]
            row = [self.num_timesteps, round(now - self.t0, 1), round((self.num_timesteps - self.n0) / max(now - self.t0, 1e-6)),
                   len(rec), round(t.mean(), 3), round(np.percentile(t, 20), 3), round((rec[:, 2] < 0.5).mean(), 3),
                   round(rec[:, 2].mean(), 3), round((rec[:, 1] / np.maximum(t, 1e-6)).mean(), 3),
                   round(rec[:, 3].mean(), 4), round(np.nanmean(rec[:, 4]), 2)]
            self.w.writerow(row)
            self.f.flush()
            print(" ".join(str(x) for x in row), flush=True)
        if now - self.last_ck > self.ckpt_every_s:
            self.save("ckpt_%d" % self.num_timesteps)
            self.last_ck = now

    def save(self, name):
        self.model.save(os.path.join(self.out, name + ".zip"))
        self.model.get_env().save(os.path.join(self.out, "vecnorm_" + name.replace("ckpt_", "") + ".pkl"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tag", required=True)
    ap.add_argument("--rv", type=int, default=2)
    ap.add_argument("--steps", type=float, default=2e8)
    ap.add_argument("--minutes", type=float, default=0)
    ap.add_argument("--init", type=str, default="")
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--per_worker", type=int, default=8)
    ap.add_argument("--n_steps", type=int, default=512)
    ap.add_argument("--batch", type=int, default=4096)
    ap.add_argument("--epochs", type=int, default=5)
    ap.add_argument("--lr", type=float, default=3e-4)
    ap.add_argument("--threads", type=int, default=1)
    ap.add_argument("--max_t", type=float, default=12.0)
    ap.add_argument("--log_std", type=float, default=-0.5)
    ap.add_argument("--dr", type=int, default=1)
    ap.add_argument("--assist", type=int, default=1)
    ap.add_argument("--tail_len", type=float, default=0.20)   # day 2: 0.35 (owner-approved skeleton change)
    ap.add_argument("--jaw", type=int, default=0)             # day 2: 1 = jaw on its hinge, held by the game's DriveJaw law
    ap.add_argument("--std_floor", type=float, default=-9.0)  # day 3: raise a fine-tuned policy's log_std to at least this
    ap.add_argument("--gains", type=str, default="")          # day 3: rlenv_dog.GAINS set ("box2d" = Unity-safe)
    ap.add_argument("--curr_eps", type=int, default=0)        # day 3: rv 3 speed curriculum length (resets per env)
    a = ap.parse_args()
    bkw = dict(tail_len=a.tail_len, jaw_hinge=bool(a.jaw))
    torch.set_num_threads(a.threads)
    out = os.path.join(HERE, "runs", a.tag)
    os.makedirs(out, exist_ok=True)
    venv = BatchSubprocVecEnv([rlenv_dog.make_env(i, rv=a.rv, max_t=a.max_t, dr=bool(a.dr), assist=bool(a.assist), bkw=bkw,
                                                  gains=a.gains or None, curr_eps=a.curr_eps,
                                                  curr_log=(os.path.join(out, "curr_%d.txt" % i) if a.rv in (4, 5) else None))
                               for i in range(a.workers * a.per_worker)], a.workers)
    venv = VecMonitor(venv)
    if a.init:
        base = os.path.basename(a.init).replace(".zip", "").replace("ckpt_", "")
        venv = VecNormalize.load(os.path.join(os.path.dirname(a.init), "vecnorm_" + base + ".pkl"), venv)
        venv.training = True
        model = PPO.load(a.init, env=venv, device="cpu",
                         custom_objects=dict(learning_rate=a.lr, n_steps=a.n_steps, batch_size=a.batch, n_epochs=a.epochs))
        with torch.no_grad():
            model.policy.log_std.clamp_(min=a.std_floor)
    else:
        venv = VecNormalize(venv, norm_obs=True, norm_reward=True, clip_obs=10.0, gamma=0.99)
        model = PPO("MlpPolicy", venv, n_steps=a.n_steps, batch_size=a.batch, n_epochs=a.epochs, learning_rate=a.lr,
                    gamma=0.99, gae_lambda=0.95, clip_range=0.2, ent_coef=0.0, vf_coef=0.5, max_grad_norm=0.5,
                    policy_kwargs=dict(net_arch=dict(pi=[256, 256], vf=[256, 256]), activation_fn=torch.nn.Tanh,
                                       log_std_init=a.log_std),
                    device="cpu", verbose=0, seed=0)
    cb = Logger(out, max_s=a.minutes * 60 if a.minutes else None)
    print("start", a, flush=True)
    model.learn(total_timesteps=int(a.steps), callback=cb, reset_num_timesteps=not a.init)
    cb.save("last")
    print("done", model.num_timesteps, flush=True)
    venv.close()


if __name__ == "__main__":
    main()
