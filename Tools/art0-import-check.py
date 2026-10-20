"""Report PNG dimensions/sizes for Resources art (ART-0 import check)."""
from PIL import Image
import glob, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

def show(pat):
    for p in sorted(glob.glob(os.path.join(ROOT, pat))):
        im = Image.open(p)
        print("%-46s %-11s %-5s %8d B" % (os.path.relpath(p, ROOT).replace("\\", "/"),
                                          "%dx%d" % im.size, im.mode, os.path.getsize(p)))

show("Assets/Resources/Art/*.png")
show("Assets/Resources/Level/*.png")
print("--- LegacyArt/Materials ---")
show("Assets/LegacyArt/Materials/*.png")
