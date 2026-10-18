using UnityEngine;

// Capture-walk swing sole. Sole owner of the airborne curve. Input is the
// frozen start and the landing goal; output is ankle position and velocity.
// Does not read walkPhase, xi, or CoM: the parent already chose those when
// it froze the plan, and it applies the live reach clamp after this returns.
//
// Horizontal carry and vertical plant are different jobs. A still-loaded
// swing must not start the curve at startY: lift-in from the slab is 0 at
// freeze, so DriveCaptureSwingLeg undid the dual-support KneeLift and the
// sole never left (swap 1, fold 6.3 s). Peel in place (Y at apex, X frozen)
// only while the caudal heel pin is still down. Once that pin opens, or
// CoM has already passed the stance toe, the parent sets catchSlide so X
// travels toward the catch — waiting for union-air froze X until CoM
// passed the toe and Recovery followed the dump (−98°). Recovery still
// carries X while glued.
public sealed class SwingTrajectory
{
    public struct Path
    {
        public float startX;
        public float startY;
        public float targetX;
        public float landY;
        public float apex;
        public float duration;
        public float xShare;
        public float swingApex;
        public float press;
        public bool catchSlide;
    }

    public struct Pose
    {
        public float x;
        public float y;
        public float vx;
        public float vy;
        // How far the X carry has eased, 0…1. The parent uses this to fade
        // the live reach clamp: a sole still on the slab must not be yanked
        // sideways by a taut hip.
        public float xEase;
    }

    public Pose Evaluate(in Path path, float age, bool leftGround, bool touching)
    {
        float peelApex = Mathf.Max(0f, path.apex);
        if (!leftGround)
            peelApex = Mathf.Max(peelApex, Mathf.Max(0f, path.swingApex));
        float originY = path.startY + peelApex;

        float t = path.duration > 0.001f ? path.duration : 1f;
        float s = age / t;
        float phase = Mathf.Clamp01(s);
        float xShare = Mathf.Clamp(path.xShare, 0.20f, 1f);
        float xPhase = Mathf.Clamp01(phase / xShare);
        float xEase = xPhase * xPhase * (3f - 2f * xPhase);
        float invT = 1f / t;
        float dXPhase = 0f;
        if (xShare > 0.0001f)
        {
            float raw = phase / xShare;
            if (raw > 0f && raw < 1f)
                dXPhase = invT / xShare;
        }
        float dXEase = 6f * xPhase * (1f - xPhase) * dXPhase;

        if (!leftGround)
        {
            // Command clearance now. A 25 % smoothstep from startY is a
            // closed-chain weld: y = startY at freeze, hip/knee IK tracks
            // the slab, and swingFoot stays 1. X stays at start unless this
            // is a recovery catch — then the sole has to travel toward xi
            // even before the probe drops.
            if (path.catchSlide)
            {
                return new Pose
                {
                    x = Mathf.Lerp(path.startX, path.targetX, xEase),
                    y = originY,
                    vx = (path.targetX - path.startX) * dXEase,
                    vy = 0f,
                    xEase = xEase
                };
            }
            return new Pose
            {
                x = path.startX,
                y = originY,
                vx = 0f,
                vy = 0f,
                xEase = 0f
            };
        }

        float plant = Mathf.Clamp01((phase - 0.70f) / 0.30f);
        float plantU = plant;
        plant = plant * plant * (3f - 2f * plant);
        float dPlant = 0f;
        {
            float raw = (phase - 0.70f) / 0.30f;
            float dPlantU = (raw > 0f && raw < 1f) ? invT / 0.30f : 0f;
            dPlant = 6f * plantU * (1f - plantU) * dPlantU;
        }

        if (s > 1f)
        {
            float sink = touching
                ? 0f
                : Mathf.Max(0f, path.press) * Mathf.Clamp01(s - 1f);
            float dSink = (!touching && s > 1f && s < 2f)
                ? Mathf.Max(0f, path.press) * invT
                : 0f;
            return new Pose
            {
                x = path.targetX,
                y = path.landY - sink,
                vx = 0f,
                vy = -dSink,
                xEase = 1f
            };
        }

        return new Pose
        {
            x = Mathf.Lerp(path.startX, path.targetX, xEase),
            y = Mathf.Lerp(originY, path.landY, plant),
            vx = (path.targetX - path.startX) * dXEase,
            vy = (path.landY - originY) * dPlant,
            xEase = xEase
        };
    }
}
