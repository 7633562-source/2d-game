#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R5 - THE SHOULDER'S PARENT CUFF (offline build of the torso's own patch).

The R5 bar: at v=0.4 the shoulder's disc holes must be <= the control's own.  The rule
is the R4 one: THE PARENT'S OWN CUFF, NO NEIGHBOUR STRETCH - at the shoulder the parent
is the TORSO (HumanArt.ForeshortenVisuals squashes only the thigh/upper arm, and the
torso's canvas is drawn 1:1), so the coverage has to be painted into torso.png itself.

Two candidate patches, both = the DEFECT SWEEP (for every arm angle of the joint's
range, the pixels the v=0.4 picture leaves unpainted inside the disc, unioned over the
range and clipped to r_cover - wider than both instruments' discs, the model's R_eye
0.0469 m and the engine's own 0.05 m):

  V1 = the sweep (it also fills the rest bay - the shoulder reads slightly fuller)
  V2 = V1 clipped to the rest coverage at EVERY angle (invisible at rest, by
       construction, but it can leave the crescent's spills unpainted)

Colours come from the arm's own canvas under the cap (bled outward where the arm has
no paint), so the patch continues the deltoid's edge instead of a new surface.

Outputs (Trials/Runs/cuff-r5/): torso-shoulder-cuff.png (the candidate torso.png, alpha
only ever ADDED where the torso is transparent), cuff-preview.png (control | v=0.4 no
cuff | v=0.4 + cuff, worst angle, 4x, disc ring), the numbers on stdout.

Usage: python Tools/ah5fr5-shoulder-cuff.py [--install] [--mode v1|v2] [--r 0.051]
"""
import argparse
import importlib.util
import os
import shutil

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
THR = 0.06


def grid_of(fr):
    ox = np.arange(fr.nx, dtype=np.float64)
    oy = np.arange(fr.ny, dtype=np.float64)
    X = fr.x0 + (ox + 0.5) / fr.scale
    Y = fr.y0 + fr.h - (oy + 0.5) / fr.scale
    return np.meshgrid(X, Y)


def cov04(fr, spec, th):
    p4 = BAKE.pose(fr, spec, th, 0.4)
    return (p4["par_now"][..., 3] > THR) | (p4["ch_now"][..., 3] > THR)


def cov0(fr, spec, th):
    p0 = BAKE.pose(fr, spec, th, 0.0)
    return (p0["par_now"][..., 3] > THR) | (p0["ch_now"][..., 3] > THR)


def build(fr, spec, xg, yg, r_cover, lo, hi, n, kmask=None):
    disc = (xg * xg + yg * yg) <= r_cover * r_cover
    angles = np.linspace(lo, hi, n)
    union = np.zeros(disc.shape, bool)
    cres = np.zeros(disc.shape, bool)
    bays = np.zeros(disc.shape, bool)
    rows = []
    for th in angles:
        cov4 = cov04(fr, spec, th)
        c0 = cov0(fr, spec, th)
        hole4 = disc & ~cov4
        bay = disc & ~c0
        union |= hole4
        if kmask is None:
            cres |= hole4 & c0
        else:
            # the mask's inner boundary: the arm swept at kmask (the engine's Sliced band
            # compresses its middle harder than k: 0.419 vs 0.600 at v=0.4) - the model's
            # own squash under-covers the engine's vacated rim by ~10 per cent
            c4m = (fr.place(spec["child"], (spec["qc"][0], spec["qc"][1] * kmask),
                            th, kmask, 0.0)[..., 3] > THR)
            cres |= c0 & ~c4m
        bays |= bay
        rows.append((th, int(hole4.sum()), int(bay.sum())))
    return disc, union, cres, bays, angles, rows


def bleed(fr, spec):
    """The arm's own picture on the crop grid (rest pose), its unpainted px filled by
    the nearest painted px - the deltoid's edge tone carried outward."""
    arm = fr.place(spec["child"], spec["qc"], 0.0, 1.0, 0.0)
    known = arm[..., 3] > 0.5
    rgb = arm[..., :3].copy()
    have = known.copy()
    for _ in range(400):
        if have.all():
            break
        acc = np.zeros_like(rgb)
        cnt = np.zeros(rgb.shape[:2], np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            src = np.roll(rgb, (dy, dx), axis=(0, 1))
            m = np.roll(have, (dy, dx), axis=(0, 1))
            acc += src * m[..., None]
            cnt += m
        new = (~have) & (cnt > 0)
        rgb[new] = acc[new] / cnt[new][..., None]
        have |= new
    return rgb


def paint_torso(torso, fr, spec, mask, colour):
    """Write `mask` into a copy of the torso canvas.  Only ADDS alpha where the torso's
    own alpha <= THR; RGB is the arm's own colour at that joint point (3x3 supersampled
    at the patch's edge so the boundary is smooth)."""
    W, H = torso["px"]
    ppu = torso["ppu"]
    pxv = torso["pivot"][0] * W
    pyv = (1.0 - torso["pivot"][1]) * H
    qp = spec["qp"]
    base = np.asarray(torso["im"]).astype(np.float32)
    acc = np.zeros((H, W), np.float32)
    rgb = np.zeros((H, W, 3), np.float32)
    xs = np.arange(W, dtype=np.float64)[None, :]
    ys = np.arange(H, dtype=np.float64)[:, None]
    sub = ((np.arange(3) + 0.5) / 3.0) - 0.5
    for dv in sub:
        for du in sub:
            X = (xs + du - pxv) / ppu - qp[0]
            Y = (pyv - (ys + dv)) / ppu - qp[1]
            gx = np.broadcast_to(((X - fr.x0) * fr.scale - 0.5).round().astype(int), (H, W)).copy()
            gy = np.broadcast_to(((fr.y0 + fr.h - Y) * fr.scale - 0.5).round().astype(int), (H, W)).copy()
            ok = (gx >= 0) & (gx < fr.nx) & (gy >= 0) & (gy < fr.ny)
            hit = np.zeros((H, W), bool)
            hh = mask[gy[ok], gx[ok]]
            hit[ok] = hh
            acc += hit
            cx = np.zeros((H, W), np.float32)
            cy = np.zeros((H, W), np.float32)
            for ch in range(3):
                cy[ok] = colour[gy[ok], gx[ok], ch]
                rgb[..., ch] += cy
    frac = acc / 9.0
    alpha = base[..., 3]
    free = alpha <= int(THR * 255)
    add = np.where(free, np.clip(frac, 0, 1) * 255.0, 0.0)
    out = base.copy()
    colw = (rgb / np.maximum(acc, 1.0)[..., None]) * 255.0
    w = np.where((add[..., None] > 0) & (frac[..., None] > 0), colw, base[..., :3])
    out[..., :3] = w
    out[..., 3] = np.clip(alpha + add, 0, 255)
    painted = int((add > 16).sum())
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA"), painted


def preview(canv_org, canv_patched, spec, ns, r_cover, theta, path):
    """rest | rest+cuff | v=0.4 no cuff | v=0.4+cuff, the disc ring drawn, 4x."""
    xg, yg = grid_of(BAKE.Frame(spec, canv_org, ns, 3000.0))
    ring = np.abs(np.sqrt(xg * xg + yg * yg) - r_cover) <= 0.0005
    rimg = np.zeros((*ring.shape, 4), np.float32)
    rimg[ring] = (1.0, 0.15, 0.15, 1.0)
    ims = []
    for label, v, cans in (("rest v=0", 0.0, canv_org),
                           ("rest v=0 +cuff", 0.0, canv_patched),
                           ("v=0.4 no cuff", 0.4, canv_org),
                           ("v=0.4 +cuff", 0.4, canv_patched)):
        fr = BAKE.Frame(spec, cans, ns, 3000.0)
        p = BAKE.pose(fr, spec, theta, v)
        comp = BAKE.over(p["par_now"], p["ch_now"])
        comp = BAKE.over(comp, rimg)
        a = (np.clip(comp, 0, 1) * 255).astype(np.uint8)
        im = Image.fromarray(a, "RGBA")
        bg = Image.new("RGB", im.size, (140, 199, 240))
        bg.paste(im, (0, 0), im)
        ims.append((label, bg))
    z = 4
    pad, strip = 8, 26
    w0, h0 = ims[0][1].size
    out = Image.new("RGB", (w0 * z * 4 + pad * 3, h0 * z + strip), (24, 24, 28))
    d = ImageDraw.Draw(out)
    for i, (label, im) in enumerate(ims):
        out.paste(im.resize((w0 * z, h0 * z), Image.NEAREST), (i * (w0 * z + pad), strip))
        d.text((i * (w0 * z + pad) + 4, 7), "%s  theta %.0f" % (label, theta),
               fill=(255, 255, 255) if i else (255, 220, 120))
    out.save(path)
    return out.size


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--r", type=float, default=0.080)
    ap.add_argument("--lo", type=float, default=-28.0)
    ap.add_argument("--hi", type=float, default=28.0)
    ap.add_argument("--n", type=int, default=29)
    ap.add_argument("--mode", default="v1", choices=("v1", "cres"))
    ap.add_argument("--install", action="store_true")
    ap.add_argument("--kmask", type=float, default=0.45)
    ap.add_argument("--theta", type=float, default=-14.0)
    args = ap.parse_args()

    os.makedirs(OUT, exist_ok=True)
    ns, joints = F.read_rig()
    keys = sorted(set(F.CANVAS.values()))
    canvases = {}
    for k in keys:
        c = F.load_canvas(k)
        if c is not None:
            canvases[k] = c
    # the mask is cut against the R4 torso (the backup), never against an already patched
    # asset: otherwise the second run sees its own paint as rest coverage and cuts nothing
    bak = os.path.join(OUT, "torso-r4.png")
    if os.path.exists(bak):
        from PIL import Image as _I
        canvases["torso"] = dict(canvases["torso"],
                                 im=_I.open(bak).convert("RGBA"))
        print("  base: the R4 torso from %s" % bak)
    spec = BAKE.resolve_spec("shoulder", "near")
    fr = BAKE.Frame(spec, canvases, ns, 3000.0)
    xg, yg = grid_of(fr)
    disc, union, cres, bays, angles, rows = build(fr, spec, xg, yg, args.r, args.lo, args.hi,
                                                 args.n, args.kmask)
    print("sweep %.1f..%.1f n=%d  r_cover %.4f m  disc %d px (model @3000)"
          % (args.lo, args.hi, args.n, args.r, disc.sum()))
    print("  theta   hole04   hole0(rest bay)   [r<=%.0f mm]" % (args.r * 1000))
    for th, h4, h0 in rows:
        print("   %+6.1f   %6d  %6d" % (th, h4, h0))
    print("  union(all defects) %d px ; crescent-only %d px ; the bays %d px ; union-cres %d px"
          % (union.sum(), cres.sum(), bays.sum(), (union & ~cres).sum()))

    walk = (-14, -8.75, -3.5, 1.75, 7.0, 12.25, 17.5, 22.75, 28.0)
    print("  the walk's angles   hole04  +v1  +cres  control   v1@rest  cres@rest")
    worst = [0, 0, 0, 0, 0]
    for th in walk:
        h4 = int((disc & ~cov04(fr, spec, th)).sum())
        h4v1 = int((disc & ~cov04(fr, spec, th) & ~union).sum())
        h4cr = int((disc & ~cov04(fr, spec, th) & ~cres).sum())
        c0 = cov0(fr, spec, th)
        h0 = int((disc & ~c0).sum())
        vis1 = int((union & disc & ~c0).sum())
        visc = int((cres & disc & ~c0).sum())
        print("   %+6.2f   %6d  %6d  %6d   %6d   %6d  %6d"
              % (th, h4, h4v1, h4cr, h0, vis1, visc))
        worst = [max(worst[0], h4v1), max(worst[1], h4cr), max(worst[2], vis1),
                 max(worst[3], visc), max(worst[4], h0)]
    print("  WORST over the walk: v1 -> v0.4 %d, cres -> v0.4 %d ; control %d" % (worst[0], worst[1], worst[4]))
    print("  the cuff shows at rest: v1 %d, cres %d px [r<=%.0f mm]" % (worst[2], worst[3], args.r * 1000))
    print("  (the round's instrument r=46.9: control worst 1165 px @ -14; the defect worst 2644 @ -14)")

    mask = union if args.mode == "v1" else cres
    # the mask's edges: the union of 29 sampled angles is scalloped; a blur + threshold
    # rounds the boundary to the sweep's own envelope before it is cut into the canvas
    f = mask.astype(np.float32)
    for _ in range(2):
        f = (f + np.roll(f, 1, 0) + np.roll(f, -1, 0) + np.roll(f, 1, 1) + np.roll(f, -1, 1)) / 5.0
    mask = f >= 0.55
    # the boundary loses a fringe when the mask is cut into the torso's own ppu (1504 vs
    # the model's 3000): grow it by ~1 mm so the engine's raster loses nothing
    for _ in range(2):
        mask = (mask | np.roll(mask, 1, 0) | np.roll(mask, -1, 0)
                | np.roll(mask, 1, 1) | np.roll(mask, -1, 1))
    print("  the mask after the boundary smoothing + 2 px growth: %d px" % mask.sum())
    # the two instruments' own discs
    for rr in (0.0469, 0.050):
        d2 = (xg * xg + yg * yg) <= rr * rr
        w_ctl = w_before = w_a = w_c = 0
        for th in walk:
            c4 = cov04(fr, spec, th)
            c0 = cov0(fr, spec, th)
            w_ctl = max(w_ctl, int((d2 & ~c0).sum()))
            w_before = max(w_before, int((d2 & ~c4).sum()))
            w_a = max(w_a, int((d2 & ~c4 & ~union).sum()))
            w_c = max(w_c, int((d2 & ~c4 & ~mask).sum()))
        print("  at r=%.4f m: control %d | v0.4 before %d | +v1(paint) %d | +cres-the-band %d"
              % (rr, w_ctl, w_before, w_a, w_c))
    colour = bleed(fr, spec)
    torso = canvases["torso"]
    out2, painted = paint_torso(torso, fr, spec, mask, colour)
    print("  the patch in the torso's own canvas (%dx%d): %d px"
          % (torso["px"][0], torso["px"][1], painted))
    cand = os.path.join(OUT, "torso-shoulder-cuff.png")
    out2.save(cand)
    print("  wrote", cand)
    canv2 = dict(canvases)
    canv2["torso"] = dict(canvases["torso"], im=out2)
    size = preview(canvases, canv2, spec, ns, args.r, args.theta,
                   os.path.join(OUT, "cuff-preview-%s.png" % args.mode))
    print("  wrote", os.path.join(OUT, "cuff-preview-%s.png" % args.mode), size)
    if args.install:
        dst = os.path.join(PROJECT, "Assets", "Resources", "Art", "Human", "torso.png")
        bak = os.path.join(OUT, "torso-r4.png")
        if not os.path.exists(bak):
            shutil.copy2(dst, bak)
            print("  backed up the R4 torso to", bak)
        shutil.copy2(cand, dst)
        print("  INSTALLED", dst)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
