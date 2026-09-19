"""LEVEL freshness audit (level worker).

Answers one question with numbers and no Unity: does every delivered evidence set
name the player that is on disk right now, and is every input that player was
built from older than the player itself?

Reads (read-only):
  Build/Headless/build-manifest.json      the headless trial player
  Build/Preview/GalleryPreview_Data/globalgamemanagers   the art catalogue player
  Trials/Runs/gallery*/*.json, species *_run.json, impact_*   evidence on disk
  every Assets/**/*.cs|png|meta|asset                          the inputs

Prints one line per player (data mtime, manifest buildGuid, fingerprint) and one
line per evidence set (newest file, the player it names), then the verdict.
"""

import json
import os
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")


def mt(path):
    try:
        return os.path.getmtime(path)
    except OSError:
        return None


def ts(value):
    if value is None:
        return "-"
    return time.strftime("%m-%d %H:%M:%S", time.localtime(value))


def newest_inputs(pattern_dir):
    newest = []
    for root, _dirs, files in os.walk(pattern_dir):
        for name in files:
            if name.endswith((".cs", ".png", ".meta", ".asset")):
                path = os.path.join(root, name)
                newest.append((mt(path), os.path.relpath(path, ROOT)))
    newest.sort(reverse=True)
    return newest


def read_json(path):
    try:
        with open(path, encoding="utf-8") as handle:
            return json.load(handle)
    except (OSError, ValueError):
        return None


def main():
    print("== players on disk ==")
    players = {}
    for label, data, manifest in (
        ("headless", os.path.join(ROOT, "Build", "Headless", "Headless_Data", "globalgamemanagers"),
         os.path.join(ROOT, "Build", "Headless", "build-manifest.json")),
        ("preview", os.path.join(ROOT, "Build", "Preview", "GalleryPreview_Data", "globalgamemanagers"),
         os.path.join(ROOT, "Build", "Preview", "build-manifest.json")),
    ):
        man = read_json(manifest) or {}
        players[label] = {
            "data": mt(data),
            "dataPath": data,
            "manifest": manifest,
            "manifestMtime": mt(manifest),
            "guid": man.get("buildGuid"),
            "fp": man.get("sourceFingerprint"),
            "files": man.get("fileCount") or man.get("files"),
            "buildTimeUtc": man.get("buildTimeUtc"),
        }
        info = players[label]
        print("  %-8s data %s | manifest %s | guid %s | files %s | built %s"
              % (label, ts(info["data"]), ts(info["manifestMtime"]),
                 str(info["guid"])[:12], info["files"], info["buildTimeUtc"]))

    print()
    print("== newest Assets inputs (the players must be newer) ==")
    newest = newest_inputs(os.path.join(ROOT, "Assets"))
    for stamp, rel in newest[:12]:
        print("  %s  %s" % (ts(stamp), rel))
    for label, info in players.items():
        if info["data"] is None or not newest:
            continue
        newer = [rel for stamp, rel in newest if stamp > info["data"]]
        print("  %-8s: %d input(s) NEWER than the player data -> %s"
              % (label, len(newer), ", ".join(newer[:6]) if newer else "fresh"))

    print()
    print("== evidence sets on disk (newest file in the set) ==")
    sets = []
    for pattern, kind in (("gallery", "gallery"), ("species", "species"), ("impact", "impact")):
        stamps = []
        for name in os.listdir(RUNS):
            low = name.lower()
            if low.startswith(pattern) and os.path.isfile(os.path.join(RUNS, name)):
                stamps.append((mt(os.path.join(RUNS, name)), name))
        if not stamps:
            continue
        stamps.sort(reverse=True)
        latest_label = None
        for _stamp, name in stamps:
            if name.endswith("-gallery.json") or name.endswith("_run.json") or name.endswith("_run.csv"):
                latest_label = name
                break
        sets.append((kind, stamps[0], len(stamps), latest_label))
    for kind, (stamp, name), count, payload in sets:
        print("  %-8s newest %s  %s  (%d files; payload %s)"
              % (kind, ts(stamp), name, count, payload))
        if payload and payload.endswith("-gallery.json"):
            data = read_json(os.path.join(RUNS, payload)) or {}
            capt = data.get("captures") or data.get("shots") or []
            print("           gallery label %s buildGuid %s stills %d"
                  % (data.get("label"), str(data.get("buildGuid"))[:12],
                     len(capt) if isinstance(capt, list) else -1))


if __name__ == "__main__":
    main()
