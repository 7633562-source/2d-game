"""ART-0 (17th pass): does the delivered trunk repeat with the bark tile period?

OUTCOME: it does not answer the question, and it is kept as the record of why.

`ArtParts.Dress` builds each wood renderer from `ArtLibrary.Tiled(ArtLibrary.Bark,
ArtParts.BarkTileMeters = 0.5)` -> a Sprite whose native size is exactly 0.5 m, drawn in Tiled mode.
Tiled mode repeats the sprite at its own native size, so a FLAT bark surface MUST come back every
0.5 m - in `art0leaffield_tree.png` (ortho 1.30369687, 414.21 px/m) that is 207.10 px. The 5x5
mean-difference curve at shifts 180..240 px is flat: 32.18 at 180 px, 34.11 at 207 px, 35.53 at
240 px, over a region whose own std is 42.0 / 29.9 / 32.2. Two readings are possible and the frame
cannot separate them: either the chosen rect is not bark, or the trunk it holds is not a flat bark
surface. It is the second - that still's lower-middle region carries the crown's cut planes crossing
the trunk (the plant lane's recorded item), so no trunk-only mask can be isolated from it and the
frame is not a bark evidence surface today.

The bark verdict therefore rests on the 2 x 2 native panel read instead
(`Tools/art0-wrap-panels-17th.py`, `Trials/Runs/art-0-wrap-17th/`), where `bark` came back as one
continuous material with no visible joint. Do not quote this probe as evidence about bark; quote it
as the reason the frame was not used.
"""
import os
import sys

import numpy as np
from PIL import Image

RUNS = r"E:/Games/2D Game/My project/Trials/Runs"
ART = r"E:/Games/2D Game/My project/Assets/Resources/Art"
LABEL = "art0leaffield"
ORTHO = 1.303696870803833
TILE_M = 0.5
RECT = (660, 730, 1140, 1075)      # x0, y0, x1, y1 - the lower trunk in this frame
HALF = 2


def box_mean(img, half):
    k = 2 * half + 1
    pad = np.pad(img, ((half, half), (half, half), (0, 0)), mode="edge")
    out = np.zeros_like(img, dtype=np.float64)
    for dy in range(k):
        for dx in range(k):
            out += pad[dy:dy + img.shape[0], dx:dx + img.shape[1]]
    return out / (k * k)


def main():
    img = np.asarray(Image.open(os.path.join(RUNS, LABEL + "_tree.png")).convert("RGB"), dtype=np.float64)
    bark = np.asarray(Image.open(os.path.join(ART, "bark.png")).convert("RGB"), dtype=np.float64)
    bmean = bark.reshape(-1, 3).mean(axis=0)
    px_per_m = (img.shape[0] / 2.0) / ORTHO
    period = TILE_M * px_per_m
    x0, y0, x1, y1 = RECT
    sub = img[y0:y1, x0:x1]
    print("frame %s  px/m %.2f  tile %.2f px" % (img.shape[:2], px_per_m, period))
    print("rect %s mean rgb %.1f, %.1f, %.1f  (shipped bark mean %.1f, %.1f, %.1f)"
          % (RECT, sub[:, :, 0].mean(), sub[:, :, 1].mean(), sub[:, :, 2].mean(),
             bmean[0], bmean[1], bmean[2]))
    print("rect std  %.1f, %.1f, %.1f" % (sub[:, :, 0].std(), sub[:, :, 1].std(), sub[:, :, 2].std()))

    g = box_mean(sub, HALF)
    h, w = g.shape[:2]
    print()
    print("=== MAD at shift d, x and y, 5x5 patch difference, 0-255 levels ===")
    for d in range(180, 241, 1):
        madx = float(np.abs(g[:, :w - d] - g[:, d:]).mean()) if w > d else float("nan")
        mady = float(np.abs(g[:h - d, :] - g[d:, :]).mean()) if h > d else float("nan")
        mark = ""
        if abs(d - period) < 1.0:
            mark = "  <- TILE 0.5 m"
        print("  d=%-4d  x %8.3f   y %8.3f%s" % (d, madx, mady, mark))
    return 0


if __name__ == "__main__":
    sys.exit(main())
