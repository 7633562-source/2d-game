"""ART-0 cap proxy: show a 128x14 cap canvas the way the level lens draws it.

The cap is a Tiled strip: 128 px at PPU 100 = 1.28 m native, and the level still is
rendered at 64.67 px/m (ortho 8.35 over 1080 px), so one repeat is 82.8 x 9.05 screen
px - the 14 px canvas is 9 px tall and 1 canvas px is 0.65 screen px. This resamples
the two canvases to that size, tiles each 4x and stacks them (old on top, new below)
with a magenta separator, then zooms the sheet with NEAREST so the sub-pixel
differences the frame blends are visible.

Usage:  python Tools/art0-cap-proxy.py <oldPng> <newPng> <out> [repeats] [zoom]
"""
import os
import sys

from PIL import Image

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials/Runs")
PX_PER_M = 1080.0 / 16.70          # level lens: ortho 8.35 = 16.70 m of height
NATIVE_M = (1.28, 0.14)            # cap sprite: 128x14 at PPU 100


def game_scale(img):
    w = int(round(NATIVE_M[0] * PX_PER_M))
    h = max(1, int(round(NATIVE_M[1] * PX_PER_M)))
    return img.convert("RGBA").resize((w, h), Image.BILINEAR)


def strip(img, repeats):
    g = game_scale(img)
    sheet = Image.new("RGBA", (g.width * repeats, g.height), (0, 0, 0, 0))
    for i in range(repeats):
        sheet.paste(g, (i * g.width, 0))
    return sheet


def main(old, new, out, repeats=4, zoom=6):
    a, b = strip(Image.open(old), repeats), strip(Image.open(new), repeats)
    print("one repeat at game scale: %dx%d px  (%.2f px/m)" % (a.width // repeats, a.height, PX_PER_M))
    z = lambda im: im.resize((im.width * zoom, im.height * zoom), Image.NEAREST)
    az, bz = z(a), z(b)
    sheet = Image.new("RGB", (az.width, az.height + bz.height + 4), (255, 0, 255))
    sheet.paste(az, (0, 0))
    sheet.paste(bz, (0, az.height + 4))
    path = os.path.join(RUNS, out)
    sheet.save(path)
    print("PROXY %s  %dx%d  (%d repeats at %dx)" % (path, sheet.width, sheet.height, repeats, zoom))
    return 0


if __name__ == "__main__":
    a = sys.argv[1:]
    sys.exit(main(a[0], a[1], a[2], int(a[3]) if len(a) > 3 else 4,
                  int(a[4]) if len(a) > 4 else 6))
