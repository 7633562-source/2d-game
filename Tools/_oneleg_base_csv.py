# One-shot hold-window metrics for oneleg_base. Not a project tool.
import csv
from pathlib import Path

p = Path(r"E:/Games/2D Game/My project/Trials/Runs/oneleg_base.csv")
rows = []
with p.open(encoding="utf-8") as f:
    for r in csv.DictReader(f, delimiter=";"):
        rows.append({k: float(r[k]) for k in r})

# Hold: lift at 5 s, release 17 s. Use after swing is up (leftGrounded < 0.5).
hold = [r for r in rows if 6.0 <= r["time"] <= 17.0]
air = [r for r in hold if r["leftGrounded"] < 0.5]
win = air if air else hold

pelv = [r["pelvisTilt"] for r in win]
torso = [r["torsoTilt"] for r in win]
comx = [r["comX"] for r in win]
off = [r["comOffset"] for r in win]
dt = 0.005

span = max(pelv) - min(pelv)
# zero-crossings of (pelvis - mean) for frequency
mean_p = sum(pelv) / len(pelv)
zc = 0
prev = pelv[0] - mean_p
for x in pelv[1:]:
    d = x - mean_p
    if prev <= 0 < d or prev >= 0 > d:
        zc += 1
    prev = d
dur = win[-1]["time"] - win[0]["time"]
freq = (zc / 2.0) / dur if dur > 0 else 0

# pelvis angular speed rms from finite diff
speeds = []
for i in range(1, len(win)):
    speeds.append((pelv[i] - pelv[i - 1]) / dt)
rms_spd = (sum(s * s for s in speeds) / len(speeds)) ** 0.5 if speeds else 0

drift = max(comx) - min(comx)
print("n", len(win), "t", f"{win[0]['time']:.3f}-{win[-1]['time']:.3f}")
print("pelvis min/max/span/mean", f"{min(pelv):.3f}", f"{max(pelv):.3f}", f"{span:.3f}", f"{mean_p:.3f}")
print("torso min/max/span", f"{min(torso):.3f}", f"{max(torso):.3f}", f"{max(torso)-min(torso):.3f}")
print("zc", zc, "freq_hz", f"{freq:.3f}", "rms_pelvis_dps", f"{rms_spd:.3f}")
print("comX min/max/drift_m", f"{min(comx):.4f}", f"{max(comx):.4f}", f"{drift:.4f}")
print("offset min/max/rms", f"{min(off):.4f}", f"{max(off):.4f}",
      f"{(sum(x*x for x in off)/len(off))**0.5:.4f}")
print("final pelv/comX", f"{win[-1]['pelvisTilt']:.3f}", f"{win[-1]['comX']:.4f}")
