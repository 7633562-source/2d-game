"""build_model.py - writes human2d.xml, the planar MuJoCo twin of the Unity human (rl-1024, day 1).

Coordinates: MuJoCo x = Unity x (forward, the man faces +x), MuJoCo z = Unity y + 2.0
(the visual ground top y = -2.0 becomes z = 0). The spawn root (Unity y = -0.82) is z = 1.18.
Sign convention: every child hinge has axis (0 1 0), so +q is a CLOCKWISE turn of the child
relative to its parent in the x-right / z-up view = Unity jointAngle (parent angle minus child angle).
The planar root hinge has axis (0 -1 0), so its q is the Unity pelvis angle (CCW +) = pelvisTilt.

Box2D details mapped by hand (skeleton.md section 8):
- contact skin: Box2D pads every polygon by the contact offset (0.01 m), so bodies rest ~0.02 m
  above the drawn ground (measured in T1: soles rest at y = -1.980). The floor plane sits at z = SKIN.
- joint friction (JointFriction.cs): tau = clamp(-K w, +-|w| Ieff/dt, +-maxTorque). The one-step
  stop cap is linear in w, so K_eff = min(K, Ieff/dt) with Ieff = 1/(1/I1 + 1/I2) of the two
  segments' own inertia; it is an affine bias actuator with forcerange +-maxTorque.
- pair friction sqrt(mu1 mu2): MuJoCo takes the max of the two geoms, so the floor gets 0.4,
  soles 0.693, palms 0.748, the rest 0.4.
"""
import math
import sys

DT = 0.005
LAT = math.cos(math.radians(80.0))
HIPX = 0.085 * LAT
SHX = 0.185 * LAT
M = 70.0
FR = dict(pelvis=0.1117, torso=0.3229, head=0.0694, neck=0.012, upper=0.0271, lower=0.0162,
          hand=0.0061, thigh=0.1416, shin=0.0433, foot=0.0137)
SIZE = dict(pelvis=(0.24, 0.16), torso=(0.24, 0.34), neck=(0.12, 0.09), head=(0.20, 0.23),
            upper=(0.10, 0.33), lower=(0.08, 0.34), hand=(0.06, 0.16), thigh=(0.16, 0.43),
            shin=(0.11, 0.43), heel=(0.182, 0.09), toe=(0.078, 0.09))
HEEL_SHARE = 0.7


def mass(seg):
    if seg == "heel":
        return M * FR["foot"] * HEEL_SHARE
    if seg == "toe":
        return M * FR["foot"] * (1 - HEEL_SHARE)
    return M * FR[seg]


def izz(seg):
    w, l = SIZE[seg]
    return mass(seg) * (w * w + l * l) / 12.0


# joint: (parent seg, child seg, K, maxTorque, lo, hi, flexor Nm, extensor Nm)
JOINTS = {
    "lumbar": ("pelvis", "torso", 30.0, 40.0, -20, 20, 90, 90),
    "neck": ("torso", "neck", 0.00784, 15.0, -30, 30, 15, 15),
    "head": ("neck", "head", 0.00784, 15.0, -30, 30, 15, 15),
    "shoulder": ("torso", "upper", 0.177722, 15.0, -160, 90, 40, 40),
    "elbow": ("upper", "lower", 0.0509677, 15.0, -165, 0, 30, 30),
    "wrist": ("lower", "hand", 0.0117525, 15.0, -60, 60, 10, 10),
    "hip": ("pelvis", "thigh", 12.0, 15.0, -90, 45, 100, 100),
    "knee": ("thigh", "shin", 12.0, 15.0, 0, 120, 130, 130),
    "ankle": ("shin", "heel", 3.0, 15.0, -45, 45, 22, 65),
    "mtp": ("heel", "toe", 0.02, 15.0, -35, 50, 12, 12),
}


def keff(j):
    p, c, K = JOINTS[j][:3]
    ie = 1.0 / (1.0 / izz(p) + 1.0 / izz(c))
    return min(K, ie / DT), ie


def build(skin=0.02, solref="0.01 1", solimp="0.95 0.99 0.001", solreflimit="0.01 1",
          solimplimit="0.95 0.99 0.001", fric_scale=1.0, sole=0.693, palm=0.748, rest=0.4,
          integrator="implicitfast", jfric=True, impratio=1.0, noslip=0, cone="pyramidal",
          margin=0.0, iterations=50, jfric_scale=1.0, jcap_scale=1.0, fixed_root=False, jmargin=0.0, foot1=False):
    """foot1 (day 5, v9 variant): one rigid foot segment per leg (heel + toe merged, no MTP hinge): a uniform box
    0.26 x 0.09 m (Unity foot box before its 70/30 split, skeleton.md section 2), mass 0.959 kg, Izz about its own
    centre 0.006050 kg m^2, sole from the heel back edge (ankle - 0.065) to the toe tip (ankle + 0.195). Built as two
    welded bodies so the 62/13 policy layout keeps working: heel_<s> carries the REAR HALF of the sole (heel_<s>_g,
    0.13 m), toe_<s> (no joint, origin at the old MTP point ankle + 0.117) carries the FRONT HALF (toe_<s>_g); each half
    has half the mass, so the welded pair is exactly the uniform box. The policy-layout MTP hinges become placeholder
    joints on two detached massive dummies far from the man (no geoms, no contact); the env feeds their q/qd as 0."""
    out = []
    a = out.append

    def body_open(name, pos, seg, ind, geom_pos, fr, joint=None):
        m = mass(seg)
        I = izz(seg)
        w, l = SIZE[seg]
        a('%s<body name="%s" pos="%.6f 0 %.6f">' % (ind, name, pos[0], pos[1]))
        if joint:
            jn, lo, hi = joint
            a('%s  <joint name="%s" type="hinge" axis="0 1 0" range="%g %g"/>' % (ind, jn, lo, hi))
        a('%s  <inertial pos="%.6f 0 %.6f" mass="%.6f" diaginertia="%.7f %.7f %.7f"/>'
          % (ind, geom_pos[0], geom_pos[1], m, I, I, I))
        a('%s  <geom name="%s_g" type="box" pos="%.6f 0 %.6f" size="%.5f 0.05 %.5f" friction="%.4f 0.005 0.0001"/>'
          % (ind, name, geom_pos[0], geom_pos[1], w / 2, l / 2, fr * fric_scale))

    a('<mujoco model="human2d">')
    a('  <compiler angle="degree" inertiafromgeom="false"/>')
    a('  <option timestep="%g" gravity="0 0 -9.81" integrator="%s" impratio="%g" cone="%s" iterations="%d" noslip_iterations="%d"/>'
      % (DT, integrator, impratio, cone, iterations, noslip))
    a('  <default>')
    a('    <joint limited="true" solreflimit="%s" solimplimit="%s" margin="%g" damping="0" armature="0"/>'
      % (solreflimit, solimplimit, jmargin))
    a('    <geom contype="1" conaffinity="0" condim="3" solref="%s" solimp="%s" margin="%g" rgba="0.7 0.6 0.5 1"/>'
      % (solref, solimp, margin))
    a('  </default>')
    a('  <worldbody>')
    a('    <light pos="0 -3 3" dir="0 1 -1"/>')
    a('    <geom name="floor" type="plane" pos="0 0 %g" size="20 2 0.1" contype="0" conaffinity="1" friction="%.4f 0.005 0.0001" rgba="0.4 0.5 0.4 1"/>'
      % (skin, rest * fric_scale))
    # root = pelvis centre, Unity root y -0.82 -> z 1.18; pelvis centre local y -0.17
    if fixed_root:  # T3: pelvis welded to the world, feet clear of the floor
        a('    <body name="pelvis" pos="0 0 2.01">')
    else:
        a('    <body name="pelvis" pos="0 0 1.01">')
        a('      <joint name="rootx" type="slide" axis="1 0 0" limited="false"/>')
        a('      <joint name="rootz" type="slide" axis="0 0 1" limited="false"/>')
        a('      <joint name="rooty" type="hinge" axis="0 -1 0" limited="false"/>')
    a('      <inertial pos="0 0 0" mass="%.6f" diaginertia="%.7f %.7f %.7f"/>'
      % (mass("pelvis"), izz("pelvis"), izz("pelvis"), izz("pelvis")))
    a('      <geom name="pelvis_g" type="box" size="0.12 0.05 0.08" friction="%.4f 0.005 0.0001"/>' % (rest * fric_scale))
    # positions below are relative to the parent body frame (= parent joint anchor)
    # chest: lumbar at pelvis top (local +0.08), chest centre +0.17 above it
    body_open("torso", (0, 0.08), "torso", "      ", (0, 0.17), rest, ("lumbar", -20, 20))
    body_open("neck", (0, 0.34), "neck", "        ", (0, 0.045), rest, ("neck", -30, 30))
    body_open("head", (0, 0.09), "head", "          ", (0, 0.115), rest, ("head", -30, 30))
    a("          </body>")
    a("        </body>")
    # arms: shoulder in chest-joint frame: chest joint at root y -0.09, shoulder root y +0.16 -> +0.25
    for side, d in (("r", -1), ("l", 1)):
        sx = d * SHX
        body_open("upper_" + side, (sx, 0.25), "upper", "        ", (0, -0.165), rest, ("shoulder_" + side, -160, 90))
        body_open("lower_" + side, (0, -0.33), "lower", "          ", (0, -0.17), rest, ("elbow_" + side, -165, 0))
        body_open("hand_" + side, (0, -0.34), "hand", "            ", (0, -0.08), palm, ("wrist_" + side, -60, 60))
        a("            </body>")
        a("          </body>")
        a("        </body>")
    a("      </body>")
    # legs: hip at root y -0.25 = pelvis local -0.08; left leg -1, right +1
    for side, d in (("r", 1), ("l", -1)):
        hx = d * HIPX
        body_open("thigh_" + side, (hx, -0.08), "thigh", "      ", (0, -0.215), rest, ("hip_" + side, -90, 45))
        body_open("shin_" + side, (0, -0.43), "shin", "        ", (0, -0.215), rest, ("knee_" + side, 0, 120))
        if foot1:
            fm = (mass("heel") + mass("toe")) / 2.0
            fi = fm * (0.13 ** 2 + 0.09 ** 2) / 12.0
            ind = "          "
            a('%s<body name="heel_%s" pos="0 0 -0.430000">' % (ind, side))
            a('%s  <joint name="ankle_%s" type="hinge" axis="0 1 0" range="-45 45"/>' % (ind, side))
            a('%s  <inertial pos="0 0 0" mass="%.6f" diaginertia="%.7f %.7f %.7f"/>' % (ind, fm, fi, fi, fi))
            a('%s  <geom name="heel_%s_g" type="box" pos="0 0 0" size="0.06500 0.05 0.04500" friction="%.4f 0.005 0.0001"/>'
              % (ind, side, sole * fric_scale))
            a('%s  <body name="toe_%s" pos="0.117000 0 0">' % (ind, side))
            a('%s    <inertial pos="0.013000 0 0" mass="%.6f" diaginertia="%.7f %.7f %.7f"/>' % (ind, fm, fi, fi, fi))
            a('%s    <geom name="toe_%s_g" type="box" pos="0.013000 0 0" size="0.06500 0.05 0.04500" friction="%.4f 0.005 0.0001"/>'
              % (ind, side, sole * fric_scale))
        else:
            # ankle at shin bottom; heel centre 0.026 ahead of the ankle, same height
            body_open("heel_" + side, (0, -0.43), "heel", "          ", (0.026, 0), sole, ("ankle_" + side, -45, 45))
            # MTP at heel front edge (ankle +0.117), toe centre +0.039 ahead of it
            body_open("toe_" + side, (0.117, 0), "toe", "            ", (0.039, 0), sole, ("mtp_" + side, -35, 50))
        a("            </body>")
        a("          </body>")
        a("        </body>")
        a("      </body>")
    a("    </body>")
    if foot1:  # layout placeholders for mtp_r / mtp_l (detached, no geoms): q/qd fed to the policy as 0
        for k, side in enumerate(("r", "l")):
            a('    <body name="mtpdummy_%s" pos="%d 0 5">' % (side, 50 + k))
            a('      <joint name="mtp_%s" type="hinge" axis="0 1 0" range="-35 50"/>' % side)
            a('      <inertial pos="0 0 0" mass="%.6f" diaginertia="%.7f %.7f %.7f"/>' % (mass("toe"), izz("toe"), izz("toe"), izz("toe")))
            a("    </body>")
    a("  </worldbody>")
    a("  <actuator>")
    names = []
    for j in ("lumbar", "neck", "head"):
        names.append((j, j))
    for side in ("r", "l"):
        for j in ("shoulder", "elbow", "wrist"):
            names.append((j + "_" + side, j))
    for side in ("r", "l"):
        for j in ("hip", "knee", "ankle", "mtp"):
            names.append((j + "_" + side, j))
    # torque motors: ctrl s in [-1,1], gear = max(flexor, extensor); the env scales s<0 on the ankle
    for jn, j in names:
        g = max(JOINTS[j][6], JOINTS[j][7])
        a('    <motor name="m_%s" joint="%s" gear="%g" ctrlrange="-1 1" ctrllimited="true"/>' % (jn, jn, g))
    if jfric:
        for jn, j in names:
            k, _ = keff(j)
            if foot1 and j == "ankle":  # one-step-stop cap with the merged foot inertia
                fI = (mass("heel") + mass("toe")) * (0.26 ** 2 + 0.09 ** 2) / 12.0
                k = min(JOINTS[j][2], 1.0 / (1.0 / izz("shin") + 1.0 / fI) / DT)
            k *= jfric_scale
            cap = JOINTS[j][3] * jcap_scale
            a('    <general name="f_%s" joint="%s" gainprm="0" biastype="affine" biasprm="0 0 %.6f" forcerange="%g %g" forcelimited="true"/>'
              % (jn, jn, -k, -cap, cap))
    a("  </actuator>")
    a("</mujoco>")
    return "\n".join(out) + "\n", names


if __name__ == "__main__":
    # usage: build_model.py [out.xml] [--foot1]; the pilot xml uses final_kw.json (jfric_scale 0.8, solref 0.01 1)
    args = [x for x in sys.argv[1:] if not x.startswith("--")]
    import json, os
    kw = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "final_kw.json")))
    xml, names = build(foot1="--foot1" in sys.argv, **kw)
    path = args[0] if args else "human2d.xml"
    open(path, "w", encoding="utf-8", newline="\n").write(xml)
    tot = sum(mass(s) * (2 if s not in ("pelvis", "torso", "neck", "head") else 1)
              for s in ("pelvis", "torso", "neck", "head", "upper", "lower", "hand", "thigh", "shin", "heel", "toe"))
    print("wrote", path, "analytic mass %.4f" % tot)
    for j in JOINTS:
        k, ie = keff(j)
        print("  %-8s K %.5g Ieff %.5g Ieff/dt %.4g -> K_eff %.4g cap %g" % (j, JOINTS[j][2], ie, ie / DT, k, JOINTS[j][3]))
