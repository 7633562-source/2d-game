using System.Globalization;
using UnityEngine;

// PoseDriveController: recorder publications, sole-plate ground-reaction meter, hip debug logger.
public partial class PoseDriveController
{
    // ─── pose gait ask publications ───
    // Recorder-only copies of what the pose library asked for this step; nothing reads them back.
    // 0 = the pose gait is not driving the walk (switch off, standing, or relaxed); never NaN.
    private float poseAskHipLDeg, poseAskKneeLDeg, poseAskAnkleLDeg;
    private float poseAskHipRDeg, poseAskKneeRDeg, poseAskAnkleRDeg;
    private float poseAskHipHeightRequestM, poseAskHipHeightLiveM;
    private float poseAskPelvisTiltTargetDeg;
    private int poseAskStepIndex;
    private int poseAskMaySwap, poseAskClockDone, poseAskLedOut;
    private int poseAskSwapReason;
    private int poseAskDriveState;
    // Recorder only: the pose path's foot placement for the step in force; nothing reads it back.
    private float poseAskPlaceXM;         // the placeX clamp's own answer, m
    private float poseAskSwingSoleXM;     // the swing foot's sole x, m
    private float poseAskStanceSoleXM;    // the stance foot's sole x, m
    private float poseAskPlaceLeadM;      // placeX - comX at the PLANT frame, m
    // Lead latch: written only at plant frames and never cleared inside a step, so the published lead
    // is the most recent plant's; the per-frame columns above are zeroed every step.
    private float placeLeadLatchM;
    // Recorder only: the reach guard's ceiling and centre, per frame; zeroed every step, not read back.
    private float poseAskReachXM;   // the guard's ceiling, m, -1 when inert
    private float poseAskHipXM;     // the hip ANCHOR x the guard measured from, m
    // Recorder only. Force and acceleration are measured on every tick, walking or not; the resolver
    // belongs to the ask block and follows its 0 sentinel. Nothing reads them back.
    private float poseGroundFxN = PoseGroundFxNoMeasureN;  // measured, N, +x = forward
    private float poseComAccelXMps2;                       // d(comVelX)/dt, m/s^2
    private float prevComVelX;                             // the one-step difference's own state
    private bool comAccelSeeded;                           // false until the second tick
    private int poseAskResolvedTerm;                       // 1..5 driving, 0 not driving
    // The four sole colliders the recorder's load meter reads (TrialRecorder.ResolveSoleColliders),
    // resolved once by name on this rig's transform.
    private readonly Collider2D[] groundFxPlate = new Collider2D[4];  // heelL, toeL, heelR, toeR
    private bool groundFxPlatesResolved;
    private ContactFilter2D groundFxFilter;
    private bool groundFxFilterReady;
    private readonly ContactPoint2D[] groundFxContacts = new ContactPoint2D[24];

    // Why the swap fired this step (capturePoseSwapReason). Codes are frozen so old CSVs stay readable.
    public const int PoseSwapReasonNone = 0;            // no swap on this step
    public const int PoseSwapReasonClock = 1;           // the half cycle ran out
    public const int PoseSwapReasonLead = 2;            // the CoM led the stance foot
    public const int PoseSwapReasonPlantedTimeout = 3;  // the swing never landed
    // Whether the pose gait wrote the joints this step (capturePoseDriveState).
    public const int PoseDriveStateStopped = 0;         // not driving (off/relaxed)
    public const int PoseDriveStateStand = 1;           // driving the stand pose
    public const int PoseDriveStateWalk = 2;            // driving the walk gait
    // capturePoseReachXM sentinel: the reach guard is inert (hip anchor at or above the leg's span).
    // A reach is a square root, never negative, so -1 collides with no reading and no 0 sentinel.
    public const float PoseReachInertM = -1f;
    // Which term resolved placeX this frame (capturePoseResolvedTerm), in the placement path's branch
    // order. Set where the value is decided; labels only, they add no branch or clamp.
    public const int PosePlaceTermNotDriving = 0;    // the pose gait is not driving the WALK
    public const int PosePlaceTermLaw = 1;           // the catching law survived the band
    public const int PosePlaceTermBandFloor = 2;     // the band's floor (stanceFootX + stepMinMeters) bound it
    public const int PosePlaceTermBandCeiling = 3;   // the band's ceiling (stanceFootX + stepMaxMeters) bound it
    public const int PosePlaceTermReachGuard = 4;    // the reach guard's clamp + ramp moved the value
    public const int PosePlaceTermReachInert = 5;    // the guard is INERT (hip anchor at or above the leg's span)
    // capturePoseGroundFxN sentinel: the meter could not run (no sole plate, or never ticked). No real
    // horizontal ground force reaches this magnitude; 0 is a reading (neither foot loaded).
    public const float PoseGroundFxNoMeasureN = -1000000f;

    // Recorder columns (deg, m, world x). Joint targets by rig side: hip - = flexion, knee + = flexion,
    // ankle + = toe down. Height request is latched at step start, so request - live = the step's sink.
    // Pelvis tilt target: - = forward. placeX = ask after band and reach clamps; soles = plate centres.
    public float capturePoseTargetHipLDeg => poseAskHipLDeg;
    public float capturePoseTargetKneeLDeg => poseAskKneeLDeg;
    public float capturePoseTargetAnkleLDeg => poseAskAnkleLDeg;
    public float capturePoseTargetHipRDeg => poseAskHipRDeg;
    public float capturePoseTargetKneeRDeg => poseAskKneeRDeg;
    public float capturePoseTargetAnkleRDeg => poseAskAnkleRDeg;
    public float capturePoseHipHeightRequestM => poseAskHipHeightRequestM;
    public float capturePoseHipHeightLiveM => poseAskHipHeightLiveM;
    public float capturePosePelvisTiltTargetDeg => poseAskPelvisTiltTargetDeg;
    public int capturePoseStepIndex => poseAskStepIndex;
    public int capturePoseMaySwap => poseAskMaySwap;
    public int capturePoseClockDone => poseAskClockDone;
    public int capturePoseLedOut => poseAskLedOut;
    public int capturePoseSwapReason => poseAskSwapReason;
    public int capturePoseDriveState => poseAskDriveState;
    public float capturePosePlaceXM => poseAskPlaceXM;
    public float capturePoseSwingSoleXM => poseAskSwingSoleXM;
    public float capturePoseStanceSoleXM => poseAskStanceSoleXM;
    public float capturePosePlaceLeadM => poseAskPlaceLeadM;
    // Reach guard ceiling (PoseReachInertM when inert) and the hip anchor x it was measured from.
    public float capturePoseReachXM => poseAskReachXM;
    public float capturePoseHipXM => poseAskHipXM;
    // Ground Fx: N, + = forward, 0 = no foot loaded (see SampleGroundReactionX). CoM accel: one-step
    // difference of comVelX, m/s^2. Resolved term: a PosePlaceTerm code, 0 = not driving the walk.
    public float capturePoseGroundFxN => poseGroundFxN;
    public float capturePoseComAccelXMps2 => poseComAccelXMps2;
    public int capturePoseResolvedTerm => poseAskResolvedTerm;

    // Sole writer of the ask block's 0 sentinel: called once per physics step before either gait
    // branch, so a row never carries the previous step's ask.
    private void ClearPoseAskRecord()
    {
        poseAskHipLDeg = 0f;
        poseAskKneeLDeg = 0f;
        poseAskAnkleLDeg = 0f;
        poseAskHipRDeg = 0f;
        poseAskKneeRDeg = 0f;
        poseAskAnkleRDeg = 0f;
        poseAskHipHeightRequestM = 0f;
        poseAskHipHeightLiveM = 0f;
        poseAskPelvisTiltTargetDeg = 0f;
        poseAskStepIndex = 0;
        poseAskMaySwap = 0;
        poseAskClockDone = 0;
        poseAskLedOut = 0;
        poseAskSwapReason = PoseSwapReasonNone;
        poseAskDriveState = PoseDriveStateStopped;
        // The lead latch (placeLeadLatchM) is deliberately not cleared: TickWalk republishes it each step.
        poseAskPlaceXM = 0f;
        poseAskSwingSoleXM = 0f;
        poseAskStanceSoleXM = 0f;
        poseAskPlaceLeadM = 0f;
        // 0 = not driving the walk; an inert reach guard is published as PoseReachInertM (-1) instead.
        poseAskReachXM = 0f;
        poseAskHipXM = 0f;
        poseAskResolvedTerm = PosePlaceTermNotDriving;
    }

    // ─── hip-channel burst logger ───
    // Debug.Log only, inside [-poseHipDbgFrom, -poseHipDbgTo] (To <= From = off); writes no state.
    // HIPW = one hip write (tag = writer); HIPF = all four hip muscles at TickGetUp entry and exit.
    private bool HipDbgOn()
    {
        float t = Time.fixedTime;
        return hipDbgTo > hipDbgFrom && t >= hipDbgFrom && t <= hipDbgTo;
    }

    private void HipDbgWrite(string tag, float sig, float tgt, HingeJoint2D hip, Muscle fx, Muscle ex)
    {
        if (!HipDbgOn()) return;
        Debug.Log("HIPW t=" + N(Time.fixedTime, 3) + " " + tag
            + " sig=" + N(sig, 4) + " tgt=" + N(tgt, 2)
            + " ang=" + N(hip != null ? hip.jointAngle : 0f, 3)
            + " flex=" + N(fx != null ? fx.activation : 0f, 5)
            + " ext=" + N(ex != null ? ex.activation : 0f, 5));
    }

    private void HipDbgFrame(string tag)
    {
        if (!HipDbgOn()) return;
        Debug.Log("HIPF t=" + N(Time.fixedTime, 3) + " " + tag
            + " Lf=" + N(balance != null && balance.leftHipFlexor != null ? balance.leftHipFlexor.activation : -1f, 5)
            + " Le=" + N(balance != null && balance.leftHipExtensor != null ? balance.leftHipExtensor.activation : -1f, 5)
            + " Rf=" + N(balance != null && balance.rightHipFlexor != null ? balance.rightHipFlexor.activation : -1f, 5)
            + " Re=" + N(balance != null && balance.rightHipExtensor != null ? balance.rightHipExtensor.activation : -1f, 5)
            + " stage=" + getUpStage);
    }

    // Per-foot vertical sole-plate force, N, recorder fzL/fzR convention: sum(normalImpulse*n.y)/dt.
    // Written each tick by SampleGroundReactionX for the load gates. 0 also means the meter is not
    // running, so an unresolved meter keeps the lead swap closed and the step on its clock.
    private float poseGroundFzLeftN;
    private float poseGroundFzRightN;

    // ─── horizontal ground reaction ───
    // Box2D plate impulse P = normalImpulse*n + tangentImpulse*(n.y, -n.x), supporting contacts only
    // (normalImpulse*n.y > 0). Fx = sum(P.x)/dt, N, + = forward: the only horizontal external force.
    private float SampleGroundReactionX()
    {
        if (!groundFxPlatesResolved) ResolveGroundFxPlates();
        bool anyPlate = false;
        for (int i = 0; i < 4; i++)
            if (groundFxPlate[i] != null) anyPlate = true;
        if (!anyPlate) return PoseGroundFxNoMeasureN;
        if (!groundFxFilterReady)
        {
            groundFxFilter = new ContactFilter2D();
            groundFxFilter.SetLayerMask(GroundLayers.Mask);
            groundFxFilter.useTriggers = false;
            groundFxFilterReady = true;
        }
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.005f;
        float impulseX = 0f;
        float impulseYLeft = 0f;
        float impulseYRight = 0f;
        // Heel and toe are two plates of one support; summing all four reads both feet.
        for (int i = 0; i < 4; i++)
        {
            Collider2D col = groundFxPlate[i];
            if (col == null) continue;
            int n = col.GetContacts(groundFxFilter, groundFxContacts);
            int count = Mathf.Min(n, groundFxContacts.Length);
            for (int k = 0; k < count; k++)
            {
                ContactPoint2D cp = groundFxContacts[k];
                // The load meter's support filter: only an impulse pushing the plate up the normal carries weight.
                if (cp.normalImpulse * cp.normal.y <= 0f) continue;
                impulseX += cp.normalImpulse * cp.normal.x
                    + cp.tangentImpulse * cp.normal.y;
                // Vertical component, summed per foot (0/1 = left heel/toe, 2/3 = right): the fzL/fzR reading.
                float fn = cp.normalImpulse * cp.normal.y;
                if (i < 2) impulseYLeft += fn;
                else impulseYRight += fn;
            }
        }
        poseGroundFzLeftN = impulseYLeft / dt;
        poseGroundFzRightN = impulseYRight / dt;
        return impulseX / dt;
    }

    private void ResolveGroundFxPlates()
    {
        groundFxPlate[0] = GroundFxPlate("LeftLegFoot");
        groundFxPlate[1] = GroundFxPlate("LeftLegToe");
        groundFxPlate[2] = GroundFxPlate("RightLegFoot");
        groundFxPlate[3] = GroundFxPlate("RightLegToe");
        bool any = groundFxPlate[0] != null || groundFxPlate[1] != null
            || groundFxPlate[2] != null || groundFxPlate[3] != null;
        // Latch only when something was found: an early call must not freeze four nulls forever.
        if (any) groundFxPlatesResolved = true;
    }

    private Collider2D GroundFxPlate(string name)
    {
        Transform t = transform.Find(name);
        return t != null ? t.GetComponent<Collider2D>() : null;
    }
}
