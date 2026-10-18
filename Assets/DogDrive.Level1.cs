using UnityEngine;

// LEVEL 1 DOG (req-231): the owner's line - "the dog follows the master but has its
// OWN AI and its own natural behaviour" (Telegram 15.10). This partial file is that
// decision layer: a seeded CHOICE to chase a crow (not a reflex), short sniff stops
// while following, a give-up window, and the no-kill rule of the level. It runs only
// when level1Brain is set (-level1Dog 1); every other take keeps its bytes.
public partial class DogDrive
{
    // The level's own knobs; the defaults are the request's line.
    public bool level1Brain;              // set by HeadlessTrial.BuildLevel1Dog
    public int level1Seed = 1;            // -level1DogSeed: the living-world RNG
    public float level1ChaseP = 0.35f;    // -level1DogChaseP: P(chase) per notice
    public float level1ChaseMax = 4f;     // -level1DogChaseMax: give up after this, s
    public float level1SniffP = 0.15f;    // -level1DogSniffP: P(sniff stop) per 5 s
    public bool level1NoKill = true;      // -level1DogNoKill: no leap / kill / eat
    // ROUND 2 (-level1DogChaseGround 1): a chase is a run at a mark the dog can reach -
    // a crow in a tree is not a chase, and the give-up is the crow AIRBORNE (flight mode)
    // above 1.5 m, not merely above. Round 1 (default off): any mark in noticeRange.
    public bool level1ChaseGround;
    public float level1Reroll = 6f;       // notice re-roll cooldown, s

    private System.Random l1Rng;
    private Bird l1Bird;                  // the bound mark's bird (flight mode), cached
    private Transform l1BirdFor;
    private bool l1Chasing;
    private float l1ChaseStart;
    private float l1NextRoll;
    private float l1SniffUntil;
    private float l1NextSniffRoll;
    private bool l1SniffHold;             // true while a sniff stop owns the frame
    private string l1Act;
    private int l1Chases;
    private int l1GiveUps;
    private int l1Sniffs;

    public bool Level1SniffHold { get { return l1SniffHold; } }
    public int Level1Chases { get { return l1Chases; } }
    public int Level1GiveUps { get { return l1GiveUps; } }
    public int Level1Sniffs { get { return l1Sniffs; } }

    // Called once by HeadlessTrial when the level1 dog is built. Seeded: the same
    // -level1DogSeed line replays the same life.
    public void ConfigureLevel1Brain(int seed, float chaseP, float chaseMax, float sniffP, bool noKill)
    {
        level1Brain = true;
        level1Seed = seed;
        level1ChaseP = Mathf.Clamp01(chaseP);
        level1ChaseMax = Mathf.Max(0.5f, chaseMax);
        level1SniffP = Mathf.Clamp01(sniffP);
        level1NoKill = noKill;
        l1Rng = new System.Random(seed * 7919 + 13);
        l1NextRoll = 0f;
        l1NextSniffRoll = 0f;
        l1Chasing = false;
        l1SniffUntil = 0f;
        Level1Log("start");
    }

    // One line per decision: LEVEL1 DOG t=<s> act=<follow|chase|giveup|sniff|sit> x=<m>.
    private void Level1Log(string act)
    {
        l1Act = act;
        Vector2 p = SelfWorldPos();
        Debug.Log("LEVEL1 DOG t=" + Time.time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                  + " act=" + act
                  + " x=" + p.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
    }

    private void Level1Act(string act)
    {
        if (act == l1Act)
            return;
        Level1Log(act);
    }

    // THE CHOICE, not a reflex: a crow in noticeRange is chased only when the seeded
    // roll says so; a failed roll re-arms after level1Reroll. A chase gives up on
    // time or on a mark that lifted more than 1.5 m above the dog - then the follow
    // trot back to the hero resumes. Returns the effective "saw prey".
    private bool Level1ChaseGate(float now, float dist, bool sawPrey)
    {
        if (!sawPrey)
        {
            if (l1Chasing)
                Level1GiveUp(now);
            return false;
        }

        if (l1Chasing)
        {
            if (now - l1ChaseStart > level1ChaseMax || Level1PreyUpHigh())
            {
                Level1GiveUp(now);
                return false;
            }
            return true;
        }

        if (level1ChaseGround && !Level1Chaseable())
            return false;                 // out of reach (a tree) - nothing to run at
        if (now < l1NextRoll)
            return false;
        l1NextRoll = now + Mathf.Max(0.5f, level1Reroll);
        if (l1Rng != null && l1Rng.NextDouble() < level1ChaseP)
        {
            l1Chasing = true;
            l1ChaseStart = now;
            l1Chases++;
            Level1Act("chase");
            return true;
        }
        return false;
    }

    private void Level1GiveUp(float now)
    {
        l1Chasing = false;
        l1GiveUps++;
        Level1Act("giveup");
        l1NextRoll = now + Mathf.Max(0.5f, level1Reroll);
    }

    // "Airborne > 1.5 m up": round 1 read the height alone. Round 2: the mark must be
    // FLYING (Fly / Glide - the level rows' own air flag) above 1.5 m to count as escaped.
    private bool Level1PreyUpHigh()
    {
        if (prey == null)
            return false;
        if (prey.position.y - SelfWorldPos().y <= 1.5f)
            return false;
        if (!level1ChaseGround)
            return true;
        Bird b = Level1PreyBird();
        return b != null && b.controller != null
            && (b.controller.mode == BirdMode.Fly || b.controller.mode == BirdMode.Glide);
    }

    // Round 2: a chaseable mark is one the dog can reach - at or below 1.5 m above it.
    private bool Level1Chaseable()
    {
        if (prey == null)
            return false;
        return prey.position.y - SelfWorldPos().y <= 1.5f;
    }

    private Bird Level1PreyBird()
    {
        if (prey == null)
        {
            l1Bird = null;
            l1BirdFor = null;
            return null;
        }
        if (l1BirdFor != prey)
        {
            l1BirdFor = prey;
            l1Bird = prey.GetComponentInParent<Bird>();
        }
        return l1Bird;
    }

    // Short stops while following: P = level1SniffP per 5 s, hold 1-3 s. The hold owns
    // the frame (Update skips TickIdle) so the nose stays down to the dirt.
    private bool Level1SniffTick(float now)
    {
        if (now < l1SniffUntil)
        {
            l1SniffHold = true;
            return true;
        }
        l1SniffHold = false;
        bool walking = control != null && (control.walkWanted || control.IsWalk());
        if (!walking || now < l1NextSniffRoll)
            return false;
        l1NextSniffRoll = now + 5f;
        if (l1Rng == null || l1Rng.NextDouble() >= level1SniffP)
            return false;
        l1SniffUntil = now + 1f + (float)l1Rng.NextDouble() * 2f;
        l1Sniffs++;
        Level1Act("sniff");
        l1SniffHold = true;
        return true;
    }

    // The no-kill hunt: nose down, stiff raised tail at the mark - no leap, no bite,
    // no eat. The give-up window (level1ChaseMax) is what ends the stare.
    private void Level1HoldStare()
    {
        control.EndWalk();
        control.ClearIdle();
        float stalk = Mathf.Clamp01(huntStalk);
        if (stalk > 0f)
            control.idleHunt = stalk;
    }
}
