using UnityEngine;

// What every plant is. Roots sit on RootY, every plant grows a canopy of
// picture-only parts, and wind is a ½ρv² sail on the wood. The one shared
// rule lives here too: every collider a plant builds carries an explicit
// PhysicsMaterial2D (bounciness 0) — never the engine default.
public abstract class PlantBase : MonoBehaviour
{
    [Header("Wind")]
    [Tooltip("Oncoming air speed, m/s. Force ~ ½ρv²·area.")]
    public float windSpeed = 2.2f;
    [Tooltip("Sail scale. 1 = formula as written, 0 = no wind.")]
    public float windScale = 0.12f;
    [Tooltip("Gust amplitude vs base speed.")]
    public float windGust = 1.1f;
    [Tooltip("Gust period, s.")]
    public float windPeriod = 4.5f;

    public static float RootY(float groundY = -2f)
    {
        return groundY;
    }

    // One shared wood material. Wood is ghost to birds and self-collisions
    // are disabled, so the friction value is inert in contact — but the
    // material is explicit, per the collider-materials rule. bounciness 0.
    private static PhysicsMaterial2D woodMaterial;

    protected static PhysicsMaterial2D WoodMaterial()
    {
        if (woodMaterial == null)
        {
            woodMaterial = new PhysicsMaterial2D("PlantWood");
            woodMaterial.friction = 0.6f;
            woodMaterial.bounciness = 0f;
        }
        return woodMaterial;
    }

    public abstract void Build();
}
