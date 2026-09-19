#!/usr/bin/env python3
"""ART-0 sign cells: is the chequer stretched, and which grid reads at the size the game draws?

The finish sign is 64x320 @ PPU 100, but the level places it as a 0.18 x 0.90 m piece
(`Assets/LevelTest1.cs:3059`), and the game camera runs ortho 4.0-5.0 (`Assets/GameProcess.cs:939-950`),
so the piece really occupies ~21 x 106 screen px - the gallery's own sign row draws the sprite at its
NATIVE 0.64 x 3.2 m, i.e. 3.55x bigger than the level ever shows it.

This probe renders candidate chequer grids through the shipping painter
(`Tools/gen-level-textures.py:paint_sign_finish`), draws each one the way StaticPlatform does (Sliced
onto the 0.18 x 0.90 m box at the game's px/m, bilinear like the shipped filterMode), magnifies the
result with NEAREST so the pixels are the only thing judged, and writes blind A/B sheets for the eye.

Run from the project root:  python Tools/art0-sign-cells.py
Writes Trials/Runs/art-0-sign-cells/ and prints the measurement table.
"""

import importlib.util
import os
import random

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Trials", "Runs", "art-0-sign-cells")
os.makedirs(OUT, exist_ok=True)

# the shipping painter, imported by path (Tools/ is outside Assets/, so it is never compiled)
spec = importlib.util.spec_from_file_location(
    "gen_level_textures", os.path.join(ROOT, "Tools", "gen-level-textures.py"))
G = importlib.util.module_from_spec(spec)
spec.loader.exec_module(G)

# The level's own numbers.
BOX = (0.18, 0.90)               # Assets/LevelTest1.cs:3059  size = new Vector2(0.18f, 0.90f)
ORTHO = 4.6                      # Assets/GameProcess.cs:939
FRAME_H = 1080
PX_PER_M = FRAME_H / (2.0 * ORTHO)

VARIANTS = [
    # name, cell_w, cell_h, rows   (rows=None -> as many whole rows as the banner holds)
    ("A-shipped-15x38", 15, 38, 5),
    ("B-square-15x15", 15, 15, 12),
    ("C-square-21x21", 21, 21, 8),
    ("D-square-11x11", 11, 11, 16),
]
MAG = 4
BANNER_TOP, BANNER_BOTTOM = 10, 196      # from paint_sign_finish
POST_R = 17


def gameplay(img):
    """Draw the canvas the way StaticPlatform does: Sliced onto the 0.18 x 0.90 m box."""
    w = max(1, int(round(BOX[0] * PX_PER_M)))
    h = max(1, int(round(BOX[1] * PX_PER_M)))
    return img.convert("RGBA").resize((w, h), Image.LANCZOS)


def cell_metrics(img, cell_w, cell_h, rows):
    """Chequer geometry on the canvas and on the drawn piece."""
    bw = G.__dict__  # keep the painter's own constants out of reach; banner edges are in the painter
    ban_left, ban_right = POST_R + 1, 61
    ban_w = ban_right - ban_left + 1
    ban_h = BANNER_BOTTOM - BANNER_TOP + 1
    sprite_px_per_canvas_px = BOX[0] * PX_PER_M / img.size[0]
    return {
        "cell canvas": (cell_w, cell_h),
        "cell aspect h/w": cell_h / float(cell_w),
        "cell screen px": (cell_w * sprite_px_per_canvas_px, cell_h * sprite_px_per_canvas_px),
        "grid": "%dx%d" % (ban_w // cell_w, rows),
        "banner canvas": (ban_w, ban_h),
        "banner screen px": (ban_w * sprite_px_per_canvas_px, ban_h * sprite_px_per_canvas_px),
        "piece screen px": (img.size[0] * sprite_px_per_canvas_px, img.size[1] * sprite_px_per_canvas_px),
    }


def main():
    canvases = {}
    for name, cw, ch, rows in VARIANTS:
        canvases[name] = G.paint_sign_finish(cell_w=cw, cell_h=ch, rows=rows)
    shipped = "A-shipped-15x38"

    # alpha must not move: the chequer is clipped by the banner silhouette, so a repaint is RGB only
    ref_alpha = np.asarray(canvases[shipped].convert("RGBA"))[:, :, 3]
    print("variant          cell      grid   cell screen px   banner screen px   piece screen px   alpha==A")
    for name, cw, ch, rows in VARIANTS:
        img = canvases[name]
        m = cell_metrics(img, cw, ch, rows)
        a = np.asarray(img.convert("RGBA"))[:, :, 3]
        print("%-16s %-9s %-6s %6.1fx%-5.1f  %6.1fx%-6.1f  %5.1fx%-5.1f  %s" % (
            name, "%dx%d" % (cw, ch), m["grid"],
            m["cell screen px"][0], m["cell screen px"][1],
            m["banner screen px"][0], m["banner screen px"][1],
            m["piece screen px"][0], m["piece screen px"][1],
            bool((a == ref_alpha).all())))

    # real drawn appearance, 4x NEAREST so the eye sees the game's own pixels
    drawn = {}
    for name in canvases:
        g = gameplay(canvases[name])
        drawn[name] = g.resize((g.size[0] * MAG, g.size[1] * MAG), Image.NEAREST)
    for name, im in drawn.items():
        im.save(os.path.join(OUT, "%s_4x.png" % name))

    # blind A/B sheets: the order is shuffled, the panels are named only LEFT / RIGHT, the
    # mapping lands in map.txt. Sheet 4 pits the two square grids against each other.
    rng = random.Random(19)
    mapping = []
    pairs = [(shipped, "B-square-15x15", "sign_cells_1.png"),
             (shipped, "C-square-21x21", "sign_cells_2.png"),
             (shipped, "D-square-11x11", "sign_cells_3.png"),
             ("B-square-15x15", "C-square-21x21", "sign_cells_4.png")]
    for a_name, b_name, fname in pairs:
        pair = [a_name, b_name]
        rng.shuffle(pair)
        panels = [drawn[p] for p in pair]
        pad, sep = 10, 6
        w = sum(p.size[0] for p in panels) + sep + 2 * pad
        h = max(p.size[1] for p in panels) + 2 * pad
        sheet = Image.new("RGBA", (w, h), (128, 128, 128, 255))
        d = ImageDraw.Draw(sheet)
        x = pad
        for p in panels:
            sheet.paste(p, (x, pad), p)
            x += p.size[0] + sep
        d.rectangle([pad + panels[0].size[0] + 1, 0, pad + panels[0].size[0] + sep - 2, h],
                    fill=(255, 0, 255, 255))
        sheet.convert("RGB").save(os.path.join(OUT, fname))
        mapping.append("%s : LEFT=%s  RIGHT=%s  (%s vs %s at %dx)" % (
            fname, pair[0], pair[1], "the drawn piece", "the drawn piece", MAG))

    with open(os.path.join(OUT, "map.txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(mapping) + "\n")
        fh.write("\nEach panel: the 64x320 canvas Sliced onto the level's 0.18 x 0.90 m piece at "
                 "%.2f px/m (ortho %.1f, 1080p), then magnified %dx with NEAREST.\n"
                 % (PX_PER_M, ORTHO, MAG))
    print("\nwrote %d sheets + map.txt to Trials/Runs/art-0-sign-cells/" % len(pairs))


if __name__ == "__main__":
    main()
