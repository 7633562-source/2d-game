using System.Globalization;
using UnityEngine;

// PoseDriveController: the stop's close - the shift's rear-leg hold (req-348).
// Every switch here is 0 = off; with a switch at 0 the landed code runs untouched.
public partial class PoseDriveController
{
    private float clCloseShiftRearHipDeg;    // -clCloseShiftRearHip: the shift's rear hip ceiling, deg behind (0 = off)
    private float clCloseStepOnUnloadS;      // -clCloseStepOnUnload: step this long after the rear sole lifts, s (0 = off)
    private float clCloseStepY0M;            // -clCloseStepY0: the step start's height ceiling over the front sole, m (0 = off)
    private float clCloseLiftHM;             // -clCloseLift: the close's lift height, m (0 = off, the constant stands)
    private float clCloseRearUpT;            // seconds the rear sole has been off the ground in this shift, s
    private float clCloseShiftRearDeg;       // the held rear hip angle, deg (latched on the unload tick)
    private bool clCloseShiftRearLatch;      // the hold's target is latched for this close
    private bool clCloseShiftRearLeft;       // the held (rear) leg is the left one

    // -clCloseLift <m> (0 = off): the height the close's step asks over its landing line. The off path
    // is the ClCloseLiftM constant itself, through this branch.
    private float ClCloseLiftH => clCloseLiftHM > 0f ? clCloseLiftHM : ClCloseLiftM;

    // In the close's shift the rear sole is free and the plain-stand branch owns both hips with the
    // stand's knee law, so the leg trails back to the hip limit (the kick). The hold keeps the rear hip
    // at the angle it had on the unload tick (capped at the switch's deg) with the sole level, and
    // leaves the support hip to the pelvis law alone, as the step branch does. True = the rear leg is
    // driven here; false = the caller runs today's code.
    private bool ClCloseShiftRearHold(float pelvisTilt, bool leftDown, bool rightDown,
                                      float kneeHold, bool kneeLp)
    {
        if (clCloseShiftRearHipDeg <= 0f || !clStanceCloseOn)
        {
            clCloseShiftRearLatch = false;
            return false;
        }
        // A phase left over from an earlier tick (a kick in between) is not a shift.
        if (clClosePhase != 1 || clCloseTickTime != Time.fixedTime)
        {
            clCloseShiftRearLatch = false;
            return false;
        }
        bool rearLeft = clCloseSwingLeft;
        if (rearLeft ? leftDown : rightDown)
        {
            clCloseShiftRearLatch = false;
            return false;
        }
        HingeJoint2D rearHip = rearLeft ? leftHipJoint : rightHipJoint;
        if (rearHip == null)
        {
            clCloseShiftRearLatch = false;
            return false;
        }
        if (!clCloseShiftRearLatch)
        {
            clCloseShiftRearDeg = Mathf.Min(rearHip.jointAngle, clCloseShiftRearHipDeg);
            clCloseShiftRearLatch = true;
            clCloseShiftRearLeft = rearLeft;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[stanceClose] rearhold t={0:F3} hip={1:F1}",
                Time.fixedTime, clCloseShiftRearDeg));
        }
        float hipDeg = clCloseShiftRearDeg;
        // Foot world angle = pelvisTilt - hip - knee - ankle: 0 is a level sole.
        DriveOneLeg(rearLeft, hipDeg, kneeHold, true, pelvisTilt - hipDeg - kneeHold, kneeLp: kneeLp);
        return true;
    }
}
