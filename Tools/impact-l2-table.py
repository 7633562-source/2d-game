#!/usr/bin/env python3
"""BALANCE-ARENA-1 - L2 no-clairvoyance table (level worker, 19.09.2026).

Reads the runs written by Tools/impact-l2-runs.sh and answers the card's L2 with numbers:

  A. STATIC: does any file in the human's control path name the thrower, a projectile or an
     impact switch? (the card's "grep the arm/torso/leg code ... and report it")
  B. BEHAVIOURAL: are the pre-contact frames of the quiet stand and of the 8 s throw run
     identical? (bracing before the hit is a defect)
  C. SEED: do time / side / aim vary with -impactSeed, while mass*speed stays ordered?
  D. DETERMINISM: is the same seed bit-identical a second time?

Everything is read back from Trials/Runs/*-impacts.{json,csv}; nothing is asserted from
source. Exit code is non-zero if any L2 expectation fails.
"""
import csv
import hashlib
import json
import os
import re
import sys

RUNS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Trials", "Runs")
ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets")

# The human's control path: the code that reads a sensor and commands a muscle. A reference
# to the thrower in ANY of these files is clairvoyance by construction.
CONTROL_FILES = [
    "BalanceController.cs",
    "BalanceControllerCaptureWalk.cs",
    "BalanceControllerWalkLift.cs",
    "GaitStateMachine.cs",
    "StepPhaseDriver.cs",
    "PoseDriveController.cs",
    "CaptureBalance.cs",
    "CapturePointPlanner.cs",
    "SwingTrajectory.cs",
    "ArmDrive.cs",
    "Muscle.cs",
    "Human.cs",
    "HumanSegment.cs",
    "VestibularSystem.cs",
    "BodyStateEstimator.cs",
    "ContactSensor.cs",
    "CenterOfMassCalculator.cs",
]
# The thrower / the thrown object / the harness that owns them.
FORBIDDEN = ["ImpactThrower", "ImpactProjectile", "ImpactRecorder", "ImpactCaption",
             "ImpactArena", "impactThrower", "impactMass", "-impact", "Projectile"]
# The physics bookkeeping columns of the per-frame CSV: they describe the harness, not the
# body. Everything else is the body and must match to the digit.
HARNESS_COLS = {"throwIndex", "projectileX", "projectileY", "projectileDistanceM",
                "contacted", "down", "fell", "passiveReached", "pushMeanFzRatio",
                "pushActive"}

failures = []
notes = []


def load(label):
    with open(os.path.join(RUNS, label + "-impacts.json"), encoding="utf-8-sig") as f:
        j = json.load(f)
    rows = []
    with open(os.path.join(RUNS, label + "-impacts.csv"), encoding="utf-8-sig", newline="") as f:
        for r in csv.DictReader(f, delimiter=";"):
            rows.append(r)
    return j, rows


def md5(path):
    h = hashlib.md5()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def sec(title):
    print()
    print("## " + title)


# ─── A. STATIC AUDIT ───────────────────────────────────────────────────────────────
sec("A. static audit - can the control path name the throw?")
present = 0
for name in CONTROL_FILES:
    path = os.path.join(ASSETS, name)
    if not os.path.exists(path):
        notes.append("control file not found: " + name)
        continue
    present += 1
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        hits = []
        for no, line in enumerate(f, 1):
            code = line.split("//")[0]
            for token in FORBIDDEN:
                if token in code:
                    hits.append((no, token, line.strip()[:90]))
    if hits:
        failures.append("CLAIRVOYANCE SURFACE in %s: %d hit(s)" % (name, len(hits)))
        for h in hits:
            print("  %s:%d  [%s]  %s" % (name, h[0], h[1], h[2]))
    else:
        print("  %-32s no thrower / projectile / impact reference" % name)
# The direction that is legal: the thrower and the harness may know the human.
for name in ("ImpactThrower.cs", "ImpactRecorder.cs", "ImpactCaption.cs", "ImpactArena.cs"):
    with open(os.path.join(ASSETS, name), encoding="utf-8-sig", errors="replace") as f:
        text = f.read()
    print("  %-32s -> human references: %d (legal direction: the thrower knows the human)"
          % (name, len(re.findall(r"\bHuman\b|\bhuman\b", text))))
print("  scanned %d control files of the %d on the list" % (present, len(CONTROL_FILES)))

# ─── A2. THE SENSOR SURFACE: WHAT CAN THE BODY PHYSICALLY FEEL? ───────────────────
sec("A2. sensor surface - every query in the control path, and the object's layer")
tagmanager = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "ProjectSettings",
                          "TagManager.asset")
ground_index = None
with open(tagmanager, encoding="utf-8-sig", errors="replace") as f:
    in_layers = False
    idx = 0
    for line in f:
        if re.match(r"\s*layers:\s*$", line):
            in_layers = True
            continue
        if in_layers:
            m = re.match(r"\s*-\s*(.*)$", line)
            if not m:
                break
            if m.group(1).strip() == "Ground":
                ground_index = idx
                break
            idx += 1
print("  Ground layer index (ProjectSettings/TagManager.asset): %s" % ground_index)
queries = []
masks = []
for name in CONTROL_FILES:
    path = os.path.join(ASSETS, name)
    if not os.path.exists(path):
        continue
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        for no, line in enumerate(f, 1):
            code = line.split("//")[0]
            if re.search(r"GetContacts\(|IsTouchingLayers\(|IsTouching\(|OverlapCircle|"
                         r"OverlapBox|OverlapPoint|\.Raycast\(", code):
                queries.append((name, no, code.strip()[:100]))
            if "SetLayerMask" in code or "layerMask" in code:
                masks.append((name, no, code.strip()[:100]))
print("  queries the control path can make: %d" % len(queries))
for q in queries:
    print("    %s:%d  %s" % q)
print("  layer masks the control path sets: %d" % len(masks))
for m in masks:
    print("    %s:%d  %s" % m)
shooter_layer_edits = []
for name in ("ImpactThrower.cs", "ImpactArena.cs"):
    with open(os.path.join(ASSETS, name), encoding="utf-8-sig", errors="replace") as f:
        for no, line in enumerate(f, 1):
            if re.search(r"\.layer\s*=|SetLayerMask|LayerMask", line.split("//")[0]):
                shooter_layer_edits.append((name, no, line.strip()[:100]))
print("  layer writes in the throw lane (ImpactThrower/ImpactArena): %d" % len(shooter_layer_edits))
for s in shooter_layer_edits:
    print("    %s:%d  %s" % s)
all_masked_to_ground = bool(masks) and all("GroundLayers.Mask" in m[2] for m in masks)
a2_fail = []
if not all_masked_to_ground:
    a2_fail.append("a control-path query is not filtered to the Ground layer: %s" % masks)
if shooter_layer_edits:
    a2_fail.append("the throw lane assigns a layer: %s" % shooter_layer_edits)
if ground_index in (None, 0):
    a2_fail.append("the Ground layer index is %s - the object's Default mask cannot be told "
                   "apart from it" % ground_index)
failures.extend(a2_fail)
if not a2_fail:
    print("  -> the thrown object keeps the Default layer (%d); every sensor the body owns is "
          "filtered to Ground (%s), so the only channel that can reach him is the solver's "
          "own contact response" % (0, ground_index))

# ─── B. BEHAVIOURAL: PRE-CONTACT EQUIVALENCE ───────────────────────────────────────
sec("B. pre-contact equivalence - quiet stand vs a scheduled 8 s throw")
quiet_j, quiet_rows = load("l2_quiet")
hit_j, hit_rows = load("l2_hit_s1")
t0 = hit_j["throws"][0]
contact = t0["contactTime"]
print("  quiet : switches count=%s, planned throws=%d, rows=%d"
      % (quiet_j["switches"]["count"], len(quiet_j.get("throws", [])), len(quiet_rows)))
print("  hit   : plannedTime=%.6f (ordered %.2f + seed jitter), contactTime=%.6f"
      % (t0["plannedTime"], hit_j["switches"]["firstThrowAt"], contact))
pre_q = [r for r in quiet_rows if float(r["time"]) < contact]
pre_h = [r for r in hit_rows if float(r["time"]) < contact]
print("  frames before contact: quiet %d, hit %d" % (len(pre_q), len(pre_h)))
if len(pre_q) != len(pre_h):
    failures.append("pre-contact frame counts differ (%d vs %d)" % (len(pre_q), len(pre_h)))
n = min(len(pre_q), len(pre_h))
bad_all = bad_phys = 0
first = None
bad_cols = {}
for i in range(n):
    diff = [c for c in pre_q[i] if pre_q[i][c] != pre_h[i][c]]
    if diff:
        bad_all += 1
        for c in diff:
            bad_cols.setdefault(c, 0)
            bad_cols[c] += 1
        if any(c not in HARNESS_COLS for c in diff):
            bad_phys += 1
        if first is None:
            first = (pre_q[i]["time"], diff)
print("  rows differing at all: %d of %d" % (bad_all, n))
print("  rows differing in a BODY column: %d of %d" % (bad_phys, n))
print("  differing columns: %s" % ({k: bad_cols[k] for k in sorted(bad_cols)} or "none"))
if first:
    print("  first difference at t=%s in %s" % first)
if bad_phys:
    failures.append("the body moves differently BEFORE the blow (bracing): %d frames" % bad_phys)
else:
    print("  -> the body is identical to the quiet stand until the blow arrives")

# ─── C. SEED: time, side, aim must vary; the ordered impulse must not ─────────────
sec("C. the seed decides time / side / aim - and nothing else")
seeds = [("l2_hit_s1", 1), ("l2_hit_s2", 2), ("l2_hit_s3", 3)]
print("  %-10s %-4s %-12s %-6s %-16s %-14s %-12s %-10s %-9s"
      % ("label", "seed", "plannedTime", "side", "aimPart", "jitter(x,y) m", "contactTime",
         "refImp N*s", "launch m/s"))
plan_times, sides, aims, refs = set(), set(), set(), set()
truth = []
for label, seed in seeds:
    j, _ = load(label)
    t = j["throws"][0]
    print("  %-10s %-4d %-12.6f %-6d %-16s (%+.5f,%+.5f) %-12.6f %-10.4f %-9.4f"
          % (label, seed, t["plannedTime"], t["side"], t["aimPart"],
             t["aimJitterMeters"][0], t["aimJitterMeters"][1], t["contactTime"],
             t["referenceImpulseNs"], t["launchSpeedMps"]))
    plan_times.add(round(t["plannedTime"], 6))
    sides.add(t["side"])
    aims.add(t["aimPart"])
    refs.add(round(t["referenceImpulseNs"], 6))
    truth.append((label, seed, t))
if len(plan_times) < 2:
    failures.append("the throw TIME does not vary with the seed")
if len(sides) < 2:
    notes.append("side did not vary across seeds 1-3 (3 draws at 50/50 can do that): %s" % sides)
if len(aims) < 2:
    notes.append("aim part did not vary across seeds 1-3 (2 candidates at 50/50): %s" % aims)
if len(refs) != 1:
    failures.append("the ORDERED impulse moved with the seed: %s" % refs)
print("  distinct plan times %d, sides %s, aim parts %s, reference impulses %s"
      % (len(plan_times), sorted(sides), sorted(aims), sorted(refs)))

# ─── D. DETERMINISM: the same seed twice ──────────────────────────────────────────
sec("D. the same seed a second time")
a = md5(os.path.join(RUNS, "l2_hit_s1-impacts.csv"))
b = md5(os.path.join(RUNS, "l2_hit_s1b-impacts.csv"))
print("  l2_hit_s1  csv md5 %s" % a)
print("  l2_hit_s1b csv md5 %s" % b)
if a != b:
    failures.append("two runs of the same seed are not bit-identical")
else:
    print("  -> bit-identical")

# ─── the recorded verdicts of each throw ─────────────────────────────────────────
sec("E. what the recorder says per throw (the card's own columns)")
print("  %-10s %-8s %-8s %-10s %-9s %-10s %-9s %-9s %-8s"
      % ("label", "fell", "ctrlFell", "timeToFall", "peakCoM", "minMargin", "steps", "latP ms",
         "clairv"))
for label, seed in seeds + [("l2_hit_s1b", 1)]:
    j, _ = load(label)
    t = j["throws"][0]
    print("  %-10s %-8s %-8s %-10.4f %-9.4f %-10.4f %-9d %-9.4f %-8s"
          % (label, t["fell"], t["ctrlFell"], t["timeToFallSeconds"], t["peakComExcursionM"],
             t["minSupportMarginM"], t["stepsAfterImpact"], t["latencyActiveMs"],
             t["clairvoyanceFlag"]))
    if t["clairvoyanceFlag"]:
        failures.append("%s: the recorder flagged clairvoyance" % label)

sec("VERDICT")
print("  fingerprint  %s" % hit_j["sourceFingerprint"])
print("  buildGuid    %s" % hit_j["buildGuid"])
for n in notes:
    print("  note: " + n)
if failures:
    for f in failures:
        print("  FAIL: " + f)
    sys.exit(1)
print("  L2 OK: no control-path reference to the throw, no pre-contact divergence, "
      "seed decides time/side/aim only, same seed bit-identical")
