"""eval_getup.py - gate GU1 for a get-up checkpoint: deterministic policy, nominal twin (no DR), Unity lying starts.
Success = STANDING reached within --gate_t (8 s) and held hold_s (3 s). Reports per start type: success %, time to
stand (median / p90), assist mean (mean over takes) / peak (max over takes) in % BW. Optional film (best back + belly
episodes, captions «со спины» / «с живота» / «встал») and the progress plot from runs/<tag>/progress.csv (Russian labels).
usage: env -u PYTHONPATH <venv>/python.exe -I eval_getup.py runs/v1/ckpt_N.zip [--eps 50] [--routes back,belly]
       [--assist 0.0] [--video best_getup.mp4] [--plot progress.png] [--out gate.json] [--wide 0] [--rv 1]
"""
import argparse
import json
import os
import subprocess
import sys

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
from evalpol import stick, FFMPEG  # noqa: E402  (rl-1024 side-view drawer + ffmpeg path, read-only import)

RU = {"back": "со спины", "belly": "с живота", "other": "сидя", "proc_back": "случайно, со спины",
      "proc_belly": "случайно, с живота", "mid": "промежуточная"}


def vecnorm_path(p):
    base = os.path.basename(p).replace(".zip", "").replace("ckpt_", "")
    return os.path.join(os.path.dirname(p), "vecnorm_" + base + ".pkl")


def load(path, **envkw):
    model = PPO.load(path, device="cpu")
    vn = VecNormalize.load(vecnorm_path(path), DummyVecEnv([lambda: getupenv.HumanGetUp(**envkw)]))
    vn.training = False
    return model, vn


def run_route(model, vn, route, eps, gate_t, hold_s, envkw, seed0=7000, want_frames=False, stochastic=False):
    env = getupenv.HumanGetUp(fixed_route=route, max_t=gate_t + hold_s + 0.5, dr=False, seed=seed0, **envkw)
    res, best = [], None
    for i in range(eps):
        o, _ = env.reset(seed=seed0 + i)
        frames = []
        done = False
        while not done:
            if want_frames and env.k % 2 == 0:
                frames.append((stick(env, 800, 500, 220.0), env.t, env.hold > 0.0))
            a, _ = model.predict(vn.normalize_obs(o[None]), deterministic=not stochastic)
            o, r, te, tr, info = env.step(a[0])
            done = te or tr
        ok = bool(info["success"]) and info["t_stand"] is not None and info["t_stand"] <= gate_t + 1e-6
        rec = dict(route=route, kind=info["kind"], ok=ok, t_stand=info["t_stand"], pel_max=round(info["pel_max"], 3),
                   assist_mean=round(info["assist_mean"], 4), assist_peak=round(info["assist_peak"], 4), ep_t=round(info["ep_t"], 2),
                   grip_mean=round(info.get("grip_mean", 0.0), 4), grip_peak=round(info.get("grip_peak", 0.0), 3),
                   grip_frac=round(info.get("grip_frac", 0.0), 3), grip_breaks=info.get("grip_breaks", 0))
        res.append(rec)
        if want_frames:
            key = (ok, -(info["t_stand"] or 99.0), info["pel_max"])
            if best is None or key > best[0]:
                best = (key, frames, rec)
    return res, best


def summarize(res):
    ok = [r["ok"] for r in res]
    ts = [r["t_stand"] for r in res if r["ok"]]
    return dict(n=len(res), succ=round(float(np.mean(ok)), 3) if ok else 0.0,
                t_stand_med=round(float(np.median(ts)), 2) if ts else None,
                t_stand_p90=round(float(np.percentile(ts, 90)), 2) if ts else None,
                assist_mean=round(float(np.mean([r["assist_mean"] for r in res])), 4),
                assist_peak=round(float(np.max([r["assist_peak"] for r in res])), 4),
                grip_mean=round(float(np.mean([r["grip_mean"] for r in res])), 4),
                grip_peak=round(float(np.max([r["grip_peak"] for r in res])), 3),
                grip_frac=round(float(np.mean([r["grip_frac"] for r in res])), 3),
                grip_breaks=int(np.sum([r["grip_breaks"] for r in res])),
                pel_max_mean=round(float(np.mean([r["pel_max"] for r in res])), 3))


def write_film(clips, out):
    from PIL import Image, ImageDraw, ImageFont
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 24)
    tmp = out + "_frames"
    os.makedirs(tmp, exist_ok=True)
    n = 0
    for route, frames, rec in clips:
        for j, (img, t, standing) in enumerate(frames + [frames[-1]] * 12):
            im = Image.fromarray(img)
            dr = ImageDraw.Draw(im)
            dr.rectangle([0, 0, im.width, 70], fill=(0, 0, 0))
            dr.text((10, 6), "Подъём: %s (%s)%s" % (RU[route], rec["kind"], ", сцепка кистей" if rec.get("grip_frac", 0) > 0 else ""), font=font, fill=(255, 255, 255))
            st = "ВСТАЛ" if standing else ""
            dr.text((10, 38), "время %.1f с   %s" % (t, st), font=font, fill=(255, 255, 0) if not standing else (120, 255, 120))
            im.save(os.path.join(tmp, "f%05d.png" % n))
            n += 1
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-framerate", "25", "-i", os.path.join(tmp, "f%05d.png"),
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", out], check=True)
    for f in os.listdir(tmp):
        os.remove(os.path.join(tmp, f))
    os.rmdir(tmp)


def plot_progress(csvs, out):
    import csv
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    fig, ax = plt.subplots(2, 1, figsize=(9, 7), sharex=True)
    for tag, path in csvs:
        rows = list(csv.DictReader(open(path)))
        if not rows:
            continue
        x = np.array([float(r["steps"]) for r in rows]) / 1e6
        f = lambda k: np.array([float(r[k]) if r.get(k) not in (None, "", "nan") else np.nan for r in rows])
        for key, lab, ls in (("succ_back", "со спины", "-"), ("succ_belly", "с живота", "--"), ("succ_other", "сидя", ":"),
                             ("succ_mid", "промежуточные", "-."), ("succ_proc", "случайные лёжа", (0, (1, 1))),
                             ("succ_stand", "стоя (удержание)", (0, (3, 1, 1, 1)))):
            ax[0].plot(x, 100 * f(key), linestyle=ls, label="%s: %s" % (tag, lab))
        ax[1].plot(x, f("ep_rew_mean"), label="%s: награда за эпизод" % tag)
        ax[1].plot(x, 100 * f("pel_max_mean"), "--", label="%s: макс. высота таза, см" % tag)
    ax[0].set_ylabel("успех (встал и держит 3 с), %")
    ax[0].set_ylim(-2, 102)
    ax[0].grid(alpha=0.3)
    ax[0].legend(fontsize=8, ncol=2)
    ax[0].set_title("Обучение подъёма (PPO, MuJoCo-двойник)")
    ax[1].set_xlabel("шагов обучения, млн")
    ax[1].set_ylabel("награда / высота")
    ax[1].grid(alpha=0.3)
    ax[1].legend(fontsize=8)
    fig.tight_layout()
    fig.savefig(out, dpi=110)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("path")
    ap.add_argument("--eps", type=int, default=50)
    ap.add_argument("--routes", default="back,belly")
    ap.add_argument("--gate_t", type=float, default=8.0)
    ap.add_argument("--rv", type=int, default=1)
    ap.add_argument("--assist", type=float, default=0.0)
    ap.add_argument("--wide", type=int, default=0)
    ap.add_argument("--grip", type=int, default=0)
    ap.add_argument("--video", default="")
    ap.add_argument("--plot", default="")
    ap.add_argument("--plot_runs", default="")
    ap.add_argument("--out", default="")
    ap.add_argument("--stochastic", type=int, default=0)
    ap.add_argument("--ramp", type=float, default=0.0, help="route mid: share of ramp poses (stand->pose interpolation at depth U(0,1))")
    a = ap.parse_args()
    envkw = dict(rv=a.rv, assist=a.assist, wide=bool(a.wide), grip=bool(a.grip), ramp=a.ramp)
    model, vn = load(a.path, **envkw)
    hold_s = getupenv.REWARD[a.rv]["hold_s"]
    summ, clips = {}, []
    for route in a.routes.split(","):
        res, best = run_route(model, vn, route, a.eps, a.gate_t, hold_s, envkw, want_frames=bool(a.video), stochastic=bool(a.stochastic))
        summ[route] = summarize(res)
        kinds = {}
        for r in res:
            kinds.setdefault(r["kind"], []).append(r["ok"])
        summ[route]["by_kind"] = {k: round(float(np.mean(v)), 2) for k, v in sorted(kinds.items())}
        print(route, json.dumps(summ[route]), flush=True)
        if best is not None:
            clips.append((route, best[1], best[2]))
    gate = all(summ[r]["succ"] >= 0.8 for r in ("back", "belly") if r in summ) and all(summ[r]["assist_mean"] <= 0.05 for r in summ)
    summ["GU1"] = bool(gate and "back" in summ and "belly" in summ)
    summ["ckpt"] = a.path
    print("GU1", summ["GU1"])
    if a.out:
        json.dump(summ, open(a.out, "w"), indent=1)
    if a.video and clips:
        write_film(clips, a.video)
        print("wrote", a.video)
    if a.plot:
        runs = a.plot_runs.split(",") if a.plot_runs else [os.path.basename(os.path.dirname(a.path))]
        plot_progress([(t, os.path.join(HERE, "runs", t, "progress.csv")) for t in runs], a.plot)
        print("wrote", a.plot)


if __name__ == "__main__":
    main()
