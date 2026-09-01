using UnityEngine;

// Компонент, представляющий мышцу, которая активно создаёт момент в суставе.
// Мышца работает поверх пассивного трения (JointFriction) и может
// сгибать или разгибать сустав в зависимости от своего направления.
public class Muscle : MonoBehaviour
{
    [Tooltip("Максимальный момент, который мышца может создать (при activation = 1).")]
    public float maxTorque = 50f;

    [Tooltip("Текущая активация мышцы от 0 до 1. Управляется внешним кодом.")]
    [Range(0f, 1f)]
    public float activation = 0f;

    [Tooltip("Направление действия мышцы: +1 = сгибает сустав, -1 = разгибает.")]
    public float direction = 1f;

    private HingeJoint2D joint;
    private Rigidbody2D rbSelf;

    void Awake()
    {
        joint = GetComponent<HingeJoint2D>();
        rbSelf = GetComponent<Rigidbody2D>();

        if (joint == null || rbSelf == null)
        {
            Debug.LogError("Muscle должен быть на том же объекте, что и HingeJoint2D и Rigidbody2D.");
        }
    }

    void FixedUpdate()
    {
        if (joint == null || joint.connectedBody == null) return;

        float torque = activation * maxTorque * direction;

        rbSelf.AddTorque(torque, ForceMode2D.Force);
        joint.connectedBody.AddTorque(-torque, ForceMode2D.Force);
    }
}