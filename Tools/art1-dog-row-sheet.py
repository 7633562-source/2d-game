#!/usr/bin/env python3
"""ART-1 dog row: what the ten delivered part PNGs look like, and how they map onto
their shipping collider boxes.

Sheets (written into Trials/Runs/):

  <label>-sheet-raw-body.png / -raw-legs.png
      the delivered paintings at a common height, on grey - "is this a dog part at all?"
  <label>-sheet-fit-body.png / -fit-legs.png
      per part, the box (red outline) with the canvas drawn two ways, stacked and
      separated by a magenta bar:
        TOP    = as delivered: the whole canvas stretched onto the box (what the
                 gallery draws today; the x/y scale mismatch is the squash)
        BOTTOM = re-framed: body scaled uniformly to fill the box in its binding
                 dimension, canvas padded to the box aspect (distortion 1.00)
      Panel captions carry the numbers (canvas aspect, box aspect).

  python Tools/art1-dog-row-sheet.py [label]

Read-only: no PNG under Assets/ is touched.
"""
from __future__ import annotations

import pathlib
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

PROJECT = pathlib.Path(__file__).resolve().parent.parent
RUNS = PROJECT / "Trials" / "Runs"
ART = PROJECT / "Assets" / "Resources" / "Art" / "Dog"

PARTS = [
    ("chest", (0.36, 0.14)),
    ("pelvis", (0.22, 0.13)),
    ("neck", (0.10, 0.08)),
    ("head", (0.14, 0.10)),
    ("tail", (0.20, 0.05)),
    ("front_upper", (0.07, 0.17)),
    ("front_lower", (0.06, 0.17)),
    ("thigh", (0.08, 0.17)),
    ("shin", (0.06, 0.17)),
    ("paw", (0.10, 0.05)),
]

BODY = ("chest", "pelvis", "neck", "head", "tail")

BG = (60, 64, 70, 255)
BOX_INK = (230, 70, 60, 255)
BAR = (255, 0, 255, 255)


def font(size: int) -> ImageFont.FreeTypeFont:
    for name in ("arial.ttf", "segoeui.ttf"):
        p = pathlib.Path("C:/Windows/Fonts") / name
        if p.exists():
            return ImageFont.truetype(str(p), size)
    return ImageFont.load_default()


def body_bbox(img: Image.Image, threshold: int = 16):
    a = np.asarray(img)[:, :, 3]
    ys, xs = np.nonzero(a >= threshold)
    if len(xs) == 0:
        return (0, 0, img.width, img.height)
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def reframe(img: Image.Image, box_px: tuple[int, int]) -> tuple[Image.Image, float]:
    """Body scaled uniformly to fill the box in its binding dimension, centred.

    Returns (canvas at box_px, the fill fraction of the canvas covered by the body
    bbox)."""
    bb = body_bbox(img)
    bw, bh = bb[2] - bb[0], bb[3] - bb[1]
    s = min(box_px[0] / bw, box_px[1] / bh)
    body = img.crop(bb).resize((max(1, int(round(bw * s))), max(1, int(round(bh * s)))), Image.LANCZOS)
    canvas = Image.new("RGBA", box_px, (0, 0, 0, 0))
    canvas.alpha_composite(body, ((box_px[0] - body.width) // 2, (box_px[1] - body.height) // 2))
    fill = (body.width * body.height) / float(box_px[0] * box_px[1])
    return canvas, fill


def sheet_raw(parts, out_name: str, common_h: int = 200) -> None:
    panels = []
    for name, _box in parts:
        img = Image.open(ART / f"{name}.png").convert("RGBA")
        scale = common_h / img.height
        panels.append((name, img.resize((max(1, int(img.width * scale)), common_h), Image.LANCZOS)))
    w = sum(p[1].width for p in panels) + 8 * (len(panels) + 1)
    h = common_h + 34
    sheet = Image.new("RGBA", (w, h), BG)
    dr = ImageDraw.Draw(sheet)
    f = font(13)
    x = 8
    for name, img in panels:
        sheet.alpha_composite(img, (x, 8))
        dr.text((x + img.width // 2, common_h + 12), name, font=f, fill=(240, 240, 240, 255), anchor="ma")
        x += img.width + 8
    path = RUNS / out_name
    sheet.convert("RGB").save(path)
    print(f"sheet {path.name} {sheet.width}x{sheet.height} parts={len(panels)}")


def sheet_fit(parts, out_name: str, px_per_m: float) -> None:
    rows = []
    for name, box in parts:
        img = Image.open(ART / f"{name}.png").convert("RGBA")
        box_px = (max(1, int(round(box[0] * px_per_m))), max(1, int(round(box[1] * px_per_m))))
        now = img.resize(box_px, Image.LANCZOS)          # the gallery's draw today
        reframed, fill = reframe(img, box_px)            # undistorted, filled
        rows.append((name, box_px, now, reframed, img.width / img.height, box[0] / box[1], fill))

    pad = 14
    palette_w = [r[1][0] + pad for r in rows]
    pan_h = max(r[1][1] for r in rows)
    cell_h = pan_h * 2 + 46
    w = sum(palette_w) + pad * (len(rows) + 1)
    sheet = Image.new("RGBA", (w, cell_h), BG)
    dr = ImageDraw.Draw(sheet)
    f = font(13)
    f2 = font(11)
    x = pad
    for name, box_px, now, reframed, ca, ba, fill in rows:
        top_y = 6 + (pan_h - box_px[1]) // 2
        sheet.alpha_composite(now, (x, top_y))
        dr.rectangle([x - 0.5, top_y - 0.5, x + box_px[0] - 0.5, top_y + box_px[1] - 0.5], outline=BOX_INK, width=1)
        bar_y = 6 + pan_h + 4
        dr.rectangle([x - 0.5, bar_y, x + box_px[0] - 0.5, bar_y + 2], fill=BAR)
        bot_y = bar_y + 6 + (pan_h - box_px[1]) // 2
        sheet.alpha_composite(reframed, (x, bot_y))
        dr.rectangle([x - 0.5, bot_y - 0.5, x + box_px[0] - 0.5, bot_y + box_px[1] - 0.5], outline=BOX_INK, width=1)
        cap_y = 6 + pan_h * 2 + 10
        dr.text((x + box_px[0] // 2, cap_y), name, font=f, fill=(245, 245, 245, 255), anchor="ma")
        dr.text((x + box_px[0] // 2, cap_y + 15), f"c {ca:.2f} b {ba:.2f} f {fill * 100:.0f}%", font=f2,
                fill=(198, 200, 206, 255), anchor="ma")
        x += box_px[0] + pad
    path = RUNS / out_name
    sheet.convert("RGB").save(path)
    print(f"sheet {path.name} {sheet.width}x{sheet.height} parts={len(rows)} px/m={px_per_m}")


def sheet_pair(parts, out_name: str, px_per_m: float, before_dir: pathlib.Path) -> None:
    """TOP = the pristine (delivered) canvas stretched onto the box, BOTTOM = the current
    canvas - the same box, the same lens. The before/after for the eye."""
    rows = []
    for name in parts:
        box = dict(PARTS)[name]
        box_px = (max(1, int(round(box[0] * px_per_m))), max(1, int(round(box[1] * px_per_m))))
        before = Image.open(before_dir / f"{name}.png").convert("RGBA").resize(box_px, Image.LANCZOS)
        after = Image.open(ART / f"{name}.png").convert("RGBA").resize(box_px, Image.LANCZOS)
        rows.append((name, box_px, before, after))

    pad = 24
    pan_h = max(r[1][1] for r in rows)
    cell_h = pan_h * 2 + 46
    w = sum(r[1][0] + pad for r in rows) + pad
    sheet = Image.new("RGBA", (w, cell_h), BG)
    dr = ImageDraw.Draw(sheet)
    f = font(15)
    f2 = font(12)
    x = pad
    for name, box_px, before, after in rows:
        top_y = 6 + (pan_h - box_px[1]) // 2
        sheet.alpha_composite(before, (x, top_y))
        dr.rectangle([x - 0.5, top_y - 0.5, x + box_px[0] - 0.5, top_y + box_px[1] - 0.5],
                     outline=BOX_INK, width=1)
        bar_y = 6 + pan_h + 4
        dr.rectangle([x - 0.5, bar_y, x + box_px[0] - 0.5, bar_y + 3], fill=BAR)
        bot_y = bar_y + 8 + (pan_h - box_px[1]) // 2
        sheet.alpha_composite(after, (x, bot_y))
        dr.rectangle([x - 0.5, bot_y - 0.5, x + box_px[0] - 0.5, bot_y + box_px[1] - 0.5],
                     outline=BOX_INK, width=1)
        cap_y = 6 + pan_h * 2 + 10
        dr.text((x + box_px[0] // 2, cap_y), name, font=f, fill=(245, 245, 245, 255), anchor="ma")
        dr.text((x + box_px[0] // 2, cap_y + 17), "top: delivered  bottom: this build", font=f2,
                fill=(198, 200, 206, 255), anchor="ma")
        x += box_px[0] + pad
    path = RUNS / out_name
    sheet.convert("RGB").save(path)
    print(f"sheet {path.name} {sheet.width}x{sheet.height} parts={len(rows)} px/m={px_per_m}")


def main(argv: list[str]) -> int:
    label = argv[0] if argv else "art1dog13"
    body = [p for p in PARTS if p[0] in BODY]
    legs = [p for p in PARTS if p[0] not in BODY]
    sheet_raw(body, f"{label}-sheet-raw-body.png")
    sheet_raw(legs, f"{label}-sheet-raw-legs.png")
    sheet_fit(body, f"{label}-sheet-fit-body.png", 640.0)
    sheet_fit(legs, f"{label}-sheet-fit-legs.png", 640.0)
    before_dir = RUNS / f"{label}-before"
    if before_dir.exists():
        sheet_pair(["neck", "shin"], f"{label}-sheet-pair-neckshin.png", 1600.0, before_dir)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
