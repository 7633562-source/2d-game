using UnityEngine;
using UnityEngine.InputSystem;

// Единственный источник намерения, который читает клавиатуру. Висит только на
// том человеке, которым играют: остальные получают намерение от своего ИИ.
//
// Используется низкоуровневый доступ к Keyboard, а не ассет InputActions:
// в проекте нет ни префабов, ни настраиваемых вручную ассетов, вся сцена
// собирается кодом. Проект переключён на новый Input System
// (activeInputHandler = 1), поэтому старый UnityEngine.Input здесь бросил бы
// исключение.
//
// В безоконном прогоне Keyboard.current равен null, и компонент просто молчит,
// не мешая стенду выставлять намерение самому.
[RequireComponent(typeof(MotionIntent))]
public class PlayerInputSource : MonoBehaviour
{
    private MotionIntent intent;

    void Awake()
    {
        intent = GetComponent<MotionIntent>();
    }

    // Опрос идёт в Update, а не в FixedUpdate: состояние клавиш относится
    // к кадру отрисовки. Для удерживаемой клавиши разницы нет, но при 200 Гц
    // физики привычка читать ввод в FixedUpdate однажды выйдет боком на
    // событиях «нажато в этом кадре».
    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Человек смотрит вправо, поэтому +X — это «вперёд».
        float move = 0f;
        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) move += 1f;
        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) move -= 1f;
        intent.moveX = move;

        bool crouching = keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed;
        intent.crouch = crouching ? 1f : 0f;
    }
}
