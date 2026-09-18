using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Global logger that writes the chosen humans' states into one text file.
// The file is split into blocks: each human has its own header, then a parameter
// line, then the table header and data rows.
// Save path is hard-coded: "E:\Games\Unity3D LOGS".
public class GlobalHumanLogger : MonoBehaviour
{
    private const string DEFAULT_LOG_FOLDER = @"E:\Games\Unity3D LOGS";

    [Header("Logging settings")]
    public string fileName = "human_log.txt";
    public string logFolderPath = "";
    public float logIntervalSeconds = 0.033f;
    public bool logToConsole = false;

    private List<Human> humansToLog = new List<Human>();
    private List<int> humanNumbers = new List<int>();
    private Dictionary<int, List<string>> logBuffers = new Dictionary<int, List<string>>();
    // Find only in BuildCache (SetHumansToLog). LogHumanState reads the fields.
    private List<HumanLogCache> humanCaches = new List<HumanLogCache>();

    private float nextLogTime = 0f;
    private bool isInitialized = false;

    private class HumanLogCache
    {
        public Human human;
        public int number;
        public bool hasTorso;
        public Rigidbody2D torsoRb;
        public HingeJoint2D hipJoint;
        public HingeJoint2D kneeJoint;
        public HingeJoint2D ankleJoint;
        public HingeJoint2D neckJoint;
        public HingeJoint2D headJoint;
        public Muscle hipFlexor;
        public Muscle hipExtensor;
        public Muscle kneeFlexor;
        public Muscle kneeExtensor;
        public Muscle ankleFlexor;
        public Muscle ankleExtensor;
        public Muscle neckFlexor;
        public Muscle neckExtensor;
        public Muscle headFlexor;
        public Muscle headExtensor;
    }

    public void SetHumansToLog(Human[] humans, int[] numbers)
    {
        humansToLog.Clear();
        humanNumbers.Clear();
        logBuffers.Clear();
        humanCaches.Clear();

        for (int i = 0; i < humans.Length; i++)
        {
            humansToLog.Add(humans[i]);
            humanNumbers.Add(numbers[i]);
            logBuffers[numbers[i]] = new List<string>();
            humanCaches.Add(BuildCache(humans[i], numbers[i]));
        }
        isInitialized = true;
    }

    void Start()
    {
        nextLogTime = Time.time + logIntervalSeconds;
        Debug.Log($"Global logger initialized. File: {Path.Combine(DEFAULT_LOG_FOLDER, fileName)}");
    }

    void FixedUpdate()
    {
        if (!isInitialized) return;

        if (Time.time >= nextLogTime)
        {
            for (int i = 0; i < humanCaches.Count; i++)
            {
                LogHumanState(humanCaches[i]);
            }
            nextLogTime += logIntervalSeconds;
        }
    }

    private HumanLogCache BuildCache(Human human, int number)
    {
        var cache = new HumanLogCache { human = human, number = number };
        if (human == null) return cache;

        Transform torsoTransform = human.transform.Find("Torso");
        cache.hasTorso = torsoTransform != null;
        cache.torsoRb = torsoTransform != null ? torsoTransform.GetComponent<Rigidbody2D>() : null;
        cache.hipJoint = GetJoint(human, "LeftLegThigh");
        cache.kneeJoint = GetJoint(human, "LeftLegShin");
        cache.ankleJoint = GetJoint(human, "LeftLegFoot");
        cache.neckJoint = GetJoint(human, "Neck");
        cache.headJoint = GetJoint(human, "Head");
        cache.hipFlexor = GetMuscle(human, "LeftLegThigh", 1f);
        cache.hipExtensor = GetMuscle(human, "LeftLegThigh", -1f);
        cache.kneeFlexor = GetMuscle(human, "LeftLegShin", 1f);
        cache.kneeExtensor = GetMuscle(human, "LeftLegShin", -1f);
        cache.ankleFlexor = GetMuscle(human, "LeftLegFoot", 1f);
        cache.ankleExtensor = GetMuscle(human, "LeftLegFoot", -1f);
        cache.neckFlexor = GetMuscle(human, "Neck", 1f);
        cache.neckExtensor = GetMuscle(human, "Neck", -1f);
        cache.headFlexor = GetMuscle(human, "Head", 1f);
        cache.headExtensor = GetMuscle(human, "Head", -1f);
        return cache;
    }

    private void LogHumanState(HumanLogCache cache)
    {
        if (cache == null || cache.human == null || !cache.hasTorso) return;

        Rigidbody2D torsoRb = cache.torsoRb;
        HingeJoint2D hipJoint = cache.hipJoint;
        HingeJoint2D kneeJoint = cache.kneeJoint;
        HingeJoint2D ankleJoint = cache.ankleJoint;
        HingeJoint2D neckJoint = cache.neckJoint;
        HingeJoint2D headJoint = cache.headJoint;
        Muscle hipFlexor = cache.hipFlexor;
        Muscle hipExtensor = cache.hipExtensor;
        Muscle kneeFlexor = cache.kneeFlexor;
        Muscle kneeExtensor = cache.kneeExtensor;
        Muscle ankleFlexor = cache.ankleFlexor;
        Muscle ankleExtensor = cache.ankleExtensor;
        Muscle neckFlexor = cache.neckFlexor;
        Muscle neckExtensor = cache.neckExtensor;
        Muscle headFlexor = cache.headFlexor;
        Muscle headExtensor = cache.headExtensor;

        float time = Time.time;
        float torsoAngle = torsoRb != null ? Mathf.DeltaAngle(0f, torsoRb.rotation) : 0f;
        float torsoAngularVelocity = torsoRb != null ? torsoRb.angularVelocity : 0f;

        float hipAngle = hipJoint != null ? hipJoint.jointAngle : 0f;
        float kneeAngle = kneeJoint != null ? kneeJoint.jointAngle : 0f;
        float ankleAngle = ankleJoint != null ? ankleJoint.jointAngle : 0f;
        float neckAngle = neckJoint != null ? neckJoint.jointAngle : 0f;
        float headAngle = headJoint != null ? headJoint.jointAngle : 0f;

        float hipFlex = hipFlexor != null ? hipFlexor.activation : 0f;
        float hipExt = hipExtensor != null ? hipExtensor.activation : 0f;
        float kneeFlex = kneeFlexor != null ? kneeFlexor.activation : 0f;
        float kneeExt = kneeExtensor != null ? kneeExtensor.activation : 0f;
        float ankleFlex = ankleFlexor != null ? ankleFlexor.activation : 0f;
        float ankleExt = ankleExtensor != null ? ankleExtensor.activation : 0f;
        float neckFlex = neckFlexor != null ? neckFlexor.activation : 0f;
        float neckExt = neckExtensor != null ? neckExtensor.activation : 0f;
        float headFlex = headFlexor != null ? headFlexor.activation : 0f;
        float headExt = headExtensor != null ? headExtensor.activation : 0f;

        int number = cache.number;
        string line = $"{time:F3};{number};{torsoAngle:F2};{torsoAngularVelocity:F2};" +
                      $"{hipAngle:F2};{kneeAngle:F2};{ankleAngle:F2};{neckAngle:F2};{headAngle:F2};" +
                      $"{hipFlex:F3};{hipExt:F3};{kneeFlex:F3};{kneeExt:F3};" +
                      $"{ankleFlex:F3};{ankleExt:F3};{neckFlex:F3};{neckExt:F3};{headFlex:F3};{headExt:F3}";

        logBuffers[number].Add(line);
        if (logToConsole) Debug.Log(line);
    }

    private static HingeJoint2D GetJoint(Human human, string childName)
    {
        Transform t = human.transform.Find(childName);
        return t != null ? t.GetComponent<HingeJoint2D>() : null;
    }

    private static Muscle GetMuscle(Human human, string segmentName, float direction)
    {
        Transform seg = human.transform.Find(segmentName);
        if (seg == null) return null;
        Muscle[] muscles = seg.GetComponents<Muscle>();
        return System.Array.Find(muscles, m => Mathf.Approximately(m.direction, direction));
    }

    public void SaveToFile()
    {
        string directory = string.IsNullOrEmpty(logFolderPath) ? DEFAULT_LOG_FOLDER : logFolderPath;
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        string fullPath = Path.Combine(directory, fileName);
        using (StreamWriter writer = new StreamWriter(fullPath, false, Encoding.UTF8))
        {
            for (int i = 0; i < humansToLog.Count; i++)
            {
                Human human = humansToLog[i];
                int num = humanNumbers[i];
                List<string> lines = logBuffers.ContainsKey(num) ? logBuffers[num] : new List<string>();

                writer.WriteLine($"### Human {num} ###");

                // Write the human's parameters
                writer.WriteLine("Parameters:");
                writer.WriteLine($"TotalMass:{human.totalMass:F2};MuscleMultiplier:{human.muscleMultiplier:F3};FrictionMultiplier:{human.frictionMultiplier:F3}");
                writer.WriteLine($"HipTorque:{human.hipMuscleTorque:F2};KneeTorque:{human.kneeMuscleTorque:F2};AnkleExtTorque:{human.ankleExtensorTorque:F2};AnkleFlexTorque:{human.ankleFlexorTorque:F2};NeckTorque:{human.neckMuscleTorque:F2};ShoulderTorque:{human.shoulderMuscleTorque:F2};ElbowTorque:{human.elbowMuscleTorque:F2};WristTorque:{human.wristMuscleTorque:F2}");
                writer.WriteLine($"HipFriction:{human.hipFriction:F2};KneeFriction:{human.kneeFriction:F2};AnkleFriction:{human.ankleFriction:F2};NeckFriction:{human.neckFriction:F2};ShoulderFriction:{human.shoulderFriction:F2};ElbowFriction:{human.elbowFriction:F2};WristFriction:{human.wristFriction:F2}");
                writer.WriteLine();

                // Table header
                writer.WriteLine("Time;HumanNumber;TorsoAngle;TorsoAngularVelocity;HipAngle;KneeAngle;AnkleAngle;NeckAngle;HeadAngle;" +
                                 "HipFlexor;HipExtensor;KneeFlexor;KneeExtensor;AnkleFlexor;AnkleExtensor;NeckFlexor;NeckExtensor;HeadFlexor;HeadExtensor");
                foreach (string line in lines)
                {
                    writer.WriteLine(line);
                }
                writer.WriteLine();
            }
        }
        Debug.Log($"Logs saved to: {fullPath}");
    }

    void OnDestroy()
    {
        SaveToFile();
    }
}