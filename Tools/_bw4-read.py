#!/usr/bin/env python
"""B-WING-4 close read-out: PRE vs the takes, row by row (temp helper).

Usage: python Tools/_bw4-read.py <preLabel> <postLabel> [<postLabel> ...]
Prints, per post: old-column compare vs pre (diff rows, first div, max deltas),
B-WING-3 column compare, then the fan's own histogram for the post label.
Also prints pairwise identity among the post labels.
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")
OLD = ("x", "y", "vx", "vy", "pitch", "mode")
BW3 = ("wingReach", "elbowDeg", "wristDeg", "chuteOn")
FAN = ("fanOpen", "fanChord")


def read(label):
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
            rows.append(p)
    idx = {c: i for i, c in enumerate(cols)}
    out = []
    for r in rows:
        d = {}
        for c, i in idx.items():
            v = r[i] if i < len(r) else ""
            try:
                d[c] = float(v)
            except ValueError:
                d[c] = v
        out.append(d)
    return out


def cmp_pair(a, b, cols):
    n = min(len(a), len(b))
    diff = {c: 0 for c in cols}
    mx = {c: 0.0 for c in cols}
    first = None
    for i in range(n):
        for c in cols:
            va, vb = a[i].get(c), b[i].get(c)
            if isinstance(va, float) and isinstance(vb, float):
                d = abs(va - vb)
                if d > 1e-9:
                    diff[c] += 1
                    if first is None:
                        first = {"row": i, "t": a[i].get("t"), "col": c,
                                 "pre": va, "post": vb}
                    if d > mx[c]:
                        mx[c] = d
    return {"n": n, "diff": {c: v for c, v in diff.items() if v},
            "first": first, "max": {c: round(v, 4) for c, v in mx.items() if v}}


def med(vals):
    s = sorted(vals)
    return round(s[len(s) // 2], 4) if s else None


def fan_hist(rows):
    fo = [r.get("fanOpen", 0.0) for r in rows]
    fc = [r.get("fanChord", 0.0) for r in rows]
    folded = [c for o, c in zip(fo, fc) if o < 0.2]
    spread = [c for o, c in zip(fo, fc) if o > 0.8]
    bins = [0, 0.005, 0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.08, 9]
    hist = [0] * (len(bins) - 1)
    for c in fc:
        for i in range(len(bins) - 1):
            if bins[i] <= c < bins[i + 1]:
                hist[i] += 1
                break
    return {
        "folded_n": len(folded), "folded_max": round(max(folded), 4) if folded else None,
        "spread_n": len(spread), "spread_min": round(min(spread), 4) if spread else None,
        "spread_med": med(spread),
        "gap": (round(min(spread) - max(folded), 4)
                if spread and folded else None),
        "hist_bins": bins, "hist": hist,
    }


def phase_fan(rows, events, label_note):
    """Burst / glide / chute spread chord, per the brief's own phases."""
    out = {}
    # burst: (takeoff, +1.5 s]
    take = [float(e["t"]) for e in events if str(e.get("event")) == "takeoff"] \
        if events else []
    burst_sp = []
    for t0 in take:
        w = [r for r in rows if t0 <= r.get("t", -1) <= t0 + 1.5]
        down = [r for r in w if r.get("fanOpen", 0) > 0.8]
        if down:
            burst_sp.append((round(t0, 2), len(down), med([r["fanChord"] for r in down])))
    out["burst_spread"] = burst_sp
    gl = [r for r in rows if int(r.get("mode", 0)) == 3
          and r.get("heightAboveSupport", 0) > 0.2 and r.get("chuteOn", 0) == 0]
    glsp = [r for r in gl if r.get("fanOpen", 0) > 0.8]
    out["glide_frames"] = len(gl)
    out["glide_spread_frames"] = len(glsp)
    out["glide_spread_chord_med"] = med([r["fanChord"] for r in glsp]) if glsp else None
    touch = [float(e["t"]) for e in events if str(e.get("event")) == "touchdown"] \
        if events else []
    ch = []
    for tt in touch:
        w = [r for r in rows if tt - 4.0 <= r.get("t", -1) <= tt
             and r.get("heightAboveSupport", 99) <= 1.5 and not r.get("grounded", 0)]
        sp = [r for r in w if r.get("fanOpen", 0) > 0.8]
        if w:
            ch.append((round(tt, 2), len(w), len(sp),
                       med([r["fanChord"] for r in sp]) if sp else None))
    out["chute"] = ch
    return out


def read_events(label):
    p = os.path.join(RUNS, label + "_events.csv")
    if not os.path.exists(p):
        return []
    rows, cols = [], None
    with open(p, encoding="utf-8-sig") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            parts = line.split(",")
            if cols is None:
                cols = parts
                continue
            d = {}
            for c, i in zip(cols, parts):
                try:
                    d[c] = float(i)
                except ValueError:
                    d[c] = i
            rows.append(d)
    return rows


def main():
    args = sys.argv[1:]
    pre = args[0]
    posts = args[1:]
    a = read(pre)
    for post in posts:
        b = read(post)
        old = cmp_pair(a, b, OLD)
        new = cmp_pair(a, b, BW3)
        print("== %s vs %s" % (pre, post))
        print("  old:", old["diff"], "first:", old["first"])
        print("  old max:", old["max"])
        print("  bw3 diff:", new["diff"], "bw3 max:", new["max"])
    # pairwise identity of the posts
    if len(posts) > 1:
        for i in range(len(posts)):
            for j in range(i + 1, len(posts)):
                x, y = read(posts[i]), read(posts[j])
                c = cmp_pair(x, y, OLD + BW3 + FAN)
                print("== pair %s vs %s: diff=%s max=%s" % (posts[i], posts[j],
                                                            c["diff"], c["max"]))
    # the fan histogram for the LAST post (the row of record)
    last = posts[-1]
    rows = read(last)
    ev = read_events(last)
    print("== fan histogram (%s)" % last)
    print(" ", fan_hist(rows))
    print("== fan phases (%s)" % last)
    print(" ", phase_fan(rows, ev, last))


if __name__ == "__main__":
    main()
