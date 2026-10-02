"""Check whether a RENDERED (-visual) take and a headless take of the same protocol are
the same run. Usage:

    python Tools/pc-visual-clock-ab.py <labelA> [<labelB> ...]

Reads Trials/Runs/<label>.json + .csv and prints, for each pair, every differing key
with identity/clock classes separated, plus the CoM x-span recomputed from each trace.
Run it on any (headless, -visual) pair before quoting the two as one experiment.

Why it exists (programmer cycle 20.09): on the human trial the two takes ARE two
different runs. `pc_vischeck_head` / `pc_vischeck_vis` on one player (fp 951769c3,
guid f4927b60, dll 10:18), one flag apart, same fall clock (7.645 s) and step count
(2), differ in 87 of 238 shared keys - comTravelX 2.7377 -> 3.1158 m, armPressStrokesL
5 -> 3, maxAbsPelvisAccY 520.8 -> 361.4. The same pair reproduces the two production
labels to the digit (headless == p_auto, visual == burn_walk), so both results are
individually reproducible and the difference is the take, not the seed.
"""
import csv
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")
IDENTITY_TOKENS = ("guid", "fingerprint", "wall", "realtime", "clock", "commandline",
                   "args", "label")


def load(label):
    with open(os.path.join(RUNS, label + ".json"), encoding="utf-8-sig") as f:
        return json.load(f)


def com_span(label):
    """CoM x-span from the run's own TrialRecorder trace (the row's comX column)."""
    path = os.path.join(RUNS, label + ".csv")
    if not os.path.exists(path):
        return None
    xs = []
    with open(path, encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f, delimiter=";")
        for row in reader:
            try:
                xs.append(float(row["comX"]))
            except (ValueError, KeyError):
                pass
    if not xs:
        return None
    return len(xs), min(xs), max(xs), max(xs) - min(xs)


def _strip_identity(obj):
    """Drop identity/clock leaves so a dict-vs-dict diff names physics only. The
    `applied` and `inputs` blocks embed the label and the command line, which differ
    by construction between two takes and would otherwise read as 40 key diffs."""
    if isinstance(obj, dict):
        return {k: _strip_identity(v) for k, v in obj.items()
                if not any(t in k.lower() for t in IDENTITY_TOKENS)}
    if isinstance(obj, list):
        return [_strip_identity(v) for v in obj]
    return obj


def compare(a_label, b_label):
    a, b = load(a_label), load(b_label)
    shared = sorted(set(a) & set(b))
    diff = [k for k in shared if a[k] != b[k]]
    identity = [k for k in diff if any(t in k.lower() for t in IDENTITY_TOKENS)]
    physics = []
    for k in diff:
        if k in identity:
            continue
        va, vb = a[k], b[k]
        if isinstance(va, dict) and isinstance(vb, dict):
            if _strip_identity(va) == _strip_identity(vb):
                identity.append(k + " (differs in label/args only)")
                continue
        physics.append(k)
    print("=== %s vs %s ===" % (a_label, b_label))
    print("  shared keys %d, differing %d (identity/clock %d, physics %d)"
          % (len(shared), len(diff), len(identity), len(physics)))
    print("  identity/clock: %s" % ", ".join(identity))
    print("  physics keys:")
    for k in physics:
        va, vb = a[k], b[k]
        if isinstance(va, float) and isinstance(vb, float):
            print("     %-28s %14.6f -> %14.6f   d=%+.6f" % (k, va, vb, vb - va))
        else:
            print("     %-28s %s -> %s" % (k, str(va)[:50], str(vb)[:50]))
    print("  traces:")
    for label in (a_label, b_label):
        s = com_span(label)
        if s:
            n, mn, mx, span = s
            print("     %-22s rows=%d  comX %+.4f .. %+.4f  span=%.4f" % (label, n, mn, mx, span))
    print()


def main(argv):
    labels = argv[1:]
    if len(labels) < 2:
        print(__doc__)
        print("example: python Tools/pc-visual-clock-ab.py pc_vischeck_head pc_vischeck_vis")
        return 2
    for i in range(0, len(labels) - 1, 2):
        compare(labels[i], labels[i + 1])
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
