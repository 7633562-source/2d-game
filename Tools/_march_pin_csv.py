# One-off: march_pin vs march_air lift windows.
import csv
from pathlib import Path

ROOT = Path(r"E:/Games/2D Game/My project/Trials/Runs")
WINDOWS = {
    "lift0_right": (5.00, 7.55, True),
    "plant0": (7.50, 9.05, None),
    "lift1_left": (9.00, 11.55, False),
}


def load(name):
    path = ROOT / f"{name}.csv"
    rows = []
    with path.open(encoding="utf-8") as f:
        r = csv.DictReader(f, delimiter=";")
        for row in r:
            rec = {k: float(row[k]) for k in row}
            rec["state"] = int(rec["state"])
            rows.append(rec)
    return rows


def first_air(rows, t0, t1, swing_right):
    g = "rightGrounded" if swing_right else "leftGrounded"
    for r in rows:
        if t0 - 1e-9 <= r["time"] <= t1 + 1e-9 and r[g] < 0.5:
            return r["time"]
    return None


def dump_around(rows, t_mid, swing_right, pre=0.20, post=0.80, step=8):
    g = "rightGrounded" if swing_right else "leftGrounded"
    sg = "leftGrounded" if swing_right else "rightGrounded"
    hip = "hipR" if swing_right else "hipL"
    knee = "kneeR" if swing_right else "kneeL"
    act_hip = "actHipR" if swing_right else "actHipL"
    act_st = "actAnkleL" if swing_right else "actAnkleR"
    act_sw = "actAnkleR" if swing_right else "actAnkleL"
    print("  t st swG stG hip knee aHip aStA aSwA comX comOff pelvis state")
    n = 0
    for r in rows:
        if r["time"] < t_mid - pre or r["time"] > t_mid + post:
            continue
        n += 1
        if n % step != 1 and abs(r["time"] - t_mid) > 0.002:
            continue
        print(
            f"  {r['time']:.3f} {r['state']:.0f} {r[g]:.0f} {r[sg]:.0f} "
            f"{r[hip]:7.1f} {r[knee]:6.1f} {r[act_hip]:5.2f} {r[act_st]:5.2f} "
            f"{r[act_sw]:5.2f} {r['comX']:7.3f} {r['comOffset']:7.3f} "
            f"{r['pelvisTilt']:7.1f}"
        )


for name in ("march_pin", "march_air"):
    path = ROOT / f"{name}.csv"
    if not path.exists():
        print(name, "NO CSV")
        continue
    rows = load(name)
    print("====", name, "====")
    print(
        f"  t0 comX={rows[0]['comX']:.3f}  t_end comX={rows[-1]['comX']:.3f} "
        f"fell_row state2 first=",
        next((r["time"] for r in rows if r["state"] >= 2), None),
    )
    for key, (a, b, swing_right) in WINDOWS.items():
        win = [r for r in rows if a - 1e-9 <= r["time"] <= b + 1e-9]
        if not win:
            print(key, "empty")
            continue
        print(
            f"{key} comX {win[0]['comX']:.3f}->{win[-1]['comX']:.3f} "
            f"d={win[-1]['comX']-win[0]['comX']:.3f} "
            f"pelvis {win[0]['pelvisTilt']:.1f}->{win[-1]['pelvisTilt']:.1f} "
            f"off {win[0]['comOffset']:.3f}->{win[-1]['comOffset']:.3f}"
        )
        if swing_right is None:
            continue
        fa = first_air(rows, a, b, swing_right)
        print(f"  first_air={fa}")
        if fa is not None:
            dump_around(rows, fa, swing_right)
    print()
