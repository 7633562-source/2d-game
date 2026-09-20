#!/usr/bin/env python3
"""Measure what the SHIPPED hip canvases actually draw, and what the pre-squash has to be.

    python Tools/art0-hip-form-probe.py [shipped|all]

Two questions, both answered from bytes rather than from a comment:

1. WHAT IS ON DISK. For `Assets/Resources/Art/Flower/hip*.png`: the canvas size, the opaque
   form's bbox as box fractions (du, dv) and the drawn world form per kind, computed the way
   `ArtParts.Dress` draws it - `Sliced`, `size = the part's box`, so canvas u lands on box.x
   and v on box.y and the drawn form is `(du * box.x, dv * box.y)` in metres. If the three
   canvases were painted with the same fractions, the world form differs by the BOX aspect
   alone (0.6957 Tulip .. 1.4062 Poppy = 2.02x) and the family split bought sampling only.

2. WHAT THE PRE-SQUASH IS. `hip_form_scale(name)` - the factor each family canvas has to paint
   its form WIDER by (horizontal box fractions only) so that every kind draws the same world
   form as the accepted round-family hip. It is the aspect ratio of the round family's mean box
   over the family's own mean box, and this probe prints it per family together with the
   residual (the intra-family box spread), which is what the split can reach.

Read-only: it writes nothing and touches no asset.
"""
import importlib.util
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GEN = os.path.join(ROOT, "Tools", "gen-flower-spike-textures.py")
FLOWER = os.path.join(ROOT, "Assets", "Resources", "Art", "Flower")


def load_gen():
    """The generator's own functions (live_box, family_box, hip_members, form_bbox, BLOOM_SIZE).

    Imported, not copied: the numbers below are the ones the painter itself works with, so this
    probe cannot drift away from the generator the way a transcribed table would.
    """
    spec = importlib.util.spec_from_file_location("gen_flower_spike", GEN)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def form_fractions(path):
    img = Image.open(path).convert("RGBA")
    a = np.asarray(img)[:, :, 3]
    ys, xs = np.nonzero(a > 4)
    if len(xs) == 0:
        return img.size, 0.0, 0.0
    return (img.size,
            (xs.max() - xs.min() + 1) / float(img.width),
            (ys.max() - ys.min() + 1) / float(img.height))


def main():
    gen = load_gen()
    names = gen.HIP_CANVASES if len(sys.argv) < 2 or sys.argv[1] != "all" else gen.HIP_CANVASES

    print("the live autumn boxes the dress reads (from the generator's own live_box):")
    for kind, season, box in sorted(gen.hip_members(), key=lambda m: m[2][0] / m[2][1]):
        print("  %-10s %-7s box %.4f x %.4f m  aspect %.4f  canvas asks %s"
              % (kind, season, box[0], box[1], box[0] / box[1], gen.hip_family(box[0] / box[1])))
    print()

    ref_box = gen.family_box("hip")
    ref_aspect = ref_box[0] / ref_box[1]
    print("family mean boxes (geometric mean of the live boxes of the family):")
    scales = {}
    for name in names:
        bx, by = gen.family_box(name)
        scales[name] = ref_aspect / (bx / by)
        print("  %-9s mean box %.4f x %.4f m  aspect %.4f  members %d  hip_form_scale %.3f"
              % (name, bx, by, bx / by, len(gen.family_members(name)), scales[name]))
    print()

    print("what is on disk now (opaque form as box fractions, drawn world form per kind):")
    shipped = {}
    for name in names:
        path = os.path.join(FLOWER, name + ".png")
        if not os.path.exists(path):
            print("  %-9s MISSING" % name)
            continue
        size, du, dv = form_fractions(path)
        shipped[name] = (du, dv, size)
        print("  %-9s %-9s form %.3f x %.3f of the box" % (name, "%dx%d" % size, du, dv))
    print()

    if "hip" not in shipped:
        return 1

    ref_du, ref_dv, _ = shipped["hip"]
    ref_world = (ref_du * ref_box[0]) / (ref_dv * ref_box[1])
    print("the accepted round hip draws world form aspect %.3f; per kind, canvas by canvas:"
          % ref_world)
    for name in names:
        if name not in shipped:
            continue
        du, dv, _ = shipped[name]
        worst = 0.0
        kinds = gen.family_members(name)
        for kind, season, box in sorted(kinds, key=lambda m: m[2][0] / m[2][1], reverse=True):
            world = (du * box[0]) / (dv * box[1])
            row = max(world / ref_world, ref_world / world)
            worst = max(worst, row)
            print("  %-9s %-16s box %.4f x %.4f  world form %.3f  vs the round hip %.3fx"
                  % (name, kind + " " + season, box[0], box[1], world, row))
        print("  %-9s worst %.3fx over %d kinds" % (name, worst, len(kinds)))
    print()
    print("WHICH canvas a kind is drawn with is the dress' own choice (`ArtParts.HipKeyFor` on the")
    print("same box aspect); each canvas' form is pre-squashed for its family's mean box, so the")
    print("residual above is that family's own box spread and not the canvas'.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
