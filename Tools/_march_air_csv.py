# One-off: first march lift windows from trial CSVs.
import csv
from pathlib import Path

ROOT = Path(r"E:/Games/2D Game/My project/Trials/Runs")
# Clock: t=5 start, hold 2.5, plant 1.5, first = left support (standLeg -1).
# Right swing 5.0–7.5, plant 7.5–9.0, left swing 9.0–11.5.
WINDOWS = {
    "lift0_right": (5.00, 7.55),
    "plant0": (7.50, 9.05),
    "lift1_left": (9.00, 11.55),
}


def load(name):
    path = ROOT / f"{name}.csv"
    rows = []
    with path.open(encoding="utf-8") as f:
        r = csv.DictReader(f, delimiter=";")
        for row in r:
            t = float(row["time"])
            rows.append({k: float(row[k]) if k != "state" else int(float(row[k])) for k in row})
            rows[-1]["time"] = t
    return rows


def summarize(rows, t0, t1, swing_right):
    win = [r for r in rows if t0 - 1e-6 <= r["time"] <= t1 + 1e-6]
    if not win:
        return "empty"
    g_key = "rightGrounded" if swing_right else "leftGrounded"
    hip_key = "hipR" if swing_right else "hipL"
    knee_key = "kneeR" if swing_right else "kneeL"
    act_hip = "actHipR" if swing_right else "actHipL"
    stance_ank = "actAnkleL" if swing_right else "actAnkleR"
    first_air = next((r["time"] for r in win if r[g_key] < 0.5), None)
    last_air = next((r["time"] for r in reversed(win) if r[g_key] < 0.5), None)
    air = [r for r in win if r[g_key] < 0.5]
    air_s = len(air) * 0.005
    knee_air = [r[knee_key] for r in air]
    hip_air = [r[hip_key] for r in air]
    # Hip still extending (plus) while commanded flex is minus.
    lines = []
    lines.append(f"  n={len(win)} air={air_s:.3f}s first_air={first_air} last_air={last_air}")
    if air:
        lines.append(
            f"  swing hip air min/mean {min(hip_air):.1f}/{sum(hip_air)/len(hip_air):.1f}  "
            f"knee min/max/mean {min(knee_air):.1f}/{max(knee_air):.1f}/{sum(knee_air)/len(knee_air):.1f}"
        )
        lines.append(
            f"  comX {win[0]['comX']:.3f}->{win[-1]['comX']:.3f}  "
            f"pelvis {win[0]['pelvisTilt']:.1f}->{win[-1]['pelvisTilt']:.1f}"
        )
    # Trace around first air ±0.4 s
    if first_air is not None:
        around = [r for r in rows if first_air - 0.15 <= r["time"] <= first_air + 0.80]
        lines.append("  t  swG stG hipK knee actHip actStAnk comOff pelvis")
        step = max(1, len(around) // 20)
        for i, r in enumerate(around):
            if i % step != 0 and r["time"] < first_air + 0.79:
                continue
            swg = r["rightGrounded"] if swing_right else r["leftGrounded"]
            stg = r["leftGrounded"] if swing_right else r["rightGrounded"]
            lines.append(
                f"  {r['time']:.3f} {swg:.0f} {stg:.0f} "
                f"{r[hip_key]:6.1f} {r[knee_key]:6.1f} "
                f"{r[act_hip]:.2f} {r[stance_ank]:.2f} "
                f"{r['comOffset']:7.3f} {r['pelvisTilt']:6.1f}"
            )
    return "\n".join(lines)


def age_when_knee(rows, t0, t1, swing_right, knee_th=60.0):
    g_key = "rightGrounded" if swing_right else "leftGrounded"
    knee_key = "kneeR" if swing_right else "kneeL"
    hip_key = "hipR" if swing_right else "hipL"
    first_air = next((r["time"] for r in rows if t0 <= r["time"] <= t1 and r[g_key] < 0.5), None)
    if first_air is None:
        return None
    for r in rows:
        if r["time"] < first_air:
            continue
        if r["time"] > t1:
            break
        if r[g_key] >= 0.5:
            continue
        if r[knee_key] >= knee_th and r[hip_key] < -40:
            return r["time"] - first_air, r["time"], r[knee_key], r[hip_key]
    return None


for name in ("march_air", "march_ank", "march_hold"):
    path = ROOT / f"{name}.csv"
    if not path.exists():
        print(name, "NO CSV")
        continue
    rows = load(name)
    print("====", name, "====")
    for key, (a, b) in WINDOWS.items():
        swing_right = key.startswith("lift0")
        if key.startswith("plant"):
            print(key, f"{a}-{b}")
            win = [r for r in rows if a <= r["time"] <= b]
            if win:
                print(
                    f"  comX {win[0]['comX']:.3f}->{win[-1]['comX']:.3f} "
                    f"d={win[-1]['comX']-win[0]['comX']:.3f} "
                    f"pelvis {win[-1]['pelvisTilt']:.1f}"
                )
            continue
        print(key, f"{a}-{b}")
        print(summarize(rows, a, b, swing_right))
        hit = age_when_knee(rows, a, b, swing_right)
        print("  age to knee>=60 & hip<-40:", hit)
    print()
