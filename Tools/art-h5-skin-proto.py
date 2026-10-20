#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5 R1 probe: THE SKINNED JOINT - the Route A kill-switch (offline, no game changes).

Route A's claim (Trials/Runs/art-route-a-skinning-plan.md): replace the OVERLAP REGION of a
joint with ONE small mesh spanning both segments, vertices carrying linear-blend skin weights
(1 on the parent's side, 1 on the child's, smooth across), so the surface bends continuously
about the hinge and the joint reads as ONE limb instead of two cut circles.

This tool bakes the joint patch from the shipped canvases (Assets/Resources/Art/Human, 1:1,
through Tools/art-h1-pose-frames.py's own rig reader/composer - no second copy of the
geometry) and renders three strips at the round's bends:
  (a) today's rigid pair      (b) the patch at rest      (c) the patch bent.
The standing eyes (Tools/art-h4-eye-read.py --knee/--ankle/--pick) read the strips; the
census below is the lane's own instrument:
  arcs_geom / arcs_vis - px on the child's cut edge over the parent's ink, visible or not
  tone_step            - the colour jump across that edge (max-channel, mean/max)
  border_residual      - the patch's own border at rest (must read as nothing)
plus the family numbers (ink px, semi px, mean alpha, exposed overlap).

PRE-COMMITTED GO/NO-GO RULE (written before any read): GO iff
  [A] the patch's knee read BEATS the rigid knee read (strictly higher score, or equal score
      with seam_arc flipped to "нет"), AND
  [B] the knee pick picks the patch in BOTH panel orders.
Anything else = NO-GO; the ankle numbers are recorded, secondary, and cannot upgrade a NO-GO.

Usage:
  python Tools/art-h5-skin-proto.py --joint knee --deg 65 --mode geom
  python Tools/art-h5-skin-proto.py --joint knee,ankle --mode render --stitch ramp
"""
import argparse
import importlib.util
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(PROJECT, "Trials", "Runs", "arth5-skin-proto")
SCALE = 3000.0            # px per metre
HALF = 0.30               # frame half-size, m
WIN = 0.10                # crop half-window for the eye panels, m
MARGIN = 0.012            # band margin beyond the apexes, m
BG = (140, 199, 240)      # the game's sky


def frames_tool():
    path = os.path.join(PROJECT, "Tools", "art-h1-pose-frames.py")
    spec = importlib.util.spec_from_file_location("art_h1_pose_frames", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def bilinear(arr, x, y):
    """arr (N,N,C) float, x/y float arrays (frame px). Clamped."""
    n = arr.shape[0]
    x = np.clip(x, 0.0, n - 1.001)
    y = np.clip(y, 0.0, n - 1.001)
    x0 = np.floor(x).astype(np.int32)
    y0 = np.floor(y).astype(np.int32)
    fx = (x - x0)[..., None]
    fy = (y - y0)[..., None]
    a = arr[y0, x0]
    b = arr[y0, x0 + 1]
    c = arr[y0 + 1, x0]
    d = arr[y0 + 1, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


class Joint(object):
    def __init__(self, pf, name, deg, scale=SCALE, half=HALF):
        self.pf = pf
        self.name = name
        self.deg = float(deg)
        self.scale = float(scale)
        self.half = float(half)
        self.N = int(round(2.0 * self.half * self.scale))
        self.c = self.N / 2.0
        ns, joints = pf.read_rig()
        for jname, pkey, ckey, cvar, pvar, h in pf.JOINTS:
            if jname == name:
                self.pkey, self.ckey = pkey, ckey
                break
        else:
            raise SystemExit("no such joint: " + name)
        canvases = {k: pf.load_canvas(v) for k, v in pf.CANVAS.items()}
        self.canvases = {k: v for k, v in canvases.items() if v is not None}
        self.spec = joints[cvar]
        self.anchor_p = self.spec["anchor_parent"]
        self.anchor_c = self.spec["anchor"]
        self.halfw_p = pf.ink_half(self.canvases[pkey], self.anchor_p)
        self.halfw_c = pf.ink_half(self.canvases[ckey], self.anchor_c)
        self.r_eye = min(self.halfw_p, self.halfw_c)

    # ---- composition (the pose tool does all the drawing) ----------------------------
    def composed(self, deg, only=None):
        if only == "parent":
            layers = [(self.pkey, self.anchor_p, 0.0)]
        elif only == "child":
            layers = [(self.ckey, self.anchor_c, deg)]
        else:
            layers = [(self.pkey, self.anchor_p, 0.0), (self.ckey, self.anchor_c, deg)]
        return self.pf.compose(self.canvases, layers, self.half, self.scale)

    def alpha(self, deg, only=None):
        return np.asarray(self.composed(deg, only))[:, :, 3]

    # ---- joint frame: the limb axis (toward the parent) ------------------------------
    def axis(self):
        if hasattr(self, "_up"):
            return self._up
        par = self.alpha(0.0, "parent") > 16
        ys, xs = np.nonzero(par)
        v = np.array([xs.mean() + 0.5 - self.c, ys.mean() + 0.5 - self.c])
        v = v / np.linalg.norm(v)
        self._up = v
        return v

    def grid(self):
        if not hasattr(self, "_g"):
            n = self.N
            ys, xs = np.mgrid[0:n, 0:n]
            self._rx = (xs + 0.5 - self.c).astype(np.float64)
            self._ry = (ys + 0.5 - self.c).astype(np.float64)
            self._g = True
        return self._rx, self._ry

    def du_dv(self):
        up = self.axis()
        rx, ry = self.grid()
        du = rx * up[0] + ry * up[1]
        dv = -rx * up[1] + ry * up[0]
        return du, dv

    # ---- the geometry the bake needs -------------------------------------------------
    def measure(self):
        up = self.axis()
        du, dv = self.du_dv()
        P = self.alpha(0.0, "parent") > 16
        C = self.alpha(0.0, "child") > 16
        near30 = np.abs(du) <= 30.0
        half_p = float(np.abs(dv[P & near30]).max()) if (P & near30).any() else 0.0
        half_c = float(np.abs(dv[C & near30]).max()) if (C & near30).any() else 0.0
        hw = 0.5 * self.r_eye * self.scale
        near = np.abs(dv) <= hw
        z1 = float(du[P & near].min()) if (P & near).any() else 0.0
        z0 = float(du[C & near].max()) if (C & near).any() else 0.0
        # rotation sign self-calibration: warp the child's rest px by M, compare to the
        # rigid child at deg (the ground truth), on the px within 0.07 m of the joint.
        res = {}
        for tag, s in (("ccw", -1.0), ("cw", 1.0)):
            res[tag] = self._rot_iou(s, du, dv, C)
        return dict(z0=z0, z1=z1, half_p=half_p, half_c=half_c, up=up, rot=res)

    def rot_matrix(self, deg, sign):
        th = math.radians(deg)
        ct, st = math.cos(th), math.sin(th) * sign
        return np.array([[ct, st], [-st, ct]])

    def _rot_iou(self, sign, du, dv, C):
        Cdeg = self.alpha(self.deg, "child") > 16
        sel = C & (np.abs(du) < 0.07 * self.scale) & (np.abs(dv) < 0.12 * self.scale)
        ys, xs = np.nonzero(sel)
        rx = xs + 0.5 - self.c
        ry = ys + 0.5 - self.c
        M = self.rot_matrix(self.deg, sign)
        mx = M[0, 0] * rx + M[0, 1] * ry
        my = M[1, 0] * rx + M[1, 1] * ry
        tx = np.clip((mx + self.c).astype(int), 0, self.N - 1)
        ty = np.clip((my + self.c).astype(int), 0, self.N - 1)
        hit = Cdeg[ty, tx]
        return float(hit.sum()) / max(1.0, float(sel.sum()))   # fraction landing on child ink

    # ---- the patch: bake + stitch ----------------------------------------------------
    def bake(self, stitch, z0, z1, hi=60.0, lo=-96.0, verbose=True):
        arr = np.asarray(self.composed(0.0)).astype(np.float32)
        patch = arr.copy()
        if stitch == "off":
            return patch, 0
        # ramp stitch: per column, replace the zone [lo, hi] (du px) with an OPAQUE vertical
        # tone ramp between the clean parent paint above and the clean child paint below -
        # the tone-match rule; feathered over 10 px at both zone edges.
        up = self.axis()
        du, dv = self.du_dv()
        a = arr[:, :, 3]
        ink = a > 16
        cols = np.nonzero(ink.any(axis=0))[0]
        changed = 0
        for x in cols:
            u = du[:, x]
            col_ink = ink[:, x]
            if not col_ink.any():
                continue
            top_sel = col_ink & (u >= hi + 2) & (u <= hi + 14) & (a[:, x] >= 240)
            bot_sel = col_ink & (u <= lo - 2) & (u >= lo - 14) & (a[:, x] >= 240)
            if not top_sel.any() or not bot_sel.any():
                continue
            c_top = arr[top_sel, x, :3].mean(axis=0)
            c_bot = arr[bot_sel, x, :3].mean(axis=0)
            rows = np.nonzero(col_ink & (u <= hi) & (u >= lo))[0]
            if not len(rows):
                continue
            t = np.clip((u[rows] - lo) / (hi - lo), 0.0, 1.0)[:, None]
            ramp = c_bot * (1 - t) + c_top * t
            m = np.clip((hi - u[rows]) / 10.0, 0.0, 1.0) * np.clip((u[rows] - lo) / 10.0, 0.0, 1.0)
            m = m[:, None]
            patch[rows, x, :3] = arr[rows, x, :3] * (1 - m) + ramp * m
            sel = (a[rows, x] >= 160)[:, None]
            newa = arr[rows, x, 3:4] * (1 - m) + 255.0 * m
            patch[rows, x, 3:4] = np.where(sel, newa, arr[rows, x, 3:4])
            changed += int(len(rows))
        if verbose:
            print("stitch ramp: %d px repainted in du [%+.0f, %+.0f]" % (changed, lo, hi))
        return patch, changed

    # ---- the warp: per-pixel linear-blend skinning, FORWARD splat --------------------
    def warp_band(self, patch, deg, dom_lo, dom_hi, w0, w1, sign, ss=3):
        """Every ink pixel of the band lands at its skinned position; the skinned image is
        accumulated (premultiplied, bilinear, ss x ss sub-samples per pixel). Sources outside
        the band never move; destinations the map does not reach stay rigid in the caller.
        Returns (straight-RGBA skinned frame, (rmin, rmax))."""
        n = self.N
        up = self.axis()
        du, dv = self.du_dv()
        parr = np.asarray(patch).astype(np.float32)
        # sources: every ink pixel - the weight field itself is identity above the zone and
        # full below it, so those pixels splat onto themselves; only the zone bends
        srcmask = parr[:, :, 3] > 8
        rmin = int(np.nonzero(srcmask.any(axis=1))[0].min())
        rmax = int(np.nonzero(srcmask.any(axis=1))[0].max())
        ys, xs = np.nonzero(srcmask)
        src = parr[ys, xs]
        M = self.rot_matrix(deg, sign)
        acc = np.zeros((n, n, 4), np.float64)      # premultiplied rgb + alpha weight
        offs = [(ox / ss, oy / ss) for ox in range(ss) for oy in range(ss)]
        sub_w = 1.0 / len(offs)
        xb = xs.astype(np.float64) + 0.5 - self.c
        yb = ys.astype(np.float64) + 0.5 - self.c
        for sx, sy in offs:
            rx = xb - 0.5 + sx
            ry = yb - 0.5 + sy
            d = rx * up[0] + ry * up[1]
            t = np.clip((w0 - d) / (w0 - w1), 0.0, 1.0)
            w = t * t * (3.0 - 2.0 * t)
            mx = M[0, 0] * rx + M[0, 1] * ry
            my = M[1, 0] * rx + M[1, 1] * ry
            dx = rx + w * (mx - rx) + self.c
            dy = ry + w * (my - ry) + self.c
            x0 = np.floor(dx - 0.5)
            y0 = np.floor(dy - 0.5)
            fx = (dx - 0.5) - x0
            fy = (dy - 0.5) - y0
            x0 = x0.astype(np.int64)
            y0 = y0.astype(np.int64)
            pa = (src[:, 3] / 255.0) * sub_w
            pw = src[:, :3] * (pa[:, None])
            for ox, oy, wg in ((0, 0, (1 - fx) * (1 - fy)), (1, 0, fx * (1 - fy)),
                               (0, 1, (1 - fx) * fy), (1, 1, fx * fy)):
                xi = x0 + ox
                yi = y0 + oy
                ok = (xi >= 0) & (xi < n) & (yi >= 0) & (yi < n)
                if not ok.any():
                    continue
                idx = yi[ok] * n + xi[ok]
                wgv = wg[ok]
                for ch in range(3):
                    acc[:, :, ch] += np.bincount(
                        idx, weights=(pw[ok, ch] * wgv), minlength=n * n).reshape(n, n)
                acc[:, :, 3] += np.bincount(
                    idx, weights=(pa[ok] * wgv), minlength=n * n).reshape(n, n)
        out = np.zeros((n, n, 4), np.float32)
        aw = np.maximum(acc[:, :, 3], 1e-9)
        for ch in range(3):
            out[:, :, ch] = np.clip(acc[:, :, ch] / aw, 0, 255)
        out[:, :, 3] = np.clip(acc[:, :, 3], 0.0, 1.0) * 255.0
        return out, (rmin, rmax)


def census(comp, par_alpha, chi_alpha, m, z0, z1, win_px, r_eye_px, label, out_lines,
           patch_alpha=None):
    h, w = comp.shape[0], comp.shape[1]
    ys, xs = np.mgrid[0:h, 0:w]
    cx, cy = w / 2.0, h / 2.0
    win = (np.abs(xs + 0.5 - cx) <= win_px) & (np.abs(ys + 0.5 - cy) <= win_px)
    P = par_alpha > 16
    C = chi_alpha > 16
    E = C.copy()
    for sh in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        E &= np.roll(C, sh, axis=(0, 1))
    edge = C & ~E
    arcs_geom = int((edge & P & win).sum())
    covered = (m > 0.5) if m is not None else np.zeros_like(P, dtype=bool)
    if patch_alpha is not None:
        hidden = covered & (patch_alpha > 16)
    else:
        hidden = np.zeros_like(P, dtype=bool)
    vis = edge & P & win & ~hidden
    arcs_vis = int(vis.sum())
    # tone step: max-channel |d| from a visible edge px to its parent-side neighbours
    g = comp[:, :, :3].astype(np.float32)
    steps = []
    yy, xx = np.nonzero(vis)
    for y, x in zip(yy.tolist(), xx.tolist()):
        base = g[y, x]
        best = None
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and P[ny, nx] and not C[ny, nx]:
                d = float(np.abs(g[ny, nx] - base).max())
                best = d if best is None else max(best, d)
        if best is not None:
            steps.append(best)
    tstep_mean = float(np.mean(steps)) if steps else 0.0
    tstep_max = float(np.max(steps)) if steps else 0.0
    a = comp[:, :, 3]
    ink = a > 16
    semi = int(((a > 16) & (a < 240)).sum())
    p_ink = int(P.sum())
    p_expo = (P & ~C)
    ex = p_ink and int(p_expo.sum()) or 0
    ex_mid = int(((par_alpha > 100) & (par_alpha <= 200) & p_expo).sum()) if p_ink else 0
    out_lines.append(
        "%s: ink %d semi %d meanA %.1f | arcs_geom %d arcs_vis %d | tone_step mean %.1f max %.1f"
        % (label, int(ink.sum()), semi, float(a[ink].mean()) if ink.any() else 0.0,
           arcs_geom, arcs_vis, tstep_mean, tstep_max))
    out_lines.append("    exposed %d (mid100-200 %d)" % (ex, ex_mid))
    return dict(arcs_geom=arcs_geom, arcs_vis=arcs_vis, tstep_mean=tstep_mean,
                tstep_max=tstep_max, semi=semi, ink=int(ink.sum()))


def panels_crop(img, win_px, c):
    x0 = int(round(c - win_px))
    x1 = int(round(c + win_px))
    return img.crop((x0, x0, x1, x1))


def rgb_over(img, bg):
    cell = Image.new("RGBA", img.size, bg + (255,))
    cell.alpha_composite(img)
    return cell.convert("RGB")


def ab_strip(p1, p2, out):
    w, h = p1.size
    strip = Image.new("RGB", (w * 2 + 8, h), (255, 0, 255))
    strip.paste(p1, (0, 0))
    strip.paste(p2, (w + 8, 0))
    strip.save(out)
    print("strip %s (%dx%d)" % (out, strip.width, strip.height))


def run_joint(name, deg, stitch, mode, scale, half, blend="zone", stitch_hi=60.0, stitch_lo=-96.0, tag=""):
    pf = frames_tool()
    j = Joint(pf, name, deg, scale, half)
    geom = j.measure()
    up = geom["up"]
    z0, z1 = geom["z0"], geom["z1"]
    sign = -1.0 if geom["rot"]["ccw"] >= geom["rot"]["cw"] else 1.0
    print("== %s @%+.0f deg: R_eye %.4f m | up (%.3f, %.3f) | z0 %+.0f px (%+.4f m) "
          "z1 %+.0f px (%+.4f m) | halfw p %.0f c %.0f px | rot ccw %.3f cw %.3f -> sign %s"
          % (name, deg, j.r_eye, up[0], up[1], z0, z0 / scale, z1, z1 / scale,
             geom["half_p"], geom["half_c"], geom["rot"]["ccw"], geom["rot"]["cw"],
             "ccw" if sign < 0 else "cw"))
    if mode == "geom":
        save_diag(j, geom, name)
        return

    win_px = WIN * scale
    r_eye_px = j.r_eye * scale
    lines = []
    lines.append("== %s @%+.0f deg  scale %.0f  win %.2f m  r_eye %.4f m  z0 %+.0f z1 %+.0f"
                 % (name, deg, scale, WIN, j.r_eye, z0, z1))

    # (a) today's rigid pair
    fm_a = j.composed(deg)
    arr_a = np.asarray(fm_a)
    par_a = j.alpha(deg, "parent")
    chi_a = j.alpha(deg, "child")
    census(arr_a, par_a, chi_a, None, z0, z1, win_px, r_eye_px, "(a) rigid  ", lines)

    # patch bake + warps
    patch, changed = j.bake(stitch, z0, z1, hi=stitch_hi, lo=stitch_lo)
    # (b) the patch at rest: replace the band rows with the patch as-is; the band covers the
    # stitch zone too, so its edges never cut the repaint
    du, dv = j.du_dv()
    m_in = MARGIN * scale
    z_dom_lo, z_dom_hi = z1 - m_in, z0 + m_in
    if stitch == "ramp":
        z_dom_lo = min(z_dom_lo, stitch_lo - 12.0)
        z_dom_hi = max(z_dom_hi, stitch_hi + 12.0)
    band = (du >= z_dom_lo) & (du <= z_dom_hi)
    fm_b = j.composed(0.0)
    arr_b = np.asarray(fm_b).astype(np.float32)
    out_b = arr_b.copy()
    out_b[band] = patch[band]
    # border residual at rest: patch edges vs the untouched composition
    border = ((du >= z_dom_hi - 6) & (du <= z_dom_hi + 6)) | \
             ((du >= z_dom_lo - 6) & (du <= z_dom_lo + 6))
    inside_ink_b = (arr_b[:, :, 3] > 16) & border
    d_res = np.abs(out_b[:, :, :3] - arr_b[:, :, :3]).max(axis=2)
    print("border residual at rest: max %.1f, px>8 %d (of %d border-ink px); repainted %d px"
          % (float(d_res[inside_ink_b].max()) if inside_ink_b.any() else 0.0,
             int((d_res[inside_ink_b] > 8).sum()), int(inside_ink_b.sum()), changed))
    diff_rest = int((d_res > 8).sum())
    print("rest diff overall: px>8 %d" % diff_rest)
    census(np.asarray(out_b, dtype=np.uint8), j.alpha(0.0, "parent"), j.alpha(0.0, "child"),
           band, z0, z1, win_px, r_eye_px, "(b) patch-rest", lines,
           patch_alpha=patch[:, :, 3])

    # (c) the patch bent
    if blend == "tight":
        w0, w1 = 75.0, -75.0
    else:
        w0, w1 = z0, z1
    warped, band_rows = j.warp_band(patch, deg, z_dom_lo, z_dom_hi, w0, w1, sign)
    arr_c = np.asarray(fm_a).astype(np.float32)
    # the base under the skinned surface: the PARENT layer only (it is static in the bend);
    # the rigid child is not the surface any more anywhere - its pixels all belong to the
    # splatted skin now (identity above the zone, rigid below, blended across)
    par_only = np.asarray(j.composed(0.0, "parent")).astype(np.float32)
    base = par_only
    # the skinned surface OVER that base
    wa = warped[:, :, 3:4] / 255.0
    ra = base[:, :, 3:4] / 255.0
    a_out = wa + ra * (1.0 - wa)
    out_c = np.zeros_like(arr_c)
    out_c[:, :, 3:4] = a_out * 255.0
    out_c[:, :, :3] = np.where(a_out > 1e-6,
                               (warped[:, :, :3] * wa + base[:, :, :3] * ra * (1.0 - wa))
                               / np.maximum(a_out, 1e-6), 0.0)
    rmin, rmax = band_rows
    m_full = np.where(warped[:, :, 3] > 16, 1.0, 0.0).astype(np.float32)
    pal_full = warped[:, :, 3]
    census(np.asarray(np.clip(out_c, 0, 255), dtype=np.uint8), par_a, chi_a,
           m_full, z0, z1, win_px, r_eye_px, "(c) patch-bent", lines, patch_alpha=pal_full)
    # visible arcs for (c): the child edge not hidden by the patch
    P = par_a > 16
    C = chi_a > 16
    E = C.copy()
    for sh in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        E &= np.roll(C, sh, axis=(0, 1))
    edge = C & ~E
    print("(c) arcs hidden by the patch: %d of %d"
          % (int((edge & P & (m_full > 0.5) & (pal_full > 16)).sum()), int((edge & P).sum())))
    # the seam in (c): on the (a)'s child-over-parent edge px, the colour step that remains
    win_m = (np.abs(np.mgrid[0:out_c.shape[0], 0:out_c.shape[1]][1] + 0.5 - j.c) <= win_px) & \
            (np.abs(np.mgrid[0:out_c.shape[0], 0:out_c.shape[1]][0] + 0.5 - j.c) <= win_px)
    selE = edge & P & win_m
    gg = np.asarray(np.clip(out_c, 0, 255), dtype=np.uint8)[:, :, :3].astype(np.float32)
    aa8 = np.asarray(np.clip(out_c, 0, 255), dtype=np.uint8)[:, :, 3].astype(np.float32)
    st_c, st_a = [], []
    yy, xx = np.nonzero(selE)
    for y, x in zip(yy.tolist(), xx.tolist()):
        base_rgb = gg[y, x]
        base_a = aa8[y, x]
        best, besta = None, None
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < out_c.shape[0] and 0 <= nx < out_c.shape[1] and P[ny, nx] and not C[ny, nx]:
                d = float(np.abs(gg[ny, nx] - base_rgb).max())
                best = d if best is None else max(best, d)
                da = abs(float(aa8[ny, nx]) - float(base_a))
                besta = da if besta is None else max(besta, da)
        if best is not None:
            st_c.append(best)
            st_a.append(besta)
    if st_c:
        arrc = np.array(st_c)
        print("(c) seam step on the old edge px: mean %.1f max %.1f | px>30 %d of %d | alpha step mean %.1f"
              % (float(arrc.mean()), float(arrc.max()), int((arrc > 30).sum()), len(st_c),
                 float(np.mean(st_a))))
    diff_ac = int((np.abs(out_c[:, :, :3] - arr_c[:, :, :3]).max(axis=2) > 8).sum())
    print("(a)->(c) changed px: %d" % diff_ac)
    aa = arr_c[:, :, 3] > 16
    cc = out_c[:, :, 3] > 16
    lost = aa & ~cc
    gain = cc & ~aa
    print("ink lost %d gained %d (of (a) ink %d)" % (int(lost.sum()), int(gain.sum()), int(aa.sum())))

    # panels + strips
    os.makedirs(OUT, exist_ok=True)
    c = j.c
    pa = rgb_over(panels_crop(fm_a, win_px, c), BG)
    pb = rgb_over(panels_crop(Image.fromarray(np.clip(out_b, 0, 255).astype(np.uint8)), win_px, c), BG)
    pc = rgb_over(panels_crop(Image.fromarray(np.clip(out_c, 0, 255).astype(np.uint8)), win_px, c), BG)
    base = os.path.join(OUT, "arth5-%s%s" % (tag, name))
    pa.save(base + "-rigid-%d.png" % int(deg))
    pb.save(base + "-patch-rest.png")
    pc.save(base + "-patch-bent-%d.png" % int(deg))
    ab_strip(pa, pc, base + "-pick1.png")      # left rigid, right patch
    ab_strip(pc, pa, base + "-pick2.png")      # left patch, right rigid
    # three-panel record
    wp3 = pa.size[0]
    hh = pa.size[1]
    rec = Image.new("RGB", (wp3 * 3 + 16, hh), (255, 0, 255))
    rec.paste(pa, (0, 0))
    rec.paste(pb, (wp3 + 8, 0))
    rec.paste(pc, (2 * (wp3 + 8), 0))
    rec.save(base + "-abc.png")
    print("panels -> %s-{rigid-%d,patch-rest,patch-bent-%d,pick1,pick2,abc}.png"
          % (base, int(deg), int(deg)))
    with open(os.path.join(OUT, "%s-census.txt" % name), "a", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")
    for ln in lines:
        print(ln)


def save_diag(j, geom, name):
    os.makedirs(OUT, exist_ok=True)
    win_px = WIN * j.scale
    c = j.c
    par = j.composed(0.0, "parent")
    chi = j.composed(0.0, "child")
    stack = j.composed(0.0)
    p1 = rgb_over(panels_crop(par, win_px, c), (32, 32, 40))
    p2 = rgb_over(panels_crop(chi, win_px, c), (32, 32, 40))
    p3 = rgb_over(panels_crop(stack, win_px, c), BG)
    dr = ImageDraw.Draw(p3)
    up = geom["up"]
    for z, col, tag in ((geom["z0"], (255, 80, 80), "z0 child_top"), (geom["z1"], (80, 255, 120), "z1 parent_bottom")):
        py = c + z * up[1]
        px = c + z * up[0]
        dr.line((px - win_px, py, px + win_px, py), fill=col, width=2)
        dr.text((4, py - 14), "%s %+.0f px" % (tag, z), fill=col)
    big = [im.resize((im.size[0] * 2, im.size[1] * 2), Image.NEAREST) for im in (p1, p2, p3)]
    w, h = big[0].size
    dg = Image.new("RGB", (w * 3 + 16, h), (0, 0, 0))
    for i, im in enumerate(big):
        dg.paste(im, (i * (w + 8), 0))
    dg.save(os.path.join(OUT, "%s-diag.png" % name))
    print("diag -> %s/%s-diag.png (parent-only | child-only | stack + zone rows)" % (OUT, name))
    zwin = 0.085 * j.scale
    for tag, im, zbg in (("stackzoom", stack, BG), ("childzoom", chi, (32, 32, 40)),
                         ("parentzoom", par, (32, 32, 40))):
        zc = rgb_over(panels_crop(im, zwin, c), zbg)
        zc = zc.resize((zc.size[0] * 2, zc.size[1] * 2), Image.NEAREST)
        zc.save(os.path.join(OUT, "%s-%s.png" % (name, tag)))
    print("zooms -> %s/{stackzoom,childzoom,parentzoom}.png (2x, +-0.085 m)" % name)
    # coarse shape map, 12 px per cell over +-252 px (frame axes, up = the parent side)
    cell = 12
    win = 21
    ap = j.alpha(0.0, "parent")
    ac = j.alpha(0.0, "child")
    P = ap > 16
    C = ac > 16
    a = np.asarray(j.composed(0.0))[:, :, 3]
    print("shape map (+-252 px, 12 px/cell: p=P only c=C only b=both . none; UPPER = semi px):")
    for yy in range(-win, win + 1):
        row = []
        for xx in range(-win, win + 1):
            y0 = int(c + yy * cell)
            x0 = int(c + xx * cell)
            y1 = y0 + cell
            x1 = x0 + cell
            if y0 < 0 or x0 < 0 or y1 > j.N or x1 > j.N:
                row.append(" ")
                continue
            pp = P[y0:y1, x0:x1].any()
            cc = C[y0:y1, x0:x1].any()
            sem = int(((a[y0:y1, x0:x1] > 16) & (a[y0:y1, x0:x1] < 240)).sum())
            ch = "p" if pp and not cc else ("c" if cc and not pp else ("b" if pp and cc else "."))
            if sem >= 8:
                ch = ch.upper()
            row.append(ch)
        print("  " + "".join(row))
    # per-row anatomy, |dv| <= 0.06 m: the layers separated
    du, dv = j.du_dv()
    arr = np.asarray(j.composed(0.0)).astype(np.int16)
    a = arr[:, :, 3]
    ap = j.alpha(0.0, "parent")
    ac = j.alpha(0.0, "child")
    P = ap > 16
    C = ac > 16
    sel_dv = np.abs(dv) <= 0.06 * j.scale
    lum = (arr[:, :, :3].astype(np.int32).sum(axis=2) / 3.0)
    print("row anatomy (du px: inkP inkC semiP semiC dark meanRGB) |dv|<=0.06m:")
    z = geom["z0"] + 16
    while z >= geom["z1"] - 16:
        sel = sel_dv & (np.abs(du - z) <= 2.0)
        ink = sel & (a > 16)
        if ink.any():
            m3 = arr[:, :, :3][ink].mean(axis=0)
            print("  du %+5.0f: inkP %5d inkC %5d semiP %4d semiC %4d dark %4d rgb %3.0f,%3.0f,%3.0f"
                  % (z, int((sel & P).sum()), int((sel & C).sum()),
                     int((sel & P & (ap < 240)).sum()), int((sel & C & (ac < 240)).sum()),
                     int((ink & (lum < 64)).sum()), m3[0], m3[1], m3[2]))
        z -= 16
    print("column ranges (dv px: P-range | C-range):")
    for dvc in (-200, -160, -120, -80, -40, 0, 40, 80, 120, 160, 200):
        sel = np.abs(dv - dvc) <= 1.5
        pr = du[sel & P]
        cr = du[sel & C]
        prs = "[%+.0f..%+.0f]" % (pr.min(), pr.max()) if pr.size else "[]"
        crs = "[%+.0f..%+.0f]" % (cr.min(), cr.max()) if cr.size else "[]"
        print("  dv %+5.0f: P %-18s C %-18s" % (dvc, prs, crs))
    du_zone = geom["z0"] - geom["z1"]
    print("overlap zone: %.0f px = %.4f m" % (du_zone, du_zone / j.scale))


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--joint", default="knee,ankle")
    ap.add_argument("--deg", type=float, default=None)
    ap.add_argument("--mode", default="render", choices=["geom", "render"])
    ap.add_argument("--stitch", default="off", choices=["off", "ramp"])
    ap.add_argument("--blend", default="zone", choices=["zone", "tight"])
    ap.add_argument("--stitch-hi", type=float, default=60.0)
    ap.add_argument("--stitch-lo", type=float, default=-96.0)
    ap.add_argument("--tag", default="")
    ap.add_argument("--scale", type=float, default=SCALE)
    ap.add_argument("--half", type=float, default=HALF)
    args = ap.parse_args(argv[1:])
    degs = {"knee": 65.0, "ankle": 40.0}
    for name in args.joint.split(","):
        name = name.strip()
        deg = args.deg if args.deg is not None else degs.get(name, 60.0)
        run_joint(name, deg, args.stitch, args.mode, args.scale, args.half, args.blend,
                  args.stitch_hi, args.stitch_lo, args.tag)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
