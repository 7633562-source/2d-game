#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f - THE FORESHORTENING'S COVERAGE, offline, from the shipped canvases.

The round's question: when the THIGH's / the UPPER ARM's visual is scaled along its own
bone axis by (1-v), does the joint's disc stay painted at every angle of the joint's own
range - or does background show between the pictures?

The model is ART-H1's own composer (Tools/art-h1-pose-frames.py: the canvases as shipped,
the rig read out of Assets/Human.cs, the dress rule "sprite pivot on the collider box
centre"): each layer is placed the way HumanArt.cs draws it, the foreshortened layer is
scaled along the bone axis exactly as the game's dress will scale it (size.y * (1-v)) with
one of the anchor shifts below, the neighbour layer is left alone (--grow adds the fix),
and the frame is measured the way the eye measures it - transparent pixels inside the
joint's disc (radius = the narrower neighbour's ink half-width where the two meet).

Anchor shifts (s: the drawn position of an image point Y is k*Y + s):
  centre  s = 0                    the sprite scales about its pivot (the box centre):
                                   both painted ends pull in
  joint   s = (1-k)*A.y            the box point AT THE JOINT keeps its drawn place
                                   (A = the foreshortened segment's own anchor there)
  inkTop  s = (1-k)*ink_top        the paint's proximal edge keeps its place
  inkBot  s = (1-k)*ink_bot        the paint's distal edge keeps its place

--grow joint=m: stretch the NEIGHBOUR canvas along its axis so the ink edge facing the
joint moves toward it by m metres (the distal/away edge held) - the task's "grow the
neighbour's own visual by the missing share".

Usage:
  python Tools/arth5f-coverage.py --joint knee,elbow,shoulder,hip --v 0,0.2,0.4
  python Tools/arth5f-coverage.py --joint knee --v 0.4 --anchor centre --grow knee=0.0324
"""
import argparse
import importlib.util
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(PROJECT, "Trials", "Runs", "arth5f-foreshorten")
SCALE = 1000.0     # px per metre in the model frames
BG = (140, 199, 240)
SHRUNK = ("thigh", "upper")     # the segments the flag shortens


def frames_tool():
    path = os.path.join(PROJECT, "Tools", "art-h1-pose-frames.py")
    spec = importlib.util.spec_from_file_location("art_h1_pose_frames", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


F = frames_tool()


def ink_extent(canvas):
    """The ink's top / bottom in box metres (pivot-local, y up)."""
    a = np.array(canvas["im"])[:, :, 3] > 16
    rows = np.where(a.any(axis=1))[0]
    h, ppu = canvas["px"][1], canvas["ppu"]
    py = canvas["pivot"][1]
    top = ((1.0 - py) * h - rows.min()) / ppu
    bot = ((1.0 - py) * h - (rows.max() + 1)) / ppu
    return top, bot


def place_scaled(canvas, anchor, joint_px, theta_deg, scale, size, k=1.0, shift=0.0):
    """One canvas with its bone axis scaled by k and shifted by `shift` metres (box frame).

    The drawn position of an image point Y is (k*Y + shift); the joint pixel must be
    painted by the image point Y' with k*Y' + shift = anchor_y, i.e. Y' = (anchor_y -
    shift)/k, whose source row is (1-pivot.y)*H' - Y'*scale*k (the resized image carries
    px-per-metre scale*k along y and scale along x).
    """
    w, h = canvas["px"]
    ppu = canvas["ppu"]
    f = scale / ppu
    im = canvas["im"]
    if abs(f - 1.0) > 1e-6 or abs(k - 1.0) > 1e-6:
        im = im.resize((max(1, int(round(w * f))), max(1, int(round(h * f * k)))),
                       Image.LANCZOS)
    ax = canvas["pivot"][0] * w * f + anchor[0] * scale
    ay = (1.0 - canvas["pivot"][1]) * h * f * k - (anchor[1] - shift) * scale
    th = np.radians(theta_deg)
    ct, st = np.cos(th), np.sin(th)
    jx, jy = joint_px
    a = ct
    b = -st
    c = ax + (0.5 - jx) * ct + (jy - 0.5) * st
    d = st
    e = ct
    g = ay + (0.5 - jx) * st + (0.5 - jy) * ct
    return im.transform(size, Image.AFFINE, (a, b, c, d, e, g), resample=Image.BILINEAR)


def compose(layers, half, scale):
    px = max(64, int(round(2 * half * scale)))
    frame = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    joint = (px / 2.0, px / 2.0)
    for canvas, anchor, theta, k, shift in layers:
        frame.alpha_composite(place_scaled(canvas, anchor, joint, theta, scale,
                                           (px, px), k, shift))
    return frame


def hole_px(frame, radius_m, scale):
    alpha = np.array(frame)[:, :, 3] > 16
    h, w = alpha.shape
    yy, xx = np.mgrid[0:h, 0:w]
    r = radius_m * scale
    cx, cy = w / 2.0, h / 2.0
    disc = ((xx + 0.5 - cx) ** 2 + (yy + 0.5 - cy) ** 2) <= r * r
    return int((disc & ~alpha).sum()), int(disc.sum())


def edge_facing(joint_is_proximal):
    """Which ink edge of the neighbour faces the joint (+1 = its top/proximal)."""
    return +1 if joint_is_proximal else -1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--joint", default="knee,elbow,shoulder,hip")
    ap.add_argument("--v", default="0,0.2,0.4")
    ap.add_argument("--anchor", default="centre")
    ap.add_argument("--steps", type=int, default=7)
    ap.add_argument("--deg", default="",
                    help="override the sampled degrees (e.g. the walk's own extremes)")
    ap.add_argument("--scale", type=float, default=SCALE)
    ap.add_argument("--radius", type=float, default=None,
                    help="override the disc radius in metres (a wider look than R_eye)")
    ap.add_argument("--grow", action="append", default=[],
                    help="joint=metres: stretch the neighbour's canvas so its ink edge "
                         "facing the joint moves toward it by metres")
    ap.add_argument("--strip", action="store_true", help="write the joint strips")
    ap.add_argument("--mark", action="store_true",
                    help="draw the disc outline + the hole in red on the saved frames")
    args = ap.parse_args()

    grows = dict(item.split("=") for item in (it.split("=", 1) for it in []))
    grows = {}
    for item in args.grow:
        jn, ms = item.split("=")
        grows[jn] = float(ms)

    ns, joints = F.read_rig()
    canvases = {k: F.load_canvas(v) for k, v in F.CANVAS.items()}
    canvases = {k: v for k, v in canvases.items() if v is not None}
    os.makedirs(OUT, exist_ok=True)
    rows = []
    frames = {}

    for name, parent_key, child_key, child_var, parent_var, half in F.JOINTS:
        if name not in args.joint.split(","):
            continue
        spec = joints.get(child_var)
        if spec is None or spec["parent"] != parent_var:
            continue
        if parent_key in SHRUNK and child_key in SHRUNK:
            continue
        if parent_key in SHRUNK:
            shrunk_side, shortened = "parent", parent_key
        elif child_key in SHRUNK:
            shrunk_side, shortened = "child", child_key
        else:
            continue
        anchor_child = spec["anchor"]
        anchor_parent = spec["anchor_parent"]
        lo, hi = spec["limits"]
        pc, cc = canvases[parent_key], canvases[child_key]
        half_p = F.ink_half(pc, anchor_parent)
        half_c = F.ink_half(cc, anchor_child)
        r_eye = min(half_p, half_c)
        p_top, p_bot = ink_extent(pc)
        c_top, c_bot = ink_extent(cc)
        shrunk_c = pc if shrunk_side == "parent" else cc
        s_top, s_bot = (p_top, p_bot) if shrunk_side == "parent" else (c_top, c_bot)
        s_anchor = anchor_parent if shrunk_side == "parent" else anchor_child

        # neighbour grow: its ink edge facing the joint moves toward it by `grow` metres
        g = grows.get(name, 0.0)
        n_key = child_key if shrunk_side == "parent" else parent_key
        n_k, n_shift = 1.0, 0.0
        if g > 0.0:
            n_top, n_bot = (c_top, c_bot) if shrunk_side == "parent" else (p_top, p_bot)
            joint_anchor = anchor_child if shrunk_side == "parent" else anchor_parent
            span = n_top - n_bot
            # the edge facing the joint: the joint sits at joint_anchor; the neighbour's
            # ink edge on that side is its top if the joint anchor y > 0 (the child's
            # top), else its bottom (the parent's bottom)
            if joint_anchor[1] > 0.0:
                n_k = (span + g) / span
                n_shift = n_bot - n_bot * n_k            # hold the far (bottom) edge
            else:
                n_k = (span + g) / span
                n_shift = n_top - n_top * n_k            # hold the far (top) edge

        # the segment's own box height (the rig's own numbers) for the end-keeping shifts
        box_var = {"thigh": "thighSize", "upper": "upperArmSize"}[shortened]
        box_h = ns[box_var].y

        for v in [float(x) for x in args.v.split(",")]:
            k = 1.0 - v
            for anchor_name in args.anchor.split(","):
                if anchor_name == "centre":
                    s = 0.0
                elif anchor_name == "joint":
                    s = (1.0 - k) * s_anchor[1]
                elif anchor_name == "down":          # hold the DISTAL box end (the knee /
                    s = -(1.0 - k) * box_h * 0.5    # the elbow) - the thigh/upper's far end
                elif anchor_name == "up":            # hold the PROXIMAL box end (the hip /
                    s = +(1.0 - k) * box_h * 0.5    # the shoulder)
                elif anchor_name == "inkBot":
                    s = (1.0 - k) * s_bot
                elif anchor_name == "inkTop":
                    s = (1.0 - k) * s_top
                elif anchor_name.startswith("s"):
                    s = float(anchor_name[1:])      # explicit shift in metres (s0.077)
                else:
                    raise SystemExit("anchor " + anchor_name)
                if shrunk_side == "parent":
                    base = [(pc, anchor_parent, 0.0, k, s), (cc, anchor_child, 0.0, n_k, n_shift)]
                else:
                    base = [(pc, anchor_parent, 0.0, n_k, n_shift), (cc, anchor_child, 0.0, k, s)]
                deg_list = ([float(x) for x in args.deg.split(",")] if args.deg
                            else [lo + (hi - lo) * (i / float(max(1, args.steps - 1)))
                                  for i in range(args.steps)])
                for deg in deg_list:
                    layers = []
                    for (c, a, _t, kk, ss) in base:
                        # the joint's own child canvas turns through the range
                        theta = deg if (c is cc and child_var not in ("",)) else 0.0
                        layers.append((c, a, theta, kk, ss))
                    frame = compose(layers, half, args.scale)
                    rr = args.radius if args.radius else r_eye
                    hole, total = hole_px(frame, rr, args.scale)
                    rows.append((name, v, anchor_name, deg, r_eye, hole, total, g))
                    frames[(name, round(v, 3), anchor_name, round(deg, 3))] = (frame, hole)
                    if args.mark and hole > 0:
                        im = frame.copy()
                        dr = ImageDraw.Draw(im)
                        c_px = im.width / 2.0
                        r_px = r_eye * args.scale
                        dr.ellipse((c_px - r_px, c_px - r_px, c_px + r_px, c_px + r_px),
                                   outline=(255, 0, 0, 255))
                        im.save(os.path.join(OUT, "%s-v%.2f-%s%+.0f-hole%d.png"
                                             % (name, v, anchor_name, deg, hole)))
        break_after = None

    print("%-9s %5s %-7s %8s %9s %8s %8s %6s" %
          ("joint", "v", "anchor", "angle", "R_eye m", "hole", "disc", "grow"))
    for r in rows:
        print("%-9s %5.2f %-7s %+8.1f %9.4f %8d %8d %6.4f" % r)

    if args.strip:
        by_joint = {}
        for (name, v, an, deg), (frame, hole) in frames.items():
            by_joint.setdefault(name, []).append((v, an, deg, frame, hole))
        for name, items in by_joint.items():
            vs = sorted({it[0] for it in items})
            ans = sorted({it[1] for it in items})
            degs = sorted({it[2] for it in items})
            cell = next(iter(items))[3].width
            strip = Image.new("RGBA", (cell * len(vs) * len(degs) + 8,
                                       cell * len(ans) + 22), BG + (255,))
            dr = ImageDraw.Draw(strip)
            for r_i, an in enumerate(ans):
                for v_i, v in enumerate(vs):
                    for d_i, deg in enumerate(degs):
                        key = (name, round(v, 3), an, round(deg, 3))
                        if key in frames:
                            strip.alpha_composite(frames[key][0],
                                                  (((v_i * len(degs)) + d_i) * cell,
                                                   r_i * cell + 22))
                        dr.text(((v_i * len(degs) + d_i) * cell + 4, r_i * cell + 6),
                                "%s v=%.2f %+.0fdeg hole %d" % (an, v, deg, frames[key][1]),
                                fill=(10, 10, 10))
            path = os.path.join(OUT, "%s-strip.png" % name)
            dr.text((4, 4), name, fill=(10, 10, 10))
            strip.convert("RGB").save(path)
            print("strip", path)

    print("out", OUT)


if __name__ == "__main__":
    main()
