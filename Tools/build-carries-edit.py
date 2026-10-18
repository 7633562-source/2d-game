#!/usr/bin/env python3
"""Does the built player actually carry a source edit? - a probe the mtime test cannot do.

The compile gate (F:/hermes-data/scripts/grok-compile-gate.sh) decides "OK" by
comparing the newest `Assets/**/*.cs` mtime against
`Build/Headless/BalanceTrial_Data/Managed/Assembly-CSharp.dll`. That test answers
"was a source written AFTER the build?" and it cannot answer the other question:

    was a source written WHILE the build was already compiling?

Unity reads the sources once, at the start of its script-compile phase (~5 s in
this project), and the player DLL is written at the END of the run. So a .cs saved
between those two moments leaves the DLL NEWER than the source - the gate prints
"Build status: OK" and every trial then quotes a player that does not contain the
edit. Measured once, 20.09: `Assets/GalleryScene.cs` 05:35:36, compile finished
05:36:02, player DLL 05:36:17.

C# string literals are stored in the assembly's metadata heap as UTF-16LE, so a
line of text added by the edit is a direct probe of the compiled image. Give this
tool the file and a distinctive substring; it reports whether the assembly carries
it. Always pass a positive control (a string from an older, known-built edit) and
expect the negative control to be absent - a probe that cannot fail proves nothing.

    python Tools/build-carries-edit.py Assets/GalleryScene.cs "dresses its own petals"
    python Tools/build-carries-edit.py --dll                     # all controls

The probe text has to be something the COMPILER KEEPS. The tool now checks it against the
source first (20.09, programmer cycle: the walk cycle 6v edit is one expression plus
comments, its probe was taken from the header comment, and a player that HAD compiled the
edit was reported as not carrying it): a probe that lives only after `//` or inside
`/* */` can never be in an assembly, so the tool refuses it instead of printing a
verdict about the build. Symbol names work too - a field/method/type name the edit
declares does reach the metadata heap.

Exit 0 when the probe is present (and both controls behave), 1 when it is absent,
2 on usage, a missing file, or an unfalsifiable probe.
"""
import argparse
import os
import sys
import time

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PLAYER = os.path.join(PROJ, "Build/Headless/BalanceTrial_Data/Managed/Assembly-CSharp.dll")
LIBRARY = os.path.join(PROJ, "Library/ScriptAssemblies/Assembly-CSharp.dll")

# A string that has been in the build for many cycles (controls, not probes).
POS_CTRL = "ART-PARTS flowers dressed="
NEG_CTRL = "this string was never written by any lane"


def _strip_block_comments(text):
    """Blank out /* ... */ runs so a line scan can tell code from comment."""
    out = []
    i = 0
    while i < len(text):
        j = text.find("/*", i)
        if j < 0:
            out.append(text[i:])
            break
        out.append(text[i:j])
        k = text.find("*/", j + 2)
        if k < 0:
            out.append("\n" * text.count("\n", j))
            break
        out.append("\n" * text.count("\n", j, k + 2))
        i = k + 2
    return "".join(out)


def source_verdict(path, text):
    """Where does the probe text live in its source? (occurrences in code, in comments)

    A probe taken from a COMMENT cannot exist in any assembly - the compiler strips it
    before the metadata heap is written - so the verdict "DOES-NOT-CARRY" is then a
    statement about the probe, not about the build. Measured 20.09 (programmer cycle):
    the walk cycle 6v edit is one expression plus comments, its probe was taken from
    the header comment, and a player that HAD compiled that edit was reported as not
    carrying it until the probe's own provenance was checked.
    """
    with open(path, "r", encoding="utf-8-sig", errors="replace") as f:
        raw = f.read()
    in_code = _strip_block_comments(raw)
    code_hits = 0
    for line in in_code.splitlines():
        if "//" in line:
            line = line.split("//", 1)[0]
        code_hits += line.count(text)
    total = raw.count(text)
    comment_hits = max(0, total - code_hits)
    return total, code_hits, comment_hits


def looks_probeable(raw, text):
    """Can this text live in the metadata heap at all?

    Only literals and MEMBER names (fields, methods, types) are written there. A LOCAL
    variable name or an expression is not - measured 20.09: `hipHeightLiveM` is a local
    of TickWalk and every build reports it absent, which makes a DOES-NOT-CARRY verdict
    a statement about the probe. (`comXSwap`, a private field, probes true.)
    """
    import re
    t = re.escape(text)
    pats = (
        r'"' + t + r'"',                       # a string literal
        r"'" + t + r"'",                       # a char literal
        r"\b(?:class|struct|enum|interface)\s+" + t + r"\b",
        r"\b(?:private|public|protected|internal|static|readonly|const)\b[^\n;{}]*\b"
        + t + r"\b\s*[=;,()]",
    )
    return any(re.search(p, raw) for p in pats)


def mtime(path):
    return time.strftime("%H:%M:%S", time.localtime(os.path.getmtime(path)))


def carries(blob, text):
    """True when the literal is in the metadata heap as UTF-16LE (or plain UTF-8)."""
    return text.encode("utf-16-le") in blob or text.encode("utf-8") in blob


def probe(dll, text):
    with open(dll, "rb") as f:
        blob = f.read()
    return blob, carries(blob, text)


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("source", nargs="?", help="a source file the edit touched (for the mtime hint)")
    ap.add_argument("text", nargs="?", help="a distinctive literal the edit added or changed")
    ap.add_argument("--dll", action="store_true", help="run the two controls only")
    args = ap.parse_args()

    if not args.dll and not (args.source and args.text):
        ap.error("give a source file and a literal, or --dll")

    if not os.path.exists(PLAYER):
        print("MISSING %s - nothing to probe; build the player first" % PLAYER)
        return 2
    with open(PLAYER, "rb") as f:
        blob = f.read()

    print("player %s  %s  %d bytes" % (mtime(PLAYER), os.path.basename(PLAYER), len(blob)))
    if os.path.exists(LIBRARY):
        print("library %s  (the editor's compile-time witness)" % mtime(LIBRARY))

    pos = carries(blob, POS_CTRL)
    neg = carries(blob, NEG_CTRL)
    print("  control  positive %-5s %r" % (pos, POS_CTRL))
    print("  control  negative %-5s %r" % (neg, NEG_CTRL))
    if not pos or neg:
        print("PROBE-UNRELIABLE - the controls did not behave (a positive control must be "
              "present and the negative absent), so a 'carries' verdict from this run means nothing")
        return 2
    if args.dll:
        print("OK - both controls behave on this player")
        return 0

    if not os.path.exists(args.source):
        print("MISSING source %s" % args.source)
        return 2
    src = os.path.join(PROJ, args.source) if not os.path.isabs(args.source) else args.source
    print("source  %s  %s" % (mtime(src), args.source))
    if os.path.getmtime(src) > os.path.getmtime(PLAYER):
        print("  note: the source is NEWER than the player - the gate's own stale test covers "
              "this case; this probe is for the source saved INSIDE the build window")
    total, in_code, in_comment = source_verdict(src, args.text)
    if total == 0:
        print("PROBE-UNFALSIFIABLE - the text is not in %s at all, so 'absent from the player' "
              "would mean nothing. Check the spelling/quoting, then probe a literal or a symbol "
              "name the edit adds." % args.source)
        return 2
    if in_code == 0:
        print("  probe provenance: %d occurrence(s), ALL inside comments - the compiler strips "
              "them, so NO assembly can carry this text and a DOES-NOT-CARRY verdict would be "
              "about the probe, not the build." % in_comment)
        print("PROBE-STRIPPED - pick a string LITERAL the edit adds, or a symbol name it declares "
              "(field/method/type names do reach the metadata heap). If the edit is comments plus "
              "an expression only, this tool cannot probe it: verify the build by the gate's own "
              "chain instead - a real 'CompileScripts' line in that run's log, TRIAL_BUILD_OK in "
              "that same truncated log, no Assets/**/*.cs newer than the DLL, and no .cs written "
              "inside the build window.")
        return 2
    print("  probe source occurrences: %d in code, %d in comments" % (in_code, in_comment))
    with open(src, "r", encoding="utf-8-sig", errors="replace") as f:
        raw = f.read()
    if not looks_probeable(raw, args.text):
        print("PROBE-WEAK - the text occurs in code but is not a literal and not a member "
              "(field/method/type) declaration: local names and expressions never reach the "
              "metadata heap, so a DOES-NOT-CARRY here describes the probe, not the player. "
              "Probe a literal or a member name, or fall back to the gate's own chain (a real "
              "'CompileScripts' line in that run's log, TRIAL_BUILD_OK in that same truncated "
              "log, no Assets/**/*.cs newer than the DLL, no .cs written inside the build window).")
        return 2
    hit = carries(blob, args.text)
    print("  probe   %-5s %r" % (hit, args.text))
    if hit:
        print("CARRIES - the player holds this edit")
        return 0
    print("DOES-NOT-CARRY - the player does NOT hold this edit: rebuild before quoting any trial "
          "made on it (the mtime test may still read OK)")
    return 1


if __name__ == "__main__":
    sys.exit(main())
