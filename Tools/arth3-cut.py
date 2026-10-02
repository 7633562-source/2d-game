#!/usr/bin/env python
"""ART-H3 (art lane) - the dressed walk take's film and stills, from the take's own files.

Same recipe as ART-H2 (arth2-cut.py) plus the round's NEW still: the WORST joint at max bend.
Reads the take's shot set (Trials/Runs/shot_<label>_*.png), its CSV and its JSON:
  * collects the frames from the walk onset (t = walkTime) to the fall onset (or +15 s),
  * encodes them at the fps the REAL frame count implies (the cinema sets drop frames, so a
    hardcoded 25 fps would play fast),
  * picks the shin-foot ("ankle") still: the most bent ankle while its foot is OFF the ground,
  * picks the full-body still: a clear mid-stride frame a few tenths before that moment,
  * ART-H3: picks the knee still at the take's biggest knee flexion (the walk lane's own CSV
    says where the worst bend is) and writes a 1:1 crop of the knee window plus a 2x zoom,
  * reports the missing-canvas / magenta check over the collected frames.

Usage:
  python Tools/arth3-cut.py --dir Trials/Runs --label human_dress3_vis \
      --onset 5.0 --span 15.0 --out-mp4 Trials/Runs/human-dressed-walk3.mp4 \
      --still-dir Trials/Runs --tag arth3
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

# The 1920x1080 window that carries both knees of the walker in the simple scene (checked
# against the ART-H2 still's occupancy: the character spans x ~800..1100, the legs y ~620..790).
KNEE_BOX = (700, 440, 1220, 960)


def load_csv_rows(path):
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        head = fh.readline().rstrip("\n").split(";")
        rows = []
        for line in fh:
            line = line.rstrip("\n")
            if not line:
                continue
            rows.append(line.split(";"))
    idx = {name: i for i, name in enumerate(head)}
    return idx, rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", required=True)
    ap.add_argument("--label", required=True)
    ap.add_argument("--onset", type=float, default=5.0)
    ap.add_argument("--span", type=float, default=15.0)
    ap.add_argument("--out-mp4", required=True)
    ap.add_argument("--still-dir", required=True)
    ap.add_argument("--tag", default="arth3")
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
    survived = float(meta.get("survivedSeconds") or 0.0)

    end = args.onset + args.span
    if fell and fall_t > args.onset + 0.5:
        end = min(end, fall_t - 0.20)
    print("take: fell=%s fallTime=%.3f survived=%.3f window=%.2f..%.2f (%.2f s)"
          % (fell, fall_t, survived, args.onset, end, end - args.onset))

    idx, rows = load_csv_rows(csv_path)
    need = ["time", "ankleL", "ankleR", "leftGrounded", "rightGrounded"]
    if any(k not in idx for k in need):
        print("CSV MISSING COLUMNS:", [k for k in need if k not in idx])
        return 2

    # --- the still: the most bent ankle while its own foot is off the ground ---
    best = None
    for r in rows:
        try:
            t = float(r[idx["time"]])
        except (ValueError, IndexError):
            continue
        if t < args.onset or t > end:
            continue
        for side in ("L", "R"):
            try:
                ang = abs(float(r[idx["ankle" + side]]))
                grip = float(r[idx["leftGrounded" if side == "L" else "rightGrounded"]])
            except (ValueError, IndexError):
                continue
            if grip > 0.5:
                continue
            if best is None or ang > best[0]:
                best = (ang, t, side)
    if best is None:
        print("NO SWING ANKLE SAMPLE FOUND")
        return 2
    ang, t_ankle, side = best
    print("still pick: t=%.3f ankle%s=%.1f deg (foot off the ground)" % (t_ankle, side, ang))

    # --- ART-H3: the worst knee of the window (the round's own close-up) ---
    knee = None
    if "kneeL" in idx and "kneeR" in idx:
        for r in rows:
            try:
                t = float(r[idx["time"]])
            except (ValueError, IndexError):
                continue
            if t < args.onset or t > end:
                continue
            for side in ("L", "R"):
                try:
                    k = abs(float(r[idx["knee" + side]]))
                except (ValueError, IndexError):
                    continue
                if knee is None or k > knee[0]:
                    knee = (k, t, side)
    if knee is None:
        print("NO KNEE SAMPLE FOUND - no knee still")
    else:
        print("knee pick: t=%.3f knee%s=%.1f deg (the window's worst)" % (knee[1], knee[2], knee[0]))

    def shot_at(t):
        """Nearest existing shot index at CAP fps."""
        want = int(round(t * CAP))
        for delta in range(0, 40):
            for cand in (want - delta, want + delta):
                p = os.path.join(run_dir, "shot_%s_%03d.png" % (args.label, cand))
                if os.path.exists(p):
                    return p, cand
        return None, None

    ankle_png, ankle_idx = shot_at(t_ankle)
    body_png, body_idx = shot_at(max(args.onset + 0.4, t_ankle - 0.28))
    knee_png, knee_idx = shot_at(knee[1]) if knee else (None, None)
    print("shots: ankle idx=%s body idx=%s knee idx=%s" % (ankle_idx, body_idx, knee_idx))

    # --- the film frames ---
    tmp = tempfile.mkdtemp(prefix="arth3cut_")
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

    os.makedirs(args.still_dir, exist_ok=True)
    outs = []
    if ankle_png:
        dst = os.path.join(args.still_dir, "%s-still-ankle-bent.png" % args.tag)
        shutil.copyfile(ankle_png, dst)
        outs.append(dst)
    if body_png:
        dst = os.path.join(args.still_dir, "%s-still-fullbody.png" % args.tag)
        shutil.copyfile(body_png, dst)
        outs.append(dst)
    if knee_png:
        # ART-H3: the new still - the knee at the window's worst bend, 1:1 and 2x.
        im = Image.open(knee_png).convert("RGB")
        crop = im.crop(KNEE_BOX)
        dst = os.path.join(args.still_dir, "%s-still-knee-bent.png" % args.tag)
        crop.save(dst)
        outs.append(dst)
        zoom = crop.resize((crop.width * 2, crop.height * 2), Image.NEAREST)
        dstz = os.path.join(args.still_dir, "%s-still-knee-zoom.png" % args.tag)
        zoom.save(dstz)
        outs.append(dstz)
        print("knee still box %s of %dx%d from %s" % (str(KNEE_BOX), im.width, im.height, knee_png))
    for o in outs:
        arr = np.asarray(Image.open(o).convert("RGB"))
        nonblack = int((arr.sum(axis=2) > 24).sum())
        print("still %s  %dx%d  picture px=%d" % (o, arr.shape[1], arr.shape[0], nonblack))
    shutil.rmtree(tmp, ignore_errors=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
