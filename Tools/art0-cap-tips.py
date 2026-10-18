"""ART-0 cycle: paint the grass-cap blade-tip band as candidates and read them ON the soil.

Finding (this cycle, measured on a real frame): `SpawnSurfaceCap` puts the cap's box top exactly
ON the ground surface and hangs the 0.14 m box INTO the soil, so whatever the canvas leaves empty
at its top is bare soil above the grass. The shipped canvas leaves its top 4 rows (mean alpha 0)
empty - tip rows 4..9, `ground_cap de-tick ... tip rows 4..9` - and the frame shows it: 4 screen
rows of soil, then 5 rows of green. The eye reads "a green band lying below the surface line".

This probe repaints that band with the shipping painter and composes each candidate the way the
level draws it: the fill's top rows as the soil behind, the cap Tiled at 1.28 m with its box top
on the surface. It writes a blind panel per candidate plus one stacked sheet, and the mapping in
plain text, so the read is of the pictures and not of the labels.

Usage:  python Tools/art0-cap-tips.py [v1:lo,hi v2:lo,hi ...]
"""
import importlib.util
import os
import random
import sys

from PIL import Image, ImageDraw

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials", "Runs")
OUT = os.path.join(RUNS, "art-0-cap-tips")
GEN = os.path.join(PROJ, "Tools", "gen-level-textures.py")

PX_PER_M = 1080.0 / 16.70          # the level lens the junction was measured in (ortho 8.35)
CAP_NATIVE_M = (1.28, 0.14)        # 128x14 at PPU 100
FILL_PPU = 128.0                   # ground_fill: 1024x256 = 8x2 m
REPEATS = 4
SOIL_ROWS = 30                     # fill canvas rows shown behind the cap


def painter_module():
    spec = importlib.util.spec_from_file_location("genlevel", GEN)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def paint_variant(mod, lo, hi, min_top):
    return mod.paint_ground_cap(tip_lo=lo, tip_hi=hi, min_top=min_top)


def scaled(img, scale):
    w = max(1, int(round(img.width * scale)))
    h = max(1, int(round(img.height * scale)))
    return img.convert("RGBA").resize((w, h), Image.BILINEAR)


def pane(cap, fill):
    """One junction window: soil behind, the cap Tiled across it with its top on the surface."""
    scale = PX_PER_M / FILL_PPU
    soil_top = fill.crop((0, 0, 900, SOIL_ROWS))       # wide enough to sit under every repeat
    soil = scaled(soil_top, scale)
    # The cap is 128 px at its own PPU 100, so it is drawn at a different scale than the fill.
    tile = scaled(cap, PX_PER_M / 100.0)
    row_h = soil.height
    window = Image.new("RGBA", (tile.width * REPEATS, row_h), (255, 255, 255, 255))
    for i in range(REPEATS):                       # the soil is continuous under the whole window
        window.paste(soil, (i * soil.width, 0))
    for i in range(REPEATS):                       # cap tiled continuously, box top ON the surface
        window.paste(tile, (i * tile.width, 0), tile)
    return window, tile


def main(argv):
    os.makedirs(OUT, exist_ok=True)
    spec = [("A", 3, 9, 1), ("B", 0, 6, 0), ("C", 0, 9, 0)]
    if argv:
        spec = []
        for v in argv:
            label, rest = v.split(":")
            lo, hi, top = [int(n) for n in rest.split(",")]
            spec.append((label, lo, hi, top))

    mod = painter_module()
    fill = Image.open(os.path.join(PROJ, "Assets/Resources/Level/ground_fill.png")).convert("RGBA")

    panes, mapping = [], []
    for label, lo, hi, min_top in spec:
        cap = paint_variant(mod, lo, hi, min_top)
        cap_path = os.path.join(OUT, "cap_%s_%d_%d_%d.png" % (label, lo, hi, min_top))
        cap.save(cap_path)
        window, tile = pane(cap, fill)
        zoom = window.resize((window.width * 5, window.height * 5), Image.NEAREST)
        path = os.path.join(OUT, "panel_%s_%d_%d_%d.png" % (label, lo, hi, min_top))
        zoom.save(path)
        panes.append((label, lo, hi, min_top, zoom))
        tops = [next((y for y in range(cap.height) if cap.getpixel((x, y))[3] > 0), -1)
                for x in range(cap.width)]
        tops = [t for t in tops if t >= 0]
        print("%s band %d..%d min_top %d -> tile %dx%d screen px, cap tip rows %d..%d, empty top rows %d"
              % (label, lo, hi, min_top, tile.width, tile.height, min(tops), max(tops), min(tops)))
        mapping.append((label, lo, hi, min_top, cap_path, path))

    # Blind sheet: the panels in a shuffled order, no labels, mapping kept in plain text.
    order = list(range(len(panes)))
    random.Random(1909).shuffle(order)
    sheet = Image.new("RGB", (max(p[4].width for p in panes) + 8,
                              sum(p[4].height + 6 for p in panes)),
                      (255, 0, 255))
    y = 0
    lines = []
    for i, idx in enumerate(order):
        label, lo, hi, min_top, zoom = panes[idx]
        sheet.paste(zoom.convert("RGB"), (4, y))
        lines.append("panel %d (top to bottom) = %s  band %d..%d  min_top %d"
                     % (i + 1, label, lo, hi, min_top))
        y += zoom.height + 6
    sheet_path = os.path.join(OUT, "sheet_blind.png")
    sheet.save(sheet_path)
    map_path = os.path.join(OUT, "sheet_blind_map.txt")
    with open(map_path, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")
    print("SHEET %s  %dx%d  (panels top to bottom, unmapped on the sheet)" % (sheet_path, width_of(sheet), sheet.height))
    print("MAP   %s" % map_path)
    for i, line in enumerate(lines):
        print("  " + line)
    return 0


def width_of(img):
    return img.width


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
