#!/usr/bin/env python3
"""ART-H1 - does a joint close at every angle? Pure geometry, before any art is painted.

The round's hard property (Yury's sheet, worked on the shin-foot example): at every
joint, at every rotation angle, NO visible gap between the two neighbouring segments'
textures.

The model: a joint is two rectangles - the shipping collider boxes of `Assets/Human.cs`,
which is what the segment textures are mapped onto - turning about the hinge. The
question this tool asks is the one the eye asks:

    is background LEFT INSIDE the joint's own disc?

The disc's radius is the joint's own scale, `min(ink half-width of the two neighbours)`:
two boxes that share only their end edge can open a wedge whose mouth sits at the
narrower neighbour's edge, i.e. exactly that far from the hinge. A joint is CLOSED when
every pixel of that disc is inked by one of the two pictures.

The pictures are read from the ACTUAL sources: each canvas' painted area (alpha) gives
its ink - the half-width at the joint end, and the GUTTER, how far short of its own box
end the paint stops. The gutter is why these joints are open today: the inventory
(`Tools/art-h1-part-inventory.py`) measures 41-69 mm of it on the limb canvases.

The four variants swept are the sheet's own four drawings:

    none    both pictures stop at the hinge  -> two boxes on one point, both wedges open
    parent  the proximal picture overhangs the hinge by m, rounded
    child   the distal picture overhangs by m, rounded
    both    the sheet's "account for the overlap": both overhang by m, rounded, and the
            narrower one's end disappears inside the wider one (the shin in the boot)

Run:  python Tools/art-h1-joint-fit.py [--json out.json] [--scale 1000]
"""
import argparse
import json
import os
import re
import sys

from PIL import Image, ImageDraw

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "Resources", "Art", "Human")
HUMAN_CS = os.path.join(PROJECT, "Assets", "Human.cs")

SCALE = 1000.0        # px per metre in the model
GAME_PXM = 250.0      # px per metre in the game (the gallery 'human' lens, floor case)

PART_FIELD = {
    "head": "headSize", "neck": "neckSize", "torso": "torsoSize",
    "pelvis": "pelvisSize", "upper": "upperArmSize", "lower": "lowerArmSize",
    "hand": "handSize", "thigh": "thighSize", "shin": "shinSize", "foot": "footSize",
}

# The rig's chain (Human.cs BuildHuman / CreateArm / CreateLeg). `end` names the end of a
# box the hinge sits on: "top" = +y of the box, "bottom" = -y, "side" = the hinge is
# INSIDE the box (the shoulder in the chest, the MTP inside the foot plate).
# `span` = the rig's own limits in degrees. `v` = the hinge's lateral offset (metres).
JOINTS = [
    ("lumbar",   "pelvis", "top",    "torso", "bottom", (-20, 20),   0.0),
    ("neck",     "torso",  "top",    "neck",  "bottom", (-30, 30),   0.0),
    ("head",     "neck",   "top",    "head",  "bottom", (-30, 30),   0.0),
    ("shoulder", "torso",  "side",   "upper", "top",    (-160, 90), -0.032),
    ("elbow",    "upper",  "bottom", "lower", "top",    (-165, 0),   0.0),
    ("wrist",    "lower",  "bottom", "hand",  "top",    (-60, 60),   0.0),
    ("hip",      "pelvis", "bottom", "thigh", "top",    (-90, 45),   0.0148),
    ("knee",     "thigh",  "bottom", "shin",  "top",    (0, 120),    0.0),
    ("ankle",    "shin",   "bottom", "foot",  "top",    (-45, 45),  -0.026),
]


def boxes():
    with open(HUMAN_CS, encoding="utf-8", errors="replace") as fh:
        text = fh.read()
    out = {}
    for m in re.finditer(
        r"public Vector2 (\w+Size)\s*=\s*new Vector2\(\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*\)",
        text,
    ):
        out[m.group(1)] = (float(m.group(2)), float(m.group(3)))
    return out


def measure(part):
    """The canvas' box and its painted area, in metres, in the box' own frame.

    World Y of canvas row r: y = h/2 - (r + 0.5) / ppu. `gap_top` is how far below the
    box' top edge the paint starts; `gap_bottom` how far above the bottom edge it stops.
    `ink_half_*` is the painted half-width in the first/last painted row.
    """
    path = os.path.join(ART, part + ".png")
    if not os.path.exists(path):
        return None
    im = Image.open(path).convert("RGBA")
    with open(path + ".meta", encoding="utf-8", errors="replace") as fh:
        m = re.search(r"^  spritePixelsToUnits: ([0-9.]+)$", fh.read(), flags=re.M)
    ppu = float(m.group(1))
    px = im.split()[3].load()
    w, h = im.width / ppu, im.height / ppu
    rows = []
    for y in range(im.height):
        xs = [x for x in range(im.width) if px[x, y] > 16]
        rows.append(xs)
    top = next((i for i, xs in enumerate(rows) if xs), None)
    bot_row = next((len(rows) - 1 - i for i, xs in enumerate(reversed(rows)) if xs), None)
    if top is None:
        return None
    # The very end row of a painted limb is often a rounded sliver, so the END's width is
    # the widest ink within `within_m` of that end - that is the band the joint's disc
    # has to be covered by.
    within = int(round(0.02 * ppu))
    half_top = max((rows[i][-1] - rows[i][0]) * 0.5 / ppu
                   for i in range(top, min(len(rows), top + within + 1)) if rows[i])
    half_bot = max((rows[i][-1] - rows[i][0]) * 0.5 / ppu
                   for i in range(max(0, bot_row - within), bot_row + 1) if rows[i])
    return {
        "w": w, "h": h, "ppu": ppu,
        "ink_half_top": half_top,
        "ink_half_bottom": half_bot,
        "gap_top": (top + 0.5) / ppu,
        "gap_bottom": (len(rows) - 1 - bot_row + 0.5) / ppu,
    }


def variant_masks(p, c, theta_deg, variant, mp, mc, scale, win):
    """Two masks of one joint, in the joint frame: +u from the parent into the child.

    p / c: dicts with half (the ink's half-width at the joint end), gap (the ink's
    gutter from the hinge) and len (the box' length). mp / mc: each side's skirt length.
    The four variants are the sheet's four drawings: "none" (both stop at the hinge),
    "parent" / "child" (one side overhangs), "both" (the overlap).
    """
    w = int(round(win * 2 * scale))
    off = win * scale
    img_p = Image.new("L", (w, w), 0)
    img_c = Image.new("L", (w, w), 0)

    def band(d, u0, u1, half, tip_u=None):
        x0, x1 = off + min(u0, u1) * scale, off + max(u0, u1) * scale
        y0, y1 = off - half * scale, off + half * scale
        d.rectangle([x0, y0, x1, y1], fill=255)
        if tip_u is not None:
            cx = off + tip_u * scale
            d.pieslice([cx - half * scale, y0, cx + half * scale, y1],
                       -90, 90, fill=255)

    # parent: u <= -gap, plus (a skirt) the run up to the hinge and m_past it, rounded
    dp = ImageDraw.Draw(img_p)
    if variant in ("parent", "both"):
        band(dp, -p["len"], -p["gap"], p["half"])
        band(dp, -p["gap"], mp, p["half"], tip_u=mp)
    else:
        band(dp, -p["len"], -p["gap"], p["half"])

    # child: u >= +gap, plus (a skirt) a run from m behind the hinge, rounded
    dc = ImageDraw.Draw(img_c)
    if variant in ("child", "both"):
        band(dc, c["gap"], c["len"], c["half"])
        band(dc, -mc, c["gap"], c["half"], tip_u=-mc)
    else:
        band(dc, c["gap"], c["len"], c["half"])

    rot = img_c.rotate(-theta_deg, resample=Image.BILINEAR, center=(off, off))
    return img_p, rot


def measure_open(img_p, img_c, r_eye, scale, win):
    """Background inside the joint's own disc: mm^2, the widest span (mm), px at 250/m."""
    w = img_p.width
    off = win * scale
    mask = Image.new("L", (w, w), 0)
    mask.paste(img_p, (0, 0), img_p)
    mask.paste(img_c, (0, 0), img_c)
    px = mask.load()
    steps = int(r_eye * scale)
    x0, x1 = int(off - steps), int(off + steps)
    open_px = 0
    widest = 0
    for y in range(x0, x1 + 1):
        run = best = 0
        for x in range(x0, x1 + 1):
            dx, dy = x - off, y - off
            if dx * dx + dy * dy > steps * steps:
                run = 0
                continue
            if px[x, y] < 128:
                open_px += 1
                run += 1
                best = max(best, run)
            else:
                run = 0
        widest = max(widest, best)
    area_mm2 = open_px / (scale * scale) * 1e6
    widest_mm = widest / scale * 1000.0
    px_game = open_px / (scale * scale) * GAME_PXM * GAME_PXM
    return area_mm2, widest_mm, px_game


def plan_for(name, p_half, p_gap, c_half, c_gap, pe, ce):
    """The margin plan for one joint: m = the joint's disc radius + 2 mm of safety.

    `R_eye = min(the two inks' half-widths at the joint end)` is the joint's own scale
    (the crack's mouth sits at the narrower neighbour's edge). The shoulder is the one
    joint whose parent ink cannot reach its disc laterally, so its distal plug is made
    longer on purpose (it stays inside the 0.12 m of chest the whole way).
    """
    r_eye = min(p_half, c_half)
    m = round(r_eye + 0.002, 4)
    if name == "shoulder":
        m = 0.05
    return r_eye, m


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", default=None)
    ap.add_argument("--plan", default=None, help="write the margin plan here")
    ap.add_argument("--scale", type=float, default=SCALE)
    args = ap.parse_args()

    boh = boxes()
    mea = {}
    for part in PART_FIELD:
        m = measure(part)
        if m is None:
            print("MISSING canvas " + part)
            continue
        mea[part] = m

    print("canvases: painted reach measured from the box' own edges (metres)")
    print("%-7s %5s %5s %9s %9s %9s %9s" % (
        "part", "boxW", "boxH", "gapTop", "gapBot", "halfTop", "halfBot"))
    for part, field in PART_FIELD.items():
        m = mea.get(part)
        if not m:
            continue
        b = boh[field]
        print("%-7s %5.3f %5.3f %9.5f %9.5f %9.5f %9.5f" % (
            part, b[0], b[1], m["gap_top"], m["gap_bottom"],
            m["ink_half_top"], m["ink_half_bottom"]))

    results = []
    plan = {"joints": [], "parts": {}}

    def add_margin(part, end, m):
        e = plan["parts"].setdefault(part, {})
        if end in ("top", "bottom", "back", "front"):
            e[end] = max(e.get(end, 0.0), round(m, 4))

    def add_fill(part, end):
        e = plan["parts"].setdefault(part, {})
        if end in ("top", "bottom", "back", "front"):
            e["fill_" + end] = True

    print("")
    print("open background inside the joint's own disc, per variant and angle")
    print("%-9s %-9s %7s %6s | %s" % ("joint", "angles", "R_eye", "m", "none / parent / child / both"))
    for name, pp, pe, cp, ce, span, hv in JOINTS:
        pm, cm = mea.get(pp), mea.get(cp)
        if pm is None or cm is None:
            continue
        if pe == "side":
            p_half, p_gap, p_len = min(pm["w"], pm["h"]) * 0.5, 0.0, max(pm["w"], pm["h"])
        elif pe == "bottom":
            p_half, p_gap, p_len = pm["ink_half_bottom"], pm["gap_bottom"], pm["h"]
        else:
            p_half, p_gap, p_len = pm["ink_half_top"], pm["gap_top"], pm["h"]
        if ce == "side":
            c_half, c_gap, c_len = cm["h"] * 0.5, 0.0, max(cm["w"], cm["h"])
        elif ce == "top":
            c_half, c_gap, c_len = cm["ink_half_top"], cm["gap_top"], cm["h"]
        else:
            c_half, c_gap, c_len = cm["ink_half_bottom"], cm["gap_bottom"], cm["h"]

        r_eye, m = plan_for(name, p_half, p_gap, c_half, c_gap, pe, ce)
        win = r_eye * 3.2 + 0.02
        angles = sorted(set(round(a, 1) for a in
                            (span[0], span[1], (span[0] + span[1]) * 0.5)))
        p = {"half": p_half, "gap": p_gap, "len": p_len}
        c = {"half": c_half, "gap": c_gap, "len": c_len}
        open_max = 0.0
        for a in angles:
            cells = []
            for variant in ("none", "parent", "child", "both"):
                ip, ic = variant_masks(p, c, a, variant, m, m, args.scale, win)
                area, widest, px = measure_open(ip, ic, r_eye, args.scale, win)
                cells.append("%7.2f/%5.1f" % (area, widest))
                results.append({"joint": name, "variant": variant, "angle": a,
                                "r_eye": round(r_eye, 4), "m": round(m, 4),
                                "open_mm2": round(area, 3), "widest_mm": round(widest, 2),
                                "px_at_250": round(px, 2)})
                if variant == "both":
                    open_max = max(open_max, area)
            print("%-9s %-9s %7.4f %6.4f | %s" % (name, str(a), r_eye, m, "  ".join(cells)))
        # The plan: BOTH neighbours' ink is filled out to its own box end (the fill), and
        # the OVERLAP - the rounded skirt - goes on the NARROWER side, because that is the
        # side whose own band covers the joint's disc and whose skirt then disappears
        # inside the wider neighbour's ink (the sheet's shin inside the boot). It is also
        # the side the rig draws on top in every pair of this rig.
        plan["joints"].append({"joint": name, "parent": pp, "parent_end": pe,
                               "child": cp, "child_end": ce, "r_eye": round(r_eye, 4),
                               "m": round(m, 4), "span": list(span),
                               "skirt_side": "parent" if p_half <= c_half else "child",
                               "open_mm2_max": round(open_max, 3)})
        add_fill(pp, pe)
        add_fill(cp, ce)
        if p_half <= c_half:
            add_margin(pp, pe, m)
        else:
            add_margin(cp, ce, m)

    # the MTP (the toe's hinge), measured like the rest: the foot's ink at the ball and
    # the toe's own crop are each 0.045 m half-height, so the toe's back skirt is 0.047 m
    # and the foot only fills.
    plan["joints"].append({"joint": "toe", "parent": "foot", "parent_end": "front",
                           "child": "toe", "child_end": "back", "r_eye": 0.045,
                           "m": 0.047, "span": [-35, 50], "skirt_side": "child",
                           "open_mm2_max": 0.0})
    add_fill("foot", "front")
    add_fill("toe", "back")
    add_margin("toe", "back", 0.047)
    add_fill("foot", "back")
    add_fill("foot", "top")
    add_margin("foot", "top", 0.0245)
    # The foot's own art is a 0.15 m foot inside a 0.26 m collider: the toes' tips ARE the
    # drawing's end, so the foot takes no fill at the ball or the heel - only the boot's
    # cuff (the top margin) and the ankle fill where the shin meets it.
    plan["parts"]["foot"].pop("fill_front", None)

    print("")
    print("THE PLAN - the skirt (an overlap past the box edge, metres) and the fills:")
    print("%-7s %10s %11s %8s %8s" % ("part", "top/+y", "bottom/-y", "back/-x", "front/+x"))
    for part in ("head", "neck", "torso", "pelvis", "upper", "lower", "hand",
                 "thigh", "shin", "foot", "toe"):
        e = plan["parts"].get(part, {})
        print("%-7s %10s %11s %8s %8s" % (
            part,
            "%s%s" % ("m=" if e.get("top") else "", "%.4f" % e["top"] if e.get("top") else
                      ("fill" if e.get("fill_top") else "-")),
            "%s%s" % ("m=" if e.get("bottom") else "", "%.4f" % e["bottom"] if e.get("bottom") else
                      ("fill" if e.get("fill_bottom") else "-")),
            "%s%s" % ("m=" if e.get("back") else "", "%.4f" % e["back"] if e.get("back") else
                      ("fill" if e.get("fill_back") else "-")),
            "%s%s" % ("m=" if e.get("front") else "", "%.4f" % e["front"] if e.get("front") else
                      ("fill" if e.get("fill_front") else "-"))))

    if args.plan:
        with open(args.plan, "w", encoding="utf-8") as fh:
            json.dump(plan, fh, indent=1)
        print("plan " + args.plan)
    if args.json:
        with open(args.json, "w", encoding="utf-8") as fh:
            json.dump({"results": results, "plan": plan,
                       "measured": mea}, fh, indent=1)
        print("json " + args.json)
    return 0


if __name__ == "__main__":
    sys.exit(main())
