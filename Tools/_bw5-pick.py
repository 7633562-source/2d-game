#!/usr/bin/env python
"""B-WING-5: pick the take's frames for the round's cuts and zoom sheets (temp helper).

Usage: python Tools/_bw5-pick.py <label>
Prints, from Trials/Runs/<label>.csv: the phase windows the films use (burst /
glide / brake), folded-vs-spread frame candidates inside them, and per-second
fan telemetry over 0..30 s. Read-only.
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")
CAP = 25.0


def read_rows(label):
    rows, cols = [], None
    with open(os.path.join(RUNS, label + ".csv"), encoding="utf-8-sig") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            p = line.split(",")
            if cols is None:
                cols = p
                continue
            d = {}
            for c, v in zip(cols, p):
                try:
                    d[c] = float(v)
                except ValueError:
                    d[c] = v
            rows.append(d)
    return rows


def main():
    label = sys.argv[1]
    mode = sys.argv[2] if len(sys.argv) > 2 else "all"
    rows = read_rows(label)
    by_t = {}
    for r in rows:
        by_t[round(r.get("t", 0.0) * CAP)] = r

    if mode == "spread":
        t0, t1 = float(sys.argv[3]), float(sys.argv[4])
        want = sys.argv[5] if len(sys.argv) > 5 else "open"
        picked = []
        for r in rows:
            t = r.get("t", -1)
            if not (t0 <= t <= t1):
                continue
            o = r.get("fanOpen", 0)
            ok = o > 0.8 if want == "open" else o < 0.2
            if ok:
                picked.append(r)
        # stride so the frames spread over the window
        n = min(len(picked), 14)
        step = max(1, len(picked) // max(1, n))
        for r in picked[::step][:14]:
            print("f%-4d t=%.2f fanO=%.2f chord=%.4f mode=%d chute=%d height=%.2f"
                  % (round(r["t"] * CAP), r["t"], r.get("fanOpen", 0), r.get("fanChord", 0),
                     int(r.get("mode", -1)), int(r.get("chuteOn", 0)), r.get("heightAboveSupport", 0)))
        print("total %s frames in %.2f..%.2f: %d" % (want, t0, t1, len(picked)))
        return

    def frame(t):
        return int(round(t * CAP))

    def show(t):
        n = frame(t)
        r = by_t.get(n)
        if not r:
            print("t=%.2f f%d  (no row)" % (t, n))
            return
        print("t=%.2f f%-4d mode=%d fanO=%.2f chord=%.4f chute=%d wr=%.0f el=%.0f height=%.2f"
              % (t, n, int(r.get("mode", -1)), r.get("fanOpen", 0), r.get("fanChord", 0),
                 int(r.get("chuteOn", 0)), r.get("wingReach", 0), r.get("elbowDeg", 0),
                 r.get("heightAboveSupport", 0)))

    # the three windows the B-WING-4 films used, on this take's event times
    print("== windows (this take)")
    print("-- burst 7.25..9.05")
    for t in (7.30, 7.60, 7.80, 8.00, 8.40, 8.80, 9.04):
        show(t)
    print("-- glide 16.30..17.80")
    for t in (16.30, 16.80, 17.30, 17.78):
        show(t)
    print("-- brake 21.90..23.44")
    for t in (21.90, 22.40, 22.90, 23.30, 23.44):
        show(t)

    # folded candidates: ground contact stretches (fanO small) early in the run
    print("== folded candidates (fanO<0.2, grounded)")
    n = 0
    for r in rows:
        if r.get("fanOpen", 1) < 0.2 and r.get("grounded", 0) and n < 6 and r.get("t", 99) > 1.0:
            print("  t=%.2f f%d fanO=%.2f chord=%.4f" % (r["t"], round(r["t"] * CAP), r["fanOpen"], r["fanChord"]))
            n += 1

    print("== per-second fan (t, mode, fanO, chord)")
    for sec in range(0, 30):
        r = by_t.get(frame(sec + 0.5))
        if r:
            print("  %2d  mode=%d fanO=%.2f chord=%.4f %s"
                  % (sec, int(r.get("mode", -1)), r.get("fanOpen", 0), r.get("fanChord", 0),
                     "CHUTE" if r.get("chuteOn", 0) else ""))


if __name__ == "__main__":
    main()
