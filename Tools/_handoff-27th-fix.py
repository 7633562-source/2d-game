#!/usr/bin/env python3
"""One-shot repair: move the ART-0 27th pass block from the middle of Trials/Runs/handoff.md
to its end, and restore the file's LF endings.

Why: the block was inserted by a patch anchored on a line that is inside the WALK CYCLE 6u
section (the section had a trailing line after it), so the entry landed mid-file and split
another lane's section; the patch also rewrote that region with CRLF.

    python Tools/_handoff-27th-fix.py
"""

import pathlib
import sys

HANDOFF = pathlib.Path(r"E:/Games/2D Game/My project/Trials/Runs/handoff.md")
MARK = "\n---\n\n## ART-0 27th pass"

BLOCK = """
---

## ART-0 27th pass — the human's hand reaches its own box (artist, 20.09)

DONE — the standing ART-0 brief's five items were re-verified on the tree as it stands (the loaders'
paths quoted from `LevelArt.PathsFor`/`LoadAt` and `ArtLibrary.Albedo`/`Tiled`, `bash
art0-import-gate.sh` -> `unity exit=0 failures=0` with both level tiles REPEATING, `art0-meta-audit.py`
`problems=0`), so this pass spent its one change where the checkers name a defect rather than
re-delivering 18.09's files.

CHANGE — `Assets/Resources/Art/Human/hand.png` only (a picture; `Human.handSize` 0.06 x 0.16 m, the
`.meta` ppu 3200 and the loader path untouched). Measured on the shipped bytes with the new
`Tools/art0-part-ink-probe.py`: the hand filled **33 %** of its own bone — the only part of the body
under half (torso 100 %, neck 99 %, thigh 81 %, shin 73 %) — and its ink was centred, so the wrist
cut sat **0.052 m below** the bone's proximal edge, i.e. the hand hung detached from the forearm that
ends at the wrist joint. `Tools/gen-human-hand-texture.py` rewrites that one canvas from the hand's
own accepted pixels (`LegacyArt/Human/hand.png`, 1024x1024): crop the ink, LANCZOS to 94 % of the box
WIDTH (the proportional maximum — the art is 0.70, the box 0.375), paste with the top row on the
canvas' top row. `31,165 119x171 px / 0.037 x 0.053 m / wrist margin 0.052 m / 31 832 B` ->
`6,0 180x261 px / 0.056 x 0.082 m / wrist margin 0.000 m / 63 865 B`. Nothing repainted.

EVIDENCE — `bash art-gallery-capture.sh art0hand27`: `PREVIEW_BUILD_OK scenes=12 errors=0`,
10 stills + 165 pan frames (16.500000 s), buildGuid `b39fe8de34ab482e8c35b7bff358e26d`, capture
failures 0, same lens (human `ortho 1,97`) and `dressedRenderers 6883` as `art0hip29`. The human still
differs by **369 px in ONE cluster** in the hand's own box (x 1067-1082 y 661-689; 285 px above 30
levels) while `fire/flower/flowerbox/level/materials/tree` are pixel-identical and `apple` (754 px) is
inside its own measured noise (the label-only control `art0hip29 -> art0hip30` moved `human
identical`, apple 532 px). Three blind pairwise reads, **three correct, seed 283 with the sides
swapped** ("пальцы чётко различимы, спрайт выше по вертикали" on the new side; the old side read as
"черновой прототип / размытое пятно"). Rule zero: the diff is ONE PNG (`git diff --stat --
Assets/Resources` = `Bin 31832 -> 63865`), `walk1_load4.json` md5 `3ad0a04a...` (fallTime 5.8650,
pelvisDrop 0.8723) and `dog_art1.json` md5 `4141622a...` (comTravelX 5.53443003, 35 cycles) unchanged,
mtimes still 09-18.

NOT DONE, NAMED — `art1-gate.sh` **REFUSED mid-pass: "a trial is in progress (trial.lock)"** (another
lane held the project; the lock was not touched), so the editor-side rule-zero assertions are PENDING
for these bytes and rule zero rests on the PNG-only diff, the records' md5s and the identical
placeholder catalogue. The assembled human STILL has no real-art frame anywhere: the `human rig` row
is ART-1b and lives in the walk stream's `HumanSegment.cs` — until it exists, the numbers above are
arithmetic on the box and the forearm's own 0.069 m of empty canvas at its distal end cannot be judged.
A procedural SIDE-VIEW hand was drawn twice and read to a stop both times ("кухонная лопатка /
варежка", then "гребень / грабли: все четыре пальца в одной плоскости"); the shipped art stays, and
its palm-view orientation on a side-view body is a separate open item. `ART-HAND` stays open on its
physics half (card 0.19 vs code 0.16 - the dispatcher's call).

FILES — `Assets/Resources/Art/Human/hand.png`, `Tools/gen-human-hand-texture.py` (new),
`Tools/art0-part-ink-probe.py` (new; `--file` mode), `Tools/_hand-box-crosscheck.py` (new),
`.cursor/tasks/ART-0-inventory.md` (gap 6), `.cursor/tasks/ART-HAND.md` (picture half).
Report `Trials/Runs/art-0-hand-27th-report.md` (Trials/Runs is gitignored - disk only).
"""


def main():
    raw = HANDOFF.read_bytes()
    text = raw.decode("utf-8").replace("\r\n", "\n")
    crlf = raw.count(b"\r\n")

    cut = text.find(MARK)
    if cut < 0:
        print("REFUSED: the ART-0 27th pass block is not in the middle of the file")
        return 1
    # drop the misplaced block: from MARK up to (and including) the report line's newline
    end = text.find("disk only).\n", cut)
    if end < 0:
        print("REFUSED: cannot find the end of the misplaced block")
        return 1
    text = text[:cut] + text[end + len("disk only).\n"):]

    if not text.endswith("\n"):
        text += "\n"
    text += BLOCK
    HANDOFF.write_text(text, encoding="utf-8", newline="\n")

    out = HANDOFF.read_text(encoding="utf-8")
    print("crlf before=%d after=%d" % (crlf, pathlib.Path(HANDOFF).read_bytes().count(b"\r\n")))
    print("lines=%d, MARK occurrences=%d" % (out.count("\n"), out.count(MARK)))
    print("--- around the repair ---")
    i = out.find("burn_walk` 05:43: fell 7.645 s, 2 swaps.")
    print(out[i - 40:i + 420])
    print("--- tail 6 lines ---")
    print("\n".join(out.rstrip("\n").split("\n")[-6:]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
