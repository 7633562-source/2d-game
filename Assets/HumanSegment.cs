using System.Collections.Generic;
using UnityEngine;

// Форма только картинки. Коллайдер всегда прямоугольник того же size.
public enum HumanVisualShape
{
    Box,
    Capsule,
    Ellipse
}

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

    // Диаметр маркера сустава в юнитах. Задаётся до ConnectTo.
    public float jointMarkerDiameter = 0.03f;

    // Спрайт живёт на отдельном дочернем объекте, а Rigidbody2D и коллайдер —
    // на самом сегменте. Только поэтому картинку можно двигать вбок, не трогая
    // физику: ось X в сагиттальной модели означает «вперёд-назад», и сдвиг
    // настоящего тела поставил бы человека в вечный выпад.
    public Transform visual;
    public float visualOffsetX;
    public float visualOffsetY;

    private static Sprite _circleSprite;
    private static readonly Dictionary<string, Sprite> BodySprites = new Dictionary<string, Sprite>();

    public void Initialize(
        string segmentName,
        Vector2 segmentSize,
        float segmentMass,
        Color color,
        Transform parent,
        int sortingOrder,
        HumanVisualShape visualShape = HumanVisualShape.Capsule,
        string albedoKey = null)
    {
        size = segmentSize;
        mass = segmentMass;
        this.sortingOrder = sortingOrder;

        gameObject.name = segmentName;
        transform.SetParent(parent, false);
        transform.localScale = Vector3.one;

        GameObject visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(transform, false);
        visual = visualObject.transform;

        sprite = visualObject.AddComponent<SpriteRenderer>();
        // Box = exact collider fill (human debug colors). Capsule/ellipse
        // grow past the box for hinge overlap. Painted PNG uses collider size.
        Vector2 draw = visualShape == HumanVisualShape.Box
            ? size
            : PictureSize(size, albedoKey);
        AssignBodySprite(draw, visualShape, albedoKey, color);
        sprite.sortingOrder = sortingOrder;
        SpriteLighting.ApplyLit(sprite);

        rb = gameObject.AddComponent<Rigidbody2D>();
        rb.mass = mass;
        // Continuous ловит тоннелирование, но на каждом шаге дорого.
        // Стопа бьёт в землю на скорости всего тела; остальные сегменты
        // держат суставы, и при 200 Гц Discrete их не проносит сквозь опору.
        rb.collisionDetectionMode = segmentName.Contains("Foot") || segmentName.Contains("Paw")
            ? CollisionDetectionMode2D.Continuous
            : CollisionDetectionMode2D.Discrete;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        // Стенд: без Interpolate — тот же приём, что у Bird. Play Mode
        // оставляет Interpolate (кадр ~60 Гц при физике 200 Гц).
        rb.interpolation = HeadlessTrial.Active
            ? RigidbodyInterpolation2D.None
            : RigidbodyInterpolation2D.Interpolate;

        collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = size;
    }

    // Только картинка: без Rigidbody2D и коллайдера. Для стаи птиц —
    // крылья и лапы не должны давать десятки тел на одну особь.
    public void InitializeVisualOnly(
        string segmentName,
        Vector2 segmentSize,
        Color color,
        Transform parent,
        int sortingOrder,
        HumanVisualShape visualShape = HumanVisualShape.Capsule,
        string albedoKey = null)
    {
        size = segmentSize;
        mass = 0f;
        this.sortingOrder = sortingOrder;
        gameObject.name = segmentName;
        transform.SetParent(parent, false);
        transform.localScale = Vector3.one;

        GameObject visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(transform, false);
        visual = visualObject.transform;

        sprite = visualObject.AddComponent<SpriteRenderer>();
        Vector2 draw = visualShape == HumanVisualShape.Box
            ? size
            : PictureSize(size, albedoKey);
        AssignBodySprite(draw, visualShape, albedoKey, color);
        sprite.sortingOrder = sortingOrder;
        SpriteLighting.ApplyLit(sprite);
    }

    // Picture on an existing renderer. Tree leaves do not need a
    // HumanSegment or a Visual child — only the sprite.
    public static void ApplyPicture(
        SpriteRenderer target,
        Vector2 colliderSize,
        HumanVisualShape visualShape,
        string albedoKey,
        Color color,
        int sortingOrder)
    {
        if (target == null)
            return;
        Vector2 draw = PictureSize(colliderSize, albedoKey);
        target.sprite = HeadlessTrial.Active
            ? GetPlaceholderSprite()
            : GetBodySprite(draw, visualShape, albedoKey);
        target.color = color;
        target.sortingOrder = sortingOrder;
        if (!HeadlessTrial.Active && ArtLibrary.IsPaintedPart(albedoKey))
        {
            target.drawMode = SpriteDrawMode.Sliced;
            target.size = draw;
        }
        else
            target.drawMode = SpriteDrawMode.Simple;
        SpriteLighting.ApplyLit(target);
    }

    // Сдвиг картинки вбок. Физика его не видит: Rigidbody2D и коллайдер
    // остаются на месте, двигается только дочерний Visual с маркерами.
    // Вызывать до ConnectTo, иначе маркер встанет по физической точке.
    public void SetVisualOffset(float offsetX)
    {
        visualOffsetX = offsetX;
        if (visual != null)
            visual.localPosition = new Vector3(offsetX, visualOffsetY, 0f);
    }

#if UNITY_EDITOR
    // Unity's BoxCollider2D gizmo sits on the body. A sagittal pair shares
    // that box, and only Visual is offset, so the far sprite looks like it
    // has no collider. This wire is the picture, not a second body.
    private void OnDrawGizmos()
    {
        if (collider == null || !collider.enabled || visual == null)
            return;
        if (Mathf.Abs(visualOffsetX) < 1e-4f && Mathf.Abs(visualOffsetY) < 1e-4f)
            return;

        Color prevColor = Gizmos.color;
        Matrix4x4 prevMatrix = Gizmos.matrix;
        Gizmos.color = new Color(0.25f, 0.78f, 0.88f, 0.75f);
        Gizmos.matrix = visual.localToWorldMatrix;
        Gizmos.DrawWireCube(collider.offset, collider.size);
        Gizmos.matrix = prevMatrix;
        Gizmos.color = prevColor;
    }
#endif

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

        // Один маркер на сустав, на дочернем сегменте. Второй, на родителе в
        // connectedAnchor, рисовал ту же самую точку: anchor и connectedAnchor
        // совпадают в мире, пока сустав цел. При разных визуальных сдвигах
        // родителя и ребёнка он бы ещё и разъезжался, изображая разрыв.
        // Стенд не рисует маркеры: лишние SpriteRenderer на 15 суставах.
        if (!HeadlessTrial.Active)
            CreateJointMarker(anchor);
    }

    // Тёмный кружок в точке крепления. Родитель — Visual, поэтому маркер
    // едет вместе с нарисованным сегментом, а не с физическим телом.
    private void CreateJointMarker(Vector2 localPosition)
    {
        GameObject marker = new GameObject("JointMarker");
        marker.transform.SetParent(visual != null ? visual : transform, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localScale = Vector3.one * jointMarkerDiameter;

        SpriteRenderer markerRenderer = marker.AddComponent<SpriteRenderer>();
        markerRenderer.sprite = GetCircleSprite();
        markerRenderer.color = new Color(0.08f, 0.07f, 0.06f, 1f);
        // На своём сегменте, не поверх всего тела: иначе дальние суставы
        // рисуются перед ближней рукой.
        markerRenderer.sortingOrder = sortingOrder + 1;
        SpriteLighting.ApplyLit(markerRenderer);
    }

    private static Sprite _placeholderSprite;

    private static Sprite GetPlaceholderSprite()
    {
        if (_placeholderSprite != null)
            return _placeholderSprite;
        Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        Color[] fill = new Color[64];
        for (int i = 0; i < fill.Length; i++)
            fill[i] = Color.white;
        tex.SetPixels(fill);
        tex.Apply(false, true);
        _placeholderSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8);
        return _placeholderSprite;
    }

    private void AssignBodySprite(Vector2 draw, HumanVisualShape visualShape, string albedoKey, Color color)
    {
        sprite.sprite = HeadlessTrial.Active
            ? GetPlaceholderSprite()
            : GetBodySprite(draw, visualShape, albedoKey);
        sprite.color = color;
        if (!HeadlessTrial.Active && ArtLibrary.IsPaintedPart(albedoKey))
        {
            sprite.drawMode = SpriteDrawMode.Sliced;
            sprite.size = draw;
        }
        else
            sprite.drawMode = SpriteDrawMode.Simple;
    }

    private static Sprite GetBodySprite(Vector2 size, HumanVisualShape shape, string albedoKey)
    {
        string albedo = string.IsNullOrEmpty(albedoKey) ? "none" : albedoKey;
        string key = $"{shape}:{size.x:F4}x{size.y:F4}:{albedo}";
        if (BodySprites.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Sprite created = ArtLibrary.IsPaintedPart(albedoKey)
            ? CreatePartSprite(size, albedoKey) ?? CreateBodySprite(size, shape, albedoKey)
            : CreateBodySprite(size, shape, albedoKey);
        BodySprites[key] = created;
        return created;
    }

    // Full PNG, including empty pixels, mapped onto the collider box.
    // Do not crop alpha: that threw away the layout and Sliced then
    // stretched the leftover silhouette into the whole bone.
    private static Sprite CreatePartSprite(Vector2 size, string key)
    {
        Texture2D src = Resources.Load<Texture2D>("Art/" + key);
        if (src == null)
            return null;
        Rect rect = new Rect(0f, 0f, src.width, src.height);
        float ppu = rect.width / Mathf.Max(0.01f, size.x);
        return Sprite.Create(
            src,
            rect,
            new Vector2(0.5f, 0.5f),
            ppu,
            0,
            SpriteMeshType.FullRect);
    }

    // Painted art is a transparent rectangle equal to the box collider.
    // Generated capsules still grow so neighbours overlap at the hinge.
    private static Vector2 PictureSize(Vector2 colliderSize, string albedoKey)
    {
        if (ArtLibrary.IsPaintedPart(albedoKey))
            return colliderSize;
        return VisualSize(colliderSize);
    }

    private static Vector2 VisualSize(Vector2 colliderSize)
    {
        float along = colliderSize.x >= colliderSize.y ? 1.18f : 1.26f;
        float across = colliderSize.x >= colliderSize.y ? 1.16f : 1.18f;
        if (colliderSize.x >= colliderSize.y)
            return new Vector2(colliderSize.x * along, colliderSize.y * across);
        return new Vector2(colliderSize.x * across, colliderSize.y * along);
    }

    // Текстура нарисованного размера. Коллайдер по-прежнему box:
    // скругление только на пикселях. Альбедо впекается в капсулу: UV в метрах,
    // кромка та же ShadeEdge — без второй SpriteRenderer.
    private static Sprite CreateBodySprite(Vector2 size, HumanVisualShape shape, string albedoKey)
    {
        // ~256 px/м. Масштаб w и h только вместе: иначе Clamp по ширине
        // вздувает PPU, спрайт становится короче коллайдера, и в суставе
        // снова щель вместо нахлёста (рука 0.10 м уезжала с 0.42 м на 0.20).
        const float pixelsPerMeter = 256f;
        int w = Mathf.Max(1, Mathf.RoundToInt(size.x * pixelsPerMeter));
        int h = Mathf.Max(1, Mathf.RoundToInt(size.y * pixelsPerMeter));
        float scale = 1f;
        if (w > 512 || h > 512)
            scale = 512f / Mathf.Max(w, h);
        float minSide = Mathf.Min(w, h) * scale;
        if (minSide < 64f)
            scale *= 64f / Mathf.Max(1f, minSide);
        w = Mathf.Max(8, Mathf.RoundToInt(w * scale));
        h = Mathf.Max(8, Mathf.RoundToInt(h * scale));
        float ppu = w / size.x;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[w * h];
        if (shape == HumanVisualShape.Box)
        {
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
        }
        else
        {
        float cx = (w - 1) * 0.5f;
        float cy = (h - 1) * 0.5f;
        float rx = w * 0.5f - 0.5f;
        float ry = h * 0.5f - 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float px = x - cx;
                float py = y - cy;
                float dist = shape == HumanVisualShape.Ellipse
                    ? EllipseDistance(px, py, rx, ry)
                    : CapsuleDistance(px, py, rx, ry);
                Color edge = ShadeEdge(dist);
                if (edge.a <= 0.001f)
                {
                    pixels[y * w + x] = edge;
                    continue;
                }

                // Метры от нижнего левого края сегмента, чтобы повтор шёл
                // одинаково на бедре 0.43 м и кисти 0.19 м.
                float mx = (x + 0.5f) / w * size.x;
                float my = (y + 0.5f) / h * size.y;
                // Ткань читается как ткань: цвет одежды остаётся sprite.color.
                Color albedo = ArtLibrary.Sample(albedoKey, mx, my);
                Color mix = Color.Lerp(Color.white, albedo, 0.78f);
                pixels[y * w + x] = new Color(
                    edge.r * mix.r,
                    edge.g * mix.g,
                    edge.b * mix.b,
                    edge.a);
            }
        }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
    }

    // Signed distance: отрицательное внутри. Единица — пиксель.
    // Скруглённый прямоугольник на весь спрайт: полный stadium давал
    // полукруги на торцах и щели в суставе даже при нахлёсте.
    private static float CapsuleDistance(float px, float py, float rx, float ry)
    {
        float radius = Mathf.Min(rx, ry) * 0.42f;
        float qx = Mathf.Abs(px) - (rx - radius);
        float qy = Mathf.Abs(py) - (ry - radius);
        float ox = Mathf.Max(qx, 0f);
        float oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
    }

    private static float EllipseDistance(float px, float py, float rx, float ry)
    {
        float nx = px / Mathf.Max(0.001f, rx);
        float ny = py / Mathf.Max(0.001f, ry);
        float len = Mathf.Sqrt(nx * nx + ny * ny);
        if (len < 0.0001f)
            return -Mathf.Min(rx, ry);
        // Приближение края в пикселях: (len - 1) * радиус по лучу.
        float edge = Mathf.Sqrt((px * px + py * py) / (len * len));
        return (len - 1f) * edge;
    }

    private static Color ShadeEdge(float dist)
    {
        // Внутри непрозрачно: прежний Clamp(0.7 − dist) делал полупрозрачный
        // ободок как раз в суставе — отсюда щели между овалами.
        float alpha = dist < 0f ? 1f : Mathf.Clamp01(1f - dist);
        if (alpha <= 0.001f)
            return Color.clear;
        float outline = Mathf.Clamp01((dist + 1.4f) / 2.2f);
        float shade = Mathf.Lerp(1f, 0.82f, outline);
        return new Color(shade, shade, shade, alpha);
    }

    // Спрайт круга для маркеров суставов
    private static Sprite GetCircleSprite()
    {
        if (_circleSprite == null)
        {
            int texSize = 32;
            Texture2D tex = new Texture2D(texSize, texSize);
            tex.filterMode = FilterMode.Bilinear;
            float center = (texSize - 1) / 2f;
            float radius = texSize / 2f - 1f;
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float a = Mathf.Clamp01(0.7f - d);
                    tex.SetPixel(x, y, a > 0f ? new Color(1f, 1f, 1f, a) : Color.clear);
                }
            }
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), texSize);
        }
        return _circleSprite;
    }
}
