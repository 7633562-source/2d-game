#!/usr/bin/env python
"""Does the player on disk depend on a file git does not have?

Why this exists: the trial player is built from the WORKING TREE, while every
record names its build by ``gitCommit`` and by a fingerprint over the files on
disk. Those two agree only if the tree git knows about IS the tree Unity
compiled. On 19.09 23:45 that was false: the manifest of the player built at
23:33:21 lists ``Assets/BodySupportGeometry.cs``, the committed
``Assets/ImpactRecorder.cs`` names that type nine times, and the file itself was
never added to git - so a fresh clone, ``git checkout`` or ``git clean -fd``
leaves a tree that cannot compile, and the accepted state survives only in one
working copy. Missing ``.meta`` files are the same defect one step milder: git
does not carry the GUID, so a fresh checkout makes Unity mint a NEW one and every
scene/prefab reference to that asset points at nothing.

Read-only: it reads the manifest file list and asks git what it tracks. It edits
nothing, stages nothing and never rebuilds.

Usage:
    python Tools/tracked-source-audit.py                # the manifest on disk
    python Tools/tracked-source-audit.py --manifest X   # another file list
Exit 0 = every source the player uses is in git and every source in git was
compiled. Exit 1 = at least one .cs source is missing from git.
"""
import argparse
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_MANIFEST = os.path.join(ROOT, "Build", "Headless", "build-manifest.files.txt")


def git(*args):
    out = subprocess.run(("git",) + args, cwd=ROOT, stdout=subprocess.PIPE,
                         stderr=subprocess.DEVNULL)
    return out.stdout.decode("utf-8", "replace")


def tracked_assets():
    """Every path git has under Assets/."""
    return {p for p in git("ls-files", "-z", "Assets").split("\0") if p}


def manifest_assets(path):
    """'Assets/...' entries of a build-manifest file list, normalised to '/'."""
    with open(path, "r", encoding="utf-8-sig", errors="replace") as fh:
        text = fh.read()
    out = set()
    for line in text.replace("\r\n", "\n").split("\n"):
        line = line.strip().strip('"')
        if line.startswith("Assets/") or line.startswith("Assets\\"):
            out.add(line.replace("\\", "/"))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--manifest", default=DEFAULT_MANIFEST)
    args = ap.parse_args()

    if not os.path.isfile(args.manifest):
        print("TRACKED-SOURCE-AUDIT BROKEN no manifest at %s" % args.manifest)
        return 1

    manifest = manifest_assets(args.manifest)
    tracked = tracked_assets()
    if not tracked:
        print("TRACKED-SOURCE-AUDIT BROKEN git tracks nothing under Assets/ - wrong repo?")
        return 1

    # 1. what the built player used but git does not carry
    missing = sorted(manifest - tracked)
    missing_cs = [p for p in missing if p.endswith(".cs")]
    missing_meta = [p for p in missing if p.endswith(".meta")]
    missing_other = [p for p in missing if not p.endswith(".cs") and not p.endswith(".meta")]

    # 2. what git carries but that player never compiled (the player predates it)
    uncompiled = sorted(p for p in tracked - manifest if p.endswith(".cs"))

    for p in missing_cs:
        print("UNTRACKED SOURCE  %s" % p)
    for p in missing_meta:
        print("untracked .meta   %s" % p)
    for p in missing_other:
        print("untracked asset   %s" % p)
    for p in uncompiled:
        print("NOT IN THE PLAYER %s" % p)

    print("manifest %d Assets entries, git tracks %d, player/git disagreement %d"
          % (len(manifest), len(tracked), len(missing) + len(uncompiled)))

    if missing_cs:
        print("BROKEN - %d C# source(s) the committed code and the built player need are "
              "only in this working tree; `git clean -fd`/a fresh clone does not compile"
              % len(missing_cs))
        return 1
    if uncompiled:
        print("BROKEN - %d source(s) in git were not in the player that made the records"
              % len(uncompiled))
        return 1
    print("TRACKED-SOURCE-AUDIT OK missing_cs=0 uncompiled=0 untracked_meta=%d"
          % len(missing_meta))
    return 0


if __name__ == "__main__":
    sys.exit(main())
