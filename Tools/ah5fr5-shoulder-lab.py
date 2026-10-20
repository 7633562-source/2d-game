#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R5 - THE SHOULDER'S OWN SWEEP, offline, from the shipped canvases.

The R5 bar: at v=0.4 the shoulder's disc holes must be <= the control's own number.
This tool prints that table (v=0 beside v=0.4, no cover beside the bake's cover) and
writes the marked sheet the numbers came from - the hole pixels in red, the disc ring
drawn - so a reader can see WHERE the hole is, not only how big it is.

The instrument is the R3 sweep's own (Tools/ah5fr3-cover-sweep.py): the joint disc of
radius R_eye (the narrower neighbour's ink half-width where the two meet) counted for
open pixels in the composition the game draws (the bake's own model of
HumanArt.ForeshortenVisuals).

Usage:
  python Tools/ah5fr5-shoulder-lab.py
  python Tools/ah5fr5-shoulder-lab.py --joint shoulder --steps 9 --sheet r5-shoulder-sheet.png
"""
import argparse
import importlib.util
import os

import numpy as np
from PIL import Image, ImageDraw

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(PROJECT, "Trials", "Runs", "cuff-r4")
BG = (186, 205, 222)          # the game's sky tone (the bake sheet's own)


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(PROJECT, "Tools", rel))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


BAKE = load("bake", "art-h5f-cover-bake.py")
F = BAKE.F


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--joint", default="shoulder")
    ap.add_argument("--steps", type=int, default=9)
    ap.add_argument("--sheet", default="r5-shoulder-sheet.png")
    ap.add_argument("--scale", type=float, default=3000.0)
    args = ap.parse_args()

    ns, joints = F.read_rig()
    canvases = {k: F.load_canvas(v) for k, v in F.CANVAS.items()}
    canvases = {k: v for k, v in canvases.items() if v is not None}

    lines = []
    strips = []
    for side in ("near", "far"):
        spec = BAKE.resolve_spec(args.joint, side)
        fr, rgba, shape = BAKE.bake(spec, canvases, ns, args.scale, verbose=False)
        cover = np.zeros_like(rgba)
        cover[..., :3] = rgba[..., :3]
        cover[..., 3] = rgba[..., 3]
        lo, hi = BAKE.RANGES[args.joint]
        thetas = [lo + (hi - lo) * i / (args.steps - 1) for i in range(args.steps)]
        r_eye = min(F.ink_half(canvases[spec["parent"]], spec["qp"]),
                    F.ink_half(canvases[spec["child"]], spec["qc"]))
        yy, xx = np.mgrid[0:fr.ny, 0:fr.nx]
        Xm = fr.x0 + (xx + 0.5) / fr.scale
        Ym = fr.y0 + fr.h - (yy + 0.5) / fr.scale
        disc = (Xm * Xm + Ym * Ym) <= r_eye * r_eye
        worst = {}
        for v in (0.0, 0.4):
            w = None
            rows = []
            for th in thetas:
                p = BAKE.pose(fr, spec, th, v)
                ctrl = BAKE.over(p["par_now"], p["ch_now"])
                game = BAKE.over(BAKE.over(cover, p["par_now"]), p["ch_now"])
                nc = int((disc & (ctrl[..., 3] <= 0.06)).sum())
                wc = int((disc & (game[..., 3] <= 0.06)).sum())
                rows.append((th, nc, wc))
                if w is None or nc > w[1]:
                    w = (th, nc, wc, ctrl, game)
            worst[v] = w
            lines.append("%s_%s v=%.2f: disc R_eye %.4f m, %d angles %.0f..%.0f deg | "
                         "no-cover worst %d px @ %+.0f deg (cover %d there)"
                         % (args.joint, side, v, r_eye, len(thetas), lo, hi, w[1], w[0], w[2]))
            for t, a, b in rows:
                lines.append("    theta %+6.1f: no-cover %5d  with-cover %d" % (t, a, b))
            for label, arr in (("v=%.1f no cover" % v, w[3]), ("v=%.1f cover" % v, w[4])):
                res = disc & (arr[..., 3] <= 0.06)
                if res.any():
                    rr, cc = np.nonzero(res)
                    lines.append("    %s: hole %d px, centroid %+.1f,%+.1f mm  bbox x %+.1f..%+.1f  y %+.1f..%+.1f mm"
                                 % (label, int(res.sum()),
                                    fr.xm(cc.mean()) * 1000, fr.ym(rr.mean()) * 1000,
                                    fr.xm(cc.min()) * 1000, fr.xm(cc.max()) * 1000,
                                    fr.ym(rr.max()) * 1000, fr.ym(rr.min()) * 1000))
                else:
                    lines.append("    %s: hole 0 px" % label)

        ims = []
        for v, lab in ((0.0, "v=0.0 no cover"), (0.4, "v=0.4 no cover"), (0.4, "v=0.4 cover")):
            arr = worst[v][3] if lab.endswith("no cover") else worst[v][4]
            base = np.zeros_like(arr[..., :3])
            base[..., :] = np.array(BG, np.float32) / 255.0
            comp = BAKE.over(np.concatenate([base, np.ones_like(arr[..., 3:4])], axis=2), arr)
            rgb = np.clip(comp[..., :3] * 255.0, 0, 255).astype(np.uint8)
            rgb[disc & (arr[..., 3] <= 0.06)] = (255, 40, 40)
            im = Image.fromarray(rgb, "RGB")
            dr = ImageDraw.Draw(im)
            crop = BAKE.JOINTS[args.joint]["crop"]
            cx = (0.0 - crop[0]) * args.scale
            cy = (crop[1] + crop[3]) * args.scale
            rr = r_eye * args.scale
            dr.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], outline=(20, 20, 60))
            dr.text((4, 3), "%s %s  t=%+.0f" % (side, lab, worst[v][0]), fill=(20, 20, 40))
            ims.append(im)
        w = sum(im.width for im in ims) + 10 * (len(ims) - 1)
        strip = Image.new("RGB", (w, ims[0].height), BG)
        x = 0
        for im in ims:
            strip.paste(im, (x, 0))
            x += im.width + 10
        strips.append(strip)

    H = sum(s.height for s in strips) + 10 * (len(strips) - 1)
    W = max(s.width for s in strips)
    sheet = Image.new("RGB", (W, H), BG)
    y = 0
    for s in strips:
        sheet.paste(s, (0, y))
        y += s.height + 10
    os.makedirs(OUT, exist_ok=True)
    sheet.save(os.path.join(OUT, args.sheet))
    txt = "\n".join(lines)
    print(txt)
    with open(os.path.join(OUT, args.sheet.replace(".png", ".txt")), "w", encoding="ascii") as fh:
        fh.write("# ART-H5f R5 shoulder sweep - Tools/ah5fr5-shoulder-lab.py\n" + txt + "\n")
    print("saved", os.path.join(OUT, args.sheet))


if __name__ == "__main__":
    main()
