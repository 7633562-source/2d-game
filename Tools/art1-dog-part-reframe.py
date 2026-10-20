#!/usr/bin/env python3
"""ART-1 dog row: fit each delivered part PNG to its shipping collider box.

The gallery draws a part as `ArtLibrary.Tiled(key, max(box))` + `Sliced` + `size = box`,
so the WHOLE canvas is scaled onto the box: the picture is distorted exactly by
`canvas_aspect / box_aspect` (measured by `Tools/art1-fit-audit.py`, the same check the
human parts pass at 1.00). The delivered dog canvases sit at 1.5000 (wide parts) and
0.6660 (limb parts) against boxes of 0.3529..4.0000 - a 0.58x..1.89x squash.

This tool re-frames, it does not repaint:

  * the painted body is scaled UNIFORMLY (one factor for x and y - its own proportions
    are preserved to the pixel) so that it fills the box in the binding dimension;
  * never upscaled (a delivered body keeps its own resolution);
  * the canvas becomes the box' aspect, the body centred, the rest transparent margin;
  * the .meta's `spritePixelsToUnits` is set so the canvas' own world size IS the box,
    exactly the contract `Tools/import-part-art.py` writes for the human parts.

The PNG's guid is left alone. Running it twice changes nothing (the second run finds the
canvas already at the box aspect with the body filling the binding dimension and leaves
the bytes alone). The delivered bytes are copied to `Trials/Runs/<label>-before/` with
their md5s first, so the before/after is auditable.

  python Tools/art1-dog-part-reframe.py [--dry-run] [--label art1dog13] [--parts chest,...]

Verdict: DOG-REFRAME OK|FAIL parts=N worst_aspect_error=0.0000 worst_fit_loss=...
"""
from __future__ import annotations

import argparse
import hashlib
import pathlib
import re
import shutil
import sys

import numpy as np
from PIL import Image

PROJECT = pathlib.Path(__file__).resolve().parent.parent
RUNS = PROJECT / "Trials" / "Runs"
ART = PROJECT / "Assets" / "Resources" / "Art" / "Dog"
DOG_CS = PROJECT / "Assets" / "Dog.cs"

# part file -> the Dog.cs field that carries its shipping collider box (the same
# authority Dog.CreateSegment passes to HumanSegment: collider.size and the drawn box
# are both this size). Duplicated on purpose from Tools/art1-fit-audit.py's dog half:
# the audit is the CHECKER and must not import the generator.
PART_FIELD = {
    "chest": "chestSize",
    "pelvis": "pelvisSize",
    "neck": "neckSize",
    "head": "headSize",
    "tail": "tailSize",
    "front_upper": "frontUpperSize",
    "front_lower": "frontLowerSize",
    "thigh": "rearThighSize",
    "shin": "rearShinSize",
    "paw": "pawSize",
}

MAX_SIDE = 512          # the importer's cap, kept: the human parts are capped the same way
ALPHA_FIT = 16          # "any ink": the fit must never clip even the faint fringe
ALPHA_BODY = 220        # the solid body, for the fill/pad report
ASPECT_EPS = 0.0015     # the importer's own ppu check
IDEMPOTENT_EPS = 0.002  # below this the canvas already IS the box aspect


def dog_boxes() -> dict[str, tuple[float, float]]:
    """The shipping collider boxes out of Assets/Dog.cs, constants resolved.

    Six of the ten fields carry a named constant for their second value
    (`chestSize = new Vector2(0.36f, DefaultChestH)`), so the file's own
    `const float Default*` lines are read first - the authority stays Dog.cs.
    """
    text = DOG_CS.read_text(encoding="utf-8", errors="replace")
    consts: dict[str, float] = {}
    for m in re.finditer(r"const float (\w+)\s*=\s*([0-9.]+)f", text):
        consts[m.group(1)] = float(m.group(2))

    def value(token: str) -> float | None:
        token = token.strip()
        m = re.match(r"([0-9.]+)f?$", token)
        if m:
            return float(m.group(1))
        return consts.get(token)

    boxes: dict[str, tuple[float, float]] = {}
    for m in re.finditer(
        r"public Vector2 (\w+Size)\s*=\s*new Vector2\(\s*([0-9.]+f|\w+)\s*,\s*([0-9.]+f|\w+)\s*\)",
        text,
    ):
        w = value(m.group(2))
        h = value(m.group(3))
        if w is not None and h is not None:
            boxes[m.group(1)] = (w, h)
    return boxes


def bbox(img: Image.Image, threshold: int):
    a = np.asarray(img)[:, :, 3]
    ys, xs = np.nonzero(a >= threshold)
    if len(xs) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def canvas_for(body: tuple[int, int], aspect: float, cap: int = MAX_SIDE):
    """The smallest integer canvas of the box' aspect that holds the body, searched in a
    small window so a rational aspect (1.25, 4.0) lands on an exact pair; falls back to
    the least-error pair within the window."""
    best = None
    for w in range(body[0], min(cap, body[0] + 64) + 1):
        h = max(body[1], int(round(w / aspect)))
        if h > cap:
            break
        err = abs((w / h) / aspect - 1.0)
        if best is None or err < best[2] - 1e-12:
            best = (w, h, err)
        if err < 1e-9:
            break
    if best is not None and best[2] < 2e-3:
        return best[0], best[1], best[2]
    for h in range(body[1], min(cap, body[1] + 64) + 1):
        w = max(body[0], int(round(h * aspect)))
        if w > cap:
            break
        err = abs((w / h) / aspect - 1.0)
        if best is None or err < best[2] - 1e-12:
            best = (w, h, err)
        if err < 1e-9:
            break
    return best


def reframe(img: Image.Image, box: tuple[float, float]) -> tuple[Image.Image, dict]:
    bb = bbox(img, ALPHA_FIT)
    bw, bh = bb[2] - bb[0], bb[3] - bb[1]
    k0 = MAX_SIDE / max(box[0], box[1])
    s0 = min(box[0] * k0 / bw, box[1] * k0 / bh)
    s = min(1.0, s0)
    body = (max(1, int(bw * s)), max(1, int(bh * s)))
    canvas_w, canvas_h, aspect_error = canvas_for(body, box[0] / box[1])
    canvas = (canvas_w, canvas_h)
    piece = img.crop(bb).resize(body, Image.LANCZOS)
    out = Image.new("RGBA", canvas, (0, 0, 0, 0))
    out.alpha_composite(piece, ((canvas[0] - body[0]) // 2, (canvas[1] - body[1]) // 2))
    solid = bbox(out, ALPHA_BODY)
    fill = ((solid[2] - solid[0]) * (solid[3] - solid[1]) / float(canvas[0] * canvas[1])
            if solid else 0.0)
    return out, {
        "body_src": (bw, bh), "scale": s, "body_px": body, "canvas": canvas,
        "fill": fill, "aspect_error": aspect_error,
    }


def meta_ppu_set(meta: pathlib.Path, ppu: int) -> str:
    text = meta.read_text(encoding="utf-8")
    return re.sub(r"^(  spritePixelsToUnits: )[0-9.]+$", r"\g<1>%d" % ppu, text,
                  count=1, flags=re.M)


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--label", default="art1dog13")
    ap.add_argument("--parts", default=",".join(PART_FIELD))
    args = ap.parse_args(argv)

    boxes = dog_boxes()
    wanted = [p.strip() for p in args.parts.split(",") if p.strip()]
    before_dir = RUNS / f"{args.label}-before"
    if not args.dry_run:
        before_dir.mkdir(parents=True, exist_ok=True)

    worst_aspect = 0.0
    worst_fit_loss = 0.0
    failures = []
    print(f"{'part':<12} {'box':>13} {'src canvas':>11} {'body src':>10} {'scale':>6} "
          f"{'canvas':>10} {'fill':>5} {'aspect err':>10} {'ppu':>6}  note")
    for part in wanted:
        if part not in PART_FIELD:
            failures.append(f"unknown part {part}")
            continue
        field = PART_FIELD[part]
        if field not in boxes:
            failures.append(f"Dog.cs has no {field} for {part}")
            continue
        box = boxes[field]
        png = ART / f"{part}.png"
        meta = ART / f"{part}.png.meta"
        img = Image.open(png).convert("RGBA")
        src_canvas = (img.width, img.height)
        src_aspect_err = abs((src_canvas[0] / src_canvas[1]) / (box[0] / box[1]) - 1.0)

        # idempotence: the canvas already at the box aspect AND the solid body filling
        # the binding dimension -> it IS this tool's output; do not touch the bytes.
        bb = bbox(img, ALPHA_FIT)
        solid = bbox(img, ALPHA_BODY)
        if solid:
            fill_x = (solid[2] - solid[0]) / float(img.width)
            fill_y = (solid[3] - solid[1]) / float(img.height)
        else:
            fill_x = fill_y = 0.0
        if src_aspect_err < IDEMPOTENT_EPS and max(fill_x, fill_y) > 0.985:
            print(f"{part:<12} {box[0]:.3f}x{box[1]:.3f} {src_canvas[0]}x{src_canvas[1]:>4} "
                  f"{'already reframed':<40} ppu kept  (no change)")
            continue

        out, info = reframe(img, box)
        ppu = max(1, int(round(out.width / box[0])))
        ppu_err_x = abs(out.width / ppu - box[0])
        ppu_err_y = abs(out.height / ppu - box[1])
        note = ""
        if ppu_err_x > ASPECT_EPS or ppu_err_y > ASPECT_EPS:
            failures.append(f"{part} ppu {ppu} gives {out.width / ppu:.4f}x{out.height / ppu:.4f} m, "
                            f"box {box[0]:.4f}x{box[1]:.4f} m")
            note = "PPU OUT OF TOLERANCE"
        if info["scale"] >= 0.999 and info["body_px"] != info["body_src"]:
            note += " (floor rounding)"
        if not args.dry_run:
            if not (before_dir / png.name).exists():
                shutil.copy2(png, before_dir / png.name)
            if not (before_dir / meta.name).exists():
                shutil.copy2(meta, before_dir / meta.name)
            out.save(png, "PNG", optimize=True)
            meta.write_text(meta_ppu_set(meta, ppu), encoding="utf-8", newline="\n")
        worst_aspect = max(worst_aspect, info["aspect_error"])
        worst_fit_loss = max(worst_fit_loss, src_aspect_err - info["aspect_error"])
        print(f"{part:<12} {box[0]:.3f}x{box[1]:.3f} {src_canvas[0]}x{src_canvas[1]:>4} "
              f"{info['body_src'][0]}x{info['body_src'][1]:<5} {info['scale']:6.3f} "
              f"{info['canvas'][0]}x{info['canvas'][1]:<6} {info['fill'] * 100:4.0f}% "
              f"{info['aspect_error']:10.5f} {ppu:6d}  {note}")

    print(f"\nDOG-REFRAME {'OK' if not failures else 'FAIL'} parts={len(wanted)} "
          f"worst_aspect_error={worst_aspect:.5f} worst_stretch_removed={worst_fit_loss:.4f} "
          f"failures={len(failures)}")
    for f in failures:
        print("DOG-REFRAME FAIL " + f)
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
