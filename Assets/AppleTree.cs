using UnityEngine;

// AppleTree : TreeBase. The orchard recipe: a low, wide, fruit-bearing crown.
// Apples and leaves are picture-only ornaments (no Rigidbody2D, no hinge, no
// collider) hung on the wood, so they ride the sway and never detach.
public class AppleTree : TreeBase
{
    [Header("Fruit — picture only")]
    [Tooltip("Apple diameter, m. Flat red circle, no texture asset.")]
    public float fruitDiameter = 0.12f;
    [Tooltip("Leaf plate size, m. Green box, no texture asset.")]
    public Vector2 leafSize = new Vector2(0.16f, 0.12f);
    [Tooltip("Large crown plate size, m. Overlapping pads read as a canopy.")]
    public Vector2 crownPadSize = new Vector2(0.60f, 0.48f);
    [Tooltip("Apples hung on each outer twig.")]
    public int applesPerTwig = 2;

    private readonly Color leafGreen = new Color(0.24f, 0.50f, 0.22f, 1f);
    private readonly Color leafGreenFar = new Color(0.20f, 0.42f, 0.14f, 1f);
    private readonly Color crownGreen = new Color(0.22f, 0.46f, 0.20f, 1f);
    private readonly Color crownGreenFar = new Color(0.18f, 0.40f, 0.16f, 1f);
    private readonly Color appleRed = new Color(0.82f, 0.16f, 0.12f, 1f);
    private readonly Color appleRedFar = new Color(0.70f, 0.13f, 0.09f, 1f);

    private int fruitCount;
    private int leafCount;
    private static Sprite appleSprite;

    public override int FruitCount => fruitCount;
    public override int LeafCount => leafCount;
    public override string KindName => "Apple";

    protected override void ApplyRecipe()
    {
        // Apple crown. The physics follows the oak hold canon (fork 28,
        // no world spring) so the empty hold stays tight; the apple
        // character is the low rounded crown + red fruit, not a looser rig.
        trunkLength = 0.82f;
        trunkWidth = 0.16f;
        stumpHeight = 0.22f;
        stumpWidth = 0.20f;
        lengthDecay = 0.72f;
        widthDecay = 0.65f;
        forkAngle = 28f;
        forkJitter = 4f;
        leaderForkShare = 0.28f;
        totalMass = 28f;
        trunkStiffness = 180f;
        twigStiffness = 22f;
        twigSpringMaxTorque = 40f;
        worldHoldScale = 0f;
        sideLengthScale = 1f;
        growsLeaves = true;
    }

    protected override void DecorateNode(HumanSegment seg, int depth, float heading, bool farSide, bool isTip)
    {
        if (seg == null || depth < 1)
            return;

        // Dense rounded crown: large overlapping pads form the canopy mass,
        // small plates add a ragged edge, fruit hangs on the outer twigs.
        if (depth >= 2)
            AttachCrownPads(seg, farSide, isTip ? 3 : 2);
        else
            AttachCrownPads(seg, farSide, 1);

        if (depth >= 2)
            AttachLeaves(seg, farSide, isTip ? 2 : 1);

        if (depth >= 2)
            AttachApples(seg, farSide, depth, heading, isTip ? applesPerTwig : 1);
    }

    // Overlapping green plates that read as a rounded canopy mass, not a
    // scatter of single leaves. Picture only; no collider.
    private void AttachCrownPads(HumanSegment twig, bool farSide, int count)
    {
        float side = farSide ? -1f : 1f;
        for (int k = 0; k < count; k++)
        {
            float t = count == 1 ? 0.5f : k / (float)(count - 1);
            float along = Mathf.Lerp(0.40f, 1.05f, t);
            float hang = side * twig.size.x * (0.30f + 0.45f * Mathf.Abs(t - 0.5f));

            GameObject go = new GameObject("Crown" + leafCount);
            go.transform.SetParent(twig.transform, false);
            go.transform.localPosition = new Vector3(hang, (along - 0.5f) * twig.size.y, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, side * (10f + 12f * (k % 3)));
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            Color c = (k & 1) == 0 ? crownGreen : crownGreenFar;
            HumanSegment.ApplyPicture(sr, crownPadSize, HumanVisualShape.Box, null, c, twig.sortingOrder + 2);
            leafCount++;
        }
    }

    private void AttachLeaves(HumanSegment twig, bool farSide, int count)
    {
        float side = farSide ? -1f : 1f;
        for (int k = 0; k < count; k++)
        {
            float u = count == 1 ? 0f : (k / (float)(count - 1) - 0.5f) * 2f;
            float along = count == 1 ? 0.90f : Mathf.Lerp(0.62f, 0.98f, k / (float)(count - 1));
            float hang = side * twig.size.x * (0.40f + 0.32f * Mathf.Abs(u));

            GameObject leaf = new GameObject("Leaf" + leafCount);
            leaf.transform.SetParent(twig.transform, false);
            leaf.transform.localPosition = new Vector3(hang, (along - 0.5f) * twig.size.y, 0f);
            leaf.transform.localRotation = Quaternion.Euler(0f, 0f, side * (16f + 9f * (k % 3)));
            SpriteRenderer sr = leaf.AddComponent<SpriteRenderer>();
            Color c = (k & 1) == 0 ? leafGreen : leafGreenFar;
            HumanSegment.ApplyPicture(sr, leafSize, HumanVisualShape.Box, null, c, twig.sortingOrder + 2);
            leafCount++;
        }
    }

    private void AttachApples(HumanSegment twig, bool farSide, int depth, float heading, int count)
    {
        if (count < 1) return;
        float side = farSide ? -1f : 1f;
        Vector3 down = twig.transform.InverseTransformDirection(Vector3.down);

        for (int k = 0; k < count; k++)
        {
            float u = count == 1 ? 0f : (k / (float)(count - 1) - 0.5f) * 2f;
            float along = count == 1 ? 0.72f : Mathf.Lerp(0.52f, 0.88f, k / (float)(count - 1));
            float hang = side * twig.size.x * 0.30f * (0.5f + 0.5f * Mathf.Abs(u));
            float drop = twig.size.x * 0.22f + fruitDiameter * 0.62f;

            // Hang toward world-down so an apple on a ±fork limb reads as
            // fruit, not a red tile on the stick. Placed once at build; the
            // apple is a child of the wood and rides the sway.
            Vector3 localPos = new Vector3(
                down.x * drop + hang * 0.45f,
                (along - 0.5f) * twig.size.y + down.y * drop,
                0f);

            GameObject apple = new GameObject("Fruit" + fruitCount);
            apple.transform.SetParent(twig.transform, false);
            apple.transform.localPosition = localPos;
            SpriteRenderer sr = apple.AddComponent<SpriteRenderer>();
            sr.sprite = AppleSprite();
            sr.color = (k & 1) == 0 ? appleRed : appleRedFar;
            sr.sortingOrder = twig.sortingOrder + 3;
            SpriteLighting.ApplyLit(sr);
            fruitCount++;
        }
    }

    // One shared soft-edged circle, generated at build time (no PNG asset).
    private static Sprite AppleSprite()
    {
        if (appleSprite != null)
            return appleSprite;
        const float ppu = 256f;
        int px = Mathf.Clamp(Mathf.RoundToInt(0.12f * ppu), 8, 512);
        Texture2D tex = new Texture2D(px, px, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (px - 1) / 2f;
        float r = px / 2f - 1f;
        for (int y = 0; y < px; y++)
        {
            for (int x = 0; x < px; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                float a = Mathf.Clamp01(0.7f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply(false, false);
        appleSprite = Sprite.Create(tex, new Rect(0f, 0f, px, px), new Vector2(0.5f, 0.5f), ppu);
        return appleSprite;
    }
}
