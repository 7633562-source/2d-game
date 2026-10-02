#!/usr/bin/env python
"""B-TAKEOFF-2 ruler: the wing's own behaviour, read off the B-NAT-0 columns.

Measurements per run (one CSV + its events CSV + JSON), one row per member:
  leap        - the takeoff's leg impulse (the `leap` event's extra) and the share of vy
                it carries 0.15 s after the take-off edge (bar: >= 0.50);
  cadence     - strokes per second in the take-off burst vs in cruise (bar: >= 1.4x), the
                length of the burst (bar: 3..6 strokes), the first hold after it;
  braking     - downstrokes inside the last 1.5 m of a descent (bar: 2..3 of them);
  fold        - the share of grounded frames with the wings along the body (wingSpan);
  aircorr     - max |autocorrelation| of bodyY over lags 1..10 s (bar: <= 0.5), and the
                same over in-flight frames only.

Usage:  python Tools/bird-t2-ruler.py <label> [<label> ...]
Writes Trials/Runs/b-takeoff-2-histogram.jsonl (appends one row per label).
"""
import json
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")
OUT = os.path.join(RUNS, "b-takeoff-2-histogram.jsonl")

# BirdMode's real order (Assets/BirdController.cs): Stand=0, Walk=1, Fly=2,
# Glide=3, Sit=4, Attack=5, Peck=6.
MODE_STAND, MODE_WALK, MODE_FLY, MODE_GLIDE, MODE_SIT, MODE_ATTACK, MODE_PECK = 0, 1, 2, 3, 4, 5, 6
FLIGHT = (MODE_FLY, MODE_GLIDE, MODE_ATTACK)

BURST_WINDOW = 1.5      # s after the take-off edge: the burst's own window
CRUISE_SKIP = 1.5       # s dropped at each end of a flight bout when reading cruise
BRAKE_HEIGHT = 1.2      # m above the support: the braking strokes' reading window
BURST_BAND_HZ = 5.0     # strokes at or above this rate belong to the take-off burst
FOLD_SPAN = 0.22        # m tip-to-tip, below which the wings read folded along the body


def read_csv(path):
    cols = None
    rows = []
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
    idx = {c: i for i, c in enumerate(cols)}
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
    return cols or [], out


def read_events(path):
    ev = []
    if not os.path.exists(path):
        return ev
    with open(path, "r", encoding="utf-8-sig") as f:
        cols = None
        for line in f:
            line = line.strip()
            if not line:
                continue
            p = line.split(",")
            if cols is None:
                cols = p
                continue
            d = dict(zip(cols, p))
            for k in ("t", "member", "vx", "vy", "pitch", "extra", "mode", "life",
                      "airborneSeconds", "heightAboveSupport"):
                if k in d:
                    try:
                        d[k] = float(d[k])
                    except ValueError:
                        pass
            ev.append(d)
    return ev


def phase_frames(mrows):
    """Per-frame flap phase advance: (t, dphase, dt) for every frame the wing
    was moving. The phase freezes in Glide and in a held pocket, so the frames
    where it moved are exactly the strokes."""
    out = []
    prev = None
    prev_t = None
    for r in mrows:
        t = r["t"]
        ph = r.get("flapPhase")
        if not isinstance(ph, float):
            prev_t = t
            continue
        if prev is not None and prev_t is not None:
            d = ph - prev
            if d < -0.5:
                d += 1.0
            dt = t - prev_t
            if d > 1e-9 and dt > 1e-9:
                out.append((t, d, dt))
        prev = ph
        prev_t = t
    return out


def phase_rate(mrows, t0, t1, band_min=0.0, band_max=1e9):
    """Strokes and the stroke rate over [t0, t1], the band filter optional."""
    adv = 0.0
    moving = 0.0
    span = 0.0
    for t, d, dt in phase_frames(mrows):
        if t < t0 or t > t1:
            continue
        rate = d / dt if dt > 0 else 0.0
        span += dt
        if rate >= band_min and rate <= band_max:
            adv += d
            moving += dt
    hz = adv / moving if moving > 1e-6 else 0.0
    return adv, hz, moving, span


def strokes(rows):
    """Downstroke edges and phase wraps: the wing's own beats, as the ruler saw them."""
    edges = []
    wraps = []
    prev_on = None
    prev_phase = None
    for r in rows:
        on = int(r.get("flapOn", 0) or 0)
        ph = r.get("flapPhase", 0.0)
        if prev_on is not None and prev_on == 0 and on == 1:
            edges.append(r["t"])
        if prev_phase is not None and isinstance(ph, float) and isinstance(prev_phase, float) and ph < prev_phase - 0.5:
            wraps.append(r["t"])
        prev_on = on
        prev_phase = ph
    return edges, wraps


def window_hz(wraps, t0, t1):
    n = sum(1 for w in wraps if t0 <= w < t1)
    span = max(1e-6, t1 - t0)
    return n, n / span


def count_window(edges, t0, t1):
    return sum(1 for e in edges if t0 <= e < t1)


def autocorr(series, dt, lag_min=1.0, lag_max=10.0, step=0.05):
    """Max |r| of the detrended series over lags 1..10 s.

    Detrended: a bird climbing 3 m has a monotone bodyY and its raw
    autocorrelation reads ~1 at every lag — that is a trend, not the bob this
    round is about. The line is removed first, so r answers "does the height
    repeat itself".
    """
    n = len(series)
    if n < 20:
        return 0.0, 0.0
    # least-squares line removal
    sx = (n - 1) * n / 2.0
    sxx = (n - 1) * n * (2 * n - 1) / 6.0
    sy = sum(series)
    sxy = sum(i * x for i, x in enumerate(series))
    denom = n * sxx - sx * sx
    slope = 0.0 if abs(denom) < 1e-9 else (n * sxy - sx * sy) / denom
    intercept = (sy - slope * sx) / n
    dev = [series[i] - (intercept + slope * i) for i in range(n)]
    mean = sum(dev) / n
    dev = [x - mean for x in dev]
    var = sum(x * x for x in dev) / n
    if var <= 1e-12:
        return 0.0, 0.0
    best_r, best_lag = 0.0, 0.0
    lag = lag_min
    while lag <= lag_max + 1e-9:
        k = int(round(lag / dt))
        if 1 <= k < n - 10:
            x1 = dev[:n - k]
            x2 = dev[k:]
            m = len(x1)
            mx = sum(x1) / m
            my = sum(x2) / m
            num = sum((x1[i] - mx) * (x2[i] - my) for i in range(m))
            vx = sum((v - mx) ** 2 for v in x1)
            vy2 = sum((v - my) ** 2 for v in x2)
            if vx > 1e-12 and vy2 > 1e-12:
                # Pearson between the two halves: bounded by 1 for any length,
                # which the biased (÷ n·var) form was not on short stretches.
                r = num / ((vx ** 0.5) * (vy2 ** 0.5))
                if abs(r) > abs(best_r):
                    best_r, best_lag = r, lag
        lag += step
    return round(best_r, 3), round(best_lag, 2)


def member_metrics(rows, events, member):
    mrows = [r for r in rows if int(r.get("member", 0)) == member]
    devents = [e for e in events if int(e.get("member", 0)) == member]
    if not mrows:
        return None
    dt = 0.005
    ground = [r for r in mrows if int(r.get("grounded", 0) or 0) == 1]
    air = [r for r in mrows if int(r.get("mode", -1)) in FLIGHT]
    has_span = "wingSpan" in mrows[0]
    fold_share = None
    span_ground = span_air = None
    if has_span and ground:
        fold = [r for r in ground if isinstance(r.get("wingSpan"), float) and r["wingSpan"] <= FOLD_SPAN]
        fold_share = len(fold) / len(ground)
        span_ground = sum(r["wingSpan"] for r in ground if isinstance(r["wingSpan"], float)) / len(ground)
    if has_span and air:
        vals = [r["wingSpan"] for r in air if isinstance(r.get("wingSpan"), float)]
        if vals:
            span_air = sum(vals) / len(vals)

    edges, wraps = strokes(mrows)

    takeoffs = []
    for e in devents:
        if e.get("event") != "takeoff":
            continue
        t0 = e["t"]
        vx, vy = e.get("vx", 0.0), e.get("vy", 0.0)

        def at(dt_off, key="vy"):
            best = None
            for r in mrows:
                if r["t"] >= dt_off:
                    best = r
                    break
            return best.get(key) if best else None

        # the leap's own event, if the build under test has one
        leap = None
        for le in devents:
            if le.get("event") == "leap" and abs(le["t"] - t0) <= 0.8:
                leap = le
        leap_vy = None
        leap_t = None
        if leap is not None:
            leap_t = leap["t"]
            try:
                leap_vy = float(leap.get("extra", 0.0))
            except (TypeError, ValueError):
                leap_vy = None
        # vy 0.15 s after the leap itself (the impulse), or after the take-off
        # edge when the build has no leap to read.
        vy015 = at((leap_t if leap_t is not None else t0) + 0.15)
        share = None
        if leap_vy is not None and vy015 not in (None, 0.0):
            share = leap_vy / vy015 if vy015 > 0 else None

        # THE BURST: the strokes at the take-off rate (>= BURST_BAND_HZ), so the
        # count is the burst's own strokes and not whatever the wing did until
        # the first hold.
        burst_adv, burst_hz, burst_moving, _ = phase_rate(
            mrows, t0, t0 + BURST_WINDOW * 1.4, band_min=BURST_BAND_HZ)
        land_t = None
        for e2 in devents:
            if e2.get("event") == "touchdown" and e2["t"] > t0:
                land_t = e2["t"]
                break
        c0, c1 = t0 + CRUISE_SKIP, (land_t - CRUISE_SKIP) if land_t else max(r["t"] for r in mrows)
        cruise_adv, cruise_hz, cruise_moving, cruise_span = phase_rate(
            mrows, c0, c1, band_max=BURST_BAND_HZ - 0.01)
        takeoffs.append({
            "t": round(t0, 3), "vy": round(vy, 3), "vy015": None if vy015 is None else round(vy015, 3),
            "leapT": None if leap_t is None else round(leap_t, 3), "leapVy": leap_vy,
            "leapShare": None if share is None else round(share, 3),
            "burstStrokes": round(burst_adv, 2), "burstHz": round(burst_hz, 2),
            "burstSpan": round(burst_moving, 2),
            "cruiseStrokes": round(cruise_adv, 2), "cruiseHz": round(cruise_hz, 2),
            "cruiseMoving": round(cruise_moving, 2), "cruiseSpan": round(cruise_span, 2),
            "cruiseFrozenShare": round(1.0 - (cruise_moving / cruise_span), 3) if cruise_span > 1e-6 else None,
            "hzRatio": None if cruise_hz < 1e-6 else round(burst_hz / cruise_hz, 2),
            "landT": None if land_t is None else round(land_t, 3),
        })

    # braking strokes: downstrokes on the way down through the last BRAKE_HEIGHT
    # metres, plus the wing plane's own extremes there (a deep stroke shows a
    # wide span swing over its beat)
    brakes = []
    for e in devents:
        if e.get("event") != "touchdown":
            continue
        t1 = e["t"]
        # the window is the LAST descent: step back from the touch until the
        # height is once again above the brake ceiling, so a low wander that
        # spent 18 s under 1.2 m does not swallow the reading
        lo = t1
        for r in reversed([x for x in mrows if x["t"] <= t1]):
            h = r.get("heightAboveSupport")
            if isinstance(h, float) and h > BRAKE_HEIGHT:
                break
            lo = r["t"]
        spans = [r["wingSpan"] for r in mrows
                 if lo <= r["t"] <= t1 and isinstance(r.get("wingSpan"), float)]
        # the descent's own strokes only: level flight inside the window is not
        # the brake (the reading the verdict names is "the last 1-1.5 m it
        # makes 2-3 deep strokes ... before the touchdown")
        desc = [r["t"] for r in mrows
                if lo <= r["t"] <= t1 and isinstance(r.get("vy"), float) and r["vy"] < -0.05]
        d_lo = min(desc) if desc else lo
        brakes.append({"t": round(t1, 3), "strokes": count_window(edges, d_lo, t1),
                       "window": round(t1 - lo, 3), "descent": round(t1 - d_lo, 3),
                       "spanMin": None if not spans else round(min(spans), 3),
                       "spanMax": None if not spans else round(max(spans), 3)})

    yseries = [r.get("bodyY", 0.0) for r in mrows]
    r_all, lag_all = autocorr(yseries, dt)
    # in-flight reading: contiguous flight stretches only (a concatenation of
    # separate flights would fabricate a continuity the bird never flew)
    stretches = []
    cur = []
    for r in mrows:
        if int(r.get("mode", -1)) in FLIGHT:
            cur.append(r.get("bodyY", 0.0))
        else:
            if len(cur) > 400:
                stretches.append(cur)
            cur = []
    if len(cur) > 400:
        stretches.append(cur)
    r_air, lag_air, air_len = 0.0, 0.0, 0
    for s in stretches:
        rr, ll = autocorr(s, dt)
        if abs(rr) > abs(r_air):
            r_air, lag_air = rr, ll
        air_len = max(air_len, len(s))

    return {
        "member": member,
        "frames": len(mrows),
        "groundFrames": len(ground),
        "airFrames": len(air),
        "foldShare": None if fold_share is None else round(fold_share, 4),
        "wingSpanGround": None if span_ground is None else round(span_ground, 4),
        "wingSpanAir": None if span_air is None else round(span_air, 4),
        "takeoffs": takeoffs,
        "brakes": brakes,
        "aircorrAll": [round(r_all, 3), lag_all],
        "aircorrAir": [round(r_air, 3), lag_air],
        "airStretchFrames": air_len,
        "strokeCount": len(edges),
    }


def bars(row):
    tk = row["perMember"][0]["takeoffs"] if row["perMember"] else []
    shares = [t["leapShare"] for t in tk if t.get("leapShare") is not None]
    ratios = [t["hzRatio"] for t in tk if t.get("hzRatio") is not None]
    bursts = [t["burstStrokes"] for t in tk]
    brakes = [b["strokes"] for m in row["perMember"] for b in m["brakes"]]
    ac = [abs(m["aircorrAll"][0]) for m in row["perMember"]]
    aca = [abs(m["aircorrAir"][0]) for m in row["perMember"]]
    return {
        "leapShareMin": None if not shares else min(shares),
        "leapShareMed": None if not shares else sorted(shares)[len(shares) // 2],
        "hzRatioMin": None if not ratios else min(ratios),
        "burstStrokes": bursts,
        "brakeStrokes": brakes,
        "aircorrAllMax": None if not ac else max(ac),
        "aircorrAirMax": None if not aca else max(aca),
    }


def main(labels):
    out_rows = []
    for label in labels:
        csv_path = os.path.join(RUNS, label + ".csv")
        json_path = os.path.join(RUNS, label + ".json")
        ev_path = os.path.join(RUNS, label + "_events.csv")
        if not os.path.exists(csv_path):
            print("MISSING " + csv_path)
            continue
        cols, rows = read_csv(csv_path)
        events = read_events(ev_path)
        meta = {}
        if os.path.exists(json_path):
            with open(json_path, "r", encoding="utf-8-sig") as f:
                meta = json.load(f)
        members = sorted(set(int(r.get("member", 0)) for r in rows))
        row = {
            "label": label,
            "fingerprint": meta.get("sourceFingerprint") or meta.get("buildFingerprint"),
            "simulatedSeconds": meta.get("simulatedSeconds"),
            "fell": meta.get("fell"),
            "airborneFraction": meta.get("airborneFraction"),
            "glideFraction": meta.get("glideFraction"),
            "perMember": [member_metrics(rows, events, m) for m in members],
            "hasSpanColumn": "wingSpan" in cols,
            "hasLeapEvent": any(e.get("event") == "leap" for e in events),
        }
        row["bars"] = bars(row)
        out_rows.append(row)
        print(json.dumps({"label": label, "bars": row["bars"],
                          "aircorr": [[m["member"], m["aircorrAll"], m["aircorrAir"]] for m in row["perMember"]]}))
        with open(OUT, "a", encoding="utf-8") as f:
            f.write(json.dumps(row) + "\n")
    print("histogram: " + OUT + " (+%d rows)" % len(out_rows))


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    main(sys.argv[1:])
