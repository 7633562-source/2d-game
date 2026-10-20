#!/usr/bin/env python3
"""Did a re-shoot of a capture label actually repaint the stills?

    python Tools/capture-still-psnr.py gallery6 gallery7
    python Tools/capture-still-psnr.py gallery6 gallery7 --pattern "{0}_{1}.png"

For every still the OLD label has, this prints ffmpeg's PSNR of the pair. `inf` means the
picture did not move at all - which for a capture whose inputs moved is a finding, not a
pass. The stills are matched by their role suffix (`gallery6_human.png` -> `gallery7_human.png`),
so a label pair with a different still set is reported, not silently skipped.

Read-only: opens PNGs, runs ffmpeg, writes nothing (the PSNR line goes to ffmpeg's stderr).
"""
from __future__ import annotations

import glob
import os
import re
import subprocess
import sys

RUNS = r"E:/Games/2D Game/My project/Trials/Runs"
FFMPEG = ("C:/Users/Admin/AppData/Local/Microsoft/WinGet/Packages/"
          "Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/"
          "ffmpeg-9.0.1-full_build/bin/ffmpeg.exe")


def roles(label, folder=RUNS):
    out = {}
    for path in glob.glob(os.path.join(folder, label + "_*.png")):
        name = os.path.basename(path)[len(label) + 1:-4]
        # `photo`, `human`, `tree`, `apple`, `materials`, `level`, `fire`, `placeholder`
        out[name] = path
    return out


def psnr(a, b):
    done = subprocess.run(
        [FFMPEG, "-hide_banner", "-i", a, "-i", b, "-lavfi", "psnr", "-f", "null", "-"],
        capture_output=True, text=True)
    match = re.search(r"average:([0-9.inf]+)", done.stderr)
    return match.group(1) if match else "FAILED"


def main(argv):
    if len(argv) < 3:
        print(__doc__)
        return 2
    old, new = argv[1], argv[2]
    old_dir = RUNS
    if "--old-dir" in argv:
        old_dir = argv[argv.index("--old-dir") + 1]
    a, b = roles(old, old_dir), roles(new)
    if not a:
        print("no stills for {0}".format(old))
        return 2
    print("{0:16s} {1:>18s} {2:>10s}".format("still", "PSNR old->new", "new bytes"))
    moved = same = 0
    for role in sorted(a):
        if role not in b:
            print("{0:16s} {1:>18s}".format(role, "no counterpart in " + new))
            continue
        value = psnr(a[role], b[role])
        size = os.path.getsize(b[role])
        print("{0:16s} {1:>18s} {2:>10d}".format(role, value, size))
        if value == "inf":
            same += 1
        else:
            moved += 1
    extra = sorted(set(b) - set(a))
    for role in extra:
        print("{0:16s} {1:>18s}".format(role, "only in " + new))
    print("STILL-PSNR old={0} new={1} repainted={2} identical={3} extra={4}".format(
        old, new, moved, same, len(extra)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
