using System.Collections.Generic;
using UnityEngine;

// Форма только картинки. Коллайдер всегда прямоугольник того же size.
public enum HumanVisualShape
{
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
        // Картинка больше коллайдера: соседние капсулы нахлёстываются
        // в суставе. Rigidbody2D и BoxCollider2D остаются на size.
        // Стенд -nographics спрайт не видит: печь капсулу не из чего.
        AssignBodySprite(VisualSize(size, albedoKey), visualShape, albedoKey, color);
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
        AssignBodySprite(VisualSize(size, albedoKey), visualShape, albedoKey, color);
        sprite.sortingOrder = sortingOrder;
        SpriteLighting.ApplyLit(sprite);
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
            // Hand collider is 0.06 x 0.32; Sliced to that box turns a
            // square palm into sausages. Keep the PNG aspect; collider stays.
            sprite.size = PartDrawSize(draw, albedoKey);
            if (albedoKey == ArtLibrary.HumanHand)
            {
                visualOffsetY = (size.y - sprite.size.y) * 0.5f;
                if (visual != null)
                    visual.localPosition = new Vector3(visualOffsetX, visualOffsetY, 0f);
            }
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

    // Авторский сегмент: силуэт уже в PNG. PPU по ширине; высоту
    // обычно дожимает Sliced на VisualSize. Кисть — исключение:
    // рисуем в пропорции PNG, иначе пальцы становятся колбасой.
    private static Sprite CreatePartSprite(Vector2 size, string key)
    {
        Texture2D src = Resources.Load<Texture2D>("Art/" + key);
        if (src == null)
            return null;
        // Generated parts sit in a padded canvas. Sliced on the full
        // frame leaves sky between bones (dog chest ~38% opaque).
        Rect rect = OpaqueRect(src);
        Vector2 draw = PartDrawSize(size, key, rect);
        float ppu = rect.width / Mathf.Max(0.01f, draw.x);
        return Sprite.Create(
            src,
            rect,
            new Vector2(0.5f, 0.5f),
            ppu,
            0,
            SpriteMeshType.FullRect);
    }

    private static Vector2 PartDrawSize(Vector2 box, string key)
    {
        if (key != ArtLibrary.HumanHand)
            return box;
        Texture2D src = Resources.Load<Texture2D>("Art/" + key);
        if (src == null)
            return box;
        return PartDrawSize(box, key, OpaqueRect(src));
    }

    private static Vector2 PartDrawSize(Vector2 box, string key, Rect rect)
    {
        if (key != ArtLibrary.HumanHand || rect.width < 1f || rect.height < 1f)
            return box;
        float aspect = rect.width / rect.height;
        // Collider is 32 cm for reach; a painted hand is ~20 cm. Do not
        // scale the art up to the box — that is the stretch the player sees.
        const float handArtLength = 0.20f;
        float bone = box.y >= box.x ? box.y : box.x;
        float length = Mathf.Min(bone, handArtLength);
        if (box.y >= box.x)
            return new Vector2(length * aspect, length);
        return new Vector2(length, length / aspect);
    }

    private static Rect OpaqueRect(Texture2D src)
    {
        if (src == null || !src.isReadable)
            return new Rect(0f, 0f, src != null ? src.width : 1, src != null ? src.height : 1);

        Color32[] px = src.GetPixels32();
        int w = src.width;
        int h = src.height;
        int minX = w;
        int minY = h;
        int maxX = -1;
        int maxY = -1;
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a < 40)
                continue;
            int x = i % w;
            int y = i / w;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        if (maxX < minX)
            return new Rect(0f, 0f, w, h);

        minX = Mathf.Max(0, minX - 1);
        minY = Mathf.Max(0, minY - 1);
        maxX = Mathf.Min(w - 1, maxX + 1);
        maxY = Mathf.Min(h - 1, maxY + 1);
        return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // Sprite larger than the collider so neighbours overlap at the
    // joint. Painted art needs more along the bone: generated silhouettes
    // stop short of the canvas edge even after OpaqueRect.
    private static Vector2 VisualSize(Vector2 colliderSize, string albedoKey = null)
    {
        bool painted = ArtLibrary.IsPaintedPart(albedoKey);
        float along = painted ? 1.36f : (colliderSize.x >= colliderSize.y ? 1.18f : 1.26f);
        float across = painted ? 1.22f : (colliderSize.x >= colliderSize.y ? 1.16f : 1.18f);
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
