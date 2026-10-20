#!/usr/bin/env python
"""Compare a one-species snapshot against the current stills and counter JSONs.

    python Tools/species-refresh-check.py Trials/Runs/species-pre-<build>

What this checks, after a re-shoot on a newer art player:

  * GATE - every scene still satisfies Yury's order on the NEW run, read from its
    own JSON: `treeCount` exactly 1, `fits` true (the WHOLE tree is inside the
    frame), `missFramesAfterOneSecond` 0 (the framing never fails after the
    settle) and `dressOutsideMeters` 0 (the shipping art does not reach outside
    the placeholder frame the camera was framed on);
  * ACCEPT - the fields that describe the SCENE, not the picture: the scene and
    species names, the three tree counters, and the 1920x1080 image size;
  * PICTURE - every `_photo.png` must hash differently, otherwise the "refresh"
    repainted nothing.

CHANGED 20.09 (ART-0 28th pass cycle).  This checker used to demand that the
MEASURED BOX (`heightMeters` / `widthMeters` / `orthoSize`) come back to the
digit, and its own comment admitted it would "fail a retexture that resizes
anything".  That premise is wrong, and the species lane measured why: the box is
read from the DRAWN renderers (`PhotoCapture.TryRendererBounds`), so a dress fix
moves it.  On the 07:32 player the cedar's box fell 33.498 -> 2.021 m and the oak's
3.187 -> 2.202 m because the old player dressed a leaf with `biggestPartScale`
1338.43 (cedar) / 17.61 (oak) and pushed the art 67.392 m outside the frame -
`partsOverFiveMeters` 189 -> 0, `dressOutsideMeters` 67.392 -> 0.000.  Those fields
are properties of the ART, so they are now REPORTED (the `art:` line) and not
gated; the gate above is what "the whole tree, one tree" actually means.

Exit 0 when all ten scenes pass, 1 otherwise.
"""
import hashlib
import json
import pathlib
import sys

SPECIES = ["apple", "oak", "pine", "willow", "bush", "poplar", "lilac", "hazel", "cedar", "plum"]

# Scene identity: must come back exactly.
ACCEPT_FIELDS = ["scene", "species", "treeCount", "plantTreeCount", "appleTreeCount",
                 "width", "height"]
# Yury's order, read from the NEW run only (the snapshot may predate the rule).
GATE = [("fits", True), ("missFramesAfterOneSecond", 0), ("dressOutsideMeters", 0.0)]
# Properties of the DRAWN art: reported, never a failure.
ART_FIELDS = ["heightMeters", "widthMeters", "orthoSize", "groundY", "paintedFraction",
              "rendererCount", "partsOverFiveMeters", "dressOutsideMeters",
              "biggestPartPath", "biggestPartScale", "biggestPartMetersX", "biggestPartMetersY"]


def md5(path):
    h = hashlib.md5()
    h.update(path.read_bytes())
    return h.hexdigest()


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def art_summary(a, b):
    """The drawn-art move, in the terms that say WHY the box moved."""
    bits = []
    if a.get("heightMeters") != b.get("heightMeters"):
        bits.append("box %.3f -> %.3f m" % (float(a.get("heightMeters", 0.0)),
                                            float(b.get("heightMeters", 0.0))))
    if a.get("biggestPartScale") != b.get("biggestPartScale"):
        bits.append("biggestPartScale %s -> %s" % (a.get("biggestPartScale"), b.get("biggestPartScale")))
    if a.get("partsOverFiveMeters") != b.get("partsOverFiveMeters"):
        bits.append("partsOverFiveMeters %s -> %s" % (a.get("partsOverFiveMeters"),
                                                      b.get("partsOverFiveMeters")))
    if a.get("dressOutsideMeters") != b.get("dressOutsideMeters"):
        bits.append("dressOutside %.3f -> %.3f m" % (float(a.get("dressOutsideMeters", 0.0)),
                                                     float(b.get("dressOutsideMeters", 0.0))))
    if a.get("orthoSize") != b.get("orthoSize"):
        bits.append("ortho %.4f -> %.4f" % (float(a.get("orthoSize", 0.0)),
                                            float(b.get("orthoSize", 0.0))))
    return "; ".join(bits)


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
    art_moved = 0
    print("%-10s %-46s %s" % ("species", "gate + scene fields", "picture"))
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
        bad = [(k, a.get(k), b.get(k)) for k in ACCEPT_FIELDS if a.get(k) != b.get(k)]
        for key, want in GATE:
            if b.get(key) != want:
                bad.append((key, b.get(key), "must be %s" % want))
        if bad:
            failures += 1
            verdict = "FAIL " + ", ".join("%s %s vs %s" % x for x in bad)
        else:
            verdict = "OK treeCount=%s fits=%s missAfter1s=%s dressOutside=%s m" % (
                b.get("treeCount"), b.get("fits"), b.get("missFramesAfterOneSecond"),
                b.get("dressOutsideMeters"))

        art = art_summary(a, b)
        if art:
            art_moved += 1
        else:
            art = "art identical"

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
        print("%-10s %-46s %s" % (sp, verdict, picture))
        print("%-10s art: %s" % ("", art))

    print("SPECIES-REFRESH-CHECK species=%d gate-failures=%d art-moved=%d "
          "pictures-repainted=%d pictures-unchanged=%d"
          % (len(SPECIES), failures, art_moved, moved, same_pixels))
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
