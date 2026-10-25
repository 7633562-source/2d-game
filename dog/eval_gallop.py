"""eval_gallop.py - day 3: judge a fast-gait checkpoint (rv 3) on the order's line: stand 1 s -> ramp 1 m/s^2 to V ->
hold -> stop (ramp down) -> stand. Gains box2d (the Unity set) unless --gains trained.
Per episode: fell, hold speed (from 1 s after reaching V to the end of the hold), gait pattern in the hold (duty per paw,
flight fraction, pair agreement, touchdown phase of fl/rr/rl against fr), assist % BW (episode mean, the env's |tau|/0.40 m
measure), stop (from 1 s after v_cmd reaches 0: mean and max |vx|, 4-paw contact fraction).
success = no fall, |hold speed - V| <= 15 % of V, stands after the stop (mean |vx| < 0.05 m/s, 4 paws down >= 90 %).
usage: env -u PYTHONPATH <venv>/python.exe eval_gallop.py runs/g2/ckpt_N.zip [--v 2.5] [--eps 10] [--dr 0/1]
       [--video out.mp4] [--out results.jsonl]"""
import argparse
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.dont_write_bytecode = True
os.environ.setdefault("OMP_NUM_THREADS", "1")
import torch  # noqa: E402

torch.set_num_threads(1)
from stable_baselines3 import PPO  # noqa: E402
from stable_baselines3.common.vec_env import DummyVecEnv, VecNormalize  # noqa: E402

import eval_dog  # noqa: E402
import rlenv_dog as E  # noqa: E402

BKW = dict(tail_len=0.35, jaw_hinge=True)
NAMES = {"fpaw_r": "ПП", "fpaw_l": "ПЛ", "rpaw_r": "ЗП", "rpaw_l": "ЗЛ"}


def plan_for(v, acc, hold):
    return [(0.0, 1.0), (v, v / acc + hold), (0.0, 1e9)]


def td_phase(tds, ref):
    """median phase (0-1) of each touchdown in tds inside the fr stride that contains it."""
    out = []
    for t in tds:
        k = np.searchsorted(ref, t) - 1
        if 0 <= k < len(ref) - 1:
            out.append((t - ref[k]) / (ref[k + 1] - ref[k]))
    if not out:
        return None
    a = 2 * np.pi * np.array(out)   # circular mean
    return round(float((math.atan2(np.mean(np.sin(a)), np.mean(np.cos(a))) / (2 * np.pi)) % 1.0), 3)


def episode_metrics(env, info, v, acc, hold):
    vl = np.array(env.ep["vlog"])
    fl = np.array(env.ep["flags"])
    t, vx, x = vl[:, 0], vl[:, 2], vl[:, 3]
    h0, h1 = 1.0 + v / acc + 1.0, 1.0 + v / acc + hold
    s0 = h1 + v / acc + 1.0
    fell = bool(info.get("fell", False))
    out = dict(t=round(env.t, 2), fell=fell, assist_bw_pct=round(100 * info.get("assist_bw", 0.0), 2))
    w = (t >= h0) & (t <= h1)
    if w.sum() > 20:
        out["speed_hold"] = round(float((x[w][-1] - x[w][0]) / (t[w][-1] - t[w][0])), 3)
        f = fl[w]
        out["duty"] = [round(float(q), 2) for q in f.mean(0)]
        out["flight_frac"] = round(float((f.sum(1) == 0).mean()), 3)
        out["diag_agree"] = round(float(0.5 * ((f[:, 0] == f[:, 3]).mean() + (f[:, 1] == f[:, 2]).mean())), 3)
        out["front_pair_agree"] = round(float((f[:, 0] == f[:, 1]).mean()), 3)
        out["rear_pair_agree"] = round(float((f[:, 2] == f[:, 3]).mean()), 3)
        tds = {p: np.array([a for a in env.ep["td"][p] if h0 <= a <= h1]) for p in E.PAWS}
        ref = tds["fpaw_r"]
        out["stride_s"] = round(float(np.median(np.diff(ref))), 3) if len(ref) > 2 else None
        out["td_phase_vs_fr"] = {E.LEG_OF[p]: td_phase(tds[p], ref) for p in ("fpaw_l", "rpaw_r", "rpaw_l")}
    else:
        out.update(speed_hold=0.0)
    ws = t >= s0
    if not fell and ws.sum() > 20:
        out["stop_mean_abs_vx"] = round(float(np.mean(np.abs(vx[ws]))), 3)
        out["stop_max_abs_vx"] = round(float(np.max(np.abs(vx[ws]))), 3)
        out["stop_4paw_frac"] = round(float((fl[ws].sum(1) == 4).mean()), 3)
        out["stood"] = bool(out["stop_mean_abs_vx"] < 0.05 and out["stop_4paw_frac"] >= 0.9)
    else:
        out["stood"] = False
    out["success"] = bool(not fell and abs(out["speed_hold"] - v) <= 0.15 * v and out["stood"])
    return out


def classify(m):
    """coarse gait name from the hold-window pattern."""
    if "td_phase_vs_fr" not in m or m["td_phase_vs_fr"]["fl"] is None:
        return "-"
    p = m["td_phase_vs_fr"]

    def near(a, b, tol=0.15):
        return a is not None and min(abs(a - b), 1 - abs(a - b)) <= tol
    if near(p["fl"], 0.5) and near(p["rl"], 0.0) and near(p["rr"], 0.5):
        return "trot"
    if near(p["fl"], 0.0) and near(p["rr"], p["rl"] if p["rl"] is not None else -9, 0.1):
        return "bound"
    if near(p["fl"], 0.0, 0.25) and p["rr"] is not None and p["rl"] is not None and not near(p["rr"], p["rl"], 0.03):
        return "gallop"
    return "other"


def run(path, v=2.5, eps=10, dr=False, hold=6.0, gains="box2d", video=None, seed0=7000, video_ep=0, title=None, rv=3,
        ptrained=False):
    acc = E.REWARD[rv]["acc"]
    max_t = 1.0 + 2 * v / acc + hold + 3.0
    env = E.DogTrot(rv=rv, max_t=max_t, dr=dr, seed=seed0, bkw=BKW, gains=gains)
    if ptrained:   # day 4: the passive gains g2-g5 were trained with (the pre-fix init order), not the Box2D-safe ones
        env.pk, env.pc = np.array(E.GAINS["trained"]["pk"], float), np.array(E.GAINS["trained"]["pc"], float)
    env.cam_dist = 2.6
    env.fixed_plan = plan_for(v, acc, hold)
    venv = VecNormalize.load(eval_dog.vecnorm_path(path), DummyVecEnv([lambda: E.DogTrot(rv=rv)]))
    venv.training = False
    model = PPO.load(path, device="cpu")
    res, frames = [], []
    for ep in range(eps):
        o, _ = env.reset(seed=seed0 + ep)
        done, info = False, {}
        while not done:
            a, _ = model.predict(venv.normalize_obs(o[None]), deterministic=True)
            o, r, te, tr, info = env.step(a[0])
            done = te or tr
            if video and ep == video_ep and env.k % 2 == 0:
                vl = env.ep["vlog"][-1]
                frames.append((env.render(), env.t, vl[3], env.v_cmd, vl[2], dict(env.cont)))
        m = episode_metrics(env, info, v, acc, hold)
        m["gait"] = classify(m)
        res.append(m)
    ok = [r for r in res if not r["fell"]]
    summ = dict(path=path, rv=rv, v=v, dr=dr, gains=gains, ptrained=bool(ptrained), eps=eps, success=sum(r["success"] for r in res),
                falls=sum(r["fell"] for r in res), stood=sum(r["stood"] for r in res),
                speed_hold_mean=round(float(np.mean([r["speed_hold"] for r in ok])), 3) if ok else 0.0,
                assist_bw_pct_mean=round(float(np.mean([r["assist_bw_pct"] for r in res])), 2),
                flight_frac_mean=round(float(np.mean([r.get("flight_frac", 0) for r in ok])), 3) if ok else 0.0,
                gaits={g: sum(r["gait"] == g for r in res) for g in sorted(set(r["gait"] for r in res))})
    if video and frames:
        write_video(frames, video, res[video_ep], v, hold, title)
    return summ, res


def write_video(frames, out, r, v, hold, title=None):
    from PIL import Image, ImageDraw, ImageFont
    import subprocess
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 22)
    small = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 18)
    tmp = out + "_frames"
    os.makedirs(tmp, exist_ok=True)
    title = title or ("Собака-двойник MuJoCo, быстрый аллюр (PPO, усиления для Unity): стойка 1 с, разгон 1 м/с² до "
                      "%.1f м/с, %.0f с, стоп, стойка" % (v, hold))
    gait_ru = {"trot": "рысь", "bound": "прыжки (баунд)", "gallop": "галоп", "other": "смешанный", "-": "-"}
    for i, (img, t, x, vc, vx, cont) in enumerate(frames):
        im = Image.fromarray(img)
        dr = ImageDraw.Draw(im)
        dr.rectangle([0, 0, im.width, 92], fill=(0, 0, 0))
        dr.text((10, 4), title, font=small, fill=(255, 255, 255))
        dr.text((10, 30), "время %.1f с   пройдено %.2f м   команда %.2f м/с   скорость %.2f м/с" % (t, x, vc, vx),
                font=font, fill=(255, 255, 0))
        on = "  ".join("%s:%s" % (NAMES[p], "земля" if cont[p] else "воздух") for p in E.PAWS)
        dr.text((10, 60), "лапы  " + on, font=small, fill=(180, 255, 180))
        dr.rectangle([0, im.height - 30, im.width, im.height], fill=(0, 0, 0))
        dr.text((10, im.height - 26), "итог: скорость на участке %.2f м/с, аллюр: %s, фаза полёта %.0f %%, помощь %.1f %% веса, "
                "после стопа %s" % (r["speed_hold"], gait_ru.get(r["gait"], r["gait"]), 100 * r.get("flight_frac", 0),
                                    r["assist_bw_pct"], "стоит" if r["stood"] else "не стоит"), font=small, fill=(255, 255, 255))
        im.save(os.path.join(tmp, "f%05d.png" % i))
    subprocess.run([eval_dog.FFMPEG, "-y", "-loglevel", "error", "-framerate", "25", "-i", os.path.join(tmp, "f%05d.png"),
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", out], check=True)
    for f in os.listdir(tmp):
        os.remove(os.path.join(tmp, f))
    os.rmdir(tmp)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("path")
    ap.add_argument("--v", type=float, default=2.5)
    ap.add_argument("--eps", type=int, default=10)
    ap.add_argument("--dr", type=int, default=0)
    ap.add_argument("--hold", type=float, default=6.0)
    ap.add_argument("--gains", default="box2d")
    ap.add_argument("--video", default="")
    ap.add_argument("--video_ep", type=int, default=0)
    ap.add_argument("--out", default="")
    ap.add_argument("--rv", type=int, default=3)
    ap.add_argument("--title", default="")
    ap.add_argument("--ptrained", type=int, default=0)
    a = ap.parse_args()
    s, res = run(a.path, v=a.v, eps=a.eps, dr=bool(a.dr), hold=a.hold, gains=a.gains, video=a.video or None,
                 video_ep=a.video_ep, rv=a.rv, title=a.title or None, ptrained=bool(a.ptrained))
    print(json.dumps(s), flush=True)
    for r in res:
        print(json.dumps(r))
    if a.out:
        with open(a.out, "a") as f:
            f.write(json.dumps(dict(summary=s, eps=res)) + "\n")
