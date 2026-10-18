using UnityEngine;

// Yard parallax. Picture only, no collider and no Rigidbody2D.
// LateUpdate after CameraFollow: the layer moves as camX * follow.
// follow → 1 — almost glued to the camera (horizon almost still on screen).
// follow → 0 — like the ground (slides away fast). Yard PlantTree do not go here.
[DefaultExecutionOrder(110)]
public class LevelBackdrop : MonoBehaviour
{
    private struct Layer
    {
        public Transform root;
        public float follow;
    }

    private Layer[] layers;
    private Camera followCamera;
    private bool cameraRetryUsed;

    // High cloud belt. Local X is wind; wrap keeps a short strip around the camera.
    private Transform[] cloudPuffs;
    private float[] cloudHomeX;
    private float[] cloudHomeY;
    private float[] cloudSpeed;
    private const float CloudSpan = 96f;
    private const int CloudOrder = -37;

    public static void Install(Transform levelRoot)
    {
        GameObject oldHills = GameObject.Find("FarHills");
        if (oldHills != null)
            Object.Destroy(oldHills);

        GameObject root = GameObject.Find("ParallaxRoot");
        if (root == null)
            root = new GameObject("ParallaxRoot");

        LevelBackdrop backdrop = root.GetComponent<LevelBackdrop>();
        if (backdrop == null)
            backdrop = root.AddComponent<LevelBackdrop>();
        backdrop.Build();
        // levelRoot is not the parent: Level chunks must not carry the mountains away.
        _ = levelRoot;
    }

    void OnEnable()
    {
        CacheFollowCamera();
    }

    private void CacheFollowCamera()
    {
        followCamera = Camera.main;
        cameraRetryUsed = false;
    }

    private void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        CacheFollowCamera();
        layers = new Layer[9];

        // Far layers — large follow: almost still on screen.
        // Near woods — small follow: slide away faster, depth reads.
        // Clouds sit just in front of the sky so they occult the sun disc
        // and stay behind the mountain silhouettes (−36).
        layers[0] = MakeLayer("Sky", 1.00f);
        layers[1] = MakeLayer("Clouds", 0.985f);
        layers[2] = MakeLayer("Horizon", 0.97f);
        layers[3] = MakeLayer("FarMountains", 0.91f);
        layers[4] = MakeLayer("MidMountains", 0.80f);
        layers[5] = MakeLayer("FarHills", 0.64f);
        layers[6] = MakeLayer("NearHills", 0.48f);
        layers[7] = MakeLayer("FarWoods", 0.33f);
        layers[8] = MakeLayer("NearWoods", 0.18f);

        BuildSky(layers[0].root);
        BuildClouds(layers[1].root);
        BuildHorizon(layers[2].root);
        BuildMountains(layers[3].root, -36, new Color(0.11f, 0.13f, 0.20f, 1f), 4.6f, 7.2f, 11);
        BuildMountains(layers[4].root, -34, new Color(0.13f, 0.15f, 0.22f, 1f), 3.2f, 5.4f, 13);
        BuildHills(layers[5].root, -32, new Color(0.10f, 0.15f, 0.13f, 1f), 1.6f, 2.6f, 14);
        BuildHills(layers[6].root, -30, new Color(0.09f, 0.14f, 0.11f, 1f), 1.1f, 1.9f, 16);
        BuildWoods(layers[7].root, -28, new Color(0.06f, 0.09f, 0.07f, 1f), 0.9f, 1.6f, 22, 3.6f);
        BuildWoods(layers[8].root, -26, new Color(0.05f, 0.08f, 0.06f, 1f), 0.7f, 1.35f, 28, 2.4f);
    }

    private Layer MakeLayer(string name, float follow)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;
        return new Layer { root = go.transform, follow = follow };
    }

    private void BuildSky(Transform parent)
    {
        GameObject sky = GameObject.Find("Sky");
        if (sky == null)
        {
            sky = new GameObject("Sky");
            sky.AddComponent<SpriteRenderer>();
        }

        sky.transform.SetParent(parent, false);
        sky.transform.localPosition = new Vector3(0f, 6f, 0f);
        sky.transform.localScale = Vector3.one;

        SpriteRenderer sr = sky.GetComponent<SpriteRenderer>();
        sr.sprite = LevelArt.Sky() != null ? LevelArt.Sky() : LevelArt.White();
        sr.color = LevelArt.Sky() != null ? Color.white : SceneLighting.NightSky;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(120f, 24f);
        sr.sortingOrder = -40;
        SpriteLighting.ApplyUnlit(sr);
    }

    private void BuildHorizon(Transform parent)
    {
        // Strip above the ground: far haze, almost does not move.
        SpawnBox(parent, "Haze", 0f, 1.8f, 140f, 2.4f, new Color(0.10f, 0.12f, 0.20f, 1f), -38);
    }

    private void BuildClouds(Transform parent)
    {
        const int count = 7;
        cloudPuffs = new Transform[count];
        cloudHomeX = new float[count];
        cloudHomeY = new float[count];
        cloudSpeed = new float[count];

        // Rest color is the night look. DayNightCycle.ToDay brightens the
        // same SpriteRenderers at noon, so the belt rides the yard clock
        // without a second Find.
        System.Random rng = new System.Random(91);
        float x = -46f;
        for (int i = 0; i < count; i++)
        {
            float w = 3.4f + (float)rng.NextDouble() * 4.2f;
            float h = 0.52f + (float)rng.NextDouble() * 0.50f;
            float y = 2.7f + (float)rng.NextDouble() * 3.2f;
            float a = 0.38f + (float)rng.NextDouble() * 0.12f;
            Color tint = new Color(0.52f, 0.56f, 0.64f, a);

            GameObject puff = new GameObject("Cloud_" + i);
            puff.transform.SetParent(parent, false);
            puff.transform.localPosition = new Vector3(x + w * 0.5f, y, 0f);

            SpawnBox(puff.transform, "Body", 0f, 0f, w, h, tint, CloudOrder);
            if (rng.NextDouble() > 0.22)
            {
                float w2 = w * (0.38f + (float)rng.NextDouble() * 0.30f);
                float h2 = h * (0.68f + (float)rng.NextDouble() * 0.38f);
                float ox = ((float)rng.NextDouble() - 0.5f) * w * 0.42f;
                SpawnBox(puff.transform, "Bump", ox, h * 0.28f, w2, h2, tint, CloudOrder + 1);
            }

            cloudPuffs[i] = puff.transform;
            cloudHomeX[i] = puff.transform.localPosition.x;
            cloudHomeY[i] = y;
            cloudSpeed[i] = 0.22f + (float)rng.NextDouble() * 0.38f;
            x += w * 0.82f + 1.4f;
        }
    }

    private void BuildMountains(Transform parent, int order, Color color, float minH, float maxH, int count)
    {
        System.Random rng = new System.Random(17 + order);
        float x = -70f;
        for (int i = 0; i < count; i++)
        {
            float w = 7f + (float)rng.NextDouble() * 9f;
            float h = minH + (float)rng.NextDouble() * (maxH - minH);
            float baseY = LevelGenerator.DefaultSurfaceY + 0.15f;
            SpawnBox(parent, "Peak_" + i, x + w * 0.5f, baseY + h * 0.5f, w, h, color, order);
            // Second peak a bit higher — a ridge silhouette, not one rectangle.
            if (rng.NextDouble() > 0.35)
            {
                float w2 = w * 0.45f;
                float h2 = h * (0.55f + (float)rng.NextDouble() * 0.35f);
                SpawnBox(
                    parent,
                    "PeakTop_" + i,
                    x + w * (0.3f + (float)rng.NextDouble() * 0.4f),
                    baseY + h * 0.35f + h2 * 0.5f,
                    w2,
                    h2,
                    color * 0.88f,
                    order + 1);
            }

            x += w * 0.62f;
        }
    }

    private void BuildHills(Transform parent, int order, Color color, float minH, float maxH, int count)
    {
        System.Random rng = new System.Random(41 + order);
        float x = -68f;
        for (int i = 0; i < count; i++)
        {
            float w = 4.5f + (float)rng.NextDouble() * 5.5f;
            float h = minH + (float)rng.NextDouble() * (maxH - minH);
            float baseY = LevelGenerator.DefaultSurfaceY + 0.05f;
            SpawnBox(parent, "Hill_" + i, x + w * 0.5f, baseY + h * 0.5f, w, h, color, order);
            x += w * 0.7f;
        }
    }

    private void BuildWoods(Transform parent, int order, Color color, float minH, float maxH, int count, float step)
    {
        System.Random rng = new System.Random(73 + order);
        float x = -66f;
        for (int i = 0; i < count; i++)
        {
            float h = minH + (float)rng.NextDouble() * (maxH - minH);
            float trunkW = 0.10f + (float)rng.NextDouble() * 0.08f;
            float crownW = 0.55f + (float)rng.NextDouble() * 0.45f;
            float crownH = h * 0.55f;
            float baseY = LevelGenerator.DefaultSurfaceY;
            float jitter = ((float)rng.NextDouble() - 0.5f) * 0.8f;
            float px = x + jitter;
            SpawnBox(parent, "Trunk_" + i, px, baseY + h * 0.35f, trunkW, h * 0.7f, color, order);
            SpawnBox(parent, "Crown_" + i, px, baseY + h * 0.55f + crownH * 0.35f, crownW, crownH, color, order + 1);
            x += step;
        }
    }

    private static void SpawnBox(
        Transform parent,
        string name,
        float x,
        float y,
        float width,
        float height,
        Color color,
        int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, y, 0f);
        go.transform.localScale = Vector3.one;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.color = color;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(width, height);
        sr.sortingOrder = sortingOrder;
        SpriteLighting.ApplyUnlit(sr);
    }

    void LateUpdate()
    {
        if (followCamera == null)
        {
            if (cameraRetryUsed)
                return;
            followCamera = Camera.main;
            cameraRetryUsed = true;
            if (followCamera == null)
                return;
        }
        else
            cameraRetryUsed = false;

        if (layers == null)
            return;

        float camX = followCamera.transform.position.x;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].root == null)
                continue;
            Vector3 p = layers[i].root.position;
            p.x = camX * layers[i].follow;
            p.y = 0f;
            layers[i].root.position = p;
        }

        TickClouds(camX);
    }

    private void TickClouds(float camX)
    {
        if (cloudPuffs == null || layers == null || layers.Length < 2)
            return;

        Transform layer = layers[1].root;
        if (layer == null)
            return;

        float layerX = layer.position.x;
        float t = Time.time;
        for (int i = 0; i < cloudPuffs.Length; i++)
        {
            Transform puff = cloudPuffs[i];
            if (puff == null)
                continue;

            float x = cloudHomeX[i] + t * cloudSpeed[i];
            float worldX = layerX + x;
            float dx = worldX - camX;
            dx -= CloudSpan * Mathf.Round(dx / CloudSpan);

            Vector3 p = puff.localPosition;
            p.x = camX + dx - layerX;
            p.y = cloudHomeY[i] + Mathf.Sin(t * 0.17f + i * 0.9f) * 0.07f;
            puff.localPosition = p;
        }
    }
}
