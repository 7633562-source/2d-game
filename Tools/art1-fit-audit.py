#!/usr/bin/env python3
"""ART-1 acceptance item 2, numerically: is any real-art part drawn squashed?

The card (.cursor/tasks/ART-1-gallery.md) says: "Human parts and tree parts show
real textures, fitted to their colliders (no stretched or squashed sprites; check
against the collider bounds numerically, not by eye)."

Nothing did that check. The gallery draws a human part as

    ArtLibrary.Tiled("Human/<part>", max(box))  +  SpriteDrawMode.Sliced  +  sr.size = box

and the level draws the finish sign as `Sliced` onto a 0.18 x 0.90 m piece
(LevelTest1.cs:3059) while the PNG is a 64 x 320 canvas - so wherever the PNG's
own aspect and the collider box's aspect disagree, the picture is scaled by a
different factor in X and in Y: squashed or stretched, silently.

This script reads the shipped bytes only - no Unity, no build, no run:

  * the part PNG's pixel size from the PNG header (IHDR), its spritePixelsToUnits
    from the `Assets/Resources/Art/**/<part>.png.meta` Unity wrote,
  * the shipping collider box from `Assets/Human.cs` (`public Vector2 <part>Size`),
  * the box the gallery actually uses, parsed out of `Assets/GalleryScene.cs`
    BuildHumanRow - so a copy that drifted from Human.cs is named, not assumed,
  * the sign's delivered box from `Assets/LevelTest1.cs`.

Usage
  python Tools/art1-fit-audit.py            # table + PASS/FAIL
  python Tools/art1-fit-audit.py --markdown  # the same table for the report
"""

from __future__ import annotations

import pathlib
import re
import struct
import sys

PROJECT = pathlib.Path(__file__).resolve().parent.parent
HUMAN_CS = PROJECT / "Assets" / "Human.cs"
GALLERY_CS = PROJECT / "Assets" / "GalleryScene.cs"
LEVELTEST_CS = PROJECT / "Assets" / "LevelTest1.cs"
ART = PROJECT / "Assets" / "Resources" / "Art"
LEVEL = PROJECT / "Assets" / "Resources" / "Level"

# part file name -> Human.cs field that carries the shipping collider box
PART_FIELD = {
    "head": "headSize",
    "neck": "neckSize",
    "torso": "torsoSize",
    "pelvis": "pelvisSize",
    "upper": "upperArmSize",
    "lower": "lowerArmSize",
    "hand": "handSize",
    "thigh": "thighSize",
    "shin": "shinSize",
    "foot": "footSize",
}

# a distortion under this is a rounding artefact, not a squashed picture
TOLERANCE = 0.02


def png_size(path: pathlib.Path) -> tuple[int, int]:
    """Pixel size straight out of the IHDR chunk (no PIL on this host)."""
    with path.open("rb") as fh:
        head = fh.read(24)
    if len(head) < 24 or head[:8] != b"\x89PNG\r\n\x1a\n" or head[12:16] != b"IHDR":
        raise SystemExit("FAIL not a PNG: " + str(path))
    width, height = struct.unpack(">II", head[16:24])
    return width, height


def alpha_bbox(path: pathlib.Path, threshold: int = 8) -> tuple[int, int, int, int] | None:
    """Bounding box of the painted pixels, decoded with zlib only.

    The importer (Tools/import-part-art.py) resizes the source PNG and never
    crops it to the alpha box, so a part's canvas is the artist's frame, not the
    silhouette: the canvas alone would under-report how far the VISIBLE body is
    stretched. Returns None for a picture without an alpha channel.
    """
    import zlib

    data = path.read_bytes()
    pos = 8
    idat = bytearray()
    width = height = depth = ctype = 0
    while pos + 8 <= len(data):
        (length,) = struct.unpack(">I", data[pos:pos + 4])
        ctag = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + length]
        if ctag == b"IHDR":
            width, height, depth, ctype = struct.unpack(">IIBB", body[:10])
        elif ctag == b"IDAT":
            idat += body
        elif ctag == b"IEND":
            break
        pos += 12 + length

    if depth != 8 or ctype not in (2, 6):
        return None
    channels = 4 if ctype == 6 else 3
    raw = zlib.decompress(bytes(idat))
    stride = width * channels
    prev = bytearray(stride)
    minx, miny, maxx, maxy = width, height, -1, -1
    off = 0
    for y in range(height):
        ftype = raw[off]
        off += 1
        line = bytearray(raw[off:off + stride])
        off += stride
        if ftype == 1:
            for i in range(channels, stride):
                line[i] = (line[i] + line[i - channels]) & 0xFF
        elif ftype == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif ftype == 3:
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 0xFF
        elif ftype == 4:
            for i in range(stride):
                a = line[i - channels] if i >= channels else 0
                b = prev[i]
                c = prev[i - channels] if i >= channels else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 0xFF
        for x in range(width):
            if ctype == 6:
                if line[x * 4 + 3] > threshold:
                    if x < minx:
                        minx = x
                    if x > maxx:
                        maxx = x
                    if y < miny:
                        miny = y
                    if y > maxy:
                        maxy = y
        prev = line
    if maxx < 0:
        return None
    return minx, miny, maxx - minx + 1, maxy - miny + 1


def sil_note(png: pathlib.Path, px: tuple[int, int]) -> str:
    """How much of the canvas is transparent frame around the painted body."""
    bb = alpha_bbox(png)
    if bb is None:
        return "no alpha"
    _, _, bw, bh = bb
    pad = 100.0 * (1.0 - (bw * bh) / float(px[0] * px[1]))
    return "%dx%d (%.0f%% pad)" % (bw, bh, pad)


def meta_ppu(path: pathlib.Path) -> float:
    """spritePixelsToUnits from the .meta Unity wrote next to the PNG."""
    text = path.read_text(encoding="utf-8", errors="replace")
    m = re.search(r"spritePixelsToUnits:\s*([0-9.]+)", text)
    if not m:
        raise SystemExit("FAIL no spritePixelsToUnits in " + str(path))
    return float(m.group(1))


def human_boxes() -> dict[str, tuple[float, float]]:
    text = HUMAN_CS.read_text(encoding="utf-8", errors="replace")
    boxes: dict[str, tuple[float, float]] = {}
    for m in re.finditer(
        r"public Vector2 (\w+Size)\s*=\s*new Vector2\(\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*\)", text
    ):
        boxes[m.group(1)] = (float(m.group(2)), float(m.group(3)))
    return boxes


def gallery_boxes() -> dict[str, tuple[float, float]]:
    """The boxes BuildHumanRow draws with, in the order the parts are listed."""
    text = GALLERY_CS.read_text(encoding="utf-8", errors="replace")
    part_list = re.search(r'string\[\] parts = \{([^}]*)\};', text)
    box_list = re.search(r"Vector2\[\] boxes\s*=\s*\{(.*?)\};", text, re.S)
    if not part_list or not box_list:
        raise SystemExit("FAIL cannot find the parts/boxes arrays in GalleryScene.cs")
    parts = re.findall(r'"([^"]+)"', part_list.group(1))
    pairs = re.findall(r"new Vector2\(\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*\)", box_list.group(1))
    if len(parts) != len(pairs):
        raise SystemExit(
            "FAIL GalleryScene.cs parts %d vs boxes %d" % (len(parts), len(pairs))
        )
    return {p: (float(a), float(b)) for p, (a, b) in zip(parts, pairs)}


def sign_boxes() -> tuple[tuple[float, float], tuple[float, float]] | None:
    """(native PNG box in metres, box the level draws it in)."""
    png = LEVEL / "sign_finish.png"
    if not png.exists():
        return None
    w, h = png_size(png)
    ppu = meta_ppu(png.with_suffix(".png.meta"))
    native = (w / ppu, h / ppu)
    text = LEVELTEST_CS.read_text(encoding="utf-8", errors="replace")
    # the FinishSign decor: `name = "FinishSign" ... size = new Vector2(0.18f, 0.90f)
    # ... kind = LevelArt.SignKey` - the sign is the only piece with that kind, so the
    # size is read off the line that names the kind, never off a neighbouring curb.
    line = next((ln for ln in text.splitlines() if "LevelArt.SignKey" in ln), None)
    if line is None:
        return None
    m = re.search(r"size\s*=\s*new Vector2\(\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*\)", line)
    if not m:
        return None
    return native, (float(m.group(1)), float(m.group(2)))


def fit(png_px: tuple[int, int], ppu: float, box: tuple[float, float]):
    """(natural metres, x scale, y scale, distortion max/min)."""
    natural = (png_px[0] / ppu, png_px[1] / ppu)
    sx = box[0] / natural[0]
    sy = box[1] / natural[1]
    dist = max(sx, sy) / min(sx, sy)
    return natural, sx, sy, dist


def main() -> int:
    markdown = "--markdown" in sys.argv
    boxes = human_boxes()
    drawn = gallery_boxes()

    rows = []
    failures = []

    for part, field in PART_FIELD.items():
        png = ART / "Human" / (part + ".png")
        if not png.exists():
            rows.append((part, "MISSING ART", "-", "-", "-", "-", "no file"))
            failures.append(part + " has no PNG at all")
            continue
        px = png_size(png)
        ppu = meta_ppu(png.with_suffix(".png.meta"))
        if field not in boxes:
            failures.append("Human.cs has no " + field)
            continue
        box = boxes[field]
        drawn_box = drawn.get(part)
        drift = ""
        if drawn_box is not None and (
            abs(drawn_box[0] - box[0]) > 1e-6 or abs(drawn_box[1] - box[1]) > 1e-6
        ):
            drift = "GALLERY COPY DRIFT %.3fx%.3f vs Human.cs %.3fx%.3f" % (
                drawn_box[0], drawn_box[1], box[0], box[1])
            failures.append(part + " " + drift)
        natural, sx, sy, dist = fit(px, ppu, box)
        bb = alpha_bbox(png)
        if bb is None:
            sil = "no alpha"
        else:
            _, _, bw, bh = bb
            pad = 100.0 * (1.0 - (bw * bh) / float(px[0] * px[1]))
            sil = "%dx%d (%.0f%% pad)" % (bw, bh, pad)
        state = "ok" if dist - 1.0 < TOLERANCE else ("SQUASHED %.2fx" % dist)
        if dist - 1.0 >= TOLERANCE:
            failures.append("%s drawn %.2fx off its own aspect" % (part, dist))
        rows.append((
            part,
            "%dx%d" % px,
            sil,
            "%.3fx%.3f" % natural,
            "%.3fx%.3f" % box,
            "%.2f/%.2f" % (sx, sy),
            state + (("  " + drift) if drift else ""),
        ))

    # the four level pictures: the sign is drawn at a box of its own
    sgn = sign_boxes()
    if sgn:
        native, box = sgn
        png = LEVEL / "sign_finish.png"
        px = png_size(png)
        ppu = meta_ppu(png.with_suffix(".png.meta"))
        _, sx, sy, dist = fit(px, ppu, box)
        state = "ok" if dist - 1.0 < TOLERANCE else ("SQUASHED %.2fx" % dist)
        if dist - 1.0 >= TOLERANCE:
            failures.append("sign_finish drawn %.2fx off its own aspect" % dist)
        rows.append((
            "sign_finish",
            "%dx%d" % px,
            sil_note(png, px),
            "%.2fx%.2f" % native,
            "%.2fx%.2f" % box,
            "%.2f/%.2f" % (sx, sy),
            state + "  (native texture -> the 0.18x0.90 m piece)",
        ))

    head = ["part", "png px", "alpha bbox", "natural m", "collider box m", "scale x/y", "verdict"]
    print("# ART-1 fit audit - is the real picture squashed onto its collider box?")
    print("# tolerance %.2f (a bigger x/y scale ratio than this is a distorted picture)" % TOLERANCE)
    if markdown:
        print("| " + " | ".join(head) + " |")
        print("|" + "---|" * len(head))
    for r in rows:
        print(("| " + " | ".join(r) + " |") if markdown else "  ".join(r))
    print()
    print("ART1-FIT parts=%d distorted=%d" % (len(rows), len(failures)))
    for f in failures:
        print("ART1-FIT FAIL " + f)
    return 0


if __name__ == "__main__":
    sys.exit(main())
