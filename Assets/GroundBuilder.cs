using UnityEngine;

public class GroundBuilder : MonoBehaviour
{
    // ─── GROUND PARAMETERS ───
    [Header("Ground parameters")]
    public Vector2 groundSize = new Vector2(20f, 2f);    // ground width and height
    public Vector2 groundPosition = new Vector2(0f, -3f); // ground center position
    public Color groundColor = new Color(0.45f, 0.34f, 0.22f); // dirt
    public Color grassColor = new Color(0.30f, 0.58f, 0.28f);
    public Color skyColor = new Color(0.55f, 0.78f, 0.94f);

    // 0.4 — former behavior: a collider with no material takes Unity's
    // default. The "sole — dirt" pair is sqrt(µ1 · µ2).
    [Header("Ground grip")]
    public float groundFriction = 0.4f;

    // ─── PUBLIC METHOD FOR GameProcess ───
    public void BuildGround()
    {
        // Ground physics is unchanged: same size, same position, same
        // BoxCollider2D on the parent with scale. Picture — child Visual
        // with inverse scale so Tiled is in world meters.
        GameObject ground = new GameObject("Ground");
        ground.transform.position = groundPosition;
        ground.transform.localScale = new Vector3(groundSize.x, groundSize.y, 1f);
        GroundLayers.Apply(ground);

        BoxCollider2D col = ground.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        PhysicsMaterial2D dirtMaterial = new PhysicsMaterial2D("GroundDirt");
        dirtMaterial.friction = Mathf.Max(0f, groundFriction);
        dirtMaterial.bounciness = 0f;
        col.sharedMaterial = dirtMaterial;

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(ground.transform, false);
        visual.transform.localScale = new Vector3(
            1f / Mathf.Max(0.0001f, groundSize.x),
            1f / Mathf.Max(0.0001f, groundSize.y),
            1f);

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        Sprite dirt = ArtLibrary.Tiled(ArtLibrary.Dirt, ArtLibrary.GroundTileMeters);
        sr.sprite = dirt != null ? dirt : CreateSquareSprite();
        sr.color = dirt != null ? Color.white : groundColor;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = groundSize;
        sr.sortingOrder = -16;
        sr.tileMode = SpriteTileMode.Continuous;
        SpriteLighting.ApplyLit(sr);

        BuildSky();
        BuildGrass();
    }

    // Sky has no collider. Course/Steps do not build Ground,
    // so LevelGenerator calls this separately.
    public void BuildBackdrop()
    {
        if (GameObject.Find("Sky") != null)
            return;
        BuildSky();
    }

    private void BuildSky()
    {
        GameObject sky = new GameObject("Sky");
        sky.transform.position = new Vector3(groundPosition.x, 6f, 0f);
        SpriteRenderer sr = sky.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.color = skyColor;
        sr.sortingOrder = -40;
        sky.transform.localScale = new Vector3(Mathf.Max(40f, groundSize.x), 20f, 1f);
        SpriteLighting.ApplyUnlit(sr);
    }

    private void BuildGrass()
    {
        // Ground top is the stance line (y = −2 at default size/position).
        float topY = groundPosition.y + groundSize.y * 0.5f;
        float width = Mathf.Max(40f, groundSize.x);
        const float grassHeight = 0.14f;
        GameObject grass = new GameObject("Grass");
        grass.transform.position = new Vector3(groundPosition.x, topY - grassHeight * 0.5f, 0f);
        grass.transform.localScale = Vector3.one;

        SpriteRenderer sr = grass.AddComponent<SpriteRenderer>();
        Sprite grassSprite = ArtLibrary.Tiled(ArtLibrary.Grass, ArtLibrary.GrassTileMeters);
        sr.sprite = grassSprite != null ? grassSprite : CreateSquareSprite();
        sr.color = grassSprite != null ? Color.white : grassColor;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(width, grassHeight);
        sr.sortingOrder = -14;
        sr.tileMode = SpriteTileMode.Continuous;
        SpriteLighting.ApplyLit(sr);
    }

    Sprite CreateSquareSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
