"""ARENA genome box vs the shipped walk band - a read-only contract audit.

WHY THIS EXISTS
    ArenaGenome.cs carries the sweep's search box (Min/Max) and a reference genome
    (Defaults). PoseDriveController.ReadSwitches carries the same 13 values as
    command-line surfaces, and ApplyArenaGenome writes the same 13 values from a
    genome. Its own header comment claims:

        "The clamps are the same ones ReadSwitches applies, so a genome can never
         leave the box the command-line switches accept."

    Nothing checked that claim. When the walk lane changed the shipped step band
    (stepMinMeters +0.02 -> -0.45) the genome box kept Min=0, so:

      * the shipped default left the box (a reference genome is no longer the
        walk the tree ships), and
      * ApplyArenaGenome's wall stayed at 0, so every negative sample - and the
        WIDE spec already proposes -0.9 .. -0.225 - lands on the box floor.
        Samples pile up on a bound and the gene reads as a constant, which is a
        false negative that no row in the log can show: the log stores the gene
        it sampled, not the value the body received.

WHAT IT CHECKS (static, no trial, no rebuild)
    A. every shipped default (PoseDriveController field initialiser) lies inside
       ArenaGenome's box for the same gene;
    B. for each gene, the clamp wrapper ApplyArenaGenome applies is the same as
       the one ReadSwitches applies (the comment's own promise);
    C. reports the deviation factor, so a box that is merely narrow is told
       apart from a box that cannot reach the shipped value at all;
    D. WALK CYCLE 53 - the BAND rule: (default - min) / (max - min) in
       [0.15, 0.85]. A default on an edge is a one-sided mutation (half the
       jitter clamps straight back) and no row of the log can show it. The
       Min[8] = -0.45 bug of cycles 6r..52 was exactly this, and the synthetic
       edge case in --selftest is that bug rebuilt from the live text.

Exit 0 = every gene inside the box and every clamp matched. Exit 1 = a break.
--selftest proves the checker can FAIL and can PASS (a checker that only ever
prints OK is not evidence).
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GENOME = os.path.join(ROOT, "Assets", "ArenaGenome.cs")
POSE = os.path.join(ROOT, "Assets", "PoseDriveController.cs")


def read(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        return f.read()


def float_list(text, start_marker):
    """Parse the first `float[] { ... }` block whose declaration contains start_marker."""
    i = text.index(start_marker)
    j = text.index("{", i)
    k = text.index("}", j)
    body = text[j + 1:k]
    # WALK CYCLE 64: a comment inside the array (the ask-side tier's own note) is
    # prose, not a value - strip it before tokenising, or the parser dies on the
    # first word. Same lesson as statements() below.
    body = re.sub(r"//[^\n]*", " ", body)
    body = re.sub(r"/\*.*?\*/", " ", body, flags=re.S)
    out = []
    for tok in body.replace("\n", " ").split(","):
        tok = tok.strip()
        if not tok:
            continue
        out.append(float(tok.rstrip("fF")))
    return out


def gene_names(text):
    i = text.index("public static readonly string[] Names")
    j = text.index("{", i)
    k = text.index("}", j)
    return re.findall(r'"([A-Za-z0-9_]+)"', text[j:k])


def pose_field_defaults(text):
    """private float <name> = <value>f; -> {name: value} (gene names only)."""
    out = {}
    for m in re.finditer(r"private\s+float\s+(\w+)\s*=\s*(-?\d+(?:\.\d+)?)f\s*;", text):
        out[m.group(1)] = float(m.group(2))
    return out


def statements(body):
    """Split a method body into statements on ';' - an assignment may wrap lines.

    A ';' inside a COMMENT or a string literal is not a separator. This file's
    own walk-lane comments carry semicolons (prose), and without the strip the
    split cuts a statement in half: the half carrying the assignment starts with
    comment prose, fails the `field = rhs` match, and the field silently reads as
    NEVER ASSIGNED - which is how a bare `stepMinMeters = g[8];` was reported as
    "ApplyArenaGenome never assigns it (gene order broken?)" the moment a
    semicolon appeared in the comment above it. Strip, then split.
    """
    body = re.sub(r"//[^\n]*", "", body)
    body = re.sub(r'"(\\.|[^"\\])*"', '""', body)
    out = []
    for stmt in body.split(";"):
        stmt = re.sub(r"\s+", " ", stmt).strip()
        if stmt:
            out.append(stmt)
    return out


def read_switches_clamps(text):
    """field -> clamp wrapper text applied around its value source in ReadSwitches."""
    i = text.index("private void ReadSwitches()")
    j = text.index("\n    }", i)
    out = {}
    for stmt in statements(text[i:j]):
        m = re.match(r"(\w+)\s*=\s*(.+)$", stmt)
        if not m:
            continue
        field, rhs = m.group(1), m.group(2)
        out[field] = clamp_wrapper(rhs, r"GetFloat\([^)]*\)")
    return out


def apply_genome_clamps(text):
    """index -> clamp wrapper around g[i] in ApplyArenaGenome."""
    i = text.index("public void ApplyArenaGenome")
    j = text.index("\n    }", i)
    out = {}
    for stmt in statements(text[i:j]):
        m = re.match(r"(\w+)\s*=\s*(.+)$", stmt)
        if not m:
            continue
        field, rhs = m.group(1), m.group(2)
        gm = re.search(r"g\[(\d+)\]", rhs)
        if not gm:
            continue
        out[field] = (int(gm.group(1)), clamp_wrapper(rhs, r"g\[\d+\]"))
    return out


def clamp_wrapper(expr, source_pattern):
    """The text around the value source, normalised: '' = raw value, else 'Name(...)'."""
    m = re.search(source_pattern, expr)
    if not m:
        return None
    before, after = expr[:m.start()], expr[m.end():]
    wrapper = (before + after).replace(",", ",")
    return re.sub(r"\s+", "", wrapper)


def clamp_reach(wrapper):
    """The numeric bounds a clamp wrapper can impose on the value it wraps.

    Returns (lo, hi) with None where the wrapper does not bound that side, so a
    mismatch can be told apart from a mismatch that BINDS the shipped value.
    """
    if not wrapper:
        return (None, None)
    lits = [float(x) for x in re.findall(r"(-?\d+(?:\.\d+)?)f", wrapper)]
    if "Mathf.Clamp(" in wrapper:
        if len(lits) >= 2:
            return (lits[-2], lits[-1])
        if len(lits) == 1:
            return (lits[0], None)
        return (None, None)
    if "Mathf.Max(" in wrapper and "Mathf.Min(" in wrapper:
        return (lits[0] if lits else None, lits[-1] if len(lits) > 1 else None)
    if "Mathf.Max(" in wrapper:
        return (lits[0] if lits else None, None)
    if "Mathf.Min(" in wrapper:
        return (None, lits[0] if lits else None)
    return (None, None)


def pose_key_defaults(text):
    """The drawn stance leg's knee per named pose, from PoseLibrary's own table.

    WALK CYCLE 64: the ask-side genes' law defaults must EQUAL the drawn keys the
    pose table carries (SwingFold stance knee 10, FootPlace 12, PlantLoad 10) - the
    gene's job is to move the pose the tree ships, so a drift between the two
    surfaces is the tier's own box-wall bug and no row of the log could show it.
    """
    out = {}
    for name in ("swingfold", "footplace", "plantload"):
        m = re.search(r'Make\("%s",\s*Leg\([^)]*\),\s*Leg\(\s*(-?\d+(?:\.\d+)?)f,\s*(-?\d+(?:\.\d+)?)f'
                      % name, text)
        if m:
            out[name] = float(m.group(2))
    return out


# WALK CYCLE 64: gene -> the drawn pose key its shipped default must equal (check E).
ASK_GENES = {
    "stanceFoldKneeDeg": "swingfold",
    "stancePlaceKneeDeg": "footplace",
    "stanceLoadKneeDeg": "plantload",
}


def audit(genome_text, pose_text, label):
    names = gene_names(genome_text)
    mn = float_list(genome_text, "public static readonly float[] Min")
    mx = float_list(genome_text, "public static readonly float[] Max")
    df = float_list(genome_text, "public static readonly float[] Defaults")
    defaults = pose_field_defaults(pose_text)
    rs = read_switches_clamps(pose_text)
    ag = apply_genome_clamps(pose_text)

    breaks, drifts = [], []
    print("--- %s ---" % label)
    for i, name in enumerate(names):
        shipped = defaults.get(name)
        if shipped is None:
            breaks.append("%s: no shipped field initialiser found" % name)
            continue
        lo, hi = mn[i], mx[i]
        inside = lo <= shipped <= hi
        note = ""
        if not inside:
            note = "  <== OUTSIDE the box [%.4f, %.4f] (box span %.4f)" % (lo, hi, hi - lo)
            breaks.append("%s: shipped %.4f is not inside the box [%.4f, %.4f]"
                          % (name, shipped, lo, hi))
        else:
            # WALK CYCLE 53 - THE BAND RULE. A shipped value ON an edge is a
            # one-sided mutation: half of the jitter clamps straight back to the
            # default and the log cannot tell that from an optimum. This is the
            # exact bug Min[8] = -0.45 carried (the shipped stepMinMeters is
            # -0.45). The rule: (default - min) / (max - min) in [0.15, 0.85].
            frac = (shipped - lo) / (hi - lo) if hi > lo else 0.0
            note = "  band %.3f" % frac
            if frac < 0.15 or frac > 0.85:
                note += "  <== ON THE EDGE: half its jitter clamps back"
                breaks.append("%s: shipped %.4f sits at %.3f of its range [%.4f, %.4f] "
                              "- outside the 0.15..0.85 band" % (name, shipped, frac, lo, hi))
        a = ag.get(name)
        b = rs.get(name)
        if a is None:
            breaks.append("%s: ApplyArenaGenome never assigns it (gene order broken?)" % name)
        else:
            idx, awrap = a
            if idx != i:
                breaks.append("%s: gene index %d in ApplyArenaGenome but %d in Names"
                              % (name, idx, i))
            if awrap != b:
                clo, chi = clamp_reach(awrap)
                binds = (clo is not None and shipped < clo) or (chi is not None and shipped > chi)
                what = ("genome clamps to [%s, %s], switches do not"
                        % ("-" if clo is None else "%.4f" % clo,
                           "-" if chi is None else "%.4f" % chi))
                note += "   clamp: %s" % what
                line = "%s: %s; shipped %.4f" % (name, what, shipped)
                if binds:
                    note += "  <== BINDS the shipped value"
                    breaks.append(line + " is inside the genome's own clamp")
                else:
                    drifts.append(line + " is still inside it - latent, no sample is moved")
        print("  %-32s shipped %-10.4f box [%.4f, %.4f]%s" % (name, shipped, lo, hi, note))
    # WALK CYCLE 64 (check E): the ask-side tier's law defaults ARE the drawn pose
    # keys (the pose table is the other surface of the same number).
    keys = pose_key_defaults(pose_text)
    for gene, pose in sorted(ASK_GENES.items()):
        shipped = defaults.get(gene)
        drawn = keys.get(pose)
        if shipped is None or drawn is None:
            breaks.append("%s: cannot read both surfaces (%s default %r, drawn %r)"
                          % (gene, pose, shipped, drawn))
        elif abs(shipped - drawn) > 1e-6:
            breaks.append("%s: law default %.4f does not equal the drawn %s stance knee %.4f"
                          % (gene, shipped, pose, drawn))
        else:
            print("  %-32s = the drawn %s stance knee %.4f (check E)" % (gene, pose, drawn))
    print("")
    for d in drifts:
        print("  DRIFT   " + d)
    if breaks:
        print("BROKEN - %s (%d break(s))" % (label, len(breaks)))
        for f in breaks:
            print("  GENOME  " + f)
        return 1, breaks, drifts
    print("OK - every shipped default is inside its box; %d latent clamp drift(s)" % len(drifts))
    return 0, breaks, drifts


def selftest():
    """The checker must PASS on the live tree and FAIL on each break it names.

    WALK CYCLE 6r fixed both breaks, so a proof that leaned on the live tree
    being broken would now print FAILED for the wrong reason. The breaks are
    therefore rebuilt synthetically, one at a time, from the live text - the
    exact two edits that were in the tree before the fix:

      * the box's Min[8] back to 0f (the shipped -0.45 leaves the box), and
      * ApplyArenaGenome's floor back around gene 8 (the clamp ReadSwitches
        does not have - it BINDS the shipped -0.45).

    The pre-6r arm (stepMin +0.02) is kept as a third case: the fixed box must
    still accept it, i.e. the fix widened the box instead of moving the wall.
    """
    genome_text = read(GENOME)
    pose_text = read(POSE)
    live, lb, _ld = audit(genome_text, pose_text, "live tree")
    box_text = genome_text.replace("0.05f, -0.65f, 0.20f", "0.05f, 0f, 0.20f")
    assert box_text != genome_text, "the box edit under test is not in the tree"
    box, bb, _bd = audit(box_text, pose_text, "synthetic box back to Min[8] = 0")
    floor_text = pose_text.replace("stepMinMeters = g[8];",
                                   "stepMinMeters = Mathf.Max(0f, g[8]);")
    assert floor_text != pose_text, "the apply-floor edit under test is not in the tree"
    floor, fb, _fd = audit(genome_text, floor_text, "synthetic apply floor at 0")
    # WALK CYCLE 53: the band rule's own case - the box exactly as it shipped until
    # this cycle, with the shipped -0.45 sitting ON Min[8]. Inside the box, so only
    # the band rule can catch it; if this case passes, the rule is not reading.
    edge_text = genome_text.replace("0.05f, -0.65f, 0.20f", "0.05f, -0.45f, 0.20f")
    assert edge_text != genome_text, "the edge-box edit under test is not in the tree"
    edge, eb, _ed = audit(edge_text, pose_text, "synthetic box with the shipped value ON the edge")
    checks = [
        ("live tree passes", live == 0 and not lb),
        ("box Min[8] = 0 is caught", box == 1 and any("stepMinMeters" in b for b in bb)),
        ("apply floor at 0 is caught", floor == 1 and any("stepMinMeters" in b for b in fb)),
        ("a shipped default ON the box edge is caught (band)",
         edge == 1 and any("stepMinMeters" in b for b in eb)),
    ]
    print("")
    for name, ok in checks:
        print("  %-46s %s" % (name, "True" if ok else "False"))
    good = all(ok for _, ok in checks)
    print("SELFTEST %s" % ("OK" if good else "FAILED"))
    return 0 if good else 1


if __name__ == "__main__":
    if "--selftest" in sys.argv:
        sys.exit(selftest())
    _code, _breaks, _drifts = audit(read(GENOME), read(POSE), "live tree")
    sys.exit(_code)
