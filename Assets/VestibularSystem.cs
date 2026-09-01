using UnityEngine;

// Вестибулярный аппарат человека.
// Читает ориентацию торса и таза в мире. Никаких сил не прикладывает.
// В будущем сюда добавятся шум, задержка и фильтрация сигналов.
public class VestibularSystem : MonoBehaviour
{
    [Header("Настройки шума (пока не используются)")]
    public float tiltNoiseDegrees = 0f;            // шум измерения наклона (градусы)
    public float angularVelocityNoiseDegrees = 0f; // шум измерения угловой скорости (град/с)

    private Rigidbody2D torsoRb;
    private Rigidbody2D pelvisRb;
    private Rigidbody2D neckRb;
    private Rigidbody2D headRb;

    void Awake()
    {
        Transform torsoTransform = transform.Find("Torso");
        if (torsoTransform == null)
            torsoTransform = transform.Find("Chest");
        if (torsoTransform != null)
        {
            torsoRb = torsoTransform.GetComponent<Rigidbody2D>();
        }
        else
        {
            Debug.LogError("VestibularSystem: Torso/Chest not found!");
        }

        Transform pelvisTransform = transform.Find("Pelvis");
        if (pelvisTransform != null)
        {
            pelvisRb = pelvisTransform.GetComponent<Rigidbody2D>();
        }
        else
        {
            Debug.LogError("VestibularSystem: Таз не найден!");
        }

        // Шея и голова не обязательны: тело собирается динамически.
        Transform neckTransform = transform.Find("Neck");
        if (neckTransform != null) neckRb = neckTransform.GetComponent<Rigidbody2D>();

        Transform headTransform = transform.Find("Head");
        if (headTransform != null) headRb = headTransform.GetComponent<Rigidbody2D>();
    }

    // Возвращает текущий наклон торса относительно вертикали (градусы)
    public float GetBodyTilt()
    {
        if (torsoRb == null) return 0f;
        float tilt = Mathf.DeltaAngle(0f, torsoRb.rotation);
        if (tiltNoiseDegrees > 0f)
            tilt += Random.Range(-tiltNoiseDegrees, tiltNoiseDegrees);
        return tilt;
    }

    // Возвращает угловую скорость торса (градусы в секунду)
    public float GetBodyAngularVelocity()
    {
        if (torsoRb == null) return 0f;
        float angVel = torsoRb.angularVelocity;
        if (angularVelocityNoiseDegrees > 0f)
            angVel += Random.Range(-angularVelocityNoiseDegrees, angularVelocityNoiseDegrees);
        return angVel;
    }

    // Наклон таза к мировой вертикали (градусы). Та же ось, что у торса:
    // иначе рабочая точка поясницы невидима, хотя именно она упирает сустав.
    public float GetPelvisTilt()
    {
        if (pelvisRb == null) return 0f;
        float tilt = Mathf.DeltaAngle(0f, pelvisRb.rotation);
        if (tiltNoiseDegrees > 0f)
            tilt += Random.Range(-tiltNoiseDegrees, tiltNoiseDegrees);
        return tilt;
    }

    public float GetPelvisAngularVelocity()
    {
        if (pelvisRb == null) return 0f;
        float angVel = pelvisRb.angularVelocity;
        if (angularVelocityNoiseDegrees > 0f)
            angVel += Random.Range(-angularVelocityNoiseDegrees, angularVelocityNoiseDegrees);
        return angVel;
    }

    // Наклон шеи и головы к мировой вертикали. У человека взгляд стабилизирует
    // именно вестибулярный рефлекс, а не угол к груди: голова обязана знать,
    // где верх, иначе она послушно едет вниз вместе с заваливающимся торсом.
    public float GetNeckTilt()
    {
        if (neckRb == null) return 0f;
        float tilt = Mathf.DeltaAngle(0f, neckRb.rotation);
        if (tiltNoiseDegrees > 0f)
            tilt += Random.Range(-tiltNoiseDegrees, tiltNoiseDegrees);
        return tilt;
    }

    public float GetNeckAngularVelocity()
    {
        if (neckRb == null) return 0f;
        float angVel = neckRb.angularVelocity;
        if (angularVelocityNoiseDegrees > 0f)
            angVel += Random.Range(-angularVelocityNoiseDegrees, angularVelocityNoiseDegrees);
        return angVel;
    }

    public float GetHeadTilt()
    {
        if (headRb == null) return 0f;
        float tilt = Mathf.DeltaAngle(0f, headRb.rotation);
        if (tiltNoiseDegrees > 0f)
            tilt += Random.Range(-tiltNoiseDegrees, tiltNoiseDegrees);
        return tilt;
    }

    public float GetHeadAngularVelocity()
    {
        if (headRb == null) return 0f;
        float angVel = headRb.angularVelocity;
        if (angularVelocityNoiseDegrees > 0f)
            angVel += Random.Range(-angularVelocityNoiseDegrees, angularVelocityNoiseDegrees);
        return angVel;
    }

    // Вычисляет ошибку равновесия как комбинацию наклона и угловой скорости.
    // Коэффициенты настраиваются извне.
    public float GetBalanceError(float tiltWeight = 1f, float angularVelocityWeight = 0.1f)
    {
        return tiltWeight * GetBodyTilt() + angularVelocityWeight * GetBodyAngularVelocity();
    }
}