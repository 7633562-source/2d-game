#!/usr/bin/env python
"""B-WING-4 close: compact drift table + the take's phase timeline (temp helper).

Usage: python Tools/_bw4-drift.py <preLabel> <label> [<label> ...]
Then prints the phase timeline of the LAST label.
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")
MODES = {0: "Ground", 1: "Walk?", 2: "Fly", 3: "Glide", 4: "Sit", 5: "?", 6: "?"}


def read(label, suffix=".csv"):
    rows, cols = [], None
    with open(os.path.join(RUNS, label + suffix), encoding="utf-8-sig") as f:
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
    return rows, cols


def main():
    args = sys.argv[1:]
    pre = args[0]
    a, _ = read(pre)
    print("label              fp        to/td  first-div            maxdx  maxdy  maxdpitch")
    for label in args[1:]:
        b, _ = read(label)
        n = min(len(a), len(b))
        first, mx = None, {"x": 0.0, "y": 0.0, "pitch": 0.0}
        for i in range(n):
            for c in ("x", "y", "pitch"):
                va, vb = a[i].get(c), b[i].get(c)
                if isinstance(va, float) and isinstance(vb, float):
                    d = abs(va - vb)
                    if d > 1e-9 and first is None:
                        first = "row %d t=%.2f %s" % (i, a[i].get("t", -1), c)
                    if d > mx[c]:
                        mx[c] = d
        ev, _ = read(label, "_events.csv")
        to = sum(1 for e in ev if e.get("event") == "takeoff")
        td = sum(1 for e in ev if e.get("event") == "touchdown")
        try:
            import json
            fp = json.load(open(os.path.join(RUNS, label + ".json"), encoding="utf-8"))["sourceFingerprint"][:8]
        except Exception:
            fp = "?"
        print("%-18s %s  %d/%d   %-20s %6.3f %6.3f %7.2f" % (
            label, fp, to, td, first or "-", mx["x"], mx["y"], mx["pitch"]))

    last = args[-1]
    rows, _ = read(last)
    print("\n== phase timeline: %s (segments >0.25 s; mode/grounded runs)" % last)
    segs = []
    cur = None
    for r in rows:
        key = (int(r.get("mode", -1)), int(r.get("grounded", 0)))
        if cur is None or key != cur[0]:
            if cur is not None:
                segs.append(cur)
            cur = [key, r, r]
        else:
            cur[2] = r
    segs.append(cur)
    for key, r0, r1 in segs:
        dur = r1["t"] - r0["t"]
        if dur < 0.25:
            continue
        w = [r for r in rows if r0["t"] - 0.001 <= r["t"] <= r1["t"] + 0.001]
        fo = [r.get("fanOpen", 0.0) for r in w]
        sp = sum(1 for v in fo if v > 0.8)
        ch = sum(1 for r in w if r.get("chuteOn", 0))
        h0 = r0.get("heightAboveSupport", 0.0)
        h1 = r1.get("heightAboveSupport", 0.0)
        hmax = max(r.get("heightAboveSupport", 0.0) for r in w)
        print("  t %5.2f..%5.2f %-6s g%d h %.2f->%.2f (max %.2f) fan>0.8 %3d/%3d chute %3d" % (
            r0["t"], r1["t"], MODES.get(key[0], "?"), key[1], h0, h1, hmax, sp, len(w), ch))


if __name__ == "__main__":
    main()
