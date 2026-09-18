using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// ARENA-1 (Yury, 18.09.2026): the parameter-sweep arena.
//
// ONE point, no lanes: every human appears at the same x, so the distance of a
// human is its own displacement from that shared spawn x and nothing else.
// Humans must NOT collide with each other: their rigidbodies go on the dedicated
// `ArenaHuman` layer, and that layer's self pair is switched OFF in
// ProjectSettings/Physics2DSettings, while ArenaHuman x Ground stays on. The
// project setting is the mechanism and the arena reads the engine state back
// (Physics2D.GetIgnoreLayerCollision) so the proof is a measurement, not a claim.
//
// A new human spawns every 500 ms with a different genome of the pose-drive walk
// parameters, walks the frame it spawns, is measured 15 s later and destroyed
// 30 s after its spawn, so the alive count oscillates instead of growing.
//
// Evolution (documented in the run summary as well): fitness = distance minus a
// fall penalty, tournament selection inside the best quartile, 5-10 pct jitter of
// each gene's range, 25 pct fresh random blood, and the best genome re-spawned
// unchanged once the log holds 20 or more measured humans. The unchanged re-spawn
// is also the collision proof: two copies, same genome, same spawn point,
// different ages.
//
// Files (append-only where the card says so):
//   Trials/Runs/arena-param-log.csv  fixed-contract rows, appended, never rewritten
//   Trials/Runs/<label>.json         run summary + one JSON row per human
//   Trials/Runs/<label>.csv          the same rows as a table
public class ArenaRunner : MonoBehaviour
{
    public const string ArenaLayerName = "ArenaHuman";
    public const int EliteMinMeasured = 20;
    public const int EliteEverySpawns = 8;
    public const float ChildShare = 0.75f;
    public const float TournamentSize = 3f;

    // ─── configuration: command line in headless, Inspector in the scene ───
    public int seed = 1;
    public string label = "arena";
    public string runId = "";
    public string sourceFingerprint = "";
    public string buildGuid = "";
    public int seedUsedByRun;
    public float spawnInterval = 0.5f;
    public float measureSeconds = 15f;
    public float cullSeconds = 30f;
    public int aliveCap = 60;
    public float durationSeconds = 90f;
    public float spawnX = 0f;
    public float spawnY = -0.82f;
    public float groundWidth = 200f;
    public float groundFriction = 0.4f;
    public float numberHeight = 0.34f;
    public float cameraSize = 5f;
    public float cameraOffsetX = 2.5f;
    public float photoCameraSize = 2.2f;
    public float photoOffsetX = 1.2f;
    public bool externalTick;
    public bool headless;
    public string outputFolder = "";
    public bool buildGroundOnBegin = true;

    // ─── state ─────────────────────────────────────────────────────────────
    private readonly List<ArenaHumanAgent> alive = new List<ArenaHumanAgent>(96);
    private readonly List<ArenaHumanAgent> measured = new List<ArenaHumanAgent>(256);
    private readonly List<ArenaHumanAgent> all = new List<ArenaHumanAgent>(256);
    private readonly List<ArenaHumanAgent> eliteCopies = new List<ArenaHumanAgent>(16);
    private readonly List<string> aliveCurve = new List<string>(256);
    private readonly List<string> wallCurve = new List<string>(256);

    private ArenaRng rng;
    private bool begun;
    private float simTime;
    private float nextSpawnTime;
    private int spawnIndex;
    private int spawnCount;
    private int measuredCount;
    private int culledCount;
    private int maxAlive;
    private float lastSpawnTime = -1f;
    private float intervalSum;
    private float intervalMin = float.MaxValue;
    private float intervalMax;
    private int intervalSamples;
    private int maxRigidbodies;
    private int overlapMax;
    private float overlapMaxTime = -1f;
    private int inheritedChildren;
    private int parentInQuartileCount;
    private readonly List<float> parentPercentiles = new List<float>(256);
    private float nextAliveSample;
    private float nextWallSample;
    private float nextRigidbodySample;
    private float lastWallSampleTime;
    private Camera arenaCamera;
    private GameObject groundHost;

    public bool Done => begun && simTime >= durationSeconds;
    public bool Begun => begun;
    public int AliveCount => alive.Count;
    public int MeasuredCount => measured.Count;
    public Camera RunnerCamera => arenaCamera;
    public IReadOnlyList<ArenaHumanAgent> MeasuredHumans => measured;

    public static int ArenaLayer
    {
        get
        {
            int layer = LayerMask.NameToLayer(ArenaLayerName);
            return layer >= 0 ? layer : 0;
        }
    }

    // ─── lifecycle ─────────────────────────────────────────────────────────
    private void Start()
    {
        if (externalTick) return;
        Begin(Time.fixedDeltaTime);
    }

    private void FixedUpdate()
    {
        if (externalTick || !begun) return;
        simTime += Time.fixedDeltaTime;
        Tick(simTime);
    }

    private void OnDestroy()
    {
        // Play mode: the scene run has no Finish(), so the summary is written when
        // the object goes away. A headless run is written by HeadlessTrial.Finish.
        if (externalTick || !begun) return;
        WriteFiles(simTime, -1f);
    }

    public void Begin(float fixedDelta)
    {
        if (begun) return;
        begun = true;
        seedUsedByRun = seed;
        rng = new ArenaRng(seed);
        simTime = 0f;
        nextSpawnTime = 0f;
        lastWallSampleTime = Time.realtimeSinceStartup;

        if (string.IsNullOrEmpty(runId))
            runId = label + "_s" + seed.ToString(CultureInfo.InvariantCulture);

        if (buildGroundOnBegin)
            BuildGroundOnly();

        LogLayerState();

        nextAliveSample = 0f;
        nextWallSample = 0f;
        if (!headless)
            EnsureArenaCamera(cameraSize, cameraOffsetX);
    }

    // The arena scene is ground and nothing else.
    private void BuildGroundOnly()
    {
        groundHost = new GameObject("ArenaGround");
        GroundBuilder ground = groundHost.AddComponent<GroundBuilder>();
        ground.groundSize = new Vector2(Mathf.Max(20f, groundWidth), 2f);
        ground.groundPosition = new Vector2(spawnX, -3f);
        ground.groundFriction = groundFriction;
        ground.BuildGround();
    }

    // Read the engine back: the ProjectSettings pair is the mechanism, this line is
    // the evidence. If the layer does not exist or the pair is live, the run is not
    // a no-collision run and that must be visible in the log, not silent.
    private void LogLayerState()
    {
        int layer = LayerMask.NameToLayer(ArenaLayerName);
        if (layer < 0)
        {
            Debug.LogError("ARENA_LAYER missing layer " + ArenaLayerName
                + " - humans would collide with each other");
            return;
        }
        bool selfIgnored = Physics2D.GetIgnoreLayerCollision(layer, layer);
        bool groundIgnored = Physics2D.GetIgnoreLayerCollision(layer, GroundLayers.Index);
        Debug.Log("ARENA_LAYER " + ArenaLayerName + "=" + layer
            + " selfPairIgnored=" + selfIgnored
            + " groundPairIgnored=" + groundIgnored);
        if (!selfIgnored)
            Debug.LogError("ARENA_LAYER self pair is LIVE - human-human collisions are on");
        if (groundIgnored)
            Debug.LogError("ARENA_LAYER ground pair is IGNORED - humans would fall through");
    }

    // ─── one fixed step ────────────────────────────────────────────────────
    public void Tick(float time)
    {
        if (!begun) return;
        simTime = time;
        float delta = Time.fixedDeltaTime;

        while (time + 1e-4f >= nextSpawnTime)
        {
            Spawn(nextSpawnTime);
            nextSpawnTime += spawnInterval;
        }

        for (int i = 0; i < alive.Count; i++)
            alive[i].Step(time);

        MeasureDue(time);
        CullDue(time);
        SampleCurves(time);
    }

    // ─── spawn + evolution ─────────────────────────────────────────────────
    private void Spawn(float time)
    {
        int id = spawnCount + 1;
        float[] genome;
        int parentId = -1;
        int childKind = 0;
        int mutationSeed = 0;
        int parentRank = -1;
        int poolSize = measured.Count;
        float parentFitness = 0f;
        bool parentBest = false;

        bool eliteTurn = measured.Count >= EliteMinMeasured
            && spawnIndex % EliteEverySpawns == 0
            && measured.Count > 0;

        if (eliteTurn)
        {
            ArenaHumanAgent best = BestMeasured();
            genome = ArenaGenome.Copy(best.genome);
            parentId = best.id;
            childKind = 2;
            mutationSeed = -1;
            parentRank = 0;
            parentFitness = best.Fitness;
            parentBest = true;
        }
        else if (poolSize > 0 && rng.Next01() < ChildShare)
        {
            List<ArenaHumanAgent> quartile = BestQuartile();
            ArenaHumanAgent parent = TournamentPick(quartile);
            genome = ArenaGenome.Copy(parent.genome);
            mutationSeed = rng.NextSeed();
            ArenaRng mutationRng = new ArenaRng(mutationSeed);
            ArenaGenome.Jitter(genome, mutationRng);
            parentId = parent.id;
            childKind = 1;
            parentRank = RankOf(parent);
            parentFitness = parent.Fitness;
            parentBest = quartile.Contains(parent);
            inheritedChildren++;
            if (parentBest) parentInQuartileCount++;
            if (poolSize > 1)
                parentPercentiles.Add(100f * (1f - parentRank / (float)(poolSize - 1)));
        }
        else
        {
            genome = ArenaGenome.Random(rng);
            mutationSeed = rng.NextSeed();
            childKind = 0;
        }

        spawnIndex++;
        spawnCount++;
        if (intervalSamples > 0)
        {
            float gap = time - lastSpawnTime;
            intervalSum += gap;
            intervalMin = Mathf.Min(intervalMin, gap);
            intervalMax = Mathf.Max(intervalMax, gap);
        }
        lastSpawnTime = time;
        intervalSamples++;

        if (alive.Count >= aliveCap)
        {
            Debug.LogWarning("ARENA_CAP alive cap " + aliveCap + " reached at t=" +
                time.ToString("0.###", CultureInfo.InvariantCulture) + " - culling the oldest");
            ArenaHumanAgent oldest = alive[0];
            Cull(oldest, time);
        }

        ArenaHumanAgent agent = SpawnBody(id, genome, childKind, mutationSeed, parentId,
            parentRank, poolSize, parentFitness, parentBest, time);
        if (agent == null) return;
        alive.Add(agent);
        all.Add(agent);
        if (childKind == 2) eliteCopies.Add(agent);
        maxAlive = Mathf.Max(maxAlive, alive.Count);
    }

    private ArenaHumanAgent SpawnBody(int id, float[] genome, int childKind, int mutationSeed,
        int parentId, int parentRank, int poolSize, float parentFitness, bool parentBest, float time)
    {
        GameObject humanObject = new GameObject("ArenaHuman_" + id);
        humanObject.transform.position = new Vector2(spawnX, spawnY);

        Human human = humanObject.AddComponent<Human>();
        // No build override is applied here on purpose: geometry, masses, friction,
        // muscle ceilings and gravity keep the rig defaults, so two humans of
        // different genomes differ only by the 13 genes.
        MotionIntent intent = humanObject.AddComponent<MotionIntent>();
        StepPhaseDriver stepDriver = humanObject.AddComponent<StepPhaseDriver>();
        stepDriver.externalDrive = true;

        human.BuildHuman();
        FactionStamp.Player(humanObject);

        PoseDriveController pose = humanObject.GetComponent<PoseDriveController>();
        if (pose == null)
        {
            Debug.LogError("ARENA_SPAWN no PoseDriveController on human " + id);
            Destroy(humanObject);
            return null;
        }
        pose.ApplyArenaGenome(genome, true);
        intent.moveX = 1f;                  // walks the frame it spawns

        SetLayerRecursively(humanObject, ArenaLayer);
        ArenaHumanAgent.AttachNumber(humanObject, id, numberHeight);

        ArenaHumanAgent agent = humanObject.AddComponent<ArenaHumanAgent>();
        agent.id = id;
        agent.childKind = childKind;
        agent.mutationSeed = mutationSeed;
        agent.parentId = parentId;
        agent.parentRank = parentRank;
        agent.poolSize = poolSize;
        agent.parentFitness = parentFitness;
        agent.parentInBestQuartile = parentBest;
        agent.genome = genome;
        agent.Bind(human, time, spawnX, Time.fixedDeltaTime);
        return agent;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        Transform t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }

    private ArenaHumanAgent BestMeasured()
    {
        ArenaHumanAgent best = null;
        float bestFitness = float.NegativeInfinity;
        for (int i = 0; i < measured.Count; i++)
        {
            float fitness = measured[i].Fitness;
            if (fitness > bestFitness)
            {
                bestFitness = fitness;
                best = measured[i];
            }
        }
        return best;
    }

    // The best quartile of the measured pool: selection happens only there, which is
    // where the "parent came from the top quartile" fraction of the report lives.
    private List<ArenaHumanAgent> BestQuartile()
    {
        List<ArenaHumanAgent> sorted = new List<ArenaHumanAgent>(measured);
        sorted.Sort((a, b) => b.Fitness.CompareTo(a.Fitness));
        int size = Mathf.Max(1, Mathf.CeilToInt(sorted.Count * 0.25f));
        if (size > sorted.Count) size = sorted.Count;
        return sorted.GetRange(0, size);
    }

    // Tournament: draw TournamentSize candidates from the quartile, keep the fittest.
    private ArenaHumanAgent TournamentPick(List<ArenaHumanAgent> quartile)
    {
        if (quartile == null || quartile.Count == 0) return measured[0];
        int draws = Mathf.Max(1, Mathf.RoundToInt(TournamentSize));
        ArenaHumanAgent best = null;
        float bestFitness = float.NegativeInfinity;
        for (int i = 0; i < draws; i++)
        {
            ArenaHumanAgent candidate = quartile[rng.NextInt(quartile.Count)];
            if (candidate.Fitness > bestFitness)
            {
                bestFitness = candidate.Fitness;
                best = candidate;
            }
        }
        return best;
    }

    private int RankOf(ArenaHumanAgent agent)
    {
        int rank = 0;
        for (int i = 0; i < measured.Count; i++)
        {
            if (measured[i] == agent) continue;
            if (measured[i].Fitness > agent.Fitness) rank++;
        }
        return rank;
    }

    // ─── measurement and cull ──────────────────────────────────────────────
    private void MeasureDue(float time)
    {
        for (int i = 0; i < alive.Count; i++)
        {
            ArenaHumanAgent agent = alive[i];
            if (agent.measuredTime >= 0f) continue;
            if (time - agent.spawnTime + 1e-4f < measureSeconds) continue;
            agent.MarkMeasured(time);
            measuredCount++;
            measured.Add(agent);
            AppendInfluenceRow(agent);
        }
    }

    private void CullDue(float time)
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            ArenaHumanAgent agent = alive[i];
            if (time - agent.spawnTime + 1e-4f < cullSeconds) continue;
            Cull(agent, time);
        }
    }

    private void Cull(ArenaHumanAgent agent, float time)
    {
        agent.MarkCulled(time, agent.UprightNow());
        culledCount++;
        alive.Remove(agent);
        if (agent.gameObject != null)
            Destroy(agent.gameObject);
    }

    private void SampleCurves(float time)
    {
        if (time + 1e-4f < nextAliveSample) return;
        nextAliveSample += 0.5f;
        aliveCurve.Add("[" + time.ToString("0.##", CultureInfo.InvariantCulture) + ","
            + alive.Count.ToString(CultureInfo.InvariantCulture) + "]");

        int overlaps = CountOverlaps();
        if (overlaps > overlapMax)
        {
            overlapMax = overlaps;
            overlapMaxTime = time;
        }

        // The RAM claim needs a body count, not an opinion: count the real
        // Rigidbody2D objects in the world every 5 s (a full scan is not cheap).
        if (time + 1e-4f >= nextRigidbodySample)
        {
            nextRigidbodySample += 5f;
            Rigidbody2D[] bodies = FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);
            maxRigidbodies = Mathf.Max(maxRigidbodies, bodies.Length);
        }

        if (time + 1e-4f < nextWallSample) return;
        nextWallSample += 1f;
        float wall = Time.realtimeSinceStartup;
        float wallDelta = wall - lastWallSampleTime;
        lastWallSampleTime = wall;
        wallCurve.Add("[" + time.ToString("0.##", CultureInfo.InvariantCulture) + ","
            + (wallDelta / Mathf.Max(0.001f, 1f)).ToString("0.###", CultureInfo.InvariantCulture) + "]");
    }

    // Humans whose pelvis stands within 0.4 m of another human's pelvis: the
    // numeric side of "several humans visibly overlap without pushing each other".
    private int CountOverlaps()
    {
        int count = 0;
        for (int i = 0; i < alive.Count; i++)
        {
            float xi = alive[i].Distance;
            for (int j = i + 1; j < alive.Count; j++)
            {
                float xj = alive[j].Distance;
                if (Mathf.Abs(xi - xj) < 0.4f)
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    // ─── camera ────────────────────────────────────────────────────────────
    public Camera EnsureArenaCamera(float size, float offsetX)
    {
        if (arenaCamera == null)
        {
            arenaCamera = Camera.main;
            if (arenaCamera == null)
                arenaCamera = FindFirstObjectByType<Camera>();
        }
        if (arenaCamera == null)
        {
            GameObject go = new GameObject("ArenaCamera");
            arenaCamera = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            arenaCamera.clearFlags = CameraClearFlags.SolidColor;
            arenaCamera.backgroundColor = SceneLighting.NightSky;
        }
        arenaCamera.orthographic = true;
        SetLens(size, spawnX + offsetX);
        return arenaCamera;
    }

    public void SetLens(float size, float centerX)
    {
        if (arenaCamera == null) return;
        arenaCamera.orthographic = true;
        arenaCamera.orthographicSize = size;
        arenaCamera.transform.position = new Vector3(centerX, -0.2f, -10f);
    }

    // ─── files ─────────────────────────────────────────────────────────────
    private string InfluenceLogPath => Path.Combine(outputFolder, "arena-param-log.csv");
    private string HumanCsvPath => Path.Combine(outputFolder, label + ".csv");
    private string SummaryPath => Path.Combine(outputFolder, label + ".json");
    private string FallbackRowPath => Path.Combine(outputFolder, label + "_paramrows.csv");
    private bool influenceHeaderChecked;
    private bool appendToInfluenceLog = true;

    private static string InfluenceHeader()
    {
        return "run_id,seed,human_id,parent_id,child_kind,spawn_t,distance_m,upright,fallen,"
            + "destroyed_t," + ArenaGenome.CsvHeader();
    }

    private bool CanAppendToInfluenceLog()
    {
        if (influenceHeaderChecked) return appendToInfluenceLog;
        influenceHeaderChecked = true;
        try
        {
            Directory.CreateDirectory(outputFolder);
            if (File.Exists(InfluenceLogPath))
            {
                string first = null;
                using (StreamReader reader = new StreamReader(InfluenceLogPath))
                    first = reader.ReadLine();
                if (first != null && first.Trim() != InfluenceHeader())
                {
                    appendToInfluenceLog = false;
                    Debug.LogError("ARENA_CSV_HEADER_MISMATCH " + InfluenceLogPath
                        + " header is not the contract header - rows go to " + FallbackRowPath);
                }
            }
            else
            {
                File.WriteAllText(InfluenceLogPath, InfluenceHeader() + "\n");
            }
        }
        catch (System.Exception e)
        {
            appendToInfluenceLog = false;
            Debug.LogError("ARENA_CSV_OPEN_FAILED " + e.Message + " - rows go to " + FallbackRowPath);
        }
        return appendToInfluenceLog;
    }

    private void AppendInfluenceRow(ArenaHumanAgent agent)
    {
        if (string.IsNullOrEmpty(outputFolder)) return;
        string path = CanAppendToInfluenceLog() ? InfluenceLogPath : FallbackRowPath;
        try
        {
            string line = string.Join(",",
                runId,
                seed.ToString(CultureInfo.InvariantCulture),
                agent.id.ToString(CultureInfo.InvariantCulture),
                agent.parentId.ToString(CultureInfo.InvariantCulture),
                agent.childKind.ToString(CultureInfo.InvariantCulture),
                agent.spawnTime.ToString("0.###", CultureInfo.InvariantCulture),
                agent.distanceAt15.ToString("0.#####", CultureInfo.InvariantCulture),
                agent.uprightAt15 ? "1" : "0",
                agent.fallen ? "1" : "0",
                "",                       // destroyed_t is filled into the CSV at cull time
                ArenaGenome.CsvValues(agent.genome));
            // destroyed_t unknown at 15 s: the human is still walking. The row is
            // rewritten by RewriteInfluenceRow at cull with the real time.
            File.AppendAllText(path, line + "\n");
        }
        catch (System.Exception e)
        {
            Debug.LogError("ARENA_CSV_WRITE_FAILED " + e.Message);
        }
    }

    // At cull the destroyed_t of an already appended row is filled in place: the
    // contract wants one row per measured human, and the row must not be written
    // twice. Only that one field of that one line is touched.
    private void FillDestroyedTime(ArenaHumanAgent agent)
    {
        if (string.IsNullOrEmpty(outputFolder)) return;
        string path = CanAppendToInfluenceLog() ? InfluenceLogPath : FallbackRowPath;
        try
        {
            if (!File.Exists(path)) return;
            string[] lines = File.ReadAllLines(path);
            string prefix = runId + "," + seed.ToString(CultureInfo.InvariantCulture) + ","
                + agent.id.ToString(CultureInfo.InvariantCulture) + ",";
            for (int i = 1; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith(prefix, System.StringComparison.Ordinal)) continue;
                string[] fields = lines[i].Split(',');
                if (fields.Length < 10) return;
                fields[9] = agent.destroyedTime.ToString("0.###", CultureInfo.InvariantCulture);
                lines[i] = string.Join(",", fields);
                File.WriteAllLines(path, lines);
                return;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("ARENA_CSV_FILL_FAILED " + e.Message);
        }
    }

    public string BuildHumanCsv()
    {
        StringBuilder sb = new StringBuilder(1024 * 64);
        sb.Append("run_id,seed,human_id,parent_id,child_kind,mutation_seed,spawn_t,measured_t,");
        sb.Append("distance_15s,upright_15s,fallen,fall_t,tilt_at_fall,destroyed_t,distance_30s,");
        sb.Append("upright_at_cull,alive_at_cull,parent_rank,pool_size,parent_fitness,parent_best_quartile,");
        sb.Append("trajectory_samples,");
        sb.Append(ArenaGenome.CsvHeader());
        sb.Append('\n');
        for (int i = 0; i < all.Count; i++)
        {
            ArenaHumanAgent a = all[i];
            sb.Append(runId).Append(',');
            sb.Append(seed.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(a.id.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(a.parentId.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(a.childKind.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(a.mutationSeed.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(a.spawnTime.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(a.measuredTime.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Fmt(a.distanceAt15)).Append(',');
            sb.Append(a.uprightAt15 ? "1" : "0").Append(',');
            sb.Append(a.fallen ? "1" : "0").Append(',');
            sb.Append(a.fallTime.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Fmt(a.tiltAtFall)).Append(',');
            sb.Append(a.destroyedTime.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Fmt(a.distanceAtCull)).Append(',');
            sb.Append(a.uprightAtCull ? "1" : "0").Append(',');
            sb.Append(a.aliveAtCull ? "1" : "0").Append(',');
            sb.Append(a.parentRank.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(a.poolSize.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Fmt(a.parentFitness)).Append(',');
            sb.Append(a.parentInBestQuartile ? "1" : "0").Append(',');
            sb.Append(a.TrajectorySamples.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(ArenaGenome.CsvValues(a.genome)).Append('\n');
        }
        return sb.ToString();
    }

    private static string Fmt(float value)
    {
        if (float.IsNaN(value)) return "";
        return value.ToString("0.#####", CultureInfo.InvariantCulture);
    }

    // JSON has no empty number: an unmeasured value is null, never "".
    private static string Jnum(float value)
    {
        if (float.IsNaN(value)) return "null";
        return value.ToString("0.#####", CultureInfo.InvariantCulture);
    }

    public string BuildSummaryJson(float simulatedSeconds, float wallSeconds)
    {
        StringBuilder json = new StringBuilder(1024 * 64);
        json.Append('{');
        json.Append("\"label\":\"").Append(label).Append('"');
        json.Append(",\"runId\":\"").Append(runId).Append('"');
        json.Append(",\"arena\":true");
        json.Append(",\"headless\":").Append(headless ? "true" : "false");
        json.Append(",\"seed\":").Append(seed.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"sourceFingerprint\":\"").Append(sourceFingerprint).Append('"');
        json.Append(",\"buildGuid\":\"").Append(buildGuid).Append('"');
        json.Append(",\"simulatedSeconds\":").Append(Num(simulatedSeconds));
        json.Append(",\"wallSeconds\":").Append(Num(wallSeconds));
        if (simulatedSeconds > 0f && wallSeconds >= 0f)
            json.Append(",\"wallPerSim\":").Append(Num(wallSeconds / simulatedSeconds));
        json.Append(",\"fixedDeltaTime\":").Append(Num(Time.fixedDeltaTime));

        json.Append(",\"config\":{");
        json.Append("\"spawnIntervalSeconds\":").Append(Num(spawnInterval));
        json.Append(",\"measureSeconds\":").Append(Num(measureSeconds));
        json.Append(",\"cullSeconds\":").Append(Num(cullSeconds));
        json.Append(",\"aliveCap\":").Append(aliveCap.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"durationSeconds\":").Append(Num(durationSeconds));
        json.Append(",\"spawnPoint\":{\"x\":").Append(Num(spawnX)).Append(",\"y\":").Append(Num(spawnY)).Append('}');
        json.Append(",\"groundWidthMeters\":").Append(Num(groundWidth));
        json.Append(",\"numberHeightMeters\":").Append(Num(numberHeight));
        json.Append('}');

        json.Append(",\"spawn\":{");
        json.Append("\"spawns\":").Append(spawnCount.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"measured\":").Append(measuredCount.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"culled\":").Append(culledCount.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"maxAlive\":").Append(maxAlive.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"aliveAtEnd\":").Append(alive.Count.ToString(CultureInfo.InvariantCulture));
        if (intervalSamples > 1)
        {
            float mean = intervalSum / (intervalSamples - 1);
            json.Append(",\"intervalMeanSeconds\":").Append(Num(mean));
            json.Append(",\"intervalMinSeconds\":").Append(Num(intervalMin));
            json.Append(",\"intervalMaxSeconds\":").Append(Num(intervalMax));
            json.Append(",\"intervalNominalSeconds\":").Append(Num(spawnInterval));
            json.Append(",\"intervalAssert\":\"").Append(
                Mathf.Abs(mean - spawnInterval) <= 0.001f ? "OK" : "DRIFT").Append('"');
        }
        json.Append(",\"aliveCurve\":[").Append(string.Join(",", aliveCurve)).Append(']');
        json.Append(",\"wallPerSimCurve\":[").Append(string.Join(",", wallCurve)).Append(']');
        json.Append(",\"maxRigidbodies\":").Append(maxRigidbodies.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"overlapMaxHumans\":").Append(overlapMax.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"overlapMaxTime\":").Append(Num(overlapMaxTime));
        json.Append('}');

        json.Append(",\"layer\":{");
        int layer = LayerMask.NameToLayer(ArenaLayerName);
        json.Append("\"name\":\"").Append(ArenaLayerName).Append('"');
        json.Append(",\"index\":").Append(layer.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"selfPairIgnored\":").Append(
            (layer >= 0 && Physics2D.GetIgnoreLayerCollision(layer, layer)) ? "true" : "false");
        json.Append(",\"groundPairIgnored\":").Append(
            (layer >= 0 && Physics2D.GetIgnoreLayerCollision(layer, GroundLayers.Index)) ? "true" : "false");
        json.Append(",\"groundLayer\":").Append(GroundLayers.Index.ToString(CultureInfo.InvariantCulture));
        json.Append('}');

        json.Append(",\"evolution\":{");
        json.Append("\"fitness\":\"distance_15s minus fall penalty\"");
        json.Append(",\"fallPenaltyMeters\":").Append(Num(ArenaGenome.FallPenaltyMeters));
        json.Append(",\"childShare\":").Append(Num(ChildShare));
        json.Append(",\"freshShare\":").Append(Num(1f - ChildShare));
        json.Append(",\"jitterShareOfRange\":\"0.05-0.10\"");
        json.Append(",\"selection\":\"tournament of 3 inside the best quartile\"");
        json.Append(",\"eliteMinMeasured\":").Append(EliteMinMeasured.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"eliteEverySpawns\":").Append(EliteEverySpawns.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"eliteCopies\":").Append(eliteCopies.Count.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"inheritedChildren\":").Append(inheritedChildren.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"parentsInBestQuartile\":").Append(parentInQuartileCount.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"parentQuartileFraction\":").Append(Num(inheritedChildren > 0
            ? parentInQuartileCount / (float)inheritedChildren : 0f));
        json.Append(",\"parentQuartileChanceFraction\":0.25");
        json.Append(",\"parentPercentileMean\":").Append(Num(MeanPercentile()));
        json.Append(",\"ranges\":").Append(ArenaGenome.RangesJson());
        json.Append('}');

        json.Append(",\"best\":");
        ArenaHumanAgent best = BestMeasured();
        if (best == null)
        {
            json.Append("null");
        }
        else
        {
            json.Append('{');
            json.Append("\"humanId\":").Append(best.id.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"fitness\":").Append(Num(best.Fitness));
            json.Append(",\"distance15\":").Append(Jnum(best.distanceAt15));
            json.Append(",\"fallen\":").Append(best.fallen ? "true" : "false");
            json.Append(",\"genome\":").Append(ArenaGenome.ToJson(best.genome));
            json.Append('}');
        }

        json.Append(",\"elitePairs\":[").Append(ElitePairsJson()).Append(']');
        json.Append(",\"humans\":[").Append(HumansJson()).Append(']');
        json.Append('}');
        return json.ToString();
    }

    private float MeanPercentile()
    {
        if (parentPercentiles.Count == 0) return 0f;
        float sum = 0f;
        for (int i = 0; i < parentPercentiles.Count; i++) sum += parentPercentiles[i];
        return sum / parentPercentiles.Count;
    }

    // The collision proof. Each group is one genome re-spawned unchanged; the first
    // copy is the reference and every later copy must repeat its distance and its
    // early path. A younger copy that is deflected means humans still touch.
    private string ElitePairsJson()
    {
        StringBuilder sb = new StringBuilder(1024);
        bool first = true;
        for (int i = 0; i < eliteCopies.Count - 1; i++)
        {
            ArenaHumanAgent reference = eliteCopies[i];
            for (int j = 1; j < eliteCopies.Count; j++)
            {
                ArenaHumanAgent other = eliteCopies[j];
                if (!SameGenome(reference.genome, other.genome)) continue;
                if (!first) sb.Append(',');
                first = false;
                float maxPath = 0f, maxDx = 0f, maxDy = 0f;
                int tracked = 0;
                for (float age = 0.5f; age <= 15f; age += 0.5f)
                {
                    float x1, y1, x2, y2;
                    if (!reference.TrajectoryAt(age, out x1, out y1)) continue;
                    if (!other.TrajectoryAt(age, out x2, out y2)) continue;
                    float dx = Mathf.Abs(x2 - x1);
                    float dy = Mathf.Abs(y2 - y1);
                    maxDx = Mathf.Max(maxDx, dx);
                    maxDy = Mathf.Max(maxDy, dy);
                    maxPath = Mathf.Max(maxPath, Mathf.Sqrt(dx * dx + dy * dy));
                    tracked++;
                }
                sb.Append("{\"referenceId\":").Append(reference.id.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"copyId\":").Append(other.id.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"referenceAgeAtMeasure\":").Append(Num(reference.measuredTime - reference.spawnTime));
                sb.Append(",\"copyAgeAtMeasure\":").Append(Num(other.measuredTime - other.spawnTime));
                sb.Append(",\"referenceDistance15\":").Append(Jnum(reference.distanceAt15));
                sb.Append(",\"copyDistance15\":").Append(Jnum(other.distanceAt15));
                sb.Append(",\"distanceDeltaMeters\":").Append(Jnum(
                    Mathf.Abs(other.distanceAt15 - reference.distanceAt15)));
                sb.Append(",\"trajectorySamples\":").Append(tracked.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"maxTrajectoryDeltaMeters\":").Append(Num(maxPath));
                sb.Append(",\"maxTrajectoryDeltaX\":").Append(Num(maxDx));
                sb.Append(",\"maxTrajectoryDeltaY\":").Append(Num(maxDy));
                sb.Append(",\"sameGenome\":true}");
            }
        }
        return sb.ToString();
    }

    private static bool SameGenome(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (Mathf.Abs(a[i] - b[i]) > 1e-6f) return false;
        return true;
    }

    private string HumansJson()
    {
        StringBuilder sb = new StringBuilder(1024 * 32);
        // Trajectories are heavy: every elite copy carries one (they are the collision
        // proof), the rest carry their sample count only.
        List<int> heavy = new List<int>();
        for (int i = 0; i < eliteCopies.Count; i++) heavy.Add(eliteCopies[i].id);
        for (int i = 0; i < all.Count; i++)
        {
            ArenaHumanAgent a = all[i];
            if (i > 0) sb.Append(',');
            sb.Append('{');
            sb.Append("\"id\":").Append(a.id.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"spawnT\":").Append(Num(a.spawnTime));
            sb.Append(",\"genome\":").Append(ArenaGenome.ToJson(a.genome));
            sb.Append(",\"parentId\":").Append(a.parentId.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"childKind\":").Append(a.childKind.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"mutationSeed\":").Append(a.mutationSeed.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"parentRank\":").Append(a.parentRank.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"poolSize\":").Append(a.poolSize.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"parentBestQuartile\":").Append(a.parentInBestQuartile ? "true" : "false");
            sb.Append(",\"measuredT\":").Append(Num(a.measuredTime));
            sb.Append(",\"distance15\":").Append(Jnum(a.distanceAt15));
            sb.Append(",\"upright15\":").Append(a.uprightAt15 ? "true" : "false");
            sb.Append(",\"fallen\":").Append(a.fallen ? "true" : "false");
            sb.Append(",\"fallT\":").Append(Num(a.fallTime));
            sb.Append(",\"tiltAtFallDeg\":").Append(Jnum(a.tiltAtFall));
            sb.Append(",\"destroyedT\":").Append(Num(a.destroyedTime));
            sb.Append(",\"distance30\":").Append(Jnum(a.distanceAtCull));
            sb.Append(",\"uprightAtCull\":").Append(a.uprightAtCull ? "true" : "false");
            sb.Append(",\"aliveAtCull\":").Append(a.aliveAtCull ? "true" : "false");
            sb.Append(",\"fitness\":").Append(Num(a.Fitness));
            sb.Append(",\"trajectorySamples\":").Append(a.TrajectorySamples.ToString(CultureInfo.InvariantCulture));
            if (heavy.Contains(a.id))
                sb.Append(",\"trajectory\":").Append(a.TrajectoryJson());
            sb.Append('}');
        }
        return sb.ToString();
    }

    private static string Num(float value)
    {
        if (float.IsNaN(value)) return "null";
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    // Write the run artifacts and return the summary. Safe to call twice: the second
    // call only refreshes the summary and the table, the append-only influence log is
    // written as rows are measured and is never rewritten.
    public string WriteFiles(float simulatedSeconds, float wallSeconds)
    {
        string summary = BuildSummaryJson(simulatedSeconds, wallSeconds);
        if (string.IsNullOrEmpty(outputFolder)) return summary;
        try
        {
            Directory.CreateDirectory(outputFolder);
            for (int i = 0; i < measured.Count; i++)
                FillDestroyedTime(measured[i]);
            File.WriteAllText(HumanCsvPath, BuildHumanCsv());
            File.WriteAllText(SummaryPath, summary);
            Debug.Log("ARENA_SUMMARY " + summary);
            Debug.Log("ARENA_CSV " + HumanCsvPath);
            Debug.Log("ARENA_LOG " + InfluenceLogPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("ARENA_WRITE_FAILED " + e.Message);
        }
        return summary;
    }

    // Called by HeadlessTrial for the frame markers.
    public string HumanCsvFilePath => HumanCsvPath;
}
