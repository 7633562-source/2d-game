#!/usr/bin/env python3
"""ART-0 eleventh pass: measure the campfire core's crown, and show it in the fire.

Offline (PIL + numpy, no Unity). Two questions, one script:

  1. SILHOUETTE. Does the shipped `Art/Campfire/core.png` have a crown (several
     licks with valleys between them) or one dome? The metric is taken on the
     bytes the engine loads: the top edge is the highest row per column whose
     alpha passes 0.5, and the crown is that profile's peaks and valleys.
  2. THE FIRE. The A/B the eye reads: the WHOLE assembly at one moment and one
     camera scale - LEFT the previous core, RIGHT the new one - rendered through
     the generator's own `render_fire`, so the tongue pool, the ages, the
     flicker, the logs and the tint ramp are identical in both cells and the
     only difference is the core picture. Written at 300 px/m (the documented
     gameplay scale), 220 px/m (2x on a 1080p still) and 1:1 on the canvas.

Usage (from the project root):

    python Tools/gen-fire-textures.py                    # writes the new core
    python Tools/art0-flamecore-probe.py <old_core.png>  # measures + A/B sheets
"""

import importlib.util
import os
import subprocess
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GEN = os.path.join(ROOT, "Tools", "gen-fire-textures.py")
SHIPPED = os.path.join(ROOT, "Assets", "Resources", "Art", "Campfire", "core.png")
OUT_DIR = os.path.join(ROOT, "Trials", "Runs", "art-flamecore-2026-09-19")
NIGHT_BG = (26, 30, 44)
ALPHA_CUT = 0.5


def load_generator():
    spec = importlib.util.spec_from_file_location("gen_fire_textures", GEN)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def top_profile(img, cut=ALPHA_CUT):
    """Highest row per column whose alpha passes `cut`, in px above the root."""
    a = np.asarray(img.convert("RGBA"), dtype=np.float64)[:, :, 3] / 255.0
    h, w = a.shape
    prof = np.zeros(w, dtype=np.float64)
    for x in range(w):
        rows = np.nonzero(a[:, x] >= cut)[0]
        if rows.size:
            prof[x] = h - rows.min()
    return prof, a


def crown_metrics(img):
    prof, a = top_profile(img)
    live = prof > 0
    if not live.any():
        return dict(peaks=0, depth=0, span=0, asym=0.0, coverage=0.0, top_row=0)
    # smooth over 5 columns so a 1 px wobble is not counted as a lick
    k = np.ones(5) / 5.0
    sm = np.convolve(prof, k, mode="same")
    sm[~live] = 0.0
    top = sm.max()
    # peaks: a column taller than both sides inside a 5 px window
    peaks = []
    for x in range(3, len(sm) - 3):
        if sm[x] <= 0.55 * top:
            continue
        win = sm[x - 3:x + 4]
        if sm[x] >= win.max() and sm[x] > sm[x - 3] and sm[x] > sm[x + 3]:
            if not peaks or x - peaks[-1] > 4:
                peaks.append(x)
    depth = 0.0
    for i in range(len(peaks) - 1):
        val = sm[peaks[i]:peaks[i + 1] + 1].min()
        depth = max(depth, min(sm[peaks[i]], sm[peaks[i + 1]]) - val)
    third = len(sm) // 3
    l = sm[:third][sm[:third] > 0]
    r = sm[-third:][sm[-third:] > 0]
    asym = (l.max() - r.max()) if (l.size and r.size) else 0.0
    # orientation: is the drawn flame wider near the BOTTOM of the canvas (an
    # upright flame) or near the top (an upside-down drop)?
    a2 = (np.asarray(img.convert("RGBA"), dtype=np.float64)[:, :, 3] / 255.0) >= ALPHA_CUT
    rows = a2.sum(axis=1)
    live_rows = np.nonzero(rows)[0]
    r0, r1 = live_rows.min(), live_rows.max()
    n = max(1, (r1 - r0 + 1) // 3)
    w_top = rows[r0:r0 + n].mean()
    w_bot = rows[r1 - n + 1:r1 + 1].mean()
    # a silhouette that reaches the canvas border is cut off: its edge is the
    # canvas edge, a straight wall where the eye expects the flame's own outline.
    edge_rows = 0
    cols = np.nonzero(a2.any(axis=0))[0]
    if cols.size:
        left, right = cols.min(), cols.max()
        for y in np.nonzero(rows)[0]:
            line = np.nonzero(a2[y])[0]
            if line.size and (line.min() == left == 0 or line.max() == right == img.width - 1):
                edge_rows += 1
    return dict(peaks=len(peaks), depth=float(depth), span=float(top),
                asym=float(asym), coverage=float(a.mean()),
                w_top=float(w_top), w_bot=float(w_bot), edge_rows=int(edge_rows))


def sheet(label, tiles, pxm_list):
    for pxm in pxm_list:
        row = tiles[pxm]
        w = sum(t.width for t in row) + 16 * (len(row) - 1)
        h = max(t.height for t in row)
        out = Image.new("RGBA", (w, h), NIGHT_BG + (255,))
        x = 0
        for t in row:
            out.alpha_composite(t, (x, 0))
            x += t.width + 16
        p = os.path.join(OUT_DIR, "%s_%dpxm.png" % (label, int(pxm)))
        out.convert("RGB").save(p)
        print("  wrote %s" % os.path.relpath(p, ROOT))


def print_profile(name, img):
    prof, a = top_profile(img)
    print("  %-4s top edge per column (px above the root, 0 = no coverage):" % name)
    print("       " + " ".join("%3d" % v for v in prof))
    pcx = a[:, img.width // 2]
    rows = [int(round(100 * y / img.height)) for y in range(img.height)]
    tails = [(y, pcx[y]) for y in range(img.height) if pcx[y] >= 0.5]
    root_row = tails[-1][0] if tails else -1
    print("       centre column: the LOWEST row with alpha>=0.5 is row %d of %d, i.e. %d px above the"
          % (root_row, img.height, (img.height - 1 - root_row) if root_row >= 0 else 0))
    print("         bottom edge (the root fade: the body dissolves into the bed over this band),"
          " alpha there %.2f" % (pcx[root_row] if root_row >= 0 else 0.0))
    prof2 = np.convolve(prof, np.ones(5) / 5.0, mode="same")
    live = prof > 0
    prof2[~live] = 0.0
    print("       crown columns (>=55%% of the top): %d of %d, their heights min/mean/max %.0f / %.0f / %.0f"
          % (int((prof2 > 0.55 * prof2.max()).sum()), img.width,
             prof2[prof2 > 0.55 * prof2.max()].min(), prof2[prof2 > 0.55 * prof2.max()].mean(),
             prof2.max()))


def frame_core_profile(path, out_dir, region=None):
    """The core in a REAL frame: its own width per row, and a magnified crop.

    The core is the white-hot element (CORE_COLOR 255/240/184 at alpha 0.82+ over
    the night background), so it is separated from the warm tongues by its blue
    channel: a tongue pixel has B far below 184, a core pixel keeps B high. The
    width per row then says which end of the drawn core is the body and which is
    the point - the question the canvas can only answer for the canvas.

    `region` is "x0,y0,x1,y1" in frame pixels; without it the window is built
    around the fire's warm tongues (a whole-page search also catches the white
    label text, and the gallery's fire row draws the core alone, with no tongues,
    so the region is the reliable way to look at that row).
    """
    img = Image.open(path).convert("RGB")
    a = np.asarray(img).astype(np.int32)
    if region:
        win = tuple(int(v) for v in region.split(","))
        print("  %s: fire window x %d..%d y %d..%d (given)"
              % (os.path.basename(path), win[0], win[2], win[1], win[3]))
    else:
        r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
        warm = (r > 185) & (b < 95) & (g > 55)
        wy, wx = np.nonzero(warm)
        if wx.size == 0:
            print("  no warm fire pixels in %s" % os.path.basename(path))
            return
        win = (max(0, wx.min() - 24), max(0, wy.min() - 180),
               min(img.width, wx.max() + 25), min(img.height, wy.max() + 30))
        print("  %s: fire window x %d..%d y %d..%d (around the warm tongues)"
              % (os.path.basename(path), win[0], win[2], win[1], win[3]))
    sub = a[win[1]:win[3], win[0]:win[2]]
    r, g, b = sub[:, :, 0], sub[:, :, 1], sub[:, :, 2]
    core = (r > 225) & (g > 205) & (b > 140)
    ys, xs = np.nonzero(core)
    if xs.size == 0:
        print("  no core-coloured pixels in the fire window of %s" % os.path.basename(path))
        return
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    rows = []
    for y in range(y0, y1 + 1):
        cols = np.nonzero(core[y])[0]
        rows.append(cols.size)
    third = max(1, len(rows) // 3)
    top = sum(rows[:third]) / third
    bot = sum(rows[-third:]) / third
    print("  core in frame: local box x %d..%d y %d..%d (%dx%d px, frame x %d y %d),"
          % (x0, x1, y0, y1, x1 - x0 + 1, y1 - y0 + 1, win[0] + x0, win[1] + y0))
    print("    mean width top third %.1f px, bottom third %.1f px" % (top, bot))
    print("    width per row (top row first): " + " ".join(str(v) for v in rows))
    crop = img.crop(win)
    big = crop.resize((crop.width * 6, crop.height * 6), Image.NEAREST)
    p = os.path.join(out_dir, os.path.splitext(os.path.basename(path))[0] + "_6x.png")
    big.save(p)
    print("    magnified 6x -> %s" % os.path.relpath(p, ROOT))


def main():
    old_path = sys.argv[1] if len(sys.argv) > 1 else None
    frame_path = sys.argv[2] if len(sys.argv) > 2 else None
    os.makedirs(OUT_DIR, exist_ok=True)
    mod = load_generator()

    if frame_path:
        frame_core_profile(frame_path, OUT_DIR, sys.argv[3] if len(sys.argv) > 3 else None)
        return

    new_img = mod.paint("core")
    old_img = Image.open(old_path).convert("RGBA") if old_path else None

    print("crown metrics (top edge at alpha >= %.1f, canvas %dx%d)" % (ALPHA_CUT, new_img.width, new_img.height))
    for name, img in (("old", old_img), ("new", new_img)):
        if img is None:
            continue
        m = crown_metrics(img)
        print("  %-4s peaks=%-2d valley=%.1f px  crown span=%.1f px  L-R shoulder=%+.1f px  coverage=%.1f %%"
              % (name, m["peaks"], m["depth"], m["span"], m["asym"], 100.0 * m["coverage"]))
        print("       %-4s silhouette rows: top third %.1f px wide, bottom third %.1f px wide (%s); rows cut by the canvas border: %d"
              % (name, m["w_top"], m["w_bot"],
                 "upright" if m["w_bot"] > m["w_top"] * 1.15 else
                 ("UPSIDE DOWN" if m["w_top"] > m["w_bot"] * 1.15 else "spindle"),
                 m["edge_rows"]))
        print_profile(name, img)

    # the assembly, both cores, one moment, one camera
    pool = {k: mod.paint(k) for k in mod.BOXES}
    pool_old = dict(pool)
    if old_img is not None:
        pool_old["core"] = old_img
    t0 = 12.0
    tiles = {}
    for pxm in (300.0, 220.0):
        left = mod.render_fire(t0, {"core"}, pool_old, pxm)
        right = mod.render_fire(t0, {"core"}, pool, pxm)
        tiles[pxm] = [left, right]
    sheet("core_ab", tiles, (300.0, 220.0))

    # 1:1 native side by side, 4x
    if old_img is not None:
        scale = 4
        wa, ha = old_img.width * scale, old_img.height * scale
        out = Image.new("RGBA", (wa * 2 + 24, ha), NIGHT_BG + (255,))
        for i, img in enumerate((old_img, new_img)):
            out.alpha_composite(img.resize((wa, ha), Image.NEAREST), (i * (wa + 24), 0))
        p = os.path.join(OUT_DIR, "core_ab_4x.png")
        out.convert("RGB").save(p)
        print("  wrote %s" % os.path.relpath(p, ROOT))
    print("evidence -> %s" % os.path.relpath(OUT_DIR, ROOT))


if __name__ == "__main__":
    main()
