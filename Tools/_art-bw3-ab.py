# ART-BW3 side-by-side sheet: two crops at the same scale, labelled, one image.
# usage: python _art-bw3-ab.py <out.png> <labelA> <pathA> <labelB> <pathB> [x0 y0 x1 y1]
import sys

from PIL import Image, ImageDraw, ImageFont

FONT = "C:/Windows/Fonts/arial.ttf"


def main():
    out, la, pa, lb, pb = sys.argv[1:6]
    box = None
    if len(sys.argv) >= 10:
        box = tuple(int(v) for v in sys.argv[6:10])
    a = Image.open(pa).convert("RGB")
    b = Image.open(pb).convert("RGB")
    if box:
        a = a.crop(box)
        b = b.crop(box)
    if a.size != b.size:
        b = b.resize(a.size, Image.LANCZOS)
    w, h = a.size
    bar = max(22, h // 16)
    sheet = Image.new("RGB", (2 * w + 10, h + bar), (18, 18, 18))
    sheet.paste(a, (0, bar))
    sheet.paste(b, (w + 10, bar))
    d = ImageDraw.Draw(sheet)
    f = ImageFont.truetype(FONT, int(bar * 0.62))
    d.text((6, 3), la, font=f, fill=(255, 235, 120))
    d.text((w + 16, 3), lb, font=f, fill=(255, 235, 120))
    sheet.save(out)
    print("ab ->", out, sheet.size)


if __name__ == "__main__":
    main()
