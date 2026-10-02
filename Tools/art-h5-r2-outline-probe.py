#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5 R2 diagnostic: where the dark contour lives in the bent-joint render.

The R1 read named "виден внутренний контур шарнира" on the bent patch strip. This probe
rebuilds the same (c) frame the R1 strips carry (the probe module does all the drawing),
then reports every dark pixel (panel RGB mean < 100) inside the eye window:

  internal   - dark px WITH parent ink beneath (a line cutting the joint surface),
  silhouette - dark px with no parent ink beneath (the limb's own outer contour),
  childcut   - dark px on the child's cut-edge set over parent ink (the census's edge & P),

and saves overlay images for the lane's own eye read:
  <tag>-bent-dark.png      the bent crop with internal dark px RED, silhouette dark px BLUE
  <tag>-child-deg.png      the child layer alone at deg (where its outline stroke sits)
  <tag>-parent-rest.png    the parent layer alone at rest

Usage: python Tools/art-h5-r2-outline-probe.py [--deg 65] [--tag r2a] [--hi 60] [--lo -96]
"""
import argparse
import importlib.util
import os

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(PROJECT, "Trials", "Runs", "arth5-r2")


def load_proto():
    path = os.path.join(PROJECT, "Tools", "art-h5-skin-proto.py")
    spec = importlib.util.spec_from_file_location("art_h5_skin_proto", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def build_c(p, j, deg, stitch="ramp", stitch_hi=60.0, stitch_lo=-96.0, blend="zone"):
    """Mirror of run_joint's (c) frame: patch bent over the parent-only base."""
    geom = j.measure()
    z0, z1 = geom["z0"], geom["z1"]
    sign = -1.0 if geom["rot"]["ccw"] >= geom["rot"]["cw"] else 1.0
    patch, changed = j.bake(stitch, z0, z1, hi=stitch_hi, lo=stitch_lo, verbose=False)
    du, dv = j.du_dv()
    m_in = p.MARGIN * j.scale
    z_dom_lo, z_dom_hi = z1 - m_in, z0 + m_in
    if stitch == "ramp":
        z_dom_lo = min(z_dom_lo, stitch_lo - 12.0)
        z_dom_hi = max(z_dom_hi, stitch_hi + 12.0)
    w0, w1 = (75.0, -75.0) if blend == "tight" else (z0, z1)
    warped, band_rows = j.warp_band(patch, deg, z_dom_lo, z_dom_hi, w0, w1, sign)
    base = np.asarray(j.composed(0.0, "parent")).astype(np.float32)
    wa = warped[:, :, 3:4] / 255.0
    ra = base[:, :, 3:4] / 255.0
    a_out = wa + ra * (1.0 - wa)
    out_c = np.zeros_like(np.asarray(j.composed(deg)).astype(np.float32))
    out_c[:, :, 3:4] = a_out * 255.0
    out_c[:, :, :3] = np.where(a_out > 1e-6,
                               (warped[:, :, :3] * wa + base[:, :, :3] * ra * (1.0 - wa))
                               / np.maximum(a_out, 1e-6), 0.0)
    return out_c, patch, du, dv, z0, z1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--deg", type=float, default=65.0)
    ap.add_argument("--tag", default="r2a")
    ap.add_argument("--scale", type=float, default=3000.0)
    ap.add_argument("--half", type=float, default=0.30)
    ap.add_argument("--hi", type=float, default=60.0)
    ap.add_argument("--lo", type=float, default=-96.0)
    ap.add_argument("--joint", default="knee")
    ap.add_argument("--profile", action="store_true",
                    help="print the luminance profile along du at several dv offsets")
    ap.add_argument("--dv", default="-40,-20,0,20,40")
    args = ap.parse_args()

    p = load_proto()
    j = p.Joint(p.frames_tool(), args.joint, args.deg, args.scale, args.half)

    if args.profile:
        arr0 = np.asarray(j.composed(0.0)).astype(np.float32)
        out_c, patch, duP, dvP, z0, z1 = build_c(p, j, args.deg, "ramp", args.hi, args.lo)
        du, dv = j.du_dv()
        c = j.c
        for dvv in [float(v) for v in args.dv.split(",")]:
            sel = np.abs(dv - dvv) < 1.5
            print("== dv %+0.0f px profile (du from +128 down to -328, step 8; lum | alpha | which)"
                  % dvv)
            for uu in range(128, -336, -8):
                band = sel & (du >= uu - 4) & (du < uu + 4)
                if not band.any():
                    continue
                lum = arr0[:, :, :3][band].mean(axis=1)
                al = arr0[:, :, 3][band]
                keep = al > 16
                if not keep.any():
                    continue
                P = np.asarray(j.composed(0.0, "parent"))[:, :, 3][band] > 16
                C = np.asarray(j.composed(0.0, "child"))[:, :, 3][band] > 16
                lump = float(lum[keep & P].mean()) if (keep & P).any() else -1.0
                lumc = float(lum[keep & C].mean()) if (keep & C).any() else -1.0
                alx = float(al[keep].mean())
                lum_bent = float(out_c[:, :, :3][band][keep].mean())
                print("  du %+5d  lum %6.1f (parent %6.1f / child %6.1f)  alpha %5.1f  "
                      "bent %6.1f  n=%d"
                      % (uu, float(lum[keep].mean()), lump, lumc, alx, lum_bent, int(keep.sum())))
        return 0

    out_c, patch, du, dv, z0, z1 = build_c(p, j, args.deg, "ramp", args.hi, args.lo)

    # 1:1 rest composition crop around the joint, 2x - the lane's own look at the art
    arr0 = np.asarray(j.composed(0.0)).astype(np.float32)
    sky0 = np.array(p.BG, dtype=np.float32)
    r0 = arr0[:, :, :3] * (arr0[:, :, 3:4] / 255.0) + sky0[None, None, :] * (1 - arr0[:, :, 3:4] / 255.0)
    cc = j.c
    xa, xb, ya, yb = int(cc - 150), int(cc + 150), int(cc - 300), int(cc + 420)
    rest_crop = Image.fromarray(r0[ya:yb, xa:xb].astype(np.uint8))
    os.makedirs(OUT, exist_ok=True)
    rest_crop.resize((rest_crop.width * 2, rest_crop.height * 2), Image.NEAREST).save(
        os.path.join(OUT, "%s-rest-crop.png" % args.tag))
    print("rest crop -> %s (joint centre at x=%d, y=%d of the crop/2)"
          % (os.path.join(OUT, "%s-rest-crop.png" % args.tag), 150, 300))

    u8 = np.clip(out_c, 0, 255).astype(np.uint8)
    rgb = u8[:, :, :3].astype(np.float32)
    alpha = u8[:, :, 3].astype(np.float32)
    sky = np.array(p.BG, dtype=np.float32)
    over = rgb * (alpha[:, :, None] / 255.0) + sky[None, None, :] * (1.0 - alpha[:, :, None] / 255.0)
    lum = over.mean(axis=2)

    P = np.asarray(j.composed(0.0, "parent"))[:, :, 3] > 16
    Cg = np.asarray(j.composed(args.deg, "child"))[:, :, 3] > 16
    E = Cg.copy()
    for sh in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        E &= np.roll(Cg, sh, axis=(0, 1))
    edge = Cg & ~E

    c = j.c
    win = 0.10 * j.scale
    yy, xx = np.mgrid[0:u8.shape[0], 0:u8.shape[1]]
    inwin = (np.abs(xx + 0.5 - c) <= win) & (np.abs(yy + 0.5 - c) <= win)
    dark = (lum < 100.0) & inwin
    dark_internal = dark & P
    dark_sil = dark & ~P
    dark_childcut = dark & edge & P

    print("== %s @%+.0f deg  dark px in win: total %d | internal(over parent) %d | "
          "silhouette %d | on child-cut-over-parent %d (of the cut set %d)"
          % (args.joint, args.deg, int(dark.sum()), int(dark_internal.sum()),
             int(dark_sil.sum()), int(dark_childcut.sum()), int((edge & P & inwin).sum())))

    for lo_b in range(-288, 96, 32):
        hi_b = lo_b + 32
        m_band = (du >= lo_b) & (du < hi_b)
        din = int((dark_internal & m_band).sum())
        dsil = int((dark_sil & m_band).sum())
        dcc = int((dark_childcut & m_band).sum())
        if din or dsil or dcc:
            print("   du [%+5d..%+5d): internal %5d  silhouette %5d  childcut %5d"
                  % (lo_b, hi_b, din, dsil, dcc))

    # luminance stats of the child-cut set (what tone the contour carries)
    cut = edge & P & inwin
    if cut.any():
        vals = lum[cut]
        print("   child-cut-over-parent px: n=%d  lum mean %.1f min %.1f max %.1f  | dark share %.2f"
              % (int(cut.sum()), float(vals.mean()), float(vals.min()), float(vals.max()),
                 float((vals < 100).mean())))
    # the parent's own cut set (its ink boundary), split by what lies beyond it
    Pe = P.copy()
    for sh in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        Pe &= np.roll(P, sh, axis=(0, 1))
    pcut = P & ~Pe & inwin
    if pcut.any():
        vals = lum[pcut]
        duv = du[pcut]
        over_c = pcut & Cg
        print("   parent-edge px: n=%d  du [%+.0f..%+.0f] overlap-with-child %d  | "
              "lum mean %.1f min %.1f | dark<125 %.2f (%d px)"
              % (int(pcut.sum()), float(duv.min()), float(duv.max()), int(over_c.sum()),
                 float(vals.mean()), float(vals.min()), float((vals < 125).mean()),
                 int((vals < 125).sum())))
        if over_c.any():
            vals2 = lum[over_c]
            print("   parent-edge OVER CHILD: n=%d lum mean %.1f min %.1f dark<125 %d"
                  % (int(over_c.sum()), float(vals2.mean()), float(vals2.min()),
                     int((vals2 < 125).sum())))

    os.makedirs(OUT, exist_ok=True)
    x0 = int(round(c - win))
    x1 = int(round(c + win))
    crop = lambda a: a[x0:x1, x0:x1]

    ov = crop(over).astype(np.uint8).copy()
    ov[crop(dark_internal)] = (255, 0, 0)
    ov[crop(dark_sil)] = (0, 0, 255)
    big = Image.fromarray(ov).resize((ov.shape[1] * 2, ov.shape[0] * 2), Image.NEAREST)
    path1 = os.path.join(OUT, "%s-bent-dark.png" % args.tag)
    big.save(path1)
    print("overlay -> %s (red = dark over parent ink, blue = dark on the silhouette)" % path1)

    def layer(key, deg):
        arr = np.asarray(j.composed(deg, key)).astype(np.float32)
        ovr = arr[:, :, :3] * (arr[:, :, 3:4] / 255.0) + sky[None, None, :] * (1 - arr[:, :, 3:4] / 255.0)
        return crop(ovr).astype(np.uint8)

    path2 = os.path.join(OUT, "%s-child-deg.png" % args.tag)
    im2 = Image.fromarray(layer("child", args.deg))
    im2.resize((im2.width * 2, im2.height * 2), Image.NEAREST).save(path2)
    path3 = os.path.join(OUT, "%s-parent-rest.png" % args.tag)
    par = Image.fromarray(layer("parent", 0.0))
    par.resize((par.width * 2, par.height * 2), Image.NEAREST).save(path3)
    print("layers -> %s, %s" % (path2, path3))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
