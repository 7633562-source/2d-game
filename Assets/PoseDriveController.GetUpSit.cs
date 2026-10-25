using System.Globalization;
using UnityEngine;

// PoseDriveController: the get-up's leg drives and the sit-to-stand stages.
public partial class PoseDriveController
{
    // One writer per leg joint (DriveOneLeg). draw = tuck depth: 0 = the landed pose, 1 = the full
    // knees-under tuck. Angles are deltas from the settle pose, so the shape holds on every build.
    private void DriveGetUpLegs(float draw)
    {
        float d = Mathf.Clamp(draw, 0f, 1f);
        DriveOneLeg(true, getUpHipL - GetUpHipDrawDeg * d,
            getUpKneeL + GetUpKneeDrawDeg * d, true, getUpAnkleL + GetUpAnkleDrawDeg * d);
        DriveOneLeg(false, getUpHipR - GetUpHipDrawDeg * d,
            getUpKneeR + GetUpKneeDrawDeg * d, true, getUpAnkleR + GetUpAnkleDrawDeg * d);
    }

    // Sit-back Tuck wait: the same fold, B deg more hip flexion (half that at the knee), so the hips
    // travel back over the planted soles while the load gate waits. B = 0 equals DriveGetUpLegs.
    private void DriveGetUpLegsSquat(float draw)
    {
        float d = Mathf.Clamp(draw, 0f, 1f);
        float b = getUpBaseSquatDeg;
        DriveOneLeg(true, getUpHipL - GetUpHipDrawDeg * d - b,
            getUpKneeL + GetUpKneeDrawDeg * d + 0.5f * b, true, getUpAnkleL + GetUpAnkleDrawDeg * d);
        DriveOneLeg(false, getUpHipR - GetUpHipDrawDeg * d - b,
            getUpKneeR + GetUpKneeDrawDeg * d + 0.5f * b, true, getUpAnkleR + GetUpAnkleDrawDeg * d);
    }

    // The human's weight, N (every rig body x gravity, as RigWeightN), cached once; the load floor.
    private float PoseRigWeightN()
    {
        if (rigWeightCacheN > 0.01f) return rigWeightCacheN;
        Rigidbody2D[] bodies = GetComponentsInChildren<Rigidbody2D>();
        float mass = 0f;
        for (int i = 0; i < bodies.Length; i++)
            if (bodies[i] != null) mass += bodies[i].mass;
        rigWeightCacheN = mass * Physics2D.gravity.magnitude;
        return rigWeightCacheN;
    }

    // Stand convergence: blend the held pose (the fold at draw 1, or the latched pose at draw 0) to
    // the stand-pose knobs. Signs: hip minus = flexion, knee plus = flexion, ankle plus = toe down.
    private void DriveGetUpLegsStand(float k, float pelvisYNow)
    {
        float kk = Mathf.Clamp01(k);
        // The knee/ankle release may lag the hip. HipFirst D: hold the fold until hip progress passes D.
        // KneeClimb C: release by the pelvis's climb since the Rise began (C m = full; no climb, no
        // release - the closed chain). Both 0: the knee follows the hip's key.
        float kKnee = kk;
        if (getUpStandHipFirst > 0f)
        {
            kKnee = Mathf.Clamp01((kk - getUpStandHipFirst) / (1f - getUpStandHipFirst));
        }
        else if (getUpStandKneeClimb > 0f)
        {
            float climbed = pelvisYNow - getUpRiseStartPelvisY;
            kKnee = Mathf.Clamp01(climbed / getUpStandKneeClimb);
        }
        float d0 = getUpStandFrom > 0 ? 0f : 1f;
        float hL = getUpHipL - GetUpHipDrawDeg * d0;
        float kL = getUpKneeL + GetUpKneeDrawDeg * d0;
        float aL = getUpAnkleL + GetUpAnkleDrawDeg * d0;
        float hR = getUpHipR - GetUpHipDrawDeg * d0;
        float kR = getUpKneeR + GetUpKneeDrawDeg * d0;
        float aR = getUpAnkleR + GetUpAnkleDrawDeg * d0;
        // The ankle's blend key (heel release): 0 = kKnee, 1 = kk, 2 = 2*kk.
        float kAnkle = kKnee;
        if (getUpStandAnkleFast == 1) kAnkle = kk;
        else if (getUpStandAnkleFast >= 2) kAnkle = Mathf.Clamp01(2f * kk);
        float hLT = Mathf.Lerp(hL, getUpStandHipDeg, kk);
        float kLT = Mathf.Lerp(kL, getUpStandKneeDeg, kKnee);
        float aLT = Mathf.Lerp(aL, getUpStandAnkleDeg, kAnkle);
        float hRT = Mathf.Lerp(hR, getUpStandHipDeg, kk);
        float kRT = Mathf.Lerp(kR, getUpStandKneeDeg, kKnee);
        float aRT = Mathf.Lerp(aR, getUpStandAnkleDeg, kAnkle);
        gusKeyk = kk; gusKeyKnee = kKnee; gusKeyAnkle = kAnkle;
        gusTgtHipL = hLT; gusTgtKneeL = kLT; gusTgtAnkleL = aLT;
        DriveOneLeg(true, hLT, kLT, true, aLT);
        DriveOneLeg(false, hRT, kRT, true, aRT);
    }

    // Sit heel-pull: the knees fold first so the heels travel under the body. Targets are deltas from
    // the latched sit pose, ramped by p: 0 = the latch, 1 = the full fold.
    private void DriveGetUpLegsSitPull(float p)
    {
        float pp = Mathf.Clamp01(p);
        DriveOneLeg(true, getUpHipL - getUpSitPullHip * pp,
            getUpKneeL + getUpSitPullKnee * pp, true, getUpAnkleL - getUpSitPullAnkle * pp);
        DriveOneLeg(false, getUpHipR - getUpSitPullHip * pp,
            getUpKneeR + getUpSitPullKnee * pp, true, getUpAnkleR - getUpSitPullAnkle * pp);
    }

    // Draw-wait plant drive: the frozen pull pose plus the plant deltas at the plant key, per leg.
    private void DriveGetUpLegsSitPullPlant(float p)
    {
        float pp = Mathf.Clamp01(p);
        float kk = seatDrawPlantK * getUpSitDrawPlant;
        DriveOneLeg(true, getUpHipL - getUpSitPullHip * pp + getUpSitDrawPlantHip * kk,
            getUpKneeL + getUpSitPullKnee * pp + getUpSitDrawPlantKnee * kk, true,
            getUpAnkleL - getUpSitPullAnkle * pp + getUpSitDrawPlantAnkle * kk);
        DriveOneLeg(false, getUpHipR - getUpSitPullHip * pp + getUpSitDrawPlantHip * kk,
            getUpKneeR + getUpSitPullKnee * pp + getUpSitDrawPlantKnee * kk, true,
            getUpAnkleR - getUpSitPullAnkle * pp + getUpSitDrawPlantAnkle * kk);
    }

    // A joint's unfold key: start = 0 returns kk (full-clock release); start > 0 delays it to the tail.
    private float UnfoldKey(float kk, float start)
    {
        if (start <= 0f) return kk;
        return Mathf.Clamp01((kk - start) / (1f - start));
    }

    // Extension after the pull: blend from the pulled pose to the stand targets (k = 1 lands where
    // DriveGetUpLegsStand does). pullKey = how far the drag got (1 = full fold); per-joint unfold keys.
    private void DriveGetUpLegsStandPulled(float k, float pullKey = 1f)
    {
        float kk = Mathf.Clamp01(k);
        float pk = Mathf.Clamp01(pullKey);
        float kHip = UnfoldKey(kk, getUpStandHipStart);
        float kKnee = kk;
        if (getUpStandKneeStart >= 0f)
            kKnee = UnfoldKey(kk, getUpStandKneeStart);
        else if (getUpStandHipFirst > 0f)
            kKnee = Mathf.Clamp01((kk - getUpStandHipFirst) / (1f - getUpStandHipFirst));
        float kAnkle = getUpStandAnkleStart >= 0f ? UnfoldKey(kk, getUpStandAnkleStart) : kKnee;
        float hLT = Mathf.Lerp(getUpHipL - getUpSitPullHip * pk, getUpStandHipDeg, kHip);
        float kLT = Mathf.Lerp(getUpKneeL + getUpSitPullKnee * pk, getUpStandKneeDeg, kKnee);
        float aLT = Mathf.Lerp(getUpAnkleL - getUpSitPullAnkle * pk, getUpStandAnkleDeg, kAnkle);
        float hRT = Mathf.Lerp(getUpHipR - getUpSitPullHip * pk, getUpStandHipDeg, kHip);
        float kRT = Mathf.Lerp(getUpKneeR + getUpSitPullKnee * pk, getUpStandKneeDeg, kKnee);
        float aRT = Mathf.Lerp(getUpAnkleR - getUpSitPullAnkle * pk, getUpStandAnkleDeg, kAnkle);
        gusKeyk = kk; gusKeyKnee = kKnee; gusKeyAnkle = kAnkle;
        gusTgtHipL = hLT; gusTgtKneeL = kLT; gusTgtAnkleL = aLT;
        DriveOneLeg(true, hLT, kLT, true, aLT);
        DriveOneLeg(false, hRT, kRT, true, aRT);
    }

    // Re-seat targets: the drag's exit pose per leg plus the leg's own share of the re-seat budget.
    private void ReseatTargets(float sL, float sR,
        out float hipL, out float kneeL, out float ankleL,
        out float hipR, out float kneeR, out float ankleR)
    {
        float pk = Mathf.Clamp01(seatPullExitKey);
        hipL = getUpHipL - getUpSitPullHip * pk + getUpSitReseatHip * sL;
        kneeL = getUpKneeL + getUpSitPullKnee * pk + getUpSitReseatKnee * sL;
        ankleL = getUpAnkleL - getUpSitPullAnkle * pk + getUpSitReseatAnkle * sL;
        hipR = getUpHipR - getUpSitPullHip * pk + getUpSitReseatHip * sR;
        kneeR = getUpKneeR + getUpSitPullKnee * pk + getUpSitReseatKnee * sR;
        ankleR = getUpAnkleR - getUpSitPullAnkle * pk + getUpSitReseatAnkle * sR;
    }

    private void DriveGetUpLegsReseat()
    {
        float hL, kL, aL, hR, kR, aR;
        ReseatTargets(seatResL, seatResR, out hL, out kL, out aL, out hR, out kR, out aR);
        DriveOneLeg(true, hL, kL, true, aL);
        DriveOneLeg(false, hR, kR, true, aR);
    }

    // Extension after a re-seat: blend from the captured per-leg hand-off pose to the stand targets,
    // with the per-joint unfold keys.
    private void DriveGetUpLegsStandReseat(float k)
    {
        float kk = Mathf.Clamp01(k);
        float kHip = UnfoldKey(kk, getUpStandHipStart);
        float kKnee = kk;
        if (getUpStandKneeStart >= 0f)
            kKnee = UnfoldKey(kk, getUpStandKneeStart);
        else if (getUpStandHipFirst > 0f)
            kKnee = Mathf.Clamp01((kk - getUpStandHipFirst) / (1f - getUpStandHipFirst));
        float kAnkle = getUpStandAnkleStart >= 0f ? UnfoldKey(kk, getUpStandAnkleStart) : kKnee;
        float hLT = Mathf.Lerp(seatResHipL, getUpStandHipDeg, kHip);
        float kLT = Mathf.Lerp(seatResKneeL, getUpStandKneeDeg, kKnee);
        float aLT = Mathf.Lerp(seatResAnkleL, getUpStandAnkleDeg, kAnkle);
        float hRT = Mathf.Lerp(seatResHipR, getUpStandHipDeg, kHip);
        float kRT = Mathf.Lerp(seatResKneeR, getUpStandKneeDeg, kKnee);
        float aRT = Mathf.Lerp(seatResAnkleR, getUpStandAnkleDeg, kAnkle);
        gusKeyk = kk; gusKeyKnee = kKnee; gusKeyAnkle = kAnkle;
        gusTgtHipL = hLT; gusTgtKneeL = kLT; gusTgtAnkleL = aLT;
        DriveOneLeg(true, hLT, kLT, true, aLT);
        DriveOneLeg(false, hRT, kRT, true, aRT);
    }

    // Corridor re-seat: the legs take turns, the active leg's share ramping over ReseatSec slices (one
    // heel slides in at a time). Exits: the corridor (gaps and sustained sole load), the shares spent,
    // or the stage budget - always bounded.
    private void RunSitReseat(float now, float dt)
    {
        if (!seatResLogged)
        {
            seatResLogged = true;
            Debug.Log("GETUP-RESEAT enter t=" + N(now, 3) + " gapL=" + N(seatGapL, 3)
                + " gapR=" + N(seatGapR, 3) + " pk=" + N(seatPullExitKey, 3)
                + " knee=" + N(getUpSitReseatKnee, 0) + " hip=" + N(getUpSitReseatHip, 0)
                + " gapTgt=" + N(getUpSitReseatGap, 3) + " fz=" + N(poseGroundFzLeftN + poseGroundFzRightN, 0));
        }
        seatReseatT += dt;
        float w = PoseRigWeightN();
        float fzSum = poseGroundFzLeftN + poseGroundFzRightN;
        if (w > 1f && fzSum >= getUpSitReseatLoad * w) seatResLoadT += dt;
        else seatResLoadT = 0f;
        bool roomL = seatResL < 0.999f && seatGapL > getUpSitReseatGap;
        bool roomR = seatResR < 0.999f && seatGapR > getUpSitReseatGap;
        bool activeRoom = seatResTurn == 0 ? roomL : roomR;
        bool otherRoom = seatResTurn == 0 ? roomR : roomL;
        if (!activeRoom && otherRoom)
        {
            seatResTurn = 1 - seatResTurn;
            seatResTurnT = 0f;
        }
        else if (activeRoom)
        {
            seatResTurnT += dt;
            float rate = dt / Mathf.Max(0.05f, getUpSitReseatSec);
            if (seatResTurn == 0) seatResL = Mathf.Clamp01(seatResL + rate);
            else seatResR = Mathf.Clamp01(seatResR + rate);
            if (seatResTurnT >= Mathf.Max(0.05f, getUpSitReseatSec) && otherRoom)
            {
                seatResTurn = 1 - seatResTurn;
                seatResTurnT = 0f;
            }
        }
        bool gapsOk = seatGapL <= getUpSitReseatGap && seatGapR <= getUpSitReseatGap;
        bool corridor = gapsOk && seatResLoadT >= getUpSitReseatLoadSec;
        bool spent = seatResL >= 0.999f && seatResR >= 0.999f;
        if (corridor || spent || seatReseatT >= getUpSitReseatMax)
        {
            seatReseatDone = true;
            ReseatTargets(seatResL, seatResR,
                out seatResHipL, out seatResKneeL, out seatResAnkleL,
                out seatResHipR, out seatResKneeR, out seatResAnkleR);
            Debug.Log("GETUP-RESEAT done t=" + N(now, 3) + " why="
                + (corridor ? "corridor" : (spent ? "spent" : "max"))
                + " gapL=" + N(seatGapL, 3) + " gapR=" + N(seatGapR, 3)
                + " loadT=" + N(seatResLoadT, 2) + " sL=" + N(seatResL, 3)
                + " sR=" + N(seatResR, 3) + " fz=" + N(fzSum, 0));
        }
    }

    private void ResetSitReseat()
    {
        seatResL = 0f; seatResR = 0f; seatReseatT = 0f; seatResTurnT = 0f;
        seatResLoadT = 0f; seatResTurn = 0; seatReseatDone = false; seatResLogged = false;
    }

    // Per-leg step targets: the latch plus the leg's own share of the step budget.
    private void StepTargets(out float hipL, out float kneeL, out float ankleL,
        out float hipR, out float kneeR, out float ankleR)
    {
        hipL = getUpHipL - getUpSitStepHip * stepFracL;
        kneeL = getUpKneeL + getUpSitStepKnee * stepFracL;
        ankleL = getUpAnkleL - getUpSitStepAnkle * stepFracL;
        hipR = getUpHipR - getUpSitStepHip * stepFracR;
        kneeR = getUpKneeR + getUpSitStepKnee * stepFracR;
        ankleR = getUpAnkleR - getUpSitStepAnkle * stepFracR;
    }

    private void DriveGetUpLegsStep()
    {
        float hL, kL, aL, hR, kR, aR;
        StepTargets(out hL, out kL, out aL, out hR, out kR, out aR);
        DriveOneLeg(true, hL, kL, true, aL);
        DriveOneLeg(false, hR, kR, true, aR);
    }

    private void ResetSitStep()
    {
        stepT = 0f; stepFracL = 0f; stepFracR = 0f; stepFromFrac = 0f; stepLeg = 0;
        stepPhase = 0; stepPhaseT = 0f; stepRound = 0; stepDone = false;
        stepLogged = false; stepLoadT = 0f;
    }

    // Post-step weight transfer: the step leaves the soles unloaded, so drive the legs partway back out
    // of the fold (hip extension, knee release, plantar press) until sum Fz >= Load*W for LoadSec, or
    // the budget ends. The extension then starts from this pose (the seatRes* hand-off).
    private void RunSitShift(float now, float dt)
    {
        if (!sitShiftEntered)
        {
            sitShiftEntered = true;
            Debug.Log("GETUP-SITSHIFT enter t=" + N(now, 3)
                + " gapL=" + N(seatGapL, 3) + " gapR=" + N(seatGapR, 3)
                + " hip=" + N(getUpSitShiftHip, 0) + " knee=" + N(getUpSitShiftKnee, 0)
                + " ankle=" + N(getUpSitShiftAnkle, 0) + " lean=" + N(getUpSitShiftLean, 0)
                + " load=" + N(getUpSitShiftLoad, 3) + " sec=" + N(getUpSitShiftSec, 2)
                + " fz=" + N(poseGroundFzLeftN + poseGroundFzRightN, 0));
        }
        sitShiftT += dt;
        float w = PoseRigWeightN();
        float fzSum = poseGroundFzLeftN + poseGroundFzRightN;
        if (fzSum > sitShiftFzMax) sitShiftFzMax = fzSum;
        float gMin = Mathf.Min(seatGapL, seatGapR);
        if (gMin < sitShiftGapMin) sitShiftGapMin = gMin;
        if (w > 1f && fzSum >= getUpSitShiftLoad * w) sitShiftLoadT += dt;
        else sitShiftLoadT = 0f;
        bool loaded = sitShiftLoadT >= getUpSitShiftLoadSec;
        bool spent = sitShiftT >= getUpSitShiftSec;
        if (loaded || spent)
        {
            float f = sitShiftFrac;
            seatResHipL = getUpHipL - getUpSitStepHip * seatResL + getUpSitShiftHip * f;
            seatResKneeL = getUpKneeL + getUpSitStepKnee * seatResL - getUpSitShiftKnee * f;
            seatResAnkleL = getUpAnkleL - getUpSitStepAnkle * seatResL + getUpSitShiftAnkle * f;
            seatResHipR = getUpHipR - getUpSitStepHip * seatResR + getUpSitShiftHip * f;
            seatResKneeR = getUpKneeR + getUpSitStepKnee * seatResR - getUpSitShiftKnee * f;
            seatResAnkleR = getUpAnkleR - getUpSitStepAnkle * seatResR + getUpSitShiftAnkle * f;
            sitShiftDone = true;
            Debug.Log("GETUP-SITSHIFT done t=" + N(now, 3)
                + " why=" + (loaded ? "load" : "spent")
                + " fz=" + N(fzSum, 0) + " fzMax=" + N(sitShiftFzMax, 0)
                + " gapL=" + N(seatGapL, 3) + " gapR=" + N(seatGapR, 3)
                + " frac=" + N(f, 3) + " loadT=" + N(sitShiftLoadT, 2));
            return;
        }
        sitShiftFrac = Mathf.Clamp01(sitShiftT / Mathf.Max(0.05f, getUpSitShiftRamp));
    }

    // The shift's drive: the step capture plus the shift deltas at the stage's fraction, per leg.
    private void DriveGetUpLegsShift()
    {
        float f = sitShiftFrac;
        float hL = getUpHipL - getUpSitStepHip * seatResL + getUpSitShiftHip * f;
        float kL = getUpKneeL + getUpSitStepKnee * seatResL - getUpSitShiftKnee * f;
        float aL = getUpAnkleL - getUpSitStepAnkle * seatResL + getUpSitShiftAnkle * f;
        float hR = getUpHipR - getUpSitStepHip * seatResR + getUpSitShiftHip * f;
        float kR = getUpKneeR + getUpSitStepKnee * seatResR - getUpSitShiftKnee * f;
        float aR = getUpAnkleR - getUpSitStepAnkle * seatResR + getUpSitShiftAnkle * f;
        DriveOneLeg(true, hL, kL, true, aL);
        DriveOneLeg(false, hR, kR, true, aR);
    }

    private void ResetSitShift()
    {
        sitShiftT = 0f; sitShiftDone = false; sitShiftEntered = false;
        sitShiftLoadT = 0f; sitShiftFzMax = 0f; sitShiftGapMin = 999f;
        sitShiftFrac = 0f;
    }

    // Plant stage: ramp the step's fold back out (knee unfold, hip extension, plantar press) until the
    // soles hold sum fz >= Fz*W for FzSec, or Max; the extension starts from the reached pose.
    private void RunSitPlant(float now, float dt)
    {
        if (!sitPlantEntered)
        {
            sitPlantEntered = true;
            Debug.Log("GETUP-SITPLANT enter t=" + N(now, 3)
                + " knee=" + N(getUpSitPlantKnee, 0) + " hip=" + N(getUpSitPlantHip, 0)
                + " ankle=" + N(getUpSitPlantAnkle, 0) + " sec=" + N(getUpSitPlantSec, 2)
                + " fz=" + N(poseGroundFzLeftN + poseGroundFzRightN, 0));
        }
        sitPlantT += dt;
        float w = PoseRigWeightN();
        float fzSum = poseGroundFzLeftN + poseGroundFzRightN;
        if (fzSum > sitPlantFzMax) sitPlantFzMax = fzSum;
        if (w > 1f && fzSum >= getUpSitPlantFz * w) sitPlantContactT += dt;
        else sitPlantContactT = 0f;
        bool contact = sitPlantContactT >= getUpSitPlantFzSec;
        bool spent = sitPlantT >= getUpSitPlantMax;
        if (contact || spent)
        {
            float f = sitPlantFrac;
            seatResHipL = getUpHipL - getUpSitStepHip * seatResL + getUpSitPlantHip * f;
            seatResKneeL = getUpKneeL + getUpSitStepKnee * seatResL - getUpSitPlantKnee * f;
            seatResAnkleL = getUpAnkleL - getUpSitStepAnkle * seatResL + getUpSitPlantAnkle * f;
            seatResHipR = getUpHipR - getUpSitStepHip * seatResR + getUpSitPlantHip * f;
            seatResKneeR = getUpKneeR + getUpSitStepKnee * seatResR - getUpSitPlantKnee * f;
            seatResAnkleR = getUpAnkleR - getUpSitStepAnkle * seatResR + getUpSitPlantAnkle * f;
            sitPlantDone = true;
            Debug.Log("GETUP-SITPLANT done t=" + N(now, 3)
                + " why=" + (contact ? "contact" : "spent")
                + " fz=" + N(fzSum, 0) + " fzMax=" + N(sitPlantFzMax, 0)
                + " frac=" + N(f, 3) + " plant=" + N(sitPlantT, 2));
            return;
        }
        sitPlantFrac = Mathf.Clamp01(sitPlantT / Mathf.Max(0.05f, getUpSitPlantSec));
    }

    // The plant's drive: the step capture plus the reach deltas at the stage's fraction, per leg.
    private void DriveGetUpLegsPlant()
    {
        float f = sitPlantFrac;
        float hL = getUpHipL - getUpSitStepHip * seatResL + getUpSitPlantHip * f;
        float kL = getUpKneeL + getUpSitStepKnee * seatResL - getUpSitPlantKnee * f;
        float aL = getUpAnkleL - getUpSitStepAnkle * seatResL + getUpSitPlantAnkle * f;
        float hR = getUpHipR - getUpSitStepHip * seatResR + getUpSitPlantHip * f;
        float kR = getUpKneeR + getUpSitStepKnee * seatResR - getUpSitPlantKnee * f;
        float aR = getUpAnkleR - getUpSitStepAnkle * seatResR + getUpSitPlantAnkle * f;
        DriveOneLeg(true, hL, kL, true, aL);
        DriveOneLeg(false, hR, kR, true, aR);
    }

    private void ResetSitPlant()
    {
        sitPlantT = 0f; sitPlantDone = false; sitPlantEntered = false;
        sitPlantFrac = 0f; sitPlantContactT = 0f; sitPlantFzMax = 0f;
    }

    // Discrete step: one leg's share ramps to its round target over StepSec, holds StepHold (optionally
    // easing back by StepPlant), then the other leg; rounds repeat. The shares publish through
    // seatResL/R and seatReseatT for the trace.
    private void RunSitStep(float now, float dt)
    {
        if (!stepLogged)
        {
            stepLogged = true;
            Debug.Log("GETUP-SITSTEP enter t=" + N(now, 3) + " gapL=" + N(seatGapL, 3)
                + " gapR=" + N(seatGapR, 3) + " hip=" + N(getUpSitStepHip, 0)
                + " knee=" + N(getUpSitStepKnee, 0) + " rounds=" + N(getUpSitStepRounds, 0)
                + " under=" + N(getUpSitUnder, 2)
                + " fz=" + N(poseGroundFzLeftN + poseGroundFzRightN, 0));
        }
        stepT += dt;
        float w = PoseRigWeightN();
        float fzSum = poseGroundFzLeftN + poseGroundFzRightN;
        if (w > 1f && fzSum >= getUpSitStepLoad * w) stepLoadT += dt;
        else stepLoadT = 0f;
        // Under-gate (SitUnder G > 0): the corridor is two-sided - both feet within G of the CoM's x.
        // The one-sided gap (comX - footX) passes trivially when a foot is ahead of the CoM.
        bool gapsOk = getUpSitUnder > 0.001f
            ? Mathf.Abs(seatGapL) <= getUpSitUnder && Mathf.Abs(seatGapR) <= getUpSitUnder
            : seatGapL <= getUpSitStepGap && seatGapR <= getUpSitStepGap;
        bool corridor = gapsOk && stepLoadT >= getUpSitStepLoadSec;
        bool spent = stepRound >= Mathf.RoundToInt(getUpSitStepRounds);
        if (corridor || spent || stepT >= getUpSitStepMax)
        {
            stepDone = true;
            StepTargets(out seatResHipL, out seatResKneeL, out seatResAnkleL,
                out seatResHipR, out seatResKneeR, out seatResAnkleR);
            Debug.Log("GETUP-SITSTEP done t=" + N(now, 3) + " why="
                + (corridor ? "corridor" : (spent ? "spent" : "max"))
                + " gapL=" + N(seatGapL, 3) + " gapR=" + N(seatGapR, 3)
                + " loadT=" + N(stepLoadT, 2) + " fL=" + N(stepFracL, 3)
                + " fR=" + N(stepFracR, 3) + " fz=" + N(fzSum, 0));
            return;
        }
        float travelSec = Mathf.Max(0.05f, getUpSitStepSec);
        float holdSec = Mathf.Max(0f, getUpSitStepHold);
        // March (SitUnder > 0): each completed round re-applies the full delta instead of splitting one
        // delta across the rounds, so the feet can close a wide base. Bounded by StepRounds and StepMax.
        float tgt = getUpSitUnder > 0.001f
            ? stepRound + 1f
            : (stepRound + 1f) / getUpSitStepRounds;
        stepPhaseT += dt;
        float ramp = stepPhase == 0 ? Mathf.Clamp01(stepPhaseT / travelSec) : 1f;
        float frac = Mathf.Lerp(stepFromFrac, tgt, ramp);
        if (stepPhase == 1 && getUpSitStepPlant > 0.001f)
        {
            float back = Mathf.Clamp01(stepPhaseT / Mathf.Max(0.05f, holdSec));
            frac = Mathf.Lerp(tgt, tgt * (1f - getUpSitStepPlant), back);
        }
        if (stepLeg == 0) stepFracL = frac; else stepFracR = frac;
        if (stepPhase == 0 && stepPhaseT >= travelSec)
        {
            stepPhase = 1;
            stepPhaseT = 0f;
        }
        else if (stepPhase == 1 && stepPhaseT >= holdSec)
        {
            stepPhase = 0;
            stepPhaseT = 0f;
            if (stepLeg == 0) stepLeg = 1;
            else { stepLeg = 0; stepRound++; }
            stepFromFrac = stepLeg == 0 ? stepFracL : stepFracR;
        }
        seatResL = stepFracL; seatResR = stepFracR; seatReseatT = stepT;
    }

    // The arc's key: its rate-gated clock over the arc duration, 0..1.
    private float SitArcKey()
    {
        return Mathf.Clamp01(getUpSitArcClock / Mathf.Max(0.05f, getUpSitArcSec));
    }

    // Seat drive. Phase 0: hold the latch while the press takes the load. Phase 1: the gated pull.
    // Phase 2: the extension from the pulled (or re-seated / stepped) pose.
    private void DriveGetUpLegsSeat()
    {
        float pullSecP = Mathf.Max(0.05f, getUpSitPullSec);
        if (seatPhase == 0)
        {
            getUpSitPullKey = 0f;
            DriveGetUpLegsSitPull(0f);
        }
        else if (getUpSitStepOn && seatPhase == 1)
        {
            // Step route (no pull fold): after the hand-off the plant reach runs first, then the weight
            // transfer, each until its exit; before the hand-off, the step machine.
            if (stepDone && getUpSitPlantOn && !sitPlantDone) DriveGetUpLegsPlant();
            else if (stepDone && getUpSitShiftOn && !sitShiftDone) DriveGetUpLegsShift();
            else DriveGetUpLegsStep();
        }
        else if (seatPhase == 1)
        {
            float p = Mathf.Clamp01(seatPullT / pullSecP);
            getUpSitPullKey = p;
            // With the plant armed the legs blend from the frozen pull pose toward the plant deltas at the
            // plant key (which ramps down on release).
            if (getUpSitDrawPlant > 0.001f) DriveGetUpLegsSitPullPlant(p);
            else DriveGetUpLegsSitPull(p);
        }
        else if (getUpSitReseatOn && !seatReseatDone)
        {
            // Re-seat drive: per-leg shares over the drag's exit pose; no extension clock until hand-off.
            DriveGetUpLegsReseat();
        }
        else
        {
            float tailSec = Mathf.Max(0.1f, GetUpRiseSeconds * getUpPatience
                - seatPressUsed - pullSecP);
            float k = Mathf.Clamp01(seatExtT / tailSec);
            // The arc's rate-gated clock replaces the time share.
            if (getUpSitArcSec > 0.001f) k = SitArcKey();
            getUpSitPullKey = 1f + k;
            // Extend from where the drag actually stopped (seatPullExitKey; 1 = full fold), or from the
            // captured re-seat / step hand-off pose.
            if ((getUpSitReseatOn && seatReseatDone) || stepDone)
                DriveGetUpLegsStandReseat(k);
            else
                DriveGetUpLegsStandPulled(k, seatPullExitKey);
        }
    }

    // Sit re-seat machine: runs each frame while a seat attempt is live; publishes seatPhase/PressNow.
    // Pull gate: soles >= Keep*W or (modes 1/3) palms together >= PressMin; a dropout <= Grace s is
    // ignored. The pull clock freezes while closed; FrozenMax forces the extension (gate-dead).
    private void RunSitSeat(float now, float dt)
    {
        if (!(getUpSitSeatMode > 0f) || !getUpSitActive || getUpStage != GetUpStage.Rise)
        {
            seatPhase = -1;
            return;
        }
        if (seatPhase < 0)
        {
            seatPhase = getUpSitSeatMode == 2f ? 1 : 0;
            seatPressUsed = 0f; seatPullT = 0f; seatExtT = 0f; seatFrozenT = 0f;
            seatGatePasses = 0; seatGatePrev = false; seatGateNow = false;
            seatGateLastOk = -999f;
            seatPullExitKey = 1f;   // ROUND 46: the drag's exit share
            ResetSitReseat();       // ROUND 49: the re-seat state, fresh
            ResetSitStep();         // ROUND 51: the step state, fresh
            seatExtArrestInit = false;   // ROUND 51c: the arrest datum is per attempt
            seatExtArrestT = 0f; seatExtArrestTrips = 0; seatExtArrestBad = false;
            ResetSitShift();        // ROUND 52: the shift state, fresh
            ResetSitPlant();        // ROUND 54: the plant state, fresh
            Debug.Log("GETUP-SITSEAT enter t=" + N(now, 3) + " mode=" + N(getUpSitSeatMode, 0)
                + " press=" + N(getUpSitSeatPress, 2) + " keep=" + N(getUpSitSeatKeep, 3)
                + " pressmin=" + N(getUpSitSeatPressMin, 3));
        }
        float weightN = PoseRigWeightN();
        float fzSum = poseGroundFzLeftN + poseGroundFzRightN;
        float palmSum = gripLoad[0] + gripLoad[1];
        bool soleOk = weightN > 1f && fzSum >= getUpSitSeatKeep * weightN;
        bool palmOk = getUpSitSeatMode != 2f && palmSum >= getUpSitSeatPressMin;
        if (soleOk || palmOk) seatGateLastOk = now;
        bool gate = (soleOk || palmOk) || (now - seatGateLastOk) <= getUpSitSeatGrace;
        if (gate && !seatGatePrev && seatPhase == 1)
        {
            seatGatePasses++;
            if (seatGatePasses == 1)
                Debug.Log("GETUP-SITSEAT gate t=" + N(now, 3) + " frozen=" + N(seatFrozenT, 2)
                    + " fz=" + N(fzSum, 0) + " palms=" + N(palmSum, 3));
        }
        seatGatePrev = gate;
        seatGateNow = gate;

        if (seatPhase == 0)
        {
            seatPressUsed += dt;
            bool bothPalms = gripLoad[0] >= getUpSitSeatPressMin && gripLoad[1] >= getUpSitSeatPressMin;
            if (bothPalms)
            {
                Debug.Log("GETUP-SITSEAT press-ok t=" + N(now, 3) + " used=" + N(seatPressUsed, 2)
                    + " gL=" + N(gripLoad[0], 3) + " gR=" + N(gripLoad[1], 3));
                getUpSitDragTilt = seatBodyTilt;   // ROUND 46: the tip datum
                seatPhase = 1;
            }
            else if (seatPressUsed >= getUpSitSeatPressMax)
            {
                Debug.Log("GETUP-SITSEAT press-cap t=" + N(now, 3) + " used=" + N(seatPressUsed, 2)
                    + " gL=" + N(gripLoad[0], 3) + " gR=" + N(gripLoad[1], 3));
                getUpSitDragTilt = seatBodyTilt;   // ROUND 46: the tip datum
                seatPhase = 1;
            }
        }
        else if (seatPhase == 1 && getUpSitStepOn)
        {
            // Step route: the discrete step replaces the pull and the corridor re-seat; after its hand-off the
            // plant reach and the weight transfer (if armed) run before the extension.
            if (!stepDone)
            {
                RunSitStep(now, dt);
                if (stepDone && !(getUpSitPlantOn && !sitPlantDone)
                    && !(getUpSitShiftOn && !sitShiftDone))
                    seatPhase = 2;
            }
            else if (getUpSitPlantOn && !sitPlantDone)
            {
                // The plant's fz contact gate holds the extension until the soles reach the deck.
                RunSitPlant(now, dt);
            }
            else if (getUpSitShiftOn && !sitShiftDone)
            {
                RunSitShift(now, dt);
                if (sitShiftDone) seatPhase = 2;
            }
            else
            {
                seatPhase = 2;
            }
        }
        else if (seatPhase == 1)
        {
            // Tip freeze: while the body tilt deviates from the drag's start pitch by more than Tip deg, the
            // pull clock freezes as for a closed gate (same budget) - the fold's reaction pitches the body.
            bool armed46 = getUpSitDragGap > 0.001f || getUpSitDragTip > 0.001f;
            bool tipBad = false;
            if (getUpSitDragTilt < 900f)
                seatTipDev = Mathf.Abs(seatBodyTilt - getUpSitDragTilt);
            if (getUpSitDragTip > 0.001f && getUpSitDragTilt < 900f)
                tipBad = seatTipDev > getUpSitDragTip;
            // Heel plant: while the gate is closed (soles unloaded) the plant window replaces the dead-man
            // count (legs down-and-close; the frozen budget starts only after it); the draw advances when the
            // gate reopens. Re-arm: each re-closed gate starts a fresh episode, up to the episode cap.
            bool plantArmed = getUpSitDrawPlant > 0.001f;
            bool reArm = plantArmed && getUpSitDrawPlantRe > 0.001f;
            if (reArm && seatDrawPlantReleased && !gate && seatDrawPlantPrevGate
                && seatDrawPlantEps + 1 < (int)getUpSitDrawPlantEps)
            {
                seatDrawPlantEps++;
                seatDrawPlantEpT = 0f;
                seatDrawPlantLogged = false; seatDrawPlantReleased = false;
                Debug.Log("GETUP-DRAWPLANT reenter t=" + N(now, 3)
                    + " eps=" + seatDrawPlantEps + " fz=" + N(fzSum, 0)
                    + " pull=" + N(seatPullT, 3) + " gapL=" + N(seatGapL, 3));
            }
            seatDrawPlantPrevGate = gate;
            bool plantWait = plantArmed && !gate
                && (reArm ? seatDrawPlantEpT : seatDrawPlantT) < getUpSitDrawPlantMax;
            if (plantWait)
            {
                seatDrawPlantEpT += dt;
                seatDrawPlantT += dt;
                if (!seatDrawPlantLogged)
                {
                    seatDrawPlantLogged = true;
                    Debug.Log("GETUP-DRAWPLANT enter t=" + N(now, 3)
                        + " K=" + N(getUpSitDrawPlant, 2) + " eps=" + seatDrawPlantEps
                        + " fz=" + N(fzSum, 0)
                        + " gapL=" + N(seatGapL, 3) + " gapR=" + N(seatGapR, 3));
                }
            }
            else if (plantArmed && gate && seatDrawPlantT > 0.001f && !seatDrawPlantReleased)
            {
                seatDrawPlantReleased = true;
                Debug.Log("GETUP-DRAWPLANT release t=" + N(now, 3)
                    + " eps=" + seatDrawPlantEps
                    + " fz=" + N(fzSum, 0) + " hold=" + N(seatDrawPlantT, 2));
            }
            float plantKTgt = plantArmed && !gate ? 1f : 0f;
            seatDrawPlantK = Mathf.MoveTowards(seatDrawPlantK, plantKTgt,
                dt / Mathf.Max(0.05f, getUpSitDrawPlantSec));
            if (gate && !tipBad) seatPullT += dt;
            else if (!plantWait) seatFrozenT += dt;
            // Gap exit: both feet within DragGap of the CoM end the drag; the extension starts from that exit
            // pose. DragUnder G > 0 makes it two-sided (|gap| <= G), so a foot ahead of the CoM no longer
            // passes at once and the fold runs until its own budget. 0 = one-sided comX - footX <= DragGap.
            bool gapOut = getUpSitDragGap > 0.001f
                && (getUpSitDragUnder > 0.001f
                    ? Mathf.Abs(seatGapL) <= getUpSitDragUnder
                        && Mathf.Abs(seatGapR) <= getUpSitDragUnder
                    : seatGapL <= getUpSitDragGap && seatGapR <= getUpSitDragGap);
            if (gapOut)
            {
                seatPullExitKey = Mathf.Clamp01(seatPullT / Mathf.Max(0.05f, getUpSitPullSec));
                Debug.Log("GETUP-SITSEAT gap-out t=" + N(now, 3) + " gapL=" + N(seatGapL, 3)
                    + " gapR=" + N(seatGapR, 3) + " pull=" + N(seatPullExitKey, 3)
                    + " tilt=" + N(seatBodyTilt, 1));
                seatPhase = 2;
            }
            else if (seatPullT >= Mathf.Max(0.05f, getUpSitPullSec))
            {
                seatPullExitKey = 1f;
                seatPhase = 2;
            }
            else if (seatFrozenT >= getUpSitSeatFrozenMax)
            {
                Debug.Log("GETUP-SITSEAT gate-dead t=" + N(now, 3) + " frozen=" + N(seatFrozenT, 2)
                    + " pull=" + N(seatPullT, 3) + " fz=" + N(fzSum, 0) + " palms=" + N(palmSum, 3));
                seatPullExitKey = armed46
                    ? Mathf.Clamp01(seatPullT / Mathf.Max(0.05f, getUpSitPullSec)) : 1f;
                seatPhase = 2;
            }
        }
        else if (!getUpSitStepOn && getUpSitReseatOn && !seatReseatDone)
        {
            // Corridor re-seat; the extension clock waits.
            RunSitReseat(now, dt);
        }
        else
        {
            // Ext sink arrest: the extension clock freezes while the body pitches off its ext-start marks
            // (bounded by ArrestMax; logged per trip).
            float extDt = dt;
            if (getUpSitExtArrestOn && seatPhase == 2)
            {
                float pelvYNow = pelvisBody != null ? pelvisBody.position.y : 0f;
                if (!seatExtArrestInit)
                {
                    seatExtArrestInit = true;
                    seatExtPelvY0 = pelvYNow;
                    seatExtPrevTilt = seatBodyTilt;
                }
                float xRate = Mathf.Abs(seatBodyTilt - seatExtPrevTilt) / Mathf.Max(dt, 1e-4f);
                seatExtPrevTilt = seatBodyTilt;
                bool bad = xRate > getUpSitExtArrestRate
                    || pelvYNow < seatExtPelvY0 - getUpSitExtArrestDrop;
                if (bad && !seatExtArrestBad)
                {
                    seatExtArrestTrips++;
                    Debug.Log("GETUP-EXTARREST trip t=" + N(now, 3) + " rate=" + N(xRate, 1)
                        + " pelvY=" + N(pelvYNow, 3) + " y0=" + N(seatExtPelvY0, 3)
                        + " frozen=" + N(seatExtArrestT, 2) + " ext=" + N(seatExtT, 2));
                }
                seatExtArrestBad = bad;
                if (bad && seatExtArrestT < getUpSitExtArrestMax)
                {
                    seatExtArrestT += dt;
                    extDt = 0f;
                }
            }
            // Co-over gate: the extension clock freezes while the CoM is still behind the feet.
            if (getUpSitExtOver > 0.001f && seatPhase == 2
                && Mathf.Min(seatGapL, seatGapR) < -getUpSitExtOver)
                extDt = 0f;
            seatExtT += extDt;
        }
        seatPressNow = getUpSitSeatPress;
        if (getUpSitSeatMode == 2f) seatPressNow = -1f;
        else if (getUpSitSeatMode == 3f && seatPhase >= 2 && getUpSitSeatHand > 0.001f)
            seatPressNow = getUpSitSeatPress * Mathf.Clamp01(1f - seatExtT / getUpSitSeatHand);
    }

    // Push trace: logs the leg/trunk commands for the first seconds after a fire.
    private void PushTraceFrame(float now, float tilt)
    {
        if (!getUpPushTraceOn || getUpPushTraceT0 < 0f) return;
        float age = now - getUpPushTraceT0;
        if (age < 0f) return;
        if (age > getUpPushTraceSec)
        {
            getUpPushTraceT0 = -1f;
            return;
        }
        float lum = lumbarJoint != null ? lumbarJoint.jointAngle : 0f;
        Debug.Log("PUSHTRACE t=" + N(now, 3) + " a=" + N(age, 3) + " st=" + getUpStage
            + " kk=" + N(gusKeyk, 3) + " kK=" + N(gusKeyKnee, 3) + " kA=" + N(gusKeyAnkle, 3)
            + " hT=" + N(gusTgtHipL, 1) + " kT=" + N(gusTgtKneeL, 1) + " aT=" + N(gusTgtAnkleL, 1)
            + " hM=" + N(leftHipJoint != null ? leftHipJoint.jointAngle : 0f, 1)
            + " kM=" + N(leftKneeJoint != null ? leftKneeJoint.jointAngle : 0f, 1)
            + " aM=" + N(leftAnkleJoint != null ? leftAnkleJoint.jointAngle : 0f, 1)
            + " tTgt=" + N(torsoTargetDeg, 1) + " tMe=" + N(tilt, 1) + " lum=" + N(lum, 1)
            + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR=" + N(poseGroundFzRightN, 0));
    }

    // The trunk's blend over the ladder: the lumbar target of the stage in force, written through
    // DriveTrunk (the walk/stand driver).
    private void DriveGetUpTrunk(float fromDeg, float toDeg, float share, float biasDeg = 0f)
    {
        torsoTargetDeg = Mathf.Lerp(fromDeg, toDeg, Mathf.Clamp01(share));
        // The trunk governor's counter-lean (0 = none).
        if (biasDeg != 0f) torsoTargetDeg += biasDeg;
        DriveTrunk(0f, 0f, vestibular != null ? vestibular.GetBodyTilt() : 0f, true,
            torsoTargetDeg);
    }
}
