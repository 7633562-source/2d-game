using UnityEngine;

// One level piece: a rectangle in the world. Physics here is only static
// support — no Rigidbody2D, no forces, no joints.
public struct LevelPiece
{
    public string name;
    public Vector2 center;
    public Vector2 size;
    public float rotationZ;
    public Color color;
    public string kind;
    public bool hasCollider;
}

// Spawn a static platform. Size is set by sprite and collider, not
// scale: otherwise child objects and contacts slide as on body segments.
public static class StaticPlatform
{
    public const int SortingOrder = -2;
    public const int SurfaceSortingOrder = -1;
    public const int DecorSortingOrder = 2;

    public static GameObject Spawn(LevelPiece piece, Transform parent)
    {
        GameObject go = new GameObject(string.IsNullOrEmpty(piece.name) ? "Platform" : piece.name);
        go.transform.SetParent(parent, false);
        go.transform.position = piece.center;
        go.transform.localScale = Vector3.one;
        if (Mathf.Abs(piece.rotationZ) > 0.001f)
            go.transform.eulerAngles = new Vector3(0f, 0f, piece.rotationZ);

        bool isSign = piece.kind == LevelArt.SignKey;
        bool isDecor = !piece.hasCollider && !isSign;
        Sprite fill = isSign ? null : (isDecor ? null : LevelArt.Fill());
        // A slab WIDER than one native tile is what stretched the fill
        // (8 m plateau 128 px/m, the 32 m "Behind" plate 32 px/m - ART-0).
        // Tiled repeats the same pixels instead of scaling them. A slab that
        // is not wider than the tile keeps Sliced, exactly as before.
        bool tiledFill = !isSign && !isDecor && fill != null
            && LevelArt.UseTiledFill()
            && piece.size.x > fill.bounds.size.x + 0.001f;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = isSign ? LevelArt.Sign() : (isDecor ? LevelArt.White() : fill);
        if (isSign)
            sr.color = LevelArt.HasSign() ? Color.white : LevelArt.SignTint();
        else if (isDecor)
            sr.color = piece.color.a > 0.01f ? piece.color : Color.white;
        else if (tiledFill)
            sr.color = Color.white;
        else
            sr.color = piece.color.a > 0.01f ? piece.color : LevelArt.FillTint();
        sr.drawMode = tiledFill ? SpriteDrawMode.Tiled : SpriteDrawMode.Sliced;
        sr.size = piece.size;
        sr.sortingOrder = isSign || !piece.hasCollider ? DecorSortingOrder : SortingOrder;
        // req-342 -level1FenceBack 1: the back fence goes behind the actors
        // (spawn time, so streamed chunks are covered too).
        if (isDecor && Level1LookFlags.FenceBehind && Level1LookFlags.IsBackFence(piece.name))
            sr.sortingOrder = Level1LookFlags.BackFenceOrder;
        if (tiledFill)
            sr.tileMode = SpriteTileMode.Continuous;
        SpriteLighting.ApplyLit(sr);

        if (piece.hasCollider)
        {
            GroundLayers.Apply(go);
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = piece.size;
            SpawnSurfaceCap(go.transform, piece.size);
        }

        return go;
    }

    // Grass sits in the top rim of the dirt, not above the surface:
    // otherwise 14 cm of grass would cover a 5 cm step of the next terrace.
    private static void SpawnSurfaceCap(Transform parent, Vector2 size)
    {
        float capHeight = 0.14f;
        GameObject cap = new GameObject("Surface");
        cap.transform.SetParent(parent, false);
        cap.transform.localPosition = new Vector3(0f, size.y * 0.5f - capHeight * 0.5f, 0f);
        cap.transform.localScale = Vector3.one;

        SpriteRenderer sr = cap.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.Cap();
        sr.color = LevelArt.CapTint();
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(size.x, capHeight);
        sr.sortingOrder = SurfaceSortingOrder;
        sr.tileMode = SpriteTileMode.Continuous;
        SpriteLighting.ApplyLit(sr);
    }
}
