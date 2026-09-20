using System.Collections.Generic;
using UnityEngine;

// Per-part real art (ART-1a). A part's picture is the shipping rectangle - the
// collider box or the renderer's own size - filled with the real texture. Size
// is never taken from localScale (project rule), and nothing here touches a
// Rigidbody2D, a collider, a joint or a mass: it is a SpriteRenderer redraw.
//
// A MATERIAL (bark, leaf) is a repeated surface: it keeps its authored world
// period and is always drawn Tiled, so a part smaller than one tile shows a
// CROP of the canvas at the canvas' own scale (ART-0 22nd pass). A PAINTED part
// (one canvas per bone) is drawn Sliced onto its box. That is the
// rectangle-equals-collider contract, not a missing texture: a missing texture
// is ArtLibrary.Albedo(key) == null and must be drawn as a labelled ArtMissing
// tile by the caller.
//
// The flower's leaflet is the one part the rig draws as a COLLAGE (16 flat cards
// under the leaf's Visual, the part's own sprite switched off). `DressFlower`
// takes that sprite back for a painted leaf-pair canvas and switches the cards
// off, keeping the two aimed `Petiole` cards - see DressFlowerLeaves (ART-0 gap 3b).
public static class ArtParts
{
    public const float BarkTileMeters = 0.5f;
    public const float LeafTileMeters = 0.5f;
    public const string FruitKey = "Apple/fruit";
    public const string BerryKey = "Strawberry/berry";
    public const string StrawberryLeafKey = "Strawberry/leaf";
    public const string StrawberryStemKey = "Strawberry/stem";

    // --- Flower parts (ART-0 gap 3, 20.09) --------------------------------------------
    // The one object that had no art at all: PlantFlower drew every petal, the distal
    // lip, the cup of a tulip, the sepals and the head plate as the flat collider box.
    // The canvases are LUMINANCE canvases (grey RGB, the shape lives in alpha), so the
    // part's own kind colour multiplies them: one canvas serves all twelve kinds, the
    // way one grass tile serves every tree. Canvas aspects match the BOX of a
    // representative kind (petal 0.63 = the rose/daisy petal box), and the box's own
    // aspect is what the part draws - a flower is 0.02-0.09 m, i.e. 10-30 px at the
    // gallery lens, so the silhouette and the shading ramp are what the eye gets.
    public const string FlowerPetalKey = "Flower/petal";
    public const string FlowerTipKey = "Flower/petal_tip";
    public const string FlowerCupKey = "Flower/cup";
    public const string FlowerCalyxKey = "Flower/calyx";
    public const string FlowerDiskKey = "Flower/disk";

    // --- Flower FOLIAGE (ART-0 gap 3b, 20.09) -----------------------------------------
    // One canvas per leaflet family, drawn in the leaf part's own box aspect and Sliced onto
    // that box - the painted-parts rule (one part, one PNG; the box is the mapping). The
    // rig paints a leaf as a COLLAGE of 16 flat cards (PlantFlower.PaintLeafMidrib: Blade,
    // BladeTip, Midrib, MidribTip, Petiole, PetioleTip, Tip, TipTip, TipR, TipRTip, BladeR,
    // BladeRTip, MidribR, MidribRTip, Leaflet0/1, LeafletTip0/1), each one flat tint with
    // hard corners, and the petals painted in the 20th pass next to them carry a ramp - so
    // the two halves of one plant no longer looked like the same drawing. The cards step
    // aside for the canvas; the two aimed `Petiole` cards stay, because their direction is
    // computed at build time from the stem join and a static canvas cannot aim.
    public const string FlowerLeafKey = "Flower/leaf";
    public const string FlowerLeafErectKey = "Flower/leaf_erect";

    // The family split, by the LEAF PART's own box aspect (`ApplyKind` gives each kind its
    // own leafSize): the broad leaflet pair is 1.667-1.944, the erect blade is 0.333-0.571,
    // a factor-3 gap, so one threshold separates them with room to spare. Each canvas is
    // drawn in the geometric mean box of its family: the worst aspect error in the set is
    // 1.364x (Iris), and `Tools/gen-flower-leaf-textures.py` prints the whole table.
    public const float LeafFamilyWide = 0.8f;

    // --- The tree's LEAF CARDS (ART-0 29th pass, 20.09) ----------------------------------
    // A leaf-named part of the TREE is dressed with the `leaf` MATERIAL - a 512 px canvas
    // `Tiled` at `LeafTileMeters` = 0.5 m, i.e. 1024 px/m, whose own leaves are 70-130 px =
    // 0.068-0.127 m long, one per 73 px cell. The rig's leaf parts (Art1Check INFO
    // `oak-leaf`, this pass) are 0.002-0.5 m and the ones the eye can see are 0.02-0.12 m,
    // so a 0.04 m part showed a 41 px window of a field whose leaves are 1.7-3x LONGER than
    // the part: `Tools/art0-leaf-window-probe.py` measures every such window at 87 % ink
    // with a CUT leaf on 100 % of its borders. That is a solid green plate edged by the
    // part's own rectangle - three vision reads of `art0leaf28_tree.png` named it as
    // "flat rectangular plates, hard stepped edges" in the crown and "flat green chips,
    // they do not read as oak leaves" on the trunk.
    //
    // So a leaf CARD (a part between TreeLeafCardMinMeters and TreeLeafCardMaxMeters, i.e.
    // a leaf-shaped thing of centimetres, NOT a surface) draws the painted leaf canvas
    // instead - one leaf in the part's own box, transparent margin, `Sliced` - which is the
    // flower's foliage mechanism (`Flower/leaf.png`, `Tools/gen-flower-leaf-textures.py`).
    // A part at or above TreeLeafCardMaxMeters keeps the material: at that size a window of
    // the field holds nine or more leaves and reads as the mass a crown is made of.
    // Below the MINIMUM the part is under 8 px at the closest lens the gallery has, and
    // the material's crop is a tone either way.
    public const string TreeLeafCardKey = "Tree/leaf_card";
    public const float TreeLeafCardMinMeters = 0.02f;
    public const float TreeLeafCardMaxMeters = 0.12f;

    // --- The lavender SPIKE and the autumn HIP (ART-0 gap 3b, the last two rows, 24th pass) -
    // Two more card collages, one canvas each, the same mechanism as the leaf:
    //   * the lavender spike (`spikeCount = 4` in ApplyKind) is a HumanSegment named "Spike_i"
    //     whose own sprite `PaintSpikeFlorets` / `PaintSpentSpike` switch OFF behind six flat
    //     cards (`Floret0/1/2` + `FloretTip0/1/2`, autumn: `SpentFloret*`);
    //   * the autumn seed head is the `Bloom` part of the other ELEVEN kinds, whose own sprite
    //     `PaintSpentBloom` switches OFF behind TWELVE flat cards (`Spent`, `SpentTip`,
    //     `SpentR`, `SpentRTip`, `SpentCap*`, `SpentCapFill*`).
    // Both canvases are drawn in the part's own box: the spike's box is `bloomSize` of one kind
    // (0.028 x 0.050 m, aspect 0.560) so its four parts are drawn EXACTLY, the hip's is the
    // geometric mean of its own family's live boxes (one canvas for all eleven kinds until the
    // 26th pass - see the three keys below).
    // The rig's `Sepal*` (calyx) cards and the spent `Disk` STAY: they are painted parts, not
    // cards, and a spent hip keeps its sepals.
    //
    // THREE canvases, not one (26th pass, 20.09). The boxes the dress READS are not the authored
    // ones: `HumanSegment.CreateBodySprite` snaps the placeholder to whole pixels at 256 px/m with
    // a 64 px minimum side and takes ppu from the width, so the drawn box aspect is an integer
    // pixel ratio. The eleven kinds' live boxes run 0.6957 (Tulip) .. 0.8205 (Lily) | 1.0000
    // (Daisy, Sunflower) .. 1.1562 (Iris) | 1.4062 (Poppy). ONE canvas cannot answer that: the
    // dress maps the canvas onto the box, so the box shapes the drawn form whatever the canvas'
    // aspect is, and one form painted at the same fractions of every box reads as a narrow hip on
    // a Tulip and a flat pancake on a Poppy - measured as a 1.536x / 1.320x error against the
    // round hip (`Tools/art0-hip-form-probe.py`, `Tools/art0-hip-lens-ab.py`). The split sits in
    // the two gaps of that set, and each canvas paints its form PRE-SQUASHED for its own family
    // (`gen-flower-spike-textures.py`'s `hip_form_scale` + `fitted_map`, which prints the whole
    // table): worst per-kind error 1.536x -> 1.116x (Lily, tall), 1.010x (Poppy), the round
    // family untouched at 1.077x. Each canvas is drawn in the geometric mean of ITS OWN family's
    // live boxes.
    //
    // The hip exists in AUTUMN only (`PlantFlower` line 222 `spentHead`): a spring or summer rig
    // draws the petal fan instead, so these eleven boxes are the whole set a hip canvas is ever
    // drawn in. The gate asserts that too - a spring rig takes no hip canvas.
    public const string FlowerSpikeKey = "Flower/spike";
    public const string FlowerHipKey = "Flower/hip";              // the round family (6 kinds)
    public const string FlowerHipTallKey = "Flower/hip_tall";     // Tulip, Lily, Crocus, Bluebell
    public const string FlowerHipWideKey = "Flower/hip_wide";     // Poppy alone: the flat bloom

    // The two thresholds, at the geometric middle of each gap in the live set:
    // sqrt(0.8205 * 1.0000) = 0.9058 and sqrt(1.1562 * 1.4062) = 1.2680. A box that drifts across
    // one only swaps one hip canvas for another - it never leaves the hip undressed.
    public const float HipFamilyTall = 0.906f;
    public const float HipFamilyWide = 1.268f;

    public static string HipKeyFor(float boxAspect)
    {
        if (boxAspect < HipFamilyTall)
            return FlowerHipTallKey;
        return boxAspect >= HipFamilyWide ? FlowerHipWideKey : FlowerHipKey;
    }

    // --- The STEM (ART-0 gap 3b's last flat element, 25th pass) -------------------------
    // `PlantFlower` line 128 builds ONE `HumanSegment` named "Stem" per plant and paints it with
    // `PlantTree.PaintVisualTaper(stem, stemWidth, stemHeight, stemColor, 0.62f, 1)` - one band,
    // so `PaintVisualConeFill` switches the part's own sprite OFF and draws six flat cards
    // (`ConeCore`, `ConeCoreTip`, `ConeEdgeL`, `ConeEdgeLTip`, `ConeEdgeR`, `ConeEdgeRTip`).
    // That cane is the last thing in the plant made of boxes (the 24th pass' 4x read named it
    // without being asked: "a perfectly straight, single-colour strip with no texture, shadow,
    // curve or detail"), and the canvas takes the part's own sprite slot back.
    //
    // This key is the only one of the family that needs TWO canvases: `ApplyKind` gives the twelve
    // kinds 0.012-0.028 m by 0.09-0.42 m, and the box the dress reads (`BoxOf` -> the placeholder
    // sprite's own size, `HumanSegment.CreateBodySprite`'s 64 px short-side boost) has an aspect
    // from 0.049 (Lavender) to 0.174 (Crocus) - a factor 3.5. One canvas there is the leaf set's
    // 1.364x compromise multiplied by 1.8, so the family splits the way `LeafFamilyWide` does, at
    // the two CORM kinds (Crocus 0.09 m, Violet 0.10 m: stubs on a corm, and the only boxes above
    // the 1.02-1.15x gap): stalk 1.521x worst (Daffodil), corm 1.228x worst.
    // The rig's WINTER chips (`Frost*`, `WinterBud*`) STAY: they are painted, tilted chips sitting
    // ON the cane, not the cane.
    public const string FlowerStemKey = "Flower/stem";
    public const string FlowerStemStoutKey = "Flower/stem_stout";

    // The family split by BOX ASPECT, at the only clean gap in the set: the twelve boxes run
    // 0.0492 (Lavender) .. 0.0980 (Daisy) .. 0.1087 (Daffodil) | 0.1153 (Violet) .. 0.1739
    // (Crocus), i.e. the threshold sits between Daffodil and Violet with a 2.9% margin on each
    // side in log space (sqrt(0.1087 * 0.1153) = 0.1120). The two kinds it catches are the CORM
    // ones - a stub peduncle on a corm, a drawing of its own - and the same factor-1.6 separation
    // `LeafFamilyWide` 0.8 uses. A box aspect that drifts across it only swaps one cane canvas for
    // the other.
    public const float StemFamilyStout = 0.112f;

    public static string StemKeyFor(float boxAspect)
    {
        return boxAspect >= StemFamilyStout ? FlowerStemStoutKey : FlowerStemKey;
    }

    public static readonly string[] FlowerKeys =
    {
        FlowerPetalKey, FlowerTipKey, FlowerCupKey, FlowerCalyxKey, FlowerDiskKey,
        FlowerLeafKey, FlowerLeafErectKey, FlowerSpikeKey,
        FlowerHipKey, FlowerHipTallKey, FlowerHipWideKey,
        FlowerStemKey, FlowerStemStoutKey
    };

    public static string FlowerLeafKeyFor(float boxAspect)
    {
        return boxAspect >= LeafFamilyWide ? FlowerLeafKey : FlowerLeafErectKey;
    }

    // A drawn part bigger than this is a unit mix-up, not a plant part.
    public const float MaxPartMeters = 40f;

    // Diagnostics for the capture report.
    public static int GiantSkips { get; private set; }
    public static int EmptySkips { get; private set; }
    public static int RevertedMoves { get; private set; }

    public static void ResetSkips()
    {
        GiantSkips = 0;
        EmptySkips = 0;
        RevertedMoves = 0;
    }

    private static string PathOf(SpriteRenderer sr)
    {
        Transform t = sr.transform;
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }

    private struct Saved
    {
        public Sprite sprite;
        public Color color;
        public SpriteDrawMode mode;
        public SpriteTileMode tileMode;
        public Vector2 size;
        public bool enabled;
        public bool valid;
    }

    // Keyed by the renderer itself, so the toggle is symmetric: real=false puts
    // back exactly the sprite the part was built with.
    private static readonly Dictionary<SpriteRenderer, Saved> Originals =
        new Dictionary<SpriteRenderer, Saved>();

    // The flower leaf collage's cards this dress switched OFF (everything under the leaf's
    // Visual except the aimed Petiole). Kept as a set, not as a flag on the card: the
    // toggle then re-enables exactly what it hid and nothing else.
    private static readonly HashSet<SpriteRenderer> HiddenCards =
        new HashSet<SpriteRenderer>();


    // Name -> art key for the plant family only. Creature and human parts are
    // NOT matched here on purpose: their binding is ART-1b / ART-2 / ART-3 and
    // their names (Head, Paw, Torso) collide with the plant words.
    public static string KeyForName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        if (Has(objectName, "Fruit") || Has(objectName, "Apple"))
            return FruitKey;
        if (Has(objectName, "Berry"))
            return BerryKey;
        if (Has(objectName, "Tuft") || Has(objectName, "Leaf") || Has(objectName, "Canopy")
            || Has(objectName, "Crown") || Has(objectName, "Pad") || Has(objectName, "Bloom"))
            return ArtLibrary.Leaf;
        if (Has(objectName, "Trunk") || Has(objectName, "Stump") || Has(objectName, "Wood")
            || Has(objectName, "Limb") || Has(objectName, "Branch") || Has(objectName, "Twig")
            || Has(objectName, "Bough") || Has(objectName, "Stick") || Has(objectName, "Bark")
            || Has(objectName, "Cone") || Has(objectName, "Taper") || Has(objectName, "Fork")
            || Has(objectName, "Leader") || Has(objectName, "Seg"))
            return ArtLibrary.Bark;
        return null;
    }

    private static bool Has(string name, string token)
    {
        return name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Name -> flower art key. Deliberately NOT part of KeyForName: that table answers for
    // the tree/berry family, where "Bloom" means a leaf and "Disk" means nothing, and the
    // yard's decor flowers (YardAmbient.AddHousePart builds PetalL/PetalUp/Disk of its own)
    // must not be caught here. `Spent` (autumn hip), `Spike_i` (lavender) and the leaflets
    // `Leaf_i` are not in this table: the bloom is this pass, the foliage is its own.
    public static string FlowerKeyForName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        if (Has(objectName, "Sepal"))
            return FlowerCalyxKey;
        if (Has(objectName, "PetalTip"))
            return FlowerTipKey;
        if (Has(objectName, "Petal"))
            return FlowerPetalKey;
        if (Has(objectName, "Disk"))
            return FlowerDiskKey;
        if (Has(objectName, "Bloom"))
            return FlowerCupKey;
        return null;
    }

    // Real art for the flower rigs under root. Scoped by the HOST COMPONENT, not by the
    // name: a renderer is dressed only when a PlantFlower sits above it, so calling this on
    // a scene root (GalleryScene.SetRealArt does) cannot repaint a tree, a berry or the
    // yard's decor flowers, whose part names collide. Returns the number of parts redrawn.
    // Two passes: the bloom (petals / lip / cup / sepals / disk, one canvas per role) and
    // the foliage (one canvas per leaflet part, the rig's card collage stepping aside).
    public static int DressFlower(Transform root, bool real)
    {
        if (root == null)
            return 0;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        int dressed = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || !sr.enabled)
                continue;   // an open kind switches its bloom box off on purpose: leave it
            if (sr.GetComponentInParent<PlantFlower>() == null)
                continue;

            string key = FlowerKeyDeep(sr);
            if (string.IsNullOrEmpty(key))
                continue;

            // keepColor: the canvas is grey and the part keeps the kind's own petal /
            // sepal / disk colour. Nothing else about the part changes.
            if (Dress(sr, key, 0f, real, true))
                dressed++;
        }

        dressed += DressFlowerLeaves(root, real);
        dressed += DressFlowerHeads(root, real);
        dressed += DressFlowerStems(root, real);

        if (real && dressed > 0)
            Debug.Log("ART-PARTS flowers dressed=" + dressed + "/" + renderers.Length
                + " root=" + root.name);
        return dressed;
    }

    // The toggle puts back exactly the cards a dress switched off, and nothing else: the set is
    // the record, not a flag on the card. Every pass that hides cards calls this on the way back
    // (the first pass to run finds the whole set and clears it; the next one finds it empty).
    private static void RestoreHiddenCards(string label)
    {
        if (HiddenCards.Count == 0)
            return;
        int back = 0;
        foreach (SpriteRenderer card in HiddenCards)
        {
            if (card == null)
                continue;
            card.enabled = true;
            back++;
        }
        Debug.Log("ART-PARTS " + label + " restore cardsBackOn=" + back + " of " + HiddenCards.Count);
        HiddenCards.Clear();
    }

    // --- the foliage (ART-0 gap 3b) ---------------------------------------------------
    // The leaf part is a HumanSegment named "Leaf_i" whose own sprite the rig switches OFF
    // (`PaintLeafMidrib`: leaf.sprite.enabled = false) and replaces with a collage of cards
    // hanging under `Visual`. The canvas takes that sprite back: the part's own box IS the
    // canvas' box (one bone, one PNG), and the cards that drew the leaflet pair step aside.
    // The `Petiole` / `PetioleTip` cards stay: the rig AIMS them at the stem join with
    // PlantTree.AimPictureAlongY, and a static canvas cannot aim.
    // The part is found by its COMPONENT, not by a renderer name: HumanSegment keeps its
    // picture on a child GameObject called "Visual", so the renderer never carries the
    // part's name (`InitializeVisualOnly`).
    private static int DressFlowerLeaves(Transform root, bool real)
    {
        if (!real)
            RestoreHiddenCards("leaf");

        HumanSegment[] parts = root.GetComponentsInChildren<HumanSegment>(true);
        int dressed = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            HumanSegment part = parts[i];
            if (part == null || part.visual == null || part.sprite == null)
                continue;
            if (!IsLeafPart(part.gameObject.name))
                continue;
            // Scoped by the HOST COMPONENT, exactly like the bloom pass: PlantStrawberry
            // builds its own "Leaf_i" HumanSegments, and they carry Art/Strawberry/leaf.png.
            if (part.GetComponentInParent<PlantFlower>() == null)
                continue;

            SpriteRenderer sr = part.sprite;
            Vector2 box = BoxOf(sr);
            if (box.x <= 0.0001f || box.y <= 0.0001f)
                continue;

            string key = FlowerLeafKeyFor(box.x / box.y);
            if (string.IsNullOrEmpty(key) || ArtLibrary.Albedo(key) == null)
                continue;   // no canvas: the caller shows a labelled ArtMissing tile

            // enable: this renderer is disabled by the rig on purpose, and the canvas is
            // what re-enables it. Dress() remembers the state it found (see Saved.enabled),
            // so real=false puts the part back to a switched-off collage sprite.
            if (!Dress(sr, key, 0f, real, true, true))
                continue;
            if (!real)
                continue;   // restoring: Dress() has just put the part's own sprite back,
                            // and the cards were switched on by the block above. Nothing
                            // else to do - hiding them here was the first gate's bug.

            dressed++;
            HideLeafCards(part.visual, key);
        }
        return dressed;
    }

    private static bool IsLeafPart(string name)
    {
        return !string.IsNullOrEmpty(name)
            && name.StartsWith("Leaf", System.StringComparison.Ordinal);
    }

    // Everything the canvas replaces under the leaf's Visual, except the aimed stalk and the
    // leaf's own renderer. That renderer sits ON this very GameObject (HumanSegment keeps its
    // picture on the child object `visual`), so a plain GetComponentsInChildren would switch
    // the canvas itself off right after the dress put it on - it did, in the first gate run.
    private static void HideLeafCards(Transform visual, string key)
    {
        SpriteRenderer[] cards = visual.GetComponentsInChildren<SpriteRenderer>(true);
        int hidden = 0;
        for (int i = 0; i < cards.Length; i++)
        {
            SpriteRenderer card = cards[i];
            if (card == null || !card.enabled)
                continue;
            if (card.gameObject == visual.gameObject)
                continue;   // the leaf part's own picture: the canvas lives here
            if (card.gameObject.name.IndexOf("Petiole", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;   // the aimed stalk, its direction is the rig's business
            card.enabled = false;
            HiddenCards.Add(card);
            hidden++;
        }
        Debug.Log("ART-PARTS leaf " + visual.parent.name + " canvas=" + key
            + " cardsHidden=" + hidden + " kept=Petiole*");
    }

    // --- the spike and the hip (ART-0 gap 3b, the last two rows, 24th pass) --------------
    // Same mechanism as the foliage, on two more part families:
    //   * a HumanSegment named "Spike_i" (lavender, four of them) whose own sprite the rig
    //     switched OFF behind the florets' flat cards -> Flower/spike;
    //   * the "Bloom" part of the eleven non-lavender kinds IN AUTUMN, whose own sprite
    //     `PaintSpentBloom` switched OFF behind twelve `Spent*` cards -> Flower/hip.
    // The season is read off the RIG's own output, not off a season field: the hip is the
    // bloom that carries a card whose name starts with "Spent", which is exactly what
    // `PaintSpentBloom` adds and `PaintBloomPetals` does not.
    private static int DressFlowerHeads(Transform root, bool real)
    {
        if (!real)
            RestoreHiddenCards("head");

        HumanSegment[] parts = root.GetComponentsInChildren<HumanSegment>(true);
        int dressed = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            HumanSegment part = parts[i];
            if (part == null || part.sprite == null)
                continue;
            // Scoped by the HOST COMPONENT, exactly like the bloom and the foliage passes:
            // PlantStrawberry builds its own parts and the yard's decor flowers collide.
            if (part.GetComponentInParent<PlantFlower>() == null)
                continue;

            SpriteRenderer sr = part.sprite;
            Vector2 box = BoxOf(sr);
            if (box.x <= 0.0001f || box.y <= 0.0001f)
                continue;

            // The canvas is asked for by the part's BOX, like the stem and the leaf: the eleven
            // kinds' live box aspects split into three hip families (see HipKeyFor).
            string key = HeadKeyFor(part, box);
            if (string.IsNullOrEmpty(key) || ArtLibrary.Albedo(key) == null)
                continue;   // no canvas: the caller shows a labelled ArtMissing tile

            // enable: the rig switched this renderer off on purpose, and the canvas is what
            // takes that slot back. Dress() remembers the state it found (Saved.enabled), so
            // real=false puts the part back to a switched-off box behind its cards.
            if (!Dress(sr, key, 0f, real, true, true))
                continue;
            if (!real)
                continue;   // restoring: Dress() has put the part's own sprite back and the
                            // cards were switched on by RestoreHiddenCards above.
            dressed++;
            HideHeadCards(part, key);
        }
        return dressed;
    }

    // "Spike_i" is the lavender raceme; a "Bloom" that carries a `Spent*` card is the autumn
    // seed head, whose canvas is chosen by the drawn box aspect. Nothing else in the flower rig
    // answers for a canvas here.
    private static string HeadKeyFor(HumanSegment part, Vector2 box)
    {
        string name = part.gameObject.name;
        if (!string.IsNullOrEmpty(name)
            && name.StartsWith("Spike", System.StringComparison.Ordinal))
            return FlowerSpikeKey;
        if (string.Equals(name, "Bloom", System.StringComparison.Ordinal)
            && HasCardNamed(part.transform, "Spent"))
            return box.y > 0.0001f ? HipKeyFor(box.x / box.y) : FlowerHipKey;
        return null;
    }

    private static bool HasCardNamed(Transform root, string token)
    {
        SpriteRenderer[] cards = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;
            if (cards[i].gameObject.name.StartsWith(token, System.StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    // The rig's flat cards this canvas replaces. Kept to the two names the spike and the hip
    // are built from (`Floret*` / `SpentFloret*` under a spike, `Spent*` under a spent bloom),
    // so the rig's painted parts - the `Sepal*` calyx, the spent `Disk` - are never touched,
    // and the part's own renderer (on the child object `visual`) is not caught either.
    private static void HideHeadCards(HumanSegment part, string key)
    {
        SpriteRenderer[] cards = part.GetComponentsInChildren<SpriteRenderer>(true);
        int hidden = 0;
        for (int i = 0; i < cards.Length; i++)
        {
            SpriteRenderer card = cards[i];
            if (card == null || !card.enabled)
                continue;
            string n = card.gameObject.name;
            if (!n.StartsWith("Floret", System.StringComparison.Ordinal)
                && !n.StartsWith("Spent", System.StringComparison.Ordinal))
                continue;
            card.enabled = false;
            HiddenCards.Add(card);
            hidden++;
        }
        Debug.Log("ART-PARTS head " + part.gameObject.name + " canvas=" + key
            + " cardsHidden=" + hidden + " kept=Sepal*/Disk");
    }

    // --- the stem (ART-0 gap 3b's last flat element, 25th pass) -------------------------
    // The part is one `HumanSegment` named exactly "Stem" (PlantFlower.AddPart, line 128); its
    // picture lives on the child object `Visual` (HumanSegment.InitializeVisualOnly) and the rig
    // switched it OFF behind six cone cards. Unlike the leaf and the head, the leaves and the
    // bloom are CHILDREN of this part (`leaf.transform.SetParent(stem.transform, false)`), so the
    // cards this pass hides are read from `part.visual` - NOT from the part, which would switch
    // the whole plant off - and only the cone cards are touched: the winter `Frost*` / `WinterBud*`
    // chips stay, they are painted chips lying on the cane.
    private static int DressFlowerStems(Transform root, bool real)
    {
        if (!real)
            RestoreHiddenCards("stem");

        HumanSegment[] parts = root.GetComponentsInChildren<HumanSegment>(true);
        int dressed = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            HumanSegment part = parts[i];
            if (part == null || part.visual == null || part.sprite == null)
                continue;
            if (!string.Equals(part.gameObject.name, "Stem", System.StringComparison.Ordinal))
                continue;
            // Scoped by the HOST COMPONENT, exactly like the bloom, the foliage and the head:
            // PlantStrawberry and PlantTree build their own parts and the names collide.
            if (part.GetComponentInParent<PlantFlower>() == null)
                continue;

            SpriteRenderer sr = part.sprite;
            Vector2 box = BoxOf(sr);
            if (box.x <= 0.0001f || box.y <= 0.0001f)
                continue;

            string key = StemKeyFor(box.x / box.y);
            if (string.IsNullOrEmpty(key) || ArtLibrary.Albedo(key) == null)
                continue;   // no canvas: the caller shows a labelled ArtMissing tile

            // enable: the rig switched this renderer off on purpose, and the canvas is what takes
            // that slot back. Dress() remembers the state it found (see Saved.enabled), so
            // real=false puts the part back to a switched-off box behind its cone cards.
            if (!Dress(sr, key, 0f, real, true, true))
                continue;
            if (!real)
                continue;   // restoring: Dress() has put the part's own sprite back and the cards
                            // were switched on by the block above. Nothing else to do.
            dressed++;
            HideStemCards(part.visual, key);
        }
        return dressed;
    }

    // The rig's cone cards: `ConeCore` / `ConeCoreTip` / `ConeEdgeL|LTip` / `ConeEdgeR|RTip` from
    // PaintVisualConeFill, or the `Taper*` band cards if the rig is ever given more than one band.
    // Nothing else under the stem's Visual is touched - the frost and the winter bud keep their
    // place on the cane.
    private static void HideStemCards(Transform visual, string key)
    {
        SpriteRenderer[] cards = visual.GetComponentsInChildren<SpriteRenderer>(true);
        int hidden = 0, kept = 0;
        for (int i = 0; i < cards.Length; i++)
        {
            SpriteRenderer card = cards[i];
            if (card == null || card.gameObject == visual.gameObject)
                continue;   // the stem's own picture: the canvas lives on the Visual itself
            string n = card.gameObject.name;
            bool cone = n.StartsWith("Cone", System.StringComparison.Ordinal)
                || n.StartsWith("Taper", System.StringComparison.Ordinal);
            if (!cone)
            {
                if (card.enabled)
                    kept++;     // Frost* / WinterBud*: the chips on the cane
                continue;
            }
            if (!card.enabled)
                continue;
            card.enabled = false;
            HiddenCards.Add(card);
            hidden++;
        }
        Debug.Log("ART-PARTS stem " + visual.parent.name + " canvas=" + key
            + " cardsHidden=" + hidden + " kept=Frost*/WinterBud* (" + kept + ")");
    }

    private static string FlowerKeyDeep(SpriteRenderer sr)
    {
        Transform t = sr.transform;
        for (int depth = 0; depth < 6 && t != null; depth++)
        {
            string key = FlowerKeyForName(t.gameObject.name);
            if (!string.IsNullOrEmpty(key))
                return key;
            t = t.parent;
        }
        return null;
    }

    // Real art on/off for every part under root. Returns the number of parts
    // redrawn. woodKey/leafKey/fruitKey decide the family; an empty key skips
    // that family. A part whose texture is missing is left alone - the caller
    // shows a labelled ArtMissing tile instead of a silent placeholder.
    public static int DressAll(
        Transform root,
        bool real,
        string woodKey = ArtLibrary.Bark,
        string leafKey = ArtLibrary.Leaf,
        string fruitKey = FruitKey,
        string stemKey = null,
        float woodTileMeters = BarkTileMeters,
        float leafTileMeters = LeafTileMeters)
    {
        if (root == null)
            return 0;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        int dressed = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null)
                continue;

            string key = FamilyKeyDeep(sr, woodKey, leafKey, fruitKey, stemKey);
            if (string.IsNullOrEmpty(key))
                continue;

            float tile = TileMetersFor(sr.gameObject.name, key, woodTileMeters, leafTileMeters);

            // The tree's leaf CARDS (29th pass): a leaf part of centimetres is a drawing,
            // not a surface, so the painted leaf canvas takes it and the material's crop
            // stays on the parts big enough to be a mass. Only the TREE family: a caller
            // that passes its own leaf key (the strawberry's Art/Strawberry/leaf) keeps it.
            if (real && key == leafKey && key == ArtLibrary.Leaf
                && ArtLibrary.Albedo(TreeLeafCardKey) != null)
            {
                Vector2 card = WorldBoxOf(sr);
                float side = Mathf.Min(card.x, card.y);
                if (side >= TreeLeafCardMinMeters && side < TreeLeafCardMaxMeters)
                {
                    if (Dress(sr, TreeLeafCardKey, 0f, true))
                        dressed++;
                    continue;
                }
            }

            if (Dress(sr, key, tile, real))
                dressed++;
        }

        if (real)
            Debug.Log("ART-PARTS dressed=" + dressed + "/" + renderers.Length + " root=" + root.name
                + " giantSkips=" + GiantSkips + " emptySkips=" + EmptySkips
                + " reverted=" + RevertedMoves);
        return dressed;
    }

    // The renderer's own name first, then its parents: the rigs put the picture
    // on a child named "Visual" and keep the part's name on the segment above
    // it (Wood_1_2/Visual), so the name that matters is often one level up.
    private static string FamilyKeyDeep(
        SpriteRenderer sr,
        string woodKey,
        string leafKey,
        string fruitKey,
        string stemKey)
    {
        Transform t = sr.transform;
        for (int depth = 0; depth < 6 && t != null; depth++)
        {
            string key = FamilyKey(t.gameObject.name, woodKey, leafKey, fruitKey, stemKey);
            if (!string.IsNullOrEmpty(key))
                return key;
            t = t.parent;
        }
        return null;
    }

    private static string FamilyKey(string name, string woodKey, string leafKey, string fruitKey, string stemKey)
    {
        string key = KeyForName(name);
        if (key == ArtLibrary.Bark)
            return woodKey;
        if (key == ArtLibrary.Leaf)
            return leafKey;
        if (key == BerryKey)
            return ArtLibrary.HasArt(ArtLibrary.Leaf) && Has(name, "Leaf") ? leafKey : BerryKey;
        if (key == FruitKey)
            return fruitKey;
        if (stemKey != null && Has(name, "Stem"))
            return stemKey;
        if (key == null && stemKey != null && Has(name, "Stem"))
            return stemKey;
        _ = stemKey;
        return key;
    }

    private static float TileMetersFor(string name, string key, float woodTileMeters, float leafTileMeters)
    {
        if (key == ArtLibrary.Bark)
            return woodTileMeters;
        if (key == ArtLibrary.Leaf)
            return leafTileMeters;
        // Fruit / berry / stem are objects, not materials: one sprite per part,
        // so the tile period is the part's own size and Tiled is never chosen.
        return 0f;
    }

    // One renderer. real=false restores the built sprite. keepColor=true leaves the
    // renderer's own tint in place: the flower canvases carry the shading as grey and the
    // part's kind colour is the tint, so whiting it out would drop the kind (and the
    // canvas comes back through it). Every other caller keeps the old behaviour.
    // enable=true switches the renderer ON as part of the dress: the flower's leaf part is
    // built with its sprite switched off (`PaintLeafMidrib` hides it behind the collage),
    // and the canvas is what takes that slot back. Only reached once the canvas is in hand.
    public static bool Dress(
        SpriteRenderer sr,
        string key,
        float tileMeters,
        bool real,
        bool keepColor = false,
        bool enable = false)
    {
        if (sr == null)
            return false;

        if (!Originals.TryGetValue(sr, out Saved saved) || !saved.valid)
        {
            saved = new Saved
            {
                sprite = sr.sprite,
                color = sr.color,
                mode = sr.drawMode,
                tileMode = sr.tileMode,
                size = sr.size,
                enabled = sr.enabled,
                valid = true
            };
            Originals[sr] = saved;
        }

        if (!real)
        {
            sr.sprite = saved.sprite;
            sr.color = saved.color;
            sr.drawMode = saved.mode;
            sr.tileMode = saved.tileMode;
            sr.size = saved.size;
            sr.enabled = saved.enabled;   // a part the rig had switched off stays off
            return true;
        }

        if (string.IsNullOrEmpty(key) || ArtLibrary.Albedo(key) == null)
            return false;

        Vector2 box = BoxOf(sr);
        if (box.x <= 0.0001f || box.y <= 0.0001f)
        {
            EmptySkips++;
            return false;
        }

        // A monster box means the rig keeps its own units and a parent scale
        // shrinks them, or the renderer drew nothing at all: either way a real
        // sprite here would cover the whole scene, so refuse.
        Vector3 scale = sr.transform.lossyScale;
        float scaleMax = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        Vector2 world = new Vector2(box.x * scaleMax, box.y * scaleMax);
        if (world.x > MaxPartMeters || world.y > MaxPartMeters)
        {
            GiantSkips++;
            if (GiantSkips <= 12)
                Debug.LogWarning("ART-PARTS skip giant " + PathOf(sr) + " world="
                    + world.x.ToString("0.00") + "x" + world.y.ToString("0.00") + " m key=" + key);
            return false;
        }

        // tileMeters is a world period, the box is in local units.
        float local = tileMeters > 0.01f ? tileMeters / Mathf.Max(0.001f, scaleMax) : 0f;
        float tile = local > 0.01f ? local : Mathf.Max(box.x, box.y);
        Sprite sprite = ArtLibrary.Tiled(key, tile);
        if (sprite == null)
            return false;

        // Draw mode FIRST (ART-0 21st pass). Unity keeps the current world box when a
        // Simple renderer is converted to Sliced/Tiled, and it does that by writing the
        // TRANSFORM's scale. With the old order (picture, size, mode) that factor came out
        // as the new canvas' own size over the size the old placeholder had reported -
        // log_a 0.14 / 0.14021 = 0.99849, log_b 1.00810, log_c 0.99375 - so the picture sat
        // on the box only because of a scale the dress never asked for, and the check
        // "dressing moves no localScale" failed on all three logs. Converting while the
        // renderer still carries its own placeholder (its world box equals its reported
        // size there) makes that factor exactly 1: nothing about the part moves, and
        // `sprite` / `size` / `color` are the only things the dress writes.
        // Evidence: Art1Check.ScaleAudit sequence B, gate assertion on every log.
        // ART-0 22nd pass: the draw mode follows the ROLE, not only the size.
        // A MATERIAL (tileMeters > 0) is a repeated surface and keeps its authored world
        // period, so it is Tiled even when the part is SMALLER than one tile: Tiled crops
        // the canvas at the canvas' own scale, while Sliced stretches the whole canvas onto
        // the rect. Chosen by size alone (the 21st pass' branch), a part smaller than the
        // period took a whole 0.50 m bark/leaf sample squeezed into its own box - the grain
        // at 1/5 of its authored scale on a 0.10 x 0.30 m limb, and on the leaf tile the
        // LEAVES themselves at 1/4, so one tree carried two bark scales and its small pads
        // carried miniature foliage.
        // A PAINTED part (tileMeters == 0: Human/head, Flower/petal, Campfire/log_a) has
        // one canvas drawn in its own box's aspect, so Sliced maps it onto the box exactly.
        bool material = tileMeters > 0.01f;
        if (material || box.x > tile + 0.0001f || box.y > tile + 0.0001f)
        {
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
        }
        else
        {
            sr.drawMode = SpriteDrawMode.Sliced;
        }

        sr.sprite = sprite;
        if (!keepColor)
            sr.color = Color.white;
        sr.size = box;
        if (enable)
            sr.enabled = true;

        // NOTE: sr.bounds read here is not trustworthy - Unity rebuilds the
        // sprite mesh on the next frame, so a Sliced/Tiled conversion still
        // reports the old Simple box. Geometry is proved end to end instead
        // (GalleryCapture compares the content box before and after dressing
        // once real frames have passed).
        return true;
    }

    // The shipping box in LOCAL units (the transform scale multiplies it): a
    // Simple renderer is its sprite's own size, a Sliced/Tiled one is sr.size.
    // A Simple renderer with no sprite draws nothing, so its stale sr.size must
    // not be trusted - it is usually a scaled-up number from the rig's own
    // local units and would draw a monster plate once a real sprite lands.
    public static Vector2 BoxOf(SpriteRenderer sr)
    {
        if (sr == null)
            return Vector2.zero;
        if (sr.drawMode == SpriteDrawMode.Simple)
            return sr.sprite != null ? Local(sr.sprite) : Vector2.zero;
        if (sr.size.x > 0.0001f && sr.size.y > 0.0001f)
            return sr.size;
        return sr.sprite != null ? Local(sr.sprite) : Vector2.zero;
    }

    private static Vector2 Local(Sprite sprite)
    {
        Bounds b = sprite.bounds;
        return new Vector2(b.size.x, b.size.y);
    }

    // Drawn size in metres after the whole parent chain has had its say.
    public static Vector2 WorldBoxOf(SpriteRenderer sr)
    {
        Vector2 local = BoxOf(sr);
        Vector3 scale = sr.transform.lossyScale;
        return new Vector2(local.x * Mathf.Abs(scale.x), local.y * Mathf.Abs(scale.y));
    }

    public static void Forget(SpriteRenderer sr)
    {
        if (sr != null)
            Originals.Remove(sr);
    }
}
