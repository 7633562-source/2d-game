using UnityEngine;

// WORLD-1 (req-285): the world's own clock, driven by the physics step counter.
// Owned by WorldBrainHost, not static, so two hosts never share a step.
public sealed class WorldClock
{
    public int Step;
    public float Dt = 0.005f;
    public int Stride = 20;

    // Fixed once by the host on its first FixedUpdate (Time.fixedDeltaTime).
    public void Configure(float dt)
    {
        Dt = dt > 0f ? dt : 0.005f;
        Stride = Mathf.Max(1, Mathf.RoundToInt(0.1f / Dt));
    }

    public float Now { get { return (float)(Step * (double)Dt); } }

    public bool Due(int offset)
    {
        int m = (Step + offset) % Stride;
        return m == 0;
    }
}
