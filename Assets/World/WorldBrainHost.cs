using UnityEngine;

// WORLD-1 (req-285): the one MonoBehaviour of the world layer. FixedUpdate only,
// execution order -60 so it reads the state of the previous physics step before
// any drive or controller runs. It is an observer: it never writes a body.
[DefaultExecutionOrder(-60)]
public sealed class WorldBrainHost : MonoBehaviour
{
    public WorldClock clock = new WorldClock();
    public WorldSoundBus bus = new WorldSoundBus();
    public HeroIntent hero = new HeroIntent();
    public WorldRecorder rec;
    public WorldStats stats = new WorldStats();
    public int BrainCount;

    WorldBrain[] brains;
    char[] actorOf;
    int[] idOf;
    const int HeroSourceId = 9001;

    bool built;
    bool started;
    bool bench;
    int logLevel;

    WorldSnapshot snap;
    Rigidbody2D heroBody;
    Bird[] crows;
    Rigidbody2D[] crowBodies;
    Dog dog;
    Rigidbody2D dogBody;
    DogStanceController dogStance;

    Vector2 heroPrev;
    bool hasPrev;
    int heroPrevPosture = -1;
    readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

    public void Configure(int seed, int log, bool benchOn, string folder, string label,
        Rigidbody2D heroRef, BalanceController balance, StepPhaseDriver step, MotionIntent motion,
        Dog dogRef, Bird[] crowsRef, Rigidbody2D[] crowBodiesRef)
    {
        if (built) return;
        logLevel = log;
        bench = benchOn;
        heroBody = heroRef;
        crows = crowsRef;
        crowBodies = crowBodiesRef;
        dog = dogRef;
        if (dog != null)
        {
            dogStance = dog.stance;
            Transform pelvis = dog.transform.Find("Pelvis");
            dogBody = pelvis != null ? pelvis.GetComponent<Rigidbody2D>() : dog.GetComponent<Rigidbody2D>();
        }

        rec = new WorldRecorder();
        rec.Log = logLevel;
        rec.Label = label;
        rec.Seed = seed;
        rec.Visual = !Application.isBatchMode;
        rec.SetFolder(folder);
        stats = rec.Stats;

        hero.Bind(heroRef, balance, step, motion);
        snap.AllocCrows(crows != null ? crows.Length : 0);

        int n = bench ? 22 : 0;   // the bench asks for 22 brains at the real offsets
        brains = new WorldBrain[n];
        actorOf = new char[n];
        idOf = new int[n];
        BrainCount = n;
        stats.Init(n);
        int k = 0;
        while (bench && k < 22)
        {
            int off = k == 0 ? 3 : (k == 1 ? 13 : (k - 2) % 20);
            brains[k] = new EchoBrain(WorldSeed.Derive(seed, WorldStream.World, k), off, k);
            actorOf[k] = 'E';
            idOf[k] = k;
            k++;
        }
        rec.Begin();
        built = true;
        Debug.Log("WORLD host built brains=" + n + " bench=" + (bench ? 1 : 0) + " log=" + logLevel
                  + " crows=" + (crows != null ? crows.Length : 0) + " dog=" + (dog != null ? 1 : 0));
    }

    void FixedUpdate()
    {
        if (!built) return;
        if (!started)
        {
            started = true;
            clock.Configure(Time.fixedDeltaTime);
            stats.T0 = (float)Time.fixedTimeAsDouble;
            if (bench) Debug.Log("WORLD bench start stride=" + clock.Stride);
        }

        clock.Step++;
        int stepNo = clock.Step;
        float t = clock.Now;

        FillSnapshot();
        hero.Update(clock, bus, HeroSourceId);
        snap.hero = hero.state;
        if (hero.callEdge && rec != null)
        {
            rec.Event(stepNo, t, 'H', 0, "call", new Vector2(snap.hero.x, snap.hero.y), -1, "squat");
            stats.EventCount++;
        }
        bus.Prune(stepNo, clock.Stride);

        long allocBefore = bench ? System.GC.GetAllocatedBytesForCurrentThread() : 0L;
        double us = 0.0;

        for (int i = 0; i < BrainCount; i++)
        {
            WorldBrain b = brains[i];
            if (!clock.Due(b.Offset)) continue;
            if (bench) watch.Restart();
            int prevAct = b.Act;
            b.Tick(ref snap, bus, clock);
            if (bench)
            {
                watch.Stop();
                us += watch.Elapsed.TotalMilliseconds * 1000.0;
                stats.BenchTicks++;
            }
            stats.BrainTicks++;
            if (i < stats.TickCounts.Length) stats.TickCounts[i]++;
            if (i * 8 + b.Act < stats.ActCounts.Length && b.Act >= 0 && b.Act < 8)
                stats.ActCounts[i * 8 + b.Act]++;
            if (b.GaitName != b.AppliedName) stats.GaitGapTicks++;
            if (rec != null && (rec.Log >= 2 || b.Act != prevAct))
            {
                Vector2 bp = new Vector2(snap.hero.x, snap.hero.y);
                rec.BrainRow(b, actorOf[i], idOf[i], stepNo, t, bp, 0f, (int)snap.hero.posture);
            }
        }
        stats.TickUs += us;

        if (bench && t >= 1f)
        {
            long delta = System.GC.GetAllocatedBytesForCurrentThread() - allocBefore;
            if (delta > 0)
            {
                stats.AllocSteps++;
                if (delta > stats.AllocMax) stats.AllocMax = delta;
            }
        }

        stats.Steps = stepNo;
        stats.HeroSteps++;
        int pi = (int)snap.hero.posture;
        if (pi >= 0 && pi < 4) stats.PostureCount[pi]++;
        if (rec != null && (rec.Log >= 2 || pi != heroPrevPosture))
        {
            HeroPosture prev = heroPrevPosture < 0 ? snap.hero.posture : (HeroPosture)heroPrevPosture;
            rec.HeroRow(stepNo, t, ref snap.hero, prev, hero.heardMask);
        }
        heroPrevPosture = pi;
        RailSample(t);
    }

    // One read-only picture per step, before any brain ticks.
    void FillSnapshot()
    {
        snap.hasDog = dogBody != null;
        if (dogBody != null)
        {
            snap.dogPos = dogBody.position;
            snap.dogVel = dogBody.linearVelocity;
            snap.dogState = dogStance != null ? (int)dogStance.currentState : -1;
            snap.dogWalking = dogStance != null && dogStance.currentState == DogStanceController.StanceState.Walk;
        }
        snap.hasCat = false;
        snap.gust = 0f;
        if (crows != null && crowBodies != null)
        {
            int n = Mathf.Min(crows.Length, crowBodies.Length);
            for (int i = 0; i < n && i < snap.crowCount; i++)
            {
                Rigidbody2D rb = crowBodies[i];
                if (rb != null)
                {
                    snap.crowPos[i] = rb.position;
                    snap.crowVel[i] = rb.linearVelocity;
                }
                BirdMode m = crows[i] != null && crows[i].controller != null ? crows[i].controller.mode : BirdMode.Stand;
                snap.crowMode[i] = m;
                snap.crowGround[i] = m == BirdMode.Stand || m == BirdMode.Walk || m == BirdMode.Peck;
                snap.crowTree[i] = m == BirdMode.Sit;
            }
        }
    }

    Vector2 BodyPosOf(int i)
    {
        return new Vector2(snap.hero.x, snap.hero.y);
    }

    // Rails audit: the numbers the prototype's section 1 table prints, read from the
    // bodies this host already reads. No extra lookup, no write.
    void RailSample(float t)
    {
        if (heroBody == null) return;
        Vector2 p = heroBody.position;
        Vector2 v = heroBody.linearVelocity;
        float sp = v.magnitude;
        if (sp > stats.HeroMaxSpeed) stats.HeroMaxSpeed = sp;
        if (Mathf.Abs(v.x) > stats.HeroMaxVx) stats.HeroMaxVx = Mathf.Abs(v.x);
        if (hasPrev)
        {
            float jump = (p - heroPrev).magnitude;
            if (jump > stats.HeroMaxJump) stats.HeroMaxJump = jump;
        }
        heroPrev = p;
        hasPrev = true;
    }
}
