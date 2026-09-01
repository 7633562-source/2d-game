using UnityEngine;

// Компонент сегмента человека.
// Хранит физические и визуальные компоненты, размер, массу, порядок отрисовки.
// Добавляет тёмные маркеры в точках крепления суставов.
public class HumanSegment : MonoBehaviour
{
    public Rigidbody2D rb;
    public SpriteRenderer sprite;
    public BoxCollider2D collider;
    public HingeJoint2D joint;

    public float mass;
    public Vector2 size;
    public int sortingOrder;

    private static Sprite _squareSprite;
    private static Sprite _circleSprite;

    // Инициализация сегмента.
    // Цвет передаётся с учётом прозрачности (альфа 0.5).
    public void Initialize(string segmentName, Vector2 segmentSize, float segmentMass, Color color, Transform parent, int sortingOrder)
    {
        size = segmentSize;
        mass = segmentMass;
        this.sortingOrder = sortingOrder;

        gameObject.name = segmentName;
        transform.SetParent(parent, false);
        transform.localScale = Vector3.one;

        sprite = gameObject.AddComponent<SpriteRenderer>();
        sprite.sprite = GetSquareSprite();
        sprite.color = color;                     // цвет с прозрачностью
        sprite.drawMode = SpriteDrawMode.Sliced;
        sprite.size = size;
        sprite.sortingOrder = sortingOrder;

        rb = gameObject.AddComponent<Rigidbody2D>();
        rb.mass = mass;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = size;
    }

    // Соединение с родительским сегментом.
    // Здесь также создаются маркеры точек крепления.
    public void ConnectTo(HumanSegment parentSegment, Vector2 anchor, Vector2 connectedAnchor, float minAngle, float maxAngle)
    {
        joint = gameObject.AddComponent<HingeJoint2D>();
        joint.connectedBody = parentSegment.rb;
        joint.anchor = anchor;
        joint.connectedAnchor = connectedAnchor;
        joint.useLimits = true;
        JointAngleLimits2D limits = joint.limits;
        limits.min = minAngle;
        limits.max = maxAngle;
        joint.limits = limits;

        // Маркер на текущем сегменте (в anchor)
        CreateJointMarker(anchor, transform);
        // Маркер на родительском сегменте (в connectedAnchor)
        CreateJointMarker(connectedAnchor, parentSegment.transform);
    }

    // Создаёт маленький тёмный кружок в заданной локальной позиции.
    private void CreateJointMarker(Vector2 localPosition, Transform parent)
    {
        GameObject marker = new GameObject("JointMarker");
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localScale = Vector3.one * 0.1f;   // диаметр ~0.1 юнита

        SpriteRenderer markerRenderer = marker.AddComponent<SpriteRenderer>();
        markerRenderer.sprite = GetCircleSprite();
        markerRenderer.color = new Color(0.05f, 0.05f, 0.05f, 1f); // почти чёрный, непрозрачный
        markerRenderer.sortingOrder = 20;                  // поверх сегментов
    }

    // Спрайт 1x1 (белый квадрат с border для Sliced)
    private static Sprite GetSquareSprite()
    {
        if (_squareSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            Vector4 border = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
            _squareSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect, border);
        }
        return _squareSprite;
    }

    // Спрайт круга для маркеров суставов
    private static Sprite GetCircleSprite()
    {
        if (_circleSprite == null)
        {
            int texSize = 32;
            Texture2D tex = new Texture2D(texSize, texSize);
            float center = (texSize - 1) / 2f;
            float radius = texSize / 2f - 1f;
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    if (dx * dx + dy * dy <= radius * radius)
                        tex.SetPixel(x, y, Color.white);
                    else
                        tex.SetPixel(x, y, Color.clear);
                }
            }
            tex.Apply();
            // pixelsPerUnit = texSize => размер спрайта 1x1 юнит, маркер масштабируется до 0.1
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), texSize);
        }
        return _circleSprite;
    }
}