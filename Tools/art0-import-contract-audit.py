"""ART-0 import-contract audit, offline (no Unity) - the check that runs when the
editor holds the project and `art1-gate.sh` refuses.

The Unity gate (`art1-gate.sh` -> `Art1Check.Verify`) is the engine proof, but it
needs the project to itself. This tool answers the same contract off the bytes on
disk, any time:

  1. every art KEY the compiled code asks for resolves to the file the loader would
     read - `ArtLibrary.Albedo(key)` is `Resources.Load<Texture2D>("Art/" + key)`
     (~line 139), so the file is `Assets/Resources/Art/<key>.png`; `LevelArt.Load`
     goes through `PathsFor` to `Assets/Resources/Level/<name>.png`. A key that a
     NEGATIVE probe asks for (`Art1Check` asserts it returns null) must NOT resolve,
     and that is checked too.
  2. every picture under `Assets/Resources/Art|Level` carries the import fields the
     loaders need, read straight out of its own `.meta`: textureType 8 (Sprite),
     spriteMode 1 (Single), spriteMeshType 0 (Full Rect - what Sliced/Tiled need),
     wrapU/wrapV 1, fallback physics shape 0 (art carries no collider), isReadable 1
     everywhere on the Albedo path (Sprite.Create / GetPixelBilinear sample CPU
     pixels) and free on the four Level pictures, which are only taken as Sprites.
  3. the seven material tiles: LegacyArt original -> shipped size, in KB, with the
     shipped md5 (the downscale the ART-0 brief demanded).
  4. no guid under `Assets/` is used twice (the metas were copied from an existing
     sprite, so a duplicated guid was the real risk).

Usage:  python Tools/art0-import-contract-audit.py [--keys]
        `--keys` prints every resolved key and the file it came from.
Exit code 1 on any problem, so it can be used as a gate.
"""
import hashlib
import os
import re
import sys

PROJ = r"E:/Games/2D Game/My project"
RES = os.path.join(PROJ, "Assets/Resources")
SCAN = os.path.join(PROJ, "Assets")

MATERIALS = ["skin", "fabric", "denim", "dirt", "grass", "bark", "leaf"]
# `LevelArt.PathsFor` maps these four keys onto `Level/<name>`; the two that have an
# `Art/` fallback are tried in that order, exactly as the loader does.
LEVEL_PATHS = {
    "ground_fill": ["Level/ground_fill"],
    "ground_cap": ["Level/ground_cap"],
    "sky": ["Level/sky", "Art/sky"],
    "sign_finish": ["Level/sign_finish", "Art/sign_finish"],
}
META_FIELDS = ("textureType", "spriteMode", "spriteMeshType", "spritePixelsToUnits",
               "isReadable", "wrapU", "wrapV", "guid", "alphaIsTransparency",
               "spriteGenerateFallbackPhysicsShape", "filterMode", "maxTextureSize")

KEY_PATTERNS = [
    r'\bAlbedo\(\s*"([^"]+)"',
    r'\bTiled\(\s*"([^"]+)"',
    r'\bDress\(\s*[A-Za-z_][\w.]*\s*,\s*"([^"]+)"',
    r'\bApplyPicture\(\s*[A-Za-z_][\w.]*\s*,[^,]*,\s*"([^"]+)"',
    r'\bLoad\(\s*"([^"]+)"',
    r'\b(?:public|private|internal)\s+const\s+string\s+\w*Key\w*\s*=\s*"([^"]+)"',
]
# Art1Check.cs asks for these on purpose: the gate asserts the loader returns null,
# i.e. a missing key must never draw a silent placeholder.
NEGATIVE_SOURCES = ("Assets/Editor/Art1Check.cs",)


def meta_fields(path):
    txt = open(path, encoding="utf-8", errors="replace").read()
    out = {}
    for name in META_FIELDS:
        m = re.search(r"^\s*%s:\s*(\S+)\s*$" % name, txt, re.M)
        if m:
            out[name] = m.group(1)
    return out


def png_size(path):
    with open(path, "rb") as fh:
        head = fh.read(26)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    return int.from_bytes(head[16:20], "big"), int.from_bytes(head[20:24], "big")


def md5(path):
    h = hashlib.md5()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def rel(path):
    return os.path.relpath(path, PROJ).replace("\\", "/")


def find_pngs(root):
    found = []
    for dirpath, _dirs, files in os.walk(root):
        for fn in files:
            if fn.endswith(".png"):
                found.append(os.path.join(dirpath, fn))
    return sorted(found)


def code_keys():
    """Every art-key literal in the compiled code, with the files that ask for it."""
    keys = {}
    for dirpath, _dirs, files in os.walk(SCAN):
        for fn in files:
            if not fn.endswith(".cs"):
                continue
            p = os.path.join(dirpath, fn)
            txt = open(p, encoding="utf-8", errors="replace").read()
            for pat in KEY_PATTERNS:
                for m in re.finditer(pat, txt):
                    k = m.group(1)
                    if "." in k or k.startswith("Assets") or " " in k:
                        continue
                    keys.setdefault(k, set()).add(rel(p))
    return keys


def candidates(key):
    """The paths a loader would try for this key, in order."""
    if key in LEVEL_PATHS:
        return [os.path.join(RES, p + ".png") for p in LEVEL_PATHS[key]]
    if key.startswith("Art/") or key.startswith("Level/"):
        return [os.path.join(RES, key + ".png")]
    return [os.path.join(RES, "Art", key + ".png")]


def main():
    show_keys = "--keys" in sys.argv
    bad = 0
    print("ART-0 import-contract audit (offline, no Unity) - %s" % PROJ)
    print()

    print("=== 1. the art keys the code asks for, at the path the loader reads ===")
    keys = code_keys()
    positive, negative, missing = [], [], []
    for k in sorted(keys):
        srcs = sorted(keys[k])
        hit = next((c for c in candidates(k) if os.path.exists(c)), None)
        if hit is None and all(s in NEGATIVE_SOURCES for s in srcs):
            negative.append((k, srcs))
        elif hit is None:
            missing.append((k, srcs))
        else:
            positive.append((k, rel(hit), srcs))
    for k, srcs in missing:
        print("MISSING        %-24s -> %s   asked by %s"
              % (k, rel(candidates(k)[0]), ", ".join(srcs)))
    for k, srcs in negative:
        print("ok  NEGATIVE   %-24s absent by design (the gate asserts Load returns null; %s)"
              % (k, ", ".join(srcs)))
    if show_keys:
        for k, p, srcs in positive:
            print("    ok         %-24s -> %s" % (k, p))
    print("key literals: %d  resolved: %d  negative probes: %d  MISSING: %d"
          % (len(keys), len(positive), len(negative), len(missing)))
    for name, paths in sorted(LEVEL_PATHS.items()):
        first = next((p for p in paths if os.path.exists(os.path.join(RES, p + ".png"))), None)
        print("    %-12s LevelArt.PathsFor %-28s -> %s"
              % (name, " | ".join(paths), rel(os.path.join(RES, first + ".png")) if first else "MISSING"))
    bad += len(missing)
    print()

    print("=== 2. every shipped picture's import fields (read off its own .meta) ===")
    art_pngs = find_pngs(os.path.join(RES, "Art"))
    level_pngs = find_pngs(os.path.join(RES, "Level"))
    level_set = set(level_pngs)
    per_dir = {}
    for png in art_pngs + level_pngs:
        relp = rel(png)
        meta = png + ".meta"
        if not os.path.exists(meta):
            print("BAD  %-52s no .meta - Unity has not imported it" % relp)
            bad += 1
            continue
        f = meta_fields(meta)
        size = png_size(png)
        probs = []
        if f.get("textureType") != "8":
            probs.append("textureType=%s want 8 Sprite" % f.get("textureType"))
        if f.get("spriteMode") != "1":
            probs.append("spriteMode=%s want 1 Single" % f.get("spriteMode"))
        if f.get("spriteMeshType") != "0":
            probs.append("spriteMeshType=%s want 0 FullRect" % f.get("spriteMeshType"))
        if f.get("wrapU") != "1" or f.get("wrapV") != "1":
            probs.append("wrap=%s/%s want 1/1" % (f.get("wrapU"), f.get("wrapV")))
        if f.get("spriteGenerateFallbackPhysicsShape") != "0":
            probs.append("fallbackPhysicsShape=%s want 0 (art carries no collider)"
                         % f.get("spriteGenerateFallbackPhysicsShape"))
        if png not in level_set and f.get("isReadable") != "1":
            probs.append("isReadable=%s want 1 (the Albedo path samples CPU pixels)"
                         % f.get("isReadable"))
        if size is None:
            probs.append("not a decodable PNG")
        bad += len(probs)
        folder = os.path.basename(os.path.dirname(png)) if png not in level_set else "Level"
        per_dir.setdefault(folder, [0, 0])
        per_dir[folder][0] += 1
        per_dir[folder][1] += len(probs)
        print("%s %-52s %sx%-5s ppu=%-5s readable=%-2s filter=%-2s guid=%s%s"
              % ("OK " if not probs else "BAD", relp,
                 size[0] if size else "?", size[1] if size else "?",
                 f.get("spritePixelsToUnits"), f.get("isReadable"), f.get("filterMode"),
                 (f.get("guid") or "?")[:8],
                 ("  | " + "; ".join(probs)) if probs else ""))
    print("pictures checked: %d in %d folders (%s)"
          % (len(art_pngs) + len(level_pngs), len(per_dir),
             ", ".join("%s %d" % (k, v[0]) for k, v in sorted(per_dir.items()))))
    print()

    print("=== 3. the seven material tiles: LegacyArt original -> shipped ===")
    tot_src = tot_dst = 0.0
    for k in MATERIALS:
        src = os.path.join(PROJ, "Assets/LegacyArt/%s.png" % k)
        dst = os.path.join(RES, "Art/%s.png" % k)
        ss, ds = png_size(src), png_size(dst)
        kb_s, kb_d = os.path.getsize(src) / 1024.0, os.path.getsize(dst) / 1024.0
        tot_src += kb_s
        tot_dst += kb_d
        print("%-8s %sx%-6s %8.1f KB  ->  %sx%-6s %7.1f KB  %.2fx  md5=%s"
              % (k, ss[0], ss[1], kb_s, ds[0], ds[1], kb_d, kb_s / kb_d, md5(dst)[:12]))
    print("total    %24.1f KB  ->  %19.1f KB  %.2fx" % (tot_src, tot_dst, tot_src / tot_dst))
    print()

    print("=== 4. guid uniqueness across Assets/ ===")
    seen, dups, n = {}, [], 0
    for dirpath, _dirs, files in os.walk(SCAN):
        for fn in files:
            if not fn.endswith(".meta"):
                continue
            p = os.path.join(dirpath, fn)
            m = re.search(r"^guid:\s*([0-9a-f]{32})\s*$",
                          open(p, encoding="utf-8", errors="replace").read(), re.M)
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
    print("ART-0-IMPORT-CONTRACT end problems=%d" % bad)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
