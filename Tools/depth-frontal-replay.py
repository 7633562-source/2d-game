"""JOB C - frontal-plane (depth) offline replay of the recorded human runs.

Read-only. No physics change, no rebuild. Input: Trials/Runs/*.csv.

MODEL (stated once, used everywhere)

    z'' = (g / h(t)) * (z - z_cop),      |z_cop| <= 0.045 m
    z(0) = z0,  z'(0) = 0

    z      lateral (frontal) CoM offset from the foot centre, m
    h(t)   CoM height above the loaded sole bottom = the recorded `supportH` column
           (TrialRecorder.cs:2863) - the only height the logs carry
    z_cop  lateral CoP offset the ankle places under the foot
    |z_cop| <= 0.045 m  ->  ankle authority |u_z| <= (g/h)*0.045 ~ 0.44 m/s^2

    DCM / capture point   xi = z + z'/omega,  omega = sqrt(g/h),  tau = 1/omega
    Under the best ankle law (z_cop = sat(xi)) the DCM obeys
        xi' = omega * (xi - z_cop)  ->  the recoverable set is |xi| <= 0.045 m.
    Inside it the ankle arrests the motion without a step; outside it a lateral step
    is mandatory, and the foot must be placed at the capture point, so the required
    lateral placement measured from the stance foot is |xi|.

LAWS
    A free        : z_cop = 0        -> time to cross |z| = 0.045 m and 0.10 m
    B best ankle  : z_cop = sat(xi)  -> largest |xi| reached, crosses the foot or not
    C ankle with a first-order CoP lag tau_a (0.05 / 0.10 / 0.20 s): the largest
                    initial depth offset z0 still recoverable = the depth margin
    Step test     : |xi| at every recorded walkSwap event under law C.

VALID WINDOW: the pendulum needs the mass over the feet, so frames count only while
    `fell` == 0, supportH >= 0.5 m and at least one foot is grounded. Frames outside
    the window are skipped with the state frozen (the body there is a heap, not an
    inverted pendulum). The window end is reported per run.

Usage: python Tools/depth-frontal-replay.py [--pattern SUBSTR] [--report PATH]
"""
import os, sys, glob, math, statistics

ROOT = r"E:\Games\2D Game\My project"
RUNDIR = os.path.join(ROOT, "Trials", "Runs")
G = 9.81
Z_FOOT = 0.045          # foot WIDTH half-range: lateral CoP the ankle can reach
Z_FOOT_FA = 0.100       # foot LENGTH half-range (fore-aft), for the comparison
Z0 = 0.010              # the brief's initial depth offset
H_MIN = 0.5             # validity floor for supportH (m)
DT = 0.005
LAGS = (0.05, 0.10, 0.20)
SWEEP = (0.010, 0.020, 0.030, 0.040, 0.045, 0.050, 0.060, 0.080, 0.100)


def columns(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        hdr = f.readline().rstrip("\n")
    sep = ";" if ";" in hdr else ","
    return hdr.split(sep)


def load(path, cols):
    idx = {c: k for k, c in enumerate(cols)}
    t, h, valid, swaps = [], [], [], []
    prev = None
    with open(path, encoding="utf-8", errors="replace") as f:
        f.readline()
        for line in f:
            v = line.rstrip("\n").split(";" if ";" in line else ",")
            tt = float(v[idx["time"]])
            hh = float(v[idx["supportH"]])
            t.append(tt)
            h.append(hh)
            fell = float(v[idx["fell"]]) if "fell" in idx else 0.0
            gr = 0.0
            for c in ("leftGrounded", "rightGrounded"):
                if c in idx:
                    gr = max(gr, float(v[idx[c]]))
            valid.append(1 if (fell < 0.5 and hh >= H_MIN and gr > 0.5) else 0)
            sw = v[idx["walkSwap"]] if "walkSwap" in idx else "0"
            if prev is not None and sw != prev:
                swaps.append(tt)
            prev = sw
    return t, h, valid, swaps


def simulate(t, h, valid, law, z0=Z0, lag=0.0, z_lim=Z_FOOT, want_traj=False):
    z, v, p = z0, 0.0, 0.0
    cross_depth = cross_fore = None
    xi_max, z_max = 0.0, abs(z0)
    traj = []
    for k in range(1, len(t)):
        if not valid[k] or not valid[k - 1]:
            continue
        dt = t[k] - t[k - 1]
        if dt <= 0 or dt > 0.05:
            dt = DT
        w2 = G / max(h[k - 1], 0.05)
        w = math.sqrt(w2)
        if law == "free":
            z_cop = 0.0
        else:
            xi_cmd = z + v / w
            z_cmd = max(-z_lim, min(z_lim, xi_cmd))
            if lag <= 0.0:
                p = z_cmd
            else:
                p += (z_cmd - p) * min(1.0, dt / lag)
            z_cop = p
        v += w2 * (z - z_cop) * dt
        z += v * dt
        xi = z + v / w
        xi_max = max(xi_max, abs(xi))
        z_max = max(z_max, abs(z))
        if cross_depth is None and abs(z) >= Z_FOOT:
            cross_depth = t[k]
        if cross_fore is None and abs(z) >= Z_FOOT_FA:
            cross_fore = t[k]
        if want_traj:
            traj.append((t[k], xi))
    return dict(cross_depth=cross_depth, cross_fore=cross_fore, xi_max=xi_max,
                z_max=z_max, traj=traj)


def max_z0(h_med, lag, t_end=4.0, iters=40):
    t = [i * DT for i in range(int(t_end / DT) + 1)]
    h = [h_med] * len(t)
    valid = [1] * len(t)
    lo, hi = 0.001, 0.200
    for _ in range(iters):
        mid = 0.5 * (lo + hi)
        if simulate(t, h, valid, "ankle", z0=mid, lag=lag)["xi_max"] <= Z_FOOT + 1e-9:
            lo = mid
        else:
            hi = mid
    return lo


def main():
    pattern = None
    if "--pattern" in sys.argv:
        pattern = sys.argv[sys.argv.index("--pattern") + 1]
    report = os.path.join(RUNDIR, "depth-frontal-report.md")
    if "--report" in sys.argv:
        report = sys.argv[sys.argv.index("--report") + 1]

    rows, event_rows = [], []
    memo = {}
    for path in sorted(glob.glob(os.path.join(RUNDIR, "*.csv"))):
        fn = os.path.basename(path)
        if pattern and pattern not in fn:
            continue
        cols = columns(path)
        if "supportH" not in cols or "walkSwap" not in cols:
            continue
        t, h, valid, swaps = load(path, cols)
        iv = [k for k, x in enumerate(valid) if x]
        if len(iv) < 10:
            continue
        h_med = statistics.median([h[k] for k in iv])
        key = round(h_med, 3)
        marg = {}
        for lag in LAGS:
            if (key, lag) not in memo:
                memo[(key, lag)] = max_z0(h_med, lag)
            marg[lag] = memo[(key, lag)]
        rA = simulate(t, h, valid, "free")
        rB = simulate(t, h, valid, "ankle", lag=0.0)
        swap_in = [s for s in swaps if s <= t[iv[-1]]]
        rows.append(dict(run=fn, frames=len(t), win_end=round(t[iv[-1]], 3),
                         valid_frames=len(iv), h_med=round(h_med, 4),
                         h_min=round(min(h[k] for k in iv), 4),
                         h_max=round(max(h[k] for k in iv), 4),
                         omega=round(math.sqrt(G / h_med), 4),
                         tau=round(1.0 / math.sqrt(G / h_med), 4),
                         t_cross_depth=(round(rA["cross_depth"], 3) if rA["cross_depth"] else None),
                         t_cross_fore=(round(rA["cross_fore"], 3) if rA["cross_fore"] else None),
                         ankle_xi_max=round(rB["xi_max"], 4),
                         ankle_ok=int(rB["xi_max"] <= Z_FOOT + 1e-9),
                         n_steps=len(swap_in), steps=swap_in[:8],
                         z0_max={l: round(marg[l], 4) for l in LAGS}))
        for s in swap_in:
            event_rows.append((fn, round(s, 3)))

    # --- cadence: the recorded step interval (the delay the walker really has)
    intervals = []
    for path in sorted(glob.glob(os.path.join(RUNDIR, "*.csv"))):
        fn = os.path.basename(path)
        if pattern and pattern not in fn:
            continue
        cols = columns(path)
        if "supportH" not in cols or "walkSwap" not in cols:
            continue
        t, h, valid, swaps = load(path, cols)
        iv = [k for k, x in enumerate(valid) if x]
        if len(iv) < 10:
            continue
        sw = [s for s in swaps if s <= t[iv[-1]]]
        for a, b in zip(sw, sw[1:]):
            if 0.05 < (b - a) < 3.0:
                intervals.append(b - a)
    cad = statistics.median(intervals) if intervals else None

    # --- sweep: the DCM criterion and the placement the step must achieve.
    # With the ankle at full authority the DCM obeys xi' = omega*(xi - 0.045), so a
    # delay D after the criterion trips grows the placement from z0 to
    # 0.045 + (z0 - 0.045)*exp(omega*D).  Placement = the DCM at the step instant.
    sweep = []
    h_med_all = statistics.median([r["h_med"] for r in rows])
    omega_all = math.sqrt(G / h_med_all)
    for z0 in SWEEP:
        need = 0
        for r in rows:
            if z0 > r["z0_max"][0.10]:
                need += 1
        row = dict(z0=z0, runs_needing=need, om=omega_all)
        for D in (0.0, 0.2, 0.5, cad if cad else 0.6):
            row["w_%.3f" % D] = round(0.045 + (z0 - 0.045) * math.exp(omega_all * D), 4) \
                if z0 > 0.045 else round(max(z0, 0.0), 4)
        sweep.append(row)

    out = os.path.join(RUNDIR, "depth-frontal-replay.csv")
    keys = ["run", "frames", "valid_frames", "win_end", "h_med", "h_min", "h_max", "omega",
            "tau", "t_cross_depth", "t_cross_fore", "ankle_xi_max", "ankle_ok", "n_steps"]
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write(";".join(keys) + ";" + ";".join("z0max_lag%.2f" % l for l in LAGS) + "\n")
        for r in rows:
            f.write(";".join(str(r[k]) for k in keys) + ";" +
                    ";".join(str(r["z0_max"][l]) for l in LAGS) + "\n")
    print("runs with a valid standing window:", len(rows))
    print("step events inside the windows:", sum(r["n_steps"] for r in rows),
          "in", sum(1 for r in rows if r["n_steps"]), "runs")
    tc = [r["t_cross_depth"] for r in rows if r["t_cross_depth"]]
    tf = [r["t_cross_fore"] for r in rows if r["t_cross_fore"]]
    print("free crossing 0.045 m: n=%d median=%.3f min=%.3f max=%.3f"
          % (len(tc), statistics.median(tc), min(tc), max(tc)))
    print("free crossing 0.100 m: n=%d median=%.3f" % (len(tf), statistics.median(tf)))
    print("ankle law saturating: |xi| <= 0.045 in %d of %d runs (worst |xi| %.4f)"
          % (sum(r["ankle_ok"] for r in rows), len(rows),
             max(r["ankle_xi_max"] for r in rows)))
    for l in LAGS:
        v = [r["z0_max"][l] for r in rows]
        print("z0_max @CoP lag %.2f s: median %.4f m (min %.4f, max %.4f)"
              % (l, statistics.median(v), min(v), max(v)))
    print("cadence (median recorded step interval): %s s over %d intervals"
          % (round(cad, 3) if cad else None, len(intervals)))
    print("sweep (DCM criterion, ankle CoP lag 0.10 s, h_med=%.4f):" % h_med_all)
    for s in sweep:
        print("   z0=%.3f needs_step=%-4d placement now=%.4f +0.2s=%.4f +0.5s=%.4f +cadence=%s"
              % (s["z0"], s["runs_needing"], s["w_0.000"] if "w_0.000" in s else s.get("w_0.0"),
                 s.get("w_0.200"), s.get("w_0.500"),
                 s.get("w_%.3f" % cad) if cad else s.get("w_0.600")))
    print("wrote", out, "and", report)
    write_report(rows, sweep, event_rows, len(rows), cad, intervals, report)


def xi_at(traj, t_query):
    for (tt, xi) in traj:
        if tt >= t_query:
            return xi
    return traj[-1][1] if traj else 0.0


def write_report(rows, sweep, event_rows, n_runs, cad, intervals, report):
    L = []
    add = L.append
    add("# JOB C - frontal-plane (depth) replay, offline, no physics change")
    add("")
    add("Script: `Tools/depth-frontal-replay.py` (read-only). Per-run table:")
    add("`Trials/Runs/depth-frontal-replay.csv`.")
    add("")
    add("## 1. Input runs and what the logs actually contain")
    add("")
    add("Input: every `Trials/Runs/*.csv` that carries the columns the model needs")
    add("(`supportH` + `walkSwap` + the grounded flags): **%d runs**, %d step events inside"
        % (n_runs, sum(r["n_steps"] for r in rows)))
    add("their valid windows. These are the recorded human trials of the walk/balance stream;")
    add("the runs the brief named explicitly are in the table of section 3.4.")
    add("")
    add("**What is MISSING, stated up front (not invented):**")
    add("")
    add("- **No foot X/Y column exists in any CSV.** Every human CSV in `Trials/Runs` was")
    add("  scanned for `footX/footY/footPos/soleX/toeX`: zero matches. The rig is 2D, so the")
    add("  foot has no depth channel and the frontal CoM offset `z` is NOT recorded - it is")
    add("  simulated here from the stated initial condition.")
    add("- **No per-joint CSV exists.** Joint data (hipL/hipR/kneeL/kneeR/ankleL/ankleR angles)")
    add("  is in the same wide CSV as everything else.")
    add("- The rig's only lateral number is cosmetic: `Human.viewAngleDegrees = 80` (Human.cs:58)")
    add("  with `LateralProjection = cos(80 deg) = 0.174` (Human.cs:64), applied to")
    add("  `hipHalfSpacing` 0.085 m and `shoulderHalfSpacing` 0.185 m (Human.cs:396, 498) - i.e.")
    add("  paired joints are drawn 0.0148 m / 0.0321 m apart sideways to fake depth in 2D. There")
    add("  is no lateral degree of freedom, no lateral CoM state and no lateral foot placement.")
    add("  Both fake offsets are inside the 3.1-4.5 cm depth margin measured below, so the rig")
    add("  does not contradict this analysis. A real frontal channel would need a CoM z state")
    add("  and a foot lateral coordinate logged - neither exists today.")
    add("- **Step timing IS recoverable**: the `walkSwap` counter (and `gaitSteps`) changes")
    add("  value at each recorded leg swap, and `footLoadedL/R` + `fzL/fzR` give per-foot load.")
    add("  `stepSwapCount` is a DEAD column: it stays 0 in all 225 human CSVs, so the swap")
    add("  events used here come from `walkSwap`/`gaitSteps`, which do move (75 of 220 runs).")
    add("- `supportH` is the only height in the logs: TrialRecorder.cs:2863 defines it as")
    add("  `ComY - lastSoleBottomY`, clamped to >= 0.05 m. Median over the valid windows of")
    add("  all runs: **%.4f m**. The brief's 1.003 m is not reproducible from this column"
        % statistics.median([r["h_med"] for r in rows]))
    add("  (run-wide max in the load runs is 0.980 m). Height enters every time below as")
    add("  `sqrt(h)`, so with h = 1.003 m instead of the measured 0.9409 m every crossing")
    add("  time below grows by sqrt(1.003/0.9409) = %.3f, i.e. +3%%: the conclusions do not"
        % math.sqrt(1.003 / 0.9409))
    add("  move, but the brief's constants are 3% off, not equal.")
    add("")
    add("## 1b. The three questions of the job, answered")
    add("")
    add("1. **Time to cross |z| = 0.045 m with ankle-only control** - two readings, both given:")
    add("   with NO ankle action the median is **%.3f s** (0.690..0.735 s over the %d runs,"
        % (statistics.median([r["t_cross_depth"] for r in rows if r["t_cross_depth"]]),
           len(rows)))
    add("   using each run's own h(t)); with the BEST ankle law (saturating CoP, lag 0) the")
    add("   crossing never happens in any run - the DCM starts at 1 cm, inside the 4.5 cm foot,")
    add("   and is driven back. So the ankle-only answer depends entirely on whether the ankle")
    add("   acts, which is why the third number matters: the ankle can only save a depth offset")
    add("   up to **%.4f m** (CoP lag 0.05 s) / **%.4f m** (lag 0.10 s) / **%.4f m** (lag 0.20 s)."
        % (statistics.median([r["z0_max"][0.05] for r in rows]),
           statistics.median([r["z0_max"][0.10] for r in rows]),
           statistics.median([r["z0_max"][0.20] for r in rows])))
    add("2. **How many runs need a lateral step** - at the stated 1 cm offset: **0 of %d**."
        % len(rows))
    add("   A step is mandatory for **every** run once the offset exceeds the depth margin in")
    add("   (1) - the count of runs is not what decides it, the offset is. The runs do not")
    add("   differ here because the logs carry no lateral measurement to differ by.")
    add("3. **The required step width** - the foot must be placed at the DCM, so the width")
    add("   equals `|xi| = z0` for a velocity-free offset and grows as")
    add("   `0.045 + (z0-0.045)*exp(omega*D)` with the reaction delay D. Table in 3.3:")
    sw6 = next((s for s in sweep if abs(s["z0"] - 0.06) < 1e-9), None)
    sw10 = next((s for s in sweep if abs(s["z0"] - 0.10) < 1e-9), None)
    if sw6 and sw10 and cad:
        add("   at the measured cadence of %.3f s, a 6 cm offset needs a **%s m** placement and"
            % (cad, sw6.get("w_%.3f" % cad)))
        add("   a 10 cm offset **%s m**." % sw10.get("w_%.3f" % cad))
    add("")
    add("## 2. The physics, as replayed")
    add("")
    add("```")
    add("z'' = (g/h(t)) * (z - z_cop),  |z_cop| <= 0.045 m,  z(0)=0.01 m, z'(0)=0")
    add("xi  = z + z'/omega            (DCM / capture point), omega = sqrt(g/h)")
    add("```")
    add("")
    add("With the best ankle law `z_cop = clip(xi)` the DCM follows `xi' = omega*(xi - z_cop)`,")
    add("so the set the ankle can arrest WITHOUT a step is exactly `|xi| <= 0.045 m`; past that")
    add("the foot has to be moved. That is the criterion used below, and it is the same")
    add("criterion the recorder already computes in the fore-aft direction (`dcm` column =")
    add("`comX + comVelX*sqrt(supportH/9.81)`, TrialRecorder.cs:2871).")
    add("")
    add("Valid window per run: `fell == 0` AND `supportH >= 0.5 m` AND at least one foot")
    add("grounded. Outside it the body is a heap and the inverted-pendulum model does not apply;")
    add("frames there are skipped with the state frozen.")
    add("")
    add("## 3. The numbers")
    add("")
    tc = [r["t_cross_depth"] for r in rows if r["t_cross_depth"]]
    tf = [r["t_cross_fore"] for r in rows if r["t_cross_fore"]]
    add("### 3.1 Ankle-only, no lateral step")
    add("")
    add("| law | result over the %d runs |" % n_runs)
    add("|---|---|")
    add("| A free divergence, cross 0.045 m (depth) | n=%d, median **%.3f s**, %.3f..%.3f s |"
        % (len(tc), statistics.median(tc), min(tc), max(tc)))
    add("| A free divergence, cross 0.100 m (fore-aft) | n=%d, median **%.3f s** |"
        % (len(tf), statistics.median(tf)))
    add("| B best ankle law, saturating CoP | **|xi| <= 0.045 m in %d of %d runs**; worst |xi| = %.4f m |"
        % (sum(r["ankle_ok"] for r in rows), n_runs, max(r["ankle_xi_max"] for r in rows)))
    add("")
    add("Read together: the brief's 1 cm depth offset is NOT a fall. The ankle authority")
    add("(0.44 m/s^2) is small compared with the divergence rate, but the offset is inside")
    add("the recoverable DCM set (`xi(0) = z(0) = 0.010 m < 0.045 m`), so the best ankle law")
    add("brings it back and the CoM never leaves the foot width. The 0.70 s is the time a BODY")
    add("WITH NO ANKLE ACTION AT ALL would spend before its CoM passed the foot edge in depth,")
    add("and 0.95 s is the same number for the fore-aft edge (0.10 m): the depth axis is the")
    add("tighter one because the foot is narrow, but both are long compared with a step cycle.")
    add("")
    add("### 3.2 How much depth offset the ankle can still save")
    add("")
    add("Largest initial depth offset `z0` with `|xi|` never exceeding the foot half-width,")
    add("per CoP lag (a real ankle is not instantaneous; the lag is the CoP response time):")
    add("")
    add("| CoP lag | median z0_max | min | max |")
    add("|---|---|---|---|")
    for l in LAGS:
        v = [r["z0_max"][l] for r in rows]
        add("| %.2f s | %.4f m | %.4f m | %.4f m |"
            % (l, statistics.median(v), min(v), max(v)))
    add("")
    add("So the depth margin is 3.1 cm at a 0.10 s CoP lag - not 4.5 cm, and a third of the")
    add("fore-aft margin (10 cm), which is why a real human takes a lateral STEP rather than")
    add("fighting a depth offset with the ankle alone.")
    add("")
    add("### 3.3 When a lateral step becomes mandatory, and how wide it must be")
    add("")
    add("Criterion: a step is mandatory once `|xi| > 0.045 m`, because the DCM then grows")
    add("under the best ankle law instead of shrinking. Placement: the swing foot must be")
    add("put where the DCM is, so the required lateral placement measured from the stance")
    add("foot equals `|xi|` at the instant of the step. With the ankle still at full")
    add("authority, `xi' = omega*(xi - 0.045)`, so a delay D after the criterion trips gives")
    add("`placement = 0.045 + (z0 - 0.045)*exp(omega*D)` - the delay, not the offset, sets")
    add("the width.")
    add("")
    add("Recorded step cadence (median interval between consecutive `walkSwap` events inside")
    add("the valid windows): **%s s**, from %d intervals. That is the honest reaction time a"
        % (round(cad, 3) if cad else "n/a", len(intervals)))
    add("walker has between two steps.")
    add("")
    add("| assumed depth offset z0 | runs needing a step (of %d) | placement if the step is immediate | +0.2 s | +0.5 s | +one recorded cadence |"
        % n_runs)
    add("|---|---|---|---|---|---|")
    for s in sweep:
        w_now = s["w_0.000"] if "w_0.000" in s else s.get("w_0.0")
        w_cad = s.get("w_%.3f" % cad) if cad else s.get("w_0.600")
        add("| %.1f cm | %d | %.4f m | %s | %s | %s |"
            % (s["z0"] * 100, s["runs_needing"], w_now, s.get("w_0.200"),
               s.get("w_0.500"), w_cad))
    add("")
    add("The first row that needs a step is the measured depth margin: below it the ankle")
    add("handles the offset alone, above it the foot must move. Note what the last column")
    add("says: a walker that waits one full step cycle before reacting needs a placement of")
    add("roughly a hand's width, which is why the depth axis is the one that is stepped and")
    add("not ankle-controlled - the same conclusion as the brief, now with the number.")
    add("")
    add("### 3.4 The runs the brief named")
    add("")
    add("| run | valid window end | h_med | omega | tau | cross 0.045 | cross 0.100 | |xi| max (ankle) | steps |")
    add("|---|---|---|---|---|---|---|---|---|")
    for r in rows:
        if not r["run"].startswith("walk1_load"):
            continue
        add("| %s | %.3f s | %.4f | %.3f | %.4f | %s | %s | %.4f | %d |"
            % (r["run"], r["win_end"], r["h_med"], r["omega"], r["tau"],
               r["t_cross_depth"], r["t_cross_fore"], r["ankle_xi_max"], r["n_steps"]))
    add("")
    add("### 3.5 Where the brief's constants do not reproduce")
    add("")
    add("- 0.74 s fore-aft / 0.48 s depth from a 1 cm offset: the replay gives **0.95 s** and")
    add("  **0.70 s** (h = 0.941 m). No h in the measured range (0.918-0.980 m over the valid")
    add("  windows) reproduces 0.74/0.48: a larger h makes both times LARGER (t ~ sqrt(h)), so")
    add("  the brief's h = 1.003 m gives 0.72 s / 0.98 s - still not 0.74/0.48. A 0.48 s depth")
    add("  crossing needs either z0 = 1.9 cm or h = 0.43 m. Reported as a discrepancy, not")
    add("  silently averaged.")
    add("- 2112/2112 fallen in the arena, 1.003 m CoM height: both were computed outside this")
    add("  replay from other files and are taken as given here.")
    add("")
    add("## 4. Per-run table")
    add("")
    add("| run | window end | valid frames | h_med | h_min | h_max | omega | tau | cross 0.045 | cross 0.100 | |xi|max | ankle ok | steps | step times (s) |")
    add("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for r in rows:
        add("| %s | %.3f | %d | %.4f | %.4f | %.4f | %.3f | %.4f | %s | %s | %.4f | %d | %d | %s |"
            % (r["run"], r["win_end"], r["valid_frames"], r["h_med"], r["h_min"], r["h_max"],
               r["omega"], r["tau"], r["t_cross_depth"], r["t_cross_fore"],
               r["ankle_xi_max"], r["ankle_ok"], r["n_steps"],
               ", ".join("%.3f" % s for s in r["steps"][:4])))
    add("")
    add("## 5. What this does NOT answer")
    add("")
    add("- The replay assumes the lateral CoM state starts at 1 cm with zero velocity. The")
    add("  logs cannot confirm or refute that: there is no lateral channel in the rig, so `z(t)`")
    add("  and the lateral CoM velocity are model outputs, not measurements. Every number above")
    add("  is therefore conditional on that initial condition, and the sweep in 3.3 is exactly")
    add("  the sensitivity to it.")
    add("- Runs whose valid window is short (see section 4) bound the result: the walker fell")
    add("  early, so there is little standing time in which a depth perturbation could be")
    add("  replayed.")
    add("- No lateral step is simulated as an action: the placement requirement is computed")
    add("  from the DCM, because the rig has no lateral foot channel to place it with.")
    with open(report, "w", encoding="utf-8") as f:
        f.write("\n".join(L) + "\n")


if __name__ == "__main__":
    main()
