using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Глобальный логгер, записывающий состояния выбранных людей в один текстовый файл.
// Файл разбит на блоки: для каждого человека свой заголовок, затем строка с параметрами,
// затем заголовок таблицы и строки данных.
// Путь сохранения: жёстко задан "E:\Games\Unity3D LOGS".
public class GlobalHumanLogger : MonoBehaviour
{
    private const string DEFAULT_LOG_FOLDER = @"E:\Games\Unity3D LOGS";

    [Header("Настройки логирования")]
    public string fileName = "human_log.txt";
    public string logFolderPath = "";
    public float logIntervalSeconds = 0.033f;
    public bool logToConsole = false;

    private List<Human> humansToLog = new List<Human>();
    private List<int> humanNumbers = new List<int>();
    private Dictionary<int, List<string>> logBuffers = new Dictionary<int, List<string>>();

    private float nextLogTime = 0f;
    private bool isInitialized = false;

    public void SetHumansToLog(Human[] humans, int[] numbers)
    {
        humansToLog.Clear();
        humanNumbers.Clear();
        logBuffers.Clear();

        for (int i = 0; i < humans.Length; i++)
        {
            humansToLog.Add(humans[i]);
            humanNumbers.Add(numbers[i]);
            logBuffers[numbers[i]] = new List<string>();
        }
        isInitialized = true;
    }

    void Start()
    {
        nextLogTime = Time.time + logIntervalSeconds;
        Debug.Log($"Глобальный логгер инициализирован. Файл: {Path.Combine(DEFAULT_LOG_FOLDER, fileName)}");
    }

    void FixedUpdate()
    {
        if (!isInitialized) return;

        if (Time.time >= nextLogTime)
        {
            for (int i = 0; i < humansToLog.Count; i++)
            {
                LogHumanState(humansToLog[i], humanNumbers[i]);
            }
            nextLogTime += logIntervalSeconds;
        }
    }

    private void LogHumanState(Human human, int number)
    {
        Transform torsoTransform = human.transform.Find("Torso");
        if (torsoTransform == null) return;
        Rigidbody2D torsoRb = torsoTransform.GetComponent<Rigidbody2D>();

        HingeJoint2D hipJoint = GetJoint(human, "LeftLegThigh");
        HingeJoint2D kneeJoint = GetJoint(human, "LeftLegShin");
        HingeJoint2D ankleJoint = GetJoint(human, "LeftLegFoot");
        HingeJoint2D neckJoint = GetJoint(human, "Neck");
        HingeJoint2D headJoint = GetJoint(human, "Head");

        Muscle hipFlexor = GetMuscle(human, "LeftLegThigh", 1f);
        Muscle hipExtensor = GetMuscle(human, "LeftLegThigh", -1f);
        Muscle kneeFlexor = GetMuscle(human, "LeftLegShin", 1f);
        Muscle kneeExtensor = GetMuscle(human, "LeftLegShin", -1f);
        Muscle ankleFlexor = GetMuscle(human, "LeftLegFoot", 1f);
        Muscle ankleExtensor = GetMuscle(human, "LeftLegFoot", -1f);
        Muscle neckFlexor = GetMuscle(human, "Neck", 1f);
        Muscle neckExtensor = GetMuscle(human, "Neck", -1f);
        Muscle headFlexor = GetMuscle(human, "Head", 1f);
        Muscle headExtensor = GetMuscle(human, "Head", -1f);

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

        string line = $"{time:F3};{number};{torsoAngle:F2};{torsoAngularVelocity:F2};" +
                      $"{hipAngle:F2};{kneeAngle:F2};{ankleAngle:F2};{neckAngle:F2};{headAngle:F2};" +
                      $"{hipFlex:F3};{hipExt:F3};{kneeFlex:F3};{kneeExt:F3};" +
                      $"{ankleFlex:F3};{ankleExt:F3};{neckFlex:F3};{neckExt:F3};{headFlex:F3};{headExt:F3}";

        logBuffers[number].Add(line);
        if (logToConsole) Debug.Log(line);
    }

    private HingeJoint2D GetJoint(Human human, string childName)
    {
        Transform t = human.transform.Find(childName);
        return t != null ? t.GetComponent<HingeJoint2D>() : null;
    }

    private Muscle GetMuscle(Human human, string segmentName, float direction)
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

                // Записываем параметры человека
                writer.WriteLine("Parameters:");
                writer.WriteLine($"TotalMass:{human.totalMass:F2};MuscleMultiplier:{human.muscleMultiplier:F3};FrictionMultiplier:{human.frictionMultiplier:F3}");
                writer.WriteLine($"HipTorque:{human.hipMuscleTorque:F2};KneeTorque:{human.kneeMuscleTorque:F2};AnkleExtTorque:{human.ankleExtensorTorque:F2};AnkleFlexTorque:{human.ankleFlexorTorque:F2};NeckTorque:{human.neckMuscleTorque:F2};ShoulderTorque:{human.shoulderMuscleTorque:F2};ElbowTorque:{human.elbowMuscleTorque:F2};WristTorque:{human.wristMuscleTorque:F2}");
                writer.WriteLine($"HipFriction:{human.hipFriction:F2};KneeFriction:{human.kneeFriction:F2};AnkleFriction:{human.ankleFriction:F2};NeckFriction:{human.neckFriction:F2};ShoulderFriction:{human.shoulderFriction:F2};ElbowFriction:{human.elbowFriction:F2};WristFriction:{human.wristFriction:F2}");
                writer.WriteLine();

                // Заголовок таблицы
                writer.WriteLine("Time;HumanNumber;TorsoAngle;TorsoAngularVelocity;HipAngle;KneeAngle;AnkleAngle;NeckAngle;HeadAngle;" +
                                 "HipFlexor;HipExtensor;KneeFlexor;KneeExtensor;AnkleFlexor;AnkleExtensor;NeckFlexor;NeckExtensor;HeadFlexor;HeadExtensor");
                foreach (string line in lines)
                {
                    writer.WriteLine(line);
                }
                writer.WriteLine();
            }
        }
        Debug.Log($"Логи сохранены в: {fullPath}");
    }

    void OnDestroy()
    {
        SaveToFile();
    }
}