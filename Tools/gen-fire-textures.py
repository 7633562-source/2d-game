#!/usr/bin/env python3
"""ART-0 gap 2: the campfire flame as painted art (Art/Campfire/*).

What the code draws today
    `Campfire.Build` / `BuildTongues` / `TickTongues` / `SoftTexture`:
    24 pooled tongues, each a 48x48 white soft disc (alpha = (1 - d)^2 over the
    inscribed ellipse) scaled onto its own box with `localScale`, coloured by the
    age ramp `FlameColor(u, heat)` (white -> yellow -> orange -> red) and faded by
    age. The ramp is fine; what is missing is the SILHOUETTE - a disc has no tip,
    so the fire reads as a stack of soft blobs (ART-0 inventory, gap list item 2:
    "campfire - procedural flame/embers").

What this tool paints
    SHIPPED: `core` 0.160 x 0.38 m - the flame core behind the tongues, one
    sprite drawn in front of them (FireOrder + 4). It carries the fire's body
    and its licked crown, which is what the procedural soft disc cannot give it:
    a defined top.

    SHIPPED since the nineteenth pass: `log_a/b/c` (0.62 x 0.14, 0.54 x 0.13,
    0.30 x 0.12 m - `Campfire.LogRow`). `BuildLogs` draws each log through
    `HumanSegment.ApplyPicture`, i.e. a collider-sized white rectangle tinted
    `LogWood` = (0.50, 0.25, 0.00, 0.5): a flat HALF-TRANSPARENT brown box. A
    read of the shipped FIRE frame named exactly that - "flat semi-transparent
    brown rectangles, no bark, no cut ends, you can see their intersections" -
    which is the original complaint in miniature. Each canvas is the log's own
    box aspect and is dressed Sliced onto that box by `Campfire.DressLog`, one
    repeat across the part, so nothing is stretched and size never comes from
    `localScale`. A missing PNG leaves today's box untouched.

    The crown is a COMPOSITION (eleventh pass): the body (`CORE_BODY`) plus the
    licks of `CORE_LICKS`, unioned with `np.maximum`. The first core was one
    dome; a real frame read it as "a smooth light spindle - no flame tongues".
    Two or three licks of different height, lean and side are what makes the top
    of the sprite read as fire rather than as a lamp.

    REJECTED: `tongue_a/b/c`, the three pooled-tongue canvases (0.055 x 0.46,
    0.080 x 0.38, 0.110 x 0.30 m). They are still generated - in memory - for the
    evidence sheets, and are deliberately NOT written into Resources. Three blind
    reviews of the same moment, same camera (LEFT procedural, MIDDLE core painted,
    RIGHT all four painted; `flame_variants_300pxm.png`) ranked the all-painted
    fire last every time: 24 painted tongues read as separate spindles ("comb
    teeth", "glass fibres" / "пучок оптоволокна"), while the soft discs merge into
    one mass. At 15-30 px on screen a tongue has no room for a silhouette, so the
    tongues keep the disc and only the core becomes a painting.

Canvas rules (painted-parts.mdc + .cursor/agents/artist.md)
    * canvas in the box's OWN aspect: the campfire sizes a picture with
      `localScale` (it has no collider and no Rigidbody2D), so a canvas whose
      aspect does not match its box would distort the silhouette;
    * RGB stays flat white. The colour is the age ramp's, written by `sr.color`
      at run time; a baked gradient would fight the ramp and the flat language
      ("no gradients inside a silhouette"). Alpha carries the shape;
    * bright centre line, feathered edge, a point at the top, and a pinched soft
      base so the tongue grows out of the coal bed instead of being cut off by a
      straight edge;
    * transparent background, no dark outline: a rim reads as a cut-out when the
      tongue is 15 px wide on screen.

Evidence written beside the art (Trials/Runs/art-fire-<date>/)
    * the four canvases at native resolution;
    * `flame_ab_<n>pxm.png` : the WHOLE fire at one moment and one camera scale,
      LEFT = today's procedural discs, RIGHT = the painted canvases. Same tongue
      pool, same ages, same flicker, same tint ramp - the state is mirrored from
      `BuildTongues` + `TickTongues`, so the only difference is the picture;
    * `fire_blind_2x2.png` : the blind A/B the ART-0 tile review asked for.

Run from the project root:   python Tools/gen-fire-textures.py
Writes the PNGs plus their .meta import settings, then prints a verification table.
"""

import math
import os
import random
import re
import time as _time

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART_DIR = os.path.join(ROOT, "Assets", "Resources", "Art")
FIRE_DIR = os.path.join(ART_DIR, "Campfire")
META_TEMPLATE = os.path.join(ROOT, "Assets", "LegacyArt", "Apple", "fruit.png.meta")
FOLDER_TEMPLATE = os.path.join(ROOT, "Assets", "Resources", "Level.meta")
OUT_DIR = os.path.join(ROOT, "Trials", "Runs", "art-fire-2026-09-19")

# ─── the boxes the code scales each canvas onto (Campfire.Build / BuildTongues) ───
BOXES = {
    "tongue_a": (0.055, 0.46),
    "tongue_b": (0.080, 0.38),
    "tongue_c": (0.110, 0.30),
    "core": (0.160, 0.38),
    # The three logs (Campfire.LogRow, read by BuildLogs and by FrozenStack). One
    # canvas each, in that box's OWN aspect: a Sliced sprite maps the whole canvas
    # onto the box, so one shared canvas would squash LogC (aspect 2.50) by 1.77x
    # against LogA (4.43).
    "log_a": (0.62, 0.14),
    "log_b": (0.54, 0.13),
    "log_c": (0.30, 0.12),
}
# Canvas heights. The tallest box is 0.46 m: at the gallery's closest lens
# (orthoSize 1.304 -> 414 px/m) that is 190 px on screen, so 256 px of canvas is
# the last useful step before the picture costs bytes and buys nothing.
CANVAS_H = {"tongue_a": 256, "tongue_b": 224, "tongue_c": 192, "core": 192,
            # 56 / 52 / 36 px give the exact box aspect (4.4286 / 4.1538 / 2.5000)
            # and put the log 48 px tall on screen at the gallery's FIRE lens
            # (orthoSize 1.59 -> 340 px/m), so the canvas is drawn at about 1:1.
            "log_a": 56, "log_b": 52, "log_c": 36}

# ─── shape parameters per canvas ───
# half      max half-width as a fraction of the canvas half-width
# tip_pow   how the top closes: 1.0 = a needle, 2.2+ = a rounded dome (derivative 0 at v=1)
# taper     width fall toward the tip (higher = the body thins earlier)
# dissolve  how translucent the licked top goes (0 = solid tip). The first pass used
#           needles at high contrast and the eye rejected them ("glass fibres / comb
#           teeth"), so the top of the tongue fades into the dark instead of ending.
# feather   alpha falloff across the half-width: 1.8 keeps the centre bright and the
#           edge soft enough that neighbouring tongues merge into one mass
# base_in   fraction of the height over which the base pinches in (soft root)
# bulge     the widening just above the root - a flame is not a cone
# wob_*     a slow width modulation: the licked edge, not a cardioid
# lean_*    centre-line: a quadratic lean plus an S-curve, in canvas half-widths
#           (the tongue's own tilt is a transform rotation, so this stays small)
FLAME = {
    # thin tall band: the hottest, whitest tongue - a soft streak, not a spike
    "tongue_a": dict(half=0.88, tip_pow=2.30, taper=0.85, dissolve=0.80, dissolve_from=0.32,
                     feather=1.90, base_in=0.12, base_pow=0.70,
                     bulge_at=0.20, bulge_amp=0.24, bulge_w=0.16,
                     wob_amp=0.10, wob_cells=2.3, wob_phase=0.4,
                     lean_q=0.14, lean_s=0.14, lean_cells=1.15, lean_phase=1.1),
    # middle band: wider, a leaf-shaped tongue with a rounded crown
    "tongue_b": dict(half=0.92, tip_pow=2.20, taper=0.90, dissolve=0.82, dissolve_from=0.30,
                     feather=1.90, base_in=0.16, base_pow=0.75,
                     bulge_at=0.24, bulge_amp=0.30, bulge_w=0.19,
                     wob_amp=0.09, wob_cells=1.9, wob_phase=2.6,
                     lean_q=-0.12, lean_s=0.16, lean_cells=1.05, lean_phase=0.3),
    # outer band: the widest, coolest tongue - body, little tip
    "tongue_c": dict(half=0.94, tip_pow=2.10, taper=0.95, dissolve=0.80, dissolve_from=0.30,
                     feather=1.90, base_in=0.18, base_pow=0.80,
                     bulge_at=0.28, bulge_amp=0.28, bulge_w=0.22,
                     wob_amp=0.08, wob_cells=1.6, wob_phase=4.2,
                     lean_q=0.10, lean_s=0.13, lean_cells=0.95, lean_phase=2.0),
    # core: broad and rounded - the BODY of the fire. The crown above it is not
    # this envelope any more: it is built from CORE_LICKS below (eleventh pass).
    "core": dict(half=0.95, tip_pow=2.00, taper=1.00, dissolve=0.72, dissolve_from=0.34,
                 feather=1.90, base_in=0.20, base_pow=0.85,
                 bulge_at=0.26, bulge_amp=0.20, bulge_w=0.26,
                 wob_amp=0.06, wob_cells=1.3, wob_phase=1.7,
                 lean_q=-0.06, lean_s=0.08, lean_cells=0.85, lean_phase=0.9),
}

# ─── the core's crown (ART-0, eleventh pass) ─────────────────────────────────
# The first core was ONE dome. Measured on the shipped bytes its top edge was a
# single plateau (1 peak, no valley deeper than 2 px), and a fresh eye read of a
# real gallery frame named the result "a smooth light spindle / lamp - no flame
# tongues". A flame's top is a CROWN: two or three licks of different height and
# lean, fused at the body. So the core canvas is composed - the body keeps the
# rounded mass low in the box and each CORE_LICKS entry adds one licked tongue in
# its own y band (`y0..y1`, canvas units, 0 = root) at `x_off` (canvas
# half-widths). The licks are unioned with np.maximum, so the valleys between
# them survive: that union is the read a single dome cannot produce.
CORE_BODY = dict(
    # The body: the widest, flattest part of the fire, sitting on the coal bed.
    # base_in/base_pow keep it wide down to the root (a fire does not taper into
    # the ground), tip_pow/taper close it above the middle so the licks - not a
    # dome - own the crown.
    half=0.72, tip_pow=2.20, taper=1.90, dissolve=0.62, dissolve_from=0.38,
    feather=1.85, base_in=0.05, base_pow=0.50,
    bulge_at=0.20, bulge_amp=0.32, bulge_w=0.30,
    wob_amp=0.08, wob_cells=1.6, wob_phase=1.7,
    lean_q=-0.06, lean_s=0.08, lean_cells=0.85, lean_phase=0.9,
    edge_amp=0.055, edge_cells=2.30, edge_phase=0.7)

# The body's alpha ramps up over the bottom 22 % of the canvas (the tongues keep
# flame_mask's 0.10): a long, wide fade is what makes the base dissolve into the
# coal bed instead of ending in a chevron cut.
CORE_ROOT_FADE = 0.22

# p of the p-norm soft union in `core_mask`: 8 rounds the lick/body crossings
# without flattening the two valleys (which the same union at p = 2 does fill).
SOFT_UNION_P = 8.0

CORE_LICKS = (
    # the tall centre lick - the hottest point of the fire, leaning a little.
    # tip_pow stays 2.40: the thirteenth pass tried 1.75 on it (and 1.70/1.65 on the
    # side licks) to round the tips, and both a 4x canvas read and a blind read of
    # the assembled fire still said "the tips are needles", while the crown's top
    # third measured 10.6 -> 9.2 px wide - narrower, i.e. the change moved away from
    # what it was for. Reverted; the tip read is a proportion question, not a
    # tip_pow one.
    dict(x_off=0.00, y0=0.34, y1=1.00, fade_in=0.30,
         p=dict(half=0.44, tip_pow=2.40, taper=0.90, dissolve=0.52, dissolve_from=0.50,
                feather=1.75, base_in=0.03, base_pow=1.00,
                bulge_at=0.28, bulge_amp=0.14, bulge_w=0.26,
                wob_amp=0.14, wob_cells=2.20, wob_phase=0.5,
                lean_q=0.20, lean_s=0.14, lean_cells=0.80, lean_phase=0.4,
                edge_amp=0.150, edge_cells=2.70, edge_phase=0.30)),
    # the left lick: shorter, and it curls out to its own side
    dict(x_off=-0.40, y0=0.24, y1=0.76, fade_in=0.34,
         p=dict(half=0.34, tip_pow=2.30, taper=0.95, dissolve=0.54, dissolve_from=0.46,
                feather=1.75, base_in=0.03, base_pow=1.00,
                bulge_at=0.30, bulge_amp=0.12, bulge_w=0.24,
                wob_amp=0.12, wob_cells=1.90, wob_phase=2.2,
                lean_q=-0.26, lean_s=-0.20, lean_cells=0.90, lean_phase=1.5,
                edge_amp=0.180, edge_cells=2.20, edge_phase=1.90)),
    # the right lick: the shortest, so the two shoulders do not match
    dict(x_off=0.40, y0=0.24, y1=0.66, fade_in=0.34,
         p=dict(half=0.32, tip_pow=2.30, taper=0.95, dissolve=0.56, dissolve_from=0.42,
                feather=1.75, base_in=0.03, base_pow=1.00,
                bulge_at=0.30, bulge_amp=0.12, bulge_w=0.24,
                wob_amp=0.12, wob_cells=1.80, wob_phase=4.1,
                lean_q=0.24, lean_s=0.16, lean_cells=0.85, lean_phase=2.7,
                edge_amp=0.165, edge_cells=2.50, edge_phase=3.40)),
)

# The tint ramp, mirrored from Campfire.FlameColor / TickTongues.
TONGUE_WHITE = (1.00, 0.97, 0.88)
TONGUE_YELLOW = (1.00, 0.86, 0.28)
TONGUE_ORANGE = (1.00, 0.50, 0.08)
TONGUE_RED = (0.92, 0.16, 0.03)
EMBER_COLOR = (1.00, 0.38, 0.08)
CORE_COLOR = (1.00, 0.94, 0.72)
WOOD_COLOR = (0.50, 0.25, 0.00)
WOOD_ALPHA = 0.50
LOG_ORDER = 12
EMBER_ORDER = 18
CORE_ORDER = 22 + 4
NIGHT_BG = (26, 30, 44)          # yard at night, so a warm picture is judged warm
SOIL = (44, 33, 24)              # the ground line the fire stands on
SOFT_DISC_N = 48                 # Campfire.SoftTexture: const int n = 48
FLICKER_SPEED = 7.1
TONGUE_COUNT = 24


# ─── painting ────────────────────────────────────────────────────────────────

def canvas_size(key):
    bw, bh = BOXES[key]
    h = CANVAS_H[key]
    w = max(8, int(round(h * bw / bh)))
    return w, h


def flame_mask(w, h, p, x_off=0.0, root_fade=0.10):
    """Alpha-only flame: 1 on the centre line, 0 outside the silhouette.

    The top closes as a dome (`1 - v**tip_pow`, whose derivative is 0 at v = 1)
    and the last third loses alpha (`dissolve`), so a tongue fades into the dark
    instead of ending in a point. A pointed, high-contrast tip is what the first
    pass drew, and the eye read 24 of them as glass fibres.

    `x_off` moves the centre line sideways in canvas half-widths; it is what
    places one crown lick (see `lobe`). `root_fade` is the fraction of the height
    over which the alpha ramps up from the root: the tongues keep the original
    0.10, and the core's body asks for a longer one so its base dissolves into
    the coal bed instead of ending in a V wedge (the base_in pinch alone draws
    that wedge - a measured eye read of the 4x canvas named it twice).
    """
    ys = (np.arange(h, dtype=np.float64) + 0.5) / h          # 0 = root, 1 = tip
    us = (np.arange(w, dtype=np.float64) + 0.5) / w * 2.0 - 1.0   # -1..1 half-width

    base = np.clip(ys / p["base_in"], 0.0, 1.0) ** p["base_pow"]
    env = (1.0 - ys ** p["tip_pow"]) ** p["taper"]
    bulge = 1.0 + p["bulge_amp"] * np.exp(-((ys - p["bulge_at"]) / p["bulge_w"]) ** 2)
    wob = 1.0 + p["wob_amp"] * np.sin(2.0 * math.pi * p["wob_cells"] * ys + p["wob_phase"])
    half = p["half"] * base * env * bulge * wob

    # A living flame's flank is not a smooth curve (ART-0, thirteenth pass). The
    # crown was already licked, but every flank of every lick was a clean arc, and
    # a fresh read of a real frame named the result "a glowing crystal": smooth
    # contour, no torn edge. `edge_amp` ripples the half-width with two harmonics
    # (the second at 2.37x the first and 1.71x its phase, so the pattern never
    # repeats inside one band), windowed to zero at the root - a fire's base is a
    # wide calm mass, not a fringe - and to zero over the last 12 % of the band,
    # where `dissolve` already owns the outline. Default 0: the three tongue
    # canvases keep the exact shape they were rejected with.
    e_amp = p.get("edge_amp", 0.0)
    if e_amp > 0.0:
        c1 = p["edge_cells"]
        ph = p["edge_phase"]
        rip = (0.62 * np.sin(2.0 * math.pi * c1 * ys + ph)
               + 0.38 * np.sin(2.0 * math.pi * c1 * 2.37 * ys + ph * 1.71))
        win = np.clip(ys / 0.18, 0.0, 1.0) * np.clip((1.0 - ys) / 0.12, 0.0, 1.0)
        half = half * (1.0 + e_amp * rip * win)

    cx = x_off + p["lean_q"] * ys ** 2 + p["lean_s"] * np.sin(
        math.pi * p["lean_cells"] * ys + p["lean_phase"]) * ys ** 0.8

    t = np.abs(us[None, :] - cx[:, None]) / np.maximum(half, 1e-6)[:, None]
    a = np.clip(1.0 - t * t, 0.0, 1.0) ** p["feather"]    # bright centre, soft edge

    # the crown dissolves: the flame body stays solid, the licked top goes translucent
    d = np.clip((ys - p["dissolve_from"]) / max(1e-6, 1.0 - p["dissolve_from"]), 0.0, 1.0)
    a *= 1.0 - p["dissolve"] * (d * d * (3.0 - 2.0 * d))[:, None]

    a *= np.clip(ys / root_fade, 0.0, 1.0)[:, None]       # no hard cut at the root

    # Row 0 of a PIL array is the TOP of the PNG, and a flame's root sits at the
    # BOTTOM of its box (the coal bed is below it), while `ys` measures height
    # ABOVE the root - so the field is flipped on the way out. Without this the
    # sprite is drawn as an upside-down drop: measured in a real gallery frame,
    # the shipped core was 23 px wide in rows 17..19 of 68 and 5 px at its bottom
    # row, i.e. the body sat at the top and the point pointed at the ground.
    return np.clip(a, 0.0, 1.0)[::-1]


def lobe(w, h, p, y0, y1, x_off=0.0, fade_in=0.30):
    """One crown lick: `flame_mask`'s shape squeezed into the y band y0..y1.

    `y0`/`y1` are canvas heights ABOVE THE ROOT (0 = the coal bed, 1 = the top of
    the canvas), and `flame_mask` returns rows top-first, so the band is pasted
    at the mirrored rows.

    `fade_in` is the fraction of the band over which the lick rises out of the
    body. A lick unioned at full strength makes a flat shelf where it leaves the
    body (a hard corner in the outline - the first read of the licks called it
    "rough flat cuts at the base of the side tongues"), so its lower end is
    ramped in instead: the union with `np.maximum` then hands over gradually.
    """
    r0 = int(round((1.0 - y1) * h))
    r1 = int(round((1.0 - y0) * h))
    hb = max(4, r1 - r0)
    band = flame_mask(w, hb, p, x_off)
    # `flame_mask` returns rows top-first, so the ramp is indexed from the LAST
    # row: t = 0 at the lick's own root, 1 further up. Ramping from row 0 would
    # fade the TIP instead and leave the flat shelf at the base - measured as
    # "detached plates at the base of the side tongues".
    t = np.clip((hb - 1 - np.arange(hb, dtype=np.float64)) / max(1e-6, fade_in * hb), 0.0, 1.0)
    band *= (t * t * (3.0 - 2.0 * t))[:, None]        # smoothstep: no shelf
    out = np.zeros((h, w), dtype=np.float64)
    out[r0:r0 + hb, :] = band
    return out


def core_mask(w, h):
    """The core: the body plus the licked crown, fused (ART-0, eleventh pass).

    A plain `np.maximum` unions the licks with the right valleys between them, but
    the outline crosses at a CORNER where a lick leaves the body, and the eye read
    that corner as a flat shelf ("detached plates"). A SOFT union - the p-norm
    (a^p + b^p)^(1/p) with p = SOFT_UNION_P - rounds exactly that crossing and
    leaves the valleys alone, because a valley is where the body, not a lick,
    owns the alpha.
    """
    a = flame_mask(w, h, CORE_BODY, 0.0, CORE_ROOT_FADE)
    for lick in CORE_LICKS:
        band = lobe(w, h, lick["p"], lick["y0"], lick["y1"], lick["x_off"], lick["fade_in"])
        a = (a ** SOFT_UNION_P + band ** SOFT_UNION_P) ** (1.0 / SOFT_UNION_P)
    return np.clip(a, 0.0, 1.0)


# ─── the core's own colour (ART-0, thirteenth pass) ─────────────────────────
# Until this pass `paint` wrote RGB = 255 into every canvas and the core reached
# the screen as flat white modulated by Campfire.CoreTint (1, 0.94, 0.72) - one
# pale cream, top to bottom. Two fresh reads of a real frame measured the result
# the same way: "there are no orange or red shades at all", the palette was
# cream / pastel yellow / white only, and the fire read as a glowing crystal.
# The colour belongs in the picture, so the core canvas now carries the same
# white -> yellow -> orange -> red ramp the procedural tongues are tinted with
# (`flame_color`, kept as the single source of the fire palette), driven by a heat
# field built from the alpha the eleventh pass already fixed:
#
#   hot  = how close the pixel is to the spine   (alpha ** 0.5, 0 at the flank)
#   root = how low it sits in the box            (1 at the coal bed, 0 at the crown)
#   q    = 1 - hot * (1 - CORE_ROOT_BIAS + CORE_ROOT_BIAS * root)
#
# q = 0 on the spine low down (white hot), ~0.45 on the spine at the crown
# (yellow), 1 on the coldest flank (amber-orange). Only RGB changes: the alpha
# channel is byte-identical to the eleventh pass's canvas, so the silhouette this
# pass is judged on is the one already accepted.
CORE_HEAT_MIN = 0.05      # not pure white even at the hottest pixel: a flame has
                          # no blown-out core, and 0 is where the ramp is softest
CORE_HEAT_MAX = 0.82      # the coldest rim stops short of red (1.0): an amber
                          # rim reads as fire, a red one as a hazard light
CORE_ROOT_BIAS = 0.45     # how much of the heat comes from the height rather
                          # than from the distance to the spine

# ON, and the way it was decided is the point. The ramp was first judged by two
# NAMED reads (LEFT = shipped, RIGHT = candidate) of the assembled fire and of the
# 4x canvas; both preferred the shipped flat white, and on that evidence the pass
# almost reverted. Blind reads of the same two moments then called it a tie in the
# assembly, and the decisive one - a blind 5x window A/B of the gallery's FIRE row
# from two real player builds (art0flame2 = white, art0flame2h = this ramp) - read
# the ramp side as "clearly the more believable fire: the warm gradient gold ->
# orange repeats how wood burns", and the shipped white side as "a cream-pale
# glowing object, an overexposed silhouette, a neon spell effect - it would look
# out of place next to painted grass, wood and cloth". That is the original
# complaint, answered on the frame.
#
# Why it is right and not a taste flip: the core canvas is drawn ALONE in the
# gallery's fire row (0.16 x 0.38 m, 40x121 px there), so in that row the canvas
# IS the whole colour of the fire, and a white canvas can only ever be a pale
# cream under CoreTint. In the assembled campfire the same canvas sits in FRONT of
# 24 procedural tongue discs whose tints carry the white -> yellow -> orange -> red
# ramp, and there the blind reads saw no difference - colouring the carrier costs
# nothing to the hot centre the discs provide.
#
# The flat-white path stays one line away (CORE_HEAT_RAMP = False) for the record:
# white_ripple_ab/ in Trials/Runs/art-flamecore-1909m/ holds the shape-only variant.
CORE_HEAT_RAMP = True


def core_heat(alpha):
    """The core canvas's heat field: 0 = hottest (white), 1 = coolest (amber)."""
    h = alpha.shape[0]
    rows = (np.arange(h, dtype=np.float64) + 0.5) / h     # 0 = top of the PNG
    root = (1.0 - rows)[:, None]                         # 1 = coal bed, 0 = crown
    hot = np.clip(alpha, 0.0, 1.0) ** 0.5
    q = 1.0 - hot * (1.0 - CORE_ROOT_BIAS + CORE_ROOT_BIAS * root)
    return np.clip(CORE_HEAT_MIN + (CORE_HEAT_MAX - CORE_HEAT_MIN) * q, 0.0, 1.0)


# ─── the logs (ART-0, nineteenth pass) ───────────────────────────────────────
# BuildLogs draws each log through HumanSegment.ApplyPicture: a collider-sized
# white rectangle tinted LogWood = (0.50, 0.25, 0.00, 0.5) - a flat
# HALF-TRANSPARENT brown box. A fresh read of the shipped FIRE row named exactly
# that ("flat semi-transparent brown rectangles, no bark, no cut ends, you can see
# their intersections"), which is Yury's original complaint in miniature. The
# canvases below are the logs' picture: an opaque wood cylinder with a bark top, a
# sawn end face and grain, in tones three to four steps apart (the flat language;
# the flame's ramp is the only gradient in this file).
LOG_CHAR = (48, 30, 20)         # the top the fire has eaten
LOG_BARK = (84, 54, 32)
LOG_BARK_DARK = (64, 40, 25)
LOG_WOOD_DARK = (106, 66, 36)
LOG_WOOD = (142, 92, 48)
LOG_WOOD_LIT = (180, 124, 70)
LOG_GRAIN = (74, 45, 25)
LOG_END = (198, 162, 112)       # the sawn face
LOG_END_RING = (156, 114, 66)
LOG_END_RIM = (104, 70, 42)
LOG_CRACK = (44, 26, 16)

# The engine's OWN log picture, for the A/B sheets: HumanSegment.CreateBodySprite
# is a hard white rectangle tinted this colour at this alpha - not the soft disc
# this tool drew into the sheets before the nineteenth pass.
LOG_WOOD_FRAC = (0.50, 0.25, 0.00)
LOG_WOOD_ALPHA = 0.50


def hard_box(rgb, alpha, px=96):
    """HumanSegment.CreateBodySprite + SpriteRenderer.color: a flat hard rectangle."""
    return Image.new("RGBA", (px, px),
                     (int(round(rgb[0] * 255)), int(round(rgb[1] * 255)),
                      int(round(rgb[2] * 255)), int(round(alpha * 255))))


def paint_log(w, h, key):
    """One log, side view: body, bark, sawn end face, grain.

    The canvas is the log's own box aspect, so the Sliced mapping (ArtParts.Dress
    with tileMeters 0 = one repeat across the part) puts it on the box without
    stretching it.

    Judged at game scale (340 px/m, `logs_ab_box.png`) and at the gallery's closest
    lens, then corrected once on the read: the first version's wood was a coarse
    bicubic mottle (soapy blobs), the grain lines were near-black wires, the bark
    was three flat bands with no grooves and the 5 % taper made the far end a
    pencil point. Now: a two-lattice mottle quantised to three tones, grain grooves
    that are a 38-level step with a lit lip, bark with grooves and a few knots, and
    a cut end that ends square.
    """
    rng = random.Random(20260920 + sum(ord(c) for c in key))
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float64)
    u = xx / float(max(1, w - 1))
    cy = 0.5 * (h - 1)

    # the cylinder: 2 % thinner at the far end (a log, not a pencil), ends rounded
    half = (0.5 * h - 0.7) * (1.0 - 0.02 * u)
    half = half * np.minimum(np.clip(u / 0.045, 0.0, 1.0) ** 0.55,
                             np.clip((1.0 - u) / 0.030, 0.0, 1.0) ** 0.55)
    dy = np.abs(yy - cy)
    alpha = np.clip(half - dy + 0.5, 0.0, 1.0)
    # 0 at the log's underside, 1 at its top
    across = ((cy + half) - yy) / np.maximum(2.0 * half, 1e-6)

    # the wood: a fine lattice for the grain and a coarse one for the tone, summed
    # and quantised into three flat steps (the flat language: no gradient in a
    # silhouette). One lattice alone gave ~10 px blobs that read as airbrush.
    def lattice(nx, ny):
        im = Image.new("L", (nx, ny))
        im.putdata([int(255 * rng.random()) for _ in range(nx * ny)])
        return np.asarray(im.resize((w, h), Image.BICUBIC), dtype=np.float64) / 255.0

    m = 0.62 * lattice(48, 12) + 0.38 * lattice(12, 3)
    rgb = np.zeros((h, w, 3), dtype=np.float64)
    rgb[:, :] = LOG_WOOD
    rgb[m < 0.43] = LOG_WOOD_DARK
    rgb[m > 0.61] = LOG_WOOD_LIT

    # grain: four grooves along the length, each a soft step with a lit lip
    for a_at, cells, ph, lip in ((0.30, 1.7, 0.4, True), (0.45, 2.3, 2.1, True),
                                 (0.60, 1.3, 3.7, False), (0.74, 2.9, 5.0, True)):
        line = cy + half * (1.0 - 2.0 * a_at) + 1.1 * np.sin(2.0 * math.pi * cells * u + ph)
        near = np.abs(yy - line)
        rgb[near <= 0.55] = LOG_GRAIN
        if lip:
            rgb[(near > 0.55) & (near <= 1.15)] = LOG_WOOD_LIT

    # two splits (checks): short, dark, not a scratch down the whole log
    for a_at, u0, u1 in ((0.62, 0.13, 0.44), (0.36, 0.56, 0.92)):
        line = cy + half * (1.0 - 2.0 * a_at) + 1.1 * np.sin(2.0 * math.pi * 2.2 * u + 1.3)
        rgb[(u >= u0) & (u <= u1) & (np.abs(yy - line) <= 0.75)] = LOG_CRACK

    # bark: an irregular top band, charred at the very top, shadow underneath
    bark_from = (0.68 + 0.055 * np.sin(2.0 * math.pi * 1.9 * u + 0.7)
                 + 0.030 * np.sin(2.0 * math.pi * 4.1 * u + 2.2))
    rgb[across > bark_from] = LOG_BARK
    rgb[across > bark_from + 0.14] = LOG_BARK_DARK
    rgb[across > bark_from + 0.30] = LOG_CHAR
    rgb[across < 0.13] = LOG_BARK_DARK
    rgb[across < 0.05] = LOG_CHAR

    # bark grooves and knots: 1 px ticks in the band and one ringed knot, dealt at
    # random positions (seeded), because bark is the one thing a flat band cannot say
    ticks = max(6, w // 16)
    for _ in range(ticks):
        tx = rng.randrange(2, max(3, w - 2))
        ty = int(round(cy - half[0, tx] * (0.80 + 0.16 * rng.random())))
        tl = 2 + rng.randrange(3)
        rgb[max(0, ty):ty + tl, max(0, tx - 1):tx + 1] = LOG_BARK_DARK
    for kx, ky in ((0.42, 0.52), (0.71, 0.40)):
        kxc = kx * (w - 1)
        kyc = cy + (0.5 * h) * (0.5 - ky) * 0.9
        e = ((xx - kxc) / max(1.6, 0.016 * w)) ** 2 + ((yy - kyc) / max(1.6, 0.10 * h)) ** 2
        rgb[e <= 1.0] = LOG_GRAIN
        rgb[e <= 0.55] = LOG_CRACK
        rgb[e <= 0.22] = LOG_WOOD_LIT

    # the sawn end face: rings, the cut bark edge, two or three radial checks
    fx = 0.058 * (w - 1)
    rx = max(1.2, (0.5 * h - 0.7) * 0.30)
    ry = max(1.2, (0.5 * h - 0.7) * 0.94)
    e = ((xx - fx) / rx) ** 2 + ((yy - cy) / ry) ** 2
    face = e <= 1.0
    r = np.sqrt(np.clip(e, 0.0, 1.0))
    rgb[face] = LOG_END
    rgb[face & ((np.floor(r * 5.0) % 2.0) == 1.0)] = LOG_END_RING
    rgb[face & (r > 0.80)] = LOG_END_RIM
    for deg in (34.0, -58.0, 128.0):
        rad = math.radians(deg)
        s = (xx - fx) * math.sin(rad) - (yy - cy) * math.cos(rad)
        along = (xx - fx) * math.cos(rad) + (yy - cy) * math.sin(rad)
        rgb[face & (np.abs(s) < 0.6) & (along > 0.6)] = LOG_CRACK

    # the far end is a cut, not a point: a dark rim on the last columns
    rgb[:, max(0, w - 2):] = LOG_BARK_DARK
    rgb[:, max(0, w - 1):] = LOG_CRACK

    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[:, :, :3] = np.round(np.clip(rgb, 0.0, 255.0)).astype(np.uint8)
    rgba[:, :, 3] = np.round(alpha * 255.0).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def paint(key):
    """The canvas: the shape in alpha, and - for the core - the heat ramp in RGB.

    The three tongue canvases keep the flat white on purpose: they are tinted per
    tongue at runtime from the age ramp (`flame_color(u, heat)`), so their own RGB
    would be multiplied away and painting them would buy nothing. They are not
    written either (see `main`).
    """
    w, h = canvas_size(key)
    if key.startswith("log_"):
        return paint_log(w, h, key)
    a = core_mask(w, h) if key == "core" else flame_mask(w, h, FLAME[key])
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    if key == "core" and CORE_HEAT_RAMP:
        lut = np.array([soften(flame_color(i / 255.0, 1.0)) for i in range(256)],
                       dtype=np.uint8)
        rgba[:, :, :3] = lut[np.round(core_heat(a) * 255.0).astype(np.uint8)]
    else:
        rgba[:, :, :3] = 255
    rgba[:, :, 3] = np.round(a * 255.0).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


# ─── import settings (same shape as Tools/gen-level-textures.py) ─────────────

def new_guid(rng):
    return "".join(rng.choice("0123456789abcdef") for _ in range(32))


def write_meta(png_path, pixels_per_unit, readable, alpha, guid):
    with open(META_TEMPLATE, "r", encoding="utf-8") as fh:
        text = fh.read()

    def set_line(key, value, indent=2):
        nonlocal text
        pattern = re.compile(r"^%s%s:.*$" % (" " * indent, re.escape(key)), re.MULTILINE)
        assert pattern.search(text), "template has no %s" % key
        text = pattern.sub("%s%s: %s" % (" " * indent, key, value), text, count=1)

    text = re.sub(r"^guid: .*$", "guid: " + guid, text, count=1, flags=re.MULTILINE)
    set_line("spriteMeshType", 0)            # FullRect: Sliced / Tiled draw modes need it
    set_line("spritePixelsToUnits", pixels_per_unit)
    set_line("isReadable", 1 if readable else 0)
    set_line("wrapU", 1, indent=4)
    set_line("wrapV", 1, indent=4)
    set_line("alphaIsTransparency", 1 if alpha else 0)
    set_line("spriteGenerateFallbackPhysicsShape", 0)   # art never carries a collider
    set_line("textureType", 8)               # Sprite
    set_line("spriteMode", 1)                # Single
    set_line("spriteID", guid[:16] + "0800000000000000", indent=4)
    text = re.sub(r"^\s*nameFileIdTable: .*$", "    nameFileIdTable: {}", text, count=1,
                  flags=re.MULTILINE)
    with open(png_path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def ensure_folder_meta(folder, rng):
    meta = folder + ".meta"
    if os.path.exists(meta):
        return False
    with open(FOLDER_TEMPLATE, "r", encoding="utf-8") as fh:
        text = fh.read()
    text = re.sub(r"^guid: .*$", "guid: " + new_guid(rng), text, count=1, flags=re.MULTILINE)
    with open(meta, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    return True


def save(img, path, pixels_per_unit, readable, alpha, guid_rng):
    img.save(path, "PNG", optimize=True)
    write_meta(path, pixels_per_unit, readable, alpha, new_guid(guid_rng))
    return os.path.getsize(path)


# ─── the fire, mirrored from Campfire.Build / BuildTongues / TickTongues ─────

def perlin01(x, y):
    """Stand-in for Mathf.PerlinNoise: the A/B feeds BOTH pictures the same value."""
    def fade(t):
        return t * t * t * (t * (t * 6 - 15) + 10)

    def grad(ix, iy):
        n = (ix * 374761393 + iy * 668265263) & 0xFFFFFFFF
        n = (n ^ (n >> 13)) * 1274126177 & 0xFFFFFFFF
        return ((n & 0xFFFF) / 32767.5) - 1.0

    xi, yi = int(math.floor(x)), int(math.floor(y))
    xf, yf = x - xi, y - yi
    u, v = fade(xf), fade(yf)
    n00, n10, n01, n11 = grad(xi, yi), grad(xi + 1, yi), grad(xi, yi + 1), grad(xi + 1, yi + 1)
    return 0.5 + 0.5 * ((n00 * (1 - u) + n10 * u) * (1 - v) + (n01 * (1 - u) + n11 * u) * v)


def tongue_pool():
    """i -> (band, w, h, spread, heat, life, phase, lean, x0, rise) - BuildTongues."""
    pool = []
    for i in range(TONGUE_COUNT):
        band = i % 3
        if band == 0:
            w, h = 0.055, 0.46
        elif band == 1:
            w, h = 0.080, 0.38
        else:
            w, h = 0.110, 0.30
        spread = ((i // 3) - 3.5) * 0.028
        pool.append(dict(
            band=band, w=w, h=h, heat=(1.0 if band == 0 else (0.62 if band == 1 else 0.28)),
            life=0.38 + (i % 7) * 0.07, phase=i * 1.618,
            lean=-24.0 + (i % 9) * 6.0, x0=spread,
            rise=0.30 + band * 0.07 + (i % 5) * 0.03,
            age=(0.38 + (i % 7) * 0.07) * (i / float(TONGUE_COUNT))))
    return pool


def flame_color(u, heat):
    if u < 0.22:
        c = mix(TONGUE_WHITE, TONGUE_YELLOW, u / 0.22)
    elif u < 0.55:
        c = mix(TONGUE_YELLOW, TONGUE_ORANGE, (u - 0.22) / 0.33)
    else:
        c = mix(TONGUE_ORANGE, TONGUE_RED, (u - 0.55) / 0.45)
    return mix(c, TONGUE_RED, 0.28 * (1.0 - heat))


def mix(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def soften(c):
    """sRGB byte from the ramp's float colour (the sprite renderer does the same)."""
    return tuple(int(round(max(0.0, min(1.0, v)) * 255.0)) for v in c)


def step_ages(pool, t_from, t_to, dt=1.0 / 60.0):
    """Integrate TickTongues' age wrap from t_from to t_to (dt = one 60 Hz frame)."""
    n = int(round((t_to - t_from) / dt))
    for _ in range(n):
        for tp in pool:
            tp["age"] += dt
            if tp["age"] >= tp["life"]:
                tp["age"] -= tp["life"]
    return pool


def soft_disc(n=SOFT_DISC_N):
    """Campfire.SoftTexture, exactly: a = clamp01(1 - d)^2, d = nx^2 + ny^2."""
    c = (n - 1) * 0.5
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float64)
    nx = (xs - c) / c
    ny = (ys - c) / c
    d = nx * nx + ny * ny
    a = np.where(d >= 1.0, 0.0, np.clip(1.0 - d, 0.0, 1.0)) ** 2
    rgba = np.zeros((n, n, 4), dtype=np.uint8)
    rgba[:, :, :3] = 255
    rgba[:, :, 3] = np.round(a * 255.0).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def tinted(sprite, rgb, alpha):
    """SpriteRenderer: src = vertexColor * texture, straight alpha.

    The MULTIPLICATION is the point: a SpriteRenderer's `color` is a vertex
    colour that modulates the texture, it does not replace it. Written as a
    replace, this tool was exact only while every canvas was flat white (RGB
    255, the shape in alpha) - the tint was then the whole colour and nobody
    could see the difference. The thirteenth pass gives the core canvas its own
    heat ramp in RGB, and with `rgb` written straight through the A/B sheets
    would have drawn the OLD and the NEW core identically white: the evidence
    would have been blind to the change it exists to judge.
    """
    a = np.asarray(sprite.convert("RGBA"), dtype=np.float64) / 255.0
    out = np.zeros_like(a)
    out[:, :, 0] = rgb[0] / 255.0 * a[:, :, 0]
    out[:, :, 1] = rgb[1] / 255.0 * a[:, :, 1]
    out[:, :, 2] = rgb[2] / 255.0 * a[:, :, 2]
    out[:, :, 3] = np.clip(a[:, :, 3] * alpha, 0.0, 1.0)
    return Image.fromarray((out * 255.0).astype(np.uint8), "RGBA")


def paste_scaled(frame, sprite, box_w, box_h, center_world, origin_px, px_per_m, angle_deg):
    """One SpriteRenderer: rotate, then scale the 1 m sprite onto its box, then paste."""
    w_px = max(1, int(round(box_w * px_per_m)))
    h_px = max(1, int(round(box_h * px_per_m)))
    img = sprite
    if abs(angle_deg) > 0.01:
        img = img.rotate(angle_deg, resample=Image.BICUBIC, expand=True)
    img = img.resize((w_px, h_px), Image.LANCZOS)
    cx = origin_px[0] + center_world[0] * px_per_m
    cy = origin_px[1] - center_world[1] * px_per_m
    frame.alpha_composite(img, (int(round(cx - w_px * 0.5)), int(round(cy - h_px * 0.5))))


def render_fire(t_state, paint_keys, painted_sprites, px_per_m=300.0, pad=0.62):
    """The whole campfire at one moment, one camera scale, over the night yard.

    `paint_keys` names the picture each sprite is drawn with: a key in the set
    uses the painted canvas, anything else keeps Campfire's soft disc. That is
    how the two candidate fixes are compared - all four canvases, or the core
    alone - without touching the tool's shape parameters.
    """
    pool = tongue_pool()
    step_ages(pool, 0.0, t_state)
    t = t_state
    n = perlin01(t * FLICKER_SPEED, 11.3)
    n2 = perlin01(11.3, t * FLICKER_SPEED * 1.37)

    w_m = 2.4
    h_m = 2.2
    W = int(round(w_m * px_per_m))
    H = int(round(h_m * px_per_m))
    frame = Image.new("RGBA", (W, H), NIGHT_BG + (255,))
    origin = (W // 2, int(round(H - pad * px_per_m)))
    # the ground line: everything below the fire root is soil, so a tongue that dips
    # under the bed is judged the way the scene shows it, not as a stroke on nothing
    soil = Image.new("RGBA", (W, H - origin[1]), SOIL + (255,))
    frame.alpha_composite(soil, (0, origin[1]))

    disc = soft_disc()

    def picture(key, box):
        if painted:
            return tinted(painted_sprites[key], (255, 255, 255), 1.0), key
        return tinted(disc, (255, 255, 255), 1.0), "disc"

    # logs (BuildLogs), and the two pictures a log can have: the engine's own - a
    # HARD rectangle in LogWood at alpha 0.5 (HumanSegment.CreateBodySprite; the
    # soft translucent disc this sheet drew before the nineteenth pass was not the
    # picture the build shows) - and the painted canvas, keyed log_a/log_b/log_c
    # and mapped Sliced onto the same box.
    for key, (bw, bh), (lx, ly), tilt in (
            ("log_a", (0.62, 0.14), (-0.04, 0.07), -18.0),
            ("log_b", (0.54, 0.13), (0.07, 0.065), 16.0),
            ("log_c", (0.30, 0.12), (0.00, 0.16), 7.0)):
        carrier = (painted_sprites[key] if key in paint_keys
                   else hard_box(LOG_WOOD_FRAC, LOG_WOOD_ALPHA))
        paste_scaled(frame, carrier, bw, bh, (lx, ly), origin, px_per_m, tilt)

    # ember bed (0.18 x 0.24 at y 0.18) - kept procedural, a coal bed HAS no silhouette
    ember = tinted(disc, soften(EMBER_COLOR), 1.0)
    paste_scaled(frame, ember, 0.18 * (0.88 + n * 0.28), 0.24 * (0.90 + n2 * 0.25),
                 (0.0, 0.18), origin, px_per_m, 0.0)

    # the 24 tongues, in sortingOrder order: band 2 (Fire+1) first, core last
    order = sorted(pool, key=lambda tp: (-tp["band"], tp["phase"]))
    for tp in order:
        u = tp["age"] / tp["life"]
        flicker = 0.80 + n * 0.26 + n2 * 0.10 * math.sin(t * 13.5 + tp["phase"])
        fade = (u / 0.08) if u < 0.08 else (1.0 - (u - 0.48) / 0.52 if u > 0.48 else 1.0)
        fade = max(0.0, fade)
        y = 0.16 + u * tp["rise"]
        x = tp["x0"] + math.sin(t * 8.4 + tp["phase"]) * 0.024 * (0.35 + u)
        rot = tp["lean"] * (0.50 + 0.50 * math.sin(t * 6.1 + tp["phase"]))
        sx = flicker * (1.12 - u * 0.78)
        sy = flicker * (1.06 - u * 0.28)
        rgb = soften(flame_color(u, tp["heat"]))
        alpha = fade * (0.55 + (1.0 - u) * 0.45)
        if alpha <= 0.03:
            continue
        key = {0: "tongue_a", 1: "tongue_b", 2: "tongue_c"}[tp["band"]]
        carrier = painted_sprites[key] if key in paint_keys else disc
        paste_scaled(frame, tinted(carrier, rgb, alpha),
                     tp["w"] * sx, tp["h"] * sy, (x, y), origin, px_per_m, rot)
    # the core, drawn in front (FireOrder + 4)
    core_carrier = painted_sprites["core"] if "core" in paint_keys else disc
    paste_scaled(frame, tinted(core_carrier, soften(CORE_COLOR), 0.82 + n * 0.10),
                 0.16 * (0.82 + n * 0.32), 0.38 * (0.86 + n2 * 0.34),
                 (0.0, 0.36), origin, px_per_m, (n - 0.5) * 14.0)
    return frame


def main():
    rng = random.Random(20260919)
    os.makedirs(FIRE_DIR, exist_ok=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    # SHIPPED: the core only. The three tongue canvases are still built - in memory -
    # because the evidence sheets compare them, but they are NOT written: three blind
    # reviews read 24 painted tongues as separate spindles ("comb teeth", "glass
    # fibres") next to the soft procedural discs, and a worse picture is not a
    # delivery (ART-0 brief: "a labelled MISSING-ART tile is better than a bad
    # texture pretending to be final"). The core is one sprite in front: there the
    # silhouette has room to read, and the review picked it as the best of four.
    shipped = ("core", "log_a", "log_b", "log_c")
    rows = []
    for key in shipped:
        img = paint(key)
        path = os.path.join(FIRE_DIR, key + ".png")
        bw, bh = BOXES[key]
        # The core becomes a 1 m sprite (Campfire.CoreSprite: ppu = tex.width) and
        # is scaled to its box by localScale. A log is dressed Sliced onto its own
        # box (ArtParts.Dress with tileMeters 0 -> ArtLibrary.Tiled recomputes
        # ppu = width / box.x), so the .meta carries the log's own px/m and the
        # imported sprite is exactly the log box.
        ppu = img.width if key == "core" else int(round(img.width / bw))
        size = save(img, path, ppu, True, True, rng)
        a = np.asarray(img)[:, :, 3].astype(np.float64) / 255.0
        rows.append((key, img.width, img.height, bw, bh,
                     img.width / float(img.height) / (bw / bh), 100.0 * a.mean(),
                     100.0 * (a >= 0.999).mean(), size))
    wrote_folder = ensure_folder_meta(FIRE_DIR, rng)

    print("painted canvases written to Resources/Art/Campfire/")
    print("  %-9s %-9s %-13s %-9s %-9s %-9s %s"
          % ("key", "canvas", "box m", "aspect", "coverage", "opaque", "bytes"))
    for key, w, h, bw, bh, aspect, cov, opaque, size in rows:
        print("  %-9s %-9dx%d %-13s %-9.4f %-9.1f %-9.1f %d"
              % (key, w, h, "%.3f x %.3f" % (bw, bh), aspect, cov, opaque, size))
    print("  folder meta written: %s" % wrote_folder)
    print("  rejected, evidence only, NOT written: tongue_a tongue_b tongue_c")

    # ── the pictures ──
    t0 = 12.0
    painted_sprites = {k: paint(k) for k in BOXES}
    all_keys = set(BOXES)
    core_only = {"core"}
    logs_only = {"log_a", "log_b", "log_c"}
    nothing = set()
    sheets = []

    # four variants of the same moment: 1 = procedural (shipped until the 13th
    # pass), 2 = core painted only, 3 = logs painted only, 4 = everything painted
    for pxm in (300.0, 150.0):
        tiles = [render_fire(t0, keys, painted_sprites, pxm)
                 for keys in (nothing, core_only, logs_only, all_keys)]
        sheet = Image.new("RGBA", (sum(t.width for t in tiles), tiles[0].height),
                          NIGHT_BG + (255,))
        x = 0
        for tile in tiles:
            sheet.alpha_composite(tile, (x, 0))
            x += tile.width
        p = os.path.join(OUT_DIR, "flame_variants_%dpxm.png" % int(pxm))
        sheet.convert("RGB").save(p)
        sheets.append(p)

    # the blind 2x2: two moments, procedural vs core-painted, neutral labels
    cells = []
    for t_state in (t0, t0 + 0.35):
        cells.append(render_fire(t_state, nothing, painted_sprites, 220.0))    # procedural
        cells.append(render_fire(t_state, core_only, painted_sprites, 220.0))  # core painted
    order = [0, 2, 3, 1]
    col_w = max(c.width for c in cells)
    row_h = max(c.height for c in cells)
    blind = Image.new("RGBA", (col_w * 2, row_h * 2), NIGHT_BG + (255,))
    labels = ["A", "B", "C", "D"]
    mapping = {}
    for slot, idx in enumerate(order):
        blind.alpha_composite(cells[idx], ((slot % 2) * col_w, (slot // 2) * row_h))
        mapping[labels[slot]] = ("procedural" if idx % 2 == 0 else "core-painted",
                                 "t=%.2f" % (t0 if idx < 2 else t0 + 0.35))
    blind.convert("RGB").save(os.path.join(OUT_DIR, "fire_blind_2x2.png"))
    with open(os.path.join(OUT_DIR, "fire_blind_2x2_map.txt"), "w", encoding="utf-8",
              newline="\n") as fh:
        for k in labels:
            fh.write("%s = %s %s\n" % (k, mapping[k][0], mapping[k][1]))

    # The logs' own pair: ONE moment, ONE camera, two panels - the engine's flat
    # translucent boxes on one side and the painted logs on the other. Which side
    # is which is random and goes to a separate map file the reader opens AFTER
    # the pick: a named panel hands the reader the answer it is expected to find
    # (the lesson of the thirteenth pass, kept in this tool on purpose).
    side_rng = random.Random(20260921)
    left_keys, right_keys = (nothing, logs_only)
    if side_rng.random() < 0.5:
        left_keys, right_keys = right_keys, left_keys
    pair = [render_fire(t0, left_keys, painted_sprites, 220.0),
            render_fire(t0, right_keys, painted_sprites, 220.0)]
    pw = pair[0].width
    ph = pair[0].height
    gap = 8
    sheet = Image.new("RGBA", (pw * 2 + gap, ph), (128, 128, 128, 255))
    sheet.alpha_composite(pair[0], (0, 0))
    sheet.alpha_composite(pair[1], (pw + gap, 0))
    sheet.convert("RGB").save(os.path.join(OUT_DIR, "logs_blind_pair.png"))
    with open(os.path.join(OUT_DIR, "logs_blind_pair_map.txt"), "w", encoding="utf-8",
              newline="\n") as fh:
        fh.write("t=%.2f  camera=220 px/m  gap=%d px\n" % (t0, gap))
        fh.write("LEFT  = %s\n" % ("painted logs" if left_keys is logs_only else "flat wood boxes"))
        fh.write("RIGHT = %s\n" % ("painted logs" if right_keys is logs_only else "flat wood boxes"))
    sheets.append(os.path.join(OUT_DIR, "logs_blind_pair.png"))

    # native resolution strip
    strip = Image.new("RGBA", (sum(p.width for p in painted_sprites.values()) + 70,
                               max(p.height for p in painted_sprites.values())), (0, 0, 0, 0))
    x = 0
    for k in ("tongue_a", "tongue_b", "tongue_c", "core", "log_a", "log_b", "log_c"):
        strip.alpha_composite(painted_sprites[k], (x, 0))
        x += painted_sprites[k].width + 10
    strip.convert("RGB").save(os.path.join(OUT_DIR, "canvases_native.png"))
    print("\nevidence -> %s" % OUT_DIR)
    for p in sheets:
        print("  " + os.path.basename(p))


if __name__ == "__main__":
    main()
