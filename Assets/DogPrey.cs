using UnityEngine;

// Position mark for a dog leap. Not a body and not a force.
// JawStrike reads Damageable on this object the same way BeakStrike
// reads a bird prey mark. No FactionMember, no brain.
public class DogPrey : MonoBehaviour
{
    public const float DefaultAheadX = 1.4f;
    public const float DefaultHeightAboveGround = 0.40f;

    public static DogPrey Spawn(Vector2 position)
    {
        GameObject mark = new GameObject("DogPrey");
        mark.transform.position = position;

        DogPrey prey = mark.AddComponent<DogPrey>();
        Damageable life = mark.AddComponent<Damageable>();
        life.maxHealth = 10f;
        life.health = life.maxHealth;

        BoxCollider2D box = mark.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.16f, 0.16f);
        box.isTrigger = true;

        SpriteRenderer sprite = mark.AddComponent<SpriteRenderer>();
        Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        Color[] fill = new Color[64];
        Color markColor = new Color(0.62f, 0.14f, 0.10f, 1f);
        for (int i = 0; i < fill.Length; i++)
            fill[i] = markColor;
        tex.SetPixels(fill);
        tex.Apply(false, true);
        sprite.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 50);
        sprite.sortingOrder = 4;
        return prey;
    }
}
