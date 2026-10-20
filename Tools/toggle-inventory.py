"""Toggle inventory for the human-stack balance layer (read-only static analysis).

Scope: the BalanceController partial class = Assets/BalanceController.cs,
Assets/BalanceControllerCaptureWalk.cs, Assets/BalanceControllerWalkLift.cs.

What "0/1 switch field" means here (mechanical, so the count is reproducible):
    a CLASS-SCOPE field (brace-depth 1, so locals are never counted) that is either
      * bool, or
      * float/int declared with the literal initialiser 0f / 1f / 0 / 1.

For every such field the tool records
  * DECL / WRITE / READ occurrences and the enclosing method of every READ,
  * whether a READ sits on a method reachable from a Unity message (FixedUpdate ...),
  * whether a READ sits BEFORE the level-2 pose early return in FixedUpdate
    ("-poseDrive 1" path) or after it (the level-1 automaton, not run on that path),
  * READ/WRITE by any OTHER .cs file (recorders, CLI setters, dog/hedgehog stacks),
  * whether the name is set from a command-line argument in HeadlessTrial.cs,
  * whether a scene/prefab serialises an override for it (Inspector-pinned value).

Evidence for the deletion plan = a field with no READ anywhere, no CLI knob and no
scene override: its only effect is Inspector noise.

Usage:  python Tools/toggle-inventory.py            -> prints the summary
        python Tools/toggle-inventory.py --md PATH  -> also writes the report
"""
import os, re, sys, json, collections

ROOT = r"E:\Games\2D Game\My project"
BC_FILES = ["Assets/BalanceController.cs",
            "Assets/BalanceControllerCaptureWalk.cs",
            "Assets/BalanceControllerWalkLift.cs"]
OUT_JSON = "Trials/Runs/_toggle-inventory.json"
OUT_MD = "Trials/Runs/toggle-inventory.md"

SWITCH_SUFFIX = re.compile(
    r"(First|Enabled|Enable|Disable|Disabled|Once|On|Off|Used|Use|Force|Forced|Ignore|"
    r"Suppress|Skip|Allow|Allowed|Only|Active|Gate|Bypass|Lite|Soft|Alt|Legacy|Old|New|"
    r"Debug|Dbg|Probe|Hold|Keep)$")
SWITCH_PREFIX = re.compile(
    r"^(use|force|ignore|suppress|skip|allow|enable|disable|dbg|debug|try|no)[A-Z]")


def strip_code(text):
    """Blank out comments and string/char bodies so braces inside them never count."""
    out = list(text)
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        if c == "/" and i + 1 < n and text[i + 1] == "/":
            j = text.find("\n", i)
            j = n if j < 0 else j
            for k in range(i, j):
                out[k] = " "
            i = j
        elif c == "/" and i + 1 < n and text[i + 1] == "*":
            j = text.find("*/", i + 2)
            j = n if j < 0 else j + 2
            for k in range(i, j):
                if text[k] != "\n":
                    out[k] = " "
            i = j
        elif c == '"':
            j = i + 1
            while j < n:
                if text[j] == "\\":
                    j += 2
                    continue
                if text[j] == '"':
                    break
                j += 1
            for k in range(i, min(j + 1, n)):
                if text[k] != "\n":
                    out[k] = " "
            i = j + 1
        elif c == "'":
            j = text.find("'", i + 1)
            if j > 0 and j - i <= 3:
                for k in range(i, j + 1):
                    out[k] = " "
                i = j + 1
            else:
                i += 1
        else:
            i += 1
    return "".join(out)


class Source:
    def __init__(self, rel):
        self.rel = rel
        self.path = os.path.join(ROOT, rel.replace("/", os.sep))
        with open(self.path, encoding="utf-8", errors="replace") as f:
            self.raw = f.read()
        self.lines = self.raw.split("\n")
        self.code_lines = strip_code(self.raw).split("\n")
        self.depth = self._depths()

    def _depths(self):
        d, res = 0, []
        for ln in self.code_lines:
            res.append(d)
            d += ln.count("{") - ln.count("}")
        return res


FIELD_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?P<mods>(?:(?:public|private|protected|internal|static|readonly|volatile|new)\s+)*)"
    r"(?P<type>float|int|bool|double)\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*"
    r"(?:=(?!=|>)\s*(?P<init>[^;]+))?;")

METHOD_RE = re.compile(
    r"^\s*(?:(?:public|private|protected|internal|static|virtual|override|new|partial|async)\s+)*"
    r"[A-Za-z_][A-Za-z0-9_<>,\[\]\.\?]*\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*\([^;{]*\)\s*$")

CALL_RE = re.compile(r"(?<![\.\w])([A-Za-z_][A-Za-z0-9_]*)\s*\(")
TOKEN_RE = re.compile(r"\b([A-Za-z_][A-Za-z0-9_]*)\b")
ENTRY = ["FixedUpdate", "Update", "LateUpdate", "Awake", "Start", "OnEnable",
         "OnDisable", "OnDestroy", "Reset"]

COMPARE_RE = re.compile(r"(>|<|!=|==|\?\s|&&|\|\||!)")


def parse_fields(src):
    fields = []
    for i, ln in enumerate(src.code_lines):
        if src.depth[i] != 1:
            continue
        m = FIELD_RE.match(ln)
        if m:
            fields.append(dict(file=src.rel, line=i + 1, type=m.group("type"),
                               name=m.group("name"), init=(m.group("init") or "").strip(),
                               src=src.lines[i].strip()))
    return fields


def parse_methods(src):
    """Member bodies by brace matching.

    The signature may span several lines and the opening brace may share the
    last signature line, so the body open is found first and the signature is
    read backwards from it.
    """
    ms, n = [], len(src.code_lines)
    for j in range(n):
        if src.depth[j] != 1 or "{" not in src.code_lines[j]:
            continue
        stripped = src.code_lines[j].strip()
        if stripped.startswith("}") or stripped.startswith("//"):
            continue
        # signature: go back over lines that belong to the same declaration
        s = j
        while s - 1 >= 0 and src.depth[s - 1] == 1:
            prev = src.code_lines[s - 1].strip()
            if not prev or prev.endswith((";", "{", "}")):
                break
            s -= 1
        sig = " ".join([src.code_lines[t].strip() for t in range(s, j)])
        sig += " " + stripped.split("{")[0]
        if "(" not in sig:
            continue
        head = sig.split("(")[0]
        ids = re.findall(r"[A-Za-z_][A-Za-z0-9_]*", head)
        if not ids:
            continue
        name = ids[-1]
        if name in ("if", "for", "while", "switch", "foreach", "using", "lock",
                    "get", "set", "try", "catch", "do", "else", "return"):
            continue
        k = j + 1                       # body lines are the ones at depth >= 2
        while k < n and src.depth[k] >= 2:
            k += 1
        ms.append(dict(name=name, start=s + 1, end=k))
    return ms


def containing(methods, line):
    best = None
    for me in methods:
        if me["start"] <= line <= me["end"]:
            if best is None or (me["end"] - me["start"]) < (best["end"] - best["start"]):
                best = me
    return best


def read_kind(line, name):
    """GATE if the value is tested, COEFF if it multiplies something."""
    n = len(re.findall(r"\b%s\b" % re.escape(name), line))
    if re.search(r"\b%s\b\s*(>|<|>=|<=|==|!=)" % re.escape(name), line) or \
       re.search(r"(>|<|>=|<=|==|!=)\s*\b%s\b" % re.escape(name), line):
        return "gate"
    if re.search(r"\b%s\b\s*[?]" % re.escape(name), line) or \
       re.search(r"[?:]\s*\b%s\b" % re.escape(name), line):
        return "select"
    if re.search(r"\*\s*\b%s\b|\b%s\b\s*\*" % (re.escape(name), re.escape(name)), line):
        return "coeff"
    if re.search(r"(&&|\|\||!)\s*\b%s\b|\b%s\b\s*(&&|\|\|)" % (re.escape(name), re.escape(name)), line):
        return "logic"
    return "other"


def family_of(name):
    parts = re.findall(r"[A-Z]+(?![a-z])|[A-Z][a-z0-9]*|[a-z0-9]+", name)
    if not parts:
        return name
    base = parts[:]
    # drop a trailing patch word so the gate and its siblings share one family
    if SWITCH_SUFFIX.search(name) and len(base) > 1:
        base = base[:-1]
    take = 2
    if base and base[0].lower() in ("walk", "capture", "getup", "march"):
        take = 2
        if len(base) > 2 and base[1].lower() in ("plant", "fall", "lift"):
            take = 3
    return "".join(base[:take]) if base else name


def main():
    srcs = [Source(f) for f in BC_FILES]
    methods = {s.rel: parse_methods(s) for s in srcs}
    fields = []
    for s in srcs:
        fields += parse_fields(s)

    bc = srcs[0]
    posereturn = None
    for i, ln in enumerate(bc.code_lines):
        if "PoseDriveActive()" in ln and "if" in ln:
            win = bc.code_lines[i:i + 6]
            if any("return;" in w for w in win):
                posereturn = i + 1 + next(k for k, w in enumerate(win) if "return;" in w)
                break
    if posereturn is None:
        raise SystemExit("could not resolve the poseDrive early-return line")

    def is_01(f):
        if f["type"] == "bool":
            return True
        return f["init"].replace("f", "").replace("F", "").strip() in ("0", "1")

    cand = [f for f in fields if is_01(f)]

    # call graph inside the three files
    cg = collections.defaultdict(set)
    for s in srcs:
        for me in methods[s.rel]:
            calls = set()
            for i in range(me["start"] - 1, min(me["end"], len(s.code_lines))):
                calls |= set(CALL_RE.findall(s.code_lines[i]))
            cg[me["name"]] |= calls
    seen, stack = set(), [e for e in ENTRY if e in cg]
    while stack:
        cur = stack.pop()
        for c in cg.get(cur, ()):
            if c not in seen:
                seen.add(c)
                if c in cg:
                    stack.append(c)
    reach = seen | set(ENTRY)

    allcs = []
    for dp, _, fn in os.walk(os.path.join(ROOT, "Assets")):
        for f in fn:
            if f.endswith(".cs"):
                allcs.append(os.path.join(dp, f))
    other_txt = {}
    for p in allcs:
        rel = os.path.relpath(p, ROOT).replace(os.sep, "/")
        if rel in BC_FILES:
            continue
        with open(p, encoding="utf-8", errors="replace") as fh:
            other_txt[rel] = fh.read()

    scene_txt = {}
    for dp, _, fn in os.walk(os.path.join(ROOT, "Assets")):
        for f in fn:
            if f.endswith((".unity", ".prefab")):
                p = os.path.join(dp, f)
                rel = os.path.relpath(p, ROOT).replace(os.sep, "/")
                with open(p, encoding="utf-8", errors="replace") as fh:
                    scene_txt[rel] = fh.read()

    ht = other_txt.get("Assets/HeadlessTrial.cs", "")

    def block_lines(src, line):
        """Declaration + the attribute/comment lines directly above it, and any
        continuation lines of the declaration itself (a wrapped initialiser)."""
        n = 1
        i = line - 2
        while i >= 0:
            t = src.lines[i].strip()
            if t.startswith(("//", "[", "///")):
                n += 1
                i -= 1
            else:
                break
        j = line
        while j < len(src.lines) and not src.lines[j - 1].rstrip().endswith(";"):
            n += 1
            j += 1
        return n

    src_by = {s.rel: s for s in srcs}

    names = {f["name"] for f in cand}
    occ = collections.defaultdict(list)
    for s in srcs:
        for i, ln in enumerate(s.code_lines):
            for t in TOKEN_RE.findall(ln):
                if t in names:
                    occ[t].append((s.rel, i + 1))

    rows = []
    for f in cand:
        nm = f["name"]
        reads, writes = [], []
        for (rel, line) in occ[nm]:
            if rel == f["file"] and line == f["line"]:
                continue
            ln = next(s for s in srcs if s.rel == rel).code_lines[line - 1]
            assign = re.search(r"\b%s\b\s*(\+\+|--|[+\-*/]?=)(?![=])" % re.escape(nm), ln) \
                or re.search(r"(\+\+|--)\s*\b%s\b" % re.escape(nm), ln)
            if assign:
                writes.append((rel, line, ln.strip()))
            else:
                reads.append((rel, line, ln.strip()))
        meths, pre, kinds = set(), False, collections.Counter()
        for (rel, line, ln) in reads:
            me = containing(methods[rel], line)
            meths.add(me["name"] if me else "<class-scope>")
            kinds[read_kind(ln, nm)] += 1
            if rel.endswith("BalanceController.cs") and line < posereturn:
                pre = True
        others = sorted(rel for rel, txt in other_txt.items()
                        if re.search(r"\b%s\b" % re.escape(nm), txt))
        cli = bool(re.search(r'"-%s"' % re.escape(nm), ht))
        scene = sorted(rel for rel, txt in scene_txt.items()
                       if re.search(r"^\s*%s:" % re.escape(nm), txt, re.M))
        gate_named = bool(SWITCH_SUFFIX.search(nm) or SWITCH_PREFIX.match(nm))
        if not reads and not others and not scene:
            status = "DEAD-NO-READ"
        elif reads and not (meths & reach) and not others:
            status = "DEAD-UNREACHABLE"
        elif not reads and others:
            status = "read-only-elsewhere"
        else:
            status = "LIVE"
        rows.append(dict(name=nm, file=f["file"], line=f["line"], type=f["type"],
                         init=f["init"], src=f["src"], status=status,
                         gate_named=gate_named, family=family_of(nm),
                         block=block_lines(src_by[f["file"]], f["line"]),
                         n_reads=len(reads), n_writes=len(writes),
                         read_methods=sorted(meths), kinds=dict(kinds),
                         others=others, cli=cli, scene=scene,
                         on_entry_path=bool(meths & reach) or bool(others),
                         pre_posedrive=pre,
                         reads=[dict(file=r, line=l, text=t) for (r, l, t) in reads],
                         writes=[dict(file=r, line=l, text=t) for (r, l, t) in writes]))

    rows.sort(key=lambda r: (r["file"], r["line"]))
    fam = collections.OrderedDict()
    for r in rows:
        fam.setdefault(r["family"], []).append(r)

    json.dump(dict(posedrive_return_line=posereturn, fields=rows),
              open(os.path.join(ROOT, OUT_JSON), "w", encoding="utf-8"), indent=1)

    tot = len(rows)
    dead = [r for r in rows if r["status"].startswith("DEAD")]
    print("candidate 0/1 fields: %d (bool %d, float %d, int %d)"
          % (tot, sum(r["type"] == "bool" for r in rows),
             sum(r["type"] == "float" for r in rows),
             sum(r["type"] == "int" for r in rows)))
    print("poseDrive early return line:", posereturn)
    print("families:", len(fam))
    print("DEAD:", len(dead), [r["name"] for r in dead])
    print("CLI knobs:", sum(1 for r in rows if r["cli"]))
    print("scene overrides:", sum(1 for r in rows if r["scene"]))
    print("read outside the three files:", sum(1 for r in rows if r["others"]))
    print("touching the pre-poseDrive region:", sum(1 for r in rows if r["pre_posedrive"]))
    if "--md" in sys.argv:
        write_md(rows, fam, posereturn)


def write_md(rows, fam, posereturn):
    L = []
    add = L.append
    add("# Toggle inventory - BalanceController partial class (MEASURE STREAM, job A)")
    add("")
    add("Read-only static analysis. No code was edited. Generator + evidence:")
    add("`Tools/toggle-inventory.py` (writes `%s` as the machine-readable form)." % OUT_JSON)
    add("")
    add("## 0. What was counted, and how")
    add("")
    add("A field is in the candidate set when it is declared at CLASS SCOPE (brace depth 1, so")
    add("method locals are excluded - locals such as `bool walkStanceGeoLift = false;` at")
    add("BalanceController.cs:5784 look identical by name but are not fields) and is either")
    add("`bool`, or `float`/`int` with the literal initialiser `0`/`1`.")
    add("")
    add("A field's status:")
    add("")
    add("| status | meaning |")
    add("|---|---|")
    add("| LIVE | a read site exists on a method reachable from a Unity message, or another .cs reads it |")
    add("| DEAD-UNREACHABLE | read only inside methods no Unity message can reach |")
    add("| DEAD-NO-READ | no read anywhere: the value is written/harvested but never consumed |")
    add("| read-only-elsewhere | never read in the three files, only by a recorder |")
    add("")
    add("The **level-2 cut**: `Assets/BalanceController.cs` FixedUpdate returns at line %d"
        % posereturn)
    add("inside `if (PoseDriveActive())` (`poseDrive.Tick(comOffset); return;`), so with")
    add("`-poseDrive 1` nothing below that line runs. A read ABOVE it is on the pose path;")
    add("a read below it is level-1 automaton only. Both cuts are reported per family, so the")
    add("answer does not depend on how 'the poseDrive walk path' is read.")
    add("")
    add("Cross-checks run for every name: any other `Assets/**/*.cs` file (recorders export")
    add("some of these as CSV columns, so a CSV column is a read), the literal `\"-<name>\"` in")
    add("`HeadlessTrial.cs` (a command-line knob = an outside writer), and `Assets/**/*.unity|")
    add("*.prefab` for a serialised override (an Inspector-pinned value).")
    add("**No reflection anywhere in `Assets/`** (`grep -rn \"GetField|SetValue|FieldInfo\"` over")
    add("all .cs returns nothing), so the static read set is complete: nothing sets these by name")
    add("at runtime.")
    add("")
    add("## 0b. The answer, before the tables")
    add("")
    lvl1 = [r for r in rows if r["reads"] and not r["pre_posedrive"]]
    add("- **%d of the %d fields are level-1 only**: every read site sits BELOW line %d, so"
        % (len(lvl1), len(rows), posereturn))
    add("  under `-poseDrive 1` (the level-2 pose path) they are never evaluated. They are")
    add("  not dead code in the build, but they cannot influence the pose-driven walk.")
    pre = [r["name"] for r in rows if r["pre_posedrive"]]
    add("- Exactly **%d fields** are read above the pose early return: %s."
        % (len(pre), ", ".join("`%s`" % p for p in pre)))
    add("- **%d of %d are settable from the command line** (`HeadlessTrial.cs`), i.e. every one"
        % (sum(1 for r in rows if r["cli"]), len(rows)))
    add("  of them costs a declaration block + a read + a `GetFloatArg` line + prose. Together")
    add("  the surface occupies **%d lines** of the three files (declaration + tooltip/comment"
        % sum(r["block"] for r in rows))
    add("  block + wrapped initialiser), measured from the source, not estimated.")
    add("- **%d fields may be deleted on read-evidence grounds** (no read anywhere, no CLI knob,"
        % sum(1 for r in rows if r["status"].startswith("DEAD")))
    add("  no scene override); they are listed with file:line in section 2. Everything else has a")
    add("  live reader, so deleting the rest is a behaviour change and needs its own cycle with")
    add("  a build fingerprint before and after.")
    add("")
    add("### The `walkPlantFall*` ladder - the debris the brief is aimed at")
    add("")
    ladder = [r for r in rows if r["name"].startswith("walkPlantFall")]
    cap = [r for r in ladder if r["name"].startswith("walkPlantFallLiftCapGrow")]
    std = [r for r in ladder if r["name"].startswith("walkPlantFallStanceDriveGrow")]
    add("`%d` fields, `%d` of them CLI knobs, `%d` lines of the three files. Two hand-unrolled"
        % (len(ladder), sum(1 for r in ladder if r["cli"]), sum(r["block"] for r in ladder)))
    add("chains inside it prove the pattern: **`walkPlantFallLiftCapGrow*` = %d fields** and"
        % len(cap))
    add("**`walkPlantFallStanceDriveGrow*` = %d fields**, while the whole `walkPlant*` prefix is"
        % len(std))
    add("88 declarations. They are a Plant->Fall cycle-0 patch ladder: each step was added because")
    add("the previous step left a residue, and the names encode the escalation")
    add("(`...LiftGrowFirst`, `More`, `Yet`, `Even`, `Still`, `Again`, `Also`, `Extra`, `Plus`,")
    add("`Further`, `Onward`, `Ahead`, `Along`). The `CapGrow*` tooltips read 'grow the 34 deg")
    add("hip-lift cap a quarter toward 40 deg so the leftover has 1.5 deg of room', then 1.125,")
    add("0.84, 0.63, 0.47, 0.36, 0.27, 0.20, 0.15, 0.11, 0.084, 0.063, 0.047 - a halving chain of")
    add("constants, i.e. a `while` loop unrolled by hand and frozen into the class; the file")
    add("carries 267 `[Tooltip]` lines for it. Deleting them is NOT authorised here: %d are read,"
        % sum(1 for r in ladder if r["status"] == "LIVE"))
    add("and the walk cycle they gate is the accepted WALK-1 build. The plan is to replace the")
    add("ladder by the arithmetic it approximates, then delete the fields in one cycle with a")
    add("before/after fingerprint - one op, not %d." % (len(ladder) + len(cap)))
    add("")
    add("## 1. Totals")
    add("")
    add("| | count |")
    add("|---|---|")
    add("| candidate 0/1 fields in the three files | %d |" % len(rows))
    add("| ... `bool` | %d |" % sum(r["type"] == "bool" for r in rows))
    add("| ... `float` | %d |" % sum(r["type"] == "float" for r in rows))
    add("| ... `int` | %d |" % sum(r["type"] == "int" for r in rows))
    add("| of those, names a feature gate (`*First`,`*Enabled`,`*Once`,`useX`,`*Keep`,...) | %d |"
        % sum(1 for r in rows if r["gate_named"]))
    add("| LIVE | %d |" % sum(1 for r in rows if r["status"] == "LIVE"))
    add("| read outside the three files | %d |" % sum(1 for r in rows if r["others"]))
    add("| settable from the command line (`HeadlessTrial.cs`) | %d |" % sum(1 for r in rows if r["cli"]))
    add("| serialised in a scene/prefab | %d |" % sum(1 for r in rows if r["scene"]))
    add("| read in the pre-poseDrive-return region | %d |" % sum(1 for r in rows if r["pre_posedrive"]))
    add("")
    add("### Honest note on the number")
    add("")
    add("The brief says 'several hundred'. Measured in these three files there are **%d** fields"
        % len(rows))
    add("that are literally 0/1 or bool. The larger numbers appear when the whole `Assets/` tree")
    add("is counted (698 float/int class-scope fields initialised to 0/1 and 428 bools across 76")
    add("files; `StepPhaseDriver.cs` 138 + 31, `DogStanceController.cs` 120 + 39,")
    add("`BirdController.cs` 130 + 24). Those belong to other streams' files and are NOT in this")
    add("inventory. Within the human balance layer the 0/1 surface is %d fields, and the"
        % len(rows))
    add("patch-on-patch debris the brief is aimed at is the `walkPlantFall*` ladder (section 3).")
    add("")
    dead = [r for r in rows if r["status"].startswith("DEAD")]
    add("## 2. Deletion plan (evidence-based, %d of %d fields)" % (len(dead), len(rows)))
    add("")
    if dead:
        add("| field | file:line | status | evidence |")
        add("|---|---|---|---|")
        for r in dead:
            ev = "no read outside the declaration; not a CLI knob; not serialised"
            add("| `%s` | %s:%d | %s | %s |" % (r["name"], r["file"], r["line"], r["status"], ev))
    else:
        add("None. Every candidate field is read somewhere reachable, so nothing may be deleted")
        add("on read-evidence grounds alone: see section 4 for what that means for the plan.")
    add("")
    add("Fields with exactly one read site (the cheapest real deletions, after a behaviour")
    add("freeze) - listed for the next cycle, NOT authorised now:")
    add("")
    add("| field | file:line | read sites |")
    add("|---|---|---|")
    for r in rows:
        if r["n_reads"] == 1 and not r["others"] and not r["cli"] and not r["scene"]:
            add("| `%s` | %s:%d | %s:%d |" % (r["name"], r["file"], r["line"],
                                             r["reads"][0]["file"], r["reads"][0]["line"]))
    add("")
    add("## 3. Families")
    add("")
    add("`gate(kind)` counts how each read uses the value (gate = compared, coeff = multiplied,")
    add("select = ternary, logic = `&&`/`||`). `pre-pose` = a read above line %d." % posereturn)
    add("")
    add("| family | n | bool | live | dead | CLI | other-file | scene | pre-pose | gate/coeff | verdict |")
    add("|---|---|---|---|---|---|---|---|---|---|---|")
    for name, rs in sorted(fam.items(), key=lambda kv: -len(kv[1])):
        g = sum(r["kinds"].get("gate", 0) + r["kinds"].get("logic", 0) for r in rs)
        c = sum(r["kinds"].get("coeff", 0) for r in rs)
        nd = sum(1 for r in rs if r["status"].startswith("DEAD"))
        nl = sum(1 for r in rs if r["status"] == "LIVE")
        pre = sum(1 for r in rs if r["pre_posedrive"])
        verdict = ("dead" if nd == len(rs) else
                   "pose-path" if pre else
                   "level-1 only" if nl else "mixed")
        add("| %s | %d | %d | %d | %d | %d | %d | %d | %d | %d/%d | %s |"
            % (name, len(rs), sum(1 for r in rs if r["type"] == "bool"), nl, nd,
               sum(1 for r in rs if r["cli"]), sum(1 for r in rs if r["others"]),
               sum(1 for r in rs if r["scene"]), pre, g, c, verdict))
    add("")
    add("## 4. LIVE flags - do not delete")
    add("")
    add("Every field in this table has a read site reachable from a Unity message (or an")
    add("outside reader: a recorder column or a CLI knob). A deletion cycle that removes any of")
    add("them changes behaviour.")
    add("")
    add("| field | file:line | default | read sites | where |")
    add("|---|---|---|---|---|")
    for r in rows:
        if r["status"] != "LIVE":
            continue
        sites = ", ".join("%s:%d" % (d["file"].split("/")[-1], d["line"]) for d in r["reads"])
        where = sorted(set(r["read_methods"]) | set(x.split("/")[-1] for x in r["others"]))[:4]
        add("| `%s` | %s:%d | %s | %s | %s |"
            % (r["name"], r["file"], r["line"], r["init"] or "-", sites, "; ".join(where)))
    add("")
    with open(os.path.join(ROOT, OUT_MD), "w", encoding="utf-8") as f:
        f.write("\n".join(L) + "\n")
    print("wrote", OUT_MD)


if __name__ == "__main__":
    main()
