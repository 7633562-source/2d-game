from PIL import Image
import os
A = "Assets/Resources/Art/Human"
for k in ["pelvis","torso","neck","head","upper","lower","hand","thigh","shin","foot","toe"]:
    p = os.path.join(A, k + ".png")
    im = Image.open(p)
    meta = open(p + ".meta", encoding="utf-8").read()
    ppu = [l for l in meta.splitlines() if "spritePixelsToUnits" in l]
    piv = [l for l in meta.splitlines() if "spritePivot" in l]
    print("%-6s %sx%s %s %s" % (k, im.width, im.height, ppu[0].strip(), piv[0].strip()))
img = Image.open("Trials/Runs/arth2-still-ankle-bent.png")
print("still", img.size)
