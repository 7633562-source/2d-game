"""still_v2.py - day 2 still: side view at stand, day-1 twin (left) vs day-2 twin (right), Russian labels.

usage: python still_v2.py [policy.zip] -> still_v2.png. The dog stands 1.0 s under the policy (v = 0) before the shot,
so the tail hangs as the springs and gravity leave it."""
import math
import os
import sys

import mujoco
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from stable_baselines3 import PPO
from stable_baselines3.common.vec_env import DummyVecEnv, VecNormalize

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rlenv_dog  # noqa: E402
from eval_dog import vecnorm_path  # noqa: E402

W, H = 900, 640


def project(scene, p):
    c = scene.camera[0]
    pos, fw, up = np.array(c.pos), np.array(c.forward), np.array(c.up)
    right = np.cross(fw, up)
    v = np.asarray(p) - pos
    zc = v @ fw
    xn, yn = (v @ right) / zc * c.frustum_near, (v @ up) / zc * c.frustum_near
    hw = c.frustum_width if c.frustum_width > 0 else (c.frustum_top - c.frustum_bottom) / 2 * W / H
    px = (xn - (c.frustum_center - hw)) / (2 * hw) * W
    py = (c.frustum_top - yn) / (c.frustum_top - c.frustum_bottom) * H
    return px, py


def tail_droop(m, d):
    g0 = mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_GEOM, "tail0_g")
    g4 = mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_GEOM, "tail4_g")
    R0, R4 = d.geom_xmat[g0].reshape(3, 3), d.geom_xmat[g4].reshape(3, 3)
    root = d.geom_xpos[g0] + R0[:, 0] * m.geom_size[g0][0]
    tip = d.geom_xpos[g4] - R4[:, 0] * m.geom_size[g4][0]
    return math.degrees(math.atan2(root[2] - tip[2], root[0] - tip[0]))


def pick_time(policy, bkw, t_end=6.0):
    """the tail swings (springs, no damper besides joint friction, as in the game): shoot when the tip angle is
    closest to its 2-6 s mean. Returns (t, mean, min, max)."""
    env = rlenv_dog.DogTrot(rv=2, max_t=t_end + 0.1, seed=7000, pose_noise=0.0, bkw=bkw)
    env.fixed_plan = [(0.0, 1e9)]
    vn = VecNormalize.load(vecnorm_path(policy), DummyVecEnv([lambda: rlenv_dog.DogTrot()]))
    vn.training = False
    model = PPO.load(policy, device="cpu")
    o, _ = env.reset(seed=7000)
    A = []
    while env.t < t_end - 1e-9:
        a, _ = model.predict(vn.normalize_obs(o[None]), deterministic=True)
        o, *_ = env.step(a[0])
        A.append((env.t, tail_droop(env.m, env.d)))
    A = np.array(A)
    w = A[A[:, 0] >= 2.0]
    mu = float(w[:, 1].mean())
    t = float(w[np.argmin(np.abs(w[:, 1] - mu)), 0])
    return t, mu, float(w[:, 1].min()), float(w[:, 1].max())


def shot(policy, bkw, stand_s=1.0):
    env = rlenv_dog.DogTrot(rv=2, max_t=stand_s + 1.0, seed=7000, pose_noise=0.0, bkw=bkw)
    env.fixed_plan = [(0.0, 1e9)]
    vn = VecNormalize.load(vecnorm_path(policy), DummyVecEnv([lambda: rlenv_dog.DogTrot()]))
    vn.training = False
    model = PPO.load(policy, device="cpu")
    o, _ = env.reset(seed=7000)
    while env.t < stand_s - 1e-9:
        a, _ = model.predict(vn.normalize_obs(o[None]), deterministic=True)
        o, *_ = env.step(a[0])
    r = mujoco.Renderer(env.m, H, W)
    cam = mujoco.MjvCamera()
    cam.type = mujoco.mjtCamera.mjCAMERA_FREE
    cam.azimuth, cam.elevation, cam.distance = 90, -5, 1.45
    cam.lookat[:] = [env.d.subtree_com[1][0] - 0.12, 0, 0.27]
    r.update_scene(env.d, cam)
    img = r.render()
    pts = {}
    m, d = env.m, env.d
    for g in ("jaw_g", "head_g", "thigh_rr_g", "pelvis_g", "tail4_g", "tail0_g", "hip_bridge_v", "shoulder_bridge_v",
              "upper_fr_g"):
        gid = mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_GEOM, g)
        if gid >= 0:
            pts[g] = project(r.scene, d.geom_xpos[gid])
    # tail tip: far end of tail4; tail root: near end of tail0
    for g, sgn, key in (("tail4_g", -1, "tail_tip"), ("tail0_g", 1, "tail_root")):
        gid = mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_GEOM, g)
        R = d.geom_xmat[gid].reshape(3, 3)
        pts[key] = project(r.scene, d.geom_xpos[gid] + R[:, 0] * sgn * m.geom_size[gid][0])
    jid = mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_BODY, "thigh_rr")
    pts["hip"] = project(r.scene, d.xpos[jid])
    pts["pelvis_bottom"] = project(r.scene, d.geom_xpos[mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_GEOM, "pelvis_g")]
                                   + np.array([0, 0, -m.geom_size[mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_GEOM, "pelvis_g")][2]]))
    return img, pts, tail_droop(m, d)


def arrow(dr, xy0, xy1, col, w=3):
    dr.line([xy0, xy1], fill=col, width=w)
    x0, y0 = xy0
    x1, y1 = xy1
    a = math.atan2(y1 - y0, x1 - x0)
    for s in (-1, 1):
        dr.line([xy1, (x1 - 14 * math.cos(a + s * 0.45), y1 - 14 * math.sin(a + s * 0.45))], fill=col, width=w)


def main():
    pol = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "runs/d3/ckpt_3129344.zip")
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, "still_v2.png")
    big = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 26)
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 20)
    small = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 17)
    ka, kb = dict(tail_len=0.20, jaw_hinge=False, visuals=False), dict(tail_len=0.35, jaw_hinge=True, visuals=True)
    ta, *sa = pick_time(pol, ka)
    tb, *sb = pick_time(pol, kb)
    a_img, a_pts, a_droop = shot(pol, ka, ta)
    b_img, b_pts, b_droop = shot(pol, kb, tb)
    print("tail tip below horizontal (shot t, mean, min, max): 20 cm %.2f s %s, 35 cm %.2f s %s"
          % (ta, [round(x, 1) for x in sa], tb, [round(x, 1) for x in sb]))
    canvas = Image.new("RGB", (2 * W + 20, H + 160), (255, 255, 255))
    canvas.paste(Image.fromarray(a_img), (0, 100))
    canvas.paste(Image.fromarray(b_img), (W + 20, 100))
    dr = ImageDraw.Draw(canvas)
    dr.text((10, 8), "Собака-двойник MuJoCo в стойке: было (день 1) и стало (день 2), вид сбоку", font=big, fill=(0, 0, 0))
    dr.text((10, 48), "было: хвост 20 см, щель между тазом и бедром, челюсть спрятана в голове", font=font, fill=(90, 90, 90))
    dr.text((W + 30, 48), "стало: хвост 35 см, мостик таз–бедро, челюсть на шарнире", font=font, fill=(0, 0, 0))
    red, blue, green, mag = (210, 30, 30), (20, 70, 200), (0, 130, 40), (150, 0, 150)

    def off(p, dx):
        return (p[0] + dx, p[1] + 100)

    def label(xy, text, col, target, f=small):
        x, y = xy
        bb = dr.multiline_textbbox((x, y), text, font=f, spacing=3)
        tx, ty = target
        ax = min(max(tx, bb[0]), bb[2])
        ay = bb[3] + 4 if ty > bb[3] else bb[1] - 4
        arrow(dr, (ax, ay), (tx, ty), col)
        dr.rectangle([bb[0] - 6, bb[1] - 4, bb[2] + 6, bb[3] + 4], fill=(255, 255, 255), outline=col, width=2)
        dr.multiline_text((x, y), text, font=f, fill=col, spacing=3)

    for P, X0, tail_txt, hip_txt, jaw_txt, _dr in (
            (a_pts, 0, "хвост 20 см", "щель: шарнир бедра на 4.3 см|ниже коробки таза",
             "челюсть не видна|(внутри коробки головы)", a_droop),
            (b_pts, W + 20, "хвост 35 см", "мостик таз–бедро: только картинка,|как HipBridge в игре; физика та же",
             "челюсть: шарнир 0–40°,|мышца 2.5 Н·м (как в игре)", b_droop)):
        o = lambda k: (P[k][0] + X0, P[k][1] + 100)
        hip_txt, jaw_txt = hip_txt.replace("|", chr(10)), jaw_txt.replace("|", chr(10))
        st = sa if X0 == 0 else sb
        label((X0 + 40, 130), (tail_txt + "|конец ниже горизонтали:|в среднем %.0f°, качается %.0f–%.0f°" % tuple(st))
              .replace("|", chr(10)), blue, o("tail_tip"), f=font)
        label((X0 + 40, 500), hip_txt, red if X0 == 0 else green, o("hip"))
        label((X0 + 600, 500), jaw_txt, mag, o("jaw_g"))
    label((W + 20 + 430, 150), "мостик плеча (как в игре)", green,
          (b_pts["shoulder_bridge_v"][0] + W + 20, b_pts["shoulder_bridge_v"][1] + 100))
    dr.text((10, H + 108), "Туловище, лапы и контакты не менялись; хвост: та же масса (2 % тела), тот же конус и пружина 2 Н·м/рад"
            " (без демпфера, как в игре, поэтому хвост качается).", font=small, fill=(60, 60, 60))
    dr.text((10, H + 130), "Кадр снят в момент, когда хвост у своего среднего угла (стойка 2–6 с). Модель: dog2d_tail35.xml.",
            font=small, fill=(60, 60, 60))
    canvas.save(out)
    print("wrote", out, {k: (round(v[0]), round(v[1])) for k, v in b_pts.items()})


if __name__ == "__main__":
    main()
