# Picture only. Apple fruit, strawberry parts, hedgehog bones.
# Canvas aspect matches the collider box (painted-parts.mdc). Face +X.
# Run from the project root: python Tools/gen-yard-sprites.py

from __future__ import annotations

import math
import os
import uuid
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets" / "Resources" / "Art"


def hash01(x: int, y: int, salt: int = 0) -> float:
    n = (x * 374761393 + y * 668265263 + salt * 1274126177) & 0xFFFFFFFF
    n = (n ^ (n >> 13)) * 1274126177 & 0xFFFFFFFF
    return (n & 0xFFFFFF) / float(0xFFFFFF)


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def mix(c0, c1, t: float):
    t = max(0.0, min(1.0, t))
    return tuple(int(lerp(c0[i], c1[i], t)) for i in range(3))


def with_alpha(rgb, a: int):
    return (rgb[0], rgb[1], rgb[2], max(0, min(255, a)))


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def ellipse_mask(w: int, h: int, cx: float, cy: float, rx: float, ry: float, softness: float = 1.4):
    img = Image.new("L", (w, h), 0)
    px = img.load()
    for y in range(h):
        for x in range(w):
            nx = (x + 0.5 - cx) / max(0.001, rx)
            ny = (y + 0.5 - cy) / max(0.001, ry)
            d = math.sqrt(nx * nx + ny * ny) - 1.0
            if d <= 0.0:
                px[x, y] = 255
            elif d < softness / max(rx, ry):
                t = 1.0 - d * max(rx, ry) / softness
                px[x, y] = int(255 * t * t)
    return img


def paste_color(dst: Image.Image, mask: Image.Image, rgb, ox: int = 0, oy: int = 0):
    layer = Image.new("RGBA", dst.size, with_alpha(rgb, 255))
    layer.putalpha(mask)
    if ox or oy:
        shifted = Image.new("RGBA", dst.size, (0, 0, 0, 0))
        shifted.paste(layer, (ox, oy))
        layer = shifted
    dst.alpha_composite(layer)


def fur_fill(img: Image.Image, mask: Image.Image, dark, mid, light, belly=None, belly_y=0.28, salt=1):
    w, h = img.size
    px = img.load()
    m = mask.load()
    for y in range(h):
        vy = y / max(1, h - 1)
        for x in range(w):
            a = m[x, y]
            if a < 2:
                continue
            n = hash01(x, y, salt)
            n2 = hash01(x * 3, y * 2, salt + 9)
            base = mix(dark, mid, 0.35 + 0.5 * n)
            if n2 > 0.72:
                base = mix(base, light, 0.45)
            # Bottom of the PNG is ground / belly (Unity sprite +Y is up).
            if belly is not None and vy > (1.0 - belly_y):
                along = (vy - (1.0 - belly_y)) / max(1e-4, belly_y)
                base = mix(base, belly, 0.55 + 0.35 * along)
            # Top edge: darker quill hint.
            if vy < 0.28:
                base = mix(base, dark, (0.28 - vy) / 0.28 * 0.55)
            px[x, y] = with_alpha(base, a)


def quill_strokes(img: Image.Image, mask: Image.Image, color, count: int, salt: int):
    w, h = img.size
    draw = ImageDraw.Draw(img)
    m = mask.load()
    for i in range(count):
        t = (i + 0.5) / count
        x = int(lerp(w * 0.08, w * 0.92, t))
        y0 = int(h * (0.58 + 0.08 * hash01(i, 2, salt)))
        y1 = int(h * (0.04 + 0.10 * hash01(i, 3, salt)))
        lean = int((hash01(i, 4, salt) - 0.5) * w * 0.06)
        if m[min(w - 1, max(0, x)), min(h - 1, max(0, y0))] < 40:
            continue
        width = 1 if w < 80 else 2
        draw.line([(x, y0), (x + lean, y1)], fill=with_alpha(color, 210), width=width)


def apple_fruit(w=256, h=256) -> Image.Image:
    img = new_canvas(w, h)
    cx, cy = w * 0.50, h * 0.54
    rx, ry = w * 0.42, h * 0.40
    body = ellipse_mask(w, h, cx, cy, rx, ry, 2.2)
    # Cleft at the top.
    cleft = ellipse_mask(w, h, cx, cy - ry * 0.15, rx * 0.22, ry * 0.18, 1.2)
    px = body.load()
    cpx = cleft.load()
    for y in range(int(cy - ry)):
        for x in range(w):
            if cpx[x, y] > 180 and abs(x - cx) < rx * 0.12:
                px[x, y] = int(px[x, y] * 0.15)
    skin = (196, 32, 36)
    shadow = (128, 18, 22)
    highlight = (236, 86, 70)
    fur_fill(img, body, shadow, skin, highlight, salt=21)
    # Specular blob, +X facing light from upper right.
    spec = ellipse_mask(w, h, cx + rx * 0.28, cy - ry * 0.32, rx * 0.16, ry * 0.12, 2.5)
    paste_color(img, spec.point(lambda v: int(v * 0.45)), (255, 210, 190))
    draw = ImageDraw.Draw(img)
    stem_x = int(cx)
    draw.line(
        [(stem_x, int(cy - ry * 0.92)), (stem_x + int(w * 0.04), int(cy - ry * 1.18))],
        fill=(72, 44, 22, 255),
        width=max(3, w // 64),
    )
    return img


def apple_leaf(w=256, h=280) -> Image.Image:
    img = new_canvas(w, h)
    mask = Image.new("L", (w, h), 0)
    draw = ImageDraw.Draw(mask)
    # Pointed leaf, proximal (stem) at left, tip +X.
    draw.polygon(
        [
            (int(w * 0.06), int(h * 0.52)),
            (int(w * 0.38), int(h * 0.12)),
            (int(w * 0.92), int(h * 0.48)),
            (int(w * 0.40), int(h * 0.88)),
        ],
        fill=255,
    )
    mask = mask.filter(ImageFilter.GaussianBlur(1.2))
    fur_fill(img, mask, (36, 92, 28), (62, 140, 42), (120, 186, 70), salt=7)
    d = ImageDraw.Draw(img)
    d.line(
        [(int(w * 0.10), int(h * 0.52)), (int(w * 0.78), int(h * 0.48))],
        fill=(40, 72, 24, 180),
        width=2,
    )
    return img


def strawberry_berry(w=224, h=192) -> Image.Image:
    img = new_canvas(w, h)
    mask = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(mask)
    # Conical berry, calyx at top, tip down. Face +X slightly.
    d.polygon(
        [
            (int(w * 0.22), int(h * 0.22)),
            (int(w * 0.78), int(h * 0.20)),
            (int(w * 0.62), int(h * 0.88)),
            (int(w * 0.38), int(h * 0.88)),
        ],
        fill=255,
    )
    d.ellipse([int(w * 0.18), int(h * 0.10), int(w * 0.82), int(h * 0.48)], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(1.1))
    fur_fill(img, mask, (140, 18, 28), (210, 36, 48), (240, 92, 86), salt=33)
    seeds = ImageDraw.Draw(img)
    m = mask.load()
    for i in range(18):
        sx = int(lerp(w * 0.30, w * 0.70, hash01(i, 1, 4)))
        sy = int(lerp(h * 0.28, h * 0.78, hash01(i, 2, 5)))
        if m[sx, sy] < 180:
            continue
        seeds.ellipse([sx, sy, sx + 3, sy + 2], fill=(240, 196, 64, 230))
    calyx = Image.new("L", (w, h), 0)
    cd = ImageDraw.Draw(calyx)
    cx, cy = w * 0.50, h * 0.16
    for k in range(5):
        ang = -90 + k * 72
        rad = math.radians(ang)
        x2 = cx + math.cos(rad) * w * 0.22
        y2 = cy + math.sin(rad) * h * 0.16
        cd.polygon(
            [
                (int(cx), int(cy)),
                (int(x2 - 6), int(y2)),
                (int(x2 + 6), int(y2)),
            ],
            fill=255,
        )
    paste_color(img, calyx.filter(ImageFilter.GaussianBlur(0.6)), (42, 118, 36))
    return img


def strawberry_leaf(w=256, h=144) -> Image.Image:
    img = new_canvas(w, h)
    mask = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(mask)
    # Trifoliate hint inside the collider box, stem at left.
    d.ellipse([int(w * 0.08), int(h * 0.18), int(w * 0.48), int(h * 0.82)], fill=255)
    d.ellipse([int(w * 0.36), int(h * 0.08), int(w * 0.78), int(h * 0.55)], fill=255)
    d.ellipse([int(w * 0.36), int(h * 0.45), int(w * 0.80), int(h * 0.92)], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(1.0))
    fur_fill(img, mask, (28, 86, 24), (58, 138, 40), (110, 176, 64), salt=11)
    return img


def strawberry_stem(w=64, h=256) -> Image.Image:
    img = new_canvas(w, h)
    mask = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(mask)
    d.rounded_rectangle([int(w * 0.28), int(h * 0.02), int(w * 0.72), int(h * 0.98)], radius=w * 0.2, fill=255)
    fur_fill(img, mask, (48, 96, 32), (72, 132, 44), (120, 168, 70), salt=2)
    return img


def bone_mask(w: int, h: int, roundness: float = 0.22) -> Image.Image:
    # Fill the collider rectangle; soft cut faces (no hard outline).
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)
    r = max(2, int(min(w, h) * roundness))
    d.rounded_rectangle([1, 1, w - 2, h - 2], radius=r, fill=255)
    return img.filter(ImageFilter.GaussianBlur(0.8))


def hedgehog_body(w, h, kind: str) -> Image.Image:
    img = new_canvas(w, h)
    mask = bone_mask(w, h, 0.18 if kind in ("chest", "pelvis", "head") else 0.28)
    dark = (62, 42, 28)
    mid = (118, 82, 48)
    light = (176, 132, 82)
    belly = (210, 186, 150)
    salt = {"chest": 1, "pelvis": 2, "neck": 3, "head": 4, "jaw": 5, "tail": 6}.get(kind, 8)
    belly_y = 0.34 if kind in ("chest", "pelvis") else 0.0
    use_belly = belly if belly_y > 0.0 else None
    if kind == "head":
        belly_y = 0.22
        use_belly = belly
        light = (196, 154, 108)
    fur_fill(img, mask, dark, mid, light, belly=use_belly, belly_y=belly_y, salt=salt)
    if kind in ("chest", "pelvis"):
        quill_strokes(img, mask, (36, 24, 16), 11 if kind == "chest" else 6, salt + 20)
    if kind == "head":
        # Snout toward +X, dark eye, soft nose.
        d = ImageDraw.Draw(img)
        eye_x, eye_y = int(w * 0.62), int(h * 0.42)
        d.ellipse([eye_x, eye_y, eye_x + max(3, w // 18), eye_y + max(3, h // 14)], fill=(24, 16, 12, 240))
        nx, ny = int(w * 0.88), int(h * 0.52)
        d.ellipse([nx, ny, nx + max(3, w // 22), ny + max(2, h // 18)], fill=(32, 20, 16, 230))
    if kind == "jaw":
        d = ImageDraw.Draw(img)
        d.line([(int(w * 0.15), int(h * 0.55)), (int(w * 0.88), int(h * 0.50))], fill=(90, 60, 40, 160), width=1)
    return img


def hedgehog_limb(w, h, kind: str) -> Image.Image:
    img = new_canvas(w, h)
    mask = bone_mask(w, h, 0.32)
    dark = (54, 36, 22)
    mid = (108, 74, 44)
    light = (168, 124, 78)
    salt = {"front_upper": 10, "front_lower": 11, "thigh": 12, "shin": 13, "paw": 14, "quill": 15}[kind]
    belly = (186, 154, 112) if kind == "paw" else None
    fur_fill(img, mask, dark, mid, light, belly=belly, belly_y=0.45 if belly else 0.0, salt=salt)
    if kind == "quill":
        # Darker spike, tip toward +Y (up on chest).
        quill_strokes(img, mask, (28, 18, 12), 3, 40)
        px = img.load()
        for y in range(h):
            for x in range(w):
                p = px[x, y]
                if p[3] < 2:
                    continue
                t = y / max(1, h - 1)
                shade = mix((28, 18, 12), (90, 64, 40), t)
                px[x, y] = with_alpha(shade, p[3])
    return img


META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 1
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {sid}
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write_meta(png_path: Path):
    meta_path = Path(str(png_path) + ".meta")
    if meta_path.exists():
        return
    guid = uuid.uuid4().hex
    sid = uuid.uuid4().hex[:16] + "0800000000000000"
    meta_path.write_text(META.format(guid=guid, sid=sid), encoding="ascii")


def write_folder_meta(folder: Path):
    meta = Path(str(folder) + ".meta")
    if meta.exists():
        return
    meta.write_text(FOLDER_META.format(guid=uuid.uuid4().hex), encoding="ascii")


def save(img: Image.Image, rel: str):
    path = ART / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    write_folder_meta(path.parent)
    img.save(path, "PNG")
    write_meta(path)
    corner = img.getpixel((0, 0))[3]
    print(f"  {rel} {img.size[0]}x{img.size[1]} A00={corner}")


def main():
    print("yard sprites ->", ART)
    save(apple_fruit(), "Apple/fruit.png")
    save(apple_leaf(), "Apple/leaf.png")
    save(strawberry_berry(), "Strawberry/berry.png")
    save(strawberry_leaf(), "Strawberry/leaf.png")
    save(strawberry_stem(), "Strawberry/stem.png")

    # Collider meters * ~256 px, clamped.
    parts = {
        "chest": (hedgehog_body(512, 228, "chest"), "Hedgehog/chest.png"),
        "pelvis": (hedgehog_body(256, 180, "pelvis"), "Hedgehog/pelvis.png"),
        "neck": (hedgehog_body(160, 128, "neck"), "Hedgehog/neck.png"),
        "head": (hedgehog_body(256, 176, "head"), "Hedgehog/head.png"),
        "jaw": (hedgehog_body(256, 92, "jaw"), "Hedgehog/jaw.png"),
        "tail": (hedgehog_body(160, 88, "tail"), "Hedgehog/tail.png"),
        "front_upper": (hedgehog_limb(140, 240, "front_upper"), "Hedgehog/front_upper.png"),
        "front_lower": (hedgehog_limb(112, 200, "front_lower"), "Hedgehog/front_lower.png"),
        "thigh": (hedgehog_limb(160, 240, "thigh"), "Hedgehog/thigh.png"),
        "shin": (hedgehog_limb(112, 200, "shin"), "Hedgehog/shin.png"),
        "paw": (hedgehog_limb(256, 154, "paw"), "Hedgehog/paw.png"),
        "quill": (hedgehog_limb(64, 240, "quill"), "Hedgehog/quill.png"),
    }
    for _, (im, rel) in parts.items():
        save(im, rel)
    print("done")


if __name__ == "__main__":
    main()
