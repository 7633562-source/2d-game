using UnityEngine;

// WORLD-1 (req-285): the hero as the world sees him. Reads the pelvis body, the
// balance state, the step driver and the crouch target; writes nothing back.
// The one output is the squat call onto the world bus.
// Walk has hysteresis (req-352): enters on the walk or on speed, leaves only after
// the walk is off and the hero stays under 0.10 m/s for 0.30 s of world steps.
// The speed entry is dead for the first second (req-370): the spawn settle is not a walk.
public sealed class HeroIntent
{
    public HeroState state;
    public int heardMask;   // step 1: the hero posts a call, he hears nothing
    public bool callEdge;   // true on the one step a squat entry puts a call on the bus

    const float WalkInSpeed = 0.15f;    // enter Walk at this speed
    const float WalkOutSpeed = 0.10f;   // leave Walk only below this
    const float WalkHoldT = 0.30f;      // and only after this long
    const float SpawnSettleT = 1.0f;    // the speed entry is dead until this world time

    Rigidbody2D body;
    BalanceController balance;
    StepPhaseDriver step;
    MotionIntent motion;
    HeroPosture prev = HeroPosture.Stand;
    int awaySteps;          // world steps spent walk-off and slow

    public void Bind(Rigidbody2D heroBody, BalanceController bal, StepPhaseDriver driver, MotionIntent mi)
    {
        body = heroBody;
        balance = bal;
        step = driver;
        motion = mi;
        prev = HeroPosture.Stand;
        awaySteps = 0;
        state.posture = HeroPosture.Stand;
        state.crowFid = 1.8f;
    }

    public void Update(WorldClock c, WorldSoundBus bus, int sourceId)
    {
        Vector2 p = body != null ? body.position : Vector2.zero;
        float vx = body != null ? body.linearVelocity.x : 0f;
        state.x = p.x;
        state.y = p.y;
        state.vx = vx;

        HeroPosture now = Posture(c);
        state.posture = now;
        state.stillT = Mathf.Abs(vx) < 0.05f ? state.stillT + c.Dt : 0f;
        // design section 2.1: the crows' flight-initiation distance by posture
        state.crowFid = now == HeroPosture.Walk ? 3f : (now == HeroPosture.Squat ? 0.9f : 1.8f);

        if (now == HeroPosture.Squat && prev != HeroPosture.Squat && bus != null)
        {
            bus.Post(c.Step, p, SoundKind.Call, sourceId);   // squat rising edge = one call
            callEdge = true;
        }
        else
        {
            callEdge = false;
        }
        prev = now;
    }

    HeroPosture Posture(WorldClock c)
    {
        if (balance != null)
        {
            BalanceController.LocomotionState ls = balance.locomotionState;
            if (ls == BalanceController.LocomotionState.Falling
                || ls == BalanceController.LocomotionState.Fallen
                || ls == BalanceController.LocomotionState.GettingUp)
            {
                awaySteps = 0;
                return HeroPosture.Fallen;
            }
            int sq = balance.SquatStateCode;   // 0 Idle, 5 Done, else a squat is running
            if (sq != 0 && sq != 5)
            {
                awaySteps = 0;
                return HeroPosture.Squat;
            }
        }
        if (motion != null && motion.crouch > 0.5f)
        {
            awaySteps = 0;
            return HeroPosture.Squat;
        }

        bool walkOn = step != null && step.walkActive;
        float speed = Mathf.Abs(state.vx);
        if (prev == HeroPosture.Walk)
        {
            // out of Walk only when the walk is off and the speed stays low: one stutter
            // step or a single slow sample must not flip the posture
            if (!walkOn && speed < WalkOutSpeed)
            {
                awaySteps++;
                if (awaySteps * c.Dt >= WalkHoldT)
                {
                    awaySteps = 0;
                    return HeroPosture.Stand;
                }
            }
            else
            {
                awaySteps = 0;
            }
            return HeroPosture.Walk;
        }
        awaySteps = 0;
        if (walkOn) return HeroPosture.Walk;
        // the spawn settle reads |vx| >= 0.15 for a moment with no step: only a real
        // step may enter Walk in the first second of the world clock
        if (speed >= WalkInSpeed && c.Now >= SpawnSettleT) return HeroPosture.Walk;
        return HeroPosture.Stand;
    }
}
