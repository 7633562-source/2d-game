"""jaw_film.py - day 2: the hinged jaw, 3 open / close cycles while the dog stands (policy holds the stand, v = 0).

usage: python jaw_film.py runs/<tag>/ckpt.zip [--out jaw_v2.mp4] [--json jaw_test.json]
The jaw is driven exactly as the game does it: DogStanceController.jawOpen 0 -> 1 -> 0 (target 0 / 35 deg),
DriveJaw / ControlJointStable hold, muscle 2.5 N m. Main view = the same camera as best_dog*.mp4, inset = head zoom."""
import argparse
import json
import math
import os
import subprocess
import sys

import mujoco
import numpy as np
from stable_baselines3 import PPO
from stable_baselines3.common.vec_env import DummyVecEnv, VecNormalize

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rlenv_dog  # noqa: E402
from eval_dog import FFMPEG, vecnorm_path  # noqa: E402

T_PRE, T_OPEN, T_CLOSE, N_CYC, T_POST = 0.8, 0.6, 0.6, 3, 0.8


def jaw_cmd(t):
    u = t - T_PRE
    if u < 0 or u >= N_CYC * (T_OPEN + T_CLOSE):
        return 0.0
    return 1.0 if (u % (T_OPEN + T_CLOSE)) < T_OPEN else 0.0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("path")
    ap.add_argument("--out", default=os.path.join(HERE, "jaw_v2.mp4"))
    ap.add_argument("--json", default=os.path.join(HERE, "jaw_test.json"))
    a = ap.parse_args()
    T = T_PRE + N_CYC * (T_OPEN + T_CLOSE) + T_POST
    env = rlenv_dog.DogTrot(rv=2, max_t=T + 0.1, dr=False, seed=7000, bkw=dict(tail_len=0.35, jaw_hinge=True, visuals=True))
    env.fixed_plan = [(0.0, 1e9)]
    vn = VecNormalize.load(vecnorm_path(a.path), DummyVecEnv([lambda: rlenv_dog.DogTrot()]))
    vn.training = False
    model = PPO.load(a.path, device="cpu")
    o, _ = env.reset(seed=7000)
    zoom = mujoco.Renderer(env.m, 240, 320)
    zc = mujoco.MjvCamera()
    zc.type = mujoco.mjtCamera.mjCAMERA_FREE
    zc.azimuth, zc.elevation, zc.distance = 90, -5, 0.30
    g_head = mujoco.mj_name2id(env.m, mujoco.mjtObj.mjOBJ_GEOM, "head_g")
    g_jaw = mujoco.mj_name2id(env.m, mujoco.mjtObj.mjOBJ_GEOM, "jaw_g")
    J = env.jaw
    rows, frames = [], []
    done = False
    while not done:
        env.jaw_open = jaw_cmd(env.t)
        act, _ = model.predict(vn.normalize_obs(o[None]), deterministic=True)
        o, r, te, tr, info = env.step(act[0])
        done = te or tr
        q = math.degrees(env.d.qpos[J["q"]])
        tau = float(env.d.ctrl[J["a"]] * env.m.actuator_gear[J["a"], 0])
        rows.append((round(env.t, 3), env.jaw_open, round(q, 2), round(tau, 3)))
        if env.k % 2 == 0:
            zc.lookat[:] = 0.5 * (env.d.geom_xpos[g_head] + env.d.geom_xpos[g_jaw])
            zoom.update_scene(env.d, zc)
            frames.append((env.render(), zoom.render(), env.t, env.jaw_open, q, tau))
    R = np.array(rows)
    # open / close timing per cycle: time from the command edge to 90 % of the swing
    cyc = []
    for c in range(N_CYC):
        t_on = T_PRE + c * (T_OPEN + T_CLOSE)
        t_off = t_on + T_OPEN
        seg_o = R[(R[:, 0] > t_on) & (R[:, 0] <= t_off)]
        seg_c = R[(R[:, 0] > t_off) & (R[:, 0] <= t_off + T_CLOSE)]
        t90o = next((x[0] - t_on for x in seg_o if x[2] >= 0.9 * 35.0), None)
        t90c = next((x[0] - t_off for x in seg_c if x[2] <= 0.1 * 35.0), None)
        cyc.append(dict(open_peak_deg=float(seg_o[:, 2].max()), t_open90_s=t90o, closed_min_deg=float(seg_c[-5:, 2].mean()),
                        t_close90_s=t90c))
    rest = R[R[:, 0] < T_PRE][:, 2]
    res = dict(policy=a.path, fell=bool(info.get("fell", False)), t=round(env.t, 2), cycles=cyc,
               closed_droop_stand_deg=round(float(rest[-10:].mean()), 2), tau_peak_nm=float(np.abs(R[:, 3]).max()))
    json.dump(res, open(a.json, "w"), indent=1)
    print(json.dumps(res))
    write(frames, a.out)


def write(frames, out):
    from PIL import Image, ImageDraw, ImageFont
    big = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 26)
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 22)
    small = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 18)
    tmp = out + "_frames"
    os.makedirs(tmp, exist_ok=True)
    for i, (img, zimg, t, cmd, q, tau) in enumerate(frames):
        im = Image.fromarray(img)
        dr = ImageDraw.Draw(im)
        dr.rectangle([0, 0, im.width, 92], fill=(0, 0, 0))
        dr.text((10, 2), "челюсть: открыть / закрыть", font=big, fill=(255, 255, 255))
        word = "ОТКРЫТЬ" if cmd > 0.5 else "закрыть"
        dr.text((10, 34), "время %.1f с   команда: %s   угол челюсти %.1f°" % (t, word, q), font=font,
                fill=(255, 255, 0))
        dr.text((10, 64), "собака стоит (политика держит стойку), 3 цикла, цель 35°", font=small, fill=(180, 255, 180))
        z = Image.fromarray(zimg)
        im.paste(z, (im.width - z.width - 8, 100))
        dr.rectangle([im.width - z.width - 8, 100, im.width - 8, 100 + z.height], outline=(255, 255, 255), width=2)
        dr.rectangle([im.width - z.width - 6, 102, im.width - z.width + 124, 126], fill=(0, 0, 0))
        dr.text((im.width - z.width, 104), "голова крупно", font=small, fill=(255, 255, 255))
        dr.rectangle([0, im.height - 30, im.width, im.height], fill=(0, 0, 0))
        dr.text((10, im.height - 26), "шарнир 0–40° (как в игре), мышца сейчас %+.1f Н·м (потолок 2.5), без контакта с землёй" % tau,
                font=small, fill=(255, 255, 255))
        im.save(os.path.join(tmp, "f%05d.png" % i))
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-framerate", "25", "-i", os.path.join(tmp, "f%05d.png"),
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", out], check=True)
    for f in os.listdir(tmp):
        os.remove(os.path.join(tmp, f))
    os.rmdir(tmp)


if __name__ == "__main__":
    main()
