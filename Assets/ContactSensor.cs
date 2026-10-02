using UnityEngine;

// Capture-walk contact facts. Sole owner of grounded / touching / loaded /
// supporting. Reads colliders and positions only. Does not read walkPhase,
// CaptureWalkState, stanceFoot or swingFoot: a swing toe scrape is a
// contact, not a gait role.
//
//     grounded    manifold contact (BodyStateEstimator, including the
//                 contactOffset fill). Heel OR toe — CoM/fell still
//                 uses this union.
//     heel        the HEEL box is in the manifold (or the contactOffset
//                 fill on that box). A swing toe scrape is not this.
//                 CoM / fell still use this union via grounded.
//     heelPin     caudal (local −X) corners of the heel box are still
//                 on the slab relative to the toe. DualSupport trail-up
//                 and the dirt-fold rocker read this, not `heel`: the
//                 heel box is 70 % of the sole, so 12° plantarflex never
//                 empties the fill and firstAir never fired (burn_walk
//                 swap 0 / travel 1.65 m / swingFoot 0.955).
//     touching    grounded, or the sole is inside 2×contactOffset of the
//                 slab — the clearance band the walker already treated as
//                 contact, not air. Landing still uses this union.
//     loaded      grounded: the solver is holding this sole, not a hover.
//     supporting  loaded, and CoM x sits on this plate, between the two
//                 loaded plates, or this is the nearer loaded sole.
public sealed class ContactSensor
{
    public struct Foot
    {
        public bool grounded;
        public bool heel;
        public bool heelPin;
        public bool touching;
        public bool loaded;
        public bool supporting;
        public float minX;
        public float maxX;
        public float centerX;
    }

    public Foot Left;
    public Foot Right;

    public bool Grounded(bool left) => left ? Left.grounded : Right.grounded;
    public bool Heel(bool left) => left ? Left.heel : Right.heel;
    public bool HeelPin(bool left) => left ? Left.heelPin : Right.heelPin;
    public bool Touching(bool left) => left ? Left.touching : Right.touching;
    public bool Loaded(bool left) => left ? Left.loaded : Right.loaded;
    public bool Supporting(bool left) => left ? Left.supporting : Right.supporting;
    public bool BothAir => !Left.loaded && !Right.loaded;

    public void Sample(
        BodyStateEstimator body,
        Vector2 com,
        BoxCollider2D leftHeel,
        BoxCollider2D leftToe,
        BoxCollider2D rightHeel,
        BoxCollider2D rightToe)
    {
        float band = Physics2D.defaultContactOffset * 2f;
        Left = SampleFoot(body, true, band, leftHeel, leftToe);
        Right = SampleFoot(body, false, band, rightHeel, rightToe);
        AssignSupport(com.x);
    }

    private static Foot SampleFoot(
        BodyStateEstimator body,
        bool left,
        float band,
        BoxCollider2D heel,
        BoxCollider2D toe)
    {
        Foot foot = default;
        Plate(heel, toe, out foot.minX, out foot.maxX, out foot.centerX);
        float clearance = 0f;
        if (body != null)
        {
            foot.grounded = left ? body.leftFootGrounded : body.rightFootGrounded;
            foot.heel = left ? body.leftHeelGrounded : body.rightHeelGrounded;
            clearance = left
                ? body.leftFootGroundClearance
                : body.rightFootGroundClearance;
            foot.touching = foot.grounded || clearance <= band;
        }
        // Geometric pin: rear heel corners vs the toe, not the filled
        // heel AABB. Cached boxes, four TransformPoint, no alloc.
        foot.heelPin = toe != null ? CaudalPin(heel, toe) : foot.heel;
        if (!foot.heel && clearance > band)
            foot.heelPin = false;
        foot.loaded = foot.grounded;
        return foot;
    }

    // Rear of the heel (CreateLeg local −X) vs the lowest toe corner.
    // 3 mm above the ball is a rocker, not a hover. Do not add the
    // contactOffset fill here: that band is ~2 cm and kept estimator
    // Heel true through a 12° peel (burn_walk).
    private static bool CaudalPin(BoxCollider2D heel, BoxCollider2D toe)
    {
        if (heel == null || toe == null) return false;
        return CaudalMinY(heel) <= toe.bounds.min.y + 0.003f;
    }

    private static float CaudalMinY(BoxCollider2D box)
    {
        Transform t = box.transform;
        Vector2 o = box.offset;
        float hx = box.size.x * 0.5f;
        float hy = box.size.y * 0.5f;
        Vector3 a = t.TransformPoint(new Vector3(o.x - hx, o.y - hy, 0f));
        Vector3 b = t.TransformPoint(new Vector3(o.x - hx, o.y + hy, 0f));
        return a.y < b.y ? a.y : b.y;
    }

    private void AssignSupport(float comX)
    {
        bool leftLoaded = Left.loaded;
        bool rightLoaded = Right.loaded;
        Left.supporting = Supports(comX, leftLoaded, rightLoaded, Left, Right);
        Right.supporting = Supports(comX, rightLoaded, leftLoaded, Right, Left);
    }

    private static bool Supports(
        float comX,
        bool selfLoaded,
        bool otherLoaded,
        Foot self,
        Foot other)
    {
        if (!selfLoaded) return false;
        if (!otherLoaded) return true;
        if (comX >= self.minX && comX <= self.maxX) return true;
        bool between = (comX - self.centerX) * (other.centerX - comX) >= 0f;
        if (between) return true;
        float selfDist = Mathf.Abs(comX - self.centerX);
        float otherDist = Mathf.Abs(comX - other.centerX);
        return selfDist <= otherDist;
    }

    private static void Plate(
        BoxCollider2D heel,
        BoxCollider2D toe,
        out float minX,
        out float maxX,
        out float centerX)
    {
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        Expand(heel, ref min, ref max);
        Expand(toe, ref min, ref max);
        if (min > max)
        {
            minX = 0f;
            maxX = 0f;
            centerX = 0f;
            return;
        }
        minX = min;
        maxX = max;
        centerX = 0.5f * (min + max);
    }

    private static void Expand(BoxCollider2D box, ref float min, ref float max)
    {
        if (box == null) return;
        min = Mathf.Min(min, box.bounds.min.x);
        max = Mathf.Max(max, box.bounds.max.x);
    }
}
