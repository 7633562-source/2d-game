using UnityEngine;

// Quadruped cosmetic layer - the expression / idle-flavour family, split out of
// DogStanceController.cs (dog cycle 2, 19.09.2026). PURE MOVE: the same class, the same
// fields, the same call order - only the file changed, so the ensemble md5 of the dog
// trial must stay identical (dog_art1_md51..5 -> cf35e1dda4).
//
// What lives here: FacingSign / SetFacing, the turn-hop notes, and the per-state
// expression routines - Walk turn/stride/trunk/rump bob, tail, ear, pant, whisker,
// squint, nose, eye, brow, puff and the whole Sit*/LeapHunt* library (scratch, sniff,
// chew, yawn, sleep, quiet, hunt, pant). 107 methods. The physical core (Bind, Sense,
// Drive*, the walk clock, get-up, leap, joints and muscles) stays in
// DogStanceController.cs.
public partial class DogStanceController
{
    public float FacingSign()
    {
        return facing >= 0f ? 1f : -1f;
    }

    // Brain writes the side. Picture on Visual; the trot inverts flex/extend.
    public void SetFacing(float sign)
    {
        float next = sign >= 0f ? 1f : -1f;
        if (next == FacingSign())
            return;
        facing = next;
        if (IsWalk() || walkWanted)
        {
            ResetWalkClock();
            NoteWalkTurn();
        }
        else
            NoteSitTurn();
        if (body != null)
            body.ApplyLookFacing(facing);
    }

    // Brain already wrote SetFacing (sit look-glance, greet, hunt stare).
    // Look scale flipped at once, so the turn read as a texture swap.
    // Hop near-front + near-hind then plant. Picture only — colliders
    // stay. Walk has its own hop (walkTurnAmp). Sleep / hunt-recover
    // plant / scratch skip. 0 = leave the instant flip. Not a yaw force.
    private void NoteSitTurn()
    {
        if (sitTurnAmp < 0.01f)
            return;
        if (!CanIdle())
            return;
        if (idleSleep > 0.01f)
            return;
        if (idleHunt > 0.01f)
            return;
        if (idleScratch > 0.01f)
            return;
        float now = Time.time;
        sitTurnBorn = now;
        sitTurnUntil = now + 0.36f;
    }

    // 0 at plant edges, peak mid-hop. Includes sitTurnAmp.
    public float SitTurnHop()
    {
        if (sitTurnAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!CanIdle())
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (Time.time >= sitTurnUntil)
            return 0f;
        float dur = sitTurnUntil - sitTurnBorn;
        if (dur < 0.05f)
            return 0f;
        float p = Mathf.Clamp01((Time.time - sitTurnBorn) / dur);
        return Mathf.Clamp(sitTurnAmp, 0f, 1.5f) * Mathf.Sin(p * Mathf.PI);
    }

    // Brain already wrote SetFacing on a trot (wander FaceToward then
    // Walk, follow about-face, hunt chase). Look scale plus a clock
    // reset still read as a texture swap. Hop the same near pair as
    // sit, then the stride. Picture only — colliders stay, leftover
    // sit is not a freeze. Leap / Sleep skip. 0 = instant flip.
    // Not a yaw force.
    private void NoteWalkTurn()
    {
        if (walkTurnAmp < 0.01f)
            return;
        if (energyRest)
            return;
        if (IsLeap())
            return;
        if (!walkWanted && !IsWalk())
            return;
        float now = Time.time;
        walkTurnBorn = now;
        walkTurnUntil = now + 0.32f;
    }

    // 0 at plant edges, peak mid-hop. Includes walkTurnAmp.
    public float WalkTurnHop()
    {
        if (walkTurnAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (Time.time >= walkTurnUntil)
            return 0f;
        float dur = walkTurnUntil - walkTurnBorn;
        if (dur < 0.05f)
            return 0f;
        float p = Mathf.Clamp01((Time.time - walkTurnBorn) / dur);
        return Mathf.Clamp(walkTurnAmp, 0f, 1.5f) * Mathf.Sin(p * Mathf.PI);
    }

    // Picture only. Leftover sit keeps the four colliders planted, so
    // a joint twitch still reads as a standing rake. Lift the SWING
    // diagonal Visuals (half 0: FL+RR, half 1: FR+RL) once those
    // knees peel. Dog LateUpdate poses proximal+mid+paw; paw-only
    // floated a stamp on planted thighs. About-face hop owns that
    // window. Leap / Sleep / rest skip. 0 = old planted pictures. Not
    // a lift force. Clock follows the knee ramp, not hip reach.
    public float WalkStrideHop(bool front, bool right)
    {
        if (walkStrideAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (!walkCaptureArmed)
            return 0f;
        if (WalkLimbIsStance(front, right))
            return 0f;
        // Same clock as the swing-knee peel. SwingU stays 0 until the
        // paw already travelled (sit-trot closed loop).
        float u = SwingPeelU();
        if (walkSwungAir)
            u = Mathf.Max(u, WalkAirBlend());
        return Mathf.Clamp(walkStrideAmp, 0f, 1.5f) * Mathf.Sin(u * Mathf.PI);
    }

    // Picture only. DriveWalk leftover sit holds the trunk collider,
    // so swing columns still ride a statue chest. Same two-dip clock
    // as walkHeadBob (each diagonal plant). Forage idleWag only —
    // hunt stalk stays stiff, CLI -dogWalk leaves idleWag 0. Hop
    // mutes. Leap / Sleep / rest skip. 0 = old still withers. Not a
    // lift force. Same walkPhase, not a new period.
    public float WalkTrunkBob()
    {
        if (walkBobAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkBobAmp, 0f, 1.5f)
            * WalkPulse();
    }

    // Picture only. DriveWalk leftover sit holds the pelvis collider,
    // so rear swing still rode a statue rump after withers heaved.
    // Same two-dip clock as WalkTrunkBob (in phase: a trot, not a
    // see-saw). Forage idleWag only — hunt stalk stays stiff, CLI
    // -dogWalk leaves idleWag 0. Hop mutes. Leap / Sleep / rest
    // skip. 0 = old still rump. Not a lift force. Sit pant after
    // EndWalk is SitRump on idlePant. Sit yawn is SitYawnRump on
    // idleYawn. Sit sniff is SitSniffRump on idleSniff. Same
    // walkPhase.
    public float WalkRumpBob()
    {
        if (walkRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkRumpAmp, 0f, 1.5f)
            * WalkPulse();
    }

    // Same first-hinge spring as sit idleWag. DriveWalk leftover sit
    // held the 8.5 Hz sit clock, so withers / rump / neck already
    // planted on walkPhase while the tail still sat-wagged. Same
    // two-dip clock as WalkTrunkBob (in phase: a trot, not a sit
    // metronome). Forage idleWag only — hunt stalk stays the stiff
    // raise, CLI -dogWalk leaves idleWag 0. Hop mutes (plant, not
    // 8.5 Hz). Leap / Sleep / rest skip. 0 = the old sit-rate wag
    // on the trot. Not a new idleTail (Walk() would zero it). Sit
    // pant after EndWalk hitches SitPantTail (droop with the
    // pump, not this clock). Sit look hitches SitLookTail (perk /
    // droop, not this clock). Sit sniff hitches SitSniffTail
    // (droop with the dirt bob). Sit scratch hitches
    // SitScratchTail (droop with the rake). Sit yawn hitches
    // SitYawnTail (droop with the gape). Sit chew hitches
    // SitChewTail (droop with the snap). Distal limp is
    // WalkDistalTail. Same walkPhase.
    public float WalkTail()
    {
        if (walkTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkTailAmp, 0f, 1.5f)
            * WalkPulse();
    }

    // Picture only. Forage Walk already wags the first hinge
    // (WalkTail on walkPhase) and hitches withers / rump / neck /
    // face with the trot — leftover walk held Tail2–Tail5 on
    // TreeSpring restAngle 0 (muscles only on the first hinge —
    // by design) so the gait still read as a stiff caudal rod on
    // a bobbing trunk. Limp the distal Visuals (accumulating
    // 6°…12°) following that same two-dip clock (env already
    // pulses walkPhase; no extra clock, not Sleep's 1.6 hitch,
    // not sniff's ~3 Hz bob, not yawn's envelope, not pant's
    // 6.5 Hz pump, not chew's 11 Hz snap, not look's signed bob,
    // not scratch's ~14 Hz rake, not quiet sit's 8.5 Hz, and not
    // a new distal muscle). Plus = clockwise, tip down, same
    // sign as IdleTailTarget's +16° * WalkTail. Hop mutes
    // (plant). Hunt stalk stays the stiff raise. CLI -dogWalk
    // leaves idleWag 0. Leap / Sleep / rest skip. Wander sniff
    // keeps WalkTail (SitSniffDistalTail skips Walk). Colliders
    // stay. Drive already wrote idleWag. Sit sniff distal is
    // SitSniffDistalTail. Quiet sit distal is SitQuietDistalTail.
    // Hunt recover distal is SitHuntDistalTail. Hunt stalk distal
    // is WalkHuntDistalTail. Hunt leap distal is LeapHuntDistalTail.
    // No idleTail: Walk() would zero it. 0 = the old rest-spring
    // forage Walk stick.
    public float WalkDistalTail()
    {
        if (walkDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkDistalTailAmp, 0f, 1.5f)
            * WalkPulse();
    }

    // Picture only. Generic walkWanted pin made Forage follow read as
    // hunt. Same two-dip clock as walkHeadBob. Forage idleWag only —
    // hunt pin stays on idleHunt, wander sniff keeps the half-fold,
    // CLI -dogWalk leaves idleWag 0. Hop mutes. Leap / Sleep / rest
    // skip. 0 = the old walk pin. Not a hinge. Sit pant after
    // EndWalk is SitEar on idlePant. Same walkPhase.
    public float WalkEarFlop()
    {
        if (walkEarAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkEarAmp, 0f, 1.5f)
            * WalkPulseAbs();
    }

    // Picture + DriveJaw. Closed follow looked stiffer than the hunt
    // snarl crack. Same two-dip clock as walkHeadBob. Forage idleWag
    // only — hunt crack stays on idleHunt, wander sniff stays closed
    // (nose on dirt), CLI -dogWalk leaves idleWag 0. Hop mutes. Leap /
    // Sleep / rest skip. 0 = the old closed forage walk. Not a new
    // idlePant (Walk() would zero it). Sit pant after EndWalk is still
    // idlePant. Same walkPhase.
    public float WalkPant()
    {
        if (walkPantAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkPantAmp, 0f, 1.5f)
            * (0.55f + 0.45f * WalkPulseAbs());
    }

    // Picture only. Rest stamps next to a hanging tongue still read as
    // a hold muzzle. Same two-dip clock as walkHeadBob. Forage idleWag
    // only — hunt pin stays on idleHunt, wander sniff keeps its own fan,
    // CLI -dogWalk leaves idleWag 0. Hop mutes. Leap / Sleep / rest
    // skip. 0 = the old rest forage walk. Not a new idleWhisker
    // (Walk() would zero it). Sit sniff stays idleSniff. Sit pant
    // after EndWalk is SitWhisker on idlePant. Sit chew is
    // SitChewWhisker on idleChew. Same walkPhase.
    public float WalkWhisker()
    {
        if (walkWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkWhiskerAmp, 0f, 1.5f)
            * (0.55f + 0.45f * WalkPulseAbs());
    }

    // Picture only. Rest pupil next to a hanging tongue still read as
    // a hunt-lock stare. Same two-dip clock as walkHeadBob. Forage
    // idleWag only — hunt recover owns SitHuntSquint, hunt stalk
    // Walk owns WalkHuntSquint, hunt leap owns LeapHuntSquint,
    // wander sniff keeps the open track
    // eye, CLI -dogWalk leaves idleWag 0. Hop mutes. Sleep /
    // rest skip. 0 = the old rest forage walk. Not a new idleBlink
    // (Walk() would zero it). Sleep / yawn / blink still own the
    // full slit. Sit pant after EndWalk is SitSquint on idlePant.
    // Same walkPhase.
    public float WalkSquint()
    {
        if (walkSquintAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkSquintAmp, 0f, 1.5f)
            * (0.55f + 0.45f * WalkPulseAbs());
    }

    // Picture only. Rest snout next to a hanging tongue still read as
    // a hunt-lock muzzle. Same two-dip clock as walkHeadBob. Forage
    // idleWag only — hunt drop stays on idleHunt, wander sniff keeps
    // its own dirt drop, CLI -dogWalk leaves idleWag 0. Hop mutes.
    // Leap owns LeapHuntNose. Sleep / rest skip. 0 = the old rest
    // forage walk. Not a new idleNose (Walk() would zero it). Sit
    // pant after EndWalk is SitNose on idlePant. Sit yawn is
    // SitYawnNose on idleYawn. Sit chew is SitChewNose on idleChew.
    // Same walkPhase.
    public float WalkNose()
    {
        if (walkNoseAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkNoseAmp, 0f, 1.5f)
            * (0.55f + 0.45f * WalkPulseAbs());
    }

    // Picture only. Rest pupil next to a dropped snout still read as
    // a hunt-lock stare through the squint. Same two-dip clock as
    // walkHeadBob. Forage idleWag only — hunt drop stays on idleHunt,
    // wander sniff keeps its own dirt drop, look-up stays idleLook,
    // CLI -dogWalk leaves idleWag 0. Hop mutes. Leap owns
    // LeapHuntEye. Sleep / rest skip. 0 = the old rest forage
    // walk. Not a new idleEye (Walk() would zero it). Sleep /
    // yawn / blink still hide the dot. Sit pant after EndWalk is
    // SitEye on idlePant. Sit chew is SitChewEye on idleChew. Same walkPhase.
    public float WalkEye()
    {
        if (walkEyeAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkEyeAmp, 0f, 1.5f)
            * (0.55f + 0.45f * WalkPulseAbs());
    }

    // Picture only. Rest brow next to a hanging tongue still read as
    // a hunt-lock forehead on the yellow box. Same two-dip clock as
    // walkHeadBob. Forage idleWag only — hunt furrow stays on
    // idleHunt, wander sniff keeps its own drop, look-up stays
    // idleLook, CLI -dogWalk leaves idleWag 0. Hop mutes. Leap /
    // Sleep owns SitSleepBrow / rest skip. 0 = the old rest forage
    // walk. Not a new idleBrow (Walk() would zero it). Sit pant
    // after EndWalk is SitBrow on idlePant. Sit yawn is SitYawnBrow
    // on idleYawn. Sit chew is SitChewBrow on idleChew. Same
    // walkPhase.
    public float WalkBrow()
    {
        if (walkBrowAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkBrowAmp, 0f, 1.5f)
            * (0.55f + 0.45f * WalkPulseAbs());
    }

    // Picture only. Hanging tongue with no air still read as a stamp,
    // not a pant (Bark puff is Call). Same two-dip clock as
    // walkHeadBob. Needs WalkPant so a closed mouth does not leak
    // vapor. Forage idleWag only — hunt recover owns SitHuntPuff,
    // hunt stalk owns WalkHuntPuff (this gate still needs WalkPant;
    // idleHunt skips), wander sniff stays closed, CLI -dogWalk
    // leaves idleWag 0. Hop mutes. Leap owns LeapHuntPuff. Sleep /
    // rest skip. 0 = the old silent forage walk tongue. Not a new
    // idlePant (Walk() would zero it). Sit pant after EndWalk is
    // SitPuff on idlePant. Sit yawn is SitYawnPuff on idleYawn.
    // Sit chew is SitChewPuff on idleChew (SitPuff / SitYawnPuff
    // skip idleChew). Hunt recover sit is SitHuntPuff on idleHunt
    // (this gate still needs WalkPant; SitPuff skips idleHunt).
    // Same walkPhase.
    public float WalkPuff()
    {
        if (walkPuffAmp < 0.01f)
            return 0f;
        if (WalkPant() < 0.01f)
            return 0f;
        return Mathf.Clamp(walkPuffAmp, 0f, 1.5f)
            * WalkPulseAbs();
    }

    // Picture only. Sit idlePant hung a tongue with no air after
    // EndWalk — same silent stamp WalkPuff already closed on the
    // trot. Pulse is the pump above the 0.55 hang so a closed mouth
    // does not leak. Brain already wrote idlePant; Walk owns
    // WalkPuff. Sit yawn is SitYawnPuff on idleYawn (this gate
    // still needs idlePant). Sit chew owns SitChewPuff (this
    // gate still needs idlePant; idleChew skips). Hunt recover
    // owns SitHuntPuff (this gate still needs idlePant; idleHunt
    // skips). Stalk Walk owns WalkHuntPuff. Leap owns LeapHuntPuff.
    // Sleep / Leap / rest skip. No idlePuff: Walk() would zero it.
    // 0 = the old silent sit tongue.
    public float SitPuff()
    {
        if (sitPuffAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitPuffAmp, 0f, 1.5f) * pulse;
    }

    // Picture only. Sit idlePant hung a round rest pupil after
    // EndWalk — same hunt-lock stare WalkSquint already closed on
    // the trot. Hold a thinner slit on the same 0.55 hang so the
    // eye does not flash open between pumps. Brain already wrote
    // idlePant; Walk owns WalkSquint. Hunt recover owns
    // SitHuntSquint. Hunt stalk Walk owns WalkHuntSquint.
    // Hunt leap owns LeapHuntSquint.
    // Sit chew owns SitChewSquint (wag-off meal; SitSquint skips
    // idleChew so leftover sit held the open sniff-track eye).
    // Sit chew ear is SitChewEar. Sit chew brow is SitChewBrow.
    // Look-up / Sleep / Leap / rest skip
    // (yawn / blink still own the full slit). No idleBlink:
    // Walk() would zero it. 0 = the old round sit-pant eye.
    // Pupil stays visible under the squint.
    public float SitSquint()
    {
        if (sitSquintAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitSquintAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idlePant hung a rest muzzle after EndWalk —
    // same hunt-lock snout WalkNose already closed on the trot. Hold
    // a milder drop on the same 0.55 hang so the nose does not flash
    // rest between pumps. Brain already wrote idlePant; Walk owns
    // WalkNose. Hunt recover / stalk keep idleHunt. Leap owns
    // LeapHuntNose. Sleep owns SitSleepNose. Look-up / chew /
    // rest skip (sniff keeps its own dirt drop, sit yawn owns
    // SitYawnNose, sit chew owns SitChewNose). No
    // idleNose: Walk() would zero it. 0 = the old rest sit-pant
    // snout.
    public float SitNose()
    {
        if (sitNoseAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitNoseAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idlePant hung a rest pupil after EndWalk —
    // same hunt-lock stare WalkEye already closed on the trot. Hold
    // a milder drop on the same 0.55 hang so the eye does not flash
    // rest between pumps. Brain already wrote idlePant; Walk owns
    // WalkEye. Hunt recover / stalk keep idleHunt. Leap owns
    // LeapHuntEye. Look-up / Sleep / rest skip (sniff keeps
    // its own dirt drop). Sit chew owns SitChewEye. No idleEye:
    // Walk() would zero it. 0 = the old rest sit-pant stare.
    // Pupil stays visible under the squint.
    public float SitEye()
    {
        if (sitEyeAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitEyeAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idlePant hung a rest brow after EndWalk —
    // same hunt-lock forehead WalkBrow already closed on the trot.
    // Hold a milder crease on the same 0.55 hang so the yellow box
    // does not flash rest between pumps. Brain already wrote
    // idlePant; Walk owns WalkBrow. Hunt / look-up / Sleep
    // (SitSleepBrow) / Leap / rest skip (sniff keeps its own drop,
    // sit yawn owns SitYawnBrow, sit chew owns SitChewBrow). No idleBrow:
    // Walk() would zero it. 0 = the old rest sit-pant forehead.
    public float SitBrow()
    {
        if (sitBrowAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitBrowAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idlePant hung rest whisker stamps after
    // EndWalk — same hold muzzle WalkWhisker already closed on the
    // trot. Hold a milder fan on the same 0.55 hang so the snout
    // does not flash rest between pumps. Brain already wrote
    // idlePant; Walk owns WalkWhisker. Hunt / look-up / chew /
    // Sleep / Leap / rest skip (sniff keeps its own fan, sit yawn
    // owns SitYawnWhisker, sit chew owns SitChewWhisker). No
    // idleWhisker: Walk() would zero it.
    // 0 = the old rest sit-pant stamps.
    public float SitWhisker()
    {
        if (sitWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitWhiskerAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idlePant hung a rest half-fold after
    // EndWalk — same hold head WalkEarFlop already closed on the
    // trot. Hold a milder flop on the same 0.55 hang so the ears
    // do not flash rest between pumps. Brain already wrote
    // idlePant; Walk owns WalkEarFlop. Hunt / look-up / chew /
    // Sleep / Leap / rest skip (sniff keeps its own half-fold,
    // chew owns SitChewEar, scratch keeps its kick, yawn owns
    // SitYawnEar). No idleEar:
    // Walk() would zero it. 0 = the old rest sit-pant half-fold.
    public float SitEar()
    {
        if (sitEarAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitEarAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idlePant heaved Chest Visual after EndWalk —
    // same statue rump WalkRumpBob already closed on the trot. Hold
    // a milder heave on the same 0.55 hang so the pelvis does not
    // flash rest between pumps. In phase with sit chest (a pant,
    // not a see-saw). Brain already wrote idlePant; Walk owns
    // WalkRumpBob. Sit yawn owns SitYawnRump. Sit sniff owns
    // SitSniffRump. Sit scratch owns SitScratchRump. Sit look
    // owns SitLookRump. Hunt / chew / Sleep / Leap skip.
    // Quiet sit owns SitQuietRump. Sleep owns SitSleepRump. No
    // idleRump: Walk() would zero it.
    // 0 = the old statue sit-pant rump.
    public float SitRump()
    {
        if (sitRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitRumpAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Sit idlePant pumped the jaw / chest / rump after EndWalk —
    // same statue neck walkHeadBob already closed on the trot.
    // Milder nose-down on the same 0.55 hang so the head does not
    // flash world-0 between pumps. Same SPD world target as sniff /
    // look / walkHeadBob, not a new neckP. Brain already wrote
    // idlePant. Hunt recover owns SitHuntHead. Sit chew owns
    // SitChewHead. Quiet sit owns SitQuietHead. Look-up / chew /
    // Sleep / Leap / rest skip (sniff / scratch keep their own sit
    // necks). Sleep stay hold. No idleHead: Walk() would zero it. 0 =
    // the old statue sit-pant neck.
    public float SitHead()
    {
        if (sitHeadAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitHeadAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already tucked the neck, flinched
    // the near ear, and lifted the near-hind Visual — the pupil stayed
    // on rest-Y, so the kick read as a forage stare through the ear.
    // Drop the same Walk/SitEye slot toward that paw. Pulse is the
    // kick above the 0.55 hang so the dot does not flash horizon
    // between rakes. Brain already wrote idleScratch; Walk owns
    // WalkEye, sit pant owns SitEye. Leap owns LeapHuntEye. Hunt /
    // look-up / chew / Sleep / sniff / pant / rest skip. Sleep /
    // yawn / blink still hide the dot. No idleEye: Walk() would
    // zero it. 0 = the old rest scratch stare.
    public float SitScratchEye()
    {
        if (sitScratchEyeAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchEyeAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already dropped the pupil toward
    // the kicking paw — the lid stayed the round rest, so the kick
    // still read as a wide forage stare through the ear. Slit the
    // same Walk/SitSquint pair on that kick. Pulse is the rake above
    // the 0.55 hang so the eye does not flash open between rakes.
    // Brain already wrote idleScratch; Walk owns WalkSquint, sit
    // pant owns SitSquint. Sit chew owns SitChewSquint. Hunt /
    // look-up / Sleep / sniff / pant / Leap / rest skip. Sleep /
    // yawn / blink still own the
    // full cover. Pupil stays visible under the squint. No idleBlink:
    // Walk() would zero it. 0 = the old round scratch stare.
    public float SitScratchSquint()
    {
        if (sitScratchLidAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchLidAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already dropped the pupil and
    // slit the lid — the brow stayed the rest stamp, so the kick
    // still read as a forage stare on the yellow box. Crease the
    // same Walk/SitBrow slot on that kick. Pulse is the rake above
    // the 0.55 hang so the forehead does not flash rest between
    // rakes. Brain already wrote idleScratch; Walk owns WalkBrow,
    // sit pant owns SitBrow, sit yawn owns SitYawnBrow. Sit chew
    // owns SitChewBrow. Hunt / look-up / chew / Sleep / sniff /
    // pant / Leap / rest skip. Hunt furrow stays on idleHunt. No
    // idleBrow: Walk() would zero it. 0 = the old rest scratch
    // forehead.
    public float SitScratchBrow()
    {
        if (sitScratchBrowAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchBrowAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already dropped the pupil, slit
    // the lid, and creased the brow — the whiskers stayed rest
    // stamps, so the kick still read as a forage stare on the yellow
    // box. Fan the same Walk/SitWhisker slot on that kick. Pulse is
    // the rake above the 0.55 hang so the snout does not flash rest
    // between rakes. Brain already wrote idleScratch; Walk owns
    // WalkWhisker, sit pant owns SitWhisker. Hunt / look-up / chew /
    // Sleep / sniff / pant / Leap / rest skip. Hunt pin stays on
    // idleHunt. Sniff keeps its own fan. Sit yawn owns
    // SitYawnWhisker. Sit chew owns SitChewWhisker. No idleWhisker:
    // Walk() would zero it.
    // 0 = the old rest scratch stamps.
    public float SitScratchWhisker()
    {
        if (sitScratchWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchWhiskerAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already dropped the pupil, slit
    // the lid, creased the brow, and fanned the whiskers — the snout
    // stayed the rest stamp, so the kick still read as a forage
    // stare on the yellow box. Drop the same Walk/SitNose slot toward
    // that paw. Pulse is the rake above the 0.55 hang so the muzzle
    // does not flash rest between rakes. Brain already wrote
    // idleScratch; Walk owns WalkNose, sit pant owns SitNose. Leap
    // owns LeapHuntNose. Hunt / look-up / chew / Sleep / sniff /
    // pant / rest skip. Hunt drop stays on idleHunt. Sniff keeps
    // its own dirt drop. No idleNose: Walk() would zero it. Sit
    // yawn owns SitYawnNose. 0 = the old rest scratch muzzle.
    public float SitScratchNose()
    {
        if (sitScratchNoseAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchNoseAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already dropped the pupil, slit
    // the lid, creased the brow, fanned the whiskers, and dropped the
    // snout — the lip stayed the rest stamp, so the kick still read
    // as a forage stare on the yellow box. Tuck the same Head-Visual
    // mouth toward that paw (sniff dirt drop, not hunt flews and not
    // a pant gape). Pulse is the rake above the 0.55 hang so the
    // flews do not flash rest between rakes. Brain already wrote
    // idleScratch; Walk / sit pant open via WalkPant / idlePant.
    // Quiet sit owns SitQuietMouth. Sleep owns SitSleepMouth. Hunt /
    // look-up / chew / sniff / pant / Leap / rest skip. Hunt raise
    // stays on idleHunt.
    // Sniff keeps its own dirt tuck. No idleMouth: Walk() would
    // zero it. 0 = the old rest scratch flews.
    public float SitScratchMouth()
    {
        if (sitScratchMouthAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchMouthAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleLook already weaves the neck, hops facing,
    // and lifts pupil / snout / brow — the ears stayed rest fold 0.20
    // except a binary perk at idleLook > 0.45, so the glance still
    // read as a statue head on a nodding neck. Hitch the same Head-
    // Visual ears with the signed bob (look-up perks, look-down folds).
    // Brain already wrote idleLook; Walk owns WalkEarFlop, sit pant
    // owns SitEar, scratch owns the near-ear kick, sit yawn owns
    // SitYawnEar. Hunt pin stays on idleHunt. Sniff keeps its own
    // half-fold. Sit chew owns SitChewEar. Sleep owns SitSleepEar.
    // Leap / rest skip. No idleEar:
    // Walk() would zero it. 0 = the old rest fold / binary perk.
    public float SitLookEar()
    {
        if (sitLookEarAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        return Mathf.Clamp(sitLookEarAmp, 0f, 1.5f)
            * Mathf.Clamp(idleLook, -1f, 1f);
    }

    // Picture only. Sit idleLook already weaves the neck, hops facing,
    // lifts pupil / snout / brow, and hitches the ears — the whiskers
    // stayed rest stamps, so the glance still read as a statue muzzle
    // on a nodding head. Fan the same Walk/SitWhisker slot with the
    // signed bob (look-up spreads, look-down tucks). Brain already
    // wrote idleLook; Walk owns WalkWhisker, sit pant owns SitWhisker,
    // scratch owns SitScratchWhisker, sit yawn owns SitYawnWhisker,
    // sit chew owns SitChewWhisker. Hunt pin stays on idleHunt.
    // Sniff keeps its own fan. Sleep / Leap / rest skip. No
    // idleWhisker: Walk() would zero it.
    // 0 = the old rest stamps.
    public float SitLookWhisker()
    {
        if (sitLookWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        return Mathf.Clamp(sitLookWhiskerAmp, 0f, 1.5f)
            * Mathf.Clamp(idleLook, -1f, 1f);
    }

    // Picture only. Sit idleLook already weaves the neck, hops facing,
    // lifts pupil / snout / brow, hitches the ears, and fans the
    // whiskers — the lid stayed the round rest, so look-down still
    // read as a wide forage stare on a nodding head. Slit the same
    // Walk/SitSquint slot on the signed bob (look-down slits, look-up
    // stays open). Brain already wrote idleLook; Walk owns WalkSquint,
    // sit pant owns SitSquint, scratch owns SitScratchSquint. Hunt
    // recover owns SitHuntSquint. Hunt stalk Walk owns WalkHuntSquint.
    // Hunt leap owns LeapHuntSquint.
    // Sit chew owns SitChewSquint.
    // Sniff keeps the open track eye.
    // Sleep / yawn / blink still own the full cover. Pupil stays
    // visible under the squint. Sleep / Leap / rest skip. No
    // idleBlink: Walk() would zero it. 0 = the old round look eye.
    public float SitLookSquint()
    {
        if (sitLookLidAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleLook >= -0.01f)
            return 0f;
        return Mathf.Clamp(sitLookLidAmp, 0f, 1.5f)
            * Mathf.Clamp(-idleLook, 0f, 1f);
    }

    // Picture only. Sit idleLook already weaves the neck, hops facing,
    // lifts pupil / snout / brow, hitches the ears, fans the whiskers,
    // and slits look-down — the lip stayed the rest stamp (tiny
    // look×0.005 / 4°), so the glance still read as a forage stare
    // on the yellow box. Hitch the same Head-Visual mouth with the
    // signed bob (look-up lifts with the nose, look-down tucks like
    // sniff). Brain already wrote idleLook; Walk / sit pant open via
    // WalkPant / idlePant. Scratch owns SitScratchMouth. Sit yawn
    // owns SitYawnMouth. Sit chew owns SitChewMouth. Quiet sit
    // owns SitQuietMouth. Sleep owns SitSleepMouth. Hunt raise
    // stays on idleHunt. Sniff keeps its own dirt tuck. Pant /
    // yawn / chew still open with DriveJaw. Leap / rest skip. No
    // idleMouth: Walk() would zero it. 0 = the old rest look flews.
    public float SitLookMouth()
    {
        if (sitLookMouthAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        return Mathf.Clamp(sitLookMouthAmp, 0f, 1.5f)
            * Mathf.Clamp(idleLook, -1f, 1f);
    }

    // Same first-hinge spring as sit idleWag. Sit idleLook already
    // weaves the neck, hops facing, and hitches the face — leftover
    // sit held the 8.5 Hz forage metronome, so the glance still read
    // as a nodding statue with a sit wag. Signed: look-up perks
    // (minus, same as hunt raise), look-down droops. Brain already
    // wrote idleLook; Walk owns WalkTail, sit pant owns SitPantTail.
    // Sit sniff owns SitSniffTail. Sit scratch owns SitScratchTail.
    // Sit yawn owns SitYawnTail. Sit chew owns SitChewTail. Distal
    // limp is SitLookDistalTail. Hunt raise stays on idleHunt.
    // Sleep / Leap / rest skip. Quiet sit between acts keeps
    // 8.5 Hz. No idleTail: Walk() would zero it. 0 = the old
    // look metronome.
    public float SitLookTail()
    {
        if (sitLookTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        return Mathf.Clamp(sitLookTailAmp, 0f, 1.5f)
            * Mathf.Clamp(idleLook, -1f, 1f);
    }

    // Picture only. Sit look already hitches the first hinge
    // (SitLookTail) and weaves neck / face / chest / rump —
    // leftover sit held Tail2–Tail5 on TreeSpring restAngle 0
    // (muscles only on the first hinge — by design) so the glance
    // still read as a stiff caudal rod on a nodding neck. Limp
    // the distal Visuals (accumulating 6°…12°) following
    // idleLook's signed bob (env already weaves ~2.2 Hz; no extra
    // clock, not Sleep's 1.6 hitch, not sniff's ~3 Hz bob, not
    // yawn's envelope, not pant's 6.5 Hz pump, not chew's 11 Hz
    // snap, and not a new distal muscle). PoseVisual plus droops;
    // IdleTailTarget perks look-up with a minus on SitLookTail, so
    // this env is negated (look-up perks, look-down droops).
    // Colliders stay. Brain already wrote idleLook; Walk owns
    // WalkTail. Sit sniff distal is SitSniffDistalTail. Sit yawn
    // distal is SitYawnDistalTail. Sit pant distal is
    // SitPantDistalTail. Sit chew distal is SitChewDistalTail.
    // Sit scratch distal is SitScratchDistalTail. Hunt recover
    // distal is SitHuntDistalTail. Hunt stalk distal is
    // WalkHuntDistalTail. Hunt leap owns LeapHuntTail. Hunt leap
    // distal is LeapHuntDistalTail. Sleep distal is
    // SitSleepDistalTail. Quiet sit distal is SitQuietDistalTail.
    // No idleTail: Walk() would zero it. 0 = the old rest-spring
    // look stick.
    public float SitLookDistalTail()
    {
        if (sitLookDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        return -Mathf.Clamp(sitLookDistalTailAmp, 0f, 1.5f)
            * Mathf.Clamp(idleLook, -1f, 1f);
    }

    // Picture only. Sit idleLook already weaves the neck, hops facing,
    // hitches the face, and hitches the tail — leftover sit held
    // quiet-sit 0.0018 (BreathLift only reads idlePant / Sleep /
    // walk / idleYawn / idleSniff / idleScratch) so the glance still
    // read as a statue chest on a nodding neck. Hitch with the signed
    // bob (look-up lifts, look-down settles). Not a 6.5 Hz pant and
    // not the quiet sin. Brain already wrote idleLook; Walk owns
    // WalkTrunkBob. Sit pant owns the idlePant branch in BreathLift.
    // Sit yawn owns SitYawnBreath. Sit sniff owns SitSniffBreath.
    // Sit scratch owns SitScratchBreath. Hunt recover owns
    // SitHuntBreath. Sleep / Leap / rest skip. Quiet sit between
    // acts stays the 0.0018 sin. Sit look rump is SitLookRump. No
    // idleBreath: Walk() would zero it. 0 = the old quiet-sit
    // look chest.
    public float SitLookBreath()
    {
        if (sitLookBreathAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        return Mathf.Clamp(sitLookBreathAmp, 0f, 1.5f)
            * Mathf.Clamp(idleLook, -1f, 1f);
    }

    // Picture only. Sit idleLook already weaves the neck, hops facing,
    // hitches the face, hitches the tail, and heaves Chest Visual —
    // leftover sit held a statue rump (ApplyRump only reads
    // WalkRumpBob / SitRump / SitYawnRump / SitSniffRump /
    // SitScratchRump) so the glance still read as a chest-only bob
    // on a nodding neck. Hitch with the signed bob (look-up lifts,
    // look-down settles). In phase with SitLookBreath (a glance,
    // not a see-saw, not a 6.5 Hz pant). Brain already wrote
    // idleLook; Walk owns WalkRumpBob. Sit pant owns SitRump.
    // Sit yawn owns SitYawnRump. Sit sniff owns SitSniffRump.
    // Sit scratch owns SitScratchRump. Hunt recover owns
    // SitHuntRump. Sit chew owns SitChewRump. Quiet sit owns
    // SitQuietRump. Sleep owns SitSleepRump. Sleep / Leap / rest
    // skip. No idleRump: Walk() would zero it.
    // 0 = the old statue look rump.
    public float SitLookRump()
    {
        if (sitLookRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        return Mathf.Clamp(sitLookRumpAmp, 0f, 1.5f)
            * Mathf.Clamp(idleLook, -1f, 1f);
    }

    // Picture only. Hunt recover sit already snarls, pins, drops
    // the nose, raises hackles, and plants the near-front paw —
    // leftover sit held quiet-sit 0.0018 (BreathLift only reads
    // idlePant / Sleep / walk / idleYawn / idleSniff / idleScratch
    // / idleLook) so the 0.70 s coil still read as a statue chest
    // on a live snarl. Held heave with idleHunt (still coil, not
    // a 6.5 Hz pant and not the quiet sin). Brain already wrote
    // idleHunt; Walk owns WalkTrunkBob (stalk stays still). Sit
    // pant owns the idlePant branch in BreathLift. Sit yawn owns
    // SitYawnBreath. Sit sniff owns SitSniffBreath. Sit chew owns
    // SitChewBreath. Sit scratch owns SitScratchBreath. Sit look
    // owns SitLookBreath. Alert stare is idleLook, not this.
    // Sleep / rest skip. Hunt leap owns LeapHuntBreath.
    // Quiet sit between acts stays the 0.0018 sin. Hunt recover
    // rump is SitHuntRump. Hunt recover neck is SitHuntHead.
    // No idleBreath: Walk() would zero it.
    // 0 = the old quiet-sit recover chest.
    public float SitHuntBreath()
    {
        if (sitHuntBreathAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return Mathf.Clamp(sitHuntBreathAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt recover sit already snarls, pins, drops
    // the nose, raises hackles, plants the near-front paw, and
    // heaves Chest Visual — leftover sit held a statue rump
    // (ApplyRump only reads WalkRumpBob / SitRump / SitYawnRump /
    // SitSniffRump / SitScratchRump / SitLookRump) so the 0.70 s
    // coil still read as a chest-only hitch on a live snarl. Held
    // hitch with idleHunt (still coil, not a 6.5 Hz pant, not a
    // see-saw). In phase with SitHuntBreath. Brain already wrote
    // idleHunt; Walk owns WalkRumpBob (stalk stays still). Sit
    // pant owns SitRump. Sit yawn owns SitYawnRump. Sit sniff
    // owns SitSniffRump. Sit scratch owns SitScratchRump. Sit
    // look owns SitLookRump. Sit chew owns SitChewRump. Alert stare is idleLook, not this.
    // Quiet sit owns SitQuietRump. Sleep owns SitSleepRump. Hunt leap owns LeapHuntRump.
    // Sleep / Leap / rest skip. Hunt recover neck is SitHuntHead.
    // No idleRump: Walk() would zero it. 0 = the old statue recover rump.
    public float SitHuntRump()
    {
        if (sitHuntRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return Mathf.Clamp(sitHuntRumpAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Sit hunt recover already snarls, pins, drops the nose,
    // raises hackles, plants the near-front paw, and heaves
    // Chest / Pelvis Visual — leftover sit held world 0
    // (IdleNeckWorldTarget only reads sleep / sniff / scratch /
    // look / yawn / hop / SitHead; SitHead skips idleHunt) so
    // the 0.70 s coil still read as a statue head on a live
    // snarl. Same 12° nose-down as stalk WalkNeckWorldTarget
    // (held coil, not a 6.5 Hz pant and not SitHead's 3°
    // pump). Brain already wrote idleHunt; Walk owns
    // WalkNeckWorldTarget (stalk already drops). Sit pant owns
    // SitHead. Sit chew owns SitChewHead. Quiet sit owns
    // SitQuietHead. Sit look / sniff / scratch / yawn keep their
    // own sit necks. Alert stare is idleLook, not this.
    // Sleep stay hold. Sleep / Leap / rest skip. Hunt recover lid
    // is SitHuntSquint. Hunt stalk lid is WalkHuntSquint. Hunt
    // leap lid is LeapHuntSquint. No idleHead: Walk() would zero
    // it. 0 = the old statue recover neck.
    public float SitHuntHead()
    {
        if (sitHuntHeadAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return Mathf.Clamp(sitHuntHeadAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt recover sit already snarls, pins, drops
    // the nose and pupil, raises hackles, plants the near-front
    // paw, heaves Chest / Pelvis Visual, and drops the SPD neck
    // — leftover sit held the round rest (SitSquint / SitLookSquint
    // / SitScratchSquint all skip idleHunt) so the 0.70 s coil
    // still read as a wide forage stare on a live snarl. Held
    // slit with idleHunt (still coil, not a 6.5 Hz pant and not
    // SitSquint's pump). Same Walk/SitSquint pair. Brain already
    // wrote idleHunt; stalk Walk owns WalkHuntSquint. Leap owns
    // LeapHuntSquint (LeapAttack zeros idleHunt). Sit pant
    // owns SitSquint. Sit look owns SitLookSquint (look-down
    // only). Sit scratch owns SitScratchSquint. Sit chew owns
    // SitChewSquint. Alert stare is
    // idleLook, not this. Sleep / yawn / blink still own the
    // full cover. Pupil stays visible under the squint. Sleep /
    // rest skip. No idleBlink: Walk() would zero it. 0 =
    // the old round recover eye.
    public float SitHuntSquint()
    {
        if (sitHuntLidAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return Mathf.Clamp(sitHuntLidAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt recover sit already snarls, pins, drops
    // the nose, raises hackles, plants the near-front paw, heaves
    // chest / rump, drops the neck, and slits the lid — leftover
    // sit held silent (SitPuff / SitYawnPuff / SitChewPuff skip
    // idleHunt; fangs own the Jaw-Visual teeth slot) so the
    // 0.70 s coil still read as a silent crack on a live snarl.
    // Held puff with idleHunt (still coil, not a 6.5 Hz pant).
    // Needs the hunt crack (DriveJaw already opens 0.20) so a
    // closed mouth does not leak vapor. Same PantPuff child,
    // cranial of the fangs. Brain already wrote idleHunt; Walk
    // forage owns WalkPuff. Stalk Walk owns WalkHuntPuff. Leap
    // owns LeapHuntPuff (LeapAttack zeros idleHunt). Sit pant
    // owns SitPuff. Sit yawn owns SitYawnPuff. Sit chew owns
    // SitChewPuff. Alert stare is idleLook, not this. Bark puff
    // is Call. Tongue stays off (fangs). Sleep / Leap / rest
    // skip. No idlePuff: Walk() would zero it. 0 = the old silent
    // recover snarl.
    public float SitHuntPuff()
    {
        if (sitHuntPuffAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return Mathf.Clamp(sitHuntPuffAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt recover sit already raises the first
    // hinge (IdleTailTarget idleHunt, minus 16°) and coils lid /
    // pupil / nose / puff / chest / rump / neck — leftover sit
    // held Tail2–Tail5 on TreeSpring restAngle 0 (muscles only
    // on the first hinge — by design) so the 0.70 s coil still
    // read as a stiff caudal rod on a live snarl. Perk the distal
    // Visuals (accumulating 6°…12°) following that same held
    // idleHunt raise (env already coils; no extra clock, not
    // Sleep's 1.6 hitch, not sniff's ~3 Hz bob, not yawn's
    // envelope, not pant's 6.5 Hz pump, not chew's 11 Hz snap,
    // not look's signed bob, not scratch's ~14 Hz rake, not
    // quiet sit's 8.5 Hz, not WalkTail's two-dip, not
    // LeapHuntTail's IsLeap 0.85, and not a new distal muscle).
    // PoseVisual plus droops; IdleTailTarget perks with a minus
    // on idleHunt, so this env is negated (held raise, same
    // idleHunt as the hinge). Colliders stay. Brain already
    // wrote idleHunt; stalk Walk owns WalkHuntDistalTail. Leap
    // owns LeapHuntDistalTail (LeapAttack zeros idleHunt).
    // Forage Walk distal is WalkDistalTail. Quiet sit distal is
    // SitQuietDistalTail. Sleep distal is SitSleepDistalTail.
    // No idleTail: Walk() would zero it. 0 = the old rest-spring
    // recover sit stick.
    public float SitHuntDistalTail()
    {
        if (sitHuntDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return -Mathf.Clamp(sitHuntDistalTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt stalk Walk already drops the pupil, nose,
    // and SPD neck, pins ears, raises the tail, and snarls —
    // leftover walk held the round rest (WalkSquint skips idleHunt;
    // SitHuntSquint skips Walk) so the chase still read as a wide
    // forage stare on a live snarl. Held slit with idleHunt (still
    // coil, not a 6.5 Hz pant and not WalkSquint's two-dip pump).
    // Same Walk/SitSquint pair. Brain already wrote idleHunt;
    // recover sit owns SitHuntSquint. Leap owns LeapHuntSquint
    // (LeapAttack zeros idleHunt). Forage Walk owns WalkSquint.
    // Sit pant owns SitSquint. Alert stare is idleLook, not this.
    // Sleep / yawn / blink still own the full cover. Pupil stays
    // visible under the squint. CLI -dogWalk leaves idleHunt 0.
    // Hop keeps the coil (not a forage mute). Sleep / rest skip.
    // No idleBlink: Walk() would zero it. 0 = the old round
    // stalk eye.
    public float WalkHuntSquint()
    {
        if (walkHuntLidAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return Mathf.Clamp(walkHuntLidAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt stalk Walk already drops the pupil, nose,
    // and SPD neck, pins ears, raises the tail, slits the lid, and
    // snarls — leftover walk held silent (WalkPuff skips idleHunt
    // because WalkPant skips idleHunt; SitHuntPuff skips Walk) so
    // the chase still read as a silent crack on a live snarl.
    // Held puff with idleHunt (still coil, not a 6.5 Hz pant and
    // not WalkPuff's two-dip pump). Needs the hunt crack (DriveJaw
    // already opens 0.20) so a closed mouth does not leak vapor.
    // Same PantPuff child, cranial of the fangs. Brain already
    // wrote idleHunt; recover sit owns SitHuntPuff. Leap owns
    // LeapHuntPuff (LeapAttack zeros idleHunt; this gate skips
    // IsLeap). Forage Walk owns WalkPuff. Sit pant owns SitPuff.
    // Sit yawn owns SitYawnPuff. Sit chew owns SitChewPuff. Alert
    // stare is idleLook, not this. Bark puff is Call. Tongue stays
    // off (fangs). CLI -dogWalk leaves idleHunt 0. Hop keeps the
    // coil (not a forage mute). Sleep / rest skip. No idlePuff:
    // Walk() would zero it. 0 = the old silent stalk snarl.
    public float WalkHuntPuff()
    {
        if (walkHuntPuffAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return Mathf.Clamp(walkHuntPuffAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt stalk Walk already raises the first
    // hinge (IdleTailTarget idleHunt, minus 16°) and coils lid /
    // pupil / nose / puff / neck — leftover walk held Tail2–Tail5
    // on TreeSpring restAngle 0 (muscles only on the first hinge
    // — by design) so the chase still read as a stiff caudal rod
    // on a live snarl. Perk the distal Visuals (accumulating
    // 6°…12°) following that same held idleHunt raise (env
    // already coils; no extra clock, not Sleep's 1.6 hitch, not
    // sniff's ~3 Hz bob, not yawn's envelope, not pant's 6.5 Hz
    // pump, not chew's 11 Hz snap, not look's signed bob, not
    // scratch's ~14 Hz rake, not quiet sit's 8.5 Hz, not
    // WalkTail's two-dip, not LeapHuntTail's IsLeap 0.85, and
    // not a new distal muscle). PoseVisual plus droops;
    // IdleTailTarget perks with a minus on idleHunt, so this env
    // is negated (held raise, same idleHunt as the hinge). Hop
    // keeps the coil (not a forage mute). Colliders stay. Brain
    // already wrote idleHunt; recover sit owns SitHuntDistalTail.
    // Leap owns LeapHuntDistalTail (LeapAttack zeros idleHunt).
    // Forage Walk distal is WalkDistalTail. CLI -dogWalk leaves
    // idleHunt 0. Sleep distal is SitSleepDistalTail. No
    // idleTail: Walk() would zero it. 0 = the old rest-spring
    // stalk Walk stick.
    public float WalkHuntDistalTail()
    {
        if (walkHuntDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return -Mathf.Clamp(walkHuntDistalTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt leap already furrows the brow, pins
    // ears, raises hackles, and snarls — leftover leap held the
    // round rest (WalkHuntSquint / SitHuntSquint skip IsLeap;
    // LeapAttack zeros idleHunt) so the pounce still read as a
    // wide forage stare on a live snarl. Held slit on IsLeap
    // (still coil, same 0.85 as stalk huntStalk, not a 6.5 Hz
    // pant and not WalkSquint's two-dip pump). Same Walk/SitSquint
    // pair. Body already wrote IsLeap; recover sit owns
    // SitHuntSquint. Stalk Walk owns WalkHuntSquint. Forage Walk
    // owns WalkSquint. Sit pant owns SitSquint. Alert stare is
    // idleLook, not this. Sleep / yawn / blink still own the
    // full cover. Pupil drop is LeapHuntEye (same leftover:
    // idleHunt 0, WalkEye/SitEye skip IsLeap). Snout drop is
    // LeapHuntNose (same leftover: idleHunt 0, WalkNose/SitNose
    // skip IsLeap). CLI -dogLeap
    // still slits from IsLeap(). Sleep / rest skip. No
    // idleBlink: Walk() / LeapAttack would zero it. 0 = the
    // old round leap eye.
    public float LeapHuntSquint()
    {
        if (leapHuntLidAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return Mathf.Clamp(leapHuntLidAmp, 0f, 1.5f) * 0.85f;
    }

    // Picture only. Hunt leap already furrows the brow and slits
    // the lid — leftover leap held rest-Y (WalkEye / SitEye skip
    // IsLeap; LeapAttack zeros idleHunt) so the pounce still read
    // as a forage stare through a live snarl slit. Held drop on
    // IsLeap (still coil, same 0.85 as stalk huntStalk, not a
    // 6.5 Hz pant and not WalkEye's two-dip pump). Same
    // Walk/SitEye pair. Body already wrote IsLeap; recover sit /
    // stalk Walk keep idleHunt. Forage Walk owns WalkEye. Sit
    // pant owns SitEye. Alert stare is idleLook, not this. Sleep
    // / yawn / blink still hide the dot. Snout drop is LeapHuntNose
    // (same leftover: idleHunt 0, WalkNose/SitNose skip IsLeap). CLI
    // -dogLeap still drops from IsLeap(). Sleep / rest skip. No
    // idleEye: Walk() / LeapAttack would zero it. 0 = the old rest-Y
    // leap stare.
    public float LeapHuntEye()
    {
        if (leapHuntEyeAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return Mathf.Clamp(leapHuntEyeAmp, 0f, 1.5f) * 0.85f;
    }

    // Picture only. Hunt leap already drops the pupil, slits the
    // lid, furrows the brow, and raises the flews — leftover leap
    // held the rest muzzle (WalkNose / SitNose skip IsLeap;
    // LeapAttack zeros idleHunt) so the pounce still read as a
    // forage snout on a live snarl. Held drop on IsLeap (still
    // coil, same 0.85 as stalk huntStalk, not a 6.5 Hz pant and
    // not WalkNose's two-dip pump). Same Walk/SitNose pair. Body
    // already wrote IsLeap; recover sit / stalk Walk keep
    // idleHunt. Forage Walk owns WalkNose. Sit pant owns SitNose.
    // Alert stare is idleLook, not this. Sleep owns SitSleepNose
    // (sprite stays on; lids own the eye). CLI -dogLeap still
    // drops from IsLeap(). Sleep / rest skip. No idleNose:
    // Walk() / LeapAttack would zero it. 0 = the old rest leap
    // muzzle.
    public float LeapHuntNose()
    {
        if (leapHuntNoseAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return Mathf.Clamp(leapHuntNoseAmp, 0f, 1.5f) * 0.85f;
    }

    // Picture only. Hunt leap already drops the pupil, slits the
    // lid, furrows the brow, drops the snout, and snarls —
    // leftover leap held silent (WalkHuntPuff / SitHuntPuff skip
    // IsLeap; LeapAttack zeros idleHunt; fangs own the Jaw-Visual
    // teeth slot) so the pounce still read as a silent crack on a
    // live snarl. Held puff on IsLeap (still coil, same 0.85 as
    // stalk huntStalk, not a 6.5 Hz pant and not WalkPuff's
    // two-dip pump). Needs the leap crack (DriveJaw already opens
    // 0.28 coil / 0.40 air) so a closed mouth does not leak vapor.
    // Same PantPuff child, cranial of the fangs. Body already
    // wrote IsLeap; recover sit owns SitHuntPuff. Stalk Walk owns
    // WalkHuntPuff. Forage Walk owns WalkPuff. Sit pant owns
    // SitPuff. Sit yawn owns SitYawnPuff. Sit chew owns
    // SitChewPuff. Alert stare is idleLook, not this. Bark puff
    // is Call. Tongue stays off (fangs). CLI -dogLeap still puffs
    // from IsLeap(). Sleep / rest skip. Chest heave is
    // LeapHuntBreath. No idlePuff: Walk() /
    // LeapAttack would zero it. 0 = the old silent leap snarl.
    public float LeapHuntPuff()
    {
        if (leapHuntPuffAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return Mathf.Clamp(leapHuntPuffAmp, 0f, 1.5f) * 0.85f;
    }

    // Picture only. Hunt leap already slits the lid, drops the
    // pupil, drops the snout, and puffs — leftover leap held a
    // statue chest (BreathLift / SitHuntBreath skip IsLeap;
    // LeapAttack zeros idleHunt) so the pounce still read as a
    // quiet-sit 0.0018 wobble on a live snarl. Held hitch on
    // IsLeap (still coil, same 0.85 as stalk huntStalk, not a
    // 6.5 Hz pant and not the quiet sin). Same Chest Visual as
    // SitHuntBreath. Body already wrote IsLeap; recover sit owns
    // SitHuntBreath. Stalk Walk stays still (WalkTrunkBob on
    // idleWag only). Forage sit pant owns the idlePant branch in
    // BreathLift. Sit yawn owns SitYawnBreath. Sit sniff owns
    // SitSniffBreath. Sit chew owns SitChewBreath. Sit scratch
    // owns SitScratchBreath. Sit look owns SitLookBreath. Alert
    // stare is idleLook, not this. Sleep / rest skip. Quiet sit
    // between acts stays the 0.0018 sin. Leap rump is LeapHuntRump.
    // CLI -dogLeap still heaves from
    // IsLeap(). No idleBreath: Walk() / LeapAttack would zero it.
    // 0 = the old statue leap chest.
    public float LeapHuntBreath()
    {
        if (leapHuntBreathAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return Mathf.Clamp(leapHuntBreathAmp, 0f, 1.5f) * 0.85f;
    }

    // Picture only. Hunt leap already slits the lid, drops the
    // pupil, drops the snout, puffs, and heaves Chest Visual —
    // leftover leap held a statue rump (ApplyRump / SitHuntRump
    // skip IsLeap; LeapAttack zeros idleHunt) so the pounce still
    // read as a chest-only hitch on a live snarl. Held hitch on
    // IsLeap (still coil, same 0.85 as stalk huntStalk, not a
    // 6.5 Hz pant, not a see-saw). Same Pelvis Visual as
    // SitHuntRump. In phase with LeapHuntBreath. Body already
    // wrote IsLeap; recover sit owns SitHuntRump. Stalk Walk
    // stays still (WalkRumpBob on idleWag only). Forage sit
    // pant owns SitRump. Sit yawn owns SitYawnRump. Sit sniff
    // owns SitSniffRump. Sit chew owns SitChewRump. Sit scratch
    // owns SitScratchRump. Sit look owns SitLookRump. Alert
    // stare is idleLook, not this. Quiet sit owns SitQuietRump.
    // Sleep owns SitSleepRump. Sleep / rest skip. CLI -dogLeap still heaves
    // from IsLeap(). Leap tail is LeapHuntTail. No idleRump:
    // Walk() / LeapAttack would zero it. 0 = the old statue leap rump.
    public float LeapHuntRump()
    {
        if (leapHuntRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return Mathf.Clamp(leapHuntRumpAmp, 0f, 1.5f) * 0.85f;
    }

    // Same first-hinge spring as sit idleHunt. Hunt leap already
    // slits the lid, drops the pupil, drops the snout, puffs, and
    // heaves Chest / Pelvis Visual — leftover leap held rest
    // (DriveLeap pins tailBaseAngle; hunt raise lives on idleHunt;
    // LeapAttack zeros idleHunt) so the pounce still read as a
    // rest tail on a live snarl. Held raise on IsLeap (still coil,
    // same 0.85 as stalk huntStalk, minus, not an 8.5 Hz wag and
    // not a 6.5 Hz pant). Same 16° as IdleTailTarget hunt. Body
    // already wrote IsLeap; recover sit / stalk Walk keep idleHunt.
    // Forage Walk owns WalkTail. Sit look owns SitLookTail. Sit
    // sniff owns SitSniffTail. Sit scratch owns SitScratchTail.
    // Sit yawn owns SitYawnTail. Sit pant owns SitPantTail. Sit
    // chew owns SitChewTail. Alert stare is idleLook, not this.
    // Sleep owns SitSleepTail. Distal Tail2–Tail5 picture limp is
    // LeapHuntDistalTail. Rest skip. CLI -dogLeap still raises
    // from IsLeap(). No idleTail: Walk() / LeapAttack would zero
    // it. 0 = the old rest leap tail.
    public float LeapHuntTail()
    {
        if (leapHuntTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return Mathf.Clamp(leapHuntTailAmp, 0f, 1.5f) * 0.85f;
    }

    // Picture only. Hunt leap already raises the first hinge
    // (LeapHuntTail) and coils lid / pupil / nose / puff / chest /
    // rump — leftover leap held Tail2–Tail5 on TreeSpring restAngle
    // 0 (muscles only on the first hinge — by design) so the
    // pounce still read as a stiff caudal rod on a live snarl.
    // Perk the distal Visuals (accumulating 6°…12°) following that
    // same held 0.85 raise (env already coils; no extra clock, not
    // Sleep's 1.6 hitch, not sniff's ~3 Hz bob, not yawn's
    // envelope, not pant's 6.5 Hz pump, not chew's 11 Hz snap, not
    // look's signed bob, not scratch's ~14 Hz rake, not quiet
    // sit's 8.5 Hz, not WalkTail's two-dip, and not a new distal
    // muscle). PoseVisual plus droops; DriveLeap perks with a
    // minus on LeapHuntTail, so this env is negated (held raise,
    // same 0.85 as stalk huntStalk). Colliders stay. Body already
    // wrote IsLeap; recover sit owns SitHuntDistalTail. Stalk Walk
    // owns WalkHuntDistalTail. Forage Walk distal is WalkDistalTail.
    // Quiet sit distal is SitQuietDistalTail. Sleep distal is
    // SitSleepDistalTail. CLI -dogLeap still perks from IsLeap().
    // No idleTail: Walk() / LeapAttack would zero it. 0 = the old
    // rest-spring leap stick.
    public float LeapHuntDistalTail()
    {
        if (leapHuntDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return -Mathf.Clamp(leapHuntDistalTailAmp, 0f, 1.5f) * 0.85f;
    }

    // Same first-hinge spring as sit idleHunt, opposite sign.
    // Night Sleep already drops the chin, shuts the lids, and
    // folds the ears — leftover sit held rest (TickSleep zeros
    // idleWag; IdleTailTarget falls to tailBaseAngle) so the hold
    // still read as an alert tail on a sleeping face. Held droop
    // (plus, limp, same 12° as sit sniff / yawn, not an 8.5 Hz wag
    // and not a 6.5 Hz pant). Distal Tail2–Tail5 stay rest spring
    // in physics (muscles only on the first hinge); picture limp
    // is SitSleepDistalTail. Brain already wrote idleSleep; Walk
    // owns WalkTail. Sit look
    // owns SitLookTail. Sit sniff owns SitSniffTail. Sit scratch
    // owns SitScratchTail. Sit yawn owns SitYawnTail. Sit pant
    // owns SitPantTail. Sit chew owns SitChewTail. Hunt raise
    // stays on idleHunt. Hunt leap owns LeapHuntTail. Sleep rump
    // is SitSleepRump. Quiet sit distal is SitQuietDistalTail. No
    // idleTail: Walk() would zero it. 0 = the old rest sleep tail.
    public float SitSleepTail()
    {
        if (sitSleepTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Night Sleep already drops the chin, shuts the
    // lids, folds the ears, droops the tail, and heaves Chest
    // Visual (BreathLift 0.0024 slow sin) — leftover sit held a
    // statue rump (ApplyRump only reads WalkRumpBob / SitRump /
    // SitYawnRump / SitSniffRump / SitScratchRump / SitLookRump /
    // SitHuntRump / SitChewRump / LeapHuntRump) so the hold still
    // read as a chest-only hitch on a sleeping face. Hitch with
    // the sleep breath (same 1.6 clock, in phase, not a see-saw,
    // not a 6.5 Hz pant and not the quiet sin). Brain already wrote
    // idleSleep; Walk owns WalkRumpBob. Sit pant owns SitRump.
    // Sit yawn owns SitYawnRump. Sit sniff owns SitSniffRump.
    // Sit scratch owns SitScratchRump. Sit look owns SitLookRump.
    // Sit chew owns SitChewRump. Hunt recover owns SitHuntRump.
    // Hunt leap owns LeapHuntRump. Sleep nose is SitSleepNose.
    // Sleep ear is SitSleepEar. Quiet sit owns SitQuietRump.
    // Distal Tail2–Tail5 stay rest spring. No idleRump: Walk()
    // would zero it. 0 = the old statue sleep rump.
    public float SitSleepRump()
    {
        if (sitSleepRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepRumpAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Night Sleep already drops the chin, shuts the
    // lids, folds the ears, droops the tail, heaves Chest Visual,
    // hitches the rump, and droops mouth / whisker / brow —
    // leftover sit held the rest muzzle (ApplyNose never read
    // idleSleep; TickSleep zeros idleWag; lids own the pupil, not
    // the snout) so the hold still read as a forage snout on a
    // sleeping face. Held drop (milder than sniff dirt, same 0.005
    // as sit pant, not a 6.5 Hz pant and not WalkNose's two-dip
    // pump). Sprite stays on (lids own the eye). Brain already
    // wrote idleSleep; Walk owns WalkNose. Sit pant owns SitNose.
    // Sit yawn owns SitYawnNose. Sit chew owns SitChewNose. Hunt
    // drop stays on idleHunt. Hunt leap owns LeapHuntNose. Sleep
    // ear is SitSleepEar. Quiet sit keeps rest. Distal Tail2–Tail5
    // stay rest spring. No idleNose: Walk() would zero it. 0 = the
    // old rest sleep muzzle.
    public float SitSleepNose()
    {
        if (sitSleepNoseAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepNoseAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Night Sleep already drops the chin, shuts the
    // lids, folds the ears to 0.88, droops the tail, heaves Chest
    // Visual, hitches the rump, drops the snout, and droops mouth /
    // whisker / brow — leftover sit held statue fold 0.88
    // (EarFoldWanted returns 0.88 on idleSleep; TickSleep zeros
    // idleWag) so the hold still read as statue ears on a sleeping
    // face. Hitch with the sleep breath (same 1.6 clock as
    // BreathLift / SitSleepRump, in phase, fold 0.88 ± 0.06, not a
    // 6.5 Hz pant and not WalkEarFlop). Brain already wrote
    // idleSleep; Walk owns WalkEarFlop. Sit pant owns SitEar.
    // Sit look owns SitLookEar. Sit yawn owns SitYawnEar. Sit chew
    // owns SitChewEar. Scratch keeps its kick. Hunt pin stays on
    // idleHunt. Hunt leap keeps the leap pin. Quiet sit owns
    // SitQuietEar. Distal Tail2–Tail5 stay rest spring. No idleEar:
    // Walk() would zero it. 0 = the old statue sleep fold 0.88.
    public float SitSleepEar()
    {
        if (sitSleepEarAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepEarAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Night Sleep already drops the chin, shuts the
    // lids, folds the ears, droops the tail, heaves Chest / Pelvis
    // Visual, drops the snout, hitches the ears with the breath,
    // and droops mouth / whisker / brow — leftover sit held the
    // statue sleep droop (ApplyBrows sleep*4 / y −0.003 / ang +10;
    // TickSleep zeros idleWag) so the hold still read as statue
    // brows on a sleeping face. Hitch with the sleep breath (same
    // 1.6 clock as BreathLift / SitSleepRump / SitSleepEar, in
    // phase, droop ± a small lift, not a 6.5 Hz pant and not
    // WalkBrow). Brain already wrote idleSleep; Walk owns WalkBrow.
    // Sit pant owns SitBrow. Sit yawn owns SitYawnBrow. Sit chew
    // owns SitChewBrow. Scratch owns SitScratchBrow. Hunt furrow
    // stays on idleHunt. Sleep mouth owns SitSleepMouth. Sleep
    // whisker owns SitSleepWhisker. Quiet sit owns SitQuietBrow.
    // Distal Tail2–Tail5 stay rest spring. No idleBrow: Walk()
    // would zero it. 0 = the old statue sleep droop.
    public float SitSleepBrow()
    {
        if (sitSleepBrowAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepBrowAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Night Sleep already drops the chin, shuts the
    // lids, folds the ears, droops the tail, heaves Chest / Pelvis
    // Visual, drops the snout, hitches the ears with the breath,
    // and hitches the brows — leftover sit held the statue sleep
    // droop (ApplyMouth sleep*4 / y −0.004 / ang −12; TickSleep
    // zeros idleWag) so the hold still read as statue flews on a
    // sleeping face. Hitch with the sleep breath (same 1.6 clock
    // as BreathLift / SitSleepRump / SitSleepEar / SitSleepBrow,
    // in phase, droop ± a small lift, not a 6.5 Hz pant, not a
    // hunt raise, and not SitLookMouth / SitYawnMouth). Brain
    // already wrote idleSleep; Walk / sit pant open via WalkPant /
    // idlePant. Scratch owns SitScratchMouth. Look owns
    // SitLookMouth. Sit yawn owns SitYawnMouth. Sit chew owns
    // SitChewMouth. Hunt raise stays on idleHunt. Sniff keeps
    // its own dirt tuck. Pant / yawn / chew still open with
    // DriveJaw. Quiet sit owns SitQuietMouth. Sleep whisker owns
    // SitSleepWhisker. Distal Tail2–Tail5 stay rest spring. No
    // idleMouth: Walk() would zero it. 0 = the old statue sleep
    // droop.
    public float SitSleepMouth()
    {
        if (sitSleepMouthAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepMouthAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Night Sleep already drops the chin, shuts the
    // lids, folds the ears, droops the tail, heaves Chest / Pelvis
    // Visual, drops the snout, and hitches the ears / brows /
    // flews with the breath — leftover sit held the statue sleep
    // droop (ApplyWhiskers sleep*4 / sleepAng 10°+30°; TickSleep
    // zeros idleWag) so the hold still read as statue stamps on a
    // sleeping face. Hitch with the sleep breath (same 1.6 clock
    // as BreathLift / SitSleepRump / SitSleepEar / SitSleepBrow /
    // SitSleepMouth, in phase, droop ± a small lift, not a 6.5 Hz
    // pant, not an 11 Hz sniff twitch, and not WalkWhisker). Brain
    // already wrote idleSleep; Walk owns WalkWhisker. Sit pant
    // owns SitWhisker. Scratch owns SitScratchWhisker. Look owns
    // SitLookWhisker. Sit yawn owns SitYawnWhisker. Sit chew owns
    // SitChewWhisker. Hunt pin stays on idleHunt. Sniff keeps its
    // own fan. Quiet sit owns SitQuietWhisker. Sleep distal owns
    // SitSleepDistalTail (picture; springs stay). No idleWhisker:
    // Walk() would zero it. 0 = the old statue sleep droop.
    public float SitSleepWhisker()
    {
        if (sitSleepWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepWhiskerAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Night Sleep already droops the first hinge
    // (SitSleepTail) and hitches chin / lids / ears / brows /
    // flews / whiskers / chest / rump / snout with the 1.6 breath
    // — leftover sit held Tail2–Tail5 on TreeSpring restAngle 0
    // (muscles only on the first hinge — by design) so the hold
    // still read as a stiff caudal rod on a sleeping face. Limp
    // the distal Visuals (plus, accumulating 6°…12°, hitch ±2°
    // with the sleep breath, same 1.6 clock as BreathLift /
    // SitSleepRump / SitSleepEar / SitSleepBrow / SitSleepMouth /
    // SitSleepWhisker, in phase, not a 6.5 Hz pant and not a new
    // distal muscle). Colliders stay. Brain already wrote
    // idleSleep; Walk owns WalkTail. Sit sniff distal is
    // SitSniffDistalTail. Sit yawn distal is SitYawnDistalTail.
    // Sit pant distal is SitPantDistalTail. Sit chew distal is
    // SitChewDistalTail. Sit look distal is SitLookDistalTail.
    // Sit scratch distal is SitScratchDistalTail. Hunt raise stays on
    // idleHunt. Hunt leap owns LeapHuntTail. Hunt leap distal is
    // LeapHuntDistalTail. Sleep first hinge is SitSleepTail. Quiet
    // sit distal is SitQuietDistalTail. No idleTail: Walk() would
    // zero it. 0 = the old rest-spring sleep stick.
    public float SitSleepDistalTail()
    {
        if (sitSleepDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepDistalTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Quiet Forage sit already heaves Chest Visual
    // (BreathLift 0.0018 sin at 2.5) and keeps the 8.5 Hz wag —
    // leftover sit held a statue rump (ApplyRump only reads
    // WalkRumpBob / SitRump / SitYawnRump / SitSniffRump /
    // SitScratchRump / SitLookRump / SitHuntRump / SitChewRump /
    // LeapHuntRump / SitSleepRump) so the hold still read as a
    // chest-only hitch on a sitting face. Hitch with the quiet
    // chest (same 2.5 clock as BreathLift, in phase, not a see-saw,
    // not a 6.5 Hz pant and not SitSleepRump's 1.6). No extra
    // idle float: between acts Drive already wrote idleWag and
    // cleared the others. Walk owns WalkRumpBob. Sit pant owns
    // SitRump. Sit yawn owns SitYawnRump. Sit sniff owns
    // SitSniffRump. Sit scratch owns SitScratchRump. Sit look
    // owns SitLookRump. Sit chew owns SitChewRump. Hunt recover
    // owns SitHuntRump. Hunt leap owns LeapHuntRump. Sleep owns
    // SitSleepRump. Distal Tail2–Tail5 limp is SitQuietDistalTail.
    // No idleRump: Walk() would zero it. 0 = the old statue quiet
    // sit rump.
    public float SitQuietRump()
    {
        if (sitQuietRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        return Mathf.Clamp(sitQuietRumpAmp, 0f, 1.5f);
    }

    // Quiet Forage sit already heaves Chest Visual (BreathLift
    // 0.0018 sin at 2.5) and Pelvis Visual (SitQuietRump) —
    // leftover sit held world 0 (IdleNeckWorldTarget only reads
    // sleep / sniff / scratch / look / yawn / hop / SitHead /
    // SitHuntHead / SitChewHead; SitHead skips the quiet gap) so
    // the hold still read as a statue head on a hitching trunk.
    // Hitch with the quiet chest (same 2.5 clock as BreathLift /
    // SitQuietRump, in phase via sitBreathPhase, not a 6.5 Hz pant
    // and not SitHead's 3° pump). Milder 1.2° nose-down. No extra
    // idle float: between acts Drive already wrote idleWag and
    // cleared the others. Walk owns WalkNeckWorldTarget. Sit pant
    // owns SitHead. Hunt recover owns SitHuntHead. Sit chew owns
    // SitChewHead. Sit look / sniff / scratch / yawn keep their own
    // sit necks. Sleep stay hold. Quiet sit ears own SitQuietEar.
    // Quiet sit brows own SitQuietBrow. Quiet sit mouth owns
    // SitQuietMouth. Quiet sit whiskers own SitQuietWhisker.
    // Distal Tail2–Tail5 limp is SitQuietDistalTail. No
    // idleHead: Walk() would zero it. 0 = the old statue quiet sit
    // neck.
    public float SitQuietHead()
    {
        if (sitQuietHeadAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        return Mathf.Clamp(sitQuietHeadAmp, 0f, 1.5f);
    }

    // Picture only. Quiet Forage sit already heaves Chest Visual
    // (BreathLift 0.0018 sin at 2.5), Pelvis Visual (SitQuietRump),
    // and the SPD neck (SitQuietHead) — leftover sit held rest fold
    // 0.20 (EarFoldWanted default; TickIdle writes idleWag and
    // cleared the others) so the hold still read as statue ears on
    // a hitching trunk and neck. Hitch with the quiet chest (same
    // 2.5 clock as BreathLift / SitQuietRump / SitQuietHead, in
    // phase via sitBreathPhase, fold 0.20 ± 0.05, not a 6.5 Hz
    // pant and not WalkEarFlop). No extra idle float: between acts
    // Drive already wrote idleWag and cleared the others. Walk owns
    // WalkEarFlop. Sit pant owns SitEar. Sit look owns SitLookEar.
    // Sit yawn owns SitYawnEar. Sit chew owns SitChewEar. Scratch
    // keeps its kick. Hunt pin stays on idleHunt. Hunt leap keeps
    // the leap pin. Sleep owns SitSleepEar. Quiet sit brows own
    // SitQuietBrow. Quiet sit mouth owns SitQuietMouth. Quiet sit
    // whiskers own SitQuietWhisker. Distal Tail2–Tail5 limp is
    // SitQuietDistalTail. No idleEar: Walk() would zero
    // it. 0 = the old statue rest fold 0.20.
    public float SitQuietEar()
    {
        if (sitQuietEarAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        return Mathf.Clamp(sitQuietEarAmp, 0f, 1.5f);
    }

    // Picture only. Quiet Forage sit already heaves Chest Visual
    // (BreathLift 0.0018 sin at 2.5), Pelvis Visual (SitQuietRump),
    // the SPD neck (SitQuietHead), and the ears (SitQuietEar) —
    // leftover sit held the rest stamp (ApplyBrows rest pose;
    // TickIdle writes idleWag and cleared the others) so the hold
    // still read as a rest forehead on a hitching trunk, neck, and
    // ears. Hitch with the quiet chest (same 2.5 clock as BreathLift
    // / SitQuietRump / SitQuietHead / SitQuietEar, in phase via
    // sitBreathPhase, droop ± a small lift, not a 6.5 Hz pant and
    // not WalkBrow). No extra idle float: between acts Drive already
    // wrote idleWag and cleared the others. Walk owns WalkBrow. Sit
    // pant owns SitBrow. Sit yawn owns SitYawnBrow. Sit chew owns
    // SitChewBrow. Scratch owns SitScratchBrow. Hunt furrow stays on
    // idleHunt. Sleep owns SitSleepBrow. Quiet sit mouth owns
    // SitQuietMouth. Quiet sit whiskers own SitQuietWhisker. Distal
    // Tail2–Tail5 limp is SitQuietDistalTail. No idleBrow: Walk() would zero
    // it. 0 = the old rest quiet sit forehead.
    public float SitQuietBrow()
    {
        if (sitQuietBrowAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        return Mathf.Clamp(sitQuietBrowAmp, 0f, 1.5f);
    }

    // Picture only. Quiet Forage sit already heaves Chest Visual
    // (BreathLift 0.0018 sin at 2.5), Pelvis Visual (SitQuietRump),
    // the SPD neck (SitQuietHead), the ears (SitQuietEar), and the
    // brows (SitQuietBrow) — leftover sit held the rest flews
    // (ApplyMouth rest pose; TickIdle writes idleWag and cleared
    // the others) so the hold still read as rest flews on a
    // hitching trunk, neck, ears, and forehead. Hitch with the
    // quiet chest (same 2.5 clock as BreathLift / SitQuietRump /
    // SitQuietHead / SitQuietEar / SitQuietBrow, in phase via
    // sitBreathPhase, droop ± a small lift, not a 6.5 Hz pant,
    // not a hunt raise, and not SitLookMouth / SitYawnMouth). No
    // extra idle float: between acts Drive already wrote idleWag
    // and cleared the others. Walk / sit pant open via WalkPant /
    // idlePant. Scratch owns SitScratchMouth. Look owns
    // SitLookMouth. Sit yawn owns SitYawnMouth. Sit chew owns
    // SitChewMouth. Hunt raise stays on idleHunt. Sniff keeps
    // its own dirt tuck. Pant / yawn / chew still open with
    // DriveJaw. Sleep owns SitSleepMouth. Quiet sit whiskers
    // own SitQuietWhisker. Distal Tail2–Tail5 limp is SitQuietDistalTail. No
    // idleMouth: Walk() would zero it. 0 = the old rest quiet sit
    // flews.
    public float SitQuietMouth()
    {
        if (sitQuietMouthAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        return Mathf.Clamp(sitQuietMouthAmp, 0f, 1.5f);
    }

    // Picture only. Quiet Forage sit already heaves Chest Visual
    // (BreathLift 0.0018 sin at 2.5), Pelvis Visual (SitQuietRump),
    // the SPD neck (SitQuietHead), the ears (SitQuietEar), the
    // brows (SitQuietBrow), and the flews (SitQuietMouth) —
    // leftover sit held the rest stamps (ApplyWhiskers rest pose;
    // TickIdle writes idleWag and cleared the others) so the hold
    // still read as rest stamps on a hitching trunk, neck, ears,
    // forehead, and flews. Hitch with the quiet chest (same 2.5
    // clock as BreathLift / SitQuietRump / SitQuietHead /
    // SitQuietEar / SitQuietBrow / SitQuietMouth, in phase via
    // sitBreathPhase, droop ± a small lift, not a 6.5 Hz pant,
    // not an 11 Hz sniff twitch, and not WalkWhisker). No extra
    // idle float: between acts Drive already wrote idleWag and
    // cleared the others. Walk owns WalkWhisker. Sit pant owns
    // SitWhisker. Scratch owns SitScratchWhisker. Look owns
    // SitLookWhisker. Sit yawn owns SitYawnWhisker. Sit chew owns
    // SitChewWhisker. Hunt pin stays on idleHunt. Sniff keeps its
    // own fan. Sleep owns SitSleepWhisker. Distal Tail2–Tail5 limp
    // is SitQuietDistalTail. No idleWhisker: Walk() would zero it. 0 = the
    // old rest quiet sit stamps.
    public float SitQuietWhisker()
    {
        if (sitQuietWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        return Mathf.Clamp(sitQuietWhiskerAmp, 0f, 1.5f);
    }

    // Picture only. Quiet Forage sit already wags the first hinge
    // (IdleTailTarget 8.5 Hz) and hitches chest / rump / neck /
    // face with the 2.5 breath — leftover sit held Tail2–Tail5 on
    // TreeSpring restAngle 0 (muscles only on the first hinge —
    // by design) so the hold still read as a stiff caudal rod on
    // a sitting wag. Limp the distal Visuals (accumulating 6°…12°)
    // following that same 8.5 Hz metronome (env already pulses;
    // Time.fixedTime so the pictures stay in phase with the hinge,
    // not Sleep's 1.6 hitch, not sniff's ~3 Hz bob, not yawn's
    // envelope, not pant's 6.5 Hz pump, not chew's 11 Hz snap, not
    // look's signed bob, not scratch's ~14 Hz rake, and not a
    // new distal muscle). Plus = clockwise, tip down, same sign
    // as IdleTailTarget's +16° sit wag. Colliders stay. Drive
    // already wrote idleWag and cleared the acts. Walk owns
    // WalkTail. Walk distal is WalkDistalTail. Sit sniff distal is SitSniffDistalTail. Sit yawn
    // distal is SitYawnDistalTail. Sit pant distal is
    // SitPantDistalTail. Sit chew distal is SitChewDistalTail.
    // Sit look distal is SitLookDistalTail. Sit scratch distal is
    // SitScratchDistalTail. Hunt recover distal is SitHuntDistalTail.
    // Hunt stalk distal is WalkHuntDistalTail. Hunt leap owns
    // LeapHuntTail. Hunt leap distal is LeapHuntDistalTail.
    // Sleep distal is SitSleepDistalTail. No idleTail: Walk() would
    // zero it. 0 = the old rest-spring quiet sit stick.
    public float SitQuietDistalTail()
    {
        if (sitQuietDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        return Mathf.Clamp(sitQuietDistalTailAmp, 0f, 1.5f)
            * idleWag * Mathf.Sin(Time.fixedTime * 8.5f);
    }

    // Same first-hinge spring as sit idleWag. Sit idleSniff already
    // drops the neck, shuffles the near-front paw, and weaves the
    // muzzle — leftover sit held the 8.5 Hz forage metronome, so
    // the track still read as a dirt statue with a sit wag. Droop
    // (plus, tip down) hitching with the sniff bob (idleSniff already
    // pulses ~3 Hz). Brain already wrote idleSniff; Walk owns
    // WalkTail, sit look owns SitLookTail, sit pant owns
    // SitPantTail. Sit scratch owns SitScratchTail. Sit yawn
    // owns SitYawnTail. Sit chew owns SitChewTail (wag-off meal;
    // this skip is the 0.08 chew dip, not rest). Hunt raise stays
    // on idleHunt. Hunt leap owns LeapHuntTail. Sleep owns
    // SitSleepTail. Distal Tail2–Tail5 limp is SitSniffDistalTail.
    // Sit yawn distal is SitYawnDistalTail. Rest skip. Quiet sit
    // between acts keeps 8.5 Hz. No idleTail: Walk() would zero it.
    // 0 = the old sniff metronome.
    public float SitSniffTail()
    {
        if (sitSniffTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSniffTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSniff);
    }

    // Picture only. Sit sniff already droops the first hinge
    // (SitSniffTail) and hitches neck / paw / muzzle / chest /
    // rump with the dirt bob — leftover sit held Tail2–Tail5 on
    // TreeSpring restAngle 0 (muscles only on the first hinge —
    // by design) so the track still read as a stiff caudal rod
    // on a dirt-tracking face. Limp the distal Visuals (plus,
    // accumulating 6°…12°) following idleSniff's ~3 Hz bob
    // (env already pulses; no extra clock, not a 6.5 Hz pant,
    // not Sleep's 1.6 hitch, and not a new distal muscle).
    // Colliders stay. Brain already wrote idleSniff; Walk owns
    // WalkTail. Sit yawn distal is SitYawnDistalTail. Sit pant
    // distal is SitPantDistalTail. Sit chew distal is
    // SitChewDistalTail. Sit look distal is SitLookDistalTail.
    // Sit scratch distal is SitScratchDistalTail. Hunt raise stays
    // on idleHunt. Hunt leap owns LeapHuntTail. Sleep distal is
    // SitSleepDistalTail. Quiet sit distal is SitQuietDistalTail.
    // No idleTail: Walk() would zero it.
    // 0 = the old rest-spring sniff stick.
    public float SitSniffDistalTail()
    {
        if (sitSniffDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSniffDistalTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSniff);
    }

    // Picture only. Sit idleSniff already drops the neck, shuffles
    // the near-front paw, weaves the muzzle, and droops the tail —
    // leftover sit held quiet-sit 0.0018 (BreathLift only reads
    // idlePant / Sleep / walk / idleYawn) so the track still read
    // as a statue chest. Hitch with the sniff bob (idleSniff
    // already pulses ~3 Hz). Not a 6.5 Hz pant and not the quiet
    // sin. Brain already wrote idleSniff; Walk owns WalkTrunkBob.
    // Sit pant owns the idlePant branch in BreathLift. Sit yawn
    // owns SitYawnBreath. Sit scratch owns SitScratchBreath.
    // Sit look owns SitLookBreath. Hunt recover owns SitHuntBreath.
    // Sleep / Leap /
    // rest skip. Chew reuses idleSniff for the paw and zeros
    // idleWag; SitChewBreath owns that meal chest. Sit sniff rump
    // is SitSniffRump. No idleBreath: Walk() would zero it. 0 =
    // the old quiet-sit sniff chest.
    public float SitSniffBreath()
    {
        if (sitSniffBreathAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSniffBreathAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSniff);
    }

    // Picture only. After a kill the brain already snaps the jaw,
    // pulses the morsel, and reuses idleSniff on the near-front
    // paw — leftover sit held quiet-sit 0.0018 (BreathLift only
    // reads pant / Sleep / walk / yawn / sniff / scratch / look /
    // hunt; SitSniffBreath skips idleChew / wag-off) so the meal
    // still read as a statue chest on a chewing jaw. Hitch with
    // the snap (idleChew already Abs-sins ~11 Hz). Hang 0.55 so a
    // closed mouth is still a meal, not the quiet sin and not a
    // 6.5 Hz pant. Session is wag-off + sniff (WriteIdlePose chew
    // zeros idleWag; forage sniff keeps wag 1). Brain already
    // wrote idleChew; Walk owns WalkTrunkBob. Sit sniff owns
    // SitSniffBreath. Sit pant owns the idlePant branch. Sit yawn
    // owns SitYawnBreath. Sit scratch owns SitScratchBreath. Sit
    // look owns SitLookBreath. Hunt recover owns SitHuntBreath.
    // Sleep / Leap / rest skip. Sit chew rump is SitChewRump.
    // Sit chew tail is SitChewTail. Sit chew lid is SitChewSquint.
    // Sit chew ear is SitChewEar. Sit chew brow is SitChewBrow.
    // Sit chew whisker is SitChewWhisker. Sit chew neck is
    // SitChewHead. Sit chew snout is SitChewNose. Sit chew
    // lip is SitChewMouth. Sit chew pupil is SitChewEye.
    // Sit chew puff is SitChewPuff.
    // No idleBreath: Walk() would
    // zero it. 0 = the old quiet-sit chew chest.
    public float SitChewBreath()
    {
        if (sitChewBreathAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewBreathAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. After a kill SitChewBreath already hitches
    // Chest Visual — leftover sit held a statue rump (ApplyRump
    // only reads WalkRumpBob / SitRump / SitYawnRump /
    // SitSniffRump / SitScratchRump / SitLookRump / SitHuntRump;
    // SitSniffRump skips wag-off sniff) so the meal still read as
    // a chest-only hitch on a chewing jaw. Hitch with the snap
    // (idleChew already Abs-sins ~11 Hz). Hang 0.55 so a closed
    // mouth is still a meal, not a statue and not a 6.5 Hz pant.
    // In phase with SitChewBreath (a meal, not a see-saw). Session
    // is wag-off + sniff. Brain already wrote idleChew; Walk owns
    // WalkRumpBob. Sit sniff owns SitSniffRump. Sit pant owns
    // SitRump. Sit yawn owns SitYawnRump. Sit scratch owns
    // SitScratchRump. Sit look owns SitLookRump. Hunt recover
    // owns SitHuntRump. Quiet sit / Sleep stay chest-only. Sleep /
    // Leap / rest skip. Sit chew tail is SitChewTail. Sit chew
    // lid is SitChewSquint. Sit chew ear is SitChewEar. Sit chew
    // brow is SitChewBrow. Sit chew whisker is SitChewWhisker.
    // Sit chew neck is SitChewHead. No
    // idleRump:
    // Walk() would zero it. 0 = the old statue chew rump.
    public float SitChewRump()
    {
        if (sitChewRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewRumpAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Same first-hinge spring as sit idleWag. After a kill
    // SitChewBreath / SitChewRump already hitch Chest / Pelvis —
    // leftover sit held rest (IdleTailTarget falls to tailBaseAngle:
    // chew zeros idleWag, SitSniffTail skips idleChew, no SitChewTail)
    // so the meal still read as a planted tail on a chewing jaw.
    // Droop (plus, tip down) hitching with the snap (idleChew already
    // Abs-sins ~11 Hz). Hang 0.55 so a closed mouth is still a meal,
    // not rest and not a 6.5 Hz pant. 10° so the snap does not rattle
    // like a full 16° wag. In phase with SitChewBreath / SitChewRump
    // (a meal, not a see-saw). Session is wag-off + sniff. Brain
    // already wrote idleChew; Walk owns WalkTail. Sit sniff owns
    // SitSniffTail. Sit pant owns SitPantTail. Sit yawn owns
    // SitYawnTail. Sit scratch owns SitScratchTail. Sit look owns
    // SitLookTail. Hunt raise stays on idleHunt. Hunt leap owns
    // LeapHuntTail. Sleep / rest skip. Sit chew lid is SitChewSquint. Sit chew ear is
    // SitChewEar. Sit chew brow is SitChewBrow. Sit chew whisker
    // is SitChewWhisker. Sit chew neck is SitChewHead. Distal limp
    // is SitChewDistalTail. Quiet sit distal is SitQuietDistalTail.
    // No idleTail:
    // Walk() would zero it. 0 = the old rest chew tail.
    public float SitChewTail()
    {
        if (sitChewTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewTailAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. Sit chew already droops the first hinge
    // (SitChewTail) and snaps jaw / chest / rump / lid / ear /
    // brow / whisker / neck / snout / lip / pupil / puff —
    // leftover sit held Tail2–Tail5 on TreeSpring restAngle 0
    // (muscles only on the first hinge — by design) so the meal
    // still read as a stiff caudal rod on a chewing jaw. Limp
    // the distal Visuals (plus, accumulating 6°…12°) following
    // idleChew's hang 0.55 + 11 Hz snap (env already hitchs; no
    // extra clock, not Sleep's 1.6 hitch, not sniff's ~3 Hz bob,
    // not yawn's envelope, not pant's 6.5 Hz pump, and not a new
    // distal muscle). Colliders stay. Brain already wrote
    // idleChew; Walk owns WalkTail. Sit sniff distal is
    // SitSniffDistalTail. Sit yawn distal is SitYawnDistalTail.
    // Sit pant distal is SitPantDistalTail. Sit look distal is
    // SitLookDistalTail. Sit scratch distal is
    // SitScratchDistalTail. Hunt raise stays on idleHunt. Hunt leap owns
    // LeapHuntTail. Sleep distal is SitSleepDistalTail. Quiet
    // sit distal is SitQuietDistalTail. No
    // idleTail: Walk() would zero it. 0 = the old rest-spring
    // chew stick.
    public float SitChewDistalTail()
    {
        if (sitChewDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewDistalTailAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. After a kill SitChewBreath / SitChewRump /
    // SitChewTail already hitch Chest / Pelvis / first-hinge —
    // leftover sit held the open sniff-track eye (SitSquint /
    // SitLookSquint / SitScratchSquint all skip idleChew; sniff
    // keeps the open track eye) so the meal still read as a
    // wide forage stare on a chewing jaw. Hitch a thinner slit
    // with the snap (idleChew already Abs-sins ~11 Hz). Hang
    // 0.55 so a closed mouth is still a meal, not the open
    // track and not a 6.5 Hz pant. Same Walk/SitSquint pair.
    // In phase with SitChewBreath / SitChewRump / SitChewTail
    // (a meal, not a see-saw). Session is wag-off + sniff.
    // Brain already wrote idleChew; Walk owns WalkSquint. Sit
    // pant owns SitSquint. Sit look owns SitLookSquint
    // (look-down only). Sit scratch owns SitScratchSquint.
    // Hunt recover owns SitHuntSquint. Hunt stalk Walk owns
    // WalkHuntSquint. Hunt leap owns LeapHuntSquint. Sniff
    // keeps the open track eye (forage wag stays on). Alert
    // stare is idleLook, not this. Sleep / yawn / blink still
    // own the full cover. Pupil stays visible under the
    // squint. Sleep / Leap / rest skip. Sit chew ear is
    // SitChewEar. Sit chew brow is SitChewBrow. Sit chew whisker
    // is SitChewWhisker. No idleBlink: Walk()
    // would zero it. 0 = the old open sniff-track chew eye.
    public float SitChewSquint()
    {
        if (sitChewLidAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewLidAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. After a kill SitChewBreath / SitChewRump /
    // SitChewTail / SitChewSquint already hitch Chest / Pelvis /
    // first-hinge / lids — leftover sit held the statue sniff
    // half-fold 0.42 (EarFoldWanted returns 0.42 on idleSniff
    // before idleChew; SitEar / SitLookEar / SitYawnEar skip
    // idleChew) so the meal still read as a sniff statue on a
    // chewing jaw. Hitch a fold with the snap (idleChew already
    // Abs-sins ~11 Hz). Hang 0.55 so a closed mouth is still a
    // meal, not the statue half-fold and not a 6.5 Hz pant.
    // Same Head-Visual pair. In phase with SitChewBreath /
    // SitChewRump / SitChewTail / SitChewSquint (a meal, not a
    // see-saw). Session is wag-off + sniff. Brain already wrote
    // idleChew; Walk owns WalkEarFlop. Sit pant owns SitEar.
    // Sit look owns SitLookEar. Sit yawn owns SitYawnEar.
    // Scratch keeps its kick. Hunt pin stays on idleHunt.
    // Sniff keeps the forage half-fold (forage wag stays on).
    // Sleep owns SitSleepEar. Leap / rest skip. Sit chew brow is SitChewBrow.
    // Sit chew whisker is SitChewWhisker.
    // No idleEar: Walk() would
    // zero it. 0 = the old statue sniff-fold chew ear.
    public float SitChewEar()
    {
        if (sitChewEarAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewEarAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. After a kill SitChewBreath / SitChewRump /
    // SitChewTail / SitChewSquint / SitChewEar already hitch Chest /
    // Pelvis / first-hinge / lids / ears — leftover sit held the
    // statue sniff furrow (ApplyBrows reads idleSniff; WalkBrow /
    // SitBrow / SitScratchBrow / SitYawnBrow skip idleChew) so the
    // meal still read as a dirt-track forehead on a chewing jaw.
    // Hitch a crease with the snap (idleChew already Abs-sins
    // ~11 Hz). Hang 0.55 so a closed mouth is still a meal, not
    // the statue sniff furrow and not a 6.5 Hz pant. Same
    // Walk/SitBrow slot (crease, not a yawn raise). In phase with
    // SitChewBreath / SitChewRump / SitChewTail / SitChewSquint /
    // SitChewEar (a meal, not a see-saw). Session is wag-off +
    // sniff. Brain already wrote idleChew; Walk owns WalkBrow.
    // Sit pant owns SitBrow. Sit yawn owns SitYawnBrow. Scratch
    // owns SitScratchBrow. Hunt furrow stays on idleHunt. Sniff
    // keeps the forage drop (forage wag stays on). Sleep owns
    // SitSleepBrow. Leap / rest skip. Sit chew whisker is
    // SitChewWhisker. No idleBrow:
    // Walk() would zero it. 0 = the old
    // statue sniff-furrow chew brow.
    public float SitChewBrow()
    {
        if (sitChewBrowAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewBrowAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. After a kill SitChewBreath / SitChewRump /
    // SitChewTail / SitChewSquint / SitChewEar / SitChewBrow already
    // hitch Chest / Pelvis / first-hinge / lids / ears / brow —
    // leftover sit held the sniff fan + 11 Hz twitch (ApplyWhiskers
    // reads idleSniff; WalkWhisker / SitWhisker / SitScratchWhisker
    // / SitLookWhisker / SitYawnWhisker skip idleChew) so the meal
    // still read as a dirt-track muzzle on a chewing jaw. Hitch a
    // milder Walk/SitWhisker fan with the snap (idleChew already
    // Abs-sins ~11 Hz). Hang 0.55 so a closed mouth is still a meal,
    // not the sniff fan+11 Hz and not a 6.5 Hz pant. Same
    // Walk/SitWhisker slot (fan, not a yawn stretch). In phase with
    // SitChewBreath / SitChewRump / SitChewTail / SitChewSquint /
    // SitChewEar / SitChewBrow (a meal, not a see-saw). Session is
    // wag-off + sniff. Brain already wrote idleChew; Walk owns
    // WalkWhisker. Sit pant owns SitWhisker. Sit yawn owns
    // SitYawnWhisker. Scratch owns SitScratchWhisker. Sit look
    // owns SitLookWhisker. Hunt pin stays on idleHunt. Sniff keeps
    // the forage fan (forage wag stays on). Sleep / Leap / rest
    // skip. Sit chew neck is SitChewHead. Sit chew snout is
    // SitChewNose. Sit chew lip is SitChewMouth. Sit chew
    // pupil is SitChewEye. No idleWhisker:
    // Walk() would zero it. 0 = the old sniff-fan chew stamps.
    public float SitChewWhisker()
    {
        if (sitChewWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewWhiskerAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Sit chew already snaps the jaw and hitches chest / rump /
    // tail / lid / ear / brow / whisker — leftover sit held sniff
    // 16° (IdleNeckWorldTarget reads idleSniff; SitHead skips
    // idleChew) so the meal still read as a dirt-track neck on a
    // chewing jaw. Hitch a milder SPD drop with the snap
    // (idleChew already Abs-sins ~11 Hz). Hang 0.55 so a closed
    // mouth is still a meal, not the held 16° sniff and not
    // SitHead's 3° pump. Same 16° as sit sniff, not a new neckP.
    // In phase with SitChewBreath / SitChewRump / SitChewTail /
    // SitChewSquint / SitChewEar / SitChewBrow / SitChewWhisker
    // (a meal, not a see-saw). Session is wag-off + sniff. Brain
    // already wrote idleChew; Walk owns WalkNeckWorldTarget. Sit
    // pant owns SitHead. Hunt recover owns SitHuntHead. Sit look
    // / sniff / scratch / yawn keep their own sit necks. Sleep /
    // Leap / rest skip. Sit chew snout is SitChewNose. Sit chew
    // lip is SitChewMouth. Sit chew pupil is SitChewEye. No
    // idleHead: Walk() would zero it.
    // 0 = the old held-sniff chew neck.
    public float SitChewHead()
    {
        if (sitChewHeadAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewHeadAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. Sit chew already snaps the jaw and hitches
    // chest / rump / tail / lid / ear / brow / whisker / neck —
    // leftover sit held the sniff drop (ApplyNose reads idleSniff;
    // WalkNose / SitNose skip idleChew) so the meal still read as
    // a dirt-track muzzle on a chewing jaw. Hitch a milder drop
    // with the snap (idleChew already Abs-sins ~11 Hz). Hang 0.55
    // so a closed mouth is still a meal, not the held sniff drop
    // and not SitNose's 0.005 pant. Same 0.007 as sit sniff, not
    // a new idleNose. In phase with SitChewBreath / SitChewRump /
    // SitChewTail / SitChewSquint / SitChewEar / SitChewBrow /
    // SitChewWhisker / SitChewHead (a meal, not a see-saw).
    // Session is wag-off + sniff. Brain already wrote idleChew;
    // Walk owns WalkNose. Sit pant owns SitNose. Sit yawn owns
    // SitYawnNose. Scratch owns SitScratchNose. Hunt drop stays
    // on idleHunt. Sleep / Leap / rest skip. Sit chew lip is
    // SitChewMouth. Sit chew pupil is SitChewEye. No idleNose:
    // Walk() would zero it. 0 = the old held-sniff chew muzzle.
    public float SitChewNose()
    {
        if (sitChewNoseAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewNoseAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. Sit chew already snaps the jaw and hitches
    // chest / rump / tail / lid / ear / brow / whisker / neck /
    // snout — leftover sit held the sniff tuck (ApplyMouth reads
    // idleSniff; Scratch / Look / Yawn skip idleChew) so the meal
    // still read as dirt-track flews on a chewing jaw. Hitch a
    // milder tuck with the snap (idleChew already Abs-sins ~11 Hz).
    // Hang 0.55 so a closed mouth is still a meal, not the held
    // sniff tuck and not SitYawnMouth's stretch lift. Same 0.006 /
    // 6° as sit sniff, not a hunt raise and not DriveJaw's open.
    // In phase with SitChewBreath / SitChewRump / SitChewTail /
    // SitChewSquint / SitChewEar / SitChewBrow / SitChewWhisker /
    // SitChewHead / SitChewNose (a meal, not a see-saw). Session
    // is wag-off + sniff. Brain already wrote idleChew; Walk /
    // sit pant open via WalkPant / idlePant. Scratch owns
    // SitScratchMouth. Look owns SitLookMouth. Sit yawn owns
    // SitYawnMouth. Quiet sit owns SitQuietMouth. Sleep owns
    // SitSleepMouth. Hunt raise stays on idleHunt. Sniff keeps
    // its own dirt tuck. Leap / rest skip. Sit chew
    // pupil is SitChewEye. No idleMouth: Walk() would zero it.
    // 0 = the old held-sniff chew flews.
    public float SitChewMouth()
    {
        if (sitChewMouthAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewMouthAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. Sit chew already snaps the jaw and hitches
    // chest / rump / tail / lid / ear / brow / whisker / neck /
    // snout / lip — leftover sit held the sniff drop (ApplyEyes
    // reads idleSniff; WalkEye / SitEye skip idleChew) so the
    // meal still read as a dirt-track stare on a chewing jaw.
    // Hitch a milder drop with the snap (idleChew already
    // Abs-sins ~11 Hz). Hang 0.55 so a closed mouth is still a
    // meal, not the held sniff drop and not SitEye's 0.004 pant.
    // Same 0.008 as sit sniff, not a new idleEye. In phase with
    // SitChewBreath / SitChewRump / SitChewTail / SitChewSquint /
    // SitChewEar / SitChewBrow / SitChewWhisker / SitChewHead /
    // SitChewNose / SitChewMouth (a meal, not a see-saw).
    // Session is wag-off + sniff. Brain already wrote idleChew;
    // Walk owns WalkEye. Sit pant owns SitEye. Scratch owns
    // SitScratchEye. Leap owns LeapHuntEye. Hunt drop stays on
    // idleHunt. Look-up stays idleLook. Sleep / yawn / blink
    // still hide the dot. Pupil stays visible under SitChewSquint.
    // Sit chew puff is SitChewPuff. No idleEye: Walk() would
    // zero it. 0 = the old held-sniff chew stare.
    public float SitChewEye()
    {
        if (sitChewEyeAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewEyeAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. Sit chew already snaps the jaw and hitches
    // chest / rump / tail / lid / ear / brow / whisker / neck /
    // snout / lip / pupil, and pulses the morsel — leftover sit
    // held silent (SitYawnPuff / SitPuff skip idleChew; morsel
    // owns the Jaw-Visual teeth slot) so the meal still read as a
    // silent lump on a chewing jaw. Pulse a cooler sliver cranial
    // of the morsel with the snap (idleChew already Abs-sins
    // ~11 Hz). No hang: vapor on a closed mouth would leak (SitPuff
    // only pumps above 0.55). Same PantPuff child, not a new
    // idlePuff. In phase with SitChewBreath / SitChewRump /
    // SitChewTail / SitChewSquint / SitChewEar / SitChewBrow /
    // SitChewWhisker / SitChewHead / SitChewNose / SitChewMouth /
    // SitChewEye (a meal, not a see-saw). Session is wag-off +
    // sniff. Brain already wrote idleChew; Walk owns WalkPuff.
    // Sit pant owns SitPuff. Sit yawn owns SitYawnPuff. Hunt
    // recover owns SitHuntPuff. Stalk Walk owns WalkHuntPuff.
    // Leap owns LeapHuntPuff. Bark puff is Call. Tongue stays off
    // (morsel). Sleep / Leap / rest skip. No idlePuff: Walk()
    // would zero it. 0 = the old silent chew lump.
    public float SitChewPuff()
    {
        if (sitChewPuffAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        if (idleChew < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewPuffAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleChew);
    }

    // Picture only. Sit idleSniff already heaves Chest Visual —
    // leftover sit held a statue rump (ApplyRump only reads
    // WalkRumpBob / SitRump / SitYawnRump) so the track still
    // read as a chest-only bob. Hitch with the sniff bob
    // (idleSniff already pulses ~3 Hz). In phase with
    // SitSniffBreath (a dirt track, not a see-saw, not a 6.5 Hz
    // pant). Brain already wrote idleSniff; Walk owns
    // WalkRumpBob. Sit pant owns SitRump. Sit yawn owns
    // SitYawnRump. Sit scratch owns SitScratchRump. Sit look
    // owns SitLookRump. Hunt recover owns SitHuntRump.
    // Quiet sit / Sleep stay chest-only. Sleep / Leap / rest
    // skip. Chew reuses idleSniff for the paw; SitChewRump owns
    // that meal rump; SitChewTail owns that meal tail (wag-off
    // is the session).
    // No idleRump: Walk() would zero it.
    // 0 = the old statue sniff rump.
    public float SitSniffRump()
    {
        if (sitSniffRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSniffRumpAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSniff);
    }

    // Same first-hinge spring as sit idleWag. Sit idleScratch already
    // tucks the neck, kicks the near-hind, and weaves the face —
    // leftover sit held the 8.5 Hz forage metronome, so the kick
    // still read as an itch with a sit wag. Droop (plus, tip down)
    // hitching with the rake (idleScratch already pulses ~14 Hz
    // above the 0.55 hang). Brain already wrote idleScratch; Walk
    // owns WalkTail, sit look owns SitLookTail, sit sniff owns
    // SitSniffTail, sit yawn owns SitYawnTail, sit pant owns
    // SitPantTail. Hunt raise stays on idleHunt. Sit chew owns
    // SitChewTail. Sleep / Leap / rest skip. Quiet sit between
    // acts keeps 8.5 Hz. Distal limp is SitScratchDistalTail.
    // No idleTail: Walk() would zero it. 0 =
    // the old scratch metronome.
    public float SitScratchTail()
    {
        if (sitScratchTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchTailAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit scratch already droops the first hinge
    // (SitScratchTail) and kicks hind / face / chest / rump —
    // leftover sit held Tail2–Tail5 on TreeSpring restAngle 0
    // (muscles only on the first hinge — by design) so the kick
    // still read as a stiff caudal rod on a raking paw. Limp
    // the distal Visuals (plus, accumulating 6°…12°) following
    // idleScratch's hang 0.55 + ~14 Hz rake (env already hitchs;
    // no extra clock, not Sleep's 1.6 hitch, not sniff's ~3 Hz
    // bob, not yawn's envelope, not pant's 6.5 Hz pump, not
    // chew's 11 Hz snap, not look's signed bob, and not a new
    // distal muscle). Colliders stay. Brain already wrote
    // idleScratch; Walk owns WalkTail. Sit sniff distal is
    // SitSniffDistalTail. Sit yawn distal is SitYawnDistalTail.
    // Sit pant distal is SitPantDistalTail. Sit chew distal is
    // SitChewDistalTail. Sit look distal is SitLookDistalTail.
    // Hunt raise stays on idleHunt. Hunt leap owns LeapHuntTail.
    // Sleep distal is SitSleepDistalTail. Quiet sit distal is
    // SitQuietDistalTail. No idleTail: Walk() would
    // zero it. 0 = the old rest-spring scratch stick.
    public float SitScratchDistalTail()
    {
        if (sitScratchDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchDistalTailAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already kicks the near-hind,
    // weaves the face, and droops the tail — leftover sit held a
    // statue rump (ApplyRump only reads WalkRumpBob / SitRump /
    // SitYawnRump / SitSniffRump) so the itch still read as a
    // planted pelvis on a kicking paw. Hitch with the rake
    // (idleScratch already pulses ~14 Hz above the 0.55 hang).
    // In phase with the hind and SitScratchBreath (an itch, not a
    // see-saw, not a 6.5 Hz pant). Brain already wrote idleScratch;
    // Walk owns WalkRumpBob. Sit pant owns SitRump. Sit yawn owns
    // SitYawnRump. Sit sniff owns SitSniffRump. Sit look owns
    // SitLookRump. Hunt recover owns SitHuntRump. Sit chew owns
    // SitChewRump. Quiet sit /
    // Sleep stay chest-only.
    // Sleep / Leap / rest skip. Chew zeros the scratch. No
    // idleRump: Walk() would zero it. 0 = the old statue
    // scratch rump.
    public float SitScratchRump()
    {
        if (sitScratchRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchRumpAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already kicks the near-hind,
    // weaves the face, droops the tail, and heaves the rump —
    // leftover sit held quiet-sit 0.0018 (BreathLift only reads
    // idlePant / Sleep / walk / idleYawn / idleSniff) so the itch
    // still read as a statue chest on a kicking paw. Hitch with
    // the rake (idleScratch already pulses ~14 Hz above the 0.55
    // hang). In phase with SitScratchRump (an itch, not a
    // see-saw, not a 6.5 Hz pant and not the quiet sin). Brain
    // already wrote idleScratch; Walk owns WalkTrunkBob. Sit
    // pant owns the idlePant branch in BreathLift. Sit yawn
    // owns SitYawnBreath. Sit sniff owns SitSniffBreath. Sit look
    // owns SitLookBreath. Hunt recover owns SitHuntBreath. Sleep /
    // Leap / rest skip.
    // Chew zeros the scratch. No idleBreath: Walk() would zero
    // it. 0 = the old quiet-sit scratch chest.
    public float SitScratchBreath()
    {
        if (sitScratchBreathAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchBreathAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Same first-hinge spring as sit idleWag. Sit idleYawn already
    // lifts the head and opens the jaw — leftover sit held the
    // 8.5 Hz forage metronome, so the stretch still read as a
    // yawn with a sit wag. Still droop (plus, tip down) following
    // the yawn envelope (idleYawn already rises / holds / falls).
    // Brain already wrote idleYawn; Walk owns WalkTail, sit look
    // owns SitLookTail, sit sniff owns SitSniffTail, sit scratch
    // owns SitScratchTail, sit pant owns SitPantTail. Hunt raise
    // stays on idleHunt. Sit chew owns SitChewTail. Distal limp
    // is SitYawnDistalTail. Sleep / Leap / rest skip. Quiet sit
    // between acts keeps 8.5 Hz. No idleTail: Walk() would zero
    // it. 0 = the old yawn metronome.
    public float SitYawnTail()
    {
        if (sitYawnTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit yawn already droops the first hinge
    // (SitYawnTail) and stretches jaw / head / tongue / lids /
    // ears / whiskers / brow / snout / lip / puff / chest / rump
    // with the gape — leftover sit held Tail2–Tail5 on TreeSpring
    // restAngle 0 (muscles only on the first hinge — by design) so
    // the stretch still read as a stiff caudal rod on a gaping
    // jaw. Limp the distal Visuals (plus, accumulating 6°…12°)
    // following idleYawn's envelope (env already rises / holds /
    // falls; no extra clock, not a 6.5 Hz pant, not Sleep's 1.6
    // hitch, not sniff's ~3 Hz bob, and not a new distal muscle).
    // Colliders stay. Brain already wrote idleYawn; Walk owns
    // WalkTail. Sit sniff distal is SitSniffDistalTail. Sit pant
    // distal is SitPantDistalTail. Sit chew distal is
    // SitChewDistalTail. Sit look distal is SitLookDistalTail.
    // Sit scratch distal is SitScratchDistalTail. Hunt raise stays
    // on idleHunt. Hunt leap owns LeapHuntTail. Sleep distal is
    // SitSleepDistalTail. Quiet sit distal is SitQuietDistalTail.
    // No idleTail: Walk() would zero it.
    // 0 = the old rest-spring yawn stick.
    public float SitYawnDistalTail()
    {
        if (sitYawnDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnDistalTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already lifts the head, opens the
    // jaw, shuts the lids, curls the tongue, and droops the tail —
    // leftover sit held a statue half-fold 0.50, so the stretch
    // still read as a forage head on a gaping jaw. Fold back with
    // the yawn envelope (idleYawn already rises / holds / falls).
    // Brain already wrote idleYawn; Walk owns WalkEarFlop, sit look
    // owns SitLookEar, sit pant owns SitEar, scratch owns the
    // near-ear kick. Hunt pin stays on idleHunt. Sniff keeps its
    // own half-fold. Sit chew owns SitChewEar. Sleep owns
    // SitSleepEar. Leap / rest skip. No idleEar:
    // Walk() would zero it. 0 = the old statue yawn half-fold.
    public float SitYawnEar()
    {
        if (sitYawnEarAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnEarAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already lifts the head, opens the
    // jaw, shuts the lids, curls the tongue, droops the tail, and
    // folds the ears — leftover sit held rest whisker stamps, so the
    // stretch still read as a forage muzzle on a gaping jaw. Fan
    // with the yawn envelope (idleYawn already rises / holds /
    // falls). No twitch: this is a slow stretch, not a 14 Hz rake.
    // Brain already wrote idleYawn; Walk owns WalkWhisker, sit look
    // owns SitLookWhisker, sit pant owns SitWhisker, scratch owns
    // SitScratchWhisker. Sit chew owns SitChewWhisker. Hunt pin
    // stays on idleHunt. Sniff keeps its own fan. Sleep / Leap /
    // rest skip. No idleWhisker: Walk() would zero it. 0 = the old
    // rest yawn stamps.
    public float SitYawnWhisker()
    {
        if (sitYawnWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnWhiskerAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already lifts the head, opens the
    // jaw, shuts the lids, curls the tongue, droops the tail, folds
    // the ears, and fans the whiskers — leftover sit held the rest
    // brow stamp, so the stretch still read as a forage forehead on
    // a gaping jaw. Raise with the yawn envelope (idleYawn already
    // rises / holds / falls). Stretch, not a hunt furrow and not a
    // 14 Hz rake. Brain already wrote idleYawn; Walk owns WalkBrow,
    // sit look already lifts via idleLook, sit pant owns SitBrow,
    // scratch owns SitScratchBrow. Sit chew owns SitChewBrow.
    // Hunt furrow stays on idleHunt.
    // Sniff keeps its own drop. Sleep / Leap / rest skip. No
    // idleBrow: Walk() would zero it. 0 = the old rest yawn forehead.
    public float SitYawnBrow()
    {
        if (sitYawnBrowAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnBrowAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already lifts the head, opens the
    // jaw, shuts the lids, curls the tongue, droops the tail, folds
    // the ears, fans the whiskers, and raises the brow — leftover
    // sit held the rest muzzle stamp, so the stretch still read as
    // a forage snout on a gaping jaw. Lift with the yawn envelope
    // (idleYawn already rises / holds / falls). Stretch, not a hunt
    // drop and not a 14 Hz rake. Brain already wrote idleYawn;
    // Walk owns WalkNose, sit look already lifts via idleLook, sit
    // pant owns SitNose, scratch owns SitScratchNose. Hunt drop
    // stays on idleHunt. Sniff keeps its own dirt drop. Sleep /
    // Leap / rest skip. No idleNose: Walk() would zero it. 0 = the
    // old rest yawn muzzle.
    public float SitYawnNose()
    {
        if (sitYawnNoseAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnNoseAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already lifts the head, opens the
    // jaw, shuts the lids, curls the tongue, droops the tail, folds
    // the ears, fans the whiskers, raises the brow, and lifts the
    // snout — leftover sit held the rest flews (DriveJaw open is
    // the pant drop, not a stretch with that muzzle), so the gape
    // still read as forage flews on a stretching face. Lift with
    // the yawn envelope (idleYawn already rises / holds / falls).
    // Stretch, not a hunt raise and not an extra pant hang. Brain
    // already wrote idleYawn; Walk / sit pant open via WalkPant /
    // idlePant. Scratch owns SitScratchMouth. Look owns
    // SitLookMouth. Sit chew owns SitChewMouth. Quiet sit owns
    // SitQuietMouth. Sleep owns SitSleepMouth. Hunt raise stays
    // on idleHunt. Sniff keeps its own dirt tuck. Pant / yawn /
    // chew still open with DriveJaw. Leap / rest skip. No
    // idleMouth: Walk() would zero it. 0 = the old rest yawn flews.
    public float SitYawnMouth()
    {
        if (sitYawnMouthAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnMouthAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already lifts the head, opens the
    // jaw, shuts the lids, curls the tongue, droops the tail, folds
    // the ears, fans the whiskers, raises the brow, lifts the snout,
    // and stretches the lip — leftover sit held silent (SitPuff
    // only reads idlePant) so the gape still read as a silent
    // stretch. Hitch with the yawn envelope (idleYawn already
    // rises / holds / falls). Needs open gape (≥0.20, same as
    // tongue) so a closed start does not leak vapor. Slow stretch,
    // not a 6.5 Hz pant. Brain already wrote idleYawn; Walk owns
    // WalkPuff. Sit pant owns SitPuff. Sit chew owns SitChewPuff.
    // Hunt recover owns SitHuntPuff. Stalk Walk owns WalkHuntPuff.
    // Leap owns LeapHuntPuff. Bark puff is Call. Sleep / Leap /
    // rest skip. No idlePuff: Walk() would zero it. 0 = the old
    // silent yawn tongue.
    public float SitYawnPuff()
    {
        if (sitYawnPuffAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.20f)
            return 0f;
        return Mathf.Clamp(sitYawnPuffAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already lifts the head, opens the
    // jaw, shuts the lids, curls the tongue, droops the tail, folds
    // the ears, fans the whiskers, raises the brow, lifts the snout,
    // stretches the lip, and puffs — leftover sit held quiet-sit
    // 0.0018 (BreathLift only reads idlePant / Sleep / walk) so the
    // gape still read as a statue chest. Hitch with the yawn
    // envelope (idleYawn already rises / holds / falls). Slow
    // inhale, not a 6.5 Hz pant and not the quiet sin. Brain already
    // wrote idleYawn; Walk owns WalkTrunkBob. Sit pant owns the
    // idlePant branch in BreathLift. Sit yawn rump is SitYawnRump.
    // Hunt recover owns SitHuntBreath. Sit look owns
    // SitLookBreath. Sit sniff owns SitSniffBreath. Sit scratch
    // owns SitScratchBreath. Sleep /
    // Leap / rest skip. No idleBreath: Walk() would zero it.
    // 0 = the old quiet-sit yawn chest.
    public float SitYawnBreath()
    {
        if (sitYawnBreathAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnBreathAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already heaves Chest Visual —
    // leftover sit held a statue rump (ApplyRump only reads
    // WalkRumpBob / SitRump / idlePant) so the gape still read as
    // a chest-only inhale. Hitch with the yawn envelope (idleYawn
    // already rises / holds / falls). In phase with SitYawnBreath
    // (a stretch, not a see-saw, not a 6.5 Hz pant). Brain already
    // wrote idleYawn; Walk owns WalkRumpBob. Sit pant owns SitRump.
    // Sit scratch owns SitScratchRump. Sit look owns SitLookRump.
    // Hunt recover owns SitHuntRump. Sit sniff owns SitSniffRump.
    // Quiet sit / Sleep stay chest-only. Sleep / Leap / rest skip. No
    // idleRump: Walk() would zero it. 0 = the old statue yawn rump.
    public float SitYawnRump()
    {
        if (sitYawnRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnRumpAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Same first-hinge spring as sit idleWag. Sit idlePant already
    // pumps the jaw, chest, rump, and neck — leftover sit held the
    // 8.5 Hz forage metronome, so EndWalk still read as a pant with
    // a happy sit wag. Still droop (plus, tip down) hitching with
    // the pump (idlePant already hangs 0.55 + 6.5 Hz). Brain already
    // wrote idlePant; Walk owns WalkTail, sit look owns SitLookTail,
    // sit sniff owns SitSniffTail, sit scratch owns SitScratchTail,
    // sit yawn owns SitYawnTail. Hunt raise stays on idleHunt. Sit
    // chew owns SitChewTail. Distal limp is SitPantDistalTail.
    // Sleep / Leap / rest skip. Quiet sit between acts keeps
    // 8.5 Hz. No idleTail: Walk() would zero it. 0 = the old
    // pant metronome.
    public float SitPantTail()
    {
        if (sitPantTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitPantTailAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit pant already droops the first hinge
    // (SitPantTail) and pumps jaw / chest / rump / neck / tongue
    // / lids / snout / pupil / brow / whiskers / ears / puff
    // with the hang — leftover sit held Tail2–Tail5 on TreeSpring
    // restAngle 0 (muscles only on the first hinge — by design) so
    // EndWalk still read as a stiff caudal rod on a panting face.
    // Limp the distal Visuals (plus, accumulating 6°…12°) following
    // idlePant's hang 0.55 + 6.5 Hz pump (env already hitchs; no
    // extra clock, not Sleep's 1.6 hitch, not sniff's ~3 Hz bob,
    // not yawn's envelope, and not a new distal muscle). Colliders
    // stay. Brain already wrote idlePant; Walk owns WalkTail. Sit
    // sniff distal is SitSniffDistalTail. Sit yawn distal is
    // SitYawnDistalTail. Sit chew distal is SitChewDistalTail.
    // Sit look distal is SitLookDistalTail. Sit scratch distal is
    // SitScratchDistalTail. Hunt raise stays on idleHunt.
    // Hunt leap owns LeapHuntTail. Sleep distal is
    // SitSleepDistalTail. Quiet sit distal is SitQuietDistalTail.
    // No idleTail: Walk() would zero it.
    // 0 = the old rest-spring pant stick.
    public float SitPantDistalTail()
    {
        if (sitPantDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitPantDistalTailAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }
}
