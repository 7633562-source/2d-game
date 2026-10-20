"""ART-0 (17th pass): md5 of every shipped picture + its .meta, so a repaint can be proven to touch
exactly the files it claims. Usage: python Tools/art0-byte-snapshot-17th.py <label>
Writes Trials/Runs/art-0-wrap-17th/<label>-bytes.txt and prints the file it wrote.
"""
import hashlib
import os
import sys

ROOT = r"E:/Games/2D Game/My project"
ART = os.path.join(ROOT, "Assets", "Resources", "Art")
LEVEL = os.path.join(ROOT, "Assets", "Resources", "Level")
OUT = os.path.join(ROOT, "Trials", "Runs", "art-0-wrap-17th")


def md5(path):
    h = hashlib.md5()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    label = sys.argv[1] if len(sys.argv) > 1 else "before"
    os.makedirs(OUT, exist_ok=True)
    names = []
    for key in ("skin", "fabric", "denim", "dirt", "grass", "bark", "leaf"):
        names.append(os.path.join(ART, key + ".png"))
    for key in ("ground_fill", "ground_cap", "sky", "sign_finish"):
        names.append(os.path.join(LEVEL, key + ".png"))
    names.append(os.path.join(ART, "Campfire", "core.png"))
    path = os.path.join(OUT, label + "-bytes.txt")
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        for p in names:
            rel = os.path.relpath(p, ROOT).replace("\\", "/")
            fh.write("%s  %s  %d\n" % (md5(p), rel, os.path.getsize(p)))
            meta = p + ".meta"
            if os.path.exists(meta):
                fh.write("%s  %s.meta  %d\n" % (md5(meta), rel, os.path.getsize(meta)))
    print("wrote %s (%d png + metas)" % (path, len(names)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
