using System.Globalization;
using UnityEngine;

// PoseDriveController: command-line switches and the arena / reference-line entries.
public partial class PoseDriveController
{
    private void ReadSwitches()
    {
        active = Mathf.RoundToInt(GetFloat("-poseDrive", 0f)) != 0;
        walkEnabled = Mathf.RoundToInt(GetFloat("-poseWalk", 0f)) != 0;
        swingDuration = Mathf.Max(0.05f, GetFloat("-poseSwingT", swingDuration));
        stanceDuration = Mathf.Max(0.05f, GetFloat("-poseStanceT", stanceDuration));
        swingLiftMeters = Mathf.Max(0f, GetFloat("-poseLift", swingLiftMeters));
        // Stance knee keys (deg) drawn at SwingFold / FootPlace / PlantLoad. 4..26 is the switch range;
        // ArenaGenome's narrower box must stay inside it, and the shipped default inside both.
        stanceFoldKneeDeg = Mathf.Clamp(GetFloat("-poseStanceFoldKnee", stanceFoldKneeDeg), 4f, 26f);
        stancePlaceKneeDeg = Mathf.Clamp(GetFloat("-poseStancePlaceKnee", stancePlaceKneeDeg), 4f, 26f);
        stanceLoadKneeDeg = Mathf.Clamp(GetFloat("-poseStanceLoadKnee", stanceLoadKneeDeg), 4f, 26f);
        swingLandShare = Mathf.Clamp(GetFloat("-poseLandShare", swingLandShare), 0.3f, 0.98f);
        heightGainDegPerMeter = GetFloat("-poseHeightGain", heightGainDegPerMeter);
        heightDampDegPerMeterPerSecond =
            GetFloat("-poseHeightDamp", heightDampDegPerMeterPerSecond);
        // Step vault (bob) amplitude, m; bounded to 0..0.06 - a dose, not a spring. 0 = off.
        bobAmpMeters = Mathf.Clamp(GetFloat("-poseBobAmp", bobAmpMeters), 0f, 0.06f);
        placementMarginMeters = GetFloat("-posePlaceMargin", placementMarginMeters);
        comLeadSwapMeters = Mathf.Max(0.01f, GetFloat("-poseLeadSwap", comLeadSwapMeters));
        // Settle step (see UpdateWalkSettle): budget in seconds; 0 = off, plain walk-and-stop.
        walkSettleBudget = Mathf.Max(0f, GetFloat("-poseWalkSettle", 6f));    // CYCLE 123 landed: armed by default (6 s); -poseWalkSettle 0 = off
        walkSettleMps = Mathf.Max(0.01f, GetFloat("-poseWalkSettleMps", walkSettleMps));
        walkSettleCatch = Mathf.Clamp(GetFloat("-poseWalkSettleCatch", walkSettleCatch), 0.5f, 1.6f);
        walkSettleLeadM = Mathf.Clamp(GetFloat("-poseWalkSettleLead", walkSettleLeadM), -0.15f, 0.25f);
        walkSettleThreatM = Mathf.Max(0f, GetFloat("-poseWalkSettleThreat", 0f));
        walkSettlePush = Mathf.Clamp01(GetFloat("-poseWalkSettlePush", walkSettlePush));
        walkSettleBendDeg = Mathf.Clamp(GetFloat("-poseWalkSettleBend", walkSettleBendDeg), 0f, 60f);
        walkSettleBendLoad = Mathf.Max(0f, GetFloat("-poseWalkSettleBendLoad", walkSettleBendLoad));
        walkSettleBendBar = Mathf.Clamp(GetFloat("-poseWalkSettleBendBar", walkSettleBendBar), 0f, 3f);
        walkSettleBendCap = Mathf.Clamp(GetFloat("-poseWalkSettleBendCap", walkSettleBendCap), 0f, 80f);
        walkSettleHoldDeg = Mathf.Clamp(GetFloat("-poseWalkSettleHold", walkSettleHoldDeg), 0f, 60f);
        walkSettleHoldMps = Mathf.Max(0.01f, GetFloat("-poseWalkSettleHoldMps", walkSettleHoldMps));
        walkSettleHoldQuiet = Mathf.Max(0f, GetFloat("-poseWalkSettleHoldQuiet", walkSettleHoldQuiet));
        walkSettleHoldMax = Mathf.Max(0f, GetFloat("-poseWalkSettleHoldMax", walkSettleHoldMax));
        walkSettleExitMps = Mathf.Max(0f, GetFloat("-poseWalkSettleExit", walkSettleExitMps));
        walkSettleExitQuiet = Mathf.Max(0f, GetFloat("-poseWalkSettleExitQuiet", walkSettleExitQuiet));
        walkSettleExitMargin = GetFloat("-poseWalkSettleExitMargin", walkSettleExitMargin);
        walkSettleGuardWin = Mathf.Max(0f, GetFloat("-poseWalkSettleGuard", walkSettleGuardWin));
        walkSettleHandOn = Mathf.RoundToInt(GetFloat("-poseWalkSettleHand", 1f)) != 0;
        walkSettleBlendS = Mathf.Clamp(GetFloat("-poseWalkSettleBlend", walkSettleBlendS), 0f, 3f);
        // Placement bias reads the knee the descent leaves instead of the drawn knee (default 0 = drawn).
        placeKneeLive = Mathf.RoundToInt(GetFloat("-posePlaceKneeLive", 0f)) != 0;
        stepMinMeters = GetFloat("-poseStepMin", stepMinMeters);
        stepMaxMeters = Mathf.Max(stepMinMeters + 0.01f, GetFloat("-poseStepMax", stepMaxMeters));
        // req-201 step-length symmetry trim (default 0 = off): gain, clamp, walk-start value, m.
        poseWalkSymTrimGain = GetFloat("-poseWalkSymTrim", poseWalkSymTrimGain);
        poseWalkSymTrimMax = Mathf.Max(0f, GetFloat("-poseWalkSymTrimMax", poseWalkSymTrimMax));
        poseWalkSymTrim0 = Mathf.Clamp(GetFloat("-poseWalkSymTrim0", poseWalkSymTrim0),
            -poseWalkSymTrimMax, poseWalkSymTrimMax);
        // req-203: the read source (0 = the plant frame, the default; 1 = the load-taking swap).
        poseWalkSymRead = GetFloat("-poseWalkSymRead", poseWalkSymRead);
        // req-226 dynamic hip anchor (absent = off): 0 off | 1 joint converge | 2 aim from the swing
        // hip. Amp m is clamped to the built +-half-spacing, 0 legal (full converge to the centre).
        hipDynMode = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-hipDynMode", 0f)), 0, 2);
        hipDynAmp = Mathf.Clamp(GetFloat("-hipDynAmp", hipDynAmp), 0f, 0.01476f);
        hipDynIn = Mathf.Clamp(GetFloat("-hipDynIn", hipDynIn), 0.005f, 1f);
        hipDynOut = Mathf.Clamp(GetFloat("-hipDynOut", hipDynOut), 0f, 1f);
        hipDynAim = Mathf.Clamp(GetFloat("-hipDynAim", hipDynAim), 0f, 1.2f);
        // -poseAimHipAnchor 1 (default 0): the step aim and the reach guard read the swing hip's own
        // hinge anchor x instead of the pelvis centre (14.76 mm apart, Human.CreateLeg). Live anchor.
        poseAimHipAnchor = GetFloat("-poseAimHipAnchor", 0f) > 0.5f;
        standCaptureSeconds = GetFloat("-poseStandCap", standCaptureSeconds);
        armSwingDeg = GetFloat("-poseArmSwing", armSwingDeg);
        // Swing ankle carry, deg, bounded to +-20 (the stance band's order); 0 = off.
        // Rig sign: plus = toe down / heel rise, minus = toe up.
        swingAnkleDeg = Mathf.Clamp(GetFloat("-poseAnkleCarry", swingAnkleDeg), -20f, 20f);
        swingAnkleRampSeconds = Mathf.Max(0f, GetFloat("-poseAnkleRamp", swingAnkleRampSeconds));
        // Stand's flat-sole toe hold (see the field block). Scale <= 1 = off, the base toe law applies;
        // the arm / drop / hold thresholds have their own switches.
        standToeHoldScale = Mathf.Max(0f, GetFloat("-standToeHold", standToeHoldScale));
        standToeHoldBiasDeg = GetFloat("-standToeBias", standToeHoldBiasDeg);
        standToeFlatArmDeg = Mathf.Max(0f, GetFloat("-standToeFlatArm", standToeFlatArmDeg));
        standToeFlatDropDeg = Mathf.Max(standToeFlatArmDeg,
            GetFloat("-standToeFlatDrop", standToeFlatDropDeg));
        standToeFlatHoldSeconds = Mathf.Max(0f,
            GetFloat("-standToeFlatHold", standToeFlatHoldSeconds));
        // Squat gate (default on): the toe hold sleeps when intent.toeHoldBlock is set or the squat
        // machine runs (intent.squatReps > 0).
        standToeSquatGate = GetFloat("-standToeSquatGate", standToeSquatGate);
        // Push-off delivery: the stance ankle's P demand is multiplied toward this value (1..20) on the
        // loaded roll window [cross+0.145, cross+0.255]. Default 1 = off.
        pushDeliverP = Mathf.Clamp(GetFloat("-posePushDeliverP", pushDeliverP), 1f, 20f);
        // Gate load bound (N) on the step that follows a flight tail; 50 = the base bound (off).
        // Clamped between SupportHoldLoadN and 800 N, the collapse band.
        poseNextTailLoadN = Mathf.Clamp(GetFloat("-poseNextTailLoadN", poseNextTailLoadN),
            PoseLibrary.SupportHoldLoadN, 800f);
        // Plant clock: seconds the incoming support's load must stay at or above the swap gate before
        // the fire may read the plant; 0 = off. Bounded to 0..0.10 s (a fifth of a step): past that,
        // the step the fire belongs to is over.
        poseTailHoldSeconds = Mathf.Clamp(GetFloat("-poseTailHold", poseTailHoldSeconds), 0f, 0.10f);
        ankleBalanceP = GetFloat("-poseAnkleP", ankleBalanceP);
        ankleBalanceD = GetFloat("-poseAnkleD", ankleBalanceD);
        clAnkleCapEta = Mathf.Clamp(GetFloat("-clAnkleCap", clAnkleCapEta), 0f, 2f);
        clWalkEntryMode = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-clWalkEntry", clWalkEntryMode)), 0, 2);
        // -clEntrySwingFirst (default 0): 1 the right leg swings first on a fresh entry, 2 alternates
        // per walk. 0 leaves the rear-foot / load decision of EnterWalkFresh untouched.
        clEntrySwingFirst = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-clEntrySwingFirst", 0f)), 0, 2);
        clAnkleCapStandEta = Mathf.Clamp(GetFloat("-clAnkleCapStand", clAnkleCapStandEta), 0f, 2f);
        clStanceCloseOn = GetFloat("-clStanceClose", 0f) > 0.5f;
        clCloseMinM = Mathf.Clamp(GetFloat("-clStanceCloseMin", clCloseMinM), 0.02f, 0.6f);
        clCloseStepS = Mathf.Clamp(GetFloat("-clStanceCloseT", clCloseStepS), 0.2f, 1.5f);
        clCloseRearmS = Mathf.Clamp(GetFloat("-clStanceCloseRearm", clCloseRearmS), 0f, 60f);
        clCloseAimM = Mathf.Clamp(GetFloat("-clStanceCloseAim", 0f), -0.25f, 0.25f);
        clCloseDropMps = Mathf.Clamp(GetFloat("-clStanceCloseDrop", 0f), 0f, 2f);
        clCloseSpeedMps = Mathf.Clamp(GetFloat("-clStanceCloseSpeed", 0f), 0f, 5f);
        clCloseLoopOn = GetFloat("-clStanceCloseLoop", 0f) > 0.5f;
        clCloseLandMps = Mathf.Clamp(GetFloat("-clStanceCloseLand", clCloseLandMps), 0.02f, 2f);
        clCloseTolM = Mathf.Clamp(GetFloat("-clStanceCloseTol", clCloseTolM), 0.005f, 0.2f);
        clCloseKx = Mathf.Clamp(GetFloat("-clStanceCloseKx", clCloseKx), 0f, 2f);
        clCloseSeatM = Mathf.Clamp(GetFloat("-clStanceCloseSeat", 0f), 0f, 0.05f);
        clClosePullBackOn = GetFloat("-clClosePullBack", 0f) > 0.5f;
        clClosePullComM = Mathf.Clamp(GetFloat("-clClosePullCom", 0f), 0f, 1f);
        clClosePullLiftK = Mathf.Clamp01(GetFloat("-clClosePullLift", 1f));
        clCloseDoneLoad = Mathf.Clamp(GetFloat("-clCloseDoneLoad", 0f), 0f, 2000f);
        clCloseStallS = Mathf.Clamp(GetFloat("-clCloseStall", 2.5f), 0.5f, 10f);
        clCloseDoneKnee = Mathf.Clamp(GetFloat("-clCloseDoneKnee", 0f), 0f, 90f);
        clClosePullHomeM = Mathf.Clamp(GetFloat("-clClosePullHome", 0f), -0.6f, 0.5f);
        clCloseShiftHoldS = Mathf.Clamp(GetFloat("-clCloseShiftHold", 0f), 0f, 5f);
        clCloseHoldM = Mathf.Clamp(GetFloat("-clStanceCloseHold", 0f), 0f, 0.05f);
        clCloseHoldHop = Mathf.Clamp(GetFloat("-clStanceCloseHoldHop", 0f), 0f, 0.2f);
        clCloseNudgeM = Mathf.Clamp(GetFloat("-clStanceCloseNudge", 0f), 0f, 0.1f);
        clCloseRetryM = Mathf.Clamp(GetFloat("-clStanceCloseRetry", 0f), 0f, 0.1f);
        clCloseRetryT = Mathf.Clamp(GetFloat("-clStanceCloseRetryT", 0f), 0f, 2f);
        clCloseRetryGain = Mathf.Clamp(GetFloat("-clStanceCloseRetryGain", 0f), 0f, 1f);
        clCloseRetryExtra = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-clStanceCloseRetries", 0f)), 0, 6);
        clStopLandPhase = Mathf.Clamp(GetFloat("-clStopLand", 0f), 0f, 1f);
        clEntryRearM = Mathf.Clamp(GetFloat("-clEntryRear", clEntryRearM), 0f, 1f);
        clEntryLoadOn = GetFloat("-clEntryLoad", 0f) > 0.5f;
        clEntryReadyS = Mathf.Clamp(GetFloat("-clEntryReady", 0f), 0f, 20f);
        clEntryReadyStaggerM = Mathf.Clamp(GetFloat("-clEntryReadyStagger", clEntryReadyStaggerM), 0.005f, 0.3f);
        clEntryReadyLatchOn = GetFloat("-clEntryReadyLatch", 1f) > 0.5f;
        clEntryReadyQuietS = Mathf.Clamp(GetFloat("-clEntryReadyQuiet", clEntryReadyQuietS), 0.02f, 5f);
        clEntryReadyVel = Mathf.Clamp(GetFloat("-clEntryReadyVel", clEntryReadyVel), 0.005f, 1f);
        clEntryMaskOn = GetFloat("-clEntryMask", 0f) > 0.5f;
        clEntryStraightDeg = Mathf.Max(0f, GetFloat("-clEntryStraight", 0f));
        // The stop's coast-down (-clReleaseRamp, seconds): on the walk key's release while walking the
        // command decays 1 -> 0.2 over the span then drops to 0 (the shape -walkPlanBrake ramps, so the
        // -assistBrake governor and the catch act on it). 0 = off: the key-up cut, tick for tick as is.
        clReleaseRamp = Mathf.Clamp(GetFloat("-clReleaseRamp", 0f), 0f, 10f);
        // The coast-down's keep (-clReleaseRampKeep k, seconds, 0 = off): the span's end does not cut
        // the command at once - the walk window is held at the floor k more seconds, then drops to 0.
        clReleaseRampKeep = Mathf.Clamp(GetFloat("-clReleaseRampKeep", 0f), 0f, 5f);
        // The coast-down's fade (-clReleaseRampFade f, seconds, 0 = off): the span's end does not cut
        // the command at the floor - it decays floor -> 0 over f more seconds (the coast's real end).
        // A press inside the fade cancels it exactly as it cancels the ramp.
        clReleaseRampFade = Mathf.Clamp(GetFloat("-clReleaseRampFade", 0f), 0f, 5f);
        // The coast-down's catch seat (-clReleaseRampCatch, share 0..1, 0 = off): while the ramp runs
        // the step placement's catch share rises 0.5 -> c on the ramp's clock (the harness brake's law
        // at -walkPlanBrakeCatch) and is restored when the ramp ends or is cancelled.
        clReleaseRampCatch = Mathf.Clamp(GetFloat("-clReleaseRampCatch", 0f), 0f, 1f);
        // The coast-down's shape (-clReleaseRampShape p, exponent, 0 = off): the span's decay is
        // re-shaped to u^p - p > 1 holds the command near full speed longer and drops fast at the
        // end; p < 1 spends the span near the floor. The end values (1 and 0.2) do not move.
        clReleaseRampShape = Mathf.Clamp(GetFloat("-clReleaseRampShape", 0f), 0f, 5f);
        // The coast's stand (-clReleaseRampClose m, metres, 0 = off): the stand left by a coast can sit
        // at a stagger between the entry gate's ready bar (0.10) and the plain close's arming bar
        // (clCloseMinM, 0.15) - the close leaves such feet alone, the gate never reads them ready, and
        // a restart waits out the gate and falls. With the switch the close's arming bar for the stand
        // right after a coast is m, so the feet are levelled as a plain stop's are.
        clReleaseRampClose = Mathf.Clamp(GetFloat("-clReleaseRampClose", 0f), 0f, 0.30f);
        // The coast's press (-clReleaseRampPress v, 0..1, 0 = off): a walk key pressed again while the
        // coast-down runs and its command is at or under v ENDS the coast as its span's end does -
        // the command to 0, the catch restored, the post-close armed - and the held key goes to the
        // entry gate as a press from a stand (the sources keep the step driver off it before the
        // gate opens). Above v (and with the switch off) the press cancels the coast as before.
        clReleaseRampPress = Mathf.Clamp(GetFloat("-clReleaseRampPress", 0f), 0f, 1f);
        // The press-ended coast's last glide (-clReleaseRampPressFall s, 0 = off): the press that
        // would end the coast instead runs the command on to the floor over s seconds - the span
        // end's own value - and only then does the end (and the gate's press) fire, s later.
        clReleaseRampPressFall = Mathf.Clamp(GetFloat("-clReleaseRampPressFall", 0f), 0f, 1.5f);
        // The caught press rides the span (-clReleaseRampPressSpan 1, 0 = off): a press that would end
        // the coast early instead lets the span run to its own end - the command keeps its ramp to the
        // floor and the end (press-marked, the held key) fires at the span's end as the natural end does.
        clReleaseRampPressSpan = GetFloat("-clReleaseRampPressSpan", 0f) > 0.5f;
        // The phase release (req-117; see ClReleasePhaseKey in Walk.cs). Default 0 = off.
        clReleasePhaseOn = GetFloat("-clReleasePhase", 0f) > 0.5f;
        clReleasePhaseD = Mathf.Clamp(GetFloat("-clReleasePhaseD", 0.10f), 0f, 1f);
        clReleasePhaseW = Mathf.Clamp(GetFloat("-clReleasePhaseW", 0.15f), 0f, 2f);
        clReleasePhaseMax = Mathf.Clamp(GetFloat("-clReleasePhaseMax", 1.75f), 0.05f, 10f);
        // The plant hook's own landing bar (req-136 C1; with u < 1 the lead >= 0.20 term is asked of an
        // early landing only (blendU < 0.99995) - a full landing is accepted as with the switch off).
        // Default 1 = today's bar 0.99995 exactly.
        clReleasePhaseLandU = Mathf.Clamp(GetFloat("-clReleasePhaseLandU", 1f), 0.5f, 1f);
        // -clReleasePhaseHold (req-144 / req-147, see ClReleasePhaseKey): a key-up the phase release
        // would pass while the walk runs is held like a pend and released at the walk's next right
        // plant + D. 0 = off (default). 2 = only when this walk has no accepted right plant yet
        // (R < 0). 1 = req-144 round 5's arm (R < 0, or a foot in the air, or the walk <= 2.6 s old).
        clReleasePhaseHold = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-clReleasePhaseHold", 0f)), 0, 2);
        // -clReleasePhaseSide (default 0): which plant the phase release accepts. 0 right only (the
        // landed behaviour); 1 either plant; 2 alternate per stop - the side is picked when the stop's
        // hold starts and flips every stop.
        clReleasePhaseSide = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-clReleasePhaseSide", 0f)), 0, 2);
        // Quiet stand (see the field block): the low-pass is given in ms.
        clStandLpS = Mathf.Clamp(GetFloat("-clStandLp", 0f), 0f, 200f) * 0.001f;
        clStandLpMask = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-clStandLpMask", clStandLpMask)), 0, 7);
        clStandLpScope = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-clStandLpScope", clStandLpScope)), 1, 2);
        clStandDMul = Mathf.Clamp(GetFloat("-clStandD", clStandDMul), 0f, 2f);
        clStandKneeMinDeg = Mathf.Clamp(GetFloat("-clStandKneeMin", 0f), 0f, 20f);
        // Kick support-side shift terms (see the field block for the law); 0 = off.
        // Signed on purpose: the term is tested in both directions.
        kickShiftGainDeg = GetFloat("-kickShiftGain", kickShiftGainDeg);
        kickSupportHold = Mathf.Clamp01(GetFloat("-kickHoldStiff", kickSupportHold));
        kickShiftTorsoDeg = GetFloat("-kickShiftTorso", kickShiftTorsoDeg);
        kickShiftComMeters = GetFloat("-kickShiftCom", kickShiftComMeters);
        // Kick wind-up: static CoM-loop offset (m) while the strike's level is up. The loop's own sign:
        // larger = stronger backward command. 0 = off.
        kickWindComMeters = GetFloat("-kickWindCom", kickWindComMeters);
        // Wind-up gain (0..1): re-scales the CoM loop's channel while the inter-stroke window is up,
        // with its reference moved to the standing foot's centre (see the field block). 0 = off.
        kickWindGain = Mathf.Clamp01(GetFloat("-kickWindGain", kickWindGain));
        // Wind-up reference latch: 0 (default) = live standing-foot plate centre, 1 = re-latched at
        // each inter-stroke window's rising edge, 2 = latched once per burst, carried across strokes.
        kickWindLatch = Mathf.Clamp(GetFloat("-kickWindLatch", kickWindLatch), 0f, 2f);
        // Wind-up release hold: seconds the term stays alive after the request level reaches 0;
        // 0 = no hold.
        kickWindHold = Mathf.Clamp(GetFloat("-kickWindHold", kickWindHold), 0f, 30f);
        // Wind-up reference: 0 (default) = standing foot's plate centre, 1 = support hull's centre.
        kickWindRef = Mathf.Clamp01(GetFloat("-kickWindRef", kickWindRef));
        // Wind-up per-window normalizer (0..1): the P-channel dose the term delivers at the error the
        // window itself carries (see the field block). 0 (default) = off.
        kickWindNorm = Mathf.Clamp01(GetFloat("-kickWindNorm", kickWindNorm));
        // Window level while a fire is due and held (balance.kickFireHeld): 0..1, -1 (default) = off.
        // 0 = the loop's own law, 1 = the wind-up term at its ceiling (see the field block).
        // -1 stays a sentinel because Clamp01(-1) would give 0, a meaningful level.
        float dueLevel = GetFloat("-kickWindDue", kickWindDue);
        kickWindDue = dueLevel < 0f ? -1f : Mathf.Clamp01(dueLevel);
        // Term level during the stroke's head (balance.kickHeadActive: the lift / unload window):
        // 0..1, -1 (default) = off; sentinel kept as for -kickWindDue.
        float headLevel = GetFloat("-kickWindHead", kickWindHead);
        kickWindHead = headLevel < 0f ? -1f : Mathf.Clamp01(headLevel);
        // Parked-stand margin, m: during the inter-stroke window the reference moves back along the
        // travel axis, so the gain sees a larger error when the stand parks near the foot's front edge.
        // 0 (default) = off; bounded to 0..0.10 m, the loop's own comOffsetReference.
        kickWindMargin = Mathf.Clamp(GetFloat("-kickWindMargin", kickWindMargin), 0f, 0.10f);
        // Head authority: 0..1 cap on the flexor-side command while balance.kickHeadActive is up;
        // one-sided, the brake side is untouched. -1 (default) = off; 0 is valid (no push at the head).
        float headPushLevel = GetFloat("-kickHeadPush", kickHeadPush);
        kickHeadPush = headPushLevel < 0f ? -1f : Mathf.Clamp01(headPushLevel);
        // Stroke flight: 0..1 term reference and level while balance.kickStrokeActive is up after the
        // head (one-legged phase and landing tail). -1 (default) = off; 0 is valid (the loop's own law).
        float flightLevel = GetFloat("-kickWindFlight", kickWindFlight);
        kickWindFlight = flightLevel < 0f ? -1f : Mathf.Clamp01(flightLevel);
        // Support plant: 1 (default) holds the support hip at the angle latched on the fire step
        // (inside the stand's hipHoldBand, as in quiet stance); 0 = hip under the free pelvis law.
        kickPlantHold = Mathf.Clamp01(GetFloat("-kickPlantHold", kickPlantHold));
        // Plant band, deg: the support hip hold's band while the strike's plant is on. -1 (default) =
        // use balance.hipHoldBand; 0 is valid (MixHipHold's band guard then turns the hold off).
        float plantBandDeg = GetFloat("-kickPlantBand", kickPlantBand);
        kickPlantBand = plantBandDeg < 0f ? -1f : plantBandDeg;
        // Walk neck/head hold inertia share, 0..1 (see the field's note).
        walkNeckHoldShare = Mathf.Clamp(GetFloat("-poseNeckHoldShare", walkNeckHoldShare), 0f, 1f);
        // Get-up and hand-grip families, both default 0: unset, none of this runs and the Fallen
        // branch keeps its base behaviour.
        getUpEnabled = Mathf.RoundToInt(GetFloat("-poseGetUp", 0f)) != 0;
        getUpPatience = Mathf.Clamp(GetFloat("-poseGetUpPatience", 1f), 0.25f, 8f);
        handGripEnabled = Mathf.RoundToInt(GetFloat("-poseHandGrip", 0f)) != 0;
        handGripHoldSeconds = Mathf.Clamp(GetFloat("-poseHandGripHold", handGripHoldSeconds),
            0f, 2f);
        handGripPressOn = Mathf.RoundToInt(GetFloat("-poseHandGripPress", 1f)) != 0;
        // Grip tear budget, m: the anchored slip a live grip survives before it tears
        // (0.03 = the dog's claw default).
        handGripSlipMeters = Mathf.Clamp(GetFloat("-poseHandGripSlip", handGripSlipMeters),
            0.005f, 0.5f);
        if (handGripEnabled)
            Debug.Log("C111-SLIP budget=" + handGripSlipMeters + " press=" + handGripPressOn);
        getUpStandTiltDeg = Mathf.Clamp(GetFloat("-poseGetUpTilt", getUpStandTiltDeg),
            -45f, -5f);
        // Roll family (on back -> roll to side first). Default follows the scene: on with -tiltPlatform
        // (the deck tosses the body back mid-ladder), off on flat ground, where the flip loop starves
        // the sit route. An explicit -poseGetUpRoll 1/0 always wins.
        float rollArg = GetFloat("-poseGetUpRoll", -1f);
        bool rollExplicit = rollArg >= 0f;
        bool onTiltDeck = GetFloat("-tiltPlatform", 0f) > 0.5f;
        getUpRollEnabled = rollExplicit ? Mathf.RoundToInt(rollArg) != 0 : onTiltDeck;
        if (getUpEnabled)
            Debug.Log("C104-ROLLGATE deck=" + onTiltDeck + " set=" + rollExplicit
                + " roll=" + getUpRollEnabled);
        getUpRollForce = Mathf.Clamp01(GetFloat("-poseGetUpRollForce", 0f));
        getUpRollDwell = Mathf.Clamp(GetFloat("-poseGetUpRollDwell", 0.35f), 0f, 3f);
        getUpRollCap = Mathf.Max(1, Mathf.RoundToInt(GetFloat("-poseGetUpRollCap", 2f)));
        // Stand family, default 0 = off (the rise keeps the plain unwind). Pose defaults hip -5 /
        // knee +7 / ankle +7 deg are the settled standing pose.
        getUpStandMode = Mathf.RoundToInt(GetFloat("-poseGetUpStand", 0f)) != 0;
        getUpStandHold = Mathf.Clamp(GetFloat("-poseGetUpStandHold", 0.5f), 0f, 0.9f);
        getUpStandHipDeg = Mathf.Clamp(GetFloat("-poseGetUpStandHip", -5f), -60f, 60f);
        getUpStandKneeDeg = Mathf.Clamp(GetFloat("-poseGetUpStandKnee", 7f), -20f, 90f);
        getUpStandAnkleDeg = Mathf.Clamp(GetFloat("-poseGetUpStandAnkle", 7f), -45f, 45f);
        getUpStandFrom = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-poseGetUpStandFrom", 0f)), 0, 1);
        // Contact gate, default 0 = off: the Rise stage clock is the stage age (riseClock == stageAge).
        getUpStandGate = Mathf.RoundToInt(GetFloat("-poseGetUpStandGate", 0f)) != 0;
        getUpStandGateMax = Mathf.Clamp(GetFloat("-poseGetUpStandGateMax", 3f), 0f, 30f);
        // Base gate, default 0 = off: no fast path, no wait, no press hold.
        getUpBaseOn = Mathf.RoundToInt(GetFloat("-poseGetUpBase", 0f)) != 0;
        getUpBaseMax = Mathf.Clamp(GetFloat("-poseGetUpBaseMax", 3f), 0f, 30f);
        getUpBasePress = Mathf.Clamp01(GetFloat("-poseGetUpBasePress", 0f));
        getUpWaitPress = Mathf.Clamp(GetFloat("-poseGetUpWaitPress", -1f), -1f, 1f);
        getUpWaitPressSec = Mathf.Clamp(GetFloat("-poseGetUpWaitPressSec", 1.2f), 0.05f, 10f);
        getUpBaseDraw = Mathf.Clamp01(GetFloat("-poseGetUpBaseDraw", 1f));
        getUpBaseRise = Mathf.RoundToInt(GetFloat("-poseGetUpBaseRise", 0f)) != 0;
        // Settle gate, default 0 = off: the base fast path fires on the bare state.
        getUpBaseSettleSec = Mathf.Clamp(GetFloat("-poseGetUpBaseSettle", 0f), 0f, 5f);
        getUpBaseSettleVel = Mathf.Clamp(GetFloat("-poseGetUpBaseSettleVel", 0.6f), 0.02f, 5f);
        // Settle anti-flicker forms, default 0 = off: Drop > 0 = debounce tolerance, Duty > 0 = duty form.
        getUpBaseSettleDrop = Mathf.Clamp(GetFloat("-poseGetUpBaseSettleDrop", 0f), 0f, 1f);
        getUpBaseSettleDutyWin = Mathf.Clamp(GetFloat("-poseGetUpBaseSettleDuty", 0f), 0f, 1f);
        getUpBaseSettleDutyFrac = Mathf.Clamp(GetFloat("-poseGetUpBaseSettleDutyFrac", 0.6f), 0.05f, 1f);
        getUpBaseSettleOn = getUpBaseSettleSec > 0f || getUpBaseSettleDutyWin > 0f;
        // Load gate / sit-back / hip-first family; all default 0 = off.
        getUpBaseLoadFrac = Mathf.Max(0f, GetFloat("-poseGetUpBaseLoad", 0f));
        getUpBaseLoadSec = Mathf.Clamp(GetFloat("-poseGetUpBaseLoadSec", 0.1f), 0f, 5f);
        getUpBaseLoadDrop = Mathf.Clamp(GetFloat("-poseGetUpBaseLoadDrop", 0f), 0f, 1f);
        getUpLoadExtOn = Mathf.RoundToInt(GetFloat("-poseGetUpLoadExt", 0f)) != 0;
        getUpLoadExtKeep = Mathf.Clamp(GetFloat("-poseGetUpLoadExtKeep", 0.04f), 0f, 1f);
        getUpLoadExtFire = Mathf.Clamp(GetFloat("-poseGetUpLoadExtFire", 0.10f), 0f, 2f);
        getUpLoadExtSec = Mathf.Clamp(GetFloat("-poseGetUpLoadExtSec", 0.3f), 0f, 5f);
        getUpLoadExtDrop = Mathf.Clamp(GetFloat("-poseGetUpLoadExtDrop", 0.05f), 0f, 1f);
        getUpLoadExtRelatch = Mathf.RoundToInt(GetFloat("-poseGetUpLoadExtRelatch", 0f)) != 0;
        getUpBaseSquatDeg = Mathf.Clamp(GetFloat("-poseGetUpBaseSquat", 0f), 0f, 60f);
        getUpStandHipFirst = Mathf.Clamp(GetFloat("-poseGetUpStandHipFirst", 0f), 0f, 0.9f);
        getUpStandKneeClimb = Mathf.Clamp(GetFloat("-poseGetUpStandKneeClimb", 0f), 0f, 3f);
        getUpAutoDwell = Mathf.Clamp(GetFloat("-autoGetUp", 0f), 0f, 30f);
        getUpStandHipStart = Mathf.Clamp(GetFloat("-poseGetUpStandHipStart", 0f), 0f, 0.95f);
        getUpStandKneeStart = Mathf.Clamp(GetFloat("-poseGetUpStandKneeStart", -1f), -1f, 0.95f);
        getUpStandAnkleStart = Mathf.Clamp(GetFloat("-poseGetUpStandAnkleStart", -1f), -1f, 0.95f);
        getUpMoveGuardOn = Mathf.RoundToInt(GetFloat("-poseGetUpMoveGuard", 0f)) != 0;
        getUpMoveGuardMax = Mathf.Clamp(GetFloat("-poseGetUpMoveGuardMax", 3f), 0f, 30f);
        getUpTrunkGovMode = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-poseGetUpTrunkGov", 0f)), 0, 3);
        getUpTrunkGovDeg = Mathf.Clamp(GetFloat("-poseGetUpTrunkGovDeg", 40f), 5f, 160f);
        getUpTrunkGovK = Mathf.Clamp(GetFloat("-poseGetUpTrunkGovK", 0.5f), 0f, 3f);
        getUpTrunkGovMax = Mathf.Clamp(GetFloat("-poseGetUpTrunkGovMax", 2f), 0f, 30f);
        getUpTrunkGovCap = Mathf.Clamp(GetFloat("-poseGetUpTrunkGovCap", 0f), 0f, 180f);
        getUpPushTraceOn = Mathf.RoundToInt(GetFloat("-poseGetUpPushTrace", 0f)) != 0;
        getUpPushTraceSec = Mathf.Clamp(GetFloat("-poseGetUpPushTraceSec", 2.5f), 0.1f, 10f);
        getUpStandAnkleFast = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-poseGetUpStandAnkleFast", 0f)), 0, 2);
        // Sit-stand conversion, default 0 = off: the window helper returns false.
        getUpSitStandOn = Mathf.RoundToInt(GetFloat("-poseGetUpSitStand", 0f)) != 0;
        getUpSitStandHold = Mathf.Clamp(GetFloat("-poseGetUpSitStandHold", 0.10f), 0f, 5f);
        getUpSitStandMax = Mathf.Clamp(GetFloat("-poseGetUpSitStandMax", 6f), 0f, 60f);
        getUpSitStandKeep = Mathf.Clamp(GetFloat("-poseGetUpSitStandKeep", 0.05f), 0f, 2f);
        getUpSitStandVel = Mathf.Clamp(GetFloat("-poseGetUpSitStandVel", 0.50f), 0f, 10f);
        getUpSitStandDropMin = Mathf.Clamp(GetFloat("-poseGetUpSitStandDrop", 0.15f), 0f, 2f);
        getUpSitStandDropMax = Mathf.Clamp(GetFloat("-poseGetUpSitStandDropMax", 0.50f), 0f, 2f);
        // Hip-channel burst logger window, s (diagnostic); off when To <= From (default).
        hipDbgFrom = GetFloat("-poseHipDbgFrom", 0f);
        hipDbgTo = GetFloat("-poseHipDbgTo", 0f);
        // Get-up hip owner: 1 = the leg drive owns the hips and the pelvis law's hip write is
        // suppressed; default 0 = both writers stay active.
        getUpHipOwner = GetFloat("-poseGetUpHipOwner", 0f);
        // Sign-aware hip arbitration (see the field block): 1 = anti-phase refusals across the ladder,
        // 2 = Rise stage only, 0 = off. The band is the deadband under which a signal counts as absent.
        getUpHipArb = GetFloat("-poseGetUpHipArb", 0f);
        getUpHipArbBand = Mathf.Clamp(GetFloat("-poseGetUpHipArbBand", 0.1f), 0f, 1f);
        getUpSitStandArbBand = Mathf.Clamp(GetFloat("-poseGetUpSitStandArbBand", -1f), -1f, 1f);
        // Sit heel-pull joint deltas, deg (default 0 = off); a nonzero knee delta enables the pull.
        getUpSitPullKnee = Mathf.Clamp(GetFloat("-poseGetUpSitPullKnee", 0f), 0f, 160f);
        getUpSitPullHip = Mathf.Clamp(GetFloat("-poseGetUpSitPullHip", 0f), 0f, 160f);
        getUpSitPullAnkle = Mathf.Clamp(GetFloat("-poseGetUpSitPullAnkle", 0f), 0f, 90f);
        getUpSitPullSec = Mathf.Clamp(GetFloat("-poseGetUpSitPullSec", 0.7f), 0.05f, 3f);
        getUpSitPullOn = getUpSitPullKnee > 0.01f;
        // Sit re-seat family (default 0 = off).
        getUpSitSeatMode = Mathf.Clamp(Mathf.Round(GetFloat("-poseGetUpSitSeat", 0f)), 0f, 4f);
        getUpSitSeatPress = Mathf.Clamp(GetFloat("-poseGetUpSitSeatPress", 0.9f), 0f, 1f);
        getUpSitSeatPressMin = Mathf.Clamp(GetFloat("-poseGetUpSitSeatPressMin", 0.10f), 0.005f, 1f);
        getUpSitSeatPressMax = Mathf.Clamp(GetFloat("-poseGetUpSitSeatPressMax", 1.2f), 0.1f, 5f);
        getUpSitSeatKeep = Mathf.Clamp(GetFloat("-poseGetUpSitSeatKeep", 0.05f), 0.005f, 2f);
        getUpSitSeatGrace = Mathf.Clamp(GetFloat("-poseGetUpSitSeatGrace", 0.15f), 0f, 1f);
        getUpSitSeatHand = Mathf.Clamp(GetFloat("-poseGetUpSitSeatHand", 0.8f), 0f, 3f);
        getUpSitSeatFrozenMax = Mathf.Clamp(GetFloat("-poseGetUpSitSeatFrozenMax", 2.5f), 0.2f, 8f);
        getUpSitDragGap = Mathf.Clamp(GetFloat("-poseGetUpSitDragGap", 0f), 0f, 2f);
        getUpSitDragUnder = Mathf.Clamp(GetFloat("-poseGetUpSitDragUnder", 0f), 0f, 2f);
        getUpSitDragTip = Mathf.Clamp(GetFloat("-poseGetUpSitDragTip", 0f), 0f, 120f);
        getUpSitArcSec = Mathf.Clamp(GetFloat("-poseGetUpSitArc", 0f), 0f, 60f);
        getUpSitArcTorso = Mathf.Clamp(GetFloat("-poseGetUpSitArcTorso", 40f), 0f, 90f);
        getUpSitArcRate = Mathf.Clamp(GetFloat("-poseGetUpSitArcRate", 40f), 0f, 400f);
        getUpSitArcTieExt = Mathf.Clamp(Mathf.Round(GetFloat("-poseGetUpSitArcTieExt", 0f)), 0f, 2f);
        getUpSitRockSec = Mathf.Clamp(GetFloat("-poseGetUpSitRock", 0f), 0f, 30f);
        getUpSitRockGate = Mathf.Clamp(GetFloat("-poseGetUpSitRockGate", 0f), 0f, 1f);
        getUpSitExtOver = Mathf.Clamp(GetFloat("-poseGetUpSitExtOver", 0f), 0f, 2f);
        getUpSitReseat = Mathf.Clamp(Mathf.Round(GetFloat("-poseGetUpSitReseat", 0f)), 0f, 1f);
        getUpSitReseatGap = Mathf.Clamp(GetFloat("-poseGetUpSitReseatGap", 0.18f), 0f, 1.5f);
        getUpSitReseatKnee = Mathf.Clamp(GetFloat("-poseGetUpSitReseatKnee", 70f), 0f, 160f);
        getUpSitReseatHip = Mathf.Clamp(GetFloat("-poseGetUpSitReseatHip", 0f), -90f, 90f);
        getUpSitReseatAnkle = Mathf.Clamp(GetFloat("-poseGetUpSitReseatAnkle", 0f), -90f, 90f);
        getUpSitReseatSec = Mathf.Clamp(GetFloat("-poseGetUpSitReseatSec", 0.60f), 0.05f, 3f);
        getUpSitReseatMax = Mathf.Clamp(GetFloat("-poseGetUpSitReseatMax", 4.0f), 0.2f, 20f);
        getUpSitReseatLoad = Mathf.Clamp(GetFloat("-poseGetUpSitReseatLoad", 0.05f), 0f, 2f);
        getUpSitReseatLoadSec = Mathf.Clamp(GetFloat("-poseGetUpSitReseatLoadSec", 0.20f), 0f, 3f);
        getUpSitReseatOn = getUpSitReseat > 0.5f;
        // Discrete step re-seat family (default 0 = off).
        getUpSitStep = Mathf.Clamp(Mathf.Round(GetFloat("-poseGetUpSitStep", 0f)), 0f, 1f);
        getUpSitStepOn = getUpSitStep > 0.5f;
        getUpSitStepHip = Mathf.Clamp(GetFloat("-poseGetUpSitStepHip", 60f), 0f, 160f);
        getUpSitStepKnee = Mathf.Clamp(GetFloat("-poseGetUpSitStepKnee", 160f), 0f, 200f);
        getUpSitStepAnkle = Mathf.Clamp(GetFloat("-poseGetUpSitStepAnkle", 50f), 0f, 90f);
        getUpSitStepSec = Mathf.Clamp(GetFloat("-poseGetUpSitStepSec", 0.55f), 0.05f, 3f);
        getUpSitStepHold = Mathf.Clamp(GetFloat("-poseGetUpSitStepHold", 0.30f), 0f, 3f);
        getUpSitStepPlant = Mathf.Clamp(GetFloat("-poseGetUpSitStepPlant", 0f), 0f, 1f);
        getUpSitStepGap = Mathf.Clamp(GetFloat("-poseGetUpSitStepGap", 0.15f), 0f, 2f);
        getUpSitStepLoad = Mathf.Clamp(GetFloat("-poseGetUpSitStepLoad", 0.05f), 0.005f, 2f);
        getUpSitStepLoadSec = Mathf.Clamp(GetFloat("-poseGetUpSitStepLoadSec", 0.20f), 0f, 3f);
        getUpSitStepMax = Mathf.Clamp(GetFloat("-poseGetUpSitStepMax", 6.0f), 0.5f, 30f);
        getUpSitStepRounds = Mathf.Clamp(GetFloat("-poseGetUpSitStepRounds", 1f), 1f, 6f);
        getUpSitUnder = Mathf.Clamp(GetFloat("-poseGetUpSitUnder", 0f), 0f, 2f);
        // Ext sink arrest family (default 0 = off).
        getUpSitExtArrest = Mathf.Clamp(Mathf.Round(GetFloat("-poseGetUpSitExtArrest", 0f)), 0f, 1f);
        getUpSitExtArrestOn = getUpSitExtArrest > 0.5f;
        getUpSitExtArrestRate = Mathf.Clamp(GetFloat("-poseGetUpSitExtArrestRate", 40f), 1f, 500f);
        getUpSitExtArrestDrop = Mathf.Clamp(GetFloat("-poseGetUpSitExtArrestDrop", 0.10f), 0.005f, 2f);
        getUpSitExtArrestMax = Mathf.Clamp(GetFloat("-poseGetUpSitExtArrestMax", 2.5f), 0f, 10f);
        // Post-step weight transfer family (default 0 = off; the doses are command-line flags).
        getUpSitShift = Mathf.Clamp(Mathf.Round(GetFloat("-poseGetUpSitShift", 0f)), 0f, 1f);
        getUpSitShiftOn = getUpSitShift > 0.5f;
        getUpSitShiftHip = Mathf.Clamp(GetFloat("-poseGetUpSitShiftHip", 25f), 0f, 120f);
        getUpSitShiftKnee = Mathf.Clamp(GetFloat("-poseGetUpSitShiftKnee", 45f), 0f, 200f);
        getUpSitShiftAnkle = Mathf.Clamp(GetFloat("-poseGetUpSitShiftAnkle", 15f), 0f, 90f);
        getUpSitShiftLean = Mathf.Clamp(GetFloat("-poseGetUpSitShiftLean", 0f), -80f, 80f);
        getUpSitDrawLean = Mathf.Clamp(GetFloat("-poseGetUpSitDrawLean", 0f), 0f, 80f);
        getUpSitDrawSeat = Mathf.Clamp(GetFloat("-poseGetUpSitDrawSeat", 40f), 0f, 120f);
        getUpSitDrawPlant = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlant", 0f), 0f, 3f);
        getUpSitDrawPlantKnee = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlantKnee", 45f), 0f, 200f);
        getUpSitDrawPlantHip = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlantHip", -25f), -120f, 120f);
        getUpSitDrawPlantAnkle = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlantAnkle", -12f), -90f, 90f);
        getUpSitDrawPlantSec = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlantSec", 1.0f), 0.05f, 8f);
        getUpSitDrawPlantMax = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlantMax", 2.0f), 0.1f, 8f);
        getUpSitDrawPlantRe = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlantRe", 0f), 0f, 1f);
        getUpSitDrawPlantEps = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlantEps", 3f), 1f, 8f);
        getUpSitShiftSec = Mathf.Clamp(GetFloat("-poseGetUpSitShiftSec", 2f), 0.1f, 10f);
        getUpSitShiftRamp = Mathf.Clamp(GetFloat("-poseGetUpSitShiftRamp", 0.9f), 0.05f, 5f);
        getUpSitShiftLoad = Mathf.Clamp(GetFloat("-poseGetUpSitShiftLoad", 0.15f), 0.005f, 2f);
        getUpSitShiftLoadSec = Mathf.Clamp(GetFloat("-poseGetUpSitShiftLoadSec", 0.25f), 0f, 3f);
        getUpSitShiftMax = Mathf.Clamp(GetFloat("-poseGetUpSitShiftMax", 4f), 0.5f, 15f);
        // Plant stage family (default 0 = off; the reach doses are command-line flags).
        getUpSitPlant = Mathf.Clamp(Mathf.Round(GetFloat("-poseGetUpSitPlant", 0f)), 0f, 1f);
        getUpSitPlantOn = getUpSitPlant > 0.5f;
        getUpSitPlantKnee = Mathf.Clamp(GetFloat("-poseGetUpSitPlantKnee", 110f), 0f, 200f);
        getUpSitPlantHip = Mathf.Clamp(GetFloat("-poseGetUpSitPlantHip", 25f), 0f, 120f);
        getUpSitPlantAnkle = Mathf.Clamp(GetFloat("-poseGetUpSitPlantAnkle", 25f), 0f, 90f);
        getUpSitPlantSec = Mathf.Clamp(GetFloat("-poseGetUpSitPlantSec", 1.2f), 0.1f, 8f);
        getUpSitPlantFz = Mathf.Clamp(GetFloat("-poseGetUpSitPlantFz", 0.05f), 0.005f, 2f);
        getUpSitPlantFzSec = Mathf.Clamp(GetFloat("-poseGetUpSitPlantFzSec", 0.10f), 0f, 3f);
        getUpSitPlantMax = Mathf.Clamp(GetFloat("-poseGetUpSitPlantMax", 2.0f), 0.5f, 15f);
        // Fall guard, default 0 = off: TickFallGuard never runs and ArmDrive's fallGuardReach stays 0.
        // Hold (s) is bounded below by the ladder's step (one frame is not a state) and above by the
        // rig's longest fall; reach (m) is bounded by the arm's chord.
        fallGuardEnabled = Mathf.RoundToInt(GetFloat("-poseFallGuard", 0f)) != 0;
        fallGuardHoldSeconds = Mathf.Clamp(GetFloat("-poseFallGuardHold", fallGuardHoldSeconds),
            0.05f, 1.20f);
        fallGuardReachMeters = Mathf.Clamp(GetFloat("-poseFallGuardReach", fallGuardReachMeters),
            0f, 0.60f);
        // Get-up plant reach, m (see the field); bounded by the arm's chord like the fall guard's.
        getUpPlantReachMeters = Mathf.Clamp(GetFloat("-poseGetUpReach", getUpPlantReachMeters),
            0f, 0.60f);
    }

    // ─── arena genome entry ───
    // The sweep arena sets its genes here, not by reflection; clamps match ReadSwitches. `enable`
    // sets active (drive) and walkEnabled (gait). New genes are appended so each keeps its index.
    public const int ArenaGeneCount = 16;

    public void ApplyArenaGenome(float[] g, bool enable)
    {
        if (g == null || g.Length < ArenaGeneCount) return;
        active = enable;
        walkEnabled = enable;
        swingDuration = Mathf.Max(0.05f, g[0]);
        stanceDuration = Mathf.Max(0.05f, g[1]);
        swingLiftMeters = Mathf.Max(0f, g[2]);
        swingLandShare = Mathf.Clamp(g[3], 0.3f, 0.98f);
        heightGainDegPerMeter = g[4];
        heightDampDegPerMeterPerSecond = g[5];
        placementMarginMeters = g[6];
        comLeadSwapMeters = Mathf.Max(0.01f, g[7]);
        // No floor here: ReadSwitches passes -poseStepMin through bare and the gene's band includes
        // negative values. ArenaGenome.Clamp bounds the gene; this line only assigns.
        stepMinMeters = g[8];
        stepMaxMeters = Mathf.Max(stepMinMeters + 0.01f, g[9]);
        armSwingDeg = g[10];
        ankleBalanceP = g[11];
        ankleBalanceD = g[12];
        // Stance knee keys: clamped to the switches' 4..26 deg band (wider than the genome box), so a
        // genome can never leave what the switches accept.
        stanceFoldKneeDeg = Mathf.Clamp(g[13], 4f, 26f);
        stancePlaceKneeDeg = Mathf.Clamp(g[14], 4f, 26f);
        stanceLoadKneeDeg = Mathf.Clamp(g[15], 4f, 26f);
    }

    // ─── played human's kickstart ───
    // An editor/Play human (GameProcess) has no command line: this call turns the pose drive on with
    // the reference walk values (ReadSwitches' clamps); without it the man falls on the level-1 path.
    public void ApplyReferenceLine(float[] v)
    {
        if (v == null || v.Length < 17) return;
        active = true;
        walkEnabled = true;
        swingDuration = Mathf.Max(0.05f, v[0]);
        stanceDuration = Mathf.Max(0.05f, v[1]);
        swingLiftMeters = Mathf.Max(0f, v[2]);
        swingLandShare = Mathf.Clamp(v[3], 0.3f, 0.98f);
        heightGainDegPerMeter = v[4];
        heightDampDegPerMeterPerSecond = v[5];
        bobAmpMeters = Mathf.Clamp(v[6], 0f, 0.06f);
        placementMarginMeters = v[7];
        comLeadSwapMeters = Mathf.Max(0.01f, v[8]);
        stepMinMeters = v[9];
        stepMaxMeters = Mathf.Max(stepMinMeters + 0.01f, v[10]);
        armSwingDeg = v[11];
        ankleBalanceP = v[12];
        ankleBalanceD = v[13];
        poseTailHoldSeconds = Mathf.Clamp(v[14], 0f, 0.10f);
        swingAnkleDeg = Mathf.Clamp(v[15], -20f, 20f);
        swingAnkleRampSeconds = Mathf.Max(0f, v[16]);
        // Optional v[17..19] (v.Length >= 20): push-off delivery P, descent-arithmetic gate, neck P.
        // A 17-value line leaves these fields at their inert defaults; NaN neckP keeps the neck's gain.
        if (v.Length >= 20)
        {
            pushDeliverP = Mathf.Clamp(v[17], 1f, 20f);
            placeKneeLive = Mathf.RoundToInt(v[18]) != 0;
            if (balance != null && !float.IsNaN(v[19]))
                balance.neckPGain = v[19];
        }
    }
    // ─── arena walk configuration entry ───
    // Sibling of ApplyArenaGenome: sets the three walk values no genome gene carries, with
    // ReadSwitches' clamps. Called only from ArenaRunner.SpawnBody; trial humans read the switches.
    public void ApplyArenaWalkConfig(float ankleCarryDeg, float ankleRampSeconds,
                                     float tailHoldSeconds)
    {
        swingAnkleDeg = Mathf.Clamp(ankleCarryDeg, -20f, 20f);
        swingAnkleRampSeconds = Mathf.Max(0f, ankleRampSeconds);
        poseTailHoldSeconds = Mathf.Clamp(tailHoldSeconds, 0f, 0.10f);
    }
}
