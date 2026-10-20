#!/usr/bin/env python3
"""ART-H1 - THE JOINT OVERLAP ON FRAMES, drawn from the SHIPPED canvases (offline).

Yury's sheet: at every joint, at every angle, no gap between the two neighbouring segments'
textures; the shin-foot joint is the worked example (three drawings of the foot at different
angles; the third is the correct overlap, "не должно быть разрывов").

This tool draws exactly that from the real files: it reads the canvases under
Assets/Resources/Art/Human with their .meta pivots, reads the rig from Assets/Human.cs (the
box sizes, the ConnectTo anchors, the hinge limits, the sorting orders - nothing is typed in
here twice), places every canvas the way HumanArt.cs dresses it in game (sprite pivot on the
collider box centre, draw mode Sliced, size = the canvas in metres), turns each child about
its own joint anchor through that joint's OWN limit range, and then measures the frame: the
number of transparent pixels inside the joint's disc (radius = the narrower neighbour's ink
half-width at the joint, the model's R_eye). 0 is the sheet's property.

Why offline: the Unity side of the same evidence is Assets/Editor/ArtHumanProbe.cs, which
renders the joints in scene N1 "Level 1" with the game's own camera - run it when the editor
is not holding Temp/UnityLockfile.

Output: Trials/Runs/frames-arth1-offline/<joint>/<joint>-<deg>.png (the joint close-up),
<...>/<joint>-ladder.png (the sheet's row of drawings), body-rest.png, body-step.png, and
Trials/Runs/art-h1-offline-frames.md (the table the report quotes).
"""
import argparse
import json
import math
import os
import re

import numpy as np
from PIL import Image, ImageDraw

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "Resources", "Art", "Human")
HUMAN_CS = os.path.join(PROJECT, "Assets", "Human.cs")
OUT = os.path.join(PROJECT, "Trials", "Runs", "frames-arth1-offline")
SCALE = 1000.0                    # px per metre in the frames
STEPS = 5                         # drawings per joint, the sheet's row

# joint, parent canvas, child canvas, the rig variables that ConnectTo joins, frame half-size
JOINTS = [
    ("lumbar",   "pelvis", "torso", "torso", "pelvis", 0.30),
    ("neck",     "torso",  "neck",  "neck",  "torso",  0.20),
    ("head",     "neck",   "head",  "head",  "neck",   0.22),
    ("shoulder", "torso",  "upper", "upper", "torso",  0.30),
    ("elbow",    "upper",  "lower", "lower", "upper",  0.24),
    ("wrist",    "lower",  "hand",  "hand",  "lower",  0.16),
    ("hip",      "pelvis", "thigh", "thigh", "pelvis", 0.34),
    ("knee",     "thigh",  "shin",  "shin",  "thigh",  0.30),
    ("ankle",    "shin",   "heel",  "heel",  "shin",   0.22),
    ("toe",      "heel",   "toe",   "toe",   "heel",   0.15),
]

# rig variable -> canvas file (Human.cs splits the foot into heel + toe; the foot canvas
# covers both boxes, its pivot is the heel box centre - see Tools/gen-human-textures.py).
CANVAS = {
    "pelvis": "pelvis", "torso": "torso", "neck": "neck", "head": "head",
    "upper": "upper", "lower": "lower", "hand": "hand",
    "thigh": "thigh", "shin": "shin", "heel": "foot", "toe": "toe",
}


class Vec(object):
    def __init__(self, x, y):
        self.x = x
        self.y = y


def read_rig():
    """Everything the frames need, out of Human.cs itself."""
    with open(HUMAN_CS, encoding="utf-8", errors="replace") as fh:
        text = fh.read()
    ns = {}
    for m in re.finditer(
            r"public Vector2 (\w+Size)\s*=\s*new Vector2\(\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*\)", text):
        ns[m.group(1)] = Vec(float(m.group(2)), float(m.group(3)))
    for m in re.finditer(r"public float (\w+)\s*=\s*(-?[0-9.]+)f", text):
        ns[m.group(1)] = float(m.group(2))
    for m in re.finditer(r"(?:private|public) const int (\w+)\s*=\s*(-?[0-9]+)", text):
        ns[m.group(1)] = int(m.group(2))
    for m in re.finditer(r"float (\w+)\s*=\s*(-?[0-9.]+)f?\s*;", text):
        ns.setdefault(m.group(1), float(m.group(2)))     # locals like heelShare = 0.70f
    # Human.cs: float heelShare = Mathf.Clamp(footHeelShare, 0.4f, 0.85f);
    ns["heelShare"] = ns.get("footHeelShare", 0.70)
    ns["LateralProjection"] = math.cos(ns["viewAngleDegrees"] * math.pi / 180.0)
    ns["heelHalf"] = ns["footSize"].x * ns["heelShare"] * 0.5
    ns["toeHalf"] = ns["footSize"].x * (1.0 - ns["heelShare"]) * 0.5
    ns["shoulderX"] = ns["shoulderHalfSpacing"] * ns["LateralProjection"]
    ns["shoulderY"] = ns["torsoSize"].y * 0.5 - ns["shoulderDropFromNeck"]
    ns["hipX"] = ns["hipHalfSpacing"] * ns["LateralProjection"]
    ns["hipY"] = -ns["pelvisSize"].y * 0.5
    ns["ankleAnchorX"] = ns["ankleHeelOffset"] - ns["heelHalf"]
    ns["heelCenterX"] = ns["hipX"] - ns["ankleHeelOffset"] + ns["heelHalf"]
    ns["ankleAnchorX"] = ns["hipX"] - ns["heelCenterX"]

    joints = {}
    pat = (r"(\w+)\.ConnectTo\((\w+),\s*new Vector2\(([^,)]+),\s*([^)]+)\),\s*"
           r"new Vector2\(([^,)]+),\s*([^)]+)\),\s*([-0-9a-zA-Z.]+),\s*([-0-9a-zA-Z.]+)\)")
    for m in re.finditer(pat, text):
        child, parent = m.group(1), m.group(2)
        joints[child] = {
            "parent": parent,
            "anchor": (num(m.group(3), ns), num(m.group(4), ns)),          # on the child
            "anchor_parent": (num(m.group(5), ns), num(m.group(6), ns)),   # on the parent
            "limits": [num(m.group(7), ns), num(m.group(8), ns)],
        }
    return ns, joints


def num(expr, ns):
    clean = re.sub(r"(\d)f\b", r"\1", expr).strip()
    return float(eval(clean, {"__builtins__": {}}, ns))


def load_canvas(key):
    path = os.path.join(ART, key + ".png")
    if not os.path.exists(path + ".meta"):
        return None            # a canvas the generator has not produced yet (the toe)
    meta = open(path + ".meta", encoding="utf-8").read()
    ppu = float(re.search(r"spritePixelsToUnits: ([0-9.]+)", meta).group(1))
    piv = re.search(r"spritePivot: \{x: ([0-9.]+), y: ([0-9.]+)\}", meta)
    if piv is None:
        piv = re.match(r".*", "spritePivot: {x: 0.5, y: 0.5}")   # pristine: centred
    im = Image.open(path).convert("RGBA")
    return {
        "key": key,
        "im": im,
        "ppu": ppu,
        "pivot": (float(piv.group(1)), float(piv.group(2))),
        "px": (im.width, im.height),
        "world": (im.width / ppu, im.height / ppu),
    }


def place(canvas, anchor, joint_px, theta_deg, scale, size):
    """One canvas onto the frame: its box anchor lands on the joint, then it turns.

    The frame is in pixels with y down; the world is y up, so a positive (Unity Z) rotation
    turns the picture counter-clockwise on screen. PIL's AFFINE maps each OUTPUT pixel to the
    source pixel, so the matrix below is the inverse of the placement.
    """
    w, h = canvas["px"]
    ppu = canvas["ppu"]
    f = scale / ppu
    im = canvas["im"]
    if abs(f - 1.0) > 1e-6:
        im = im.resize((max(1, int(round(w * f))), max(1, int(round(h * f)))), Image.LANCZOS)
    ax = canvas["pivot"][0] * w * f + anchor[0] * scale
    # the pivot's y is measured from the BOTTOM in Unity, the pixel rows from the top
    ay = (1.0 - canvas["pivot"][1]) * h * f - anchor[1] * scale
    th = math.radians(theta_deg)
    ct, st = math.cos(th), math.sin(th)
    jx, jy = joint_px
    a = ct
    b = -st
    c = ax + (0.5 - jx) * ct + (jy - 0.5) * st
    d = st
    e = ct
    g = ay + (0.5 - jx) * st + (0.5 - jy) * ct
    return im.transform(size, Image.AFFINE, (a, b, c, d, e, g), resample=Image.BILINEAR)


def compose(canvases, layers, half, scale):
    px = max(64, int(round(2 * half * scale)))
    frame = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    joint = (px / 2.0, px / 2.0)
    for canvas_key, anchor, theta in layers:
        frame.alpha_composite(place(canvases[canvas_key], anchor, joint, theta, scale, (px, px)))
    return frame


def ink_half(canvas, anchor):
    """The painted strip's half-extent ACROSS the limb, in metres, at the joint.

    The joint sits at `anchor` in the box frame; the limb runs from the joint towards the box
    centre, so the across direction is perpendicular to that. The strip is taken within 25 px
    of the anchor, i.e. exactly where the two pictures have to meet.
    """
    w, h = canvas["px"]
    ppu = canvas["ppu"]
    a = np.array(canvas["im"])[:, :, 3] > 16
    ax_px = canvas["pivot"][0] * w + anchor[0] * ppu
    # the pivot's y is measured from the BOTTOM in Unity, the pixel rows from the top
    ay_px = (1.0 - canvas["pivot"][1]) * h - anchor[1] * ppu
    inward = np.array([-anchor[0], -anchor[1]], dtype=float)
    n = np.linalg.norm(inward)
    if n < 1e-9:
        return 0.0
    inward /= n
    across = np.array([inward[1], -inward[0]])
    strip = 25.0
    rows, cols = np.where(a)
    px = cols + 0.5 - ax_px
    py = ay_px - (rows + 0.5)
    along = px * inward[0] + py * inward[1]
    keep = (along > -strip) & (along < strip)
    if not keep.any():
        return 0.0
    across_vals = px[keep] * across[0] + py[keep] * across[1]
    return (across_vals.max() - across_vals.min()) * 0.5 / ppu


def measure(frame, radius_m, scale):
    alpha = np.array(frame)[:, :, 3] > 16
    h, w = alpha.shape
    yy, xx = np.mgrid[0:h, 0:w]
    r = radius_m * scale
    cx, cy = w / 2.0, h / 2.0
    disc = ((xx + 0.5 - cx) ** 2 + (yy + 0.5 - cy) ** 2) <= r * r
    total = int(disc.sum())
    hole = int((disc & ~alpha).sum())
    return hole, total


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--scale", type=float, default=SCALE)
    ap.add_argument("--steps", type=int, default=STEPS)
    ap.add_argument("--only", default=None)
    args = ap.parse_args()

    ns, joints = read_rig()
    canvases = {k: load_canvas(v) for k, v in CANVAS.items()}
    canvases = {k: v for k, v in canvases.items() if v is not None}
    os.makedirs(OUT, exist_ok=True)
    rows = []
    worst = (0, "")

    for name, parent_key, child_key, child_var, parent_var, half in JOINTS:
        if args.only and name not in args.only.split(","):
            continue
        spec = joints.get(child_var)
        if spec is None or spec["parent"] != parent_var:
            rows.append((name, -1.0, -1.0, 0, 0, "NO ConnectTo LINE"))
            continue
        if parent_key not in canvases or child_key not in canvases:
            rows.append((name, -1.0, -1.0, 0, 0, "CANVAS MISSING"))
            continue
        anchor_child = spec["anchor"]
        anchor_parent = spec["anchor_parent"]
        lo, hi = spec["limits"]

        # R_eye: the narrower neighbour's painted half-width where the two pictures meet
        half_p = ink_half(canvases[parent_key], anchor_parent)
        half_c = ink_half(canvases[child_key], anchor_child)
        r_eye = min(half_p, half_c)

        frames = []
        for i in range(args.steps):
            t = 0.0 if args.steps == 1 else i / float(args.steps - 1)
            deg = lo + (hi - lo) * t
            layers = [(parent_key, anchor_parent, 0.0), (child_key, anchor_child, deg)]
            frame = compose(canvases, layers, half, args.scale)
            hole, total = measure(frame, r_eye, args.scale)
            d = os.path.join(OUT, name)
            os.makedirs(d, exist_ok=True)
            path = os.path.join(d, "%s-%+04.0f.png" % (name, deg))
            frame.save(path)
            frames.append((deg, frame, hole, total))
            rows.append((name, deg, r_eye, hole, total, os.path.relpath(path, PROJECT)))
            if hole > worst[0]:
                worst = (hole, "%s %+.0f deg" % (name, deg))

        # the sheet's row of drawings, labelled
        cell = frames[0][1].width
        pad = 10
        ladder = Image.new("RGBA", (cell * len(frames) + pad * (len(frames) - 1), cell + 22),
                           (18, 18, 22, 255))
        draw = ImageDraw.Draw(ladder)
        for i, (deg, frame, hole, total) in enumerate(frames):
            ladder.alpha_composite(frame, (i * (cell + pad), 22))
            draw.text((i * (cell + pad) + 6, 5),
                      "%s %+.0f deg  hole %d px" % (name, deg, hole), fill=(230, 230, 120))
        ladder.save(os.path.join(OUT, name + "-ladder.png"))

    lap = os.path.join(OUT, "probe-report.txt")
    with open(lap, "w", encoding="utf-8") as fh:
        fh.write("ART-H1 offline joint frames (canvases as shipped, rig from Assets/Human.cs)\n")
        fh.write("%-9s %8s %9s %7s %7s %s\n" % ("joint", "angle", "R_eye m", "hole", "disc", "frame"))
        for r in rows:
            fh.write("%-9s %+8.1f %9.4f %7d %7d %s\n" % r)
        fh.write("worst %d px at %s\n" % (worst[0], worst[1]))
    print("%-9s %8s %9s %7s %7s" % ("joint", "angle", "R_eye m", "hole", "disc"))
    for r in rows:
        print("%-9s %+8.1f %9.4f %7d %7d" % (r[0], r[1], r[2], r[3], r[4]))
    print("worst %d px at %s" % worst)
    print("out", OUT)


if __name__ == "__main__":
    main()
