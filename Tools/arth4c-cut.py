#!/usr/bin/env python3
"""ART-H4c (art lane): the CLEAN look stills + film, cut from a -rigOverlay 0 take.

Same picks as arth3-cut.py (the window's worst knee, the most bent swing ankle, the
fullbody mid-stride), PLUS the NEAR limb's own worst knee - the fair subject of the
art, because the far limb carries ArtLibrary.DimFar (x0.70 RGB, alpha 0.62).
Reads the take's own CSV, so every pick is the body's, not typed in here twice.

Also counts near-black pixels (sum < 24) inside the knee window of each still: on a
capture WITHOUT -rigOverlay 0 the rig's debug draw (skeleton bars + joint markers)
sits in this window, so the count is the flag's own proof.

Usage:
  python Tools/arth4c-cut.py --label human_dress4c_vis --onset 5.0 --span 15.0 \
      --tag arth4c --out-mp4 Trials/Runs/human-dressed-walk4c.mp4
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image

CAP = 25.0  # -shotRate on the take
KNEE_BOX = (700, 440, 1220, 960)  # carries both knees of the walker


def load_csv_rows(path):
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        head = fh.readline().rstrip("\n").split(";")
        rows = []
        for line in fh:
            line = line.rstrip("\n")
            if not line:
                continue
            rows.append(line.split(";"))
    return {name: i for i, name in enumerate(head)}, rows


def near_black(arr):
    return int((arr.sum(axis=2) < 24).sum())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", default="Trials/Runs")
    ap.add_argument("--label", required=True)
    ap.add_argument("--onset", type=float, default=5.0)
    ap.add_argument("--span", type=float, default=15.0)
    ap.add_argument("--out-mp4", required=True)
    ap.add_argument("--tag", default="arth4c")
    ap.add_argument("--still-dir", default="Trials/Runs")
    args = ap.parse_args()

    run_dir = args.dir
    csv_path = os.path.join(run_dir, args.label + ".csv")
    json_path = os.path.join(run_dir, args.label + ".json")
    if not os.path.exists(csv_path):
        print("NO CSV", csv_path)
        return 2
    with open(json_path, "r", encoding="utf-8", errors="replace") as fh:
        meta = json.load(fh)
    fell = bool(meta.get("fell"))
    fall_t = float(meta.get("fallTime") or 0.0)
    end = args.onset + args.span
    if fell and fall_t > args.onset + 0.5:
        end = min(end, fall_t - 0.20)
    print("take: fell=%s fallTime=%.3f window=%.2f..%.2f (%.2f s)"
          % (fell, fall_t, args.onset, end, end - args.onset))

    idx, rows = load_csv_rows(csv_path)
    need = ["time", "ankleL", "ankleR", "kneeL", "kneeR", "leftGrounded", "rightGrounded"]
    if any(k not in idx for k in need):
        print("CSV MISSING COLUMNS:", [k for k in need if k not in idx])
        return 2

    def window_rows():
        for r in rows:
            try:
                t = float(r[idx["time"]])
            except (ValueError, IndexError):
                continue
            if args.onset <= t <= end:
                yield t, r

    # the window's worst knee (either limb) - the round's original still
    knee_any = None
    for t, r in window_rows():
        for side in ("L", "R"):
            try:
                k = abs(float(r[idx["knee" + side]]))
            except (ValueError, IndexError):
                continue
            if knee_any is None or k > knee_any[0]:
                knee_any = (k, t, side)
    # the NEAR limb's own worst (R; "Left*" is the far side, ArtLibrary.IsFarSide)
    knee_near = None
    for t, r in window_rows():
        try:
            k = abs(float(r[idx["kneeR"]]))
        except (ValueError, IndexError):
            continue
        if knee_near is None or k > knee_near[0]:
            knee_near = (k, t, "R")
    # the most bent swing ankle
    ankle = None
    for t, r in window_rows():
        for side in ("L", "R"):
            try:
                ang = abs(float(r[idx["ankle" + side]]))
                grip = float(r[idx["leftGrounded" if side == "L" else "rightGrounded"]])
            except (ValueError, IndexError):
                continue
            if grip > 0.5:
                continue
            if ankle is None or ang > ankle[0]:
                ankle = (ang, t, side)
    if knee_any is None or ankle is None or knee_near is None:
        print("NO SAMPLE FOUND", knee_any, knee_near, ankle)
        return 2
    print("knee pick (both):  t=%.3f knee%s=%.1f deg" % (knee_any[1], knee_any[2], knee_any[0]))
    print("knee pick (near):  t=%.3f knee%s=%.1f deg" % (knee_near[1], knee_near[2], knee_near[0]))
    print("ankle pick:        t=%.3f ankle%s=%.1f deg (foot off the ground)"
          % (ankle[1], ankle[2], ankle[0]))

    def shot_at(t):
        want = int(round(t * CAP))
        for delta in range(0, 40):
            for cand in (want - delta, want + delta):
                p = os.path.join(run_dir, "shot_%s_%03d.png" % (args.label, cand))
                if os.path.exists(p):
                    return p, cand
        return None, None

    knee_png, knee_i = shot_at(knee_any[1])
    near_png, near_i = shot_at(knee_near[1])
    ankle_png, ankle_i = shot_at(ankle[1])
    body_png, body_i = shot_at(max(args.onset + 0.4, ankle[1] - 0.28))
    print("shots: knee idx=%s near idx=%s ankle idx=%s body idx=%s"
          % (knee_i, near_i, ankle_i, body_i))

    os.makedirs(args.still_dir, exist_ok=True)
    saved = []

    def save_crop(src, name, factor):
        im = Image.open(src).convert("RGB")
        crop = im.crop(KNEE_BOX)
        if factor != 1:
            crop = crop.resize((crop.width * factor, crop.height * factor), Image.NEAREST)
        dst = os.path.join(args.still_dir, "%s-still-%s.png" % (args.tag, name))
        crop.save(dst)
        saved.append((dst, KNEE_BOX, factor))
        return dst

    if knee_png:
        save_crop(knee_png, "knee-bent", 1)
        save_crop(knee_png, "knee-zoom", 2)
    if near_png:
        save_crop(near_png, "knee-near", 1)
        save_crop(near_png, "knee-near-zoom", 2)
    for src, name in ((ankle_png, "ankle-bent"), (body_png, "fullbody")):
        if src:
            dst = os.path.join(args.still_dir, "%s-still-%s.png" % (args.tag, name))
            shutil.copyfile(src, dst)
            saved.append((dst, None, 1))

    for dst, box, factor in saved:
        arr = np.asarray(Image.open(dst).convert("RGB"))
        nonblack = int((arr.sum(axis=2) > 24).sum())
        nb = near_black(arr)
        where = "box %s x%d" % (str(box), factor) if box else "full frame"
        print("still %s  %dx%d  %s  picture px=%d  near-black px=%d  (overlay proof: 0 = clean)"
              % (dst, arr.shape[1], arr.shape[0], where, nonblack, nb))

    # --- the film ---
    tmp = tempfile.mkdtemp(prefix="arth4ccut_")
    n = 0
    missing = []
    magenta_frames = []
    first_idx = int(round(args.onset * CAP))
    last_idx = int(round(end * CAP))
    for i in range(first_idx, last_idx + 1):
        src = os.path.join(run_dir, "shot_%s_%03d.png" % (args.label, i))
        if not os.path.exists(src):
            missing.append(i)
            continue
        shutil.copyfile(src, os.path.join(tmp, "f%04d.png" % n))
        if n % 10 == 0 or i in (first_idx, last_idx):
            arr = np.asarray(Image.open(src).convert("RGB"))
            mag = int(((arr[:, :, 0] == 255) & (arr[:, :, 1] == 0) & (arr[:, :, 2] == 255)).sum())
            if mag:
                magenta_frames.append((i, mag))
        n += 1
    if n == 0:
        print("NO FRAMES IN THE WINDOW")
        return 2
    fps = n / (end - args.onset)
    print("film: %d frames of %d asked (%d dropped), fps=%.3f (%.3f s)"
          % (n, last_idx - first_idx + 1, len(missing), fps, n / fps))
    print("magenta: %s" % ("CLEAN" if not magenta_frames else "FOUND " + str(magenta_frames[:6])))
    ff = ["ffmpeg", "-y", "-loglevel", "error", "-framerate", "%.6f" % fps,
          "-i", os.path.join(tmp, "f%04d.png"),
          "-vf", "format=yuv420p", "-c:v", "libx264", "-crf", "18",
          "-movflags", "+faststart", args.out_mp4]
    rc = subprocess.call(ff)
    print("ffmpeg rc=%d -> %s (%s bytes)" % (rc, args.out_mp4,
          os.path.getsize(args.out_mp4) if os.path.exists(args.out_mp4) else "-"))
    shutil.rmtree(tmp, ignore_errors=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
