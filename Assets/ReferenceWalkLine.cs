// THE PLAYED HUMAN'S REFERENCE WALK (25.09).
//
// WHY: in the editor / Play Mode (GameProcess) the human has no command line, so
// PoseDriveController.ReadSwitches left the pose drive OFF and the played man fell
// on the level-1 path while the trial's stand walked fine. This class hands the
// accepted line's own values to the fresh human through the controller's public
// call - the same discipline as the arena's ApplyArenaGenome.
//
// WHERE THE VALUES COME FROM: `F:/hermes-data/walk-landed-line.txt` when it exists
// (the single source every film script reads - the accepted line, re-based 25.09
// to the c79 set: swingT 0.600000 / stanceT 0.209341 plus -posePushDeliverP 12 /
// -posePlaceKneeLive 1 / -neckP 2.5), else the BUILT-IN copy below. The WebGL
// player cannot read F:, so the built-in copy is what the demo page walks.
// 28.09 (task "Demo ship: walkable build"): the built-in copy is the demo's own
// line - the evolution's best genome g3c04 (the demo seed), the exact values the
// v2 climb runs carried (Trials/Runs/wrefdbg_g3c04_s2_v2w.json's own command
// line). The FILE keeps the films' accepted c79 set, untouched: the two serve
// different callers and must not be merged back.
// The order matches PoseDriveController.ApplyReferenceLine exactly.
using System.Globalization;
using System.IO;
using UnityEngine;

public static class ReferenceWalkLine
{
    private const string LineFile = "F:/hermes-data/walk-landed-line.txt";

    // The accepted line's own numbers, in ApplyReferenceLine's order:
    // swingT, stanceT, lift, landShare, heightGain, heightDamp, bobAmp,
    // placeMargin, leadSwap, stepMin, stepMax, armSwing, ankleP, ankleD,
    // tailHold, ankleCarry, ankleRamp - and then the three switches the accepted
    // line carries beyond the 17 (the c79 re-base): pushDeliverP, placeKneeLive,
    // neckP.
    public static readonly float[] Values =
    {
        0.647691f, 0.204027f, 0.068534f, 0.779368f, 458.829945f, 11.391295f,
        0.016721f, 0.016170f, 0.076010f, -0.266938f, 0.318932f, 6.840425f,
        0.538593f, 0.938273f, 0.012218f, -13.626857f, 0.103108f,
        11.871645f, 1f, 1.790733f
    };

    private static readonly string[] FlagNames =
    {
        "-poseSwingT", "-poseStanceT", "-poseLift", "-poseLandShare", "-poseHeightGain",
        "-poseHeightDamp", "-poseBobAmp", "-posePlaceMargin", "-poseLeadSwap",
        "-poseStepMin", "-poseStepMax", "-poseArmSwing", "-poseAnkleP", "-poseAnkleD",
        "-poseTailHold", "-poseAnkleCarry", "-poseAnkleRamp"
    };

    // The three switches (the c79 re-base): OPTIONAL in the file - an older line
    // predates them. Absent or unparsable = the inert value below, the same state
    // the shipped fields carry, so every existing trial row stays bit for bit.
    // NaN = "no token": the neck keeps its own gain (the balance's shipped value).
    private static readonly string[] SwitchNames =
    {
        "-posePushDeliverP", "-posePlaceKneeLive", "-neckP"
    };

    private static readonly float[] SwitchInert = { 1f, 0f, float.NaN };

    // One call per played human: pose drive ON with the accepted line's values.
    public static bool Apply(GameObject humanObject)
    {
        if (humanObject == null)
            return false;
        PoseDriveController pose = humanObject.GetComponent<PoseDriveController>();
        if (pose == null)
        {
            Debug.LogWarning("REFWALK: no PoseDriveController on " + humanObject.name);
            return false;
        }

        float[] values = Values;
        string source = "built-in";
        float[] fromFile;
        if (TryReadLineFile(out fromFile))
        {
            values = fromFile;
            source = "walk-landed-line.txt";
        }

        pose.ApplyReferenceLine(values);
        Debug.Log("REFWALK: the played human walks the accepted line (source " + source +
            ", swingT " + values[0].ToString("0.######", CultureInfo.InvariantCulture) +
            ", stanceT " + values[1].ToString("0.######", CultureInfo.InvariantCulture) +
            ", pushDeliverP " + values[17].ToString("0.######", CultureInfo.InvariantCulture) +
            ", placeKneeLive " + values[18].ToString("0.######", CultureInfo.InvariantCulture) +
            ", neckP " + values[19].ToString("0.######", CultureInfo.InvariantCulture) + ")");
        return true;
    }

    // "‑poseSwingT 0.600000 ‑poseStanceT ..." -> the 17 values in order, then the
    // three switches (inert when their token is absent). Any of the 17 missing or
    // unparsable = no file read: the built-in copy is the fallback.
    private static bool TryReadLineFile(out float[] values)
    {
        values = null;
        try
        {
            if (!File.Exists(LineFile))
                return false;
            string text = File.ReadAllText(LineFile);
            string[] tokens = text.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
            float[] parsed = new float[FlagNames.Length + SwitchNames.Length];
            for (int i = 0; i < FlagNames.Length; i++)
            {
                int at = System.Array.IndexOf(tokens, FlagNames[i]);
                if (at < 0 || at + 1 >= tokens.Length)
                    return false;
                if (!float.TryParse(tokens[at + 1], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out parsed[i]))
                    return false;
            }
            for (int i = 0; i < SwitchNames.Length; i++)
            {
                float value = SwitchInert[i];
                int at = System.Array.IndexOf(tokens, SwitchNames[i]);
                float read;
                if (at >= 0 && at + 1 < tokens.Length &&
                    float.TryParse(tokens[at + 1], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out read))
                    value = read;
                parsed[FlagNames.Length + i] = value;
            }
            values = parsed;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
