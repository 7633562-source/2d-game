#!/usr/bin/env python
"""B-TAKEOFF-2 tail: the round's films from the take's own shot set (bird lane).

Reads `Trials/Runs/shot_<label>_<n>.png` (the -visual take at -shotRate 25) and cuts:
  * `clip`   one mp4 from one or several time windows, optionally slowed (speed < 1);
  * `concat` the montage from already-cut clips;
  * `strip`  one 8-frame sheet for the eyes (the grok-burn convention).

Usage:
  python Tools/bird-t2-cut.py clip  --label bT2film_wander --segments "2.9:7.9" \
        --out Trials/Runs/bT2-takeoff.mp4
  python Tools/bird-t2-cut.py clip  --label bT2film_wander --segments "0.55:1.75,4.95:5.75" \
        --speed 0.5 --out Trials/Runs/bT2-walk-folded.mp4
  python Tools/bird-t2-cut.py concat --inputs a.mp4:b.mp4 --out montage.mp4
  python Tools/bird-t2-cut.py strip --label bT2film_wander --segments "2.9:7.9" --out X.png
"""
import argparse
import os
import shutil
import subprocess
import sys
import tempfile

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")
CAP = 25.0  # -shotRate of the take


def shots(label):
    """frame number -> path, for every shot_<label>_<n>.png on disk."""
    out = {}
    pre = "shot_%s_" % label
    for name in os.listdir(RUNS):
        if not (name.startswith(pre) and name.endswith(".png")):
            continue
        try:
            out[int(name[len(pre):-4])] = os.path.join(RUNS, name)
        except ValueError:
            pass
    return out


def parse_segments(text):
    segs = []
    for part in text.split(","):
        a, b = part.split(":")
        segs.append((float(a), float(b)))
    return segs


def frames_in(fr, segs):
    picked = []
    for a, b in segs:
        i0, i1 = int(round(a * CAP)), int(round(b * CAP))
        picked += [n for n in sorted(fr) if i0 <= n <= i1]
    return picked


def encode(frames, fr, fps, out_path):
    tmp = tempfile.mkdtemp(prefix="birdt2cut_")
    try:
        for k, n in enumerate(frames):
            shutil.copyfile(fr[n], os.path.join(tmp, "f%05d.png" % k))
        cmd = ["ffmpeg", "-y", "-loglevel", "error", "-framerate", "%.6f" % fps,
               "-i", os.path.join(tmp, "f%05d.png"), "-vf", "format=yuv420p",
               "-c:v", "libx264", "-crf", "18", "-movflags", "+faststart", out_path]
        rc = subprocess.call(cmd)
        if rc != 0:
            raise SystemExit("ffmpeg rc=%d" % rc)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    print("clip: %d frames, fps=%.3f (%.2f s) -> %s (%d bytes)"
          % (len(frames), fps, len(frames) / fps, out_path,
             os.path.getsize(out_path) if os.path.exists(out_path) else -1))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=("clip", "concat", "strip"))
    ap.add_argument("--label")
    ap.add_argument("--segments")
    ap.add_argument("--speed", type=float, default=1.0)
    ap.add_argument("--inputs")
    ap.add_argument("--out")
    ap.add_argument("--pick", type=int, default=8)
    args = ap.parse_args()

    if args.mode == "concat":
        tmp = tempfile.mkdtemp(prefix="birdt2cat_")
        try:
            lst = os.path.join(tmp, "list.txt")
            with open(lst, "w", encoding="utf-8") as f:
                for p in args.inputs.split("|"):
                    f.write("file '%s'\n" % os.path.abspath(p).replace("\\", "/"))
            rc = subprocess.call(["ffmpeg", "-y", "-loglevel", "error", "-f", "concat",
                                  "-safe", "0", "-i", lst, "-c", "copy", args.out])
            if rc != 0:
                raise SystemExit("ffmpeg rc=%d" % rc)
        finally:
            shutil.rmtree(tmp, ignore_errors=True)
        print("concat -> %s (%d bytes)" % (args.out, os.path.getsize(args.out)))
        return

    if not args.label or not args.segments:
        raise SystemExit("clip/strip need --label and --segments")
    fr = shots(args.label)
    if not fr:
        raise SystemExit("no shots for " + args.label)
    frames = frames_in(fr, parse_segments(args.segments))

    if args.mode == "clip":
        encode(frames, fr, CAP * args.speed, args.out)
        return

    # strip: one sheet for the eyes, the grok-burn convention (4 columns x 2 rows).
    pick = [frames[i] for i in range(0, len(frames), max(1, len(frames) // args.pick))][:args.pick]
    ims = []
    for n in pick:
        im = Image.open(fr[n]).convert("RGB")
        w = 460
        im = im.resize((w, max(1, int(im.height * w / im.width))))
        d = ImageDraw.Draw(im)
        d.rectangle([0, 0, 132, 22], fill=(0, 0, 0))
        d.text((6, 5), "t=%.2fs f%d" % (n / CAP, n), fill=(255, 255, 0))
        ims.append(im)
    h = max(i.height for i in ims)
    cols = 4
    rows = (len(ims) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * 460, rows * h), (30, 30, 30))
    for i, im in enumerate(ims):
        sheet.paste(im, ((i % cols) * 460, (i // cols) * h))
    sheet.save(args.out)
    print("strip: %d frames, t=%.2f..%.2f -> %s" % (len(pick), pick[0] / CAP, pick[-1] / CAP, args.out))


if __name__ == "__main__":
    main()
