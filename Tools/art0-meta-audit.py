"""ART-0 offline audit: the import contract of the shipped pictures,
read straight out of the .meta bytes (no Unity, so it can run any time).

For every file the loaders read it prints the fields the loaders depend on:
  textureType 8 = Sprite, spriteMode 1 = Single, spriteMeshType 0 = Full Rect
  (Sliced / Tiled need Full Rect), spritePixelsToUnits, isReadable (the seven
  material tiles are sampled with GetPixelBilinear, so they MUST be readable),
  wrapU/wrapV, spriteGenerateFallbackPhysicsShape 0 (art carries no collider).
It also checks that no guid in Assets/ is used twice and reports pixel size
and file size of every picture against its LegacyArt original.
"""
import hashlib
import os
import re
import sys

PROJ = r"E:/Games/2D Game/My project"
LEVEL = ["ground_fill", "ground_cap", "sky", "sign_finish"]
MATERIALS = ["skin", "fabric", "denim", "dirt", "grass", "bark", "leaf"]
EXTRA = [("Art/Campfire/core.png", "Campfire/core"),
         ("Art/Campfire/log_a.png", "Campfire/log_a"),
         ("Art/Campfire/log_b.png", "Campfire/log_b"),
         ("Art/Campfire/log_c.png", "Campfire/log_c"),
         # ART-0 gap 3 (20.09): the flower's bloom. Luminance canvases (shape in alpha),
         # read through Albedo -> Tiled -> Sprite.Create, so they must be readable.
         ("Art/Flower/petal.png", "Flower/petal"),
         ("Art/Flower/petal_tip.png", "Flower/petal_tip"),
         ("Art/Flower/cup.png", "Flower/cup"),
         ("Art/Flower/calyx.png", "Flower/calyx"),
         ("Art/Flower/disk.png", "Flower/disk"),
         # ART-0 gap 3b (20.09): the flower's foliage - one canvas per leaflet family, the
         # same luminance convention (shape in alpha, the kind's tint multiplies it).
         ("Art/Flower/leaf.png", "Flower/leaf"),
         ("Art/Flower/leaf_erect.png", "Flower/leaf_erect"),
         # ART-0 gap 3b, the last two rows (24th pass, 20.09): the lavender spike and the
         # autumn hip. Same luminance convention (shape in alpha, the kind's tint multiplies
         # it), so they must be readable too.
         ("Art/Flower/spike.png", "Flower/spike"),
         ("Art/Flower/hip.png", "Flower/hip"),
         # ART-0 gap 3b, the last flat element (25th pass, 20.09): the stalk and the two corm
         # kinds' stem. Same luminance convention (shape in alpha, the kind's tint multiplies it),
         # so they must be readable too.
         ("Art/Flower/stem.png", "Flower/stem"),
         ("Art/Flower/stem_stout.png", "Flower/stem_stout")]
EXPECT_PPU = {"ground_fill": 128, "ground_cap": 100, "sky": 32, "sign_finish": 100}
# Everything on the Albedo path needs CPU pixels (Sprite.Create / GetPixelBilinear); the
# four Level pictures are only ever taken as Sprites, so they are not readable on purpose.
EXPECT_READABLE = {k: "1" for k in MATERIALS}
EXPECT_READABLE.update({key: "1" for _, key in EXTRA})


def meta_fields(path):
    txt = open(path, encoding="utf-8", errors="replace").read()
    fields = {}
    for name in ("textureType", "spriteMode", "spriteMeshType", "spritePixelsToUnits",
                 "isReadable", "wrapU", "wrapV", "guid", "alphaIsTransparency",
                 "spriteGenerateFallbackPhysicsShape", "filterMode", "maxTextureSize"):
        m = re.search(r"^\s*%s:\s*(\S+)\s*$" % name, txt, re.M)
        if m:
            fields[name] = m.group(1)
    return fields


def png_size(path):
    with open(path, "rb") as fh:
        head = fh.read(26)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    w = int.from_bytes(head[16:20], "big")
    h = int.from_bytes(head[20:24], "big")
    return w, h


def md5(path):
    h = hashlib.md5()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def kb(path):
    return os.path.getsize(path) / 1024.0


def main():
    bad = 0
    print("=== shipped pictures: import fields ===")
    rows = [("Art/%s.png" % k, k) for k in MATERIALS]
    rows += [("Level/%s.png" % k, k) for k in LEVEL]
    rows += EXTRA
    for rel, key in rows:
        png = os.path.join(PROJ, "Assets/Resources", rel)
        meta = png + ".meta"
        if not os.path.exists(png) or not os.path.exists(meta):
            print("MISSING FILE  %-28s png=%s meta=%s" % (rel, os.path.exists(png), os.path.exists(meta)))
            bad += 1
            continue
        f = meta_fields(meta)
        size = png_size(png)
        want_readable = EXPECT_READABLE.get(key)
        problems = []
        if f.get("textureType") != "8":
            problems.append("textureType=%s (want 8 Sprite)" % f.get("textureType"))
        if f.get("spriteMode") != "1":
            problems.append("spriteMode=%s (want 1 Single)" % f.get("spriteMode"))
        if f.get("spriteMeshType") != "0":
            problems.append("spriteMeshType=%s (want 0 FullRect)" % f.get("spriteMeshType"))
        if f.get("wrapU") != "1" or f.get("wrapV") != "1":
            problems.append("wrapU/V=%s/%s (want 1/1 Repeat)" % (f.get("wrapU"), f.get("wrapV")))
        if f.get("spriteGenerateFallbackPhysicsShape") != "0":
            problems.append("fallbackPhysicsShape=%s (want 0 - art carries no collider)"
                            % f.get("spriteGenerateFallbackPhysicsShape"))
        if want_readable is not None and f.get("isReadable") != want_readable:
            problems.append("isReadable=%s (want %s - Sample/GetPixelBilinear)" % (f.get("isReadable"), want_readable))
        if key in EXPECT_PPU and f.get("spritePixelsToUnits") != str(EXPECT_PPU[key]):
            problems.append("ppu=%s (want %s)" % (f.get("spritePixelsToUnits"), EXPECT_PPU[key]))
        status = "OK " if not problems else "BAD"
        bad += len(problems)
        print("%s %-28s %sx%s ppu=%-4s readable=%-2s wrap=%s/%s physShape=%s guid=%s %s"
              % (status, rel, size[0] if size else "?", size[1] if size else "?",
                 f.get("spritePixelsToUnits"), f.get("isReadable"),
                 f.get("wrapU"), f.get("wrapV"), f.get("spriteGenerateFallbackPhysicsShape"),
                 (f.get("guid") or "?")[:8], ("| " + "; ".join(problems)) if problems else ""))

    print()
    print("=== material tiles: LegacyArt original -> shipped 512 ===")
    total_src = total_dst = 0.0
    for k in MATERIALS:
        src = os.path.join(PROJ, "Assets/LegacyArt/%s.png" % k)
        dst = os.path.join(PROJ, "Assets/Resources/Art/%s.png" % k)
        if not os.path.exists(src):
            print("no LegacyArt source for %s" % k)
            continue
        ss, ds = png_size(src), png_size(dst)
        total_src += kb(src)
        total_dst += kb(dst)
        print("%-8s %sx%s %8.1f KB  ->  %sx%s %7.1f KB  %.2fx  md5=%s"
              % (k, ss[0], ss[1], kb(src), ds[0], ds[1], kb(dst), kb(src) / kb(dst), md5(dst)[:12]))
    print("total    %25.1f KB  ->  %18.1f KB  %.2fx" % (total_src, total_dst, total_src / total_dst))

    print()
    print("=== guid uniqueness across Assets/ ===")
    seen = {}
    dups = []
    n = 0
    for root, _dirs, files in os.walk(os.path.join(PROJ, "Assets")):
        for fn in files:
            if not fn.endswith(".meta"):
                continue
            p = os.path.join(root, fn)
            m = re.search(r"^guid:\s*([0-9a-f]{32})\s*$", open(p, encoding="utf-8", errors="replace").read(), re.M)
            if not m:
                continue
            n += 1
            g = m.group(1)
            if g in seen:
                dups.append((g, seen[g], p))
            else:
                seen[g] = p
    print("meta files with a guid: %d, distinct: %d, duplicates: %d" % (n, len(seen), len(dups)))
    for g, a, b in dups[:20]:
        print("DUP %s\n    %s\n    %s" % (g, a, b))
    bad += len(dups)

    print()
    print("ART-0-META-AUDIT end problems=%d" % bad)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
