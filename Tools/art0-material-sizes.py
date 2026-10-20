"""ART-0: the material tiles' original and shipped sizes, from the files themselves.

The delivery card records the seven material tiles as 1024-class originals downscaled to 512. This
prints what is on disk today (IHDR size + bytes) next to the shipped copy, so the "original -> final"
column of the report can be checked and not trusted.

Usage:  python Tools/art0-material-sizes.py
"""
import os
import struct

PROJ = r"E:/Games/2D Game/My project"
KEYS = ["skin", "fabric", "denim", "dirt", "grass", "bark", "leaf"]


def ihdr(path):
    with open(path, "rb") as fh:
        head = fh.read(33)
    if head[:8] != b"\x89PNG\r\n\x1a\n" or head[12:16] != b"IHDR":
        return None
    w, h = struct.unpack(">II", head[16:24])
    return w, h


def main():
    print("%-8s %-22s %-14s %-22s %-14s" % ("key", "LegacyArt original", "bytes", "shipped", "bytes"))
    for key in KEYS:
        src = os.path.join(PROJ, "Assets", "LegacyArt", key + ".png")
        dst = os.path.join(PROJ, "Assets", "Resources", "Art", key + ".png")
        a = ihdr(src) if os.path.exists(src) else None
        b = ihdr(dst) if os.path.exists(dst) else None
        print("%-8s %-22s %-14s %-22s %-14s" % (
            key,
            "%dx%d" % a if a else "missing",
            os.path.getsize(src) if os.path.exists(src) else "-",
            "%dx%d" % b if b else "missing",
            os.path.getsize(dst) if os.path.exists(dst) else "-"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
