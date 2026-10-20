#!/usr/bin/env python3
"""ART-1 dog row: read every dog part PNG against the box it will be drawn on.

The gallery's CREATURES row draws the dog's ten part PNGs the way the HUMAN row
draws its parts: `ArtLibrary.Tiled(key, max(box))` + `Sliced` + `size = box`, so
the WHOLE canvas is stretched onto the box. That mapping is only distortion-free
when the canvas aspect equals the box aspect, which is what this probe measures.

Boxes are the shipping collider sizes from `Assets/Dog.cs` (fields chestSize,
pelvisSize, ... pawSize; file names per .cursor/tasks/ART-DOG.md).

  python Tools/art1-dog-parts-probe.py

Verdict line: DOG-PARTS-READ OK|FAIL parts=N worst_stretch=x.xxxx
"""
from __future__ import annotations

import json
import pathlib
import re
import sys

import numpy as np
from PIL import Image

PROJECT = pathlib.Path(__file__).resolve().parent.parent
ART = PROJECT / "Assets" / "Resources" / "Art" / "Dog"

# part file -> box (w, h) in metres, from Assets/Dog.cs [Header("Segment sizes (collider, m)")]
PARTS = [
    ("chest", (0.36, 0.14), "chestSize"),
    ("pelvis", (0.22, 0.13), "pelvisSize"),
    ("neck", (0.10, 0.08), "neckSize"),
    ("head", (0.14, 0.10), "headSize"),
    ("tail", (0.20, 0.05), "tailSize"),
    ("front_upper", (0.07, 0.17), "frontUpperSize"),
    ("front_lower", (0.06, 0.17), "frontLowerSize"),
    ("thigh", (0.08, 0.17), "rearThighSize"),
    ("shin", (0.06, 0.17), "rearShinSize"),
    ("paw", (0.10, 0.05), "pawSize"),
]


def meta_fields(meta_path: pathlib.Path) -> dict:
    text = meta_path.read_text(encoding="utf-8", errors="replace")
    keys = ["textureType", "spriteMode", "spriteMeshType", "wrapU", "wrapV",
            "isReadable", "spritePixelsToUnits", "spriteGenerateFallbackPhysicsShape",
            "filterMode", "alphaIsTransparency"]
    out = {}
    for key in keys:
        m = re.search(rf"\b{key}:\s*(-?\d+)", text)
        if m:
            out[key] = int(m.group(1))
    return out


def read_part(name: str, box: tuple[float, float], field: str) -> dict:
    png = ART / f"{name}.png"
    if not png.exists():
        return {"part": name, "exists": False}
    img = Image.open(png).convert("RGBA")
    arr = np.asarray(img)
    w, h = img.size
    alpha = arr[:, :, 3]
    a16 = alpha >= 16
    a220 = alpha >= 220
    coverage = float(a16.mean())
    ys, xs = np.nonzero(a220)
    bbox = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1) if len(xs) else None
    bbox_wh = (bbox[2] - bbox[0], bbox[3] - bbox[1]) if bbox else (0, 0)
    canvas_aspect = w / h
    box_aspect = box[0] / box[1]
    bbox_aspect = (bbox_wh[0] / bbox_wh[1]) if bbox_wh[1] else 0.0
    meta = meta_fields(png.with_suffix(".png.meta")) if png.with_suffix(".png.meta").exists() else {}
    return {
        "part": name, "exists": True, "file": f"Art/Dog/{name}.png",
        "box_m": [box[0], box[1]], "box_field": field,
        "canvas": [w, h], "canvas_aspect": round(canvas_aspect, 4),
        "box_aspect": round(box_aspect, 4),
        "stretch": round(canvas_aspect / box_aspect, 4),
        "alpha220_bbox": bbox, "alpha220_bbox_wh": [bbox_wh[0], bbox_wh[1]],
        "bbox_aspect": round(bbox_aspect, 4),
        "bbox_stretch": round(bbox_aspect / box_aspect, 4) if box_aspect else 0.0,
        "coverage_a16": round(coverage, 4),
        "px_per_m_canvas": [round(w / box[0], 1), round(h / box[1], 1)],
        "meta": meta,
    }


def main(argv: list[str]) -> int:
    rows = [read_part(name, box, field) for name, box, field in PARTS]
    worst = 0.0
    worst_bbox = 0.0
    problems = 0
    for row in rows:
        if not row["exists"]:
            print(f"MISSING FILE {row['part']}")
            problems += 1
            continue
        print(f"{row['part']:<12} canvas={row['canvas'][0]}x{row['canvas'][1]}"
              f" aspect={row['canvas_aspect']:<7} box={row['box_m'][0]}x{row['box_m'][1]}"
              f" box_aspect={row['box_aspect']:<7} stretch={row['stretch']:<7}"
              f" bbox={row['alpha220_bbox_wh'][0]}x{row['alpha220_bbox_wh'][1]}"
              f" bbox_stretch={row['bbox_stretch']:<7} cover={row['coverage_a16']}"
              f" px/m={row['px_per_m_canvas'][0]}x{row['px_per_m_canvas'][1]}")
        meta = row["meta"]
        print(f"             meta textureType={meta.get('textureType')}"
              f" spriteMode={meta.get('spriteMode')} meshType={meta.get('spriteMeshType')}"
              f" wrap={meta.get('wrapU')}/{meta.get('wrapV')} readable={meta.get('isReadable')}"
              f" ppu={meta.get('spritePixelsToUnits')} fallbackShape={meta.get('spriteGenerateFallbackPhysicsShape')}")
        worst = max(worst, abs(row["stretch"]))
        worst_bbox = max(worst_bbox, abs(row["bbox_stretch"]))
        if row["coverage_a16"] < 0.35:
            print(f"             NOTE {row['part']}: thin alpha coverage {row['coverage_a16']}")

    payload = {"parts": rows, "worst_stretch": worst, "worst_bbox_stretch": worst_bbox}
    out = PROJECT / "Trials" / "Runs" / "art1-dog-parts-probe.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(payload, indent=1), encoding="utf-8")
    print(f"json {out}")
    verdict = "OK" if problems == 0 else "FAIL"
    print(f"DOG-PARTS-READ {verdict} parts={len(rows)} worst_canvas_stretch={worst:.4f}"
          f" worst_opaque_stretch={worst_bbox:.4f} missing={problems}")
    return 0 if problems == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
