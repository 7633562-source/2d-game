# One-shot CSV slice for walklift_unload. Not a project tool.
import csv
from pathlib import Path

p = Path(r"E:/Games/2D Game/My project/Trials/Runs/walklift_unload.csv")
rows = []
with p.open(encoding="utf-8") as f:
    for r in csv.DictReader(f, delimiter=";"):
        rows.append({k: float(r[k]) for k in r})

# First walk window 5.0 .. 10.0 (fall ~9.87)
win = [r for r in rows if 5.0 <= r["time"] <= 10.0]

def air_spans(key):
    spans = []
    start = None
    for r in win:
        air = r[key] < 0.5
        if air and start is None:
            start = r
        elif not air and start is not None:
            spans.append((start["time"], r["time"], r["time"] - start["time"]))
            start = None
    if start is not None:
        spans.append((start["time"], win[-1]["time"], win[-1]["time"] - start["time"]))
    return spans

print("samples in 5-10s", len(win))
print("left air spans", air_spans("leftGrounded")[:8], "n=", len(air_spans("leftGrounded")))
print("right air spans", air_spans("rightGrounded")[:8], "n=", len(air_spans("rightGrounded")))

# swaps
swaps = []
prev = -1
for r in win:
    s = int(r["walkSwap"])
    if s != prev:
        if s > prev:
            swaps.append(r)
        prev = s
print("swap events", [(r["time"], int(r["walkSwap"]), int(r["walkPhase"]), r["standLegCmd"],
                       r["leftGrounded"], r["rightGrounded"], r["torsoTilt"], r["kneeL"], r["kneeR"],
                       r["pelvisY"], r["comOffset"]) for r in swaps])

# 50 ms samples around first lift (left or right goes 1->0 after 5.0)
print("\n--- first 1.2s of walk, every 40 ms ---")
t0 = 5.0
while t0 <= 6.4:
    r = min(win, key=lambda x: abs(x["time"] - t0))
    print(f"{r['time']:.3f} ph={int(r['walkPhase'])} sw={int(r['walkSwap'])} cmd={r['standLegCmd']:.0f} "
          f"g={int(r['leftGrounded'])}/{int(r['rightGrounded'])} "
          f"knee={r['kneeL']:.1f}/{r['kneeR']:.1f} hip={r['hipL']:.1f}/{r['hipR']:.1f} "
          f"ank={r['ankleL']:.1f}/{r['ankleR']:.1f} actA={r['actAnkleL']:.2f}/{r['actAnkleR']:.2f} "
          f"tilt={r['torsoTilt']:.1f} pelY={r['pelvisY']:.3f} off={r['comOffset']:.3f}")
    t0 += 0.04

print("\n--- around each swap +/- 0.15s every 50ms ---")
for ev in swaps:
    t = ev["time"]
    tt = t - 0.15
    print(f"\n# swap {int(ev['walkSwap'])} at {t:.3f}")
    while tt <= t + 0.25:
        r = min(win, key=lambda x: abs(x["time"] - tt))
        print(f"  {r['time']:.3f} ph={int(r['walkPhase'])} cmd={r['standLegCmd']:.0f} "
              f"g={int(r['leftGrounded'])}/{int(r['rightGrounded'])} "
              f"knee={r['kneeL']:.1f}/{r['kneeR']:.1f} hip={r['hipL']:.1f}/{r['hipR']:.1f} "
              f"actA={r['actAnkleL']:.2f}/{r['actAnkleR']:.2f} "
              f"tilt={r['torsoTilt']:.1f} off={r['comOffset']:.3f} pelY={r['pelvisY']:.3f}")
        tt += 0.05
