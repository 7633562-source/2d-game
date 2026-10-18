using UnityEngine;

// ARENA-1: the intent of the digit drawn on an arena human's head.
//
// Why a component and not a field: ArenaRunner calls
// `ArenaHumanAgent.AttachNumber(headObject, id, NumberHeight)` and only THEN adds
// the ArenaHumanAgent, so the number has to carry its own target from the drawing
// code to the agent's first measurement. The agent reads it back, measures the
// rendered glyph and corrects it once (ArenaHumanAgent.CalibrateNumber).
//
// Its own file on purpose: Unity does not add a nested class as a component.
public class ArenaNumberTarget : MonoBehaviour
{
    public int id;
    public float headHeightMeters;      // the head's own collider box, metres
    public float requestedHeightMeters; // what ArenaRunner.numberHeight asked for
    public float targetHeightMeters;    // the clamped size actually drawn
}
