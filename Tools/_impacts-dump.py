#!/usr/bin/env python3
"""Read an impact run JSON and print its structure (grid cycle helper)."""
import json
import sys

path = sys.argv[1]
j = json.load(open(path, encoding="utf-8-sig"))
print("top keys:", list(j.keys()))
if "inputs" in j:
    print("inputs:", json.dumps(j["inputs"]))
if "meta" in j:
    print("meta:", json.dumps(j["meta"])[:600])
t = j.get("throws")
print("throws:", len(t) if isinstance(t, list) else t)
if isinstance(t, list) and t:
    print("throw0 keys:")
    for k, v in t[0].items():
        print("   ", k, "=", v)
for key in ("samples", "summary", "stand"):
    if key in j:
        v = j[key]
        print(key, "->", json.dumps(v)[:900] if not isinstance(v, list) else f"list[{len(v)}]")
