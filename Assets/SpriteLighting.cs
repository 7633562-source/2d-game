using UnityEngine;

// Sprite materials for URP 2D. Without the Lit shader Light2D is invisible:
// Built-in Sprites/Default ignores light.
public static class SpriteLighting
{
    private static Material _lit;
    private static Material _unlit;

    public static void ApplyLit(SpriteRenderer sprite)
    {
        if (!SceneLighting.Enabled)
        {
            ApplyUnlit(sprite);
            return;
        }
        if (sprite == null)
            return;
        Material mat = Lit();
        if (mat != null)
            sprite.sharedMaterial = mat;
    }

    public static void ApplyUnlit(SpriteRenderer sprite)
    {
        if (sprite == null)
            return;
        Material mat = Unlit();
        if (mat != null)
            sprite.sharedMaterial = mat;
    }

    public static Material Lit()
    {
        if (_lit != null)
            return _lit;
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (shader == null)
            return null;
        _lit = new Material(shader);
        _lit.name = "SpriteLitShared";
        return _lit;
    }

    public static Material Unlit()
    {
        if (_unlit != null)
            return _unlit;
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;
        _unlit = new Material(shader);
        _unlit.name = "SpriteUnlitShared";
        return _unlit;
    }
}
