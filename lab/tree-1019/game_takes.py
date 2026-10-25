"""Read the game's own tree takes (read-only) and extract what the game does today:
joint-angle spectra (natural / forced frequencies), damping from free decay, and the
crow seat / release response (cl269_PR on the r12 tree, tree_probe_seg1017 on HEAD).
Output: game_takes.json + printed table."""
import json, os, sys
import numpy as np
RUNS = r"E:/Games/2D Game/My project/Trials/Runs"
HERE = os.path.dirname(os.path.abspath(__file__))

def load(name):
    p = os.path.join(RUNS, name + "_joints.csv")
    with open(p, encoding="utf-8-sig") as f:
        hdr = f.readline().strip().split(",")
    cols = [i for i, h in enumerate(hdr) if h == "t" or h.endswith("_angle")]
    A = np.loadtxt(p, delimiter=",", skiprows=1, usecols=cols)
    names = [hdr[i] for i in cols]
    return names, A

def spectrum(x, dt, fmax=20.0):
    x = x - x.mean()
    n = len(x)
    w = np.hanning(n)
    X = np.abs(np.fft.rfft(x * w)) ** 2
    f = np.fft.rfftfreq(n, dt)
    m = (f > 0.05) & (f < fmax)
    return f[m], X[m]

def peaks(f, P, k=3):
    idx = [i for i in range(1, len(P) - 1) if P[i] > P[i - 1] and P[i] >= P[i + 1]]
    idx.sort(key=lambda i: -P[i])
    return [round(float(f[i]), 3) for i in idx[:k]]

R = {}
for take in ("cl269_T0off", "cl269_T4", "cl269_T4w0", "cl269_PR", "tree_probe_seg1017"):
    try:
        names, A = load(take)
    except Exception as e:
        print(take, "ERR", e); continue
    t = A[:, 0]; dt = float(np.median(np.diff(t)))
    r = {"dt": dt, "T": float(t[-1]), "joints": {}}
    for j, nm in enumerate(names[1:], start=1):
        x = A[:, j]
        steady = t > 5.0
        f, P = spectrum(x[steady], dt)
        f0, P0 = spectrum(x[t < 4.0], dt) if np.sum(t < 4.0) > 100 else (f, P)
        r["joints"][nm] = dict(mean=float(x[steady].mean()), pkpk=float(np.ptp(x[steady])),
                               peaks_steady=peaks(f, P), peaks_start=peaks(f0, P0))
    R[take] = r
    print("==", take, "dt", dt, "T", round(t[-1], 1))
    for nm, v in r["joints"].items():
        print("  %-10s pkpk %8.4f deg  steady peaks %s  start peaks %s" % (nm, v["pkpk"], v["peaks_steady"], v["peaks_start"]))
    if take in ("cl269_PR", "tree_probe_seg1017"):
        seat = 13.09 if take == "cl269_PR" else 26.97
        rel = 25.04 if take == "cl269_PR" else None
        ev = {}
        for j, nm in enumerate(names[1:], start=1):
            x = A[:, j]
            pre = x[(t > seat - 1.0) & (t < seat)].mean()
            post = x[(t > seat + 1.5) & (t < seat + 2.5)].mean() if seat + 2.5 < t[-1] else x[t > seat + 0.5].mean()
            w = (t >= seat) & (t < seat + 1.0)
            peak = x[w][np.argmax(np.abs(x[w] - pre))] - pre if w.any() else 0
            d = dict(seat_step=float(post - pre), seat_peak=float(peak))
            if rel:
                pre2 = x[(t > rel - 1.0) & (t < rel)].mean()
                post2 = x[(t > rel + 1.5) & (t < rel + 2.5)].mean()
                w2 = (t >= rel) & (t < rel + 1.0)
                d["rel_step"] = float(post2 - pre2)
                d["rel_peak"] = float(x[w2][np.argmax(np.abs(x[w2] - pre2))] - pre2)
            ev[nm] = d
        r["seat"] = ev
        print("  seat/release (deg):")
        for nm, d in ev.items():
            if max(abs(v) for v in d.values()) > 0.01:
                print("   ", nm, {k: round(v, 3) for k, v in d.items()})
json.dump(R, open(os.path.join(HERE, "game_takes.json"), "w"), indent=1)
