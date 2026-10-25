using UnityEngine;

// Text for the gallery: labels, scale-bar captions, MISSING ART notices.
// TextMesh only - no collider, no Rigidbody2D, no UI canvas, so a label can
// never touch physics. The built-in font is the one the arena head numbers
// already prove renders in a build (ArenaHumanAgent.LabelFont).
public static class GalleryLabels
{
    private static Font _font;
    private static bool _resolved;

    public static Font Font()
    {
        if (_resolved)
            return _font;
        _resolved = true;
        _font = LoadBuiltinFont("LegacyRuntime.ttf") ?? LoadBuiltinFont("Arial.ttf");
        if (_font == null)
            Debug.LogError("GalleryLabels: no built-in font - labels will not render.");
        return _font;
    }

    private static Font LoadBuiltinFont(string name)
    {
        try
        {
            return Resources.GetBuiltinResource<Font>(name);
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    // heightMeters is the cap height of the text in world metres. TextMesh maps
    // characterSize * fontSize / 10 to that height for the built-in font.
    public static TextMesh Label(
        Transform parent,
        string text,
        Vector3 localPosition,
        float heightMeters,
        Color color,
        int sortingOrder)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        TextMesh mesh = go.AddComponent<TextMesh>();
        Font font = Font();
        if (font != null)
            mesh.font = font;
        mesh.text = text;
        mesh.fontSize = 72;
        mesh.characterSize = Mathf.Max(0.004f, heightMeters) / 7.2f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = sortingOrder;
            if (font != null && font.material != null)
                renderer.sharedMaterial = font.material;
        }
        return mesh;
    }

    // Readable on any background: a dark copy offset by one text line fraction
    // under a light copy. Two TextMeshes, no shader work.
    public static void Readable(
        Transform parent,
        string text,
        Vector3 localPosition,
        float heightMeters,
        Color color,
        int sortingOrder)
    {
        Label(parent, text, localPosition + new Vector3(heightMeters * 0.06f, -heightMeters * 0.08f, 0.01f),
            heightMeters, new Color(0f, 0f, 0f, 0.85f), sortingOrder);
        Label(parent, text, localPosition, heightMeters, color, sortingOrder + 1);
    }

    // A 1 m bar (the card's scale bar), a white plate with a dark outline copy
    // and a "1 m" caption. Purely a picture: no collider.
    public static GameObject ScaleBar(
        Transform parent,
        Vector3 localPosition,
        float meters,
        Color color,
        int sortingOrder,
        float captionHeightMeters = 0.12f)
    {
        GameObject bar = new GameObject("ScaleBar");
        bar.transform.SetParent(parent, false);
        bar.transform.localPosition = localPosition;
        bar.transform.localScale = Vector3.one;

        AddPlate(bar.transform, "BarOutline", new Vector2(meters + 0.04f, 0.075f),
            new Color(0f, 0f, 0f, 0.85f), sortingOrder);
        AddPlate(bar.transform, "Bar", new Vector2(meters, 0.035f), color, sortingOrder + 1);
        AddPlate(bar.transform, "TickL", new Vector2(0.035f, 0.11f),
            new Color(0f, 0f, 0f, 0.85f), sortingOrder + 1, new Vector3(-meters * 0.5f, 0f, 0f));
        AddPlate(bar.transform, "TickR", new Vector2(0.035f, 0.11f),
            new Color(0f, 0f, 0f, 0.85f), sortingOrder + 1, new Vector3(meters * 0.5f, 0f, 0f));

        Readable(bar.transform, "1 m", new Vector3(0f, 0.20f, 0f), captionHeightMeters,
            Color.white, sortingOrder + 2);
        return bar;
    }

    // A flat plate of any size: the galleries' captions and the impact video's HUD both
    // need one behind their text. Public, and it hands back the renderer so a caller that
    // changes the lens later can resize its own plate.
    public static SpriteRenderer AddPlate(
        Transform parent,
        string name,
        Vector2 size,
        Color color,
        int sortingOrder,
        Vector3 localPosition = default(Vector3))
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = Vector3.one;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.color = color;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.sortingOrder = sortingOrder;
        SpriteLighting.ApplyUnlit(sr);
        return sr;
    }
}
