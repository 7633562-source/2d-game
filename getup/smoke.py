"""smoke.py - random policy on HumanGetUp: reset distribution sanity (pelvis height / tilt / contacts per start type),
policy-step FPS, obs finiteness, and a contact sheet starts.png (Russian labels) of 12 settled starts.
usage: env -u PYTHONPATH <venv>/python.exe -I smoke.py
"""
import math
import os
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "rl-1024")))
import getupenv  # noqa: E402
from evalpol import stick  # noqa: E402  (the rl-1024 side-view drawer, read-only import)

env = getupenv.HumanGetUp(seed=1, dr=True)
rows = []
frames = []
t0 = time.time()
n_steps = 0
for ep in range(36):
    fr = ["back", "belly", "other", "mid", "proc_back", "proc_belly"][ep % 6]
    env.fixed_route = fr
    o, _ = env.reset()
    assert np.isfinite(o).all(), "non-finite obs at reset"
    heel, toe, hand, other, headc, kneec = env.cont
    rows.append((fr, env.start_kind, o[38], math.degrees(env.d.qpos[2]), math.degrees(env._torso_angle()),
                 sum(heel) + sum(toe), sum(hand), other, env.d.ncon))
    if ep < 12:
        frames.append((stick(env, 400, 260, 110.0), "%s / %s" % (fr, env.start_kind)))
    done = False
    while not done:
        a = env.action_space.sample()
        o, r, te, tr, info = env.step(a)
        n_steps += 1
        assert np.isfinite(o).all() and np.isfinite(r)
        done = te or tr
    rows[-1] += (round(info["pel_max"], 2), info["success"], round(info["assist_mean"], 4))
dt = time.time() - t0
print("random policy: %d policy steps in %.1f s = %.0f steps/s (one core, incl. resets with settle)" % (n_steps, dt, n_steps / dt))
print("%-11s %-10s %6s %6s %6s %4s %4s %5s %4s %7s %4s %6s" % ("start", "kind", "pelZ", "tilt", "torso", "feet", "hand", "other", "ncon", "pelMax", "ok", "assist"))
for r in rows:
    print("%-11s %-10s %6.2f %6.0f %6.0f %4d %4d %5s %4d %7.2f %4s %6.4f" % r)

# FPS without the reset cost
env.fixed_route = "belly"
o, _ = env.reset()
t0 = time.time()
k = 0
while time.time() - t0 < 5.0:
    o, r, te, tr, info = env.step(env.action_space.sample())
    k += 1
    if te or tr:
        env.reset()
print("steady-state FPS (random actions, belly starts): %.0f policy steps/s = %.0f physics steps/s" % (k / 5.0, k * 4 / 5.0))

from PIL import Image, ImageDraw, ImageFont  # noqa: E402
font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 14)
W, H = 400, 260
sheet = Image.new("RGB", (W * 4, H * 3 + 30), (255, 255, 255))
dr = ImageDraw.Draw(sheet)
dr.text((10, 6), "Стартовые позы после оседания (0,4 с): со спины / с живота / сидя (из падений Unity), промежуточные, случайные лёжа", font=font, fill=(0, 0, 0))
RU = {"back": "со спины", "belly": "с живота", "other": "сидя (Unity)", "mid": "промежуточная", "proc_back": "случайно, спина", "proc_belly": "случайно, живот"}
for i, (img, lab) in enumerate(frames):
    im = Image.fromarray(img)
    d2 = ImageDraw.Draw(im)
    fr, kind = lab.split(" / ")
    d2.text((6, 4), "%s: %s" % (RU[fr], kind), font=font, fill=(0, 0, 0))
    sheet.paste(im, ((i % 4) * W, 30 + (i // 4) * H))
sheet.save(os.path.join(HERE, "starts.png"))
print("wrote starts.png")
