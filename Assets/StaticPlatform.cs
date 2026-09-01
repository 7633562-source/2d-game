using UnityEngine;

// Один кусок уровня: прямоугольник в мире. Физика здесь только статическая
// опора — без Rigidbody2D, без сил, без суставов.
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

// Спавн статической площадки. Размер задаётся спрайтом и коллайдером, не
// масштабом: иначе как у сегментов тела поедут дочерние объекты и контакты.
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
        bool tiledFill = !isSign && !isDecor && LevelArt.UseTiledFill();

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = isSign ? LevelArt.Sign() : (isDecor ? LevelArt.White() : LevelArt.Fill());
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

    // Трава лежит в верхней кромке грунта, не торчит выше поверхности:
    // иначе 14 см травы перекрыли бы ступень 5 см соседней террасы.
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
