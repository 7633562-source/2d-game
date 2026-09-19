#!/usr/bin/env python3
"""BALANCE-ARENA-1 current-build cycle table.

    python Tools/impact-cb-table.py

Three things, all read from the run JSONs and nothing typed by hand:

1. identity - every `imp_cb*` run's label, seed, sourceFingerprint, buildGuid, against
   `Build/Headless/build-manifest.json` (the card's acceptance: the fingerprint of the
   delivered run must be the manifest's own).
2. rule zero - `imp_cb_quiet`'s stand numbers against `Trials/Runs/burn_stance.json`.
3. the throw rows - the same class (seed 1, circle 0.25 m, 4 kg, 8 m/s -> torso) as it was
   measured on every accepted build of the day, so a move in the RESPONSE is visible as a
   number and not as an opinion.
"""
import io
import json
import os
import sys

PROJECT = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJECT, "Trials", "Runs")
MANIFEST = os.path.join(PROJECT, "Build", "Headless", "build-manifest.json")

CARD_NUMBERS = ["pelvisDrop", "rmsShoulderJointSpeed", "rmsElbowJointSpeed", "armSwingFraction",
                "sensorTick", "maxAbsPelvisAccY", "wSupNewtons"]

ROW_KEYS = ["contactTime", "contactPart", "referenceImpulseNs", "deliveredImpulseNs",
            "closingSpeedMps", "fell", "ctrlFell", "timeToFallSeconds", "peakComExcursionM",
            "peakComDropM", "minSupportMarginM", "wentOutsideSupport", "stepsAfterImpact",
            "backInsideSupportSeconds", "latencyActiveMs", "pushedBack", "pushFrameAfterContact",
            "pushBackPeakFzRatio", "peakFzTotalN", "limitHitFraction"]

# build -> run label whose throw[0] is the same class, oldest first.
TRACE = [
    ("5794f0ef 11:56", "imp_v1"),
    ("59b8b311 18:03", "l2_hit_s1"),
    ("7ebbad89 19:24", "imp_cb1"),
]


def load(label):
    with io.open(os.path.join(RUNS, label + "-impacts.json"), encoding="utf-8-sig") as handle:
        return json.load(handle)


def main():
    with io.open(MANIFEST, encoding="utf-8-sig") as handle:
        manifest = json.load(handle)
    print("manifest: fingerprint {0} buildGuid {1} gitCommit {2} buildTimeUtc {3} dirty {4}".format(
        manifest.get("fingerprint"), manifest.get("buildGuid"), manifest.get("gitCommit", "")[:8],
        manifest.get("buildTimeUtc"), manifest.get("gitDirty")))

    labels = [name for name in sys.argv[1:]] or [
        "imp_cb_quiet", "imp_cb1", "imp_cb2", "imp_cb3", "imp_cb1L", "imp_cb2L", "imp_cb3L"]
    print("\n== identity")
    print("{0:14s} {1:>4s} {2:>10s} {3:12s} {4:7s}".format("label", "seed", "manifest?", "buildGuid", "throws"))
    for label in labels:
        path = os.path.join(RUNS, label + "-impacts.json")
        if not os.path.exists(path):
            print("{0:14s}   (no impacts json yet)".format(label))
            continue
        doc = load(label)
        mf = "MATCH" if doc.get("sourceFingerprint") == manifest.get("fingerprint") else "STALE"
        print("{0:14s} {1:>4} {2:>10s} {3:12s} {4:7d}".format(
            label, doc.get("switches", {}).get("impactSeed", "-"), mf,
            (doc.get("buildGuid") or "")[:12], len(doc.get("throws", []))))

    print("\n== rule zero (imp_cb_quiet vs burn_stance)")
    try:
        base = json.load(io.open(os.path.join(RUNS, "burn_stance.json"), encoding="utf-8-sig"))
        quiet = json.load(io.open(os.path.join(RUNS, "imp_cb_quiet.json"), encoding="utf-8-sig"))
        for key in CARD_NUMBERS:
            a, b = base.get(key), quiet.get(key)
            flag = "ok" if a == b else "MOVED"
            print("  {0:24s} {1!r:>12} -> {2!r:>12}  {3}".format(key, a, b, flag))
    except IOError as exc:
        print("  {0}".format(exc))

    print("\n== the same class on every accepted build (throw[0])")
    rows = []
    for build, label in TRACE:
        try:
            rows.append((build, label, load(label)["throws"][0]))
        except (IOError, KeyError, IndexError):
            print("  {0}: no run".format(label))
    if rows:
        for key in ROW_KEYS:
            line = "  {0:26s}".format(key)
            first = None
            for _, _, row in rows:
                value = row.get(key)
                if first is None:
                    first = value
                line += " {0!r:>26s}".format(value)
            print(line)
        print("  {0:26s}".format("build/run") + "".join(
            " {0:>26s}".format(build + " " + label) for build, label, _ in rows))


if __name__ == "__main__":
    main()
