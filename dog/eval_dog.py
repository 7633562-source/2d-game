"""eval_dog.py - evaluate a saved PPO trot policy on rlenv_dog.DogTrot (deterministic), optional MP4.

usage: python eval_dog.py runs/<tag>/ckpt_N.zip [--eps 10] [--dr 0] [--video best_dog.mp4] [--out eval.jsonl]
Episode: stand 1.0 s, command ramps at 0.5 m/s^2 to 0.8 m/s (the game's -dogTrotV) and holds; 12.6 s total.
Success (task: steady trot at game speed for 10 s from a stand): no fall in 12.6 s, and in the window 2.6-12.6 s
mean speed within 0.8 +- 0.12 m/s, diagonal pairs agree >= 0.7 of the time, every paw touches down >= 8 times."""
import argparse
import json
import os
import subprocess
import sys

import numpy as np
from stable_baselines3 import PPO
from stable_baselines3.common.vec_env import DummyVecEnv, VecNormalize

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rlenv_dog  # noqa: E402

FFMPEG = r"C:/Users/Admin/AppData/Local/hermes/tools/ffmpeg-9.0.1-win32-x64/bin/ffmpeg.exe"
V = 0.8
T0, T1 = 2.6, 12.6


def vecnorm_path(p):
    base = os.path.basename(p).replace(".zip", "").replace("ckpt_", "")
    return os.path.join(os.path.dirname(p), "vecnorm_" + base + ".pkl")


def metrics(env, info):
    E = env.ep
    vl = np.array(E["vlog"])
    fl = np.array(E["flags"])
    t = vl[:, 0]
    w = (t >= T0) & (t <= T1)
    out = dict(t=round(env.t, 2), fell=bool(info.get("fell", False)), unstable=int(info.get("unstable", 0)),
               assist_bw_pct=round(100 * info.get("assist_bw", 0.0), 2))
    if "jaw_max_deg" in info:
        out["jaw_max_deg"] = round(info["jaw_max_deg"], 2)
        out["jaw_p95_deg"] = round(info["jaw_p95_deg"], 2)
    if w.sum() > 10:
        x = vl[w, 3]
        out["speed"] = round(float((x[-1] - x[0]) / (t[w][-1] - t[w][0])), 3)
        f = fl[w]
        out["diag_agree"] = round(float(0.5 * ((f[:, 0] == f[:, 3]).mean() + (f[:, 1] == f[:, 2]).mean())), 3)
        out["duty"] = [round(float(v), 2) for v in f.mean(0)]
        tds = [np.array(E["td"][p]) for p in rlenv_dog.PAWS]
        out["touchdowns"] = [int(((a >= T0) & (a <= T1)).sum()) for a in tds]
        per = [np.median(np.diff(a[(a >= T0) & (a <= T1)])) for a in tds if ((a >= T0) & (a <= T1)).sum() > 2]
        out["stride_s"] = round(float(np.median(per)), 3) if per else None
        out["slip_m_per_paw_s"] = round(E["slip"] / env.t / 4, 3)
    else:
        out.update(speed=0.0, diag_agree=0.0, touchdowns=[0, 0, 0, 0])
    out["success"] = bool(not out["fell"] and env.t >= T1 - 1e-6 and abs(out["speed"] - V) <= 0.12
                          and out["diag_agree"] >= 0.7 and min(out["touchdowns"]) >= 8)
    return out


def run(path, eps=10, dr=False, video=None, seed0=7000, video_ep=0, rv=2, assist=True, bkw=None, cam_dist=1.9,
        title=None, env_hook=None):
    env = rlenv_dog.DogTrot(rv=rv, max_t=T1, dr=dr, seed=seed0, assist=assist, bkw=bkw)
    if env_hook is not None:   # day 3: e.g. the transfer PD law (env.law, env.kd)
        env_hook(env)
    env.cam_dist = cam_dist
    env.fixed_plan = [(0.0, 1.0), (V, 1e9)]
    venv = VecNormalize.load(vecnorm_path(path), DummyVecEnv([lambda: rlenv_dog.DogTrot()]))
    venv.training = False
    model = PPO.load(path, device="cpu")
    res, frames = [], []
    for ep in range(eps):
        o, _ = env.reset(seed=seed0 + ep)
        done = False
        info = {}
        while not done:
            a, _ = model.predict(venv.normalize_obs(o[None]), deterministic=True)
            o, r, te, tr, info = env.step(a[0])
            done = te or tr
            if video and ep == video_ep and env.k % 2 == 0:
                vx = env.ep["vlog"][-1][2]
                frames.append((env.render(), env.t, env.d.subtree_com[1][0], env.v_cmd, dict(env.cont)))
        res.append(metrics(env, info))
    summ = dict(path=path, dr=dr, assist=assist, eps=eps, success=sum(r["success"] for r in res),
                falls=sum(r["fell"] for r in res),
                speed_mean=round(float(np.mean([r["speed"] for r in res])), 3),
                diag_mean=round(float(np.mean([r["diag_agree"] for r in res])), 3),
                assist_bw_pct_mean=round(float(np.mean([r["assist_bw_pct"] for r in res])), 2))
    if "jaw_max_deg" in res[0]:
        summ["jaw_max_deg"] = max(r["jaw_max_deg"] for r in res)
        summ["jaw_p95_deg_mean"] = round(float(np.mean([r["jaw_p95_deg"] for r in res])), 2)
    if video:
        write_video(frames, video, res[video_ep], title=title)
    return summ, res


TITLE1 = "Собака-двойник MuJoCo, рысь (PPO): стойка 1 с, разгон 0.5 м/с², цель 0.8 м/с"


def write_video(frames, out, r, title=None):
    from PIL import Image, ImageDraw, ImageFont
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 22)
    small = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 18)
    tmp = out + "_frames"
    os.makedirs(tmp, exist_ok=True)
    names = {"fpaw_r": "ПП", "fpaw_l": "ПЛ", "rpaw_r": "ЗП", "rpaw_l": "ЗЛ"}
    for i, (img, t, x, vc, cont) in enumerate(frames):
        im = Image.fromarray(img)
        dr = ImageDraw.Draw(im)
        dr.rectangle([0, 0, im.width, 92], fill=(0, 0, 0))
        dr.text((10, 4), title or TITLE1, font=small, fill=(255, 255, 255))
        dr.text((10, 30), "время %.1f с   пройдено %.2f м   команда %.2f м/с" % (t, x, vc), font=font, fill=(255, 255, 0))
        on = "  ".join("%s:%s" % (names[p], "земля" if cont[p] else "воздух") for p in rlenv_dog.PAWS)
        dr.text((10, 60), "лапы  " + on, font=small, fill=(180, 255, 180))
        dr.rectangle([0, im.height - 30, im.width, im.height], fill=(0, 0, 0))
        dr.text((10, im.height - 26), "итог эпизода: скорость %.2f м/с, диагонали вместе %.0f %%, помощь %.1f %% веса"
                % (r["speed"], 100 * r["diag_agree"], r["assist_bw_pct"]), font=small, fill=(255, 255, 255))
        im.save(os.path.join(tmp, "f%05d.png" % i))
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-framerate", "25", "-i", os.path.join(tmp, "f%05d.png"),
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", out], check=True)
    for f in os.listdir(tmp):
        os.remove(os.path.join(tmp, f))
    os.rmdir(tmp)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("path")
    ap.add_argument("--eps", type=int, default=10)
    ap.add_argument("--dr", type=int, default=0)
    ap.add_argument("--video", default="")
    ap.add_argument("--video_ep", type=int, default=0)
    ap.add_argument("--out", default="")
    ap.add_argument("--rv", type=int, default=2)
    ap.add_argument("--assist", type=int, default=1)
    ap.add_argument("--tail_len", type=float, default=0.20)
    ap.add_argument("--jaw", type=int, default=0)
    ap.add_argument("--visuals", type=int, default=0)
    ap.add_argument("--cam_dist", type=float, default=1.9)
    ap.add_argument("--title", default="")
    a = ap.parse_args()
    bkw = dict(tail_len=a.tail_len, jaw_hinge=bool(a.jaw), visuals=bool(a.visuals))
    summ, res = run(a.path, a.eps, bool(a.dr), a.video or None, video_ep=a.video_ep, rv=a.rv, assist=bool(a.assist),
                    bkw=bkw, cam_dist=a.cam_dist, title=a.title or None)
    summ["bkw"] = bkw
    print(json.dumps(summ))
    for r in res:
        print(json.dumps(r))
    if a.out:
        with open(a.out, "a") as f:
            f.write(json.dumps(dict(summary=summ, episodes=res)) + "\n")
