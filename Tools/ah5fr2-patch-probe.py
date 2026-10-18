# Per-row/col summary of the knee patch texture: alpha coverage, mean colour, ink bbox.
import numpy as np
from PIL import Image

P = r"E:/Games/2D Game/My project/Assets/Resources/Art/Human/knee_patch.png"
im = Image.open(P).convert("RGBA")
a = np.array(im)
al = a[..., 3]
print("size", im.size, "alpha>16 px", int((al > 16).sum()), "of", al.size)
rows = np.where((al > 16).any(axis=1))[0]
cols = np.where((al > 16).any(axis=0))[0]
print("ink rows", rows.min(), rows.max(), "ink cols", cols.min(), cols.max())
# alpha coverage per row band (25 rows)
H = al.shape[0]
for r0 in range(0, H, 25):
    band = al[r0:r0 + 25]
    cov = (band > 16).mean()
    rowmean = band.mean()
    print("rows %3d-%3d cov %.3f meanA %6.1f" % (r0, min(r0 + 24, H - 1), cov, rowmean))
