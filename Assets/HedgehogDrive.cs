using UnityEngine;

// Circumstances, not force. In Update writes only Walk / EndWalk / Bite
// and the crawl goal. Walk is the crawl wish; the stance controller
// supplies the body force. Food is plant StrawberryBerry, not a Hostile.
// Life stays Forage.
[DefaultExecutionOrder(-50)]
public class HedgehogDrive : MonoBehaviour
{
    [Header("Policy")]
    [Tooltip("Stand turns this on explicitly. Play is not headless.")]
    public bool allowHeadless;

    [Header("Food")]
    [Tooltip("Plant-layer bush (PlantStrawberry). Drive eats live StrawberryBerry only.")]
    public PlantStrawberry plantBush;
    [Tooltip("See a berry and crawl toward it, m.")]
    public float noticeRange = 6f;
    [Tooltip("Stop the look-crawl and chew when |dx| is this small, m.")]
    public float nibbleRange = 0.07f;
    [Tooltip("Chew only if the berry is within this height of the snout, m.")]
    public float nibbleHeight = 0.14f;
    [Tooltip("How often to rescan live berries, s. Not every frame.")]
    public float berryScanPeriod = 0.20f;

    [Header("Life")]
    public float ownStrength = 0.4f;

    public LifeState Life { get; private set; } = LifeState.Forage;

    private HedgehogStanceController control;
    private Transform cachedBerry;
    private float nextBerryScan = -1f;

    void Awake()
    {
        control = GetComponent<HedgehogStanceController>();
    }

    void Update()
    {
        if (HeadlessTrial.Active && !allowHeadless)
            return;
        if (control == null)
            control = GetComponent<HedgehogStanceController>();
        if (control == null || control.energyRest)
        {
            if (control != null)
            {
                control.EndWalk();
                control.ClearCrawlGoal();
            }
            return;
        }

        Vector2 mouth = control.MouthPoint();
        Transform berry = CurrentBerry(mouth);
        Life = OrganismLife.ChooseWake(false, false, false, false);

        if (berry == null)
        {
            control.EndWalk();
            control.ClearCrawlGoal();
            control.feedWanted = false;
            return;
        }

        Vector2 fruit = berry.position;
        float dx = fruit.x - mouth.x;
        float dy = fruit.y - mouth.y;
        control.crawlSign = dx >= 0f ? 1f : -1f;
        control.SetCrawlGoal(fruit.x, fruit);
        control.feedWanted = Mathf.Abs(dx) < 0.40f;

        bool atFruit = Mathf.Abs(dx) <= nibbleRange && Mathf.Abs(dy) <= nibbleHeight;
        if (atFruit)
        {
            // Legs stop paddling the picture. The stance still holds X
            // on the crawl goal so the snout stays under the berry.
            control.EndWalk();
            if (!control.IsChewing)
                control.Bite();
        }
        else
        {
            control.Walk();
        }
    }

    private Transform CurrentBerry(Vector2 mouth)
    {
        if (cachedBerry == null
            || Time.time >= nextBerryScan
            || !BerryStillEdible(cachedBerry))
        {
            cachedBerry = NearestPlantBerry(mouth);
            nextBerryScan = Time.time + Mathf.Max(0.05f, berryScanPeriod);
        }
        return cachedBerry;
    }

    private static bool BerryStillEdible(Transform berry)
    {
        if (berry == null)
            return false;
        StrawberryBerry fruit = berry.GetComponent<StrawberryBerry>();
        if (fruit == null || !fruit.edible)
            return false;
        return fruit.Life == null || !fruit.Life.IsDead;
    }

    private Transform NearestPlantBerry(Vector2 mouth)
    {
        if (plantBush != null && plantBush.BerryCount < 1)
            plantBush = null;

        StrawberryBerry[] berries = FindObjectsByType<StrawberryBerry>(FindObjectsSortMode.None);
        Transform best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < berries.Length; i++)
        {
            StrawberryBerry berry = berries[i];
            if (berry == null || !berry.edible)
                continue;
            if (berry.Life != null && berry.Life.IsDead)
                continue;
            Vector2 p = berry.transform.position;
            // Horizontal first: height is a nibble gate, not a detour.
            float score = Mathf.Abs(p.x - mouth.x) + 0.25f * Mathf.Abs(p.y - mouth.y);
            if (score >= bestScore)
                continue;
            float reach = Vector2.Distance(mouth, p);
            if (reach > noticeRange)
                continue;
            bestScore = score;
            best = berry.transform;
            plantBush = berry.GetComponentInParent<PlantStrawberry>();
        }
        return best;
    }
}
