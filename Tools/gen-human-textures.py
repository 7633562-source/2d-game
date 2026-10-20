#!/usr/bin/env python3
"""ART-H1 - paint the mannequin's segment canvases WITH THE JOINT OVERLAP ACCOUNTED FOR.

Yury's sheet: the segments' textures are separate elements that must CONNECT in game,
and at a joint, at every angle, no gap may show between the two neighbours (his worked
example is the shin-foot joint; the arrow: "no gaps").

The plan comes from the model, `Tools/art-h1-joint-fit.py`, which measures the shipped
canvases and answers the closure question in millimetres of open background inside the
joint's own disc (its `--plan` JSON is this script's input, so the numbers painted here
are the numbers that were verified):

  * FILL - where a canvas' paint stops 4-7 cm short of its own box end (the gutter the
    inventory prints), the paint is continued out to the box edge. Whatever the joint,
    the segment's texture now reaches its own hinge.
  * SKIRT - the OVERLAP: on the narrower side of each joint the paint continues PAST the
    box edge by `m` metres and ends in a round tip. That tip is what closes the wedge:
    it slips into the wider neighbour's paint, the way the sheet draws the shin's end
    inside the boot. At rest it is hidden; turned, it fills the gap.

ART-H4 - "THE CIRCULAR OVERLAP SEAM".  The disc's circle the designer read at a bent joint
is the added paint's own contour: the skirt and the joint patch repeat the limb's tone at its
cut, which at the knee is the drawing's shaded end band (~145) while the segment across the
seam reads ~180 - so the contour painted a tone step and the eye saw a line.  All the ADDED
paint at a joint (the skirt past the box edge and the patch's own ring) is now blended toward
the tone ACROSS the seam - the neighbour canvas' paint just inside its own box edge, read from
the joint plan - with the weight peaking at the cut/rim (0.85) and easing away from it.  The
drawing's own pixels are untouched: the fill inside the box and the patch's pinned art keep
the ART-H3 rule.

ART-H4f - "THE JOINT ARC ENDS OPAQUE" (24.09, tried and REVERTED - the report's ART-H4f
section).  The last canvas-side lever the round could name: the alpha ramps ON THE JOINT ARC
(the disc rim's ~12 px fade, the dome tip's ~9.6 px feather) became a 2 px anti-aliased edge
- the added paint opaque up to its contour, the 50 % contour unmoved.  Measured: the exposed
overlap's translucent px fell 44 % (knee) / 52 % (ankle) and the frames stayed `worst 0 px`,
but the standing A/B pick preferred the FADED joint at the knee in BOTH panel orders (the
opaque edge read as "отдельный вырезанный круг") and the acceptance read stayed flat
(knee 3/3, look 2/3/3 vs 2/2/3), so the variant was REVERTED and this file keeps the ART-H3
fades - the lever is recorded as measured, not shipped.

The pixels are the canvas' own: the extension is the last 2 cm of the painted limb
mirrored (np.pad reflect), so the colour ramp continues, and only the tip is masked to a
half-disc. Nothing is repainted, moved or rescaled: the canvas keeps its
spritePixelsToUnits, so the art's own scale is untouched, and gains a pivot that marks
where the shipping collider box sits inside the longer canvas.

The canvases grow, so `maxTextureSize` is raised to fit them - and the dress reads the
drawn size from the imported sprite (px / ppu), never from a number in C#.

PIVOT ARITHMETIC (fixed this round). The dress puts the sprite's PIVOT on the segment's
Visual origin, which is the centre of the shipping collider box, so the pivot must be the
box' own centre inside the grown canvas. Two traps cost the previous round its canvases:

  * a grow along X sliced the array along Y (`body = a[first:]`), so the foot lost 100
    rows of its own art and its pivot came out at y = -0.1257;
  * the box' position inside the grown canvas was guessed in px (`npx - first`) instead of
    being carried through each grow, so a second grow on the same axis would move it.

`extend_canvas` now returns the offset the original array's origin lands at, the caller
sums those offsets, and the pivot is computed from that offset and the original size -
never from the growth amounts.

Run:  python Tools/gen-human-textures.py                       # the ten + the toe
      python Tools/gen-human-textures.py --dry-run             # measure, write nothing
      python Tools/gen-human-textures.py --only shin,thigh
"""
import argparse
import hashlib
import importlib.util
import json
import math
import os
import re
import sys

import numpy as np
from PIL import Image, ImageDraw

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "Resources", "Art", "Human")
HUMAN_CS = os.path.join(PROJECT, "Assets", "Human.cs")
# Human.cs: the heel is HEEL_SHARE of footSize.x, the toe the rest, and the two boxes abut
# at the ball; the foot canvas covers both, so its pivot is the HEEL box centre.
HEEL_SHARE = 0.70
PLAN = os.path.join(PROJECT, "Trials", "Runs", "art-h1-joint-plan.json")
SHEET = os.path.join(PROJECT, "Trials", "Runs", "art-h1-parts-overlap.png")

PART_FIELD = {
    "head": "headSize", "neck": "neckSize", "torso": "torsoSize",
    "pelvis": "pelvisSize", "upper": "upperArmSize", "lower": "lowerArmSize",
    "hand": "handSize", "thigh": "thighSize", "shin": "shinSize", "foot": "footSize",
}

# canvas edge -> (numpy axis, which side) of the ART's own ends
AXIS = {"top": (0, 0), "bottom": (0, 1), "back": (1, 0), "front": (1, 1)}

ALPHA_MIN = 16


def boxes():
    with open(HUMAN_CS, encoding="utf-8", errors="replace") as fh:
        text = fh.read()
    out = {}
    for m in re.finditer(
        r"public Vector2 (\w+Size)\s*=\s*new Vector2\(\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*\)",
        text,
    ):
        out[m.group(1)] = (float(m.group(2)), float(m.group(3)))
    return out


def read_meta(path):
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def meta_ppu(meta):
    m = re.search(r"^  spritePixelsToUnits: ([0-9.]+)$", meta, flags=re.M)
    return float(m.group(1))


def write_meta(path, meta, ppu, pivot, size):
    """Keep the identity (guid / spriteID), move the pivot, fit the texture cap."""
    cap = 32
    while cap < max(size):
        cap *= 2
    out = meta
    out = re.sub(r"^  maxTextureSize: \d+$", "  maxTextureSize: %d" % cap, out, count=1, flags=re.M)
    out = re.sub(r"^  alignment: \d+$", "  alignment: 9", out, count=1, flags=re.M)
    out = re.sub(r"^  spritePivot: \{x: [0-9.]+, y: [0-9.]+\}$",
                 "  spritePivot: {x: %.6f, y: %.6f}" % (pivot[0], pivot[1]),
                 out, count=1, flags=re.M)
    out = re.sub(r"^  spritePixelsToUnits: [0-9.]+$",
                 "  spritePixelsToUnits: %s" % ("%g" % ppu), out, count=1, flags=re.M)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(out)


def already_grown(meta):
    """True when this canvas already carries the ART-H1 pivot (alignment 9 = custom).

    The generator is NOT idempotent - every run would add the margins again. The pristine
    canvases come back with `python Tools/import-part-art.py --groups Human` (it re-pads
    the LegacyArt originals and re-writes the box-matched ppu), so that is the recipe to
    repeat before a re-run: import -> art-h1-joint-fit --plan -> gen-human-textures.
    """
    m = re.search(r"^  alignment: (\d+)$", meta, flags=re.M)
    return bool(m) and int(m.group(1)) == 9


def ink_rows_span(a):
    """(first, last) rows that carry ink, or (None, None)."""
    rows = np.where((a[:, :, 3] > ALPHA_MIN).any(axis=1))[0]
    if len(rows) == 0:
        return None, None
    return int(rows[0]), int(rows[-1])


def ink_cols_span(a):
    cols = np.where((a[:, :, 3] > ALPHA_MIN).any(axis=0))[0]
    if len(cols) == 0:
        return None, None
    return int(cols[0]), int(cols[-1])


def band_profile(a, axis, anchor, sign, strip):
    """Half-width (px) of the ink in the `strip` rows next to `anchor`, measured from the
    ink's own centre, plus the ink's centre offset (px) from the canvas' centre line."""
    idx = [anchor + sign * k for k in range(strip)]
    idx = [i for i in idx if 0 <= i < a.shape[axis]]
    lo = hi = None
    for i in idx:
        row = a[i, :, 3] if axis == 0 else a[:, i, 3]
        xs = np.where(row > ALPHA_MIN)[0]
        if len(xs) == 0:
            continue
        lo = xs[0] if lo is None else min(lo, xs[0])
        hi = xs[-1] if hi is None else max(hi, xs[-1])
    if lo is None:
        return 0.0, 0.0
    centre = (lo + hi) * 0.5
    return (hi - lo + 1) * 0.5, centre - a.shape[1 - axis] * 0.5


def ink_span(a, axis, min_px=5):
    """The painted body's first/last index along `axis`, ignoring hairline rows/columns.

    The legacy canvases carry a 1 px edge line (a few pixels on a row), which would make the
    transparent gutter look painted and stop the fill. A row/column counts as paint when it
    holds at least `min_px` pixels.
    """
    m = a[:, :, 3] > 16
    counts = m.sum(axis=1 if axis == 0 else 0)
    idx = np.where(counts >= min_px)[0]
    if len(idx) == 0:
        return None, None
    return int(idx[0]), int(idx[-1])


def canvas_tone_inside(part, end):
    """ART-H4: mean RGB of a canvas' ink just inside its own cut at `end`.

    The H3 mute sample (`soften_extension`), but taken on the NEIGHBOUR canvas: rows/cols
    8..22 px inside the ink's outermost line at that end, opaque pixels only, the middle
    70% of the ink's width (the drawn outline runs along the sides and would drag the mean
    dark). This is the tone the eye sees across the seam.
    """
    path = os.path.join(ART, part + ".png")
    if not os.path.exists(path):
        return None
    a = np.array(Image.open(path).convert("RGBA"))
    axis, side = AXIS[end]
    first, last = ink_span(a, axis)
    if first is None:
        return None
    edge = first if side == 0 else last
    # ART-H4: the neighbour's VISIBLE paint at the box edge is its own fill/skirt, and that
    # is a MIRROR of the art - so the tone at depth d beyond the cut is the art's own tone at
    # depth d inside it.  The cut sits `g` px short of the box edge (the transparent gutter
    # the growth fills), so the sample is taken at that depth: 8..22 px inside the cut for a
    # canvas whose art reaches its box edge, deeper for one with a gutter.
    g = edge if side == 0 else (a.shape[axis] - 1 - edge)
    g = min(g, max(0, (last - first + 1) - 30))
    ink = []
    for k in range(8 + g, 23 + g):
        i = edge + k if side == 0 else edge - k
        if i < 0 or i >= a.shape[axis]:
            continue
        line = a[i] if axis == 0 else a[:, i]
        sel = line[:, 3] > 240
        xs = np.where(sel)[0]
        if len(xs) < 6:
            continue
        lo = xs[0] + int(len(xs) * 0.15)
        hi = xs[-1] - int(len(xs) * 0.15)
        seg = line[lo:hi + 1]
        seg = seg[seg[:, 3] > 240][:, :3]
        if len(seg):
            ink.append(seg.astype(np.float64))
    if not ink:
        return None
    return np.concatenate(ink, axis=0).mean(axis=0)


def seam_tone(plan_joints, part, end):
    """ART-H4 - the tone ACROSS the seam, for the end `part`/`end` grows.

    The joint plan knows both sides of every hinge, so the neighbour of this end and the
    neighbour's OWN end at that hinge are read off it, and the tone is the neighbour's paint
    just inside its box edge there. Returns None when the hinge's other end is not an axis
    end (the shoulder sits INSIDE the chest box) or the canvas is missing - the caller then
    keeps the ART-H3 behaviour.
    """
    for j in plan_joints:
        nb, nb_end = None, None
        if j.get("parent") == part and j.get("parent_end") == end:
            nb, nb_end = j.get("child"), j.get("child_end")
        elif j.get("child") == part and j.get("child_end") == end:
            nb, nb_end = j.get("parent"), j.get("parent_end")
        if nb is None:
            continue
        if nb_end not in AXIS:
            return None
        return canvas_tone_inside(nb, nb_end)
    return None


def extend_canvas(a, end, npx, cap_px=0, strip=24, ppu=1200.0, seam_rgb=None, skirt_px=0):
    """Grow the painted body by `npx` px at canvas `end`, mirroring the art it grows from.

    a: (H, W, 4) uint8. Returns (grown, delta): delta is where the ORIGINAL array's first
    row/column lands along the grown axis (0 when the far end grows), so the caller can
    carry the shipping box' position through several grows without re-deriving it.

    The transparent gutter at that end is dropped first, so the mirror starts at the paint;
    `npx` = gutter + skirt margin + round cap, so the result reaches exactly the box edge
    plus the skirt the joint model verified.

    ART-H3 - "THE JOINTS' READ".  The extension is a MIRROR of the art's own cut rows, and
    a mirror repaints the drawn cut three times over: the rim's dark band, its bright line
    and the semi-transparent rows of the drawing all come back doubled right at the seam -
    the "hard, unblended overlay line" the designer's eye reads.  Three repairs, all on the
    ADDED pixels only (the art itself is not repainted):
      * solidify - where the drawing just inside the cut is opaque, the extension is opaque
        too, so the art's soft edge rows stop leaking the background through the skirt;
      * mute - the first ~1.2 cm of the extension is blended toward the limb's own tone a
        few px inside the cut, so no rim/bright-line twin is painted at the seam;
      * feather - the added silhouette's edges ramp over ~6 mm (side edges and the cap's
        tip), so a skirt reads as a soft contour and the joint's disc as a limb's round end
        instead of a flat circular cut.  The cap's own profile is a slimmer, rounded dome.
        ART-H4f kept this feather after the "no fade" variant lost the standing A/B.
    """
    if npx <= 0:
        return a, 0
    axis, side = AXIS[end]
    first, last = ink_span(a, axis)
    if first is None:
        return a, 0
    if axis == 0:
        body = a[first:] if side == 0 else a[:last + 1]
    else:
        body = a[:, first:] if side == 0 else a[:, :last + 1]
    delta = (npx - first) if side == 0 else 0
    pad_width = [(0, 0), (0, 0), (0, 0)]
    pad_width[axis] = (npx, 0) if side == 0 else (0, npx)
    grown = np.pad(body, pad_width, mode="reflect")
    grown = soften_extension(grown, axis, side, body.shape[axis], ppu, seam_rgb, skirt_px)
    if cap_px <= 0:
        return grown, delta

    # the ink's band at its own end (not at the canvas edge: the gutter is transparent)
    half, centre = band_profile(a, axis, first if side == 0 else last,
                                1 if side == 0 else -1, min(strip, a.shape[axis]))
    if half < 4:
        return grown, delta
    cap_px = int(min(cap_px, grown.shape[axis]))
    cx = a.shape[1 - axis] * 0.5 + centre
    n = grown.shape[axis]
    # ART-H4f (24.09, tried and REVERTED - the report's ART-H4f section): the tip's rim as
    # a 2 px anti-aliased edge (feather 0.0017*ppu, no fade) removed the faint fringe but
    # the pick preferred the faded joint, so the ART-H3 feather stays as it was.
    feather = float(max(3, int(round(0.008 * ppu))))
    xs = np.arange(grown.shape[1 - axis]) + 0.5
    for k in range(cap_px + 1):
        # k = how far this line sits from the tip (0 = the outermost line of the skirt)
        depth = cap_px - k
        # ART-H3: a slimmer rounded dome (a limb's end), not the flat half-disc.
        t = depth / float(cap_px) if cap_px else 1.0
        allow = half * max(0.0, 1.0 - t * t) ** 0.75
        idx = k if side == 0 else n - 1 - k
        cov = np.clip((allow + feather - np.abs(xs - cx)) / feather, 0.0, 1.0)
        if axis == 0:
            grown[idx, :, 3] = (grown[idx, :, 3] * cov).astype(np.uint8)
        else:
            grown[:, idx, 3] = (grown[:, idx, 3] * cov).astype(np.uint8)
    return grown, delta


def soften_extension(grown, axis, side, body_len, ppu, seam_rgb=None, skirt_px=0):
    """ART-H3 - the three repairs of an extension (see extend_canvas' docstring), plus the
    ART-H4 tone match across the seam.

    Operates on the ADDED rows/cols only (the art's own rows are never touched): solidity
    copied from the drawing just inside the cut, the rim echo muted toward the limb's own
    tone, and the added silhouette's side edges feathered.

    ART-H4 - "THE CIRCULAR OVERLAP SEAM".  H3 muted the added paint toward the limb's OWN
    tone at the cut, and at a joint that tone is the drawing's shaded end band (measured:
    the thigh's knee end reads ~145 while the shin's top is ~180), so the overlap - the
    skirt - still painted a tone step across the seam and the eye read its contour as an
    arc.  The tone that makes the seam stop reading as a line is the one on the OTHER side
    of it, so the skirt's pixels (the added paint BEYOND the box edge, `skirt_px` px into
    the extension) are now blended toward `seam_rgb` - the neighbour canvas' paint just
    inside its own box edge - with the weight peaking at the cut (0.85) and easing out
    quadratically toward the tip, where the limb's own ramp takes over again.  The fill
    part of the extension (inside the box, visible at rest) keeps the H3 rule.

    ART-H4d (24.09, tried and REVERTED - the report's ART-H4d section): a terminal alpha
    fade on the parent-side skirts (`feather` -> 3.5 cm at the tip + a decaying coverage
    floor + an axial taper) removed the hard arc from the offline A/B but the judge read the
    fade's wash as "полупрозрачно" and picked the un-faded panel in BOTH panel orders
    (Trials/Runs/reviews/arth4d-stills_gemini.md); the knee bar did not move either, so the
    shipped canvases keep this rule set unchanged.
    """
    n = grown.shape[axis]
    npx = n - body_len
    if npx <= 0:
        return grown

    def row(i):
        return grown[i] if axis == 0 else grown[:, i]

    def art_row(k):
        # k steps from the cut into the drawing (0 = the drawing's own outermost row)
        return (body_len - 1 - k) if side == 1 else (npx + k)

    mute = max(4, int(round(0.012 * ppu)))
    feather = max(3, int(round(0.006 * ppu)))
    inset = 8
    depth = 7
    samp = np.stack([row(art_row(k)) for k in range(inset, inset + depth)], axis=0)
    opaque = samp[:, :, 3] > 240
    oc = opaque.sum(axis=0)
    solid = oc >= max(1, depth - 2)
    rgb = (samp[:, :, :3] * opaque[:, :, None]).sum(axis=0) / np.maximum(oc, 1)[:, None]
    tlum = 0.2126 * rgb[:, 0] + 0.7152 * rgb[:, 1] + 0.0722 * rgb[:, 2]

    seam_target = None
    seam_lum = 0.0
    if seam_rgb is not None:
        seam_target = np.asarray(seam_rgb, dtype=np.float64).reshape(1, 3)
        seam_lum = float(0.2126 * seam_target[0, 0] + 0.7152 * seam_target[0, 1]
                         + 0.0722 * seam_target[0, 2])
    skirt_px = int(max(0, min(skirt_px, npx - 1)))
    skirt_len = max(1, npx - skirt_px)
    match_w = 0.85

    rows = range(body_len, n) if side == 1 else range(0, npx)
    for i in rows:
        k = (i - body_len) if side == 1 else (npx - 1 - i)
        line = row(i).astype(np.float64)
        if k < mute:
            w = 0.65 * (1.0 - k / float(mute))
            lum = 0.2126 * line[:, 0] + 0.7152 * line[:, 1] + 0.0722 * line[:, 2]
            m = solid & (np.abs(lum - tlum) > 8.0)
            line[m, 0] = line[m, 0] * (1.0 - w) + rgb[m, 0] * w
            line[m, 1] = line[m, 1] * (1.0 - w) + rgb[m, 1] * w
            line[m, 2] = line[m, 2] * (1.0 - w) + rgb[m, 2] * w
        if seam_target is not None and k >= skirt_px:
            # ART-H4b - the tone match as a LEVEL SHIFT over a LARGER region ("a larger blend
            # region"): the skirt's mean tone is the tone ACROSS the seam, but the shift
            # leaves every pixel's own deviation alone, so the band keeps the drawing's
            # mirrored shading.  The H4.0 repaint toward a flat RGB erased the texture
            # (measured: the thigh's last ~100 rows came out uniform) and the eye read the
            # flat band as a pasted piece even while the seam-step number fell.
            d = (k - skirt_px) / float(skirt_len)
            w = match_w * max(0.55, 1.0 - d * d)
            lum = 0.2126 * line[:, 0] + 0.7152 * line[:, 1] + 0.0722 * line[:, 2]
            m = solid & (np.abs(lum - seam_lum) > 6.0)
            if m.any() and w > 0.0:
                cur = line[m, :3].mean(axis=0)
                shift = (seam_target[0, :3] - cur) * w
                line[m, :3] = np.clip(np.round(line[m, :3] + shift), 0, 255)
        if axis == 0:
            grown[i, :, :3] = np.clip(np.round(line[:, :3]), 0, 255).astype(np.uint8)
            alpha = grown[i, :, 3].astype(np.float64)
        else:
            grown[:, i, :3] = np.clip(np.round(line[:, :3]), 0, 255).astype(np.uint8)
            alpha = grown[:, i, 3].astype(np.float64)
        # solidify: the drawing just inside the cut is opaque, so the extension carries
        # no translucent echo of its soft edge rows (before the feather, so the feathered
        # rim keeps its ramp)
        alpha = np.where(solid & (alpha >= 64.0), 255.0, alpha)
        idx = np.where(alpha > 16)[0]
        if len(idx) >= 3:
            lo, hi = idx[0], idx[-1]
            xs = np.arange(alpha.size, dtype=np.float64)
            f = np.minimum(np.clip((xs - lo) / feather, 0.0, 1.0),
                           np.clip((hi - xs) / feather, 0.0, 1.0))
            f = np.maximum(f, 0.3)
            alpha = alpha * np.where(alpha > 16, f, 0.0)
        if axis == 0:
            grown[i, :, 3] = np.clip(np.round(alpha), 0, 255).astype(np.uint8)
        else:
            grown[:, i, 3] = np.clip(np.round(alpha), 0, 255).astype(np.uint8)
    return grown


def rebuild(part, plan, dry, seam=None):
    src_path = os.path.join(ART, part + ".png")
    meta_path = src_path + ".meta"
    im = Image.open(src_path).convert("RGBA")
    meta = read_meta(meta_path)
    ppu = meta_ppu(meta)
    a = np.array(im)
    h, w = a.shape[0], a.shape[1]
    e = plan.get(part, {})
    seam = seam or {}

    def need(end):
        """How much to grow at `end`, in px: the fill + the skirt, and the round cap.

        The skirt's paint reaches `margin + half` past the box edge - `margin` of band
        plus a half-disc cap of radius `half` (the ink's half-width at that end), which is
        the shape `Tools/art-h1-joint-fit.py` verified as closed.  Returns the box edge's
        own offset into the growth (`skirt_px`) too: everything past it is the skirt, and
        that is the paint the ART-H4 tone match owns.
        """
        fill = bool(e.get("fill_" + end))
        margin = float(e.get(end, 0.0))
        if not fill and margin <= 0:
            return 0, 0, 0
        axis, side = AXIS[end]
        first, last = ink_span(a, axis)
        if first is None:
            return 0, 0, 0
        end_row = first if side == 0 else last
        gutter = float((first if side == 0 else (a.shape[axis] - 1 - last)) / ppu)
        half, _centre = band_profile(a, axis, end_row, 1 if side == 0 else -1,
                                     min(24, a.shape[axis]))
        cap = int(round(half)) if margin > 0 else 0
        total = int(round((gutter + margin) * ppu)) + cap
        return total, cap, int(round(gutter * ppu))

    out = a
    dx0, dy0 = 0, 0          # where the ORIGINAL canvas' first column/row lands
    info = []
    for end in ("top", "bottom", "back", "front"):
        npx, cap, skirt_px = need(end)
        if npx <= 0:
            continue
        out, delta = extend_canvas(out, end, npx, cap_px=cap, ppu=ppu,
                                   seam_rgb=seam.get(end), skirt_px=skirt_px)
        axis, _side = AXIS[end]
        if axis == 0:
            dy0 += delta
        else:
            dx0 += delta
        info.append((end, npx, cap))
    if not info:
        return None

    # The pivot is the shipping box' centre inside the grown canvas. The box IS the canvas
    # that came in (`Human.cs` maps the collider onto the picture 1:1 - GAME_PXM aside the
    # two are the same rectangle), so its centre is (dx0 + w/2, dy0 + h/2) - no echo of the
    # growth amounts, so a second grow on the same axis cannot move it.
    new_h, new_w = out.shape[0], out.shape[1]
    box_cx = dx0 + w * 0.5
    # The foot's canvas covers the whole foot, but it is dressed on the HEEL segment
    # (Human.cs splits the foot into "RightLegFoot" + "RightLegToe" boxes), so the pivot
    # must be the heel's box centre inside the art, not the canvas centre.
    if part == "foot":
        box_cx += boxes()["footSize"][0] * (HEEL_SHARE - 1.0) * 0.5 * ppu
    box_cy = dy0 + h * 0.5
    pivot = (box_cx / new_w, 1.0 - box_cy / new_h)

    if not dry:
        Image.fromarray(out).save(src_path)
        write_meta(meta_path, meta, ppu, pivot, (new_w, new_h))
    return {"part": part, "old": (w, h), "new": (new_w, new_h), "ppu": ppu,
            "world": (new_w / ppu, new_h / ppu), "pivot": pivot,
            "box": (dx0, dy0, w, h), "grow": info}


def foot_box_px(meta, im):
    """The whole-foot box (the canvas that came in) inside the CURRENT foot canvas, px.

    Falls back to the meta for a foot the generator skipped (already grown): its pivot is
    then the heel box centre, one `0.5 - HEEL_SHARE` of footSize.x right of the foot box
    centre.
    """
    ppu = meta_ppu(meta)
    m = re.search(r"^  spritePivot: \{x: ([0-9.]+), y: ([0-9.]+)\}$", meta, flags=re.M)
    pivx, pivy = float(m.group(1)), float(m.group(2))
    box = boxes()["footSize"]
    W, H = im.width, im.height
    cx = pivx * W
    if already_grown(meta):
        cx += box[0] * (1.0 - HEEL_SHARE) * 0.5 * ppu
    return (cx - box[0] * 0.5 * ppu, (1.0 - pivy) * H - box[1] * 0.5 * ppu,
            box[0] * ppu, box[1] * ppu)


def rebuild_toe(foot_box, plan, dry, joints=None):
    """The toe is a new canvas: the front of the foot's art (its own box) plus the MTP
    plug the model asks for, so the toe's picture reaches back into the heel's paint.

    The crop is taken through the FOOT BOX as the generator laid it out (or, when the foot
    was skipped, through the foot's own pivot), so it crops the same rectangle whether or
    not the foot canvas has already grown for the ankle.
    """
    src = os.path.join(ART, "foot.png")
    im = Image.open(src).convert("RGBA")
    meta = read_meta(src + ".meta")
    ppu = meta_ppu(meta)
    a = np.array(im)
    H, W = a.shape[0], a.shape[1]
    if foot_box is None:
        foot_box = foot_box_px(meta, im)
    gx, gy, fw, fh = foot_box
    x0 = int(round(gx + fw * HEEL_SHARE))
    x1 = int(round(gx + fw))
    y0 = int(round(gy))
    y1 = int(round(gy + fh))
    crop = a[max(0, y0):min(H, y1), max(0, x0):min(W, x1)].copy()
    m = float(plan.get("toe", {}).get("back", 0.0))
    # the same rule as `need()`: the MTP plug = margin + the round cap of the toe's own
    # half-height at the ball (the toe carries the skirt of the MTP joint)
    axis, side = AXIS["back"]
    first, _last = ink_span(crop, axis)
    half = 0.0
    if first is not None:
        half, _c = band_profile(crop, axis, first, 1, min(24, crop.shape[axis]))
    cap = int(round(half)) if m > 0 else 0
    npx = int(round(m * ppu)) + cap
    out, delta = extend_canvas(crop, "back", npx, cap_px=cap, ppu=ppu,
                               seam_rgb=seam_tone(joints or [], "toe", "back"),
                               skirt_px=0)
    new_h, new_w = out.shape[0], out.shape[1]
    pivot = ((delta + crop.shape[1] * 0.5) / new_w, 0.5)
    path = os.path.join(ART, "toe.png")
    if not dry:
        Image.fromarray(out).save(path)
        toe_meta = meta.replace(
            re.search(r"^guid: ([0-9a-f]{32})$", meta, flags=re.M).group(1),
            hashlib.md5(b"ARTH1:Human/toe").hexdigest())
        toe_meta = re.sub(r"^  spriteID: [0-9a-f]{32}$",
                          "  spriteID: %s" % hashlib.md5(b"ARTH1:Human/toe").hexdigest(),
                          toe_meta, count=1, flags=re.M)
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
            fh.write(toe_meta)
        write_meta(path + ".meta", read_meta(path + ".meta"), ppu, pivot, (new_w, new_h))
    return {"part": "toe", "old": (crop.shape[1], crop.shape[0]), "new": (new_w, new_h),
            "ppu": ppu, "world": (new_w / ppu, new_h / ppu), "pivot": pivot,
            "box": (0, 0, crop.shape[1], crop.shape[0]), "grow": [("back", npx, cap)]}


def meta_pivot(meta):
    """The sprite pivot the meta carries - the box centre inside the canvas."""
    m = re.search(r"^  spritePivot: \{x: ([0-9.]+), y: ([0-9.]+)\}$", meta, flags=re.M)
    return (float(m.group(1)), float(m.group(2))) if m else (0.5, 0.5)


# --- the joint disc: the sheet's "must connect", made measurable ------------------------
# `Tools/art-h1-pose-frames.py` measures the OPEN px inside the joint's disc: radius = the
# narrower neighbour's ink half-width at the hinge. The plan's skirts close most of it, but
# the ART's band at a joint is not centred on the hinge (the thigh's band at the knee sits
# ~19 px to the right), so a sliver stays open at every angle. A disc painted in the
# PARENT's canvas around the hinge - mirrored from the parent's own ink - closes the disc at
# every angle: the parent does not rotate while this joint's child swings, and its paint is
# drawn UNDER the child, so the patch shows only where the child leaves the disc bare.

def frames_tool():
    """The pose tool's rig reader and joint list, imported - no second copy of Human.cs'."""
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                        "art-h1-pose-frames.py")
    spec = importlib.util.spec_from_file_location("art_h1_pose_frames", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def ink_half(a, ppu, pivot, anchor):
    """`Tools/art-h1-pose-frames.py: ink_half` - the ink's half-extent ACROSS the limb
    within 25 px of the anchor, in metres - so the disc's radius IS the radius the frames
    measure a hole against."""
    h, w = a.shape[0], a.shape[1]
    m = a[:, :, 3] > ALPHA_MIN
    ax_px = pivot[0] * w + anchor[0] * ppu
    ay_px = (1.0 - pivot[1]) * h - anchor[1] * ppu
    n = math.hypot(anchor[0], anchor[1])
    if n < 1e-9:
        return 0.0
    inward = (-anchor[0] / n, -anchor[1] / n)
    across = (inward[1], -inward[0])
    rows, cols = np.where(m)
    px = cols + 0.5 - ax_px
    py = ay_px - (rows + 0.5)
    along = px * inward[0] + py * inward[1]
    keep = (along > -25.0) & (along < 25.0)
    if not keep.any():
        return 0.0
    t = px[keep] * across[0] + py[keep] * across[1]
    return (t.max() - t.min()) * 0.5 / ppu


def paint_disc(a, cx, cy, r, inward, across, ppu=1200.0, seam_rgb=None):
    """Mirror-fill the transparent px of the disc (centre cx,cy px, radius r) from the ink
    already in the canvas: a line's own across-span, reflected about itself - the same
    reflect the end fills use. Returns how many px stayed transparent.

    ART-H3: the patch is the joint's exposed disc, so it now ends like a limb, not like a
    cut: the shape is elongated ~12% along the limb's axis (a stump, not a circle) and its
    rim fades over ~7 mm (a halo ring) instead of stopping dead - a hard circular cut was
    the designer's first fault.  (ART-H4f measured the no-fade variant and kept this: see
    the module docstring.)  The added ring's colours are washed toward their own mean so a
    dark contour line mirrored into the patch does not draw an arc across the limb.
    """
    H, W = a.shape[0], a.shape[1]
    elong = 1.12
    # ART-H4f (24.09, tried and REVERTED - the report's ART-H4f section): the rim as a 2 px
    # anti-aliased edge (no alpha fade) halved the translucent band's exposed px, but the
    # standing A/B pick preferred THIS fade at the knee in BOTH panel orders ("размытые
    # края сглаживают сочленение, не создавая эффекта отдельного вырезанного круга"), so
    # the ART-H3 fade stays and the lever is recorded as measured.
    fade = float(max(3, int(round(0.010 * ppu))))
    r_out = r + fade
    r_bb = elong * r_out + 2.0
    x0 = max(0, int(math.floor(cx - r_bb)))
    x1 = min(W, int(math.ceil(cx + r_bb)))
    y0 = max(0, int(math.floor(cy - r_bb)))
    y1 = min(H, int(math.ceil(cy + r_bb)))
    if x1 <= x0 or y1 <= y0:
        return 0
    pad = int(r_bb) + 6
    X0, X1 = max(0, x0 - pad), min(W, x1 + pad)
    Y0, Y1 = max(0, y0 - pad), min(H, y1 + pad)
    ny, nx = np.mgrid[Y0:Y1, X0:X1]
    ddx = nx + 0.5 - cx
    ddy = cy - (ny + 0.5)
    ss = ddx * inward[0] + ddy * inward[1]
    tt = ddx * across[0] + ddy * across[1]
    ink = a[Y0:Y1, X0:X1, 3] > ALPHA_MIN
    if not ink.any():
        rr = np.sqrt((ss / elong) ** 2 + tt * tt)
        return int((rr <= r_out + 0.5).sum())
    sbin = np.floor(ss[ink]).astype(np.int64)
    order = np.argsort(sbin, kind="stable")
    b_ink = sbin[order]
    t_ink = tt[ink][order]
    ubins, starts = np.unique(b_ink, return_index=True)
    ends = np.append(starts[1:], b_ink.size)
    lo = np.minimum.reduceat(t_ink, starts)
    hi = np.maximum.reduceat(t_ink, starts)

    yy, xx = np.mgrid[y0:y1, x0:x1]
    hdx = xx + 0.5 - cx
    hdy = cy - (yy + 0.5)
    hs = hdx * inward[0] + hdy * inward[1]
    ht = hdx * across[0] + hdy * across[1]
    nr = np.sqrt((hs / elong) ** 2 + ht * ht)
    disc = nr <= (r_out + 0.5)
    hole = disc & (a[y0:y1, x0:x1, 3] <= ALPHA_MIN)
    if not hole.any():
        return 0
    H_s, H_t = hs[hole], ht[hole]
    H_y, H_x = yy[hole], xx[hole]
    idx = np.searchsorted(ubins, np.floor(H_s).astype(np.int64))
    cand = np.clip(np.stack([idx - 1, idx]), 0, ubins.size - 1)
    dpick = np.argmin(np.abs(ubins[cand] - H_s[None, :]), axis=0)
    bi = cand[dpick, np.arange(H_s.size)]
    L, Hh = lo[bi], hi[bi]
    span = np.maximum(Hh - L, 1e-6)
    d = np.abs(H_t - L) % (2.0 * span)
    d = np.where(d > span, 2.0 * span - d, d)
    t_src = L + d
    s_src = ubins[bi] + 0.5
    # A line with no ink (the growth past the box edge, or a gap in the art) mirrors about
    # the HINGE instead: that continues the limb's own drawing (muscle, bone), where the
    # nearest-inked-line rule would copy the segment's CUT face - a white patch at the joint.
    no_ink = np.abs(ubins[bi] - H_s) > 1.5
    s_hinge = -H_s
    sx_h = cx + s_hinge * inward[0] + H_t * across[0]
    sy_h = cy - (s_hinge * inward[1] + H_t * across[1])
    ix_h = np.clip(np.round(sx_h - 0.5).astype(np.int64), 0, W - 1)
    iy_h = np.clip(np.round(sy_h - 0.5).astype(np.int64), 0, H - 1)
    ok_h = (a[iy_h, ix_h, 3] > ALPHA_MIN) & no_ink
    sx = np.where(no_ink, sx_h, cx + s_src * inward[0] + t_src * across[0])
    sy = np.where(no_ink, sy_h, cy - (s_src * inward[1] + t_src * across[1]))
    ix = np.clip(np.round(sx - 0.5).astype(np.int64), 0, W - 1)
    iy = np.clip(np.round(sy - 0.5).astype(np.int64), 0, H - 1)
    ok = (a[iy, ix, 3] > ALPHA_MIN)
    filled = ok.copy()
    a[H_y[ok], H_x[ok]] = a[iy[ok], ix[ok]]
    # the art carries transparent gaps INSIDE its own silhouette (the anatomical drawing's
    # pieces), and there the same-line source is transparent too: fall back to the nearest
    # ink by an expanding window, so the disc ends up solid.
    left = 0
    for j in np.where(~ok)[0]:
        py, px = int(H_y[j]), int(H_x[j])
        found = False
        for rad in (2, 4, 8, 16, 32, 48):
            ylo, yhi = max(0, py - rad), min(H, py + rad + 1)
            xlo, xhi = max(0, px - rad), min(W, px + rad + 1)
            win = a[ylo:yhi, xlo:xhi, 3] > ALPHA_MIN
            if win.any():
                wy, wx = np.where(win)
                k = int(np.argmin((wy + ylo - py) ** 2 + (wx + xlo - px) ** 2))
                a[py, px] = a[wy[k] + ylo, wx[k] + xlo]
                filled[j] = True
                found = True
                break
        if not found:
            left += 1
    # ART-H3 - the patch now ENDS like a limb and READS like one:
    #   * its fill is relaxed into the tones of the drawing that surrounds it (a couple of
    #     hundred Jacobi passes, the drawing's own px pinned) - a smooth bulge instead of a
    #     mirrored fragment with straight seams and glitch bands across the joint;
    #   * the rim fades over `fade` px, so the silhouette melts into the limb instead of
    #     stopping on a circular arc (the designer's "flat circular cut").
    if filled.any():
        wy0 = max(0, int(math.floor(cy - r_bb)) - 2)
        wy1 = min(H, int(math.ceil(cy + r_bb)) + 3)
        wx0 = max(0, int(math.floor(cx - r_bb)) - 2)
        wx1 = min(W, int(math.ceil(cx + r_bb)) + 3)
        win = a[wy0:wy1, wx0:wx1]
        free = np.zeros(win.shape[:2], bool)
        free[H_y - wy0, H_x - wx0] = True
        free &= win[:, :, 3] > ALPHA_MIN
        col = win[:, :, :3].astype(np.float64)
        nbrs = ((1, 0), (-1, 0), (0, 1), (0, -1))
        src = (win[:, :, 3] > ALPHA_MIN)
        for _ in range(240):
            num = np.zeros_like(col)
            den = np.zeros(win.shape[:2])
            for dy, dx in nbrs:
                nb = np.roll(col, (dy, dx), axis=(0, 1))
                nv = np.roll(src, (dy, dx), axis=(0, 1))
                if dy == 1:
                    nb[0, :, :] = col[0, :, :]
                    nv[0, :] = src[0, :]
                elif dy == -1:
                    nb[-1, :, :] = col[-1, :, :]
                    nv[-1, :] = src[-1, :]
                if dx == 1:
                    nb[:, 0, :] = col[:, 0, :]
                    nv[:, 0] = src[:, 0]
                elif dx == -1:
                    nb[:, -1, :] = col[:, -1, :]
                    nv[:, -1] = src[:, -1]
                num += np.where(nv[:, :, None], nb, 0.0)
                den += nv
            avg = num / np.maximum(den, 1.0)[:, :, None]
            col[free] = avg[free]
        win[:, :, :3] = np.clip(np.round(col), 0, 255).astype(np.uint8)
        rr = np.sqrt((H_s / elong) ** 2 + H_t * H_t)
        ys, xsh = H_y[filled], H_x[filled]
        rr_f = rr[filled]
        # ART-H4 - "THE CIRCULAR OVERLAP SEAM": the added paint's tone is matched ACROSS the
        # seam (the neighbour canvas' tone just inside its own box edge, `seam_rgb`), with
        # the weight peaking at the rim and easing to the centre, so the contour the eye
        # reads as the disc's circle carries the tone of what is beyond it and stops reading
        # as a line.  The bulge's middle keeps the limb's own relaxed tone.
        if seam_rgb is not None:
            # ART-H4b - the same rule at the disc's ring: a level shift toward the tone ACROSS
            # the seam, not a repaint (the ring keeps the bulge's own shading).
            st = np.asarray(seam_rgb, dtype=np.float64).reshape(3)
            w = 0.85 * np.clip(rr_f / max(1e-6, r_out), 0.0, 1.0) ** 2
            cur = a[ys, xsh, :3].astype(np.float64)
            shift = (st - cur.mean(axis=0))[None, :] * w[:, None]
            a[ys, xsh, :3] = np.clip(np.round(cur + shift), 0, 255).astype(np.uint8)
        ramp = np.clip((r_out + 0.5 - rr_f) / fade, 0.0, 1.0)
        a[ys, xsh, 3] = np.clip(np.round(a[ys, xsh, 3].astype(np.float64) * ramp), 0, 255
                                ).astype(np.uint8)
    return left


def joint_patches(dry):
    """One disc, per joint, painted into the PARENT's canvas; the canvas grows where the
    disc crosses its edge (transparent, so the disc's own shape is the only paint added).
    Returns [(joint, R m, patch r px, grown px, left transparent px)]."""
    pf = frames_tool()
    _, joints = pf.read_rig()
    out = []
    for name, pkey, ckey, cvar, pvar, _half in pf.JOINTS:
        ppart, cpart = pf.CANVAS[pkey], pf.CANVAS[ckey]
        p_path = os.path.join(ART, ppart + ".png")
        c_path = os.path.join(ART, cpart + ".png")
        if not (os.path.exists(p_path) and os.path.exists(c_path)):
            out.append((name, 0.0, 0, 0, 0))
            continue
        p_meta = read_meta(p_path + ".meta")
        c_meta = read_meta(c_path + ".meta")
        p_ppu, c_ppu = meta_ppu(p_meta), meta_ppu(c_meta)
        p_a = np.array(Image.open(p_path).convert("RGBA"))
        c_a = np.array(Image.open(c_path).convert("RGBA"))
        p_pivot, c_pivot = meta_pivot(p_meta), meta_pivot(c_meta)
        anchor = joints[cvar]["anchor_parent"]
        c_anchor = joints[cvar]["anchor"]
        p_half = ink_half(p_a, p_ppu, p_pivot, anchor)
        c_half = ink_half(c_a, c_ppu, c_pivot, c_anchor)
        # The disc's radius once the patch is there is the min of the two halves - and the
        # patch lifts the parent's own measure, so the min can become the CHILD's half (the
        # head's, the hip's, the ankle's leak when the patch is only the narrower side's).
        # The patch is the WIDER of the two, then the disc is always inside it.
        r_m = max(p_half, c_half)
        if r_m <= 0.0:
            out.append((name, 0.0, 0, 0, 0))
            continue
        r_px = int(math.ceil(r_m * p_ppu)) + 3
        # ART-H3: the canvas must carry the patch's whole shape - the elongation and the
        # fading rim included - or the halo would be cut by the canvas edge it grew for.
        r_bb = 1.12 * (r_px + max(3, int(round(0.010 * p_ppu)))) + 2.0
        h, w = p_a.shape[0], p_a.shape[1]
        cx = p_pivot[0] * w + anchor[0] * p_ppu
        cy = (1.0 - p_pivot[1]) * h - anchor[1] * p_ppu
        grow = {"top": max(0, int(math.ceil(r_bb - cy))),
                "bottom": max(0, int(math.ceil(cy + r_bb - h))),
                "back": max(0, int(math.ceil(r_bb - cx))),
                "front": max(0, int(math.ceil(cx + r_bb - w)))}
        a = p_a
        dx = dy = 0
        for end in ("top", "bottom", "back", "front"):
            n = grow[end]
            if n <= 0:
                continue
            axis = 0 if end in ("top", "bottom") else 1
            before = end in ("top", "back")
            pad = [(0, 0), (0, 0), (0, 0)]
            pad[axis] = (n, 0) if before else (0, n)
            a = np.pad(a, pad, mode="constant", constant_values=0)
            if axis == 0 and before:
                dy += n
            elif axis == 1 and before:
                dx += n
        cx += dx
        cy += dy
        n = math.hypot(anchor[0], anchor[1])
        inward = (-anchor[0] / n, -anchor[1] / n)
        across = (inward[1], -inward[0])
        # ART-H4: the tone across THIS seam - the child's paint just inside its own box edge
        # at the joint end (the side its own anchor sits on).
        c_anchor = joints[cvar]["anchor"]
        if abs(c_anchor[1]) >= abs(c_anchor[0]):
            c_end = "top" if c_anchor[1] > 0 else "bottom"
        else:
            c_end = "front" if c_anchor[0] > 0 else "back"
        seam_rgb = canvas_tone_inside(cpart, c_end)
        left = paint_disc(a, cx, cy, r_px, inward, across, ppu=p_ppu, seam_rgb=seam_rgb)
        W2, H2 = a.shape[1], a.shape[0]
        bx = p_pivot[0] * w + dx
        by = (1.0 - p_pivot[1]) * h + dy
        pivot = (bx / W2, 1.0 - by / H2)
        if not dry:
            Image.fromarray(a).save(p_path)
            write_meta(p_path + ".meta", p_meta, p_ppu, pivot, (W2, H2))
        out.append((name, r_m, r_px, sum(grow.values()), left))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--force", action="store_true",
                    help="run even when a canvas already carries the ART-H1 pivot")
    ap.add_argument("--only", default=None)
    ap.add_argument("--plan", default=PLAN)
    args = ap.parse_args()

    with open(args.plan, encoding="utf-8") as fh:
        plan_full = json.load(fh)
    plan = plan_full["parts"]
    only = None if not args.only else set(args.only.split(","))

    # ART-H4 - the tone across every seam, read from the PRISTINE canvases BEFORE any growth:
    # the sample band sits INSIDE the box edge, which the growth never touches, but taking it
    # up front makes the value independent of the part loop's order as well.
    seam = {}
    for j in plan_full.get("joints", []):
        for part, end in ((j.get("parent"), j.get("parent_end")),
                          (j.get("child"), j.get("child_end"))):
            if part in PART_FIELD and end in AXIS and (part, end) not in seam:
                seam[(part, end)] = seam_tone(plan_full.get("joints", []), part, end)
    if seam:
        print("seam tones (the tone across each joint, RGB): " +
              ", ".join("%s.%s=(%d,%d,%d)" % (p, e, c[0], c[1], c[2])
                        for (p, e), c in sorted(seam.items()) if c is not None))

    rows = []
    foot_box = None
    print("%-7s %-11s %-11s %6s %-13s %-19s %s" % (
        "part", "old px", "new px", "ppu", "world(m)", "pivot", "grow(px: fill+skirt)"))
    for part in PART_FIELD:
        if only and part not in only:
            continue
        if not args.force and already_grown(read_meta(os.path.join(ART, part + ".png.meta"))):
            print("%-7s ALREADY GROWN - restore with Tools/import-part-art.py --groups Human,"
                  " then Tools/art-h1-joint-fit.py --plan, then re-run (or pass --force)" % part)
            continue
        r = rebuild(part, plan, args.dry_run,
                    seam={e: seam.get((part, e)) for e in AXIS})
        if r is None:
            print("%-7s (no plan - untouched)" % part)
            continue
        rows.append(r)
        if part == "foot":
            foot_box = r["box"]
        print("%-7s %-11s %-11s %6.0f %6.3fx%-6.3f %-19s %s" % (
            r["part"], "%dx%d" % r["old"], "%dx%d" % r["new"], r["ppu"],
            r["world"][0], r["world"][1],
            "(%.4f, %.4f)" % r["pivot"],
            " ".join("%s=%d+%d" % (e, n, t) for e, n, t in r["grow"])))
    if not only or "foot" in (only or ()):
        r = rebuild_toe(foot_box, plan, args.dry_run, plan_full.get("joints", []))
        rows.append(r)
        print("%-7s %-11s %-11s %6.0f %6.3fx%-6.3f %-19s %s" % (
            r["part"], "%dx%d" % r["old"], "%dx%d" % r["new"], r["ppu"],
            r["world"][0], r["world"][1], "(%.4f, %.4f)" % r["pivot"],
            " ".join("%s=%d+%d" % (e, n, t) for e, n, t in r["grow"])))

    if not args.dry_run and only is None:
        # the discs: the parents' patches - after the fills and skirts, so R reads the
        # canvases as the frames tool will read them
        print("joint discs (painted into each parent canvas - the frames' ink_half radius)")
        for name, r_m, r_px, grown, left in joint_patches(args.dry_run):
            print("  %-8s R=%.4f m  patch r=%d px  grow=%d px  left=%d px"
                  % (name, r_m, r_px, grown, left))

    if not args.dry_run:
        # the eye's sheet: every canvas with its collider box drawn (magenta) and the
        # skirt hatched - so the overlap is visible as a rectangle that is SHORTER than
        # the canvas but LONGER than the paint
        scale = 150.0
        cells = []
        for r in rows:
            im = Image.open(os.path.join(ART, r["part"] + ".png")).convert("RGBA")
            w = max(1, int(round(r["world"][0] * scale)))
            h = max(1, int(round(r["world"][1] * scale)))
            cell = Image.new("RGBA", (w + 16, h + 16), (36, 36, 36, 255))
            cell.alpha_composite(im.resize((w, h), Image.LANCZOS), (8, 8))
            d = ImageDraw.Draw(cell)
            pv = r["pivot"]
            cx, cy = pv[0] * w + 8, (1.0 - pv[1]) * h + 8
            box_field = PART_FIELD.get(r["part"])
            if r["part"] == "foot":
                bw = boxes()["footSize"][0] * HEEL_SHARE
                bh = boxes()["footSize"][1]
            elif r["part"] == "toe":
                bw = boxes()["footSize"][0] * (1.0 - HEEL_SHARE)
                bh = boxes()["footSize"][1]
            elif box_field:
                bw, bh = boxes()[box_field]
            else:
                bw, bh = boxes()["footSize"][0] * 0.30, boxes()["footSize"][1]
            d.rectangle([cx - bw * scale / 2, cy - bh * scale / 2,
                         cx + bw * scale / 2, cy + bh * scale / 2],
                        outline=(255, 0, 255, 255))
            d.text((2, 2), r["part"], fill=(255, 255, 0, 255))
            cells.append(cell)
        sheet_w = sum(c.width for c in cells) + 8 * (len(cells) + 1)
        sheet_h = max(c.height for c in cells) + 16
        sheet = Image.new("RGBA", (sheet_w, sheet_h), (12, 12, 12, 255))
        x = 8
        for c in cells:
            sheet.alpha_composite(c, (x, 8))
            x += c.width + 8
        sheet.save(SHEET)
        print("sheet %s %dx%d" % (SHEET, sheet.width, sheet.height))
    return 0


if __name__ == "__main__":
    sys.exit(main())
