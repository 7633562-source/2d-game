using UnityEngine;

public class GroundBuilder : MonoBehaviour
{
    // ─── ПАРАМЕТРЫ ЗЕМЛИ ───
    [Header("Параметры земли")]
    public Vector2 groundSize = new Vector2(20f, 2f);    // ширина и высота земли
    public Vector2 groundPosition = new Vector2(0f, -3f); // позиция центра земли
    public Color groundColor = new Color(0.3f, 0.6f, 0.3f); // цвет (зелёный)

    // ─── ПУБЛИЧНЫЙ МЕТОД ДЛЯ ВЫЗОВА ИЗ GameProcess ───
    public void BuildGround()
    {
        // Создаём пустой объект для земли
        GameObject ground = new GameObject("Ground");

        // Ставим его в нужную позицию
        ground.transform.position = groundPosition;

        // Добавляем спрайт-рендерер, чтобы землю было видно
        SpriteRenderer sr = ground.AddComponent<SpriteRenderer>();

        // Создаём спрайт 1x1 (белый квадрат) — мы его растянем и покрасим
        sr.sprite = CreateSquareSprite();

        // Красим в заданный цвет
        sr.color = groundColor;

        // Масштабируем, чтобы квадрат стал прямоугольником нужного размера
        ground.transform.localScale = new Vector3(groundSize.x, groundSize.y, 1f);

        // Добавляем BoxCollider2D, чтобы персонаж мог стоять на земле
        BoxCollider2D col = ground.AddComponent<BoxCollider2D>();
        // Размер коллайдера оставляем 1x1, потому что масштаб объекта сам изменит его физический размер
        col.size = Vector2.one;
    }

    // ─── СОЗДАНИЕ ПРОСТОГО СПРАЙТА 1x1 ───
    Sprite CreateSquareSprite()
    {
        // Создаём текстуру 1x1 пиксель
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white); // заливаем белым
        tex.Apply();

        // Превращаем текстуру в спрайт с центром в (0.5, 0.5)
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}