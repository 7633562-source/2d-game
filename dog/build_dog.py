"""build_dog.py - writes dog2d.xml, the planar MuJoCo twin of the Unity dog (dog-twin-1024).

Source: E:/Games/2D Game/dog454/Assets/Dog.cs (branch land/dog-1024, HEAD 352ee30a), read-only. See skeleton.md.

Coordinates: MuJoCo x = Unity x (the dog faces +x), MuJoCo z = Unity y + 2.0 (ground top y = -2 -> z = 0).
Unity root (the Dog transform) spawns at y = StandingRootOffset(-2) = -1.53419 (HeadlessTrial.cs:2937).

Joint zero = the SPAWN pose. Unity HingeJoint2D reads jointAngle = parentRot - childRot - referenceAngle, the
reference captured when the joint is created at the build pose (DogStanceController.Turn.cs:148; checked on
cl505_x_s07.csv row 1: lumbar 0, hip -1.08 at t = 0.005 although chest -8 / pelvis 0 world). Limits are on the same
referenced angle. So every MuJoCo body is placed at its spawn pose and q = 0 there; q = Unity jointAngle.
Sign: child hinge axis (0 1 0) -> +q = child turns clockwise in the x-right/z-up view = parent - child (Unity).
Root hinge axis (0 -1 0) -> q = pelvis world angle CCW + (= pelvisTilt).

Box2D vs MuJoCo (same mapping as cache/rl-1024/build_model.py):
- contact skin: the floor plane sits at z = SKIN (Box2D pads polygons by the 0.01 contact offset each);
- joint friction tau = clamp(-K w, +-|w| Ieff/dt, +-cap) -> affine bias actuator, K_eff = min(K, Ieff/dt);
- pair friction sqrt(mu1 mu2) -> MuJoCo takes the max of the two geoms: the floor gets the smallest pair value
  (body 0.4 x ground 0.4 = 0.4) and each geom gets its own pair value with the ground.
"""
import math
import sys

DT = 0.005
M_TOTAL = 25.0
GROUND_MU = 0.4          # GroundBuilder default (human skeleton.md, same GroundBuilder in dog454)

# segment: (w = local x size, h = local y size, mass fraction)   Dog.cs:77-87 sizes, fractions at CreateSegment calls
SEG = {
    "pelvis": (0.22, 0.13, 0.140),   # Dog.cs:78, 402
    "chest": (0.36, 0.14, 0.220),    # Dog.cs:77, 408
    "neck": (0.10, 0.08, 0.030),     # Dog.cs:79, 423
    "head": (0.14, 0.10, 0.070),     # Dog.cs:80, 441
    "jaw": (0.10, 0.032, 0.008),     # Dog.cs:81, 497
    "upper": (0.07, 0.17, 0.040),    # Dog.cs:84, 545
    "lower": (0.06, 0.17, 0.028),    # Dog.cs:85, 557
    "thigh": (0.08, 0.17, 0.080),    # Dog.cs:86, 596
    "shin": (0.06, 0.17, 0.040),     # Dog.cs:87, 608
    "paw": (0.10, 0.05, 0.012),      # Dog.cs:88, 557/608 PlacePaw(..., 0.012f)
}
N_TAIL = 5                           # Dog.cs:104
TAIL_LEN, TAIL_H = 0.20, 0.05        # Dog.cs:83 (build(tail_len=...) rebinds TAIL_LEN; day 2: 0.35 approved by the owner)
TAIL_FRAC_EACH = 0.020 / N_TAIL      # Dog.cs:701
FRAC_SUM = (SEG["pelvis"][2] + SEG["chest"][2] + SEG["neck"][2] + SEG["head"][2] + SEG["jaw"][2]
            + N_TAIL * TAIL_FRAC_EACH + 2 * (SEG["upper"][2] + SEG["lower"][2] + SEG["paw"][2])
            + 2 * (SEG["thigh"][2] + SEG["shin"][2] + SEG["paw"][2]))
MASS_SCALE = 1.0 / FRAC_SUM          # NormalizeMass, Dog.cs:1244-1264 (sum of fractions 0.912 -> x1.0965)

CHEST_PITCH = -8.0                   # Dog.cs:68
SH_ANG, EL_ANG, HIP_ANG, KN_ANG = -20.0, 24.0, -12.0, 24.0   # Dog.cs:60-63 (design pose = jointAngle 0 at spawn)
SHOULDER_LX = 0.10                   # Dog.cs:73
HIP_LX = -0.04                       # Dog.cs:91
PAW_HEEL = 0.025                     # Dog.cs:92

# joint: (K friction, cap, lo, hi, flexor Nm, extensor Nm)  Dog.cs:108-118 friction, 136-146 ceilings,
# limits at the ConnectTo calls (Dog.cs:413-417, 431-435, 449-453, 548-552, 560-564, 599-603, 611-615, 770-774, 721-725)
JOINT = {
    "lumbar": (8.0, 20.0, -20, 20, 80, 80),
    "neck": (0.01, 15.0, -30, 30, 8, 8),
    "head": (0.01, 15.0, -30, 30, 8, 8),
    "shoulder": (3.0, 15.0, -90, 90, 35, 35),
    "elbow": (3.0, 15.0, 0, 120, 40, 40),
    "hip": (3.0, 15.0, -90, 90, 40, 40),
    "knee": (3.0, 15.0, 0, 120, 45, 45),
    "paw": (1.0, 15.0, -45, 45, 7, 17),
    "tail": (0.4, 15.0, -40, 40, 3, 3),
    "jaw": (0.01, 15.0, 0, 40, 2.5, 2.5),   # Dog.cs:112 jawFriction, 138 jawMuscleTorque, 502-506 limits 0..40 (+ opens)
}
TAIL_SPRING = 2.0                    # TreeSpring N m/rad about jointAngle 0, cap 2 N m (Dog.cs:105-106, 736-746)
# day 3, owner's hip remark (build(hip_opt=...)): THIGH_EXTRA = added thigh length, HIP_SHIFT = anchor shift in the
# pelvis frame, PELVIS_DY = pelvis box centre offset below the spine line (the body origin stays on the spine line).
THIGH_EXTRA, HIP_SHIFT, PELVIS_DY = 0.0, (0.0, 0.0), 0.0
HIP_STAND_Q = -12.0                  # stand hip target (rlenv_dog.STAND, Dog.cs ApplySpawnPoseToStance)


def rot(deg, v):
    r = math.radians(deg)
    c, s = math.cos(r), math.sin(r)
    return (v[0] * c - v[1] * s, v[0] * s + v[1] * c)


def add(a, b):
    return (a[0] + b[0], a[1] + b[1])


def mass(seg):
    if seg == "tail":
        return max(0.02, M_TOTAL * TAIL_FRAC_EACH) * MASS_SCALE
    return max(0.02, M_TOTAL * SEG[seg][2]) * MASS_SCALE


def izz(m, w, h):
    return m * (w * w + h * h) / 12.0


def layout():
    """Unity root-frame spawn layout (Dog.BuildDog, Dog.cs:374-448, 530-626, 750-777):
    returns dict body -> (centre (x, y), world deg, size (w, h), mass, joint anchor (x, y) or None)."""
    L = {}
    chest_caudal = rot(CHEST_PITCH, (-SEG["chest"][0] / 2, 0.0))
    pel_c = (chest_caudal[0] - SEG["pelvis"][0] / 2, chest_caudal[1])
    L["pelvis"] = (pel_c, 0.0, None)
    L["chest"] = ((0.0, 0.0), CHEST_PITCH, add(pel_c, (SEG["pelvis"][0] / 2, 0.0)))
    nj = rot(CHEST_PITCH, (SEG["chest"][0] / 2, 0.0))
    L["neck"] = (add(nj, (SEG["neck"][0] / 2, 0.0)), 0.0, nj)
    hj = add(L["neck"][0], (SEG["neck"][0] / 2, 0.0))
    L["head"] = (add(hj, (SEG["head"][0] / 2, 0.0)), 0.0, hj)
    tmj = add(L["head"][0], (-SEG["head"][0] * 0.18, -SEG["head"][1] * 0.38))
    L["jaw"] = (add(tmj, (SEG["jaw"][0] / 2, 0.0)), 0.0, tmj)
    seg_len = TAIL_LEN / N_TAIL
    tip = (pel_c[0] - SEG["pelvis"][0] / 2, pel_c[1])
    for i in range(N_TAIL):
        L["tail%d" % i] = ((tip[0] - seg_len / 2, tip[1]), 0.0, tip)
        tip = (tip[0] - seg_len, tip[1])
    upper_w = CHEST_PITCH - SH_ANG
    lower_w = upper_w - EL_ANG
    thigh_w = -HIP_ANG
    shin_w = thigh_w - KN_ANG
    sh = rot(CHEST_PITCH, (SHOULDER_LX, -SEG["chest"][1] / 2))
    lu, ll = SEG["upper"][1], SEG["lower"][1]
    el = add(sh, rot(upper_w, (0, -lu)))
    an_f = add(el, rot(lower_w, (0, -ll)))
    drop_f = lu * math.cos(math.radians(upper_w)) + ll * math.cos(math.radians(lower_w)) + SEG["paw"][1]
    lt, ls = SEG["thigh"][1], SEG["shin"][1]
    lt0 = lt - THIGH_EXTRA
    drop_r = lt0 * math.cos(math.radians(thigh_w)) + ls * math.cos(math.radians(shin_w)) + SEG["paw"][1]
    sole_y = sh[1] - drop_f
    hip = (pel_c[0] + HIP_LX + HIP_SHIFT[0], sole_y + drop_r + HIP_SHIFT[1])
    kn = add(hip, rot(thigh_w, (0, -lt)))
    an_r = add(kn, rot(shin_w, (0, -ls)))
    for side in ("r", "l"):
        L["upper_f" + side] = (add(sh, rot(upper_w, (0, -lu / 2))), upper_w, sh)
        L["lower_f" + side] = (add(el, rot(lower_w, (0, -ll / 2))), lower_w, el)
        L["paw_f" + side] = ((an_f[0] - PAW_HEEL + SEG["paw"][0] / 2, an_f[1] - SEG["paw"][1] / 2), 0.0, an_f)
        L["thigh_r" + side] = (add(hip, rot(thigh_w, (0, -lt / 2))), thigh_w, hip)
        L["shin_r" + side] = (add(kn, rot(shin_w, (0, -ls / 2))), shin_w, kn)
        L["paw_r" + side] = ((an_r[0] - PAW_HEEL + SEG["paw"][0] / 2, an_r[1] - SEG["paw"][1] / 2), 0.0, an_r)
    start_y = -2.0 - sole_y   # StandingRootOffset (Dog.cs:316-322)
    return L, start_y


def seg_of(body):
    for k in ("upper", "lower", "thigh", "shin", "paw"):
        if body.startswith(k):
            return k
    if body.startswith("tail"):
        return "tail"
    return body


def size_of(body):
    if body.startswith("tail"):
        i = int(body[4:])
        taper = 1.0 if N_TAIL == 1 else 1.0 + (0.45 - 1.0) * i / (N_TAIL - 1)
        return (TAIL_LEN / N_TAIL, TAIL_H * taper)
    s = SEG[seg_of(body)]
    return (s[0], s[1])


def keff(parent, child):
    j = joint_of(child)
    K = JOINT[j][0]
    ip = izz(mass(seg_of(parent)), *size_of(parent))
    ic = izz(mass(seg_of(child)), *size_of(child))
    ie = 1.0 / (1.0 / ip + 1.0 / ic)
    return min(K, ie / DT), ie


def joint_of(body):
    return {"chest": "lumbar", "neck": "neck", "head": "head", "upper": "shoulder", "lower": "elbow",
            "thigh": "hip", "shin": "knee", "paw": "paw", "tail": "tail", "jaw": "jaw"}.get(seg_of(body))


# tree: child -> parent
PARENT = {"chest": "pelvis", "neck": "chest", "head": "neck", "jaw": "head", "tail0": "pelvis"}
for _i in range(1, N_TAIL):
    PARENT["tail%d" % _i] = "tail%d" % (_i - 1)
for _s in ("r", "l"):
    PARENT.update({"upper_f" + _s: "chest", "lower_f" + _s: "upper_f" + _s, "paw_f" + _s: "lower_f" + _s,
                   "thigh_r" + _s: "pelvis", "shin_r" + _s: "thigh_r" + _s, "paw_r" + _s: "shin_r" + _s})
# joint names in MuJoCo
JNAME = {"chest": "lumbar", "neck": "neck", "head": "head", "jaw": "jaw"}
for _i in range(N_TAIL):
    JNAME["tail%d" % _i] = "tail%d" % _i
for _s in ("r", "l"):
    JNAME.update({"upper_f" + _s: "shoulder_" + _s, "lower_f" + _s: "elbow_" + _s, "paw_f" + _s: "fpaw_" + _s,
                  "thigh_r" + _s: "hip_" + _s, "shin_r" + _s: "knee_" + _s, "paw_r" + _s: "rpaw_" + _s})
# actuated joints in a fixed order (the policy layout). The jaw is NOT in it: day 1 welded it; day 2 (owner: "да это
# отдельный сустав") build(jaw_hinge=True) adds the hinge "jaw" with m_jaw / f_jaw appended AFTER every other actuator,
# so all day-1 actuator indices stay the same. The jaw collider is a trigger (Dog.cs:510-511) -> contype 0 either way.
JOINTS_ORDER = (["lumbar", "neck", "head"] + ["tail%d" % i for i in range(N_TAIL)]
                + ["shoulder_r", "elbow_r", "fpaw_r", "shoulder_l", "elbow_l", "fpaw_l",
                   "hip_r", "knee_r", "rpaw_r", "hip_l", "knee_l", "rpaw_l"])
BODY_OF_J = {v: k for k, v in JNAME.items()}
JAW_COLOR = "0.86 0.76 0.58 1"         # Art/Dog/Palette.cs:183 (Jaw)


def visual_geoms(L, start_y):
    """visual-only boxes (contype 0, conaffinity 0, no mass: inertiafromgeom is false), keyed by body:
    the game's own seam covers, Dog.cs:621-690 (box look, DogArt.BoxLook = true):
    - HipBridge on the pelvis Visual: centred on the hip anchor, w = rearThighSize.x * 1.25,
      h = (|hipInPelvisY| - pelvisSize.y / 2) + thighWidth / 2 + 0.006, pelvis colour;
    - ShoulderBridge on the chest Visual: centred on the shoulder anchor, w = frontUpperSize.x * 1.25,
      h = upperWidth / 2 + 0.006, chest colour, turned with the chest.
    Returns body -> list of (name, centre in body-origin frame (x, z), deg world, half w, half h, half depth, rgba)."""
    pel_c = L["pelvis"][0]
    lum = L["chest"][2]
    hip = L["thigh_rr"][2]
    sh = L["upper_fr"][2]
    hip_in_pel_y = hip[1] - pel_c[1]
    h_hip = (abs(hip_in_pel_y) - SEG["pelvis"][1] / 2) + SEG["thigh"][0] / 2 + 0.006
    h_sh = SEG["upper"][0] / 2 + 0.006
    return {
        "pelvis": [("hip_bridge_v", (hip[0] - pel_c[0], hip[1] - pel_c[1]), 0.0, SEG["thigh"][0] * 1.25 / 2, h_hip / 2,
                    0.039, "0.7 0.5 0.3 1")],
        "chest": [("shoulder_bridge_v", (sh[0] - lum[0], sh[1] - lum[1]), CHEST_PITCH, SEG["upper"][0] * 1.25 / 2,
                   h_sh / 2, 0.039, "0.75 0.55 0.35 1")],
    }


def build(skin=0.02, paw_mu=0.4, body_mu=0.4, solref="0.01 1", solimp="0.95 0.99 0.001", solreflimit="0.01 1",
          solimplimit="0.95 0.99 0.001", integrator="implicitfast", jfric_scale=1.0, iterations=50, cone="pyramidal",
          noslip=0, impratio=1.0, tail_len=0.20, jaw_hinge=False, visuals=False, jaw_spring=0.0,
          jaw_limit="-40000 -400", hip_opt=None, tail_k=2.0):
    """paw_mu / body_mu are the Unity MATERIAL frictions; the pair with the ground is sqrt(mu * 0.4).
    tail_len: total tail length (5 segments, same taper and mass fraction, inertias recomputed; rebinds TAIL_LEN).
    jaw_hinge: the jaw on a hinge at the TMJ (Dog.cs:491-511: limits 0..40 deg, + opens, friction 0.01, muscle 2.5 N m);
      False = day-1 weld. The hold law (DriveJaw, DogStanceController.cs:8190-8223) lives in rlenv_dog.
      jaw_limit: solreflimit of the jaw stop only. The default soft stop let the 2.5 N m snap overshoot to 44.7 deg
      (tests/t_jawlimit.py); "-40000 -400" (direct stiffness / damping, ~ a 5 ms stop) keeps it at 39.8 deg, close to
      Box2D's hard velocity stop. jaw_spring: optional passive spring N m/rad, NOT in the game (default 0).
    visuals: picture-only geoms (hip / shoulder bridges as in the game, the jaw drawn in front of the head in its game
      colour, as its sorting order 3 > head 2). No physics effect (contype 0, conaffinity 0, explicit inertials)."""
    global TAIL_LEN, TAIL_SPRING
    TAIL_LEN = float(tail_len)
    TAIL_SPRING = float(tail_k)   # day 3: mirror of -dogTailK (req-535; game default 2, candidate 4)
    saved = apply_hip_opt(hip_opt)
    try:
        return _build(skin, paw_mu, body_mu, solref, solimp, solreflimit, solimplimit, integrator, jfric_scale, iterations,
                      cone, noslip, impratio, jaw_hinge, visuals, jaw_spring, jaw_limit)
    finally:
        restore_hip_opt(saved)


HIP_INSET = 1.0 / 6.0   # options A/B/C put the hip anchor 1/6 of the pelvis box height above its bottom face


def hip_in_pelvis():
    """(hip anchor - pelvis box centre) y in the pelvis frame, m; and the box half height."""
    L, _ = layout()
    return L["thigh_rr"][2][1] - (L["pelvis"][0][1] + PELVIS_DY), SEG["pelvis"][1] / 2


def apply_hip_opt(opt):
    """day 3, the owner's hip remark (25.10): the hip anchor sits 4.3 cm BELOW the pelvis box (Dog.cs:589-603), the game
    hides the gap with a picture (AddHipBridge, Dog.cs:621, 640-660). Options, all keeping the total mass 25 kg:
    A: anchor moved into the box (HIP_INSET above the bottom face) along the thigh's STAND axis (thigh world 12 deg at
       build + 12 deg stand rotation = 24 deg), thigh lengthened by the same distance, thigh mass kept (inertia from
       the longer box): the knee, shin and paw at the stand stay where they were;
    B: anchor kept; the pelvis box extended downward until the anchor is HIP_INSET above its bottom face, same mass;
    C: chest pitch raised (less nose-down) until the pelvis box comes down to the anchor (same inset); stand joint
       angles kept, so the front legs and the trunk change attitude.
    Returns the saved globals for restore_hip_opt."""
    global CHEST_PITCH, THIGH_EXTRA, HIP_SHIFT, PELVIS_DY
    saved = (CHEST_PITCH, THIGH_EXTRA, HIP_SHIFT, PELVIS_DY, SEG["thigh"], SEG["pelvis"])
    if not opt:
        return saved
    y0, hh = hip_in_pelvis()
    target = -hh + 2 * hh * HIP_INSET
    if opt == "A":
        dy = target - y0
        a = math.radians(-HIP_ANG - HIP_STAND_Q)   # thigh world angle at the stand
        d = dy / math.cos(a)
        THIGH_EXTRA = d
        HIP_SHIFT = (-d * math.sin(a), dy)
        SEG["thigh"] = (SEG["thigh"][0], SEG["thigh"][1] + d, SEG["thigh"][2])
    elif opt == "B":
        h0 = SEG["pelvis"][1]
        bottom = y0 - 2 * hh * HIP_INSET            # new bottom face (pelvis frame), top face stays at +h0/2
        h1 = h0 / 2 - bottom
        SEG["pelvis"] = (SEG["pelvis"][0], h1, SEG["pelvis"][2])
        PELVIS_DY = -(h1 - h0) / 2
    elif opt == "C":
        lo, hi = CHEST_PITCH, 40.0
        for _ in range(60):
            CHEST_PITCH = 0.5 * (lo + hi)
            y, _ = hip_in_pelvis()
            if y < target:
                lo = CHEST_PITCH
            else:
                hi = CHEST_PITCH
    else:
        raise ValueError(opt)
    return saved


def restore_hip_opt(saved):
    global CHEST_PITCH, THIGH_EXTRA, HIP_SHIFT, PELVIS_DY
    CHEST_PITCH, THIGH_EXTRA, HIP_SHIFT, PELVIS_DY, SEG["thigh"], SEG["pelvis"] = saved


def _build(skin, paw_mu, body_mu, solref, solimp, solreflimit, solimplimit, integrator, jfric_scale, iterations, cone,
           noslip, impratio, jaw_hinge, visuals, jaw_spring, jaw_limit):
    L, start_y = layout()
    vis = visual_geoms(L, start_y) if visuals else {}
    pair_paw = math.sqrt(paw_mu * GROUND_MU)
    pair_body = math.sqrt(body_mu * GROUND_MU)
    pair_limb = math.sqrt(0.4 * GROUND_MU)  # limbs carry no material -> Unity default 0.4
    floor_mu = min(pair_paw, pair_body, pair_limb)
    children = {}
    for c, p in PARENT.items():
        children.setdefault(p, []).append(c)
    order = ["chest", "neck", "head", "jaw"] + ["tail%d" % i for i in range(N_TAIL)]
    out = []
    a = out.append

    def world(pt):  # Unity root frame -> MuJoCo world
        return (pt[0], pt[1] + start_y + 2.0)

    def emit(body, parent_origin, ind):
        cen, deg, anc = L[body]
        origin = world(anc) if anc is not None else world(cen)
        rel = (origin[0] - parent_origin[0], origin[1] - parent_origin[1])
        w, h = size_of(body)
        m = mass(seg_of(body))
        I = izz(m, w, h)
        gc = world(cen)
        gp = (gc[0] - origin[0], gc[1] - origin[1])
        if body == "pelvis":
            gp = (gp[0], gp[1] + PELVIS_DY)
        a('%s<body name="%s" pos="%.6f 0 %.6f">' % (ind, body, rel[0], rel[1]))
        if body == "pelvis":
            a('%s  <joint name="rootx" type="slide" axis="1 0 0" limited="false"/>' % ind)
            a('%s  <joint name="rootz" type="slide" axis="0 0 1" limited="false"/>' % ind)
            a('%s  <joint name="rooty" type="hinge" axis="0 -1 0" limited="false"/>' % ind)
        elif body == "jaw":
            if jaw_hinge:
                sp = ' stiffness="%g" springref="0"' % jaw_spring if jaw_spring > 0 else ""  # option, NOT in the game
                if jaw_limit:  # solreflimit for the jaw stop only (Box2D's limit is a hard velocity stop)
                    sp += ' solreflimit="%s"' % jaw_limit
                a('%s  <joint name="jaw" type="hinge" axis="0 1 0" range="%g %g"%s/>' % (ind, JOINT["jaw"][2], JOINT["jaw"][3], sp))
        else:
            j = joint_of(body)
            lo, hi = JOINT[j][2], JOINT[j][3]
            extra = ' stiffness="%g" springref="0"' % TAIL_SPRING if j == "tail" else ""
            a('%s  <joint name="%s" type="hinge" axis="0 1 0" range="%g %g"%s/>' % (ind, JNAME[body], lo, hi, extra))
        a('%s  <inertial pos="%.6f 0 %.6f" mass="%.6f" diaginertia="%.8f %.8f %.8f"/>' % (ind, gp[0], gp[1], m, I, I, I))
        sg = seg_of(body)
        if sg == "paw":
            mu, ct = pair_paw, 1
        elif sg in ("upper", "lower", "thigh", "shin"):
            mu, ct = pair_limb, 1
        else:
            mu, ct = pair_body, 1
        if body == "jaw":
            ct = 0
        rgba = {"paw": "0.25 0.2 0.15 1", "chest": "0.75 0.55 0.35 1", "pelvis": "0.7 0.5 0.3 1"}.get(sg, "0.8 0.62 0.42 1")
        if body.endswith("l") and sg in ("upper", "lower", "thigh", "shin", "paw"):
            rgba = "0.55 0.42 0.3 1"
        depth = 0.04
        if visuals and body == "jaw":
            depth, rgba = 0.042, JAW_COLOR  # drawn in front of the head box (game sorting order jaw 3 > head 2)
        a('%s  <geom name="%s_g" type="box" pos="%.6f 0 %.6f" euler="0 %.4f 0" size="%.5f %g %.5f" friction="%.4f 0.005 0.0001" contype="%d" rgba="%s"/>'
          % (ind, body, gp[0], gp[1], -deg, w / 2, depth, h / 2, mu, ct, rgba))
        for (vn, vc, vdeg, vw, vh, vd, vrgba) in vis.get(body, []):
            a('%s  <geom name="%s" type="box" pos="%.6f 0 %.6f" euler="0 %.4f 0" size="%.5f %g %.5f" contype="0" conaffinity="0" rgba="%s"/>'
              % (ind, vn, vc[0], vc[1], -vdeg, vw, vd, vh, vrgba))
        kids = children.get(body, [])
        kids = sorted(kids, key=lambda k: order.index(k) if k in order else 100 + list(PARENT).index(k))
        for k in kids:
            emit(k, origin, ind + "  ")
        a("%s</body>" % ind)

    a('<mujoco model="dog2d">')
    a('  <compiler angle="degree" inertiafromgeom="false"/>')
    a('  <option timestep="%g" gravity="0 0 -9.81" integrator="%s" impratio="%g" cone="%s" iterations="%d" noslip_iterations="%d"/>'
      % (DT, integrator, impratio, cone, iterations, noslip))
    a('  <visual><global offwidth="1280" offheight="720"/></visual>')
    a('  <default>')
    a('    <joint limited="true" solreflimit="%s" solimplimit="%s" damping="0" armature="0"/>' % (solreflimit, solimplimit))
    a('    <geom contype="1" conaffinity="0" condim="3" solref="%s" solimp="%s"/>' % (solref, solimp))
    a('  </default>')
    a('  <worldbody>')
    a('    <light pos="0 -3 3" dir="0 1 -1"/>')
    a('    <geom name="floor" type="plane" pos="0 0 %g" size="60 2 0.1" contype="0" conaffinity="1" friction="%.4f 0.005 0.0001" rgba="0.45 0.55 0.4 1"/>'
      % (skin, floor_mu))
    emit("pelvis", (0.0, 0.0), "    ")
    a('  </worldbody>')
    a('  <actuator>')
    for jn in JOINTS_ORDER:
        b = BODY_OF_J[jn]
        j = joint_of(b)
        g = max(JOINT[j][4], JOINT[j][5])
        if j == "tail" and jn != "tail0":
            continue  # only the first tail joint has muscles (Dog.cs:729-730)
        a('    <motor name="m_%s" joint="%s" gear="%g" ctrlrange="-1 1" ctrllimited="true"/>' % (jn, jn, g))
    for jn in JOINTS_ORDER:
        b = BODY_OF_J[jn]
        k, _ = keff(PARENT[b], b)
        k *= jfric_scale
        cap = JOINT[joint_of(b)][1]
        a('    <general name="f_%s" joint="%s" gainprm="0" biastype="affine" biasprm="0 0 %.6f" forcerange="%g %g" forcelimited="true"/>'
          % (jn, jn, -k, -cap, cap))
    if jaw_hinge:  # appended last: every day-1 actuator keeps its index
        k, _ = keff("head", "jaw")
        a('    <motor name="m_jaw" joint="jaw" gear="%g" ctrlrange="-1 1" ctrllimited="true"/>' % JOINT["jaw"][4])
        a('    <general name="f_jaw" joint="jaw" gainprm="0" biastype="affine" biasprm="0 0 %.6f" forcerange="%g %g" forcelimited="true"/>'
          % (-k * jfric_scale, -JOINT["jaw"][1], JOINT["jaw"][1]))
    a('  </actuator>')
    a('</mujoco>')
    return "\n".join(out) + "\n", start_y


if __name__ == "__main__":
    # python build_dog.py [out.xml] [tail_len] [jaw_hinge 0/1] [visuals 0/1]; no extra args = the 1:1 day-1 dog2d.xml
    path = sys.argv[1] if len(sys.argv) > 1 else "dog2d.xml"
    tl = float(sys.argv[2]) if len(sys.argv) > 2 else 0.20
    jh = bool(int(sys.argv[3])) if len(sys.argv) > 3 else False
    vz = bool(int(sys.argv[4])) if len(sys.argv) > 4 else False
    xml, sy = build(tail_len=tl, jaw_hinge=jh, visuals=vz)
    open(path, "w", encoding="utf-8", newline="\n").write(xml)
    L, _ = layout()
    tot = sum(mass(seg_of(b)) for b in L)
    print("wrote", path, "start_y %.5f  mass %.4f  scale %.5f" % (sy, tot, MASS_SCALE))
    for b in L:
        w, h = size_of(b)
        m = mass(seg_of(b))
        print("  %-9s m %.4f  I %.6f  centre (%.4f, %.4f) deg %.1f" % (b, m, izz(m, w, h), L[b][0][0], L[b][0][1], L[b][1]))
    for jn in JOINTS_ORDER:
        b = BODY_OF_J[jn]
        k, ie = keff(PARENT[b], b)
        print("  %-10s K %.4g Ieff %.5g Ieff/dt %.4g -> K_eff %.4g cap %g" % (jn, JOINT[joint_of(b)][0], ie, ie / DT, k, JOINT[joint_of(b)][1]))
