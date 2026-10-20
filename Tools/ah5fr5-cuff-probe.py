#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R5 probe - the two canvases at the near shoulder, the hinge and the discs
drawn in, 4x, plus the candidate patch's own colour statistics.

The patch's colours must continue the arm's skin, not its outline: this prints what the
candidate actually wrote (RGB stats of the added px) and shows both canvases so the
tone/boundary can be chosen from the art itself, not from the mask.

Usage: python Tools/ah5fr5-cuff-probe.py
"""
import importlib.util
import os

import numpy as np
from PIL import Image, ImageDraw

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(PROJECT, "Trials", "Runs", "cuff-r5")


def load_mod(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(PROJECT, "Tools", rel))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


BAKE = load_mod("bake", "art-h5f-cover-bake.py")
F = BAKE.F


def main():
    ns, joints = F.read_rig()
    spec = BAKE.resolve_spec("shoulder", "near")
    qp, qc = spec["qp"], spec["qc"]
    canv = {k: F.load_canvas(k) for k in ("torso", "upper")}
    crops = {}
    for key, anchor in (("torso", qp), ("upper", qc)):
        c = canv[key]
        W, H = c["px"]
        ppu = c["ppu"]
        pxv = c["pivot"][0] * W
        pyv = (1.0 - c["pivot"][1]) * H
        # the crop: the hinge +/- 80 mm in the joint frame -> canvas px
        half = 0.080
        x0 = int(pxv + (0.0 + anchor[0] - half) * ppu)
        x1 = int(pxv + (0.0 + anchor[0] + half) * ppu)
        y0 = int(pyv - (0.0 + anchor[1] + half) * ppu)
        y1 = int(pyv - (0.0 + anchor[1] - half) * ppu)
        im = c["im"].crop((x0, y0, x1, y1))
        d = ImageDraw.Draw(im)
        # the joint frame's discs, r = 46.9 / 50 / 52 mm, centred at the hinge
        for r, col in ((0.0469, (255, 60, 60, 255)), (0.050, (255, 210, 60, 255)),
                       (0.052, (60, 255, 120, 255))):
            cxp = (0.0 + anchor[0]) * ppu + pxv - x0
            cyp = pyv - (0.0 + anchor[1]) * ppu - y0
            rp = r * ppu
            d.ellipse((cxp - rp, cyp - rp, cxp + rp, cyp + rp), outline=col, width=1)
        crops[key] = im
    # side by side, 4x, on sky
    z = 4
    pad, strip = 10, 26
    w0 = max(im.width for im in crops.values())
    h0 = max(im.height for im in crops.values())
    out = Image.new("RGB", (w0 * z * 2 + pad, h0 * z + strip), (24, 24, 28))
    d = ImageDraw.Draw(out)
    for i, key in enumerate(("torso", "upper")):
        im = crops[key]
        bg = Image.new("RGB", im.size, (140, 199, 240))
        bg.paste(im, (0, 0), im)
        out.paste(bg.resize((im.width * z, im.height * z), Image.NEAREST),
                  (i * (w0 * z + pad), strip))
        d.text((i * (w0 * z + pad) + 4, 6), "%s.png at the near hinge (x0=%d y0=%d)"
               % (key, 0, 0), fill=(240, 240, 240))
    p1 = os.path.join(OUT, "canvases-view.png")
    out.save(p1)
    print("wrote", p1, out.size)
    # why the patch is black: the arm's own grid and the bleed
    CUFF = load_mod("cuff", "ah5fr5-shoulder-cuff.py")
    fr = BAKE.Frame(spec, canv, ns, 3000.0)
    arm = fr.place(spec["child"], spec["qc"], 0.0, 1.0, 0.0)
    pm = arm[..., 3] > 0.5
    print("arm on the crop grid: painted %d px ; rgb mean %s ; rgb max %s"
          % (pm.sum(), arm[..., :3][pm].mean(axis=0).round(1) if pm.any() else "-",
             arm[..., :3].max()))
    col = CUFF.bleed(fr, spec)
    print("bled grid: mean %s max %s ; the arm's raw im: mean %s"
          % (col.reshape(-1, 3).mean(axis=0).round(1), col.max(),
             np.asarray(canv["upper"]["im"]).astype(np.float32).mean()))
    # the candidate's own colours
    a = np.asarray(Image.open(os.path.join(OUT, "torso-shoulder-cuff.png")).convert("RGBA")).astype(np.int16)
    b = np.asarray(Image.open(os.path.join(PROJECT, "Assets", "Resources", "Art",
                                           "Human", "torso.png")).convert("RGBA")).astype(np.int16)
    ch = (np.abs(a - b).sum(axis=2) > 8)
    ys, xs = np.nonzero(ch)
    print("changed px %d ; added-alpha px %d" % (ch.sum(), int(((a[..., 3] > b[..., 3] + 8)).sum())))
    if ch.sum():
        rgb = a[ys, xs, :3]
        print("their RGB: mean %s min %s max %s" % (rgb.mean(axis=0).round(1),
                                                    rgb.min(axis=0), rgb.max(axis=0)))
        dark = (rgb.max(axis=1) < 70).sum()
        print("of them dark (max channel < 70): %d (%.1f%%)" % (dark, 100.0 * dark / len(ys)))
        # a few samples across the patch
        for k in range(0, len(ys), max(1, len(ys) // 8)):
            print("   px(%d,%d) rgb %s" % (xs[k], ys[k], rgb[k]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
