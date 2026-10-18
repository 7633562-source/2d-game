#!/usr/bin/env python3
"""ART-H1 inventory of the mannequin's segment canvases (read-only).

For every part in Assets/Resources/Art/Human/<part>.png it prints:

  * the canvas size and the spritePixelsToUnits from its .meta, i.e. the WORLD
    size the canvas is mapped to (canvas px / ppu);
  * the shipping collider box the canvas is supposed to equal (Human.cs), so
    the box contract is checked in numbers, not assumed;
  * the painted area: alpha coverage, the content bbox in canvas px and in
    METRES relative to the box (how far the ink falls short of / runs past
    each edge of the box);
  * the ink extent along the last 10 % rows at each end (is the end rounded and
    how wide is the throat there) - the number the joint overlap plan needs.

It also writes a contact sheet Trials/Runs/art-h1-parts.png: one row per part,
the canvas on a checker background, the box drawn as a magenta rectangle, so
the eye can see the padding and the shape of every end.

Run:  python Tools/art-h1-part-inventory.py
"""
import os
import re
import sys

from PIL import Image, ImageDraw

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "Resources", "Art", "Human")
HUMAN_CS = os.path.join(PROJECT, "Assets", "Human.cs")
SHEET = os.path.join(PROJECT, "Trials", "Runs", "art-h1-parts.png")

PARTS = ("head", "neck", "torso", "pelvis", "upper", "lower", "hand",
         "thigh", "shin", "foot")

PART_FIELD = {
    "head": "headSize", "neck": "neckSize", "torso": "torsoSize",
    "pelvis": "pelvisSize", "upper": "upperArmSize", "lower": "lowerArmSize",
    "hand": "handSize", "thigh": "thighSize", "shin": "shinSize",
    "foot": "footSize",
}


def boxes():
    with open(HUMAN_CS, encoding="utf-8", errors="replace") as fh:
        text = fh.read()
    out = {}
    for m in re.finditer(
        r"public Vector2 (\w+Size)\s*=\s*new Vector2\(\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*\)",
        text,
    ):
        out[m.group(1)] = (float(m.group(2)), float(m.group(3)))
    return out


def ppu_of(part):
    with open(os.path.join(ART, part + ".png.meta"), encoding="utf-8",
              errors="replace") as fh:
        m = re.search(r"^  spritePixelsToUnits: ([0-9.]+)$", fh.read(), flags=re.M)
    return float(m.group(1)) if m else 0.0


def alpha_rows(im, y):
    px = im.load()
    return [x for x in range(im.width) if px[x, y][3] > 16]


def main():
    boh = boxes()
    rows = []
    print("%-7s %-11s %-9s %-13s %-13s %6s %6s %8s %8s %7s" % (
        "part", "canvas", "ppu", "box(m)", "canvas(m)", "ink%", "gapR", "gapT",
        "gapB", "gapL"))
    for part in PARTS:
        path = os.path.join(ART, part + ".png")
        if not os.path.exists(path):
            print("%-7s MISSING" % part)
            continue
        im = Image.open(path).convert("RGBA")
        ppu = ppu_of(part)
        box = boh[PART_FIELD[part]]
        world = (im.width / ppu, im.height / ppu) if ppu else (0.0, 0.0)

        a = im.split()[3]
        ink = sum(1 for v in a.getdata() if v > 16)
        share = 100.0 * ink / (im.width * im.height)
        bbox = a.getbbox()
        # bbox in metres relative to the box, box centred on the canvas
        if bbox:
            x0, y0, x1, y1 = bbox
            mx0 = x0 / ppu - world[0] / 2.0
            mx1 = x1 / ppu - world[0] / 2.0
            # canvas rows run top-down, world Y runs up
            my1 = world[1] / 2.0 - y0 / ppu
            my0 = world[1] / 2.0 - y1 / ppu
            gapT = box[1] * 0.5 - my1
            gapB = my0 + box[1] * 0.5
            gapL = mx0 + box[0] * 0.5
            gapR = box[0] * 0.5 - mx1
        else:
            gapT = gapB = gapL = gapR = float("nan")
        print("%-7s %4dx%-6d %-9.0f %5.3fx%-5.3f %5.3fx%-5.3f %5.1f%% %6.3f %8.3f %8.3f %7.3f"
              % (part, im.width, im.height, ppu, box[0], box[1],
                 world[0], world[1], share, gapR, gapT, gapB, gapL))
        rows.append((part, im, box, ppu, world))

    # contact sheet, box drawn in magenta
    scale = 220.0            # px per metre on the sheet
    pad = 12
    cells = []
    for part, im, box, ppu, world in rows:
        w = max(1, int(round(box[0] * scale)))
        h = max(1, int(round(box[1] * scale)))
        cell = Image.new("RGBA", (w + 2 * pad, h + 2 * pad), (40, 40, 40, 255))
        pic = im.resize((w, h), Image.LANCZOS)
        cell.alpha_composite(pic, (pad, pad))
        d = ImageDraw.Draw(cell)
        d.rectangle([pad, pad, pad + w - 1, pad + h - 1], outline=(255, 0, 255, 255))
        d.text((2, 2), part, fill=(255, 255, 0, 255))
        cells.append(cell)

    sheet_w = sum(c.width for c in cells) + 10 * (len(cells) + 1)
    sheet_h = max(c.height for c in cells) + 20
    sheet = Image.new("RGBA", (sheet_w, sheet_h), (12, 12, 12, 255))
    x = 10
    for c in cells:
        sheet.alpha_composite(c, (x, 10))
        x += c.width + 10
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    sheet.save(SHEET)
    print("sheet %s %dx%d" % (SHEET, sheet.width, sheet.height))
    return 0


if __name__ == "__main__":
    sys.exit(main())
