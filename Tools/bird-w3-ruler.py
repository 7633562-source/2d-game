#!/usr/bin/env python
"""B-WING-3 ruler: the wing's deployment, read off the B-NAT-0 rows.

Per run (one CSV + its events CSV), one row per label, appended to
Trials/Runs/b-wing-3-histogram.jsonl:

  burst     - inside [takeoff, takeoff+1.5 s] of every takeoff event:
              reach ratio (max/min tip reach), elbow swing deg, wrist swing deg,
              stroke count (wrist-bend upstrokes);
  glide     - airborne Glide/pocket frames: median reach / the run's own rigid max;
  chute     - the last 1.5 m of every descent before a touchdown: frames with
              chuteOn, strokes (wrist-bend upstrokes), downstroke frames;
  land      - per touchdown: |vy|, |vx|, the flare peak (events) and the
              airborne-Sit frame count (the B-LAND-1 regression read);
  identity  - when a second label is given (the pre-control), the old columns are
              compared row by row: differing rows per column + first divergence.

Usage:  python Tools/bird-w3-ruler.py <label> [<label> ...]
        python Tools/bird-w3-ruler.py --compare <preLabel> <postLabel>
Writes Trials/Runs/b-wing-3-histogram.jsonl (appends one row per label).
"""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")
OUT = os.path.join(RUNS, "b-wing-4-histogram.jsonl")

MODE_FLY, MODE_GLIDE, MODE_SIT = 2, 3, 4
BURST_WINDOW = 1.5     # s after the takeoff edge: the burst's own window
CHUTE_HEIGHT = 1.5     # m: the last stretch of a descent the chute rows live in
OLD_COLUMNS = ("x", "y", "vx", "vy", "pitch", "mode")
NEW_COLUMNS = ("wingReach", "elbowDeg", "wristDeg", "chuteOn")
# B-WING-4: the fan's own columns (appended after the B-WING-3 four).
FAN_COLUMNS = ("fanOpen", "fanChord")


def read_csv(path):
    cols = None
    rows = []
    try:
        with open(path, "r", encoding="utf-8-sig") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                parts = line.split(",")
                if cols is None:
                    cols = parts
                    continue
                rows.append(parts)
    except FileNotFoundError:
        return [], {}
    idx = {c: i for i, c in enumerate(cols or [])}
    out = []
    for r in rows:
        d = {}
        for c, i in idx.items():
            v = r[i] if i < len(r) else ""
            if c in ("supportKind",):
                d[c] = v
            else:
                try:
                    d[c] = float(v)
                except ValueError:
                    d[c] = v
        out.append(d)
    return out, idx


def nan_count(rows, cols):
    n = 0
    for r in rows:
        for c in cols:
            v = r.get(c, 0.0)
            if isinstance(v, float) and v != v:
                n += 1
    return n


def strokes(vals):
    """Count the upstroke peaks of a joint-bend series: a local max above the
    series' mid-band. One stroke = one fold-unfold cycle."""
    if len(vals) < 5:
        return 0
    lo, hi = min(vals), max(vals)
    if hi - lo < 5.0:
        return 0
    mid = lo + 0.5 * (hi - lo)
    peaks = 0
    for i in range(1, len(vals) - 1):
        if vals[i] >= mid and vals[i] >= vals[i - 1] and vals[i] > vals[i + 1]:
            if i == 0 or vals[i] != vals[i - 1]:
                peaks += 1
    return peaks


def window(rows, t0, t1):
    return [r for r in rows if t0 <= r.get("t", -1) <= t1]


def read_run(label):
    rows, _ = read_csv(os.path.join(RUNS, label + ".csv"))
    events, _ = read_csv(os.path.join(RUNS, label + "_events.csv"))
    if not rows:
        return None
    has_new = NEW_COLUMNS[0] in rows[0]
    has_fan = FAN_COLUMNS[0] in rows[0]
    d = {"label": label, "frames": len(rows), "has_new_columns": bool(has_new),
         "nan": nan_count(rows, OLD_COLUMNS + (NEW_COLUMNS if has_new else ())
                          + (FAN_COLUMNS if has_fan else ()))}
    takeoffs = [e for e in events if str(e.get("event")) == "takeoff"]
    touchdowns = [e for e in events if str(e.get("event")) == "touchdown"]
    flares = [e for e in events if str(e.get("event")) == "flare_peak"]
    d["takeoffs"] = len(takeoffs)
    d["touchdowns"] = len(touchdowns)

    # --- the burst: the deployment inside the takeoff window
    burst = []
    for e in takeoffs:
        t0 = float(e.get("t", 0.0))
        w = window(rows, t0, t0 + BURST_WINDOW)
        if not has_new or len(w) < 8:
            continue
        reach = [r["wingReach"] for r in w]
        elbow = [r["elbowDeg"] for r in w]
        wrist = [r["wristDeg"] for r in w]
        mx = max(reach)
        mn = min(x for x in reach if x > 0.02) if any(x > 0.02 for x in reach) else 0.0
        burst.append({
            "t": round(t0, 3),
            "reach_max": round(mx, 4),
            "reach_min": round(mn, 4),
            "reach_ratio": round(mx / mn, 3) if mn > 0 else None,
            "elbow_swing": round(max(elbow) - min(elbow), 1),
            "wrist_swing": round(max(wrist) - min(wrist), 1),
            "strokes": strokes(wrist),
        })
    d["burst"] = burst
    if burst:
        d["burst_best_ratio"] = max(b["reach_ratio"] or 0 for b in burst)
        d["burst_elbow_swing"] = max(b["elbow_swing"] for b in burst)
        d["burst_wrist_swing"] = max(b["wrist_swing"] for b in burst)

    # --- the glide: the held wing's own spread
    if has_new:
        gl = [r for r in rows if int(r.get("mode", 0)) == MODE_GLIDE
              and r.get("heightAboveSupport", 0) > 0.2 and r.get("chuteOn", 0) == 0]
        air = [r for r in rows if r.get("heightAboveSupport", 0) > 0.2]
        if gl:
            rl = sorted(r["wingReach"] for r in gl)
            d["glide_frames"] = len(gl)
            d["glide_reach_med"] = round(rl[len(rl) // 2], 4)
            d["rigid_max"] = round(max((r["wingReach"] for r in air), default=0.0), 4)
            if d["rigid_max"] > 0:
                d["glide_ratio"] = round(d["glide_reach_med"] / d["rigid_max"], 3)
            # B-WING-3: the spread read itself — a held wing is a straight chain
            # (elbow ≈ 0), a plank folded along the back is not.
            el = sorted(r["elbowDeg"] for r in gl)
            d["glide_elbow_med"] = round(el[len(el) // 2], 1)
            d["glide_wrist_max"] = round(max(r["wristDeg"] for r in gl), 1)

    # --- the chute: the last 1.5 m of every descent before a touch
    chutes = []
    for e in touchdowns:
        tt = float(e.get("t", 0.0))
        w = [r for r in window(rows, tt - 4.0, tt)
             if r.get("heightAboveSupport", 99) <= CHUTE_HEIGHT and not r.get("grounded", 0)]
        if not w or not has_new:
            continue
        wrist = [r["wristDeg"] for r in w]
        elbow = [r["elbowDeg"] for r in w]
        chutes.append({
            "t": round(tt, 3),
            "frames": len(w),
            "window_s": round(w[-1]["t"] - w[0]["t"], 2),
            "chute_frames": sum(1 for r in w if r.get("chuteOn", 0)),
            "strokes": strokes(wrist),
            "elbow_swing": round(max(elbow) - min(elbow), 1),
            "downstrokes": strokes([-x for x in elbow]),
            # B-WING-4: the parachute's own fan — open frames and the chord.
            "fan_open_frames": sum(1 for r in w if r.get("fanOpen", 0.0) > 0.8),
            "fan_chord_max": round(max((r.get("fanChord", 0.0) for r in w), default=0.0), 4),
        })
    d["chutes"] = chutes
    d["chute_fired"] = sum(1 for c in chutes if c["chute_frames"] > 0)

    # --- B-WING-4: the fan's own area — folded vs spread, and the round's three
    # spread phases (the burst's downstroke, the glide, the chute).
    if has_fan:
        fan = {}

        def med(sel, col):
            if not sel:
                return None
            v = sorted(r.get(col, 0.0) for r in sel)
            return round(v[len(v) // 2], 4)

        folded = [r for r in rows if r.get("fanOpen", 0.0) < 0.2]
        spread = [r for r in rows if r.get("fanOpen", 0.0) > 0.8]
        fan["folded_frames"] = len(folded)
        fan["folded_chord_med"] = med(folded, "fanChord")
        fan["folded_chord_max"] = round(max((r.get("fanChord", 0.0) for r in folded), default=0.0), 4)
        fan["spread_frames"] = len(spread)
        fan["spread_chord_med"] = med(spread, "fanChord")
        fan["spread_chord_max"] = round(max((r.get("fanChord", 0.0) for r in spread), default=0.0), 4)
        if fan["spread_chord_med"] and fan["folded_chord_max"]:
            fan["chord_ratio"] = round(fan["spread_chord_med"] / fan["folded_chord_max"], 2)
        burst_open = []
        for e in takeoffs:
            t0 = float(e.get("t", 0.0))
            burst_open += [r for r in window(rows, t0, t0 + BURST_WINDOW)
                           if r.get("fanOpen", 0.0) > 0.8]
        fan["burst_spread_frames"] = len(burst_open)
        fan["burst_spread_chord_med"] = med(burst_open, "fanChord")
        glide_open = [r for r in rows
                      if int(r.get("mode", 0)) == MODE_GLIDE and r.get("fanOpen", 0.0) > 0.8
                      and r.get("heightAboveSupport", 0) > 0.2]
        fan["glide_spread_frames"] = len(glide_open)
        fan["glide_spread_chord_med"] = med(glide_open, "fanChord")
        d["fan"] = fan

    # --- the landing bars (no regression) + airborne Sit
    land = []
    for e in touchdowns:
        land.append({
            "t": round(float(e.get("t", 0.0)), 3),
            "vy": round(float(e.get("vy", 0.0)), 3),
            "vx": round(float(e.get("vx", 0.0)), 3),
        })
    d["land"] = land
    d["flare_peak_deg"] = max((float(e.get("pitch", 0.0)) for e in flares), default=None)
    sit_air = [r for r in rows if int(r.get("mode", 0)) == MODE_SIT and r.get("heightAboveSupport", 0) > 0.3]
    d["airborne_sit_frames"] = len(sit_air)
    d["max_touchdown_vy"] = max((abs(x["vy"]) for x in land), default=0.0)
    d["max_touchdown_vx"] = max((abs(x["vx"]) for x in land), default=0.0)
    return d


def compare(pre_label, post_label):
    a, _ = read_csv(os.path.join(RUNS, pre_label + ".csv"))
    b, _ = read_csv(os.path.join(RUNS, post_label + ".csv"))
    n = min(len(a), len(b))
    diff = {c: 0 for c in OLD_COLUMNS}
    first = None
    for i in range(n):
        for c in OLD_COLUMNS:
            va, vb = a[i].get(c), b[i].get(c)
            if isinstance(va, float) and isinstance(vb, float):
                if abs(va - vb) > 1e-9:
                    diff[c] += 1
                    if first is None:
                        first = {"row": i, "t": a[i].get("t"), "column": c,
                                 "pre": va, "post": vb}
    return {"pre": pre_label, "post": post_label, "rows_compared": n,
            "diff": diff, "first_divergence": first}


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return
    if args[0] == "--compare":
        res = compare(args[1], args[2])
        print(json.dumps(res, ensure_ascii=False, indent=1))
        return
    rows_out = []
    for label in args:
        d = read_run(label)
        if d is None:
            print("no rows for %s" % label)
            continue
        with open(OUT, "a", encoding="utf-8") as f:
            f.write(json.dumps(d, ensure_ascii=False) + "\n")
        rows_out.append(d)
        print(json.dumps({k: d[k] for k in d if k not in ("burst", "chutes", "land")},
                         ensure_ascii=False))
        for b in d.get("burst", []):
            print("  burst t=%s ratio=%s elbow=%s wrist=%s strokes=%s" % (
                b["t"], b["reach_ratio"], b["elbow_swing"], b["wrist_swing"], b["strokes"]))
        for c in d.get("chutes", []):
            print("  chute t=%s window=%ss frames=%d chute=%d strokes=%d elbow=%s" % (
                c["t"], c["window_s"], c["frames"], c["chute_frames"], c["strokes"], c["elbow_swing"]))


if __name__ == "__main__":
    main()
