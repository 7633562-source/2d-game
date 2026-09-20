using UnityEngine;

// Small plant, not a tree. Picture only: no Rigidbody2D, no collider.
// Sit contact stays on PlantTree BirdPerch pads. No muscles, no wind.
public enum FlowerKind
{
    Rose = 0,
    Tulip = 1,
    Daisy = 2,
    Poppy = 3,
    Sunflower = 4,
    Lily = 5,
    Daffodil = 6,
    Violet = 7,
    Crocus = 8,
    Lavender = 9,
    Iris = 10,
    Bluebell = 11
}

public class PlantFlower : MonoBehaviour
{
    [Header("Kind")]
    [Tooltip("Locked by a subclass (FlowerRose…). Auto = seed % 12.")]
    public FlowerKind kind = FlowerKind.Rose;
    public int seed = 1;

    public PlantSeason season = PlantSeason.Summer;
    public PlantSeason ResolvedSeason { get; private set; }

    public FlowerKind ResolvedKind { get; private set; }
    public int PartCount { get; private set; }
    public float StemHeight { get; private set; }

    private float stemHeight = 0.22f;
    private float stemWidth = 0.018f;
    private Color stemColor = new Color(0.22f, 0.46f, 0.20f, 1f);
    private int leafCount = 2;
    private Vector2 leafSize = new Vector2(0.06f, 0.035f);
    private Color leafColor = new Color(0.24f, 0.50f, 0.22f, 1f);
    private Vector2 bloomSize = new Vector2(0.08f, 0.07f);
    private Color bloomColor = new Color(0.78f, 0.16f, 0.18f, 1f);
    private Vector2 diskSize = Vector2.zero;
    private Color diskColor = Color.yellow;
    private int spikeCount;

    public static float RootY(float groundY = -2f)
    {
        return groundY;
    }

    public static FlowerKind KindFromIndex(int index)
    {
        const int n = 12;
        int k = index % n;
        if (k < 0) k += n;
        return (FlowerKind)k;
    }

    public static FlowerKind ParseKind(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return FlowerKind.Rose;
        switch (raw.Trim().ToLowerInvariant())
        {
            case "tulip": return FlowerKind.Tulip;
            case "daisy": return FlowerKind.Daisy;
            case "poppy": return FlowerKind.Poppy;
            case "sunflower": return FlowerKind.Sunflower;
            case "lily": return FlowerKind.Lily;
            case "daffodil": return FlowerKind.Daffodil;
            case "violet": return FlowerKind.Violet;
            case "crocus": return FlowerKind.Crocus;
            case "lavender": return FlowerKind.Lavender;
            case "iris": return FlowerKind.Iris;
            case "bluebell": return FlowerKind.Bluebell;
            default: return FlowerKind.Rose;
        }
    }

    public static System.Type ComponentType(FlowerKind recipe)
    {
        switch (recipe)
        {
            case FlowerKind.Tulip: return typeof(FlowerTulip);
            case FlowerKind.Daisy: return typeof(FlowerDaisy);
            case FlowerKind.Poppy: return typeof(FlowerPoppy);
            case FlowerKind.Sunflower: return typeof(FlowerSunflower);
            case FlowerKind.Lily: return typeof(FlowerLily);
            case FlowerKind.Daffodil: return typeof(FlowerDaffodil);
            case FlowerKind.Violet: return typeof(FlowerViolet);
            case FlowerKind.Crocus: return typeof(FlowerCrocus);
            case FlowerKind.Lavender: return typeof(FlowerLavender);
            case FlowerKind.Iris: return typeof(FlowerIris);
            case FlowerKind.Bluebell: return typeof(FlowerBluebell);
            default: return typeof(FlowerRose);
        }
    }

    public void BuildFlower()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        if (HeadlessTrial.Active)
        {
            string seasonArg = PlantTree.ReadPlantArg("-flowerSeason");
            if (seasonArg.Length == 0)
                seasonArg = PlantTree.ReadPlantArg("-treeSeason");
            if (seasonArg.Length > 0)
                season = PlantTree.ParseSeason(seasonArg);
        }

        ResolvedKind = kind;
        if ((int)ResolvedKind < 0 || (int)ResolvedKind > 11)
            ResolvedKind = FlowerKind.Rose;
        ApplyKind(ResolvedKind);
        ApplySeasonTint();
        StemHeight = stemHeight;
        PartCount = 0;

        // Picture lean about the root. Not a hinge. Summer PartCount 62.
        float stemLean = ((Mathf.Abs(seed) % 7) - 3) * 3.5f;
        transform.localRotation = Quaternion.Euler(0f, 0f, stemLean);
        // Head and leaflets peel the cane lean (phototropic). Side
        // pose 32° stays. Not a hinge.
        float headRest = -stemLean * 0.70f;

        HumanSegment stem = AddPart("Stem", new Vector2(stemWidth, stemHeight), stemColor, 0);
        stem.transform.localPosition = new Vector3(0f, stemHeight * 0.5f, 0f);
        PlantTree.PaintVisualTaper(stem, stemWidth, stemHeight, stemColor, 0.62f, 1);
        if (ResolvedSeason == PlantSeason.Winter)
        {
            PaintWinterStemTip(stem);
            PaintWinterStemBud(stem);
        }

        // Winter is a tapered stick plus a bud. Summer PartCount 62
        // (PLANT-3) stays — this skip is winter-only.
        if (ResolvedSeason != PlantSeason.Winter)
        {
            int nLeaf = leafCount;
            if (ResolvedSeason == PlantSeason.Autumn && nLeaf > 2)
                nLeaf--;
            for (int i = 0; i < nLeaf; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float along = i == 0 ? 0.24f : (i == 1 ? 0.52f : 0.28f + 0.16f * i);
                float stemAt = Mathf.Lerp(stemWidth, stemWidth * 0.62f, Mathf.Clamp01(along));
                // Sun vs shade leaflet. One leafColor made a plastic card.
                Color leafletTint = (i & 1) == 0
                    ? leafColor
                    : (ResolvedSeason == PlantSeason.Autumn
                        ? Color.Lerp(leafColor, new Color(0.42f, 0.22f, 0.10f, 1f), 0.40f)
                        : Color.Lerp(leafColor, stemColor, 0.24f));
                HumanSegment leaf = AddPart("Leaf_" + i, leafSize, leafletTint, 1);
                leaf.transform.SetParent(stem.transform, false);
                leaf.transform.localPosition = new Vector3(
                    side * (stemAt * 0.5f + leafSize.x * 0.35f),
                    (Mathf.Clamp01(along) - 0.5f) * stemHeight,
                    0f);
                // Autumn spent hang. Summer 32° stays. Not a hinge.
                // Sit on the tapered stem, not as a sibling card beside
                // it (strawberry already parents leaflets to the runner).
                // headRest peels the cane lean so a leaflet is a canopy,
                // not a flag on the stick. +36 still summer-tight beside
                // TuftFill under TuftLTip / bark 0.04 at ortho 5;
                // +38 reads spent. Side pose 32° stays.
                float hang = ResolvedSeason == PlantSeason.Autumn ? 38f : 0f;
                leaf.transform.localRotation = Quaternion.Euler(
                    0f, 0f, side * (32f + 14f * (i % 3) + hang) + headRest);
                PaintLeafMidrib(leaf, side, leafletTint, stemAt);
            }

            if (spikeCount > 0)
            {
                for (int i = 0; i < spikeCount; i++)
                {
                    float t = spikeCount == 1 ? 0.5f : i / (float)(spikeCount - 1);
                    HumanSegment spike = AddPart(
                        "Spike_" + i,
                        bloomSize,
                        Color.Lerp(bloomColor, diskColor, t * 0.35f),
                        3);
                    spike.transform.SetParent(stem.transform, false);
                    // (t−0.5)·0.03 still sat ON the cane (lilac rachis
                    // analog). Spike rides the left pad; florets stay
                    // relative. PartCount 62.
                    spike.transform.localPosition = new Vector3(
                        -stemWidth * 0.28f + (t - 0.5f) * 0.03f,
                        (Mathf.Clamp01(0.62f + t * 0.38f) - 0.5f) * stemHeight,
                        0f);
                    spike.transform.localRotation = Quaternion.Euler(0f, 0f, headRest);
                    if (ResolvedSeason == PlantSeason.Autumn)
                        PaintSpentSpike(spike);
                    else
                        PaintSpikeFlorets(spike);
                }
            }
            else
            {
                HumanSegment bloom = AddPart("Bloom", bloomSize, bloomColor, 3);
                bloom.transform.SetParent(stem.transform, false);
                // x=0 was a lollipop on the cane (raceme hub analog).
                // Head rides the left pad; petals are children.
                // PartCount 62.
                bloom.transform.localPosition = new Vector3(
                    -bloomSize.x * 0.18f,
                    stemHeight * 0.5f + bloomSize.y * 0.42f,
                    0f);
                bloom.transform.localRotation = Quaternion.Euler(0f, 0f, headRest);
                PaintCalyx(bloom);
                if (ResolvedSeason == PlantSeason.Autumn)
                    PaintSpentBloom(bloom);
                else
                    PaintBloomPetals(bloom);

                if (diskSize.x > 0.001f && diskSize.y > 0.001f)
                {
                    // Autumn spent head: a small rust seed that rides
                    // the bloom, not a summer coin beside a dead flower.
                    // Disk stays a HumanSegment so PLANT-3 PartCount 62.
                    bool spentHead = ResolvedSeason == PlantSeason.Autumn;
                    Vector2 diskSz = spentHead
                        ? new Vector2(diskSize.x * 0.55f, diskSize.y * 0.55f)
                        : diskSize;
                    HumanSegment disk = AddPart("Disk", diskSz, diskColor, 4);
                    if (spentHead)
                    {
                        disk.transform.SetParent(bloom.transform, false);
                        // x=0 sat IN the spent hip (oak Dome analog).
                        // Peak rides the left pad. PartCount 62.
                        disk.transform.localPosition = new Vector3(
                            -diskSz.x * 0.18f, bloomSize.y * 0.08f, 0f);
                        disk.transform.localRotation = Quaternion.identity;
                    }
                    else
                    {
                        disk.transform.SetParent(stem.transform, false);
                        disk.transform.localPosition = new Vector3(
                            -bloomSize.x * 0.18f,
                            stemHeight * 0.5f + bloomSize.y * 0.42f,
                            0f);
                        disk.transform.localRotation = Quaternion.Euler(0f, 0f, headRest);
                        if (ResolvedKind == FlowerKind.Daffodil)
                            PlantTree.PaintVisualTaper(disk, diskSz.x, diskSz.y, diskColor, 0.55f, 3);
                    }
                }
            }
        }

        // Real petal art (ART-0 gap 3, 20.09). Picture only: no part is added, no box
        // changes, no collider and no size is touched - ArtParts.Dress swaps the sprite
        // and keeps the kind's colour through the grey canvas. A headless run without
        // visual capture is not drawing a picture anyway, so it keeps the flat boxes; the
        // gallery's own toggle moves them back and forth (GalleryScene.SetRealArt).
        if (ArtLibrary.RealArtEnabled || HeadlessTrial.VisualCapture)
            ArtParts.DressFlower(transform, true);
    }

    // Flat petal fan around the bloom box. Not a HumanSegment — PartCount
    // stays the PLANT-3 62. Ellipse is ignored by ApplyPicture.
    // Cup (tulip/crocus/lily), hang (bluebell), fan (iris/violet),
    // nested rose (not a daisy ring).
    private void PaintBloomPetals(HumanSegment bloom)
    {
        if (ReferenceEquals(bloom, null) || spikeCount > 0)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        if (ResolvedSeason == PlantSeason.Winter)
            return;

        bool cup = ResolvedKind == FlowerKind.Tulip
            || ResolvedKind == FlowerKind.Crocus
            || ResolvedKind == FlowerKind.Lily
            || ResolvedKind == FlowerKind.Daffodil;
        bool hang = ResolvedKind == FlowerKind.Bluebell;
        bool ring = ResolvedKind == FlowerKind.Sunflower || ResolvedKind == FlowerKind.Daisy;
        bool fan = ResolvedKind == FlowerKind.Iris || ResolvedKind == FlowerKind.Violet;
        bool rose = ResolvedKind == FlowerKind.Rose;
        bool poppy = ResolvedKind == FlowerKind.Poppy;

        int n = 5;
        if (ring)
            n = 8;
        else if (poppy)
            n = 4;
        else if (cup || hang || fan)
            n = 3;

        Vector2 petal = new Vector2(bloomSize.x * 0.36f, bloomSize.y * 0.52f);
        float reach = Mathf.Max(bloomSize.x, bloomSize.y) * 0.42f;
        if (cup)
        {
            petal = new Vector2(bloomSize.x * 0.42f, bloomSize.y * 0.70f);
            reach = bloomSize.y * 0.22f;
        }
        else if (hang)
        {
            petal = new Vector2(bloomSize.x * 0.38f, bloomSize.y * 0.62f);
            reach = -bloomSize.y * 0.28f;
        }
        else if (fan)
        {
            petal = new Vector2(bloomSize.x * 0.44f, bloomSize.y * 0.58f);
            reach = bloomSize.y * 0.16f;
        }
        else if (rose || poppy)
        {
            // Nested fan, not a daisy ring. Poppy is four overlapping
            // cups; rose five. PartCount stays the PLANT-3 62.
            petal = new Vector2(bloomSize.x * 0.42f, bloomSize.y * 0.60f);
            reach = bloomSize.y * 0.12f;
            if (poppy)
            {
                petal = new Vector2(bloomSize.x * 0.50f, bloomSize.y * 0.58f);
                reach = bloomSize.y * 0.10f;
            }
        }

        Color petalColor = Color.Lerp(bloomColor, Color.white, 0.12f);
        // Cup / hang keep the bloom box as the vessel. Open kinds hide
        // it so the petal fan is the silhouette, not a card behind them.
        if (!cup && !hang && bloom.sprite != null)
            bloom.sprite.enabled = false;
        int order = ring ? bloom.sortingOrder : bloom.sortingOrder - 1;
        for (int petalI = 0; petalI < n; petalI++)
        {
            float a;
            if (cup || hang || fan)
                a = (petalI - (n - 1) * 0.5f) * 28f;
            else if (rose || poppy)
                a = (petalI - (n - 1) * 0.5f) * 22f;
            else
                a = (petalI / (float)n) * 360f;
            float rad = a * Mathf.Deg2Rad;
            GameObject go = new GameObject("Petal" + petalI);
            go.transform.SetParent(bloom.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            Color petalPaint = petalColor;
            if ((petalI & 1) == 1)
                petalPaint = Color.Lerp(petalColor, stemColor, 0.16f);
            else if (ResolvedSeason == PlantSeason.Autumn && petalI == 0)
                petalPaint = Color.Lerp(petalColor, new Color(0.72f, 0.48f, 0.16f, 1f), 0.28f);
            HumanSegment.ApplyPicture(
                sr,
                petal,
                HumanVisualShape.Box,
                null,
                petalPaint,
                order);
            float px = Mathf.Sin(rad) * Mathf.Abs(reach);
            float py = Mathf.Cos(rad) * reach;
            // Cup / hang / fan / rose 0° petal sat ON the bloom Y
            // (lilac spray centre analog). −0.18 × petal 0.42 still
            // reached +0.03 (poppy 0.50 even more) — a disc through
            // the head. Width kisses x=0 from the left. Daisy ring
            // keeps the circle. PartCount 62.
            bool facePetal = !ring && Mathf.Abs(a) < 1f;
            if (facePetal)
                px = -petal.x * 0.50f;
            go.transform.localPosition = new Vector3(px, py, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, a);
            GameObject petalTip = new GameObject("PetalTip" + petalI);
            petalTip.transform.SetParent(go.transform, false);
            SpriteRenderer petalTipSr = petalTip.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                petalTipSr,
                new Vector2(petal.x * 0.48f, petal.y * 0.26f),
                HumanVisualShape.Box,
                null,
                petalPaint,
                order);
            // Petal box without a distal lip stayed a card after
            // FrostTip (tree Petal before PetalTip). Hang off the
            // outer end; PartCount 62.
            petalTip.transform.localPosition = new Vector3(0f, petal.y * 0.42f, 0f);
            if (!facePetal)
                continue;
            // One 0° petal at −0.52 left the head a comma (strawberry
            // before PetalR). Far half kisses x=0. PartCount 62.
            GameObject petalR = new GameObject("PetalR");
            petalR.transform.SetParent(bloom.transform, false);
            SpriteRenderer petalRSr = petalR.AddComponent<SpriteRenderer>();
            float farW = petal.x * 0.90f;
            HumanSegment.ApplyPicture(
                petalRSr,
                new Vector2(farW, petal.y * 0.92f),
                HumanVisualShape.Box,
                null,
                Color.Lerp(petalColor, Color.white, 0.08f),
                order);
            petalR.transform.localPosition = new Vector3(farW * 0.50f, py, 0f);
            petalR.transform.localRotation = Quaternion.Euler(0f, 0f, a);
            GameObject petalTipR = new GameObject("PetalTipR");
            petalTipR.transform.SetParent(petalR.transform, false);
            SpriteRenderer petalTipRSr = petalTipR.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                petalTipRSr,
                new Vector2(farW * 0.48f, petal.y * 0.24f),
                HumanVisualShape.Box,
                null,
                Color.Lerp(petalColor, Color.white, 0.08f),
                order);
            petalTipR.transform.localPosition = new Vector3(0f, petal.y * 0.38f, 0f);
        }
    }

    // Autumn: a seed head / hip, not a summer petal fan tinted rust.
    // Disk kinds keep the HumanSegment disk (sunflower plate). Petals
    // are not HumanSegment — PartCount stays the PLANT-3 62.
    private void PaintSpentBloom(HumanSegment bloom)
    {
        if (ReferenceEquals(bloom, null) || spikeCount > 0)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;

        if (bloom.sprite != null)
            bloom.sprite.enabled = false;
        // 0.78 still summer-orange beside DomeFill / bark 0.10 at
        // ortho 5. 0.90 still orange beside TuftFill / bark 0.04.
        // 0.92 reads a spent hip without a hinge. PartCount 62.
        Color spent = Color.Lerp(bloomColor, new Color(0.42f, 0.28f, 0.12f, 1f), 0.92f);
        GameObject hip = new GameObject("Spent");
        hip.transform.SetParent(bloom.transform, false);
        SpriteRenderer hipSr = hip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            hipSr,
            new Vector2(bloomSize.x * 0.36f, bloomSize.y * 0.40f),
            HumanVisualShape.Box,
            null,
            spent,
            bloom.sortingOrder);
        // x=0 sat ON the spent cane (bloom lollipop analog). 0.48 at
        // −0.16 still reached +0.08 (Needle1 analog). Width 0.36
        // kisses x=0; SpentR is the far half. Cap rides this pad.
        // PartCount 62.
        hip.transform.localPosition = new Vector3(
            -bloomSize.x * 0.18f, -bloomSize.y * 0.06f, 0f);
        GameObject hipTip = new GameObject("SpentTip");
        hipTip.transform.SetParent(bloom.transform, false);
        SpriteRenderer hipTipSr = hipTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            hipTipSr,
            new Vector2(bloomSize.x * 0.20f, bloomSize.y * 0.16f),
            HumanVisualShape.Box,
            null,
            spent,
            bloom.sortingOrder);
        // Hip without a distal lip stayed a spent card after PetalTip
        // (tree Drop before DropTip). Hang off Spent; PartCount 62.
        hipTip.transform.localPosition = new Vector3(
            -bloomSize.x * 0.18f, -bloomSize.y * 0.22f, 0f);
        GameObject hipR = new GameObject("SpentR");
        hipR.transform.SetParent(bloom.transform, false);
        SpriteRenderer hipRSr = hipR.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            hipRSr,
            new Vector2(bloomSize.x * 0.32f, bloomSize.y * 0.36f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(spent, stemColor, 0.18f),
            bloom.sortingOrder);
        // 0.36 at −0.28 right −0.10 left a comma hip (tree ovate
        // before CheekR). Width 0.36 kisses x=0; SpentR is the far
        // half. PartCount 62.
        hipR.transform.localPosition = new Vector3(
            bloomSize.x * 0.16f, -bloomSize.y * 0.04f, 0f);
        GameObject hipRTip = new GameObject("SpentRTip");
        hipRTip.transform.SetParent(bloom.transform, false);
        SpriteRenderer hipRTipSr = hipRTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            hipRTipSr,
            new Vector2(bloomSize.x * 0.16f, bloomSize.y * 0.14f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(spent, stemColor, 0.18f),
            bloom.sortingOrder);
        // SpentTip closed the near hip. Far SpentR without a lip
        // stayed a coin after WaistTip (tree DropR before DropRTip).
        // Hang off SpentR; PartCount 62.
        hipRTip.transform.localPosition = new Vector3(
            bloomSize.x * 0.16f, -bloomSize.y * 0.20f, 0f);
        GameObject cap = new GameObject("SpentCap");
        cap.transform.SetParent(bloom.transform, false);
        SpriteRenderer capSr = cap.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            capSr,
            new Vector2(bloomSize.x * 0.28f, bloomSize.y * 0.22f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(spent, stemColor, 0.35f),
            bloom.sortingOrder);
        cap.transform.localPosition = new Vector3(
            -bloomSize.x * 0.18f, bloomSize.y * 0.18f, 0f);
        GameObject capTip = new GameObject("SpentCapTip");
        capTip.transform.SetParent(bloom.transform, false);
        SpriteRenderer capTipSr = capTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            capTipSr,
            new Vector2(bloomSize.x * 0.16f, bloomSize.y * 0.12f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(spent, stemColor, 0.35f),
            bloom.sortingOrder);
        // SpentCap without a distal lip stayed a table on the hip
        // (pine Cap before CapTip). Hang off Spent; PartCount 62.
        capTip.transform.localPosition = new Vector3(
            -bloomSize.x * 0.18f, bloomSize.y * 0.28f, 0f);
        GameObject capR = new GameObject("SpentCapR");
        capR.transform.SetParent(bloom.transform, false);
        SpriteRenderer capRSr = capR.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            capRSr,
            new Vector2(bloomSize.x * 0.24f, bloomSize.y * 0.20f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(spent, stemColor, 0.28f),
            bloom.sortingOrder);
        // Left-only SpentCap was a one-tent hip after Spent/SpentR
        // kissed (tree Apex before ApexR). Far peak rides SpentR.
        // PartCount 62.
        capR.transform.localPosition = new Vector3(
            bloomSize.x * 0.16f, bloomSize.y * 0.16f, 0f);
        GameObject capRTip = new GameObject("SpentCapRTip");
        capRTip.transform.SetParent(bloom.transform, false);
        SpriteRenderer capRTipSr = capRTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            capRTipSr,
            new Vector2(bloomSize.x * 0.12f, bloomSize.y * 0.10f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(spent, stemColor, 0.28f),
            bloom.sortingOrder);
        // SpentCapTip closed the near peak. Far SpentCapR without a
        // lip stayed a table after WaistTip (pine CapR before CapRTip).
        // Hang off SpentR; PartCount 62.
        capRTip.transform.localPosition = new Vector3(
            bloomSize.x * 0.16f, bloomSize.y * 0.26f, 0f);
        // CapFill rides Cap under CapTip. SpentCap / SpentCapR kissed
        // but no shoulder chip under SpentCapTip left the autumn hip
        // a one-tent after ApexFill / DomeFill closed tree peaks.
        // PartCount 62 — not HumanSegment.
        GameObject capFill = new GameObject("SpentCapFill");
        capFill.transform.SetParent(bloom.transform, false);
        SpriteRenderer capFillSr = capFill.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            capFillSr,
            new Vector2(bloomSize.x * 0.14f, bloomSize.y * 0.10f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(spent, stemColor, 0.35f),
            bloom.sortingOrder);
        // Chip rides SpentCap under SpentCapTip; right edge toward
        // SpentCapTip. PartCount 62.
        capFill.transform.localPosition = new Vector3(
            -bloomSize.x * 0.20f, bloomSize.y * 0.22f, 0f);
        GameObject capFillTip = new GameObject("SpentCapFillTip");
        capFillTip.transform.SetParent(bloom.transform, false);
        SpriteRenderer capFillTipSr = capFillTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            capFillTipSr,
            new Vector2(bloomSize.x * 0.08f, bloomSize.y * 0.06f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(spent, stemColor, 0.35f),
            bloom.sortingOrder);
        // SpentCapTip closed the hip peak. SpentCapFill without a lip
        // stayed a peak mast chip (CapFill before CapFillTip). Hang
        // off SpentCap toward SpentCapTip. PartCount 62.
        capFillTip.transform.localPosition = new Vector3(
            -bloomSize.x * 0.20f, bloomSize.y * 0.26f, 0f);
        GameObject capFillR = new GameObject("SpentCapFillR");
        capFillR.transform.SetParent(bloom.transform, false);
        SpriteRenderer capFillRSr = capFillR.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            capFillRSr,
            new Vector2(bloomSize.x * 0.12f, bloomSize.y * 0.09f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(spent, stemColor, 0.28f),
            bloom.sortingOrder);
        // On SpentCapR under SpentCapRTip. PartCount 62.
        capFillR.transform.localPosition = new Vector3(
            bloomSize.x * 0.18f, bloomSize.y * 0.20f, 0f);
        GameObject capFillRTip = new GameObject("SpentCapFillRTip");
        capFillRTip.transform.SetParent(bloom.transform, false);
        SpriteRenderer capFillRTipSr = capFillRTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            capFillRTipSr,
            new Vector2(bloomSize.x * 0.07f, bloomSize.y * 0.05f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(spent, stemColor, 0.28f),
            bloom.sortingOrder);
        // Left edge kisses SpentCapRTip right. PartCount 62.
        capFillRTip.transform.localPosition = new Vector3(
            bloomSize.x * 0.18f, bloomSize.y * 0.24f, 0f);
    }

    // Lavender spike: overlapping florets on a taper, not four bloomSize
    // cards. Not HumanSegment — PartCount stays the PLANT-3 62.
    private void PaintSpikeFlorets(HumanSegment spike)
    {
        if (ReferenceEquals(spike, null))
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        if (spike.sprite != null)
            spike.sprite.enabled = false;

        int n = 3;
        for (int k = 0; k < n; k++)
        {
            float u = n == 1 ? 0f : (k / (float)(n - 1) - 0.5f) * 2f;
            float along = (k - 1) * bloomSize.y * 0.22f;
            // k==1 0.78 at −0.18 outer +0.21 still a sausage on the
            // spike (lilac 0.70 floret analog). Laterals 0.58 at ±0.16
            // crossed the cane the same way. Open packets. PartCount 62.
            float floretX;
            float floretW;
            float floretH;
            if (k == 0)
            {
                floretX = -bloomSize.x * 0.32f;
                floretW = 0.42f;
                floretH = 0.34f;
            }
            else if (k == 1)
            {
                // 0.36 at −0.22 right −0.04 / k==2 left +0.08 left a
                // 0.12 sky slit on the spike (willow Hang5 analog).
                // Packet kisses x=0 from the left. PartCount 62.
                floretX = -bloomSize.x * 0.18f;
                floretW = 0.36f;
                floretH = 0.42f;
            }
            else
            {
                // Width 0.40; left edge 0 against k==1 right 0
                // (pine BodyR analog). PartCount 62.
                floretX = bloomSize.x * 0.20f;
                floretW = 0.40f;
                floretH = 0.34f;
            }
            GameObject go = new GameObject("Floret" + k);
            go.transform.SetParent(spike.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                sr,
                new Vector2(bloomSize.x * floretW, bloomSize.y * floretH),
                HumanVisualShape.Box,
                null,
                Color.Lerp(bloomColor, diskColor, k * 0.18f),
                spike.sortingOrder);
            go.transform.localPosition = new Vector3(floretX, along, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, u * 10f);
            GameObject floretTip = new GameObject("FloretTip" + k);
            floretTip.transform.SetParent(go.transform, false);
            SpriteRenderer floretTipSr = floretTip.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                floretTipSr,
                new Vector2(bloomSize.x * floretW * 0.48f, bloomSize.y * floretH * 0.28f),
                HumanVisualShape.Box,
                null,
                Color.Lerp(bloomColor, diskColor, k * 0.18f),
                spike.sortingOrder);
            // Floret box without a distal lip stayed a spike card
            // after PetalTip (tree packed Tooth before LobeL1). Hang
            // off the outer packet; PartCount 62.
            floretTip.transform.localPosition = new Vector3(0f, bloomSize.y * floretH * 0.38f, 0f);
        }
    }

    // Autumn lavender: spent raceme, not a rust-tinted summer box.
    private void PaintSpentSpike(HumanSegment spike)
    {
        if (ReferenceEquals(spike, null))
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        if (spike.sprite != null)
            spike.sprite.enabled = false;

        // 0.80 still summer-lavender beside spent hip 0.90 / oak
        // russet. 0.84 still lavender beside TuftFill / bark 0.04.
        // 0.88 reads a spent raceme. PartCount 62.
        Color spent = Color.Lerp(bloomColor, new Color(0.42f, 0.32f, 0.16f, 1f), 0.88f);
        int n = 3;
        for (int k = 0; k < n; k++)
        {
            GameObject go = new GameObject("SpentFloret" + k);
            go.transform.SetParent(spike.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                sr,
                new Vector2(bloomSize.x * 0.32f, bloomSize.y * 0.22f),
                HumanVisualShape.Box,
                null,
                spent,
                spike.sortingOrder);
            go.transform.localPosition = new Vector3(
                // k==2 at −0.22 right −0.06 / k==1 at +0.26 left +0.10
                // left a spent-cane slit (summer k==1 analog). Width
                // 0.32 kisses x=0. Open packets. PartCount 62.
                k == 0 ? -bloomSize.x * 0.28f
                    : (k == 1 ? bloomSize.x * 0.16f : -bloomSize.x * 0.16f),
                (k - 1) * bloomSize.y * 0.18f,
                0f);
            GameObject spentTip = new GameObject("SpentFloretTip" + k);
            spentTip.transform.SetParent(go.transform, false);
            SpriteRenderer spentTipSr = spentTip.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                spentTipSr,
                new Vector2(bloomSize.x * 0.16f, bloomSize.y * 0.10f),
                HumanVisualShape.Box,
                null,
                spent,
                spike.sortingOrder);
            spentTip.transform.localPosition = new Vector3(0f, bloomSize.y * 0.12f, 0f);
        }
    }

    // Winter rime on the stem tip. Not HumanSegment — PartCount stays 62.
    private void PaintWinterStemTip(HumanSegment stem)
    {
        if (ReferenceEquals(stem, null) || ReferenceEquals(stem.visual, null))
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        GameObject go = new GameObject("Frost");
        go.transform.SetParent(stem.visual, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        float tipW = stemWidth * 0.62f;
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(tipW * 0.70f, stemHeight * 0.08f),
            HumanVisualShape.Box,
            null,
            new Color(0.78f, 0.80f, 0.82f, 0.75f),
            stem.sortingOrder + 1);
        // 1.15 at +0.22 inner −0.36 — a frost table on the cane
        // (tree TrunkRime0 analog). Right chip. PartCount 62.
        go.transform.localPosition = new Vector3(tipW * 0.55f, stemHeight * 0.42f, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
        GameObject frostL = new GameObject("FrostL");
        frostL.transform.SetParent(stem.visual, false);
        SpriteRenderer frostLSr = frostL.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            frostLSr,
            new Vector2(tipW * 0.62f, stemHeight * 0.07f),
            HumanVisualShape.Box,
            null,
            new Color(0.78f, 0.80f, 0.82f, 0.70f),
            stem.sortingOrder + 1);
        // Right-only Frost was a one-tent cane after WinterBudL
        // (tree TrunkRime1 analog). Far chip. PartCount 62.
        frostL.transform.localPosition = new Vector3(-tipW * 0.50f, stemHeight * 0.40f, 0f);
        frostL.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
        GameObject frostTip = new GameObject("FrostTip");
        frostTip.transform.SetParent(stem.visual, false);
        SpriteRenderer frostTipSr = frostTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            frostTipSr,
            new Vector2(tipW * 0.38f, stemHeight * 0.045f),
            HumanVisualShape.Box,
            null,
            new Color(0.80f, 0.82f, 0.84f, 0.72f),
            stem.sortingOrder + 1);
        // Frost/FrostL without a distal lip stayed a frost table on
        // the cane (tree TrunkRime before RimeTip). Hang off the chip.
        // PartCount 62.
        frostTip.transform.localPosition = new Vector3(tipW * 0.72f, stemHeight * 0.46f, 0f);
        frostTip.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        GameObject frostLTip = new GameObject("FrostLTip");
        frostLTip.transform.SetParent(stem.visual, false);
        SpriteRenderer frostLTipSr = frostLTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            frostLTipSr,
            new Vector2(tipW * 0.34f, stemHeight * 0.040f),
            HumanVisualShape.Box,
            null,
            new Color(0.80f, 0.82f, 0.84f, 0.68f),
            stem.sortingOrder + 1);
        frostLTip.transform.localPosition = new Vector3(-tipW * 0.66f, stemHeight * 0.44f, 0f);
        frostLTip.transform.localRotation = Quaternion.Euler(0f, 0f, -16f);
    }

    // Spent axillary bud. Frost alone left a bare cane. Not HumanSegment
    // — PartCount stays the stem. Same language as tree AttachWinterBuds.
    private void PaintWinterStemBud(HumanSegment stem)
    {
        if (ReferenceEquals(stem, null) || ReferenceEquals(stem.visual, null))
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        float tipW = stemWidth * 0.62f;
        Color bud = Color.Lerp(stemColor, new Color(0.42f, 0.36f, 0.24f, 1f), 0.40f);
        GameObject go = new GameObject("WinterBud");
        go.transform.SetParent(stem.visual, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(Mathf.Max(0.010f, tipW * 0.80f), stemHeight * 0.10f),
            HumanVisualShape.Box,
            null,
            bud,
            stem.sortingOrder + 1);
        // 1.35 at +0.35 inner −0.32 — a bud sausage on the cane
        // (lavender 0.78 floret analog). Right pad. PartCount is the stem.
        go.transform.localPosition = new Vector3(tipW * 0.55f, stemHeight * 0.28f, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
        GameObject tip = new GameObject("WinterBudTip");
        tip.transform.SetParent(stem.visual, false);
        SpriteRenderer tipSr = tip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            tipSr,
            new Vector2(Mathf.Max(0.008f, tipW * 0.80f), stemHeight * 0.06f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(bud, Color.white, 0.12f),
            stem.sortingOrder + 1);
        tip.transform.localPosition = new Vector3(tipW * 0.55f, stemHeight * 0.34f, 0f);
        tip.transform.localRotation = Quaternion.Euler(0f, 0f, 36f);
        GameObject budL = new GameObject("WinterBudL");
        budL.transform.SetParent(stem.visual, false);
        SpriteRenderer budLSr = budL.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            budLSr,
            new Vector2(Mathf.Max(0.010f, tipW * 0.72f), stemHeight * 0.09f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(bud, stemColor, 0.18f),
            stem.sortingOrder + 1);
        // Right-only WinterBud was a one-tent cane after summer TipR
        // (tree spur Bud before BudR). Far bud. PartCount is the stem.
        budL.transform.localPosition = new Vector3(-tipW * 0.50f, stemHeight * 0.26f, 0f);
        budL.transform.localRotation = Quaternion.Euler(0f, 0f, -24f);
        GameObject tipL = new GameObject("WinterBudTipL");
        tipL.transform.SetParent(stem.visual, false);
        SpriteRenderer tipLSr = tipL.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            tipLSr,
            new Vector2(Mathf.Max(0.008f, tipW * 0.70f), stemHeight * 0.05f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(bud, Color.white, 0.08f),
            stem.sortingOrder + 1);
        tipL.transform.localPosition = new Vector3(-tipW * 0.50f, stemHeight * 0.32f, 0f);
        tipL.transform.localRotation = Quaternion.Euler(0f, 0f, -32f);
    }

    // Centre vein so a garden leaf is not a card. Not HumanSegment —
    // PartCount stays the PLANT-3 62. tint is the leaflet (sun/shade).
    private void PaintLeafMidrib(HumanSegment leaf, float side, Color tint, float stemAt)
    {
        if (ReferenceEquals(leaf, null) || ReferenceEquals(leaf.visual, null))
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        if (leaf.sprite != null)
            leaf.sprite.enabled = false;
        // 0.92×1.05 Blade was a green card filling the sinus between
        // the two leaflets (strawberry mid-sausage analog). −0.28 ×
        // 0.32 still inner −0.12 (pine Fill analog). Blade rides
        // Leaflet0; sinus stays empty. PartCount 62.
        GameObject blade = new GameObject("Blade");
        blade.transform.SetParent(leaf.visual, false);
        SpriteRenderer bladeSr = blade.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            bladeSr,
            new Vector2(leafSize.x * 0.28f, leafSize.y * 0.70f),
            HumanVisualShape.Box,
            null,
            tint,
            leaf.sortingOrder);
        // −0.28 × 0.32 inner −0.12 still sat IN the sinus after
        // Leaflet0 left for −0.42 (pine Fill analog). Blade rides
        // Leaflet0; Leaflet1 inner +0.26 stays open. PartCount 62.
        blade.transform.localPosition = new Vector3(
            -leafSize.x * 0.42f,
            leafSize.y * 0.06f,
            0f);
        GameObject bladeTip = new GameObject("BladeTip");
        bladeTip.transform.SetParent(leaf.visual, false);
        SpriteRenderer bladeTipSr = bladeTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            bladeTipSr,
            new Vector2(leafSize.x * 0.16f, leafSize.y * 0.22f),
            HumanVisualShape.Box,
            null,
            tint,
            leaf.sortingOrder);
        // Blade without a distal lip stayed a garden card after
        // FrostTip (tree packed Tooth before LobeL1). Hang off
        // Leaflet0; PartCount 62.
        bladeTip.transform.localPosition = new Vector3(
            -leafSize.x * 0.42f,
            leafSize.y * 0.36f,
            0f);
        GameObject go = new GameObject("Midrib");
        go.transform.SetParent(leaf.visual, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(leafSize.x * 0.16f, leafSize.y * 0.78f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tint, stemColor, 0.35f),
            leaf.sortingOrder);
        // −0.10 sat IN the sinus between leaflets after Tip left
        // for Leaflet0 (ovate Petiole analog). −0.28 stacked Blade
        // into the notch so the terminal flooded the sinus (pine
        // Fill analog). Vein rides Leaflet0; Leaflet1 inner +0.26
        // stays open. PartCount 62.
        go.transform.localPosition = new Vector3(-leafSize.x * 0.42f, leafSize.y * 0.04f, 0f);
        GameObject midribTip = new GameObject("MidribTip");
        midribTip.transform.SetParent(leaf.visual, false);
        SpriteRenderer midribTipSr = midribTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            midribTipSr,
            new Vector2(leafSize.x * 0.10f, leafSize.y * 0.18f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tint, stemColor, 0.35f),
            leaf.sortingOrder);
        // Midrib without a distal lip stayed a garden vein card after
        // BladeTip (tree packed Wrist before WristTip). Hang off
        // Leaflet0; PartCount 62.
        midribTip.transform.localPosition = new Vector3(
            -leafSize.x * 0.42f,
            leafSize.y * 0.40f,
            0f);
        Vector3 stemJoin = new Vector3(side * stemAt * 0.5f, leaf.transform.localPosition.y, 0f);
        Vector3 stemWorld = transform.TransformPoint(stemJoin);
        Vector3 stemVisual = leaf.visual.InverseTransformPoint(stemWorld);
        float petioleReach = Mathf.Sqrt(stemVisual.x * stemVisual.x + stemVisual.y * stemVisual.y);
        GameObject petiole = new GameObject("Petiole");
        petiole.transform.SetParent(leaf.visual, false);
        SpriteRenderer petioleSr = petiole.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            petioleSr,
            new Vector2(
                Mathf.Max(0.004f, leafSize.x * 0.14f),
                Mathf.Max(0.010f, petioleReach * 0.72f)),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tint, stemColor, 0.50f),
            leaf.sortingOrder);
        PlantTree.AimPictureAlongY(petiole.transform, stemVisual);
        GameObject petioleTip = new GameObject("PetioleTip");
        petioleTip.transform.SetParent(petiole.transform, false);
        SpriteRenderer petioleTipSr = petioleTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            petioleTipSr,
            new Vector2(
                Mathf.Max(0.003f, leafSize.x * 0.08f),
                Mathf.Max(0.006f, petioleReach * 0.22f)),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tint, stemColor, 0.50f),
            leaf.sortingOrder);
        // Tree ovate PetioleTip closed the stalk. Garden Petiole
        // without a lip stayed a stick after BladeTip (CarmB before
        // CarmBTip). Hang along +Y of the aimed join; PartCount 62.
        petioleTip.transform.localPosition = new Vector3(0f, petioleReach * 0.28f, 0f);
        GameObject tip = new GameObject("Tip");
        tip.transform.SetParent(leaf.visual, false);
        SpriteRenderer tipSr = tip.AddComponent<SpriteRenderer>();
        Color tipTint = ResolvedSeason == PlantSeason.Autumn
            ? Color.Lerp(tint, new Color(0.78f, 0.42f, 0.10f, 1f), 0.28f)
            : tint;
        HumanSegment.ApplyPicture(
            tipSr,
            new Vector2(leafSize.x * 0.28f, leafSize.y * 0.40f),
            HumanVisualShape.Box,
            null,
            tipTint,
            leaf.sortingOrder);
        // (0, 0.38) sat IN the sinus between leaflets (oak Dome analog).
        // −0.28 stacked the peak into the notch (pine Fill analog).
        // Peak rides Leaflet0; leafSize stays.
        tip.transform.localPosition = new Vector3(
            -leafSize.x * 0.42f,
            leafSize.y * 0.28f,
            0f);
        GameObject tipTip = new GameObject("TipTip");
        tipTip.transform.SetParent(leaf.visual, false);
        SpriteRenderer tipTipSr = tipTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            tipTipSr,
            new Vector2(leafSize.x * 0.16f, leafSize.y * 0.18f),
            HumanVisualShape.Box,
            null,
            tipTint,
            leaf.sortingOrder);
        // Tree ovate TipTip closed the cup peak. Garden Tip without a
        // lip stayed a paddle after BladeTip (packed Tip after
        // WristRTip). Hang off Tip; PartCount 62.
        tipTip.transform.localPosition = new Vector3(
            -leafSize.x * 0.42f,
            leafSize.y * 0.42f,
            0f);
        GameObject tipR = new GameObject("TipR");
        tipR.transform.SetParent(leaf.visual, false);
        SpriteRenderer tipRSr = tipR.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            tipRSr,
            new Vector2(leafSize.x * 0.24f, leafSize.y * 0.36f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tipTint, stemColor, 0.12f),
            leaf.sortingOrder);
        // Left-only Tip was a one-tent garden leaf after laterals
        // at ±0.42 (tree Apex before ApexR). Far peak rides Leaflet1;
        // sinus between leaflets stays empty. PartCount 62.
        tipR.transform.localPosition = new Vector3(
            leafSize.x * 0.42f,
            leafSize.y * 0.24f,
            0f);
        GameObject tipRTip = new GameObject("TipRTip");
        tipRTip.transform.SetParent(leaf.visual, false);
        SpriteRenderer tipRTipSr = tipRTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            tipRTipSr,
            new Vector2(leafSize.x * 0.14f, leafSize.y * 0.16f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tipTint, stemColor, 0.12f),
            leaf.sortingOrder);
        // TipTip closed the near garden peak. Far TipR without a lip
        // stayed a paddle after BladeRTip (tree TipR after WristRTip).
        // Hang off TipR; sinus stays empty. PartCount 62.
        tipRTip.transform.localPosition = new Vector3(
            leafSize.x * 0.42f,
            leafSize.y * 0.38f,
            0f);
        GameObject bladeR = new GameObject("BladeR");
        bladeR.transform.SetParent(leaf.visual, false);
        SpriteRenderer bladeRSr = bladeR.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            bladeRSr,
            new Vector2(leafSize.x * 0.24f, leafSize.y * 0.62f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tint, stemColor, 0.12f),
            leaf.sortingOrder);
        // Left-only Blade was a one-tent garden leaf after TipR
        // (tree Wrist before WristR). Far blade rides Leaflet1;
        // sinus stays empty. PartCount 62.
        bladeR.transform.localPosition = new Vector3(
            leafSize.x * 0.42f,
            leafSize.y * 0.06f,
            0f);
        GameObject bladeRTip = new GameObject("BladeRTip");
        bladeRTip.transform.SetParent(leaf.visual, false);
        SpriteRenderer bladeRTipSr = bladeRTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            bladeRTipSr,
            new Vector2(leafSize.x * 0.14f, leafSize.y * 0.20f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tint, stemColor, 0.12f),
            leaf.sortingOrder);
        bladeRTip.transform.localPosition = new Vector3(
            leafSize.x * 0.42f,
            leafSize.y * 0.32f,
            0f);
        GameObject midR = new GameObject("MidribR");
        midR.transform.SetParent(leaf.visual, false);
        SpriteRenderer midRSr = midR.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            midRSr,
            new Vector2(leafSize.x * 0.14f, leafSize.y * 0.68f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tint, stemColor, 0.28f),
            leaf.sortingOrder);
        midR.transform.localPosition = new Vector3(
            leafSize.x * 0.42f,
            leafSize.y * 0.04f,
            0f);
        GameObject midRTip = new GameObject("MidribRTip");
        midRTip.transform.SetParent(leaf.visual, false);
        SpriteRenderer midRTipSr = midRTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            midRTipSr,
            new Vector2(leafSize.x * 0.08f, leafSize.y * 0.16f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tint, stemColor, 0.28f),
            leaf.sortingOrder);
        midRTip.transform.localPosition = new Vector3(
            leafSize.x * 0.42f,
            leafSize.y * 0.36f,
            0f);
        // Two side leaflets so a garden leaf is a cluster, not one
        // card. ±0.34 × 0.40 still inner ±0.14 (fruit cheek analog).
        // Laterals |x| 0.42 × 0.32; sinus stays empty. PartCount 62.
        for (int lobeI = 0; lobeI < 2; lobeI++)
        {
            float lobeSign = lobeI == 0 ? -1f : 1f;
            GameObject lobe = new GameObject("Leaflet" + lobeI);
            lobe.transform.SetParent(leaf.visual, false);
            SpriteRenderer lobeSr = lobe.AddComponent<SpriteRenderer>();
            Color lobeTint = (lobeI & 1) == 0
                ? tint
                : Color.Lerp(tint, stemColor, 0.18f);
            if (ResolvedSeason == PlantSeason.Autumn && lobeI == 0)
                lobeTint = Color.Lerp(tint, new Color(0.78f, 0.42f, 0.10f, 1f), 0.40f);
            HumanSegment.ApplyPicture(
                lobeSr,
                new Vector2(leafSize.x * 0.32f, leafSize.y * 0.52f),
                HumanVisualShape.Box,
                null,
                lobeTint,
                leaf.sortingOrder);
            lobe.transform.localPosition = new Vector3(
                lobeSign * leafSize.x * 0.42f,
                leafSize.y * 0.04f,
                0f);
            lobe.transform.localRotation = Quaternion.Euler(0f, 0f, lobeSign * side * 26f);
            GameObject leafletTip = new GameObject("LeafletTip" + lobeI);
            leafletTip.transform.SetParent(lobe.transform, false);
            SpriteRenderer leafletTipSr = leafletTip.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                leafletTipSr,
                new Vector2(leafSize.x * 0.18f, leafSize.y * 0.22f),
                HumanVisualShape.Box,
                null,
                lobeTint,
                leaf.sortingOrder);
            // Leaflet box without a distal lip stayed a garden card
            // after FrostTip (tree packed Tooth before LobeL1). Hang
            // off the outer end; PartCount 62.
            leafletTip.transform.localPosition = new Vector3(0f, leafSize.y * 0.22f, 0f);
        }
    }

    // Green sepals under the bloom. Not HumanSegment — PartCount stays 62.
    private void PaintCalyx(HumanSegment bloom)
    {
        if (ReferenceEquals(bloom, null) || spikeCount > 0)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        if (ResolvedSeason == PlantSeason.Winter)
            return;

        Vector2 sepal = new Vector2(bloomSize.x * 0.22f, bloomSize.y * 0.32f);
        Color sepalColor = Color.Lerp(stemColor, leafColor, 0.40f);
        for (int sepalI = 0; sepalI < 2; sepalI++)
        {
            float side = sepalI == 0 ? -1f : 1f;
            GameObject go = new GameObject("Sepal" + sepalI);
            go.transform.SetParent(bloom.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                sr,
                sepal,
                HumanVisualShape.Box,
                null,
                sepalColor,
                bloom.sortingOrder - 2);
            go.transform.localPosition = new Vector3(
                side * bloomSize.x * 0.26f,
                -bloomSize.y * 0.38f,
                0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, side * 28f);
            GameObject sepalTip = new GameObject("SepalTip" + sepalI);
            sepalTip.transform.SetParent(go.transform, false);
            SpriteRenderer sepalTipSr = sepalTip.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                sepalTipSr,
                new Vector2(sepal.x * 0.55f, sepal.y * 0.28f),
                HumanVisualShape.Box,
                null,
                sepalColor,
                bloom.sortingOrder - 2);
            // Sepal box without a distal lip stayed a green card
            // after FrostTip (petal before PetalTip). Hang off the
            // outer end; PartCount 62.
            sepalTip.transform.localPosition = new Vector3(0f, sepal.y * 0.42f, 0f);
        }
    }

    private HumanSegment AddPart(string name, Vector2 size, Color color, int sortingOrder)
    {
        GameObject part = new GameObject(name);
        HumanSegment segment = part.AddComponent<HumanSegment>();
        segment.InitializeVisualOnly(
            name,
            size,
            color,
            transform,
            sortingOrder,
            HumanVisualShape.Box);
        PartCount++;
        return segment;
    }

    private void ApplySeasonTint()
    {
        ResolvedSeason = season;
        if (ResolvedSeason == PlantSeason.Auto)
            ResolvedSeason = (PlantSeason)((Mathf.Abs(seed) % 4) + 1);

        switch (ResolvedSeason)
        {
            case PlantSeason.Spring:
                leafColor = Color.Lerp(leafColor, new Color(0.42f, 0.72f, 0.28f, 1f), 0.58f);
                bloomColor = Color.Lerp(bloomColor, Color.white, 0.22f);
                stemColor = Color.Lerp(stemColor, new Color(0.28f, 0.56f, 0.22f, 1f), 0.22f);
                // Flush is smaller than the summer head. PartCount stays
                // the PLANT-3 62 — petals are not HumanSegment.
                bloomSize = new Vector2(bloomSize.x * 0.88f, bloomSize.y * 0.88f);
                leafSize = new Vector2(leafSize.x * 0.90f, leafSize.y * 0.90f);
                if (diskSize.x > 0.001f)
                    diskSize = new Vector2(diskSize.x * 0.88f, diskSize.y * 0.88f);
                // Shared 0.58 lime left the bed as one flush card
                // (tree spring before ApplySpringLeafKind). Kind walks
                // the lime. PartCount 62.
                ApplySpringLeafKind();
                break;
            case PlantSeason.Autumn:
                ApplyAutumnLeaf();
                bloomColor = Color.Lerp(bloomColor, new Color(0.70f, 0.28f, 0.10f, 1f), 0.55f);
                stemColor = Color.Lerp(stemColor, new Color(0.32f, 0.28f, 0.12f, 1f), 0.28f);
                diskColor = Color.Lerp(diskColor, new Color(0.72f, 0.48f, 0.16f, 1f), 0.45f);
                break;
            case PlantSeason.Winter:
                leafColor = new Color(0.36f, 0.28f, 0.14f, 1f);
                bloomColor = Color.Lerp(bloomColor, new Color(0.55f, 0.50f, 0.46f, 1f), 0.55f);
                // 0.35 toward a green-grey left a summer stick. Wood
                // brown matches the leafless tree cane. PartCount is
                // the stem only — frost is not HumanSegment.
                stemColor = Color.Lerp(stemColor, new Color(0.30f, 0.26f, 0.18f, 1f), 0.70f);
                diskColor = Color.Lerp(diskColor, new Color(0.42f, 0.38f, 0.32f, 1f), 0.50f);
                break;
        }
    }

    // After the shared 0.58 lime. Autumn already walks a family;
    // spring without this left rose/daisy/lavender as one flush card.
    // Picture only — PartCount 62.
    private void ApplySpringLeafKind()
    {
        switch (ResolvedKind)
        {
            case FlowerKind.Sunflower:
            case FlowerKind.Daisy:
            case FlowerKind.Daffodil:
                leafColor = Color.Lerp(leafColor, new Color(0.62f, 0.78f, 0.18f, 1f), 0.22f);
                break;
            case FlowerKind.Rose:
            case FlowerKind.Poppy:
                leafColor = Color.Lerp(leafColor, new Color(0.36f, 0.68f, 0.22f, 1f), 0.18f);
                break;
            case FlowerKind.Tulip:
            case FlowerKind.Lily:
                leafColor = Color.Lerp(leafColor, new Color(0.48f, 0.74f, 0.20f, 1f), 0.16f);
                break;
            case FlowerKind.Lavender:
            case FlowerKind.Iris:
            case FlowerKind.Bluebell:
                leafColor = Color.Lerp(leafColor, new Color(0.40f, 0.62f, 0.28f, 1f), 0.16f);
                break;
            case FlowerKind.Violet:
            case FlowerKind.Crocus:
                leafColor = Color.Lerp(leafColor, new Color(0.44f, 0.70f, 0.26f, 1f), 0.14f);
                break;
            default:
                leafColor = Color.Lerp(leafColor, new Color(0.46f, 0.74f, 0.22f, 1f), 0.12f);
                break;
        }
    }

    // Kind family first so a rose is rust, a sunflower gold, not one
    // brown for the whole bed.
    private void ApplyAutumnLeaf()
    {
        switch (ResolvedKind)
        {
            case FlowerKind.Sunflower:
            case FlowerKind.Daisy:
            case FlowerKind.Daffodil:
                leafColor = new Color(0.78f, 0.62f, 0.16f, 1f);
                break;
            case FlowerKind.Rose:
            case FlowerKind.Poppy:
                leafColor = new Color(0.62f, 0.22f, 0.10f, 1f);
                break;
            case FlowerKind.Tulip:
            case FlowerKind.Lily:
                leafColor = new Color(0.72f, 0.42f, 0.12f, 1f);
                break;
            case FlowerKind.Lavender:
            case FlowerKind.Iris:
            case FlowerKind.Bluebell:
                leafColor = new Color(0.42f, 0.32f, 0.18f, 1f);
                break;
            case FlowerKind.Violet:
            case FlowerKind.Crocus:
                leafColor = new Color(0.48f, 0.28f, 0.16f, 1f);
                break;
            default:
                leafColor = new Color(0.58f, 0.34f, 0.10f, 1f);
                break;
        }
    }

    private void ApplyKind(FlowerKind recipe)
    {
        stemHeight = 0.22f;
        stemWidth = 0.018f;
        stemColor = new Color(0.22f, 0.46f, 0.20f, 1f);
        leafCount = 2;
        leafSize = new Vector2(0.06f, 0.035f);
        leafColor = new Color(0.24f, 0.50f, 0.22f, 1f);
        bloomSize = new Vector2(0.08f, 0.07f);
        bloomColor = new Color(0.78f, 0.16f, 0.18f, 1f);
        diskSize = Vector2.zero;
        diskColor = new Color(0.90f, 0.78f, 0.20f, 1f);
        spikeCount = 0;

        switch (recipe)
        {
            case FlowerKind.Tulip:
                stemHeight = 0.28f;
                stemWidth = 0.016f;
                leafCount = 2;
                leafSize = new Vector2(0.05f, 0.10f);
                bloomSize = new Vector2(0.07f, 0.10f);
                bloomColor = new Color(0.86f, 0.28f, 0.38f, 1f);
                break;
            case FlowerKind.Daisy:
                stemHeight = 0.20f;
                leafCount = 3;
                bloomSize = new Vector2(0.10f, 0.10f);
                bloomColor = new Color(0.96f, 0.96f, 0.92f, 1f);
                diskSize = new Vector2(0.035f, 0.035f);
                diskColor = new Color(0.92f, 0.74f, 0.16f, 1f);
                break;
            case FlowerKind.Poppy:
                stemHeight = 0.26f;
                stemWidth = 0.014f;
                leafCount = 2;
                leafSize = new Vector2(0.07f, 0.04f);
                bloomSize = new Vector2(0.11f, 0.08f);
                bloomColor = new Color(0.86f, 0.12f, 0.10f, 1f);
                diskSize = new Vector2(0.03f, 0.03f);
                diskColor = new Color(0.12f, 0.10f, 0.10f, 1f);
                break;
            case FlowerKind.Sunflower:
                stemHeight = 0.42f;
                stemWidth = 0.028f;
                leafCount = 4;
                leafSize = new Vector2(0.09f, 0.05f);
                bloomSize = new Vector2(0.16f, 0.16f);
                bloomColor = new Color(0.94f, 0.78f, 0.14f, 1f);
                diskSize = new Vector2(0.07f, 0.07f);
                diskColor = new Color(0.36f, 0.22f, 0.08f, 1f);
                break;
            case FlowerKind.Lily:
                stemHeight = 0.30f;
                leafCount = 2;
                leafSize = new Vector2(0.05f, 0.09f);
                bloomSize = new Vector2(0.09f, 0.11f);
                bloomColor = new Color(0.96f, 0.88f, 0.90f, 1f);
                break;
            case FlowerKind.Daffodil:
                stemHeight = 0.18f;
                leafCount = 2;
                leafSize = new Vector2(0.04f, 0.09f);
                bloomSize = new Vector2(0.08f, 0.07f);
                bloomColor = new Color(0.96f, 0.82f, 0.18f, 1f);
                diskSize = new Vector2(0.04f, 0.045f);
                diskColor = new Color(0.94f, 0.62f, 0.10f, 1f);
                break;
            case FlowerKind.Violet:
                stemHeight = 0.10f;
                stemWidth = 0.012f;
                leafCount = 2;
                leafSize = new Vector2(0.05f, 0.03f);
                bloomSize = new Vector2(0.055f, 0.05f);
                bloomColor = new Color(0.42f, 0.22f, 0.62f, 1f);
                diskSize = new Vector2(0.018f, 0.018f);
                diskColor = new Color(0.90f, 0.78f, 0.30f, 1f);
                break;
            case FlowerKind.Crocus:
                stemHeight = 0.09f;
                stemWidth = 0.014f;
                leafCount = 2;
                leafSize = new Vector2(0.03f, 0.08f);
                bloomSize = new Vector2(0.05f, 0.07f);
                bloomColor = new Color(0.62f, 0.38f, 0.82f, 1f);
                break;
            case FlowerKind.Lavender:
                stemHeight = 0.24f;
                stemWidth = 0.012f;
                leafCount = 4;
                leafSize = new Vector2(0.035f, 0.018f);
                bloomSize = new Vector2(0.028f, 0.05f);
                bloomColor = new Color(0.52f, 0.36f, 0.72f, 1f);
                diskColor = new Color(0.40f, 0.24f, 0.58f, 1f);
                spikeCount = 4;
                break;
            case FlowerKind.Iris:
                stemHeight = 0.27f;
                leafCount = 2;
                leafSize = new Vector2(0.04f, 0.12f);
                bloomSize = new Vector2(0.09f, 0.08f);
                bloomColor = new Color(0.36f, 0.28f, 0.72f, 1f);
                diskSize = new Vector2(0.03f, 0.04f);
                diskColor = new Color(0.78f, 0.62f, 0.22f, 1f);
                break;
            case FlowerKind.Bluebell:
                stemHeight = 0.16f;
                stemWidth = 0.013f;
                leafCount = 2;
                leafSize = new Vector2(0.04f, 0.07f);
                bloomSize = new Vector2(0.05f, 0.07f);
                bloomColor = new Color(0.38f, 0.48f, 0.86f, 1f);
                break;
        }
    }
}
