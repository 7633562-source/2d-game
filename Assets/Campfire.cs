using UnityEngine;
using UnityEngine.Rendering.Universal;

// Yard campfire: picture logs + tongue pool + Unlit ParticleSystem.
// Tongues are a fixed sprite pool (recycle by age). Sparks, smoke, and
// embers are three ParticleSystems (internal pools, no Instantiate).
// Sprite smoke boxes were removed: HumanSegment.ApplyPicture ignores
// Ellipse, so five cool-gray rectangles read as blue slabs on NightSky.
// No Rigidbody2D, no collider, no heat force.
[DefaultExecutionOrder(120)]
public class Campfire : MonoBehaviour
{
    public const float YardX = 32.5f;
    public const float CullX = 18f;
    public const float LabX = 0f;

    // The fire's own sorting bands. LogOrder / EmberOrder / FireOrder are public
    // because the gallery's FIRE row draws the assembled fire at these bands
    // (Campfire.FrozenStack), so the catalogue cannot drift from the yard.
    public const int LogOrder = 12;
    private const int GlowOrder = 16;
    public const int EmberOrder = 18;
    private const int SmokeOrder = 20;
    public const int FireOrder = 22;
    private const int SparkOrder = 24;

    // req-342 -level1FireBehind 1: the whole fire moves behind the actors and
    // keeps its internal order (logs, glow, ember, smoke, fire, sparks, then the
    // +1..+4 core and ember pieces): 12..26 -> -33..-28. Flag off = the value.
    public static int LookOrder(int order)
    {
        if (!Level1LookFlags.FireBehind)
            return order;
        if (order <= 13) return -33;
        if (order <= 17) return -32;
        if (order <= 19) return -31;
        if (order <= 21) return -30;
        if (order <= 24) return -29;
        return -28;
    }
    public const int TongueCount = 24;
    private const float SmokeWind = 0.28f;

    // The pieces Build() and BuildLogs() stand on the ground, in metres from the
    // fire's own root (the feet of the logs). Public so the gallery draws them at
    // the yard's own heights instead of retyping them.
    public const float EmberBoxWidth = 0.18f;
    public const float EmberBoxHeight = 0.24f;
    public const float EmberCenterY = 0.18f;
    public const float CoreCenterY = 0.36f;
    public static readonly Color EmberTint = new Color(1f, 0.38f, 0.08f, 1f);
    public static readonly Color LogWood = new Color(0.50f, 0.25f, 0f, 0.5f);

    [Header("Light")]
    public float baseIntensity = 0.032f;
    public float baseOuterRadius = 2.6f;
    public float innerRadius = 0.10f;
    public float flickerSpeed = 7.1f;
    public float intensityJitter = 0.16f;
    public float radiusJitter = 0.07f;

    [Header("Particles")]
    public int sparkMax = 32;
    public int smokeMax = 20;
    public int emberMax = 16;
    public float sparkRate = 18f;
    public float smokeRate = 6f;
    public float emberRate = 8f;

    private Light2D flameLight;
    private SpriteRenderer ember;
    private SpriteRenderer glow;
    private Transform flameVisual;
    private float emberW;
    private float emberH;
    private float flameW;
    private float flameH;
    private float glowW;
    private float glowH;
    private Transform[] tongues;
    private SpriteRenderer[] tongueSr;
    private float[] tongueAge;
    private float[] tongueLife;
    private float[] tonguePhase;
    private float[] tongueLean;
    private float[] tongueX0;
    private float[] tongueRise;
    private float[] tongueW;
    private float[] tongueH;
    private float[] tongueHeat;
    private ParticleSystem sparksPs;
    private ParticleSystem smokePs;
    private ParticleSystem embersPs;
    private Transform mothHost;
    private Camera followCamera;
    private float noiseOffset;
    private float glowNight;
    private bool emitting = true;
    private bool built;

    private static Texture2D SoftDiscTex;
    private static Sprite SoftDiscSprite;
    private static Sprite PaintedCore;
    private static bool PaintedCoreChecked;
    private static Material ParticleMat;

    // ART-0 gap 2 (19.09): the flame core is painted art - Art/Campfire/core.png,
    // "Campfire/core" for ArtLibrary.Albedo. The 24 pooled tongues deliberately
    // keep the soft disc: at 15-30 px on screen a painted tongue reads as a
    // spindle and the fire falls apart (three blind reviews, see
    // Tools/gen-fire-textures.py and Trials/Runs/art-fire-2026-09-19/). The core
    // is one sprite drawn in front, so a silhouette has room: it gives the fire
    // the licked crown the disc cannot. A missing PNG keeps today's disc, and no
    // collider, joint, mass or Body is involved either way - this file has none.
    //
    // The gallery's fire row (GalleryScene.BuildFireRow) reads the same key, the
    // same box and the same tint, so the catalogue cannot drift from the fire the
    // game actually shows.
    //
    // ART-0 nineteenth pass: the three LOGS are painted art too, keyed the same way
    // (Art/Campfire/log_*.png). BuildLogs drew each log as a collider-sized white
    // rectangle tinted LogWood = (0.50, 0.25, 0.00, 0.5) - a flat HALF-TRANSPARENT
    // brown box - and a read of the shipped FIRE frame named exactly that ("flat
    // semi-transparent rectangles, no bark, no cut ends, you can see their
    // intersections"): the original complaint in miniature. One canvas per log, in
    // that log's own box aspect (LogRow), because a Sliced sprite maps the whole
    // canvas onto the box and a shared canvas would stretch the short log against
    // the long one. A missing PNG leaves today's box, so the fire never has a hole.
    public static readonly string[] LogArtKeys =
    {
        "Campfire/log_a", "Campfire/log_b", "Campfire/log_c"
    };

    public const string CoreArtKey = "Campfire/core";
    public const float CoreBoxWidth = 0.16f;
    public const float CoreBoxHeight = 0.38f;
    public static readonly Color CoreTint = new Color(1f, 0.94f, 0.72f, 1f);

    private static readonly Color TongueWhite = new Color(1.00f, 0.97f, 0.88f, 1f);
    private static readonly Color TongueYellow = new Color(1.00f, 0.86f, 0.28f, 1f);
    private static readonly Color TongueOrange = new Color(1.00f, 0.50f, 0.08f, 1f);
    private static readonly Color TongueRed = new Color(0.92f, 0.16f, 0.03f, 1f);

    // ─── the assembled fire as a frozen picture (ART-0) ────────────────────
    // The gallery's FIRE row draws the fire the yard shows: the three log boxes,
    // the ember disc, the 24 tongue discs of the pool and the painted core, in the
    // pool's own sorting bands and at the heights Build() places them. The live
    // flicker is Perlin noise on Time.time, so a live fire would not repeat frame
    // for frame; this stack is frozen at FrozenStackTime with the flicker pair at
    // its mean. Nothing here ticks, and nothing here is a body: the fire has no
    // collider and no Rigidbody2D, so this moves no physics number.
    //
    // The pool's geometry and its pose live once, in PoolRow and TonguePicture,
    // and the logs' in LogRow. TickTongues, BuildTongues and BuildLogs call the
    // same three, and Tools/art0-fire-stack-check.py reads this file and fails if
    // a literal of any of them moves - a catalogue that drifts from the fire is
    // worse than no catalogue.
    public const float FrozenStackTime = 0.70f;
    public const float FrozenStackFlicker = 0.50f;

    public enum FirePieceKind
    {
        Log = 0,
        Disc = 1,
        Core = 2
    }

    public struct FirePiece
    {
        public string name;
        public FirePieceKind kind;
        public Vector3 local;
        public float tilt;
        public Vector2 size;
        public Color color;
        public int order;
        public bool on;
    }

    private static FirePiece[] frozenStack;

    // Bottom to top: the three logs, the ember, the 24 tongues, the painted core.
    public static FirePiece[] FrozenStack()
    {
        if (frozenStack != null)
            return frozenStack;

        FirePiece[] pieces = new FirePiece[3 + 1 + TongueCount + 1];
        int k = 0;

        for (int i = 0; i < 3; i++)
        {
            LogRow(i, out string logName, out Vector2 logSize, out Vector3 logLocal, out float logTilt);
            LogPiece(pieces, ref k, logName, logSize, logLocal, logTilt);
        }

        DiscPiece(pieces, ref k, FirePieceKind.Disc, "Ember",
            new Vector2(EmberBoxWidth, EmberBoxHeight), new Vector3(0f, EmberCenterY, 0f), 0f,
            EmberTint, LookOrder(EmberOrder));

        for (int i = 0; i < TongueCount; i++)
        {
            float w;
            float h;
            int order;
            float heat;
            float life;
            float phase;
            float lean;
            float x0;
            float rise;
            PoolRow(i, out w, out h, out order, out heat, out life, out phase, out lean, out x0, out rise);

            // The age the pool is born with (BuildTongues), advanced by
            // FrozenStackTime and wrapped the way TickTongues wraps it.
            float age = life * (i / (float)TongueCount) + FrozenStackTime;
            if (age >= life)
                age -= life;

            TonguePicture(age / life, FrozenStackTime, FrozenStackFlicker, FrozenStackFlicker,
                phase, lean, x0, rise, w, h, heat,
                out Vector3 local, out float tilt, out float scaleX, out float scaleY,
                out Color color, out bool on);

            pieces[k].name = "Tongue_" + i;
            pieces[k].kind = FirePieceKind.Disc;
            pieces[k].local = local;
            pieces[k].tilt = tilt;
            pieces[k].size = new Vector2(scaleX, scaleY);
            pieces[k].color = color;
            pieces[k].order = order;
            pieces[k].on = on;
            k++;
        }

        DiscPiece(pieces, ref k, FirePieceKind.Core, "FlameCore",
            new Vector2(CoreBoxWidth, CoreBoxHeight), new Vector3(0f, CoreCenterY, 0f), 0f,
            CoreTint, LookOrder(FireOrder + 4));

        frozenStack = pieces;
        return pieces;
    }

    private static void LogPiece(FirePiece[] into, ref int k, string name, Vector2 size, Vector3 local, float tilt)
    {
        into[k].name = name;
        into[k].kind = FirePieceKind.Log;
        into[k].size = size;
        into[k].local = local;
        into[k].tilt = tilt;
        into[k].color = LogWood;
        into[k].order = LookOrder(LogOrder);
        into[k].on = true;
        k++;
    }

    private static void DiscPiece(FirePiece[] into, ref int k, FirePieceKind kind, string name,
        Vector2 size, Vector3 local, float tilt, Color color, int order)
    {
        into[k].name = name;
        into[k].kind = kind;
        into[k].size = size;
        into[k].local = local;
        into[k].tilt = tilt;
        into[k].color = color;
        into[k].order = order;
        into[k].on = true;
        k++;
    }

    public static Campfire EnsureOnYard()
    {
        return SpawnAt(YardX, LevelTest1.SurfaceAt(YardX));
    }

    // Flat-ground lab (scene Campfire): feet of the logs on y = −2.
    public static Campfire SpawnAt(float x, float surfaceY)
    {
        Campfire existing = FindFirstObjectByType<Campfire>();
        if (existing != null)
        {
            existing.Build();
            return existing;
        }

        GameObject root = new GameObject("YardCampfire");
        root.transform.position = new Vector3(x, surfaceY, 0f);
        Campfire fire = root.AddComponent<Campfire>();
        fire.Build();
        return fire;
    }

    private void Build()
    {
        if (built)
            return;

        built = true;
        noiseOffset = 11.3f;
        followCamera = Camera.main;

        BuildLogs();

        emberW = EmberBoxWidth;
        emberH = EmberBoxHeight;
        ember = MakeDisc(
            "Ember",
            new Vector2(emberW, emberH),
            new Vector3(0f, EmberCenterY, 0f),
            0f,
            EmberTint,
            LookOrder(EmberOrder));

        flameW = CoreBoxWidth;
        flameH = CoreBoxHeight;
        SpriteRenderer flame = MakeDisc(
            "FlameCore",
            new Vector2(flameW, flameH),
            new Vector3(0f, CoreCenterY, 0f),
            0f,
            CoreTint,
            LookOrder(FireOrder + 4),
            CoreSprite());
        flameVisual = flame.transform;

        BuildTongues();
        BuildParticles();

        mothHost = YardAmbient.PlantFireMoths(transform);

        glowW = 1.85f;
        glowH = 1.45f;
        glow = MakeDisc(
            "Glow",
            new Vector2(glowW, glowH),
            new Vector3(0f, 0.42f, 0f),
            0f,
            new Color(1f, 0.48f, 0.12f, 0f),
            LookOrder(GlowOrder));
        glow.enabled = false;

        if (SceneLighting.Enabled)
            BuildLight();
    }

    // Night01 from DayNightCycle. Daytime alpha stays 0 so the halo is not a noon lamp.
    public void ApplyNight(float night01)
    {
        glowNight = night01 < 0f ? 0f : (night01 > 1f ? 1f : night01);
    }

    void LateUpdate()
    {
        TickCulling();
        TickFlame();
    }

    private void TickCulling()
    {
        if (followCamera == null)
        {
            followCamera = Camera.main;
            if (followCamera == null)
                return;
        }

        float dx = transform.position.x - followCamera.transform.position.x;
        bool near = dx * dx < CullX * CullX;
        if (near == emitting)
            return;

        emitting = near;
        SetVisible(near);
    }

    private void TickFlame()
    {
        float t = Time.time * flickerSpeed;
        float n = Mathf.PerlinNoise(t, noiseOffset);
        float n2 = Mathf.PerlinNoise(noiseOffset, t * 1.37f);

        if (ember != null && ember.enabled)
        {
            ember.transform.localScale = new Vector3(
                emberW * (0.88f + n * 0.28f),
                emberH * (0.90f + n2 * 0.25f),
                1f);
        }

        if (flameVisual != null && flameVisual.gameObject.activeSelf)
        {
            flameVisual.localScale = new Vector3(
                flameW * (0.82f + n * 0.32f),
                flameH * (0.86f + n2 * 0.34f),
                1f);
            flameVisual.localRotation = Quaternion.Euler(0f, 0f, (n - 0.5f) * 14f);
        }

        if (emitting)
            TickTongues(n, n2);

        if (glow != null)
        {
            float a = emitting ? glowNight * (0.30f + n * 0.22f) : 0f;
            Color gc = glow.color;
            gc.a = a;
            glow.color = gc;
            glow.enabled = a > 0.01f;
            glow.transform.localScale = new Vector3(
                glowW * (0.92f + n * 0.16f),
                glowH * (0.90f + n2 * 0.14f),
                1f);
        }

        if (flameLight == null || !flameLight.enabled)
            return;

        flameLight.intensity = baseIntensity * (1f + (n - 0.5f) * 2f * intensityJitter);
        flameLight.pointLightOuterRadius = baseOuterRadius * (1f + (n2 - 0.5f) * 2f * radiusJitter);
        flameLight.transform.localPosition = new Vector3(0f, 0.28f + n * 0.06f, 0f);
    }

    // Rise, lean, fade, wrap. Arrays are the pool: no Instantiate, no new Color[].
    // Color walks white → yellow → orange → red by age so the tongue is one flame, not three bands.
    private void TickTongues(float n, float n2)
    {
        if (tongues == null)
            return;

        float dt = Time.deltaTime;
        float t = Time.time;
        for (int i = 0; i < tongues.Length; i++)
        {
            Transform xf = tongues[i];
            SpriteRenderer sr = tongueSr[i];
            if (xf == null || sr == null)
                continue;

            float life = tongueLife[i];
            float age = tongueAge[i] + dt;
            if (age >= life)
                age -= life;
            tongueAge[i] = age;

            float u = age / life;
            TonguePicture(u, t, n, n2, tonguePhase[i], tongueLean[i], tongueX0[i], tongueRise[i],
                tongueW[i], tongueH[i], tongueHeat[i],
                out Vector3 local, out float tilt, out float scaleX, out float scaleY,
                out Color rgb, out bool on);

            xf.localPosition = local;
            xf.localRotation = Quaternion.Euler(0f, 0f, tilt);
            xf.localScale = new Vector3(scaleX, scaleY, 1f);
            sr.color = rgb;
            sr.enabled = on;
        }
    }

    // One tongue's picture at age fraction u, time t and flicker pair (n, n2):
    // where it stands, how it leans, how big it is drawn, its colour by age and
    // whether it is lit at all. TickTongues writes these onto the live pool every
    // frame. FrozenStack reads the same function for the catalogue's static fire,
    // so the catalogue and the yard cannot drift apart.
    private static void TonguePicture(
        float u, float t, float n, float n2,
        float phase, float lean, float x0, float rise, float w, float h, float heat,
        out Vector3 local, out float tilt, out float scaleX, out float scaleY,
        out Color color, out bool on)
    {
        float flicker = 0.80f + n * 0.26f + n2 * 0.10f * Mathf.Sin(t * 13.5f + phase);
        float fade = u < 0.08f
            ? u / 0.08f
            : (u > 0.48f ? 1f - (u - 0.48f) / 0.52f : 1f);
        if (fade < 0f)
            fade = 0f;

        float y = 0.16f + u * rise;
        float x = x0 + Mathf.Sin(t * 8.4f + phase) * 0.024f * (0.35f + u);
        local = new Vector3(x, y, 0f);
        tilt = lean * (0.50f + 0.50f * Mathf.Sin(t * 6.1f + phase));

        float sx = flicker * (1.12f - u * 0.78f);
        float sy = flicker * (1.06f - u * 0.28f);
        scaleX = w * sx;
        scaleY = h * sy;

        Color rgb = FlameColor(u, heat);
        rgb.a = fade * (0.55f + (1f - u) * 0.45f);
        color = rgb;
        on = rgb.a > 0.03f;
    }

    private static Color FlameColor(float u, float heat)
    {
        Color c;
        if (u < 0.22f)
            c = Color.Lerp(TongueWhite, TongueYellow, u / 0.22f);
        else if (u < 0.55f)
            c = Color.Lerp(TongueYellow, TongueOrange, (u - 0.22f) / 0.33f);
        else
            c = Color.Lerp(TongueOrange, TongueRed, (u - 0.55f) / 0.45f);

        Color cooler = Color.Lerp(c, TongueRed, 0.28f * (1f - heat));
        return cooler;
    }

    private void BuildTongues()
    {
        tongues = new Transform[TongueCount];
        tongueSr = new SpriteRenderer[TongueCount];
        tongueAge = new float[TongueCount];
        tongueLife = new float[TongueCount];
        tonguePhase = new float[TongueCount];
        tongueLean = new float[TongueCount];
        tongueX0 = new float[TongueCount];
        tongueRise = new float[TongueCount];
        tongueW = new float[TongueCount];
        tongueH = new float[TongueCount];
        tongueHeat = new float[TongueCount];

        for (int i = 0; i < TongueCount; i++)
        {
            float w;
            float h;
            int order;
            float heat;
            float life;
            float phase;
            float lean;
            float x0;
            float rise;
            PoolRow(i, out w, out h, out order, out heat, out life, out phase, out lean, out x0, out rise);

            SpriteRenderer sr = MakeDisc(
                "Tongue_" + i,
                new Vector2(w, h),
                new Vector3(x0, 0.18f, 0f),
                0f,
                new Color(1f, 0.80f, 0.25f, 0f),
                order);
            sr.enabled = false;

            tongues[i] = sr.transform;
            tongueSr[i] = sr;
            tongueW[i] = w;
            tongueH[i] = h;
            tongueHeat[i] = heat;
            tongueLife[i] = life;
            tongueAge[i] = life * (i / (float)TongueCount);
            tonguePhase[i] = phase;
            tongueLean[i] = lean;
            tongueX0[i] = x0;
            tongueRise[i] = rise;
        }
    }

    // The pool's geometry, one row per tongue: the band's box, its sorting order,
    // its heat, and the life / phase / lean / drift the pose reads. BuildTongues
    // builds the 24 renderers from these rows and FrozenStack reads the same rows,
    // so there is one description of a tongue in the project, not two.
    private static void PoolRow(int i, out float w, out float h, out int order, out float heat,
        out float life, out float phase, out float lean, out float x0, out float rise)
    {
        int band = i % 3;
        if (band == 0)
        {
            w = 0.055f;
            h = 0.46f;
            order = LookOrder(FireOrder + 3);
        }
        else if (band == 1)
        {
            w = 0.080f;
            h = 0.38f;
            order = LookOrder(FireOrder + 2);
        }
        else
        {
            w = 0.110f;
            h = 0.30f;
            order = LookOrder(FireOrder + 1);
        }

        heat = band == 0 ? 1f : (band == 1 ? 0.62f : 0.28f);
        life = 0.38f + (i % 7) * 0.07f;
        phase = i * 1.618f;
        lean = -24f + (i % 9) * 6.0f;
        x0 = ((i / 3) - 3.5f) * 0.028f;
        rise = 0.30f + band * 0.07f + (i % 5) * 0.03f;
    }

    private void BuildParticles()
    {
        sparksPs = MakeParticleSystem("Sparks", LookOrder(SparkOrder));
        ConfigureSparks(sparksPs);
        smokePs = MakeParticleSystem("Smoke", LookOrder(SmokeOrder));
        ConfigureSmoke(smokePs);
        embersPs = MakeParticleSystem("Embers", LookOrder(EmberOrder + 1));
        ConfigureEmbers(embersPs);
        SetParticlesPlaying(true);
    }

    private ParticleSystem MakeParticleSystem(string name, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.22f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
        main.stopAction = ParticleSystemStopAction.None;
        main.gravityModifier = 0f;
        main.startRotation = 0f;
        main.startSpeed = 0f;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.14f, 0.05f, 0.01f);

        ParticleSystem.CollisionModule collision = ps.collision;
        collision.enabled = false;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sharedMaterial = ParticleMaterial();
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.minParticleSize = 0.004f;
        renderer.maxParticleSize = 0.35f;
        renderer.allowRoll = false;
        return ps;
    }

    private void ConfigureSparks(ParticleSystem ps)
    {
        ParticleSystem.MainModule main = ps.main;
        main.maxParticles = sparkMax < 1 ? 1 : sparkMax;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.70f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.038f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.92f, 0.45f, 1f),
            new Color(1f, 0.55f, 0.12f, 1f));
        main.gravityModifier = -0.35f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.55f, 1.15f);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = sparkRate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 16f;
        shape.radius = 0.05f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = SparkGradient();

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.55f, 0.70f),
            new Keyframe(1f, 0.05f)));

        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.12f);
        vel.y = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        vel.space = ParticleSystemSimulationSpace.Local;
    }

    private void ConfigureSmoke(ParticleSystem ps)
    {
        ParticleSystem.MainModule main = ps.main;
        main.maxParticles = smokeMax < 1 ? 1 : smokeMax;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
        // Warm gray. Cool (0.50, 0.50, 0.52) on NightSky read as blue boxes.
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.40f, 0.37f, 0.32f, 0.55f),
            new Color(0.28f, 0.26f, 0.22f, 0.40f));
        main.gravityModifier = -0.08f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = smokeRate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.10f, 0.04f, 0.01f);

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = SmokeGradient();

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.40f, 1.05f),
            new Keyframe(1f, 1.70f)));

        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(SmokeWind * 0.70f, SmokeWind * 1.15f);
        vel.y = new ParticleSystem.MinMaxCurve(0.28f, 0.52f);
        vel.space = ParticleSystemSimulationSpace.Local;
    }

    private void ConfigureEmbers(ParticleSystem ps)
    {
        ParticleSystem.MainModule main = ps.main;
        main.maxParticles = emberMax < 1 ? 1 : emberMax;
        main.duration = 1.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.70f, 1.40f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.022f, 0.048f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.22f, 0.04f, 1f),
            new Color(0.70f, 0.08f, 0.02f, 1f));
        main.gravityModifier = -0.12f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = emberRate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.20f, 0.06f, 0.01f);

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = EmberGradient();

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.80f),
            new Keyframe(0.50f, 1.00f),
            new Keyframe(1f, 0.15f)));

        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.10f);
        vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
        vel.space = ParticleSystemSimulationSpace.Local;
    }

    private static ParticleSystem.MinMaxGradient SparkGradient()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.70f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.12f), 0.45f),
                new GradientColorKey(new Color(1f, 0.28f, 0.04f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.85f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        return new ParticleSystem.MinMaxGradient(g);
    }

    private static ParticleSystem.MinMaxGradient SmokeGradient()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.42f, 0.39f, 0.34f), 0f),
                new GradientColorKey(new Color(0.32f, 0.30f, 0.26f), 0.50f),
                new GradientColorKey(new Color(0.22f, 0.21f, 0.19f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.38f, 0.12f),
                new GradientAlphaKey(0.22f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        return new ParticleSystem.MinMaxGradient(g);
    }

    private static ParticleSystem.MinMaxGradient EmberGradient()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.28f, 0.05f), 0f),
                new GradientColorKey(new Color(0.85f, 0.10f, 0.02f), 0.55f),
                new GradientColorKey(new Color(0.35f, 0.04f, 0.01f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.15f, 0f),
                new GradientAlphaKey(1f, 0.10f),
                new GradientAlphaKey(0.70f, 0.60f),
                new GradientAlphaKey(0f, 1f)
            });
        return new ParticleSystem.MinMaxGradient(g);
    }

    private void SetParticlesPlaying(bool on)
    {
        SetOneParticle(sparksPs, on);
        SetOneParticle(smokePs, on);
        SetOneParticle(embersPs, on);
    }

    private static void SetOneParticle(ParticleSystem ps, bool on)
    {
        if (ps == null)
            return;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = on;
        if (on)
        {
            if (!ps.isPlaying)
                ps.Play(true);
        }
        else
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void SetVisible(bool on)
    {
        if (ember != null)
            ember.enabled = on;
        if (flameVisual != null)
            flameVisual.gameObject.SetActive(on);
        if (tongueSr != null)
        {
            for (int i = 0; i < tongueSr.Length; i++)
            {
                if (tongueSr[i] != null)
                    tongueSr[i].enabled = on;
            }
        }
        SetParticlesPlaying(on);
        if (mothHost != null)
            mothHost.gameObject.SetActive(on);
        if (glow != null)
            glow.enabled = on && glowNight > 0.02f;

        if (flameLight != null)
            flameLight.enabled = on && SceneLighting.Enabled;
    }

    private void BuildLogs()
    {
        for (int i = 0; i < 3; i++)
        {
            string name;
            Vector2 size;
            Vector3 local;
            float tilt;
            LogRow(i, out name, out size, out local, out tilt);
            SpriteRenderer log = MakeBox(name, size, local, tilt, LogWood, LookOrder(LogOrder));
            DressLog(log, LogIndex(name));
        }
    }

    // LogA -> 0, LogB -> 1, LogC -> 2: the order LogRow, BuildLogs and FrozenStack
    // already use, so the picture cannot land on the wrong log.
    public static int LogIndex(string name)
    {
        if (name == "LogB")
            return 1;
        if (name == "LogC")
            return 2;
        return 0;
    }

    // One log's box, in metres (LogRow is the single source of it). The catalogue
    // and the import check read this instead of retyping the three boxes.
    public static Vector2 LogBox(int index)
    {
        LogRow(index, out string name, out Vector2 size, out Vector3 local, out float tilt);
        return size;
    }

    // The painted canvas on an already-sized log renderer. `ArtParts.Dress` with a
    // zero tile period means "one repeat across the part": the whole canvas is
    // mapped Sliced onto the box the log already has, size stays on `sprite.size`,
    // and `localScale` is untouched. Returns false - leaving today's flat box - when
    // the canvas is not in the build. Not gated by `ArtLibrary.RealArtEnabled`:
    // the flame core beside it is painted everywhere, and the catalogue must draw
    // the same pile the yard draws.
    public static bool DressLog(SpriteRenderer sr, int index)
    {
        if (sr == null || index < 0 || index >= LogArtKeys.Length)
            return false;

        // In a headless run without visual capture ApplyPicture puts an 8x8
        // placeholder on the log, so the renderer's own box is 1 x 1 m and not the
        // log's - dressing there would map the canvas onto the wrong rectangle.
        // A headless trial therefore keeps the flat box, exactly as it keeps the
        // procedural disc beside the painted core.
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return false;

        return ArtParts.Dress(sr, LogArtKeys[index], 0f, true);
    }

    // The three logs, one row each: the box, where the yard stands it and its
    // tilt. BuildLogs builds the renderers from these rows and FrozenStack reads
    // them, so the catalogue cannot show a different pile from the yard.
    private static void LogRow(int i, out string name, out Vector2 size, out Vector3 local, out float tilt)
    {
        if (i == 0)
        {
            name = "LogA";
            size = new Vector2(0.62f, 0.14f);
            local = new Vector3(-0.04f, 0.07f, 0f);
            tilt = -18f;
        }
        else if (i == 1)
        {
            name = "LogB";
            size = new Vector2(0.54f, 0.13f);
            local = new Vector3(0.07f, 0.065f, 0f);
            tilt = 16f;
        }
        else
        {
            name = "LogC";
            size = new Vector2(0.30f, 0.12f);
            local = new Vector3(0.00f, 0.16f, 0f);
            tilt = 7f;
        }
    }

    private SpriteRenderer MakeBox(string name, Vector2 size, Vector3 local, float tilt, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        go.transform.localScale = Vector3.one;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(sr, size, HumanVisualShape.Box, null, color, sortingOrder);
        SpriteLighting.ApplyUnlit(sr);
        return sr;
    }

    // Soft disc, 1 m × 1 m sprite. Size is localScale, not transform of a collider.
    // `sprite` swaps the picture (the painted flame core); it never changes the size,
    // the colour or the sorting order.
    private SpriteRenderer MakeDisc(string name, Vector2 size, Vector3 local, float tilt, Color color, int sortingOrder, Sprite sprite = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : SoftSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        sr.drawMode = SpriteDrawMode.Simple;
        SpriteLighting.ApplyUnlit(sr);
        return sr;
    }

    // The painted flame core, or null when the art is not in the build: 1 m wide
    // sprite like SoftSprite, so localScale stays the only sizing device.
    public static Sprite CoreSprite()
    {
        if (PaintedCoreChecked)
            return PaintedCore;

        PaintedCoreChecked = true;
        Texture2D tex = ArtLibrary.Albedo(CoreArtKey);
        if (tex == null)
            return null;

        PaintedCore = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), tex.width, 0, SpriteMeshType.FullRect);
        PaintedCore.name = "CampfireCore";
        return PaintedCore;
    }

    public static Sprite SoftSprite()
    {
        if (SoftDiscSprite != null)
            return SoftDiscSprite;
        Texture2D tex = SoftTexture();
        SoftDiscSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        return SoftDiscSprite;
    }

    private static Texture2D SoftTexture()
    {
        if (SoftDiscTex != null)
            return SoftDiscTex;

        const int n = 48;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float nx = (x - c) / c;
                float ny = (y - c) / c;
                float d = nx * nx + ny * ny;
                float a = d >= 1f ? 0f : Mathf.Clamp01(1f - d);
                a *= a;
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        SoftDiscTex = tex;
        return SoftDiscTex;
    }

    // Own material: SpriteLighting.Unlit is shared with every Unlit sprite.
    // Writing mainTexture on that asset would punch a disc into the logs.
    private static Material ParticleMaterial()
    {
        if (ParticleMat != null)
            return ParticleMat;
        Material shared = SpriteLighting.Unlit();
        if (shared == null)
            return null;
        ParticleMat = new Material(shared);
        ParticleMat.name = "CampfireParticleUnlit";
        Texture2D disc = SoftTexture();
        ParticleMat.mainTexture = disc;
        if (ParticleMat.HasProperty("_BaseMap"))
            ParticleMat.SetTexture("_BaseMap", disc);
        if (ParticleMat.HasProperty("_MainTex"))
            ParticleMat.SetTexture("_MainTex", disc);
        return ParticleMat;
    }

    private void BuildLight()
    {
        GameObject go = new GameObject("FlameLight");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.28f, 0f);

        flameLight = go.AddComponent<Light2D>();
        flameLight.lightType = Light2D.LightType.Point;
        flameLight.color = new Color(1f, 0.50f, 0.18f, 1f);
        flameLight.intensity = baseIntensity;
        flameLight.pointLightInnerRadius = innerRadius;
        flameLight.pointLightOuterRadius = baseOuterRadius;
        flameLight.pointLightInnerAngle = 360f;
        flameLight.pointLightOuterAngle = 360f;
        flameLight.falloffIntensity = 0.70f;
        flameLight.blendStyleIndex = 1;
        flameLight.overlapOperation = Light2D.OverlapOperation.Additive;
        flameLight.shadowsEnabled = false;
        flameLight.volumetricEnabled = false;
        flameLight.volumeIntensity = 0f;
        flameLight.enabled = SceneLighting.Enabled;
    }
}
