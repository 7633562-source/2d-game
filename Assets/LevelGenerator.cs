using System.Collections.Generic;
using UnityEngine;

public enum LevelProfile
{
    Flat = 0,
    Course = 1,
    Steps = 2,
    Test1 = 3
}

// Слой мира: площадки вокруг игрока. Не читает суставы и не создаёт моменты.
// Стенд (HeadlessTrial) сюда не заходит — у него по-прежнему GroundBuilder.
public class LevelGenerator : MonoBehaviour
{
    [Header("Профиль")]
    public LevelProfile profile = LevelProfile.Flat;
    public int seed = 1;

    [Header("Чанки")]
    public float chunkWidth = 16f;
    public int keepBehind = 2;
    public int keepAhead = 3;

    [Header("Ступени")]
    [Tooltip("Высота одной ступени. Верх спавна всегда y = −2, как у GroundBuilder.")]
    public float stepHeight = 0.05f;
    [Tooltip("Насколько поверхность может подняться над спавном.")]
    public float maxRise = 0.20f;

    // Верх земли в стойке: центр GroundBuilder (−3) + половина высоты (1).
    public const float DefaultSurfaceY = -2f;
    public const float GroundThickness = 2f;
    public const float SpawnAboveSurface = 1.18f;

    public float SpawnY => DefaultSurfaceY + SpawnAboveSurface;

    private Transform followTarget;
    private Transform root;
    private readonly Dictionary<int, GameObject> loaded = new Dictionary<int, GameObject>();
    // Повторно используем буфер выгрузки: new List каждый Update даёт мусор на главном потоке.
    private readonly List<int> dropBuffer = new List<int>(8);
    private bool running;
    private bool grovePlanted;
    private int lastStreamCenter = int.MinValue;

    public void ApplyOverrideFlag()
    {
        string path = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "force-level.flag");
        if (!System.IO.File.Exists(path))
            return;

        string raw = System.IO.File.ReadAllText(path).Trim().ToLowerInvariant();
        if (raw == "course" || raw == "1")
            profile = LevelProfile.Course;
        else if (raw == "steps")
            profile = LevelProfile.Steps;
        else if (raw == "test1" || raw == "yard" || raw == "dvor" || raw == "3")
            profile = LevelProfile.Test1;
        else if (raw == "flat" || raw == "0")
            profile = LevelProfile.Flat;
    }

    public void Begin()
    {
        if (profile == LevelProfile.Flat)
            return;

        if (chunkWidth < 4f)
            chunkWidth = 4f;
        if (keepBehind < 1)
            keepBehind = 1;
        if (keepAhead < 1)
            keepAhead = 1;
        if (stepHeight < 0f)
            stepHeight = 0f;

        if (root == null)
        {
            GameObject rootObject = new GameObject("Level");
            root = rootObject.transform;
        }

        running = true;
        GroundBuilder ground = GetComponent<GroundBuilder>();
        if (ground != null)
            ground.BuildBackdrop();
        LevelBackdrop.Install(root);
        Debug.Log($"LevelGenerator: {Title()}, seed {seed}, чанк {chunkWidth} м");
        StreamAround(0f, true);
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        // После человека: роще нужны его коллайдеры, чтобы их развести.
        if (!grovePlanted && profile == LevelProfile.Test1)
        {
            LevelGrove.Plant(root, target);
            grovePlanted = true;
        }
    }

    void Update()
    {
        if (!running || profile == LevelProfile.Flat)
            return;

        float x = followTarget != null ? followTarget.position.x : 0f;
        StreamAround(x);
    }

    private void StreamAround(float playerX, bool force = false)
    {
        int center = Mathf.FloorToInt(playerX / chunkWidth);
        if (!force && center == lastStreamCenter)
            return;
        lastStreamCenter = center;

        int min = center - keepBehind;
        int max = center + keepAhead;

        dropBuffer.Clear();
        foreach (var pair in loaded)
        {
            if (pair.Key < min || pair.Key > max)
                dropBuffer.Add(pair.Key);
        }

        for (int i = 0; i < dropBuffer.Count; i++)
        {
            int index = dropBuffer[i];
            if (loaded.TryGetValue(index, out GameObject chunk) && chunk != null)
                Destroy(chunk);
            loaded.Remove(index);
        }

        for (int index = min; index <= max; index++)
        {
            if (loaded.ContainsKey(index))
                continue;
            loaded[index] = BuildChunk(index);
        }
    }

    private GameObject BuildChunk(int chunkIndex)
    {
        GameObject chunk = new GameObject("Chunk_" + chunkIndex);
        chunk.transform.SetParent(root, false);

        List<LevelPiece> pieces = GenerateChunk(chunkIndex);
        for (int i = 0; i < pieces.Count; i++)
            StaticPlatform.Spawn(pieces[i], chunk.transform);

        if (profile == LevelProfile.Test1)
            LevelTest1.SpawnDecor(chunkIndex, chunkWidth, chunk.transform);

        return chunk;
    }

    public string Title()
    {
        if (profile == LevelProfile.Test1)
            return LevelTest1.Title;
        return profile.ToString();
    }

    public List<LevelPiece> GenerateChunk(int chunkIndex)
    {
        if (profile == LevelProfile.Test1)
            return LevelTest1.PiecesForChunk(chunkIndex, chunkWidth);

        float x0 = chunkIndex * chunkWidth;
        var pieces = new List<LevelPiece>(8);

        // Чанки под спавном и позади — та же высота, что у GroundBuilder.
        if (chunkIndex <= 0)
        {
            pieces.Add(MakeSlab(x0, chunkWidth, DefaultSurfaceY, DirtColor(chunkIndex)));
            return pieces;
        }

        if (profile == LevelProfile.Steps)
        {
            float surface = DefaultSurfaceY + stepHeight * chunkIndex;
            float cap = DefaultSurfaceY + maxRise;
            if (surface > cap)
                surface = cap;
            pieces.Add(MakeSlab(x0, chunkWidth, surface, DirtColor(chunkIndex)));
            return pieces;
        }

        // Course: сплошная опора без щелей. Высоту чанка N продолжаем с конца
        // N−1, иначе на стыке 16 м получится обрыв из ниоткуда.
        float surfaceY = DefaultSurfaceY;
        List<LevelPiece> generated = pieces;
        for (int i = 1; i <= chunkIndex; i++)
            generated = BuildCourseChunk(i, ref surfaceY);
        return generated;
    }

    private List<LevelPiece> BuildCourseChunk(int chunkIndex, ref float surfaceY)
    {
        float x = chunkIndex * chunkWidth;
        float remaining = chunkWidth;
        var pieces = new List<LevelPiece>(8);
        System.Random rng = new System.Random(MixSeed(seed, chunkIndex));

        while (remaining > 0.05f)
        {
            float width = 2f + (float)rng.NextDouble() * 4f;
            if (width > remaining || remaining - width < 1f)
                width = remaining;

            int delta = rng.Next(-1, 3);
            surfaceY += delta * stepHeight;
            float minY = DefaultSurfaceY - stepHeight;
            float maxY = DefaultSurfaceY + maxRise;
            if (surfaceY < minY)
                surfaceY = minY;
            if (surfaceY > maxY)
                surfaceY = maxY;

            pieces.Add(MakeSlab(x, width, surfaceY, DirtColor(chunkIndex)));
            x += width;
            remaining -= width;
        }

        return pieces;
    }

    private static LevelPiece MakeSlab(float leftX, float width, float surfaceY, Color color)
    {
        return new LevelPiece
        {
            name = "Slab",
            center = new Vector2(leftX + width * 0.5f, surfaceY - GroundThickness * 0.5f),
            size = new Vector2(width, GroundThickness),
            rotationZ = 0f,
            color = color,
            kind = LevelArt.FillKey,
            hasCollider = true
        };
    }

    private static Color DirtColor(int chunkIndex)
    {
        float t = (Mathf.Abs(chunkIndex) % 5) / 5f;
        return Color.Lerp(
            new Color(0.42f, 0.30f, 0.16f, 1f),
            new Color(0.34f, 0.24f, 0.12f, 1f),
            t);
    }

    private static int MixSeed(int a, int b)
    {
        unchecked
        {
            return a * 397 ^ b * -1640531527;
        }
    }
}
