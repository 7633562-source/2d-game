"""LEVEL 20.09: why the gallery prints "logs dressed 0/3" while the logs are painted.

GalleryScene.BuildFireRow counts a log as dressed when
`sr.drawMode == SpriteDrawMode.Sliced && ArtParts.BoxOf(sr) == piece.size`.
Before that call it runs HumanSegment.ApplyPicture, which puts a WHITE body sprite
on the renderer; that sprite is pixel-snapped (CreateBodySprite: 256 px/m, a 64-px
minimum side, ppu = w / size.x), so its own bounds are not exactly the box it was
asked for. ArtParts.Dress then reads that box, dresses the canvas onto it and sets
drawMode = Sliced - the picture is right - but the renderer's `size` is now the
SNAPPED box, and the counter compares it with the requested one.

This reproduces the arithmetic from the two sources (no Unity): it reads the three
boxes out of Campfire.LogRow, applies HumanSegment.CreateBodySprite's constants to
each, and prints the box the counter would see against the box it wants.

Usage:
    python Tools/level-logs-dress-counter.py
"""
import re

ROOT = "E:/Games/2D Game/My project"
CAMPAIGN = ROOT + "/Assets/Campfire.cs"
HUMPART = ROOT + "/Assets/HumanSegment.cs"

# HumanSegment.CreateBodySprite, quoted constants.
PIXELS_PER_METER = 256.0
MAX_TEX = 16384
MAX_SIDE = 512
MIN_SIDE = 64
CLAMP_LO, CLAMP_HI = 8, 16384

# Unity's Vector2 operator== : (lhs - rhs).sqrMagnitude < kEpsilon * kEpsilon.
EPS2 = 9.99999944e-11


def body_sprite_box(size):
    """The bounds of the white box sprite ApplyPicture leaves on the renderer."""
    w = max(1, int(round(size[0] * PIXELS_PER_METER)))
    h = max(1, int(round(size[1] * PIXELS_PER_METER)))
    scale = 1.0
    if w > MAX_SIDE or h > MAX_SIDE:
        scale = MAX_SIDE / float(max(w, h))
    min_side = min(w, h) * scale
    if min_side < MIN_SIDE:
        scale *= MIN_SIDE / max(1.0, min_side)
    w = min(max(int(round(w * scale)), CLAMP_LO), MAX_TEX)
    h = min(max(int(round(h * scale)), CLAMP_LO), MAX_TEX)
    ppu = w / size[0]
    return (w / ppu, h / ppu), (w, h, ppu)


def log_boxes(text):
    out = []
    for name, sx, sy in re.findall(r'name = "(Log[ABC])";\s*\n\s*size = new Vector2\(([0-9.]+)f, ([0-9.]+)f\)',
                                   text):
        out.append((name, (float(sx), float(sy))))
    return out


def main():
    with open(CAMPAIGN, encoding="utf-8") as f:
        boxes = log_boxes(f.read())
    if not boxes:
        raise SystemExit("no LogRow boxes read from " + CAMPAIGN)

    print("log   requested box (m)   white body sprite box (m)   delta (m)      Vector2== ")
    fails = 0
    for name, size in boxes:
        got, tex = body_sprite_box(size)
        dx, dy = got[0] - size[0], got[1] - size[1]
        sqr = dx * dx + dy * dy
        equal = sqr < EPS2
        fails += 0 if equal else 1
        print("%-5s %-19s %-27s (%+.5f,%+.5f)  %s   tex %dx%d ppu %.1f"
              % (name, "%.2f x %.2f" % size, "%.5f x %.5f" % got, dx, dy,
                 "equal" if equal else "NOT equal", tex[0], tex[1], tex[2]))

    print()
    print("LOGS-DRESS-COUNTER  boxes=%d counter_can_hold=%d  (0 means the counter is 0 by"
          " construction, not because the dress was refused)" % (len(boxes), len(boxes) - fails))
    print("The dress itself is decided by the frame, not by this counter: see"
          " Tools/level-fire-window-flat.py.")


main()
