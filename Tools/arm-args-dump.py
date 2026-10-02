#!/usr/bin/env python
# Read-only: the switch line of each stored p_arms* run, so a series that differs in
# ONE switch can be told from a series that differs in five. ARM cycle 11.
# Usage: python Tools/arm-args-dump.py p_arms53 p_arms54 ...
import sys, os, json, glob

RUNS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Trials", "Runs")


def main():
    labels = sys.argv[1:]
    if not labels:
        labels = sorted(os.path.basename(p)[:-5] for p in glob.glob(os.path.join(RUNS, "p_arms*.json")))
    for label in labels:
        jp = os.path.join(RUNS, label + ".json")
        if not os.path.exists(jp):
            continue
        try:
            js = json.load(open(jp))
        except Exception:
            print("%-14s (unreadable json)" % label)
            continue
        a = js.get("commandArgs") or []
        if isinstance(a, str):
            a = a.split()
        pairs = []
        i = 0
        while i < len(a):
            if a[i].startswith("-arm"):
                nxt = a[i + 1] if i + 1 < len(a) and not a[i + 1].startswith("-") else ""
                pairs.append((a[i] + " " + nxt).strip())
                i += 2 if nxt else 1
            else:
                i += 1
        print("%-14s %s" % (label, " | ".join(pairs) if pairs else "(no -arm switches: all defaults)"))


if __name__ == "__main__":
    main()
