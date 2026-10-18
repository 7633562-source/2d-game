#!/usr/bin/env python3
"""ART-1a / ART-1 item 2 - make the per-part art reachable at runtime AND fit.

The 62 real PNGs live in Assets/LegacyArt, which is NOT a Resources folder, so
Resources.Load cannot see them (ART-0 finding 2). This script imports the
per-part subset into Assets/Resources/Art/<Group>/<part>.png - the exact key
space ArtLibrary already uses ("Human/head" -> Art/Human/head.png) - and writes
a Sprite .meta for each file, copied from the shape ART-0 already proved
(Assets/Resources/Art/bark.png.meta).

ART-1 acceptance item 2 (added 19.09): a human part is drawn by `Sliced` onto
its shipping collider box (`HumanSegment` and `GalleryScene` both set
`sr.size` = the box from `Assets/Human.cs`), so a canvas whose OWN aspect
differs from the box's is scaled by a different factor in X and in Y - the
picture is squashed (measured, `Tools/art1-fit-audit.py`). A `Human/<part>`
canvas is therefore padded with transparent margin to the box's aspect, and its
.meta `spritePixelsToUnits` is set so the canvas' own world size IS the box:
then `sx == sy`, the picture keeps its proportions, and the collider contract
(canvas == box) survives untouched. No painted pixel is moved or repainted - the
body simply stops being stretched. `Tools/art1-fit-audit.py` is the checker.

Rules:
- the seven material tiles (skin fabric denim dirt grass bark leaf) are NOT
  touched: ART-0 owns them;
- a part is downscaled to at most --max-side pixels (default 512) with LANCZOS.
  The parts are stretched onto collider boxes of a few centimetres, so 1024-2048
  px sources buy no visible detail and cost build weight (ART-0 finding 4 made
  the same call for the material tiles); padding happens BEFORE that cap, so the
  cap can never break the box aspect;
- groups other than Human have no collider box of their own in `Human.cs`, so
  they are imported unchanged (their rig binding is ART-3);
- guids are md5(key), deterministic, and the run fails loudly if a generated
  guid already exists in the project.

Run:  python Tools/import-part-art.py [--max-side 512] [--groups Human] [--dry-run]
"""

import argparse
import hashlib
import os
import re
import sys

from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC_ROOT = os.path.join(PROJECT, "Assets", "LegacyArt")
DST_ROOT = os.path.join(PROJECT, "Assets", "Resources", "Art")
META_TEMPLATE = os.path.join(DST_ROOT, "bark.png.meta")
HUMAN_CS = os.path.join(PROJECT, "Assets", "Human.cs")

# ART-0 owns these; do not re-import them here.
MATERIALS = {"skin", "fabric", "denim", "dirt", "grass", "bark", "leaf"}

GROUPS = ("Human", "Dog", "Crow", "Chicken", "Hedgehog", "Apple", "Strawberry")

# part file name -> the Human.cs field that carries its shipping collider box.
# This map is duplicated from Tools/art1-fit-audit.py on purpose: the audit is
# the CHECKER (it reads the shipped bytes and the drawn box), the importer must
# not import the checker and silently agree with itself. Both read the same
# authority, `Assets/Human.cs`.
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


def guid_for(key: str) -> str:
    return hashlib.md5(("ART1A:" + key).encode("utf-8")).hexdigest()


def human_boxes():
    """The shipping collider boxes, straight out of Assets/Human.cs."""
    with open(HUMAN_CS, encoding="utf-8", errors="replace") as fh:
        text = fh.read()
    boxes = {}
    for m in re.finditer(
        r"public Vector2 (\w+Size)\s*=\s*new Vector2\(\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*\)", text
    ):
        boxes[m.group(1)] = (float(m.group(2)), float(m.group(3)))
    return boxes


def pad_to_aspect(im, aspect):
    """Transparent margin so the canvas aspect equals the collider box aspect."""
    w, h = im.size
    if w <= 0 or h <= 0 or aspect <= 0.0:
        return im, (0, 0)
    if w / float(h) < aspect:
        nw, nh = int(round(h * aspect)), h
    else:
        nw, nh = w, int(round(w / aspect))
    if (nw, nh) == (w, h):
        return im, (0, 0)
    canvas = Image.new("RGBA", (nw, nh), (0, 0, 0, 0))
    canvas.paste(im, ((nw - w) // 2, (nh - h) // 2))
    return canvas, (nw - w, nh - h)


def build_meta(template: str, guid: str, ppu, max_side: int) -> str:
    out = template
    out = re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + guid, out, count=1, flags=re.M)
    out = re.sub(r"^  spriteID: [0-9a-f]{32}$", "  spriteID: " + guid + "0800000000000000",
                 out, count=1, flags=re.M)
    out = re.sub(r"^  spritePixelsToUnits: [0-9.]+$", "  spritePixelsToUnits: %s" % ppu,
                 out, count=1, flags=re.M)
    out = re.sub(r"^  isReadable: \d$", "  isReadable: 1", out, count=1, flags=re.M)
    out = re.sub(r"^  alphaIsTransparency: \d$", "  alphaIsTransparency: 1", out, count=1, flags=re.M)
    out = re.sub(r"^  maxTextureSize: \d+$", "  maxTextureSize: %d" % max_side,
                 out, count=1, flags=re.M)
    out = re.sub(r"^    maxTextureSize: \d+$", "    maxTextureSize: %d" % max_side,
                 out, count=1, flags=re.M)
    return out


def existing_guids():
    guids = {}
    for dirpath, _dirs, files in os.walk(os.path.join(PROJECT, "Assets")):
        for f in files:
            if not f.endswith(".meta"):
                continue
            p = os.path.join(dirpath, f)
            try:
                with open(p, encoding="utf-8", errors="replace") as fh:
                    head = fh.read(400)
            except OSError:
                continue
            m = re.search(r"^guid: ([0-9a-f]{32})$", head, flags=re.M)
            if m:
                guids.setdefault(m.group(1), []).append(os.path.relpath(p, PROJECT))
    return guids


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--max-side", type=int, default=512)
    ap.add_argument("--groups", default=",".join(GROUPS),
                    help="comma-separated subset of the groups to import")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    wanted = [g.strip() for g in args.groups.split(",") if g.strip()]
    unknown = [g for g in wanted if g not in GROUPS]
    if unknown:
        print("FAIL unknown group(s): " + ", ".join(unknown))
        return 1

    with open(META_TEMPLATE, encoding="utf-8") as fh:
        template = fh.read()

    boxes = human_boxes()
    taken = existing_guids()
    used_here = {}
    rows = []
    failures = []

    for group in wanted:
        src_dir = os.path.join(SRC_ROOT, group)
        if not os.path.isdir(src_dir):
            failures.append("missing source group " + group)
            continue
        for name in sorted(os.listdir(src_dir)):
            if not name.lower().endswith(".png"):
                continue
            stem = os.path.splitext(name)[0]
            if group == "Materials" or (group == "" and stem in MATERIALS):
                continue
            key = "%s/%s" % (group, stem)
            if stem in MATERIALS and group == "":
                continue

            src = os.path.join(src_dir, name)
            dst_dir = os.path.join(DST_ROOT, group)
            dst = os.path.join(dst_dir, name)
            meta = dst + ".meta"

            with Image.open(src) as im:
                w, h = im.size
                scale = min(1.0, args.max_side / float(max(w, h)))
                out_size = (max(1, int(round(w * scale))), max(1, int(round(h * scale))))
                if out_size == (w, h):
                    converted = im.convert("RGBA")
                else:
                    converted = im.convert("RGBA").resize(out_size, Image.LANCZOS)

            # ART-1 item 2: canvas aspect == shipping collider box aspect.
            box = None
            pad = (0, 0)
            ppu = 100
            note = ""
            if group == "Human" and stem in PART_FIELD:
                field = PART_FIELD[stem]
                if field not in boxes:
                    failures.append("Human.cs has no %s for Human/%s" % (field, stem))
                    continue
                box = boxes[field]
                converted, pad = pad_to_aspect(converted, box[0] / box[1])
                pad_note = "pad%d,%d" % pad
                cw, ch = converted.size
                if max(cw, ch) > args.max_side:
                    k = args.max_side / float(max(cw, ch))
                    cw2, ch2 = max(1, int(round(cw * k))), max(1, int(round(ch * k)))
                    converted = converted.resize((cw2, ch2), Image.LANCZOS)
                    pad_note += " cap%d" % cw2
                ppu = max(1, int(round(converted.size[0] / box[0])))
                note = "%.3fx%.3f %s ppu%d" % (box[0], box[1], pad_note, ppu)
                # the canvas' own world size must BE the box, to the pixel
                if abs(converted.size[0] / float(ppu) - box[0]) > 0.0015:
                    failures.append("Human/%s ppu %d gives %.4f m, box %.4f m"
                                    % (stem, ppu, converted.size[0] / float(ppu), box[0]))

            guid = guid_for(key)
            if guid in taken and os.path.relpath(meta, PROJECT) not in taken[guid]:
                failures.append("guid collision for %s: %s" % (key, taken[guid]))
            if guid in used_here:
                failures.append("guid reused in this run: %s and %s" % (used_here[guid], key))
            used_here[guid] = key

            if not args.dry_run:
                os.makedirs(dst_dir, exist_ok=True)
                converted.save(dst, "PNG", optimize=True)
                with open(meta, "w", encoding="utf-8", newline="\n") as fh:
                    fh.write(build_meta(template, guid, "%d" % ppu if isinstance(ppu, int)
                                        else ppu, args.max_side))

            rows.append((key, w, h, converted.size[0], converted.size[1],
                         os.path.getsize(src) // 1024,
                         os.path.getsize(dst) // 1024 if not args.dry_run else -1,
                         guid[:8],
                         note))

    print("%-26s %11s %11s %8s %8s %8s  %s" % ("key", "source", "canvas", "srcKB", "dstKB",
                                               "guid", "collider box / pad / ppu"))
    for key, w, h, ow, oh, skb, dkb, g, note in rows:
        print("%-26s %5dx%-5d %5dx%-5d %8d %8d %8s  %s" % (key, w, h, ow, oh, skb, dkb, g, note))
    print("imported %d part files, %d KB -> %d KB (groups %s)" % (
        len(rows), sum(r[5] for r in rows), sum(max(0, r[6]) for r in rows), ",".join(wanted)))

    if failures:
        for f in failures:
            print("FAIL " + f)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
