using UnityEngine;

// Capture-walk inverted-pendulum plan. Sole owner of omega0, xi, and the
// landing X. Reads CoM / hip / ankle positions and CoM velocity. Does not
// read walkPhase, CaptureWalkState, stanceFoot or swingFoot: whether this
// is a periodic step or a recovery, and whether a glued re-plan should
// keep the previous start, are inputs from the parent.
//
//     omega0 = sqrt(g / h)         h = CoM height over the stance sole
//     xi     = x_com + v / omega0
//     xi(T)  = p + (xi - p) e^{ωT}   p = stance sole, while it holds
//     b*     = v_des * T / (exp(omega0 * T) - 1)   cruise only
//     x_foot = xi(T) - b* + kv * (v - v_des)
//
// Landing ON xi(T) stops the pendulum at touchdown. A gait may sit a little
// behind that landing point so residual speed remains. If freeze-xi is already
// past the stance sole, b* is zero: that is a catch, not a cruise. Euler v*T is the
// travel of a free particle, not of a pendulum: from rest it is 0, so the
// first plant sits on freeze-xi and then b* / kv put the sole behind the
// capture point. The parent freezes the plan once; this class does not
// track the swing.
public sealed class CapturePointPlanner
{
    public struct Model
    {
        public float lookahead;
        public float speedGain;
        public float stepDuration;
        public float recoveryDuration;
        public float stepMin;
        public float stepMax;
        public float reachMargin;
        public float recoveryMargin;
        public float stanceLegLength;
        public float landLegLength;
        public float thighLength;
        public float shinLength;
        public float swingApex;
        public float footLandLift;
        public int abandonPutDown;
        // Xi this far ahead of the stance sole (foot centre) is a true
        // catch: land ON xi(T), b* = 0. Must be the toe arm, not 4 cm —
        // a walking plant already sits ~8 cm behind live xi.
        public float catchingPast;
    }

    public struct Body
    {
        public float comX;
        public float comVelX;
        public float omega0;
        public float dir;
        public float vDes;
        public bool recovery;
        public int swingSide;
        public float swingAnkleX;
        public float swingAnkleY;
        public float hipX;
        public float hipY;
        public float groundY;
        public float soleThickness;
        public float stanceX;
        public bool airborne;
        public int abandonStreak;
        public bool holdStart;
        public float heldStartX;
        public float heldStartY;
        public float heldApex;
    }

    public struct Sample
    {
        public float omega0;
        public float xi;
    }

    public struct Plan
    {
        public bool valid;
        public bool recovery;
        public int swingSide;
        public float dir;
        public float startX;
        public float startY;
        public float landAnkleY;
        public float apex;
        public float targetX;
        public float duration;
        public float omega0;
        public float capturePointX;
        public float bStar;
        public float speedAtFreeze;
        public float speedDesired;
        public float stanceX;
        public float hipLandX;
        public float reachSpan;
    }

    public Sample Last { get; private set; }
    public Plan LastPlan { get; private set; }

    // Live capture point for the gait. Height is the loaded sole, not y = -2,
    // so a curb does not invent a different omega0. Floor 0.20 m keeps the
    // pendulum defined if the CoM reading drops through the slab.
    public Sample Measure(float comX, float comY, float comVelX, float groundY)
    {
        float height = Mathf.Max(0.20f, comY - groundY);
        float omega0 = Mathf.Sqrt(9.81f / height);
        var sample = new Sample
        {
            omega0 = omega0,
            xi = comX + comVelX / omega0
        };
        Last = sample;
        return sample;
    }

    // One footstep, written from positions and velocity on the release frame.
    // targetX is the LIPM capture point at touchdown, not freeze-xi and not
    // Euler v*T. While the stance sole holds, xi grows as e^{ωT} about that
    // sole. A free-particle lookahead is 0 from rest, so the first plant sits
    // behind live xi and the swing arrives after the trunk has already gone.
    public Plan Freeze(in Model model, in Body body)
    {
        float t = body.recovery
            ? Mathf.Max(0.05f, model.recoveryDuration)
            : Mathf.Max(0.05f, model.stepDuration);
        float omega = Mathf.Max(0.5f, body.omega0);
        float xiNow = body.comX + body.comVelX / omega;
        float look = Mathf.Max(0f, model.lookahead);
        // ξ(t) = p + (ξ − p) e^{ωt}. lookahead = 1 is that full prediction;
        // 0 keeps freeze-xi (the old Euler miss from rest).
        float growth = Mathf.Exp(omega * t);
        float xiExp = body.stanceX + (xiNow - body.stanceX) * growth;
        float xiLand = xiNow + (xiExp - xiNow) * look;
        // b* is the cruise offset behind landing-xi. From rest the full
        // v_des-sized offset is a 10 cm plant behind freeze-xi, which the
        // gait's 6 cm lostFwd aborts at 0.10 s — the first step never lands.
        // b*=0 belongs to a catch past the TOE (CoM rotating about the
        // tip). 4 cm past the foot centre is still on the plate, and it
        // is the residual of a good plant: zeroing b* there turned every
        // second freeze into a stop (capwalk7 vault vs post-plant squat).
        float bStar = 0f;
        float catchArm = model.catchingPast > 0.001f ? model.catchingPast : 0.13f;
        bool catching = (xiNow - body.stanceX) * body.dir > catchArm;
        if (!body.recovery && !catching && Mathf.Abs(body.vDes) > 0.0001f)
        {
            float denom = growth - 1f;
            if (denom > 0.0001f)
            {
                bStar = body.vDes * t / denom;
                float vRatio = Mathf.Clamp01(
                    Mathf.Abs(body.comVelX)
                    / Mathf.Max(0.05f, Mathf.Abs(body.vDes)));
                bStar *= vRatio;
            }
        }
        float landY = body.groundY + body.soleThickness
                      + Mathf.Max(0f, model.footLandLift);
        // Hip of the same pendulum: p + (x−p) cosh(ωT) + (v/ω) sinh(ωT).
        // Bounding against freeze-time hip + v*T truncates the catch to the
        // instantaneous footprint the same way Euler truncates xi.
        float hipCosh = 0.5f * (growth + 1f / growth);
        float hipSinh = 0.5f * (growth - 1f / growth);
        float hipExp = body.stanceX
                       + (body.hipX - body.stanceX) * hipCosh
                       + (body.comVelX / omega) * hipSinh;
        float hipLandX = body.hipX + (hipExp - body.hipX) * look;
        // putDown is only for a hanging airborne swing that cannot reach the
        // slab. A still-touching swing that failed twice is the opposite
        // problem: replacing that target with hipX is a step of length 0.
        bool putDown = model.abandonPutDown > 0
                       && body.abandonStreak >= model.abandonPutDown
                       && body.airborne;
        float freezeDrop = body.hipY - landY;
        float freezeSpan = ReachSpanX(freezeDrop, model);
        float xFoot = putDown
            ? hipLandX
            : PlaceFootX(xiLand, xiNow, bStar, body.comVelX, body.vDes,
                body.dir, body.stanceX, body.recovery, model);
        // Recovery must land ON xi, not on the hip circle. ClampToReach
        // after a 120° squat pins targetX under the hip (capwalk16/17
        // catchSlide was a no-op: start ≈ target, the sole never left).
        // SolveCaptureLegIk still cannot exceed the bars; it just is not
        // retracted before the solve.
        // Periodic ClampToReach used to win over PlaceFootX: tilt-seeded
        // xiLand ~0.31 m, land-chord span ~0.26 m, plant 5–10 cm behind
        // where CoM arrives. Two of those plants is burn_walk 18:24 —
        // swap 2, supportMargin −0.174 at walk start +1.30 s, GETUP
        // 6.355. Keep the circle when it still catches; if it would
        // plant behind landing-xi, restore PlaceFootX's floor.
        if (!body.recovery)
        {
            float clamped = ClampToReach(xFoot, hipLandX, freezeDrop, model);
            float maxBehind = Mathf.Max(0f, model.recoveryMargin - 0.005f);
            if ((xiLand - clamped) * body.dir > maxBehind)
                xFoot = xiLand - maxBehind * body.dir;
            else
                xFoot = clamped;
            if ((xFoot - xiNow) * body.dir < 0f)
                xFoot = xiNow;
            float floor = Mathf.Max(0f, model.stepMin);
            float origin = body.stanceX;
            if ((xFoot - origin) * body.dir < floor)
                xFoot = origin + floor * body.dir;
            float step = Mathf.Clamp((xFoot - origin) * body.dir,
                floor, Mathf.Abs(model.stepMax));
            xFoot = origin + step * body.dir;
        }
        // Apex is a CLEARANCE over the landing height, not something added to
        // wherever the ankle happens to hang. A re-plan frozen with the foot
        // already in the air must not re-add the full apex.
        float apex = putDown
            ? 0f
            : Mathf.Max(0f, Mathf.Max(0f, model.swingApex)
                            - Mathf.Max(0f, body.swingAnkleY - landY));

        float startX = body.swingAnkleX;
        float startY = body.swingAnkleY;
        if (body.holdStart)
        {
            startX = body.heldStartX;
            startY = body.heldStartY;
            apex = Mathf.Max(apex, body.heldApex);
        }

        var plan = new Plan
        {
            valid = true,
            recovery = body.recovery,
            swingSide = body.swingSide >= 0 ? 1 : -1,
            dir = body.dir,
            startX = startX,
            startY = startY,
            landAnkleY = landY,
            apex = apex,
            targetX = xFoot,
            duration = t,
            omega0 = body.omega0,
            capturePointX = xiLand,
            bStar = bStar,
            speedAtFreeze = body.comVelX,
            speedDesired = body.vDes,
            stanceX = body.stanceX,
            hipLandX = hipLandX,
            reachSpan = Mathf.Max(0f, freezeSpan)
        };
        LastPlan = plan;
        return plan;
    }

    // Half-width of the footprint the two bars can put a sole into `drop`
    // metres below the hip: sqrt(reach^2 - drop^2). Negative = the bars are
    // not resolved (or the bound is off), so the caller keeps its own number.
    public float ReachSpanX(float drop, in Model model)
    {
        float bars = model.thighLength + model.shinLength;
        if (bars < 0.1f) return -1f;
        float margin = Mathf.Clamp01(model.reachMargin);
        if (margin <= 0.0001f) return -1f;
        // The circle has to be STRICTLY larger than the pose the walker stands
        // in. Flooring margin at stanceLegLength makes drop == reach, so the
        // span is identically zero and every landing point pins under the hip.
        float stance = Mathf.Clamp01(model.stanceLegLength);
        margin = Mathf.Max(margin, Mathf.Min(1f, stance + 0.02f));
        float reach = bars * margin;
        float d = Mathf.Abs(drop);
        // Landing may be more flexed than mid-stance. Using the high freeze
        // hip as drop made span ≈ 0.17 m, so xi(T) past that was clipped and
        // the sole planted behind CoM (capwalk7: 3 steps, then vault over
        // the toe). Cap drop at the landing chord; the swing hip falls onto
        // that pose at T. Stance knee target is untouched.
        float landFrac = Mathf.Clamp01(model.landLegLength);
        if (landFrac > 0.10f)
        {
            float landDrop = bars * landFrac;
            if (landDrop < d)
                d = landDrop;
        }
        if (d >= reach) return 0f;
        return Mathf.Sqrt(reach * reach - d * d);
    }

    // x_foot = xiLand - b* + kv*(v - v_des), then a stride floor from the
    // SUPPORT. Measuring from the trailing swing ankle made a stacked first
    // step 5 cm from the back sole, which is still behind freeze-xi. A plant
    // behind freeze-xi is already a miss: lostFwd aborts at 0.10 s and the
    // sole never arrives. Cruise may sit behind landing-xi, but not further
    // than recoveryMargin or the same abort fires at plant.
    private static float PlaceFootX(float xiLand, float xiNow, float bStar,
                                    float comVelX, float vDes, float dir,
                                    float stanceX, bool recovery, in Model model)
    {
        float xFoot = xiLand - bStar
                      + Mathf.Max(0f, model.speedGain) * (comVelX - vDes);
        if (recovery)
            return xFoot;
        float maxBehind = Mathf.Max(0f, model.recoveryMargin - 0.005f);
        if ((xiLand - xFoot) * dir > maxBehind)
            xFoot = xiLand - maxBehind * dir;
        if ((xFoot - xiNow) * dir < 0f)
            xFoot = xiNow;
        float floor = Mathf.Max(0f, model.stepMin);
        float origin = stanceX;
        if ((xFoot - origin) * dir < floor)
            xFoot = origin + floor * dir;
        float step = Mathf.Clamp((xFoot - origin) * dir,
            floor, Mathf.Abs(model.stepMax));
        return origin + step * dir;
    }

    private float ClampToReach(float xFoot, float hipX, float drop, in Model model)
    {
        float span = ReachSpanX(drop, model);
        if (span < 0f) return xFoot;
        return Mathf.Clamp(xFoot, hipX - span, hipX + span);
    }
}
