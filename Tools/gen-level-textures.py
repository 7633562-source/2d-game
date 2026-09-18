#!/usr/bin/env python3
"""ART-0 delivery: the four level textures LevelArt asks for + the material tiles ArtLibrary reads.

Contract (quoted from the tree, no guessing):

  Assets/LevelArt.cs
    LevelArt.PathsFor      -> "Level/ground_fill", "Level/ground_cap", "Level/sky", "Level/sign_finish"
    LevelArt.LoadAt        -> Resources.Load<Sprite>(path), then Resources.LoadAll<Sprite>(path)
  Assets/Resources/Level/README.txt
    "Do not change file names: ground_fill, ground_cap, sky, sign_finish"
  ArtLibrary (pre-stub revision a13565ca, the last one that loaded anything)
    LoadSource             -> Resources.Load<Texture2D>("Art/" + key)
    keys                   -> skin, fabric, denim, dirt, grass, bark, leaf
    SoftMap / TileMap      -> need src.isReadable (GetPixelBilinear), TileSize = 512

So: level textures are Sprite-imported under Resources/Level/, materials are readable textures
under Resources/Art/. The materials are downscaled 1024 -> 512 here, because ArtLibrary.TileMap
downsamples to 512 at runtime anyway and tiled surfaces gain nothing above that at this scale.

Painted here: ground_fill (soil side view), ground_cap (grass strip), sky (colour ramp),
sign_finish (finish pennant). All four are seamless in X where they are tiled.

Run from the project root:   python Tools/gen-level-textures.py
Writes the PNGs plus their .meta import settings, then prints a verification table.
"""

import math
import os
import random
import re
import shutil

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LEGACY = os.path.join(ROOT, "Assets", "LegacyArt")
LEVEL_DIR = os.path.join(ROOT, "Assets", "Resources", "Level")
ART_DIR = os.path.join(ROOT, "Assets", "Resources", "Art")
META_TEMPLATE = os.path.join(LEGACY, "Apple", "fruit.png.meta")

# ─── palette (matches the flat colours the code falls back to) ───
DIRT_BASE = (107, 77, 41)      # LevelArt.FillTint fallback 0.42 0.30 0.16
DIRT_TOP = (94, 68, 36)
DIRT_DEEP = (69, 48, 27)
STRATA_LIGHT = (124, 93, 52)
STRATA_DARK = (85, 62, 32)
PEBBLE_LIGHT = (140, 108, 68)
PEBBLE_DARK = (60, 43, 23)

GRASS_BODY = (76, 148, 71)     # LevelArt.CapTint fallback 0.30 0.58 0.28
GRASS_ROOT = (53, 107, 49)
GRASS_BLADE = (90, 163, 83)
GRASS_TIP = (116, 192, 103)
GRASS_BLADE_DARK = (61, 124, 57)

SKY_TOP = (94, 155, 220)
SKY_MID = (140, 199, 240)      # DayNightCycle.daySky 0.55 0.78 0.94
SKY_HORIZON = (195, 225, 244)
SKY_WARM = (255, 240, 222)

POLE = (61, 38, 20)            # LevelTest1.FencePostTint 0.24 0.15 0.08
POLE_LIGHT = (78, 51, 25)
POLE_DARK = (42, 26, 13)
FLAG = (209, 56, 46)           # LevelArt.SignTint 0.82 0.22 0.18
FLAG_DARK = (163, 38, 32)
FLAG_LIGHT = (242, 233, 218)


def mix(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def smooth(t):
    return t * t * (3.0 - 2.0 * t)


def wrap_noise(width, cells, seed, octaves=1, gain=0.5, growth=2.0):
    """Value noise in x with an integer number of cells over `width`.

    The cell grid wraps by construction, so x=0 and x=width give the same value:
    the painted band is seamless when tiled horizontally. `growth` sets the octave ratio;
    a non-integer ratio keeps the octaves incommensurate, so the sum has no short visible
    repeat inside the texture (powers of two make the coarse octave repeat every 1-2 m).
    """
    rng = random.Random(seed)
    layers = []
    n = cells
    for o in range(octaves):
        layers.append((n, [rng.random() for _ in range(n)], gain ** o))
        n = min(width, int(round(n * growth)))
    total = sum(w for _, _, w in layers)

    def at(x):
        acc = 0.0
        for n, vals, w in layers:
            f = x / float(width) * n
            i = int(math.floor(f))
            t = f - i
            acc += w * (vals[i % n] + (vals[(i + 1) % n] - vals[i % n]) * smooth(t))
        return acc / total

    return at


def new_image(w, h, rgba=True):
    return Image.new("RGBA" if rgba else "RGB", (w, h), (0, 0, 0, 0) if rgba else (0, 0, 0))


# ─── ground_fill: side view of soil, 1024x256 @ 128 PPU = 8 x 2 m ───
# 8 x 2 m is the shape of the common slabs (LevelTest1 Spawn / FinishPlaza / UpperCourt), so those
# draw at 1:1. Sliced stretches the whole canvas onto the piece, therefore the texel density is
# texture_width / piece_width: 128 px/m on an 8 m slab, 32 px/m on the 32 m "Behind" slab.
# Painting stays horizontal-dominant so a wide slab degrades without turning shapes into smears.
def paint_ground_fill(w=1024, h=256):
    img = new_image(w, h)
    px = img.load()
    strata = wrap_noise(w, 5, 101, octaves=3, gain=0.55, growth=2.3)
    coarse = wrap_noise(w, 3, 105, octaves=2, gain=0.5, growth=2.7)
    wobble = wrap_noise(w, 4, 102, octaves=2, gain=0.5, growth=2.5)
    clods = wrap_noise(w, 24, 106, octaves=2, gain=0.5, growth=2.3)    # ~33 cm lumps
    clods2 = wrap_noise(w, 13, 108, octaves=2, gain=0.5, growth=2.3)   # incommensurate: no 33 cm repeat
    crumb = wrap_noise(w, 128, 107, octaves=2, gain=0.5, growth=2.3)   # ~6 cm crumbs
    # Precompute the x-only fields once: the per-pixel loop must stay cheap.
    f_strata = [strata(x) for x in range(w)]
    f_coarse = [coarse(x) for x in range(w)]
    f_wobble = [wobble(x) for x in range(w)]
    f_clods = [(clods(x) + clods2(x)) * 0.5 for x in range(w)]
    f_crumb = [crumb(x) for x in range(w)]
    grain = random.Random(103)

    for y in range(h):
        v = y / float(h - 1)
        if v < 0.09:                      # the strip the grass cap covers
            base = mix(DIRT_TOP, DIRT_BASE, v / 0.09)
        else:
            base = mix(DIRT_BASE, DIRT_DEEP, (v - 0.09) / 0.91)
        for x in range(w):
            # Quantised strata: flat layers with hard edges read crisp. A smooth gradient would read
            # as a blurred smear as soon as the slab stretches the fill horizontally.
            t = y / 9.0 + f_wobble[x] * 3.5 + f_strata[x] * 5.0
            li = int(math.floor(t))
            frac = t - li
            tone = ((li * 2654435761) % 7) / 7.0
            c = mix(base, STRATA_LIGHT if tone > 0.5 else STRATA_DARK, abs(tone - 0.5) * 0.55)
            if frac < 0.15 or frac > 0.90:
                c = mix(c, STRATA_DARK, 0.50)                 # the line between two layers
            # Coarse band, also quantised: keeps structure when a wide slab stretches the fill.
            wide = math.sin((y * 0.044) + f_coarse[x] * 1.5 + 0.8)
            c = mix(c, STRATA_DARK if wide > 0 else STRATA_LIGHT, abs(wide) * 0.14)
            clod = f_clods[x]
            c = mix(c, STRATA_LIGHT if clod > 0.5 else STRATA_DARK, abs(clod - 0.5) * 0.70)
            cr = f_crumb[x]
            c = mix(c, STRATA_LIGHT if cr > 0.5 else STRATA_DARK, abs(cr - 0.5) * 0.50)
            j = grain.randint(-12, 12)                         # loose grain, seamless in statistics
            px[x, y] = (max(0, min(255, c[0] + j)), max(0, min(255, c[1] + j)),
                        max(0, min(255, c[2] + j)), 255)

    rng = random.Random(104)
    for _ in range(6):                                         # root fibres under the cap line
        fx, flen = rng.uniform(0.0, w), rng.uniform(52.0, 130.0)
        fy = rng.uniform(h * 0.07, h * 0.20)
        for y in range(int(fy) - 2, int(fy) + 3):
            for i in range(int(flen)):
                x = int((fx + i) % w)
                c = mix(px[x, y][:3], PEBBLE_DARK, 0.34)
                px[x, y] = (c[0], c[1], c[2], 255)
    for _ in range(8):                                         # cracks: 1 px jagged lines
        cx, cy = rng.uniform(0.0, w), rng.uniform(h * 0.15, h * 0.95)
        length = int(rng.uniform(60, 200))
        for i in range(length):
            if rng.random() < 0.25:
                cy += rng.choice((-1.0, 1.0))
            y = int(cy)
            if 0 <= y < h:
                x = int((cx + i) % w)
                c = mix(px[x, y][:3], PEBBLE_DARK, 0.70)
                px[x, y] = (c[0], c[1], c[2], 255)
    for _ in range(48):                                        # stones, drawn in their own box
        cx, cy = rng.uniform(0.0, w), rng.uniform(h * 0.10, h * 0.97)
        ry = rng.uniform(1.5, 4.5)
        rx = ry * rng.uniform(1.2, 2.2)                        # wider than tall, never a circle
        light = rng.random() < 0.4
        for dy in range(-int(ry) - 1, int(ry) + 2):
            y = int(cy) + dy
            if y < 0 or y >= h:
                continue
            for dx in range(-int(rx) - 1, int(rx) + 2):
                nx, ny = dx / rx, dy / ry
                d = nx * nx + ny * ny
                if d >= 1.0:
                    continue
                x = int(cx + dx) % w
                k = min(0.85, (1.0 - d) * 1.45)
                if light and dy < -ry * 0.35:
                    k = min(0.95, k + 0.25)                    # lit top edge: reads as a stone
                c = mix(px[x, y][:3], PEBBLE_LIGHT if light else PEBBLE_DARK, k)
                px[x, y] = (c[0], c[1], c[2], 255)
    return img


# ─── ground_cap: grass strip, 128x14 @ 100 PPU = 1.28 x 0.14 m (cap height) ───
# 0.14 m is fixed by StaticPlatform.SpawnSurfaceCap (capHeight = 0.14f), so the strip has to read
# as grass inside 14 px: uneven blade silhouette, light blade tips, a dark root line at the bottom.
def paint_ground_cap(w=128, h=14):
    img = new_image(w, h)
    px = img.load()
    tall = wrap_noise(w, 6, 201, octaves=3, gain=0.55)     # clumps of taller grass
    teeth = wrap_noise(w, 32, 203, octaves=2, gain=0.5)    # per-blade variation
    shade = wrap_noise(w, 16, 202, octaves=3, gain=0.5)
    for x in range(w):
        f = tall(x) * 0.62 + teeth(x) * 0.38
        top = int(round(1.0 + f * 4.2))          # 1..5: uneven top, sky shows above short blades
        top = max(0, min(5, top))
        dark = teeth(x) > 0.87                   # a few very dark blades break the ribbon
        for y in range(h):
            if y < top:
                continue
            if y >= h - 3:
                c = mix(GRASS_ROOT, GRASS_BODY, 0.2 + shade(x) * 0.35)
            elif y <= top + 1:
                c = mix(GRASS_TIP, GRASS_BLADE, 0.3 + shade(x) * 0.5)
            else:
                c = GRASS_BLADE_DARK if dark else mix(GRASS_BODY, GRASS_BLADE, shade(x) * 0.65)
            px[x, y] = (c[0], c[1], c[2], 255)
    return img


# ─── sky: colour ramp, 512x128 @ 32 PPU (drawn Sliced over 120x24 m) ───
# Stops sit where the camera can actually see them: the sky object is 24 m tall centred at y = 6
# (LevelBackdrop.BuildSky) and the camera shows about y in [-6, 4.2], i.e. texture v 0.58 .. 0.99.
# So the deep zenith colour is off-screen and the visible band runs daySky -> horizon haze.
SKY_STOPS = ((0.00, SKY_TOP), (0.30, SKY_TOP), (0.62, SKY_MID), (0.90, SKY_HORIZON), (1.00, SKY_HORIZON))
SKY_WARM_FROM = 0.88


def ramp(stops, v):
    for i in range(len(stops) - 1):
        v0, c0 = stops[i]
        v1, c1 = stops[i + 1]
        if v <= v1 or i == len(stops) - 2:
            if v1 <= v0:
                return c1
            return mix(c0, c1, smooth((v - v0) / (v1 - v0)))
    return stops[-1][1]


def paint_sky(w=512, h=128):
    img = new_image(w, h)
    px = img.load()
    dither = random.Random(301)
    for y in range(h):
        v = y / float(h - 1)
        c = ramp(SKY_STOPS, v)
        c = mix(c, SKY_WARM, max(0.0, (v - SKY_WARM_FROM) / (1.0 - SKY_WARM_FROM)) * 0.14)
        for x in range(w):
            d = dither.choice((-1, 0, 0, 0, 1))     # kills 8-bit banding in a smooth ramp
            px[x, y] = (max(0, min(255, c[0] + d)), max(0, min(255, c[1] + d)),
                        max(0, min(255, c[2] + d)), 255)
    return img


# ─── sign_finish: finish pennant on a post, 64x320 @ 100 PPU ───
# The piece box is 0.18 x 0.90 m (LevelTest1 "FinishSign"), aspect 1:5, and StaticPlatform draws it
# Sliced, so the canvas is painted 1:5 and comes out undistorted.
def paint_sign_finish(w=64, h=320):
    img = new_image(w, h)
    px = img.load()
    pole_l, pole_r = 20, 35
    flag_top, flag_bottom = 8, 160       # ~48% of the post: the pennant has to read at ~16 screen px
    stripe_top, stripe_bottom = 132, 150
    rect_right, tip = 43, 63
    foot_top = h - 10
    outline = 2
    half = (flag_bottom - flag_top) * 0.5
    tip_y = (flag_top + flag_bottom) * 0.5
    for y in range(h):
        for x in range(w):
            c = None
            # post, with a wider foot so it does not look pasted onto the grass line
            if pole_l <= x <= pole_r:
                if x == pole_l:
                    c = POLE_DARK
                elif x <= pole_l + 2:
                    c = POLE_LIGHT
                elif x >= pole_r - 1:
                    c = POLE_DARK
                else:
                    c = POLE
            if y >= foot_top and 20 <= x <= 43:
                c = POLE_DARK if (x <= 21 or x >= 42 or y >= h - 2) else POLE
            # pennant: rectangle at the mast, then a taper to a point
            if flag_top <= y <= flag_bottom and x > pole_r:
                if x <= rect_right:
                    inside = True
                    on_edge = (y - flag_top) < outline or (flag_bottom - y) < outline
                else:
                    t = (x - rect_right) / float(tip - rect_right)
                    span = 1.0 - t
                    d = abs(y - tip_y)
                    inside = d <= half * span
                    on_edge = inside and (abs(d - half * span) < outline or x > tip - outline)
                if inside:
                    if stripe_top <= y <= stripe_bottom:
                        c = FLAG_LIGHT
                    else:
                        c = FLAG_DARK if on_edge else FLAG
            if c is not None:
                px[x, y] = (c[0], c[1], c[2], 255)
    return img


# ─── materials: LegacyArt 1024 -> Resources/Art 512 ───
MATERIALS = ["skin", "fabric", "denim", "dirt", "grass", "bark", "leaf"]


def write_meta(png_path, pixels_per_unit, readable, alpha, guid):
    """Copy the project's own Single-sprite .meta shape and set the fields the loaders need."""
    with open(META_TEMPLATE, "r", encoding="utf-8") as fh:
        text = fh.read()

    def set_line(key, value, indent=2):
        nonlocal text
        pattern = re.compile(r"^%s%s:.*$" % (" " * indent, re.escape(key)), re.MULTILINE)
        assert pattern.search(text), "template has no %s" % key
        text = pattern.sub("%s%s: %s" % (" " * indent, key, value), text, count=1)

    def set_text(key, value, indent=2):
        nonlocal text
        pattern = re.compile(r"^%s%s:.*$" % (" " * indent, re.escape(key)), re.MULTILINE)
        text = pattern.sub("%s%s: %s" % (" " * indent, key, value), text, count=1)

    text = re.sub(r"^guid: .*$", "guid: " + guid, text, count=1, flags=re.MULTILINE)
    set_line("spriteMeshType", 0)            # FullRect: Sliced / Tiled draw modes need it
    set_line("spritePixelsToUnits", pixels_per_unit)
    set_line("isReadable", 1 if readable else 0)
    set_line("wrapU", 1, indent=4)           # Repeat, for the tiled surfaces
    set_line("wrapV", 1, indent=4)
    set_line("alphaIsTransparency", 1 if alpha else 0)
    set_line("spriteGenerateFallbackPhysicsShape", 0)   # art never carries a collider
    set_line("textureType", 8)               # Sprite
    set_line("spriteMode", 1)                # Single
    set_text("spriteID", guid[:16] + "0800000000000000", indent=4)
    text = re.sub(r"^\s*nameFileIdTable: .*$", "    nameFileIdTable: {}", text, count=1, flags=re.MULTILINE)

    with open(png_path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def new_guid(rng):
    return "".join(rng.choice("0123456789abcdef") for _ in range(32))


def save(img, path, pixels_per_unit, readable, alpha, guid_rng):
    img.save(path, "PNG", optimize=True)
    write_meta(path, pixels_per_unit, readable, alpha, new_guid(guid_rng))
    return os.path.getsize(path)


def seam_error(img):
    """Horizontal tiling check: the jump across the wrap seam vs the jump between neighbours.

    A white-noise grain makes column 0 differ from the last column by the same amount two
    neighbouring columns differ by, so the seam is invisible; the structured strata/blades wrap
    exactly by construction. Reported as (seam jump, typical inner jump), mean over all rows.
    """
    px = img.load()
    w, h = img.size
    seam = sum(abs(px[0, y][i] - px[w - 1, y][i]) for y in range(h) for i in range(3)) / float(h * 3)
    inner = sum(abs(px[x, y][i] - px[x + 1, y][i])
                for x in range(0, w - 1, 7) for y in range(h) for i in range(3)) / float(len(range(0, w - 1, 7)) * h * 3)
    return seam, inner


def main():
    os.makedirs(LEVEL_DIR, exist_ok=True)
    os.makedirs(ART_DIR, exist_ok=True)
    guid_rng = random.Random(20260918)
    rows = []

    level = [
        ("ground_fill", paint_ground_fill(), 128, False, False),
        ("ground_cap", paint_ground_cap(), 100, False, True),
        ("sky", paint_sky(), 32, False, False),
        ("sign_finish", paint_sign_finish(), 100, False, True),
    ]
    for name, img, ppu, readable, alpha in level:
        path = os.path.join(LEVEL_DIR, name + ".png")
        size = save(img, path, ppu, readable, alpha, guid_rng)
        seam, inner = seam_error(img)
        rows.append(("Resources/Level/" + name + ".png", "%dx%d" % img.size, ppu,
                     "%d KB" % (size // 1024) if size >= 1024 else "%d B" % size,
                     "tile seam x: %.2f vs inner %.2f" % (seam, inner)))

    for key in MATERIALS:
        src = os.path.join(LEGACY, key + ".png")
        before = os.path.getsize(src)
        img = Image.open(src).convert("RGB")
        src_size = img.size
        out = img.resize((512, 512), Image.LANCZOS)
        path = os.path.join(ART_DIR, key + ".png")
        size = save(out, path, 512, True, False, guid_rng)
        rows.append(("Resources/Art/" + key + ".png",
                     "%dx%d -> %dx%d" % (src_size[0], src_size[1], 512, 512), 512,
                     "%d KB -> %d KB" % (before // 1024, size // 1024), "readable"))

    meta_dir = os.path.join(ROOT, "Assets", "Resources", "Art.meta")
    if not os.path.exists(meta_dir):
        folder_meta = os.path.join(ROOT, "Assets", "Resources", "Level.meta")
        with open(folder_meta, "r", encoding="utf-8") as fh:
            text = fh.read()
        text = re.sub(r"^guid: .*$", "guid: " + new_guid(guid_rng), text, count=1, flags=re.MULTILINE)
        with open(meta_dir, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)

    print("%-38s %-22s %5s %18s %s" % ("file", "pixels", "PPU", "bytes", "note"))
    for r in rows:
        print("%-38s %-22s %5s %18s %s" % r)
    print("\nwrote %d PNGs + 1 folder meta" % len(rows))


if __name__ == "__main__":
    main()
