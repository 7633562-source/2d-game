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

    void Awake()
    {
        Transform torsoTransform = transform.Find("Torso");
        if (torsoTransform != null)
        {
            torsoRb = torsoTransform.GetComponent<Rigidbody2D>();
        }
        else
        {
            Debug.LogError("VestibularSystem: Торс не найден!");
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

    // Вычисляет ошибку равновесия как комбинацию наклона и угловой скорости.
    // Коэффициенты настраиваются извне.
    public float GetBalanceError(float tiltWeight = 1f, float angularVelocityWeight = 0.1f)
    {
        return tiltWeight * GetBodyTilt() + angularVelocityWeight * GetBodyAngularVelocity();
    }
}