# ART-BW1 probe 11: the 18 reads at a glance - score(s) + the closing (fault) line per file.
import os
import re

R = r"E:\Games\2D Game\My project\Trials\Runs\reviews"
files = sorted(f for f in os.listdir(R) if f.startswith("aBW1_") and f.endswith(".md"))
for f in files:
    txt = open(os.path.join(R, f), encoding="utf-8", errors="replace").read()
    # drop the meta footer if present (lines starting with '#' markers or 'meta')
    body = txt.split("\n---")[0]
    scores = re.findall(r"(\d(?:[.,]\d)?)\s*/\s*10", body)
    tail = " ".join(body.strip().split())[-320:]
    print("### %s   scores=%s" % (f, scores))
    print("    ...%s" % tail)
