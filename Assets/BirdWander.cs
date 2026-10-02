using UnityEngine;

// Random flight without forces: only mode and the nose sign.
// Seed is required — a stand repeat must match.
public sealed class BirdWander
{
    public int takeoffCount;
    public int walkCount;
    public int landCount;
    public int facingFlipCount;
    public int perchLandCount;
    public int dirtRejectCount;
    public int peckCount;
    // Brain handshake: TickFlock must not Sit this Glide on a twig.
    public bool DirtBout => dirtBout;
    // Sit picture. Brain writes these; BirdController draws them. Not Peck (beak Hurt).
    public float IdleLook;
    public float IdlePreen;
    public float IdleHop;
    // Dirt forage picture. CrowCruise writes Peck in staged bursts;
    // ChickenGround still uses ChickenWander Cos. Not Hurt.
    public float SearchCrouch;
    public float PeckWindup;
    public float PeckStrike;
    public float PeckHeadUp;
    public bool PeckMiss;
    // Aerial play picture. Roll is look-bank only. Wander never writes Attack:
    // a forage stoop dumps pitch past the stand's 55° fall.
    public float PlayRoll;
    public bool PlayDive;
    // Flock sets false so members wait for a pack ForceFly instead of dripping off.
    public bool allowSelfFly = true;

    private System.Random rng;
    // Forage is dirt feeding (Walk + Peck). Body modes stay Walk/Peck —
    // LifeState.Forage is the shared life root, not a BirdMode.
    private enum Phase
    {
        Sit,
        Forage,
        Fly,
        Glide
    }

    private Phase phase = Phase.Sit;
    private float phaseEnds;
    private float nextTurn;
    private float facing = 1f;
    private bool wasAirborne;
    // Perch forage: Glide only after the bird has left the slot (hit the orbit leash).
    private bool perchLeftSlot;
    private int orbitHits;
    private int orbitsNeeded;
    private bool beyondLeash;
    private float weaveGlideUntil;
    private float nextWeavePulse;
    // Glide-to-dirt forage: Sit/Walk on soil, then Fly back to the slot.
    private bool dirtBout;
    private bool didDirtWalk;
    // Dirt bout: search crouch → 2–3 pecks → look, then Fly.
    // Hens keep ChickenWander. Pad Sit still Fly without Peck.
    private enum DirtAct
    {
        None,
        Search,
        Peck,
        Look
    }

    private enum PeckAct
    {
        Windup,
        Strike,
        HeadUp
    }

    private DirtAct dirtAct;
    private PeckAct peckAct;
    private float actEnds;
    private int pecksLeft;
    private float nextIdle;
    private float idleUntil;
    private int idleKind;
    private float nextPlay;
    private float playUntil;
    private int playKind;
    // Per-bird bout scale from the wander seed. Flock members share
    // sitMin/walkMin; this keeps peck/walk/fly clocks from locking.
    private float tempo = 1f;

    public void Reset(int seed, float now, float sitMin, float sitMax, float startFacing)
    {
        rng = new System.Random(seed);
        tempo = 0.78f + (float)rng.NextDouble() * 0.50f;
        facing = startFacing >= 0f ? 1f : -1f;
        phase = Phase.Sit;
        phaseEnds = now + Tempo(Rand(sitMin, sitMax));
        nextTurn = now + Rand(2.2f, 5.5f);
        wasAirborne = false;
        perchLeftSlot = false;
        orbitHits = 0;
        orbitsNeeded = 2;
        beyondLeash = false;
        weaveGlideUntil = 0f;
        nextWeavePulse = now + 999f;
        dirtBout = false;
        didDirtWalk = false;
        pecksLeft = 0;
        ClearDirtAct();
        ClearIdle();
        nextIdle = now + Rand(1.4f, 4f);
        playKind = 0;
        playUntil = 0f;
        nextPlay = now + 999f;
        PlayDive = false;
        PlayRoll = 0f;
        takeoffCount = 0;
        walkCount = 0;
        landCount = 0;
        facingFlipCount = 0;
        perchLandCount = 0;
        dirtRejectCount = 0;
        peckCount = 0;
    }

    public void Step(
        bool grounded,
        float x,
        float homeX,
        float now,
        float sitMin,
        float sitMax,
        float walkMin,
        float walkMax,
        float flyMin,
        float flyMax,
        float glideLead,
        float turnMin,
        float turnMax,
        float leash,
        bool wantPerch,
        bool onPerch,
        float homeDeadzone,
        float y,
        float homeY,
        float cruiseClearance,
        float weaveGlide,
        float lookMin,
        float lookMax,
        out BirdMode mode,
        out float face)
    {
        if (!grounded)
            wasAirborne = true;
        else if (wasAirborne)
        {
            wasAirborne = false;
            if (wantPerch && !onPerch)
            {
                if (phase == Phase.Glide || dirtBout)
                {
                    // Chosen (or missed-slot) dirt descent. Short Sit, then Forage.
                    dirtBout = true;
                    landCount++;
                    EnterSit(now, sitMin, sitMax, lookMin, lookMax);
                }
                else
                {
                    // Dirt is not home on a Fly hop. Back to the slot.
                    dirtRejectCount++;
                    EnterFly(now, flyMin, flyMax, turnMin, turnMax);
                    SetFace(x > homeX ? -1f : 1f);
                }
            }
            else
            {
                // Pad: long Sit, then Fly. Flat dirt: forage Sit, then Walk+Peck.
                dirtBout = !wantPerch;
                didDirtWalk = false;
                landCount++;
                if (wantPerch && onPerch)
                    perchLandCount++;
                EnterSit(now, sitMin, sitMax, lookMin, lookMax);
            }
        }

        if (phase == Phase.Sit && grounded)
        {
            if (!wantPerch && !dirtBout)
            {
                dirtBout = true;
                if (phaseEnds - now > 1.25f)
                    phaseEnds = now + Tempo(Rand(0.45f, 1.25f));
            }

            if (now >= nextTurn)
            {
                SetFace(-facing);
                nextTurn = now + Rand(lookMin, lookMax);
            }

            TickIdle(now);

            if (now >= phaseEnds)
            {
                if (dirtBout || !wantPerch)
                    EnterForage(now, walkMin, walkMax, turnMin, turnMax);
                else if (allowSelfFly)
                    EnterFly(now, flyMin, flyMax, turnMin, turnMax);
                else
                    phaseEnds = now + Tempo(Rand(sitMin, sitMax));
            }
        }
        else
            ClearIdle();

        if (phase == Phase.Forage)
        {
            if (leash > 0.01f && Mathf.Abs(x - homeX) > leash)
                SetFace(x > homeX ? -1f : 1f);
            else if (now >= nextTurn)
            {
                SetFace(-facing);
                nextTurn = now + Rand(turnMin, turnMax);
            }

            TickDirtForage(now, x, homeX, wantPerch, flyMin, flyMax, turnMin, turnMax);
        }

        if (phase == Phase.Fly)
        {
            // Climb above the pad before circling, or Glide hugs the crown.
            bool climbed = !wantPerch || (y - homeY) >= Mathf.Max(0.15f, cruiseClearance);
            bool outside = leash > 0.01f && Mathf.Abs(x - homeX) > leash;
            if (outside)
            {
                if (!wantPerch || climbed)
                {
                    SetFace(x > homeX ? -1f : 1f);
                    if (wantPerch)
                    {
                        perchLeftSlot = true;
                        if (!beyondLeash)
                            orbitHits++;
                    }
                }
            }
            else if (now >= nextTurn && (!wantPerch || climbed))
            {
                // Weave inside the orbit — not a straight out-and-back.
                SetFace(-facing);
                nextTurn = now + Rand(turnMin, turnMax);
            }

            beyondLeash = outside;

            bool farFromSlot = !wantPerch
                || Mathf.Abs(x - homeX) > Mathf.Max(0.8f, leash * 0.45f);
            TickAirPlay(now, climbed, farFromSlot);
            // Weave pulses chatter Fly/Glide every fraction of a second.
            // Pack cruise holds Fly until the bout Glide; 0 disables the pulse.
            if (wantPerch && climbed && farFromSlot && weaveGlide > 0.02f
                && now >= nextWeavePulse)
            {
                // Outer-arc Glide pulse dumps leftover speed. Not near the pad:
                // TickFlock Sit on any Glide that overlaps a perch trigger.
                weaveGlideUntil = now + Rand(0.18f, weaveGlide);
                nextWeavePulse = weaveGlideUntil + Rand(turnMin, turnMax);
            }

            float lead = Mathf.Max(0f, glideLead);
            if (wantPerch)
            {
                float giveUp = phaseEnds + Mathf.Max(flyMax, 2f);
                bool lapsDone = orbitHits >= orbitsNeeded;
                if ((perchLeftSlot && climbed && lapsDone && now >= phaseEnds - lead)
                    || now >= giveUp)
                {
                    // About one bout in two sinks to dirt instead of the slot.
                    dirtBout = rng.Next(0, 2) == 0;
                    phase = Phase.Glide;
                }
            }
            else if (now >= phaseEnds - lead)
            {
                // Flat dirt: Glide must ask for a dirt plant. False kept
                // preferDirtLand off, so the body held a cruise sink and
                // landCount stayed 0 for the whole bout.
                dirtBout = true;
                phase = Phase.Glide;
            }
        }

        // Glide-to-slot: x chatters through homeX every Update.
        // Dirt descent keeps the nose and sinks — FaceHome would climb the pad.
        if (phase == Phase.Glide)
        {
            PlayDive = false;
            PlayRoll = 0f;
            if (wantPerch && !dirtBout)
                FaceHome(x, homeX, homeDeadzone);
        }

        if (phase == Phase.Sit)
            mode = BirdMode.Sit;
        else if (phase == Phase.Forage)
        {
            if (dirtAct == DirtAct.Peck)
                mode = BirdMode.Peck;
            else if (dirtAct == DirtAct.Look)
                mode = BirdMode.Sit;
            else
                mode = BirdMode.Walk;
        }
        else if (phase == Phase.Fly)
        {
            bool pulse = now < weaveGlideUntil;
            bool nearSlot = wantPerch && Mathf.Abs(x - homeX) < 0.8f;
            mode = pulse && !nearSlot ? BirdMode.Glide : BirdMode.Fly;
        }
        else
            mode = BirdMode.Glide;

        face = facing;
    }

    // TickFlock writes Sit on contact. Sync the wander phase so the next
    // Step does not put Fly back over that Sit. Already sitting — leave the timer.
    public void ForceSit(float now, float sitMin, float sitMax, float lookMin, float lookMax)
    {
        if (phase == Phase.Sit)
            return;
        ClearIdle();
        ClearDirtAct();
        EnterSit(now, sitMin, sitMax, lookMin, lookMax);
    }

    // Insect hunt does not Step. Fly/Glide or a dirt Forage clock used to
    // write leftover cruise Fly the first Forage frame after the eat.
    // Keep dirtBout: TickWander copies it to preferDirtLand, and a false
    // here re-armed hoverMean Fly on the plate after the eat.
    public void HoldHuntSit(float now, float sitMin, float sitMax, float lookMin, float lookMax)
    {
        dirtBout = true;
        didDirtWalk = false;
        if (phase == Phase.Fly || phase == Phase.Glide || phase == Phase.Forage)
        {
            ClearIdle();
            ClearDirtAct();
            EnterSit(now, sitMin, sitMax, lookMin, lookMax);
            return;
        }

        if (phase == Phase.Sit && now >= phaseEnds)
            phaseEnds = now + Tempo(Rand(0.85f, 2.2f));
    }

    // Pack land: sink together. Perch keeps the slot heading; dirt sets DirtBout.
    public void ForceGlide(bool toDirt)
    {
        if (phase != Phase.Fly && phase != Phase.Glide)
            return;
        phase = Phase.Glide;
        dirtBout = toDirt;
        weaveGlideUntil = 0f;
        PlayDive = false;
        PlayRoll = 0f;
        playKind = 0;
    }

    // Flock startle / keep-away. Restarts a fly bout so Sit does not snap mid-air.
    // Overlay may call this every Update while the bout holds — do not re-roll
    // RNG or ClearIdle on a bird that is already flying.
    public void ForceFly(float now, float flyMin, float flyMax, float turnMin, float turnMax)
    {
        if (phase == Phase.Fly)
        {
            dirtBout = false;
            didDirtWalk = false;
            float hold = now + Mathf.Max(0.4f, flyMin);
            if (phaseEnds < hold)
                phaseEnds = hold;
            return;
        }

        ClearIdle();
        dirtBout = false;
        didDirtWalk = false;
        ClearDirtAct();
        if (phase == Phase.Glide)
        {
            phase = Phase.Fly;
            phaseEnds = now + Tempo(Rand(flyMin, flyMax));
            PlayDive = false;
            PlayRoll = 0f;
            playKind = 0;
            return;
        }

        EnterFly(now, flyMin, flyMax, turnMin, turnMax);
    }

    private void EnterSit(float now, float sitMin, float sitMax, float lookMin, float lookMax)
    {
        phase = Phase.Sit;
        // Dirt settle is a plant, not a 12–22 s perch rest.
        phaseEnds = now + (dirtBout
            ? Tempo(Rand(0.45f, 1.25f))
            : Tempo(Rand(sitMin, sitMax)));
        nextTurn = now + Rand(lookMin, lookMax);
        weaveGlideUntil = 0f;
        pecksLeft = 0;
        nextIdle = now + Rand(1.2f, 3.8f);
        ClearDirtAct();
        ClearIdle();
        playKind = 0;
        PlayDive = false;
        PlayRoll = 0f;
    }

    private void EnterForage(float now, float walkMin, float walkMax, float turnMin, float turnMax)
    {
        phase = Phase.Forage;
        walkCount++;
        if (dirtBout)
            didDirtWalk = true;
        if (rng.Next(0, 2) == 0)
            SetFace(-facing);
        // Safety only. The bout is event-gated: search → pecks → look.
        phaseEnds = now + Tempo(12f);
        nextTurn = now + Rand(turnMin, turnMax);
        pecksLeft = 0;
        dirtAct = DirtAct.Search;
        actEnds = now + Tempo(Rand(0.55f, 1.20f));
        SearchCrouch = 1f;
        ClearPeckPose();
        ClearIdle();
        playKind = 0;
        PlayDive = false;
        PlayRoll = 0f;
    }

    private void TickDirtForage(
        float now,
        float x,
        float homeX,
        bool wantPerch,
        float flyMin,
        float flyMax,
        float turnMin,
        float turnMax)
    {
        // Hung-bout lid only. Search / Peck / Look own their clocks.
        if (now >= phaseEnds)
        {
            EndDirtForage(now, x, homeX, wantPerch, flyMin, flyMax, turnMin, turnMax);
            return;
        }

        if (dirtAct == DirtAct.Search)
        {
            SearchCrouch = 1f;
            if (now >= actEnds)
                EnterPeckBurst(now);
            return;
        }

        if (dirtAct == DirtAct.Peck)
        {
            TickPeckBurst(now);
            return;
        }

        if (dirtAct == DirtAct.Look)
        {
            TickLook(now);
            if (now >= actEnds)
                EndDirtForage(now, x, homeX, wantPerch, flyMin, flyMax, turnMin, turnMax);
        }
    }

    private void EndDirtForage(
        float now,
        float x,
        float homeX,
        bool wantPerch,
        float flyMin,
        float flyMax,
        float turnMin,
        float turnMax)
    {
        if (dirtBout && wantPerch)
            SetFace(x > homeX ? -1f : 1f);
        if (allowSelfFly)
        {
            dirtBout = false;
            didDirtWalk = false;
            EnterFly(now, flyMin, flyMax, turnMin, turnMax);
            return;
        }

        // Stay with the pack: local Walk/Peck is done, sit until ForceFly.
        dirtBout = false;
        didDirtWalk = false;
        EnterSit(now, 0.8f, 1.6f, turnMin, turnMax);
    }

    private void EnterPeckBurst(float now)
    {
        dirtAct = DirtAct.Peck;
        SearchCrouch = 0f;
        pecksLeft = 2 + rng.Next(0, 2);
        StartPeckWindup(now);
    }

    private void StartPeckWindup(float now)
    {
        peckAct = PeckAct.Windup;
        PeckMiss = false;
        PeckWindup = 1f;
        PeckStrike = 0f;
        PeckHeadUp = 0f;
        actEnds = now + Tempo(Rand(0.10f, 0.20f));
    }

    private void TickPeckBurst(float now)
    {
        if (now < actEnds)
            return;

        if (peckAct == PeckAct.Windup)
        {
            peckAct = PeckAct.Strike;
            PeckMiss = rng.Next(0, 3) == 0;
            PeckWindup = 0f;
            PeckStrike = 1f;
            PeckHeadUp = 0f;
            pecksLeft--;
            peckCount++;
            actEnds = now + Tempo(Rand(0.10f, 0.16f));
            return;
        }

        if (peckAct == PeckAct.Strike)
        {
            peckAct = PeckAct.HeadUp;
            PeckWindup = 0f;
            PeckStrike = 0f;
            PeckHeadUp = 1f;
            actEnds = now + Tempo(Rand(0.16f, 0.30f));
            return;
        }

        if (pecksLeft > 0)
            StartPeckWindup(now);
        else
            EnterLook(now);
    }

    private void EnterLook(float now)
    {
        dirtAct = DirtAct.Look;
        SearchCrouch = 0f;
        ClearPeckPose();
        idleKind = 1;
        idleUntil = now + Tempo(Rand(0.75f, 1.60f));
        actEnds = idleUntil;
        TickLook(now);
    }

    private void TickLook(float now)
    {
        IdleLook = 0.35f + 0.55f * Mathf.Sin(now * 2.1f);
        IdlePreen = 0f;
        IdleHop = 0f;
    }

    private void ClearDirtAct()
    {
        dirtAct = DirtAct.None;
        pecksLeft = 0;
        SearchCrouch = 0f;
        ClearPeckPose();
    }

    private void ClearPeckPose()
    {
        PeckWindup = 0f;
        PeckStrike = 0f;
        PeckHeadUp = 0f;
        PeckMiss = false;
    }

    private void EnterFly(float now, float flyMin, float flyMax, float turnMin, float turnMax)
    {
        phase = Phase.Fly;
        takeoffCount++;
        perchLeftSlot = false;
        orbitHits = 0;
        orbitsNeeded = 1 + rng.Next(0, 2);
        beyondLeash = false;
        weaveGlideUntil = 0f;
        if (!dirtBout)
            didDirtWalk = false;
        if (rng.Next(0, 2) == 0)
            SetFace(-facing);
        phaseEnds = now + Tempo(Rand(flyMin, flyMax));
        nextTurn = now + Rand(turnMin, turnMax);
        nextWeavePulse = now + Rand(turnMin, turnMax);
        ClearDirtAct();
        ClearIdle();
        playKind = 0;
        PlayDive = false;
        PlayRoll = 0f;
        playUntil = 0f;
        nextPlay = now + Rand(1.1f, 2.8f);
    }

    private void TickIdle(float now)
    {
        if (idleKind != 0 && now >= idleUntil)
        {
            ClearIdle();
            nextIdle = now + Rand(1.6f, 4.4f);
        }

        if (idleKind == 0 && now >= nextIdle)
        {
            int pick = rng.Next(0, 10);
            if (pick < 4)
            {
                idleKind = 1;
                idleUntil = now + Rand(0.55f, 1.35f);
            }
            else if (pick < 8)
            {
                idleKind = 2;
                idleUntil = now + Rand(0.7f, 1.7f);
            }
            else
            {
                idleKind = 3;
                idleUntil = now + Rand(0.2f, 0.4f);
            }
        }

        if (idleKind == 1)
            IdleLook = Mathf.Sin(now * 2.4f) * 0.9f;
        else if (idleKind == 2)
            IdlePreen = 0.65f + 0.35f * Mathf.Sin(now * 8.5f);
        else if (idleKind == 3)
            IdleHop = Mathf.Abs(Mathf.Sin(now * 22f));
    }

    private void TickAirPlay(float now, bool climbed, bool farFromSlot)
    {
        if (playKind != 0 && now >= playUntil)
        {
            playKind = 0;
            PlayDive = false;
            PlayRoll = 0f;
            nextPlay = now + Rand(1.4f, 3.6f);
        }

        if (playKind == 0 && climbed && farFromSlot && now >= nextPlay)
        {
            // Bank only. Kind 1 used to be an Attack stoop and failed pitch-fall.
            playKind = 2;
            playUntil = now + Rand(0.4f, 0.85f);
        }

        PlayDive = false;
        PlayRoll = playKind == 2 && now < playUntil
            ? Mathf.Sin(now * 9f)
            : 0f;
    }

    private void ClearIdle()
    {
        idleKind = 0;
        IdleLook = 0f;
        IdlePreen = 0f;
        IdleHop = 0f;
    }

    private void FaceHome(float x, float homeX, float deadzone)
    {
        float dx = x - homeX;
        if (Mathf.Abs(dx) <= Mathf.Max(0f, deadzone))
            return;
        SetFace(dx > 0f ? -1f : 1f);
    }

    private void SetFace(float sign)
    {
        float next = sign >= 0f ? 1f : -1f;
        if (next != facing)
            facingFlipCount++;
        facing = next;
    }

    private float Tempo(float seconds)
    {
        return seconds * tempo;
    }

    private float Rand(float min, float max)
    {
        if (max < min)
            max = min;
        return (float)(min + rng.NextDouble() * (max - min));
    }
}
