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
}
# Canvas heights. The tallest box is 0.46 m: at the gallery's closest lens
# (orthoSize 1.304 -> 414 px/m) that is 190 px on screen, so 256 px of canvas is
# the last useful step before the picture costs bytes and buys nothing.
CANVAS_H = {"tongue_a": 256, "tongue_b": 224, "tongue_c": 192, "core": 192}

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
    lean_q=-0.06, lean_s=0.08, lean_cells=0.85, lean_phase=0.9)

# The body's alpha ramps up over the bottom 22 % of the canvas (the tongues keep
# flame_mask's 0.10): a long, wide fade is what makes the base dissolve into the
# coal bed instead of ending in a chevron cut.
CORE_ROOT_FADE = 0.22

# p of the p-norm soft union in `core_mask`: 8 rounds the lick/body crossings
# without flattening the two valleys (which the same union at p = 2 does fill).
SOFT_UNION_P = 8.0

CORE_LICKS = (
    # the tall centre lick - the hottest point of the fire, leaning a little
    dict(x_off=0.00, y0=0.34, y1=1.00, fade_in=0.30,
         p=dict(half=0.44, tip_pow=2.40, taper=0.90, dissolve=0.52, dissolve_from=0.50,
                feather=1.75, base_in=0.03, base_pow=1.00,
                bulge_at=0.28, bulge_amp=0.14, bulge_w=0.26,
                wob_amp=0.14, wob_cells=2.20, wob_phase=0.5,
                lean_q=0.20, lean_s=0.14, lean_cells=0.80, lean_phase=0.4)),
    # the left lick: shorter, and it curls out to its own side
    dict(x_off=-0.40, y0=0.24, y1=0.76, fade_in=0.34,
         p=dict(half=0.34, tip_pow=2.30, taper=0.95, dissolve=0.54, dissolve_from=0.46,
                feather=1.75, base_in=0.03, base_pow=1.00,
                bulge_at=0.30, bulge_amp=0.12, bulge_w=0.24,
                wob_amp=0.12, wob_cells=1.90, wob_phase=2.2,
                lean_q=-0.26, lean_s=-0.20, lean_cells=0.90, lean_phase=1.5)),
    # the right lick: the shortest, so the two shoulders do not match
    dict(x_off=0.40, y0=0.24, y1=0.66, fade_in=0.34,
         p=dict(half=0.32, tip_pow=2.30, taper=0.95, dissolve=0.56, dissolve_from=0.42,
                feather=1.75, base_in=0.03, base_pow=1.00,
                bulge_at=0.30, bulge_amp=0.12, bulge_w=0.24,
                wob_amp=0.12, wob_cells=1.80, wob_phase=4.1,
                lean_q=0.24, lean_s=0.16, lean_cells=0.85, lean_phase=2.7)),
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


def paint(key):
    """White RGB + the shape in alpha: the age ramp writes the colour."""
    w, h = canvas_size(key)
    a = core_mask(w, h) if key == "core" else flame_mask(w, h, FLAME[key])
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
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
    """SpriteRenderer: src = vertexColor * texture, straight alpha."""
    a = np.asarray(sprite.convert("RGBA"), dtype=np.float64) / 255.0
    out = np.zeros_like(a)
    out[:, :, 0] = rgb[0] / 255.0
    out[:, :, 1] = rgb[1] / 255.0
    out[:, :, 2] = rgb[2] / 255.0
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

    # logs (BuildLogs): three translucent wood boxes, unchanged by this cycle
    for name, (bw, bh), (lx, ly), tilt in (
            ("LogA", (0.62, 0.14), (-0.04, 0.07), -18.0),
            ("LogB", (0.54, 0.13), (0.07, 0.065), 16.0),
            ("LogC", (0.30, 0.12), (0.00, 0.16), 7.0)):
        wood = tinted(disc, soften(WOOD_COLOR), WOOD_ALPHA)
        paste_scaled(frame, wood, bw, bh, (lx, ly), origin, px_per_m, tilt)

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
    shipped = ("core",)
    rows = []
    for key in shipped:
        img = paint(key)
        path = os.path.join(FIRE_DIR, key + ".png")
        size = save(img, path, img.width, True, True, rng)
        a = np.asarray(img)[:, :, 3].astype(np.float64) / 255.0
        bw, bh = BOXES[key]
        rows.append((key, img.width, img.height, bw, bh,
                     img.width / float(img.height) / (bw / bh), 100.0 * a.mean(), size))
    wrote_folder = ensure_folder_meta(FIRE_DIR, rng)

    print("painted canvases written to Resources/Art/Campfire/")
    print("  %-9s %-9s %-13s %-9s %-9s %s" % ("key", "canvas", "box m", "aspect", "coverage", "bytes"))
    for key, w, h, bw, bh, aspect, cov, size in rows:
        print("  %-9s %-9dx%d %-13s %-9.4f %-9.1f %d"
              % (key, w, h, "%.3f x %.3f" % (bw, bh), aspect, cov, size))
    print("  folder meta written: %s" % wrote_folder)
    print("  rejected, evidence only, NOT written: tongue_a tongue_b tongue_c")

    # ── the pictures ──
    t0 = 12.0
    painted_sprites = {k: paint(k) for k in BOXES}
    all_keys = set(BOXES)
    core_only = {"core"}
    nothing = set()
    sheets = []

    # three variants of the same moment: 1 = procedural (shipped),
    # 2 = core painted only, 3 = all four canvases painted
    for pxm in (300.0, 150.0):
        tiles = [render_fire(t0, keys, painted_sprites, pxm)
                 for keys in (nothing, core_only, all_keys)]
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
    # native resolution strip
    strip = Image.new("RGBA", (sum(p.width for p in painted_sprites.values()) + 30,
                               max(p.height for p in painted_sprites.values())), (0, 0, 0, 0))
    x = 0
    for k in ("tongue_a", "tongue_b", "tongue_c", "core"):
        strip.alpha_composite(painted_sprites[k], (x, 0))
        x += painted_sprites[k].width + 10
    strip.convert("RGB").save(os.path.join(OUT_DIR, "canvases_native.png"))
    print("\nevidence -> %s" % OUT_DIR)
    for p in sheets:
        print("  " + os.path.basename(p))


if __name__ == "__main__":
    main()
