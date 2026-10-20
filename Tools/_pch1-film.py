# -*- coding: utf-8 -*-
"""B-PERCH-1 film: crop-scaled mp4 from a -shotRate 20 take.
Usage: python Tools/_pch1-film.py <label> <t0:t1> <out.mp4> [scale] [crop x0,y0,x1,y1]
"""
import os
import shutil
import subprocess
import sys
import tempfile

from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
CAP = 20.0


def shots(label):
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


def main():
    label, seg, out = sys.argv[1], sys.argv[2], sys.argv[3]
    scale = int(sys.argv[4]) if len(sys.argv) > 4 else 4
    crop = sys.argv[5] if len(sys.argv) > 5 else "860,520,1090,770"
    fps = float(sys.argv[6]) if len(sys.argv) > 6 else CAP   # B-PERCH-23: 5 = 4x slow-mo, 80 = 4x fast
    x0, y0, x1, y1 = [int(v) for v in crop.split(",")]
    a, b = [float(v) for v in seg.split(":")]
    fr = shots(label)
    frames = [n for n in sorted(fr) if int(round(a * CAP)) <= n <= int(round(b * CAP))]
    if not frames:
        raise SystemExit("no frames in %s for %s" % (label, seg))
    tmp = tempfile.mkdtemp(prefix="pch1film_")
    try:
        for k, n in enumerate(frames):
            im = Image.open(fr[n]).convert("RGB").crop((x0, y0, x1, y1))
            im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
            im.save(os.path.join(tmp, "f%05d.png" % k))
        cmd = ["ffmpeg", "-y", "-loglevel", "error", "-framerate", "%.6f" % fps,
               "-i", os.path.join(tmp, "f%05d.png"),
               "-vf", "format=yuv420p", "-c:v", "libx264", "-crf", "18",
               "-movflags", "+faststart", out]
        rc = subprocess.call(cmd)
        if rc != 0:
            raise SystemExit("ffmpeg rc=%d" % rc)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    print("film: %d frames, %.2f s -> %s (%d bytes)"
          % (len(frames), len(frames) / fps, out, os.path.getsize(out)))


if __name__ == "__main__":
    main()
