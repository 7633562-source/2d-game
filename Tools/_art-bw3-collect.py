# ART-BW3 collect: condense the round's eye reads for the close report.
# usage: python Tools/_art-bw3-collect.py row|mic|ab   (prints condensed text)
import glob
import re
import sys

R = r"E:\Games\2D Game\My project\Trials\Runs\reviews"
PAT = {
    "row": ["aBW3_row_before_*.md", "aBW3_row_pre_*.md", "aBW3_row_after_*.md", "aBW3b_row_after_*.md"],
    "mic": ["aBW3_before_mic_*.md", "aBW3_after_mic_*.md", "aBW3b_after_mic_*.md"],
    "ab": ["aBW3_ab1_*.md", "aBW3_ab2_*.md", "aBW3b_ab1_*.md", "aBW3b_ab2_*.md"],
    "aBW2row": ["aBW2_ab1_*.md", "aBW2_ab2_*.md", "aBW2_after_mic_*.md", "aBW2_before_mic_*.md",
                "aBW2_form_after_*.md", "aBW2_form_before_*.md"],
}[sys.argv[1]]


def one(p):
    t = open(p, encoding="utf-8").read().replace("\r", "")
    route = re.search(r"Route: .*?\(([^)]+)\)(.*)", t)
    a = t.split("## ANSWER", 1)
    a = a[1].strip() if len(a) > 1 else t
    a = re.sub(r"\n{2,}", "\n", a)
    m = route.group(1).split("/")[-1] if route else "?"
    cache = "CACHE" if route and "cache" in route.group(2).lower() else ""
    return m, cache, a


for pat in PAT:
    for p in sorted(glob.glob(R + "\\" + pat)):
        m, c, a = one(p)
        name = p.rsplit("\\", 1)[1]
        if sys.argv[1] in ("row",):
            print("== %s [%s] %s" % (name, m, c))
            print(a[:700] if len(a) <= 700 else a[:400] + " ... " + a[-300:])
            print()
        else:
            scores = re.findall(r"\d+\s*/\s*10|\d+(?=\s*[—-]\s*(?:сам|главн|the single))", a)
            print("== %s [%s] %s :: %s" % (name, m, c, " | ".join(scores[:6])))
