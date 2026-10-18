using UnityEngine;

public partial class StepPhaseDriver
{
    private bool HeelPlantOn()
    {
        return fallSwingHeelPlant > 0.5f;
    }

    private bool IsSwingHeelLoaded()
    {
        if (bodyState == null) return true;
        return currentStance > 0.5f
            ? bodyState.leftFootHeelLoaded
            : bodyState.rightFootHeelLoaded;
    }

    // Catch/Recover landing. Toe scrape is not a plant: the heel must be
    // the loaded end (airHeel already points the toe up). 0 = any contact.
    private bool IsSwingFootPlanted()
    {
        if (!HeelPlantOn())
            return IsSwingFootGrounded();
        return IsSwingHeelLoaded();
    }

    // Cycle-0 initiation never takes an airborne heel-first plant: geo
    // lift pitches the still-stacked sole onto the toe, so heel-loaded
    // stays false and Catch/Recover have no exit (walkSwap 0). Later
    // cycles keep heel-first — a toe scrape after a real flight is not
    // a catch.
    // Cycle 0 also plants when the sole is inside the combined contactOffset
    // of the slab (comment 20): a reaching 10°/10° pose still sits inside
    // that band before overlap, and waiting for GetContacts-only is
    // sinkAbort / walkSwap=0. Later cycles keep heel-first.
    private bool IsFirstStepPlanted()
    {
        if (FallCatchCycleCount == 0)
            return IsSwingFootGrounded() || SwingFootNearContact();
        return IsSwingFootPlanted();
    }

    private static float ContactOffsetBand()
    {
        return Physics2D.defaultContactOffset * 2f;
    }

    private bool SwingFootNearContact()
    {
        return SwingFootClearance() <= ContactOffsetBand();
    }

    // Ground clearance of the foot that is currently the swing.
    private float SwingFootClearance()
    {
        if (bodyState == null) return 0f;
        return currentStance > 0.5f
            ? bodyState.leftFootGroundClearance
            : bodyState.rightFootGroundClearance;
    }

    // How far ahead of the CoM the catch plant must land, in metres
    // (catchLeadCapture above). Public because BalanceController commands the
    // step that this very method judges: the two must read one number, not a
    // field and a live value that drift apart.
    public float ActiveCatchFootLead()
    {
        float tuned = Mathf.Max(0f, catchFootLead);
        if (catchLeadCapture <= 0.5f)
            return tuned;
        float h = balance != null ? Mathf.Max(0.1f, balance.walkXCoMHeight) : 1f;
        float omega0 = Mathf.Sqrt(9.81f / h);
        float capture = ComVelForwardX() / omega0 + Mathf.Max(0f, catchLeadCaptureMargin);
        // Min() on the floor keeps the clamp well ordered if catchFootLead is
        // ever set below catchFootLeadMin from the Inspector or CLI.
        float floor = Mathf.Min(tuned, Mathf.Max(0f, catchFootLeadMin));
        return Mathf.Clamp(capture, floor, tuned);
    }

    // True when the swing foot centre is ahead of CoM along the walk
    // direction by ActiveCatchFootLead. That is the catch plant: a foot under
    // the hip cannot produce a restoring moment about the CoM.
    private bool IsSwingFootAheadOfCom()
    {
        if (bodyState == null || comCalculator == null) return false;
        Vector2 com = comCalculator.GetCenterOfMass();
        Vector2 swing = currentStance > 0.5f
            ? bodyState.leftFootPosition
            : bodyState.rightFootPosition;
        return (swing.x - com.x) * WalkDir() >= ActiveCatchFootLead();
    }

    // The first flight is what makes a capture plant possible. Stacked
    // dirt never grows AheadOfCom (initiation already walked CoM past both
    // soles). After that flight the plant that becomes stance must be
    // AheadOfCom, or PickStanceFromFeet hands a sole the mass has passed
    // and UpdateState names Falling.
    private bool FirstStepHasFlown()
    {
        return swingWasAirborne || reachAirEnterTime >= 0f;
    }

    private bool CatchFootIsReady()
    {
        if (FallCatchCycleCount == 0 && !FirstStepHasFlown())
            return IsSwingFootAheadOfStance();
        return IsSwingFootAheadOfCom();
    }

    // Cycle-0 Catch plant: swing ahead of the STANCE foot, not of CoM.
    // Initiation lean walks CoM past both stacked soles (PlantComReady
    // allows 0.30 m), so IsSwingFootAheadOfCom then asks for stance-span
    // plus that lead — a slide µ=1.2 cannot make. Span ≥ FootLeadDeadzone
    // is what EnterFallCatchPlant needs to hand the next stance to the
    // front foot instead of flipping two stacked soles.
    private bool IsSwingFootAheadOfStance()
    {
        if (bodyState == null) return false;
        Vector2 swing = currentStance > 0.5f
            ? bodyState.leftFootPosition
            : bodyState.rightFootPosition;
        Vector2 stance = currentStance > 0.5f
            ? bodyState.rightFootPosition
            : bodyState.leftFootPosition;
        // FootLeadDeadzone is the span PickStanceFromFeet needs to hand
        // the next support to the front foot. catchFootLeadMin (3.5 cm)
        // is a CoM-ahead floor; stacked initiation never grows it.
        return (swing.x - stance.x) * WalkDir() >= FootLeadDeadzone;
    }

    // Same band over the commanded stance foot: Plant asks it to decide
    // whether this plant was caught (PlantRolledFromBehind) or whether the
    // mass is still running away from the support. Recover swap does not
    // read this — AheadOfStance can be true with CoM still past the
    // plant, and waiting for the band there was walkSwap 0.
    private bool IsComCaughtByStanceFoot()
    {
        return StanceComLead() <= CaughtLeadBand();
    }

    private float CaughtLeadBand()
    {
        return Mathf.Max(0f, plantCaughtLeadMax);
    }

    // The mass is running past the support. Judged on the capture point as
    // well as the position: the ankle can only hold a CoM whose extrapolated
    // landing point stays inside the sole, and that sole's ceiling is
    // m·g·arm about the toe (balance-actuators.mdc) — the same ceiling a
    // two-foot stand has on a shorter arm. Past this band no stance torque
    // arrests the fall; only a plant ahead of the capture point does.
    // Cycle 0 is excluded: its lean is commanded, and PlantComReady already
    // gives initiation the 0.30 m band.
    private bool ComRunningPastStance()
    {
        if (FallCatchCycleCount == 0)
            return false;
        float bound = Mathf.Max(0.05f, plantComLeadMax);
        return StanceComLead() > bound || StanceLeadCapture > bound;
    }

    // ...and there is a leg free to do it with: the commanded support is
    // loaded, so the other one can be thrown forward, and the body is not
    // already a heap. A stand is the right answer to a collapse; it is the
    // wrong answer to a body that is still tall and moving.
    private bool KeepSteppingOnRunaway()
    {
        return walkRunawayKeepStepping > 0.5f
            && !plantStandSurrender
            && ComRunningPastStance()
            && IsStanceFootGrounded()
            && !SinkCollapsed();
    }

    // +1 = stance on the right (swing left), −1 the other way. Geometric
    // rule for a landing AND for cold BeginWalk: the leading foot carries
    // the weight, so the trailing leg is the one that steps forward.
    // Standing on the trailer hands swing to the already-leading foot and
    // the body walks itself backward (EnterFallCatchPlant).
    private float PickStanceFromFeet(float fallback)
    {
        if (bodyState == null) return fallback;
        // Only a loaded foot can be stance. One foot down decides on its
        // own — otherwise the geometry can hand the role to a leg that is
        // in the air and the next phase has nothing under it.
        bool leftDown = FootDown(true);
        bool rightDown = FootDown(false);
        if (leftDown && !rightDown) return -1f;
        if (rightDown && !leftDown) return 1f;
        // Neither foot is down (cold BeginWalk in the air, or mid-fall). The
        // geometric lead can hand stance to the trailing foot and the brake
        // then holds a leg that will carry nothing. The lower foot lands
        // first, so that is the support the next Plant can actually use.
        // Every landing call site has a foot down, so this is BeginCycle only.
        if (!leftDown && !rightDown && plantAirLowestFoot > 0.5f)
        {
            float leftClear = bodyState.leftFootGroundClearance;
            float rightClear = bodyState.rightFootGroundClearance;
            if (Mathf.Abs(leftClear - rightClear) > 0.01f)
                return leftClear < rightClear ? -1f : 1f;
        }
        float lead = (bodyState.rightFootPosition.x - bodyState.leftFootPosition.x) * WalkDir();
        if (Mathf.Abs(lead) < FootLeadDeadzone) return fallback;
        return lead > 0f ? 1f : -1f;
    }

    // Where "forward" is. No lateral axis in the sagittal model: +1 = +X.
    private float WalkDir()
    {
        return intent != null && Mathf.Abs(intent.moveX) > 0.01f
            ? Mathf.Sign(intent.moveX)
            : 1f;
    }

    // How far CoM has gone ahead of the stance foot along the walk direction.
    private float StanceComLead()
    {
        if (bodyState == null || comCalculator == null) return 0f;
        Vector2 com = comCalculator.GetCenterOfMass();
        Vector2 stanceFoot = currentStance > 0.5f
            ? bodyState.rightFootPosition
            : bodyState.leftFootPosition;
        return (com.x - stanceFoot.x) * WalkDir();
    }

    private float SwingComLead()
    {
        if (bodyState == null || comCalculator == null) return 0f;
        Vector2 com = comCalculator.GetCenterOfMass();
        Vector2 swingFoot = currentStance > 0.5f
            ? bodyState.leftFootPosition
            : bodyState.rightFootPosition;
        return (com.x - swingFoot.x) * WalkDir();
    }

    // Swing sole sits at least FootLeadDeadzone ahead of the stance sole
    // along walk. Stacked feet (the 0.2 m shuffle) return false.
    private bool SwingFootIsAhead()
    {
        if (bodyState == null) return false;
        float stanceX = currentStance > 0.5f
            ? bodyState.rightFootPosition.x
            : bodyState.leftFootPosition.x;
        float swingX = currentStance > 0.5f
            ? bodyState.leftFootPosition.x
            : bodyState.rightFootPosition.x;
        return (swingX - stanceX) * WalkDir() >= FootLeadDeadzone;
    }

    private bool BothFeetGrounded()
    {
        return bodyState != null && FootDown(true) && FootDown(false);
    }

    // Last rung of the recovery ladder: at least one foot on the dirt, so
    // the quiet two-foot controller has something to stand on. Airborne
    // stay on the landing brake until a foot arrives.
    private bool AnyFootGrounded()
    {
        if (walkRecoverAnyPose <= 0.5f)
            return BothFeetGrounded();
        return bodyState != null && (FootDown(true) || FootDown(false));
    }

    private float AbsComOffset()
    {
        return comCalculator != null ? Mathf.Abs(comCalculator.GetCoMOffsetX()) : float.PositiveInfinity;
    }

    // WalkLift clock swap: the new sole must be a catch plate (ahead
    // of CoM and of the old stance). Mid-pair offset ≤ 0.05 / margin
    // 0.02 passed the 3 cm viewpoint lunge and dumped (burn_walk
    // swap 2). Do not use TiltSettled (12°) — plant's operating tilt
    // is already past that.
    private bool WalkLiftSwapSettled()
    {
        if (!BothFeetGrounded()) return false;
        if (!swingWasAirborne) return false;
        if (!SwingFootIsAhead()) return false;
        if (bodyState == null || comCalculator == null) return false;
        Vector2 com = comCalculator.GetCenterOfMass();
        Vector2 swing = currentStance > 0.5f
            ? bodyState.leftFootPosition
            : bodyState.rightFootPosition;
        if ((swing.x - com.x) * WalkDir() < FootLeadDeadzone)
            return false;
        return AbsBodyTilt() <= 20f;
    }

    // Diagnostic: CoM vs the landing foot. walk2_walk Recover floor is
    // 0.60 m — a step, not recoverComOffsetMax 0.16. Kept so the quantity
    // stays named; Plant no longer gates on it.
    private float AbsCatchFootComOffset()
    {
        if (bodyState == null || comCalculator == null)
            return float.PositiveInfinity;
        Vector2 com = comCalculator.GetCenterOfMass();
        float catchX = currentStance > 0.5f
            ? bodyState.leftFootPosition.x
            : bodyState.rightFootPosition.x;
        return Mathf.Abs(com.x - catchX);
    }

    private bool IsTransferReady()
    {
        if (bodyState == null || comCalculator == null)
            return false;
        if (!BothFeetGrounded())
            return false;
        if (walkTiltRecoverDeg > 0.001f && AbsBodyTilt() > walkTiltRecoverDeg)
            return false;
        // Mid-offset only: the "CoM over the next foot" threshold on a cold
        // first→left did not fire (walk_swap_soft2 swap=0). Soft entry
        // after EnterStance — dualSupportSwingScale in BalanceController.
        return AbsComOffset() <= transferComOffsetMax;
    }

    private bool IsTransferFallbackReady()
    {
        if (bodyState == null || comCalculator == null)
            return false;
        if (!BothFeetGrounded())
            return false;
        if (walkTiltRecoverDeg > 0.001f && AbsBodyTilt() > walkTiltRecoverDeg)
            return false;
        return AbsComOffset() <= transferFallbackComMax;
    }

    private float AbsBodyTilt()
    {
        return vestibular != null ? Mathf.Abs(vestibular.GetBodyTilt()) : 0f;
    }

    // Recover / Catch / post-catch Plant: absolute 12° / 40 °/s band.
    // Plant→Fall uses this; abort is the Fall emergency, not this gate.
    private bool TiltSettled()
    {
        // Absolute band, not a multiple of the abort angle. The stand runs
        // fallAbortDeg = 30, so abort × 1.5 was 45° — a gate that let Plant
        // start the next fall from a 15° tip with 25 deg/s still on the
        // trunk (walk2_blend2, 6.605 s), and that fall never came back.
        return AbsBodyTilt() < TiltSettledMaxDeg
            && AbsBodyOmega() < RecoverOmegaSettledDeg;
    }

    private float AbsBodyOmega()
    {
        return vestibular != null ? Mathf.Abs(vestibular.GetBodyAngularVelocity()) : 0f;
    }
}
