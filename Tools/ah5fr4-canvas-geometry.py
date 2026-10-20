#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""R4 probe: the thigh/shin/upper/lower canvases' own geometry - px, ppu, pivot,
the ink extent in pivot-local metres - so the cuff's numbers are read, not guessed."""
import os
import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HUMAN = os.path.join(PROJECT, "Assets", "Resources", "Art", "Human")


def meta(path):
    with open(path, encoding="utf-8") as fh:
        text = fh.read()
    out = {}
    for line in text.splitlines():
        line = line.strip()
        if line.startswith("spritePixelsToUnits:"):
            out["ppu"] = float(line.split(":")[1].strip())
        if line.startswith("spritePivot:"):
            v = line.split("{")[1].split("}")[0]
            out["pivot"] = tuple(float(x.split(":")[1]) for x in v.split(","))
        if line.startswith("spriteBorder:"):
            v = line.split("{")[1].split("}")[0]
            out["border"] = tuple(float(x.split(":")[1]) for x in v.split(","))
        if line.startswith("maxTextureSize:"):
            out["max"] = int(line.split(":")[1].strip())
    return out


for key in ("thigh", "shin", "upper", "lower"):
    png = os.path.join(HUMAN, key + ".png")
    m = meta(png + ".meta")
    im = Image.open(png).convert("RGBA")
    a = np.array(im)[:, :, 3] > 16
    rows = np.where(a.any(axis=1))[0]
    cols = np.where(a.any(axis=0))[0]
    w, h = im.size
    ppu = m["ppu"]
    py = m["pivot"][1]
    top_m = ((1.0 - py) * h - rows.min()) / ppu
    bot_m = ((1.0 - py) * h - (rows.max() + 1)) / ppu
    print("%-6s px %dx%d ppu %.1f pivot %.4f,%.4f border %s max %d" %
          (key, w, h, ppu, m["pivot"][0], m["pivot"][1], m.get("border"), m.get("max", -1)))
    print("       box m %.4f x %.4f  ink rows %d..%d  ink top %+.4f bot %+.4f m" %
          (w / ppu, h / ppu, rows.min(), rows.max(), top_m, bot_m))
    print("       ink cols %d..%d  half-width near join %.4f m (pivot-local)" %
          (cols.min(), cols.max(), min(py, 1 - py) * 0))
