"""JOB B - WIDE genome spec generator (read-only).

Reads the "WIDE genome" section of .cursor/tasks/ARENA-1-param-sweep.md, resolves
every listed parameter to its declaration in Assets/*.cs, classifies it (gain /
time / length / angle / share / friction / mass / limit) and proposes a concrete
sampling range with the file:line of the default.

Writes Trials/Runs/arena-wide-genome.md.

Rule table (from the brief):
    gains, ceilings, torques, frictions   0.25x .. 4x
    timings, durations, rates, speeds     0.5x .. 2x
    lengths, margins, offsets             0.5x .. 2x, never negative
    angles                                +-50%  (0.5x .. 1.5x)
    shares / fractions                    0.5x .. 2x, clamped to [0, 1]
    masses, multipliers                   0.7x .. 1.4x
    joint limits                          +-50% of magnitude, inside the shipped
                                          hardware limit, ordered by construction
"""
import os, re, math, collections

ROOT = r"E:\Games\2D Game\My project"
MD = os.path.join(ROOT, ".cursor", "tasks", "ARENA-1-param-sweep.md")
OUT = os.path.join(ROOT, "Trials", "Runs", "arena-wide-genome.md")
lines = open(MD, encoding="utf-8").read().split("\n")

FILE_OF = dict(PoseDriveController="PoseDriveController.cs",
               BalanceControllerCaptureWalk="BalanceControllerCaptureWalk.cs",
               BalanceControllerWalkLift="BalanceControllerWalkLift.cs",
               StepPhaseDriver="StepPhaseDriver.cs", ArmDrive="ArmDrive.cs",
               BalanceController="BalanceController.cs", Human="Human.cs",
               ActuatorMixer="ActuatorMixer.cs")
TITLE = dict(C1="C1 pose drive", C2="C2 capture walk", C3="C3 walk lift",
             C4="C4 step phase / gating", C5="C5 arm drive", C6="C6 balance loop",
             B1="B1 muscle ceilings", B2="B2 actuator", B3="B3 contact",
             B4="B4 geometry / mass")

# --- declaration index (CLASS SCOPE only: brace depth 1, so method locals such as
# --- `float walk = ...` are never mistaken for a genome parameter)
def strip_code(text):
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
        else:
            i += 1
    return "".join(out)


files = []
for dp, _, fn in os.walk(os.path.join(ROOT, "Assets")):
    for f in fn:
        if f.endswith(".cs"):
            files.append(os.path.relpath(os.path.join(dp, f), ROOT).replace(os.sep, "/"))
DECL = re.compile(r"^\s*(?:\[[^\]]*\]\s*)*"
                  r"(?:(?:public|private|protected|internal|static|readonly|const)\s+)*"
                  r"(float|int|double)\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?:=\s*([^;]+))?;")
decl = {}
for rel in files:
    raw = open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace").read()
    code = strip_code(raw).split("\n")
    depth = 0
    for i, l in enumerate(code, 1):
        d = depth
        depth += l.count("{") - l.count("}")
        if d != 1:
            continue
        m = DECL.match(l)
        if m:
            decl.setdefault(m.group(2), []).append(dict(file=rel, line=i, type=m.group(1),
                                                        default=(m.group(3) or "").strip()))


# --- the WIDE section, parsed strictly: a token counts only when it is declared
# --- in the file of the class named in the group header.
start = next(i for i, l in enumerate(lines) if l.startswith("## WIDE genome"))
end = next(i for i, l in enumerate(lines[start:], start) if l.startswith("### Ranges"))
GEN13 = ["swingDuration", "stanceDuration", "swingLiftMeters", "swingLandShare",
         "heightGainDegPerMeter", "heightDampDegPerMeterPerSecond", "placementMarginMeters",
         "comLeadSwapMeters", "stepMinMeters", "stepMaxMeters", "armSwingDeg",
         "ankleBalanceP", "ankleBalanceD"]
groups, cur = collections.OrderedDict(), None
for l in lines[start:end]:
    m = re.match(r"- ([CB]\d) ", l)
    if m:
        cur = m.group(1)
        cm = re.search(r"\(([A-Za-z0-9]+)\)", l)
        groups[cur] = dict(classname=cm.group(1), raw=[l])
        continue
    if cur and l.startswith(("  ", "\t")):
        groups[cur]["raw"].append(l)

rows, unresolved = [], []
for g, d in groups.items():
    cls = d["classname"]
    own = FILE_OF.get(cls, "")
    text = " ".join(d["raw"])
    names = []
    for t in re.findall(r"[A-Za-z_][A-Za-z0-9_]*", text):
        hits = decl.get(t, [])
        if not hits:
            continue
        if not any(h["file"].endswith(own) for h in hits):
            if g == "C1" and any(h["file"].endswith("PoseDriveController.cs") for h in hits):
                pass
            else:
                continue
        if t not in names:
            names.append(t)
    if g == "C1":
        for p in GEN13:
            if p not in names and p in decl:
                names.insert(0, p)
    for n in names:
        hits = [h for h in decl[n] if h["file"].endswith(own)] or decl[n]
        rows.append(dict(group=g, cls=cls, name=n, **hits[0]))
    if not names:
        unresolved.append(g)
    print("%s %-30s %d params" % (g, cls, len(names)))

# --- classification and ranges
ANGLE = ("deg", "angle", "tilt", "lean", "pitch", "flex", "apex", "roll", "yaw")
LENGTH = ("meter", "length", "margin", "offset", "height", "clear", "spacing", "lead",
          "step", "reach", "stride", "width", "travel", "distance", "lift", "swap")
TIME = ("seconds", "second", "duration", "time", "tau", "rate", "speed", "hz", "frames",
        "lookahead", "cadence", "ramp", "delay", "hold", "age", "interval")
SHARE = ("share", "fraction")
GAIN = ("gain", "scale", "cap", "bias", "weight", "amp", "p", "d", "boost", "ceiling",
        "torque", "mul")
LIMIT = ("limitmin", "limitmax")


def classify(name):
    """Name-based, ordered so an explicit unit beats a generic word."""
    n = name.lower()
    if "multiplier" in n or n.endswith("mass"):
        return "mass"
    if "friction" in n:
        return "friction"
    if any(n.endswith(k) for k in LIMIT):
        return "limit"
    if "torque" in n or "muscle" in n:
        return "gain"
    if "share" in n or "fraction" in n or "ratio" in n:
        return "share"
    if "persec" in n or "persecond" in n or "perSec" in name:
        return "time"
    if any(k in n for k in ("second", "duration", "tau", "time", "lookahead", "frames",
                            "cadence", "delay", "hold", "age", "interval", "rate")):
        return "time"
    if any(k in n for k in ("gain", "scale", "cap", "bias", "weight", "amp", "boost",
                            "ceiling")):
        return "gain"
    if "speed" in n:
        return "time"
    if any(k in n for k in ("meter", "length", "margin", "offset", "height", "clear",
                            "spacing", "distance", "width", "trigger", "seed", "floor")):
        return "length"
    if any(k in n for k in ("deg", "angle", "tilt", "lean", "pitch", "flex", "apex", "roll")):
        return "angle"
    if any(k in n for k in ("step", "reach", "lead", "stride", "lift", "travel", "swap")):
        return "length"
    return "gain"


# Rows whose NAME hides the unit. Each entry was checked against the Tooltip /
# comment next to the declaration; the reason is quoted in the report.
OVERRIDE = {
    "heightGainDegPerMeter": ("gain", "deg per m: a gain, the name's 'Meter' is the denominator"),
    "heightDampDegPerMeterPerSecond": ("gain", "deg per m per s: a gain (damping)"),
    "stepMinMeters": ("signed_length", "signed band, comment PoseDriveController.cs:65-70"),
    "captureLookahead": ("share", "Tooltip CaptureWalk.cs:180: share of LIPM xi(T), 1 = predictor"),
    "captureSwingApex": ("length", "Tooltip CaptureWalk.cs:186: apex clearance, m"),
    "captureCatchPast": ("length", "CoM past the catch point, m"),
    "captureDoubleSupportMin": ("time", "s"),
    "captureDoubleSupportMax": ("time", "s"),
    "captureSupportKneeExtendRate": ("time", "deg/s rate"),
    "captureSpeedGain": ("gain", "kv coefficient, s"),
    "stanceComTrigger": ("length", "Tooltip StepPhaseDriver.cs:54: CoM ahead of the stance foot, m"),
    "stanceComTriggerPerSpeed": ("signed_gain", "signed slope per speed, m per m/s"),
    "walkCadenceStanceGain": ("gain", "gain"),
    "walkLiftArmGain": ("angle", "Tooltip WalkLift.cs:89: contra shoulder amplitude, deg"),
    "walkLiftHipRateDeg": ("time", "deg/s rate"),
    "walkLiftCoupleHipMax": ("share", "Tooltip WalkLift.cs:85: clamp on a normalised feed-forward"),
    "walkLiftReactionCap": ("share", "Tooltip WalkLift.cs:87: |tau_sw| threshold, normalised"),
    "walkLiftLoadRatio": ("share", "Tooltip WalkLift.cs:29: stance share 0..1"),
    "walkLiftStancePdScale": ("gain", "PD scale"),
    "walkLiftHipRateScale": ("gain", "scale"),
    "captureLaunchAnkleCap": ("share", "Tooltip CaptureWalk.cs:331: ankle |command| ceiling 0..1"),
}

# Ranges the code itself already bounds tighter than the rule allows.
CLAMPED = {
    "footHeelShare": (0.40, 0.85, "Human.cs:534 clamps the share to [0.4, 0.85]"),
    "captureLaunchAnkleCap": (0.14, 1.00, "a 0..1 command ceiling cannot exceed 1"),
    "captureStanceHipTorqueCap": (11.25, 100.0, "Tooltip :307: inert above hipMuscleTorque (100 N*m)"),
    "captureStanceHipTorqueCapForward": (32.5, 100.0, "Tooltip :311: inert above hipMuscleTorque (100 N*m)"),
    "kneeLimitMin": (0.0, 0.0, "straight-leg stop: a hardware constant, keep fixed"),
}



def parse_default(s):
    s = s.strip().rstrip("f").strip()
    try:
        return float(s)
    except ValueError:
        return None


def range_for(kind, default, name):
    """Returns (lo, hi, rule, note)."""
    if default is None:
        return (None, None, "no literal default", "default is an expression - read it in the source")
    if kind == "limit":
        if abs(default) < 1e-9:
            return (0.0, 0.0, "hardware stop", "0 is the straight-leg / neutral stop: keep fixed")
        lo = min(default, default * 1.5) if default < 0 else default * 0.5
        hi = max(default, default * 1.5) if default < 0 else default
        if default < 0:
            lo, hi = default, default * 0.5
        return (round(lo, 6), round(hi, 6), "+-50%, inside the shipped limit",
                "invariant min <= max - 15 deg enforced at sample time")
    if abs(default) < 1e-9 and kind != "share":
        return (0.0, 0.0, "ZERO DEFAULT", "feature is OFF in the source (gated by > 0.001): "
                "the range is a decision - see the ZERO-DEFAULT section")
    return None


def apply_rule(kind, default, name):
    d = default
    gain = ("gains 0.25x-4x", d * 0.25, d * 4.0)
    if kind == "gain":
        return gain[1], gain[2], gain[0]
    if kind == "signed_gain":
        lo, hi = sorted((d * 0.25, d * 4.0))
        return lo, hi, "gains 0.25x-4x, sign kept (negative default)"
    if kind == "friction":
        return d * 0.25, d * 4.0, "frictions 0.25x-4x"
    if kind == "mass":
        return d * 0.7, d * 1.4, "mass/multiplier 0.7x-1.4x"
    if kind == "time":
        return min(d * 0.5, d * 2.0), max(d * 0.5, d * 2.0), "timing/rate 0.5x-2x"
    if kind == "length":
        return min(d * 0.5, d * 2.0), max(d * 0.5, d * 2.0), "length/margin 0.5x-2x"
    if kind == "signed_length":
        return min(d * 0.5, d * 2.0), max(d * 0.5, d * 2.0), \
            "length/margin 0.5x-2x, sign kept (negative default)"
    if kind == "angle":
        return min(d * 0.5, d * 1.5), max(d * 0.5, d * 1.5), "angle +-50% (0.5x-1.5x)"
    if kind == "share":
        return max(0.0, d * 0.5), min(1.0, d * 2.0), "share 0.5x-2x clamped to [0,1]"
    return d * 0.25, d * 4.0, "gains 0.25x-4x"


# --- existing arena box for the 13 (ArenaGenome.cs:48-63)
agn = open(os.path.join(ROOT, "Assets", "ArenaGenome.cs"), encoding="utf-8").read()
def arr(tag):
    m = re.search(r"float\[\] %s\s*=\s*\{([^}]*)\}" % tag, agn)
    return [float(x.strip().rstrip("f")) for x in m.group(1).split(",") if x.strip()]
AMIN, AMAX, ADEF = arr("Min"), arr("Max"), arr("Defaults")
ARENA_BOX = dict(zip(GEN13, zip(AMIN, AMAX, ADEF)))

for r in rows:
    d = parse_default(r["default"])
    if d is None:
        # an expression default (e.g. "Mathf.Max(...)"): keep it visible, skip the range
        r.update(kind="expression", lo=None, hi=None, rule="default is an expression",
                 note="resolve the value from the code before sampling")
        continue
    kind = classify(r["name"])
    note = ""
    if r["name"] in OVERRIDE:
        kind, note = OVERRIDE[r["name"]]
        note = "override: " + note
    if kind == "limit":
        lo, hi, rule, note = range_for("limit", d, r["name"])
    elif abs(d) < 1e-9:
        lo, hi, rule, note = 0.0, 0.0, "ZERO DEFAULT", "see the ZERO-DEFAULT section"
    else:
        lo, hi, rule = apply_rule(kind, d, r["name"])
    if r["name"] in CLAMPED:
        clo, chi, why = CLAMPED[r["name"]]
        lo, hi = max(lo, clo), min(hi, chi)
        note = (note + " | " if note else "") + "code-bounded: " + why
    r.update(kind=kind, lo=round(lo, 6), hi=round(hi, 6), rule=rule, note=note)

# --- ZERO-DEFAULT proposals: anchored on the physics of the field, not on 0.
ZERO_RANGE = {
    "stanceHeelStrikeMinAge": (0.0, 0.20, "s", "phase age before a heel strike may fire; "
                               "0 = off. Anchor: a step period is 0.375 s measured (depth report)"),
    "stanceHeelAirMin": (0.0, 8.0, "frames", "air frames before a heel strike may fire; 0 = off; "
                         "a 0.005 s frame makes 8 frames = 0.04 s"),
    "walkTiltAbortDeg": (0.0, 45.0, "deg", "tilt that aborts the walk; 0 = off. Inside the "
                         "60 deg fall-criterion tilt and the 12 deg TiltSettled band used by the build"),
    "walkTiltRecoverDeg": (0.0, 30.0, "deg", "tilt that ends the abort; must stay below the abort"),
    "kneeLimitMin": (0.0, 0.0, "deg", "straight-leg stop - hardware constant, do not sample"),
    "mass": (None, None, "", "not a field of Human: matched by name in prose"),
    "already": (None, None, "", "not a field of the group: matched by name in prose"),
}

print("classified:", collections.Counter(r["kind"] for r in rows))
expr = [r for r in rows if r["kind"] == "expression"]
print("expression defaults:", [(r["name"], r["default"]) for r in expr])
zero = [r for r in rows if r["kind"] == "zero" or r["rule"] == "ZERO DEFAULT"]
print("zero defaults:", [r["name"] for r in zero])

# ------------------------------------------------------------------ report
L = []
add = L.append
add("# WIDE genome - concrete sampling ranges (MEASURE STREAM, job B)")
add("")
add("Source of the parameter list: `.cursor/tasks/ARENA-1-param-sweep.md`, section")
add("\"WIDE genome\". Read-only: nothing in `Assets/` was edited. Generator:")
add("`Tools/arena-wide-genome-scan.py`. Machine-readable form:")
add("`Trials/Runs/_wide-genome-params.json`.")
add("")
add("## Rules applied")
add("")
add("| kind | range | why |")
add("|---|---|---|")
add("| gain / torque / ceiling | 0.25x .. 4x default | brief, `### Ranges and rules` |")
add("| friction | 0.25x .. 4x default | brief |")
add("| timing / duration / rate / speed | 0.5x .. 2x default | brief |")
add("| length / margin / offset | 0.5x .. 2x default, never negative | brief |")
add("| angle | +-50% (0.5x .. 1.5x) | brief |")
add("| share / fraction | 0.5x .. 2x, clamped to [0, 1] | brief + the code's own clamps |")
add("| mass / multiplier | 0.7x .. 1.4x | brief, no super-heavy bodies |")
add("| joint limit | +-50% of magnitude, inside the shipped hardware limit | brief |")
add("")
add("The classification is by name and is printed per row in the `rule` column, so a wrong")
add("guess is visible instead of hidden. Every value is clamped to the box at sample time,")
add("and - per the brief - the bounds must be handled by REFLECTION, not by clamping, so")
add("samples do not pile up on a bound (26.5% / 23.8% did in the 13-gene build).")
add("")
add("## Hard invariants (enforced at sample time, repair logged)")
add("")
add("- `stepMaxMeters >= stepMinMeters` (ArenaGenome.cs:161-163 already does this)")
add("- `hipLimitMin < hipLimitMax`, `kneeLimitMin < kneeLimitMax`, `toeLimitMin < toeLimitMax`")
add("- every torque > 0, every duration > 0")
add("- joint limits stay inside the shipped hardware values (Human.cs:170-177: hip -90..45,")
add("  knee 0..120, toe -35..50)")
add("- `footHeelShare` inside the code's own clamp [0.4, 0.85] (Human.cs:534)")
add("")
add("## Parameters")
add("")
add("`file:line` is the declaration the default lives in; `group` is the arena group tag.")
add("")
add("| # | parameter | group | class | default | min | max | rule | file:line | note |")
add("|---|---|---|---|---|---|---|---|---|---|")
n = 0
for r in rows:
    n += 1
    add("| %d | `%s` | %s | %s | %s | %s | %s | %s | %s:%d | %s |"
        % (n, r["name"], r["group"], r["cls"],
           r["default"] or "-", r["lo"] if r["lo"] is not None else "-",
           r["hi"] if r["hi"] is not None else "-", r["rule"], r["file"], r["line"],
           r["note"] or ""))
add("")
ov = [r for r in rows if r["note"].startswith("override")]
add("### Rows the name-based classifier would have got wrong")
add("")
add("The classifier is mechanical (it is printed per row above), so %d rows were overridden"
    % len(ov))
add("after reading the tooltip next to the declaration. Each override is auditable:")
add("")
add("| parameter | kind used | why | file:line |")
add("|---|---|---|---|")
for r in ov:
    add("| `%s` | %s | %s | %s:%d |" % (r["name"], r["kind"], r["note"][10:], r["file"], r["line"]))
add("")
add("### Ceilings above what the body can deliver")
add("")
add("Some gains are ceilings on a torque, and 4x of the shipped value can land above what")
add("the muscles can produce at all, which would make part of the box unreachable (the same")
add("class of waste as the bound-sitting the brief complains about). The muscle ceilings are")
add("B1, so the effective max for those rows is `min(4x default, the matching B1 ceiling)`:")
add("")
add("| parameter | 4x value | B1 ceiling in the same stack | file:line |")
add("|---|---|---|---|")
B1 = {r["name"]: r for r in rows if r["group"] == "B1"}
CEIL_MAP = {"captureStanceHipTorqueCap": "hipMuscleTorque",
            "captureStanceHipTorqueCapForward": "hipMuscleTorque",
            "captureLegPushCap": "kneeMuscleTorque",
            "captureLaunchAnkleCap": "ankleExtensorTorque",
            "captureSupportKneeExtendRate": "kneeMuscleTorque"}
for r in rows:
    if r["name"] not in CEIL_MAP or r["hi"] is None:
        continue
    b1 = B1.get(CEIL_MAP[r["name"]])
    add("| `%s` | %s | %s | %s |"
        % (r["name"], r["hi"],
           (b1["default"] + " N*m (`" + b1["name"] + "`)") if b1 else "n/a",
           (b1["file"] + ":" + str(b1["line"])) if b1 else "-"))
add("")

by_group = collections.OrderedDict()
for r in rows:
    by_group.setdefault(r["group"], []).append(r)
add("## Per group")
add("")
add("| group | what | parameters | zero defaults | shares | limits |")
add("|---|---|---|---|---|---|")
for g, rs in by_group.items():
    add("| %s | %s | %d | %d | %d | %d |"
        % (g, TITLE.get(g, ""), len(rs),
           sum(1 for r in rs if r["rule"] == "ZERO DEFAULT"),
           sum(1 for r in rs if r["kind"] == "share"),
           sum(1 for r in rs if r["kind"] == "limit")))
add("")
add("## ZERO-DEFAULT parameters (the range is a decision, not a multiple)")
add("")
add("These are OFF in the source (`= 0` and gated by `> 0.001f`), so `0.25x..4x of 0` is")
add("meaningless. The proposed box is anchored on the physics of the field, and `0` stays")
add("inside it so the sweep can still switch the feature off.")
add("")
add("| parameter | proposed min | max | unit | anchor |")
add("|---|---|---|---|---|")
for r in rows:
    if r["rule"] == "ZERO DEFAULT":
        z = ZERO_RANGE.get(r["name"])
        if z:
            add("| `%s` | %s | %s | %s | %s |" % (r["name"], z[0], z[1], z[2], z[3]))
        else:
            add("| `%s` | 0 | ? | - | not covered by the table above: decide from the call site "
                "%s:%d |" % (r["name"], r["file"], r["line"]))
add("")
add("## Consistency with the 13-gene box already in the code")
add("")
add("`Assets/ArenaGenome.cs:48-63` already declares Min/Max/Defaults for the 13 original")
add("genes. Those boxes are NARROWER than the rules above (they were hand-set), so the")
add("question is whether to widen them or to keep the measured box:")
add("")
add("| parameter | default (code) | arena box Min..Max | rule-derived Min..Max | arena box width | rule width |")
add("|---|---|---|---|---|---|")
for p in GEN13:
    box = ARENA_BOX.get(p)
    row = next((r for r in rows if r["name"] == p), None)
    if not box or not row or row["lo"] is None:
        continue
    add("| `%s` | %s | %s .. %s | %s .. %s | %.2fx | %.2fx |"
        % (p, box[2], box[0], box[1], row["lo"], row["hi"],
           (box[1] - box[0]) / box[2], (row["hi"] - row["lo"]) / box[2]))
add("")
add("## Findings that matter for the unfreeze")
add("")
add("1. **The arena apply path floors `stepMinMeters` at 0, so the genome cannot express the")
add("   signed band the controller now uses.** `PoseDriveController.cs:228` reads")
add("   `stepMinMeters = Mathf.Max(0f, g[8]);`. The field's working-tree default is `-0.45f`")
add("   (`PoseDriveController.cs:70`, with the reasoning in the comment at lines 58-70: the band")
add("   is deliberately one step either side of the stance foot \"so the law can put the pivot")
add("   behind a CoM that is falling backwards\"). Through the genome path gene 8 can never be")
add("   negative, so half of that band is unreachable; `ArenaGenome.Defaults[8]` is still `0.02`")
add("   (the pre-6r value) and `Min[8] = 0`, so both the default and the floor are stale. The")
add("   new box must sample the SIGN and the apply path must stop flooring at 0.")
add("   NOTE: this file is read from the WORKING TREE, and `Assets/PoseDriveController.cs` has")
add("   uncommitted changes from the walk stream (mtime 04:07). If that stream edits it again,")
add("   the boxes here must be re-read - the generator does that in one command.")
add("2. **Four parameters are off in the source** (`= 0`, gated by `> 0.001f`) and are listed")
add("   in the ZERO-DEFAULT section: for them the sweep is not widening a box, it is switching")
add("   a feature on. They should be sampled first and separately, or the influence log will")
add("   mix \"this gain is bad\" with \"this feature was off\".")
add("3. **The existing 13-gene box is not the rule-derived box** (table above). Keep the")
add("   measured box for the 13 and apply the rules to the new parameters only.")
add("4. **Signs are not decoration**: `stepMinMeters` and `stanceComTriggerPerSpeed` are")
add("   negative by design. A range generator that multiplies by 0.5 and 2 without sorting")
add("   would emit `min > max` for them; the `min`/`max` columns here are already ordered.")
add("")
add("## How to use this file")
add("")
add("The table is the spec for the genome arrays: one `Min`, one `Max` and one `Defaults`")
add("entry per row, in the row order (the influence-log contract is append-only, so new")
add("genes go at the END of the existing 13). `group` becomes the group tag column, and the")
add("`rule` and `note` columns are the audit trail for why each box is what it is.")
add("")

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(L) + "\n")
print("wrote", OUT, "rows:", len(rows))
