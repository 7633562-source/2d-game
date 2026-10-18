#!/usr/bin/env python3
"""Stage ONLY this cycle's hunks of a file another lane is also editing.

The worker lane shares one tree: `Assets/TrialRecorder.cs` already carries the
walk stream's uncommitted A2/A3/A4 columns. `git add <file>` would stage their
work under this cycle's message, so the patch is filtered by hunk instead:
hunks whose added lines mention one of the ARMS CYCLE 8 markers are kept, the
rest are dropped, and the filtered patch is applied to the INDEX only.

Usage:
    python Tools/stage-cycle8-hunks.py            # dry run: print the plan
    python Tools/stage-cycle8-hunks.py --apply    # stage the kept hunks
"""

import os
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FILE = "Assets/TrialRecorder.cs"
MARKERS = ("armWindow", "armPressWindow", "Matched press window", "matched press window")


def git(*args, **kw):
    return subprocess.run(("git",) + args, cwd=REPO, capture_output=True,
                          text=True, **kw)


def split_patch(text):
    """(header, [hunk, ...]) - the diff header plus every @@ block."""
    lines = text.splitlines(keepends=True)
    header, hunks, cur = [], [], None
    for line in lines:
        if line.startswith("@@"):
            if cur is not None:
                hunks.append(cur)
            cur = [line]
        elif cur is None:
            header.append(line)
        else:
            cur.append(line)
    if cur is not None:
        hunks.append(cur)
    return header, hunks


def main():
    apply_it = "--apply" in sys.argv
    diff = git("diff", "--", FILE).stdout
    if not diff.strip():
        print("%s: no working-tree diff" % FILE)
        return
    header, hunks = split_patch(diff)
    keep, drop = [], []
    for hunk in hunks:
        body = "".join(l for l in hunk if l.startswith("+"))
        (keep if any(m in body for m in MARKERS) else
         (drop if body.strip() else drop)).append(hunk)
    print("%s: %d hunks, %d kept, %d dropped" % (FILE, len(hunks), len(keep), len(drop)))
    for hunk in keep:
        adds = [l.strip() for l in hunk if l.startswith("+")]
        print("  KEEP %s (+%d) first: %s" % (hunk[0].strip(), len(adds), adds[0][:70]))
    for hunk in drop:
        adds = [l.strip() for l in hunk if l.startswith("+")]
        print("  drop %s (+%d) first: %s" % (hunk[0].strip(), len(adds),
                                             adds[0][:70] if adds else "-"))
    if not apply_it:
        print("dry run; re-run with --apply to stage the kept hunks")
        return
    patch = "".join(header) + "".join("".join(h) for h in keep)
    path = os.path.join(REPO, "Tools", "_arm_cycle8_stage.patch")
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(patch)
    res = git("apply", "--cached", "--recount", path)
    print("git apply --cached exit=%d %s" % (res.returncode, res.stderr.strip()))
    print(git("diff", "--cached", "--stat").stdout.strip())


if __name__ == "__main__":
    main()
