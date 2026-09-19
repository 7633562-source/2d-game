#!/usr/bin/env python3
"""ARENA-1 number-on-the-head check (Yury's 18.09 correction, level cycle 19.09).

Three questions, answered from the run's own files:

  1. Is the digit INSIDE the head and within the correction's ceiling?
     Parsed from the `ARENA-NUMBER` lines the agent logs after it has measured the
     rendered glyph (head height, target, drawn, ratio, insideHead).
  2. Did the digit change any physics? Two arena runs on the SAME build with
     different `-arenaNumber` requests (the digit is a TextMesh - no collider, no
     rigidbody, nothing the solver can see) must produce identical humans:
     same ids, spawn times, distances, fall flags and fall times.
  3. How many heads are in the proof photo? The arena's trajectory ring records each
     human's pelvis every 0.25 s, so the count of pelvises inside the photo lens at
     the photo time is computed, not guessed. A head sits ~0.65 m above the pelvis,
     so the lens must cover both.

  python Tools/arena-number-check.py arena_num1 arena_num2 --photo 6 --lens 2.2

No Unity, no engine: JSON + log text only.
"""
from __future__ import annotations

import argparse
import json
import pathlib
import re

PROJECT = pathlib.Path(__file__).resolve().parent.parent
RUNS = PROJECT / "Trials" / "Runs"
LOGS = PROJECT / "Logs"

NUMBER_RE = re.compile(
    r"ARENA-NUMBER id=(?P<id>-?\d+) head=(?P<head>[\d.]+)m target=(?P<target>[\d.]+)m "
    r"drawn=(?P<drawn>[\d.]+)m ratio=(?P<ratio>[\d.]+) measured=(?P<measured>True|False) "
    r"insideHead=(?P<inside>True|False)")


def number_lines(label: str) -> list[dict]:
    path = LOGS / ("arena-" + label + ".log")
    if not path.exists():
        return []
    out = []
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        m = NUMBER_RE.search(line)
        if m:
            d = m.groupdict()
            out.append({"id": int(d["id"]), "head": float(d["head"]),
                        "target": float(d["target"]), "drawn": float(d["drawn"]),
                        "ratio": float(d["ratio"]),
                        "measured": d["measured"] == "True",
                        "inside": d["inside"] == "True"})
    return out


def humans(label: str):
    """(id -> human row, whole JSON) for one arena run; ({}, {}) when absent."""
    path = RUNS / (label + ".json")
    if not path.exists():
        return {}, {}
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    out = {}
    for h in data.get("humans", []):
        out[int(h["id"])] = h
    return out, data


def head_positions(data: dict, at: float) -> list[tuple[int, float, float]]:
    """Pelvis positions from the trajectory ring at the sample nearest `at`."""
    found = []
    for h in data.get("humans", []):
        ring = h.get("trajectory") or []
        best = None
        for sample in ring:
            t = sample.get("t", sample[0] if isinstance(sample, list) else None)
            if t is None:
                continue
            if best is None or abs(t - at) < abs(best[0] - at):
                best = (t, sample)
        if best is None:
            continue
        s = best[1]
        if isinstance(s, dict):
            found.append((int(h["id"]), s.get("x"), s.get("y")))
        else:
            found.append((int(h["id"]), s[1], s[2]))
    return found


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("labels", nargs="+")
    ap.add_argument("--photo", type=float, default=6.0, help="photo time in seconds")
    ap.add_argument("--lens", type=float, default=2.2, help="photo camera half-height, m")
    ap.add_argument("--photo-x", type=float, default=1.2,
                    help="photo camera centre x (ArenaRunner arenaPhotoOffsetX)")
    ap.add_argument("--camera-y", type=float, default=-0.2,
                    help="camera centre y (ArenaRunner.SetLens)")
    ap.add_argument("--aspect", type=float, default=16.0 / 9.0)
    ap.add_argument("--head-above-pelvis", type=float, default=0.65)
    args = ap.parse_args()

    failures = []
    print("== the digit ==")
    for label in args.labels:
        rows = number_lines(label)
        if not rows:
            print("  %-12s no ARENA-NUMBER lines" % label)
            failures.append(label + ": no number lines")
            continue
        ratios = [r["ratio"] for r in rows]
        print("  %-12s humans=%d head=%.3f-%.3f m target=%.3f-%.3f m drawn=%.3f-%.3f m "
              "ratio=%.2f-%.2f measured=%d/%d insideHead=%d/%d"
              % (label, len(rows), min(r["head"] for r in rows), max(r["head"] for r in rows),
                 min(r["target"] for r in rows), max(r["target"] for r in rows),
                 min(r["drawn"] for r in rows), max(r["drawn"] for r in rows),
                 min(ratios), max(ratios),
                 sum(1 for r in rows if r["measured"]), len(rows),
                 sum(1 for r in rows if r["inside"]), len(rows)))
        for r in rows:
            if r["ratio"] > 0.60:
                failures.append("%s: id %d drawn is %.0f%% of the head (> 60%%)"
                                % (label, r["id"], 100 * r["ratio"]))
            if not r["inside"]:
                failures.append("%s: id %d digit outside the head box" % (label, r["id"]))

    print("\n== physics control (same build, different -arenaNumber) ==")
    if len(args.labels) >= 2:
        a, b = args.labels[0], args.labels[1]
        ha, da = humans(a)
        hb, db = humans(b)
        print("  %s: %d humans, %s: %d humans" % (a, len(ha), b, len(hb)))
        if len(ha) != len(hb):
            failures.append("human count differs: %d vs %d" % (len(ha), len(hb)))
        # The whole row, not a hand-picked subset: the digit is a TextMesh, so
        # nothing about a human - genome, parentage, spawn, distance, fall, path -
        # may differ between two runs that only asked for a different digit size.
        shared = set(ha) & set(hb)
        print("  shared ids compared: %d" % len(shared))
        diffs = []
        for hid in sorted(shared):
            if ha[hid] == hb[hid]:
                continue
            keys = set(ha[hid]) | set(hb[hid])
            for k in sorted(keys):
                if ha[hid].get(k) != hb[hid].get(k):
                    diffs.append("id %d %s: %r vs %r" % (hid, k, ha[hid].get(k), hb[hid].get(k)))
        print("  differing fields (whole rows): %d" % len(diffs))
        for d in diffs[:5]:
            print("    " + d)
        if diffs:
            failures.append("physics moved: %d field differences" % len(diffs))
        identical = sum(1 for hid in shared if ha[hid] == hb[hid])
        print("  identical rows: %d/%d" % (identical, len(shared)))
        if identical != len(shared):
            failures.append("identical rows %d of %d" % (identical, len(shared)))
    else:
        da = humans(args.labels[0])[1] if args.labels else {}
        db = {}

    print("\n== heads in the proof frame ==")
    label = args.labels[0]
    _, data = humans(label)
    half_h = args.lens
    half_w = args.lens * args.aspect
    px_per_meter = 1080.0 / (2.0 * args.lens)
    rows = number_lines(label)
    digit_m = max((r["drawn"] for r in rows), default=0.0)
    print("  lens centre x=%.2f y=%.2f half-height %.2f m -> %.1f px per metre; "
          "a %.3f m digit is %.0f px tall"
          % (args.photo_x, args.camera_y, half_h, px_per_meter, digit_m, digit_m * px_per_meter))
    for t in (args.photo, args.photo + 8, args.photo + 16, args.photo + 24):
        inside = []
        for hid, x, y in head_positions(data, t):
            if x is None or y is None:
                continue
            head_y = y + args.head_above_pelvis
            if (args.photo_x - half_w <= x <= args.photo_x + half_w
                    and abs(y - args.camera_y) <= half_h
                    and abs(head_y - args.camera_y) <= half_h):
                inside.append((hid, round(x, 2), round(y, 2)))
        print("  t=%5.1f s: %d whole bodies inside the lens %s" % (t, len(inside), inside[:8]))
        if t > 16.0:
            print("          (the trajectory ring covers the first ~16 s only; "
                  "sample %5.1f s is the last one recorded, not a frame)" % t)
        if abs(t - args.photo) < 1e-6 and len(inside) < 3:
            failures.append("photo t=%.1f s shows %d bodies, the correction wants three heads"
                            % (t, len(inside)))

    print()
    if failures:
        for f in failures:
            print("FAIL", f)
        return 1
    print("ARENA-NUMBER-CHECK OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
