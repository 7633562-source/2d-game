"""make_ref.py - phase-averaged Unity trot joint profiles from a take -> ref_trot.json.
Phase per leg from that leg's paw touchdowns (flag 0->1); profile over 50 bins, take window 3-33 s."""
import csv, json, sys, numpy as np
RUNS = "E:/Games/2D Game/dog454/Trials/Runs"
take = sys.argv[1] if len(sys.argv) > 1 else "cl505_x_s07.csv"
r = list(csv.DictReader(open(RUNS + "/" + take, encoding="utf-8")))
g = lambda k: np.array([float(x[k]) for x in r])
t = g("t"); w = (t >= 3) & (t <= 33)
NB = 50
out = {"take": take, "bins": NB}
legs = {"fr": ("frG", {"shoulder": "shoR", "elbow": "elbR"}), "fl": ("flG", {"shoulder": "shoL", "elbow": "elbL"}),
        "rr": ("rrG", {"hip": "hipR", "knee": "kneeR"}), "rl": ("rlG", {"hip": "hipL", "knee": "kneeL"})}
for leg, (fk, joints) in legs.items():
    f = g(fk) > 0.5
    td = np.where(w[1:] & f[1:] & ~f[:-1])[0] + 1
    per = np.diff(t[td]); ok = (per > 0.4) & (per < 1.2)
    ph = np.full(len(t), np.nan)
    for i in range(len(td) - 1):
        if ok[i]:
            a, b = td[i], td[i + 1]
            ph[a:b] = (t[a:b] - t[a]) / (t[b] - t[a])
    m = ~np.isnan(ph)
    bi = np.minimum((ph[m] * NB).astype(int), NB - 1)
    d = {"period": float(np.median(per[ok])), "duty": float(f[m].mean()), "n": int(ok.sum())}
    for jn, col in joints.items():
        v = g(col)[m]
        d[jn] = [float(np.mean(v[bi == k])) for k in range(NB)]
    out[leg] = d
    print(leg, "period %.3f duty %.2f n %d" % (d["period"], d["duty"], d["n"]), {k: (round(min(d[k]), 1), round(max(d[k]), 1)) for k in joints})
# lumbar vs FR phase
f = g("frG") > 0.5
td = np.where(w[1:] & f[1:] & ~f[:-1])[0] + 1
ph = np.full(len(t), np.nan)
for i in range(len(td) - 1):
    a, b = td[i], td[i + 1]
    if 0.4 < t[b] - t[a] < 1.2:
        ph[a:b] = (t[a:b] - t[a]) / (t[b] - t[a])
m = ~np.isnan(ph); bi = np.minimum((ph[m] * NB).astype(int), NB - 1); v = g("lumbar")[m]
out["lumbar_vs_fr"] = [float(np.mean(v[bi == k])) for k in range(NB)]
# phase offsets of touchdowns relative to FR
def tds(k):
    f = g(k) > 0.5; return t[np.where(w[1:] & f[1:] & ~f[:-1])[0] + 1]
T = out["fr"]["period"]; fr = tds("frG")
for k in ("flG", "rrG", "rlG"):
    o = []
    for x in tds(k):
        j = np.searchsorted(fr, x) - 1
        if 0 <= j < len(fr): o.append(((x - fr[j]) / T) % 1.0)
    o = np.array(o); ang = np.angle(np.mean(np.exp(2j * np.pi * o))) / (2 * np.pi) % 1.0
    out["offset_" + k[:2]] = float(ang); print("offset", k, round(float(ang), 3))
json.dump(out, open("ref_trot.json", "w"), indent=1)
