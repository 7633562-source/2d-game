#!/usr/bin/env python3
"""ART-0 27th pass - the human's hand canvas: the hand reaches its own box.

Measured before this pass (`Tools/art0-part-ink-probe.py`, shipped bytes only):

    part   canvas     ink bbox      inkX%  inkY%  box m        inkX m  inkLen m  wrist margin
    hand   192x512    31,165 119x171   62%    33%  0.060x0.160   0.037   0.053    0.052 m

`Human.handSize` is 0.06 x 0.16 m and `Assets/Resources/Art/Human/hand.png.meta` carries
`spritePixelsToUnits: 3200`, so the canvas' own world size IS the box (0.06 x 0.16 m) and
`HumanSegment` draws the whole canvas onto that box with `Sliced` (`painted-parts.mdc`).
Two things were wrong with the old canvas, both measured, neither one a matter of taste:

  * the painted hand was 0.053 m long on a 0.16 m bone (33 %) while every other part fills
    60-100 % of its own box (torso 100 %, shin 73 %, thigh 81 %) - the hand was the only
    part under half, and the worst fill of the body;
  * its ink was centred, so the wrist cut sat 0.052 m BELOW the bone's proximal edge: in the
    rig the hand hung detached, a third of a bone away from the forearm that ends at the
    wrist joint.

This script rewrites that ONE canvas - same 192 x 512 px, same `.meta`, same loader path
(`ArtLibrary.Albedo("Human/hand")` -> `Resources/Art/Human/hand`) - from the hand's own
accepted pixels (`Assets/LegacyArt/Human/hand.png`, the painted palm the game has shipped):

  * crop to the painted ink, scale it with LANCZOS so it fills FILL_FRACTION of the box
    WIDTH (the proportional maximum: the art's aspect is 0.70, the box' is 0.375, so filling
    the box' 0.16 m length would need a 0.111 m deep palm in a 0.06 m box - the width is the
    binding edge, and the rest of the bone stays empty on purpose, as `painted-parts.mdc`
    allows for a hand in a long box);
  * place it with the wrist cut ON the canvas' top row = the bone's proximal edge = the
    wrist joint, so the hand starts where the forearm ends instead of 0.052 m later;
  * keep the transparent background and the painted pixels themselves: nothing is redrawn,
    no outline is added, no colour is shifted.

Rejected on the way here (recorded so no later pass repeats it): a procedural SIDE-VIEW hand
redrawn from scratch in the box' aspect. Two attempts (a straight-fingered mitten, then a
four-lobe silhouette with a thumb and interior shading) were read to a stop by the same
vision model: the first "reads as a spatula / oven mitt", the second "reads as a comb or
garden rake - all four fingers in one plane, which is a front view, not a side view". A
front-view hand painted into a side-view rig is a bigger art decision than this pass owns,
and a bad canvas is worse than a small one - so the accepted art stays, at the right size.

Nothing here is physics: `Human.handSize`, the collider and every `.meta` field are untouched.

Run:  python Tools/gen-human-hand-texture.py            # rewrite the canvas
      python Tools/gen-human-hand-texture.py --check    # read it, change nothing
"""

import argparse
import pathlib
import sys

from PIL import Image

PROJECT = pathlib.Path(__file__).resolve().parent.parent
SHIP = PROJECT / "Assets" / "Resources" / "Art" / "Human" / "hand.png"
LEGACY = PROJECT / "Assets" / "LegacyArt" / "Human" / "hand.png"

W, H = 192, 512          # the canvas: 0.06 x 0.16 m at ppu 3200 (the shipping box)
PPU = 3200.0             # documented here so every number below converts to metres
FILL_FRACTION = 0.94     # of the box WIDTH; the cut faces keep a hair of margin
ALPHA_CUT = 8            # "any painted pixel at all" - the crop keeps the soft rim
SOLID_ALPHA = 40         # "the hand a viewer sees" - the scale is set by this box


def ink_box(im):
    bb = im.split()[3].point(lambda a: 255 if a > ALPHA_CUT else 0).getbbox()
    if bb is None:
        raise SystemExit("FAIL canvas has no painted pixels at all")
    return bb


def report(im, label):
    """getbbox() answers (left, upper, right, lower) - EXCLUSIVE right/lower, so the ink's
    size is right-left / lower-upper. (The first version of this script printed the raw
    tuple as if it were x,y,w,h and claimed 97 % where the truth is 94 %.)"""
    solid = im.split()[3].point(lambda a: 255 if a > SOLID_ALPHA else 0).getbbox()
    faint = ink_box(im)
    sx, sy, sw, sh = solid[0], solid[1], solid[2] - solid[0], solid[3] - solid[1]
    pad = 100.0 * (1.0 - (sw * sh) / float(im.width * im.height))
    print("  %-7s canvas %dx%d  solid ink %d,%d %dx%d  inkX %3.0f%% inkY %3.0f%% (%2.0f%% pad)"
          "  ink %.3f x %.3f m  wrist margin %.3f m  (faint box %d,%d %dx%d)" % (
              label, im.width, im.height, sx, sy, sw, sh,
              100.0 * sw / im.width, 100.0 * sh / im.height, pad,
              sw / PPU, sh / PPU, sy / PPU,
              faint[0], faint[1], faint[2] - faint[0], faint[3] - faint[1]))


def fit():
    """The legacy hand's ink, scaled to FILL_FRACTION of the box width, wrist at the top.

    Two bboxes, on purpose: the crop keeps every faint pixel of the painted hand (the soft
    contour and its own shadow), while the SCALE is set by the SOLID silhouette
    (alpha > SOLID_ALPHA), so the number the report quotes is the hand a viewer sees and not
    a faint halo. A 3.5x downscale fades an anti-aliased rim below any alpha cut, so scaling
    by the >8 box would have delivered a 0.79x hand with a 0.21x ghost around it.
    """
    src = Image.open(LEGACY).convert("RGBA")
    alpha = src.split()[3]
    crop_box = alpha.point(lambda a: 255 if a > ALPHA_CUT else 0).getbbox()
    solid_box = alpha.point(lambda a: 255 if a > SOLID_ALPHA else 0).getbbox()
    if crop_box is None or solid_box is None:
        raise SystemExit("FAIL the legacy hand has no painted pixels")

    x0, y0, x1, y1 = crop_box
    sx, sy, sx1, sy1 = solid_box
    ink = src.crop((x0, y0, x1, y1))

    target_w = int(round(W * FILL_FRACTION))
    scale = target_w / float(sx1 - sx)
    size = (max(1, int(round(ink.width * scale))), max(1, int(round(ink.height * scale))))
    if size[1] > H:
        raise SystemExit("FAIL scaled ink %d px is taller than the canvas %d px" % (size[1], H))
    ink = ink.resize(size, Image.LANCZOS)

    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    # no mask: the ink already carries its own alpha, and pasting through an alpha mask
    # would square it (every soft edge pixel would fade again and the silhouette shrink)
    canvas.paste(ink, ((W - target_w) // 2 - int(round((sx - x0) * scale)),
                       -int(round((sy - y0) * scale))))
    return canvas


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="read the shipped PNG, change nothing")
    args = ap.parse_args()

    before = Image.open(SHIP).convert("RGBA")
    before_bytes = SHIP.stat().st_size
    report(before, "before")
    if args.check:
        return 0

    after = fit()
    report(after, "after")
    after.save(SHIP)
    print("  wrote %s (%d B, was %d B)" % (SHIP, SHIP.stat().st_size, before_bytes))
    return 0


if __name__ == "__main__":
    sys.exit(main())
