using UnityEngine;

// WORLD-1 (req-285): one read-only picture of the world per physics step.
// Filled by WorldBrainHost before any brain ticks; brains never touch a body.
public enum HeroPosture : byte { Stand = 0, Walk = 1, Squat = 2, Fallen = 3 }

public struct HeroState
{
    public float x, y, vx;
    public HeroPosture posture;
    public float stillT;
    public float crowFid;
}

public struct WorldSnapshot
{
    public HeroState hero;

    public Vector2 dogPos, dogVel;
    public int dogState;
    public bool dogWalking;
    public bool hasDog;

    public Vector2 catPos, catVel;
    public int catPlace;
    public bool hasCat;

    public Vector2[] crowPos;
    public Vector2[] crowVel;
    public BirdMode[] crowMode;
    public bool[] crowGround;
    public bool[] crowTree;
    public int crowCount;

    public float gust;   // 0 until the wind step (port-plan section 5, step 8)

    public void AllocCrows(int n)
    {
        crowCount = n < 0 ? 0 : n;
        crowPos = new Vector2[crowCount];
        crowVel = new Vector2[crowCount];
        crowMode = new BirdMode[crowCount];
        crowGround = new bool[crowCount];
        crowTree = new bool[crowCount];
    }
}
