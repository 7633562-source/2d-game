using UnityEngine;

// req-260 round 2 (-birdLife, default off): the flock's own life clock.
//
// Why it exists. BirdFlockDrive.Bind sets wander.allowSelfFly = false, so a crow
// never lifts by itself: only the startle, the mob and keep-away do, and a Sit
// that ends re-rolls 12-22 s. The static -birdThreat mark is re-seen every frame,
// so the mob never ends and the pack hovers over the dirt in Attack.
//
// What this does, per crow and with its own System.Random:
//  - habituation to a static threat (salience decays 6-12 s, fresh again only if
//    the mark moves > 0.4 m or the crow closes > 1 m), so a static mark gives a
//    first mob then short re-check sorties;
//  - its own mob bout cap (3-6 s), then salience 0;
//  - its own take-off timer on the ground (4-8 s) and on a perch (3-6 s), a
//    leader lift with neighbours joining after 0.5-1.5 s (P 0.65), at most
//    MaxAir airborne crows from the clock;
//  - Attack only while more than 1 m above the last grounded y (no hover-sit).
//
// Draws happen on transitions only, never per tick, so the rng stream cannot
// depend on the frame count. The helper touches no Unity object: positions,
// modes and times arrive through the arguments.

public struct CrowAsk
{
    public bool takeoff;   // this crow lifts now (own timer or a join)
    public bool leader;    // it was the own timer, so neighbours may follow
}

public class CrowLifeClock
{
    public const int MaxAir = 2;      // crows the clock keeps airborne at once

    private readonly int n;
    private readonly System.Random[] rng;
    private readonly float[] tempo;
    private readonly float[] nextGo;    // absolute time of the next own take-off (-1 unset)
    private readonly float[] joinAt;    // absolute time a follower lifts (-1 none)
    private readonly float[] sal;       // threat salience 0..1
    private readonly Vector2[] salRef;  // mark position the salience was last reset at
    private readonly float[] tauHab;
    private readonly float[] tauRec;
    private readonly float[] reactAt;
    private readonly float[] mobStart;  // start of the current mob bout (-1 idle)
    private readonly float[] mobCap;
    private readonly float[] dirtY;     // last grounded body y (Attack guard)
    private readonly float[] lastCall;  // cached call time of the salience verdict
    private readonly bool[] lastVerdict;
    private readonly bool[] wasAir;     // airborne at the last Post
    private readonly float[] airFrom;   // time the current air bout began (-1 grounded)
    private readonly float[] airCap;    // length of that bout, drawn at the lift
    private readonly float[] landAt;    // time of the last touchdown (-1 never)
    private readonly float[] fleeFrom;  // lift time of the current flee-lane flight (-1 grounded)
    private readonly float[] fleeCap;   // its own length, drawn at the lift

    public CrowLifeClock(int count, int driveSeed)
    {
        n = count < 1 ? 1 : count;
        rng = new System.Random[n];
        tempo = new float[n];
        nextGo = new float[n];
        joinAt = new float[n];
        sal = new float[n];
        salRef = new Vector2[n];
        tauHab = new float[n];
        tauRec = new float[n];
        reactAt = new float[n];
        mobStart = new float[n];
        mobCap = new float[n];
        dirtY = new float[n];
        lastCall = new float[n];
        lastVerdict = new bool[n];
        wasAir = new bool[n];
        airFrom = new float[n];
        airCap = new float[n];
        landAt = new float[n];
        fleeFrom = new float[n];
        fleeCap = new float[n];
        for (int i = 0; i < n; i++)
        {
            // Own stream: hash of the drive seed and the member index. Never the
            // wander seed (seed + i*17) and never the drive rng (startle, pair
            // picks and PickResettleSite draw from it).
            unchecked
            {
                rng[i] = new System.Random(driveSeed * 7919 + i * 104729 + 0x4C1F);
            }
            tempo[i] = 0.85f + 0.30f * Next(i);
            nextGo[i] = -1f;
            joinAt[i] = -1f;
            sal[i] = 1f;
            tauHab[i] = 6f + 6f * Next(i);
            tauRec[i] = 40f + 40f * Next(i);
            reactAt[i] = 0.35f + 0.20f * Next(i);
            mobStart[i] = -1f;
            mobCap[i] = 3f + 3f * Next(i);
            lastCall[i] = -1f;
            dirtY[i] = 0f;
            airFrom[i] = -1f;
            airCap[i] = 4f;
            landAt[i] = -1f;
            fleeFrom[i] = -1f;
            fleeCap[i] = 4f;
        }
    }

    private float Next(int i)
    {
        return (float)rng[i].NextDouble();
    }

    private float Range(int i, float a, float b)
    {
        return a + (b - a) * Next(i);
    }

    // ---------------------------------------------------------------- salience
    // Called once per crow per tick (both the wake lane and the flee lane ask);
    // a second call inside the same fixed step returns the cached verdict, so
    // the decay cannot run twice and the state cannot diverge between lanes.
    public bool Salient(int i, float now, Vector2 threatPos, bool sees)
    {
        if (i < 0 || i >= n)
            return true;
        if (lastCall[i] == now)
            return lastVerdict[i];
        float dt = lastCall[i] < 0f ? 0f : Mathf.Max(0f, now - lastCall[i]);
        lastCall[i] = now;
        if (sees)
        {
            float dx = threatPos.x - salRef[i].x;
            float dy = threatPos.y - salRef[i].y;
            // Fresh = the mark moved more than 0.4 m, or this is the first
            // reading after a blind gap (a crow that looked away and back).
            bool fresh = dx * dx + dy * dy > 0.16f || dt <= 0f || dt > 3f;
            if (fresh)
            {
                sal[i] = 1f;
                salRef[i] = threatPos;
                if (mobStart[i] < 0f)
                    mobStart[i] = now;
            }
            else
            {
                sal[i] = Mathf.Max(0f, sal[i] - dt / Mathf.Max(0.5f, tauHab[i]));
            }
            // The crow's own bout cap: after 3-6 s the mark stops being salient,
            // so a static threat cannot hold the pack in the mob lane for ever.
            if (mobStart[i] >= 0f && now - mobStart[i] > mobCap[i])
            {
                sal[i] = 0f;
                mobStart[i] = -1f;
            }
        }
        else
        {
            mobStart[i] = -1f;
            // Recovery is slow and capped while the mark has not moved: a static
            // mark brings short re-check sorties, never a full new mob.
            sal[i] = Mathf.Min(0.6f, sal[i] + dt / Mathf.Max(1f, tauRec[i]));
        }
        bool verdict = sal[i] > reactAt[i];
        lastVerdict[i] = verdict;
        return verdict;
    }

    public bool PackSalient(float now)
    {
        for (int i = 0; i < n; i++)
            if (sal[i] > reactAt[i])
                return true;
        return false;
    }

    // ------------------------------------------------------------------ take-off
    // airCount = members the drive sees airborne this tick (clock ones only are
    // counted by the drive through AskAlreadyAir, flushes are exempt).
    public CrowAsk Pre(int i, float now, bool grounded, bool onTree, bool salient, int airCount)
    {
        CrowAsk a = default(CrowAsk);
        if (i < 0 || i >= n)
            return a;
        if (!grounded)
            return a;
        if (nextGo[i] < 0f)
        {
            // First lift soon after the bind, then the cycle timer below.
            nextGo[i] = now + Range(i, 1.5f, 4.0f) * tempo[i];
            return a;
        }
        bool due = now >= nextGo[i];
        // Land-dwell floor: a crow that just came down is not pulled straight back
        // up by a neighbour's lift (that kept the pack airborne).
        bool join = joinAt[i] >= 0f && now >= joinAt[i] && now - landAt[i] >= 2.5f;
        if (join)
            joinAt[i] = -1f;
        if (!due && !join)
            return a;
        // An alarmed crow is in the mob lane: the clock does not lift it.
        if (salient)
        {
            nextGo[i] = now + Range(i, 2f, 4f);
            joinAt[i] = -1f;
            return a;
        }
        if (airCount >= MaxAir)
        {
            // A capped crow re-checks in half a second to a second and a half.
            nextGo[i] = now + Range(i, 0.5f, 1.5f);
            return a;
        }
        a.takeoff = true;
        a.leader = due;
        // Own take-off interval: 4-8 s off the dirt, 3-6 s off a perch, times the
        // crow's tempo (max 9.2 s, so the <= 18 s interval invariant holds by
        // construction and needs no clip).
        nextGo[i] = now + Range(i, onTree ? 3f : 4f, onTree ? 6f : 8f) * tempo[i];
        return a;
    }

    // A leader lift calls this for every neighbour inside the join radius; the
    // drive owns the neighbour scan (index order), the helper owns the draw.
    public bool OfferJoin(int k, float now, float dx)
    {
        if (k < 0 || k >= n || Mathf.Abs(dx) > 3.0f)
            return false;
        if (now - landAt[k] < 2.5f)
            return false;
        if (joinAt[k] >= 0f)
            return false;
        if (Next(k) > 0.65f)
            return false;
        joinAt[k] = now + Range(k, 0.5f, 1.5f);
        return true;
    }

    // ------------------------------------------------------------------ landing
    // Called for every crow that reaches the wander step; the landing edge is
    // kept here so the drive needs no new Member field. The next own lift is
    // re-armed from the landing (shorter off a perch than off the dirt).
    public bool Post(int i, float now, bool grounded, bool onTree, float bodyY)
    {
        if (i < 0 || i >= n)
            return false;
        if (!grounded)
        {
            if (!wasAir[i])
            {
                wasAir[i] = true;
                airFrom[i] = now;
                airCap[i] = Range(i, 1.8f, 2.8f);
            }
            return false;
        }
        dirtY[i] = bodyY;
        airFrom[i] = -1f;
        if (!wasAir[i])
            return false;
        wasAir[i] = false;
        joinAt[i] = -1f;
        landAt[i] = now;
        float lo = onTree ? 4f : 10f;
        float hi = onTree ? 7f : 15f;
        nextGo[i] = now + Range(i, lo, hi) * tempo[i];
        // The landing edge: the drive opens a forage bout on it.
        return true;
    }

    // Air bout: a lift is a commute, not a hover. The drive asks AirTimeout and
    // drops the crow to the dirt with the wander's own descent, so the cycle
    // closes (lift - fly - land - forage) instead of hanging in a glide.
    public bool AirTimeout(int i, float now)
    {
        if (i < 0 || i >= n)
            return false;
        if (airFrom[i] < 0f)
            return false;
        return now - airFrom[i] > airCap[i];
    }

    // Attack (stoop) only well above the dirt: the 0.2 m hover-sit goes.
    public bool AttackAllowed(int i, float bodyY)
    {
        if (i < 0 || i >= n)
            return true;
        return bodyY - dirtY[i] > 1.0f;
    }

    // Flee-lane air cap: the mob lane wrote Fly every tick for as long as the
    // mob window lasted, so a scared crow hung 15-22 s in the air. The flee
    // flight gets its own bout; past it the drive glides the crow down.
    public bool FleeAirDone(int i, float now, bool airborne)
    {
        if (i < 0 || i >= n)
            return false;
        if (!airborne)
        {
            fleeFrom[i] = -1f;
            return false;
        }
        if (fleeFrom[i] < 0f)
        {
            fleeFrom[i] = now;
            fleeCap[i] = Range(i, 2.5f, 4.5f) * tempo[i];
            return false;
        }
        return now - fleeFrom[i] > fleeCap[i];
    }
}
