using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// B-LAND-2 "THE TRIGGER PERCH" — the bird side of the owner's landing sheet
// (25.09, "Упрощенный образец дерева").
//
// The sheet: the tree carries trigger points, a bird may sit ONLY there, and a
// bird's mass reaches the branch and bends it. The plant side built the ring
// (16 isTrigger circles, only with `-treeTriggers`) and the load path
// (PlantTree.ApplyPerchLoad fed from the ring when `-treeLoadOnTrigger`).
// THIS file is the bird side: the approach assist, the perch ON a named ring
// point, the mass hand-off and the guardian read.
//
// WHY AN ASSIST EXISTS: in the current build the solo wander's return glide
// holds altitude ~= weight and carries no thrust (run bTGsink3_tree: Glide
// entered at ~11 s and hung, vy~0, to 59.9 s), so the channel has no airborne
// arrival at any pad. The assist leads the bird — x-guidance plus a led descent
// — and then hands it to the accepted pad machinery (BirdPerch): flight mode
// Glide, slow, perch volume overlapping the ring mark, which is exactly what
// BirdController.TryGlideLand waits for.
//
// RULES KEPT:
//   * The tree files are READ-ONLY. Everything here is runtime use of their
//     public API (GetLandingSlots / GetLandingMarkers / GetLandingKind,
//     landingLoadSlot, loadOnLandingTrigger, perchLoadKg).
//   * Runs without a ring are untouched: the component arms only when a tree
//     with LandingTriggerCount > 0 AND a driven bird are both in the scene;
//     until then it writes nothing and touches nothing.
//   * In a ring run the sheet is read literally: the handler stands the old
//     BirdPerch pad colliders down once the bird is airborne, so the only
//     seats in play are the sheet's trigger points.
//
// The mass: while the bird is SEATED ON THE TARGET MARK, `tree.perchLoadKg` is
// set to the bird's own mass; it is set back to 0 when the bird leaves. So one
// run carries the load-on (the bend) and the load-off (the return) from the
// bird's real weight, read by the plant's own recorder (landingLoadDropM /
// landingLoadReturnM).
[DefaultExecutionOrder(-40)]
public class BirdTriggerPerch : MonoBehaviour
{
    // ---------------------------------------------------------------- the boot
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindFirstObjectByType<BirdTriggerPerch>() != null)
            return;
        GameObject host = new GameObject("BirdTriggerPerch");
        DontDestroyOnLoad(host);
        host.AddComponent<BirdTriggerPerch>();
    }

    // ------------------------------------------------------------------ state
    private enum Stage { Seek, Armed, Done }

    private Stage stage = Stage.Seek;
    private float bootAt;
    private float nextSeekAt;

    private PlantTree tree;
    private Bird bird;
    private BirdController control;
    private Rigidbody2D body;
    private BirdPerch markPad;
    private int slotIndex = -1;
    private Vector2 slotPoint;
    private Transform mark;

    // B-PERCH-4: the exposure pick (which ring mark is not ringed by the crown).
    private string exposureLog = "none";
    private readonly List<SpriteRenderer> leafSprites = new List<SpriteRenderer>(128);
    private float seatPadY = float.NaN;      // the pad's own Y at the pick
    private float seatMeasured = float.NaN;  // claws-on-the-wood-surface seat, m
    private float seatFace = float.NaN;      // the support face the rule read, m
    private string seatHitsLog = "";         // the ray's tree-solid hit ys (ARM note)

    // B-PERCH-7 (26.09): THE CLAWS-CAN-REACH SEAT. The B-PERCH-5/6 seat read the
    // wood's face at the SLOT's own x - but the sitter keeps its arrival x
    // (HoldBodyPosition: |bodyX - pad.x| < 0.4 -> the bird's own x), and the take
    // bPCH6b seated 0.147 m left of the slot: the face under its claws was ~0.09 m
    // ABOVE the face under the slot's x, so the seat pull yanked the bird 0.145 m
    // down THROUGH the twig (support dirt at 1.688 m, solidFramesCore 597, the
    // eyes: "the claws pass through the wood"). The seat is now read at the
    // sitter's own CLAW BAND columns (centre + both sides) and refreshed while
    // the bird is in reach, so the hold the capture binds is the one the claws'
    // own column gives. The ARM note carries the per-column hit lists (clawHits).
    private const float ClawHalfW = 0.03f;   // the claws' own half-span, m
    // B-PERCH-13 (26.09): THE FEET'S OWN CLEARANCE. The B-PERCH-12 verdict named
    // the phase's fault with its own numbers: the leaf/wood test ran around the
    // seated BODY box (0.15 m above the claws), so a candidate could win on
    // exposure while the feet stood inside the crown's leaf mass (slot 11,
    // eyes 2/10: "the wood's top edge ~30 px under the feet"). The test now runs
    // around the CLAW PLANE the sitter will hold - the wood's face under the
    // slot's centre column (the accepted face rule) - scanning the claws' own
    // columns +- the claw band, 0.03 m under the plane up to 0.03 over it; a
    // candidate whose claw plane carries LEAF mass within ClawClearMin is
    // INADMISSIBLE whatever its body-box band, and the pick takes the best
    // admissible one (the refused ones are logged, `inadm`).
    private const float ClawClearMin = 0.03f;   // a leaf within this of the claw plane disqualifies the slot, m
    private const float ClawClearBand = 0.03f;  // the slab's half-height around the claw plane, m
    // B-PERCH-12 (26.09): THE SEAT STAYS ON THE MARK'S BAND. 0.40 let the
    // claws' own column drag the sitter anywhere within 40 cm of the accepted
    // seat: take bPCH12a (the level-band pick, slot 11) had the bird arrive
    // |dx| 0.146 m short of the mark, over the limb's end - the read gave
    // seat -0.4343 against the pad's own 0.0949 (accepted by 0.003 against
    // the window) and the seat TELEPORTED the body 0.58 m down (y 0.4149 ->
    // -0.1663 between 13.110 and 13.120) onto a ledge 0.55 m below the mark's
    // band; the pose then read as the bird standing off the bark's edge (the
    // leg column carries 27 px of air). The claws' read is now trusted only
    // within 0.30 m of the pad's own value; outside it the seat keeps the
    // pad's own value AND the pad centres the sitter on the mark's own x
    // (holdXWin below). 0.30 keeps the accepted fork seat (bPCH11a: the claws'
    // own column at 0.3364 against the pad's 0.0949, a 0.24 disagreement - the
    // pose the eyes scored 6) and refuses the cliff (bPCH12a: 0.53 off).
    private const float ClawSeatWin = 0.30f; // the measured seat stays inside this of the pad's own
    // B-PERCH-14 (26.09): THE SEAT AT THE EXPOSED FORK. The eyes read the seat
    // zoom 3-4/10 at the fork with the fault always named the same way: the feet
    // sit INSIDE the branch's brown fill, several px below the top silhouette
    // (the claw band's centre column rides the crotch's inner slope while the
    // crest of the band's first faces sits ~6 px higher - the fill's top edge
    // reads there). The seat at a sloped band is now the CREST-AND-CENTRE MEAN:
    // the centre column's seat (gate unchanged, ClawSeatWin on it) lifted by
    // half of the band's own rise, capped at ClawLiftCap. A level band is
    // untouched (spread 0 -> lift 0). The A/B on the owner camera (takes
    // bPCH14a/b/c at seatDy 0.335 / 0.365 / 0.393, the same box): the
    // alternative eye reads 4/10 "sunk in", 9/10 "OK", 5/10 "floating" -
    // best B (0.365 = the centre's 0.335 + half of the 0.058 band rise);
    // the lift reproduces it: 0.3352 + 0.0289 = 0.3641.
    private const float ClawLiftCap = 0.06f; // the crest-and-centre mean's lift cap, m
    private float seatLift;                  // the last lift added to the claws' seat, m
    // The hold's x window while the claws' own column disagrees with the pad:
    // the bird is centred on the mark instead of keeping its arrival x.
    private const float SeatHoldXWin = 0.06f;
    private float seatClawX = float.NaN;     // the x the claws-column seat read last
    private int seatClawSrc;                 // 0 flag, 1 slot column, 2 claws column
    private float seatClawLogged = float.NaN;// the last seat value written to the record
    private int seatClawNotes;               // CLAWSEAT rows written (capped)
    private string clawReachLog = "none";    // the pick's claw-band test (ARM note)
    private string clawHitsLog = "";         // per-column hit lists (ARM note)
    private float nextSeatReadAt = -1f;
    private readonly List<float> clawYs = new List<float>(8);

    private float birdMass = 0.5f;

    // guardian
    private readonly List<Collider2D> treeSolid = new List<Collider2D>(64);
    private readonly List<Collider2D> birdCore = new List<Collider2D>(8);
    private readonly List<Collider2D> birdTouch = new List<Collider2D>(8);
    private readonly List<Collider2D> ringCols = new List<Collider2D>(32);
    private int scans;
    private int solidFramesCore;
    private int solidFramesTouch;
    private int solidPairsCorePeak;
    private string firstSolidName = "";
    private float firstSolidT = -1f;
    private int ringHitSlotMask;
    private int ringTouchFrames;

    // the perch
    private bool padsDown;
    private int padsDisabled;
    private int offers;
    private int sitwNotes;
    private float nextSitwAt;
    private bool ringSeated;
    private bool everFlew;
    // B-PERCH-10: the seat is decided at the PHYSICS STEP (FixedUpdate) and
    // the release keeps a grace - the drive's per-frame flight write must not
    // hide the perch from the seat's read (see the FixedUpdate note).
    private float ringUnperchedSince = -1f;
    private const float ringReleaseGrace = 0.25f;
    // B-PERCH-11: THE SETTLE WINDOW. On the seat the trigger opens a window in
    // the controller (perchSettleUntil): while it runs the step state reads
    // Sit, the pad is kept through the drive's per-frame fly flicker, and the
    // wander's own grounded read returns (bPCH10b: the bird left 0.06 s after
    // the touch - mode=2 at every step; bPCH3d, the accepted take: mode=4).
    private const float ringSettleWindow = 2.5f;
    private float seatAt = -1f;
    private float releaseAt = -1f;
    private int seatCount;
    private float seatHeldMax;
    private float airborneSince = -1f;
    private float noGuideUntil;
    private bool guideOn;
    private bool guideRetired;
    private int guideEngages;
    private float firstGuideAt = -1f;
    private int woodSitsBlocked;
    private bool woodBlockActive;
    private float lastWoodBlockAt = -9f;
    private float sitNoPadSince = -1f;
    private int writes;
    private float writeLogAt = -1f;
    private int writeProbe;

    // output
    private string outDir;
    private string label = "btrig";
    private readonly StringBuilder ev = new StringBuilder(4096);
    private float nextFlushAt;
    private float nextLoadAssertAt;
    private float nextHeartbeatAt;
    private bool flushed;

    // -------------------------------------------------------------------- boot
    private void Awake()
    {
        bootAt = Time.time;
        nextSeekAt = 0f;
        Application.quitting += FlushAll;
    }

    private void OnDestroy()
    {
        FlushAll();
        Application.quitting -= FlushAll;
    }

    private void Update()
    {
        if (stage == Stage.Done)
            return;
        if (stage == Stage.Seek)
        {
            if (Time.time < nextSeekAt)
                return;
            nextSeekAt = Time.time + 0.5f;
            TryArm();
            return;
        }

        // Armed. Seats, the mass hand-off, and the guide decision all read the
        // controller's own state; the velocity write lives in FixedUpdate.
        if (bird == null || control == null || tree == null || body == null)
            return;

        bool flight = IsFlight(control.mode);
        bool perched = control.IsPerched;
        float dist = MarkDist();

        // The pads stand down on the first airborne frame — the sheet's "a bird
        // may sit ONLY on trigger points" read literally for this run.
        if (!padsDown && (flight || (Time.time > bootAt + 1f && !perched)))
            StandPadsDown();

        // The ring seat: a seat can only be the target mark once the pads are
        // down (every other pad's collider is dead), so no distance test is
        // trusted for the count — only the perch state and the flight history.
        if (flight)
            everFlew = true;
        // B-PERCH-10: the seat/leave decision moved to FixedUpdate. In Update
        // it was blind: the drive writes its mode every FRAME, so while the
        // bird sat welded at the ring the wander's Fly phase kept this read at
        // flight=1/perched=0 (bPCH10a Player.log hbU: mode=2/3, perch=0 across
        // the whole hold while the physics steps sat at mode=4). The physics
        // step sees the settled state (the CLAWSEAT/PAD_OFFER rows prove it).

        // Airborne clock + the guide decision.
        if (!flight)
            airborneSince = -1f;
        else if (airborneSince < 0f)
            airborneSince = Time.time;

        // B-PERCH-9 probe: while the bird sits at the ring unseated, sample the
        // controller's own view (capped, every 4 s) so the record names which
        // door is shut.
        if (!ringSeated && sitwNotes < 12 && Time.time >= nextSitwAt
            && (perched || control.mode == BirdMode.Sit))
        {
            sitwNotes++;
            nextSitwAt = Time.time + 4f;
            Note("SITW", Time.time, "perched=" + (perched ? 1 : 0)
                + " mode=" + (int)control.mode + " hunt=" + (control.IsHuntSoil() ? 1 : 0)
                + " flew=" + (everFlew ? 1 : 0) + " padDown=" + (padsDown ? 1 : 0));
        }

        // The latch: once leading, the guide stays on until the bird is seated
        // or out of the air. The old bare `dist > 0.9` boundary sat exactly on
        // the bird's hover distance and flapped the guide on and off (run
        // bLND2d_tree: 37 engages, 304 writes, the bird never turned).
        // B-PERCH-9: the approach latch (the bPCH8c shape). The brain flips
        // the mode out of the flight set at dist ~0.85 (bPCH7c/bPCH9a: 11.97,
        // writes 20) and the guide died with it, so the ring's arrival was the
        // unguided fall. While the bird is close and above the branch the
        // guide survives its own mode flip and keeps steering to the hold.
        bool aboveBranch = body.position.y > slotPoint.y + 0.10f;
        bool wantGuide = (flight || (guideOn && dist < 1.2f && aboveBranch))
            && !ringSeated && !perched && !guideRetired
            && Time.time >= noGuideUntil
            && (guideOn || dist > 0.45f
                || (airborneSince > 0f && Time.time - airborneSince > 5f));
        if (wantGuide && !guideOn)
        {
            guideOn = true;
            guideEngages++;
            if (firstGuideAt < 0f)
                firstGuideAt = Time.time;
            Note("GUIDE_ON", Time.time, "dist=" + N(dist) + " alt=" + N(body.position.y));
            Debug.Log("[BTRIGPERCH] guide ON t=" + N(Time.time) + " dist=" + N(dist));
        }
        else if (!wantGuide && guideOn)
        {
            guideOn = false;
            Note("GUIDE_OFF", Time.time,
                "dist=" + N(dist) + " seated=" + (ringSeated ? 1 : 0));
            Debug.Log("[BTRIGPERCH] guide OFF t=" + N(Time.time) + " dist=" + N(dist));
        }

        // The mass, re-asserted so a stray write cannot unhook it.
        if (ringSeated && Time.time >= nextLoadAssertAt)
        {
            nextLoadAssertAt = Time.time + 0.5f;
            if (tree.perchLoadKg != birdMass)
                tree.perchLoadKg = birdMass;
        }

        if (Time.time >= nextHeartbeatAt)
        {
            nextHeartbeatAt = Time.time + 2f;
            Debug.Log("[BTRIGPERCH] hbU t=" + N(Time.time) + " mode=" + (int)control.mode
                + " spl=" + (body != null && body.simulated ? 1 : 0)
                + " flight=" + (flight ? 1 : 0) + " perch=" + (perched ? 1 : 0)
                + " dist=" + N(dist) + " guide=" + (guideOn ? 1 : 0)
                + " writes=" + writes + " seats=" + seatCount);
        }

        if (Time.time >= nextFlushAt)
        {
            nextFlushAt = Time.time + 2f;
            FlushAll();
        }
    }

    private static bool IsFlight(BirdMode m)
    {
        return m == BirdMode.Fly || m == BirdMode.Glide || m == BirdMode.Attack;
    }

    private float MarkDist()
    {
        if (body == null || mark == null)
            return 999f;
        return ((Vector2)body.position - (Vector2)mark.position).magnitude;
    }

    // ------------------------------------------------------------- the arming
    private void TryArm()
    {
        PlantTree found = null;
        PlantTree[] trees = FindObjectsByType<PlantTree>(FindObjectsSortMode.None);
        for (int i = 0; i < trees.Length; i++)
        {
            if (trees[i] != null && trees[i].LandingTriggerCount > 0)
            {
                found = trees[i];
                break;
            }
        }
        if (found == null)
        {
            if (Time.time - bootAt > 20f)
            {
                stage = Stage.Done;
                Debug.Log("[BTRIGPERCH] idle: no landing ring in the scene (" + N(Time.time) + " s)");
            }
            return;
        }

        Bird foundBird = null;
        Bird[] birds = FindObjectsByType<Bird>(FindObjectsSortMode.None);
        for (int i = 0; i < birds.Length; i++)
        {
            Bird b = birds[i];
            if (b == null || b.controller == null)
                continue;
            bool driven = b.GetComponent<BirdDrive>() != null
                || b.GetComponent<BirdFlockDrive>() != null;
            if (!driven)
                continue;
            foundBird = b;
            break;
        }
        if (foundBird == null)
        {
            if (Time.time - bootAt > 20f)
            {
                stage = Stage.Done;
                Debug.Log("[BTRIGPERCH] idle: ring found, no driven bird (" + N(Time.time) + " s)");
            }
            return;
        }

        tree = found;
        bird = foundBird;
        control = bird.controller;
        Transform bodyT = bird.transform.Find("Body");
        body = bodyT != null ? bodyT.GetComponent<Rigidbody2D>() : null;
        if (body == null && bird.bodyCollider != null)
            body = bird.bodyCollider.attachedRigidbody;
        if (body == null)
        {
            stage = Stage.Done;
            Debug.Log("[BTRIGPERCH] idle: no body rb");
            return;
        }

        Vector2[] slots = tree.GetLandingSlots();
        Transform[] marks = tree.GetLandingMarkers();
        if (slots == null || marks == null || slots.Length == 0)
        {
            stage = Stage.Done;
            Debug.Log("[BTRIGPERCH] idle: ring empty");
            return;
        }

        // The guardian's shelves: every solid collider the tree owns (the
        // exposure pick below scores the wood overlap, so it needs the list
        // before it runs); the bird's own colliders split later into the seat
        // device (triggers) and the body.
        tree.GetComponentsInChildren(true, treeSolid);
        int solid = 0;
        for (int i = treeSolid.Count - 1; i >= 0; i--)
        {
            Collider2D c = treeSolid[i];
            if (c == null || c.isTrigger)
                treeSolid.RemoveAt(i);
            else
                solid++;
        }

        // B-PERCH-4 (26.09, bird lane): THE PERCH TO EXPOSED WOOD. The reads
        // named the pocket: slot 7 rings the sitter in the crown at any seat
        // height ("a bird sitting inside a bush", 4/10). The pick now scores
        // every bending ring mark by the open air around the seated body (leaf
        // sprites hugging the body's sides / over the head = the pocket) and
        // seats the most exposed one. A hand probe still wins (-perchSlot);
        // the old chain (flag -> span -> fork) stays as the fallback.
        float seatDyPick = ReadFloat("-perchSeatDy", -0.037f);
        int pick = Mathf.RoundToInt(ReadFloat("-perchSlot", -1f));
        if (pick < 0 || pick >= slots.Length || !Bends(marks[pick])
            || tree.GetLandingKind(pick) == LandingKind.Top)
        {
            pick = PickExposed(slots, marks, seatDyPick, out exposureLog);
        }
        if (pick < 0)
        {
            int flag = Mathf.Clamp(tree.landingLoadSlot, 0, slots.Length - 1);
            if (Bends(marks[flag]) && tree.GetLandingKind(flag) != LandingKind.Top)
                pick = flag;
        }
        if (pick < 0)
        {
            for (int i = 0; i < slots.Length; i++)
                if (tree.GetLandingKind(i) == LandingKind.Span && Bends(marks[i]))
                {
                    pick = i;
                    break;
                }
        }
        if (pick < 0)
        {
            for (int i = 0; i < slots.Length; i++)
                if (tree.GetLandingKind(i) == LandingKind.Fork && Bends(marks[i]))
                {
                    pick = i;
                    break;
                }
        }
        if (pick < 0)
        {
            stage = Stage.Done;
            Debug.Log("[BTRIGPERCH] idle: no bending ring mark");
            return;
        }

        slotIndex = pick;
        slotPoint = slots[pick];
        mark = marks[pick];
        label = ReadArg("-label", "btrig", 40);
        string outArg = ReadArg("-out", "", 200);
        outDir = !string.IsNullOrEmpty(outArg)
            ? outArg
            : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Trials", "Runs"));

        // The target mark becomes the seat the accepted pad machinery knows:
        // a BirdPerch on the ring's own circle (it is a child of the wood, so
        // the sitter rides the bend). The other 15 marks stay bare triggers —
        // the bird may meet them (counted) but not sit on them.
        markPad = mark.GetComponent<BirdPerch>();
        if (markPad == null)
            markPad = mark.gameObject.AddComponent<BirdPerch>();

        // B-PERCH-1/3 (25.09, bird lane): the seat height is a run flag so the
        // default scene keeps the accepted pose. Negative = the bird sits lower
        // on the pad: at the crown slots the ring mark floats over the leaf pads,
        // so lowering the sitter puts the claws on the wood's top edge instead.
        // Default -0.037 m is the measured seat (B-PERCH-3, adopted 25.09 on the
        // seat-lift A/B): the claw strip (rows 597-603) meets the wood's top edge
        // (~602) and the eyes read "the feet stand ON the branch" (7/10 against
        // 4/10 for the deeper -0.075, which put claws and body below the edge);
        // the core contact is unchanged (620 solid frames). -0.12 sinks the body
        // into the trunk. The flag overrides for sweeps.
        // B-PERCH-4 (26.09): at the wood-exposed slots the pad sits inside the
        // wood and the claws end 20-35 px short of the surface (the takes
        // bPCH4x/y: "the body sinks into the trunk", 3-4/10) - the global
        // -0.037 was tuned at the crown slot 7. A per-slot measurement is the
        // named fix; its first probe mis-reads (see below), so it runs gated.
        // B-PERCH-4 (26.09): THE MEASURED SEAT - experiment, gated. The first
        // probe (a downward ray from slot.y+0.8, first hit by distance) read the
        // structure ABOVE the slot: at slot 7 it computed +0.058 against the
        // accepted -0.037, and the take bPCH4z7_tree floated (solidFramesCore 0,
        // the seat parked at 24.9 s). B-PERCH-5 (26.09) re-made the probe: the
        // hit NEAREST the mark's circle (the wood face under the claws) and every
        // hit y logged in the ARM note (seatHits). The DEFAULT stays the accepted
        // -0.037 (or the flag); the measured value runs only under -perchSeatAuto 1
        // until the slot-7 verification reproduces -0.037.
        float padY = markPad.CurrentPadWorld().y;
        seatPadY = padY;
        seatMeasured = MeasuredSeatDy(slotPoint, padY);         // the slot-column read (record)
        // B-PERCH-7: the seat the bird will actually hold comes from the CLAWS'
        // own columns, read while the bird is in reach (see FixedUpdate). At
        // ARM the pad carries the flag (the A/B sweeps), else the slot-column
        // read (the record of record), else the accepted pose - the claws'
        // column cannot be read before the bird is there.
        float seatFlagF = ReadFloat("-perchSeatDy", float.NaN);
        float seatDy;
        seatClawSrc = 0;
        if (!float.IsNaN(seatFlagF))
        {
            seatDy = seatFlagF;
        }
        else if (!float.IsNaN(seatMeasured))
        {
            seatDy = seatMeasured;
            seatClawSrc = 1;
        }
        else
        {
            seatDy = -0.037f;
        }
        markPad.seatDy = seatDy;
        // B-PERCH-12: the hold's x window starts at the accepted far value; the
        // claws' own column narrows it while it disagrees with the pad's band.
        markPad.holdXWin = BirdPerch.HoldXWinFar;

        // The load path is pointed at this mark (the runner's flags do the same;
        // asserting it here keeps a hand-run honest).
        tree.loadOnLandingTrigger = true;
        tree.landingLoadSlot = slotIndex;
        tree.perchLoadKg = 0f;

        birdMass = bird.totalMass > 0.01f ? bird.totalMass : 0.5f;

        // (The tree's solid colliders were collected before the pick.)
        ringCols.Clear();
        for (int i = 0; i < marks.Length; i++)
        {
            Collider2D c = marks[i] != null ? marks[i].GetComponent<Collider2D>() : null;
            if (c != null)
                ringCols.Add(c);
        }
        List<Collider2D> mine = new List<Collider2D>(16);
        bird.GetComponentsInChildren(true, mine);
        for (int i = 0; i < mine.Count; i++)
        {
            Collider2D c = mine[i];
            if (c == null)
                continue;
            if (c.isTrigger)
                birdTouch.Add(c);
            else
                birdCore.Add(c);
        }

        stage = Stage.Armed;
        seatAt = -1f;
        noGuideUntil = 0f;
        Note("ARM", Time.time, "slot=" + slotIndex + " kind=" + tree.GetLandingKind(slotIndex)
            + " mass=" + N(birdMass) + " point=" + N(slotPoint.x) + ":" + N(slotPoint.y)
            + " solids=" + solid + " ring=" + ringCols.Count + " core=" + birdCore.Count
            + " seat=" + N(markPad.seatDy) + " seatPadY=" + N(seatPadY)
            + " seatMeasured=" + N(seatMeasured) + " seatFace=" + N(seatFace)
            + " seatSrc=" + seatClawSrc + " clawX=" + NF(seatClawX)
            + " seatWin=" + N(ClawSeatWin)
            + " seatHits=" + seatHitsLog
            + " clawHits=" + clawHitsLog
            + " clawReach=" + clawReachLog
            + " clawSky=" + bestSkyLog
            + " expo=" + exposureLog);
        Debug.Log("[BTRIGPERCH] armed slot=" + slotIndex + " kind=" + tree.GetLandingKind(slotIndex)
            + " mass=" + N(birdMass) + " point=" + N(slotPoint.x) + "," + N(slotPoint.y)
            + " solids=" + solid + " ring=" + ringCols.Count + " out=" + outDir
            + " seat=" + N(markPad.seatDy) + " seatMeasured=" + N(seatMeasured)
            + " seatFace=" + N(seatFace) + " seatSrc=" + seatClawSrc
            + " seatHits=" + seatHitsLog + " clawHits=" + clawHitsLog
            + " clawReach=" + clawReachLog
            + " expo=" + exposureLog);
        FlushAll();
    }

    private static bool Bends(Transform m)
    {
        return m != null && m.parent != null && m.parent.GetComponent<Rigidbody2D>() != null;
    }

    // The wood's top surface at the slot's x: the FIRST tree-solid face UNDER the
    // slot's top overhang. The first-hit rule (B-PERCH-4) read the structure ABOVE
    // the slot (slot 7: +0.058 against the accepted -0.037); the nearest-face rule
    // (B-PERCH-5) then degenerated at the wood-exposed forks, where a face sits AT
    // the pad's own level (slot 1: -0.3744, 0.000 away) and the claws ended 0.07-0.12 m
    // short (take bPCH5b: seat -0.03, read 1/10). B-PERCH-6: the topmost hit belongs
    // to the structure above the slot, so the support face is the first hit below it
    // (slot 1: -0.2825 -> seat +0.0619; slot 7: 0.1736 -> -0.0354, the accepted).
    // Boxes of the blocky rig are honest here (the ray meets the face, not the AABB).
    // NaN when no wood sits under the x. Every hit y lands in the ARM note (seatHits)
    // so the next round can re-tune the rule from the record alone.
    private float MeasuredSeatDy(Vector2 slotPoint, float padY)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            new Vector2(slotPoint.x, slotPoint.y + 0.8f), Vector2.down, 1.6f);
        List<float> ys = new List<float>(8);
        StringBuilder log = new StringBuilder(64);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || !treeSolid.Contains(c))
                continue;
            float y = hits[i].point.y;
            ys.Add(y);
            if (log.Length < 64)
                log.Append(N(y)).Append(';');
        }
        seatHitsLog = log.Length > 0 ? log.ToString().TrimEnd(';') : "none";
        if (ys.Count == 0)
        {
            seatFace = float.NaN;
            return float.NaN;
        }
        // top -> bottom (RaycastAll's own order is not documented)
        ys.Sort((a, b) => b.CompareTo(a));
        float face = ys.Count >= 2 ? ys[1] : ys[0];
        seatFace = face;
        // The claws' own band (ClawBand, 3 cm) hangs below the sitter's body box:
        // the seat that puts the claws' tips ON the face is the face minus the band.
        // The rule reproduces the accepted -0.037 at slot 7 within 0.3 px (face
        // 0.1736 - pad 0.179 - 0.03 = -0.0354; the -0.0054 seat floated 2-6 px).
        return face - padY - ClawBand;
    }

    // B-PERCH-7 (26.09): the wood's faces under a world x - every tree-solid hit
    // of a down-ray from (x, fromY) with `range`, top -> bottom (clawYs). False
    // when the ray finds no wood. The optional log collects the raw hit ys.
    private bool FacesUnder(float x, float fromY, float range, StringBuilder log)
    {
        clawYs.Clear();
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            new Vector2(x, fromY), Vector2.down, range);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || !treeSolid.Contains(c))
                continue;
            clawYs.Add(hits[i].point.y);
            if (log != null && log.Length < 88)
                log.Append(N(hits[i].point.y)).Append(';');
        }
        if (clawYs.Count == 0)
            return false;
        clawYs.Sort((a, b) => b.CompareTo(a));
        return true;
    }

    // B-PERCH-13 (26.09): the wood's face under a world x by the accepted rule -
    // the FIRST hit UNDER the top overhang (MeasuredSeatDy's ys[1]), with no
    // record side effects. NaN when the ray finds no wood. The claw-plane slab
    // of the feet's-own-clearance test is placed on this face.
    private float FaceUnder(float x, float fromY, float range)
    {
        if (!FacesUnder(x, fromY, range, null))
            return float.NaN;
        return clawYs.Count >= 2 ? clawYs[1] : clawYs[0];
    }

    // The seat read at the sitter's OWN claw-band columns (B-PERCH-7). The
    // support face is the FIRST tree-solid hit going DOWN FROM THE SITTER'S OWN
    // SOLE PLANE at the centre claw column - the surface the claws will meet. A
    // ray from far above reads the CANOPY over the bird instead: take bPCH7a's
    // claw column carried five faces 0.20-0.28 m above the seat, the ys[1] rule
    // (B-PERCH-6) picked a canopy face, the read left ClawSeatWin and the seat
    // kept the slot-column value - the take repeated bPCH6b's fault exactly
    // (solidFramesCore 597, identical). The claws' own tips ARE the perch
    // volume's bottom (Bird.cs builds the volume down to the feet's bottom), so
    // the seat that stands them ON the surface is the face itself, no band. A
    // centre with no wood falls back to a side column; every column's hit list
    // lands in the ARM/SEAT notes (clawHits). A read outside ClawSeatWin of the
    // accepted -0.037 returns NaN (the caller keeps its own value).
    // THE A/B IN THE EYES (26.09 ~03:4x): this rule on the free-pick owner take
    // read sequence 3 / contact 4 (bPCH7c); the "nearest face within +-0.15"
    // variant with ClawSeatWin 0.75 read sequence 2 / contact 3 (bPCH7d) - the
    // wider band caught a crown twig 0.117 m above the claws and stood the bird
    // 0.07 m above the twig it met. The first-hit rule stays the code base.
    private float ClawSeatDy(float clawsX, float padY, float refSoleY, float slotX)
    {
        seatClawX = clawsX;
        StringBuilder log = new StringBuilder(220);
        float centre = float.NaN;
        float side = float.NaN;
        float sideD = float.MaxValue;
        float crest = float.NaN;   // B-PERCH-14: the band's highest first face
        for (int i = -1; i <= 1; i++)
        {
            float cx = clawsX + i * ClawHalfW;
            StringBuilder one = new StringBuilder(40);
            bool has = FacesUnder(cx, refSoleY + 0.10f, 0.80f, one);
            log.Append(i == 0 ? "c=" : (i < 0 ? "l=" : "r="));
            if (has)
            {
                float f = clawYs[0];   // the first hit under the claws
                log.Append('[').Append(one).Append(']');
                if (float.IsNaN(crest) || f > crest)
                    crest = f;
                if (i == 0)
                    centre = f;
                else
                {
                    float d = Mathf.Abs(cx - slotX);
                    if (d < sideD)
                    {
                        sideD = d;
                        side = f;
                    }
                }
            }
            else
                log.Append("none");
            log.Append(' ');
        }
        clawHitsLog = log.ToString().TrimEnd();
        float face = !float.IsNaN(centre) ? centre : side;
        if (float.IsNaN(face))
            return float.NaN;
        seatFace = face;
        float seat = face - padY;
        // B-PERCH-12: the gate is the PAD's own value the caller would keep
        // (the ARM-time seatMeasured / the flag), not the -0.037 constant tuned
        // at slot 7 - at the exposed slots the pad's own value IS the band the
        // pick verified (slot 11: 0.0949), and the old constant gated the
        // band's own read out.
        float keep = markPad != null ? markPad.seatDy : -0.037f;
        if (Mathf.Abs(seat - keep) > ClawSeatWin)
            return float.NaN;
        // B-PERCH-14: the crest-and-centre mean (a sloped band = a fork's
        // crotch; the fill's top edge reads at the crest, the feet must stand
        // between it and the centre's own slope). The gate above judged the
        // centre's seat, so the accepted family is unaffected.
        seatLift = 0f;
        if (!float.IsNaN(crest) && crest > face)
        {
            seatLift = Mathf.Min((crest - face) * 0.5f, ClawLiftCap);
            seat += seatLift;
        }
        return seat;
    }

    // ------------------------------------------------- the exposure pick (B-PERCH-4)
    // The visual pocket is made of leaf sprites, not colliders (crown blocks
    // carry no solid box), so the leaf test runs on their bounds: a candidate
    // slot is scored by the best open side around the seated body plus half of
    // the head-room; a leaf block overlapping the body box drives the score
    // down. The first take on this metric (bPCH4x_tree) picked slot 2 and the
    // eyes read the body CLIPPING INTO THE TRUNK (3/10, core contact 587
    // frames) - so the wood's own solid colliders now carry a second term:
    // wood mass overlapping the body box above the claw band is penalised
    // (a slot whose sitter stands inside the wood is rejected).
    private const float BodyHalfW = 0.09f;   // seated body half-width, m
    private const float BodyHeight = 0.22f;  // claws -> crown of the head, m
    private const float ReachCap = 0.50f;    // clearance cap used for scoring
    private const float ClawBand = 0.03f;    // the wood under the claws is fine
    // B-PERCH-12 (26.09): THE TOP-EDGE BAND - MEASURED, NOT ADOPTED. The claws'
    // reach test (B-PERCH-7) asks only whether the band's columns FIND wood. At
    // the fork's crotch they find it at three different heights: take bPCH11a
    // (slot 1, kind Fork) held the band with the first faces spread 0.18 m
    // (l +0.0197 / c -0.038 / r -0.1615) and the phase-6 contact zoom read 4-6
    // ON THE POSE. The band's spread was priced here (spread 0.2337 -> pen
    // 1.16 at slot 1; the pick then took slot 11, kind Span, spread 0.0054) -
    // and the take bPCH12b read **2/10**: at slot 11 the sitter's feet stand
    // INSIDE the crown's leaf mass (the slot's own expo carried "hit", right
    // 0.0041 / top 0.0007) with the wood's top edge hidden 30 px below the
    // feet. A LEVEL band is not the pose the eyes want; the FEET's own
    // clearance is. The price is REVERTED (the pick is the B-PERCH-11 exposure
    // choice again); the read stays only as the record's own columns (f=/sp=).
    private const float BandSpreadFree = 0.04f;  // a level top edge, m (read only)
    private const float BandSpreadK = 0f;        // the price - measured, reverted

    // B-PERCH-16 (26.09): THE CLAWS' OWN SKY - the open top edge. The phase's
    // standing fault is the same in every seat read: the feet read INSIDE the
    // branch's brown fill. The pixels of the take of record (bPCH14e_tree, the
    // standing half-rise value, frame 290 mid-hold) name the cause: at the
    // fork's crotch the claw band's columns carry wood ABOVE the claw plane -
    // the left limb's wall rises 0.0586 m (the half-rise the seat parks the
    // claws below the crest) over the plane, so the leg/claw sprites are drawn
    // against the brown fill and the contact never reads as a silhouette. The
    // seat height cannot fix it (the band's slope at slot 1 is 0.2337 m across
    // 0.12 m: the eye's ladder reads 0.335 sunk / 0.365 OK / 0.393 floating on
    // the SAME wall). A candidate whose claw columns sit UNDER wood within
    // ClawSkyMin above the claw plane is priced, so the pick prefers a slot
    // whose claws stand against open air (a limb's own top edge) over a
    // slightly better exposure inside the crotch. Read only by the pick; the
    // seat math is untouched.
    private const float ClawSkyMin = 0.12f;   // the open air wanted above the claw plane, m
    private const float ClawSkyK = 12f;       // score price per metre of missing sky
    private string clawSkyLog = "none";       // the pick's sky read (ARM note)
    private string bestSkyLog = "none";       // the winning candidate's own sky read

    private int PickExposed(Vector2[] slots, Transform[] marks, float seatDy, out string log)
    {
        CollectLeafSprites();
        int best = -1;
        float bestScore = float.MinValue;
        StringBuilder sb = new StringBuilder(900);
        StringBuilder rb = new StringBuilder(1200);
        int bestAny = -1;
        float bestAnyScore = float.MinValue;
        StringBuilder xb = new StringBuilder(300);
        for (int i = 0; i < slots.Length; i++)
        {
            if (!Bends(marks[i]) || tree.GetLandingKind(i) == LandingKind.Top)
                continue;
            float score;
            string why;
            bool reach;
            bool clear;
            string reachWhy;
            Exposure(slots[i], seatDy, out score, out why, out reach, out clear, out reachWhy);
            if (rb.Length < 1100)
                rb.Append(rb.Length > 0 ? ";" : "").Append(i).Append(reachWhy);
            if (!reach)
                continue;   // B-PERCH-7: no wood under the centre claw column
            if (!clear)
            {
                // B-PERCH-13: the claw plane carries leaf mass - the pocket. The
                // candidate is refused whatever its band; it is kept as the
                // last resort (allclaw) so the take still seats if every slot is
                // leaf-dense at the feet.
                if (xb.Length < 300)
                    xb.Append(xb.Length > 0 ? ";" : "").Append(i).Append('@').Append(N(score));
                if (score > bestAnyScore)
                {
                    bestAnyScore = score;
                    bestAny = i;
                    bestSkyLog = clawSkyLog;
                }
                continue;
            }
            if (sb.Length < 900)
                sb.Append(sb.Length > 0 ? "; " : "").Append(i).Append('=')
                  .Append(N(score)).Append(why);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
                bestSkyLog = clawSkyLog;
            }
        }
        clawReachLog = rb.Length > 0 ? rb.ToString() : "none";
        if (best >= 0)
        {
            log = best + "@" + N(bestScore) + " [" + sb + "]"
                + (xb.Length > 0 ? " inadm[" + xb + "]" : "");
        }
        else if (bestAny >= 0)
        {
            best = bestAny;
            log = "allclaw " + bestAny + "@" + N(bestAnyScore) + " inadm[" + xb + "]";
        }
        else
        {
            log = "none";
        }
        return best;
    }

    private void Exposure(Vector2 slotPoint, float seatDy, out float score, out string why,
        out bool clawsReach, out bool feetClear, out string reachWhy)
    {
        // B-PERCH-7: THE CLAWS-CAN-REACH TEST. The claws hang at the slot's own
        // band columns, and a candidate whose CENTRE column finds no wood is
        // rejected outright (the sitter would grip air there). A missing side
        // column is a penalty: the claw band is wider than the wood it holds.
        bool hasC = FacesUnder(slotPoint.x, slotPoint.y + seatDy + 0.8f, 1.6f, null);
        float faceC = hasC ? clawYs[0] : float.NaN;
        bool hasL = FacesUnder(slotPoint.x - ClawHalfW, slotPoint.y + seatDy + 0.8f, 1.6f, null);
        float faceL = hasL ? clawYs[0] : float.NaN;
        bool hasR = FacesUnder(slotPoint.x + ClawHalfW, slotPoint.y + seatDy + 0.8f, 1.6f, null);
        float faceR = hasR ? clawYs[0] : float.NaN;
        clawsReach = hasC;
        // B-PERCH-12: the band's own level. A stepped band (crotch, notch) is
        // what reads as the legs sunk through the bark; the price rides the
        // score so a level top edge wins over a slightly better exposure.
        float bandSpread = BandSpread(faceL, faceC, faceR);
        float bandPen = bandSpread > BandSpreadFree
            ? (bandSpread - BandSpreadFree) * BandSpreadK : 0f;
        // B-PERCH-13: THE FEET'S OWN CLEARANCE (the slab test). The claw plane =
        // the wood's face under the slot's centre column by the accepted rule
        // (the first hit under the top overhang); the pick's own seat plane is
        // the fallback when no wood sits there. The slab runs the claws' own
        // columns +- the claw band (x) and 0.03 m under the plane up to 0.03
        // over it (y); the distance to the nearest LEAF sprite's bounds decides
        // admissibility (a leaf within ClawClearMin = the pocket).
        float planeFace = FaceUnder(slotPoint.x, slotPoint.y + seatDy + 0.8f, 1.6f);
        float planeY = float.IsNaN(planeFace) ? slotPoint.y + seatDy : planeFace;
        float clawSky = ClawSky(slotPoint.y + seatDy, slotPoint.x);
        float skyPen = (ClawSkyMin - clawSky) * ClawSkyK;
        float lx0 = slotPoint.x - ClawHalfW - ClawBand;
        float lx1 = slotPoint.x + ClawHalfW + ClawBand;
        float ly0 = planeY - ClawClearBand;
        float ly1 = planeY + ClawClearBand;
        float leafNear2 = float.MaxValue;
        for (int i = 0; i < leafSprites.Count; i++)
        {
            SpriteRenderer s = leafSprites[i];
            if (s == null)
                continue;
            Bounds b = s.bounds;
            float dx = Mathf.Max(0f, Mathf.Max(lx0 - b.max.x, b.min.x - lx1));
            float dy = Mathf.Max(0f, Mathf.Max(ly0 - b.max.y, b.min.y - ly1));
            float d2 = dx * dx + dy * dy;
            if (d2 < leafNear2)
                leafNear2 = d2;
        }
        float leafNear = leafNear2 == float.MaxValue
            ? ReachCap : Mathf.Min(ReachCap, Mathf.Sqrt(leafNear2));
        feetClear = leafNear >= ClawClearMin;
        reachWhy = ":" + (hasL ? "l" : "") + (hasC ? "c" : "") + (hasR ? "r" : "")
            + (hasC ? N(faceC) : "nowood")
            + " f=" + NF(faceL) + ";" + NF(faceC) + ";" + NF(faceR)
            + " sp=" + NF(bandSpread) + " pen=" + N(bandPen)
            + " pf=" + NF(planeFace) + " cl=" + NF(leafNear) + (feetClear ? "" : "!");
        float seatY = slotPoint.y + seatDy;
        float x0 = slotPoint.x - BodyHalfW, x1 = slotPoint.x + BodyHalfW;
        float y0 = seatY, y1 = seatY + BodyHeight;
        float left = ReachCap, right = ReachCap, top = ReachCap;
        bool overlap = false;
        for (int i = 0; i < leafSprites.Count; i++)
        {
            SpriteRenderer s = leafSprites[i];
            if (s == null)
                continue;
            Bounds b = s.bounds;
            bool yRow = b.min.y < y1 && b.max.y > y0;
            bool xRow = b.min.x < x1 && b.max.x > x0;
            if (yRow && xRow)
                overlap = true;
            else if (yRow)
            {
                if (b.max.x <= x0)
                    left = Mathf.Min(left, x0 - b.max.x);
                else if (b.min.x >= x1)
                    right = Mathf.Min(right, b.min.x - x1);
            }
            else if (xRow && b.min.y >= y1)
                top = Mathf.Min(top, b.min.y - y1);
        }
        float pen = 0f;
        for (int i = 0; i < treeSolid.Count; i++)
        {
            Collider2D c = treeSolid[i];
            if (c == null)
                continue;
            Bounds b = c.bounds;
            float ox = Mathf.Min(x1, b.max.x) - Mathf.Max(x0, b.min.x);
            float oy = Mathf.Min(y1, b.max.y) - Mathf.Max(y0 + ClawBand, b.min.y);
            if (ox > 0f && oy > 0f)
                pen += ox * oy;
        }
        score = Mathf.Max(left, right) + 0.5f * top - (overlap ? 1f : 0f) - pen * 40f
            - (hasL && hasR ? 0f : 0.6f) - bandPen - skyPen;
        why = "(" + N(left) + "," + N(right) + "," + N(top) + (overlap ? ",hit" : "")
            + ",w" + N(pen) + (hasL && hasR ? "" : ",band") + ",sp" + NF(bandSpread)
            + ",sky" + NF(clawSky) + ")";
    }

    // B-PERCH-16: the open air above the CLAW PLANE (the sitter's own seat
    // plane - where the claws' tips stand) at the claws' own columns - the
    // smallest first tree-solid hit within ClawSkyMin over the plane (a column
    // with no wood inside the window is open sky). This is the wall the eye
    // reads behind the feet: the wood above them at their own columns.
    private float ClawSky(float planeY, float slotX)
    {
        float worst = ClawSkyMin;
        StringBuilder sb = new StringBuilder(24);
        for (int i = -1; i <= 1; i++)
        {
            float cx = slotX + i * ClawHalfW;
            float open = ClawSkyMin;
            RaycastHit2D[] hits = Physics2D.RaycastAll(
                new Vector2(cx, planeY + ClawSkyMin), Vector2.down, ClawSkyMin);
            for (int h = 0; h < hits.Length; h++)
            {
                Collider2D c = hits[h].collider;
                if (c == null || !treeSolid.Contains(c))
                    continue;
                float dy = hits[h].point.y - planeY;
                if (dy < open)
                    open = dy;
            }
            if (open < worst)
                worst = open;
            sb.Append(i == 0 ? "c=" : (i < 0 ? "l=" : "r=")).Append(N(open)).Append(' ');
        }
        clawSkyLog = sb.ToString().TrimEnd();
        return worst;
    }

    // The spread of the first faces under the claw band's columns (B-PERCH-12).
    // Missing columns (no wood) are ignored; fewer than two columns left = 0.
    private static float BandSpread(float l, float c, float r)
    {
        float mn = float.MaxValue;
        float mx = float.MinValue;
        int n = 0;
        for (int i = 0; i < 3; i++)
        {
            float v = i == 0 ? l : (i == 1 ? c : r);
            if (float.IsNaN(v) || float.IsInfinity(v))
                continue;
            if (v < mn)
                mn = v;
            if (v > mx)
                mx = v;
            n++;
        }
        return n >= 2 ? mx - mn : 0f;
    }

    private void CollectLeafSprites()
    {
        if (leafSprites.Count > 0)
            return;
        SpriteRenderer[] all = tree.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer s = all[i];
            if (s == null || !s.enabled || s.sprite == null)
                continue;
            if (NameHasLeaf(s.transform))
                leafSprites.Add(s);
        }
    }

    private static bool NameHasLeaf(Transform t)
    {
        for (int up = 0; t != null && up < 6; up++, t = t.parent)
        {
            string n = t.name;
            if (n.IndexOf("Leaf", System.StringComparison.Ordinal) >= 0
                || n.IndexOf("Crown", System.StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }

    private static string ReadArg(string name, string fallback, int cap)
    {
        string[] args = LauncherArgs.Args();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != name)
                continue;
            string s = args[i + 1];
            StringBuilder ok = new StringBuilder(cap);
            for (int j = 0; j < s.Length && ok.Length < cap; j++)
            {
                char c = s[j];
                if (name == "-out" ? c >= ' ' : (char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                    ok.Append(c);
            }
            if (ok.Length > 0)
                return ok.ToString();
        }
        return fallback;
    }

    // ReadArg strips '.', so the seat offset gets its own reader.
    private static float ReadFloat(string name, float fallback)
    {
        string[] args = LauncherArgs.Args();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != name)
                continue;
            float v;
            return float.TryParse(args[i + 1], NumberStyles.Float,
                CultureInfo.InvariantCulture, out v) ? v : fallback;
        }
        return fallback;
    }

    // ----------------------------------------------------------- the pads down
    private void StandPadsDown()
    {
        padsDown = true;
        BirdPerch[] pads = tree.GetComponentsInChildren<BirdPerch>(true);
        for (int i = 0; i < pads.Length; i++)
        {
            BirdPerch pad = pads[i];
            if (pad == null || (mark != null && pad.gameObject == mark.gameObject))
                continue;
            Collider2D c = pad.GetComponent<Collider2D>();
            if (c != null && c.enabled)
            {
                c.enabled = false;
                padsDisabled++;
            }
        }
        Note("PADS_DOWN", Time.time, "count=" + padsDisabled);
        Debug.Log("[BTRIGPERCH] pads down: " + padsDisabled + " at " + N(Time.time) + " s");
    }

    // ------------------------------------------------------------ the ring seat
    private void SeatOnRing()
    {
        ringSeated = true;
        guideRetired = true;
        seatCount++;
        seatAt = Time.time;
        // B-PERCH-11: THE SETTLE WINDOW - the seat opens the controller's own
        // step-state hold (the drive's per-frame fly flicker must not lift the
        // bird 0.06 s after the touch: bPCH10b).
        if (control != null)
            control.perchSettleUntil = Time.time + ringSettleWindow;
        tree.perchLoadKg = birdMass;
        nextLoadAssertAt = Time.time + 0.5f;
        Note("SEAT", Time.time, "slot=" + slotIndex + " mass=" + N(birdMass)
            + " bodyY=" + N(body.position.y) + " markY=" + N(mark.position.y)
            + " bodyX=" + N(body.position.x) + " seat=" + N(markPad != null ? markPad.seatDy : 0f)
            + " seatSrc=" + seatClawSrc + " lift=" + N(seatLift) + " clawX=" + NF(seatClawX)
            + " holdXWin=" + N(markPad != null ? markPad.holdXWin : 0f)
            + " clawHits=" + clawHitsLog);
        Debug.Log("[BTRIGPERCH] SEAT on ring slot=" + slotIndex + " t=" + N(Time.time)
            + " mass=" + N(birdMass) + "kg -> load ON");
    }

    private void LeaveRing()
    {
        ringSeated = false;
        releaseAt = Time.time;
        if (control != null)
            control.perchSettleUntil = 0f;
        // B-PERCH-12: the hold's x window returns to the accepted far value.
        if (markPad != null)
            markPad.holdXWin = BirdPerch.HoldXWinFar;
        if (seatAt > 0f)
        {
            float held = Time.time - seatAt;
            if (held > seatHeldMax)
                seatHeldMax = held;
        }
        tree.perchLoadKg = 0f;
        noGuideUntil = Time.time + 1.0f;
        Note("RELEASE", Time.time, "held=" + N(seatAt > 0f ? Time.time - seatAt : 0f)
            + " markY=" + N(mark.position.y));
        Debug.Log("[BTRIGPERCH] RELEASE t=" + N(Time.time) + " seatHeld="
            + N(seatHeldMax) + "s -> load OFF");
    }

    // --------------------------------------------------------------- the guide
    private void FixedUpdate()
    {
        if (stage != Stage.Armed || body == null)
            return;

        // The guardian: every 4th fixed step (50 Hz at 200 Hz physics), the
        // bird's own geometry against every solid collider the tree owns, and
        // the bird's touch against the ring's marks. AABB first, the solver's
        // own Collider2D.Distance to confirm.
        if ((Time.frameCount & 3) == 0)
            GuardianScan();

        // B-PERCH-10: THE SEAT'S OWN READ. The seat was decided in Update and
        // the drive writes its mode every FRAME: while the bird sat welded at
        // the ring the wander's Fly phase kept the Update-side read at
        // flight=1/perched=0 (bPCH10a Player.log hbU: mode=2/3, perch=0 across
        // the whole hold; the physics steps sat at mode=4) - so onRing never
        // rose and the seat never fired (bPCH9d/bPCH10a: seats=0). Here the
        // step sees the settled state the CLAWSEAT/PAD_OFFER rows read. The
        // RELEASE keeps a grace: the flicker's single unperched step (5 ms)
        // must not drop the load off the branch - a seat ends only when the
        // bird stays unperched past the grace.
        if (!ringSeated)
        {
            if (control.IsPerched && everFlew && padsDown)
            {
                ringUnperchedSince = -1f;
                SeatOnRing();
            }
        }
        else if (control.IsPerched)
            ringUnperchedSince = -1f;
        else
        {
            if (ringUnperchedSince < 0f)
                ringUnperchedSince = Time.time;
            if (Time.time - ringUnperchedSince > ringReleaseGrace)
                LeaveRing();
        }

        // The sheet, read literally: a bird rests ONLY on a trigger point. A Sit
        // with no pad under it is allowed only when what holds the bird is dirt;
        // when the support probe finds the tree's own wood (the recorder's own
        // support column), the bird goes back into the air and the guide
        // resumes. The 0.25 s settle first lets a pad capture claim the Sit.
        bool sitting = control.mode == BirdMode.Sit;
        if (sitting && !control.IsPerched)
        {
            if (sitNoPadSince < 0f)
                sitNoPadSince = Time.time;
            if (!woodBlockActive && Time.time - sitNoPadSince > 0.25f
                && WoodSupportBelow())
            {
                woodBlockActive = true;
                woodSitsBlocked++;
                control.mode = BirdMode.Glide;
                control.preferDirtLand = false;
                Vector2 v = body.linearVelocity;
                if (v.y < 0.45f)
                {
                    v.y = 0.45f;
                    body.linearVelocity = v;
                }
                if (Time.time - lastWoodBlockAt > 1f)
                {
                    lastWoodBlockAt = Time.time;
                    Note("WOOD_SIT_BLOCK", Time.time,
                        "pos=" + N(body.position.x) + ":" + N(body.position.y));
                    Debug.Log("[BTRIGPERCH] wood sit blocked t=" + N(Time.time)
                        + " at " + N(body.position.x) + "," + N(body.position.y));
                }
            }
        }
        else
        {
            sitNoPadSince = -1f;
            if (!sitting)
                woodBlockActive = false;
        }

        if (Time.time >= writeLogAt)
        {
            writeLogAt = Time.time + 2f;
            Debug.Log("[BTRIGPERCH] hbF t=" + N(Time.time) + " mode=" + (int)control.mode
                + " spl=" + (body.simulated ? 1 : 0) + " guide=" + (guideOn ? 1 : 0)
                + " v=" + N(body.linearVelocity.x) + ":" + N(body.linearVelocity.y)
                + " pos=" + N(body.position.x) + ":" + N(body.position.y)
                + " writes=" + writes);
        }

        // B-PERCH-7: keep the seat honest while armed. The bird keeps its arrival
        // x when it seats (HoldBodyPosition), so the face its claws meet is the
        // one at ITS x, not at the slot's - re-read it while the bird is in
        // reach, so the hold the capture binds matches the claws' own column.
        // The ray starts just above the bird's own claw plane (the perch
        // volume's lowest point), so it reads the surface under the claws, not
        // the canopy over the bird. The -perchSeatDy flag still wins, and the
        // read never leaves ClawSeatWin of the accepted -0.037.
        if (markPad != null && !ringSeated && !control.IsPerched && body.simulated
            && Time.time >= nextSeatReadAt)
        {
            float bx = body.position.x;
            float dxClaw = Mathf.Abs(bx - slotPoint.x);
            // the last hand-span before the capture: read every physics step, so
            // the hold the capture binds comes from the claws' own column, not
            // from a column the bird left 0.05 s ago.
            nextSeatReadAt = Time.time + (dxClaw < 0.12f ? 0f : 0.05f);
            if (float.IsNaN(ReadFloat("-perchSeatDy", float.NaN)))
            {
                if (dxClaw < 0.35f)
                {
                    float sole = bird.perchCollider != null && bird.perchCollider.enabled
                        ? bird.perchCollider.bounds.min.y
                        : body.position.y - 0.15f;
                    float seat = ClawSeatDy(bx, seatPadY, sole, slotPoint.x);
                    if (!float.IsNaN(seat))
                    {
                        markPad.seatDy = seat;
                        seatClawSrc = 2;
                        markPad.holdXWin = BirdPerch.HoldXWinFar;
                    }
                    else if (markPad != null)
                    {
                        // B-PERCH-12: the claws' own column disagrees with the
                        // pad's band (a drop past the limb's end): the seat
                        // keeps the pad's own value and the pad centres the
                        // sitter on the mark's own x - the band the pick
                        // verified - instead of keeping the arrival x over the
                        // cliff.
                        markPad.holdXWin = SeatHoldXWin;
                    }
                    // the read's own history in the record (the seat binds at the
                    // capture; the last value before it is the one that counts).
                    // Only changes, capped - the approach is ~1 s of reads.
                    if (seatClawNotes < 60
                        && (float.IsNaN(seatClawLogged) != float.IsNaN(seat)
                            || (!float.IsNaN(seat) && Mathf.Abs(seat - seatClawLogged) > 0.004f)))
                    {
                        seatClawNotes++;
                        seatClawLogged = seat;
                        Note("CLAWSEAT", Time.time, "bx=" + N(bx) + " sole=" + N(sole)
                            + " seat=" + NF(seat) + " lift=" + N(seatLift) + " pad=" + N(markPad.seatDy)
                            + " hits=" + clawHitsLog);
                    }
                }
            }
        }

        if (!guideOn || !body.simulated)
            return;

        Vector2 p = body.position;
        // The aim is the seat pose the pad machinery itself holds: the perch
        // volume's own bottom at the mark's point. Then the hand-off fires as
        // the bird arrives, and the snap to the hold is a few centimetres.
        float aimY = slotPoint.y + 0.147f;
        if (markPad != null)
            aimY = markPad.HoldBodyPosition(bird, slotPoint.x).y;
        float dx = slotPoint.x - p.x;
        // A shallow lead only: the aim itself is the seat pose, so the bird
        // arrives at the mark instead of hovering above it (the pad capture
        // needs the perch volume to reach the trigger circle).
        float leadY = aimY + 0.12f * Mathf.Clamp01(Mathf.Abs(dx) / 0.30f);

        // B-PERCH-9: the offer at the hold. The target pad's own circle rides
        // the branch AXIS (0.31 m under the twig surface at slot 1), so its
        // own trigger door opens only after the sink; the guide knows the
        // hold pose, so it hands the pad to the controller inside the arrival
        // window (B-PERCH-8 bPCH8d: offers 8830, consumed never - the flying
        // branch cleared offeredPerch before the glide read; the B-PERCH-9
        // controller keeps this step's offer).
        float dyHold = p.y - aimY;
        if (markPad != null && Mathf.Abs(dx) < 0.12f
            && dyHold >= -0.04f && dyHold <= 0.10f)
        {
            offers++;
            if (offers == 1)
                Note("PAD_OFFER", Time.time, "x=" + N(p.x) + " y=" + N(p.y)
                    + " holdY=" + N(aimY) + " dy=" + N(dyHold));
            control.OfferPerchHold(markPad);
        }

        Vector2 want;
        want.x = Mathf.Clamp(dx * 1.7f, -1.25f, 1.25f);
        want.y = Mathf.Clamp((leadY - p.y) * 1.6f, -0.42f, 0.85f);
        float dist = (new Vector2(slotPoint.x, aimY) - p).magnitude;
        if (dist < 0.06f)
            want *= 0.25f;

        Vector2 before = body.linearVelocity;
        body.linearVelocity = Vector2.MoveTowards(
            body.linearVelocity, want, 6f * Time.fixedDeltaTime);
        writes++;
        if (writeProbe < 3 && (writes % 40) == 1)
        {
            writeProbe++;
            Debug.Log("[BTRIGPERCH] write#" + writes + " t=" + N(Time.time)
                + " before=" + N(before.x) + ":" + N(before.y)
                + " after=" + N(body.linearVelocity.x) + ":" + N(body.linearVelocity.y)
                + " want=" + N(want.x) + ":" + N(want.y));
        }

        // The flight gate the pad landing waits for, kept true while leading.
        // B-PERCH-9: steer-only - the forced Glide is skipped unless the bird
        // itself still flies (bPCH8a's unguarded force flapped the pad's
        // touchdown/bounce cycle at 200 Hz, the touch never settled).
        if (IsFlight(control.mode))
        {
            control.mode = BirdMode.Glide;
            control.preferDirtLand = false;
        }
        if (Mathf.Abs(dx) > 0.08f)
            control.SetFacing(dx > 0f ? 1f : -1f);
    }

    private void GuardianScan()
    {
        scans++;
        if (mark == null)
            return;
        int corePeak = 0;
        int touchPeak = 0;
        for (int i = 0; i < birdCore.Count; i++)
        {
            Collider2D b = birdCore[i];
            if (b == null || !b.enabled)
                continue;
            corePeak += SolidPairs(b);
        }
        for (int i = 0; i < birdTouch.Count; i++)
        {
            Collider2D b = birdTouch[i];
            if (b == null || !b.enabled)
                continue;
            touchPeak += SolidPairs(b);
        }
        if (corePeak > 0)
        {
            solidFramesCore++;
            if (corePeak > solidPairsCorePeak)
                solidPairsCorePeak = corePeak;
        }
        if (touchPeak > 0)
            solidFramesTouch++;
        if (corePeak > 0 && firstSolidT < 0f)
        {
            firstSolidT = Time.time;
            Note("SOLID_CORE", Time.time, "pairs=" + corePeak + " name=" + firstSolidName);
        }

        // The ring touch: which marks the bird's own geometry met.
        bool any = false;
        for (int i = 0; i < ringCols.Count; i++)
        {
            Collider2D c = ringCols[i];
            if (c == null || !c.enabled)
                continue;
            for (int j = 0; j < birdCore.Count; j++)
            {
                if (Overlaps(birdCore[j], c))
                {
                    ringHitSlotMask |= 1 << Mathf.Min(i, 30);
                    any = true;
                    break;
                }
            }
            if (!any)
            {
                for (int j = 0; j < birdTouch.Count; j++)
                {
                    if (Overlaps(birdTouch[j], c))
                    {
                        any = true;
                        break;
                    }
                }
            }
        }
        if (any)
            ringTouchFrames++;
    }

    private int SolidPairs(Collider2D b)
    {
        if (b == null)
            return 0;
        int n = 0;
        Bounds bb = b.bounds;
        for (int i = 0; i < treeSolid.Count; i++)
        {
            Collider2D s = treeSolid[i];
            if (s == null || !s.enabled)
                continue;
            if (!bb.Intersects(s.bounds))
                continue;
            if (Overlaps(b, s))
            {
                n++;
                if (firstSolidName.Length == 0)
                    firstSolidName = s.gameObject.name;
            }
        }
        return n;
    }

    // The recorder's own support classification (BirdTrialRecorder.SupportOf),
    // mirrored: a ray from above the bird's body down to whatever holds it. A
    // pad counts as a real seat; the tree's own wood does not.
    private bool WoodSupportBelow()
    {
        if (tree == null || body == null)
            return false;
        float fromY = body.position.y + 0.1f;
        for (int i = 0; i < birdCore.Count; i++)
        {
            Collider2D c = birdCore[i];
            if (c != null && c.enabled)
                fromY = Mathf.Max(fromY, c.bounds.max.y + 0.03f);
        }
        RaycastHit2D hit = Physics2D.Raycast(
            new Vector2(body.position.x, fromY), Vector2.down, 2f, GroundLayers.Mask);
        if (hit.collider == null)
            return false;
        if (BirdPerch.FromCollider(hit.collider) != null)
            return false;                       // a pad holds the bird: a real seat
        return hit.transform.IsChildOf(tree.transform);
    }

    private static bool Overlaps(Collider2D a, Collider2D b)
    {
        if (a == null || b == null)
            return false;
        if (!a.bounds.Intersects(b.bounds))
            return false;
        return a.Distance(b).isOverlapped;
    }

    // -------------------------------------------------------------- the output
    private void Note(string what, float t, string detail)
    {
        // The record stays comma-clean: the lane's RECORD-LINT reads every stored
        // CSV with a plain split(',') (the B-PERCH-4 ARM note's expo list broke 3
        // records), so the detail field swaps commas for semicolons.
        ev.Append(what).Append(',').Append(N(t)).Append(',')
            .Append(detail.Replace(',', ';')).Append('\n');
    }

    private static string N(float v)
    {
        return v.ToString("0.####", CultureInfo.InvariantCulture);
    }

    // (B-PERCH-8) The record must stay finite: the lint (Tools/impact-json-lint.py,
    // rule 7) rejects any recorded cell carrying a NaN/Infinity token, and two of
    // this file's numbers are legitimately unread at times - the claws' column is
    // unknown at ARM (clawX) and a claws-seat read outside ClawSeatWin returns NaN
    // during the approach. Empty, never NaN.
    private static string NF(float v)
    {
        return float.IsNaN(v) || float.IsInfinity(v) ? "" : N(v);
    }

    private void FlushAll()
    {
        if (stage != Stage.Armed || string.IsNullOrEmpty(outDir))
            return;
        try
        {
            if (!Directory.Exists(outDir))
                return;
            StringBuilder csv = new StringBuilder(ev.Length + 128);
            csv.Append("event,t,detail\n");
            csv.Append(ev);
            File.WriteAllText(Path.Combine(outDir, label + "_trigperch.csv"), csv.ToString());

            StringBuilder s = new StringBuilder(512);
            s.Append("label=").Append(label).Append('\n');
            s.Append("slot=").Append(slotIndex).Append('\n');
            s.Append("kind=").Append(tree != null ? tree.GetLandingKind(slotIndex).ToString() : "?")
                .Append('\n');
            s.Append("point=").Append(N(slotPoint.x)).Append(':').Append(N(slotPoint.y)).Append('\n');
            s.Append("birdMassKg=").Append(N(birdMass)).Append('\n');
            s.Append("seats=").Append(seatCount).Append('\n');
            s.Append("seatHeldMaxS=").Append(N(seatHeldMax)).Append('\n');
            s.Append("seatAt=").Append(N(seatAt)).Append('\n');
            s.Append("releaseAt=").Append(N(releaseAt)).Append('\n');
            s.Append("padsDisabled=").Append(padsDisabled).Append('\n');
            s.Append("offers=").Append(offers).Append('\n');
            if (control != null)
            {
                s.Append("ctrlOffersSeen=").Append(control.dbgOfferSeen).Append('\n');
                s.Append("ctrlLandRefused=").Append(control.dbgLandRefused).Append('\n');
                s.Append("ctrlLandBound=").Append(control.dbgLandBound).Append('\n');
                s.Append("ctrlSitOffer=").Append(control.dbgSitOffer).Append('\n');
                s.Append("ctrlSitAttach=").Append(control.dbgSitAttach).Append('\n');
                s.Append("ctrlSettleHold=").Append(control.dbgSettleHold).Append('\n');
                s.Append("ctrlHuntClear=").Append(control.dbgHuntClear).Append('\n');
            }
            s.Append("woodSitsBlocked=").Append(woodSitsBlocked).Append('\n');
            s.Append("guideRetired=").Append(guideRetired ? 1 : 0).Append('\n');
            s.Append("guideEngages=").Append(guideEngages).Append('\n');
            s.Append("firstGuideAt=").Append(N(firstGuideAt)).Append('\n');
            s.Append("writes=").Append(writes).Append('\n');
            s.Append("scans=").Append(scans).Append('\n');
            s.Append("solidFramesCore=").Append(solidFramesCore).Append('\n');
            s.Append("solidPairsCorePeak=").Append(solidPairsCorePeak).Append('\n');
            s.Append("solidFramesTouch=").Append(solidFramesTouch).Append('\n');
            s.Append("firstSolidName=").Append(firstSolidName).Append('\n');
            s.Append("firstSolidAt=").Append(N(firstSolidT)).Append('\n');
            s.Append("ringHitSlotMask=").Append(ringHitSlotMask).Append('\n');
            s.Append("ringTouchFrames=").Append(ringTouchFrames).Append('\n');
            s.Append("now=").Append(N(Time.time)).Append('\n');
            File.WriteAllText(Path.Combine(outDir, label + "_trigperch.summary"), s.ToString());
            flushed = true;
        }
        catch (System.Exception e)
        {
            if (!flushed)
            {
                flushed = true;
                Debug.LogWarning("[BTRIGPERCH] write failed: " + e.Message);
            }
        }
    }
}
