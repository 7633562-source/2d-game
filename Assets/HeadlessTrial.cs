using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

// Прогон одного человека без окна редактора, для численного анализа баланса.
// Активируется аргументом -trial в командной строке собранного плеера:
//   BalanceTrial.exe -batchmode -nographics -trial -duration 10 -label base -logFile -
//   -fixedDelta 0.005 — шаг физики (0.02 = 50 Гц, 0.005 = 200 Гц)
//   -activationSpeed 10 — скорость набора силы мышцы
//   -pushImpulse 20 -pushTime 5 — толчок в торс (Н·с, + вперёд) на 5-й секунде
// Все коэффициенты баланса можно переопределить аргументами, не пересобирая плеер,
// поэтому один билд обслуживает любой перебор параметров.
public class HeadlessTrial : MonoBehaviour
{
    // GameProcess проверяет этот флаг и не строит своих 50 человек,
    // иначе прогон утонет в 700 Rigidbody2D.
    public static bool Active { get; private set; }

    // Предохранитель: если физика зависнет, процесс не должен жить вечно.
    private const float REAL_TIME_LIMIT_SECONDS = 600f;

    private float duration = 10f;
    private string label = "trial";
    private string outputFolder;
    private float startY = -0.82f;

    // Толчок в торс: проверка, держит ли регулятор возмущение, а не только
    // сам себя. Без него равновесие может быть просто статической позой.
    private float pushTime;
    private float pushImpulse;
    private Rigidbody2D torsoBody;

    private Human human;
    private TrialRecorder recorder;
    private TrialRunInfo runInfo;

    [Serializable]
    private class BuildManifestDto
    {
        public string fingerprint;
        public string algorithm;
        public string unityVersion;
        public string unityPath;
        public string buildTimeUtc;
        public bool repositoryPresent;
        public bool gitAvailable;
        public string gitCommit;
        public bool gitDirty;
        public string buildGuid;
        public string outputPath;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "-trial") < 0) return;

        Active = true;

        // Шаг физики задаём до загрузки сцены: иначе первый FixedUpdate
        // ещё идёт со стандартными 0.02 с, и сравнение частот будет кривым.
        float fixedDelta = GetFloatArg("-fixedDelta", Time.fixedDeltaTime);
        if (fixedDelta > 0f)
            Time.fixedDeltaTime = fixedDelta;

        GameObject host = new GameObject("HeadlessTrial");
        DontDestroyOnLoad(host);
        host.AddComponent<HeadlessTrial>();
    }

    private void Start()
    {
        duration = GetFloatArg("-duration", 10f);
        label = GetStringArg("-label", "trial");
        outputFolder = GetStringArg("-out", DefaultOutputFolder());
        startY = GetFloatArg("-startY", -0.82f);
        pushImpulse = GetFloatArg("-pushImpulse", 0f);
        pushTime = GetFloatArg("-pushTime", 5f);

        string provenanceError;
        if (!TryAcceptProvenance(out runInfo, out provenanceError))
        {
            Debug.LogError("TRIAL_PROVENANCE_FAILED " + provenanceError);
            Application.Quit(3);
            return;
        }

        BuildTrialScene();
        StartCoroutine(RunTrial());
    }

    // ─── СБОРКА МИНИМАЛЬНОЙ СЦЕНЫ: ЗЕМЛЯ + ОДИН ЧЕЛОВЕК ───
    private void BuildTrialScene()
    {
        GameObject groundHost = new GameObject("TrialGround");
        GroundBuilder ground = groundHost.AddComponent<GroundBuilder>();
        ground.groundSize = new Vector2(20f, 2f);
        ground.groundPosition = new Vector2(0f, -3f);
        ground.BuildGround();

        GameObject humanObject = new GameObject("TrialHuman");
        // y = −0.82 ставит стопы точно на верхнюю кромку земли (−2.0):
        // (pelvisSize.y + torsoSize.y)/2 + бедро + голень + стопа
        // = 0.25 + 0.43 + 0.43 + 0.07 = 1.18.
        humanObject.transform.position = new Vector2(0f, startY);

        human = humanObject.AddComponent<Human>();

        // Моменты и трение читаются при построении тела, поэтому меняем их до BuildHuman.
        float muscleMultiplier = GetFloatArg("-muscle", 1f);
        float frictionMultiplier = GetFloatArg("-friction", 1f);
        ApplyMultipliers(human, muscleMultiplier, frictionMultiplier);

        human.BuildHuman();

        // Коэффициенты регуляторов живут в BalanceController, который появляется
        // внутри BuildHuman, поэтому переопределяем их после сборки.
        ApplyGains(human.GetComponent<BalanceController>());

        Transform torso = human.transform.Find("Torso");
        torsoBody = torso != null ? torso.GetComponent<Rigidbody2D>() : null;
    }

    private void ApplyMultipliers(Human target, float muscle, float friction)
    {
        target.muscleMultiplier = muscle;
        target.frictionMultiplier = friction;

        target.hipMuscleTorque *= muscle;
        target.kneeMuscleTorque *= muscle;
        target.ankleExtensorTorque *= muscle;
        target.ankleFlexorTorque *= muscle;
        target.shoulderMuscleTorque *= muscle;
        target.elbowMuscleTorque *= muscle;
        target.wristMuscleTorque *= muscle;
        target.neckMuscleTorque *= muscle;
        target.lumbarMuscleTorque *= muscle;

        target.hipFriction *= friction;
        target.kneeFriction *= friction;
        target.ankleFriction *= friction;
        target.shoulderFriction *= friction;
        target.elbowFriction *= friction;
        target.wristFriction *= friction;
        target.neckFriction *= friction;
        target.lumbarFriction *= friction;
        target.lumbarFrictionMaxTorque = GetFloatArg("-lumbarFrictionCap", target.lumbarFrictionMaxTorque);
    }

    private void ApplyGains(BalanceController balance)
    {
        if (balance == null)
        {
            Debug.LogError("HeadlessTrial: BalanceController не найден на человеке.");
            return;
        }

        balance.hipPGain = GetFloatArg("-hipP", balance.hipPGain);
        balance.hipDGain = GetFloatArg("-hipD", balance.hipDGain);
        balance.kneePGain = GetFloatArg("-kneeP", balance.kneePGain);
        balance.kneeDGain = GetFloatArg("-kneeD", balance.kneeDGain);
        balance.comProportionalGain = GetFloatArg("-comP", balance.comProportionalGain);
        balance.comDerivativeGain = GetFloatArg("-comD", balance.comDerivativeGain);

        balance.hipBaseAngle = GetFloatArg("-hipBase", balance.hipBaseAngle);
        balance.kneeBaseAngle = GetFloatArg("-kneeBase", balance.kneeBaseAngle);
        balance.hipBalanceGain = GetFloatArg("-hipBalance", balance.hipBalanceGain);
        balance.ankleComP = GetFloatArg("-ankleComP", balance.ankleComP);
        balance.ankleComD = GetFloatArg("-ankleComD", balance.ankleComD);
        balance.lumbarPGain = GetFloatArg("-lumbarP", balance.lumbarPGain);
        balance.lumbarDGain = GetFloatArg("-lumbarD", balance.lumbarDGain);
        balance.lumbarTargetTilt = GetFloatArg("-lumbarTilt", balance.lumbarTargetTilt);
        balance.lumbarErrorReferenceDegrees = GetFloatArg("-lumbarRef", balance.lumbarErrorReferenceDegrees);
        balance.recoveryCoMOffset = GetFloatArg("-recoveryOffset", balance.recoveryCoMOffset);
        balance.fallCoMOffset = GetFloatArg("-fallOffset", balance.fallCoMOffset);

        // Скорость, с которой активация мышцы догоняет команду регулятора.
        // 500 — почти мгновенно; 10 — мышца набирает силу за ~0.1 с.
        balance.muscleActivationSpeed = GetFloatArg("-activationSpeed", balance.muscleActivationSpeed);
    }

    // ─── САМ ПРОГОН: ШАГАЕМ ФИЗИКОЙ И СНИМАЕМ СОСТОЯНИЕ ───
    private IEnumerator RunTrial()
    {
        // Первый шаг пропускаем: на нём тело ещё оседает в суставах.
        yield return new WaitForFixedUpdate();

        recorder = new TrialRecorder(human, runInfo);

        float simulated = 0f;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        bool pushed = false;

        while (simulated < duration)
        {
            yield return new WaitForFixedUpdate();

            simulated += Time.fixedDeltaTime;

            if (!pushed && pushImpulse != 0f && simulated >= pushTime)
            {
                ApplyPush(simulated);
                pushed = true;
            }

            recorder.Sample(simulated);

            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: превышен лимит реального времени, прогон прерван.");
                break;
            }
        }

        Finish(simulated);
    }

    // Импульс прикладываем к торсу: так толкают человека в плечо, а не в стопу.
    private void ApplyPush(float time)
    {
        if (torsoBody == null)
        {
            Debug.LogWarning("HeadlessTrial: торс не найден, толчок не применён.");
            return;
        }

        torsoBody.AddForce(new Vector2(pushImpulse, 0f), ForceMode2D.Impulse);
        recorder.MarkPush(time, pushImpulse);
    }

    private void Finish(float simulated)
    {
        try
        {
            Directory.CreateDirectory(outputFolder);

            string csvPath = Path.Combine(outputFolder, label + ".csv");
            File.WriteAllText(csvPath, recorder.BuildCsv());

            string summary = recorder.BuildSummaryJson(simulated);
            File.WriteAllText(Path.Combine(outputFolder, label + ".json"), summary);

            // Маркер в stdout, чтобы результат читался прямо из лога прогона.
            Debug.Log("TRIAL_SUMMARY " + summary);
            Debug.Log("TRIAL_CSV " + csvPath);
        }
        catch (Exception e)
        {
            Debug.LogError("HeadlessTrial: не удалось записать результаты: " + e.Message);
            Application.Quit(2);
            return;
        }

        Application.Quit(0);
    }

    private bool TryAcceptProvenance(out TrialRunInfo info, out string error)
    {
        info = null;
        error = null;

        BuildManifestDto manifest = LoadBuildManifest();
        string argFingerprint = GetStringArg("-sourceFingerprint", "");
        string manifestFingerprint = manifest != null ? manifest.fingerprint : "";
        string manifestGuid = manifest != null ? manifest.buildGuid : "";
        string appGuid = Application.buildGUID;

        if (!string.IsNullOrEmpty(argFingerprint) && !string.IsNullOrEmpty(manifestFingerprint)
            && argFingerprint != manifestFingerprint)
        {
            error = "sourceFingerprint arg != build-manifest.fingerprint";
            return false;
        }

        if (!string.IsNullOrEmpty(appGuid) && !string.IsNullOrEmpty(manifestGuid)
            && NormalizeGuid(appGuid) != NormalizeGuid(manifestGuid))
        {
            error = "Application.buildGUID != build-manifest.buildGuid";
            return false;
        }

        string fingerprint = !string.IsNullOrEmpty(manifestFingerprint)
            ? manifestFingerprint
            : (argFingerprint ?? "");

        info = new TrialRunInfo
        {
            Label = label,
            Duration = duration,
            StartY = startY,
            RequestedPushImpulse = pushImpulse,
            RequestedPushTime = pushTime,
            MuscleMultiplier = GetFloatArg("-muscle", 1f),
            FrictionMultiplier = GetFloatArg("-friction", 1f),
            SourceFingerprint = fingerprint ?? "",
            ManifestUnityVersion = manifest != null ? manifest.unityVersion : "",
            ManifestBuildGuid = manifestGuid ?? "",
            ManifestBuildTimeUtc = manifest != null ? manifest.buildTimeUtc : "",
            ManifestPath = FindBuildManifestPath() ?? "",
            CommandArgs = Environment.GetCommandLineArgs()
        };
        return true;
    }

    private static string NormalizeGuid(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("-", "").ToLowerInvariant();
    }

    private static BuildManifestDto LoadBuildManifest()
    {
        string path = FindBuildManifestPath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            return JsonUtility.FromJson<BuildManifestDto>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning("HeadlessTrial: не прочитал build-manifest: " + e.Message);
            return null;
        }
    }

    private static string FindBuildManifestPath()
    {
        string fromArg = GetStringArg("-buildManifest", null);
        if (!string.IsNullOrEmpty(fromArg) && File.Exists(fromArg))
            return fromArg;

        try
        {
            string parent = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(parent))
            {
                string nextToExe = Path.Combine(parent, "build-manifest.json");
                if (File.Exists(nextToExe))
                    return nextToExe;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string DefaultOutputFolder()
    {
        try
        {
            string parent = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(parent))
                return Path.Combine(parent, "trials");
        }
        catch
        {
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "trials");
    }

    // ─── ЧТЕНИЕ АРГУМЕНТОВ КОМАНДНОЙ СТРОКИ ───
    private static string GetStringArg(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length) return fallback;
        return args[index + 1];
    }

    private static float GetFloatArg(string name, float fallback)
    {
        string raw = GetStringArg(name, null);
        if (string.IsNullOrEmpty(raw)) return fallback;

        // Инвариантная культура обязательна: на русской локали запятая
        // и точка разбираются иначе, и параметры молча съезжают.
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            return value;

        Debug.LogWarning($"HeadlessTrial: не разобрал {name}={raw}, беру {fallback}.");
        return fallback;
    }
}
