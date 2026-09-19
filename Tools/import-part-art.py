#!/usr/bin/env python3
"""ART-1a - make the per-part art reachable at runtime.

The 62 real PNGs live in Assets/LegacyArt, which is NOT a Resources folder, so
Resources.Load cannot see them (ART-0 finding 2). This script imports the
per-part subset into Assets/Resources/Art/<Group>/<part>.png - the exact key
space ArtLibrary already uses ("Human/head" -> Art/Human/head.png) - and writes
a Sprite .meta for each file, copied from the shape ART-0 already proved
(Assets/Resources/Art/bark.png.meta).

Rules:
- the seven material tiles (skin fabric denim dirt grass bark leaf) are NOT
  touched: ART-0 owns them;
- a part is downscaled to at most --max-side pixels (default 512) with LANCZOS.
  The parts are stretched onto collider boxes of a few centimetres, so 1024-2048
  px sources buy no visible detail and cost build weight (ART-0 finding 4 made
  the same call for the material tiles);
- guids are md5(key), deterministic, and the run fails loudly if a generated
  guid already exists in the project.

Run:  python Tools/import-part-art.py [--max-side 512] [--dry-run]
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

# ART-0 owns these; do not re-import them here.
MATERIALS = {"skin", "fabric", "denim", "dirt", "grass", "bark", "leaf"}

GROUPS = ("Human", "Dog", "Crow", "Chicken", "Hedgehog", "Apple", "Strawberry")


def guid_for(key: str) -> str:
    return hashlib.md5(("ART1A:" + key).encode("utf-8")).hexdigest()


def build_meta(template: str, guid: str, ppu: int, max_side: int) -> str:
    out = template
    out = re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + guid, out, count=1, flags=re.M)
    out = re.sub(r"^  spriteID: [0-9a-f]{32}$", "  spriteID: " + guid + "0800000000000000",
                 out, count=1, flags=re.M)
    out = re.sub(r"^  spritePixelsToUnits: \d+$", "  spritePixelsToUnits: %d" % ppu,
                 out, count=1, flags=re.M)
    out = re.sub(r"^  isReadable: \d$", "  isReadable: 1", out, count=1, flags=re.M)
    out = re.sub(r"^  alphaIsTransparency: \d$", "  alphaIsTransparency: 1", out, count=1, flags=re.M)
    out = re.sub(r"^  maxTextureSize: \d+$", "  maxTextureSize: %d" % max_side, out, count=1, flags=re.M)
    out = re.sub(r"^    maxTextureSize: \d+$", "    maxTextureSize: %d" % max_side, out, flags=re.M)
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
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    with open(META_TEMPLATE, encoding="utf-8") as fh:
        template = fh.read()

    taken = existing_guids()
    used_here = {}
    rows = []
    failures = []

    for group in GROUPS:
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
                    fh.write(build_meta(template, guid, 100, args.max_side))

            rows.append((key, w, h, out_size[0], out_size[1],
                         os.path.getsize(src) // 1024,
                         os.path.getsize(dst) // 1024 if not args.dry_run else -1,
                         guid[:8]))

    print("%-26s %11s %11s %8s %8s %s" % ("key", "source", "import", "srcKB", "dstKB", "guid"))
    for key, w, h, ow, oh, skb, dkb, g in rows:
        print("%-26s %5dx%-5d %5dx%-5d %8d %8d %s" % (key, w, h, ow, oh, skb, dkb, g))
    print("imported %d part files, %d KB -> %d KB" % (
        len(rows), sum(r[5] for r in rows), sum(max(0, r[6]) for r in rows)))

    if failures:
        for f in failures:
            print("FAIL " + f)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
