#!/usr/bin/env python3
"""The grass cap A/B of the ONE-SPECIES stills: swap the cap canvas, keep everything else.

    python Tools/species-cap-ab.py status
    python Tools/species-cap-ab.py old      # write the pre-22:23 cap bytes into the asset
    python Tools/species-cap-ab.py new      # restore the shipped cap bytes

Why: the ten `tree<species>_photo.png` stills were re-shot on the 22:25 preview player, the
only build of the day that carries `Assets/Resources/Level/ground_cap.png` (repainted by the
artist at 22:23, ART-0 "cap tips"). Resources bake into a player at BUILD time, so the only
way to show that the refresh really repaints the species pictures - and that the cap is the
change it claims, not some other art - is to build the same player twice with the two cap
canvases and shoot the same ten scenes through the same protocol.

Both byte sequences are proved, not looked up: the OLD canvas is repainted through the
generator's own painter and `save()` (band 3..9, min_top 1 - the shipped parameters) and its
md5 is checked against the value the artist recorded before the repaint, the NEW one against
the file that ships today. A mismatch aborts and writes nothing.
"""
import hashlib
import importlib.util
import os
import shutil
import sys

PROJ = r"E:/Games/2D Game/My project"
GEN = os.path.join(PROJ, "Tools", "gen-level-textures.py")
ASSET = os.path.join(PROJ, "Assets", "Resources", "Level", "ground_cap.png")
KEPT = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Temp", "species-cap-ab")
NEW_MD5 = "8d5f842ec5e85750000720a72e355e89"      # shipped since 22:23
OLD_MD5 = "67f407c7122aece81fb239440709daea"      # shipped before, repainted by the generator


def md5(path):
    return hashlib.md5(open(path, "rb").read()).hexdigest()


def generator():
    spec = importlib.util.spec_from_file_location("genlevel", GEN)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def status():
    digest = md5(ASSET)
    which = "NEW (shipped)" if digest == NEW_MD5 else "OLD (pre-22:23)" if digest == OLD_MD5 else "UNKNOWN"
    print("SPECIES-CAP-AB status %s md5 %s" % (which, digest))
    if os.path.exists(os.path.join(KEPT, "ground_cap_new.png")):
        print("  kept copy: %s" % os.path.join(KEPT, "ground_cap_new.png"))
    return 0 if digest in (NEW_MD5, OLD_MD5) else 1


def write_old():
    mod = generator()
    img = mod.paint_ground_cap(tip_lo=3, tip_hi=9, min_top=1)
    size = mod.save(img, ASSET, 100, False, True, __import__("random").Random(1))
    digest = md5(ASSET)
    print("SPECIES-CAP-AB wrote OLD cap band 3..9 min_top 1 -> %d bytes md5 %s" % (size, digest))
    if digest != OLD_MD5:
        print("  FAIL the repainted canvas is not the recorded pre-repaint bytes")
        return 1
    return 0


def write_new():
    os.makedirs(KEPT, exist_ok=True)
    kept = os.path.join(KEPT, "ground_cap_new.png")
    if not os.path.exists(kept):
        print("FAIL no kept copy of the shipped canvas at %s" % kept)
        return 1
    shutil.copyfile(kept, ASSET)
    digest = md5(ASSET)
    print("SPECIES-CAP-AB restored NEW cap from %s -> md5 %s" % (kept, digest))
    if digest != NEW_MD5:
        print("  FAIL the restored canvas is not the shipped bytes")
        return 1
    return 0


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ("status", "old", "new"):
        print(__doc__)
        return 2
    if sys.argv[1] == "status":
        return status()
    # First call: keep the shipped canvas so "new" is always the same bytes.
    os.makedirs(KEPT, exist_ok=True)
    kept = os.path.join(KEPT, "ground_cap_new.png")
    if not os.path.exists(kept) and md5(ASSET) == NEW_MD5:
        shutil.copyfile(ASSET, kept)
        print("SPECIES-CAP-AB kept the shipped canvas at %s" % kept)
    return write_old() if sys.argv[1] == "old" else write_new()


if __name__ == "__main__":
    raise SystemExit(main())
