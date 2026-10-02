#!/usr/bin/env python
# Read-only: is the press's elbow activation throttled by its own ceiling?
# ARM cycle 11. Reads stored Trials/Runs/p_arms*.json + .csv only; no trial, no build.
#
# ArmDrive.Drive computes  signal = raw / (1 + damping)  with
#     damping = k * dt / Ieff,   k = dGain * flexor.maxTorque * Rad2Deg / speedRef
# so the press's torque ceiling (pressTorqueScale x the elbow base, 4 x 30 = 120 N.m)
# enters the PD's DAMPER, not only its authority: quadrupling maxTorque quadruples
# damping and throttles the very activation that has to press. If so, the press's
# own |actElbow| must fall as its ceiling rises - and the stored runs that differ
# only in -armPushScale can say it without a new trial.
#
# Usage: python Tools/arm-press-ceiling-check.py            (all p_arms* runs)
#        python Tools/arm-press-ceiling-check.py p_arms70 p_arms71
import sys, os, json, glob

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
RUNS = os.path.join(ROOT, "Trials", "Runs")


def args_of(js):
    a = js.get("commandArgs") or js.get("commandargs") or []
    if isinstance(a, str):
        a = a.split()
    return list(a)


def flag(args, name, default=None):
    if name in args:
        i = args.index(name)
        if i + 1 < len(args):
            return args[i + 1]
        return "1"
    return default


def load_csv(label):
    path = os.path.join(RUNS, label + ".csv")
    if not os.path.exists(path):
        return None, None, None
    with open(path, "r") as f:
        hdr = f.readline().rstrip("\n").rstrip("\r").split(";")
    idx = {n: i for i, n in enumerate(hdr)}
    rows = []
    with open(path, "r") as f:
        f.readline()
        for line in f:
            p = line.rstrip("\n").rstrip("\r").split(";")
            if len(p) == len(hdr):
                rows.append(p)
    return hdr, idx, rows


def f(row, idx, name):
    try:
        return float(row[idx[name]])
    except (KeyError, ValueError):
        return 0.0


def pearson(xs, ys):
    n = len(xs)
    if n < 3:
        return float("nan")
    mx = sum(xs) / n
    my = sum(ys) / n
    sxy = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    sxx = sum((x - mx) ** 2 for x in xs)
    syy = sum((y - my) ** 2 for y in ys)
    if sxx <= 0 or syy <= 0:
        return float("nan")
    return sxy / (sxx * syy) ** 0.5


def main():
    labels = sys.argv[1:]
    if not labels:
        labels = sorted(os.path.basename(p)[:-5] for p in glob.glob(os.path.join(RUNS, "p_arms*.json")))
    samples = []       # (label, build, side, scale, act, contact, ext)
    print("%-10s %-9s %-5s %-6s %5s %7s %8s %8s %8s %8s %8s" % (
        "run", "build", "push", "scale", "phL", "actEL", "actER", "contactL", "contactR", "extL", "extR"))
    for label in labels:
        jp = os.path.join(RUNS, label + ".json")
        if not os.path.exists(jp):
            continue
        try:
            js = json.load(open(jp))
        except Exception:
            continue
        hdr, idx, rows = load_csv(label)
        if rows is None:
            continue
        fp = str(js.get("buildFingerprint") or js.get("fingerprint") or js.get("buildGuid") or "?")[:8]
        a = args_of(js)
        push = flag(a, "-armPush", "1")
        scale = flag(a, "-armPushScale", "4")
        down_row = next((i for i, r in enumerate(rows) if f(r, idx, "down") > 0.5), None)
        if down_row is None:
            print("%-10s %-9s %-5s %-6s %5s  no down edge" % (label, fp, push, scale, "-"))
            continue
        win = rows[down_row:]
        out = []
        for side in ("L", "R"):
            press = [r for r in win if f(r, idx, "armPressPhase" + side) > 0]
            if not press:
                out += [0.0, 0.0, 0.0]
                continue
            act = sum(abs(f(r, idx, "actElbow" + side)) for r in press) / len(press)
            con = sum(1 for r in press if f(r, idx, "handContact" + side) > 0.5) / len(press)
            ext = max(f(r, idx, "armStrokeExt" + side) for r in win)
            samples.append((label, fp, side, float(scale), act, con, ext))
            out += [act, con, ext]
        ph_l = sum(1 for r in win if f(r, idx, "armPressPhaseL") > 0) / len(win)
        print("%-10s %-9s %-5s %-6s %5.2f %7.3f %8.3f %8.4f %8.4f %8.2f %8.2f" % (
            label, fp, push, scale, ph_l, out[0], out[3], out[1], out[4], out[2], out[5]))

    print("\nper-side samples (press ON only): %d" % len(samples))
    if samples:
        scale = [s[3] for s in samples]
        act = [s[4] for s in samples]
        con = [s[5] for s in samples]
        ext = [s[6] for s in samples]
        print("  ALL RUNS: corr(ceilingScale, actElbow)   = %+.3f" % pearson(scale, act))
        print("  ALL RUNS: corr(actElbow, palmContact)    = %+.3f" % pearson(act, con))
        print("  ALL RUNS: corr(actElbow, strokeExt)      = %+.3f" % pearson(act, ext))
        for build in sorted(set(s[1] for s in samples)):
            grp = [s for s in samples if s[1] == build]
            if len(grp) < 3:
                continue
            sc = [s[3] for s in grp]
            if len(set(sc)) < 2:
                continue
            print("  build %-9s n=%2d corr(ceilingScale, actElbow) = %+.3f  corr(actElbow, palmContact) = %+.3f   scales %s"
                  % (build, len(grp), pearson(sc, [s[4] for s in grp]),
                     pearson([s[4] for s in grp], [s[5] for s in grp]),
                     sorted(set(sc))))


if __name__ == "__main__":
    main()
