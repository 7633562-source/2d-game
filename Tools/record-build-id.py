#!/usr/bin/env python
"""Which player did a stored run actually come out of?

Why this exists: a run label names a parameter set, not a build. Three times on
19.09 a comment was written against a run whose build had already been replaced
(the arm stream built at 19:55, 20:04 and 20:07:50 inside thirteen minutes), so a
"measured" number could not be reproduced on the tree it was read from. Every
trial record carries the answer - ``buildGuid``, ``sourceFingerprint`` and the
manifest time it was launched with - and the fingerprint is a hash over
Assets/Packages/ProjectSettings, so it can be recomputed for the tree on disk and
compared.

  fingerprint recorded == current fingerprint  -> the run speaks for this tree
  different                                    -> the run's build is gone; the
                                                  number cannot be re-measured
                                                  without a rebuild + a run

Read-only: opens JSON/CSV records and hashes sources. It edits nothing, launches
nothing and never writes a manifest.

Usage:
    python Tools/record-build-id.py                     # the 12 newest records
    python Tools/record-build-id.py p_arms80 p_arms81   # named labels
    python Tools/record-build-id.py --depth 40          # wider default window
"""
import glob
import importlib.util
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")
BUILD = os.path.join(ROOT, "Build", "Headless")
MANIFEST = os.path.join(BUILD, "build-manifest.json")


def _manifest_module():
    """Tools/trial-manifest.py, loaded for its files()/fingerprint() pair."""
    path = os.path.join(ROOT, "Tools", "trial-manifest.py")
    spec = importlib.util.spec_from_file_location("trial_manifest", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def current_fingerprint():
    mod = _manifest_module()
    rows = mod.files()
    return mod.fingerprint(rows), len(rows)


def record(label):
    with open(os.path.join(RUNS, label + ".json"), encoding="utf-8-sig") as f:
        doc = json.load(f)
    meta = doc.get("meta", {}) if isinstance(doc.get("meta"), dict) else {}
    return dict(label=label,
                guid=str(doc.get("buildGuid", ""))[:8],
                fp=str(doc.get("sourceFingerprint", "")),
                built=str(meta.get("manifestBuildTimeUtc", "")),
                unity=str(doc.get("unityVersion", "")))


def main():
    argv = [a for a in sys.argv[1:]]
    depth = 12
    if "--depth" in argv:
        i = argv.index("--depth")
        depth = int(argv[i + 1])
        del argv[i:i + 2]
    labels = [a for a in argv if not a.startswith("--")]

    if not labels:
        paths = glob.glob(os.path.join(RUNS, "*.json"))
        paths.sort(key=lambda p: os.path.getmtime(p), reverse=True)
        labels = [os.path.basename(p)[:-5] for p in paths[:depth]]

    cur, count = current_fingerprint()
    man = {}
    if os.path.exists(MANIFEST):
        with open(MANIFEST, encoding="utf-8-sig") as f:
            man = json.load(f)
    print("current tree: fingerprint %s over %d files" % (cur[:16], count))
    print("last manifest: %s  buildGuid %s  built %s  gitCommit %s"
          % (str(man.get("fingerprint", "-"))[:16], str(man.get("buildGuid", "-"))[:8],
             man.get("buildTimeUtc", "-"), str(man.get("gitCommit", "-"))[:8]))
    print()
    print("%-14s %-9s %-17s %-21s %s" % ("label", "buildGuid", "fingerprint", "manifest build (UTC)", "for this tree?"))
    by_fp = {}
    for label in labels:
        try:
            r = record(label)
        except Exception as exc:  # noqa: BLE001 - one bad record must not stop the sweep
            print("%-14s UNREADABLE (%s)" % (label, exc))
            continue
        same = "YES" if r["fp"] == cur else "no"
        by_fp.setdefault(r["fp"], []).append(label)
        print("%-14s %-9s %-17s %-21s %s" % (r["label"], r["guid"], r["fp"][:16], r["built"], same))
    print()
    for fp, group in sorted(by_fp.items(), key=lambda kv: -len(kv[1])):
        print("fingerprint %s : %d label(s) %s" % (fp[:16], len(group), ", ".join(group)))


main()
