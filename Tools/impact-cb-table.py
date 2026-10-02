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
# Superseded evidence is moved aside instead of deleted: a superseded run is still the only
# record of that build. Two archives exist now - the 19:24 (7ebbad89) set, superseded by the
# 22:32 (bf191843) re-run, and the bf191843 set itself, superseded when the tree moved on and
# the manifest became a12521af (23:33).
ARCHIVE = os.path.join(RUNS, "impact-pre-bf191843")
ARCHIVE_A = os.path.join(RUNS, "impact-pre-a12521af")
# The 23:33 (a12521af) set itself, superseded inside a two-minute window by the dog lane's
# rebuilds at 00:30 and 00:32: `impact-pre-8868d6a6/` is named for the manifest that
# superseded it, as the two folders above are.
ARCHIVE_B = os.path.join(RUNS, "impact-pre-8868d6a6")
# The 00:35 (39a689fd) set, superseded at 02:11 by `8c4be56d` (638 files): the walk lane's
# build added one recorder column and moved three of its own arm-window columns, so the
# eight 39a689fd labels were archived as `impact-pre-8c4be56d/` and re-run (level cycle 20.09
# 02:33). Named for the manifest that superseded them, as the three folders above are.
ARCHIVE_C = os.path.join(RUNS, "impact-pre-8c4be56d")
# The 02:11 (8c4be56d) set, superseded at 02:35 by 6ff2325c, and the 02:35 set itself,
# superseded at 05:42 by 1073b8df (level cycle 20.09 06:10). Each folder is named for the
# manifest that superseded the set it holds, so the TRACE row below reads the folder, not
# RUNS: a row that reads RUNS silently becomes the newest set the moment a new one lands
# (the 8c4be56d row did exactly that between the 02:35 and the 06:10 cycles - it printed
# 8c4be56d while reading 6ff2325c, and the two sets happen to be byte-identical, so the
# mislabel was invisible until this archive made it visible).
ARCHIVE_D = os.path.join(RUNS, "impact-pre-6ff2325c")
ARCHIVE_E = os.path.join(RUNS, "impact-pre-1073b8df")
MANIFEST = os.path.join(PROJECT, "Build", "Headless", "build-manifest.json")

CARD_NUMBERS = ["pelvisDrop", "rmsShoulderJointSpeed", "rmsElbowJointSpeed", "armSwingFraction",
                "sensorTick", "maxAbsPelvisAccY", "wSupNewtons"]

ROW_KEYS = ["contactTime", "contactPart", "referenceImpulseNs", "deliveredImpulseNs",
            "closingSpeedMps", "fell", "ctrlFell", "timeToFallSeconds", "peakComExcursionM",
            "peakComDropM", "minSupportMarginM", "wentOutsideSupport", "stepsAfterImpact",
            "backInsideSupportSeconds", "latencyActiveMs", "pushedBack", "pushFrameAfterContact",
            "pushBackPeakFzRatio", "peakFzTotalN", "limitHitFraction"]

# build -> run label whose throw[0] is the same class, oldest first, with the folder that
# holds it (a label reused by a later re-run names two different players, so the row has to
# say which file it read).
TRACE = [
    ("5794f0ef 11:56", "imp_v1", RUNS),
    ("59b8b311 18:03", "l2_hit_s1", RUNS),
    ("7ebbad89 19:24", "imp_cb1", ARCHIVE),
    ("bf191843 22:32", "imp_cb1", ARCHIVE_A),
    ("a12521af 23:33", "imp_cb1", ARCHIVE_B),
    ("39a689fd 00:35", "imp_cb1", ARCHIVE_C),
    ("8c4be56d 02:11", "imp_cb1", ARCHIVE_D),
    ("6ff2325c 02:35", "imp_cb1", ARCHIVE_E),
    ("1073b8df 05:42", "imp_cb1", RUNS),
]


def load(label, folder=RUNS):
    with io.open(os.path.join(folder, label + "-impacts.json"), encoding="utf-8-sig") as handle:
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
    print("{0:14s} {1:>4s} {2:>10s} {3:>6s} {4:12s} {5:7s}".format(
        "label", "seed", "manifest?", "guid?", "buildGuid", "throws"))
    for label in labels:
        path = os.path.join(RUNS, label + "-impacts.json")
        if not os.path.exists(path):
            print("{0:14s}   (no impacts json yet)".format(label))
            continue
        doc = load(label)
        mf = "MATCH" if doc.get("sourceFingerprint") == manifest.get("fingerprint") else "STALE"
        # The fingerprint is over the manifest's file NAMES (trial-manifest.py), so two
        # rebuilds of the same file set print the same fingerprint while being two builds.
        # The buildGuid is the only field that tells those apart, and it is printed here
        # because a fingerprint MATCH can hide a run made on a different player.
        guid = (doc.get("buildGuid") or "")
        gm = "same" if guid == manifest.get("buildGuid") else "OTHER"
        print("{0:14s} {1:>4} {2:>10s} {3:>6s} {4:12s} {5:7d}".format(
            label, doc.get("switches", {}).get("impactSeed", "-"), mf, gm,
            guid[:12], len(doc.get("throws", []))))

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
    for build, label, folder in TRACE:
        try:
            rows.append((build + " " + label + ("" if folder == RUNS else " (kept)"),
                         load(label, folder)["throws"][0]))
        except (IOError, KeyError, IndexError):
            print("  {0}: no run".format(label))
    if rows:
        for key in ROW_KEYS:
            line = "  {0:26s}".format(key)
            for _, row in rows:
                line += " {0!r:>26s}".format(row.get(key))
            print(line)
        print("  {0:26s}".format("build/run") + "".join(
            " {0:>26s}".format(name) for name, _ in rows))


if __name__ == "__main__":
    main()
