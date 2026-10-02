#!/usr/bin/env python3
"""What moved in the tree after the player was built?

Why this exists: the manifest fingerprint of the tree on disk (``dfdf5920``) and
the fingerprint of the last build (``917a1c8b``, 23:33) disagree, so no stored
run names the tree as it is now. The manifest stores file NAMES only, not
hashes, so the drift has to be located by write time: a file whose bytes changed
must have been written after the build.

Read-only. It answers three things:
  1. every file under the manifest roots (Assets, Packages, ProjectSettings)
     written after ``build-manifest.json`` was written;
  2. whether any of them is COMPILED (.cs) - a `.cs` newer than the player's
     ``Assembly-CSharp.dll`` means the player is stale and nothing may be
     measured on it (the BUILD-ID rule);
  3. whether the set is art-only (PNG/meta under Resources) - then the physics is
     untouched and a run on the player still speaks for the compiled code.

Usage:  python Tools/impact-drift-audit.py
"""
import json
import os
import time

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOTS = ("Assets", "Packages", "ProjectSettings")
BUILD = os.path.join(PROJECT, "Build", "Headless")
MANIFEST = os.path.join(BUILD, "build-manifest.json")
DLL = os.path.join(BUILD, "BalanceTrial_Data", "Managed", "Assembly-CSharp.dll")


def is_editor_script(rel):
    """Assets/**/Editor/**/*.cs compiles into Assembly-CSharp-Editor.dll, which is not
    shipped inside the player: such a file can be newer than the build without making the
    player stale (the 20.09 02:37 write of Assets/Editor/Art1Check.cs is exactly that)."""
    parts = rel.replace("\\", "/").lower().split("/")
    return "editor" in parts[:-1]


def newest(root, exts, skip_editor=False):
    best = (0.0, "")
    for dirpath, _dirs, names in os.walk(os.path.join(PROJECT, root)):
        for name in names:
            if exts and not name.lower().endswith(exts):
                continue
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, PROJECT).replace("\\", "/")
            if skip_editor and is_editor_script(rel):
                continue
            t = os.stat(full).st_mtime
            if t > best[0]:
                best = (t, rel)
    return best


def main():
    with open(MANIFEST, encoding="utf-8-sig") as fh:
        man = json.load(fh)
    build_t = os.stat(MANIFEST).st_mtime
    dll_t = os.stat(DLL).st_mtime

    print("manifest   buildTimeUtc=%s  fingerprint=%s  buildGuid=%s"
          % (man["buildTimeUtc"], man["fingerprint"][:16], man["buildGuid"][:8]))
    print("manifest   written  %s" % time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(build_t)))
    print("player dll written  %s" % time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(dll_t)))

    cs_t, cs_rel = newest("Assets", (".cs",), skip_editor=True)
    ed_t, ed_rel = newest("Assets", (".cs",))
    editor_only = ed_t > cs_t and is_editor_script(ed_rel)
    print("newest .cs runtime  %s  %s"
          % (time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(cs_t)), cs_rel))
    if editor_only:
        print("newest .cs EDITOR   %s  %s  (Assembly-CSharp-Editor.dll - not shipped)"
              % (time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(ed_t)), ed_rel))
    print("VERDICT dll_vs_sources: %s" % ("PLAYER IS NEWER THAN EVERY RUNTIME .cs (compiled code current)"
                                          if dll_t > cs_t else
                                          "STALE PLAYER: a runtime .cs is newer than the dll"))

    rows = []
    for root in ROOTS:
        for dirpath, _dirs, names in os.walk(os.path.join(PROJECT, root)):
            for name in names:
                full = os.path.join(dirpath, name)
                t = os.stat(full).st_mtime
                if t > build_t:
                    rows.append((t, os.path.relpath(full, PROJECT).replace("\\", "/"),
                                 os.path.getsize(full)))
    rows.sort()
    print("\nfiles under %s written after the manifest: %d" % ("/".join(ROOTS), len(rows)))
    kinds = {}
    for t, rel, size in rows:
        ext = os.path.splitext(rel)[1].lower()
        kinds[ext] = kinds.get(ext, 0) + 1
        print("  %s  %8d  %s" % (time.strftime("%H:%M:%S", time.localtime(t)), size, rel))
    print("\nextensions: %s" % ", ".join("%s=%d" % kv for kv in sorted(kinds.items())))
    compiled = [rel for _t, rel, _s in rows if rel.lower().endswith(".cs") and not is_editor_script(rel)]
    editor_moved = [rel for _t, rel, _s in rows if rel.lower().endswith(".cs") and is_editor_script(rel)]
    if editor_moved:
        print("note: Editor-only script(s) moved after the build (not in the player): %s"
              % ", ".join(editor_moved))
    print("VERDICT drift_is_art_only: %s"
          % ("YES - no compiled file moved after the build, so the physics this player runs "
             "is the physics in the tree" if not compiled else
             "NO - compiled files moved after the build: %s" % ", ".join(compiled)))
    return 1 if compiled else 0


if __name__ == "__main__":
    raise SystemExit(main())
