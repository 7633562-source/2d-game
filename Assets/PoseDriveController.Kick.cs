using System.Globalization;
using UnityEngine;

// PoseDriveController: the kick's stand-side terms (support shift, wind-up, plant, head, flight).
public partial class PoseDriveController
{
    // ─── support-side shift ───
    // While the kick's shift level is up the pelvis target leans toward the CoM like the level-1 stand:
    // pelvisTargetTilt -= kickShiftGainDeg * KickShiftSignal * kickShiftLevel. Signed, deg; 0 = off.
    private float kickShiftGainDeg;
    // ─── wind-up ───
    // Static offset, m, added to the ankle loop's CoM while the kick level is up; + = CoM read further
    // ahead = stronger backward command. 0 (default) = off: at ~140/m one cm is already full scale.
    private float kickWindComMeters;
    // ─── wind-up gain ───
    // In the inter-stroke window (kick level up, no stroke in flight) the ankle loop aims at the
    // standing foot's plate centre, P scaled by this gain (0..1; D by its root) to bound it. 0 = off.
    private float kickWindGain;
    // Live term state, written every step: gain x shift level while the window is up (else 0f and
    // neither field is read), and the loop's reference x, m, while the term is live.
    private float kickWindLevel;
    private float kickWindRefX;
    // ─── wind-up reference latch ───
    // A live reference is positive feedback (the burst drags the standing foot, the target follows).
    // 0 (default) = live; 1 = re-latched at each window's rising edge; 2 = latched once per episode.
    private float kickWindLatch;
    // Latch state: the latched point, whether the window was up last step (rising edge), and whether
    // the burst's own frame is held.
    private float kickWindRefLatched;
    private bool kickWindWindowUp;
    private bool kickWindFrameHeld;
    // ─── wind-up release hold ───
    // Seconds the term stays alive after the kick level reaches 0, so the reference returns to the hull
    // centre after the settle. Loaded while the request is up; 0 (default) = immediate hand-over.
    private float kickWindHold;
    private float kickWindHoldClock;
    // ─── wind-up reference point ───
    // The term's reference while the window is up: 0 (default) = the standing foot's plate centre;
    // 1 = the support hull's centre (state.supportCenterX), so the release's hand-over step is 0.
    private float kickWindRef;
    // ─── wind-up normalizer ───
    // Window level = clamp01(kickWindNorm / (perMetre * carry)), carry = |CoM - ref| latched at the
    // rising edge, so P = kickWindNorm * err / carry stays bounded at any error scale. 0 = off.
    private float kickWindNorm;
    // Normalizer state, written only while the window is up: the carry (m) latched at the rising edge
    // and the level it buys; the release hold reads both.
    private float kickWindCarry;
    private float kickWindNormLevel;
    private const float KickWindCarryFloor = 0.02f;
    private const float KickWindCarryCeil = 0.50f;
    // ─── wind-up level while a fire is held ───
    // Level used while a fire is due and held (balance.kickFireHeld) instead of the window's own.
    // -1 (default) = off; 0 = plain loop toward the hull centre; 1 = full authority on the term's ref.
    private float kickWindDue = -1f;
    // ─── wind-up at the stroke's head ───
    // While balance.kickHeadActive (the lift's unload window) the loop targets the standing foot at
    // this level, braking instead of the hull centre's forward push. -1 (default) = off; 0..1 = level.
    private float kickWindHead = -1f;
    // While a stroke is in flight the support knee uses the walk's stance hold
    // (PoseLibrary.StanceHoldDeliverP, 7x torque per error). 1 (default) = on, 0 = off.
    private float kickSupportHold = 1f;
    // Trunk lean, deg, on the same signal and level: the trunk carries ~40% of the mass, the pelvis
    // little. + leans the chest back as the CoM goes forward (forward = minus on the target). 0 = off.
    private float kickShiftTorsoDeg;
    // Ankle loop CoM target offset, m, on the same signal (the lean layer's leanComShift pattern,
    // its own sign). 0 (default) = off.
    private float kickShiftComMeters;
    // ─── support plant ───
    // Holds the support hip within the hold band of the angle the strike latched on its fire step
    // (balance.kickSupportHipDeg) via MixHipHold for the whole episode. 1 (default) = on, 0 = off.
    private float kickPlantHold = 1f;
    // ─── support plant band ───
    // Hold band, deg, for the support hip while the plant is on, so the hold can act between strokes.
    // -1 (default) = balance.hipHoldBand; 0 = hold retired (MixHipHold's band guard); >= 0.5 = band.
    private float kickPlantBand = -1f;
    // ─── wind-up parking margin ───
    // Moves the window's reference back, m: err = comX - (ref - margin), so the stand parks further
    // from the foot's front edge (+ err = extensor brake). Window only, [0, 0.10] m; 0 (default) = off.
    private float kickWindMargin;

    // ─── head push cap ───
    // While balance.kickHeadActive the hull centre sits ahead of the CoM and the loop pushes forward;
    // cap that flexor side only: command = Max(command, -kickHeadPush). 0 = no push; -1 = off.
    private float kickHeadPush = -1f;

    // ─── wind-up during the stroke's flight ───
    // In flight past the head the loop targets the term's reference at this level: on landing the
    // hull centre jumps ahead of the CoM and the plain law would push forward. -1 = off; 0..1 = level.
    private float kickWindFlight = -1f;

    // The level-1 stand's balance signal (BalanceController.UpdateState): clamp(comProportionalGain *
    // offset/comOffsetReference + comDerivativeGain * rate/tiltSpeedReference, +-maxBalanceSignal),
    // offset = comX - support centre, rate = vestibular body angular velocity. Used while shifting.
    private float KickShiftSignal(float comX)
    {
        float centre = state != null ? state.supportCenterX : comX;
        float offsetN = (comX - centre) / Mathf.Max(0.01f, balance.comOffsetReference);
        float rate = vestibular != null ? vestibular.GetBodyAngularVelocity() : 0f;
        float rateN = rate / Mathf.Max(1f, balance.tiltSpeedReference);
        return Mathf.Clamp(balance.comProportionalGain * offsetN
            + balance.comDerivativeGain * rateN,
            -balance.maxBalanceSignal, balance.maxBalanceSignal);
    }

    // ─── shared joint drives ───
    // The wind-up term's reference point: kickWindRef 0 (default) = the standing foot's plate centre,
    // 1 = the support hull's centre (state.supportCenterX). Only the reference moves.
    private float KickWindRefPoint(Vector2 standFoot)
    {
        if (kickWindRef > 0.5f && state != null) return state.supportCenterX;
        return standFoot.x;
    }
}
