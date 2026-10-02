#!/usr/bin/env python3
"""BALANCE-ARENA-1 delivered evidence: are the frames ours, and are they 1080p?

    python Tools/impact-evidence-check.py

The card's evidence clause is not only about numbers: the stills and the video must come
from the build whose identity the run names, and a frame written before the build is a
photograph of somebody else's player (the level lane has been caught by exactly that three
times today, in the species and gallery lanes). So this checker reads, for every delivered
`impact_imp_cb*_photo_*.png` and every `impact_*.mp4`:

  * the pixel size out of the PNG's own IHDR / out of ffprobe - 1920x1080 or it fails;
  * the file's mtime against `Build/Headless/build-manifest.json` - a frame older than the
    manifest it claims is named STALE, not silently accepted;
  * ffmpeg PSNR against the same-named frame in the superseded archive
    (`Trials/Runs/impact-pre-a12521af/`): `inf` means the picture did not move at all,
    which for a caption-drawing scene is itself a finding worth printing.

Read-only: opens picture and video files, runs ffprobe/ffmpeg, writes nothing.

Usage:  python Tools/impact-evidence-check.py [archive-folder]
"""
import glob
import json
import os
import struct
import subprocess
import sys
import time

PROJECT = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJECT, "Trials", "Runs")
MANIFEST = os.path.join(PROJECT, "Build", "Headless", "build-manifest.json")
FFMPEG = ("C:/Users/Admin/AppData/Local/Microsoft/WinGet/Packages/"
          "Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/"
          "ffmpeg-9.0.1-full_build/bin/ffmpeg.exe")
FFPROBE = FFMPEG.replace("ffmpeg.exe", "ffprobe.exe")
DEFAULT_ARCHIVE = os.path.join(RUNS, "impact-pre-a12521af")
WANT = (1920, 1080)


def png_size(path):
    with open(path, "rb") as handle:
        head = handle.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return (0, 0)
    return struct.unpack(">II", head[16:24])


def probe(path):
    done = subprocess.run(
        [FFPROBE, "-v", "error", "-select_streams", "v:0", "-show_entries",
         "stream=width,height,nb_frames,duration", "-of", "default=noprint_wrappers=1", path],
        capture_output=True, text=True)
    fields = dict(line.split("=", 1) for line in done.stdout.strip().splitlines() if "=" in line)
    return fields


def psnr(a, b):
    if not (os.path.exists(a) and os.path.exists(b)):
        return "no counterpart"
    done = subprocess.run(
        [FFMPEG, "-loglevel", "info", "-i", a, "-i", b, "-lavfi", "psnr", "-f", "null", "-"],
        capture_output=True, text=True)
    text = done.stderr
    marker = "average:"
    if marker not in text:
        return "not comparable"
    value = text.split(marker)[1].split()[0]
    return "identical" if value.startswith("inf") else value + " dB"


def main(argv):
    archive = argv[0] if argv else DEFAULT_ARCHIVE
    manifest_t = os.stat(MANIFEST).st_mtime
    build = json.load(open(MANIFEST, encoding="utf-8-sig"))
    print("manifest buildGuid=%s fingerprint=%s written %s"
          % (build["buildGuid"][:8], build["fingerprint"][:16],
             time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(manifest_t))))
    print("archive %s" % archive)

    problems = 0
    print("\n== stills")
    print("%-46s %-11s %-9s %-8s %s" % ("file", "size", "after?", "vs 22:32", "mtime"))
    stills = sorted(glob.glob(os.path.join(RUNS, "impact_imp_cb*_photo_*.png")))
    for path in stills:
        name = os.path.basename(path)
        size = png_size(path)
        ok = size == WANT
        problems += 0 if ok else 1
        after = "yes" if os.stat(path).st_mtime > manifest_t else "STALE"
        problems += 0 if after == "yes" else 1
        print("%-46s %-11s %-9s %-8s %s"
              % (name, "%dx%d%s" % (size[0], size[1], "" if ok else " FAIL"), after,
                 psnr(path, os.path.join(archive, name)),
                 time.strftime("%H:%M:%S", time.localtime(os.stat(path).st_mtime))))

    print("\n== video")
    print("%-60s %-11s %-9s %-9s %s" % ("file", "frames", "seconds", "size", "written"))
    for path in sorted(glob.glob(os.path.join(RUNS, "impact_*.mp4"))):
        fields = probe(path)
        w, h = int(fields.get("width", 0)), int(fields.get("height", 0))
        ok = (w, h) == WANT
        problems += 0 if ok else 1
        print("%-60s %-11s %-9s %-9s %s"
              % (os.path.basename(path), fields.get("nb_frames"), fields.get("duration"),
                 "%dx%d%s" % (w, h, "" if ok else " FAIL"),
                 time.strftime("%H:%M:%S", time.localtime(os.stat(path).st_mtime))))

    print("\nIMPACT-EVIDENCE-CHECK %s stills=%d problems=%d"
          % ("OK" if problems == 0 else "FAIL", len(stills), problems))
    return 0 if problems == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
