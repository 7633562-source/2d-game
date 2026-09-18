using System.Collections.Generic;
using UnityEngine;

// Picture shape only. The collider is always a rectangle of the same size.
public enum HumanVisualShape
{
    Box,
    Capsule,
    Ellipse
}

// Human segment component.
// Holds physics and visual parts, size, mass, and draw order.
// Adds dark markers at joint attachment points.
public class HumanSegment : MonoBehaviour
{
    public Rigidbody2D rb;
    public SpriteRenderer sprite;
    public BoxCollider2D collider;
    public HingeJoint2D joint;

    public float mass;
    public Vector2 size;
    public int sortingOrder;

    // Joint marker diameter in units. Set before ConnectTo.
    public float jointMarkerDiameter = 0.03f;

    // The sprite lives on a child object; Rigidbody2D and the collider stay
    // on the segment itself. That is why the picture can shift sideways without
    // touching physics: X in the sagittal model is forward-back, and moving
    // the real body would put the human in a permanent lunge.
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
        string key = albedoKey;
        Color tint = color;
        ArtLibrary.BindPicture(segmentName, transform, ref key, ref tint);
        // Flat color square on the collider, same as Human layout boxes.
        _ = visualShape;
        AssignBodySprite(size, tint);
        sprite.sortingOrder = sortingOrder;
        SpriteLighting.ApplyLit(sprite);

        rb = gameObject.AddComponent<Rigidbody2D>();
        rb.mass = mass;
        // Continuous catches tunneling, but it is expensive every step.
        // The foot hits the ground at whole-body speed; the other segments
        // are held by joints, and at 200 Hz Discrete does not punch them through support.
        rb.collisionDetectionMode = segmentName.Contains("Foot")
            || segmentName.Contains("Paw")
            || segmentName.Contains("Toe")
            ? CollisionDetectionMode2D.Continuous
            : CollisionDetectionMode2D.Discrete;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        // Stand: no Interpolate — the same trick as Bird. Play Mode
        // keeps Interpolate (frame ~60 Hz while physics is 200 Hz).
        rb.interpolation = HeadlessTrial.Active
            ? RigidbodyInterpolation2D.None
            : RigidbodyInterpolation2D.Interpolate;

        collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = size;
    }

    // Picture only: no Rigidbody2D and no collider. For a bird flock —
    // wings and feet must not add dozens of bodies per bird.
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
        string key = albedoKey;
        Color tint = color;
        ArtLibrary.BindPicture(segmentName, transform, ref key, ref tint);
        _ = visualShape;
        AssignBodySprite(size, tint);
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
        string key = albedoKey;
        Color tint = color;
        ArtLibrary.BindPicture(target.gameObject.name, target.transform, ref key, ref tint);
        _ = visualShape;
        target.sprite = HeadlessTrial.Active && !HeadlessTrial.VisualCapture
            ? GetPlaceholderSprite()
            : GetBodySprite(colliderSize);
        target.color = tint;
        target.sortingOrder = sortingOrder;
        target.drawMode = SpriteDrawMode.Simple;
        SpriteLighting.ApplyLit(target);
    }

    // Side shift of the picture. Physics does not see it: Rigidbody2D and collider
    // stay put; only the child Visual with markers moves.
    // Call before ConnectTo, or the marker sits on the physics point.
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

    // Connect to the parent segment.
    // Joint-attachment markers are created here as well.
    public void ConnectTo(HumanSegment parentSegment, Vector2 anchor, Vector2 connectedAnchor, float minAngle, float maxAngle)
    {
        joint = gameObject.AddComponent<HingeJoint2D>();
        joint.connectedBody = parentSegment.rb;
        // Unity's default limits are ≈ ±177°. Assigning while useLimits is
        // already true is ignored on some 2D builds, so the debugger still
        // sees the unused default arc. Pose is antagonist muscles, not the
        // hinge motor (no spring-on-angle on HingeJoint2D).
        joint.autoConfigureConnectedAnchor = false;
        joint.useMotor = false;
        joint.useLimits = false;
        joint.anchor = anchor;
        joint.connectedAnchor = connectedAnchor;
        JointAngleLimits2D limits = joint.limits;
        limits.min = minAngle;
        limits.max = maxAngle;
        joint.limits = limits;
        joint.useLimits = true;

        // Circles on both boxes: child's anchor and the parent's
        // connectedAnchor. In physics they are one world point; on
        // Visual they sit on each rectangle so the join is readable.
        // Stand skips markers: extra SpriteRenderer on every hinge.
        // Skeleton bars use the same gate and follow the hinges in LateUpdate.
        if (!HeadlessTrial.Active || HeadlessTrial.VisualCapture)
        {
            CreateJointMarker(anchor);
            if (parentSegment != null)
                parentSegment.CreateJointMarker(connectedAnchor);
            HumanSkeletonLines.EnsureOn(transform);
        }
    }

    // Dark circle at a joint local point. Parent is Visual, so the
    // marker rides the picture, not the Rigidbody2D.
    public void CreateJointMarker(Vector2 localPosition)
    {
        GameObject marker = new GameObject("JointMarker");
        marker.transform.SetParent(visual != null ? visual : transform, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localScale = Vector3.one * jointMarkerDiameter;

        SpriteRenderer markerRenderer = marker.AddComponent<SpriteRenderer>();
        markerRenderer.sprite = GetCircleSprite();
        markerRenderer.color = new Color(0.08f, 0.07f, 0.06f, 1f);
        // On this segment, not above the whole body: otherwise far joints
        // draw in front of the near arm.
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
        tex.Apply(false, false);
        _placeholderSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8);
        return _placeholderSprite;
    }

    private void AssignBodySprite(Vector2 draw, Color color)
    {
        sprite.sprite = HeadlessTrial.Active && !HeadlessTrial.VisualCapture
            ? GetPlaceholderSprite()
            : GetBodySprite(draw);
        sprite.color = color;
        sprite.drawMode = SpriteDrawMode.Simple;
    }

    private static Sprite GetBodySprite(Vector2 size)
    {
        string key = $"{size.x:F4}x{size.y:F4}";
        if (BodySprites.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Sprite created = CreateBodySprite(size);
        BodySprites[key] = created;
        return created;
    }

    // White rectangle the size of the collider. Tint is sprite.color —
    // same path as Human layout boxes. No PNG, no edge shade.
    private static Sprite CreateBodySprite(Vector2 size)
    {
        const float pixelsPerMeter = 256f;
        const int maxTex = 16384;
        int w = Mathf.Max(1, Mathf.RoundToInt(size.x * pixelsPerMeter));
        int h = Mathf.Max(1, Mathf.RoundToInt(size.y * pixelsPerMeter));
        float scale = 1f;
        if (w > 512 || h > 512)
            scale = 512f / Mathf.Max(w, h);
        float minSide = Mathf.Min(w, h) * scale;
        if (minSide < 64f)
            scale *= 64f / Mathf.Max(1f, minSide);
        // A long thin box (laundry line) with the 64 px min-side boost
        // exceeds Unity's 16384 texture cap and throws in Play.
        w = Mathf.Clamp(Mathf.RoundToInt(w * scale), 8, maxTex);
        h = Mathf.Clamp(Mathf.RoundToInt(h * scale), 8, maxTex);
        float ppu = w / size.x;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
    }

    // Circle sprite for joint markers
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
