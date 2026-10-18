"""ART-0 (nineteenth pass): inspect the painted log canvases and render the log A/B.

The acceptance here is a PICTURE, so this writes the pictures the eye needs and prints the
measurements that go with them:

  * per canvas: size, box aspect error, alpha coverage, the OPAQUE share (the shipped box is
    a 50 %-alpha flat rectangle - the defect), the distinct-colour count and the silhouette's
    half-width profile (an end-to-end log is not a rectangle);
  * `logs_ab_box.png` - the three logs at their own box size and tilt, at the gallery FIRE
    lens (340 px/m), LEFT = the engine's flat LogWood box, RIGHT = the painted canvas. Same
    geometry, same tilt: the only difference is the picture;
  * `native_4x.png` - the three canvases magnified 4x NEAREST for the eye read.

Usage: python Tools/art0-log-probe.py
"""
import os

import numpy as np
from PIL import Image, ImageDraw

ROOT = r"E:/Games/2D Game/My project"
FIRE = os.path.join(ROOT, "Assets", "Resources", "Art", "Campfire")
OUT = os.path.join(ROOT, "Trials", "Runs", "art-fire-2026-09-19")
KEYS = ("log_a", "log_b", "log_c")
BOXES = {"log_a": (0.62, 0.14), "log_b": (0.54, 0.13), "log_c": (0.30, 0.12)}
LAYOUT = {"log_a": (0.62, 0.14, -18.0), "log_b": (0.54, 0.13, 16.0), "log_c": (0.30, 0.12, 7.0)}
WOOD = (int(round(0.50 * 255)), int(round(0.25 * 255)), 0, int(round(0.50 * 255)))
PX_PER_M = 340.0          # the gallery FIRE lens (orthoSize 1.59)


def engine_box(w_m, h_m, px_per_m):
    """HumanSegment.CreateBodySprite + SpriteRenderer.color: a flat hard translucent rect."""
    return Image.new("RGBA", (max(1, int(round(w_m * px_per_m))), max(1, int(round(h_m * px_per_m)))),
                     WOOD)


def painted_box(key, px_per_m):
    """ArtParts.Dress(key, tileMeters 0): the whole canvas Sliced onto the log's box."""
    w_m, h_m = BOXES[key]
    img = Image.open(os.path.join(FIRE, key + ".png")).convert("RGBA")
    return img.resize((max(1, int(round(w_m * px_per_m))), max(1, int(round(h_m * px_per_m)))),
                      Image.LANCZOS)


def tray(pictures, gap=26, pad=18):
    """The three logs on a neutral ground, each rotated by its own tilt, at the fire's centre."""
    w = sum(p.width for p in pictures) + gap * (len(pictures) - 1) + pad * 2
    h = max(p.height for p in pictures) + pad * 2
    img = Image.new("RGB", (w, h), (26, 30, 44))
    x = pad
    for p in pictures:
        img.paste(p, (x, (h - p.height) // 2), p)
        x += p.width + gap
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    print("%-7s %-9s %-8s %-9s %-8s %-8s %-8s %s"
          % ("key", "canvas", "aspect", "cover%", "opaque%", "colours", "maxhw", "mid-widths"))
    verdict = []
    for key in KEYS:
        img = Image.open(os.path.join(FIRE, key + ".png")).convert("RGBA")
        a = np.asarray(img).astype(np.float64)
        alpha = a[:, :, 3] / 255.0
        w_m, h_m = BOXES[key]
        aspect = img.width / float(img.height) / (w_m / h_m)
        rgb = a[:, :, :3].astype(np.uint8)
        flat = rgb.reshape(-1, 3)
        seen = np.unique(flat[np.repeat(alpha.ravel() > 0.5, 1)], axis=0)
        widths = (alpha > 0.5).sum(axis=0)
        prof = [int(widths[int(round(i * (img.width - 1) / 6.0))]) for i in range(7)]
        print("%-7s %-9s %-8.4f %-9.1f %-8.1f %-8d %-8d %s"
              % (key, "%dx%d" % (img.width, img.height), aspect,
                 100.0 * alpha.mean(), 100.0 * (alpha >= 0.999).mean(),
                 len(seen), int(widths.max()), prof))
        # the pass's own gate, so the numbers above are not just decoration: a painted log
        # must be in its box's aspect (nothing stretched), filled (it covers the box the flat
        # rectangle covered), opaque (the complaint was 0.5 alpha) and many-toned (a flat box
        # is one colour - that is what the picture it replaces was)
        bad = (abs(aspect - 1.0) > 0.01 or alpha.mean() < 0.85
               or (alpha >= 0.999).mean() < 0.80 or len(seen) < 8)
        verdict.append((key, bad, aspect, 100.0 * alpha.mean(), 100.0 * (alpha >= 0.999).mean(),
                        len(seen)))
    worst = max(abs(v[2] - 1.0) for v in verdict)
    print("\nLOG-PICTURE %s canvases=%d aspect_error_max=%.4f coverage_min=%.1f%% opaque_min=%.1f%% "
          "tones_min=%d" % ("OK" if not any(v[1] for v in verdict) else "BAD", len(verdict), worst,
                            min(v[3] for v in verdict), min(v[4] for v in verdict),
                            min(v[5] for v in verdict)))

    # the picture the eye judges: same geometry, same tilts, one picture each side.
    # Two lenses: the gallery's FIRE row (340 px/m, where the catalogue shows it) and
    # the yard (GameProcess runs the camera at ortho 4.6 -> 117 px/m), because a
    # picture can read at one scale and turn to mush at the other.
    for pxm, tag in ((PX_PER_M, "box"), (117.0, "yard")):
        left, right = [], []
        for key in KEYS:
            w_m, h_m, tilt = LAYOUT[key]
            left.append(engine_box(w_m, h_m, pxm).rotate(tilt, resample=Image.BICUBIC, expand=True))
            right.append(painted_box(key, pxm).rotate(tilt, resample=Image.BICUBIC, expand=True))
        gap = 40
        a_img = tray(left)
        b_img = tray(right)
        sheet = Image.new("RGB", (a_img.width + gap + b_img.width, max(a_img.height, b_img.height)),
                          (26, 30, 44))
        sheet.paste(a_img, (0, 0))
        sheet.paste(b_img, (a_img.width + gap, 0))
        d = ImageDraw.Draw(sheet)
        d.text((18, 6), "LEFT", fill=(200, 200, 200))
        d.text((a_img.width + gap + 18, 6), "RIGHT", fill=(200, 200, 200))
        name = "logs_ab_%s.png" % tag
        sheet.save(os.path.join(OUT, name))
        print("A/B %-5s sheet -> %s (%0.0f px/m, same geometry both sides)"
              % (tag, os.path.join(OUT, name), pxm))

    mag = 4
    strip = Image.new("RGB", (sum(Image.open(os.path.join(FIRE, k + ".png")).width * mag + 16
                                 for k in KEYS), max(
        Image.open(os.path.join(FIRE, k + ".png")).height * mag for k in KEYS)), (26, 30, 44))
    x = 0
    for k in KEYS:
        im = Image.open(os.path.join(FIRE, k + ".png")).convert("RGB")
        im = im.resize((im.width * mag, im.height * mag), Image.NEAREST)
        strip.paste(im, (x, 0))
        x += im.width + 16
    strip.save(os.path.join(OUT, "logs_native_4x.png"))
    print("canvases 4x  -> %s" % os.path.join(OUT, "logs_native_4x.png"))


if __name__ == "__main__":
    main()
