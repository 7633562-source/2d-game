"""ART-0 cycle: the numbers for the grass-cap tip-band candidates.

Two things can be measured about a cap canvas without a build:

1. `cap_tick_report` / `seam_error` from the generator itself - the guards that keep a tiled strip
   from coming back as a ruler mark (a column standing out of its neighbours, a height spike) and
   the wrap seam. Lifting the blade-tip band changes the silhouette for every column, so these are
   re-run, not assumed.
2. The junction the level draws: soil from the surface down, the cap box top ON the surface. For a
   candidate canvas this prints how many screen rows above the first mostly-green row the window
   shows, at the level lens (64.67 px/m) - i.e. the bare-soil lip the eye judged.

Usage:  python Tools/art0-cap-tips-verify.py Trials/Runs/art-0-cap-tips/cap_*.png
"""
import importlib.util
import os
import sys

import numpy as np
from PIL import Image

PROJ = r"E:/Games/2D Game/My project"
GEN = os.path.join(PROJ, "Tools", "gen-level-textures.py")
PX_PER_M = 1080.0 / 16.70
FILL = os.path.join(PROJ, "Assets", "Resources", "Level", "ground_fill.png")


def generator():
    spec = importlib.util.spec_from_file_location("genlevel", GEN)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def junction_lip(cap, fill):
    """Rows of the drawn junction: the cap Tiled at 1.28 m with its box top on the soil surface."""
    scale = PX_PER_M / 100.0
    tile = cap.convert("RGBA").resize(
        (max(1, int(round(cap.width * scale))), max(1, int(round(cap.height * scale)))),
        Image.BILINEAR)
    soil = fill.convert("RGBA").resize(
        (max(1, int(round(fill.width * PX_PER_M / 128.0))),
         max(1, int(round(fill.height * PX_PER_M / 128.0)))), Image.BILINEAR)
    window = soil.copy()
    for i in range(4):
        window.paste(tile, (i * tile.width, 0), tile)
    a = np.asarray(window).astype(np.float64)
    rgb, alpha = a[:, :, :3], a[:, :, 3] / 255.0
    rows = []
    for y in range(tile.height):
        px = rgb[y, :tile.width * 4]
        al = alpha[y, :tile.width * 4]
        # composite over the soil, as the renderer does
        soil_row = np.asarray(soil)[y, :tile.width * 4, :3].astype(np.float64)
        comp = px * al[:, None] + soil_row * (1.0 - al[:, None])
        r, g, b = comp[:, 0].mean(), comp[:, 1].mean(), comp[:, 2].mean()
        green = bool(g > r + 12 and g > b + 12)
        rows.append((y, r, g, b, green, float((alpha[y, :tile.width * 4] > 0).mean())))
    first_green = next((y for y in range(len(rows)) if rows[y][4]), None)
    return rows, first_green, tile


def main(paths):
    mod = generator()
    fill = Image.open(FILL)
    print("%-26s %-14s %-30s %s" % ("canvas", "tip rows", "lip (soil rows above green)", "tick guards"))
    for path in paths:
        cap = Image.open(path).convert("RGBA")
        a = np.asarray(cap)[:, :, 3] > 0
        tops = [int(np.argmax(a[:, x])) if a[:, x].any() else -1 for x in range(cap.width)]
        rows, first_green, tile = junction_lip(cap, fill)
        worst, wx, step, spike, bright, bx, tuft = mod.cap_tick_report(cap)
        seam, inner = mod.seam_error(cap)
        print("%-26s %-14s %-30s %s" % (
            os.path.basename(path),
            "%d..%d std %.2f" % (min(tops), max(tops), float(np.std(tops))),
            "green from row %s of %d (%s)" % (first_green, tile.height,
                                              "none" if first_green is None else
                                              "%.0f%% of the band is bare soil above it"
                                              % (100.0 * first_green / float(tile.height))),
            "dark %.1f x=%d bright %.1f x=%d spike %.0f px step %.2f tufts %.0f%% seam %.2f/%.2f"
            % (worst, wx, bright, bx, spike, step, tuft * 100, seam, inner)))
        print("%-26s   per drawn row: %s" % (
            "", " ".join("%d:%s%.0f" % (y, "G" if r[4] else "-", 100 * r[5]) for y, r in enumerate(rows))))
    return 0


if __name__ == "__main__":
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    sys.exit(main(sys.argv[1:]))
