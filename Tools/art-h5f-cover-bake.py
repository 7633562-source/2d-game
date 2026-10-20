#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f-R2 - THE JOINT COVER BAKE (the foreshortened joint's covering surface).

The R1 take left a wedge of background between the shortened thigh's end and the shin's
top (both take stills and Tools/arth5f-coverage.py measure it).  This tool bakes, for each
foreshortened joint (knee, elbow, hip), ONE picture-only surface that spans that wedge:

  * shape  = the REST limb (parent + child canvases at v=0) inside the crop - every pixel
             the limb owned before the shortening, so no background can show at v<=0.4;
             the surface is drawn UNDER both sprites, so each keeps its own silhouette.
  * tone   = the rest composition (the child drawn over the parent, the dress's own order)
             with the region ABOVE the child's top edge re-toned: per column the child's
             own tones are smeared upward (the surface continuing into depth) with a
             darkening falloff toward the joint (the occlusion shadow under the shortened
             parent's end).  The sprites' internal cut edges are removed with the R2
             lever's column inpaint so no old edge line survives inside the cover.
  * blend  = parent-driven: the cover must NOT rotate with the child - the region the
             child vacates at a bent joint (the crease) is exactly what has to stay
             covered, so w=0 for every vertex (sidecar blend=parent).

Writes Assets/Resources/Art/Human/cover_<joint>.png (+ .meta) and cover_<joint>.txt.

Usage:
  python Tools/art-h5f-cover-bake.py --joint knee --write
  python Tools/art-h5f-cover-bake.py --joint knee --lab 0.4,0    # compose+measure a pose
  python Tools/art-h5f-cover-bake.py --joint knee --lab 0.4,65 --save r2-lab-knee-t65.png
"""
import argparse
import importlib.util
import os

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(PROJECT, "Trials", "Runs", "arth5f-foreshorten")
ART = os.path.join(PROJECT, "Assets", "Resources", "Art", "Human")

# joint: parent = the upper segment (its box holds the hinge), child = the segment below.
# short  = which of the two the owner's -foreshorten squashes toward the hinge:
#          "parent" for the knee/elbow (the thigh/upper arm squash, the shin/lower arm
#          grow by `missing`), "child" for the hip/shoulder (the thigh/upper arm squash
#          about their own pivots and nothing grows - the gap opens against the pelvis/
#          torso picture above them).
# crop = (x0, y0, w, h) in joint metres (origin = the hinge, y up).
# qp/qc = the hinge in the parent's / the child's own box frame (the rig's own halves).
JOINTS = {
    "knee": dict(parent="thigh", child="shin", short="parent",
                 qp=(0.0, -0.215), qc=(0.0, 0.215),
                 crop=(-0.090, -0.085, 0.180, 0.150), falloff=0.22),
    "elbow": dict(parent="upper", child="lower", short="parent",
                  qp=(0.0, -0.165), qc=(0.0, 0.170),
                  crop=(-0.064, -0.062, 0.128, 0.118), falloff=0.22),
    "hip": dict(parent="pelvis", child="thigh", short="child",
                qp=(0.014760, -0.080), qc=(0.0, 0.215),
                side_x=(0.014760, -0.014760),   # +/- hipHalfSpacing*cos(viewAngle)
                crop=(-0.108, -0.080, 0.216, 0.150), falloff=0.22),
    "shoulder": dict(parent="torso", child="upper", short="child",
                     qp=(-0.032125, 0.080), qc=(0.0, 0.165),
                     side_x=(-0.032125, 0.032125),  # -direction*shoulderHalfSpacing*cos(viewAngle)
                     crop=(-0.085, -0.075, 0.170, 0.145), falloff=0.22),
}

# the joint's own angle range on the accepted walk (Tools/ah5fr2-angles-probe.py,
# row of record c65post_walk.csv): knee 0..65, elbow -52..-18, hip -32..+15,
# shoulder -12..+27 (the arm's own swing).
RANGES = {"knee": (0.0, 66.0), "elbow": (-54.0, -15.0), "hip": (-33.0, 18.0),
          "shoulder": (-14.0, 28.0)}


def load_mod(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(PROJECT, "Tools", rel))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


F = load_mod("h1", "art-h1-pose-frames.py")     # the rig + the canvases (ART-H1's reader)
COV = load_mod("cov", "arth5f-coverage.py")     # ink_extent

# the round's own value: the flag's maximum, the pose every number is quoted at.
COV_V = 0.4


def game_numbers(canvas, box_h, v=COV_V):
    """The game's own arithmetic: HumanArt.ForeshortenVisuals."""
    k = 1.0 - v
    ink_bot = canvas["pivot"][1] * canvas["world"][1]
    missing = max(0.0, box_h * 0.5 - ink_bot * k) + 0.005
    return k, missing


COV_V = 0.4


class Frame(object):
    """One joint frame: the crop raster at `scale` px/m, origin = the hinge, y up."""

    def __init__(self, spec, canvases, ns, scale=3000.0):
        self.spec = spec
        self.ns = ns
        self.scale = scale
        self.x0, self.y0, self.w, self.h = spec["crop"]
        self.nx = int(round(self.w * scale))
        self.ny = int(round(self.h * scale))
        self.cans = canvases
        self.box_h = {"thigh": ns["thighSize"].y, "upper": ns["upperArmSize"].y,
                      "pelvis": ns["pelvisSize"].y, "torso": ns["torsoSize"].y}[spec["parent"]]
        self.box_hc = {"shin": ns["shinSize"].y, "lower": ns["lowerArmSize"].y,
                       "thigh": ns["thighSize"].y, "upper": ns["upperArmSize"].y}[spec["child"]]

    # crop pixel -> joint metres
    def xm(self, px):
        return self.x0 + (px + 0.5) / self.scale

    def growth(self, key, missing):
        """ForeshortenVisuals' neighbour grow: nk over the neighbour's own canvas, its
        far edge held (nLift = (1-nk)*nBot, nBot = the paint's bottom edge, box metres)."""
        top, bot = COV.ink_extent(self.cans[key])
        span = top - bot
        nk = (span + missing) / span
        return nk, (1.0 - nk) * bot

    def ym(self, py):
        return self.y0 + self.h - (py + 0.5) / self.scale

    def place(self, key, anchor, theta, kk, ss):
        """The canvas into the crop grid, alpha-composited: (RGBA float array)."""
        c = self.cans[key]
        W, H = c["px"]
        ppu = c["ppu"]
        th = np.radians(theta)
        ct, st = np.cos(th), np.sin(th)
        pxv = c["pivot"][0] * W
        pyv = (1.0 - c["pivot"][1]) * H
        # output px -> joint metres -> the canvas's local (u, v) -> source px
        s = self.scale
        ox = np.arange(self.nx, dtype=np.float64)
        oy = np.arange(self.ny, dtype=np.float64)
        X = self.x0 + (ox + 0.5) / s
        Y = self.y0 + self.h - (oy + 0.5) / s
        Xg, Yg = np.meshgrid(X, Y)
        # world = R(theta)*((u, kk*v+ss) - q): the segment's centre = -R*q, the sprite
        # scales about its pivot (the centre) and rides the segment's transform.
        u = ct * Xg + st * Yg + anchor[0]
        vv = -st * Xg + ct * Yg + anchor[1]
        v = (vv - ss) / kk
        sx = pxv + u * ppu
        sy = pyv - v * ppu
        # bilinear sample of the source; outside the picture = nothing drawn at all
        inside = (sx > 0.0) & (sx < W - 1.0) & (sy > 0.0) & (sy < H - 1.0)
        sx = np.clip(sx, 0, W - 1.001)
        sy = np.clip(sy, 0, H - 1.001)
        x0i = np.floor(sx).astype(np.int32); x1i = x0i + 1
        y0i = np.floor(sy).astype(np.int32); y1i = y0i + 1
        fx = (sx - x0i)[..., None]; fy = (sy - y0i)[..., None]
        a = np.asarray(c["im"]).astype(np.float32) / 255.0
        p00 = a[y0i, x0i]; p01 = a[y0i, x1i]; p10 = a[y1i, x0i]; p11 = a[y1i, x1i]
        out = (p00 * (1 - fx) * (1 - fy) + p01 * fx * (1 - fy)
               + p10 * (1 - fx) * fy + p11 * fx * fy)
        out[..., 3] *= inside
        return out


def over(dst, src):
    """src over dst (both RGBA float)."""
    sa = src[..., 3:4]
    da = dst[..., 3:4]
    out_a = sa + da * (1 - sa)
    with np.errstate(invalid="ignore", divide="ignore"):
        out_rgb = np.where(out_a > 0, (src[..., :3] * sa + dst[..., :3] * da * (1 - sa)) / np.maximum(out_a, 1e-9), 0.0)
    return np.concatenate([out_rgb, out_a], axis=2)


def child_top_edge(alpha):
    """Per column: the topmost painted row of `alpha` (bool grid), or None."""
    ny, nx = alpha.shape
    has = alpha.any(axis=0)
    top = np.full(nx, -1, dtype=np.int32)
    cols = np.nonzero(has)[0]
    for c in cols:
        rows = np.nonzero(alpha[:, c])[0]
        top[c] = rows.min()
    return top


def pose(fr, spec, theta=0.0, v=COV_V):
    """The two canvases at v, theta - the placements HumanArt.ForeshortenVisuals
    produces.

    R4: the `short` segment's picture pulls back along its own bone (scaled about its
    own pivot), the hinge it drives sits at the SHORTENED box' end - so the shortened
    segment's own frame vector q scales by k - and THE NEIGHBOUR IS NOT TOUCHED
    (nk = 1, no lift: the joint is the parent's own cuff's job).  The engine's part
    dump (Assets/Editor/ArtCuffCheck.cs) is the reference for both: at v=0.4 the thigh's
    cAnchor reads (0,-0.129) = qp*k and the shin's drawn height stays its own."""
    k = 1.0 - v
    if spec.get("short", "parent") == "parent":
        par_kk = k
        nk, nshift = 1.0, 0.0
        qp = (spec["qp"][0], spec["qp"][1] * k)     # the hinge at the shortened box end
        qc = spec["qc"]
    else:
        par_kk = 1.0
        nk, nshift = k, 0.0
        qp = spec["qp"]
        qc = (spec["qc"][0], spec["qc"][1] * k)     # the shortened child's own anchor
    return dict(k=k, missing=0.0, nk=nk, nshift=nshift,
                par_now=fr.place(spec["parent"], qp, 0.0, par_kk, 0.0),
                ch_now=fr.place(spec["child"], qc, theta, nk, nshift))


def bake(spec, canvases, ns, scale=3000.0, falloff=None, verbose=True):
    fr = Frame(spec, canvases, ns, scale)
    falloff = spec["falloff"] if falloff is None else falloff
    short = spec.get("short", "parent")
    p = pose(fr, spec, 0.0)
    k, missing, nk, nshift = p["k"], p["missing"], p["nk"], p["nshift"]
    par = fr.place(spec["parent"], spec["qp"], 0.0, 1.0, 0.0)
    par_now, ch_now = p["par_now"], p["ch_now"]
    ch = fr.place(spec["child"], spec["qc"], 0.0, 1.0, 0.0)
    rest = over(par, ch)                         # the dress's order: the child over
    shape = rest[..., 3] > 0.05                  # the rest limb = the cover's shape (the
    # eye's own threshold: what the control paints at all, soft flanks included)
    ch_a = ch[..., 3] > 0.05
    ch_now_a = ch_now[..., 3] > 0.05
    par_now_a = par_now[..., 3] > 0.05
    ny, nx = shape.shape
    top = child_top_edge(ch_a)                   # the child's own edge (the tone source)
    bot04 = np.full(nx, -1, dtype=np.int32)      # the upper segment's edge, this pose
    for c in np.nonzero(par_now_a.any(axis=0))[0]:
        bot04[c] = np.nonzero(par_now_a[:, c])[0].max()
    bot_now = np.full(nx, -1, dtype=np.int32)    # the lower segment's edge, this pose
    for c in np.nonzero(ch_now_a.any(axis=0))[0]:
        bot_now[c] = np.nonzero(ch_now_a[:, c])[0].min()

    # per column: the child's tone just under its top edge (the surface to continue)
    src = np.zeros((nx, 3), np.float32)
    have = np.zeros(nx, bool)
    for c in range(nx):
        t = top[c]
        if t < 0:
            continue
        lo = min(ny - 1, t + 2)
        hi = min(ny - 1, t + 10)
        src[c] = ch[lo:hi + 1, c, :3].mean(axis=0)
        have[c] = True
    if have.any():                                # columns without a source borrow the
        idx = np.nonzero(have)[0]                 # nearest painted column's tone
        for c in range(nx):
            if not have[c]:
                src[c] = src[idx[np.argmin(np.abs(idx - c))]]

    tone = ch[..., :3].copy()                     # the child's own paint stays
    filled = 0
    for c in range(nx):
        rows = np.nonzero(shape[:, c])[0]
        if not len(rows):
            continue
        t = bot_now[c] if bot_now[c] >= 0 else top[c]
        y_cap = fr.ym(t) if t >= 0 else fr.ym(rows.max())
        y_t04 = fr.ym(bot04[c]) if bot04[c] >= 0 else fr.ym(rows.min())
        span = max(1e-6, y_t04 - y_cap)
        for r in rows:
            if ch_now_a[r, c]:
                continue                          # the child's paint: keep it
            y = fr.ym(r)
            s = min(1.0, max(0.0, (y - y_cap) / span))
            tone[r, c, :] = src[c] * (1.0 - falloff * s)
            filled += 1
    if verbose:
        print("%s: crop %dx%d px, shape %.1f%% , re-toned %d px, missing %.4f m"
              % (spec.get("name", "joint"), nx, ny, 100.0 * shape.mean(), filled, missing))
    # the cover is a covering surface: fill the rest limb's interior notches (the art's
    # own soft creases read as holes at 0.35 alpha) so nothing shows through them.
    inv = ~shape
    outside = np.zeros_like(inv)
    outside[0, :] |= inv[0, :]; outside[-1, :] |= inv[-1, :]
    outside[:, 0] |= inv[:, 0]; outside[:, -1] |= inv[:, -1]
    for _ in range(2000):
        grown = outside.copy()
        grown[1:, :] |= outside[:-1, :]; grown[:-1, :] |= outside[1:, :]
        grown[:, 1:] |= outside[:, :-1]; grown[:, :-1] |= outside[:, 1:]
        grown &= inv
        if (grown == outside).all():
            break
        outside = grown
    shape = shape | (inv & ~outside)
    # a 1 px dilation keeps hairline seams shut along the shape's boundary (the drawn
    # sprites' soft edges sit right on it) - it can only ever hide more, never less.
    from PIL import Image as _Im, ImageFilter as _If
    sh = _Im.fromarray((shape * 255).astype(np.uint8), "L").filter(_If.MaxFilter(3))
    shape = np.asarray(sh) > 0
    rgba = np.concatenate([np.clip(tone, 0, 1), shape[..., None].astype(np.float32)], axis=2)
    return fr, rgba, shape


def resolve_spec(name, side):
    spec = dict(JOINTS[name])
    spec["name"] = name
    if "side_x" in spec:
        i = 0 if side != "far" else 1
        spec["qp"] = (spec["side_x"][i], spec["qp"][1])
        spec["name"] = "%s_%s" % (name, "near" if i == 0 else "far")
    return spec


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--joint", default="knee")
    ap.add_argument("--side", default="near", choices=("near", "far"),
                    help="which side's hinge the sided joints bake for")
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--lab", default="", help="v,theta - compose the game view at that pose")
    ap.add_argument("--save", default="")
    ap.add_argument("--sheet", default="", help="A/B/C sheet: control | no cover | cover")
    ap.add_argument("--scale", type=float, default=3000.0)
    ap.add_argument("--falloff", type=float, default=None)
    ap.add_argument("--all", action="store_true", help="every joint in the walk's own range")
    args = ap.parse_args()

    ns, joints = F.read_rig()
    canvases = {k: F.load_canvas(v) for k, v in F.CANVAS.items()}
    canvases = {k: v for k, v in canvases.items() if v is not None}

    names = list(JOINTS) if args.all else [args.joint]
    for name in names:
        sides = ("near", "far") if "side_x" in JOINTS[name] else (args.side,)
        for side in sides:
            spec = resolve_spec(name, side)
            fr, rgba, shape = bake(spec, canvases, ns, args.scale, args.falloff)
            if args.write:
                img = Image.fromarray((rgba * 255.0).astype(np.uint8), "RGBA")
                png = os.path.join(ART, "cover_%s.png" % spec["name"])
                img.save(png)
                txt = os.path.join(ART, "cover_%s.txt" % spec["name"])
                with open(txt, "w", encoding="ascii") as fh:
                    fh.write("# cover_%s - baked by Tools/art-h5f-cover-bake.py (ART-H5f R2 tool, R3 bake).\n"
                             "# The joint frame J: origin = the hinge, x right, y up (the\n"
                             "# root's rest axes); the crop is the joint's own metres.\n"
                             "# blend=parent: the cover rides the parent segment's transform,\n"
                             "# world = Mparent*(p + Qp) - it must NOT rotate with the child:\n"
                             "# the crease the child vacates has to stay painted.\n"
                             "# short=%s: the owner's squash moves the %s;\n"
                             "# the shape is the rest limb (parent+child at v=0) with its\n"
                             "# interior notches filled - every wedge at v<=0.4 is inside it.\n"
                             "# tone: the child's own paint, its tone smeared above the child's\n"
                             "# edge, darkening toward the joint (falloff=%.2f).\n"
                             % (spec["name"], spec.get("short", "parent"),
                                "parent" if spec.get("short", "parent") == "parent"
                                else "child", spec["falloff"]))
                    fh.write("scale=%.4f\n" % args.scale)
                    fh.write("x0=%.6f\ny0=%.6f\nw=%.6f\nh=%.6f\n" % spec["crop"])
                    fh.write("upx=0.000000\nupy=1.000000\n")
                    fh.write("qpx=%.6f\nqpy=%.6f\nqcx=%.6f\nqcy=%.6f\n"
                             % (spec["qp"][0], spec["qp"][1], spec["qc"][0], spec["qc"][1]))
                    fh.write("blend=parent\n")
                    fh.write("side=%s\n" % side)
                # the metas ride with the bake (fresh guids, the shipped canvases' own
                # import settings): a picture Unity will import as a Sprite and a sidecar
                # it will import as a TextAsset - the two things the runtime loads.
                import uuid
                meta = open(os.path.join(ART, "shin.png.meta"), encoding="utf-8").read()
                new_guid = uuid.uuid4().hex
                meta = meta.replace("guid: " + meta.split("guid: ")[1].split("\n")[0],
                                    "guid: " + new_guid)
                with open(png + ".meta", "w", encoding="utf-8", newline="\n") as fh:
                    fh.write(meta)
                tmeta = open(os.path.join(ART, "knee_patch.txt.meta"), encoding="utf-8").read()
                tguid = uuid.uuid4().hex
                tmeta = tmeta.replace("guid: " + tmeta.split("guid: ")[1].split("\n")[0],
                                      "guid: " + tguid)
                with open(txt + ".meta", "w", encoding="utf-8", newline="\n") as fh:
                    fh.write(tmeta)
                print("wrote", png, "+", txt, "(guids %s / %s)" % (new_guid, tguid))

    if args.lab:
        v, theta = [float(x) for x in args.lab.split(",")]
        name = args.joint
        spec = resolve_spec(name, args.side)
        fr, rgba, shape = bake(spec, canvases, ns, args.scale, args.falloff, verbose=False)
        cover = np.zeros_like(rgba)
        cover[..., :3] = rgba[..., :3]
        cover[..., 3] = rgba[..., 3]
        p = pose(fr, spec, theta, v)
        par, ch = p["par_now"], p["ch_now"]
        game = over(over(cover, par), ch)        # the cover drawn UNDER both sprites
        cov = over(par, ch)                      # the control: the two sprites alone
        hole_game = int(((game[..., 3] <= 0.06) & (shape)).sum())
        hole_cov = int(((cov[..., 3] <= 0.06) & (shape)).sum())
        # the round's own instrument: the joint's disc, R_eye, without / with the cover
        r_eye = min(F.ink_half(canvases[spec["parent"]], spec["qp"]),
                    F.ink_half(canvases[spec["child"]], spec["qc"]))
        yy, xx = np.mgrid[0:fr.ny, 0:fr.nx]
        Xm = fr.x0 + (xx + 0.5) / fr.scale
        Ym = fr.y0 + fr.h - (yy + 0.5) / fr.scale
        disc = (Xm * Xm + Ym * Ym) <= r_eye * r_eye
        d_nc = int((disc & (cov[..., 3] <= 0.06)).sum())
        d_wc = int((disc & (game[..., 3] <= 0.06)).sum())
        print("%s v=%.2f theta=%+.1f: uncovered inside the shape: with-cover %d / no-cover %d "
              "(of %d); disc R_eye %.4f m: with-cover %d / no-cover %d"
              % (name, v, theta, hole_game, hole_cov, int(shape.sum()), r_eye, d_wc, d_nc))
        print("   px/m %.0f -> disc px %d" % (fr.scale, int(disc.sum())))
        res = disc & (game[..., 3] <= 0.06)
        if res.any():
            rr, cc = np.nonzero(res)
            print("   residual %d px, bbox x %+.1f..%+.1f mm  y %+.1f..%+.1f mm"
                  % (res.sum(), fr.xm(cc.min()) * 1000, fr.xm(cc.max()) * 1000,
                     fr.ym(rr.max()) * 1000, fr.ym(rr.min()) * 1000))
        if args.save or args.sheet:
            from PIL import ImageDraw as _Dr
            pan = []
            panels = []
            if args.sheet:
                ctrl = over(fr.place(spec["parent"], spec["qp"], 0.0, 1.0, 0.0),
                            fr.place(spec["child"], spec["qc"], 0.0, 1.0, 0.0))
                panels = ((ctrl, "A  v=0  control"), (cov, "B  v=%.1f  no cover" % v),
                          (game, "C  v=%.1f  cover" % v))
            else:
                panels = ((game, ""),)
            for arr, lab in panels:
                im = Image.fromarray((arr * 255.0).astype(np.uint8), "RGBA")
                bg = Image.new("RGB", im.size, (186, 205, 222))   # the game's sky tone
                bg.paste(im, (0, 0), im)
                if lab:
                    _Dr.Draw(bg).text((5, 3), lab, fill=(20, 20, 40))
                pan.append(bg)
            if args.sheet:
                W = sum(p.width for p in pan) + 10 * (len(pan) - 1)
                sheet = Image.new("RGB", (W, pan[0].height), (186, 205, 222))
                x = 0
                for p in pan:
                    sheet.paste(p, (x, 0)); x += p.width + 10
                sp = os.path.join(OUT, args.sheet)
                sheet.save(sp); print("saved sheet", sp)
            if args.save:
                p = os.path.join(OUT, args.save)
                pan[0].save(p); print("saved", p)


if __name__ == "__main__":
    main()
