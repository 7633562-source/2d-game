# Candidate push-back definition, evaluated OFFLINE on the already accepted grid runs.
# No build, no physics: this decides the definition before any code is written.
#
# Definition under test: the blow loads the ground through the body during the collision
# (the first ~100 ms here, as a conservative proxy for the projectile's contact window);
# the BODY's push-back is a 40 ms sliding mean of fzTotal that exceeds body weight by 5 %,
# measured only after that proxy window. The stand's own idle oscillation is +-0.05 N
# (0.007 % of weight, measured pre-impact), so 5 % is ~500x the idle noise.
import csv, json, os, sys

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
PROXY_MS = 100      # proxy for the passive window (the recorder measures it fixed)
MEAN_MS = 40
THRESHOLD = 1.05

def load(label):
    rows = []
    with open(os.path.join(RUNS, label + ".csv"), newline="", encoding="utf-8-sig") as fh:
        for r in csv.DictReader(fh, delimiter=";"):
            try:
                rows.append((float(r["time"]), float(r["fzTotal"]), float(r["wSup"])))
            except (KeyError, ValueError):
                pass
    return rows

def contact(label):
    p = os.path.join(RUNS, label + "-impacts.json")
    if not os.path.exists(p):
        return None
    d = json.load(open(p, encoding="utf-8-sig"))
    ts = d.get("throws") or []
    return ts[0].get("contactTime") if ts else None

def analyse(label, t0):
    rows = load(label)
    w = max((r[2] for r in rows), default=0.0)
    if not rows or w <= 0 or t0 is None:
        return None
    dt = 0.005
    k = int(round((MEAN_MS / 1000.0) / dt))
    seg = [r for r in rows if (r[0] - t0) * 1000 >= PROXY_MS]
    fz = [r[1] for r in seg]
    if len(fz) < k:
        return None
    means = [sum(fz[i:i + k]) / k for i in range(len(fz) - k + 1)]
    sustained = [i for i, m in enumerate(means) if m > THRESHOLD * w]
    excess = 0.0
    for i in sustained:
        excess += sum((v - w) for v in fz[i:i + k]) * dt / k
    longest = 0
    run = 0
    for i in range(len(means)):
        if means[i] > THRESHOLD * w:
            run += 1
            longest = max(longest, run)
        else:
            run = 0
    peak = max(means)
    peak_ms = (means.index(peak) + k / 2) * dt * 1000 + PROXY_MS
    first = (sustained[0] + k / 2) * dt * 1000 + PROXY_MS if sustained else None
    return dict(peak_ratio=peak / w, peak_ms=peak_ms, push=bool(sustained),
                first_ms=first, hold_ms=longest * dt * 1000, excess_ns=excess)

if __name__ == "__main__":
    args = sys.argv[1:]
    if args and args[0].isdigit():
        PROXY_MS = int(args.pop(0))
    print(f"definition: {MEAN_MS} ms sliding mean of fzTotal > {THRESHOLD} * bodyWeight, "
          f"only after contact + {PROXY_MS} ms")
    print(f"{'label':22s} {'push':5s} {'peak fz/W':>9s} {'peak at':>9s} {'first':>8s} "
          f"{'hold ms':>8s} {'excess N*s':>10s}")
    for label in args:
        res = analyse(label, contact(label) if "-impacts" in label or True else None)
        if res is None:
            print(f"{label:22s} n/a")
            continue
        first = "-" if res["first_ms"] is None else f"{res['first_ms']:.0f}"
        print(f"{label:22s} {str(res['push']):5s} {res['peak_ratio']:9.3f} "
              f"{res['peak_ms']:8.0f}ms {first:>7s}ms {res['hold_ms']:8.0f} "
              f"{res['excess_ns']:10.3f}")
