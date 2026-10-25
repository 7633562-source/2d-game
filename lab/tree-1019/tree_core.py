"""tree-1019 core: planar articulated tree (joint space) + Box2D-style maximal-coordinate solver.

Conventions (same as cache/tree-1018/limb_model.py, which reproduced r10/r12 within ~5 %):
  * body b = a rod of length L[b], width W[b], mass M[b], inertia I[b] about its centre;
  * absolute angle phi_b = HEAD_b + sum of joint offsets q over b's ancestors-or-self;
    rod direction u = (-sin phi, cos phi) (heading > 0 leans the rod to -x);
  * joint j sits at the base of body j; parent < 0 means a static anchor (stump / static trunk).
  * dt = Time.fixedDeltaTime = 0.005 s (GameProcess.cs:134, TimeManager.asset).
Wind law = PlantTree.ApplyWind (q = 0.5*1.2*windScale 0.12, area = w*L*clamp(pic)*WindSail(depth),
HeadingSailMul 1 -> 1.22), leaves = r12 ApplyLeafWind (Vogel area / sqrt(1+(u/5)^2), relative speed).
"""
import math
import numpy as np

G = 9.81
DT = 0.005
Q_DYN = 0.5 * 1.2 * 0.12          # 1/2 rho * windScale (PlantTree.windScale 0.12)
PIC = {0: 0.79, 1: 0.719, 2: 0.683, 3: 0.640}


def wind_sail(d):
    if d <= 0:
        return 0.18
    return 0.70 + 0.55 * d + (0.85 if d >= 2 else 0.25)


class Tree:
    """Mutable builder, then finish() freezes arrays."""

    def __init__(self, name):
        self.name = name
        self.P, self.L, self.W, self.M, self.I, self.HEAD, self.ORD = [], [], [], [], [], [], []
        self.K, self.C, self.MT, self.LO, self.HI, self.AREA, self.ROOT, self.TAG = [], [], [], [], [], [], [], []
        self.pts = []           # foliage / extra point masses: (body, frac, mass, area)

    def add(self, parent, L, W, m, I, head_deg, order, k, c, maxT, lo_deg, hi_deg, area=None,
            root=(0.0, 0.22), tag=""):
        self.P.append(parent); self.L.append(L); self.W.append(W); self.M.append(m); self.I.append(I)
        self.HEAD.append(math.radians(head_deg)); self.ORD.append(order)
        self.K.append(k); self.C.append(c); self.MT.append(maxT)
        self.LO.append(math.radians(lo_deg)); self.HI.append(math.radians(hi_deg))
        if area is None:
            area = W * L * min(1.0, max(0.45, PIC[min(order, 3)])) * wind_sail(order)
        self.AREA.append(area); self.ROOT.append(root); self.TAG.append(tag)
        return len(self.P) - 1

    def finish(self):
        for nm in ("P", "L", "W", "M", "I", "HEAD", "ORD", "K", "C", "MT", "LO", "HI", "AREA"):
            setattr(self, nm, np.array(getattr(self, nm), dtype=float if nm not in ("P", "ORD") else int))
        self.ROOT = np.array(self.ROOT, float)
        n = self.n = len(self.P)
        A = np.zeros((n, n))
        for b in range(n):
            x = b
            while x >= 0:
                A[b, x] = 1.0
                x = self.P[x]
        self.A = A                       # ancestor-or-self
        self.S = A - np.eye(n)           # strict ancestors
        self.children = [[c for c in range(n) if self.P[c] == b] for b in range(n)]
        self.leaf_bodies = [b for b in range(n) if not self.children[b]]
        self.rest = self.kin(np.zeros(n))
        self.hold = -self.grav_Q(np.zeros(n), self.pts)
        return self

    # ---------- kinematics ----------
    def kin(self, q):
        phi = self.HEAD + self.A @ q
        u = np.stack([-np.sin(phi), np.cos(phi)], 1)
        e = self.L[:, None] * u
        rootpt = self.ROOT.copy()
        base = rootpt + self.S @ e        # roots of all chains share their own ROOT (static anchor)
        # for bodies with a dynamic parent ROOT is the chain root anchor: base = anchor + sum ancestors
        return phi, base, e

    def point_pos(self, q, bodies, fracs):
        phi, base, e = self.kin(q)
        return base[bodies] + fracs[:, None] * e[bodies], phi, base, e

    def jac(self, P, bodies, base):
        """Point Jacobians (n_pts, 2, n): dP/dq_a = A[b,a] * R90(P - base_a)."""
        r = P[:, None, :] - base[None, :, :]
        Ab = self.A[bodies]
        return np.stack([-r[..., 1] * Ab, r[..., 0] * Ab], 1)

    def mass_points(self, pts):
        b = list(range(self.n)) + [p[0] for p in pts]
        f = [0.5] * self.n + [p[1] for p in pts]
        m = list(self.M) + [p[2] for p in pts]
        return np.array(b, int), np.array(f, float), np.array(m, float)

    def mass_matrix(self, q, pts, Ivec=None):
        Ivec = self.I if Ivec is None else Ivec
        bodies, fracs, ms = self.mass_points(pts)
        P, phi, base, e = self.point_pos(q, bodies, fracs)
        J = self.jac(P, bodies, base)
        Mm = np.einsum("p,pin,pim->nm", ms, J, J)
        Mm += self.A.T @ (Ivec[:, None] * self.A)
        return Mm

    def grav_Q(self, q, pts):
        bodies, fracs, ms = self.mass_points(pts)
        P, phi, base, e = self.point_pos(q, bodies, fracs)
        J = self.jac(P, bodies, base)
        return -G * (ms @ J[:, 1, :])

    def hessian(self, pts, eps=1e-5):
        n = self.n
        H = np.zeros((n, n))
        for i in range(n):
            d = np.zeros(n); d[i] = eps
            H[:, i] = -(self.grav_Q(d, pts) - self.grav_Q(-d, pts)) / (2 * eps)
        return 0.5 * (H + H.T)

    def subtree(self, b):
        return [x for x in range(self.n) if self.A[x, b] > 0]

    def reach(self, b):
        """Largest distance from joint b to a tip in its subtree, at the rest pose."""
        phi, base, e = self.rest
        tips = base[self.subtree(b)] + e[self.subtree(b)]
        return float(np.max(np.linalg.norm(tips - base[b], axis=1)))


# ---------- inertia rules ----------
def box_inertia(m, L, W):
    return m * (L * L + W * W) / 12.0


def inertia_rule(tree, rule, floor=0.008, wdt=0.5, amax=1.0, iters=6):
    """'true'  : box inertia, game floor minSegmentInertia (0.008 HEAD; pass floor).
       'r12'   : bole pieces raised to k dt^2 / 0.25 (fleet TuneBoleChain, about the centre).
       'hinge' : any piece raised so that k dt^2 / (I + m (L/2)^2) <= wdt^2 (hinge-aware)."""
    I = np.array([max(box_inertia(tree.M[b], tree.L[b], tree.W[b]), floor) for b in range(tree.n)])
    for b in range(tree.n):
        k = tree.K[b]
        if rule == "r12" and tree.ORD[b] == 0:
            I[b] = max(I[b], k * DT * DT / 0.25)
        elif rule == "hinge":
            need = k * DT * DT / (wdt * wdt) - tree.M[b] * (0.5 * tree.L[b]) ** 2
            I[b] = max(I[b], need)
    if rule == "pair":
        # free-pair explicit-spring bound a = k dt^2 (1/I_child + 1/I_parent) <= amax (fleet r12: 0.25 on the
        # bole only). Raise the lighter side first; static parent = infinite inertia.
        for _ in range(iters):
            for b in range(tree.n):
                p = tree.P[b]
                a = tree.K[b] * DT * DT * (1.0 / I[b] + (1.0 / I[p] if p >= 0 else 0.0))
                if a <= amax:
                    continue
                if p < 0:
                    I[b] = tree.K[b] * DT * DT / amax
                else:
                    need = tree.K[b] * DT * DT / amax          # 1/I_eff target
                    lo_ = b if I[b] <= I[p] else p
                    other = p if lo_ == b else b
                    rest = 1.0 / need - 1.0 / I[other]
                    I[lo_] = 1.0 / rest if rest > 0 else 2.0 * need
                    if rest <= 0:
                        I[other] = max(I[other], 2.0 * need)
    return I


# ---------- wind ----------
class WindField:
    """TreeWindField spec (req-287 round 14) + Poisson gust fronts (tree-1017b), seeded.
    u(t,x,y) = max(0.2, uMean*(y/yRef)^alpha + slow(t') + fast(t') + fronts(t,x)),
    t' = t - (x - xUp)/uAdv (Taylor frozen turbulence)."""

    def __init__(self, seed, seconds=70.0, uMean=2.6, sigSlow=1.4, tauSlow=3.0, sigFast=0.5, tauFast=0.5,
                 uAdv=3.0, xUp=-2.0, alpha=0.15, yRef=2.0, fronts=False, front_mean_s=12.0, dtw=0.01,
                 front_amp=(2.0, 4.0), sigTurb=0.0, tauTurb=0.1, nTurb=8):
        rng = np.random.default_rng(seed)
        n = int((seconds + 8.0) / dtw) + 2
        self.dtw, self.n, self.uMean, self.alpha, self.yRef, self.uAdv, self.xUp = dtw, n, uMean, alpha, yRef, uAdv, xUp

        def ou(sig, tau):
            a = math.exp(-dtw / tau); b = sig * math.sqrt(1 - a * a)
            x = np.empty(n); x[0] = rng.normal() * sig
            e = rng.normal(size=n)
            for i in range(1, n):
                x[i] = a * x[i - 1] + b * e[i]
            return x

        self.slow = ou(sigSlow, tauSlow) if sigSlow > 0 else np.zeros(n)
        self.fast = ou(sigFast, tauFast) if sigFast > 0 else np.zeros(n)
        # small eddies (smaller than the crown): nTurb independent OU streams, a body reads stream id % nTurb
        self.turb = np.stack([ou(sigTurb, tauTurb) for _ in range(nTurb)]) if sigTurb > 0 else None
        self.nTurb = nTurb
        self.fr = []
        if fronts:
            t = rng.exponential(front_mean_s) * 0.5 + 2.0
            while t < seconds:
                self.fr.append((t, rng.uniform(*front_amp), rng.uniform(0.5, 0.7), rng.uniform(3.0, 5.0)))
                t += rng.exponential(front_mean_s)

    def __call__(self, t, x, y, ids=None):
        x = np.asarray(x, float); y = np.asarray(y, float)
        tt = t - (x - self.xUp) / self.uAdv + 5.0
        i = np.clip(tt / self.dtw, 0, self.n - 2); i0 = i.astype(int); f = i - i0
        s = self.slow[i0] * (1 - f) + self.slow[i0 + 1] * f + self.fast[i0] * (1 - f) + self.fast[i0 + 1] * f
        if self.turb is not None and ids is not None:
            k = np.asarray(ids) % self.nTurb
            j = min(int(t / self.dtw), self.n - 1)
            s = s + self.turb[k, j]
        u = self.uMean * (np.maximum(y, 0.05) / self.yRef) ** self.alpha + s
        for (t0, a, sg, uf) in self.fr:
            u = u + a * np.exp(-0.5 * ((t - t0 - (x - self.xUp) / uf) / sg) ** 2)
        return np.maximum(0.2, u)


class SineWind:
    """HEAD ApplyWind sinusoid (windSpeed 2.2, gust 1.1, period 4.5), depth-phased."""

    def __init__(self, U=2.2, gust=1.1, period=4.5, seed=1, cap=3):
        self.U, self.g, self.om, self.seed, self.cap = U, gust, 2 * math.pi / period, seed, cap

    def depth_speed(self, t, d):
        d = np.asarray(d, float)
        return self.U * (1 + self.g * np.sin(t * self.om + 0.62 * d + 0.17 * self.seed) * (0.32 + 0.68 * d / self.cap))


# ---------- crow events ----------
def crow_event(rng, t_land, t_leave, body, frac, mode):
    """One randomised landing + take-off. Bird velocity in m/s, branch gets +m v_in (arrest) and
    -m v_out (push-off reaction); half-sine pulses. mode: 'step' | 'kick' | 'kickmass'."""
    side = rng.choice([-1.0, 1.0])
    vin = rng.uniform(0.8, 1.8); ain = math.radians(rng.uniform(20, 60))
    vout = rng.uniform(1.8, 2.8); aout = math.radians(rng.uniform(45, 75))
    return dict(body=body, frac=frac, mode=mode, m=rng.uniform(0.40, 0.52), t_land=t_land, t_leave=t_leave,
                v_in=np.array([side * vin * math.cos(ain), -vin * math.sin(ain)]),
                v_out=np.array([side * vout * math.cos(aout), vout * math.sin(aout)]),
                Ta=rng.uniform(0.03, 0.07), Tp=rng.uniform(0.06, 0.10))


def crow_force(ev, t):
    """(force on the perch point, seated?) at time t."""
    if ev is None or t < ev["t_land"] or t >= ev["t_leave"]:
        return np.zeros(2), False
    F = np.array([0.0, -ev["m"] * G]) if ev["mode"] != "kickmass" else np.zeros(2)   # kickmass: weight via M + gravity
    if ev["mode"] in ("kick", "kickmass", "kicks"):
        g = ev.get("scale", 1.0) if ev["mode"] == "kicks" else 1.0   # kicks: inelastic share m_sub/(m_sub+m)
        ta = t - ev["t_land"]
        if ta < ev["Ta"]:
            F = F + g * ev["m"] * ev["v_in"] * (math.pi / (2 * ev["Ta"])) * math.sin(math.pi * ta / ev["Ta"])
        tp = ev["t_leave"] - t
        if tp < ev["Tp"]:
            F = F - g * ev["m"] * ev["v_out"] * (math.pi / (2 * ev["Tp"])) * math.sin(math.pi * (ev["Tp"] - tp) / ev["Tp"])
    return F, True


# ---------- joint-space simulation (reference: implicit linear spring/damper) ----------
def simulate(tree, seconds, wind=None, wood_drag="abs", crow=None, record_every=4, pluck=None,
             Ivec=None, extra_c=None, foliage=True, seed=0, m_every=5):
    """Returns dict of time traces. wood_drag: 'abs' (game ApplyWind) | 'rel' (relative speed) | 'off'."""
    n = tree.n
    pts0 = tree.pts if foliage else []
    hold = tree.hold if foliage else -tree.grav_Q(np.zeros(n), [])
    q = np.zeros(n); v = np.zeros(n)
    if pluck is not None:
        v = v + pluck
    k, c, mt = tree.K, tree.C + (0 if extra_c is None else extra_c), tree.MT
    Kd, Cd = np.diag(k), np.diag(c)
    lo, hi = tree.LO, tree.HI
    nstep = int(round(seconds / DT))
    fb = np.array([p[0] for p in pts0], int); ff = np.array([p[1] for p in pts0], float)
    fa = np.array([p[3] for p in pts0], float)
    rec = {"t": [], "q": [], "tipx": [], "tipy": [], "perch": [], "u": []}
    Mm = None
    pin = np.zeros(n)
    for s in range(nstep):
        t = s * DT
        Fc, seated = crow_force(crow, t)
        pts = list(pts0)
        if crow is not None and seated and crow["mode"] == "kickmass":
            pts = pts + [(crow["body"], crow["frac"], crow["m"], 0.0)]
        if Mm is None or s % m_every == 0 or (crow is not None and abs(t - crow["t_land"]) < DT) or \
                (crow is not None and abs(t - crow["t_leave"]) < DT):
            Mm = tree.mass_matrix(q, pts, Ivec)
        Q = tree.grav_Q(q, pts) + hold
        phi, base, e = tree.kin(q)
        if wind is not None:
            com = base + 0.5 * e
            Jc = tree.jac(com, np.arange(n), base)
            if isinstance(wind, SineWind):
                uw = wind.depth_speed(t, np.minimum(tree.ORD, 3))
                uf = np.full(len(fb), wind.U)
            else:
                uw = wind(t, com[:, 0], com[:, 1], ids=np.arange(n))
            hs = 1.0 + 0.22 * np.abs(np.sin(phi))
            if wood_drag != "off":
                if wood_drag == "rel":
                    vx = Jc[:, 0, :] @ v
                    rel = uw - vx
                    Fx = Q_DYN * np.abs(rel) * rel * tree.AREA * hs
                else:
                    Fx = Q_DYN * np.sign(uw) * uw * uw * tree.AREA * hs
                Q = Q + Fx @ Jc[:, 0, :]
            if len(fb):
                P = base[fb] + ff[:, None] * e[fb]
                Jf = tree.jac(P, fb, base)
                if not isinstance(wind, SineWind):
                    uf = wind(t, P[:, 0], P[:, 1], ids=n + np.arange(len(fb)))
                rel = uf - Jf[:, 0, :] @ v
                A = fa / np.sqrt(1 + (uf / 5.0) ** 2)
                Q = Q + (Q_DYN * np.abs(rel) * rel * A) @ Jf[:, 0, :]
        if crow is not None and seated:
            Pp = base[crow["body"]] + crow["frac"] * e[crow["body"]]
            Jp = tree.jac(Pp[None], np.array([crow["body"]]), base)[0]
            Q = Q + Jp.T @ Fc
        spring = np.clip(-k * q, -mt, mt)
        Asys = Mm + DT * Cd + DT * DT * Kd
        rhs = DT * (Q + spring - c * v) - DT * DT * (k * v)
        v = v + np.linalg.solve(Asys, rhs)
        q = q + DT * v
        olo = q < lo; ohi = q > hi
        if olo.any() or ohi.any():
            q = np.where(olo, lo, np.where(ohi, hi, q))
            v = np.where(olo & (v < 0), 0.0, np.where(ohi & (v > 0), 0.0, v))
            pin += olo | ohi
        if s % record_every == 0:
            phi, base, e = tree.kin(q)
            tips = base + e
            rec["t"].append(t); rec["q"].append(q.copy()); rec["tipx"].append(tips[:, 0].copy())
            rec["tipy"].append(tips[:, 1].copy())
            if crow is not None:
                rec["perch"].append(base[crow["body"]] + crow["frac"] * e[crow["body"]])
            if wind is not None and not isinstance(wind, SineWind):
                rec["u"].append(float(wind(t, np.array([0.0]), np.array([1.6]))[0]))
    out = {kk: np.array(vv) for kk, vv in rec.items()}
    out["pinned_frac"] = pin / nstep
    return out


# ---------- linear modal analysis ----------
def modal(tree, Ivec=None, extra_c=None, foliage=True):
    pts = tree.pts if foliage else []
    Mm = tree.mass_matrix(np.zeros(tree.n), pts, Ivec)
    H = tree.hessian(pts)
    K = np.diag(tree.K) + H
    C = np.diag(tree.C + (0 if extra_c is None else extra_c))
    n = tree.n
    Mi = np.linalg.inv(Mm)
    Asys = np.block([[np.zeros((n, n)), np.eye(n)], [-Mi @ K, -Mi @ C]])
    lam, V = np.linalg.eig(Asys)
    keep = lam.imag > 1e-6
    lam, V = lam[keep], V[:n, keep]
    wn = np.abs(lam); zeta = -lam.real / wn
    order = np.argsort(wn)
    modes = []
    for i in order:
        shape = np.abs(V[:, i]) * np.sqrt(np.diag(Mm))
        j = int(np.argmax(shape))
        modes.append(dict(f=float(wn[i] / (2 * math.pi)), zeta=float(zeta[i]), joint=j, order=int(tree.ORD[j])))
    # undamped generalised eigen (ideal exact-constraint solver): omega_max * dt
    w2 = np.linalg.eigvals(np.linalg.solve(Mm, np.diag(tree.K)))
    return dict(modes=modes, wdt_max=float(math.sqrt(max(w2.real)) * DT), mineigKH=float(np.min(np.linalg.eigvalsh(K))))


def static_load(tree, body, frac, m, Ivec=None):
    """Linear static response to a point weight m on body at frac (hold excludes it, like ApplyPerchLoad)."""
    pts = tree.pts
    K = np.diag(tree.K) + tree.hessian(pts + [(body, frac, m, 0.0)])
    phi, base, e = tree.rest
    Pp = base[body] + frac * e[body]
    Jp = tree.jac(Pp[None], np.array([body]), base)[0]
    dq = np.linalg.solve(K, Jp.T @ np.array([0.0, -m * G]))
    return dict(drop=float(-(Jp @ dq)[1]), dq_deg=np.degrees(dq))


# ---------- Box2D-style maximal-coordinate solver (stability at the game step) ----------
def b2sim(tree, Ivec, seconds=3.0, pluck_deg_s=20.0, seed=0, vel_iters=8, pos_iters=3, kmul=1.0,
          friction=True, limits=True, record=False, damper="game"):
    """Sequential-impulse solver modelled on Box2D v2.4 b2RevoluteJoint (soft-free point constraint,
    warm starting, speculative angle limits, NGS position pass), TreeSpring explicit spring (maxTorque
    clamp) and JointFriction explicit damper (stopTorque clamp) applied as pair torques before the step,
    hold torque = static gravity moment at bind. Returns growth metrics; 'unstable' when joint rates
    explode or a piece flips."""
    rng = np.random.default_rng(seed)
    n = tree.n
    phi0, base0, e0 = tree.rest
    com0 = base0 + 0.5 * e0
    # bodies 0..n-1 dynamic, n = ground (static)
    x = [float(v) for v in com0[:, 0]]; y = [float(v) for v in com0[:, 1]]; a = [float(v) for v in phi0]
    vx = [0.0] * n; vy = [0.0] * n; w = [0.0] * n
    im = [1.0 / tree.M[b] for b in range(n)]; ii = [1.0 / Ivec[b] for b in range(n)]
    hold = tree.hold
    k = tree.K * kmul; c = tree.C * math.sqrt(kmul); mt = tree.MT * kmul
    joints = []
    for b in range(n):
        p = tree.P[b]
        # anchor local to child: (0, -L/2) in the rod frame; local to parent: (0, +L/2) or world for ground
        la_c = (0.0, -0.5 * tree.L[b])
        if p >= 0:
            la_p = (0.0, 0.5 * tree.L[p])
            ref = phi0[b] - phi0[p]
        else:
            la_p = (float(base0[b, 0]), float(base0[b, 1]))
            ref = phi0[b]
        joints.append(dict(c=b, p=p, lc=la_c, lp=la_p, ref=ref, P=[0.0, 0.0], lo_imp=0.0, hi_imp=0.0,
                           lo=tree.LO[b], hi=tree.HI[b]))
    # pluck: random joint-rate kick, converted to body angular velocities through the chain
    qd = np.radians(pluck_deg_s) * rng.uniform(-1, 1, n)
    J = tree.jac(com0, np.arange(n), base0)
    v0 = np.einsum("pin,n->pi", J, qd); w0 = tree.A @ qd
    for b in range(n):
        vx[b], vy[b], w[b] = float(v0[b, 0]), float(v0[b, 1]), float(w0[b])

    def rot(ang, lx, ly):
        ca, sa = math.cos(ang), math.sin(ang)
        return ca * lx - sa * ly, sa * lx + ca * ly

    nstep = int(round(seconds / DT))
    rate_hist = []
    trace = []
    unstable = False
    for s in range(nstep):
        # --- forces: gravity, spring, damper, hold ---
        tq = [0.0] * n
        for jt in joints:
            b, p = jt["c"], jt["p"]
            ang = a[b] - (a[p] if p >= 0 else 0.0) - jt["ref"]
            wr = w[b] - (w[p] if p >= 0 else 0.0)
            ts = max(-mt[b], min(mt[b], -k[b] * ang))
            tf = 0.0
            if friction:
                inv = ii[b] + (ii[p] if p >= 0 else 0.0)
                if damper == "implicit":
                    # backward-Euler factor on the free pair: tau = -c w / (1 + c dt (1/I_c + 1/I_p))
                    tf = -c[b] * wr / (1.0 + c[b] * DT * inv)
                else:
                    tf = -c[b] * wr                     # JointFriction: explicit, stopTorque clamp
                    stop = abs(wr) / (inv * DT)
                    tf = max(-stop, min(stop, tf))
                tf = max(-mt[b] * 2, min(mt[b] * 2, tf))
            tau = hold[b] + ts + tf
            tq[b] += tau
            if p >= 0:
                tq[p] -= tau
        for b in range(n):
            vy[b] += -G * DT
            w[b] += DT * ii[b] * tq[b]
        # --- velocity constraints init + warm start ---
        for jt in joints:
            b, p = jt["c"], jt["p"]
            rcx, rcy = rot(a[b], *jt["lc"])
            if p >= 0:
                rpx, rpy = rot(a[p], *jt["lp"])
                mp, ip = im[p], ii[p]
            else:
                rpx = rpy = 0.0; mp = ip = 0.0
            mc, ic = im[b], ii[b]
            k11 = mp + mc + rpy * rpy * ip + rcy * rcy * ic
            k12 = -rpy * rpx * ip - rcy * rcx * ic
            k22 = mp + mc + rpx * rpx * ip + rcx * rcx * ic
            det = k11 * k22 - k12 * k12
            jt.update(rc=(rcx, rcy), rp=(rpx, rpy), K=(k11, k12, k22, det), mp=mp, ip=ip,
                      axial=1.0 / (ic + ip))
            Px, Py = jt["P"]
            ax = jt["lo_imp"] - jt["hi_imp"]
            if p >= 0:
                vx[p] -= mp * Px; vy[p] -= mp * Py; w[p] -= ip * (rpx * Py - rpy * Px + ax)
            vx[b] += mc * Px; vy[b] += mc * Py; w[b] += ic * (rcx * Py - rcy * Px + ax)
        # --- velocity iterations ---
        for it in range(vel_iters):
            for jt in joints:
                b, p = jt["c"], jt["p"]
                mc, ic, mp, ip = im[b], ii[b], jt["mp"], jt["ip"]
                if limits:
                    ang = a[b] - (a[p] if p >= 0 else 0.0) - jt["ref"]
                    wp = w[p] if p >= 0 else 0.0
                    Cl = ang - jt["lo"]; bias = Cl / DT if Cl > 0 else 0.0
                    imp = -jt["axial"] * ((w[b] - wp) + bias)
                    old = jt["lo_imp"]; jt["lo_imp"] = max(old + imp, 0.0); imp = jt["lo_imp"] - old
                    w[b] += ic * imp
                    if p >= 0: w[p] -= ip * imp
                    wp = w[p] if p >= 0 else 0.0
                    Cu = jt["hi"] - ang; bias = Cu / DT if Cu > 0 else 0.0
                    imp = -jt["axial"] * ((wp - w[b]) + bias)
                    old = jt["hi_imp"]; jt["hi_imp"] = max(old + imp, 0.0); imp = jt["hi_imp"] - old
                    w[b] -= ic * imp
                    if p >= 0: w[p] += ip * imp
                rcx, rcy = jt["rc"]; rpx, rpy = jt["rp"]
                vpx = vpy = wp = 0.0
                if p >= 0:
                    vpx, vpy, wp = vx[p], vy[p], w[p]
                cdx = vx[b] - w[b] * rcy - vpx + wp * rpy
                cdy = vy[b] + w[b] * rcx - vpy - wp * rpx
                k11, k12, k22, det = jt["K"]
                ix = -(k22 * cdx - k12 * cdy) / det
                iy = -(-k12 * cdx + k11 * cdy) / det
                jt["P"][0] += ix; jt["P"][1] += iy
                if p >= 0:
                    vx[p] -= mp * ix; vy[p] -= mp * iy; w[p] -= ip * (rpx * iy - rpy * ix)
                vx[b] += mc * ix; vy[b] += mc * iy; w[b] += ic * (rcx * iy - rcy * ix)
        # --- integrate positions (Box2D clamps) ---
        for b in range(n):
            tr2 = (vx[b] * DT) ** 2 + (vy[b] * DT) ** 2
            if tr2 > 4.0:
                r = 2.0 / math.sqrt(tr2); vx[b] *= r; vy[b] *= r
            if abs(w[b] * DT) > 0.5 * math.pi:
                w[b] *= 0.5 * math.pi / abs(w[b] * DT)
            x[b] += DT * vx[b]; y[b] += DT * vy[b]; a[b] += DT * w[b]
        # --- position iterations (NGS) ---
        for it in range(pos_iters):
            for jt in joints:
                b, p = jt["c"], jt["p"]
                mc, ic, mp, ip = im[b], ii[b], jt["mp"], jt["ip"]
                if limits:
                    ang = a[b] - (a[p] if p >= 0 else 0.0) - jt["ref"]
                    corr = 0.0
                    if ang < jt["lo"] - math.radians(2.0):
                        corr = min(jt["lo"] - ang - math.radians(2.0), math.radians(8.0))
                    elif ang > jt["hi"] + math.radians(2.0):
                        corr = -min(ang - jt["hi"] - math.radians(2.0), math.radians(8.0))
                    if corr != 0.0:
                        lim_imp = jt["axial"] * corr
                        a[b] += ic * lim_imp
                        if p >= 0: a[p] -= ip * lim_imp
                rcx, rcy = rot(a[b], *jt["lc"])
                if p >= 0:
                    rpx, rpy = rot(a[p], *jt["lp"]); cpx, cpy = x[p] + rpx, y[p] + rpy
                else:
                    rpx = rpy = 0.0; cpx, cpy = jt["lp"]
                Cx = x[b] + rcx - cpx; Cy = y[b] + rcy - cpy
                k11 = mp + mc + rpy * rpy * ip + rcy * rcy * ic
                k12 = -rpy * rpx * ip - rcy * rcx * ic
                k22 = mp + mc + rpx * rpx * ip + rcx * rcx * ic
                det = k11 * k22 - k12 * k12
                ix = -(k22 * Cx - k12 * Cy) / det; iy = -(-k12 * Cx + k11 * Cy) / det
                if p >= 0:
                    x[p] -= mp * ix; y[p] -= mp * iy; a[p] -= ip * (rpx * iy - rpy * ix)
                x[b] += mc * ix; y[b] += mc * iy; a[b] += ic * (rcx * iy - rcy * ix)
        # --- metrics ---
        rates = [abs(w[b] - (w[tree.P[b]] if tree.P[b] >= 0 else 0.0)) for b in range(n)]
        rate_hist.append(max(rates))
        if record and s % 2 == 0:
            angs = [a[b] - (a[tree.P[b]] if tree.P[b] >= 0 else 0.0) - joints[b]["ref"] for b in range(n)]
            trace.append((s * DT, angs, [x[b] + rot(a[b], 0.0, 0.5 * tree.L[b])[0] for b in range(n)]))
        if not all(map(math.isfinite, w)) or max(rates) > 200.0:
            unstable = True
            break
    rh = np.array(rate_hist)
    nwin = int(0.5 / DT)
    early = float(np.max(rh[:nwin])) if len(rh) >= nwin else float("nan")
    late = float(np.max(rh[-nwin:])) if len(rh) >= 2 * nwin else float("nan")
    r0 = float(np.max(np.abs(qd)))
    mx = float(np.max(rh)) if len(rh) else float("nan")
    res = dict(unstable=bool(unstable or mx > 5 * r0), early=early, late=late, steps=len(rh),
               r0=r0, maxrate=mx, growth=float(late / max(r0, 1e-9)))
    if record:
        res["trace"] = trace
    return res
