using UnityEngine;

// One edible fruit on PlantStrawberry. Picture + trigger collider.
// Not a perch. Not a muscle. BeakStrike already calls Damageable.Hurt
// on overlap during Peck / Attack; no faction stamp (berries are food,
// not Insects). Dead berry is eaten and destroyed.
public class StrawberryBerry : MonoBehaviour
{
    [Tooltip("When false the collider is off; Peck cannot eat it.")]
    public bool edible = true;

    public Damageable Life { get; private set; }

    private PlantStrawberry bush;
    private BoxCollider2D hit;
    private bool eaten;

    public static StrawberryBerry Attach(
        PlantStrawberry owner,
        Transform parent,
        Vector3 localPosition,
        Vector2 size,
        float health,
        Color color,
        int index)
    {
        GameObject go = new GameObject("Berry_" + index);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;

        SpriteRenderer sprite = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sprite,
            size,
            HumanVisualShape.Box,
            null,
            new Color(color.r, color.g, color.b, 0.5f),
            5);

        StrawberryBerry berry = go.AddComponent<StrawberryBerry>();
        berry.bush = owner;
        berry.edible = true;

        berry.hit = go.AddComponent<BoxCollider2D>();
        berry.hit.size = size;
        berry.hit.isTrigger = true;

        berry.Life = go.AddComponent<Damageable>();
        berry.Life.maxHealth = Mathf.Max(0.01f, health);
        berry.Life.health = berry.Life.maxHealth;
        return berry;
    }

    void Update()
    {
        if (eaten)
            return;
        if (hit != null)
            hit.enabled = edible;

        if (Life != null && Life.IsDead)
        {
            eaten = true;
            if (bush != null)
                bush.NotifyBerryEaten();
            Destroy(gameObject);
        }
    }
}
