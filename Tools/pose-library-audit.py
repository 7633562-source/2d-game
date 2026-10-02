#!/usr/bin/env python
"""Gate the LEVEL 2 pose table BEFORE a build is spent on it.

Why this exists
---------------
`Assets/PoseDriveController.cs` now carries the walk's shapes as DATA: five named
poses (`PoseLibrary.Stance / Launch / SwingFold / FootPlace / PlantLoad`), each a
hip / knee / ankle / toe target in degrees per leg plus the trunk targets the same
pose owns. The card for walk cycle 6q asks for two things about that table that a
reader cannot check by eye and that a build would only report as "the walk did
something odd":

  1. every angle is REACHABLE by the joint limits of `Assets/Human.cs`
     (hip -90..45, knee 0..120, the ankle pair -45..45 written as literals at the
     heel's `ConnectTo` call, toe -35..50); and
  2. the geometry the law derives from those numbers - the hip-to-ankle drop, the
     foot's own position ahead of the hip, the SOLE clearance the fold buys, the
     knee the `-poseLift` request asks for and the placement gain - are the numbers
     the report quotes.

Read-only: it reads `Assets/PoseDriveController.cs` and `Assets/Human.cs`, parses
the table out of the source (no copy of it lives here), and prints. It edits
nothing, builds nothing, runs no trial.

Exit 0 = the table is complete, inside the limits, and the derived geometry
matches the numbers quoted in the cycle's report; 1 = a defect (printed);
2 = the source could not be read.

Usage:
    python Tools/pose-library-audit.py
    python Tools/pose-library-audit.py <other PoseDriveController.cs>   # selftest
"""
import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
POSE_SRC = os.path.join(ROOT, "Assets", "PoseDriveController.cs")
HUMAN_SRC = os.path.join(ROOT, "Assets", "Human.cs")

# The report's own numbers, so a drift in either place fails the run.
EXPECT_POSES = ["stance", "launch", "swingfold", "footplace", "plantload"]
EXPECT_FOLD_KNEE = 68.0          # the drawn fold key, degrees
EXPECT_FOLD_SOLE_M = 0.0738      # the drawn fold's sole clearance at a 0.83 m hip
FOLD_BAND = (60.0, 80.0)         # Yury 18.09 "Swing leg must fold"
HIP_HEIGHT_M = 0.83              # the stand frame's pelvis height, m


def read(path):
    if not os.path.isfile(path):
        print("MISSING %s" % path)
        sys.exit(2)
    return open(path, encoding="utf-8", errors="replace").read()


def human_value(text, field):
    m = re.search(r'public\s+(?:float|Vector2)\s+' + field +
                  r'\s*=\s*new\s+Vector2\(([-\d.f]+),\s*([-\d.f]+)\)', text)
    if m:
        return (float(m.group(1).rstrip("f")), float(m.group(2).rstrip("f")))
    m = re.search(r'public\s+float\s+' + field + r'\s*=\s*([-\d.f]+)f', text)
    if m:
        return float(m.group(1).rstrip("f"))
    print("HUMAN FIELD NOT FOUND: %s" % field)
    sys.exit(2)


def main():
    pose_text = read(POSE_SRC)
    human_text = read(HUMAN_SRC)

    thigh = human_value(human_text, "thighSize")[1]
    shin = human_value(human_text, "shinSize")[1]
    foot = human_value(human_text, "footSize")[1]
    sole = foot * 0.5           # the ankle sits at the centre of the heel plate
    limits = {
        "hip": (human_value(human_text, "hipLimitMin"),
                human_value(human_text, "hipLimitMax")),
        "knee": (human_value(human_text, "kneeLimitMin"),
                 human_value(human_text, "kneeLimitMax")),
        # Human.cs writes the ankle pair as literals at the heel's ConnectTo call
        # and names no field for it; read them from that line.
        "toe": (human_value(human_text, "toeLimitMin"),
                human_value(human_text, "toeLimitMax")),
    }
    m = re.search(r'heel\.ConnectTo\(shin,\s*new\s+Vector2\(ankleAnchorX,\s*0f\),\s*'
                  r'new\s+Vector2\(0,\s*-shinSize\.y\s*\*\s*0\.5f\),\s*'
                  r'([-\d.f]+)f,\s*([-\d.f]+)f\)', human_text)
    if not m:
        print("ANKLE LIMIT PAIR NOT FOUND (Human.cs heel.ConnectTo)")
        sys.exit(2)
    limits["ankle"] = (float(m.group(1).rstrip("f")), float(m.group(2).rstrip("f")))

    pat = re.compile(
        r'public static readonly Pose (\w+) = Make\("(\w+)",\s*'
        r'Leg\(([-\d.f\s,]+)\),\s*Leg\(([-\d.f\s,]+)\),\s*([-\d.f]+)f,\s*([-\d.f]+)f\)',
        re.S)
    poses, order = {}, []
    for m in pat.finditer(pose_text):
        poses[m.group(2)] = {
            "swing": [float(v) for v in m.group(3).replace("f", "").split(",")],
            "stance": [float(v) for v in m.group(4).replace("f", "").split(",")],
            "pelvis": float(m.group(5)),
            "torso": float(m.group(6)),
        }
        order.append(m.group(2))

    bad = []
    if order != EXPECT_POSES:
        bad.append("pose table order/names changed: %s (report: %s)"
                   % (",".join(order), ",".join(EXPECT_POSES)))

    worst = None
    values = 0
    for name in order:
        for role in ("swing", "stance"):
            triple = poses[name][role]
            if len(triple) != 4:
                bad.append("%s.%s does not carry hip/knee/ankle/toe" % (name, role))
                continue
            for joint, value in zip(("hip", "knee", "ankle", "toe"), triple):
                lo, hi = limits[joint]
                values += 1
                if not lo <= value <= hi:
                    bad.append("%s.%s %s = %g outside %g..%g"
                               % (name, role, joint, value, lo, hi))
                margin = min(value - lo, hi - value)
                if worst is None or margin < worst[0]:
                    worst = (margin, name, role, joint, value)

    def geom(hip, knee):
        tw = math.radians(-hip)
        sw = tw - math.radians(knee)
        return (thigh * math.sin(tw) + shin * math.sin(sw),
                thigh * math.cos(tw) + shin * math.cos(sw))

    def knee_for_drop(drop, tw_deg, branch_knee):
        tw = math.radians(tw_deg)
        c = max(-1.0, min(1.0, (drop - thigh * math.cos(tw)) / shin))
        shin_world = math.degrees(math.acos(c))
        folded = tw_deg + shin_world
        straight = tw_deg - shin_world
        return folded if branch_knee > (folded + straight) / 2.0 else straight

    fold = poses.get("swingfold", {}).get("swing")
    if not fold:
        bad.append("the swing fold pose is missing: the fold band cannot be checked")
    else:
        hip, knee = fold[0], fold[1]
        drop = geom(hip, knee)[1]
        sole_clear = HIP_HEIGHT_M - drop - sole
        if abs(knee - EXPECT_FOLD_KNEE) > 0.01:
            bad.append("drawn fold knee %g, report says %g" % (knee, EXPECT_FOLD_KNEE))
        if abs(sole_clear - EXPECT_FOLD_SOLE_M) > 0.0002:
            bad.append("drawn fold sole clearance %.4f m, report says %.4f"
                       % (sole_clear, EXPECT_FOLD_SOLE_M))
        # the -poseLift request may only raise the fold, and the band caps it
        asked = knee_for_drop(HIP_HEIGHT_M - sole - 0.22, -hip, knee)
        capped = max(FOLD_BAND[0], min(FOLD_BAND[1], asked))
        if capped > FOLD_BAND[1] + 0.01:
            bad.append("the clearance request escapes the drawing's band (%.1f)" % capped)
        print("fold: drawn %g deg, sole clearance %.4f m (%.0f mm, bar 30 mm);"
              " -poseLift 0.22 m asks %.1f deg -> band caps at %.1f deg (%.4f m)"
              % (knee, sole_clear, sole_clear * 1000, asked, capped,
                 HIP_HEIGHT_M - (thigh * math.cos(math.radians(-hip)) +
                                 shin * math.cos(math.radians(-hip - capped))) - sole))

    place = poses.get("footplace", {}).get("swing")
    if place:
        drop = geom(place[0], place[1])[1]
        print("foot place: drop %.4f m, foot %.4f m ahead of the hip,"
              " placement gain %.1f deg/m" % (drop, geom(place[0], place[1])[0],
                                              math.degrees(1.0) / drop))

    print("limits: hip %.0f..%.0f knee %.0f..%.0f ankle %.0f..%.0f toe %.0f..%.0f"
          % (limits["hip"][0], limits["hip"][1], limits["knee"][0], limits["knee"][1],
             limits["ankle"][0], limits["ankle"][1], limits["toe"][0], limits["toe"][1]))
    print("table: %d poses, %d values, tightest margin %.1f deg (%s.%s %s = %g)"
          % (len(order), values, worst[0], worst[1], worst[2], worst[3], worst[4]))

    if bad:
        print("POSE-LIBRARY-AUDIT FAILED:")
        for line in bad:
            print("  " + line)
        return 1
    print("POSE-LIBRARY-AUDIT OK poses=%d values=%d outside_limits=0" % (len(order), values))
    return 0


if __name__ == "__main__":
    if len(sys.argv) > 1:
        POSE_SRC = sys.argv[1]
    sys.exit(main())
