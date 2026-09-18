using UnityEngine;

// One yard clock: sky / veil / discs / stars / dusk blush / distant flocks /
// campfire glow / quieter forage. Picture only. No Light2D (SceneLighting stays
// off), no forces, not 200 Hz. Ragdoll birds keep their brain: this only
// stretches public sit / fly / walk bouts. Sky flocks are not Bird.
[DefaultExecutionOrder(130)]
public class DayNightCycle : MonoBehaviour
{
    public const float DefaultCycleSeconds = 240f;

    [Header("Clock")]
    [Tooltip("Full day-plus-night, s. Noon at phase 0.")]
    public float cycleSeconds = DefaultCycleSeconds;
    [Tooltip("0 = noon, 0.5 = midnight.")]
    public float startPhase = 0.08f;

    [Header("Look")]
    public Color daySky = new Color(0.55f, 0.78f, 0.94f, 1f);
    public Color nightSky = new Color(0.07f, 0.09f, 0.16f, 1f);
    [Tooltip("Sky at the horizon window (Night01 near 0.5). Without this the clock is a dimmer.")]
    public Color duskSky = new Color(0.90f, 0.46f, 0.26f, 1f);
    public Color sunColor = new Color(1f, 0.92f, 0.55f, 1f);
    public Color duskSun = new Color(1f, 0.52f, 0.20f, 1f);
    public Color moonColor = new Color(0.72f, 0.80f, 0.95f, 1f);
    public Color duskGlow = new Color(1f, 0.42f, 0.12f, 1f);
    public Color duskHaze = new Color(0.70f, 0.32f, 0.18f, 1f);
    public Color duskCloud = new Color(0.82f, 0.50f, 0.42f, 1f);
    [Tooltip("How far the sky walks toward duskSky at the horizon peak.")]
    public float duskSkyMix = 0.62f;
    [Tooltip("Veil alpha at midnight. Sits under the fire, above dirt.")]
    public float veilAlpha = 0.42f;

    [Header("Birds")]
    [Tooltip("Sit bout scale at midnight. 1 — unchanged.")]
    public float nightSitScale = 2.4f;
    [Tooltip("Fly bout scale at midnight. Floor still leaves keep-away a short hop.")]
    public float nightFlyScale = 0.35f;
    [Tooltip("Walk bout scale at midnight (hens).")]
    public float nightWalkScale = 0.40f;

    public static float Night01 { get; private set; }
    public static DayNightCycle Instance { get; private set; }

    private const int VeilOrder = 8;
    private const int DiscOrder = -39;
    private const int GlowOrder = -39;
    private const int StarOrder = -39;
    private const int SkyBirdOrder = -38;
    private const int StarCount = 18;
    private const int SkyFlockCount = 2;
    private const int SkyBirdCount = 5;
    private const float BirdRefresh = 2f;
    private const float NightEps = 0.004f;
    private const float MinFly = 1.2f;
    private const float StarNightOn = 0.06f;
    private const float SkyBirdNightOff = 0.72f;
    private const float SkyBirdSpan = 26f;

    private Camera followCamera;
    private SpriteRenderer sky;
    private SpriteRenderer veil;
    private SpriteRenderer sun;
    private SpriteRenderer sunGlow;
    private SpriteRenderer moon;
    private SpriteRenderer haze;
    private Color hazeRest;
    private Color hazeDay;
    private SpriteRenderer[] clouds;
    private Color[] cloudsRest;
    private Color[] cloudsDay;
    private SpriteRenderer[] stars;
    private Vector2[] starHome;
    private float[] starPhase;
    private float[] starRate;
    private float[] starBaseA;
    private bool starsLit;
    private Transform[] skyFlocks;
    private Vector2[] skyFlockHome;
    private float[] skyFlockSpeed;
    private Transform[] skyWings;
    private float[] skyWingPhase;
    private float[] skyWingRate;
    private SpriteRenderer[] skyBirdSr;
    private bool skyBirdsLit;
    private SpriteRenderer[] hills;
    private Color[] hillsRest;
    private Color[] hillsDay;
    private Campfire fire;
    private BirdDrive[] drives;
    private float[] sitMin0;
    private float[] sitMax0;
    private float[] flyMin0;
    private float[] flyMax0;
    private float[] walkMin0;
    private float[] walkMax0;
    private BirdFlockDrive[] flocks;
    private float[] flockSitMin0;
    private float[] flockSitMax0;
    private float[] flockFlyMin0;
    private float[] flockFlyMax0;
    private float[] flockWalkMin0;
    private float[] flockWalkMax0;
    private float[] flockStartleMin0;
    private float[] flockStartleMax0;
    private float[] flockPlayMin0;
    private float[] flockPlayMax0;
    private float nextBirdBind;
    private float appliedNight = -1f;
    private bool looksBound;
    private bool birdsBound;

    public static DayNightCycle Ensure()
    {
        if (HeadlessTrial.Active)
            return null;
        if (Instance != null)
            return Instance;

        GameObject go = new GameObject("DayNightCycle");
        return go.AddComponent<DayNightCycle>();
    }

    void Awake()
    {
        Instance = this;
        Night01 = 0f;
    }

    void OnEnable()
    {
        Instance = this;
        BindLooks();
        TickClock(true);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Night01 = 0f;
        }
    }

    void LateUpdate()
    {
        TickClock(false);
    }

    private void TickClock(bool force)
    {
        float span = cycleSeconds > 8f ? cycleSeconds : DefaultCycleSeconds;
        float phase = startPhase + Time.time / span;
        phase -= Mathf.Floor(phase);
        float wave = Mathf.Cos(phase * (Mathf.PI * 2f));
        float night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, -0.28f, wave));
        Night01 = night;

        PlaceDiscs(phase);
        bool birdsNew = BindBirdsIfDue();
        if (!force && !birdsNew && Mathf.Abs(night - appliedNight) < NightEps)
            return;

        appliedNight = night;
        ApplyLook(night);
        ApplyFire(night);
        ApplyBirds(night);
    }

    private void BindLooks()
    {
        if (looksBound)
            return;
        looksBound = true;
        followCamera = Camera.main;

        GameObject skyGo = GameObject.Find("Sky");
        if (skyGo != null)
            sky = skyGo.GetComponent<SpriteRenderer>();

        GameObject parallax = GameObject.Find("ParallaxRoot");
        if (parallax != null)
        {
            SpriteRenderer[] found = parallax.GetComponentsInChildren<SpriteRenderer>(true);
            int n = 0;
            int nCloud = 0;
            for (int i = 0; i < found.Length; i++)
            {
                SpriteRenderer sr = found[i];
                if (sr == null || sr == sky)
                    continue;
                n++;
                if (IsCloudSprite(sr))
                    nCloud++;
            }

            hills = new SpriteRenderer[n];
            hillsRest = new Color[n];
            hillsDay = new Color[n];
            clouds = new SpriteRenderer[nCloud];
            cloudsRest = new Color[nCloud];
            cloudsDay = new Color[nCloud];
            int w = 0;
            int c = 0;
            for (int i = 0; i < found.Length; i++)
            {
                SpriteRenderer sr = found[i];
                if (sr == null || sr == sky)
                    continue;
                hills[w] = sr;
                hillsRest[w] = sr.color;
                hillsDay[w] = ToDay(sr.color);
                w++;
                if (sr.name == "Haze")
                {
                    haze = sr;
                    hazeRest = sr.color;
                    hazeDay = ToDay(sr.color);
                }

                if (IsCloudSprite(sr))
                {
                    clouds[c] = sr;
                    cloudsRest[c] = sr.color;
                    cloudsDay[c] = ToDay(sr.color);
                    c++;
                }
            }
        }

        fire = FindFirstObjectByType<Campfire>();
        BuildDiscs();
        BuildStars();
        BuildSkyBirds();
    }

    private void BuildDiscs()
    {
        veil = MakeSliced("NightVeil", new Vector2(48f, 22f), new Color(0.07f, 0.09f, 0.16f, 0f), VeilOrder);
        // Glow first so the disc paints on top at the same sorting order.
        sunGlow = MakeDisc("SunGlow", new Vector2(3.5f, 1.85f), duskGlow, GlowOrder);
        sunGlow.enabled = false;
        sun = MakeDisc("SunDisc", new Vector2(1.15f, 1.15f), sunColor, DiscOrder);
        moon = MakeDisc("MoonDisc", new Vector2(0.72f, 0.72f), moonColor, DiscOrder);
    }

    // Stars stay on this object, not under ParallaxRoot. The hill tint loop
    // brightens every backdrop sprite at noon; white dots on a day sky are wrong.
    private void BuildStars()
    {
        stars = new SpriteRenderer[StarCount];
        starHome = new Vector2[StarCount];
        starPhase = new float[StarCount];
        starRate = new float[StarCount];
        starBaseA = new float[StarCount];

        System.Random rng = new System.Random(53);
        for (int i = 0; i < StarCount; i++)
        {
            float ox = -9.1f + (float)rng.NextDouble() * 18.2f;
            float oy = 1.05f + (float)rng.NextDouble() * 3.85f;
            float s = 0.034f + (float)rng.NextDouble() * 0.055f;
            float a = 0.42f + (float)rng.NextDouble() * 0.48f;
            Color tint = new Color(0.86f, 0.90f, 0.98f, 0f);

            SpriteRenderer sr = MakeDisc("Star_" + i, new Vector2(s, s), tint, StarOrder);
            sr.enabled = false;
            stars[i] = sr;
            starHome[i] = new Vector2(ox, oy);
            starPhase[i] = (float)rng.NextDouble() * 6.28f;
            starRate[i] = 0.35f + (float)rng.NextDouble() * 0.85f;
            starBaseA[i] = a;
        }
    }

    private SpriteRenderer MakeSliced(string name, Vector2 size, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.color = color;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.sortingOrder = order;
        SpriteLighting.ApplyUnlit(sr);
        return sr;
    }

    private SpriteRenderer MakeDisc(string name, Vector2 size, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(sr, size, HumanVisualShape.Ellipse, null, color, order);
        SpriteLighting.ApplyUnlit(sr);
        return sr;
    }

    private void PlaceDiscs(float phase)
    {
        if (followCamera == null)
        {
            followCamera = Camera.main;
            if (followCamera == null)
                return;
        }

        Vector3 cam = followCamera.transform.position;
        float ang = phase * (Mathf.PI * 2f);
        float skyY = cam.y + 1.15f;
        float orbitX = 7.4f;
        float orbitY = 3.3f;

        float dusk = Dusk01(Night01);
        if (sun != null)
        {
            sun.transform.position = new Vector3(cam.x + Mathf.Sin(ang) * orbitX, skyY + Mathf.Cos(ang) * orbitY, 0f);
            Color c = Color.Lerp(sunColor, duskSun, dusk);
            c.a = 1f - Night01;
            sun.color = c;
            sun.enabled = c.a > 0.03f;
        }

        if (sunGlow != null)
        {
            if (sun != null)
                sunGlow.transform.position = sun.transform.position;
            Color gc = duskGlow;
            gc.a = dusk * (1f - Night01 * 0.30f) * 0.48f;
            sunGlow.color = gc;
            sunGlow.enabled = gc.a > 0.02f;
        }

        if (moon != null)
        {
            moon.transform.position = new Vector3(cam.x - Mathf.Sin(ang) * orbitX, skyY - Mathf.Cos(ang) * orbitY, 0f);
            Color c = moonColor;
            c.a = Night01;
            moon.color = c;
            moon.enabled = c.a > 0.03f;
        }

        if (veil != null)
        {
            veil.transform.position = new Vector3(cam.x, cam.y + 0.4f, 0f);
            Color c = veil.color;
            c.a = Night01 * veilAlpha;
            veil.color = c;
            veil.enabled = c.a > 0.01f;
        }

        PlaceStars(cam);
        PlaceSkyBirds(cam);
    }

    private void PlaceStars(Vector3 cam)
    {
        if (stars == null)
            return;

        bool lit = Night01 > StarNightOn;
        if (!lit)
        {
            if (!starsLit)
                return;
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null)
                    stars[i].enabled = false;
            }
            starsLit = false;
            return;
        }

        starsLit = true;
        float t = Time.time;
        float night = Night01;
        for (int i = 0; i < stars.Length; i++)
        {
            SpriteRenderer sr = stars[i];
            if (sr == null)
                continue;
            Vector2 h = starHome[i];
            sr.transform.position = new Vector3(cam.x + h.x, cam.y + h.y, 0f);
            float tw = 0.70f + 0.30f * Mathf.Sin(t * starRate[i] + starPhase[i]);
            Color c = sr.color;
            c.a = night * starBaseA[i] * tw;
            sr.color = c;
            sr.enabled = c.a > 0.02f;
        }
    }

    // Side-view dashes, not a top-down V: X is forward, there is no lateral
    // axis. Two flocks so the stamp does not read as one decal. They live
    // here, not under ParallaxRoot — hill noon-tint would bleach them into
    // clouds. Not Bird: no Rigidbody2D, no mode, no flap lift.
    private void BuildSkyBirds()
    {
        skyFlocks = new Transform[SkyFlockCount];
        skyFlockHome = new Vector2[SkyFlockCount];
        skyFlockSpeed = new float[SkyFlockCount];
        skyWings = new Transform[SkyBirdCount];
        skyWingPhase = new float[SkyBirdCount];
        skyWingRate = new float[SkyBirdCount];
        skyBirdSr = new SpriteRenderer[SkyBirdCount * 2];

        skyFlockHome[0] = new Vector2(-3.6f, 2.55f);
        skyFlockSpeed[0] = 0.68f;
        skyFlocks[0] = MakeSkyFlockRoot("SkyFlockA", skyFlockHome[0]);
        BindSkyBird(skyFlocks[0], 0, 0, new Vector2(0.62f, 0f));
        BindSkyBird(skyFlocks[0], 1, 1, new Vector2(-0.28f, 0.32f));
        BindSkyBird(skyFlocks[0], 2, 2, new Vector2(-0.34f, -0.24f));

        skyFlockHome[1] = new Vector2(7.4f, 3.35f);
        skyFlockSpeed[1] = 0.46f;
        skyFlocks[1] = MakeSkyFlockRoot("SkyFlockB", skyFlockHome[1]);
        BindSkyBird(skyFlocks[1], 3, 0, new Vector2(0.48f, 0.10f));
        BindSkyBird(skyFlocks[1], 4, 1, new Vector2(-0.42f, -0.18f));
    }

    private Transform MakeSkyFlockRoot(string name, Vector2 home)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(home.x, home.y, 0f);
        return root.transform;
    }

    private void BindSkyBird(Transform flock, int index, int slot, Vector2 local)
    {
        Transform wing;
        SpriteRenderer bodySr;
        SpriteRenderer wingSr;
        MakeSkyBird(flock, "SkyBird_" + index, local, out wing, out bodySr, out wingSr);
        skyWings[index] = wing;
        skyWingPhase[index] = slot * 0.85f + flock.localPosition.x * 0.07f;
        skyWingRate[index] = 4.2f + slot * 0.55f;
        skyBirdSr[index * 2] = bodySr;
        skyBirdSr[index * 2 + 1] = wingSr;
    }

    private Transform MakeSkyBird(
        Transform parent,
        string name,
        Vector2 local,
        out Transform wing,
        out SpriteRenderer bodySr,
        out SpriteRenderer wingSr)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(local.x, local.y, 0f);

        Color tint = new Color(0.10f, 0.11f, 0.14f, 0f);
        bodySr = MakeSkyPart(root.transform, "Body", new Vector2(0.26f, 0.07f), Vector3.zero, 8f, tint, HumanVisualShape.Ellipse, SkyBirdOrder);
        wingSr = MakeSkyPart(
            root.transform,
            "Wing",
            new Vector2(0.18f, 0.055f),
            new Vector3(-0.02f, 0.018f, 0f),
            22f,
            tint,
            HumanVisualShape.Box,
            SkyBirdOrder + 1);
        wing = wingSr.transform;
        bodySr.enabled = false;
        wingSr.enabled = false;
        return root.transform;
    }

    private static SpriteRenderer MakeSkyPart(
        Transform parent,
        string name,
        Vector2 size,
        Vector3 local,
        float tilt,
        Color tint,
        HumanVisualShape shape,
        int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(sr, size, shape, null, tint, order);
        SpriteLighting.ApplyUnlit(sr);
        return sr;
    }

    private void PlaceSkyBirds(Vector3 cam)
    {
        if (skyFlocks == null)
            return;

        bool lit = Night01 < SkyBirdNightOff;
        if (!lit)
        {
            if (!skyBirdsLit)
                return;
            SetSkyBirdsEnabled(false);
            skyBirdsLit = false;
            return;
        }

        skyBirdsLit = true;
        float t = Time.time;
        float day = 1f - Night01;
        float a = day * 0.88f;

        for (int i = 0; i < skyFlocks.Length; i++)
        {
            Transform flock = skyFlocks[i];
            if (flock == null)
                continue;

            float ox = skyFlockHome[i].x + t * skyFlockSpeed[i];
            ox -= SkyBirdSpan * Mathf.Round(ox / SkyBirdSpan);
            float oy = skyFlockHome[i].y + Mathf.Sin(t * 0.31f + i * 1.7f) * 0.09f;
            flock.position = new Vector3(cam.x + ox, cam.y + oy, 0f);
        }

        for (int i = 0; i < skyBirdSr.Length; i++)
        {
            SpriteRenderer sr = skyBirdSr[i];
            if (sr == null)
                continue;
            Color c = sr.color;
            c.a = a;
            sr.color = c;
            sr.enabled = a > 0.03f;
        }

        for (int i = 0; i < skyWings.Length; i++)
        {
            Transform wing = skyWings[i];
            if (wing == null)
                continue;
            float flap = Mathf.Sin(t * skyWingRate[i] + skyWingPhase[i]) * 16f;
            wing.localRotation = Quaternion.Euler(0f, 0f, 22f + flap);
        }
    }

    private void SetSkyBirdsEnabled(bool on)
    {
        if (skyBirdSr == null)
            return;
        for (int i = 0; i < skyBirdSr.Length; i++)
        {
            if (skyBirdSr[i] != null)
                skyBirdSr[i].enabled = on;
        }
    }

    private void ApplyLook(float night)
    {
        float dusk = Dusk01(night);
        Color skyNow = Color.Lerp(daySky, nightSky, night);
        if (dusk > 0.01f)
            skyNow = Color.Lerp(skyNow, duskSky, dusk * duskSkyMix);
        if (followCamera != null)
            followCamera.backgroundColor = skyNow;
        if (sky != null)
        {
            bool textured = LevelArt.Sky() != null;
            Color skyPaint = textured
                ? Color.Lerp(Color.white, new Color(0.42f, 0.50f, 0.78f, 1f), night)
                : skyNow;
            if (textured && dusk > 0.01f)
                skyPaint = Color.Lerp(skyPaint, duskSky, dusk * duskSkyMix * 0.55f);
            sky.color = skyPaint;
        }

        if (hills != null)
        {
            for (int i = 0; i < hills.Length; i++)
            {
                if (hills[i] == null)
                    continue;
                hills[i].color = Color.Lerp(hillsDay[i], hillsRest[i], night);
            }
        }

        if (haze != null)
        {
            Color h = Color.Lerp(hazeDay, hazeRest, night);
            if (dusk > 0.01f)
                h = Color.Lerp(h, duskHaze, dusk * 0.72f);
            haze.color = h;
        }

        if (clouds == null)
            return;
        for (int i = 0; i < clouds.Length; i++)
        {
            if (clouds[i] == null)
                continue;
            Color c = Color.Lerp(cloudsDay[i], cloudsRest[i], night);
            if (dusk > 0.01f)
            {
                c = Color.Lerp(c, duskCloud, dusk * 0.50f);
                c.a = cloudsRest[i].a;
            }

            clouds[i].color = c;
        }
    }

    private void ApplyFire(float night)
    {
        if (fire != null)
            fire.ApplyNight(night);
    }

    private bool BindBirdsIfDue()
    {
        if (birdsBound && CacheAlive())
            return false;

        float now = Time.time;
        if (now < nextBirdBind)
            return false;

        if (fire == null)
            fire = FindFirstObjectByType<Campfire>();

        BirdDrive[] foundDrives = FindObjectsByType<BirdDrive>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        BirdFlockDrive[] foundFlocks = FindObjectsByType<BirdFlockDrive>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (foundDrives.Length == 0 && foundFlocks.Length == 0)
        {
            nextBirdBind = now + 0.25f;
            return false;
        }

        nextBirdBind = now + BirdRefresh;

        CaptureDrives(foundDrives);
        CaptureFlocks(foundFlocks);
        birdsBound = true;
        return true;
    }

    private bool CacheAlive()
    {
        if (drives != null)
        {
            for (int i = 0; i < drives.Length; i++)
            {
                if (drives[i] != null)
                    return true;
            }
        }

        if (flocks != null)
        {
            for (int i = 0; i < flocks.Length; i++)
            {
                if (flocks[i] != null)
                    return true;
            }
        }

        return false;
    }

    private void CaptureDrives(BirdDrive[] found)
    {
        drives = found;
        int n = drives.Length;
        sitMin0 = new float[n];
        sitMax0 = new float[n];
        flyMin0 = new float[n];
        flyMax0 = new float[n];
        walkMin0 = new float[n];
        walkMax0 = new float[n];
        for (int i = 0; i < n; i++)
        {
            BirdDrive d = drives[i];
            sitMin0[i] = d.sitMin;
            sitMax0[i] = d.sitMax;
            flyMin0[i] = d.flyMin;
            flyMax0[i] = d.flyMax;
            walkMin0[i] = d.walkMin;
            walkMax0[i] = d.walkMax;
        }
    }

    private void CaptureFlocks(BirdFlockDrive[] found)
    {
        flocks = found;
        int n = flocks.Length;
        flockSitMin0 = new float[n];
        flockSitMax0 = new float[n];
        flockFlyMin0 = new float[n];
        flockFlyMax0 = new float[n];
        flockWalkMin0 = new float[n];
        flockWalkMax0 = new float[n];
        flockStartleMin0 = new float[n];
        flockStartleMax0 = new float[n];
        flockPlayMin0 = new float[n];
        flockPlayMax0 = new float[n];
        for (int i = 0; i < n; i++)
        {
            BirdFlockDrive f = flocks[i];
            flockSitMin0[i] = f.sitMin;
            flockSitMax0[i] = f.sitMax;
            flockFlyMin0[i] = f.flyMin;
            flockFlyMax0[i] = f.flyMax;
            flockWalkMin0[i] = f.walkMin;
            flockWalkMax0[i] = f.walkMax;
            flockStartleMin0[i] = f.startleMin;
            flockStartleMax0[i] = f.startleMax;
            flockPlayMin0[i] = f.playMin;
            flockPlayMax0[i] = f.playMax;
        }
    }

    private void ApplyBirds(float night)
    {
        float sit = Mathf.Lerp(1f, nightSitScale, night);
        float fly = Mathf.Lerp(1f, nightFlyScale, night);
        float walk = Mathf.Lerp(1f, nightWalkScale, night);
        float hush = Mathf.Lerp(1f, 2.6f, night);

        if (drives != null)
        {
            for (int i = 0; i < drives.Length; i++)
            {
                BirdDrive d = drives[i];
                if (d == null)
                    continue;
                d.sitMin = sitMin0[i] * sit;
                d.sitMax = sitMax0[i] * sit;
                d.flyMin = Mathf.Max(MinFly, flyMin0[i] * fly);
                d.flyMax = Mathf.Max(d.flyMin + 0.4f, flyMax0[i] * fly);
                d.walkMin = walkMin0[i] * walk;
                d.walkMax = Mathf.Max(d.walkMin + 0.2f, walkMax0[i] * walk);
            }
        }

        if (flocks != null)
        {
            for (int i = 0; i < flocks.Length; i++)
            {
                BirdFlockDrive f = flocks[i];
                if (f == null)
                    continue;
                f.sitMin = flockSitMin0[i] * sit;
                f.sitMax = flockSitMax0[i] * sit;
                f.flyMin = Mathf.Max(MinFly, flockFlyMin0[i] * fly);
                f.flyMax = Mathf.Max(f.flyMin + 0.4f, flockFlyMax0[i] * fly);
                f.walkMin = flockWalkMin0[i] * walk;
                f.walkMax = Mathf.Max(f.walkMin + 0.2f, flockWalkMax0[i] * walk);
                f.startleMin = flockStartleMin0[i] * hush;
                f.startleMax = flockStartleMax0[i] * hush;
                f.playMin = flockPlayMin0[i] * hush;
                f.playMax = flockPlayMax0[i] * hush;
            }
        }
    }

    private static Color ToDay(Color night)
    {
        return new Color(
            Mathf.Clamp01(night.r * 1.65f + 0.10f),
            Mathf.Clamp01(night.g * 1.52f + 0.12f),
            Mathf.Clamp01(night.b * 1.18f + 0.16f),
            night.a);
    }

    // Peaks at Night01 = 0.5 (sun on the horizon). Noon and midnight stay 0.
    private static float Dusk01(float night)
    {
        float t = 1f - Mathf.Abs(night * 2f - 1f);
        return t < 0f ? 0f : t;
    }

    private static bool IsCloudSprite(SpriteRenderer sr)
    {
        Transform p = sr.transform.parent;
        if (p == null)
            return false;
        Transform layer = p.parent;
        return layer != null && layer.name == "Clouds";
    }

}
