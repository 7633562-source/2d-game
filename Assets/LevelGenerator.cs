using System.Collections.Generic;
using UnityEngine;

public enum LevelProfile
{
    Flat = 0,
    Course = 1,
    Steps = 2,
    Test1 = 3
}

// World layer: platforms around the player. Does not read joints or create torques.
// The stand (HeadlessTrial) never enters here — it still uses GroundBuilder.
public class LevelGenerator : MonoBehaviour
{
    [Header("Profile")]
    public LevelProfile profile = LevelProfile.Flat;
    public int seed = 1;

    [Header("Chunks")]
    public float chunkWidth = 16f;
    public int keepBehind = 2;
    public int keepAhead = 3;

    [Header("Steps")]
    [Tooltip("Height of one step. Spawn top is always y = −2, as on GroundBuilder.")]
    public float stepHeight = 0.05f;
    [Tooltip("How far the surface may rise above spawn.")]
    public float maxRise = 0.20f;

    [Header("Drawn level")]
    [Tooltip("Human X on Test1. World showcase 0. Level1 −8, left of the apple.")]
    public float playerSpawnX = 0f;
    [Tooltip("Apple, strawberries, hedgehog. On in scene Level1. Off in World.")]
    public bool plantDrawnLevel = false;
    [Tooltip("CLEARING-1: scene Clearing. Keeps the drawn arrangement (fire at its drawn spot, spawn X) and the World showcase pack stands down, but plants the showcase Static grove plus the clearing's own props (berries, flower bed, its own oak) instead of the Level1 tableau. Scene Clearing sets this WITH plantDrawnLevel.")]
    public bool clearingProps = false;

    // Ground top in stance: GroundBuilder center (−3) + half height (1).
    public const float DefaultSurfaceY = -2f;
    public const float GroundThickness = 2f;
    public const float SpawnAboveSurface = 1.18f;

    public float SpawnY => DefaultSurfaceY + SpawnAboveSurface;

    private Transform followTarget;
    private Transform root;
    private readonly Dictionary<int, GameObject> loaded = new Dictionary<int, GameObject>();
    // Reuse the unload buffer: a new List every Update garbage-collects on the main thread.
    private readonly List<int> dropBuffer = new List<int>(8);
    private bool running;
    private bool grovePlanted;
    private bool campfirePlanted;
    private int lastStreamCenter = int.MinValue;

    public void ApplyOverrideFlag()
    {
        // Desk-only flag. On WebGL dataPath is a URL; File.Exists throws and
        // used to abort GameProcess.Start before Begin / CreateHumans —
        // empty World, no hero, no Walk button. Same gate as PlayerInputSource.
#if !(UNITY_WEBGL && !UNITY_EDITOR)
        try
        {
            string path = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "force-level.flag");
            if (System.IO.File.Exists(path))
            {
                string raw = System.IO.File.ReadAllText(path).Trim().ToLowerInvariant();
                if (raw == "course" || raw == "1")
                    profile = LevelProfile.Course;
                else if (raw == "steps")
                    profile = LevelProfile.Steps;
                else if (raw == "level1")
                {
                    profile = LevelProfile.Test1;
                    plantDrawnLevel = true;
                    playerSpawnX = LevelTest1.DrawnPlayerX;
                }
                else if (raw == "test1" || raw == "yard" || raw == "dvor" || raw == "3")
                    profile = LevelProfile.Test1;
                else if (raw == "flat" || raw == "0")
                    profile = LevelProfile.Flat;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("LevelGenerator: force-level.flag skipped (" + e.GetType().Name + ").");
        }
#endif

        // GameProcess reads LevelTest1.PlayerX for Test1. Copy the scene field
        // even when the flag file is absent.
        LevelTest1.PlayerX = playerSpawnX;
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
        try
        {
            GroundBuilder ground = GetComponent<GroundBuilder>();
            if (ground != null)
                ground.BuildBackdrop();
            LevelBackdrop.Install(root);
            Debug.Log($"LevelGenerator: {Title()}, seed {seed}, chunk {chunkWidth} m");
            StreamAround(0f, true);
        }
        catch (System.Exception e)
        {
            // GameProcess.CreateHumans is after Begin. A picture throw here
            // used to skip the hero and the Walk button on WebGL World.
            running = false;
            Debug.LogException(e);
            return;
        }

        // Static grove / apple have no wood fixtures. Plant before birds
        // so GameProcess can seat the flock (Level1 apple, World gate trees).
        // Isolated from the chunk loop: a prop throw must not unload the yard
        // or skip CreateHumans.
        if (profile == LevelProfile.Test1)
        {
            try
            {
                PlantYardProps(null);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        if (profile == LevelProfile.Test1)
            PlantYardProps(target);
    }

    private void PlantYardProps(Transform human)
    {
        if (!grovePlanted)
        {
            // CLEARING-1: the clearing keeps the drawn arrangement (fire at its
            // drawn spot, spawn X) but plants the showcase Static grove and the
            // clearing's own props instead of the Level1 tableau.
            LevelGrove.Plant(root, human, plantDrawnLevel && !clearingProps);
            grovePlanted = true;
            if (clearingProps)
                LevelGrove.PlantClearingProps(root);
        }
        if (!campfirePlanted)
        {
            Campfire fire = Campfire.EnsureOnYard();
            if (plantDrawnLevel)
                LevelGrove.PlaceDrawnCampfire(fire);
            campfirePlanted = true;
        }

        if (profile == LevelProfile.Test1)
            DayNightCycle.Ensure();
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
        {
            try
            {
                LevelTest1.SpawnDecor(chunkIndex, chunkWidth, chunk.transform);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        return chunk;
    }

    public string Title()
    {
        if (clearingProps)
            return "Clearing";
        if (profile == LevelProfile.Test1)
            return plantDrawnLevel ? "Level1" : LevelTest1.Title;
        return profile.ToString();
    }

    public List<LevelPiece> GenerateChunk(int chunkIndex)
    {
        if (profile == LevelProfile.Test1)
            return LevelTest1.PiecesForChunk(chunkIndex, chunkWidth);

        float x0 = chunkIndex * chunkWidth;
        var pieces = new List<LevelPiece>(8);

        // Chunks under spawn and behind — the same height as GroundBuilder.
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

        // Course: continuous support with no gaps. Chunk N height continues from
        // the end of N−1, otherwise a 16 m seam becomes a cliff from nowhere.
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
