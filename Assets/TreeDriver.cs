using UnityEngine;

// One tree step: Sway spring/damping/wind/wake in FixedUpdate,
// Static Apple / Lilac / Hazel / Cedar / Plum picture sway in LateUpdate (no wood bodies).
// Sway flutters cached leaves and crown masses in LateUpdate (no Find).
// Otherwise TreeSpring / JointFriction order at the same
// DefaultExecutionOrder is undefined — same reason as ActuatorDriver.
[DefaultExecutionOrder(50)]
public class TreeDriver : MonoBehaviour
{
    private PlantTree tree;
    private TreeSpring[] springs = System.Array.Empty<TreeSpring>();
    private JointFriction[] frictions = System.Array.Empty<JointFriction>();

    public void RebuildCache(PlantTree owner)
    {
        tree = owner;
        springs = GetComponentsInChildren<TreeSpring>(true);
        frictions = GetComponentsInChildren<JointFriction>(true);
        System.Array.Sort(springs, CompareByHierarchyPath);
        System.Array.Sort(frictions, CompareByHierarchyPath);

        for (int i = 0; i < springs.Length; i++)
        {
            if (!ReferenceEquals(springs[i], null))
                springs[i].drivenExternally = true;
        }

        for (int i = 0; i < frictions.Length; i++)
        {
            if (!ReferenceEquals(frictions[i], null))
                frictions[i].drivenExternally = true;
        }
    }

    private static int CompareByHierarchyPath(Component a, Component b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (ReferenceEquals(a, null)) return 1;
        if (ReferenceEquals(b, null)) return -1;
        return string.CompareOrdinal(GetHierarchyPath(a.transform), GetHierarchyPath(b.transform));
    }

    private static string GetHierarchyPath(Transform t)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
        while (!ReferenceEquals(t, null))
        {
            sb.Insert(0, t.name);
            sb.Insert(0, '/');
            t = t.parent;
        }
        return sb.ToString();
    }

    void FixedUpdate()
    {
        if (ReferenceEquals(tree, null) || tree.rig != TreeRig.Sway)
            return;

        for (int i = 0; i < springs.Length; i++)
        {
            if (!ReferenceEquals(springs[i], null))
                springs[i].Apply();
        }

        for (int i = 0; i < frictions.Length; i++)
        {
            if (!ReferenceEquals(frictions[i], null))
                frictions[i].Apply();
        }

        if (tree.windScale > 0f)
            tree.ApplyWind();
        if (tree.birdWakeForce > 0f && tree.birdWakeRange > 0f)
            tree.ApplyBirdWake();
        // Injected sitter load: the plant owns its own load, so the perch
        // acceptance never depends on the bird layer.
        if (tree.perchLoadKg > 0f)
            tree.ApplyPerchLoad(Time.fixedDeltaTime);
    }

    void LateUpdate()
    {
        if (ReferenceEquals(tree, null))
            return;
        // Pack-tip oak rides one LeafNode on true tips. Petioles
        // AimPictureAlongY at the leaflet (Unity Atan2(-x,y) from +Y).
        // Pine fascicle / cedar pancakes: flutter amp splits at grow.
        // Wind force, Static look-sway, and Sway leaf flutter share
        // WindClock (Time.fixedTime) so a gust is one canopy, not a
        // sticker on a pole. Time.time is the frame clock and wandered
        // Extra wind. Hold (-treeWind 0) zeroes windScale.
        // Side forks keep going outward (Grow rest heading). ½ρv² and
        // wake use live wood rotation so a bent lateral is a plate, not
        // a rest-heading pole. Look amp uses HeadingSailMul on last
        // lookHeading (same 1…1.22 as the force). Sway leaf push is
        // world +X then InverseTransformDirection — local XY on a
        // ±fork limb rustled along the stick. Hold still bails.
        // Look / leaf season amp is PictureSeasonAmp (SeasonSailMul
        // plus winter woody 0.80). A private 0.90 / 1.18 / 0.80 table
        // left the picture off the force sail.
        // Pine/cedar stacked hats hang 0.18…0.22 off the spine; flutter
        // amp uses HeadingSailMul of hang lean, not 0° wood.
        // Deciduous dome on the leader is the same hole: wood heading
        // is ~8° of fork 28, so plate sail uses hang + crown envelope.
        // Tuft hubs rest −PalmateTrack·heading. LateUpdate peels the
        // same fraction of extra hinge / look bend so a palmate stays
        // a canopy after the first gust (grow rest left a flag). Fruit
        // and willow hang keep world-down (track 1); pine / poplar
        // needles stay on the twig (track 0). BindLeafSway caches
        // track, wood index, hang station. No name scan in LateUpdate.
        // Artist: poplar lenticel C has stem-thick arms (not 6 mm
        // hyphens); oak Dome/LobeTip ride LobeL so the palmate sinus
        // stays empty (FingerR 0.68 is the outer tooth); cedar FanM
        // and leaflet Fan1 |x|≥0.36; pine SlopeL2/R2 and Whorl off
        // the mast; poplar Waist 0.28 on KiteL; willow Hang off-pole;
        // hazel Tuft2 / bush Fill off the spine. Pine/cedar base hats
        // flutter more than the tip cap.
        // Apple Core/Dome ride LobeL so the orchard bite stays empty
        // (oak 12:14 analog); bush Core/Dome sit on TuftL; ovate
        // leaflet Waist 0.28 on Base (not a 0.52 sausage); poplar
        // leaflet kites unequal + Waist on KiteL; willow bole gets
        // one lite peel across the two Strip fibres.
        // Axis hats after bite-waist: oak Wrist / poplar Apex+Tip /
        // pine Cap / cedar FanDome / winter TwigDome / ovate+bush
        // Tips sat IN the sinus (oak Dome analog). They ride the left
        // pad. Poplar bole gets the same lite peel as willow so the
        // C-lenticels are not ticks on a silver pole.
        // Nodal tuft hub sat on the wood axis after those hats left;
        // HeadingSailMul of ~8° (fork 28) left the cluster as pole
        // flutter. Hub rides the left pad; amp is PicturePlateSail
        // of hang + leaflet envelope. Terminal leaflet rides that
        // pad so the sinus between laterals stays empty.
        // RacemeHub sat at 0.04 of wood width after the tuft left —
        // still a panicle on a pole. Hub rides the same 0.28 pad;
        // amp is PicturePlateSail of hang + bloom envelope. CatkinHub
        // 0.12 was a tick; same pad. Centre spray lance at hub origin
        // + fan 6° was a mast (Dome analog). Florets ride the hub.
        // After those pads: poplar Wrist and pine Sheath still sat
        // on the axis (Cap analog); apple Scallop1 filled the bite;
        // winter centre twig was a leafless mast. They ride the left
        // pad. OddLeaflet 0.16 on ToothR / FingerR / Reach / Twig1 /
        // FanL2 / FringeR / HemR / WhorlR so odd extras are not one
        // card. Bush / hazel / plum bole get the willow lite peel.
        // Crotch 0.55. Flutter ~4.6 cm along the gust.
        // RacemeHub / CatkinHub are wood children: Static look-sway
        // lists them at BindLookSway; florets ride the hub. Sway
        // flutter uses the grow-time leaf cache. Hub hang is the
        // 0.28 tuft pad so HeadingSailMul of wood ~8° does not
        // leave a panicle as pole flutter.
        // After panicle pads: lilac internodal scars and pine/cedar
        // shingles without a flake were still ticks / bricks on a
        // pole (willow peel analog). Bole gets PaintLiteBolePeel.
        // Pine Needle1 sat at u=0 after Sheath left the pad — a mast
        // through the fascicle (spray centre analog). Peak rides
        // Sheath; odd outer mixes 0.16. Spray 0.28×0.78 lance was a
        // pine needle on orchard / hedge; kind ovate + waist on the
        // left pad. Spur ovate gets the same waist. leafSize / fork
        // 28 / visualTaper 0.58 stay.
        // After those peels: bloom Calyx and fruit Stem sat IN the
        // petal / cheek sinus; lilac Rachis at the hub origin was a
        // mast through the panicle; spray laterals 0/2 stacked at the
        // hub; ForkFill sat on the daughter axis (T-bar leftover).
        // They ride the left / inner pad. Garden Frost / spent hip
        // leave the cane.
        // After those pads: plum Core/Dome still sat on the droplet
        // axis (apple bite analog); ovate Petiole at x=0.02 filled
        // the leaf bite from below (oak Wrist analog); spur Tip 0.06
        // sat on the blade spine; first-fork Scale chips / lilac
        // scars / C-lenticels had no flake. Core/Dome/Petiole/Tip
        // ride the left pad; depth-1 wood gets PaintLiteBolePeel.
        // After those pads: plum Knot +0.10 filled the droplet sinus
        // (apple Scallop1 analog); fruit Suture 0.02 sat between
        // Drop/DropR (oak Dome analog); ovate Notch −0.14 sat on the
        // blade spine; hazel CatkinTail sat at the hub origin (lilac
        // Rachis analog); winter spur Bud 0.04 sat on the stick.
        // Knot/Suture/Notch/Tail/Bud ride the left pad. Garden cup
        // 0° petal and leaf Blade/Midrib leave the sinus the same
        // way. leafSize / fork 28 / taper 0.58 stay.
        // After those pads: apple Scallop1 at u×0.30 = −0.084 still
        // sat IN the orchard bite (comment said LobeL); fruit cheeks
        // 0.72/0.68 overlapped into a disc; pine needles u×0.08 sat
        // on the 6 cm spine; cedar FringeM −0.12 filled the fan
        // sinus; oak BarkBreak 0.04 and Furrow3 0.06 sat on the bole
        // spine; palmate leaflet had no FingerL (mitten). Scallops
        // and cheeks ride LobeL/CheekL; needles |x|≥0.28; FringeM
        // hangs off Fan1; bark chips on the flank; FingerL on
        // LobeL0. leafSize / fork 28 / taper 0.58 stay.
        // After those pads: poplar Waist at −0.12 × 0.28 still
        // reached +0.02 and filled the kite valley (Needle1 analog);
        // ovate Waist/Tip the same on Base; pine Needle1 0.28 on
        // Sheath still crossed 0; garden ±0.22 × 0.48 and berry
        // ±0.28 × 0.62 overlapped into a disc (cheek analog).
        // Waist/Tip ride KiteL/Base; Needle1 0.18 on Sheath;
        // leaflets |x|≥0.34. leafSize / fork 28 / taper 0.58 stay.
        // After those pads: poplar KiteR 0.42 at 0.18 still reached
        // −0.03 (Waist analog); plum Core 0.36×0.58 still a sausage
        // in the droplet; pine Sheath 0.42 at −0.12 still crossed 0;
        // spur Waist 0.40 at −0.20 still reached 0; spray Waist 0.42
        // at −0.12 still crossed 0; hazel Tuft0 0.50
        // filled the coppice sinus; plum BarkSeam1 at −0.04 sat on
        // the spine (BarkBreak analog). KiteR / Core / Sheath / Waist
        // / Tuft0 ride the outer pads; seams on opposite flanks.
        // leafSize / fork 28 / taper 0.58 stay.
        // After those pads: pine Needle1 at −0.12 × 0.18 still reached
        // −0.03 (KiteR analog); ovate Base 0.44 at −0.16 still reached
        // +0.06; apple CheekL 0.56 kissed CheekR; bloom PetalL 0.70
        // fused PetalR; garden ±0.34 × 0.40 inner ±0.14; lilac 0.70
        // florets zipped the panicle. Needle1 rides Sheath; Base/cheeks
        // /petals/leaflets open the sinus. leafSize / fork 28 / taper
        // 0.58 stay.
        // After those pads: pine BaseL 0.36 at −0.22 reached −0.04 and
        // BaseR 0.34 at +0.20 reached +0.03 (fused fir hem); LayerL
        // 0.30 at −0.22 still −0.07; willow Hang2 0.48 at +0.24 sat on
        // the 11 cm spine and Arc/Drip walked INTO the mast; willow
        // Blade 0.70 at stepX×0.08 was the hang paddle; oak Flake0
        // 0.28 at −0.10 reached the bole spine; spur Tip 0.48 filled
        // the visible blade. Base/Layer ride SlopeL2/R2; Hang steps
        // outward; Blade rides Tip; Flake0 on Furrow0; spur Tip on
        // Waist. leafSize / fork 28 / taper 0.58 stay.
        // After those pads: pine SlopeL inner −0.15 / SlopeR inner
        // +0.10 left a sky slit down the fir (two tents); willow
        // Hang1/Hang2 the same through the curtain; cedar FanL/R
        // a sky table; Needle2 at +0.30 a stray stick; hazel 0.70
        // catkins zipped the hub; garden/berry terminal stacked on
        // Leaflet0. Body/BodyR / Hang4 / FanBody fill the slit on the
        // left/right pads; Needle2 on Sheath's right lip; catkin
        // packets open; Blade/Mid sit toward the trifoliate notch.
        // leafSize / fork 28 / taper 0.58 stay.
        // After those pads: Body right −0.04 / BodyR left +0.05 still
        // a sky slit (Hang4 analog); Cap right −0.05 a sky peak;
        // Hang hang-sprite inner −0.11 / Hang4 +0.07 the same mast;
        // FanBody right −0.04 / FanR left +0.10 a sky table; Needle2
        // at +0.12 never touched Sheath −0.08, and dense n=2 dumped
        // k==1 into that else. Fill / CapFill / Hang5 / FanFill ride
        // the left/right lip; Needle1 on Sheath, Needle2 at −0.08.
        // Hem BaseL/R and FanL2/Hem stay off-axis. Pine Fill / CapFill /
        // willow Hang5 / cedar FanFill sat ON the mast (cross x=0).
        // Left/right lips close the slit; Needle2 stays on Sheath.
        // Poplar kite gets the same two-lip fill. Pine/cedar bole
        // Peel2 rides the left flank so shingles are bark, not a
        // brick belt. leafSize / fork 28 / taper 0.58 stay.
        // After those lips: ForkFill 0.88 at 0.18 still belted the
        // daughter (T-bar); CollarL/R 0.52/0.46 still a mushroom;
        // oak Flake2 −0.08 × 0.18 kissed the limb spine; TrunkRime0
        // inner 0 and FrostCap0 inner −0.02 a winter barcode; spray
        // Lance −0.18 × 1.00 outer +0.32 reached back through the hub
        // on the right fan; cedar FanFill / poplar Fill outer 0 still
        // on the table/flame mast; winter Bud Tip at 0.004 on the
        // bud stick; lavender 0.78 at −0.18 a sausage on the spike;
        // garden/berry Frost 1.15 at +0.22 a table on the cane.
        // ForkFill/Collar/Flake2/Rime/Frost/Lance ride the flanks;
        // FanFill/Fill outer < 0; florets open; frost chips on the
        // right pad. leafSize / fork 28 / taper 0.58 stay.
        // After those flanks: pine Fill right −0.02 / FillR left +0.02
        // a 0.04 sky slit; CapFill the same peak; cedar FanFill right
        // −0.05 / FanFillR left +0.07 a 0.12 table; poplar Fill the
        // same flame gap; willow Hang5/6 a curtain slit; BarkPeel
        // 0.34 at +0.06 a lite belt on the spine; pine needles blunt
        // posts; plum/lilac ovate a left comma. Fill lips kiss at
        // x=0 (not a mast plate); peel on the near flank + lip;
        // NeedleTip; plum/lilac CheekR. leafSize / fork 28 / taper
        // 0.58 stay.
        // After that kiss: Fill+FillR at x=0 was still a 0.32-span
        // mast through the fir / cedar table / poplar valley; willow
        // Hang5 still kissed the 11 cm spine; fascicle needles piled
        // left of the sheath; garden 0° petal and berry SpringBloomPetal
        // crossed the head. Body/FanBody/Cap close the cone; Fill chips
        // ride those pads (outer < 0); Needle2 fans +X; 0° petal
        // |x| > half. leafSize / fork 28 / visualTaper 0.58 stay.
        // After that cone: oak/apple bole Peel 0.38/0.34 at +0.06…0.08
        // still a lite belt on the spine; pine Cap without CapR left a
        // 0.09 sky slit at the peak; sheath wholly left piled needles
        // on one lip; plum/lilac CheekR 0.28 at +0.08 was a midrib
        // chip so the ovate stayed a comma. Peel on the near flank;
        // Cap/CapR kiss at x=0; sheath cup kisses 0 with L/on-cup/R
        // fan; CheekR is the far egg half. leafSize / fork 28 /
        // visualTaper 0.58 stay.
        // After that bole: willow Hang5 at −0.22 right −0.12 / Hang6
        // at +0.20 left +0.11 left a 0.23 sky slit; cedar FanDome
        // 0.16 at −0.42 was a one-tent peak (Cap without CapR);
        // bush TuftL right −0.07 / TuftR left +0.14 two hedge tents;
        // lavender k==1 right −0.04 / k==2 left +0.08 a spike slit;
        // strawberry one petal at ±0.52 a comma disc. Hang5/Hang6
        // and FanDome/FanDomeR and TuftL/TuftR kiss at x=0; florets
        // and SpringBloomPetalR the same. leafSize / fork 28 /
        // visualTaper 0.58 stay.
        // After that hedge: poplar KiteL right −0.05 / KiteR left
        // +0.10 a 0.15 sky slit down the 15 cm flame (pine Slope
        // tents); bush Blade/Tooth/Tip stacked at −0.28 a left
        // comma (plum ovate before CheekR); PaintLiteBolePeel was
        // near-only so willow/poplar/hedge/cane were a tick on a
        // pole. KiteL/KiteR kiss at x=0; bush CheekR is the far
        // egg; helper Peel2 is the far bark flank (pine/cedar
        // share it — no second BarkPeel2). leafSize / fork 28 /
        // visualTaper 0.58 stay.
        // After that flame: apple ToothR / hazel EarR were a stub
        // so ovate Base/Waist/Tip at −0.22 stayed a left comma;
        // winter TwigFork / Dome / F were left-only (FanDome
        // without FanDomeR); bloom PetalL right −0.04 / PetalR
        // left +0.07 a 0.11 head slit; garden 0° petal and berry
        // ±0.52 a comma disc. Ovate Base/CheekR kiss at x=0
        // (teeth stay outer); winter far fork / DomeR / FR at
        // +max |X| (not a leader flag); bloom cups kiss; 0° /
        // SpringBloomPetalR kiss. leafSize / fork 28 /
        // visualTaper 0.58 stay.
        // After that ovate: poplar leaflet KiteL right −0.05 /
        // KiteR left +0.10 a 0.15 sky slit on the 7 cm tuft
        // (crown flame analog — artist kept the valley); cedar
        // Fan1 right −0.05 / Fan2 left +0.24 a 0.29 leaflet
        // tent; hazel Tuft2 left-only a one-tent coppice
        // (TwigDome without DomeR); spur extras rode a visible
        // paddle so packed CheekR never landed; Peel2 had no
        // lip (near Peel brick analog); pine 0.22 needles were
        // 1.3 cm posts. Leaflet kites and cedar Fan1/Fan2 kiss
        // at x=0; hazel Tuft2R on Tuft1; spur Hide+far half;
        // spray CheekR; Peel2Lip; needles 0.16/0.12/0.14.
        // Coppice sinus between Tuft0/Tuft1 stays empty.
        // leafSize / fork 28 / visualTaper 0.58 stay.
        // After that kiss: poplar KiteL2 at −0.30 right −0.12
        // a one-tent hem under the kissed flame (Tuft2 analog);
        // oak leader spur used |heading|>8 so fork 28×0.28≈7.8°
        // still folded the last internode back (zigzag); uniform
        // spur box was a telegraph stick; spring lime was one
        // flush card. KiteL2/KiteR2 kiss; spreadLimb 5° matches
        // OutwardSideHeading; spur PaintPictureCone; spring
        // walks kind. Oak palmate sinus stays empty.
        // After that cone: poplar Base 0.40 at −0.16 right +0.04
        // filled the hem valley; bush Dome / oak Skirt / pine Apex
        // left-only one-tents; hazel cork no lip; spring oak still
        // the leftover lime. Base/BaseR; DomeR/FillR; SkirtR;
        // Apex/ApexR kiss; leaflet LobeL2/R2 ride the lobes;
        // CorkTip; oak walks spring. leafSize / fork 28 /
        // visualTaper 0.58 stay.
        // After that hem: poplar Apex and leaflet Tip still left-only
        // peaks after KiteL/R kissed (pine Apex before ApexR);
        // orchard caudal stopped at LobeR (oak Skirt analog);
        // winter weep/flame/coppice/orchard returned before
        // TwigDomeR; bloom PetalTip left-only. ApexR/TipR/SkirtR;
        // winter kind DomeR; PetalTipR. Oak palmate sinus stays
        // empty. leafSize / fork 28 / visualTaper 0.58 stay.
        // After that peak: ovate/bush Tip and oak LobeTip still
        // left-only after CheekR / LobeR2; packed oak/ovate spur
        // skipped TipR; poplar leaflet had no Base/BaseR; hazel
        // coppice and orchard/oak summer Dome no far peak; willow
        // fibres / lilac nodes / orchard chips had no CorkTip
        // lip. TipR/LobeTipR/BaseR; DomeR on LobeR at |X|;
        // coppice FillR; FibreTip/NodeTip/ChipTip/SeamTip.
        // Oak palmate / orchard bite stay empty (no kiss at x=0).
        // leafSize / fork 28 / visualTaper 0.58 stay.
        // After that far peak: oak/poplar Wrist and ovate Petiole
        // still left-only stalks after TipR; bush Tooth left-only
        // on Blade; oak/orchard Core left-only basal; orchard no
        // near Skirt; winter spur Bud and twig bud Tip left-only;
        // garden Blade/Midrib and berry MidWaist left-only after
        // TipR/MidR; winter cane/runner bud a right comma.
        // WristR/PetioleR/ToothR/CoreR/Skirt; BudR/BudTipR/TipR;
        // BladeR/MidribR/MidWaistR; WinterBudL. Palmate sinus /
        // orchard bite stay empty. leafSize / fork 28 / taper
        // 0.58 stay.
        // After that stalk: poplar/ovate Waist still left-only on
        // KiteL/Base after WristR; plum Notch left-only; willow
        // Blade/Tip/Drip a +X comma; bush Core left-only; packed
        // ovate Waist and palmate caudal skipped WristR; packed
        // kite no WaistR; cedar Droop right-only; oak Furrow
        // cracks no CorkTip lip. WaistR/NotchR; willow BladeR/
        // TipR/DripR; CoreR; packed WristR/WaistR; DroopL;
        // FurrowTip. Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that bole lip: 28° ForkWeb/ForkFill stayed rectangles
        // (T-bar); oak branch Crack/Flake ticks with no CorkTip;
        // winter cane/runner Frost right-only after WinterBudL.
        // ForkWebLip/ForkFillLip; BarkCrackTip/FlakeTip/Flake2Tip;
        // FrostL. Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that fork lip: branch Collar and bole Buttress
        // rectangles; oak bole Flake0/1/Break without CorkTip;
        // bush Stool without a lip; pine/cedar fork Scale and
        // bush/hazel chips without ScaleTip; orchard Knob without
        // ChipTip. Palmate mitten: no ToothL / LobeR1 on leaf or
        // crown; packed spur no Tooth; pine Slope roof slats.
        // CollarLTip/RTip; ButtressLTip/RTip; Flake0Tip/Flake1Tip/
        // BreakTip; StoolLTip/RTip; BarkScaleTip/PlateTip/KnobTip;
        // ToothL/LobeR1; packed ToothL/R; pine SlopeLTip/RTip.
        // Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that collar lip: stump RootSoil/RootButt still a
        // plank on the slab; winter TrunkRime/FrostCap still a
        // frost table; cane/runner Frost/FrostL still a rectangle.
        // RootSoilLTip/RTip; RootButtLTip/RTip; TrunkRime0Tip/
        // Rime1Tip; FrostCap0Tip/1Tip; FrostTip/FrostLTip.
        // Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that stump lip: poplar C-lenticel still a hyphen;
        // spur ConeL/R a telegraph stick; winter Twig a comb of
        // posts; packed palmate stopped at ToothR (no LobeR1);
        // pine Layer/Base/Whorl still roof slats after SlopeTip;
        // cedar FanL/R still coins. BarkLensTip/CarmTip; ConeLTip/
        // ConeRTip; TwigTip/TwigForkTip; packed LobeR1; LayerLTip/
        // LayerRTip/BaseLTip/BaseRTip/WhorlLTip/WhorlRTip;
        // FanLTip/FanRTip. Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that cone lip: packed palmate / oak crown stopped at
        // LobeR1 (no LobeL1); pine Cap/Fringe still a table and posts
        // after WhorlTip; cedar FanL2/R2/M/M2 and Fringe/Hem still
        // coins. Garden petal/sepal/leaflet and berry Mid still cards
        // after FrostTip. Now: packed+crown LobeL1; CapTip/FringeTip;
        // FanL2Tip/FanR2Tip/FanMTip/FanM2Tip; FringeLTip/RTip;
        // HemLTip/RTip; PetalTip/SepalTip/LeafletTip/BladeTip;
        // LeafletMidTip. Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that outer lip: poplar CarmB still a hyphen after
        // CarmTip; pine SlopeL2/R2 still roof slats after SlopeTip;
        // cedar FanBody/FanDome/Droop still coins after FanL2Tip;
        // FringeM still a post after FringeLTip; packed palmate no
        // Finger after LobeL1; oak/leaflet LobeL1 still blunt;
        // willow Drip a paddle; apple Scallop a coin; pine Sheath
        // a bark card. Now: BarkCarmBTip; SlopeL2Tip/R2Tip;
        // FanBodyTip; FanDomeTip/FanDomeRTip; DroopTip/DroopLTip;
        // FringeMTip; packed FingerL/R + LobeL1Tip/R1Tip; leaflet
        // FanTip/FringeTip; SheathTip; DripTip; ScallopTip;
        // KnotTip. Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that distal lip: leaflet/crown inner LobeL0/LobeR
        // and caudal LobeL2/Skirt still blunt after LobeL1Tip; pine
        // Body a plank after SlopeL2Tip; plum Drop / apple Cheek
        // coins after ScallopTip; tree Calyx a bark card after
        // SheathTip; garden Spent a hip card; berry RunnerLeaf a
        // trifoliate card. Now: LobeL0Tip/LobeR0Tip/LobeL2Tip/
        // LobeR2Tip/WristTip; packed Lobe0Tip/LobeR0Tip; crown
        // LobeLTip/LobeRTip/SkirtTip; BodyTip/BodyRTip; DropTip/
        // CheekLTip; CalyxTip; SpentTip; RunnerLeafTip. Palmate
        // sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that inner lip: grow ConeCore/ConeEdge still a
        // telegraph post after spur ConeLTip; orchard Lobe/Skirt/
        // Dome still blunt after oak palmate LobeLTip; oak/leaflet
        // Finger/Tooth stubs after LobeL0Tip; poplar/pine Apex a
        // sausage after BodyTip; willow Hang/Arc paddles after
        // DripTip; ovate Ear/Petiole/Notch cards. Now: ConeCoreTip/
        // ConeEdgeLTip/RTip; orchard LobeLTip/RTip/SkirtTip/DomeTip/
        // CoreTip; oak FingerLTip/ToothLTip/FingerRTip/ToothRTip/
        // DomeTip/CoreTip/ReachTip; leaflet Finger/Tooth tips;
        // packed Finger/Tooth/Wrist tips; ApexTip; HangTip/ArcTip;
        // EarTip/PetioleTip/NotchTip. Palmate sinus / orchard bite
        // stay empty. leafSize / fork 28 / taper 0.58 stay.
        // After that distal cone: poplar leaflet/crown KiteL2 still
        // a blunt hem after KiteLTip; packed spur kite a telegraph
        // paddle; bush Core/Tuft/Dome a sofa after orchard LobeLTip;
        // hazel Tuft0 a coppice card; winter TwigDome a leafless
        // flag; oak CoreR a blunt far stalk; multi-band Taper a
        // stair after ConeCoreTip; garden Midrib/Petiole/Floret
        // cards; berry Petiole/MidWaist/RunnerLeaf2/SpringBloom
        // cards. Now: KiteLTip/KiteRTip/KiteL2Tip/KiteR2Tip;
        // packed KiteLTip; WristTip; bush CoreTip/TuftLTip/DomeTip;
        // hazel Tuft0Tip; TwigDomeTip/TwigFTip; TaperTip;
        // CoreRTip; FloretTip/MidribTip/PetioleTip; berry
        // LeafletMidWaistTip/RunnerLeaf2Tip/SpringBloomPetalTip.
        // Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that kite hem: ovate/kite/spray Waist still a
        // midrib card after KiteL2Tip; bush/hazel/pine/poplar Fill
        // a sofa/stool/mast chip; pine CapFill / cedar FanFill a
        // peak-table mast; spray Lance a paddle; PetioleR/NotchR/
        // BladeR far stubs; bloom PetalL a cup coin after PetalTip;
        // fruit Sepal/Stem/Suture bark cards; lilac Raceme/Rachis
        // and hazel Catkin packets posts. Now: WaistTip/WaistRTip;
        // FillTip/FillRTip; CapFillTip/FanFillTip/ApexFillTip; LanceTip;
        // PetioleRTip/NotchRTip/BladeRTip; PetalLTip/PetalRTip;
        // SepalLTip/RTip; StemTip; SutureTip; RachisTip;
        // RacemeTip; CatkinTailTip/CatkinTip; spur PetioleTip.
        // Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that waist lip: poplar crown/leaflet Base a hem card
        // after KiteL2Tip; packed ovate Base/CheekR a spur egg;
        // spray CheekR a far paddle; orchard CoreR a blunt far stalk;
        // ovate Base/CheekR egg coins; internodal Cone a telegraph
        // post after ConeCoreTip; garden SpentR/SpentCapR hip coins;
        // berry SpringBloom a flush card behind petals. Now:
        // BaseTip/BaseRTip; packed BaseTip/CheekRTip; spray
        // CheekRTip; orchard CoreRTip; ovate BaseTip/CheekRTip;
        // ConeTip; garden SpentRTip/SpentCapRTip; berry parent
        // sprite off. Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that hem: packed-tuft Petiole still a brown card
        // after spur PetioleTip; packed palmate WristR and oak
        // WristR far stalks; willow Blade a sickle paddle; bush
        // CheekR a hedge coin; cedar Fan1 a mid pancake; bush/hazel
        // BarkPlate a brick after BarkPlateTip; willow Hang a
        // curtain paddle after HangTip. Now: cluster PetioleTip;
        // packed+leaflet WristRTip; BladeTip; bush CheekRTip;
        // FanTip on every fan; BarkPlate a short flake + long lip;
        // Hang a C start. Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that peak: packed/leaflet/willow/bush/poplar/ovate
        // _Tip/_TipR still paddles after BladeTip / CheekRTip /
        // WristRTip; apple-96 Tip a tile; winter bud Tip a scale;
        // garden Tip/TipR cup paddles after BladeTip. Now: TipTip /
        // TipRTip; apple-96 TipTip; bud TipTip/TipRTip; garden
        // TipTip/TipRTip; berry LeafletMidTipTip/LeafletMidRTipTip.
        // Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that lip: ForkFill a sofa after Lip; bush/hazel
        // BarkPlate a brick + PlateTip a hyphen; Flake2 a brick;
        // pine/cedar shingles tiles; internodal Cone a telegraph
        // post; willow Hang a curtain paddle; leaflet Blade on the
        // sickle spine; hedge/hazel Fill a sofa; cedar Fan1 a
        // pancake; pine Needle a stick. Now: ForkFillTip; BarkPlate
        // a shorter flake + longer lip; Flake2 shorter; ScaleTip
        // longer; Cone a short core; Hang a C start; BladeTip off
        // the sickle; FillWaistTip; Fan narrower + tip off-pad;
        // Needle taper. Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that crotch: crown/tuft still cut at 8° while Grow
        // and internodal spurs use SpreadLimbDeg 5. Oak leader
        // fork 28 × 0.28 ≈ 7.8° sat as a hat on the pole; autumn
        // hang pad stayed summer 0.28; ConeTip floated at 0.42 of
        // the full spur past the short core. Now: one 5° cut;
        // ClusterHangMul spring 0.22 / autumn 0.40; ConeTip on the
        // core distal. Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that peak: Visual 19:46 leftover on the tree — Needle
        // still a full-length post; Fan2 a pancake; ForkFillTip on the
        // belt; Collar a mushroom; FillWaist a sofa; Flake2 a brick;
        // PlateTip/ScaleTip hyphens of a 3 cm flake; Hang a hyphen C;
        // internodal Cone 0.52 a telegraph; willow Tip a curtain
        // paddle. Now: Needle short shaft + long distal; Fan2 thinner
        // + tip off-pad; ForkFillTip off the Y; Collar flanks; FillWaist
        // chip off TuftL; Flake2 shorter; PlateTip/ScaleTip a length
        // fraction; Hang a taller C; Cone 0.40 core; willow Tip a peak.
        // Palmate sinus / orchard bite stay empty.
        // leafSize / fork 28 / taper 0.58 stay.
        // After that Fan2 pass: FanR 0.28 at +0.12 still crossed x=0;
        // pine BodyR kissed the fir mast; willow Tip 0.36 still a
        // mid-curtain; autumn leafTilt +10 / hang 0.40 still summer
        // at ortho 5. Now: FanR outer table; BodyR off-spine; willow
        // Tip a sickle mid; autumn leafTilt +16 / ClusterHangMul 0.46;
        // garden spent 0.62; berry hang +26 / russet leaf. Collider /
        // hinge / fork 28 / taper 0.58 stay.
        // After that FanR outer: cedar FanBody right stayed at 0 while
        // FanR left sat at +0.18 (sky table slit); FanFillR sat in the
        // gap; pine FillR the same on BodyR; willow TipR 0.40 rebuilt
        // the mid-curtain; bark dark 0.42 still soft at 1x. Now:
        // FanBody kisses FanR; Fill chips on outer pads; TipR sickle;
        // bark dark/lite punch. Collider / fork 28 / taper 0.58 stay.
        // After that FanBody kiss: pine Body 0.28 at −0.12 right +0.02
        // / BodyR left +0.13 still a 0.11 fir slit; autumn hang 0.46 /
        // leafTilt +16 / sail 1.12 still summer at ortho 5. Now: Body
        // kisses BodyR; Fill chips on Body pads; ClusterHangMul 0.50;
        // leafTilt +18; autumn sail 1.18; spent oak/willow russet;
        // garden spent 0.68 / hang +24; berry hang +30. Collider /
        // fork 28 / taper 0.58 stay.
        // After that Body kiss: willow Hang5/6 still used the outer
        // tall-C pad 0.28 while the kiss math assumed 0.72 — ~0.11
        // sky slit at x=0 (pine Body analog); bark dark 0.34 soft
        // after autumn russet. Now: mid Hang5/6 pad 0.72 kisses at
        // x=0; outer Hang0–4 keep tall C; bark dark/lite punch.
        // Collider / fork 28 / taper 0.58 stay.
        // After that Hang5/6 kiss: Hang0 at −0.50 / Hang1 at −0.30
        // left a 0.04 left slit; Hang4 at +0.18 / Hang2 at +0.36 left
        // a 0.08 right slit; Hang3 at +0.50 left a 0.11 outer gap.
        // Now: Hang0/1/2/3 stations walked in so tall-C pads kiss;
        // garden spent hang +28 / hip 0.74; berry hang +34 / russet.
        // Collider / fork 28 / taper 0.58 stay.
        // After that Hang0–3 kiss: Cap/CapR body kissed but CapTip at
        // −0.18 / CapRTip at +0.18 left a peak slit above CapR top;
        // FanDomeTip / ApexTip the same; willow Tip 0.26 mid-lance;
        // bark 0.26 soft after autumn russet. Now: CapTip/FanDomeTip/
        // ApexTip kiss at x=0; CapR/FanDomeR/ApexR match near top;
        // willow Tip distal sickle; bark dark 0.22 / lite 1.65.
        // Collider / fork 28 / taper 0.58 stay.
        // After that CapTip peak kiss: CapFill at −0.16 / CapFillTip at
        // −0.24 left a 0.08 shoulder slit under CapTip left −0.12;
        // bark 0.22 soft after autumn russet; garden +28 / hip 0.74
        // still summer-tight. Now: CapFill chips ride Cap under CapTip
        // and kiss CapTip/CapRTip; bark dark 0.18 / lite 1.72; garden
        // hang +30 / hip 0.78 / spike 0.72; berry hang +36 / russet.
        // Collider / fork 28 / taper 0.58 stay.
        // After that CapFill under CapTip: FanDome / FanDomeR kissed at
        // mid-Y but no shoulder chip under FanDomeTip left the cedar
        // peak a one-tent (pine CapFill before CapFillTip); bark 0.18
        // soft after autumn russet. Now: FanDomeFill / FanDomeFillR
        // ride FanDome under FanDomeTip and kiss FanDomeTip /
        // FanDomeRTip; FanFill stays the FanBody table chip; bark dark
        // 0.16 / lite 1.78; oak autumn russet deepened. Collider /
        // fork 28 / taper 0.58 / hang pad 0.50 / sail 1.18 stay.
        // After that FanDomeFill: FanFill at −0.16 / Y −0.10 sat ON
        // the FanBody mid table (tip is down — CapFill shoulder slit
        // analog); bark 0.16 soft after autumn russet; garden +30 /
        // hip 0.78 still summer-tight. Now: FanFill / FanFillR ride
        // FanBody / FanR under FanBodyTip / FanRTip; bark dark 0.14 /
        // lite 1.85; garden hang +32 / hip 0.82 / spike 0.76; berry
        // hang +38 / russet; oak/willow autumn deepened. Collider /
        // fork 28 / taper 0.58 / hang pad 0.50 / sail 1.18 stay.
        // After that FanFill: poplar Apex / pine tip-hat Apex kissed
        // at mid-Y but no shoulder chip under ApexTip left the flame /
        // fir tip a one-tent (pine CapFill before CapFillTip); bark
        // 0.14 soft after autumn russet. Now: ApexFill / ApexFillR
        // ride Apex / ApexR under ApexTip / ApexRTip; bark dark 0.12 /
        // lite 1.92; oak/willow/poplar autumn deepened. Collider /
        // fork 28 / taper 0.58 / hang pad 0.50 / sail 1.18 stay.
        // After that ApexFill: oak / bush / orchard Dome / DomeR still
        // had no shoulder chip under DomeTip (pine CapFill before
        // CapFillTip); bark 0.12 soft after autumn russet; garden +32 /
        // hip 0.82 still summer-tight. Now: DomeFill / DomeFillR ride
        // Dome / DomeR under DomeTip / DomeRTip; SpentCapFill on the
        // autumn hip; bark dark 0.10 / lite 2.00; garden hang +34 /
        // hip 0.86 / spike 0.80; berry hang +40 / russet; oak/willow/
        // poplar autumn deepened. Collider / fork 28 / taper 0.58 /
        // hang pad 0.50 / sail 1.18 stay.
        // After that DomeFill: winter leafless TwigDome / TwigDomeR
        // still had Tip but no shoulder chip under TwigDomeTip
        // (pine CapFill before CapFillTip); bark 0.10 soft after
        // autumn russet; garden +34 / hip 0.86 still summer-tight.
        // Now: TwigDomeFill / TwigDomeFillR ride TwigDome /
        // TwigDomeR under TwigDomeTip / TwigDomeRTip (weep / flame /
        // coppice / orchard / oak); bark dark 0.08 / lite 2.08;
        // garden hang +36 / hip 0.90 / spike 0.84; berry hang +42 /
        // russet; oak/willow/poplar autumn deepened. Collider /
        // fork 28 / taper 0.58 / hang pad 0.50 / sail 1.18 stay.
        // After that TwigDomeFill: oak / bush / orchard Core /
        // CoreR still had Tip but no shoulder chip under CoreTip
        // (pine CapFill before CapFillTip); bark 0.08 soft after
        // autumn russet. Now: CoreFill / CoreFillR ride Core /
        // CoreR under CoreTip / CoreRTip (palmate / hedge /
        // orchard stalk); bark dark 0.06 / lite 2.16; autumn
        // leafTilt +20; oak/willow/poplar autumn deepened.
        // Garden / berry stay plant TwigDomeFill values. Collider /
        // fork 28 / taper 0.58 / hang pad 0.50 / sail 1.18 stay.
        // After that CoreFill: bush TuftL / TuftR and hazel
        // Tuft0/1/2 still had Tip but no shoulder chip under
        // TuftLTip / Tuft0Tip (pine CapFill before CapFillTip);
        // bark 0.06 soft after autumn russet; garden +36 / hip
        // 0.90 still summer-tight. Now: TuftFill / TuftFillR ride
        // TuftL / TuftR under TuftLTip / TuftRTip; TuftFill0/1/2
        // on hazel coppice; bark dark 0.04 / lite 2.24; autumn
        // leafTilt +22; garden hang +38 / hip 0.92 / spike 0.88;
        // berry hang +44 / russet; oak/willow/poplar autumn
        // deepened. Collider / fork 28 / taper 0.58 / hang pad
        // 0.50 / sail 1.18 stay.
        // Flutter only cached leaf/crown roots. Amp, phase, and rest pose
        // are bound at grow — LateUpdate does not scan names. Match
        // ApplyWind: a zero windSpeed is a still canopy. Fly-by look
        // still ticks when lookBirdSwayDegrees is on.
        // Winter deciduous woody crowns use seasonAmp 0.80; translation
        // is centimetres in ApplyLeafSway (not a 1 cm vanish at ortho 5;
        // 4.6 cm along the gust).
        // Flags bound at grow (season sail, windScale, cache length).
        // LateUpdate does not scan names or Find.
        bool look = tree.WantsLookSwayTick;
        bool leaf = tree.WantsLeafSwayTick;
        if (!look && !leaf)
            return;
        if (look)
            tree.ApplyLookSway();
        if (leaf)
            tree.ApplyLeafSway();
    }
}
