using UnityEngine;

// Компонент, реализующий вязкое трение в суставе через прямые моменты.
// Формула: M = -K * ω, где ω — относительная угловая скорость в радианах/с.
// Момент прикладывается к обоим телам равными и противоположными значениями,
// что соответствует третьему закону Ньютона и сохраняет суммарный момент системы.
[RequireComponent(typeof(HingeJoint2D))]
public class JointFriction : MonoBehaviour
{
    [Tooltip("Коэффициент вязкого трения (K). Чем больше, тем сильнее сопротивление.")]
    public float damping = 2f;

    [Tooltip("Максимальный момент трения. Ограничивает силу, чтобы не было разлёта при ударах.")]
    public float maxTorque = 15f;

    private HingeJoint2D joint;
    private Rigidbody2D rbSelf;

    void Awake()
    {
        joint = GetComponent<HingeJoint2D>();
        rbSelf = GetComponent<Rigidbody2D>();

        if (joint == null || rbSelf == null)
        {
            Debug.LogError("JointFriction должен быть на том же объекте, что и HingeJoint2D и Rigidbody2D.");
        }
    }

    void FixedUpdate()
    {
        if (joint == null || joint.connectedBody == null || rbSelf == null) return;

        // Относительная скорость: Unity хранит angularVelocity в градусах/с.
        float relativeAngularVelocityRad =
            (rbSelf.angularVelocity - joint.connectedBody.angularVelocity) * Mathf.Deg2Rad;

        // Пара τ, −τ даёт относительное ускорение τ·(1/I₁ + 1/I₂).
        // Кинематические и статические тела имеют inertia = 0 — их в сумму не берём.
        float invInertia = 0f;
        if (rbSelf.inertia > 0f) invInertia += 1f / rbSelf.inertia;
        float connectedInertia = joint.connectedBody.inertia;
        if (connectedInertia > 0f) invInertia += 1f / connectedInertia;
        if (invInertia <= 0f) return;

        float torque = -damping * relativeAngularVelocityRad;

        // Явный демпфер устойчив только при K·Δt/I < 2. У шеи это превышено
        // в 57 раз, у локтя в 4 — потолок maxTorque лишь маскировал расходимость,
        // превращая её в предельный цикл. τ_max гасит относительную скорость
        // ровно за шаг и не даёт перелететь через нуль.
        float stopTorque = Mathf.Abs(relativeAngularVelocityRad) / (invInertia * Time.fixedDeltaTime);
        torque = Mathf.Clamp(torque, -stopTorque, stopTorque);
        torque = Mathf.Clamp(torque, -maxTorque, maxTorque);

        rbSelf.AddTorque(torque, ForceMode2D.Force);
        joint.connectedBody.AddTorque(-torque, ForceMode2D.Force);
    }
}
