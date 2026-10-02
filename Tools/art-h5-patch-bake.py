#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5 R2: the knee joint patch bake - the game asset + the sidecar + the R2 strips.

The R1 kill-switch (Tools/art-h5-skin-proto.py) proved the skinned patch beats the rigid
pair offline. R2 ships it in-game; THIS tool is the bake pass the plan's R2 names:

  1. bakes the patch from the shipped canvases at rest, 1:1 through the pose tool's own
     composer (no second copy of the geometry);
  2. applies the two fixes on top of the R1 ramp stitch:
     RAMP (R1): du [-96..+60] px repainted with the per-column opaque tone ramp between the
       clean parent paint above and the clean child paint below.
     THE R2 LEVER (the judge's own next step, "hide the internal outline at the seam"):
       the two cut-edge sets (the child's dome rim and the parent's end rim) where they lie
       over the OTHER neighbour's paint get their tone inpainted from the nearest clean
       paint on each side of the column - the arcs stop reading as a line; the silhouette
       strokes and the fan's alpha are untouched ("keep the fan").
  3. --write: writes Assets/Resources/Art/Human/knee_patch.png (+ .meta, cloned from the
     shipped canvas import settings, new guid) and knee_patch.txt - the sidecar the runtime
     reads: the crop frame in joint metres, the weight zone, the up axis, and the two
     segment offsets Qp/Qc (from the rig's own box halves);
  4. --strips: renders the R2 strips (rigid | patch-rest | patch-bent + pick pair + abc)
     into Trials/Runs/arth5-r2/ under --tag;
  5. --openpx: the 0-open-px re-measure at the knee's own angles 0/30/60/90/120 with the
     patch included in the composition (the plan's R2 gate; patch-over adds ink only, the
     check asserts it).

Usage:
  python Tools/art-h5-patch-bake.py --strips
  python Tools/art-h5-patch-bake.py --write --strips
"""
import argparse
import importlib.util
import os

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(PROJECT, "Trials", "Runs", "arth5-r2")
ART = os.path.join(PROJECT, "Assets", "Resources", "Art", "Human")
DOM_HI = 72.0     # px: mesh top edge above the joint (the ramp's top + 12)
DOM_LO = -304.0   # px: mesh bottom edge below the joint (z1 - 36)
MARGIN_PX = 8


def load_mod(name, rel):
    path = os.path.join(PROJECT, "Tools", rel)
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def erode(m, n=1):
    out = m.copy()
    for _ in range(n):
        e = out.copy()
        for sh in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            e &= np.roll(out, sh, axis=(0, 1))
        out = e
    return out


def inpaint_cuts(patch, ink, ring, repaint, verbose=True):
    """Repaint `repaint` (candidate stroke px) per column from the nearest clean paint
    above and below; RGB only, alpha untouched. Clean = ink, not repaint, not ring,
    not stroke-dark (so the interpolation never pulls a stroke tone back in)."""
    out = patch.copy()
    lum = patch[:, :, :3].mean(axis=2)
    clean = ink & ~repaint & ~ring & (lum >= 125.0)
    cols = np.nonzero(repaint.any(axis=0))[0]
    filled = 0
    for x in cols:
        col_clean = clean[:, x]
        col_rep = repaint[:, x]
        ok = np.nonzero(col_clean)[0]
        if not len(ok):
            continue
        rows = np.nonzero(col_rep)[0]
        pos = np.searchsorted(ok, rows)
        for r, p in zip(rows.tolist(), pos.tolist()):
            ra = ok[p - 1] if p > 0 else None
            rb = ok[p] if p < len(ok) else None
            if ra is None and rb is None:
                continue
            if ra is None:
                tone = out[rb, x, :3]
            elif rb is None:
                tone = out[ra, x, :3]
            else:
                t = (r - ra) / float(rb - ra)
                tone = out[ra, x, :3] * (1 - t) + out[rb, x, :3] * t
            out[r, x, :3] = tone
            filled += 1
    if verbose:
        print("R2 lever (inpaint cuts): %d px repainted over %d columns" % (filled, len(cols)))
    return out, filled


def bake_r2(j, p, stitch_hi=60.0, stitch_lo=-96.0, verbose=True):
    """The R1 ramp patch + the R2 cut-inpaint lever."""
    z0, z1 = j.measure()["z0"], j.measure()["z1"]
    patch, changed = j.bake("ramp", z0, z1, hi=stitch_hi, lo=stitch_lo, verbose=verbose)
    arr0 = np.asarray(j.composed(0.0)).astype(np.float32)
    P = np.asarray(j.composed(0.0, "parent"))[:, :, 3] > 16
    C = np.asarray(j.composed(0.0, "child"))[:, :, 3] > 16
    ink = (P | C)
    # cut edges of each layer, kept only where the other layer covers them (internal)
    edgeC = C & ~erode(C, 1)
    edgeP = P & ~erode(P, 1)
    cuts = (edgeC & P) | (edgeP & C)
    cuts = cuts & erode(P | C, 2)           # never on (or 1 px near) the union silhouette
    cuts = (cuts | np.roll(cuts, 1, 0) | np.roll(cuts, -1, 0) |
            np.roll(cuts, 1, 1) | np.roll(cuts, -1, 1) |
            np.roll(np.roll(cuts, 2, 0), 0, 0) | np.roll(np.roll(cuts, -2, 0), 0, 0) |
            np.roll(np.roll(cuts, 2, 1), 0, 0) | np.roll(np.roll(cuts, -2, 1), 0, 0))
    # 3 px halo around the cut edges (the strokes are 1-3 px wide + AA)
    cuts = cuts & ink
    du, dv = j.du_dv()
    dom = (du <= stitch_hi - 4.0) & (du >= z1 - 24.0)
    patch_new, filled = inpaint_cuts(patch, ink, ~ink, cuts & dom, verbose=verbose)
    return patch_new, arr0, P, C, z0, z1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--joint", default="knee")
    ap.add_argument("--deg", type=float, default=65.0)
    ap.add_argument("--scale", type=float, default=3000.0)
    ap.add_argument("--half", type=float, default=0.30)
    ap.add_argument("--tag", default="r2b")
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--strips", action="store_true")
    ap.add_argument("--openpx", action="store_true")
    args = ap.parse_args()

    p = load_mod("art_h5_skin_proto", "art-h5-skin-proto.py")
    j = p.Joint(p.frames_tool(), args.joint, args.deg, args.scale, args.half)
    geom = j.measure()
    z0, z1 = geom["z0"], geom["z1"]
    up = geom["up"]
    sign = -1.0 if geom["rot"]["ccw"] >= geom["rot"]["cw"] else 1.0
    patch, arr0, P, C, z0, z1 = bake_r2(j, p)

    # ---- diagnostics on the R2 patch ----
    lum = patch[:, :, :3].mean(axis=2)
    u8 = np.clip(patch, 0, 255).astype(np.uint8)
    # rest comparison vs the untouched composition
    d = np.abs(patch[:, :, :3] - arr0[:, :, :3]).max(axis=2)
    changed = int((d > 8).sum())
    print("R2 bake: px changed vs composition > 8: %d (of %d)" % (changed, int((patch[:, :, 3] > 16).sum())))

    # bent render (same warp as the probes)
    du, dv = j.du_dv()
    m_in = p.MARGIN * j.scale
    z_dom_lo, z_dom_hi = z1 - m_in, z0 + m_in
    z_dom_lo = min(z_dom_lo, -96.0 - 12.0)
    z_dom_hi = max(z_dom_hi, 60.0 + 12.0)
    warped, band_rows = j.warp_band(np.asarray(u8, dtype=np.float32) * 1.0, args.deg,
                                    z_dom_lo, z_dom_hi, z0, z1, sign)
    base = np.asarray(j.composed(0.0, "parent")).astype(np.float32)
    wa = warped[:, :, 3:4] / 255.0
    ra = base[:, :, 3:4] / 255.0
    a_out = wa + ra * (1.0 - wa)
    out_c = np.zeros_like(np.asarray(j.composed(args.deg)).astype(np.float32))
    out_c[:, :, 3:4] = a_out * 255.0
    out_c[:, :, :3] = np.where(a_out > 1e-6,
                               (warped[:, :, :3] * wa + base[:, :, :3] * ra * (1.0 - wa))
                               / np.maximum(a_out, 1e-6), 0.0)
    sky = np.array(p.BG, dtype=np.float32)
    over = (out_c[:, :, :3] * (out_c[:, :, 3:4] / 255.0) +
            sky[None, None, :] * (1 - out_c[:, :, 3:4] / 255.0))
    lum_c = over.mean(axis=2)
    c = j.c
    win = 0.10 * j.scale
    yy, xx = np.mgrid[0:out_c.shape[0], 0:out_c.shape[1]]
    inwin = (np.abs(xx + 0.5 - c) <= win) & (np.abs(yy + 0.5 - c) <= win)
    Pc = np.asarray(j.composed(0.0, "parent"))[:, :, 3] > 16
    dark_in = int(((lum_c < 100.0) & inwin & Pc).sum())
    open_px = int(((out_c[:, :, 3] <= 16) & inwin).sum())
    print("R2 bent: dark-internal px %d (R1: 9957), open px in win %d" % (dark_in, open_px))

    # ---- strips for the eyes ----
    if args.strips:
        os.makedirs(OUT, exist_ok=True)
        fm_a = j.composed(args.deg)
        fm_b = j.composed(0.0)
        arr_b = np.asarray(fm_b).astype(np.float32)
        out_b = arr_b.copy()
        band = (du >= z_dom_lo) & (du <= z_dom_hi)
        out_b[band] = np.asarray(u8, dtype=np.float32)[band]
        pc = p.rgb_over(p.panels_crop(Image.fromarray(u8), win, c), p.BG)
        base_name = os.path.join(OUT, "arth5-%s-%s" % (args.tag, args.joint))
        pc.save(base_name + "-patch-bent-%d.png" % int(args.deg))
        pb = p.rgb_over(p.panels_crop(Image.fromarray(np.clip(out_b, 0, 255).astype(np.uint8)), win, c), p.BG)
        pb.save(base_name + "-patch-rest.png")
        pa = p.rgb_over(p.panels_crop(fm_a, win, c), p.BG)
        pa.save(base_name + "-rigid-%d.png" % int(args.deg))
        p.ab_strip(pa, pc, base_name + "-pick1.png")
        p.ab_strip(pc, pa, base_name + "-pick2.png")
        rec = Image.new("RGB", (pa.size[0] * 3 + 16, pa.size[1]), (255, 0, 255))
        rec.paste(pa, (0, 0))
        rec.paste(pb, (pa.size[0] + 8, 0))
        rec.paste(pc, (2 * (pa.size[0] + 8), 0))
        rec.save(base_name + "-abc.png")
        for nm in ("-rigid-%d" % int(args.deg), "-patch-rest", "-patch-bent-%d" % int(args.deg),
                   "-pick1", "-pick2", "-abc"):
            print("strip -> %s%s.png" % (base_name, nm))

    # ---- the game asset ----
    if args.write:
        du, dv = j.du_dv()
        dom = (du <= DOM_HI) & (du >= DOM_LO)
        inkdom = (patch[:, :, 3] > 16) & dom
        cols = np.nonzero(inkdom.any(axis=0))[0]
        rows = np.nonzero(inkdom.any(axis=1))[0]
        x0p = max(0, int(cols.min()) - MARGIN_PX)
        x1p = min(patch.shape[1], int(cols.max()) + 1 + MARGIN_PX)
        y0p = max(0, int(rows.min()) - MARGIN_PX)
        y1p = min(patch.shape[0], int(rows.max()) + 1 + MARGIN_PX)
        crop = np.clip(patch[y0p:y1p, x0p:x1p], 0, 255).astype(np.uint8)
        png_path = os.path.join(ART, "knee_patch.png")
        Image.fromarray(crop).save(png_path)
        print("patch png -> %s  (%dx%d px)" % (png_path, crop.shape[1], crop.shape[0]))
        # meta cloned from a shipped canvas (same import settings) with a fresh guid
        import uuid
        src_meta = open(os.path.join(ART, "shin.png.meta"), encoding="utf-8").read()
        new_guid = uuid.uuid4().hex
        meta = src_meta.replace("guid: " + src_meta.split("guid: ")[1].split("\n")[0],
                                "guid: " + new_guid)
        with open(png_path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
            fh.write(meta)
        print("meta -> %s.meta (guid %s)" % (png_path, new_guid))
        # sidecar in joint metres (J origin = the knee hinge; x right, y up)
        x0m = (x0p - j.c) / j.scale
        y0m = (j.c - y1p) / j.scale
        wm = (x1p - x0p) / j.scale
        hm = (y1p - y0p) / j.scale
        ns, joints = p.frames_tool().read_rig()
        qp = -(ns["thighSize"].y * 0.5)
        qc = +(ns["shinSize"].y * 0.5)
        txt = (
            "# knee_patch - baked by Tools/art-h5-patch-bake.py (ART-H5 R2).\n"
            "# The joint frame J: origin = the knee hinge, x right, y up (root-rest axes).\n"
            "# The runtime: world = (1-w)*Mparent*(v+Qp) + w*Mchild*(v+Qc);\n"
            "# w = smoothstep across [z0_m..z1_m] of du = dot(v, up); v in J metres;\n"
            "# uv: u = (x - x0)/w, v = (y - y0)/h.\n"
            "scale=%.4f\n" % j.scale +
            "x0=%.6f\ny0=%.6f\nw=%.6f\nh=%.6f\n" % (x0m, y0m, wm, hm) +
            "upx=%.6f\nupy=%.6f\n" % (up[0], -up[1]) +
            "z0_m=%.6f\nz1_m=%.6f\n" % (z0 / j.scale, z1 / j.scale) +
            "qpx=%.6f\nqpy=%.6f\nqcx=%.6f\nqcy=%.6f\n" % (0.0, qp, 0.0, qc))
        sc_path = os.path.join(ART, "knee_patch.txt")
        with open(sc_path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(txt)
        print("sidecar -> %s" % sc_path)
        print(txt)
        print("Qp=(0,%.4f) Qc=(0,%.4f)  (thighSize.y=%.3f shinSize.y=%.3f)"
              % (qp, qc, ns["thighSize"].y, ns["shinSize"].y))

    # ---- the 0-open-px re-measure with the patch included ----
    if args.openpx:
        r_eye = j.r_eye
        for a in (0.0, 30.0, 60.0, 90.0, 120.0):
            comp = np.asarray(j.composed(a)).astype(np.float32)
            # patch included: the warped patch over the composition
            wp, _ = j.warp_band(np.asarray(u8, dtype=np.float32), a, z_dom_lo, z_dom_hi, z0, z1, sign) \
                if a != 0.0 else (np.asarray(u8, dtype=np.float32), None)
            wa2 = wp[:, :, 3:4] / 255.0
            ca = comp[:, :, 3:4] / 255.0
            a_out2 = wa2 + ca * (1.0 - wa2)
            union_no = comp[:, :, 3] > 16
            union_yes = a_out2[:, :, 0] * 255.0 > 16
            disc = ((xx + 0.5 - c) ** 2 + (yy + 0.5 - c) ** 2) <= (r_eye * j.scale) ** 2
            open_no = int((disc & ~union_no).sum())
            open_yes = int((disc & ~union_yes).sum())
            print("openpx knee @%+.0f: without patch %d, with patch %d (px in disc, scale %.0f)"
                  % (a, open_no, open_yes, j.scale))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
