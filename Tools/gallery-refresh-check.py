#!/usr/bin/env python3
"""ART-1 gallery evidence: what a gallery capture labels, and how two labels compare.

Read-only, no Unity. Usage:

  python Tools/gallery-refresh-check.py                       # newest two labels
  python Tools/gallery-refresh-check.py gallery5 gallery6     # explicit pair

It prints, for the NEW label: every top-level key of <label>-gallery.json, the
RowBounds/Stills entries it carries, the pixel size of every <label>_*.png still
(decoded from the PNG's own IHDR, no Pillow), and the PNG's mtime against the
preview player's build time - so a still that predates the build that is supposed
to have produced it is named, not assumed.

Then it diffs the two labels field by field. A field that moves is printed with
both values; the acceptance rows (realItems, missingItems, dressedRenderers) are
reported as MOVED or IDENTICAL on purpose.
"""
from __future__ import annotations

import json
import pathlib
import struct
import sys

PROJECT = pathlib.Path(__file__).resolve().parent.parent
RUNS = PROJECT / "Trials" / "Runs"
PREVIEW = PROJECT / "Build" / "Preview" / "GalleryPreview_Data" / "resources.assets"

# The rows ART-1 keeps an eye on: their painted fraction is what the eye judges.
WATCH = ("realItems", "missingItems", "dressedRenderers")


def labels_by_mtime() -> list[str]:
    files = sorted(RUNS.glob("*-gallery.json"), key=lambda p: p.stat().st_mtime)
    return [f.name[: -len("-gallery.json")] for f in files]


def load(label: str) -> dict:
    p = RUNS / (label + "-gallery.json")
    if not p.exists():
        raise SystemExit("FAIL no capture JSON at " + str(p))
    return json.loads(p.read_text(encoding="utf-8-sig"))


def png_size(path: pathlib.Path) -> tuple[int, int] | None:
    with path.open("rb") as fh:
        head = fh.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    return struct.unpack(">II", head[16:24])


def list_key(item: dict, fallback: int) -> str:
    for k in ("name", "target", "object", "key", "label"):
        if isinstance(item, dict) and k in item:
            return str(item[k])
    return "#%d" % fallback


def diff_lists(old: list, new: list, indent: str = "      ") -> int:
    """Per-entry diff of two list values, matched by their own name field."""
    oi = {list_key(v, i): v for i, v in enumerate(old) if isinstance(v, dict)}
    ni = {list_key(v, i): v for i, v in enumerate(new) if isinstance(v, dict)}
    moved = 0
    for name in sorted(set(oi) | set(ni)):
        a, b = oi.get(name), ni.get(name)
        if a is None or b is None:
            print("%s%s %s" % (indent, "ONLY-" + ("new" if a is None else "old"), name))
            moved += 1
            continue
        fields = [k for k in sorted(set(a) | set(b)) if a.get(k) != b.get(k)]
        if fields:
            moved += 1
            print("%s%s:" % (indent, name))
            for k in fields:
                print("%s  %-16s %r -> %r" % (indent, k, a.get(k), b.get(k)))
    return moved


def main() -> int:
    args = sys.argv[1:]
    if len(args) >= 2:
        old_label, new_label = args[0], args[1]
    else:
        labels = labels_by_mtime()
        if len(labels) < 2:
            raise SystemExit("FAIL need two *-gallery.json labels")
        old_label, new_label = labels[-2], labels[-1]

    new = load(new_label)
    old = load(old_label)

    build_time = PREVIEW.stat().st_mtime if PREVIEW.exists() else None
    print("GALLERY-REFRESH old=%s new=%s" % (old_label, new_label))
    print("  preview player resources.assets: %s (%s)"
          % ("missing" if build_time is None else _ts(build_time),
             "-" if build_time is None else pathlib.Path(PREVIEW).name))
    for key in sorted(new.keys()):
        if isinstance(new[key], (list, dict)):
            print("  %-20s %s (len %d)" % (key, type(new[key]).__name__, len(new[key])))
        else:
            print("  %-20s %s" % (key, new[key]))

    stills = sorted(RUNS.glob(new_label + "_*.png"))
    bad = 0
    print("  stills: %d" % len(stills))
    for s in stills:
        size = png_size(s)
        mtime = s.stat().st_mtime
        stale = build_time is not None and mtime < build_time
        ok = size == (1920, 1080) and not stale
        bad += 0 if ok else 1
        print("    %-34s %s %s %s"
              % (s.name, "%dx%d" % size if size else "not-png",
                 _ts(mtime), "STALE" if stale else "after-build"))
    pan = RUNS / (new_label + "_pan.mp4")
    print("  pan: %s %s" % (pan.name if pan.exists() else "MISSING",
                            "" if not pan.exists() else "%d B" % pan.stat().st_size))

    moved = 0
    print("  fields vs %s:" % old_label)
    for key in sorted(set(new) | set(old)):
        a, b = old.get(key), new.get(key)
        if isinstance(a, (list, dict)) or isinstance(b, (list, dict)):
            if isinstance(a, list) and isinstance(b, list):
                print("    %-20s list (%d -> %d)" % (key, len(a), len(b)))
                n = diff_lists(a, b)
                print("    %-20s %s" % ("", "identical" if n == 0 else "MOVED (%d entries)" % n))
                moved += 0 if n == 0 else 1
                continue
            same = a == b
            print("    %-20s %s" % (key, "identical" if same else "MOVED (list/dict)"))
            moved += 0 if same else 1
            continue
        same = a == b
        tag = "identical" if same else "MOVED"
        watch = " *" if key in WATCH else ""
        moved += 0 if same else 1
        print("    %-20s %-9s %s%s" % (key, tag, "" if same else "%r -> %r" % (a, b), watch))

    verdict = "OK" if bad == 0 else "FAIL"
    print("GALLERY-REFRESH-CHECK %s stills_bad=%d fields_moved=%d" % (verdict, bad, moved))
    return 0 if bad == 0 else 2


def _ts(t: float) -> str:
    import datetime
    return datetime.datetime.fromtimestamp(t).strftime("%H:%M:%S")


if __name__ == "__main__":
    raise SystemExit(main())
