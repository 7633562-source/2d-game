using UnityEngine;

// Flat procedural dog look. Collider size stays on Dog; this only tints
// the white box HumanSegment already bakes and stacks picture plates on
// Visual. No PNG, no Resources, no localScale on a Rigidbody2D.
public static class DogArt
{
    // VIS-DOG: one sable coat, not a hedgehog-brown spare limb. Chest,
    // pelvis, both thighs, and both front uppers share the saddle.
    // Distal bones (shin + front lower) darken toward the paw — not a
    // pale sock and not a second species on the front. Neck ruff and
    // cream muzzle. Lid is a shade darker than the head so a shut eye
    // reads at 1x (Head==Lid was an invisible box). Far side is DimFar.
    // Dog.cs already tints marks at spawn — no Find.
    //
    // VIS-DOG look verdict (01.09 PNG gens): pelvis read as a limb, thigh
    // as a furred human arm. HumanSegment ignores Ellipse, so the silhouette
    // is stacked rectangles on Visual (same trick as tree bark plates).
    // 11:52: Fan2 |x|≥0.36, FingerR 0.68. Artist 11:59 / 12:14:
    // C-lenticel, willow fills the bone, ToothR 0.50 / Reach 0.76 /
    // Core 0.12; Dome on LobeL; FanM |x|≥0.36; C arms stem-thick;
    // Hang off the pole; whorl on the outer fans. Visual 12:16
    // ported that 1x. Artist 12:33: apple Core/Dome still sat on
    // the axis and filled the bite; ovate Waist 0.52 was a sausage;
    // two 0.42 kites were remex; willow bole two Strip ticks, no
    // peel. Visual 12:45: Core on LobeL, Waist 0.28, lite peel.
    // Plant 12:37 berry 0.44+Waist / tuft mix / odd petal 0.16:
    // !bole Fill 0.44 + waist, odd fan and twig mix. Artist 12:50:
    // Wrist/Apex/Cap/FanDome/TwigDome/Tip sat ON the axis after
    // Core/Dome left LobeL. Plant 12:58: hub on the left pad,
    // terminal the same pad, flower Blade 0.44 + Tip off axis,
    // strawberry LeafletMid on Waist. Artist 13:22: pine Sheath
    // rides Needle0, not a mast. Plant 13:34: hub 0.28 pad, spray
    // centre on the left pad, PetalTip on PetalL, Floret/Mid on
    // Waist — palettes still had Mid −0.06 and spike plates at x=0.
    // Plant 13:49: Calyx/Stem ride the left pad, Rachis −0.14 bloom,
    // florets 0.38…0.16, spray laterals off the hub, ForkFill inner
    // crotch 0.18, Frost/spent hip off the cane — palettes still had
    // Waist in the sinus, column Peel/Waist on the mast, one spray
    // at the hub. Plant 14:06: Knot +0.10 / Suture 0.02 / Notch −0.14
    // / CatkinTail at the hub / Bud 0.04 on the cane / 0° petal on
    // bloom Y / LeafletMid still on Waist. Visual 14:20: FillG and
    // Mid on LobeL. Plant 14:20 / artist 14:12 leftover: kite Waist
    // −0.12×0.28 filled the valley; Needle1 0.28 crossed 0; garden
    // ±0.22 a disc; FringeM in the fan sinus; Furrow3 0.04 on the
    // spine; fruit 0.72/0.68 a disc; no FingerL. Now: Waist/Sheath
    // on KiteL; Needle1 0.18 on Sheath; leaflets |x|>=0.34; FringeM
    // on Fan1; furrow on the flank; cheeks leave a sinus; FingerL
    // on LobeL0. Plant 14:40 / artist 14:49 leftover: Needle1 0.18
    // still a thick inner; Head Coat0 0.56 at -0.22 kissed Coat1;
    // Mid 0.40 at -0.34 inner -0.14; Hang rib 0.70 on the axis and
    // Arc followed tilt into the mast; Flake0 0.28 at -0.10 on the
    // spine; cedar Coat0 at -0.26 not on Fan2. Now: Needle1 0.16
    // on Sheath; cheeks 0.42 at -0.28; leaflets |x| 0.42 x 0.32;
    // Hang 0.42 steps off-axis; Flake0 on Furrow0; Base on Fan2.
    // Plant 15:18/15:28 leftover: ForkFill 0.16 at −0.18 still a
    // thin crotch belt (tree 0.88 T-bar); limb Fill 0.44 at −0.08
    // outer +0.14 on the mast; bole Fill 0.42 outer −0.01 a FanFill
    // table; Flake2 −0.08 × 0.18 kissed the spine; CoatFrost −0.10
    // a rime barcode. Now: ForkFill 0.36 lip |x|>half; Collar on
    // chest flanks; Fill/FillR lips; Flake2/Frost on the side.
    // Plant 15:41 leftover: those lips at x=0 rebuilt a 0.32-span
    // mast (fir Body analog); Sheath at −0.22 piled needles left of
    // the cup; 0° Petal0 at −0.28 cut the head. Now: Body/FanBody
    // kiss at x=0; Fill chips outer<0; Needle2 fans +X; petal
    // |x|>half. Artist 16:10 leftover: Body/FanBody 0.24 tents were
    // still equal pads (poplar KiteL right −0.05 / KiteR left +0.10
    // analog); FanDome at −0.42 one tent; Peel2 −0.06 a spine belt;
    // AddTwigSpray left pads a comma. Now: Body/FanBody are unequal
    // kites that kiss at x=0; FanDome/FanDomeR kiss at max-Y; Peel
    // near / Peel2 far flank; CheekR is the far egg. Plant 16:21
    // leftover: TwigF/Dome only on −X (winter oak one tent); Petal0
    // a left comma; x-long FillWaist +0.12 crossed 0. Artist 16:32:
    // leaflet Twig0 slit 0.17; needles 0.22 posts; Peel2 no lip;
    // Tuft2 left-only. Now: ForkR / DomeR / FR at +max |X|; PetalR
    // far |x|>half so the head stays; FillWaist lip outer < 0;
    // Twig0 kisses x=0; needles 0.16/0.14; Peel2Lip; Tuft2R.
    // Artist 16:50 leftover: poplar Base 0.40 filled the hem after
    // KiteL2; bush Dome and oak Skirt and pine Apex were one-tents;
    // leaflet stopped at Wrist; cork had no lip. Now: Twig0R/Base/
    // BaseR; DomeR/SkirtR; Apex/ApexR; LobeL2/R2; Tuft2; CorkTip.
    // Artist 17:24 leftover: poplar/ovate Waist left-only after
    // WristR; plum Notch left-only; willow Blade/Tip/Drip a +X
    // comma; bush Core left-only; packed palmate skipped WristR;
    // kite no WaistR; cedar Droop right-only; oak Furrow no
    // CorkTip lip. Now: WaistR/NotchR; BladeR/HangTipR/DripR;
    // CoreR; packed WristR/WaistR; DroopL; FurrowTip.
    // Artist 17:35 leftover: fork Collar and bole Buttress stayed
    // rectangles; oak Flake0/1/Break no CorkTip; bush Stool no lip;
    // pine Scale and bush chips no ScaleTip; orchard Knob no ChipTip.
    // Palmate mitten: no ToothL / LobeR1; packed spur no Tooth;
    // pine Slope roof slats. Now: CollarLTip/RTip; ButtressLTip/RTip;
    // Flake0Tip/Flake1Tip/BreakTip; StoolLTip/RTip; ScaleTip/PlateTip/
    // KnobTip; ToothL/LobeR1; packed ToothL/R; SlopeLTip/RTip.
    // Artist 17:56 leftover: C-lens hyphen; ConeL/R telegraph posts;
    // winter Twig comb; packed no LobeL1 past ToothL; pine Layer/Base/
    // Whorl/Cap roof slats; cedar FanL2/M coins. Plant 18:06 leftover:
    // Cap/Fringe/Hem still posts; FanL2/R2/M/M2 coins; garden petal/
    // sepal/leaflet/blade/Mid cards. Now: LensTip/CarmTip; ConeLTip/
    // RTip; TwigTip/TwigForkTip; PackedLobeL1; LayerLTip/RTip;
    // BaseLTip/RTip; WhorlLTip/RTip; CapTip/CapRTip; FanLTip/RTip/
    // L2Tip/R2Tip/MTip/M2Tip; FringeLTip/RTip; HemLTip/RTip;
    // PetalTip/PetalTipR; SepalTip; LeafletTip; BladeTip;
    // LeafletMidTip/MidRTip.
    // Artist 18:37 / plant 18:21 leftover: grow ConeCore a post after
    // ConeLTip; orchard Lobe/Skirt/Dome/Core blunt; palmate Finger/
    // Tooth stubs after LobeL0Tip; Apex a sausage after BodyTip;
    // willow Hang/Arc paddles after DripTip; ovate Ear/Petiole/Notch
    // cards; packed Wrist/Finger after LobeL1. Now: ConeCoreTip/
    // ConeEdgeLTip/RTip; BodyTip/BodyRTip; ApexTip/ApexRTip;
    // KiteLTip/KiteRTip; CoreTip; LobeL0Tip/R0Tip/L2Tip/R2Tip;
    // LobeLTip; WristTip; FingerLTip/RTip; ToothLTip/RTip;
    // PackedFinger/Tooth/Wrist tips; ReachTip; DomeTip; SkirtTip;
    // HangTip/ArcTip; NotchTip; CalyxTip; SpentTip; RunnerLeafTip;
    // DropTip/CheekLTip; EarTip/PetioleTip.
    // Artist 18:52 / plant 18:48 leftover: ovate/kite/spray Waist a
    // midrib card after KiteL2Tip; bush/hazel/pine/poplar Fill a
    // sofa/stool/mast; pine CapFill / cedar FanFill a peak table;
    // spray Lance a paddle; PetioleR/NotchR/BladeR far stubs; bloom
    // PetalL a cup coin after PetalTip; fruit Sepal/Stem/Suture
    // bark cards; lilac Raceme/Rachis and hazel Catkin posts.
    // Now: WaistTip/WaistRTip; FillTip/FillRTip; CapFillTip/
    // FanFillTip; LanceTip; PetioleRTip/NotchRTip/BladeRTip;
    // PetalLTip/PetalRTip; SepalLTip/RTip; StemTip; SutureTip;
    // RachisTip; RacemeTip; CatkinTailTip/CatkinTip; KiteL2Tip;
    // TuftLTip; TaperTip; FloretTip; spur PetioleTip.
    // Artist 19:11 leftover: packed-tuft Petiole a brown card after
    // spur PetioleTip; packed/oak WristR far stalks; willow Blade a
    // sickle paddle; bush CheekR a hedge coin; cedar Fan2 a mid
    // pancake; bush/hazel BarkPlate a brick after PlateTip; willow
    // Hang a curtain paddle after HangTip. Now: packed PetioleTip;
    // WristRTip; PackedWristRTip; CheekRTip; Fan2Tip; FillWaistTip;
    // ForkFillTip; Flake2Tip; BarkPlateTip; Hang a C start.
    // Artist 19:32 leftover: FillWaist a sofa after FillTip; ForkFillTip
    // sat on the belt; Flake2 a brick; PlateTip a hyphen; ScaleTip short;
    // internodal Cone a telegraph; Fan2 a pancake after FanTip; Needle a
    // stick; Hang rib a curtain; packed Tip a paddle after BladeTip.
    // Now: short FillWaist + lip off TuftL; ForkFillTip off the Y;
    // Flake2 shorter; PlateTip/ScaleTip longer; CoatCone a short core;
    // Fan2Tip off-pad; Needle taper; Hang C + BladeTip off the sickle;
    // TipTip/TipRTip.
    // Artist 05:24 leftover: FanBody right@0 vs FanR left@+0.18 left
    // a sky table; FanFillR/pine FillR sat in the slit; TipR 0.40
    // rebuilt the mid-curtain; bark dark 0.42 still soft; autumn
    // oak/willow still summer-orange. Now: FanBody kisses FanR; Fill
    // chips on outer pads; TipR sickle + TipRTip; bark Groove/Ridge
    // punch; spent tip russet. Collider stays.
    // Plant 05:40 / artist 05:35 leftover: Hang0/1/2/3 stations still
    // at −0.38/−0.36/+0.24/+0.50 (0.04–0.11 slits); Hang mid pad 0.28
    // after Hang5/6 kiss-math; bark 0.34/1.48 soft beside autumn
    // russet; SpentTip still summer leaf. Now: Hang stations kiss;
    // mid Hang5/6 pad 0.72; bark dark 0.26 / lite 1.58; SpentTip
    // hip 0.74 russet. Collider stays.
    // Plant 05:51 / artist 05:46 leftover: CapTip -0.18 peak slit;
    // CapFillTip -0.26 mast; bark 0.26 soft; SpentTip 0.74 summer.
    // Now: CapTip/FanDomeTip/ApexTip kiss x=0; CapFill under CapTip;
    // bark dark 0.18 / lite 1.72; SpentTip hip 0.78; willow Tip distal.
    // Artist 06:04 / plant 05:59 leftover: ApexTip kiss without
    // ApexFill left a one-tent tip hat; FanFill at -0.16/-0.10 sat
    // ON FanBody table; bark 0.16/1.78 soft after ApexFill. Now:
    // ApexFill under ApexTip; FanFill under FanBodyTip; bark dark
    // 0.12 / lite 1.92; SpentTip hip 0.82 russet.
    // Plant 06:09 / 06:33 leftover: DomeTip kiss without DomeFill
    // left a one-tent hedge peak; TwigDome without Tip/Fill left a
    // one-tent winter peak after DomeFill; bark 0.12/1.92 soft.
    // Now: DomeFill under DomeTip; TwigDomeTip + TwigDomeFill under
    // it; bark dark 0.08 / lite 2.08; SpentTip hip 0.90 russet.
    // Artist 06:40 leftover: CoreTip kiss without CoreFill left a
    // one-tent sofa stalk after TwigDomeFill; bark 0.08/2.08 soft.
    // Now: CoreFill/CoreFillR under CoreTip/CoreRTip; bark dark 0.06
    // / lite 2.16; SpentTip hip 0.92 russet.
    // CoatLeave / CoatRocker / CoatCatch /
    // CoatHeel / CoatToe / CoatAir / CoatSole / CoatTrail /
    // CoatPitch / CoatYield / CoatHold / CoatRoll / CoatPush /
    // CoatChord / CoatGrip / CoatZero / CoatRise / CoatLean /
    // CoatExtend / CoatScrape / CoatKnee / CoatCadence stay. Collider stays.
    static readonly Color Trunk = new Color(0.48f, 0.32f, 0.14f, 0.95f);
    static readonly Color Chest = Trunk;
    static readonly Color Pelvis = Trunk;
    static readonly Color Thigh = Trunk;
    static readonly Color Neck = new Color(0.58f, 0.42f, 0.24f, 0.95f);
    static readonly Color Head = new Color(0.68f, 0.50f, 0.30f, 0.95f);
    static readonly Color Jaw = new Color(0.86f, 0.76f, 0.58f, 0.95f);
    static readonly Color Tail = new Color(0.28f, 0.16f, 0.08f, 0.95f);
    static readonly Color Shin = new Color(0.32f, 0.20f, 0.10f, 0.95f);
    static readonly Color Upper = Trunk;
    static readonly Color Lower = Shin;
    static readonly Color Paw = new Color(0.12f, 0.08f, 0.06f, 0.95f);

    static readonly Color Ear = new Color(0.48f, 0.32f, 0.16f, 0.90f);
    static readonly Color EarInner = new Color(0.62f, 0.38f, 0.28f, 0.90f);
    static readonly Color Eye = new Color(0.08f, 0.07f, 0.09f, 0.94f);
    static readonly Color Lid = new Color(0.42f, 0.28f, 0.14f, 0.92f);
    static readonly Color Brow = new Color(0.14f, 0.09f, 0.07f, 0.90f);
    static readonly Color Nose = new Color(0.10f, 0.07f, 0.08f, 0.94f);
    static readonly Color Whisker = new Color(0.16f, 0.11f, 0.10f, 0.88f);
    static readonly Color Mouth = new Color(0.22f, 0.08f, 0.10f, 0.92f);
    static readonly Color Tongue = new Color(0.86f, 0.32f, 0.38f, 0.92f);
    static readonly Color Tooth = new Color(0.96f, 0.93f, 0.86f, 0.94f);
    static readonly Color Hackle = new Color(0.16f, 0.11f, 0.09f, 0.90f);
    static readonly Color BarkPuff = new Color(1.00f, 0.93f, 0.78f, 0.85f);
    static readonly Color PantPuff = new Color(0.88f, 0.94f, 1.00f, 0.42f);
    static readonly Color Chew = new Color(0.62f, 0.14f, 0.10f, 0.92f);

    static readonly Color Belly = new Color(0.34f, 0.20f, 0.08f, 0.90f);
    static readonly Color Rump = new Color(0.40f, 0.26f, 0.12f, 0.90f);
    static readonly Color Withers = new Color(0.38f, 0.24f, 0.10f, 0.90f);
    static readonly Color Pad = new Color(0.08f, 0.05f, 0.04f, 0.92f);
    static readonly Color Toe = new Color(0.10f, 0.06f, 0.04f, 0.90f);
    static readonly Color Blaze = new Color(0.90f, 0.82f, 0.64f, 0.90f);
    static readonly Color Stop = new Color(0.38f, 0.24f, 0.12f, 0.90f);
    static readonly Color Hock = new Color(0.22f, 0.13f, 0.07f, 0.90f);
    static readonly Color Claw = new Color(0.18f, 0.14f, 0.12f, 0.92f);
    static readonly Color Mask = new Color(0.18f, 0.11f, 0.07f, 0.90f);
    static readonly Color Jowl = new Color(0.78f, 0.64f, 0.46f, 0.88f);
    static readonly Color Elbow = new Color(0.26f, 0.16f, 0.08f, 0.90f);
    static readonly Color Stifle = new Color(0.28f, 0.17f, 0.09f, 0.90f);
    static readonly Color Brisket = new Color(0.30f, 0.18f, 0.08f, 0.88f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallPass()
    {
        if (!Application.isPlaying)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        GameObject go = new GameObject("DogArt.MarkPass");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<MarkPass>();
    }

    // One LateUpdate after Start (dogs exist), then destroy — not a
    // 200 Hz Find, not FindObjectOfType.
    sealed class MarkPass : MonoBehaviour
    {
        void LateUpdate()
        {
            Dog[] dogs = Object.FindObjectsByType<Dog>(FindObjectsSortMode.None);
            for (int i = 0; i < dogs.Length; i++)
            {
                Dog dog = dogs[i];
                if (dog == null)
                    continue;
                PaintTree(dog.transform);
                Dress(dog.transform);
            }
            Destroy(gameObject);
        }
    }

    public static Color Tint(string objectName)
    {
        Color c = Base(objectName);
        return IsFar(objectName) ? ArtLibrary.DimFar(c) : c;
    }

    public static void PaintTree(Transform root)
    {
        if (root == null)
            return;
        SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
            Paint(srs[i]);
    }

    public static void Paint(SpriteRenderer sr)
    {
        string name = ColorName(sr);
        if (name == null)
            return;
        sr.color = Tint(name);
    }

    // Picture plates on Visual. Physics stays on the segment box.
    public static void Dress(Transform root)
    {
        if (root == null)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            if (sr == null)
                continue;
            string goName = sr.gameObject.name;
            if (goName == "EarNear" || goName == "EarFar")
            {
                Transform ear = sr.transform;
                if (HasCoat(ear))
                    continue;
                DressEar(ear, sr.sortingOrder, goName, sr.size);
                HideSlab(sr);
                continue;
            }
            if (goName == "Nose")
            {
                Transform nose = sr.transform;
                if (HasCoat(nose))
                    continue;
                DressNose(nose, sr.sortingOrder, sr.size);
                HideSlab(sr);
                continue;
            }
            if (goName != "Visual")
                continue;
            Transform visual = sr.transform;
            if (HasCoat(visual))
                continue;
            Transform bone = visual.parent;
            if (bone == null)
                continue;
            DressBone(visual, sr.sortingOrder, bone.name, BoxOf(visual));
            HideSlab(sr);
        }
    }

    static string ColorName(SpriteRenderer sr)
    {
        if (sr == null)
            return null;
        string name = sr.gameObject.name;
        if (name == "JointMarker")
            return null;
        if (name.StartsWith("Coat", System.StringComparison.Ordinal))
            return null;
        if (name == "Visual")
        {
            Transform p = sr.transform.parent;
            return p != null ? p.name : name;
        }
        return name;
    }

    static bool IsFar(string name)
    {
        if (ArtLibrary.IsFarSide(name))
            return true;
        if (string.IsNullOrEmpty(name))
            return false;
        if (name.IndexOf("Caudal", System.StringComparison.Ordinal) >= 0)
            return true;
        return name.IndexOf("Far", System.StringComparison.Ordinal) >= 0;
    }

    static Color Base(string name)
    {
        if (string.IsNullOrEmpty(name))
            return Trunk;
        if (name.IndexOf("Paw", System.StringComparison.Ordinal) >= 0)
            return Paw;
        if (name.IndexOf("Shin", System.StringComparison.Ordinal) >= 0)
            return Shin;
        if (name.IndexOf("Thigh", System.StringComparison.Ordinal) >= 0)
            return Thigh;
        if (name.IndexOf("Lower", System.StringComparison.Ordinal) >= 0)
            return Lower;
        if (name.IndexOf("Upper", System.StringComparison.Ordinal) >= 0)
            return Upper;
        if (name.IndexOf("Jaw", System.StringComparison.Ordinal) >= 0)
            return Jaw;
        if (name.StartsWith("Tail", System.StringComparison.Ordinal))
            return Tail;
        if (name.IndexOf("EarNear", System.StringComparison.Ordinal) >= 0)
            return EarInner;
        if (name.IndexOf("Ear", System.StringComparison.Ordinal) >= 0)
            return Ear;
        if (name.IndexOf("Lid", System.StringComparison.Ordinal) >= 0)
            return Lid;
        if (name.IndexOf("Brow", System.StringComparison.Ordinal) >= 0)
            return Brow;
        if (name.IndexOf("Whisker", System.StringComparison.Ordinal) >= 0)
            return Whisker;
        if (name.IndexOf("Nose", System.StringComparison.Ordinal) >= 0)
            return Nose;
        if (name.IndexOf("Mouth", System.StringComparison.Ordinal) >= 0)
            return Mouth;
        if (name.IndexOf("Tongue", System.StringComparison.Ordinal) >= 0)
            return Tongue;
        if (name.IndexOf("Tooth", System.StringComparison.Ordinal) >= 0
            || name.IndexOf("Snarl", System.StringComparison.Ordinal) >= 0)
            return Tooth;
        if (name.IndexOf("Hackle", System.StringComparison.Ordinal) >= 0)
            return Hackle;
        if (name.IndexOf("Chew", System.StringComparison.Ordinal) >= 0)
            return Chew;
        if (name.IndexOf("BarkPuff", System.StringComparison.Ordinal) >= 0)
            return BarkPuff;
        if (name.IndexOf("PantPuff", System.StringComparison.Ordinal) >= 0)
            return PantPuff;
        if (name.IndexOf("Eye", System.StringComparison.Ordinal) >= 0)
            return Eye;
        if (name == "Head")
            return Head;
        if (name == "Neck")
            return Neck;
        if (name == "Pelvis")
            return Pelvis;
        return Chest;
    }

    static void DressBone(Transform visual, int order, string bone, Vector2 box)
    {
        bool far = IsFar(bone);
        int o = order + 1;
        // Paw silhouette is the digits. A fill here is a fourth pad.
        // Tail fill was the 06:49 potato under the cloud. Flame plates
        // own the plume (artist 07:08 hid the crown slab).
        if (bone.IndexOf("Paw", System.StringComparison.Ordinal) < 0
            && !bone.StartsWith("Tail", System.StringComparison.Ordinal))
        {
            bool bole = bone == "Chest" || bone == "Pelvis";
            AddFill(visual, box, Base(bone), order, far, bole);
        }
        if (bone == "Chest")
        {
            // 08:40 pancakes 0.32/0.28/0.24 collapsed to one shelf
            // (artist 08:48 cedar Y 0.22→0.48). Thick pads fill the
            // 14 cm barrel. Caudal −X, cranial +X, −Y dirt.
            AddCedarPads(visual, box, Shade(Belly, far), Shade(Brisket, far), o, true);
            AddPlate(visual, "Coat1", new Vector2(box.x * 0.24f, box.y * 0.48f),
                Shade(Trunk, far), o, new Vector3(-box.x * 0.52f, 0.01f, 0f));
            AddPlate(visual, "Coat2", new Vector2(box.x * 0.32f, box.y * 0.42f),
                Shade(Neck, far), o, new Vector3(box.x * 0.42f, box.y * 0.12f, 0f));
            AddPlate(visual, "Coat3", new Vector2(box.x * 0.24f, box.y * 0.16f),
                Shade(Withers, far), o, new Vector3(-box.x * 0.22f, box.y * 0.42f, 0f), -6f);
            // 09:02 ConeTilt blaze was a 0° FrostCap table (artist 09:05).
            // Two short chips follow the keel.
            AddPlate(visual, "Coat4", new Vector2(box.x * 0.22f, box.y * 0.07f),
                Shade(Blaze, far), o, new Vector3(box.x * 0.22f, box.y * 0.36f, 0f),
                ConeTilt(box, true) * 0.55f);
            // Plant 15:28: 0.14 at −0.10 inner −0.03 still a rime
            // barcode (TrunkRime1 analog). Left flank. Collider stays.
            AddPlate(visual, "CoatFrost", new Vector2(box.x * 0.18f, box.y * 0.055f),
                Shade(Blaze, far), o, new Vector3(-box.x * 0.18f, box.y * 0.22f, 0f),
                -ConeTilt(box, true) * 0.40f);
            AddPlate(visual, "Coat6", new Vector2(box.x * 0.20f, box.y * 0.14f),
                Shade(Withers, far), o, new Vector3(box.x * 0.16f, box.y * 0.38f, 0f), 8f);
            // Apple sinus (artist 08:48 orchard): unequal L/R so 1x is
            // not three coins / hem fringe.
            AddPlate(visual, "Coat8", new Vector2(box.x * 0.56f, box.y * 0.36f),
                Shade(Belly, far), o, new Vector3(-box.x * 0.32f, -box.y * 0.04f, 0f), -22f);
            AddPlate(visual, "Coat9", new Vector2(box.x * 0.24f, box.y * 0.22f),
                Shade(Brisket, far), o, new Vector3(box.x * 0.34f, -box.y * 0.08f, 0f), 18f);
            // Artist 09:28: Finger still a potato. Twig fork + oak-fork
            // flakes at chest/neck and chest/pelvis joins.
            AddTwigSpray(visual, box, Shade(Belly, far), o);
            AddPlate(visual, "CoatFork0", new Vector2(box.x * 0.18f, box.y * 0.07f),
                Shade(Withers, far), o, new Vector3(-box.x * 0.48f, box.y * 0.02f, 0f), 12f);
            AddPlate(visual, "CoatFork1", new Vector2(box.x * 0.14f, box.y * 0.055f),
                Shade(Neck, far), o, new Vector3(box.x * 0.42f, box.y * 0.08f, 0f), -10f);
            // Plant 15:28: 0.16 at −0.18 still a thin crotch belt
            // (tree 0.88 T-bar). Lip 0.36, |x|>half so the join is a
            // Y. Collar on the flanks, not a 0° mushroom. Collider stays.
            AddPlate(visual, "CoatForkFill", new Vector2(box.x * 0.36f, box.y * 0.12f),
                Shade(Trunk, far), o, new Vector3(-box.x * 0.20f, -box.y * 0.46f, 0f), 8f);
            // Artist 19:32 leftover: crotch Fill a sofa after FillTip
            // (Cap before CapTip). Hang the chip further off the Y.
            // Collider stays.
            AddPlate(visual, "CoatForkFillTip", new Vector2(box.x * 0.12f, box.y * 0.05f),
                Shade(Trunk, far), o, new Vector3(-box.x * 0.40f, -box.y * 0.58f, 0f), 12f);
            AddPlate(visual, "CoatCollarL", new Vector2(box.x * 0.36f, box.y * 0.16f),
                Shade(Withers, far), o, new Vector3(-box.x * 0.32f, -box.y * 0.42f, 0f), -8f);
            AddPlate(visual, "CoatCollarR", new Vector2(box.x * 0.32f, box.y * 0.14f),
                Shade(Neck, far), o, new Vector3(box.x * 0.30f, -box.y * 0.40f, 0f), 7f);
            // Artist 17:35 leftover: rectangle collars without a distal
            // chip stayed a 0° mushroom after ForkFill (bole Furrow
            // without CorkTip). Lip hangs off the flank. Collider stays.
            AddPlate(visual, "CoatCollarLTip", new Vector2(box.x * 0.20f, box.y * 0.08f),
                Shade(Withers, far), o, new Vector3(-box.x * 0.42f, -box.y * 0.46f, 0f), -14f);
            AddPlate(visual, "CoatCollarRTip", new Vector2(box.x * 0.18f, box.y * 0.07f),
                Shade(Neck, far), o, new Vector3(box.x * 0.40f, -box.y * 0.44f, 0f), 12f);
            // Artist 17:56 leftover: chest wedges without a distal chip
            // stayed telegraph sticks (Cone before ConeTip). Hang off
            // the outer end. Collider stays.
            AddPlate(visual, "CoatConeLTip", new Vector2(box.x * 0.14f, box.y * 0.08f),
                Shade(Trunk, far), o, new Vector3(-box.x * 0.58f, box.y * 0.18f, 0f), -12f);
            AddPlate(visual, "CoatConeRTip", new Vector2(box.x * 0.12f, box.y * 0.07f),
                Shade(Neck, far), o, new Vector3(box.x * 0.50f, box.y * 0.20f, 0f), 10f);
            // Artist 18:37 leftover: grow ConeCore still a telegraph
            // post after spur ConeLTip (Furrow before CorkTip). Distal
            // chip plus two edge wedges. Collider stays.
            // Artist 19:32 leftover: internodal Cone a telegraph after
            // ConeTip (grow ConeCore analog). Short core; edges own
            // the taper. Collider stays.
            AddPlate(visual, "CoatCone", new Vector2(box.x * 0.12f, box.y * 0.26f),
                Shade(Trunk, far), o, new Vector3(-box.x * 0.04f, box.y * 0.10f, 0f), -4f);
            AddPlate(visual, "CoatConeCoreTip", new Vector2(box.x * 0.08f, box.y * 0.06f),
                Shade(Trunk, far), o, new Vector3(-box.x * 0.04f, box.y * 0.48f, 0f), -6f);
            AddPlate(visual, "CoatConeEdgeLTip", new Vector2(box.x * 0.08f, box.y * 0.10f),
                Shade(Trunk, far), o, new Vector3(-box.x * 0.46f, box.y * 0.28f, 0f), -18f);
            AddPlate(visual, "CoatConeEdgeRTip", new Vector2(box.x * 0.07f, box.y * 0.09f),
                Shade(Neck, far), o, new Vector3(box.x * 0.40f, box.y * 0.28f, 0f), 16f);
            return;
        }
        if (bone == "Pelvis")
        {
            // Artist 09:28: 0.92 kite still a plus; Base 0.82 a card.
            // Egg 0.58/0.52/0.44. Twig fork, not Finger potato.
            AddPlate(visual, "Coat0", new Vector2(box.x * 0.58f, box.y * 0.52f),
                Shade(Rump, far), o, new Vector3(-box.x * 0.12f, box.y * 0.04f, 0f), -8f);
            AddPlate(visual, "Coat5", new Vector2(box.x * 0.52f, box.y * 0.44f),
                FarSilver(Rump, far), o, new Vector3(box.x * 0.14f, -0.02f * box.y, 0f), 7f);
            AddPlate(visual, "Coat1", new Vector2(box.x * 0.44f, box.y * 0.36f),
                Shade(Trunk, far), o, new Vector3(-box.x * 0.22f, box.y * 0.22f, 0f), 4f);
            AddPlate(visual, "Coat2", new Vector2(box.x * 0.50f, box.y * 0.24f),
                Shade(Belly, far), o, new Vector3(-box.x * 0.22f, -box.y * 0.08f, 0f), 4f);
            AddPlate(visual, "Coat6", new Vector2(box.x * 0.40f, box.y * 0.16f),
                Shade(Belly, far), o, new Vector3(-box.x * 0.28f, -box.y * 0.34f, 0f), -6f);
            AddPlate(visual, "Coat4", new Vector2(box.x * 0.34f, box.y * 0.40f),
                Shade(Tail, far), o, new Vector3(-box.x * 0.48f, -box.y * 0.18f, 0f), -12f);
            AddTwigSpray(visual, box, Shade(Rump, far), o);
            return;
        }
        if (bone == "Neck")
        {
            // 08:40 internodal rings still a collar tile. Bloom petals
            // + calyx (artist 08:48 apple square→petals) is a ruff.
            AddPlate(visual, "Coat0", new Vector2(box.x * 0.58f, box.y * 0.52f),
                Shade(Neck, far), o, new Vector3(-box.x * 0.12f, -box.y * 0.06f, 0f), -8f);
            AddPlate(visual, "Coat1", new Vector2(box.x * 0.52f, box.y * 0.44f),
                Shade(Withers, far), o, new Vector3(box.x * 0.14f, -box.y * 0.04f, 0f), 7f);
            AddPlate(visual, "Coat2", new Vector2(box.x * 0.44f, box.y * 0.36f),
                Shade(Head, far), o, new Vector3(-box.x * 0.22f, box.y * 0.18f, 0f), 4f);
            AddPlate(visual, "Coat3", new Vector2(box.x * 0.28f, box.y * 0.24f),
                Shade(Trunk, far), o, new Vector3(-box.x * 0.18f, -box.y * 0.22f, 0f), 6f);
            AddTwigSpray(visual, box, Shade(Head, far), o);
            return;
        }
        if (bone == "Head")
        {
            // Apple unequal cheeks (artist 08:48 fruit), not two coins.
            // Plant 14:40: 0.56 at -0.22 still reached +0.06 and kissed
            // Coat1 (Needle1 analog). Narrow left cheek; sinus stays empty.
            // Collider stays.
            AddPlate(visual, "Coat0", new Vector2(box.x * 0.42f, box.y * 0.68f),
                Shade(Jaw, far), o, new Vector3(-box.x * 0.28f, -box.y * 0.06f, 0f), -10f);
            AddPlate(visual, "Coat1", new Vector2(box.x * 0.42f, box.y * 0.52f),
                Shade(Head, far), o, new Vector3(box.x * 0.28f, 0.02f * box.y, 0f), 12f);
            AddPlate(visual, "Coat2", new Vector2(box.x * 0.28f, box.y * 0.36f),
                Shade(Jaw, far), o, new Vector3(box.x * 0.42f, 0.02f, 0f));
            AddPlate(visual, "Coat3", new Vector2(box.x * 0.36f, box.y * 0.22f),
                Shade(Stop, far), o, new Vector3(-box.x * 0.22f, box.y * 0.02f, 0f));
            AddPlate(visual, "Coat4", new Vector2(box.x * 0.42f, box.y * 0.28f),
                Shade(Mask, far), o, new Vector3(-box.x * 0.08f, box.y * 0.18f, 0f));
            AddPlate(visual, "Coat5", new Vector2(box.x * 0.34f, box.y * 0.26f),
                Shade(Jowl, far), o, new Vector3(box.x * 0.18f, -box.y * 0.34f, 0f));
            // Plant 14:40: sepals ride the cheek pads, not the bite.
            AddPlate(visual, "Coat6", new Vector2(box.x * 0.22f, box.y * 0.18f),
                Shade(Blaze, far), o, new Vector3(-box.x * 0.28f, -box.y * 0.36f, 0f), -16f);
            AddPlate(visual, "Coat7", new Vector2(box.x * 0.20f, box.y * 0.16f),
                Shade(Blaze, far), o, new Vector3(box.x * 0.28f, -box.y * 0.34f, 0f), 14f);
            AddTwigSpray(visual, box, Shade(Head, far), o);
            return;
        }
        if (bone == "Jaw")
        {
            AddPlate(visual, "Coat0", new Vector2(box.x * 0.82f, box.y * 0.50f),
                Shade(Jaw, far), o, new Vector3(box.x * 0.12f, -box.y * 0.14f, 0f));
            AddPlate(visual, "Coat1", new Vector2(box.x * 0.48f, box.y * 0.42f),
                Shade(Jowl, far), o, new Vector3(box.x * 0.22f, -box.y * 0.28f, 0f));
            return;
        }
        if (bone.IndexOf("Upper", System.StringComparison.Ordinal) >= 0)
        {
            // 08:40 pancakes 0.28 still a shelf. Cedar pads along the
            // column (Y*0.48). Distal stays a short dark cuff.
            AddCedarPads(visual, box, Shade(Rump, far), Shade(Withers, far), o, false);
            AddPlate(visual, "Coat2", new Vector2(box.x * 0.34f, box.y * 0.18f),
                Shade(Shin, far), o, new Vector3(-box.x * 0.18f, -box.y * 0.32f, 0f));
            return;
        }
        if (bone.IndexOf("Thigh", System.StringComparison.Ordinal) >= 0)
        {
            AddCedarPads(visual, box, Shade(Rump, far), Shade(Withers, far), o, false);
            AddPlate(visual, "Coat2", new Vector2(box.x * 0.34f, box.y * 0.18f),
                Shade(Shin, far), o, new Vector3(-box.x * 0.18f, -box.y * 0.32f, 0f));
            AddPlate(visual, "Coat3", new Vector2(box.x * 0.55f, box.y * 0.16f),
                Shade(Stifle, far), o, new Vector3(-box.x * 0.28f, -box.y * 0.32f, 0f), 14f);
            return;
        }
        if (bone.IndexOf("Shin", System.StringComparison.Ordinal) >= 0)
        {
            // 08:40 internodal rings still pine shelves. Pointed buds
            // (artist 08:48 0.018 ticks→overlapping 48°). Hock caudal −X.
            AddPointedBud(visual, box, Shade(Shin, far), Shade(Paw, far), o, -1f);
            // Plant 15:28: 1.35 at −0.42 outer +0.26 still crossed
            // the shin (spray Lance analog). |x|>half. Collider stays.
            AddPlate(visual, "Coat2", new Vector2(box.x * 0.52f, box.y * 0.22f),
                Shade(Hock, far), o, new Vector3(-box.x * 0.42f, -box.y * 0.22f, 0f), 18f);
            return;
        }
        if (bone.IndexOf("Lower", System.StringComparison.Ordinal) >= 0)
        {
            AddPointedBud(visual, box, Shade(Shin, far), Shade(Paw, far), o, 1f);
            // Plant 15:28: 1.40 at −0.38 outer +0.32 still crossed
            // the forearm (spray Lance analog). |x|>half. Collider stays.
            AddPlate(visual, "Coat2", new Vector2(box.x * 0.52f, box.y * 0.24f),
                Shade(Elbow, far), o, new Vector3(-box.x * 0.38f, box.y * 0.28f, 0f), -16f);
            AddPlate(visual, "Coat3", new Vector2(box.x * 0.42f, box.y * 0.18f),
                Shade(Pad, far), o, new Vector3(-box.x * 0.55f, -box.y * 0.08f, 0f), -22f);
            return;
        }
        if (bone.IndexOf("Paw", System.StringComparison.Ordinal) >= 0)
        {
            bool front = bone.IndexOf("Front", System.StringComparison.Ordinal) >= 0;
            AddPlate(visual, "Coat0", new Vector2(box.x * 0.48f, box.y * 0.34f),
                Shade(Pad, far), o, new Vector3(box.x * 0.08f, -box.y * 0.22f, 0f));
            // +X four weight-bearing toes. 04:45 left three (a bird).
            // Digit 3 is the long middle; 2 and 5 shorter; heel −X.
            AddPlate(visual, "Coat1", new Vector2(box.x * 0.12f, box.y * 0.34f),
                Shade(Toe, far), o, new Vector3(box.x * 0.04f, -box.y * 0.10f, 0f));
            AddPlate(visual, "Coat2", new Vector2(box.x * 0.13f, box.y * 0.42f),
                Shade(Toe, far), o, new Vector3(box.x * 0.16f, -box.y * 0.08f, 0f));
            AddPlate(visual, "Coat3", new Vector2(box.x * 0.15f, box.y * 0.50f),
                Shade(Toe, far), o, new Vector3(box.x * 0.30f, -box.y * 0.06f, 0f));
            AddPlate(visual, "Coat4", new Vector2(box.x * 0.13f, box.y * 0.40f),
                Shade(Toe, far), o, new Vector3(box.x * 0.44f, -box.y * 0.08f, 0f));
            AddPlate(visual, "Coat5", new Vector2(box.x * 0.20f, box.y * 0.26f),
                Shade(Pad, far), o, new Vector3(-box.x * 0.28f, -box.y * 0.04f, 0f));
            AddPlate(visual, "Coat6", new Vector2(box.x * 0.09f, box.y * 0.20f),
                Shade(Claw, far), o, new Vector3(box.x * 0.48f, -box.y * 0.20f, 0f), -8f);
            AddPlate(visual, "Coat7", new Vector2(box.x * 0.08f, box.y * 0.18f),
                Shade(Claw, far), o, new Vector3(box.x * 0.34f, -box.y * 0.22f, 0f), -6f);
            // Trot plant: weight on the long middle digit, not a
            // sliding brick (sit-trot skate). Push pad at +X dirt.
            AddPlate(visual, "CoatPush", new Vector2(box.x * 0.11f, box.y * 0.16f),
                Shade(Claw, far), o, new Vector3(box.x * 0.38f, -box.y * 0.28f, 0f), -12f);
            // Plant stop: a short upright brace at the +X toes so
            // the paw reads as a digitigrade lock, not a ski. Same
            // dirt as CoatPush. No PNG.
            AddPlate(visual, "CoatBrace", new Vector2(box.x * 0.07f, box.y * 0.30f),
                Shade(Claw, far), o, new Vector3(box.x * 0.34f, -box.y * 0.16f, 0f), 16f);
            // Diagonal plant spike: weight on the +X toes into dirt
            // so the paw is a digitigrade lock, not the sit-trot ski.
            // Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatSpike", new Vector2(box.x * 0.05f, box.y * 0.18f),
                Shade(Claw, far), o, new Vector3(box.x * 0.42f, -box.y * 0.32f, 0f), -22f);
            // Trot lock: a second +X tick into dirt so the plant
            // reads as a digitigrade stop, not the sit-trot ski.
            // Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatLock", new Vector2(box.x * 0.04f, box.y * 0.22f),
                Shade(Claw, far), o, new Vector3(box.x * 0.46f, -box.y * 0.36f, 0f), -28f);
            // In-place lift: a short +X tick off the dirt so the
            // swing paw reads as leaving, not the sit-trot ski.
            // Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatLift", new Vector2(box.x * 0.05f, box.y * 0.14f),
                Shade(Claw, far), o, new Vector3(box.x * 0.50f, -box.y * 0.42f, 0f), -34f);
            // Quiet plant: a flat +X pad on the dirt so the stance
            // CoM reads as a measured sit, not a collapsed heap.
            // Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatStand", new Vector2(box.x * 0.18f, box.y * 0.08f),
                Shade(Pad, far), o, new Vector3(box.x * 0.22f, -box.y * 0.34f, 0f));
            // Plant-ahead: a cranial +X tick off the dirt so the
            // swing paw reads as a stride, not a 34 ms hop on the
            // sit-trot ski. Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatStride", new Vector2(box.x * 0.06f, box.y * 0.16f),
                Shade(Toe, far), o, new Vector3(box.x * 0.56f, -box.y * 0.18f, 0f), -20f);
            // Air stride: a cranial +X tick off the dirt so the
            // swing paw reads as reach, not a sit-X hop. Picture
            // only. Collider stays. No PNG.
            AddPlate(visual, "CoatReach", new Vector2(box.x * 0.05f, box.y * 0.12f),
                Shade(Toe, far), o, new Vector3(box.x * 0.64f, -box.y * 0.06f, 0f), -14f);
            // Open-limb plant: a further +X tick above the dirt so
            // the catch reads as a stride plant, not the folded
            // sit-X hop that never swapped. Picture only. Collider
            // stays. No PNG.
            AddPlate(visual, "CoatPlant", new Vector2(box.x * 0.05f, box.y * 0.10f),
                Shade(Toe, far), o, new Vector3(box.x * 0.80f, box.y * 0.08f, 0f), -8f);
            // Swap catch: a raised +X tick so the other diagonal
            // reads as a plant after the hip-relative leave, not
            // the sit-trot ski that never left. Picture only.
            // Collider stays. No PNG.
            AddPlate(visual, "CoatSwap", new Vector2(box.x * 0.05f, box.y * 0.12f),
                Shade(Toe, far), o, new Vector3(box.x * 0.72f, box.y * 0.22f, 0f), -16f);
            // Hip-relative leave: a raised +X tick so the swing
            // reads as coming off the twin, not a plus-paw ski
            // that stayed in dirt. Picture only. Collider stays.
            // No PNG.
            AddPlate(visual, "CoatLeave", new Vector2(box.x * 0.05f, box.y * 0.10f),
                Shade(Toe, far), o, new Vector3(box.x * 0.58f, box.y * 0.34f, 0f), -12f);
            // Dirt rocker: a short +Y tick so the heel reads as
            // rising while the toe still kisses, not a 12 cm IK
            // squat on a planted pad. Picture only. Collider stays.
            // No PNG.
            AddPlate(visual, "CoatRocker", new Vector2(box.x * 0.05f, box.y * 0.10f),
                Shade(Claw, far), o, new Vector3(box.x * 0.40f, box.y * 0.48f, 0f), -6f);
            // First swap catch: a cranial +X tick above the dirt
            // so the free foot reads as planting ahead, not the
            // sit-trot ski. Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatCatch", new Vector2(box.x * 0.05f, box.y * 0.12f),
                Shade(Toe, far), o, new Vector3(box.x * 0.88f, box.y * 0.18f, 0f), -10f);
            // Heel-rise: a caudal −X tick so the pad reads as
            // rocking onto the toe, not a 6 cm knee on a flat
            // heel+toe pin. Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatHeel", new Vector2(box.x * 0.05f, box.y * 0.10f),
                Shade(Claw, far), o, new Vector3(-box.x * 0.36f, box.y * 0.42f, 0f), 8f);
            // Toe-only kiss: a cranial tick on the dirt so the
            // rocker knee reads as leaving the heel, not a sit-trot
            // ski. Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatToe", new Vector2(box.x * 0.05f, box.y * 0.10f),
                Shade(Toe, far), o, new Vector3(box.x * 0.48f, -box.y * 0.28f, 0f), -4f);
            // True air: a raised tick so the pad reads as off the
            // 2 cm slab band, not a 12 cm IK chair on leftover sit.
            // Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatAir", new Vector2(box.x * 0.05f, box.y * 0.12f),
                Shade(Toe, far), o, new Vector3(box.x * 0.62f, box.y * 0.52f, 0f), -8f);
            // Corners vs slab: a caudal tick so the pad reads as
            // a heel-above-toe rocker, not a GetContacts mid-band
            // pin that froze the sit-trot. Picture only. Collider
            // stays. No PNG.
            AddPlate(visual, "CoatSole", new Vector2(box.x * 0.05f, box.y * 0.10f),
                Shade(Claw, far), o, new Vector3(-box.x * 0.22f, box.y * 0.56f, 0f), 6f);
            // Trail-first leave: a raised caudal tick so the hind
            // reads as peeling after heel-rise, not an Arm-clock
            // 6 cm squat on a flat pin. Picture only. Collider
            // stays. No PNG.
            AddPlate(visual, "CoatTrail", new Vector2(box.x * 0.05f, box.y * 0.12f),
                Shade(Claw, far), o, new Vector3(-box.x * 0.48f, box.y * 0.62f, 0f), 10f);
            // Heel-pin pitch: a caudal-low tick so the pad reads as
            // plantarflex under a held hip, not Dual-soft hip+knee
            // (limp squat) and not a 6 cm knee on a still-flat pin.
            // Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatPitch", new Vector2(box.x * 0.05f, box.y * 0.10f),
                Shade(Claw, far), o, new Vector3(-box.x * 0.40f, -box.y * 0.22f, 0f), 14f);
            // Dual-soft trail knee leftover: a short caudal tick.
            // dog_w60 0.35× P at leftover sit dumped the pelvis.
            // Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatYield", new Vector2(box.x * 0.05f, box.y * 0.10f),
                Shade(Claw, far), o, new Vector3(-box.x * 0.16f, -box.y * 0.40f, 0f), 18f);
            // Hip hold + extra-flex target: a cranial-high tick so
            // the column reads as holding length while plus 0.18
            // pitches the pad, not Dual-soft hip+knee (limp squat).
            // Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatHold", new Vector2(box.x * 0.05f, box.y * 0.10f),
                Shade(Claw, far), o, new Vector3(box.x * 0.18f, box.y * 0.48f, 0f), -6f);
            // Cranial hip roll: a cranial tick so the pad reads as
            // rotating over the toe under a minus hip, not extra
            // plus-knee on a held column (dog_w61 squat). Picture
            // only. Collider stays. No PNG.
            AddPlate(visual, "CoatRoll", new Vector2(box.x * 0.05f, box.y * 0.10f),
                Shade(Toe, far), o, new Vector3(box.x * 0.32f, box.y * 0.22f, 0f), -14f);
            // CoatChord: sit-relative +18° dirt rocker from Arm so
            // the trail matches stance open 24°→9°, not inverted-V
            // 6 cm (~63° chair, dog_w58) and not a 3 mm wait on a
            // welded pad (dog_w63). Picture only. Collider stays.
            // No PNG.
            AddPlate(visual, "CoatChord", new Vector2(box.x * 0.05f, box.y * 0.10f),
                Shade(Claw, far), o, new Vector3(-box.x * 0.28f, box.y * 0.18f, 0f), 12f);
            // CoatGrip: trail hip HOLDS leftover sit and +18° is ON
            // from Arm so the pad is not a caudal skate (dog_w64
            // hip plus 10°) and not leftover sit on the Arm tick
            // (U=0 while stance opened 6°). Picture only. Collider
            // stays. No PNG.
            AddPlate(visual, "CoatGrip", new Vector2(box.x * 0.05f, box.y * 0.10f),
                Shade(Pad, far), o, new Vector3(box.x * 0.12f, box.y * 0.08f, 0f), -8f);
            // CoatZero: after Arm the trail paw releases (Zero), so
            // +18° can leave a toe kiss. Plus 0.18 on a full-P hip
            // cannot pitch the sole (human ankle). Dual plus-paws
            // until heel-rise, then this Zero. Picture only.
            // Collider stays. No PNG.
            AddPlate(visual, "CoatZero", new Vector2(box.x * 0.05f, box.y * 0.12f),
                Shade(Claw, far), o, new Vector3(box.x * 0.08f, box.y * 0.28f, 0f), -18f);
            // CoatRise: Dual-soft hip + plus 0.18 until the heel
            // is 3 mm up, then Arm Zero + +18° on a toe kiss.
            // Arm at minDual Zeroed a still-flat pad (dog_w66
            // leftover sit-trot). Picture only. Collider stays.
            // No PNG.
            AddPlate(visual, "CoatRise", new Vector2(box.x * 0.05f, box.y * 0.12f),
                Shade(Claw, far), o, new Vector3(-box.x * 0.12f, box.y * 0.34f, 0f), 16f);
            // CoatLean: Dual nose-down from Dual t=0 so CoM can
            // leave the trail while plus 0.18 still pitches. DualReady
            // 0.12 s ceiling Zeroed a still-flat pad (dog_w67 leftover
            // sit-trot). Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatLean", new Vector2(box.x * 0.05f, box.y * 0.12f),
                Shade(Pad, far), o, new Vector3(box.x * 0.04f, box.y * 0.40f, 0f), -10f);
            // CoatExtend: Dual-soft trail hip extra-extends 4–8°
            // caudal (same ramp as stance) so plus 0.18 can pitch
            // the heel 3 mm. Leftover-sit Dual-soft fought plus
            // 0.18 (dog_w68 leftover sit-trot). After Arm the hip
            // HOLDS leftover sit — dog_w64 10° full-P caudal
            // skated. Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatExtend", new Vector2(box.x * 0.05f, box.y * 0.12f),
                Shade(Hock, far), o, new Vector3(-box.x * 0.18f, box.y * 0.22f, 0f), 14f);
            // CoatScrape: DualReady Arms on heel-free + toe scrape
            // (SwingReadyToRocker), then TrailCatchSlide walks CoP
            // without waiting for union-air. HeelRose alone never
            // fired on a pad already on the toe (dog_w72 leftover
            // sit-trot 0.286 km/h). CoatCatch is the cranial plant
            // tick above. Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatScrape", new Vector2(box.x * 0.05f, box.y * 0.12f),
                Shade(Claw, far), o, new Vector3(box.x * 0.22f, box.y * 0.30f, 0f), -22f);
            // CoatKnee: GETUP leads with knee/elbow tuck before hip
            // joins (dog_w73). Measured post-fall knee only ~35° while
            // hip already ~48° — simultaneous tuck starved the stifle.
            // Picture only. Collider stays. No PNG.
            AddPlate(visual, "CoatKnee", new Vector2(box.x * 0.05f, box.y * 0.14f),
                Shade(Hock, far), o, new Vector3(-box.x * 0.06f, box.y * 0.44f, 0f), 20f);
            // CoatCadence: dog_w74 half ≤ 0.18 s (peel/air 0.06) so
            // the diagonal clocks past sit-trot Dual park. Picture
            // only. Collider stays. No PNG.
            AddPlate(visual, "CoatCadence", new Vector2(box.x * 0.05f, box.y * 0.12f),
                Shade(Pad, far), o, new Vector3(box.x * 0.14f, box.y * 0.48f, 0f), -14f);
            if (front)
            {
                // Caudal −X, slightly +Y. Dewclaw lives on the forearm
                // wrist, not on the rear pad — four dewclaws was a cat.
                AddPlate(visual, "Coat8", new Vector2(box.x * 0.12f, box.y * 0.24f),
                    Shade(Claw, far), o, new Vector3(-box.x * 0.42f, box.y * 0.16f, 0f), 22f);
            }
            else
            {
                // +Y shin. Digitigrade metatarsal, not a fifth front pad.
                AddPlate(visual, "Coat8", new Vector2(box.x * 0.38f, box.y * 0.30f),
                    Shade(Hock, far), o, new Vector3(-box.x * 0.06f, box.y * 0.24f, 0f), 8f);
            }
            return;
        }
        if (bone.StartsWith("Tail", System.StringComparison.Ordinal))
        {
            // 08:40 0.88/0.36 still plus. Poplar flame + apex (artist
            // 08:48). Wrist +X pelvis, free −X.
            AddPlate(visual, "Coat3", new Vector2(box.x * 0.22f, box.y * 0.28f),
                Shade(Rump, far), o, new Vector3(box.x * 0.28f, -box.y * 0.04f, 0f), 4f);
            AddPlate(visual, "Coat0", new Vector2(box.x * 0.58f, box.y * 0.52f),
                Shade(Tail, far), o, new Vector3(-box.x * 0.12f, 0.02f * box.y, 0f), -8f);
            AddPlate(visual, "Coat1", new Vector2(box.x * 0.52f, box.y * 0.44f),
                FarSilver(Tail, far), o, new Vector3(box.x * 0.14f, box.y * 0.06f, 0f), 7f);
            AddPlate(visual, "Coat4", new Vector2(box.x * 0.44f, box.y * 0.36f),
                Shade(Tail, far), o, new Vector3(-box.x * 0.22f, box.y * 0.22f, 0f), 4f);
            // Artist 10:39 C-sickle still stepped +Y. Hang now downs.
            // Plant 05:40: Hang0 at −0.38 right left a 0.04 slit vs
            // Hang1 — closer left sickle (−0.42). Collider stays.
            AddWillowHang(visual, box, Shade(Paw, far), o, 2, -0.42f, -0.02f, 14f);
            AddTwigSpray(visual, box, Shade(Tail, far), o);
        }
    }

    static void DressEar(Transform ear, int order, string name, Vector2 box)
    {
        bool far = IsFar(name);
        int o = order + 1;
        if (box.x < 0.001f || box.y < 0.001f)
            box = new Vector2(0.038f, 0.055f);
        // Artist 09:28: 0.92 kite still a plus. Egg 0.58/0.52/0.44
        // plus twig fork, not Finger potato. Petiole 8° stays.
        AddPlate(ear, "Coat0", new Vector2(box.x * 0.58f, box.y * 0.52f),
            Shade(Ear, far), o, new Vector3(-box.x * 0.12f, box.y * 0.04f, 0f), -8f);
        AddPlate(ear, "Coat1", new Vector2(box.x * 0.52f, box.y * 0.44f),
            FarSilver(EarInner, far), o, new Vector3(box.x * 0.14f, 0f, 0f), 7f);
        AddPlate(ear, "Coat2", new Vector2(box.x * 0.44f, box.y * 0.36f),
            Shade(Ear, far), o, new Vector3(-box.x * 0.22f, box.y * 0.22f, 0f), 4f);
        AddPlate(ear, "CoatEarTip", new Vector2(box.x * 0.22f, box.y * 0.16f),
            Shade(Ear, far), o, new Vector3(-box.x * 0.28f, box.y * 0.32f, 0f), -10f);
        AddPlate(ear, "Coat3", new Vector2(box.x * 0.50f, box.y * 0.24f),
            Shade(EarInner, far), o, new Vector3(-box.x * 0.14f, -box.y * 0.08f, 0f), 4f);
        AddTwigSpray(ear, box, Shade(Ear, far), o);
        // Petiole 8° (artist 09:05): a short stalk, not a second stick.
        AddPlate(ear, "CoatP", new Vector2(box.x * 0.16f, box.y * 0.28f),
            Shade(Ear, far), o, new Vector3(0.02f * box.x, -box.y * 0.46f, 0f), 8f);
        // Artist 18:37 leftover: ovate Petiole a stick after WristTip.
        // Hang off Base. Collider stays.
        AddPlate(ear, "CoatPetioleTip", new Vector2(box.x * 0.10f, box.y * 0.12f),
            Shade(Ear, far), o, new Vector3(0.02f * box.x, -box.y * 0.58f, 0f), 12f);
        // Artist 18:52 leftover: PetioleR a far stub after
        // PetioleTip. Hang off the far ear pad. Collider stays.
        AddPlate(ear, "CoatPetioleRTip", new Vector2(box.x * 0.08f, box.y * 0.10f),
            Shade(Ear, far), o, new Vector3(box.x * 0.18f, -box.y * 0.52f, 0f), -8f);
    }

    static void DressNose(Transform nose, int order, Vector2 box)
    {
        int o = order + 1;
        if (box.x < 0.001f || box.y < 0.001f)
            box = new Vector2(0.022f, 0.016f);
        // Sagittal: one naris. Two equal lobes were a cube cluster
        // (artist 08:14 plum). Droplet + suture. Dog.cs still sniffs.
        AddPlate(nose, "Coat0", new Vector2(box.x * 0.70f, box.y * 0.88f),
            Nose, o, new Vector3(-box.x * 0.08f, -box.y * 0.04f, 0f), -10f);
        // Plant 18:21 leftover: plum Drop a droplet card after ScallopTip
        // (Droop before DroopTip). Hang off Drop. Collider stays.
        AddPlate(nose, "CoatDropTip", new Vector2(box.x * 0.32f, box.y * 0.22f),
            Nose, o, new Vector3(-box.x * 0.12f, -box.y * 0.36f, 0f), -6f);
        AddPlate(nose, "Coat1", new Vector2(box.x * 0.52f, box.y * 0.70f),
            Nose, o, new Vector3(box.x * 0.14f, -box.y * 0.02f, 0f), 12f);
        AddPlate(nose, "CoatCheekLTip", new Vector2(box.x * 0.28f, box.y * 0.18f),
            Nose, o, new Vector3(-box.x * 0.22f, -box.y * 0.28f, 0f), -12f);
        // Plant 14:06: fruit Suture 0.02 sat IN the sinus between
        // Drop/DropR (oak Dome analog). Groove rides Drop. Collider stays.
        AddPlate(nose, "Coat2", new Vector2(box.x * 0.16f, box.y * 0.48f),
            Pad, o, new Vector3(-box.x * 0.10f, -box.y * 0.04f, 0f), -8f);
        // Artist 18:52 leftover: fruit Suture a bark card after
        // DropTip. Hang off the groove. Collider stays.
        AddPlate(nose, "CoatSutureTip", new Vector2(box.x * 0.08f, box.y * 0.22f),
            Pad, o, new Vector3(-box.x * 0.10f, -box.y * 0.28f, 0f), -8f);
    }

    static Color Shade(Color c, bool far)
    {
        return far ? ArtLibrary.DimFar(c) : c;
    }

    // Far kite / small lobe: silver, not a second same-tint rhomb
    // (artist 08:14 palLeafFar on poplar).
    static Color FarSilver(Color c, bool far)
    {
        if (!far)
            return Shade(c, false);
        return new Color(Mathf.Min(1f, c.r * 1.38f + 0.12f),
            Mathf.Min(1f, c.g * 1.28f + 0.10f),
            Mathf.Min(1f, c.b * 1.18f + 0.10f), c.a * 0.62f);
    }

    // Plant 12:37: flower odd petal lerps stem 0.16; tuft leaflets
    // mix LeafTint so a fan is not one card.
    static Color OddLeaflet(Color petal, Color stem)
    {
        return Color.Lerp(petal, stem, 0.16f);
    }

    // Undercoat after HideSlab. 08:40 internodal rings + Slope chips
    // still a zipper. Bole is apple orchard cloud (unequal sinus);
    // limb is fascicle sheath. No mast, no barcode.
    static void AddFill(Transform visual, Vector2 box, Color color, int order, bool far, bool bole)
    {
        Color c = Shade(color, far);
        Color groove = Shade(GrooveOf(color), far);
        Color ridge = Shade(RidgeOf(color), far);
        if (box.x >= box.y)
        {
            float along = box.x;
            float across = box.y;
            if (!bole)
            {
                // Long ridge/groove was a barcode (artist 09:05 willow
                // ConeEdge strips). Short fibres along the cone.
                // Plant 12:37: strawberry mid 0.78 sausage → 0.44 + Waist
                // 0.28. Limb Fill 0.70 was that card. Collider stays.
                // Plant 13:34 column Fill at x=0 was a mast. Visual
                // 14:20 moved the tall path; this x-long Fill still
                // sat on across=0. Pad 0.28. Collider stays.
                // Plant 15:41: Fill 0.44 at −0.22 outer 0 kissed
                // FillR 0.20 at +0.10 — a 0.64-span mast (poplar
                // Fill analog after the lips closed). Chip on KiteL
                // (outer < 0); FillR on KiteR. Collider stays.
                AddPlate(visual, "CoatFill", new Vector2(along * 0.14f, across * 0.22f),
                    c, order, new Vector3(-along * 0.22f, -across * 0.28f, 0f), 2f);
                AddPlate(visual, "CoatFillR", new Vector2(along * 0.14f, across * 0.20f),
                    ridge, order, new Vector3(along * 0.28f, -across * 0.10f, 0f), 6f);
                // Artist 18:52 leftover: limb Fill a sofa after
                // KiteL2Tip (Cap before CapTip). Hang off Fill.
                // Collider stays.
                AddPlate(visual, "CoatFillTip", new Vector2(along * 0.08f, across * 0.12f),
                    c, order, new Vector3(-along * 0.30f, -across * 0.36f, 0f), -4f);
                AddPlate(visual, "CoatFillRTip", new Vector2(along * 0.08f, across * 0.10f),
                    ridge, order, new Vector3(along * 0.36f, -across * 0.16f, 0f), 10f);
                // Plant 16:21 leftover: 0.28 at +0.12 still crossed 0
                // (column FillWaist analog). Lip 0.36, outer < 0.
                // Collider stays.
                AddPlate(visual, "CoatFillWaist", new Vector2(along * 0.18f, across * 0.10f),
                    ridge, order, new Vector3(-along * 0.24f, -across * 0.08f, 0f), 6f);
                // Artist 19:32 leftover: FillWaist a sofa after FillTip
                // (Cap before CapTip). Short chip + lip off TuftL.
                // Collider stays.
                AddPlate(visual, "CoatFillWaistTip", new Vector2(along * 0.10f, across * 0.06f),
                    ridge, order, new Vector3(-along * 0.36f, -across * 0.14f, 0f), 12f);
                AddPlate(visual, "CoatFillB", new Vector2(along * 0.16f, across * 0.14f),
                    groove, order, new Vector3(-along * 0.20f, -across * 0.16f, 0f), 10f);
                AddPlate(visual, "CoatFillA", new Vector2(along * 0.14f, across * 0.11f),
                    ridge, order, new Vector3(along * 0.18f, across * 0.18f, 0f), -10f);
                // Artist 10:47: one flake on a long limb was a tick
                // (BarkFlake2). Second lite chip at a different Y.
                AddPlate(visual, "CoatFlake2", new Vector2(along * 0.12f, across * 0.034f),
                    ridge, order, new Vector3(-along * 0.16f, across * 0.12f, 0f), -8f);
                // Artist 19:32 leftover: bush/hazel plate a brick after
                // PlateTip (Furrow before CorkTip). Short flake + long
                // lip. Collider stays.
                AddPlate(visual, "CoatFlake2Tip", new Vector2(along * 0.08f, across * 0.028f),
                    ridge, order, new Vector3(-along * 0.24f, across * 0.06f, 0f), -14f);
                AddPlate(visual, "CoatBarkPlateTip", new Vector2(along * 0.14f, across * 0.055f),
                    groove, order, new Vector3(-along * 0.32f, across * 0.00f, 0f), -22f);
                // Artist 12:33: two Strip fibres were ticks on a pole.
                // One lite peel across the cone is bark, not a third
                // rung. Collider stays.
                AddPlate(visual, "CoatPeel", new Vector2(along * 0.26f, across * 0.048f),
                    ridge, order, new Vector3(along * 0.18f, -across * 0.06f, 0f), 4f);
                AddPlate(visual, "CoatPeel2", new Vector2(along * 0.20f, across * 0.040f),
                    ridge, order, new Vector3(-along * 0.18f, across * 0.16f, 0f), -12f);
                // Artist 16:32 leftover: far Peel2 without a lip stayed
                // a tick on the pole. Lite chip off Peel2. Collider stays.
                AddPlate(visual, "CoatPeel2Lip", new Vector2(along * 0.10f, across * 0.028f),
                    ridge, order, new Vector3(-along * 0.26f, across * 0.12f, 0f), -18f);
                // Artist 16:50 leftover: hazel cork without a lip stayed
                // ticks on the coppice. Distal chip off Peel2Lip.
                // Collider stays.
                AddPlate(visual, "CoatCorkTip", new Vector2(along * 0.08f, across * 0.022f),
                    groove, order, new Vector3(-along * 0.30f, across * 0.08f, 0f), -22f);
                // Artist 17:05 leftover: willow fibres / lilac nodes /
                // orchard chips had no distal lip after CorkTip.
                // Collider stays.
                AddPlate(visual, "CoatFibreTip", new Vector2(along * 0.044f, across * 0.012f),
                    groove, order, new Vector3(-along * 0.34f, across * 0.04f, 0f), -28f);
                AddPlate(visual, "CoatNodeTip", new Vector2(along * 0.10f, across * 0.022f),
                    ridge, order, new Vector3(along * 0.22f, -across * 0.14f, 0f), 16f);
                AddPlate(visual, "CoatSeamTip", new Vector2(along * 0.08f, across * 0.06f),
                    ridge, order, new Vector3(-along * 0.28f, -across * 0.20f, 0f), -14f);
                AddPlate(visual, "CoatChipTip", new Vector2(along * 0.12f, across * 0.028f),
                    groove, order, new Vector3(along * 0.22f, -across * 0.12f, 0f), 10f);
                // Artist 17:35 leftover: pine Scale / bush chips /
                // orchard Knob had no distal lip after ChipTip.
                // Collider stays.
                AddPlate(visual, "CoatScaleTip", new Vector2(along * 0.16f, across * 0.042f),
                    ridge, order, new Vector3(along * 0.26f, -across * 0.16f, 0f), 14f);
                AddPlate(visual, "CoatPlateTip", new Vector2(along * 0.16f, across * 0.055f),
                    groove, order, new Vector3(-along * 0.24f, across * 0.18f, 0f), -16f);
                AddPlate(visual, "CoatKnobTip", new Vector2(along * 0.08f, across * 0.028f),
                    ridge, order, new Vector3(along * 0.18f, across * 0.14f, 0f), 8f);
                return;
            }
            // Artist 12:50: Wrist sat ON the axis after Core left
            // LobeL. Bole Fill (0.02, 0.02) is that hat. Pad rides
            // LobeL. Collider stays.
            // Plant 15:41: Fill+FillR at ±0.07 rebuilt the 0.28-span
            // mast the lips closed (fir Body analog). Artist 16:10:
            // equal 0.24 Body/BodyR were still two pads (poplar
            // KiteL/KiteR 0.15 slit). Unequal kites kiss at x=0;
            // Fill chip rides Body (outer < 0). Collider stays.
            // Artist 05:24: BodyR outer (not mast kiss); FillR on BodyR
            // pad, not the fir slit. Collider stays.
            AddPlate(visual, "CoatBody", new Vector2(along * 0.28f, across * 0.34f),
                c, order, new Vector3(-along * 0.12f, across * 0.02f, 0f), -8f);
            AddPlate(visual, "CoatBodyR", new Vector2(along * 0.18f, across * 0.22f),
                ridge, order, new Vector3(along * 0.22f, -across * 0.02f, 0f), 6f);
            // Plant 18:21 leftover: fir Body a plank after SlopeL2Tip
            // (FanBody before FanBodyTip). Hang off the near pad.
            // Collider stays.
            AddPlate(visual, "CoatBodyTip", new Vector2(along * 0.14f, across * 0.14f),
                c, order, new Vector3(-along * 0.20f, -across * 0.08f, 0f), -14f);
            AddPlate(visual, "CoatBodyRTip", new Vector2(along * 0.10f, across * 0.10f),
                ridge, order, new Vector3(along * 0.32f, -across * 0.10f, 0f), 12f);
            AddPlate(visual, "CoatFill", new Vector2(along * 0.14f, across * 0.24f),
                c, order, new Vector3(-along * 0.20f, across * 0.02f, 0f), -6f);
            AddPlate(visual, "CoatFillR", new Vector2(along * 0.12f, across * 0.20f),
                ridge, order, new Vector3(along * 0.28f, 0f, 0f), 5f);
            // Artist 18:52 leftover: bole Fill a mast chip after
            // BodyTip (Cap before CapTip). Hang off Fill.
            // Collider stays.
            AddPlate(visual, "CoatFillTip", new Vector2(along * 0.08f, across * 0.10f),
                c, order, new Vector3(-along * 0.28f, -across * 0.06f, 0f), -12f);
            AddPlate(visual, "CoatFillRTip", new Vector2(along * 0.07f, across * 0.09f),
                ridge, order, new Vector3(along * 0.36f, -across * 0.08f, 0f), 11f);
            // Plant 18:48 leftover: grow Taper a telegraph stair
            // after ConeCoreTip (Furrow before CorkTip). Hang off
            // Body. Collider stays.
            AddPlate(visual, "CoatTaperTip", new Vector2(along * 0.10f, across * 0.08f),
                c, order, new Vector3(-along * 0.32f, -across * 0.22f, 0f), -18f);
            AddPlate(visual, "CoatFillB", new Vector2(along * 0.56f, across * 0.36f),
                groove, order, new Vector3(-along * 0.32f, -across * 0.04f, 0f), -22f);
            // Artist 17:35 leftover: two cone chips without a distal
            // lip stayed a plank at the butts (Collar before CollarTip).
            // Hang off the flare. Collider stays.
            AddPlate(visual, "CoatButtressLTip", new Vector2(along * 0.18f, across * 0.08f),
                ridge, order, new Vector3(-along * 0.42f, -across * 0.44f, 0f), -16f);
            AddPlate(visual, "CoatButtressRTip", new Vector2(along * 0.16f, across * 0.07f),
                ridge, order, new Vector3(along * 0.38f, -across * 0.42f, 0f), 14f);
            AddPlate(visual, "CoatFillH", new Vector2(along * 0.24f, across * 0.22f),
                ridge, order, new Vector3(along * 0.34f, -across * 0.08f, 0f), 18f);
            // Artist 12:50: Tip/Apex (0.04, 0.30) sat IN the sinus
            // after Dome left LobeL. Peak rides LobeL. Collider stays.
            AddPlate(visual, "CoatFillC", new Vector2(along * 0.30f, across * 0.28f),
                c, order, new Vector3(-along * 0.17f, across * 0.22f, 0f), -6f);
            // Plant 05:51: CapFill at -0.16 / CapFillTip -0.24 left a
            // 0.08 shoulder slit under CapTip -0.06. Chip rides Cap;
            // CapFillTip kisses CapTip. CapFillR under CapRTip.
            // Collider stays.
            AddPlate(visual, "CoatCapFill", new Vector2(along * 0.12f, across * 0.18f),
                c, order, new Vector3(-along * 0.12f, across * 0.28f, 0f), -6f);
            AddPlate(visual, "CoatCapFillTip", new Vector2(along * 0.07f, across * 0.08f),
                c, order, new Vector3(-along * 0.14f, across * 0.34f, 0f), -12f);
            AddPlate(visual, "CoatCapFillR", new Vector2(along * 0.12f, across * 0.16f),
                ridge, order, new Vector3(along * 0.12f, across * 0.26f, 0f), 5f);
            AddPlate(visual, "CoatCapFillRTip", new Vector2(along * 0.07f, across * 0.07f),
                ridge, order, new Vector3(along * 0.13f, across * 0.32f, 0f), 11f);
            // Plant 14:06: Knot +0.10 sat IN the droplet sinus after
            // Core left LobeL. FillG −0.12 was that leftover. Pad
            // rides LobeL. Collider stays.
            AddPlate(visual, "CoatFillG", new Vector2(along * 0.30f, across * 0.20f),
                groove, order, new Vector3(-along * 0.22f, -across * 0.28f, 0f), 16f);
            // Oak furrows sit INSIDE the cone (artist 09:05). Full-length
            // BarkRidge on ConeEdge was a barcode.
            AddPlate(visual, "CoatFurrow0", new Vector2(along * 0.10f, across * 0.09f),
                groove, order, new Vector3(-along * 0.33f, -across * 0.12f, 0f), -12f);
            // Artist 14:12: BarkBreak 0.04 sat ON the bole spine after
            // Flake0 left the flank. Chip rides Furrow0. Collider stays.
            AddPlate(visual, "CoatBreak", new Vector2(along * 0.32f, across * 0.042f),
                ridge, order, new Vector3(-along * 0.22f, -across * 0.18f, 0f), 8f);
            AddPlate(visual, "CoatFurrow1", new Vector2(along * 0.08f, across * 0.11f),
                groove, order, new Vector3(-along * 0.15f, across * 0.08f, 0f), 8f);
            AddPlate(visual, "CoatFurrow2", new Vector2(along * 0.07f, across * 0.08f),
                groove, order, new Vector3(along * 0.11f, -across * 0.06f, 0f), -6f);
            // Artist 14:12: 0.06 sat ON the tip spine (BarkBreak analog).
            // Crack rides the right flank like Furrow1. Collider stays.
            AddPlate(visual, "CoatFurrow3", new Vector2(along * 0.09f, across * 0.10f),
                groove, order, new Vector3(along * 0.33f, across * 0.22f, 0f), 4f);
            // Artist 17:24 leftover: oak Furrow cracks without a lip
            // stayed ticks on the bole (CorkTip analog). Distal chip
            // off each furrow. Collider stays.
            AddPlate(visual, "CoatFurrow0Tip", new Vector2(along * 0.06f, across * 0.040f),
                ridge, order, new Vector3(-along * 0.40f, -across * 0.16f, 0f), -16f);
            AddPlate(visual, "CoatFurrow1Tip", new Vector2(along * 0.05f, across * 0.036f),
                ridge, order, new Vector3(-along * 0.22f, across * 0.04f, 0f), 12f);
            AddPlate(visual, "CoatFurrow2Tip", new Vector2(along * 0.05f, across * 0.032f),
                ridge, order, new Vector3(along * 0.16f, -across * 0.10f, 0f), -10f);
            AddPlate(visual, "CoatFurrow3Tip", new Vector2(along * 0.06f, across * 0.038f),
                ridge, order, new Vector3(along * 0.40f, across * 0.16f, 0f), 10f);
            // Artist 14:49: 0.28 at -0.10 still reached the bole spine
            // (Furrow3 analog). Chip rides Furrow0. Collider stays.
            AddPlate(visual, "CoatFlake0", new Vector2(along * 0.22f, across * 0.070f),
                ridge, order, new Vector3(-along * 0.22f, -across * 0.16f, 0f), 8f);
            AddPlate(visual, "CoatFlake1", new Vector2(along * 0.22f, across * 0.058f),
                ridge, order, new Vector3(along * 0.12f, across * 0.12f, 0f), -8f);
            // Artist 17:35 leftover: bole Flake0/1 and Break without a
            // lip stayed bricks on the trunk (Furrow before FurrowTip).
            // Distal chip off the flank. Collider stays.
            AddPlate(visual, "CoatFlake0Tip", new Vector2(along * 0.12f, across * 0.032f),
                groove, order, new Vector3(-along * 0.30f, -across * 0.22f, 0f), -10f);
            AddPlate(visual, "CoatFlake1Tip", new Vector2(along * 0.12f, across * 0.028f),
                groove, order, new Vector3(along * 0.20f, across * 0.08f, 0f), 12f);
            AddPlate(visual, "CoatBreakTip", new Vector2(along * 0.16f, across * 0.022f),
                groove, order, new Vector3(-along * 0.26f, -across * 0.24f, 0f), -8f);
            AddPlate(visual, "CoatPeel", new Vector2(along * 0.26f, across * 0.048f),
                ridge, order, new Vector3(along * 0.18f, -across * 0.06f, 0f), 4f);
            // Artist 16:10: 0.26 at −0.06 spanned −0.19…+0.07 — a flake
            // on the spine (pine Fill analog). Peel near +X; Peel2 far
            // −X so the bole is not a one-sided tick. Collider stays.
            AddPlate(visual, "CoatPeel2", new Vector2(along * 0.20f, across * 0.040f),
                ridge, order, new Vector3(-along * 0.18f, across * 0.16f, 0f), -12f);
            // Artist 16:32 leftover: far Peel2 without a lip stayed
            // a tick on the pole. Lite chip off Peel2. Collider stays.
            AddPlate(visual, "CoatPeel2Lip", new Vector2(along * 0.10f, across * 0.028f),
                ridge, order, new Vector3(-along * 0.26f, across * 0.12f, 0f), -18f);
            // Artist 16:50 leftover: hazel cork without a lip stayed
            // ticks on the coppice. Distal chip off Peel2Lip.
            // Collider stays.
            AddPlate(visual, "CoatCorkTip", new Vector2(along * 0.08f, across * 0.022f),
                groove, order, new Vector3(-along * 0.30f, across * 0.08f, 0f), -22f);
            // Artist 17:05 leftover: willow fibres / lilac nodes /
            // orchard chips had no distal lip after CorkTip.
            // Collider stays.
            AddPlate(visual, "CoatFibreTip", new Vector2(along * 0.044f, across * 0.012f),
                groove, order, new Vector3(-along * 0.34f, across * 0.04f, 0f), -28f);
            AddPlate(visual, "CoatNodeTip", new Vector2(along * 0.10f, across * 0.022f),
                ridge, order, new Vector3(along * 0.22f, -across * 0.14f, 0f), 16f);
            AddPlate(visual, "CoatSeamTip", new Vector2(along * 0.08f, across * 0.06f),
                ridge, order, new Vector3(-along * 0.28f, -across * 0.20f, 0f), -14f);
            AddPlate(visual, "CoatChipTip", new Vector2(along * 0.12f, across * 0.028f),
                groove, order, new Vector3(along * 0.22f, -across * 0.12f, 0f), 10f);
            AddPlate(visual, "CoatScaleTip", new Vector2(along * 0.16f, across * 0.042f),
                ridge, order, new Vector3(along * 0.26f, -across * 0.16f, 0f), 14f);
            AddPlate(visual, "CoatPlateTip", new Vector2(along * 0.16f, across * 0.055f),
                groove, order, new Vector3(-along * 0.24f, across * 0.18f, 0f), -16f);
            AddPlate(visual, "CoatKnobTip", new Vector2(along * 0.08f, across * 0.028f),
                ridge, order, new Vector3(along * 0.18f, across * 0.14f, 0f), 8f);
            // Artist 11:09: oak peel without a lip stayed a brick of
            // chips. Dark tick off Peel is furrow, not a fourth Crack.
            AddPlate(visual, "CoatPeelLip", new Vector2(along * 0.12f, across * 0.032f),
                groove, order, new Vector3(along * 0.24f, -across * 0.10f, 0f), 16f);
            // Artist 11:59: 8° dash alone was a hyphen. C tall in Y
            // plus that stem is a lenticel. Collider stays.
            AddLenticelC(visual, along, across, ridge, order, true);
            return;
        }
        float colAlong = box.y;
        float colAcross = box.x;
            if (!bole)
            {
                // Plant 13:34: RacemeHub 0.04 / CatkinHub 0.12 of wood
                // was a panicle on a pole. Column Fill at x=0 is that
                // hub. Pad 0.28. Collider stays.
                AddPlate(visual, "CoatFill", new Vector2(colAcross * 0.28f, colAlong * 0.44f),
                    c, order, new Vector3(-colAcross * 0.28f, -colAlong * 0.08f, 0f), 2f);
                AddPlate(visual, "CoatFillR", new Vector2(colAcross * 0.20f, colAlong * 0.22f),
                    ridge, order, new Vector3(colAcross * 0.28f, colAlong * 0.10f, 0f), 6f);
                // Artist 18:52 leftover: column Fill a stool after
                // TuftLTip (Cap before CapTip). Hang off Fill.
                // Collider stays.
                AddPlate(visual, "CoatFillTip", new Vector2(colAcross * 0.14f, colAlong * 0.18f),
                    c, order, new Vector3(-colAcross * 0.38f, -colAlong * 0.16f, 0f), -4f);
                AddPlate(visual, "CoatFillRTip", new Vector2(colAcross * 0.10f, colAlong * 0.12f),
                    ridge, order, new Vector3(colAcross * 0.36f, colAlong * 0.18f, 0f), 10f);
                // Plant 15:28: ForkFill 0.24 at −0.18 still a crotch
                // belt. Lip 0.36, |x|>half. Collider stays.
                AddPlate(visual, "CoatFillWaist", new Vector2(colAcross * 0.18f, colAlong * 0.10f),
                    ridge, order, new Vector3(-colAcross * 0.24f, colAlong * 0.08f, 0f), 6f);
                AddPlate(visual, "CoatFillWaistTip", new Vector2(colAcross * 0.10f, colAlong * 0.06f),
                    ridge, order, new Vector3(-colAcross * 0.36f, colAlong * 0.02f, 0f), 12f);
                AddPlate(visual, "CoatFillB", new Vector2(colAcross * 0.16f, colAlong * 0.14f),
                    groove, order, new Vector3(-colAcross * 0.20f, -colAlong * 0.16f, 0f), 10f);
                AddPlate(visual, "CoatFillA", new Vector2(colAcross * 0.14f, colAlong * 0.11f),
                    ridge, order, new Vector3(colAcross * 0.18f, colAlong * 0.18f, 0f), -10f);
                AddPlate(visual, "CoatFlake2", new Vector2(colAcross * 0.12f, colAlong * 0.034f),
                    ridge, order, new Vector3(-colAcross * 0.16f, colAlong * 0.12f, 0f), -8f);
                AddPlate(visual, "CoatFlake2Tip", new Vector2(colAcross * 0.08f, colAlong * 0.028f),
                    ridge, order, new Vector3(-colAcross * 0.24f, colAlong * 0.06f, 0f), -14f);
                AddPlate(visual, "CoatBarkPlateTip", new Vector2(colAcross * 0.14f, colAlong * 0.055f),
                    groove, order, new Vector3(-colAcross * 0.32f, colAlong * 0.00f, 0f), -22f);
                // Plant 13:49: Rachis at the hub origin was a mast
                // through the panicle. Pad −0.14 bloom. Collider stays.
                AddPlate(visual, "CoatPeel", new Vector2(colAcross * 0.048f, colAlong * 0.26f),
                    ridge, order, new Vector3(colAcross * 0.18f, colAlong * 0.06f, 0f), 4f);
                AddPlate(visual, "CoatPeel2", new Vector2(colAcross * 0.040f, colAlong * 0.20f),
                    ridge, order, new Vector3(-colAcross * 0.18f, colAlong * 0.16f, 0f), -12f);
                AddPlate(visual, "CoatPeel2Lip", new Vector2(colAcross * 0.028f, colAlong * 0.10f),
                    ridge, order, new Vector3(-colAcross * 0.26f, colAlong * 0.12f, 0f), -18f);
                AddPlate(visual, "CoatCorkTip", new Vector2(colAcross * 0.022f, colAlong * 0.08f),
                    groove, order, new Vector3(-colAcross * 0.30f, colAlong * 0.08f, 0f), -22f);
                // Artist 17:05 leftover: willow fibres / lilac nodes /
                // orchard chips had no distal lip after CorkTip.
                // Collider stays.
                AddPlate(visual, "CoatFibreTip", new Vector2(colAcross * 0.012f, colAlong * 0.044f),
                    groove, order, new Vector3(-colAcross * 0.34f, colAlong * 0.04f, 0f), -28f);
                AddPlate(visual, "CoatNodeTip", new Vector2(colAcross * 0.022f, colAlong * 0.10f),
                    ridge, order, new Vector3(colAcross * 0.22f, -colAlong * 0.14f, 0f), 16f);
                AddPlate(visual, "CoatSeamTip", new Vector2(colAcross * 0.06f, colAlong * 0.08f),
                    ridge, order, new Vector3(-colAcross * 0.28f, -colAlong * 0.20f, 0f), -14f);
                AddPlate(visual, "CoatChipTip", new Vector2(colAcross * 0.028f, colAlong * 0.12f),
                    groove, order, new Vector3(colAcross * 0.22f, -colAlong * 0.12f, 0f), 10f);
                AddPlate(visual, "CoatScaleTip", new Vector2(colAcross * 0.042f, colAlong * 0.16f),
                    ridge, order, new Vector3(colAcross * 0.26f, -colAlong * 0.16f, 0f), 14f);
                AddPlate(visual, "CoatPlateTip", new Vector2(colAcross * 0.055f, colAlong * 0.16f),
                    groove, order, new Vector3(-colAcross * 0.24f, colAlong * 0.18f, 0f), -16f);
                AddPlate(visual, "CoatKnobTip", new Vector2(colAcross * 0.028f, colAlong * 0.08f),
                    ridge, order, new Vector3(colAcross * 0.18f, colAlong * 0.14f, 0f), 8f);
                return;
            }
            // Artist 05:24: BodyR outer; FillR on BodyR pad. Collider stays.
            AddPlate(visual, "CoatBody", new Vector2(colAcross * 0.28f, colAlong * 0.34f),
                c, order, new Vector3(-colAcross * 0.12f, colAlong * 0.02f, 0f), -8f);
            AddPlate(visual, "CoatBodyR", new Vector2(colAcross * 0.18f, colAlong * 0.22f),
                ridge, order, new Vector3(colAcross * 0.22f, -colAlong * 0.02f, 0f), 6f);
            AddPlate(visual, "CoatBodyTip", new Vector2(colAcross * 0.14f, colAlong * 0.14f),
                c, order, new Vector3(-colAcross * 0.20f, -colAlong * 0.08f, 0f), -14f);
            AddPlate(visual, "CoatBodyRTip", new Vector2(colAcross * 0.10f, colAlong * 0.10f),
                ridge, order, new Vector3(colAcross * 0.32f, -colAlong * 0.10f, 0f), 12f);
            AddPlate(visual, "CoatFill", new Vector2(colAcross * 0.14f, colAlong * 0.24f),
                c, order, new Vector3(-colAcross * 0.20f, colAlong * 0.02f, 0f), -6f);
            AddPlate(visual, "CoatFillR", new Vector2(colAcross * 0.12f, colAlong * 0.20f),
                ridge, order, new Vector3(colAcross * 0.28f, 0f, 0f), 5f);
            // Artist 18:52 leftover: column bole Fill a mast after
            // BodyTip. Hang off Fill. Collider stays.
            AddPlate(visual, "CoatFillTip", new Vector2(colAcross * 0.08f, colAlong * 0.10f),
                c, order, new Vector3(-colAcross * 0.28f, -colAlong * 0.06f, 0f), -12f);
            AddPlate(visual, "CoatFillRTip", new Vector2(colAcross * 0.07f, colAlong * 0.09f),
                ridge, order, new Vector3(colAcross * 0.36f, -colAlong * 0.08f, 0f), 11f);
            AddPlate(visual, "CoatTaperTip", new Vector2(colAcross * 0.08f, colAlong * 0.10f),
                c, order, new Vector3(-colAcross * 0.32f, -colAlong * 0.22f, 0f), -18f);
            AddPlate(visual, "CoatFillB", new Vector2(colAcross * 0.36f, colAlong * 0.56f),
                groove, order, new Vector3(-colAcross * 0.28f, -colAlong * 0.32f, 0f), -22f);
            AddPlate(visual, "CoatButtressLTip", new Vector2(colAcross * 0.18f, colAlong * 0.08f),
                ridge, order, new Vector3(-colAcross * 0.42f, -colAlong * 0.44f, 0f), -16f);
            AddPlate(visual, "CoatButtressRTip", new Vector2(colAcross * 0.16f, colAlong * 0.07f),
                ridge, order, new Vector3(colAcross * 0.38f, -colAlong * 0.42f, 0f), 14f);
            AddPlate(visual, "CoatFillH", new Vector2(colAcross * 0.22f, colAlong * 0.24f),
                ridge, order, new Vector3(-colAcross * 0.18f, colAlong * 0.34f, 0f), 18f);
            AddPlate(visual, "CoatFillC", new Vector2(colAcross * 0.28f, colAlong * 0.30f),
                c, order, new Vector3(-colAcross * 0.17f, colAlong * 0.22f, 0f), -6f);
            // Plant 05:51: column CapFill under CapTip (Body Fill
            // after Body kiss). CapFillTip kisses CapTip; CapFillR
            // under CapRTip. Collider stays.
            AddPlate(visual, "CoatCapFill", new Vector2(colAcross * 0.12f, colAlong * 0.18f),
                c, order, new Vector3(-colAcross * 0.12f, colAlong * 0.28f, 0f), -6f);
            AddPlate(visual, "CoatCapFillTip", new Vector2(colAcross * 0.07f, colAlong * 0.08f),
                c, order, new Vector3(-colAcross * 0.14f, colAlong * 0.34f, 0f), -12f);
            AddPlate(visual, "CoatCapFillR", new Vector2(colAcross * 0.12f, colAlong * 0.16f),
                ridge, order, new Vector3(colAcross * 0.12f, colAlong * 0.26f, 0f), 5f);
            AddPlate(visual, "CoatCapFillRTip", new Vector2(colAcross * 0.07f, colAlong * 0.07f),
                ridge, order, new Vector3(colAcross * 0.13f, colAlong * 0.32f, 0f), 11f);
            AddPlate(visual, "CoatFillG", new Vector2(colAcross * 0.20f, colAlong * 0.30f),
                groove, order, new Vector3(-colAcross * 0.28f, -colAlong * 0.12f, 0f), 16f);
            AddPlate(visual, "CoatFurrow0", new Vector2(colAcross * 0.10f, colAlong * 0.09f),
                groove, order, new Vector3(-colAcross * 0.33f, -colAlong * 0.12f, 0f), -12f);
            AddPlate(visual, "CoatBreak", new Vector2(colAlong * 0.042f, colAcross * 0.32f),
                ridge, order, new Vector3(-colAcross * 0.18f, -colAlong * 0.22f, 0f), 8f);
            AddPlate(visual, "CoatFurrow1", new Vector2(colAcross * 0.08f, colAlong * 0.11f),
                groove, order, new Vector3(-colAcross * 0.15f, colAlong * 0.08f, 0f), 8f);
            AddPlate(visual, "CoatFurrow2", new Vector2(colAcross * 0.07f, colAlong * 0.08f),
                groove, order, new Vector3(colAcross * 0.11f, -colAlong * 0.06f, 0f), -6f);
            AddPlate(visual, "CoatFurrow3", new Vector2(colAcross * 0.09f, colAlong * 0.10f),
                groove, order, new Vector3(colAcross * 0.33f, colAlong * 0.04f, 0f), 4f);
            // Artist 17:24 leftover: oak Furrow cracks without a lip
            // stayed ticks on the bole (CorkTip analog). Distal chip
            // off each furrow. Collider stays.
            AddPlate(visual, "CoatFurrow0Tip", new Vector2(colAcross * 0.06f, colAlong * 0.040f),
                ridge, order, new Vector3(-colAcross * 0.40f, -colAlong * 0.16f, 0f), -16f);
            AddPlate(visual, "CoatFurrow1Tip", new Vector2(colAcross * 0.05f, colAlong * 0.036f),
                ridge, order, new Vector3(-colAcross * 0.22f, colAlong * 0.04f, 0f), 12f);
            AddPlate(visual, "CoatFurrow2Tip", new Vector2(colAcross * 0.05f, colAlong * 0.032f),
                ridge, order, new Vector3(colAcross * 0.16f, -colAlong * 0.10f, 0f), -10f);
            AddPlate(visual, "CoatFurrow3Tip", new Vector2(colAcross * 0.06f, colAlong * 0.038f),
                ridge, order, new Vector3(colAcross * 0.40f, colAlong * 0.16f, 0f), 10f);
            AddPlate(visual, "CoatFlake0", new Vector2(colAcross * 0.22f, colAlong * 0.070f),
                ridge, order, new Vector3(-colAcross * 0.22f, -colAlong * 0.16f, 0f), 8f);
            AddPlate(visual, "CoatFlake1", new Vector2(colAcross * 0.22f, colAlong * 0.058f),
                ridge, order, new Vector3(colAcross * 0.12f, colAlong * 0.12f, 0f), -8f);
            AddPlate(visual, "CoatFlake0Tip", new Vector2(colAcross * 0.12f, colAlong * 0.032f),
                groove, order, new Vector3(-colAcross * 0.30f, -colAlong * 0.22f, 0f), -10f);
            AddPlate(visual, "CoatFlake1Tip", new Vector2(colAcross * 0.12f, colAlong * 0.028f),
                groove, order, new Vector3(colAcross * 0.20f, colAlong * 0.08f, 0f), 12f);
            AddPlate(visual, "CoatBreakTip", new Vector2(colAcross * 0.16f, colAlong * 0.022f),
                groove, order, new Vector3(-colAcross * 0.26f, -colAlong * 0.24f, 0f), -8f);
            AddPlate(visual, "CoatPeel", new Vector2(colAcross * 0.26f, colAlong * 0.048f),
                ridge, order, new Vector3(colAcross * 0.18f, -colAlong * 0.06f, 0f), 4f);
            AddPlate(visual, "CoatPeel2", new Vector2(colAcross * 0.20f, colAlong * 0.040f),
                ridge, order, new Vector3(-colAcross * 0.18f, colAlong * 0.16f, 0f), -12f);
            AddPlate(visual, "CoatPeel2Lip", new Vector2(colAcross * 0.10f, colAlong * 0.028f),
                ridge, order, new Vector3(-colAcross * 0.26f, colAlong * 0.12f, 0f), -18f);
            AddPlate(visual, "CoatCorkTip", new Vector2(colAcross * 0.08f, colAlong * 0.022f),
                groove, order, new Vector3(-colAcross * 0.30f, colAlong * 0.08f, 0f), -22f);
            // Artist 17:05 leftover: willow fibres / lilac nodes /
            // orchard chips had no distal lip after CorkTip.
            // Collider stays.
            AddPlate(visual, "CoatFibreTip", new Vector2(colAcross * 0.044f, colAlong * 0.012f),
                groove, order, new Vector3(-colAcross * 0.34f, colAlong * 0.04f, 0f), -28f);
            AddPlate(visual, "CoatNodeTip", new Vector2(colAcross * 0.10f, colAlong * 0.022f),
                ridge, order, new Vector3(colAcross * 0.22f, -colAlong * 0.14f, 0f), 16f);
            AddPlate(visual, "CoatSeamTip", new Vector2(colAcross * 0.08f, colAlong * 0.06f),
                ridge, order, new Vector3(-colAcross * 0.28f, -colAlong * 0.20f, 0f), -14f);
            AddPlate(visual, "CoatChipTip", new Vector2(colAcross * 0.12f, colAlong * 0.028f),
                groove, order, new Vector3(colAcross * 0.22f, -colAlong * 0.12f, 0f), 10f);
            AddPlate(visual, "CoatScaleTip", new Vector2(colAcross * 0.16f, colAlong * 0.042f),
                ridge, order, new Vector3(colAcross * 0.26f, -colAlong * 0.16f, 0f), 14f);
            AddPlate(visual, "CoatPlateTip", new Vector2(colAcross * 0.16f, colAlong * 0.055f),
                groove, order, new Vector3(-colAcross * 0.24f, colAlong * 0.18f, 0f), -16f);
            AddPlate(visual, "CoatKnobTip", new Vector2(colAcross * 0.08f, colAlong * 0.028f),
                ridge, order, new Vector3(colAcross * 0.18f, colAlong * 0.14f, 0f), 8f);
            AddPlate(visual, "CoatPeelLip", new Vector2(colAcross * 0.12f, colAlong * 0.032f),
                groove, order, new Vector3(colAcross * 0.24f, -colAlong * 0.10f, 0f), 16f);
            AddLenticelC(visual, colAlong, colAcross, ridge, order, false);
    }

    // Artist 11:59: 8° dash alone was a 3 cm hyphen. Stem tall in Y,
    // two short arms toward the pith. −Y of the stem is dirt.
    static void AddLenticelC(
        Transform visual, float along, float across, Color ridge, int order, bool xLong)
    {
        float stemW;
        float stemH;
        Vector3 at;
        if (xLong)
        {
            stemW = Mathf.Max(across * 0.10f, 0.008f);
            stemH = Mathf.Max(across * 0.28f, 0.016f);
            at = new Vector3(along * 0.16f, across * 0.06f, 0f);
        }
        else
        {
            stemW = Mathf.Max(across * 0.10f, 0.008f);
            stemH = Mathf.Max(along * 0.055f, 0.012f);
            at = new Vector3(across * 0.16f, along * 0.06f, 0f);
        }
        AddPlate(visual, "CoatLenticel", new Vector2(stemW, stemH),
            ridge, order, at, 8f);
        // Artist 12:14: arms at stemH*0.22 were 6 mm hyphens.
        // Thickness follows the stem so the C opening reads at 1x.
        float armThick = Mathf.Max(stemW * 1.20f, 0.012f);
        float armLong = Mathf.Max(stemW * 2.40f, stemH * 0.55f);
        AddPlate(visual, "CoatCarm", new Vector2(armLong, armThick),
            ridge, order,
            at + new Vector3(-(stemW * 0.45f + armLong * 0.28f), stemH * 0.38f, 0f), 8f);
        AddPlate(visual, "CoatCarmB", new Vector2(armLong * 0.82f, armThick * 0.90f),
            ridge, order,
            at + new Vector3(-(stemW * 0.35f + armLong * 0.22f), -stemH * 0.36f, 0f), 8f);
        // Artist 17:56 leftover: stem and C-arms without a distal chip
        // stayed hyphens on the silver pole (Furrow before CorkTip).
        // Hang off the opening. Collider stays.
        AddPlate(visual, "CoatLensTip", new Vector2(stemW * 1.40f, stemH * 0.28f),
            ridge, order,
            at + new Vector3(stemW * 0.18f, stemH * 0.42f, 0f), 12f);
        AddPlate(visual, "CoatCarmTip", new Vector2(armLong * 0.38f, armThick * 0.80f),
            ridge, order,
            at + new Vector3(-(stemW * 0.55f + armLong * 0.42f), stemH * 0.38f, 0f), 8f);
        // Artist 18:16 leftover: lower CarmB without a lip stayed a
        // hyphen after CarmTip (Lens before LensTip). Hang off the
        // opening. Collider stays.
        AddPlate(visual, "CoatCarmBTip", new Vector2(armLong * 0.32f, armThick * 0.72f),
            ridge, order,
            at + new Vector3(-(stemW * 0.48f + armLong * 0.36f), -stemH * 0.42f, 0f), 8f);
    }

    // Artist 11:44: FanL2/FanR2 |x|≥0.36; fringe/hem 8–10° short
    // ticks on the outer fans. Visual 11:29 left Fan2 at 0.18/0.22
    // (still hats). Column path had no FanR2 and FringeM at |x|=0.10.
    // FingerR past ToothR lives in AddTwigSpray. Collider stays.
    static void AddCedarPads(Transform visual, Vector2 box, Color mid, Color edge, int o, bool xLong)
    {
        Color odd = OddLeaflet(mid, edge);
        if (xLong)
        {
            // Artist 14:49: BaseL 0.36 at -0.22 fused the fir hem
            // (KiteR analog). Pad rides Fan2 / SlopeL2. Collider stays.
            AddPlate(visual, "Coat0", new Vector2(box.x * 0.30f, box.y * 0.28f),
                edge, o, new Vector3(-box.x * 0.42f, -box.y * 0.32f, 0f), -14f);
            AddPlate(visual, "CoatFan2", new Vector2(box.x * 0.28f, box.y * 0.26f),
                odd, o, new Vector3(-box.x * 0.42f, -box.y * 0.18f, 0f), -8f);
            // Artist 19:11 leftover: Fan2 a mid pancake after FanLTip
            // (Fringe before FringeTip). Hang off every fan pad.
            // Collider stays.
            AddPlate(visual, "CoatFan2Tip", new Vector2(box.x * 0.10f, box.y * 0.06f),
                odd, o, new Vector3(-box.x * 0.58f, -box.y * 0.30f, 0f), -16f);
            // Artist 05:24: FanBody 0.24 at −0.12 vs FanR +0.18 left a
            // sky slit; FanFillR sat in it. Wider FanBody kisses FanR;
            // Fill chips ride outer pads. Collider stays.
            AddPlate(visual, "CoatFanBody", new Vector2(box.x * 0.40f, box.y * 0.26f),
                edge, o, new Vector3(-box.x * 0.02f, -box.y * 0.12f, 0f), -10f);
            // Plant 05:59: FanFill under FanBodyTip (not the mid
            // table). FanFillTip kisses FanBodyTip; FanFillR under
            // FanRTip. Collider stays.
            AddPlate(visual, "CoatFanFill", new Vector2(box.x * 0.12f, box.y * 0.16f),
                odd, o, new Vector3(-box.x * 0.14f, -box.y * 0.15f, 0f), -8f);
            AddPlate(visual, "CoatFanFillR", new Vector2(box.x * 0.12f, box.y * 0.14f),
                mid, o, new Vector3(box.x * 0.34f, -box.y * 0.10f, 0f), 6f);
            AddPlate(visual, "CoatFanFillTip", new Vector2(box.x * 0.07f, box.y * 0.08f),
                odd, o, new Vector3(-box.x * 0.20f, -box.y * 0.22f, 0f), -14f);
            AddPlate(visual, "CoatFanFillRTip", new Vector2(box.x * 0.07f, box.y * 0.07f),
                mid, o, new Vector3(box.x * 0.38f, -box.y * 0.16f, 0f), 12f);
            AddPlate(visual, "CoatFanR", new Vector2(box.x * 0.20f, box.y * 0.16f),
                edge, o, new Vector3(box.x * 0.28f, -box.y * 0.06f, 0f), 12f);
            // Artist 12:50: FanDome (−0.04, 0.38) was a hat on the
            // spine. Plant 15:56: 0.16 at −0.42 was one tent (Cap
            // without CapR). FanDome/FanDomeR kiss at x=0 at max-Y.
            // Collider stays.
            AddPlate(visual, "CoatFanDome", new Vector2(box.x * 0.18f, box.y * 0.18f),
                odd, o, new Vector3(-box.x * 0.09f, box.y * 0.32f, 0f), -8f);
            AddPlate(visual, "CoatFanDomeR", new Vector2(box.x * 0.16f, box.y * 0.16f),
                mid, o, new Vector3(box.x * 0.08f, box.y * 0.28f, 0f), 6f);
            // Plant 13:34: centre spray at hub origin + fan 6° was a
            // mast. Lance rides the left pad. Plant 13:49: laterals
            // 0/2 still stacked at that hub — fan stations off it.
            // Collider stays.
            AddPlate(visual, "CoatSpray", new Vector2(box.x * 0.22f, box.y * 0.18f),
                odd, o, new Vector3(-box.x * 0.28f, box.y * 0.08f, 0f), 6f);
            // Artist 18:52 leftover: spray Lance a paddle after
            // PetioleTip (Cap before CapTip). Hang off the lance.
            // Collider stays.
            AddPlate(visual, "CoatLanceTip", new Vector2(box.x * 0.10f, box.y * 0.08f),
                odd, o, new Vector3(-box.x * 0.38f, box.y * 0.16f, 0f), 12f);
            // Artist 14:49: LayerL 0.30 at -0.22 still reached -0.07
            // after BaseL left the hem. Mid wedge rides SlopeL.
            // Collider stays.
            AddPlate(visual, "CoatSprayL", new Vector2(box.x * 0.18f, box.y * 0.16f),
                odd, o, new Vector3(-box.x * 0.28f, box.y * 0.04f, 0f), -18f);
            AddPlate(visual, "CoatSprayR", new Vector2(box.x * 0.16f, box.y * 0.14f),
                odd, o, new Vector3(box.x * 0.18f, box.y * 0.10f, 0f), 24f);
            AddPlate(visual, "Coat5", new Vector2(box.x * 0.28f, box.y * 0.26f),
                edge, o, new Vector3(box.x * 0.32f, -box.y * 0.34f, 0f), 12f);
            AddPlate(visual, "CoatFanR2", new Vector2(box.x * 0.32f, box.y * 0.24f),
                mid, o, new Vector3(box.x * 0.36f, box.y * 0.14f, 0f), 8f);
            AddPlate(visual, "Coat7", new Vector2(box.x * 0.28f, box.y * 0.22f),
                odd, o, new Vector3(-box.x * 0.38f, box.y * 0.12f, 0f), -11f);
            AddPlate(visual, "CoatFanM2", new Vector2(box.x * 0.22f, box.y * 0.18f),
                edge, o, new Vector3(box.x * 0.40f, box.y * 0.06f, 0f), 8f);
            AddPlate(visual, "CoatDroop", new Vector2(box.x * 0.34f, box.y * 0.24f),
                edge, o, new Vector3(box.x * 0.32f, -box.y * 0.22f, 0f), 5f);
            // Artist 17:24 leftover: right-only Droop was a one-tent
            // cedar belly after FanDomeR (HemL analog). Far hang stays
            // Droop; this pad rides FanL. Collider stays.
            AddPlate(visual, "CoatDroopL", new Vector2(box.x * 0.30f, box.y * 0.22f),
                odd, o, new Vector3(-box.x * 0.30f, -box.y * 0.28f, 0f), -8f);
            // Artist 17:35 leftover: two stools without a distal chip
            // stayed ticks on the hedge butts; Slope wedges stayed
            // roof slats (Flake before FlakeTip). Lip hangs off the
            // outer pad. Collider stays.
            AddPlate(visual, "CoatStoolLTip", new Vector2(box.x * 0.16f, box.y * 0.10f),
                odd, o, new Vector3(-box.x * 0.48f, -box.y * 0.38f, 0f), -18f);
            AddPlate(visual, "CoatStoolRTip", new Vector2(box.x * 0.14f, box.y * 0.09f),
                edge, o, new Vector3(box.x * 0.40f, -box.y * 0.40f, 0f), 16f);
            AddPlate(visual, "CoatSlopeLTip", new Vector2(box.x * 0.14f, box.y * 0.16f),
                edge, o, new Vector3(-box.x * 0.44f, -box.y * 0.12f, 0f), -16f);
            AddPlate(visual, "CoatSlopeRTip", new Vector2(box.x * 0.12f, box.y * 0.14f),
                odd, o, new Vector3(box.x * 0.40f, -box.y * 0.14f, 0f), 14f);
            float fringeH = Mathf.Max(box.y * 0.28f, box.x * 0.022f);
            AddPlate(visual, "CoatFringeL", new Vector2(box.x * 0.06f, fringeH),
                edge, o, new Vector3(-box.x * 0.42f, -box.y * 0.36f, 0f), -10f);
            AddPlate(visual, "CoatFringeR", new Vector2(box.x * 0.05f, fringeH * 0.88f),
                odd, o, new Vector3(box.x * 0.36f, -box.y * 0.18f, 0f), 8f);
            AddPlate(visual, "CoatFringeM", new Vector2(box.x * 0.05f, fringeH * 0.72f),
                edge, o, new Vector3(-box.x * 0.36f, -box.y * 0.40f, 0f), 9f);
            AddPlate(visual, "CoatHemL", new Vector2(box.x * 0.06f, fringeH * 0.90f),
                edge, o, new Vector3(-box.x * 0.48f, -box.y * 0.48f, 0f), -10f);
            AddPlate(visual, "CoatHemR", new Vector2(box.x * 0.05f, fringeH * 0.78f),
                odd, o, new Vector3(box.x * 0.44f, -box.y * 0.22f, 0f), 8f);
            // Artist 11:59: Slope without a mid tuft was two wedges.
            // Short needles 8–10° between the fans. Collider stays.
            AddPlate(visual, "CoatWhorlL", new Vector2(box.x * 0.10f, box.y * 0.18f),
                edge, o, new Vector3(-box.x * 0.36f, -box.y * 0.28f, 0f), -10f);
            AddPlate(visual, "CoatWhorlR", new Vector2(box.x * 0.09f, box.y * 0.16f),
                odd, o, new Vector3(box.x * 0.32f, -box.y * 0.30f, 0f), 9f);
            // Artist 17:56 / plant 18:06 leftover: FanL2/M coins,
            // Layer/Base/Cap/Whorl/Fringe/Hem posts after SlopeTip.
            // Distal lips hang off the outer pad. Collider stays.
            AddPlate(visual, "CoatFanLTip", new Vector2(box.x * 0.16f, box.y * 0.12f),
                edge, o, new Vector3(-box.x * 0.38f, -box.y * 0.46f, 0f), -20f);
            AddPlate(visual, "CoatFanL2Tip", new Vector2(box.x * 0.14f, box.y * 0.10f),
                odd, o, new Vector3(-box.x * 0.52f, -box.y * 0.24f, 0f), -14f);
            AddPlate(visual, "CoatFanRTip", new Vector2(box.x * 0.14f, box.y * 0.10f),
                edge, o, new Vector3(box.x * 0.28f, -box.y * 0.12f, 0f), 18f);
            AddPlate(visual, "CoatFanR2Tip", new Vector2(box.x * 0.16f, box.y * 0.10f),
                mid, o, new Vector3(box.x * 0.46f, box.y * 0.08f, 0f), 14f);
            AddPlate(visual, "CoatFanMTip", new Vector2(box.x * 0.14f, box.y * 0.10f),
                odd, o, new Vector3(-box.x * 0.48f, box.y * 0.06f, 0f), -16f);
            AddPlate(visual, "CoatFanM2Tip", new Vector2(box.x * 0.12f, box.y * 0.08f),
                edge, o, new Vector3(box.x * 0.50f, 0f, 0f), 14f);
            AddPlate(visual, "CoatLayerLTip", new Vector2(box.x * 0.14f, box.y * 0.12f),
                odd, o, new Vector3(-box.x * 0.38f, 0f, 0f), -18f);
            AddPlate(visual, "CoatLayerRTip", new Vector2(box.x * 0.12f, box.y * 0.10f),
                odd, o, new Vector3(box.x * 0.28f, box.y * 0.04f, 0f), 16f);
            AddPlate(visual, "CoatBaseLTip", new Vector2(box.x * 0.16f, box.y * 0.12f),
                edge, o, new Vector3(-box.x * 0.52f, -box.y * 0.38f, 0f), -16f);
            AddPlate(visual, "CoatBaseRTip", new Vector2(box.x * 0.14f, box.y * 0.10f),
                edge, o, new Vector3(box.x * 0.42f, -box.y * 0.42f, 0f), 14f);
            // Artist 05:46 CapTip peak kiss: -0.18/+0.16 left a slit
            // above CapR. Kiss at x=0. Collider stays.
            AddPlate(visual, "CoatCapTip", new Vector2(box.x * 0.12f, box.y * 0.14f),
                odd, o, new Vector3(-box.x * 0.06f, box.y * 0.36f, 0f), -12f);
            AddPlate(visual, "CoatCapRTip", new Vector2(box.x * 0.10f, box.y * 0.12f),
                mid, o, new Vector3(box.x * 0.05f, box.y * 0.34f, 0f), 10f);
            AddPlate(visual, "CoatFringeLTip", new Vector2(box.x * 0.04f, fringeH * 0.40f),
                edge, o, new Vector3(-box.x * 0.48f, -box.y * 0.44f, 0f), -16f);
            AddPlate(visual, "CoatFringeRTip", new Vector2(box.x * 0.035f, fringeH * 0.36f),
                odd, o, new Vector3(box.x * 0.42f, -box.y * 0.26f, 0f), 14f);
            AddPlate(visual, "CoatHemLTip", new Vector2(box.x * 0.04f, fringeH * 0.36f),
                edge, o, new Vector3(-box.x * 0.54f, -box.y * 0.56f, 0f), -16f);
            AddPlate(visual, "CoatHemRTip", new Vector2(box.x * 0.035f, fringeH * 0.32f),
                odd, o, new Vector3(box.x * 0.50f, -box.y * 0.30f, 0f), 14f);
            AddPlate(visual, "CoatWhorlLTip", new Vector2(box.x * 0.06f, box.y * 0.08f),
                edge, o, new Vector3(-box.x * 0.42f, -box.y * 0.34f, 0f), -16f);
            AddPlate(visual, "CoatWhorlRTip", new Vector2(box.x * 0.05f, box.y * 0.07f),
                odd, o, new Vector3(box.x * 0.38f, -box.y * 0.36f, 0f), 14f);
            // Artist 18:16 leftover: FanBody/FanDome/Droop coins after
            // FanL2Tip; FringeM a post after FringeLTip. Distal lips.
            // Collider stays.
            AddPlate(visual, "CoatFanBodyTip", new Vector2(box.x * 0.12f, box.y * 0.12f),
                edge, o, new Vector3(-box.x * 0.18f, -box.y * 0.20f, 0f), -16f);
            // Artist 05:46 FanDomeTip peak kiss at x=0. Artist 05:55:
            // FanDomeFill under FanDomeTip (CapFill under CapTip).
            // Collider stays.
            AddPlate(visual, "CoatFanDomeTip", new Vector2(box.x * 0.10f, box.y * 0.08f),
                odd, o, new Vector3(-box.x * 0.05f, box.y * 0.40f, 0f), -14f);
            AddPlate(visual, "CoatFanDomeRTip", new Vector2(box.x * 0.08f, box.y * 0.07f),
                mid, o, new Vector3(box.x * 0.04f, box.y * 0.38f, 0f), 12f);
            AddPlate(visual, "CoatFanDomeFill", new Vector2(box.x * 0.10f, box.y * 0.14f),
                odd, o, new Vector3(-box.x * 0.10f, box.y * 0.34f, 0f), -6f);
            AddPlate(visual, "CoatFanDomeFillTip", new Vector2(box.x * 0.06f, box.y * 0.07f),
                odd, o, new Vector3(-box.x * 0.12f, box.y * 0.38f, 0f), -12f);
            AddPlate(visual, "CoatFanDomeFillR", new Vector2(box.x * 0.10f, box.y * 0.12f),
                mid, o, new Vector3(box.x * 0.10f, box.y * 0.32f, 0f), 5f);
            AddPlate(visual, "CoatFanDomeFillRTip", new Vector2(box.x * 0.06f, box.y * 0.06f),
                mid, o, new Vector3(box.x * 0.11f, box.y * 0.36f, 0f), 11f);
            AddPlate(visual, "CoatDroopTip", new Vector2(box.x * 0.16f, box.y * 0.10f),
                edge, o, new Vector3(box.x * 0.40f, -box.y * 0.30f, 0f), 10f);
            AddPlate(visual, "CoatDroopLTip", new Vector2(box.x * 0.14f, box.y * 0.10f),
                odd, o, new Vector3(-box.x * 0.38f, -box.y * 0.36f, 0f), -14f);
            AddPlate(visual, "CoatFringeMTip", new Vector2(box.x * 0.04f, fringeH * 0.32f),
                edge, o, new Vector3(-box.x * 0.42f, -box.y * 0.48f, 0f), 12f);
            return;
        }
        AddPlate(visual, "Coat0", new Vector2(box.x * 0.24f, box.y * 0.30f),
            edge, o, new Vector3(-box.x * 0.42f, -box.y * 0.32f, 0f), -14f);
        AddPlate(visual, "CoatFan2", new Vector2(box.x * 0.24f, box.y * 0.28f),
            odd, o, new Vector3(-box.x * 0.42f, -box.y * 0.18f, 0f), -8f);
        AddPlate(visual, "CoatFan2Tip", new Vector2(box.x * 0.08f, box.y * 0.06f),
            odd, o, new Vector3(-box.x * 0.58f, -box.y * 0.30f, 0f), -16f);
        // Artist 05:24: FanBody kisses FanR; Fill chips on outer pads.
        // Collider stays.
        AddPlate(visual, "CoatFanBody", new Vector2(box.x * 0.40f, box.y * 0.26f),
            edge, o, new Vector3(-box.x * 0.02f, -box.y * 0.12f, 0f), -10f);
        // Plant 05:59: FanFill under FanBodyTip (not the mid table).
        // Collider stays.
        AddPlate(visual, "CoatFanFill", new Vector2(box.x * 0.12f, box.y * 0.16f),
            odd, o, new Vector3(-box.x * 0.14f, -box.y * 0.15f, 0f), -8f);
        AddPlate(visual, "CoatFanFillR", new Vector2(box.x * 0.12f, box.y * 0.14f),
            mid, o, new Vector3(box.x * 0.34f, -box.y * 0.10f, 0f), 6f);
        AddPlate(visual, "CoatFanFillTip", new Vector2(box.x * 0.07f, box.y * 0.08f),
            odd, o, new Vector3(-box.x * 0.20f, -box.y * 0.22f, 0f), -14f);
        AddPlate(visual, "CoatFanFillRTip", new Vector2(box.x * 0.07f, box.y * 0.07f),
            mid, o, new Vector3(box.x * 0.38f, -box.y * 0.16f, 0f), 12f);
        AddPlate(visual, "CoatFanR", new Vector2(box.x * 0.20f, box.y * 0.16f),
            edge, o, new Vector3(box.x * 0.28f, -box.y * 0.06f, 0f), 12f);
        AddPlate(visual, "CoatFanDome", new Vector2(box.x * 0.18f, box.y * 0.16f),
            odd, o, new Vector3(-box.x * 0.09f, box.y * 0.32f, 0f), -8f);
        AddPlate(visual, "CoatFanDomeR", new Vector2(box.x * 0.16f, box.y * 0.14f),
            mid, o, new Vector3(box.x * 0.08f, box.y * 0.28f, 0f), 6f);
        // Plant 13:34: centre spray at hub origin + fan 6° was a
        // mast. Lance rides the left pad. Plant 13:49: laterals
        // 0/2 still stacked at that hub — fan stations off it.
        // Collider stays.
        AddPlate(visual, "CoatSpray", new Vector2(box.x * 0.20f, box.y * 0.16f),
            odd, o, new Vector3(-box.x * 0.28f, box.y * 0.08f, 0f), 6f);
        // Artist 18:52 leftover: y-spray Lance a paddle after
        // PetioleTip. Hang off the lance. Collider stays.
        AddPlate(visual, "CoatLanceTip", new Vector2(box.x * 0.10f, box.y * 0.08f),
            odd, o, new Vector3(-box.x * 0.38f, box.y * 0.16f, 0f), 12f);
        AddPlate(visual, "CoatSprayL", new Vector2(box.x * 0.16f, box.y * 0.14f),
            odd, o, new Vector3(-box.x * 0.28f, box.y * 0.04f, 0f), -18f);
        AddPlate(visual, "CoatSprayR", new Vector2(box.x * 0.14f, box.y * 0.12f),
            odd, o, new Vector3(box.x * 0.18f, box.y * 0.10f, 0f), 24f);
        AddPlate(visual, "Coat5", new Vector2(box.x * 0.24f, box.y * 0.26f),
            edge, o, new Vector3(box.x * 0.32f, -box.y * 0.34f, 0f), 12f);
        AddPlate(visual, "CoatFanR2", new Vector2(box.x * 0.28f, box.y * 0.24f),
            mid, o, new Vector3(box.x * 0.36f, box.y * 0.14f, 0f), 8f);
        AddPlate(visual, "Coat7", new Vector2(box.x * 0.22f, box.y * 0.18f),
            odd, o, new Vector3(-box.x * 0.38f, box.y * 0.12f, 0f), -11f);
        AddPlate(visual, "CoatFanM2", new Vector2(box.x * 0.16f, box.y * 0.18f),
            edge, o, new Vector3(box.x * 0.40f, box.y * 0.06f, 0f), 8f);
        AddPlate(visual, "CoatFringeL", new Vector2(box.x * 0.10f, box.y * 0.22f),
            edge, o, new Vector3(-box.x * 0.42f, -box.y * 0.36f, 0f), -10f);
        AddPlate(visual, "CoatFringeR", new Vector2(box.x * 0.08f, box.y * 0.20f),
            odd, o, new Vector3(box.x * 0.36f, -box.y * 0.34f, 0f), 8f);
        AddPlate(visual, "CoatFringeM", new Vector2(box.x * 0.08f, box.y * 0.16f),
            edge, o, new Vector3(-box.x * 0.36f, -box.y * 0.40f, 0f), 9f);
        AddPlate(visual, "CoatHemL", new Vector2(box.x * 0.10f, box.y * 0.20f),
            edge, o, new Vector3(-box.x * 0.48f, -box.y * 0.44f, 0f), -10f);
        AddPlate(visual, "CoatHemR", new Vector2(box.x * 0.08f, box.y * 0.18f),
            odd, o, new Vector3(box.x * 0.44f, -box.y * 0.22f, 0f), 8f);
        AddPlate(visual, "CoatWhorlL", new Vector2(box.x * 0.10f, box.y * 0.18f),
            edge, o, new Vector3(-box.x * 0.36f, -box.y * 0.28f, 0f), -10f);
        AddPlate(visual, "CoatWhorlR", new Vector2(box.x * 0.09f, box.y * 0.16f),
            odd, o, new Vector3(box.x * 0.32f, -box.y * 0.30f, 0f), 9f);
        AddPlate(visual, "CoatStoolLTip", new Vector2(box.x * 0.16f, box.y * 0.10f),
            odd, o, new Vector3(-box.x * 0.48f, -box.y * 0.38f, 0f), -18f);
        AddPlate(visual, "CoatStoolRTip", new Vector2(box.x * 0.14f, box.y * 0.09f),
            edge, o, new Vector3(box.x * 0.40f, -box.y * 0.40f, 0f), 16f);
        AddPlate(visual, "CoatSlopeLTip", new Vector2(box.x * 0.14f, box.y * 0.16f),
            edge, o, new Vector3(-box.x * 0.44f, -box.y * 0.12f, 0f), -16f);
        AddPlate(visual, "CoatSlopeRTip", new Vector2(box.x * 0.12f, box.y * 0.14f),
            odd, o, new Vector3(box.x * 0.40f, -box.y * 0.14f, 0f), 14f);
        // Artist 17:56 / plant 18:06 leftover: Y-long fans and fir
        // hem still coins/posts after SlopeTip. Distal lips. Collider stays.
        AddPlate(visual, "CoatFanLTip", new Vector2(box.x * 0.16f, box.y * 0.12f),
            edge, o, new Vector3(-box.x * 0.38f, -box.y * 0.46f, 0f), -20f);
        AddPlate(visual, "CoatFanL2Tip", new Vector2(box.x * 0.14f, box.y * 0.10f),
            odd, o, new Vector3(-box.x * 0.52f, -box.y * 0.24f, 0f), -14f);
        AddPlate(visual, "CoatFanRTip", new Vector2(box.x * 0.14f, box.y * 0.10f),
            edge, o, new Vector3(box.x * 0.28f, -box.y * 0.12f, 0f), 18f);
        AddPlate(visual, "CoatFanR2Tip", new Vector2(box.x * 0.16f, box.y * 0.10f),
            mid, o, new Vector3(box.x * 0.46f, box.y * 0.08f, 0f), 14f);
        AddPlate(visual, "CoatFanMTip", new Vector2(box.x * 0.14f, box.y * 0.10f),
            odd, o, new Vector3(-box.x * 0.48f, box.y * 0.06f, 0f), -16f);
        AddPlate(visual, "CoatFanM2Tip", new Vector2(box.x * 0.12f, box.y * 0.08f),
            edge, o, new Vector3(box.x * 0.50f, 0f, 0f), 14f);
        AddPlate(visual, "CoatLayerLTip", new Vector2(box.x * 0.14f, box.y * 0.12f),
            odd, o, new Vector3(-box.x * 0.38f, 0f, 0f), -18f);
        AddPlate(visual, "CoatLayerRTip", new Vector2(box.x * 0.12f, box.y * 0.10f),
            odd, o, new Vector3(box.x * 0.28f, box.y * 0.04f, 0f), 16f);
        AddPlate(visual, "CoatBaseLTip", new Vector2(box.x * 0.16f, box.y * 0.12f),
            edge, o, new Vector3(-box.x * 0.52f, -box.y * 0.38f, 0f), -16f);
        AddPlate(visual, "CoatBaseRTip", new Vector2(box.x * 0.14f, box.y * 0.10f),
            edge, o, new Vector3(box.x * 0.42f, -box.y * 0.42f, 0f), 14f);
        // Artist 05:46 CapTip peak kiss at x=0. Collider stays.
        AddPlate(visual, "CoatCapTip", new Vector2(box.x * 0.12f, box.y * 0.14f),
            odd, o, new Vector3(-box.x * 0.06f, box.y * 0.36f, 0f), -12f);
        AddPlate(visual, "CoatCapRTip", new Vector2(box.x * 0.10f, box.y * 0.12f),
            mid, o, new Vector3(box.x * 0.05f, box.y * 0.34f, 0f), 10f);
        AddPlate(visual, "CoatFringeLTip", new Vector2(box.x * 0.05f, box.y * 0.12f),
            edge, o, new Vector3(-box.x * 0.48f, -box.y * 0.44f, 0f), -16f);
        AddPlate(visual, "CoatFringeRTip", new Vector2(box.x * 0.04f, box.y * 0.10f),
            odd, o, new Vector3(box.x * 0.42f, -box.y * 0.26f, 0f), 14f);
        AddPlate(visual, "CoatHemLTip", new Vector2(box.x * 0.05f, box.y * 0.10f),
            edge, o, new Vector3(-box.x * 0.54f, -box.y * 0.56f, 0f), -16f);
        AddPlate(visual, "CoatHemRTip", new Vector2(box.x * 0.04f, box.y * 0.09f),
            odd, o, new Vector3(box.x * 0.50f, -box.y * 0.30f, 0f), 14f);
        AddPlate(visual, "CoatWhorlLTip", new Vector2(box.x * 0.06f, box.y * 0.08f),
            edge, o, new Vector3(-box.x * 0.42f, -box.y * 0.34f, 0f), -16f);
        AddPlate(visual, "CoatWhorlRTip", new Vector2(box.x * 0.05f, box.y * 0.07f),
            odd, o, new Vector3(box.x * 0.38f, -box.y * 0.36f, 0f), 14f);
        AddPlate(visual, "CoatFanBodyTip", new Vector2(box.x * 0.12f, box.y * 0.12f),
            edge, o, new Vector3(-box.x * 0.18f, -box.y * 0.20f, 0f), -16f);
        // Artist 05:46 FanDomeTip peak kiss; artist 05:55 FanDomeFill
        // under FanDomeTip. Collider stays.
        AddPlate(visual, "CoatFanDomeTip", new Vector2(box.x * 0.10f, box.y * 0.08f),
            odd, o, new Vector3(-box.x * 0.05f, box.y * 0.40f, 0f), -14f);
        AddPlate(visual, "CoatFanDomeRTip", new Vector2(box.x * 0.08f, box.y * 0.07f),
            mid, o, new Vector3(box.x * 0.04f, box.y * 0.38f, 0f), 12f);
        AddPlate(visual, "CoatFanDomeFill", new Vector2(box.x * 0.10f, box.y * 0.14f),
            odd, o, new Vector3(-box.x * 0.10f, box.y * 0.34f, 0f), -6f);
        AddPlate(visual, "CoatFanDomeFillTip", new Vector2(box.x * 0.06f, box.y * 0.07f),
            odd, o, new Vector3(-box.x * 0.12f, box.y * 0.38f, 0f), -12f);
        AddPlate(visual, "CoatFanDomeFillR", new Vector2(box.x * 0.10f, box.y * 0.12f),
            mid, o, new Vector3(box.x * 0.10f, box.y * 0.32f, 0f), 5f);
        AddPlate(visual, "CoatFanDomeFillRTip", new Vector2(box.x * 0.06f, box.y * 0.06f),
            mid, o, new Vector3(box.x * 0.11f, box.y * 0.36f, 0f), 11f);
        AddPlate(visual, "CoatDroopTip", new Vector2(box.x * 0.16f, box.y * 0.10f),
            edge, o, new Vector3(box.x * 0.40f, -box.y * 0.30f, 0f), 10f);
        AddPlate(visual, "CoatDroopLTip", new Vector2(box.x * 0.14f, box.y * 0.10f),
            odd, o, new Vector3(-box.x * 0.38f, -box.y * 0.36f, 0f), -14f);
        AddPlate(visual, "CoatFringeMTip", new Vector2(box.x * 0.04f, box.y * 0.08f),
            edge, o, new Vector3(-box.x * 0.42f, -box.y * 0.48f, 0f), 12f);
    }

    // Artist 08:48 winter bud: 0.018 ticks at ±18° were cards. Staggered
    // overlapping boxes at 48° plus a tip. sideSign +1 cranial, −1 caudal.
    static void AddPointedBud(Transform visual, Vector2 box, Color body, Color tip, int o, float sideSign)
    {
        // Artist 10:23: even belts still a brick ladder. Irregular Y
        // plus a hanging lip (BarkScaleTip). Collider stays.
        AddPlate(visual, "Coat0", new Vector2(box.x * 0.16f, box.y * 0.10f),
            body, o, new Vector3(-box.x * 0.10f, -box.y * 0.36f, 0f), -28f);
        // Artist 13:22: pine Sheath at x=0 was a mast under the
        // needles (crown Cap analog). Pad rides Needle0. Collider stays.
        // Plant 14:20: 0.42 at −0.12 still reached +0.09 — a bark
        // mast under the fascicle after Needle1 left the pad (Waist
        // analog). Cup rides Needle0. Collider stays.
        // Plant 15:41: 0.28 at −0.22 sat wholly left of 0, so
        // Needle1 piled on that cup and Needle2 never touched it.
        // Cup kisses x=0; left / right fan. Collider stays.
        AddPlate(visual, "CoatSheath", new Vector2(box.x * 0.24f, box.y * 0.22f),
            body, o, new Vector3(-box.x * 0.10f, -box.y * 0.38f, 0f), 4f);
        // Artist 18:16 leftover: Sheath still a bark card after
        // CarmBTip. Hang off the cup. Collider stays.
        AddPlate(visual, "CoatSheathTip", new Vector2(box.x * 0.12f, box.y * 0.10f),
            body, o, new Vector3(-box.x * 0.18f, -box.y * 0.46f, 0f), 8f);
        // Plant 14:06: winter spur Bud 0.04 sat ON the stick after
        // Tip left the blade (ovate Petiole analog). Peak rides the
        // left pad. Collider stays.
        AddPlate(visual, "CoatBud", new Vector2(box.x * 0.16f, box.y * 0.10f),
            OddLeaflet(body, tip), o, new Vector3(-box.x * 0.18f, box.y * 0.36f, 0f), 18f);
        // Artist 16:50 leftover: left-only Bud was a one-tent pine hat
        // (Apex without ApexR). Width 0.24 kisses x=0 at max-Y; ApexR
        // is the far peak. Collider stays.
        AddPlate(visual, "CoatApex", new Vector2(box.x * 0.24f, box.y * 0.16f),
            OddLeaflet(body, tip), o, new Vector3(-box.x * 0.12f, box.y * 0.28f, 0f), -8f);
        AddPlate(visual, "CoatApexR", new Vector2(box.x * 0.22f, box.y * 0.14f),
            OddLeaflet(body, tip), o, new Vector3(box.x * 0.11f, box.y * 0.26f, 0f), 6f);
        // Artist 18:37 leftover: Apex still a flame sausage after
        // BodyTip (Cap before CapTip). Hang off the peaks. Collider stays.
        // Artist 05:46 ApexTip peak kiss at x=0. Collider stays.
        AddPlate(visual, "CoatApexTip", new Vector2(box.x * 0.12f, box.y * 0.08f),
            OddLeaflet(body, tip), o, new Vector3(-box.x * 0.07f, box.y * 0.42f, 0f), -12f);
        AddPlate(visual, "CoatApexRTip", new Vector2(box.x * 0.10f, box.y * 0.07f),
            OddLeaflet(body, tip), o, new Vector3(box.x * 0.06f, box.y * 0.40f, 0f), 10f);
        // Artist 06:04: ApexFill under ApexTip (CapFill under CapTip).
        // Tip-hat Apex/ApexR kissed at mid-Y; no shoulder chip left a
        // one-tent after CapFill. Collider stays.
        AddPlate(visual, "CoatApexFill", new Vector2(box.x * 0.12f, box.y * 0.16f),
            OddLeaflet(body, tip), o, new Vector3(-box.x * 0.14f, box.y * 0.32f, 0f), -6f);
        AddPlate(visual, "CoatApexFillTip", new Vector2(box.x * 0.07f, box.y * 0.07f),
            OddLeaflet(body, tip), o, new Vector3(-box.x * 0.16f, box.y * 0.38f, 0f), -12f);
        AddPlate(visual, "CoatApexFillR", new Vector2(box.x * 0.12f, box.y * 0.14f),
            OddLeaflet(body, tip), o, new Vector3(box.x * 0.14f, box.y * 0.30f, 0f), 5f);
        AddPlate(visual, "CoatApexFillRTip", new Vector2(box.x * 0.07f, box.y * 0.06f),
            OddLeaflet(body, tip), o, new Vector3(box.x * 0.15f, box.y * 0.36f, 0f), 11f);
        // Artist 13:43: Needle1 at u=0 was a mast through the
        // fascicle after Sheath. Odd outer 0.16 on Sheath.
        // Collider stays.
        // Plant 14:20: k==1 at −0.12 × 0.28 still reached +0.02
        // (poplar Waist analog). Long inner is 0.18 on Sheath.
        // Collider stays.
        // Plant 14:40: 0.18 at -0.12 still reached -0.03 after Sheath
        // left for Needle0 (KiteR analog). Inner 0.16 on Sheath.
        // Collider stays.
        AddPlate(visual, "CoatNeedle1", new Vector2(box.x * 0.11f, box.y * 0.10f),
            OddLeaflet(body, tip), o, new Vector3(-box.x * 0.24f, -box.y * 0.38f, 0f), -8f);
        AddPlate(visual, "CoatNeedle2", new Vector2(box.x * 0.09f, box.y * 0.10f),
            OddLeaflet(body, tip), o, new Vector3(box.x * 0.12f, box.y * 0.02f, 0f), 10f);
        AddPlate(visual, "CoatTip", new Vector2(box.x * 0.08f, box.y * 0.042f),
            tip, o, new Vector3(-box.x * 0.22f, -box.y * 0.36f, 0f), -14f);
        // Plant 19:23 leftover: packed Tip a paddle after BladeTip
        // (Cap before CapTip). Hang off Tip. Collider stays.
        AddPlate(visual, "CoatTipTip", new Vector2(box.x * 0.05f, box.y * 0.028f),
            tip, o, new Vector3(-box.x * 0.28f, -box.y * 0.42f, 0f), -18f);
        // Artist 05:24 leftover: TipR 0.40 mid-curtain beside Tip sickle.
        // Far sickle lip matches Tip; TipRTip owns the peak. Collider stays.
        AddPlate(visual, "CoatTipR", new Vector2(box.x * 0.24f, box.y * 0.14f),
            OddLeaflet(body, tip), o, new Vector3(box.x * 0.22f, -box.y * 0.36f, 0f), 40f);
        AddPlate(visual, "CoatTipRTip", new Vector2(box.x * 0.14f, box.y * 0.10f),
            OddLeaflet(body, tip), o, new Vector3(box.x * 0.32f, -box.y * 0.44f, 0f), 48f);
        // Artist 17:35 leftover: packed spur skipped Tooth; needle
        // distal tick rides the fascicle (BarkScaleTip). Collider stays.
        AddPlate(visual, "CoatNeedleTip", new Vector2(box.x * 0.08f, box.y * 0.26f),
            OddLeaflet(body, tip), o, new Vector3(-box.x * 0.24f, box.y * 0.42f, 0f), -14f);
        AddPlate(visual, "Coat1", new Vector2(box.x * 0.14f, box.y * 0.14f),
            tip, o, new Vector3(box.x * 0.08f, -box.y * 0.08f, 0f), 22f);
        AddPlate(visual, "CoatTip1", new Vector2(box.x * 0.07f, box.y * 0.055f),
            body, o, new Vector3(box.x * 0.12f, -box.y * 0.20f, 0f), 36f);
        AddPlate(visual, "Coat4", new Vector2(box.x * 0.16f, box.y * 0.12f),
            body, o, new Vector3(sideSign * box.x * 0.30f, box.y * 0.12f, 0f),
            sideSign * -18f);
        AddPlate(visual, "CoatTip2", new Vector2(box.x * 0.08f, box.y * 0.05f),
            tip, o, new Vector3(sideSign * box.x * 0.34f, box.y * 0.02f, 0f),
            sideSign * -4f);
    }

    // Artist 10:47: two max-Y plates were a flag on the pole, forks
    // bare. Artist 12:33: Core at (0.02, −0.20) still sat in the bite
    // (apple Core-on-axis). Core/Dome ride LobeL; KiteL 0.34 + Waist
    // 0.28 on that kite; ToothR / FingerR / Reach stay outer. Collider stays.
    static void AddTwigSpray(Transform visual, Vector2 box, Color c, int o)
    {
        Color odd = OddLeaflet(c, GrooveOf(c));
        // Artist 12:50: Wrist (0, −0.40) sat IN the sinus from
        // below (crown Core analog). Pad rides LobeL0. Collider stays.
        AddPlate(visual, "CoatCore", new Vector2(box.x * 0.22f, box.y * 0.28f),
            c, o, new Vector3(-box.x * 0.22f, -box.y * 0.36f, 0f), 6f);
        // Artist 17:24 leftover: left-only Core was a one-tent hedge
        // stalk after DomeR (oak Core before CoreR). Far basal rides
        // LobeR; sinus stays empty. Collider stays.
        AddPlate(visual, "CoatCoreR", new Vector2(box.x * 0.18f, box.y * 0.22f),
            odd, o, new Vector3(box.x * 0.34f, -box.y * 0.28f, 0f), -6f);
        // Artist 18:37 leftover: orchard Core a blunt basal card after
        // WristTip (CarmB before CarmBTip). Hang off LobeL. Collider stays.
        AddPlate(visual, "CoatCoreTip", new Vector2(box.x * 0.10f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.28f, -box.y * 0.44f, 0f), 12f);
        // Artist 06:40: CoreR without a lip stayed a blunt far basal
        // after CoreTip (CarmB before CarmBTip). Hang off CoreR.
        // Collider stays.
        AddPlate(visual, "CoatCoreRTip", new Vector2(box.x * 0.08f, box.y * 0.10f),
            odd, o, new Vector3(box.x * 0.40f, -box.y * 0.36f, 0f), -12f);
        // Artist 06:40: CoreFill under CoreTip (CapFill under CapTip).
        // Hedge/orchard CoreTip kiss without a shoulder chip left a
        // one-tent sofa stalk after TwigDomeFill. Chip rides Core
        // under CoreTip; right edge toward CoreTip left. Collider stays.
        AddPlate(visual, "CoatCoreFill", new Vector2(box.x * 0.10f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.24f, -box.y * 0.40f, 0f), 6f);
        AddPlate(visual, "CoatCoreFillTip", new Vector2(box.x * 0.06f, box.y * 0.06f),
            c, o, new Vector3(-box.x * 0.26f, -box.y * 0.42f, 0f), 10f);
        AddPlate(visual, "CoatCoreFillR", new Vector2(box.x * 0.10f, box.y * 0.10f),
            odd, o, new Vector3(box.x * 0.30f, -box.y * 0.30f, 0f), -4f);
        AddPlate(visual, "CoatCoreFillRTip", new Vector2(box.x * 0.06f, box.y * 0.05f),
            odd, o, new Vector3(box.x * 0.36f, -box.y * 0.34f, 0f), -8f);
        AddPlate(visual, "CoatWristTip", new Vector2(box.x * 0.12f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.22f, -box.y * 0.48f, 0f), 10f);
        // Plant 14:06: plum Knot +0.10 sat IN the droplet sinus
        // after Core/Dome left LobeL (apple Scallop1 analog). Pad
        // rides LobeL. Collider stays.
        AddPlate(visual, "CoatKnot", new Vector2(box.x * 0.22f, box.y * 0.34f),
            c, o, new Vector3(-box.x * 0.22f, -box.y * 0.18f, 0f), -8f);
        // Artist 18:16 leftover: Knot without a distal chip stayed a
        // plum coin after ScallopTip. Hang off the pad. Collider stays.
        AddPlate(visual, "CoatKnotTip", new Vector2(box.x * 0.10f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.28f, -box.y * 0.28f, 0f), -12f);
        // Artist 12:33: two 0.42 kites were remex. KiteL 0.34 + Waist
        // 0.28 on that kite is a flame, not a card. Artist 16:32:
        // −0.34 right −0.17 left a 0.17 sky slit (leaflet analog).
        // Width 0.34 at −0.17 kisses x=0. Collider stays.
        AddPlate(visual, "CoatTwig0", new Vector2(box.x * 0.34f, box.y * 0.22f),
            c, o, new Vector3(-box.x * 0.17f, box.y * 0.02f, 0f), -34f);
        AddPlate(visual, "CoatKiteLTip", new Vector2(box.x * 0.18f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.28f, box.y * 0.12f, 0f), -40f);
        // Artist 16:50 leftover: Twig0 kissed x=0 with no far hem
        // (poplar KiteL2 without KiteR2). Width 0.30 at +0.15 kisses
        // x=0; Base rides Twig0, BaseR rides Twig0R. Collider stays.
        AddPlate(visual, "CoatTwig0R", new Vector2(box.x * 0.30f, box.y * 0.20f),
            odd, o, new Vector3(box.x * 0.15f, 0f, 0f), 14f);
        AddPlate(visual, "CoatKiteRTip", new Vector2(box.x * 0.16f, box.y * 0.10f),
            odd, o, new Vector3(box.x * 0.24f, box.y * 0.08f, 0f), 20f);
        // Plant 18:48 leftover: leaflet KiteL2 a blunt hem after
        // ApexTip (Cap before CapTip). Hang off Twig0.
        // Collider stays.
        AddPlate(visual, "CoatKiteL2", new Vector2(box.x * 0.36f, box.y * 0.20f),
            c, o, new Vector3(-box.x * 0.18f, -box.y * 0.16f, 0f), -18f);
        AddPlate(visual, "CoatKiteL2Tip", new Vector2(box.x * 0.18f, box.y * 0.10f),
            c, o, new Vector3(-box.x * 0.28f, -box.y * 0.24f, 0f), -24f);
        AddPlate(visual, "CoatKiteR2Tip", new Vector2(box.x * 0.16f, box.y * 0.09f),
            odd, o, new Vector3(box.x * 0.24f, -box.y * 0.20f, 0f), 16f);
        AddPlate(visual, "CoatBase", new Vector2(box.x * 0.28f, box.y * 0.16f),
            odd, o, new Vector3(-box.x * 0.16f, -box.y * 0.34f, 0f), -18f);
        AddPlate(visual, "CoatBaseR", new Vector2(box.x * 0.26f, box.y * 0.14f),
            odd, o, new Vector3(box.x * 0.15f, -box.y * 0.32f, 0f), 12f);
        // Plant 13:49: Calyx x=0 sat IN PetalL/R after PetalTip left
        // for PetalL (oak Wrist analog). Waist is that calyx. Pad
        // rides PetalL. Collider stays.
        // Plant 14:20: −0.12 × 0.28 still reached +0.02 and filled
        // the kite valley (Needle1 analog after Sheath left the pad).
        // Pad rides KiteL. Collider stays.
        AddPlate(visual, "CoatWaist", new Vector2(box.x * 0.28f, box.y * 0.24f),
            odd, o, new Vector3(-box.x * 0.22f, -box.y * 0.08f, 0f), 4f);
        // Artist 17:24 leftover: left-only Waist was a one-tent ovate
        // midrib after TipR/CheekR (leaflet Waist before WaistR). Far
        // chip rides CheekR; valley stays the kites. Collider stays.
        AddPlate(visual, "CoatWaistR", new Vector2(box.x * 0.24f, box.y * 0.20f),
            odd, o, new Vector3(box.x * 0.22f, -box.y * 0.06f, 0f), -4f);
        // Artist 18:52 leftover: Waist a midrib card after KiteL2Tip
        // (Cap before CapTip). Hang off Waist. Collider stays.
        AddPlate(visual, "CoatWaistTip", new Vector2(box.x * 0.14f, box.y * 0.10f),
            odd, o, new Vector3(-box.x * 0.32f, -box.y * 0.16f, 0f), 10f);
        AddPlate(visual, "CoatWaistRTip", new Vector2(box.x * 0.12f, box.y * 0.08f),
            odd, o, new Vector3(box.x * 0.32f, -box.y * 0.12f, 0f), -10f);
        // Plant 18:21 leftover: tree Calyx a bark card after SepalTip
        // (Sheath before SheathTip). Hang off PetalL. Collider stays.
        AddPlate(visual, "CoatCalyxTip", new Vector2(box.x * 0.16f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.24f, -box.y * 0.22f, 0f), 10f);
        // Artist 18:52 leftover: spur Petiole a stick after
        // FloretTip (CarmB before CarmBTip). Hang off Calyx.
        // Collider stays. Spur prefix so ear/comb CoatPetioleTip
        // is not a second child of the same Visual.
        AddPlate(visual, "CoatSpurPetioleTip", new Vector2(box.x * 0.10f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.28f, -box.y * 0.32f, 0f), 14f);
        AddPlate(visual, "CoatSpurPetioleRTip", new Vector2(box.x * 0.08f, box.y * 0.10f),
            odd, o, new Vector3(box.x * 0.22f, -box.y * 0.28f, 0f), -8f);
        // Artist 19:11 leftover: packed-tuft Petiole a brown card after
        // spur PetioleTip (CarmB before CarmBTip). Hang further along
        // the tuft. Collider stays.
        AddPlate(visual, "CoatPackedPetioleTip", new Vector2(box.x * 0.08f, box.y * 0.10f),
            c, o, new Vector3(-box.x * 0.34f, -box.y * 0.40f, 0f), 18f);
        AddPlate(visual, "CoatPackedPetioleRTip", new Vector2(box.x * 0.06f, box.y * 0.08f),
            odd, o, new Vector3(box.x * 0.28f, -box.y * 0.36f, 0f), -12f);
        // Artist 16:10: bush Blade/Tooth/Tip at −0.28 was a left
        // comma (plum ovate before CheekR). Leaflet KiteL/Waist keep
        // their valley; CheekR is the far egg half. Collider stays.
        AddPlate(visual, "CoatCheekR", new Vector2(box.x * 0.28f, box.y * 0.56f),
            odd, o, new Vector3(box.x * 0.14f, -box.y * 0.02f, 0f), 10f);
        // Artist 19:11 leftover: bush CheekR a hedge coin after
        // BaseTip (Drop before DropTip). Hang off the far egg.
        // Collider stays.
        AddPlate(visual, "CoatCheekRTip", new Vector2(box.x * 0.14f, box.y * 0.16f),
            odd, o, new Vector3(box.x * 0.22f, -box.y * 0.18f, 0f), 16f);
        // Plant 14:06: LeafletMid −0.16 still sat in the sinus
        // between laterals after Waist left that pad (plum Notch
        // analog). Mid/MidWaist ride Leaflet0 (−0.22). Collider stays.
        // Plant 14:20: ±0.22 × 0.48 overlapped at 0 (fruit cheek
        // analog). Laterals |x| 0.34 × 0.40; sinus stays empty.
        // Collider stays.
        // Plant 14:40: +/-0.34 x 0.40 still inner +/-0.14 (fruit cheek
        // analog). Laterals |x| 0.42 x 0.32; sinus stays empty.
        // Collider stays.
        AddPlate(visual, "CoatMid", new Vector2(box.x * 0.32f, box.y * 0.52f),
            odd, o, new Vector3(-box.x * 0.42f, box.y * 0.04f, 0f));
        AddPlate(visual, "CoatMidWaist", new Vector2(box.x * 0.28f, box.y * 0.32f),
            odd, o, new Vector3(-box.x * 0.42f, box.y * 0.02f, 0f));
        // Plant 18:06 leftover: Mid without a distal lip stayed a
        // trifoliate card after FrostTip (garden Blade before BladeTip).
        // Hang off Leaflet0; far tip rides Leaflet1. Collider stays.
        AddPlate(visual, "CoatLeafletTip", new Vector2(box.x * 0.18f, box.y * 0.22f),
            odd, o, new Vector3(-box.x * 0.42f, box.y * 0.22f, 0f));
        AddPlate(visual, "CoatLeafletMidTip", new Vector2(box.x * 0.16f, box.y * 0.22f),
            odd, o, new Vector3(-box.x * 0.42f, box.y * 0.28f, 0f));
        AddPlate(visual, "CoatLeafletMidRTip", new Vector2(box.x * 0.14f, box.y * 0.18f),
            odd, o, new Vector3(box.x * 0.42f, box.y * 0.26f, 0f));
        // Plant 18:21 leftover: berry RunnerLeaf a card after MidTip.
        // Distal chip. Collider stays.
        AddPlate(visual, "CoatRunnerLeafTip", new Vector2(box.x * 0.12f, box.y * 0.16f),
            odd, o, new Vector3(-box.x * 0.48f, box.y * 0.16f, 0f), -8f);
        // Plant 13:49: raceme florets 0.08…0.22 hugged the rachis
        // mast. Outer station 0.38. Collider stays.
        AddPlate(visual, "CoatTwig1", new Vector2(box.x * 0.16f, box.y * 0.14f),
            odd, o, new Vector3(box.x * 0.38f, box.y * 0.06f, 0f), 28f);
        // Plant 18:48 leftover: garden Floret a bed card after
        // CalyxTip. Hang off the floret. Collider stays.
        AddPlate(visual, "CoatFloretTip", new Vector2(box.x * 0.08f, box.y * 0.08f),
            odd, o, new Vector3(box.x * 0.46f, box.y * 0.12f, 0f), 34f);
        // Artist 18:52 leftover: lilac Raceme a panicle coin after
        // FloretTip. Hang off Twig1. Collider stays.
        AddPlate(visual, "CoatRacemeTip", new Vector2(box.x * 0.08f, box.y * 0.06f),
            odd, o, new Vector3(box.x * 0.50f, box.y * 0.02f, 0f), 22f);
        // Plant 14:06: hazel CatkinTail sat at the hub origin
        // (lilac Rachis analog). Stem rides the left pad; packets
        // stagger off it. Collider stays.
        AddPlate(visual, "CoatTail", new Vector2(box.x * 0.14f, box.y * 0.28f),
            c, o, new Vector3(-box.x * 0.14f, box.y * 0.06f, 0f), 8f);
        // Artist 18:52 leftover: lilac Rachis a green mast after
        // FloretTip; hazel Catkin packets posts. Hang off the
        // left pad. Collider stays.
        AddPlate(visual, "CoatRachisTip", new Vector2(box.x * 0.08f, box.y * 0.10f),
            c, o, new Vector3(-box.x * 0.14f, box.y * 0.22f, 0f), 12f);
        AddPlate(visual, "CoatCatkinTailTip", new Vector2(box.x * 0.08f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.14f, -box.y * 0.08f, 0f), 8f);
        AddPlate(visual, "CoatCatkinTip", new Vector2(box.x * 0.10f, box.y * 0.10f),
            odd, o, new Vector3(-box.x * 0.22f, box.y * 0.14f, 0f), -6f);
        AddPlate(visual, "CoatTwigL", new Vector2(box.x * 0.14f, box.y * 0.12f),
            odd, o, new Vector3(-box.x * 0.18f, box.y * 0.04f, 0f), -12f);
        // Plant 16:21 leftover: one left fork was a comma on the
        // leafless spray (bush Blade before CheekR). Fork rides
        // TwigF; ForkR is the far stick at +fork. Collider stays.
        AddPlate(visual, "CoatTwigFork", new Vector2(box.x * 0.12f, box.y * 0.28f),
            c, o, new Vector3(-box.x * 0.22f, box.y * 0.08f, 0f), -28f);
        AddPlate(visual, "CoatTwigForkR", new Vector2(box.x * 0.10f, box.y * 0.24f),
            odd, o, new Vector3(box.x * 0.22f, box.y * 0.06f, 0f), 28f);
        // Artist 17:56 leftover: fork sticks without a distal chip
        // stayed two posts on the leafless spray (Twig before TwigTip).
        // Hang off the outer end. Collider stays.
        AddPlate(visual, "CoatTwigForkTip", new Vector2(box.x * 0.07f, box.y * 0.10f),
            c, o, new Vector3(-box.x * 0.28f, box.y * 0.18f, 0f), -34f);
        AddPlate(visual, "CoatTwigForkRTip", new Vector2(box.x * 0.06f, box.y * 0.09f),
            odd, o, new Vector3(box.x * 0.28f, box.y * 0.16f, 0f), 34f);
        // Artist 12:50: Tip (0.04, 0.28) sat IN the sinus (crown
        // Dome analog). Peak rides LobeL. Flower Tip −0.22/0.28.
        // Collider stays.
        AddPlate(visual, "CoatDome", new Vector2(box.x * 0.20f, box.y * 0.24f),
            c, o, new Vector3(-box.x * 0.22f, box.y * 0.22f, 0f), -12f);
        // Artist 17:05 leftover: summer Dome stayed on LobeL after
        // SkirtR (oak/orchard). Far peak rides LobeR at |X|; do not
        // kiss x=0 — that fills the palmate/orchard bite. Collider stays.
        AddPlate(visual, "CoatDomeR", new Vector2(box.x * 0.18f, box.y * 0.22f),
            odd, o, new Vector3(box.x * 0.34f, box.y * 0.22f, 0f), 10f);
        // Artist 18:37 leftover: orchard Dome a blunt peak after
        // LobeL0Tip (Cap before CapTip). Hang off LobeL. Collider stays.
        AddPlate(visual, "CoatDomeTip", new Vector2(box.x * 0.12f, box.y * 0.10f),
            c, o, new Vector3(-box.x * 0.28f, box.y * 0.32f, 0f), -16f);
        AddPlate(visual, "CoatDomeRTip", new Vector2(box.x * 0.10f, box.y * 0.09f),
            odd, o, new Vector3(box.x * 0.40f, box.y * 0.30f, 0f), 14f);
        // Plant 06:09: DomeFill under DomeTip (CapFill under CapTip).
        // Hedge DomeTip kiss without a shoulder chip left a one-tent
        // peak after ApexFill. Chip rides Dome under DomeTip.
        // Collider stays.
        AddPlate(visual, "CoatDomeFill", new Vector2(box.x * 0.12f, box.y * 0.14f),
            c, o, new Vector3(-box.x * 0.24f, box.y * 0.28f, 0f), -10f);
        AddPlate(visual, "CoatDomeFillTip", new Vector2(box.x * 0.07f, box.y * 0.08f),
            c, o, new Vector3(-box.x * 0.28f, box.y * 0.34f, 0f), -16f);
        AddPlate(visual, "CoatDomeFillR", new Vector2(box.x * 0.12f, box.y * 0.12f),
            odd, o, new Vector3(box.x * 0.36f, box.y * 0.26f, 0f), 8f);
        AddPlate(visual, "CoatDomeFillRTip", new Vector2(box.x * 0.07f, box.y * 0.07f),
            odd, o, new Vector3(box.x * 0.40f, box.y * 0.32f, 0f), 14f);
        // Artist 16:50 leftover: left-only Skirt was a one-tent hem
        // under the palmate (oak Skirt without SkirtR). Far caudal
        // rides LobeR; sinus stays empty. Collider stays.
        AddPlate(visual, "CoatSkirt", new Vector2(box.x * 0.30f, box.y * 0.20f),
            odd, o, new Vector3(-box.x * 0.32f, -box.y * 0.24f, 0f), 16f);
        AddPlate(visual, "CoatSkirtR", new Vector2(box.x * 0.22f, box.y * 0.16f),
            odd, o, new Vector3(box.x * 0.34f, -box.y * 0.20f, 0f), -12f);
        // Artist 18:37 leftover: orchard Skirt blunt after oak palmate
        // LobeLTip. Distal hem. Collider stays.
        AddPlate(visual, "CoatSkirtTip", new Vector2(box.x * 0.16f, box.y * 0.10f),
            odd, o, new Vector3(-box.x * 0.40f, -box.y * 0.32f, 0f), 20f);
        // Plant 14:06: ovate Notch −0.14 sat on the blade spine
        // after Tip left for Waist. Peak rides Dome/Tip on LobeL.
        // Collider stays.
        AddPlate(visual, "CoatNotch", new Vector2(box.x * 0.22f, box.y * 0.28f),
            c, o, new Vector3(-box.x * 0.22f, box.y * 0.42f, 0f), -8f);
        // Artist 17:24 leftover: left-only Notch was a one-tent plum
        // after TipR/CheekR (oak LobeTip before LobeTipR). Far peak
        // rides CheekR; bite stays empty. Collider stays.
        AddPlate(visual, "CoatNotchR", new Vector2(box.x * 0.18f, box.y * 0.22f),
            odd, o, new Vector3(box.x * 0.22f, box.y * 0.38f, 0f), 8f);
        // Artist 18:37 leftover: ovate Notch a card after LobeL0Tip.
        // Hang off Dome. Collider stays.
        AddPlate(visual, "CoatNotchTip", new Vector2(box.x * 0.12f, box.y * 0.10f),
            c, o, new Vector3(-box.x * 0.28f, box.y * 0.52f, 0f), -12f);
        // Artist 18:52 leftover: NotchR a far stub after NotchTip.
        // Hang off CheekR. Collider stays.
        AddPlate(visual, "CoatNotchRTip", new Vector2(box.x * 0.10f, box.y * 0.08f),
            odd, o, new Vector3(box.x * 0.30f, box.y * 0.48f, 0f), 12f);
        // Plant 13:49: fruit Stem x=0 sat IN the CheekL/R bite.
        // Peak rides CheekL. Collider stays.
        // Plant 14:40: Stem -0.12 sat IN the CheekL/R bite after
        // cheeks left 0.56. Peak rides CheekL. Collider stays.
        AddPlate(visual, "CoatStem", new Vector2(box.x * 0.14f, box.y * 0.28f),
            c, o, new Vector3(-box.x * 0.28f, box.y * 0.42f, 0f), 8f);
        // Plant 18:06 leftover: sepal boxes without a distal lip stayed
        // green cards after FrostTip (petal before PetalTip). Hang off
        // the outer end. Collider stays.
        AddPlate(visual, "CoatSepalTip", new Vector2(box.x * 0.12f, box.y * 0.09f),
            c, o, new Vector3(-box.x * 0.28f, box.y * 0.54f, 0f), 8f);
        // Artist 18:52 leftover: fruit Stem/Sepal/Suture bark cards
        // after CalyxTip. Hang off CheekL. Collider stays.
        AddPlate(visual, "CoatStemTip", new Vector2(box.x * 0.08f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.28f, box.y * 0.58f, 0f), 12f);
        AddPlate(visual, "CoatSepalLTip", new Vector2(box.x * 0.10f, box.y * 0.08f),
            c, o, new Vector3(-box.x * 0.36f, box.y * 0.50f, 0f), -6f);
        AddPlate(visual, "CoatSepalRTip", new Vector2(box.x * 0.09f, box.y * 0.07f),
            odd, o, new Vector3(box.x * 0.22f, box.y * 0.48f, 0f), 8f);
        AddPlate(visual, "CoatSutureTip", new Vector2(box.x * 0.06f, box.y * 0.16f),
            c, o, new Vector3(-box.x * 0.10f, box.y * 0.36f, 0f), -8f);
        // Plant 14:06: cup 0° petal sat ON the bloom Y (lilac spray
        // centre analog). Peak rides the left pad; daisy ring stays
        // a circle. Collider stays.
        // Plant 14:40: PetalL 0.70 fused PetalR. Peak rides the left
        // petal pad; daisy ring stays a circle. Collider stays.
        // Plant 15:41: 0° Petal0 at −0.28 × 0.20 still reached −0.18
        // and cut the head (garden cup analog). Peak |x|>half.
        // Collider stays.
        AddPlate(visual, "CoatPetal0", new Vector2(box.x * 0.20f, box.y * 0.24f),
            odd, o, new Vector3(-box.x * 0.52f, box.y * 0.28f, 0f), 8f);
        // Artist 18:52 leftover: PetalL a cup coin after PetalTip
        // (Cap before CapTip). Hang off Petal0. Collider stays.
        AddPlate(visual, "CoatPetalLTip", new Vector2(box.x * 0.10f, box.y * 0.10f),
            odd, o, new Vector3(-box.x * 0.62f, box.y * 0.18f, 0f), -12f);
        // Plant 16:21 leftover: one 0° petal at −0.52 left the head
        // a comma (strawberry before PetalR). Far cup at +max |X|,
        // not a kiss at x=0 — that cut the head (15:41). Collider stays.
        AddPlate(visual, "CoatPetalR", new Vector2(box.x * 0.18f, box.y * 0.22f),
            odd, o, new Vector3(box.x * 0.52f, box.y * 0.26f, 0f), -8f);
        // Artist 18:52 leftover: far PetalR a cup coin. Hang off
        // PetalR. Collider stays.
        AddPlate(visual, "CoatPetalRTip", new Vector2(box.x * 0.09f, box.y * 0.09f),
            odd, o, new Vector3(box.x * 0.62f, box.y * 0.16f, 0f), 12f);
        // Plant 18:06 leftover: petal boxes without a distal lip stayed
        // cards after FrostTip (tree Petal before PetalTip). Hang off
        // the outer end. Collider stays.
        AddPlate(visual, "CoatPetalTip", new Vector2(box.x * 0.10f, box.y * 0.12f),
            odd, o, new Vector3(-box.x * 0.52f, box.y * 0.42f, 0f), 8f);
        AddPlate(visual, "CoatPetalTipR", new Vector2(box.x * 0.09f, box.y * 0.10f),
            odd, o, new Vector3(box.x * 0.52f, box.y * 0.38f, 0f), -8f);
        AddPlate(visual, "CoatTwigF", new Vector2(box.x * 0.22f, box.y * 0.18f),
            c, o, new Vector3(-box.x * 0.48f, box.y * 0.04f, 0f), -20f);
        // Plant 16:21 leftover: left-only F was a one-tent winter
        // oak (FanDome without FanDomeR). Far stick at +max |X|.
        // Collider stays.
        AddPlate(visual, "CoatTwigFR", new Vector2(box.x * 0.16f, box.y * 0.20f),
            odd, o, new Vector3(box.x * 0.48f, box.y * 0.02f, 0f), 20f);
        // Artist 17:56 leftover: bare sticks without a distal chip
        // stayed a comb of posts (bole Furrow before CorkTip). Hang
        // off the outer end. Collider stays.
        AddPlate(visual, "CoatTwigTip", new Vector2(box.x * 0.14f, box.y * 0.04f),
            c, o, new Vector3(-box.x * 0.54f, box.y * 0.12f, 0f), -14f);
        AddPlate(visual, "CoatTwigTipR", new Vector2(box.x * 0.10f, box.y * 0.04f),
            odd, o, new Vector3(box.x * 0.54f, box.y * 0.10f, 0f), 14f);
        // Artist 12:50: TwigDome (−0.02, 0.36) was a flag on the
        // leafless leader. Peak rides TwigF at max |X|. Collider stays.
        AddPlate(visual, "CoatTwigDome", new Vector2(box.x * 0.16f, box.y * 0.14f),
            c, o, new Vector3(-box.x * 0.48f, box.y * 0.28f, 0f), -20f);
        AddPlate(visual, "CoatTwigDomeR", new Vector2(box.x * 0.14f, box.y * 0.12f),
            odd, o, new Vector3(box.x * 0.48f, box.y * 0.26f, 0f), 20f);
        // Plant 06:33: TwigDomeTip + TwigDomeFill under it (DomeFill
        // under DomeTip). Winter TwigDome without Tip/Fill left a
        // one-tent leafless peak after DomeFill. Collider stays.
        AddPlate(visual, "CoatTwigDomeTip", new Vector2(box.x * 0.10f, box.y * 0.08f),
            c, o, new Vector3(-box.x * 0.52f, box.y * 0.34f, 0f), -22f);
        AddPlate(visual, "CoatTwigDomeRTip", new Vector2(box.x * 0.09f, box.y * 0.07f),
            odd, o, new Vector3(box.x * 0.52f, box.y * 0.32f, 0f), 22f);
        AddPlate(visual, "CoatTwigDomeFill", new Vector2(box.x * 0.10f, box.y * 0.08f),
            c, o, new Vector3(-box.x * 0.44f, box.y * 0.30f, 0f), -14f);
        AddPlate(visual, "CoatTwigDomeFillTip", new Vector2(box.x * 0.06f, box.y * 0.05f),
            c, o, new Vector3(-box.x * 0.50f, box.y * 0.34f, 0f), -18f);
        AddPlate(visual, "CoatTwigDomeFillR", new Vector2(box.x * 0.09f, box.y * 0.07f),
            odd, o, new Vector3(box.x * 0.44f, box.y * 0.28f, 0f), 14f);
        AddPlate(visual, "CoatTwigDomeFillRTip", new Vector2(box.x * 0.05f, box.y * 0.04f),
            odd, o, new Vector3(box.x * 0.50f, box.y * 0.32f, 0f), 18f);
        // Artist 16:32 leftover: left-only Tuft2 was a one-tent
        // coppice (winter Dome without DomeR). Far peak rides the
        // right pad; sinus stays empty. Collider stays.
        // Artist 16:50 leftover: palettes had Tuft2R with no left
        // pad (hazel Tuft2 at −0.36). Peak sits above the left fork;
        // Tuft2R stays the far peak. Sinus stays empty. Collider stays.
        AddPlate(visual, "CoatTuft2", new Vector2(box.x * 0.22f, box.y * 0.28f),
            c, o, new Vector3(-box.x * 0.36f, box.y * 0.32f, 0f), -8f);
        AddPlate(visual, "CoatTuft2R", new Vector2(box.x * 0.18f, box.y * 0.24f),
            odd, o, new Vector3(box.x * 0.32f, box.y * 0.28f, 0f), 10f);
        // Plant 18:48 leftover: hedge TuftL a blunt mound after
        // CoreTip (Cap before CapTip). Hang off the left disc.
        // Collider stays.
        AddPlate(visual, "CoatTuftLTip", new Vector2(box.x * 0.12f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.48f, box.y * 0.40f, 0f), -14f);
        AddPlate(visual, "CoatTuftRTip", new Vector2(box.x * 0.10f, box.y * 0.10f),
            odd, o, new Vector3(box.x * 0.42f, box.y * 0.36f, 0f), 16f);
        // Plant 18:21 leftover: garden Spent a thigh card after PetalTip
        // (Drop before DropTip). Hang off the left pad. Collider stays.
        // Plant 05:40: hip 0.74 russet (summer leaf still orange at
        // ortho 5). Collider stays.
        // Plant 05:51: hip 0.78 russet after CapFill bark punch.
        // Artist 06:04: deepen after ApexFill bark punch (oak spent).
        // Plant 06:33: deepen after TwigDomeFill bark punch (oak spent).
        // Artist 06:40: deepen after CoreFill bark punch (oak spent).
        Color spent = Color.Lerp(c, new Color(0.50f, 0.14f, 0.02f, c.a), 0.92f);
        AddPlate(visual, "CoatSpentTip", new Vector2(box.x * 0.12f, box.y * 0.10f),
            spent, o, new Vector3(-box.x * 0.42f, box.y * 0.22f, 0f), -12f);
        AddPlate(visual, "CoatLobeL1", new Vector2(box.x * 0.16f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.64f, box.y * 0.16f, 0f), -48f);
        // Artist 18:16 leftover: LobeL1 still blunt after packed Tooth.
        // Distal chip past the outer pad. Collider stays.
        AddPlate(visual, "CoatLobeL1Tip", new Vector2(box.x * 0.10f, box.y * 0.08f),
            c, o, new Vector3(-box.x * 0.72f, box.y * 0.20f, 0f), -52f);
        // Artist 18:37 leftover: crown LobeL blunt after leaflet
        // LobeL0Tip. Hang off LobeL. Collider stays.
        AddPlate(visual, "CoatLobeLTip", new Vector2(box.x * 0.16f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.58f, box.y * 0.14f, 0f), -40f);
        // Plant 18:21 leftover: inner LobeL0/LobeR0 blunt after
        // LobeL1Tip (Cap before CapTip). Hang off the pads. Collider stays.
        AddPlate(visual, "CoatLobeL0", new Vector2(box.x * 0.42f, box.y * 0.26f),
            c, o, new Vector3(-box.x * 0.32f, box.y * 0.02f, 0f), -44f);
        AddPlate(visual, "CoatLobeL0Tip", new Vector2(box.x * 0.22f, box.y * 0.14f),
            c, o, new Vector3(-box.x * 0.48f, box.y * 0.08f, 0f), -52f);
        AddPlate(visual, "CoatLobeR0", new Vector2(box.x * 0.28f, box.y * 0.20f),
            odd, o, new Vector3(box.x * 0.34f, box.y * 0.10f, 0f), 32f);
        AddPlate(visual, "CoatLobeR0Tip", new Vector2(box.x * 0.16f, box.y * 0.12f),
            odd, o, new Vector3(box.x * 0.48f, box.y * 0.16f, 0f), 38f);
        // Artist 14:12: palmate stopped at LobeL1 — a mitten on the
        // left (FingerR stub analog). Outer tooth on LobeL0; sinus
        // stays empty. Collider stays.
        AddPlate(visual, "CoatFingerL", new Vector2(box.x * 0.16f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.52f, box.y * 0.18f, 0f), -42f);
        AddPlate(visual, "CoatFingerLTip", new Vector2(box.x * 0.10f, box.y * 0.08f),
            c, o, new Vector3(-box.x * 0.60f, box.y * 0.24f, 0f), -48f);
        // Artist 17:35 leftover: palmate stopped at FingerL — a mitten
        // on the left (crown ToothR without ToothL). Inner tooth on
        // LobeL0; sinus stays empty. Collider stays.
        AddPlate(visual, "CoatToothL", new Vector2(box.x * 0.14f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.44f, box.y * 0.14f, 0f), -36f);
        AddPlate(visual, "CoatToothLTip", new Vector2(box.x * 0.08f, box.y * 0.08f),
            c, o, new Vector3(-box.x * 0.52f, box.y * 0.18f, 0f), -42f);
        // Artist 17:05 leftover: oak LobeTip still left-only after
        // LobeL2/R2 (crown Dome before DomeR). Far peak rides LobeR
        // at |X|; sinus stays empty. Collider stays.
        AddPlate(visual, "CoatLobeTipR", new Vector2(box.x * 0.16f, box.y * 0.18f),
            odd, o, new Vector3(box.x * 0.34f, box.y * 0.16f, 0f), 14f);
        // Artist 16:50 leftover: leaflet stopped at Wrist — a one-tent
        // hem under the palmate (crown KiteL2 analog). LobeL2 rides
        // LobeL1; LobeR2 rides ToothR. Sinus stays empty. Collider stays.
        AddPlate(visual, "CoatLobeL2", new Vector2(box.x * 0.32f, box.y * 0.18f),
            c, o, new Vector3(-box.x * 0.32f, -box.y * 0.14f, 0f), -18f);
        AddPlate(visual, "CoatLobeL2Tip", new Vector2(box.x * 0.16f, box.y * 0.10f),
            c, o, new Vector3(-box.x * 0.42f, -box.y * 0.20f, 0f), -24f);
        AddPlate(visual, "CoatLobeR2", new Vector2(box.x * 0.24f, box.y * 0.16f),
            odd, o, new Vector3(box.x * 0.34f, -box.y * 0.06f, 0f), 14f);
        AddPlate(visual, "CoatLobeR2Tip", new Vector2(box.x * 0.12f, box.y * 0.09f),
            odd, o, new Vector3(box.x * 0.44f, -box.y * 0.12f, 0f), 20f);
        // Artist 17:24 leftover: packed palmate skipped caudal after
        // TipR (full leaf Wrist before WristR). Far join rides LobeR;
        // sinus stays empty. Collider stays.
        AddPlate(visual, "CoatWristR", new Vector2(box.x * 0.18f, box.y * 0.22f),
            odd, o, new Vector3(box.x * 0.34f, -box.y * 0.28f, 0f), -6f);
        // Artist 19:11 leftover: oak/packed WristR a far petiole card
        // after BaseTip (CarmB before CarmBTip). Hang off LobeR2.
        // Collider stays.
        AddPlate(visual, "CoatWristRTip", new Vector2(box.x * 0.10f, box.y * 0.10f),
            odd, o, new Vector3(box.x * 0.34f, -box.y * 0.38f, 0f), -10f);
        // Artist 11:59: crown ToothR 0.50 / FingerR 0.68 / Reach 0.76.
        // Palettes still had ToothR 0.56 and Reach −0.58 in the sinus.
        AddPlate(visual, "CoatToothR", new Vector2(box.x * 0.16f, box.y * 0.12f),
            odd, o, new Vector3(box.x * 0.50f, box.y * 0.08f, 0f), 36f);
        AddPlate(visual, "CoatToothRTip", new Vector2(box.x * 0.08f, box.y * 0.08f),
            odd, o, new Vector3(box.x * 0.58f, box.y * 0.12f, 0f), 42f);
        AddPlate(visual, "CoatFingerR", new Vector2(box.x * 0.14f, box.y * 0.12f),
            c, o, new Vector3(box.x * 0.68f, box.y * 0.30f, 0f), 46f);
        AddPlate(visual, "CoatFingerRTip", new Vector2(box.x * 0.08f, box.y * 0.08f),
            c, o, new Vector3(box.x * 0.76f, box.y * 0.36f, 0f), 52f);
        // Artist 17:35 leftover: palmate stopped at FingerR — a mitten
        // on the right (LobeL1 without LobeR1). Outer pad past ToothR;
        // sinus stays empty. Packed spur ToothL/R sit on WristR.
        // Collider stays.
        AddPlate(visual, "CoatLobeR1", new Vector2(box.x * 0.16f, box.y * 0.12f),
            odd, o, new Vector3(box.x * 0.80f, box.y * 0.22f, 0f), 42f);
        AddPlate(visual, "CoatPackedToothL", new Vector2(box.x * 0.22f, box.y * 0.22f),
            c, o, new Vector3(-box.x * 0.40f, box.y * 0.12f, 0f), -32f);
        AddPlate(visual, "CoatPackedToothLTip", new Vector2(box.x * 0.12f, box.y * 0.12f),
            c, o, new Vector3(-box.x * 0.48f, box.y * 0.18f, 0f), -38f);
        AddPlate(visual, "CoatPackedToothR", new Vector2(box.x * 0.20f, box.y * 0.20f),
            odd, o, new Vector3(box.x * 0.38f, box.y * 0.10f, 0f), 28f);
        AddPlate(visual, "CoatPackedToothRTip", new Vector2(box.x * 0.10f, box.y * 0.10f),
            odd, o, new Vector3(box.x * 0.46f, box.y * 0.16f, 0f), 34f);
        AddPlate(visual, "CoatPackedFingerL", new Vector2(box.x * 0.16f, box.y * 0.14f),
            c, o, new Vector3(-box.x * 0.46f, box.y * 0.16f, 0f), -36f);
        AddPlate(visual, "CoatPackedFingerLTip", new Vector2(box.x * 0.10f, box.y * 0.08f),
            c, o, new Vector3(-box.x * 0.54f, box.y * 0.22f, 0f), -42f);
        AddPlate(visual, "CoatPackedFingerR", new Vector2(box.x * 0.14f, box.y * 0.12f),
            odd, o, new Vector3(box.x * 0.44f, box.y * 0.14f, 0f), 32f);
        AddPlate(visual, "CoatPackedFingerRTip", new Vector2(box.x * 0.08f, box.y * 0.08f),
            odd, o, new Vector3(box.x * 0.52f, box.y * 0.20f, 0f), 38f);
        AddPlate(visual, "CoatPackedWristTip", new Vector2(box.x * 0.12f, box.y * 0.10f),
            c, o, new Vector3(-box.x * 0.26f, -box.y * 0.40f, 0f), 10f);
        AddPlate(visual, "CoatPackedWristRTip", new Vector2(box.x * 0.10f, box.y * 0.10f),
            odd, o, new Vector3(box.x * 0.24f, -box.y * 0.34f, 0f), -10f);
        AddPlate(visual, "CoatPackedLobe0Tip", new Vector2(box.x * 0.14f, box.y * 0.10f),
            c, o, new Vector3(-box.x * 0.36f, box.y * 0.06f, 0f), -28f);
        // Plant 18:06 leftover: packed palmate stopped at ToothL after
        // LobeR1 closed the right — a mitten on the left. Outer pad
        // past ToothL; sinus stays empty. Collider stays.
        AddPlate(visual, "CoatPackedLobeL1", new Vector2(box.x * 0.18f, box.y * 0.16f),
            c, o, new Vector3(-box.x * 0.52f, box.y * 0.10f, 0f), -38f);
        AddPlate(visual, "CoatReach", new Vector2(box.x * 0.20f, box.y * 0.16f),
            odd, o, new Vector3(box.x * 0.76f, box.y * 0.04f, 0f), 24f);
        AddPlate(visual, "CoatReachTip", new Vector2(box.x * 0.12f, box.y * 0.10f),
            odd, o, new Vector3(box.x * 0.88f, box.y * 0.06f, 0f), 30f);
    }

    // Artist 11:09: hang frame +Y is world down, so extras at −Y
    // climbed the wood. Visual here is not hang-rotated: −Y is dirt.
    // Artist 11:59: 0.10×0.48 rib was a 4 mm tick / Blade 0.22 of
    // 8×18 cm a 1.8 cm tick. C-sickle fills the bone. Collider stays.
    static void AddWillowHang(
        Transform visual, Vector2 box, Color c, int o, int slot,
        float px, float py, float tilt)
    {
        Vector3 origin = new Vector3(box.x * px, box.y * py, 0f);
        // Artist 14:49: rotSide followed tilt, so a left hang stepped
        // +X and a right hang -X — Arc/Drip walked INTO the mast
        // (Hang2 analog). Step away from the bone axis. -Y is dirt.
        float rotSide = tilt >= 0f ? 1f : -1f;
        float outX = origin.x >= 0f ? 1f : -1f;
        float stepX = Mathf.Max(box.x * 0.42f, 0.028f);
        float drop = Mathf.Max(box.y * 0.28f, 0.032f);
        string ribName = slot == 0 ? "Coat0" : "CoatHang" + slot;
        // Artist 19:32 leftover: Hang a curtain paddle after HangTip
        // (Droop before DroopTip). Short C at origin; tip hangs off
        // the C. Artist 05:35: mid Hang5/6 pad 0.72 kisses at x=0;
        // outer slots keep tall C 0.28. Collider stays.
        bool midKiss = slot == 5 || slot == 6;
        float hangPad = midKiss ? 0.72f : 0.28f;
        float hangTipPad = midKiss ? 0.36f : 0.14f;
        float tipOff = midKiss ? 0.40f : 0.22f;
        AddPlate(visual, ribName,
            new Vector2(box.x * hangPad, box.y * 0.30f),
            c, o, origin, tilt);
        AddPlate(visual, "CoatHangTip" + slot,
            new Vector2(box.x * hangTipPad, box.y * (midKiss ? 0.14f : 0.12f)),
            c, o, origin + new Vector3(outX * box.x * tipOff, -drop * 0.18f, 0f),
            tilt + rotSide * 12f);
        AddPlate(visual, "CoatArc" + slot,
            new Vector2(box.x * 0.52f, box.y * 0.36f),
            c, o, origin + new Vector3(outX * stepX, -drop, 0f),
            tilt + rotSide * 12f);
        AddPlate(visual, "CoatArcTip" + slot,
            new Vector2(box.x * 0.26f, box.y * 0.14f),
            c, o, origin + new Vector3(outX * stepX * 1.28f, -drop * 1.22f, 0f),
            tilt + rotSide * 16f);
        AddPlate(visual, "CoatDrip" + slot,
            new Vector2(box.x * 0.36f, box.y * 0.40f),
            c, o, origin + new Vector3(outX * stepX * 1.55f, -drop * 1.55f, 0f),
            tilt + rotSide * 18f);
        // Artist 18:16 leftover: willow Drip still a paddle after
        // LobeL1Tip. Distal chip. Collider stays.
        AddPlate(visual, "CoatDripTip" + slot,
            new Vector2(box.x * 0.18f, box.y * 0.16f),
            c, o, origin + new Vector3(outX * stepX * 1.70f, -drop * 1.72f, 0f),
            tilt + rotSide * 20f);
        // Artist 17:24 leftover: Blade/Tip/Drip all at +stepX was a
        // hanging comma (bush Blade before CheekR). BladeR kisses the
        // join from the right; HangTipR/DripR are distal lips
        // (CorkTip analog). Collider stays.
        Color hangOdd = OddLeaflet(c, GrooveOf(c));
        AddPlate(visual, "CoatBladeR" + slot,
            new Vector2(box.x * 0.28f, box.y * 0.36f),
            hangOdd, o, origin + new Vector3(outX * stepX * 0.38f, -drop * 0.10f, 0f),
            tilt + rotSide * 4f);
        // Artist 18:52 leftover: BladeR a far stub after BladeTip.
        // Hang off the far join. Collider stays.
        AddPlate(visual, "CoatBladeRTip" + slot,
            new Vector2(box.x * 0.14f, box.y * 0.16f),
            hangOdd, o, origin + new Vector3(outX * stepX * 0.22f, drop * 0.06f, 0f),
            tilt + rotSide * 2f);
        // Artist 05:24 leftover: HangTipR 0.40×0.28 rebuilt the
        // mid-curtain. Far sickle lip; TipRTip owns the peak.
        // Collider stays.
        // Artist 05:46: Tip 0.26 mid-lance -> distal sickle 0.22 at
        // 1.18; TipTip owns the peak. Collider stays.
        AddPlate(visual, "CoatHangTipR" + slot,
            new Vector2(box.x * 0.22f, box.y * 0.14f),
            hangOdd, o, origin + new Vector3(outX * stepX * 1.18f, -drop * 1.12f, 0f),
            tilt + rotSide * 16f);
        AddPlate(visual, "CoatTipRTip" + slot,
            new Vector2(box.x * 0.14f, box.y * 0.10f),
            hangOdd, o, origin + new Vector3(outX * stepX * 1.42f, -drop * 1.34f, 0f),
            tilt + rotSide * 20f);
        AddPlate(visual, "CoatDripR" + slot,
            new Vector2(box.x * 0.28f, box.y * 0.32f),
            hangOdd, o, origin + new Vector3(outX * stepX * 1.82f, -drop * 1.78f, 0f),
            tilt + rotSide * 22f);
        // Plant 18:06 leftover: Blade without a distal lip stayed a
        // garden card after FrostTip (packed Tooth before LobeL1).
        // Hang off the rib. Collider stays.
        AddPlate(visual, "CoatBladeTip" + slot,
            new Vector2(box.x * 0.14f, box.y * 0.12f),
            hangOdd, o, origin + new Vector3(outX * stepX * 0.78f, -drop * 0.44f, 0f),
            tilt + rotSide * 12f);
    }

    // Artist 06:40 CoreFill bark punch: 0.08/2.08 soft after
    // TwigDomeFill — punch harder so a furrow is a crack beside the
    // sofa-stalk chip. Collider stays.
    static Color GrooveOf(Color c)
    {
        return new Color(c.r * 0.06f, c.g * 0.045f, c.b * 0.038f, c.a);
    }

    static Color RidgeOf(Color c)
    {
        return new Color(Mathf.Min(1f, c.r * 2.16f), Mathf.Min(1f, c.g * 1.93f),
            Mathf.Min(1f, c.b * 1.65f), c.a);
    }

    // Edge slope of the picture cone, degrees. Keel plates follow this
    // so a still frame is a taper, not a barcode of vertical strips.
    static float ConeTilt(Vector2 box, bool xLong)
    {
        if (xLong)
        {
            float halfDrop = box.y * 0.09f;
            if (halfDrop < 0.002f || box.x < 0.004f)
                return 0f;
            return Mathf.Atan2(halfDrop, box.x) * Mathf.Rad2Deg;
        }
        float drop = box.x * 0.23f;
        if (drop < 0.002f || box.y < 0.004f)
            return 0f;
        return Mathf.Atan2(drop, box.y) * Mathf.Rad2Deg;
    }

    static void HideSlab(SpriteRenderer sr)
    {
        if (sr == null)
            return;
        Color c = sr.color;
        c.a = 0f;
        sr.color = c;
    }

    static Vector2 BoxOf(Transform visual)
    {
        Transform bone = visual.parent;
        if (bone != null)
        {
            HumanSegment seg = bone.GetComponent<HumanSegment>();
            if (seg != null && seg.size.x > 0.001f && seg.size.y > 0.001f)
                return seg.size;
        }
        SpriteRenderer sr = visual.GetComponent<SpriteRenderer>();
        if (sr != null && sr.drawMode == SpriteDrawMode.Sliced && sr.size.x > 0.001f)
            return sr.size;
        if (sr != null && sr.sprite != null)
            return sr.sprite.bounds.size;
        return new Vector2(0.12f, 0.10f);
    }

    static bool HasCoat(Transform visual)
    {
        for (int i = 0; i < visual.childCount; i++)
        {
            if (visual.GetChild(i).name == "Coat0" || visual.GetChild(i).name == "CoatFill")
                return true;
        }
        return false;
    }

    static void AddPlate(Transform visual, string name, Vector2 size, Color color, int order, Vector3 localPos, float zRot = 0f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(visual, false);
        go.transform.localPosition = localPos;
        if (zRot != 0f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, zRot);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.color = color;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.sortingOrder = order;
        SpriteLighting.ApplyLit(sr);
    }
}
