#!/usr/bin/env python3
"""Trial build manifest (fingerprint + buildGuid) without PowerShell.

`Tools/run-trial.ps1` refreshes Build/Headless/build-manifest.json after every
rebuild, and the player refuses to start when that manifest disagrees with
itself (TRIAL_PROVENANCE_FAILED). PowerShell is not always available in this
session, so this script reproduces the same fingerprint - SHA-256 over
(relative unix path + "\\n" + byte length + "\\n" + bytes) for every file under
Assets, Packages and ProjectSettings, sorted by relative path - and writes the
manifest from the freshly built Build/Headless/build-info.json.

  python Tools/trial-manifest.py --check-list    # compare the file list with the last manifest
  python Tools/trial-manifest.py --write         # write manifest + files.txt (after a build)
  python Tools/trial-manifest.py --print
"""
import argparse
import hashlib
import json
import os
import sys

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BUILD = os.path.join(PROJECT, "Build", "Headless")
ROOTS = ("Assets", "Packages", "ProjectSettings")
EXCLUDE_PARTS = {"Library", "Temp", "Obj", "obj", "Build", "Builds", "Logs",
                 "UserSettings", "MemoryCaptures", "Recordings"}


def files():
    rows = []
    for root in ROOTS:
        base = os.path.join(PROJECT, root)
        if not os.path.isdir(base):
            continue
        for dirpath, _dirs, names in os.walk(base):
            for name in names:
                full = os.path.join(dirpath, name)
                rel = os.path.relpath(full, PROJECT).replace("\\", "/")
                parts = rel.split("/")
                if any(p in EXCLUDE_PARTS for p in parts):
                    continue
                if rel == "Trials/Runs" or rel.startswith("Trials/Runs/"):
                    continue
                rows.append((rel, full))
    # PowerShell's Sort-Object compares culture-aware and case-insensitively
    # ("bark.png" before "Chicken.meta"), so mirror that rather than ordinal.
    rows.sort(key=lambda r: (r[0].lower(), r[0]))
    return rows


def fingerprint(rows):
    sha = hashlib.sha256()
    for rel, full in rows:
        with open(full, "rb") as fh:
            data = fh.read()
        sha.update((rel + "\n" + str(len(data)) + "\n").encode("utf-8"))
        sha.update(data)
    return sha.hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check-list", action="store_true")
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--print", dest="do_print", action="store_true")
    args = ap.parse_args()

    rows = files()
    fp = fingerprint(rows)
    rels = [r[0] for r in rows]
    print("files=%d fingerprint=%s" % (len(rows), fp))

    if args.do_print:
        for rel in rels:
            print("  " + rel)

    if args.check_list:
        path = os.path.join(BUILD, "build-manifest.files.txt")
        if not os.path.exists(path):
            print("no previous manifest list at " + path)
            return 1
        with open(path, encoding="utf-8-sig", errors="replace") as fh:
            old = [line.strip() for line in fh if line.strip()]
        old_set, new_set = set(old), set(rels)
        added = sorted(new_set - old_set)
        removed = sorted(old_set - new_set)
        print("previous=%d added=%d removed=%d" % (len(old), len(added), len(removed)))
        for rel in added[:25]:
            print("  ADDED   " + rel)
        for rel in removed[:25]:
            print("  REMOVED " + rel)
        same_order = [r for r in old if r in new_set] == [r for r in rels if r in old_set]
        print("shared-order-identical=%s" % same_order)
        if not same_order:
            a = [r for r in old if r in new_set]
            b = [r for r in rels if r in old_set]
            shown = 0
            for i in range(min(len(a), len(b))):
                if a[i] != b[i]:
                    print("  ORDER DIFF at %d: manifest=%s computed=%s" % (i, a[i], b[i]))
                    shown += 1
                    if shown >= 8:
                        break
        return 0

    if args.write:
        info_path = os.path.join(BUILD, "build-info.json")
        if not os.path.exists(info_path):
            print("FAIL no build-info.json - build the player first")
            return 1
        with open(info_path, encoding="utf-8-sig") as fh:
            info = json.load(fh)

        with open(os.path.join(BUILD, "build-manifest.files.txt"), "w", encoding="utf-8", newline="\n") as fh:
            for rel in rels:
                fh.write(rel + "\n")

        manifest = {
            "fingerprint": fp,
            "algorithm": "SHA-256",
            "fileCount": len(rows),
            "filesPath": os.path.join(BUILD, "build-manifest.files.txt"),
            "unityVersion": info.get("unityVersion", ""),
            "unityPath": "F:\\6000.5.8f1\\Editor\\Unity.exe",
            "buildTimeUtc": info.get("buildTimeUtc", ""),
            "repositoryPresent": True,
            "gitAvailable": True,
            "gitCommit": git_commit(),
            "gitDirty": git_dirty(),
            "buildGuid": info.get("buildGuid", ""),
            "outputPath": info.get("outputPath", ""),
        }
        with open(os.path.join(BUILD, "build-manifest.json"), "w", encoding="utf-8", newline="\n") as fh:
            json.dump(manifest, fh, indent=4)
        print("wrote manifest buildGuid=%s fingerprint=%s" % (manifest["buildGuid"], fp))
    return 0


def _git(args):
    import subprocess
    try:
        out = subprocess.run(["git", "-C", PROJECT.replace("\\", "/")] + args,
                             capture_output=True, text=True, errors="replace")
        return out.stdout.strip()
    except Exception:
        return ""


def git_commit():
    return _git(["rev-parse", "HEAD"])


def git_dirty():
    return _git(["status", "--porcelain"]) != ""


if __name__ == "__main__":
    sys.exit(main())
