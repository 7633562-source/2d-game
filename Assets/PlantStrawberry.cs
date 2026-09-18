using UnityEngine;

// Low berry bush, not a tree and not a garden flower.
// Picture stems/leaves; berries are edible (Damageable + trigger).
// No hinges, no wind, no BirdPerch — a crow does not sit here.
public class PlantStrawberry : MonoBehaviour
{
    [Header("Build")]
    public int seed = 1;
    [Tooltip("How many stems fan from the stump. Picture only.")]
    public int stemCount = 3;
    public float stemHeight = 0.12f;
    public float stemWidth = 0.014f;
    public int leafCount = 6;
    public Vector2 leafSize = new Vector2(0.05f, 0.028f);

    [Header("Berries")]
    [Tooltip("Edible fruit. BeakStrike already Hurts any Damageable it overlaps.")]
    public int berryCount = 5;
    public Vector2 berrySize = new Vector2(0.028f, 0.024f);
    public float berryHealth = 1f;

    [Header("Season — picture only")]
    public PlantSeason season = PlantSeason.Summer;
    public PlantSeason ResolvedSeason { get; private set; }

    public int PartCount { get; private set; }
    public int BerryCount { get; private set; }
    public int EatenCount { get; private set; }
    public float BushHeight { get; private set; }

    private static readonly Color StemColor = new Color(0.28f, 0.52f, 0.22f, 1f);
    private static readonly Color LeafColor = new Color(0.30f, 0.58f, 0.24f, 1f);
    private static readonly Color BerryColor = new Color(0.86f, 0.14f, 0.22f, 1f);

    public static float RootY(float groundY = -2f)
    {
        return groundY;
    }

    public void BuildBush()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        if (HeadlessTrial.Active)
        {
            string seasonArg = PlantTree.ReadPlantArg("-berrySeason");
            if (seasonArg.Length == 0)
                seasonArg = PlantTree.ReadPlantArg("-flowerSeason");
            if (seasonArg.Length == 0)
                seasonArg = PlantTree.ReadPlantArg("-treeSeason");
            if (seasonArg.Length > 0)
                season = PlantTree.ParseSeason(seasonArg);
        }

        PartCount = 0;
        BerryCount = 0;
        EatenCount = 0;
        BushHeight = stemHeight;

        int stems = Mathf.Clamp(stemCount, 1, 8);
        int leaves = Mathf.Max(0, leafCount);
        int berries = Mathf.Max(0, berryCount);

        HumanSegment[] stemSegs = new HumanSegment[stems];
        float[] stemHeadings = new float[stems];
        Color stemTint = StemColor;
        Color leafTint = LeafColor;
        Color berryTint = BerryColor;
        ApplySeasonTint(ref stemTint, ref leafTint, ref berryTint);
        for (int i = 0; i < stems; i++)
        {
            float t = stems == 1 ? 0.5f : i / (float)(stems - 1);
            float heading = (t - 0.5f) * 46f;
            HumanSegment stem = AddPart("Stem_" + i, new Vector2(stemWidth, stemHeight), stemTint, 0);
            stem.transform.localPosition = new Vector3(
                (t - 0.5f) * 0.04f,
                stemHeight * 0.5f,
                0f);
            stem.transform.localRotation = Quaternion.Euler(0f, 0f, heading);
            PlantTree.PaintVisualTaper(stem, stemWidth, stemHeight, stemTint, 0.70f, 1);
            if (ResolvedSeason == PlantSeason.Winter)
            {
                PaintWinterStemTip(stem);
                PaintWinterStemBud(stem);
            }
            else
                PaintSideShoot(stem, (i % 2 == 0) ? -1f : 1f, stemTint, leafTint);
            stemSegs[i] = stem;
            stemHeadings[i] = heading;
        }

        // Winter is a tapered stick plus a bud. Do not spawn hidden
        // leaflets — summer berry/leaf counts stay.
        if (ResolvedSeason != PlantSeason.Winter)
        {
            int nBushLeaf = leaves;
            if (ResolvedSeason == PlantSeason.Autumn && nBushLeaf > 3)
                nBushLeaf -= 2;
            for (int i = 0; i < nBushLeaf; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                HumanSegment stem = stemSegs[i % stems];
                float alongLeaf = 0.22f + 0.12f * (i / stems);
                float stemAt = Mathf.Lerp(stemWidth, stemWidth * 0.70f, Mathf.Clamp01(alongLeaf));
                Color leafletTint = (i & 1) == 0
                    ? leafTint
                    : Color.Lerp(leafTint, stemTint, 0.22f);
                HumanSegment leaf = AddPart("Leaf_" + i, leafSize, leafletTint, 1);
                leaf.transform.SetParent(stem.transform, false);
                leaf.transform.localPosition = new Vector3(
                    side * (stemAt * 0.5f + leafSize.x * 0.30f),
                    (Mathf.Clamp01(alongLeaf) - 0.5f) * stemHeight,
                    0f);
                // Autumn spent hang. Summer 48° stays. Not a hinge.
                // Tree TuftFill / bark 0.04 left berry at +42 still
                // summer-tight beside garden +38. +44 reads spent.
                // Spread runners flatten the trifoliate toward world-up
                // so a 23° stem is a canopy, not a flag on the stick.
                // SpreadLimbDeg matches oak leader.
                float hang = ResolvedSeason == PlantSeason.Autumn ? 44f : 0f;
                float stemH = stemHeadings[i % stems];
                float flatten = Mathf.Abs(stemH) > PlantTree.SpreadLimbDeg
                    ? -stemH * 0.70f
                    : 0f;
                leaf.transform.localRotation = Quaternion.Euler(
                    0f, 0f, side * (48f + hang) + flatten);
                PaintLeaflets(leaf, leafletTint, side, stemAt);
                if (leaf.sprite != null)
                    leaf.sprite.enabled = false;
            }
        }

        if (ResolvedSeason == PlantSeason.Spring)
            PaintSpringBloom(stemSegs);

        // Spring is bloom. Ripe fruit with the flush is the orchard
        // bauble analog. Summer carries the crop; autumn keeps three.
        if (ResolvedSeason == PlantSeason.Summer
            || ResolvedSeason == PlantSeason.Autumn)
        {
            System.Random rng = new System.Random(seed);
            for (int i = 0; i < berries; i++)
            {
                if (ResolvedSeason == PlantSeason.Autumn && i >= 3)
                    continue;
                HumanSegment stem = stemSegs[i % stems];
                // Two nodes on the stem, not a comb. Same berry count.
                int node = (i / stems) & 1;
                float along = node == 0 ? 0.40f : 0.58f;
                float side = (i % 2 == 0) ? -1f : 1f;
                if (rng.NextDouble() < 0.5)
                    side = -side;
                float stemAt = Mathf.Lerp(stemWidth, stemWidth * 0.70f, Mathf.Clamp01(along));
                float hang = berrySize.x * (0.32f + 0.10f * (i % 3));
                Vector3 down = stem.transform.InverseTransformDirection(Vector3.down);
                float alongY = (Mathf.Clamp01(along) - 0.5f) * stemHeight;
                float drop = stemAt * 0.5f + hang;
                // Center-origin stem. stemHeight*along sat past the tip
                // (root-origin analog). Hang world-down so a 23° runner
                // carries a berry, not a flag. Side stagger stays.
                Vector3 local = new Vector3(
                    down.x * drop + side * hang * 0.35f,
                    alongY + down.y * drop,
                    0f);
                StrawberryBerry.Attach(this, stem.transform, local, berrySize, berryHealth, berryTint, i);
                BerryCount++;
            }
        }
    }

    public void NotifyBerryEaten()
    {
        EatenCount++;
        BerryCount = Mathf.Max(0, BerryCount - 1);
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

    // Winter rime on the stem tip. Not HumanSegment — PartCount stays stems+leaves.
    private void PaintWinterStemTip(HumanSegment stem)
    {
        if (ReferenceEquals(stem, null) || ReferenceEquals(stem.visual, null))
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        GameObject go = new GameObject("Frost");
        go.transform.SetParent(stem.visual, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        float tipW = stemWidth * 0.70f;
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(tipW * 0.70f, stemHeight * 0.10f),
            HumanVisualShape.Box,
            null,
            new Color(0.78f, 0.80f, 0.82f, 0.75f),
            stem.sortingOrder + 1);
        // 1.20 at +0.22 inner −0.38 — a frost table on the runner
        // (flower Frost analog). Right chip. PartCount stays stems.
        go.transform.localPosition = new Vector3(tipW * 0.55f, stemHeight * 0.42f, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
        GameObject frostL = new GameObject("FrostL");
        frostL.transform.SetParent(stem.visual, false);
        SpriteRenderer frostLSr = frostL.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            frostLSr,
            new Vector2(tipW * 0.62f, stemHeight * 0.08f),
            HumanVisualShape.Box,
            null,
            new Color(0.78f, 0.80f, 0.82f, 0.70f),
            stem.sortingOrder + 1);
        // Right-only Frost was a one-tent runner after WinterBudL
        // (flower Frost before FrostL). Far chip. PartCount stays stems.
        frostL.transform.localPosition = new Vector3(-tipW * 0.52f, stemHeight * 0.40f, 0f);
        frostL.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
        GameObject frostTip = new GameObject("FrostTip");
        frostTip.transform.SetParent(stem.visual, false);
        SpriteRenderer frostTipSr = frostTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            frostTipSr,
            new Vector2(tipW * 0.38f, stemHeight * 0.055f),
            HumanVisualShape.Box,
            null,
            new Color(0.80f, 0.82f, 0.84f, 0.72f),
            stem.sortingOrder + 1);
        // Frost/FrostL without a distal lip stayed a frost table on
        // the runner (flower Frost before FrostTip). Hang off the chip.
        // PartCount stays stems.
        frostTip.transform.localPosition = new Vector3(tipW * 0.72f, stemHeight * 0.46f, 0f);
        frostTip.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        GameObject frostLTip = new GameObject("FrostLTip");
        frostLTip.transform.SetParent(stem.visual, false);
        SpriteRenderer frostLTipSr = frostLTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            frostLTipSr,
            new Vector2(tipW * 0.34f, stemHeight * 0.048f),
            HumanVisualShape.Box,
            null,
            new Color(0.80f, 0.82f, 0.84f, 0.68f),
            stem.sortingOrder + 1);
        frostLTip.transform.localPosition = new Vector3(-tipW * 0.68f, stemHeight * 0.44f, 0f);
        frostLTip.transform.localRotation = Quaternion.Euler(0f, 0f, -16f);
    }

    // Spent axillary bud. Frost alone left a bare runner. Not
    // HumanSegment — PartCount stays stems. Same language as the flower.
    private void PaintWinterStemBud(HumanSegment stem)
    {
        if (ReferenceEquals(stem, null) || ReferenceEquals(stem.visual, null))
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        float tipW = stemWidth * 0.70f;
        Color bud = new Color(0.34f, 0.28f, 0.18f, 1f);
        GameObject go = new GameObject("WinterBud");
        go.transform.SetParent(stem.visual, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(Mathf.Max(0.010f, tipW * 0.80f), stemHeight * 0.12f),
            HumanVisualShape.Box,
            null,
            bud,
            stem.sortingOrder + 1);
        // 1.30 at +0.40 inner −0.25 — a bud sausage on the runner
        // (flower WinterBud analog). Right pad. PartCount stays stems.
        go.transform.localPosition = new Vector3(tipW * 0.55f, stemHeight * 0.28f, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 26f);
        GameObject tip = new GameObject("WinterBudTip");
        tip.transform.SetParent(stem.visual, false);
        SpriteRenderer tipSr = tip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            tipSr,
            new Vector2(Mathf.Max(0.008f, tipW * 0.75f), stemHeight * 0.07f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(bud, Color.white, 0.10f),
            stem.sortingOrder + 1);
        tip.transform.localPosition = new Vector3(tipW * 0.58f, stemHeight * 0.34f, 0f);
        tip.transform.localRotation = Quaternion.Euler(0f, 0f, 34f);
        GameObject budL = new GameObject("WinterBudL");
        budL.transform.SetParent(stem.visual, false);
        SpriteRenderer budLSr = budL.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            budLSr,
            new Vector2(Mathf.Max(0.010f, tipW * 0.72f), stemHeight * 0.10f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(bud, new Color(0.28f, 0.22f, 0.14f, 1f), 0.20f),
            stem.sortingOrder + 1);
        // Right-only WinterBud was a one-tent runner after summer
        // MidR (flower WinterBud before WinterBudL). Far bud.
        // PartCount stays stems.
        budL.transform.localPosition = new Vector3(-tipW * 0.52f, stemHeight * 0.26f, 0f);
        budL.transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
        GameObject tipL = new GameObject("WinterBudTipL");
        tipL.transform.SetParent(stem.visual, false);
        SpriteRenderer tipLSr = tipL.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            tipLSr,
            new Vector2(Mathf.Max(0.008f, tipW * 0.68f), stemHeight * 0.06f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(bud, Color.white, 0.08f),
            stem.sortingOrder + 1);
        tipL.transform.localPosition = new Vector3(-tipW * 0.52f, stemHeight * 0.32f, 0f);
        tipL.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
    }

    // Picture-only side shoot so a stem reads as a branched runner,
    // not a lollipop stick. Not HumanSegment — PartCount stays stems+leaves.
    // leafletTint is the seasonal leaf so autumn runners are not lime.
    private void PaintSideShoot(HumanSegment stem, float side, Color tint, Color leafletTint)
    {
        if (ReferenceEquals(stem, null) || ReferenceEquals(stem.visual, null))
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;

        GameObject go = new GameObject("SideShoot");
        go.transform.SetParent(stem.visual, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(stemWidth * 0.72f, stemHeight * 0.32f),
            HumanVisualShape.Box,
            null,
            tint,
            stem.sortingOrder);
        go.transform.localPosition = new Vector3(
            side * stemWidth * 0.40f,
            -stemHeight * 0.06f,
            0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, side * 32f);
        GameObject runner = new GameObject("RunnerTip");
        runner.transform.SetParent(stem.visual, false);
        SpriteRenderer runnerSr = runner.AddComponent<SpriteRenderer>();
        // 0.90-wide tip was a second fat stick (tree spur box analog).
        // Narrower chip tapers the runner. stemWidth stays.
        HumanSegment.ApplyPicture(
            runnerSr,
            new Vector2(stemWidth * 0.48f, stemHeight * 0.18f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tint, leafletTint, 0.55f),
            stem.sortingOrder);
        runner.transform.localPosition = new Vector3(
            side * stemWidth * 0.70f,
            stemHeight * 0.04f,
            0f);
        runner.transform.localRotation = Quaternion.Euler(0f, 0f, side * 48f);
        GameObject runnerLeaf = new GameObject("RunnerLeaf");
        runnerLeaf.transform.SetParent(stem.visual, false);
        SpriteRenderer runnerLeafSr = runnerLeaf.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            runnerLeafSr,
            new Vector2(leafSize.x * 0.32f, leafSize.y * 0.40f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tint, leafletTint, 0.70f),
            stem.sortingOrder);
        runnerLeaf.transform.localPosition = new Vector3(
            side * stemWidth * 0.92f,
            stemHeight * 0.10f,
            0f);
        runnerLeaf.transform.localRotation = Quaternion.Euler(0f, 0f, side * 28f);
        GameObject runnerLeafTip = new GameObject("RunnerLeafTip");
        runnerLeafTip.transform.SetParent(stem.visual, false);
        SpriteRenderer runnerLeafTipSr = runnerLeafTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            runnerLeafTipSr,
            new Vector2(leafSize.x * 0.16f, leafSize.y * 0.18f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tint, leafletTint, 0.70f),
            stem.sortingOrder);
        // RunnerLeaf without a distal lip stayed a trifoliate card
        // after LeafletTip (garden Blade before BladeTip). Hang off
        // the outer shoot; stemWidth / leafSize stay.
        runnerLeafTip.transform.localPosition = new Vector3(
            side * stemWidth * 1.08f,
            stemHeight * 0.16f,
            0f);
        runnerLeafTip.transform.localRotation = Quaternion.Euler(0f, 0f, side * 34f);
        GameObject runnerLeaf2 = new GameObject("RunnerLeaf2");
        runnerLeaf2.transform.SetParent(stem.visual, false);
        SpriteRenderer runnerLeaf2Sr = runnerLeaf2.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            runnerLeaf2Sr,
            new Vector2(leafSize.x * 0.24f, leafSize.y * 0.32f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(leafletTint, tint, 0.30f),
            stem.sortingOrder);
        runnerLeaf2.transform.localPosition = new Vector3(
            side * stemWidth * 0.78f,
            stemHeight * 0.02f,
            0f);
        runnerLeaf2.transform.localRotation = Quaternion.Euler(0f, 0f, side * 52f);
        GameObject runnerLeaf2Tip = new GameObject("RunnerLeaf2Tip");
        runnerLeaf2Tip.transform.SetParent(stem.visual, false);
        SpriteRenderer runnerLeaf2TipSr = runnerLeaf2Tip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            runnerLeaf2TipSr,
            new Vector2(leafSize.x * 0.12f, leafSize.y * 0.14f),
            HumanVisualShape.Box,
            null,
            Color.Lerp(leafletTint, tint, 0.30f),
            stem.sortingOrder);
        // RunnerLeafTip closed the outer shoot. RunnerLeaf2 without
        // a lip stayed a caudal trifoliate card (garden Blade before
        // BladeTip). Hang off the inner leaflet; stemWidth stays.
        runnerLeaf2Tip.transform.localPosition = new Vector3(
            side * stemWidth * 0.92f,
            stemHeight * 0.08f,
            0f);
        runnerLeaf2Tip.transform.localRotation = Quaternion.Euler(0f, 0f, side * 58f);
    }

    // Terminal leaflet plus two laterals so the bush is trifoliate,
    // not a pair of side cards. Body sprite stays off. Not HumanSegment
    // — PartCount stays stems+leaves.
    private void PaintLeaflets(HumanSegment leaf, Color tint, float side, float stemAt)
    {
        if (ReferenceEquals(leaf, null) || ReferenceEquals(leaf.visual, null))
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;

        Vector2 term = new Vector2(leafSize.x * 0.28f, leafSize.y * 0.50f);
        Transform stemXform = leaf.transform.parent;
        Vector3 stemJoin = new Vector3(side * stemAt * 0.5f, leaf.transform.localPosition.y, 0f);
        Vector3 stemWorld = stemXform != null
            ? stemXform.TransformPoint(stemJoin)
            : leaf.transform.TransformPoint(new Vector3(-side * leafSize.x * 0.30f, 0f, 0f));
        Vector3 stemVisual = leaf.visual.InverseTransformPoint(stemWorld);
        float petioleReach = Mathf.Sqrt(stemVisual.x * stemVisual.x + stemVisual.y * stemVisual.y);
        GameObject petiole = new GameObject("Petiole");
        petiole.transform.SetParent(leaf.visual, false);
        SpriteRenderer petioleSr = petiole.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            petioleSr,
            new Vector2(
                Mathf.Max(0.004f, leafSize.x * 0.14f),
                Mathf.Max(0.008f, petioleReach * 0.72f)),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tint, new Color(0.32f, 0.26f, 0.14f, 1f), 0.35f),
            leaf.sortingOrder);
        PlantTree.AimPictureAlongY(petiole.transform, stemVisual);
        GameObject petioleTip = new GameObject("PetioleTip");
        petioleTip.transform.SetParent(petiole.transform, false);
        SpriteRenderer petioleTipSr = petioleTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            petioleTipSr,
            new Vector2(
                Mathf.Max(0.003f, leafSize.x * 0.08f),
                Mathf.Max(0.005f, petioleReach * 0.22f)),
            HumanVisualShape.Box,
            null,
            Color.Lerp(tint, new Color(0.32f, 0.26f, 0.14f, 1f), 0.35f),
            leaf.sortingOrder);
        // Garden PetioleTip closed the stalk. Berry Petiole without
        // a lip stayed a trifoliate stick after LeafletMidTip
        // (CarmB before CarmBTip). Hang along +Y; leafSize stays.
        petioleTip.transform.localPosition = new Vector3(0f, petioleReach * 0.28f, 0f);

        GameObject mid = new GameObject("LeafletMid");
        mid.transform.SetParent(leaf.visual, false);
        SpriteRenderer midSr = mid.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            midSr,
            term,
            HumanVisualShape.Box,
            null,
            ResolvedSeason == PlantSeason.Spring
                ? Color.Lerp(tint, new Color(0.50f, 0.78f, 0.28f, 1f), 0.18f)
                : (ResolvedSeason == PlantSeason.Autumn
                    ? Color.Lerp(tint, new Color(0.78f, 0.38f, 0.08f, 1f), 0.32f)
                    : tint),
            leaf.sortingOrder);
        // −0.16 still sat in the sinus between laterals at ±0.28
        // after Waist left the same pad (plum Notch analog). −0.28
        // × 0.36 inner −0.10 flooded the notch (garden Blade analog).
        // Mid rides Leaflet0; laterals |x| 0.42 × 0.32.
        // leafSize stays.
        mid.transform.localPosition = new Vector3(
            -leafSize.x * 0.42f,
            leafSize.y * 0.12f,
            0f);

        GameObject midWaist = new GameObject("LeafletMidWaist");
        midWaist.transform.SetParent(leaf.visual, false);
        SpriteRenderer midWaistSr = midWaist.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            midWaistSr,
            new Vector2(leafSize.x * 0.24f, leafSize.y * 0.32f),
            HumanVisualShape.Box,
            null,
            ResolvedSeason == PlantSeason.Autumn
                ? Color.Lerp(tint, new Color(0.86f, 0.62f, 0.12f, 1f), 0.22f)
                : tint,
            leaf.sortingOrder);
        midWaist.transform.localPosition = new Vector3(
            -leafSize.x * 0.42f,
            leafSize.y * 0.02f,
            0f);
        GameObject midWaistTip = new GameObject("LeafletMidWaistTip");
        midWaistTip.transform.SetParent(leaf.visual, false);
        SpriteRenderer midWaistTipSr = midWaistTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            midWaistTipSr,
            new Vector2(leafSize.x * 0.12f, leafSize.y * 0.12f),
            HumanVisualShape.Box,
            null,
            ResolvedSeason == PlantSeason.Autumn
                ? Color.Lerp(tint, new Color(0.86f, 0.62f, 0.12f, 1f), 0.22f)
                : tint,
            leaf.sortingOrder);
        // MidWaist without a distal lip stayed a trifoliate card
        // after LeafletMidTip (garden Blade before BladeTip). Hang
        // off Leaflet0; leafSize stays.
        midWaistTip.transform.localPosition = new Vector3(
            -leafSize.x * 0.42f,
            leafSize.y * 0.16f,
            0f);
        GameObject midTip = new GameObject("LeafletMidTip");
        midTip.transform.SetParent(leaf.visual, false);
        SpriteRenderer midTipSr = midTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            midTipSr,
            new Vector2(leafSize.x * 0.16f, leafSize.y * 0.22f),
            HumanVisualShape.Box,
            null,
            ResolvedSeason == PlantSeason.Spring
                ? Color.Lerp(tint, new Color(0.50f, 0.78f, 0.28f, 1f), 0.18f)
                : (ResolvedSeason == PlantSeason.Autumn
                    ? Color.Lerp(tint, new Color(0.78f, 0.38f, 0.08f, 1f), 0.32f)
                    : tint),
            leaf.sortingOrder);
        // Mid without a distal lip stayed a trifoliate card after
        // FrostTip (garden Blade before BladeTip). Hang off Leaflet0;
        // leafSize stays.
        midTip.transform.localPosition = new Vector3(
            -leafSize.x * 0.42f,
            leafSize.y * 0.28f,
            0f);
        GameObject midTipTip = new GameObject("LeafletMidTipTip");
        midTipTip.transform.SetParent(leaf.visual, false);
        SpriteRenderer midTipTipSr = midTipTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            midTipTipSr,
            new Vector2(leafSize.x * 0.10f, leafSize.y * 0.12f),
            HumanVisualShape.Box,
            null,
            ResolvedSeason == PlantSeason.Spring
                ? Color.Lerp(tint, new Color(0.50f, 0.78f, 0.28f, 1f), 0.18f)
                : (ResolvedSeason == PlantSeason.Autumn
                    ? Color.Lerp(tint, new Color(0.78f, 0.38f, 0.08f, 1f), 0.32f)
                    : tint),
            leaf.sortingOrder);
        // Garden TipTip closed the cup peak. Berry MidTip sat on the
        // 0.50 card (tree Tip after BladeTip). Hang off Leaflet0;
        // leafSize stays.
        midTipTip.transform.localPosition = new Vector3(
            -leafSize.x * 0.42f,
            leafSize.y * 0.40f,
            0f);
        GameObject midR = new GameObject("LeafletMidR");
        midR.transform.SetParent(leaf.visual, false);
        SpriteRenderer midRSr = midR.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            midRSr,
            new Vector2(leafSize.x * 0.24f, leafSize.y * 0.44f),
            HumanVisualShape.Box,
            null,
            ResolvedSeason == PlantSeason.Spring
                ? Color.Lerp(tint, new Color(0.42f, 0.70f, 0.22f, 1f), 0.14f)
                : Color.Lerp(tint, new Color(0.22f, 0.46f, 0.16f, 1f), 0.18f),
            leaf.sortingOrder);
        // Left-only Mid was a one-tent trifoliate after laterals at
        // ±0.42 (tree Apex before ApexR). Far peak rides Leaflet1;
        // sinus stays empty. leafSize stays.
        midR.transform.localPosition = new Vector3(
            leafSize.x * 0.42f,
            leafSize.y * 0.10f,
            0f);
        GameObject midWaistR = new GameObject("LeafletMidWaistR");
        midWaistR.transform.SetParent(leaf.visual, false);
        SpriteRenderer midWaistRSr = midWaistR.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            midWaistRSr,
            new Vector2(leafSize.x * 0.22f, leafSize.y * 0.28f),
            HumanVisualShape.Box,
            null,
            ResolvedSeason == PlantSeason.Autumn
                ? Color.Lerp(tint, new Color(0.72f, 0.48f, 0.10f, 1f), 0.18f)
                : Color.Lerp(tint, new Color(0.22f, 0.46f, 0.16f, 1f), 0.12f),
            leaf.sortingOrder);
        // Left-only MidWaist was a one-tent trifoliate stalk after
        // MidR (garden Blade before BladeR). Far waist rides Leaflet1;
        // sinus stays empty. leafSize stays.
        midWaistR.transform.localPosition = new Vector3(
            leafSize.x * 0.42f,
            leafSize.y * 0.02f,
            0f);
        GameObject midWaistRTip = new GameObject("LeafletMidWaistRTip");
        midWaistRTip.transform.SetParent(leaf.visual, false);
        SpriteRenderer midWaistRTipSr = midWaistRTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            midWaistRTipSr,
            new Vector2(leafSize.x * 0.10f, leafSize.y * 0.10f),
            HumanVisualShape.Box,
            null,
            ResolvedSeason == PlantSeason.Autumn
                ? Color.Lerp(tint, new Color(0.72f, 0.48f, 0.10f, 1f), 0.18f)
                : Color.Lerp(tint, new Color(0.22f, 0.46f, 0.16f, 1f), 0.12f),
            leaf.sortingOrder);
        midWaistRTip.transform.localPosition = new Vector3(
            leafSize.x * 0.42f,
            leafSize.y * 0.14f,
            0f);
        GameObject midRTip = new GameObject("LeafletMidRTip");
        midRTip.transform.SetParent(leaf.visual, false);
        SpriteRenderer midRTipSr = midRTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            midRTipSr,
            new Vector2(leafSize.x * 0.14f, leafSize.y * 0.20f),
            HumanVisualShape.Box,
            null,
            ResolvedSeason == PlantSeason.Spring
                ? Color.Lerp(tint, new Color(0.42f, 0.70f, 0.22f, 1f), 0.14f)
                : Color.Lerp(tint, new Color(0.22f, 0.46f, 0.16f, 1f), 0.18f),
            leaf.sortingOrder);
        midRTip.transform.localPosition = new Vector3(
            leafSize.x * 0.42f,
            leafSize.y * 0.24f,
            0f);
        GameObject midRTipTip = new GameObject("LeafletMidRTipTip");
        midRTipTip.transform.SetParent(leaf.visual, false);
        SpriteRenderer midRTipTipSr = midRTipTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            midRTipTipSr,
            new Vector2(leafSize.x * 0.08f, leafSize.y * 0.10f),
            HumanVisualShape.Box,
            null,
            ResolvedSeason == PlantSeason.Spring
                ? Color.Lerp(tint, new Color(0.42f, 0.70f, 0.22f, 1f), 0.14f)
                : Color.Lerp(tint, new Color(0.22f, 0.46f, 0.16f, 1f), 0.18f),
            leaf.sortingOrder);
        // LeafletMidTipTip closed the near peak. Far MidRTip sat on
        // the 0.44 card (garden TipR after BladeRTip). Hang off
        // Leaflet1; sinus stays empty. leafSize stays.
        midRTipTip.transform.localPosition = new Vector3(
            leafSize.x * 0.42f,
            leafSize.y * 0.34f,
            0f);

        Vector2 size = new Vector2(leafSize.x * 0.32f, leafSize.y * 0.52f);
        for (int k = 0; k < 2; k++)
        {
            float sign = k == 0 ? -1f : 1f;
            GameObject go = new GameObject("Leaflet" + k);
            go.transform.SetParent(leaf.visual, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            Color leafletTint = tint;
            if (ResolvedSeason == PlantSeason.Autumn && k == 0)
                leafletTint = Color.Lerp(tint, new Color(0.86f, 0.62f, 0.12f, 1f), 0.45f);
            else if (ResolvedSeason == PlantSeason.Summer && k == 1)
                leafletTint = Color.Lerp(tint, new Color(0.22f, 0.46f, 0.16f, 1f), 0.28f);
            else if (ResolvedSeason == PlantSeason.Spring && k == 1)
                leafletTint = Color.Lerp(tint, new Color(0.50f, 0.78f, 0.28f, 1f), 0.30f);
            HumanSegment.ApplyPicture(
                sr,
                size,
                HumanVisualShape.Box,
                null,
                leafletTint,
                leaf.sortingOrder);
            go.transform.localPosition = new Vector3(
                sign * leafSize.x * 0.42f,
                leafSize.y * (k == 0 ? -0.04f : 0.10f),
                0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, sign * side * 28f);
            GameObject leafletTip = new GameObject("LeafletTip" + k);
            leafletTip.transform.SetParent(go.transform, false);
            SpriteRenderer leafletTipSr = leafletTip.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                leafletTipSr,
                new Vector2(size.x * 0.48f, size.y * 0.28f),
                HumanVisualShape.Box,
                null,
                leafletTint,
                leaf.sortingOrder);
            // Leaflet box without a distal lip stayed a trifoliate
            // card after FrostTip (garden Leaflet before LeafletTip).
            // Hang off the outer end; leafSize stays.
            leafletTip.transform.localPosition = new Vector3(0f, size.y * 0.42f, 0f);
        }
    }

    // Tiny white bloom in spring. Not HumanSegment — PartCount stays stems+leaves.
    private void PaintSpringBloom(HumanSegment[] stems)
    {
        if (stems == null || stems.Length == 0)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;

        Vector2 bloom = new Vector2(0.018f, 0.016f);
        Color petal = new Color(0.96f, 0.94f, 0.90f, 1f);
        int n = Mathf.Min(2, stems.Length);
        for (int bloomI = 0; bloomI < n; bloomI++)
        {
            HumanSegment stem = stems[bloomI];
            if (ReferenceEquals(stem, null))
                continue;
            GameObject go = new GameObject("SpringBloom" + bloomI);
            go.transform.SetParent(stem.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                sr,
                bloom,
                HumanVisualShape.Box,
                null,
                petal,
                stem.sortingOrder + 2);
            // Spread runners flatten the flush toward world-up so a
            // 23° stem is a canopy, not a flag on the stick. Same
            // 0.70 as the trifoliate. SpreadLimbDeg matches the tree
            // leader. Not a hinge.
            float stemH = Mathf.DeltaAngle(0f, stem.transform.localEulerAngles.z);
            float flatten = Mathf.Abs(stemH) > PlantTree.SpreadLimbDeg
                ? -stemH * 0.70f
                : 0f;
            go.transform.localPosition = new Vector3(
                (bloomI == 0 ? -1f : 1f) * (stemWidth * 0.8f),
                (0.68f - 0.5f) * stemHeight,
                0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, flatten);
            GameObject petal2 = new GameObject("SpringBloomPetal" + bloomI);
            petal2.transform.SetParent(go.transform, false);
            SpriteRenderer petal2Sr = petal2.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                petal2Sr,
                new Vector2(bloom.x * 0.72f, bloom.y * 0.80f),
                HumanVisualShape.Box,
                null,
                Color.Lerp(petal, new Color(0.90f, 0.72f, 0.78f, 1f), 0.28f),
                stem.sortingOrder + 2);
            petal2.transform.localPosition = new Vector3(
                // 0.72-wide petal at ±0.52 left a 0.30 bloom gap
                // before PetalR (pine Body analog). Width 0.72 kisses
                // x=0 from the near side. bloom stays.
                (bloomI == 0 ? -1f : 1f) * bloom.x * 0.36f,
                bloom.y * 0.12f,
                0f);
            petal2.transform.localRotation = Quaternion.Euler(
                0f, 0f, (bloomI == 0 ? -1f : 1f) * 18f);
            // Far half kisses x=0 of the bloom parent (pine BodyR).
            // bloom stays. Not HumanSegment.
            GameObject petalR = new GameObject("SpringBloomPetalR" + bloomI);
            petalR.transform.SetParent(go.transform, false);
            SpriteRenderer petalRSr = petalR.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                petalRSr,
                new Vector2(bloom.x * 0.68f, bloom.y * 0.72f),
                HumanVisualShape.Box,
                null,
                Color.Lerp(petal, new Color(0.98f, 0.90f, 0.92f, 1f), 0.22f),
                stem.sortingOrder + 2);
            petalR.transform.localPosition = new Vector3(
                (bloomI == 0 ? 1f : -1f) * bloom.x * 0.34f,
                bloom.y * 0.06f,
                0f);
            petalR.transform.localRotation = Quaternion.Euler(
                0f, 0f, (bloomI == 0 ? 1f : -1f) * 14f);
            GameObject petalTip = new GameObject("SpringBloomPetalTip" + bloomI);
            petalTip.transform.SetParent(petal2.transform, false);
            SpriteRenderer petalTipSr = petalTip.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                petalTipSr,
                new Vector2(bloom.x * 0.36f, bloom.y * 0.32f),
                HumanVisualShape.Box,
                null,
                Color.Lerp(petal, new Color(0.90f, 0.72f, 0.78f, 1f), 0.28f),
                stem.sortingOrder + 2);
            // Garden PetalTip closed the cup. Berry SpringBloomPetal
            // without a lip stayed a flush card after RunnerLeafTip
            // (tree packed Tooth before LobeL1). Hang off the near
            // half; bloom stays.
            petalTip.transform.localPosition = new Vector3(0f, bloom.y * 0.36f, 0f);
            GameObject petalRTip = new GameObject("SpringBloomPetalRTip" + bloomI);
            petalRTip.transform.SetParent(petalR.transform, false);
            SpriteRenderer petalRTipSr = petalRTip.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                petalRTipSr,
                new Vector2(bloom.x * 0.32f, bloom.y * 0.28f),
                HumanVisualShape.Box,
                null,
                Color.Lerp(petal, new Color(0.98f, 0.90f, 0.92f, 1f), 0.22f),
                stem.sortingOrder + 2);
            petalRTip.transform.localPosition = new Vector3(0f, bloom.y * 0.32f, 0f);
            // Tree bloom hides the grow box so petals own the head.
            // Parent 1.8 cm SpringBloom stayed a flush card behind
            // PetalTip (garden Bloom analog). Petals stay; bloom size
            // stays. Not HumanSegment.
            sr.enabled = false;
        }
    }

    private void ApplySeasonTint(ref Color stemTint, ref Color leafTint, ref Color berryTint)
    {
        ResolvedSeason = season;
        if (ResolvedSeason == PlantSeason.Auto)
            ResolvedSeason = (PlantSeason)((Mathf.Abs(seed) % 4) + 1);

        switch (ResolvedSeason)
        {
            case PlantSeason.Spring:
                leafTint = Color.Lerp(leafTint, new Color(0.40f, 0.72f, 0.26f, 1f), 0.58f);
                berryTint = Color.Lerp(berryTint, new Color(0.92f, 0.28f, 0.30f, 1f), 0.30f);
                stemTint = Color.Lerp(stemTint, new Color(0.32f, 0.58f, 0.24f, 1f), 0.22f);
                break;
            case PlantSeason.Autumn:
                // Tree autumn family gold/russet. 0.38/0.04 still
                // orange beside TuftFill / bark 0.04. Darker russet
                // matches spent garden hip 0.92.
                leafTint = new Color(0.32f, 0.03f, 0.02f, 1f);
                berryTint = new Color(0.22f, 0.02f, 0.02f, 1f);
                stemTint = Color.Lerp(stemTint, new Color(0.34f, 0.28f, 0.12f, 1f), 0.48f);
                break;
            case PlantSeason.Winter:
                // 0.40 toward green-grey left a summer runner. Wood
                // brown is the leafless cane. Berries stay off.
                stemTint = Color.Lerp(stemTint, new Color(0.30f, 0.26f, 0.18f, 1f), 0.70f);
                leafTint = new Color(0.34f, 0.26f, 0.12f, 1f);
                berryTint = Color.Lerp(berryTint, new Color(0.50f, 0.12f, 0.14f, 1f), 0.35f);
                break;
        }
    }
}
