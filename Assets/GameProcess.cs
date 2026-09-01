using UnityEngine;
using System.Collections.Generic;

public class GameProcess : MonoBehaviour
{
    [Header("Настройки создания людей")]
    public int humanCount = 1;
    public Vector2 humanStartPosition = new Vector2(-36.75f, -0.82f); // стопы касаются земли
    public float spacing = 1.5f;

    [Header("Номера людей для логирования")]
    public int[] logHumanNumbers = new int[] { 1, 10, 20, 30, 40, 50 };

    private GroundBuilder groundBuilder;
    private List<Human> humans = new List<Human>();

    void Start()
    {
        // В headless-прогоне сцену строит HeadlessTrial (один человек),
        // иначе к нему добавились бы ещё 50 и результат стал бы бессмысленным.
        if (HeadlessTrial.Active) return;

        // 200 Гц: при 50 Гц регулятор не успевает и раскачивает тело.
        Time.fixedDeltaTime = 0.005f;

        if (humanCount < 1) humanCount = 1;
        if (logHumanNumbers == null || logHumanNumbers.Length == 0)
            logHumanNumbers = new int[] { 1 };

        float startX = -(humanCount - 1) * spacing / 2f;
        humanStartPosition = new Vector2(startX, -0.82f);

        groundBuilder = GetComponent<GroundBuilder>();
        if (groundBuilder == null)
        {
            Debug.LogError("GroundBuilder не найден на GameManager!");
            return;
        }

        groundBuilder.groundSize = new Vector2(400f, 2f);
        groundBuilder.groundPosition = new Vector2(0f, -3f);
        groundBuilder.BuildGround();

        CreateHumans();
        DisableCollisionsBetweenHumans();
        SetupGlobalLogger();

        Debug.Log($"Логгер настроен на {logHumanNumbers.Length} человек(а): [{string.Join(", ", logHumanNumbers)}]");
    }

    private void CreateHumans()
    {
        for (int i = 0; i < humanCount; i++)
        {
            int humanNumber = i + 1;

            float t = humanCount == 1 ? 0.5f : i / (float)(humanCount - 1);
            float muscleMultiplier = 0.8f + t * 0.4f;
            float frictionMultiplier = 0.5f + t * 0.5f;

            GameObject humanObject = new GameObject("Human_" + humanNumber);
            humanObject.transform.position = new Vector2(
                humanStartPosition.x + i * spacing,
                humanStartPosition.y
            );

            Human human = humanObject.AddComponent<Human>();

            human.hipMuscleTorque *= muscleMultiplier;
            human.kneeMuscleTorque *= muscleMultiplier;
            human.ankleExtensorTorque *= muscleMultiplier;
            human.ankleFlexorTorque *= muscleMultiplier;
            human.shoulderMuscleTorque *= muscleMultiplier;
            human.elbowMuscleTorque *= muscleMultiplier;
            human.wristMuscleTorque *= muscleMultiplier;
            human.neckMuscleTorque *= muscleMultiplier;
            human.lumbarMuscleTorque *= muscleMultiplier;

            human.hipFriction *= frictionMultiplier;
            human.kneeFriction *= frictionMultiplier;
            human.ankleFriction *= frictionMultiplier;
            human.shoulderFriction *= frictionMultiplier;
            human.elbowFriction *= frictionMultiplier;
            human.wristFriction *= frictionMultiplier;
            human.neckFriction *= frictionMultiplier;
            human.lumbarFriction *= frictionMultiplier;

            human.muscleMultiplier = muscleMultiplier;
            human.frictionMultiplier = frictionMultiplier;

            human.BuildHuman();
            human.AddNumberLabel(humanNumber);

            humans.Add(human);
        }
    }

    private void DisableCollisionsBetweenHumans()
    {
        for (int i = 0; i < humans.Count; i++)
        {
            Collider2D[] collidersA = humans[i].GetComponentsInChildren<Collider2D>();
            for (int j = i + 1; j < humans.Count; j++)
            {
                Collider2D[] collidersB = humans[j].GetComponentsInChildren<Collider2D>();
                foreach (var ca in collidersA)
                {
                    foreach (var cb in collidersB)
                    {
                        Physics2D.IgnoreCollision(ca, cb, true);
                    }
                }
            }
        }
    }

    private void SetupGlobalLogger()
    {
        GlobalHumanLogger logger = gameObject.AddComponent<GlobalHumanLogger>();
        logger.fileName = "human_log.txt";
        logger.logFolderPath = "";
        logger.logIntervalSeconds = 0.033f;

        List<Human> selectedHumans = new List<Human>();
        List<int> selectedNumbers = new List<int>();
        foreach (int num in logHumanNumbers)
        {
            if (num >= 1 && num <= humans.Count)
            {
                selectedHumans.Add(humans[num - 1]);
                selectedNumbers.Add(num);
            }
        }

        logger.SetHumansToLog(selectedHumans.ToArray(), selectedNumbers.ToArray());
    }
}