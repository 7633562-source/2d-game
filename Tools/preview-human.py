# Preview the human with the same recipe as HumanSegment + Human.
# Not physics: pixels only. Run: python Tools/preview-human.py
from __future__ import annotations

import math
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets" / "Resources" / "Art"
OUT = ROOT / "Tools" / "preview-human.png"

PPM = 360.0
BODY_TILE = 0.38
SOFT = 128
MIX = 0.78
MARKER_D = 0.03

SKIN = np.array([0.92, 0.74, 0.60], dtype=np.float32)
SHIRT = np.array([0.24, 0.46, 0.78], dtype=np.float32)
PANTS = np.array([0.20, 0.30, 0.50], dtype=np.float32)
SOCKS = np.array([0.84, 0.84, 0.88], dtype=np.float32)
SHOES = np.array([0.22, 0.14, 0.12], dtype=np.float32)
NECK = np.array([0.86, 0.68, 0.55], dtype=np.float32)
SKY = np.array([0.07, 0.09, 0.16, 1.0], dtype=np.float32)


def dim_far(c: np.ndarray) -> np.ndarray:
    return c * 0.52


def visual_size(sx: float, sy: float) -> tuple[float, float]:
    if sx >= sy:
        return sx * 1.18, sy * 1.16
    return sx * 1.18, sy * 1.26


def capsule_distance(px: float, py: float, rx: float, ry: float) -> float:
    radius = min(rx, ry) * 0.42
    qx = abs(px) - (rx - radius)
    qy = abs(py) - (ry - radius)
    ox = max(qx, 0.0)
    oy = max(qy, 0.0)
    return math.sqrt(ox * ox + oy * oy) + min(max(qx, qy), 0.0) - radius


def ellipse_distance(px: float, py: float, rx: float, ry: float) -> float:
    nx = px / max(0.001, rx)
    ny = py / max(0.001, ry)
    length = math.sqrt(nx * nx + ny * ny)
    if length < 0.0001:
        return -min(rx, ry)
    edge = math.sqrt((px * px + py * py) / (length * length))
    return (length - 1.0) * edge


def shade_edge(dist: float) -> tuple[float, float]:
    alpha = 1.0 if dist < 0.0 else max(0.0, min(1.0, 1.0 - dist))
    if alpha <= 0.001:
        return 0.0, 0.0
    outline = max(0.0, min(1.0, (dist + 1.4) / 2.2))
    shade = 1.0 + (0.82 - 1.0) * outline
    return shade, alpha


def load_soft(key: str) -> np.ndarray | None:
    path = ART / f"{key}.png"
    if not path.exists():
        return None
    im = Image.open(path).convert("RGB")
    im = im.transpose(Image.FLIP_TOP_BOTTOM)
    im = im.resize((SOFT, SOFT), Image.Resampling.BILINEAR)
    return np.asarray(im, dtype=np.float32) / 255.0


SOFT_MAPS = {
    "skin": load_soft("skin"),
    "fabric": load_soft("fabric"),
    "denim": load_soft("denim"),
    "dirt": load_soft("dirt"),
    "grass": load_soft("grass"),
}


def wrap01(t: float) -> float:
    return t - math.floor(t)


def bilerp(tex: np.ndarray, u: float, v: float) -> np.ndarray:
    h, w = tex.shape[0], tex.shape[1]
    x = wrap01(u) * w - 0.5
    y = wrap01(v) * h - 0.5
    x0 = int(math.floor(x))
    y0 = int(math.floor(y))
    fx = x - x0
    fy = y - y0
    x1 = x0 + 1
    y1 = y0 + 1

    def at(ix: int, iy: int) -> np.ndarray:
        return tex[iy % h, ix % w]

    return (
        at(x0, y0) * (1 - fx) * (1 - fy)
        + at(x1, y0) * fx * (1 - fy)
        + at(x0, y1) * (1 - fx) * fy
        + at(x1, y1) * fx * fy
    )


def sample_albedo(key: str | None, mx: float, my: float) -> np.ndarray:
    if not key or key not in SOFT_MAPS or SOFT_MAPS[key] is None:
        return np.array([1.0, 1.0, 1.0], dtype=np.float32)
    return bilerp(SOFT_MAPS[key], mx / BODY_TILE, my / BODY_TILE)


def bake_sprite(size: tuple[float, float], shape: str, albedo_key: str | None) -> tuple[np.ndarray, float, float]:
    sx, sy = size
    w = max(1, int(round(sx * 256.0)))
    h = max(1, int(round(sy * 256.0)))
    scale = 1.0
    if w > 512 or h > 512:
        scale = 512.0 / max(w, h)
    min_side = min(w, h) * scale
    if min_side < 64.0:
        scale *= 64.0 / max(1.0, min_side)
    w = max(8, int(round(w * scale)))
    h = max(8, int(round(h * scale)))
    pixels = np.zeros((h, w, 4), dtype=np.float32)
    cx = (w - 1) * 0.5
    cy = (h - 1) * 0.5
    rx = w * 0.5 - 0.5
    ry = h * 0.5 - 0.5
    for y in range(h):
        for x in range(w):
            px = x - cx
            py = y - cy
            dist = ellipse_distance(px, py, rx, ry) if shape == "ellipse" else capsule_distance(px, py, rx, ry)
            shade, alpha = shade_edge(dist)
            if alpha <= 0.001:
                continue
            mx = (x + 0.5) / w * sx
            my = (y + 0.5) / h * sy
            albedo = sample_albedo(albedo_key, mx, my)
            mix = 1.0 + (albedo - 1.0) * MIX
            pixels[y, x, 0] = shade * mix[0]
            pixels[y, x, 1] = shade * mix[1]
            pixels[y, x, 2] = shade * mix[2]
            pixels[y, x, 3] = alpha
    world_w = w / (w / sx)
    world_h = h / (w / sx)
    return pixels, world_w, world_h


def alpha_over(dst: np.ndarray, src: np.ndarray, x0: int, y0: int) -> None:
    sh, sw = src.shape[0], src.shape[1]
    dh, dw = dst.shape[0], dst.shape[1]
    x1 = max(0, x0)
    y1 = max(0, y0)
    x2 = min(dw, x0 + sw)
    y2 = min(dh, y0 + sh)
    if x1 >= x2 or y1 >= y2:
        return
    sx1 = x1 - x0
    sy1 = y1 - y0
    patch = src[sy1 : sy1 + (y2 - y1), sx1 : sx1 + (x2 - x1)]
    view = dst[y1:y2, x1:x2]
    a = patch[..., 3:4]
    view[..., :3] = patch[..., :3] * a + view[..., :3] * (1.0 - a)
    view[..., 3:4] = a + view[..., 3:4] * (1.0 - a)


class Canvas:
    def __init__(self, xmin: float, ymin: float, xmax: float, ymax: float, ppm: float):
        self.xmin = xmin
        self.ymin = ymin
        self.ppm = ppm
        self.w = int(math.ceil((xmax - xmin) * ppm))
        self.h = int(math.ceil((ymax - ymin) * ppm))
        self.px = np.zeros((self.h, self.w, 4), dtype=np.float32)
        self.px[..., :] = SKY

    def world_to_px(self, x: float, y: float) -> tuple[float, float]:
        return (x - self.xmin) * self.ppm, (y - self.ymin) * self.ppm

    def blit_sprite(self, sprite: np.ndarray, world_w: float, world_h: float, cx: float, cy: float, color: np.ndarray):
        tinted = sprite.copy()
        tinted[..., 0] *= color[0]
        tinted[..., 1] *= color[1]
        tinted[..., 2] *= color[2]
        left = cx - world_w * 0.5
        bottom = cy - world_h * 0.5
        x0, y0 = self.world_to_px(left, bottom)
        alpha_over(self.px, tinted, int(round(x0)), int(round(y0)))

    def blit_marker(self, cx: float, cy: float):
        r = MARKER_D * 0.5
        n = 32
        tex = np.zeros((n, n, 4), dtype=np.float32)
        center = (n - 1) / 2.0
        radius = n / 2.0 - 1.0
        for y in range(n):
            for x in range(n):
                d = math.sqrt((x - center) ** 2 + (y - center) ** 2) - radius
                a = max(0.0, min(1.0, 0.7 - d))
                if a > 0:
                    tex[y, x] = (0.08, 0.07, 0.06, a)
        self.blit_sprite(tex, MARKER_D, MARKER_D, cx, cy, np.array([1.0, 1.0, 1.0], dtype=np.float32))

    def save(self, path: Path) -> None:
        img = np.clip(self.px[::-1], 0.0, 1.0)
        Image.fromarray((img * 255.0).astype(np.uint8), "RGBA").save(path)


def main() -> None:
    pelvis = (0.24, 0.16)
    torso = (0.24, 0.34)
    head = (0.20, 0.23)
    neck = (0.12, 0.09)
    upper = (0.10, 0.33)
    lower = (0.08, 0.32)
    hand = (0.06, 0.24)
    thigh = (0.16, 0.43)
    shin = (0.11, 0.43)
    foot = (0.26, 0.07)

    view = 80.0 * math.pi / 180.0
    lat = math.cos(view)
    hip_half = 0.085
    shoulder_half = 0.185
    vis = 0.045
    heel = 0.065
    shoulder_drop = 0.125

    trunk_h = pelvis[1] + torso[1]
    pelvis_cy = -trunk_h * 0.5 + pelvis[1] * 0.5
    torso_cy = -trunk_h * 0.5 + pelvis[1] + torso[1] * 0.5
    neck_cy = torso_cy + torso[1] * 0.5 + neck[1] * 0.5
    head_cy = torso_cy + torso[1] * 0.5 + neck[1] + head[1] * 0.5

    hip_root_y = pelvis_cy - pelvis[1] * 0.5
    shoulder_y = torso[1] * 0.5 - shoulder_drop
    shoulder_root_y = torso_cy + shoulder_y

    layers: list[tuple] = []

    def add(name: str, size, shape, albedo, color, px, py, ox, order, anchor=None):
        vx, vy = visual_size(*size)
        cx = px + ox
        cy = py
        layers.append((order, name, (vx, vy), shape, albedo, color, cx, cy, anchor, size))

    add("Pelvis", pelvis, "capsule", "denim", PANTS, 0.0, pelvis_cy, 0.0, 0, (0.0, pelvis[1] * 0.5))
    add("Torso", torso, "capsule", "fabric", SHIRT, 0.0, torso_cy, 0.0, 0, (0.0, -torso[1] * 0.5))
    add("Neck", neck, "capsule", "skin", NECK, 0.0, neck_cy, 0.0, 1, (0.0, -neck[1] * 0.5))
    add("Head", head, "ellipse", "skin", SKIN, 0.0, head_cy, 0.0, 2, (0.0, -head[1] * 0.5))

    for side, dim, thigh_o, shin_o, foot_o, arm_uo, arm_lo, arm_ho in (
        (-1, True, -6, -5, -4, -3, -2, -1),
        (1, False, 3, 4, 5, 6, 7, 8),
    ):
        d = dim_far if dim else (lambda c: c)
        hip_x = side * hip_half * lat
        sh_x = side * shoulder_half * lat
        shift = side * vis

        thigh_cy = hip_root_y - thigh[1] * 0.5
        shin_cy = hip_root_y - thigh[1] - shin[1] * 0.5
        foot_half = foot[0] * 0.5
        foot_cx = hip_x - heel + foot_half
        foot_cy = hip_root_y - thigh[1] - shin[1] - foot[1] * 0.5
        ankle_ax = hip_x - foot_cx

        add(f"{'L' if side < 0 else 'R'}Thigh", thigh, "capsule", "denim", d(PANTS), hip_x, thigh_cy, shift, thigh_o, (0.0, thigh[1] * 0.5))
        add(f"{'L' if side < 0 else 'R'}Shin", shin, "capsule", "fabric", d(SOCKS), hip_x, shin_cy, shift, shin_o, (0.0, shin[1] * 0.5))
        add(f"{'L' if side < 0 else 'R'}Foot", foot, "capsule", "denim", d(SHOES), foot_cx, foot_cy, shift, foot_o, (ankle_ax, foot[1] * 0.5))

        upper_cy = shoulder_root_y - upper[1] * 0.5
        lower_cy = shoulder_root_y - upper[1] - lower[1] * 0.5
        hand_cy = shoulder_root_y - upper[1] - lower[1] - hand[1] * 0.5
        add(f"{'L' if side < 0 else 'R'}Upper", upper, "capsule", "fabric", d(SHIRT), sh_x, upper_cy, shift, arm_uo, (0.0, upper[1] * 0.5))
        add(f"{'L' if side < 0 else 'R'}Lower", lower, "capsule", "skin", d(SKIN), sh_x, lower_cy, shift, arm_lo, (0.0, lower[1] * 0.5))
        add(f"{'L' if side < 0 else 'R'}Hand", hand, "capsule", "skin", d(SKIN), sh_x, hand_cy, shift, arm_ho, (0.0, hand[1] * 0.5))

    pads = []
    for _, _, size, _, _, _, cx, cy, _, _ in layers:
        pads.append((cx - size[0] * 0.5, cy - size[1] * 0.5, cx + size[0] * 0.5, cy + size[1] * 0.5))
    xmin = min(p[0] for p in pads) - 0.18
    ymin = min(p[1] for p in pads) - 0.12
    xmax = max(p[2] for p in pads) + 0.18
    ymax = max(p[3] for p in pads) + 0.12

    canvas = Canvas(xmin, ymin, xmax, ymax, PPM)

    ground_y = hip_root_y - thigh[1] - shin[1] - foot[1]
    dirt = SOFT_MAPS.get("dirt")
    grass = SOFT_MAPS.get("grass")
    if dirt is not None:
        gh = int(0.22 * PPM)
        gy0 = int(round((ground_y - 0.22 - ymin) * PPM))
        for y in range(max(0, gy0), min(canvas.h, gy0 + gh)):
            my = (y + 0.5) / PPM + ymin
            for x in range(canvas.w):
                mx = (x + 0.5) / PPM + xmin
                c = bilerp(dirt, mx / 2.5, my / 2.5)
                canvas.px[y, x, :3] = c
                canvas.px[y, x, 3] = 1.0
        if grass is not None:
            gh2 = int(0.045 * PPM)
            gy1 = int(round((ground_y - ymin) * PPM))
            for y in range(max(0, gy1), min(canvas.h, gy1 + gh2)):
                my = (y + 0.5) / PPM + ymin
                for x in range(canvas.w):
                    mx = (x + 0.5) / PPM + xmin
                    c = bilerp(grass, mx / 0.5, my / 0.5)
                    canvas.px[y, x, :3] = c
                    canvas.px[y, x, 3] = 1.0

    baked: dict[tuple, tuple] = {}
    layers.sort(key=lambda t: t[0])
    for order, name, vsize, shape, albedo, color, cx, cy, anchor, phys in layers:
        key = (vsize, shape, albedo)
        if key not in baked:
            baked[key] = bake_sprite(vsize, shape, albedo)
        sprite, ww, wh = baked[key]
        canvas.blit_sprite(sprite, ww, wh, cx, cy, color)
        if anchor is not None:
            canvas.blit_marker(cx + anchor[0], cy + anchor[1])

    canvas.save(OUT)
    print(f"wrote {OUT} {canvas.w}x{canvas.h}")


if __name__ == "__main__":
    main()
