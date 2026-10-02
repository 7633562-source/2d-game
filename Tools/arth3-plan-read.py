#!/usr/bin/env python3
"""ART-H3 probe: print the joint plan (per part/end and per joint) compactly."""
import json, os
P = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                 "Trials", "Runs", "art-h1-joint-plan.json")
d = json.load(open(P, encoding="utf-8"))
for part, e in d.get("parts", {}).items():
    ends = []
    for k, v in e.items():
        ends.append("%s=%s" % (k, v))
    print("part %-7s %s" % (part, " ".join(ends)))
print()
for j in d["joints"]:
    print("joint %-8s %s(%s)->%s(%s) R=%.4f m=%.4f skirt=%s span=%s" % (
        j["joint"], j["parent"], j.get("parent_end"), j["child"], j.get("child_end"),
        j["r_eye"], j["m"], j["skirt_side"], j["span"]))
