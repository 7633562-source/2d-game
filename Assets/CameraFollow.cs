using System.Collections.Generic;
using UnityEngine;

// Picture only: camera in LateUpdate, does not touch forces or Rigidbody2D.
// Target is CoM of human, bird, tree, dog, or hedgehog — not the object root.
// Flock: centroid of the birds, otherwise the frame keeps one bird at the row edge.
[DefaultExecutionOrder(100)]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Human human;
    public Bird bird;
    public PlantTree tree;
    public Dog dog;
    public Hedgehog hedgehog;
    // Bird / dog / tree / World yard. Not the SampleScene human stand:
    // CoM + 1.1 m puts the whole 1.77 m chain below the view center.
    public Vector3 offset = new Vector3(0f, 1.1f, -10f);

    [Header("Human stand frame (SampleScene)")]
    [Tooltip("From CoM. -0.10 m centers the 1.77 m chain (CoM sits 0.10 m above the head-feet midpoint).")]
    public Vector3 humanStandOffset = new Vector3(0f, -0.10f, -10f);
    [Tooltip("Half-height of the SampleScene Game view. 2 m = 4 m tall, about 1.1 m margin above the crown and below the soles.")]
    public float humanStandOrthoSize = 2.0f;
    [Tooltip("If on, LateUpdate keeps orthographicSize and centers Y on live head-to-feet sprites. SampleScene stand only.")]
    public bool frameFullBody;

    [Header("Smoothing")]
    [Tooltip("Seconds to the target for SmoothDamp. Larger — softer, but lags.")]
    public float smoothTime = 0.28f;
    [Tooltip("Camera speed cap, m/s. 0 = no cap.")]
    public float maxSpeed = 18f;

    [Header("Look-ahead from CoM velocity")]
    [Tooltip("Seconds of horizontal look-ahead. 0 = position only.")]
    public float lookAheadTime = 0.22f;
    public float lookAheadMax = 0.7f;
    public float lookAheadSmooth = 0.35f;

    private Vector3 velocity;
    private float lookAheadX;
    private float lookAheadVel;
    private bool snapped;
    private IReadOnlyList<Bird> flock;
    private Camera framedCamera;
    private HumanSegment frameHead;
    private HumanSegment[] frameFeet;

    public void Bind(Human target)
    {
        human = target;
        bird = null;
        tree = null;
        dog = null;
        hedgehog = null;
        flock = null;
        ResetFollow();
    }

    public void Bind(Bird target)
    {
        bird = target;
        human = null;
        tree = null;
        dog = null;
        hedgehog = null;
        flock = null;
        ResetFollow();
    }

    public void Bind(PlantTree target)
    {
        tree = target;
        human = null;
        bird = null;
        dog = null;
        hedgehog = null;
        flock = null;
        ResetFollow();
    }

    public void Bind(Dog target)
    {
        dog = target;
        human = null;
        bird = null;
        tree = null;
        hedgehog = null;
        flock = null;
        ResetFollow();
    }

    public void Bind(Hedgehog target)
    {
        hedgehog = target;
        human = null;
        bird = null;
        tree = null;
        dog = null;
        flock = null;
        ResetFollow();
    }

    public void BindFlock(IReadOnlyList<Bird> targets)
    {
        flock = targets;
        bird = null;
        human = null;
        tree = null;
        dog = null;
        hedgehog = null;
        ResetFollow();
    }

    private void ResetFollow()
    {
        snapped = false;
        velocity = Vector3.zero;
        lookAheadX = 0f;
        lookAheadVel = 0f;
        CacheHumanFrameParts();
    }

    void Awake()
    {
        framedCamera = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        // -visual (with graphics) must run the same LateUpdate as Play Mode.
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        if (!HasFollowTarget())
            return;

        if (frameFullBody)
            ApplyHumanStandLens();

        Vector3 desired = DesiredPosition();
        if (!snapped)
        {
            transform.position = desired;
            velocity = Vector3.zero;
            snapped = true;
            return;
        }

        float cap = maxSpeed > 0.01f ? maxSpeed : Mathf.Infinity;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desired,
            ref velocity,
            Mathf.Max(0.04f, smoothTime),
            cap,
            Time.deltaTime);
    }

    private bool HasFollowTarget()
    {
        return human != null || bird != null || tree != null || dog != null
            || hedgehog != null
            || (flock != null && flock.Count > 0);
    }

    private bool TryFlockCentroid(out Vector2 point, out float velX)
    {
        point = Vector2.zero;
        velX = 0f;
        if (flock == null) return false;

        int n = 0;
        Vector2 sum = Vector2.zero;
        float velSum = 0f;
        for (int i = 0; i < flock.Count; i++)
        {
            Bird member = flock[i];
            if (member == null) continue;

            Vector2 p = member.transform.position;
            float vx = 0f;
            if (member.sensors != null)
            {
                p = member.sensors.GetCenterOfMass();
                vx = member.sensors.comVelocity.x;
            }

            sum += p;
            velSum += vx;
            n++;
        }

        if (n < 1) return false;
        point = sum / n;
        velX = velSum / n;
        return true;
    }

    private Vector3 DesiredPosition()
    {
        Vector2 point = Vector2.zero;
        float velX = 0f;
        if (!TryFlockCentroid(out point, out velX))
        {
            if (bird != null)
            {
                point = bird.transform.position;
                if (bird.sensors != null)
                {
                    point = bird.sensors.GetCenterOfMass();
                    velX = bird.sensors.comVelocity.x;
                }
            }
            else if (human != null)
            {
                point = human.transform.position;
                if (human.comCalculator != null)
                    point = human.comCalculator.GetCenterOfMass();
                if (human.bodyStateEstimator != null)
                    velX = human.bodyStateEstimator.comVelocity.x;
                if (frameFullBody && TryHumanSpriteRange(out float headTop, out float feetBottom))
                    point.y = (headTop + feetBottom) * 0.5f;
            }
            else if (dog != null)
            {
                point = dog.GetFollowPoint();
                if (dog.stance != null)
                    velX = dog.stance.comVelocity.x;
            }
            else if (hedgehog != null)
            {
                point = hedgehog.GetFollowPoint();
                if (hedgehog.stance != null)
                    velX = hedgehog.stance.comVelocity.x;
            }
            else if (tree != null)
            {
                point = tree.GetFollowPoint();
            }
        }

        float wantLook = Mathf.Clamp(velX * lookAheadTime, -lookAheadMax, lookAheadMax);
        lookAheadX = Mathf.SmoothDamp(
            lookAheadX,
            wantLook,
            ref lookAheadVel,
            Mathf.Max(0.04f, lookAheadSmooth));

        Vector3 usedOffset = frameFullBody ? humanStandOffset : offset;
        float y = frameFullBody ? point.y : point.y + usedOffset.y;
        return new Vector3(
            point.x + usedOffset.x + lookAheadX,
            y,
            usedOffset.z);
    }

    // SampleScene stand: the scene Camera serializes size 5. A one-shot
    // in GameProcess.Start is not enough — the serialized Camera can keep
    // 5, which is 2.5x this lens and paints the 1.77 m body at ~16% of
    // the Game view. Size comes from live sprite head-to-feet so the
    // painted body, not the CoM guess, owns the frame.
    private void ApplyHumanStandLens()
    {
        if (framedCamera == null)
            framedCamera = GetComponent<Camera>();
        if (framedCamera == null)
            return;
        framedCamera.orthographic = true;
        float size = humanStandOrthoSize;
        if (TryHumanSpriteRange(out float headTop, out float feetBottom))
        {
            float bodyH = headTop - feetBottom;
            if (bodyH > 0.05f)
                size = Mathf.Max(size, bodyH / 0.88f);
        }
        if (TryHumanSpanX(out float leftEdge, out float rightEdge))
        {
            // An upright body is owned by its height; a fallen one lies
            // across the frame, so width owns the lens then. Never below
            // the stand lens: the camera must not zoom in on a fall.
            float aspect = framedCamera.aspect > 0.1f ? framedCamera.aspect : 16f / 9f;
            float spanX = rightEdge - leftEdge + 1f;
            size = Mathf.Max(size, spanX * 0.5f / aspect);
        }
        framedCamera.orthographicSize = size;
    }

    private void CacheHumanFrameParts()
    {
        frameHead = human != null ? human.headSegment : null;
        if (human == null)
        {
            frameFeet = null;
            return;
        }

        Transform root = human.transform;
        frameFeet = new[]
        {
            FindSegment(root, "LeftLegFoot"),
            FindSegment(root, "RightLegFoot"),
            FindSegment(root, "LeftLegToe"),
            FindSegment(root, "RightLegToe")
        };
    }

    private static HumanSegment FindSegment(Transform root, string name)
    {
        Transform child = root.Find(name);
        return child != null ? child.GetComponent<HumanSegment>() : null;
    }

    // Live sprite AABB, not collider localSize and not CoM. That is
    // what the Game view actually paints.
    private bool TryHumanSpriteRange(out float headTop, out float feetBottom)
    {
        headTop = float.NegativeInfinity;
        feetBottom = float.PositiveInfinity;
        ExpandRange(frameHead, ref headTop, ref feetBottom);
        if (frameFeet != null)
        {
            for (int i = 0; i < frameFeet.Length; i++)
                ExpandRange(frameFeet[i], ref headTop, ref feetBottom);
        }

        return headTop > feetBottom && !float.IsInfinity(headTop);
    }

    private static void ExpandRange(HumanSegment segment, ref float top, ref float bottom)
    {
        if (segment == null)
            return;
        if (segment.sprite != null)
        {
            Bounds box = segment.sprite.bounds;
            if (box.max.y > top) top = box.max.y;
            if (box.min.y < bottom) bottom = box.min.y;
            return;
        }

        float y = segment.transform.position.y;
        float half = segment.size.y * 0.5f;
        if (y + half > top) top = y + half;
        if (y - half < bottom) bottom = y - half;
    }

    private bool TryHumanSpanX(out float left, out float right)
    {
        left = float.PositiveInfinity;
        right = float.NegativeInfinity;
        ExpandX(frameHead, ref left, ref right);
        if (frameFeet != null)
        {
            for (int i = 0; i < frameFeet.Length; i++)
                ExpandX(frameFeet[i], ref left, ref right);
        }

        return right > left && !float.IsInfinity(right);
    }

    private static void ExpandX(HumanSegment segment, ref float left, ref float right)
    {
        if (segment == null)
            return;
        if (segment.sprite != null)
        {
            if (segment.sprite.bounds.min.x < left) left = segment.sprite.bounds.min.x;
            if (segment.sprite.bounds.max.x > right) right = segment.sprite.bounds.max.x;
            return;
        }

        float half = segment.size.x * 0.5f;
        float x = segment.transform.position.x;
        if (x - half < left) left = x - half;
        if (x + half > right) right = x + half;
    }
}
