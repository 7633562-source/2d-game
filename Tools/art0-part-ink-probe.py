#!/usr/bin/env python3
"""ART-0 27th pass probe - where does each human part's ink sit inside its canvas?

Reads the shipped bytes only (Assets/Resources/Art/Human/*.png), no Unity.
Prints, per part: canvas px, ink bbox, ink fraction of the canvas in x and y,
the ink's world size in metres (canvas px / spritePixelsToUnits from the .meta),
the ink's top/bottom margins, and the shipping collider box from Human.cs.
"""

import pathlib
import re
import sys

from PIL import Image

PROJECT = pathlib.Path(__file__).resolve().parent.parent
ART = PROJECT / "Assets" / "Resources" / "Art" / "Human"
LEGACY = PROJECT / "Assets" / "LegacyArt" / "Human"
HUMAN_CS = PROJECT / "Assets" / "Human.cs"

PARTS = ["head", "neck", "torso", "pelvis", "upper", "lower", "hand", "thigh", "shin", "foot"]
FIELDS = {
    "head": "headSize", "neck": "neckSize", "torso": "torsoSize", "pelvis": "pelvisSize",
    "upper": "upperArmSize", "lower": "lowerArmSize", "hand": "handSize",
    "thigh": "thighSize", "shin": "shinSize", "foot": "footSize",
}


def boxes():
    text = HUMAN_CS.read_text(encoding="utf-8", errors="replace")
    out = {}
    for m in re.finditer(
        r"public Vector2 (\w+Size)\s*=\s*new Vector2\(\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*\)", text
    ):
        out[m.group(1)] = (float(m.group(2)), float(m.group(3)))
    return out


def main():
    if len(sys.argv) > 2 and sys.argv[1] == "--file":
        path = pathlib.Path(sys.argv[2])
        im = Image.open(path).convert("RGBA")
        a = im.split()[3]
        for cut in (8, 40, 128):
            bb = a.point(lambda v, c=cut: 255 if v > c else 0).getbbox()
            print("%s alpha>%d -> %s" % (path.name, cut, bb))
        hist = a.histogram()
        print("opaque>40 px = %d, >8 px = %d, canvas %dx%d" % (
            sum(hist[41:]), sum(hist[9:]), im.width, im.height))
        return 0

    box = boxes()
    print("%-7s %10s %12s %7s %7s %14s %9s %9s %9s" % (
        "part", "canvas", "ink bbox", "inkX%", "inkY%", "box m", "inkX m", "inkY m", "inkLen m"))
    for p in PARTS:
        png = ART / (p + ".png")
        meta = (ART / (p + ".png.meta")).read_text(encoding="utf-8", errors="replace")
        ppu = float(re.search(r"spritePixelsToUnits:\s*([0-9.]+)", meta).group(1))
        im = Image.open(png).convert("RGBA")
        w, h = im.size
        bb = im.split()[3].point(lambda a: 255 if a > 8 else 0).getbbox()
        bx, by, bw, bh = bb
        bw, bh = bb[2] - bb[0], bb[3] - bb[1]
        b = box[FIELDS[p]]
        ink_x = bw / ppu
        ink_y = bh / ppu
        src = LEGACY / (p + ".png")
        src_txt = "-"
        if src.exists():
            s = Image.open(src).convert("RGBA")
            sbb = s.split()[3].point(lambda a: 255 if a > 8 else 0).getbbox()
            if sbb:
                src_txt = "%dx%d ink %d,%d %dx%d" % (
                    s.width, s.height, sbb[0], sbb[1], sbb[2] - sbb[0], sbb[3] - sbb[1])
        print("%-7s %10s %12s %6.0f%% %6.0f%% %14s %9.3f %9.3f %9.3f  top=%d bottom=%d canvas=%dx%d | legacy %s" % (
            p, "%dx%d" % (w, h), "%d,%d %dx%d" % (bx, by, bw, bh),
            100.0 * bw / w, 100.0 * bh / h, "%.3fx%.3f" % b, ink_x, ink_y, ink_y,
            by, h - (by + bh), w, h, src_txt))
    return 0


if __name__ == "__main__":
    sys.exit(main())
