using UnityEngine;

// Drop-in spawn without editing GameProcess. Attach to any scene object.
// SampleScene / World stay untouched until a later card adds hedgehogCount.
public class HedgehogHost : MonoBehaviour
{
    [Header("Spawn")]
    public int hedgehogCount = 1;
    public int bushCount = 2;
    public float originX = 0f;
    public float hedgehogSpacing = 0.7f;
    public float bushOffsetX = 1.1f;
    public float bushSpacing = 0.55f;
    public float groundY = -2f;

    void Start()
    {
        if (hedgehogCount < 1 && bushCount < 1)
            return;

        for (int i = 0; i < hedgehogCount; i++)
        {
            float x = originX + i * hedgehogSpacing;
            Hedgehog.Spawn(new Vector2(x, Hedgehog.StandingRootY(groundY)), "Hedgehog_" + (i + 1));
        }

        for (int i = 0; i < bushCount; i++)
        {
            float x = originX + bushOffsetX + i * bushSpacing;
            GameObject go = new GameObject("StrawberryBush_" + (i + 1));
            go.transform.position = new Vector2(x, PlantStrawberry.RootY(groundY));
            PlantStrawberry plant = go.AddComponent<PlantStrawberry>();
            plant.seed = i + 1;
            plant.BuildBush();
        }
    }
}
