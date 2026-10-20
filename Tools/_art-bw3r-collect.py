# ART-BW3R collect: condense the aR1 (queued re-read) and aL1 (legs) eye reads.
# usage: python Tools/_art-bw3r-collect.py <prefix> [chars]
import glob
import re
import sys

R = r"E:\Games\2D Game\My project\Trials\Runs\reviews"
pref = sys.argv[1]
lim = int(sys.argv[2]) if len(sys.argv) > 2 else 420

for p in sorted(glob.glob(R + "\\" + pref + "*.md")):
    t = open(p, encoding="utf-8").read().replace("\r", "")
    route = re.search(r"Route: .*?\(([^)]+)\)(.*)", t)
    m = route.group(1).split("/")[-1] if route else "?"
    cache = "CACHE" if route and "cache" in route.group(2).lower() else ""
    a = t.split("## ANSWER", 1)
    a = a[1].strip() if len(a) > 1 else t
    a = re.sub(r"\n{2,}", " ", a)
    scores = re.findall(r"\d+(?:\.\d+)?\s*/\s*10", a)
    print("== %s [%s] %s :: %s" % (p.rsplit("\\", 1)[1], m, cache, " | ".join(scores[:8])))
    print("   " + a[:lim].replace("\n", " "))
