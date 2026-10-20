using UnityEngine;

// ─── ONE FALL CRITERION FOR THE WHOLE PROJECT (C1, 18.09.2026) ───
//
// Instrumentation only: no controller reads anything in this file. It exists
// because three separate local implementations lied three times:
//
//   human  `fell` true at 8.415 s on a body that had already collapsed at
//          6.035 s (the automaton parked at Recovery while the torso lay down);
//   dog    `fell` false for 25 of 40 s while the dog lay on its side, because
//          the stance automaton stayed in Walk;
//   arena  `fallen` true in 2112 of 2112 logged rows, because the drop arm was
//          measured against the running MAXIMUM of the pelvis height, which in
//          the spawn pose is 0.186 m ABOVE the settled stance - so 0.064 m of
//          ordinary walk-onset sink latched the flag.
//
// The rule is deliberately blunt and has nothing to tune:
//
//   down  :=  (height < baseline - 0.25 m)  OR  (|tilt| > 60 deg)
//   fired :=  down held for >= 0.15 s, and the reported time is the FIRST frame
//             of that run (the onset), not the end of the settle.
//
// The baseline is an INPUT and must be a SETTLED stand height measured while
// the body is upright - never the spawn pose, never a running maximum. Use the
// Baseline sampler below, or hand in a value measured the same way.
public static class FallCriterion
{
    // The body is down when the root has sunk this far below its own settled
    // stand height ...
    public const float DropMeters = 0.25f;
    // ... or when the body tilt passes this. Both arms are physical, and either
    // one alone is enough: a body that lies down at constant height and a body
    // that sinks without tilting have both been observed.
    public const float TiltDegrees = 60f;
    // One frame of noise is not a fall; a dive that recovers inside this window
    // is not a fall either (p_arms4: |tilt| crossed 45 deg for one frame at
    // 6.115 s and came back to 26.6 deg with the pelvis still up).
    public const float SustainSeconds = 0.15f;
    // A body leaning past this is no longer evidence of a standing pose, so the
    // baseline sampler refuses to read its height as the stand height.
    public const float UprightTiltDegrees = 45f;

    // Per-body latch. The caller owns one and passes it by ref; nothing here is
    // mutable static, so two bodies in one trial can never poison each other.
    public struct Latch
    {
        public bool fired;
        public float fireTime;   // first frame of the sustained run, -1 while up
        public float downAge;    // length of the current down run, seconds
        public float downSince;  // time of the first frame of the current run

        public void Reset()
        {
            fired = false;
            fireTime = -1f;
            downAge = 0f;
            downSince = 0f;
        }
    }

    // One frame. Returns true while the body is down on this frame (not latched),
    // and sets latch.fired / latch.fireTime on the first sustained run.
    public static bool Step(ref Latch latch, float baselineSupportHeight,
                            float currentRootHeight, float tiltDeg,
                            float dt, float time)
    {
        bool down = Down(baselineSupportHeight, currentRootHeight, tiltDeg);
        if (down)
        {
            if (latch.downAge <= 0f) latch.downSince = time;
            latch.downAge += dt;
            if (!latch.fired && latch.downAge >= SustainSeconds)
            {
                latch.fired = true;
                latch.fireTime = latch.downSince;
            }
        }
        else
        {
            latch.downAge = 0f;
        }
        return down;
    }

    // The rule on one frame, no state: the physical predicate alone.
    public static bool Down(float baselineSupportHeight, float currentRootHeight,
                            float tiltDeg)
    {
        return currentRootHeight < baselineSupportHeight - DropMeters
            || Mathf.Abs(tiltDeg) > TiltDegrees;
    }

    // Settled stand height, measured from the stream. Feed it only frames that
    // are evidence of standing (the sampler drops anything leaning past
    // UprightTiltDegrees), and freeze it before the event being judged - a body
    // that has already started to sink must not lower its own bar. The value is
    // the MEAN of the accepted frames, so one noisy sample cannot set it.
    public struct Baseline
    {
        private double sum;
        private int count;

        public float height;
        public bool valid;
        public bool frozen;

        public void Feed(float currentRootHeight, float tiltDeg)
        {
            if (frozen) return;
            if (Mathf.Abs(tiltDeg) > UprightTiltDegrees) return;
            sum += currentRootHeight;
            count++;
            height = (float)(sum / count);
            valid = true;
        }

        public void Freeze()
        {
            frozen = true;
        }

        // Falls back to a caller-supplied height when no upright frame was ever
        // seen (for example a trial that starts from a heap). The caller must
        // say which value it passed; nothing here invents one.
        public float Value(float fallback)
        {
            return valid ? height : fallback;
        }
    }
}
