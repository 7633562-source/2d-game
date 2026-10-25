using UnityEngine;

// Picture-only stick figure on the Human. Connects hinge world points
// with thin black bars. No forces, no Human geometry, not in headless
// without -visual — same gate as HumanSegment joint markers.
[DefaultExecutionOrder(80)]
public class HumanSkeletonLines : MonoBehaviour
{
    [Header("Skeleton overlay")]
    public float lineWidth = 0.014f;
    public int sortingOrder = 110;

    Transform _root;
    SpriteRenderer[] _bones;
    HumanSegment _torso;
    HumanSegment _neck;
    HumanSegment _head;
    HumanSegment _leftUpper;
    HumanSegment _leftLower;
    HumanSegment _leftHand;
    HumanSegment _rightUpper;
    HumanSegment _rightLower;
    HumanSegment _rightHand;
    HumanSegment _leftThigh;
    HumanSegment _leftShin;
    HumanSegment _leftFoot;
    HumanSegment _leftToe;
    HumanSegment _rightThigh;
    HumanSegment _rightShin;
    HumanSegment _rightFoot;
    HumanSegment _rightToe;
    bool _bound;

    static Sprite _bar;

    public static void EnsureOn(Transform node)
    {
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        if (!HeadlessTrial.RigOverlay)
            return;
        Human human = node.GetComponentInParent<Human>();
        if (human == null)
            return;
        if (human.GetComponent<HumanSkeletonLines>() != null)
            return;
        human.gameObject.AddComponent<HumanSkeletonLines>();
    }

    void LateUpdate()
    {
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        if (!HeadlessTrial.RigOverlay)
            return;
        if (!_bound)
            Bind();
        if (!_bound)
            return;
        Tick();
    }

    HumanSegment FindSeg(string name)
    {
        Transform child = transform.Find(name);
        return child != null ? child.GetComponent<HumanSegment>() : null;
    }

    void Bind()
    {
        _torso = FindSeg("Torso");
        _neck = FindSeg("Neck");
        _head = FindSeg("Head");
        _leftUpper = FindSeg("LeftArmUpper");
        _leftLower = FindSeg("LeftArmLower");
        _leftHand = FindSeg("LeftArmHand");
        _rightUpper = FindSeg("RightArmUpper");
        _rightLower = FindSeg("RightArmLower");
        _rightHand = FindSeg("RightArmHand");
        _leftThigh = FindSeg("LeftLegThigh");
        _leftShin = FindSeg("LeftLegShin");
        _leftFoot = FindSeg("LeftLegFoot");
        _leftToe = FindSeg("LeftLegToe");
        _rightThigh = FindSeg("RightLegThigh");
        _rightShin = FindSeg("RightLegShin");
        _rightFoot = FindSeg("RightLegFoot");
        _rightToe = FindSeg("RightLegToe");

        // ConnectTo adds this mid-BuildHuman; wait until the last toes exist.
        if (_torso == null || _neck == null || _head == null
            || _leftHand == null || _rightHand == null
            || _leftToe == null || _rightToe == null)
            return;

        GameObject rootGo = new GameObject("Skeleton");
        _root = rootGo.transform;
        _root.SetParent(transform, false);

        _bones = new[]
        {
            MakeBone("PelvisBar"),
            MakeBone("SpinePelvisLumbar"),
            MakeBone("SpineLumbarNeck"),
            MakeBone("SpineNeckHead"),
            MakeBone("LeftArmUpper"),
            MakeBone("LeftArmLower"),
            MakeBone("RightArmUpper"),
            MakeBone("RightArmLower"),
            MakeBone("LeftLegThigh"),
            MakeBone("LeftLegShin"),
            MakeBone("LeftLegFoot"),
            MakeBone("RightLegThigh"),
            MakeBone("RightLegShin"),
            MakeBone("RightLegFoot")
        };
        _bound = true;
    }

    SpriteRenderer MakeBone(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(_root, false);
        SpriteRenderer sprite = go.AddComponent<SpriteRenderer>();
        sprite.sprite = BarSprite();
        sprite.color = Color.black;
        sprite.drawMode = SpriteDrawMode.Sliced;
        sprite.size = new Vector2(0.01f, lineWidth);
        sprite.sortingOrder = sortingOrder;
        SpriteLighting.ApplyUnlit(sprite);
        return sprite;
    }

    static Sprite BarSprite()
    {
        if (_bar != null)
            return _bar;
        Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        Color[] fill = new Color[64];
        for (int i = 0; i < fill.Length; i++)
            fill[i] = Color.white;
        tex.SetPixels(fill);
        tex.Apply(false, false);
        tex.filterMode = FilterMode.Point;
        _bar = Sprite.Create(
            tex,
            new Rect(0f, 0f, 8f, 8f),
            new Vector2(0.5f, 0.5f),
            8f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(1f, 1f, 1f, 1f));
        return _bar;
    }

    static Vector3 JointWorld(HumanSegment segment)
    {
        if (segment == null || segment.joint == null)
            return Vector3.zero;
        Vector3 point = segment.transform.TransformPoint(segment.joint.anchor);
        point.z = 0f;
        return point;
    }

    // Local box corner (nx/ny = ±1), including collider.offset.
    // (0, 0) is the box center. TransformPoint follows the rotated segment;
    // collider.bounds is AABB and would break a tilted foot.
    static Vector3 ColliderCorner(HumanSegment segment, float nx, float ny)
    {
        if (segment == null || segment.collider == null)
            return Vector3.zero;
        BoxCollider2D box = segment.collider;
        Vector2 local = box.offset + new Vector2(nx * box.size.x * 0.5f, ny * box.size.y * 0.5f);
        Vector3 point = segment.transform.TransformPoint(local);
        point.z = 0f;
        return point;
    }

    static Vector3 SoleToeFront(HumanSegment toe)
    {
        return ColliderCorner(toe, 1f, 0f);
    }

    void Place(int index, Vector3 a, Vector3 b)
    {
        SpriteRenderer sprite = _bones[index];
        Vector3 delta = b - a;
        float length = delta.magnitude;
        if (length < 1e-5f)
        {
            sprite.enabled = false;
            return;
        }

        sprite.enabled = true;
        sprite.transform.position = (a + b) * 0.5f;
        sprite.transform.rotation = Quaternion.Euler(
            0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        sprite.size = new Vector2(length, lineWidth);
    }

    void Tick()
    {
        Vector3 hipL = JointWorld(_leftThigh);
        Vector3 hipR = JointWorld(_rightThigh);
        Vector3 pelvis = (hipL + hipR) * 0.5f;
        Vector3 lumbar = JointWorld(_torso);
        Vector3 neck = JointWorld(_neck);
        Vector3 head = JointWorld(_head);

        int i = 0;
        Place(i++, hipL, hipR);
        Place(i++, pelvis, lumbar);
        Place(i++, lumbar, neck);
        Place(i++, neck, head);

        Place(i++, JointWorld(_leftUpper), JointWorld(_leftLower));
        Place(i++, JointWorld(_leftLower), JointWorld(_leftHand));
        Place(i++, JointWorld(_rightUpper), JointWorld(_rightLower));
        Place(i++, JointWorld(_rightLower), JointWorld(_rightHand));

        Vector3 leftFootCenter = ColliderCorner(_leftFoot, 0f, 0f);
        Vector3 rightFootCenter = ColliderCorner(_rightFoot, 0f, 0f);
        Place(i++, hipL, JointWorld(_leftShin));
        Place(i++, JointWorld(_leftShin), leftFootCenter);
        Place(i++, leftFootCenter, SoleToeFront(_leftToe));
        Place(i++, hipR, JointWorld(_rightShin));
        Place(i++, JointWorld(_rightShin), rightFootCenter);
        Place(i++, rightFootCenter, SoleToeFront(_rightToe));
    }
}
