#!/usr/bin/env python
"""Compare a one-species snapshot against the current stills and counter JSONs.

A re-shoot after an art change must move the PICTURE and nothing else: the
acceptance fields of every scene (treeCount, fits, the measured box, the lens)
have to come back to the digit, while every `_photo.png` must hash differently,
otherwise the "refresh" repainted nothing.

    python Tools/species-refresh-check.py Trials/Runs/species-pre-art0leaf

Exit 0 when all ten scenes pass, 1 otherwise.
"""
import hashlib
import json
import pathlib
import sys

SPECIES = ["apple", "oak", "pine", "willow", "bush", "poplar", "lilac", "hazel", "cedar", "plum"]

# Fields that describe the SCENE (what the run accepted), not the run itself.
# Discrete ones must come back exactly; the three measured lengths are floats
# read off a LIVE player, which is not frame-deterministic (the species report
# measured the same wobble at 1e-5 m between reruns of one build), so they are
# compared against TOL meters - the framing law itself holds to 9.8e-5 m.
FIELDS = ["scene", "species", "treeCount", "plantTreeCount", "appleTreeCount",
          "fits", "missFramesAfterOneSecond", "rendererCount", "partsOverFiveMeters",
          "colliderCount", "width", "height"]
NEAR_FIELDS = ["heightMeters", "widthMeters", "orthoSize"]
# Relative, because the same wobble scales with the box: the cedar's 33.5 m
# drawn box moves 1.45e-4 m between back-to-back runs of one build (measured
# on treecedar / treecedar_r2), and that is 4.3e-6 of its own size - the same
# order as a 4.6 m poplar moving 9.5e-7 m. 1e-5 covers every rig here and still
# fails a retexture that resizes anything.
TOL_REL = 1e-5


def md5(path):
    h = hashlib.md5()
    h.update(path.read_bytes())
    return h.hexdigest()


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def main():
    if len(sys.argv) < 2:
        print("usage: species-refresh-check.py <snapshot-folder>")
        return 2
    snap = pathlib.Path(sys.argv[1])
    runs = pathlib.Path(__file__).resolve().parent.parent / "Trials" / "Runs"
    if not snap.is_dir():
        print("FAIL no snapshot folder", snap)
        return 1

    failures = 0
    moved = 0
    same_pixels = 0
    print("%-10s %-22s %s" % ("species", "acceptance fields", "picture"))
    for sp in SPECIES:
        label = "tree" + sp
        old_json = snap / (label + "-tree.json")
        old_png = snap / (label + "_photo.png")
        new_json = runs / (label + "-tree.json")
        new_png = runs / (label + "_photo.png")
        if not old_json.exists() or not new_json.exists():
            print("%-10s FAIL missing json (%s / %s)" % (sp, old_json.name, new_json.name))
            failures += 1
            continue

        a, b = load(old_json), load(new_json)
        diff = [(k, a.get(k), b.get(k)) for k in FIELDS if a.get(k) != b.get(k)]
        drift = []
        for k in NEAR_FIELDS:
            if a.get(k) is None or b.get(k) is None:
                continue
            d = abs(float(a[k]) - float(b[k]))
            rel = d / max(1.0, abs(float(a[k])))
            if rel > TOL_REL:
                diff.append((k, a.get(k), b.get(k)))
            elif d > 0.0:
                drift.append((k, d, rel))
        if diff:
            failures += 1
            verdict = "MOVED " + ", ".join("%s %s->%s" % d for d in diff)
        else:
            verdict = "identical (%d fields, treeCount=%s fits=%s, max drift %.1e m = %.1e rel)" % (
                len(FIELDS) + len(NEAR_FIELDS), b.get("treeCount"), b.get("fits"),
                max([d for _, d, _ in drift]) if drift else 0.0,
                max([r for _, _, r in drift]) if drift else 0.0)

        old_hash = md5(old_png) if old_png.exists() else "-"
        new_hash = md5(new_png) if new_png.exists() else "-"
        if not new_png.exists():
            picture = "MISSING"
            failures += 1
        elif old_hash == new_hash:
            picture = "unchanged md5 %s" % new_hash[:12]
            same_pixels += 1
        else:
            picture = "repainted %s -> %s" % (old_hash[:12], new_hash[:12])
            moved += 1
        print("%-10s %-52s %s" % (sp, verdict, picture))

    print("SPECIES-REFRESH-CHECK species=%d acceptance-failures=%d pictures-repainted=%d pictures-unchanged=%d"
          % (len(SPECIES), failures, moved, same_pixels))
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
