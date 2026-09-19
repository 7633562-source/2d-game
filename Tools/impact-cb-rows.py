#!/usr/bin/env python3
"""The three quotable classes of the current build, one row each.

    python Tools/impact-cb-rows.py                 # imp_cb1 imp_cb2 imp_cb3
    python Tools/impact-cb-rows.py imp_cb1L imp_cb2L imp_cb3L

Why a second tool beside `impact-cb-table.py`: that one answers the card's IDENTITY and
RULE ZERO clauses and traces ONE class across builds; this one prints the per-throw row
the report and the caption quote, for every class of the delivered triple, read from the
runs' own `-impacts.json` (never typed by hand). Every printed row carries the label's own
`sourceFingerprint`/`buildGuid` and is checked against `Build/Headless/build-manifest.json`,
so a STALE row cannot be quoted as current.
"""
import io
import json
import os
import sys

PROJECT = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJECT, "Trials", "Runs")
ARCHIVE = os.path.join(RUNS, "impact-pre-bf191843")
MANIFEST = os.path.join(PROJECT, "Build", "Headless", "build-manifest.json")

KEYS = ["contactTime", "contactPart", "referenceImpulseNs", "deliveredImpulseNs",
        "closingSpeedMps", "fell", "ctrlFell", "timeToFallSeconds", "peakComExcursionM",
        "peakComDropM", "minSupportMarginM", "wentOutsideSupport", "stepsAfterImpact",
        "backInsideSupportSeconds", "latencyActiveMs", "pushedBack", "pushFrameAfterContact",
        "pushBackPeakFzRatio", "peakFzTotalN", "limitHitFraction"]


def find(label):
    """The run's file: root first, then the kept older evidence (a reused label names two
    different players, so the caller is told which folder answered)."""
    for folder in (RUNS, ARCHIVE):
        path = os.path.join(folder, label + "-impacts.json")
        if os.path.exists(path):
            return folder, path
    return None, None


def main():
    manifest = json.load(io.open(MANIFEST, encoding="utf-8-sig"))
    labels = sys.argv[1:] or ["imp_cb1", "imp_cb2", "imp_cb3"]
    print("manifest: fingerprint {0} buildGuid {1} buildTimeUtc {2}".format(
        manifest.get("fingerprint"), manifest.get("buildGuid"), manifest.get("buildTimeUtc")))
    for label in labels:
        folder, path = find(label)
        if path is None:
            print("\n== {0}: no impacts json".format(label))
            continue
        doc = json.load(io.open(path, encoding="utf-8-sig"))
        match = "MATCH" if doc.get("sourceFingerprint") == manifest.get("fingerprint") else "STALE"
        where = "Trials/Runs" if folder == RUNS else os.path.basename(folder)
        print("\n== {0}  ({1})  {2}  buildGuid {3}".format(
            label, where, match, (doc.get("buildGuid") or "")[:12]))
        sw = doc.get("switches", {})
        print("   switches: shape={0} size={1} mass={2} speed={3} aim={4} seed={5} side={6} count={7} at={8}".format(
            sw.get("shape"), sw.get("sizeMeters"), sw.get("massKg"), sw.get("speedMps"),
            sw.get("aim"), sw.get("impactSeed"), sw.get("side"), sw.get("count"), sw.get("firstThrowAt")))
        throws = doc.get("throws", [])
        for index, row in enumerate(throws):
            print("   -- throw[{0}]".format(index))
            for key in KEYS:
                print("      {0:26s} {1!r}".format(key, row.get(key)))
            ref, delivered = row.get("referenceImpulseNs"), row.get("deliveredImpulseNs")
            if ref:
                print("      {0:26s} {1:.4f}".format("delivered/reference", delivered / ref))


if __name__ == "__main__":
    main()
