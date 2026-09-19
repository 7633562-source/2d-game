using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// ─── BALANCE-ARENA-1: PER-THROW RECORDING ───
//
// The measurement half of the impact harness. It is READ-ONLY instrumentation:
// nothing here writes a joint, a muscle, a collider or a rigidbody, so a run with
// -impactCount 0 has the physics of the quiet stand.
//
// Sources are the project's own: the shared FallCriterion (Assets/FallCriterion.cs)
// for `fell`, the CONTROLLER's own answer to the same question
// (BalanceController.GeometricFallState, the BodySupportGeometry ladder against the
// estimator's live support span - published per throw as well, because the card asks
// for "the shared FallCriterion AND the controller's own state" and the two do not
// agree on every window), the geometric support ladder (BodySupportGeometry) against the
// estimator's live support span, the solver's own contact impulses for the vertical
// load channel (fzTotal, read exactly like TrialRecorder's load meter), and
// Muscle.activation * maxTorque * direction for the APPLIED joint torque.
//
// Latency (card item L1) is reported on three frames, because a hit has two
// responses and only one of them is a controller decision:
//   contactTime        the frame the solver reports the first human contact
//   passiveResponseMs  the first frame the CoM acceleration leaves its pre-impact
//                      noise band - the body being shoved, no code involved
//   activeResponseMs   the first frame a leg joint torque leaves its own pre-impact
//                      noise band - the first thing the CONTROLLER did about it
// A command that changes at or before the contact frame is clairvoyance, and the
// run flags it as such.
public class ImpactRecorder
{
    private const float BaselineWindowStart = 4.0f;   // same convention as TrialRecorder
    private const float BaselineWindowEnd = 5.0f;
    private const int HistoryFrames = 100;            // 0.5 s at 200 Hz
    private const int JointCount = 6;                 // hip L/R, knee L/R, ankle L/R

    // ─── THE BLOW AND THE BODY'S ANSWER (audit of 19.09) ───
    // `Trials/Runs/impact-grid-report.md` section 5c measured the old definition: the
    // flag was set on the first window frame with fzTotal > bodyWeight, and the window
    // opened at the contact, so `pushedBack` was the COLLISION's own ground load - true
    // in 12/12 classes at contact + 0..5 ms, and the above-weight impulse was 468-473
    // frames of the stand's own re-settling in the classes that stayed up against 3-13
    // frames in the ones that went down, i.e. not comparable between them. The blow and
    // the answer are separate channels now:
    //   * the PASSIVE window is the first 50 ms after the contact. The card's own
    //     latency model (30-50 ms spinal reflex, 100-150 ms long-loop, 150-250 ms
    //     voluntary) says no living controller can put force on the ground that early,
    //     so every above-weight frame inside it is the mechanical arrival of the blow.
    //     A measured contact-duration column is reported next to it, because the object
    //     can keep grazing the body long after its momentum has arrived - the 0.25 m
    //     circle at 0.5 kg stays in contact for 280 ms while the 0.60 m plate is done in
    //     10 ms, and a boundary drawn at "the contact ended" would call the same body
    //     answer a push in one shape and not in the other.
    //   * `pushedBack` is a SUSTAINED rise of the smoothed ground force after that.
    // 5 % of body weight is ~500x the stand's own idle amplitude (measured pre-impact:
    // +-0.05 N, 0.007 % of 695 N), and 40 ms is longer than the same 30-50 ms spinal
    // reflex, so a shorter bump cannot be a leg pushing either.
    private const float PassiveWindowSeconds = 0.05f;  // the card's spinal-reflex floor
    private const int ContactSettleFrames = 5;         // 25 ms without a new contact impulse
    private const int PushMeanFrames = 8;              // 40 ms of the smoothed load
    private const float PushThresholdRatio = 0.05f;    // +5 % of body weight

    private readonly Human human;
    private readonly ImpactThrower thrower;
    private readonly string label;
    private readonly string outputFolder;
    private readonly string sourceFingerprint;
    private readonly string buildGuid;
    private readonly float startY;
    private float groundTopY;
    private readonly float fixedDelta;
    private readonly float windowSeconds;

    private CenterOfMassCalculator com;
    private BodyStateEstimator state;
    private VestibularSystem vestibular;
    private BalanceController balance;
    private StepPhaseDriver stepDriver;
    private Transform pelvis;
    private readonly Collider2D[] sole = new Collider2D[4];
    private bool solesResolved;
    private bool stanceMeasured;
    private ContactFilter2D groundFilter;
    private bool groundFilterReady;
    private readonly ContactPoint2D[] contactBuffer = new ContactPoint2D[16];
    private readonly float[] torques = new float[JointCount];
    private readonly float[] ceilings = new float[JointCount];

    private readonly StringBuilder rows = new StringBuilder(1 << 20);
    private bool headerWritten;
    private int samples;

    private FallCriterion.Latch fallLatch;
    private FallCriterion.Baseline standBaseline;
    private float fallTime = -1f;
    private float bodyWeightN;
    private bool downNow;

    private bool hasLastCom;
    private float lastComX;
    private float lastComVX;

    // Rolling pre-impact history: the noise band a response has to leave to count.
    private readonly float[] histAccel = new float[HistoryFrames];
    private readonly float[,] histTau = new float[JointCount, HistoryFrames];
    private int histIndex;
    private int histFilled;

    private class ThrowWindow
    {
        public int index;
        public bool contacted;
        public float contactTime = -1f;
        public string contactPart = string.Empty;
        public Vector2 contactPoint;
        public Vector2 aimPoint;
        public float referenceImpulseNs;
        public float closingSpeedMps;
        public float comXAtContact;
        public float comYAtContact;
        public int stepsAtContact;
        public int lastSteps;
        public float peakComExcursionM;
        public float peakComDropM = 0f;
        public float minSupportMarginM = float.MaxValue;
        public float backInsideSupportSeconds = -1f;
        public bool wentOutsideSupport;
        public float insideRunSeconds;
        public float comExcursionAtEndM;
        public float marginAtEndM;
        public readonly float[] peakTau = new float[JointCount];
        public readonly float[] limitFrames = new float[JointCount];
        public float framesInWindow;
        public float peakFzTotalN;
        // The passive window: the first PassiveWindowSeconds after the contact.
        public bool passiveReached;
        public float passivePeakFzRatio;
        public float passiveExcessImpulseNs;
        // The measured duration of the projectile's contact with the body (geometry,
        // not force): the delivered impulse grows while the object is still touching.
        public float lastDeliveredImpulse;
        public int impulseStableFrames;
        public float contactEndTime = -1f;
        // The body's answer, measured only after the passive window.
        public float peakFzAfterPassiveN;
        public readonly float[] recentFz = new float[PushMeanFrames];
        public int recentCount;
        public int recentIndex;
        public float pushMeanRatio;
        public float pushMeanRatioPeak;
        public bool pushedBack;
        public float pushTime = -1f;
        public int pushFrame = -1;          // frames after the contact frame
        public float pushBackHoldSeconds;
        public float pushBackImpulseNs;
        public float pushBackPeakRatio;
        public float groundPushImpulseNs;   // = pushBackImpulseNs, kept for the audit tools
        public bool fell;
        public float criterionFallTime = -1f;
        public bool alreadyDownAtContact;
        // ─── the CONTROLLER's own fall ladder (card: `fell` = criterion AND controller) ───
        // Read from BalanceController.GeometricFallState: BodySupportGeometry against
        // the estimator's live span, evaluated fresh every frame (0 supported, 1
        // critical = the capture point has left the hull, 2 fallen = the CoM itself
        // has, 3 no support = no foot is carrying the body, which is NOT a fall).
        // `fell` above stays the shared FallCriterion, unchanged and comparable with
        // every run already published; this is a second, independent verdict on the
        // same window, so the two can be read against each other.
        public int ctrlStateAtContact = -1;
        public int ctrlStatePeakAfterContact = -1;
        public float ctrlCriticalAfterContactSeconds = -1f;
        public float ctrlFallenAfterContactSeconds = -1f;
        public float ctrlCriticalFrames;
        public float ctrlFallenFrames;
        public float deliveredImpulseNs;
        public float deliveredImpulseAtEntryNs;
        public float deliveredVerticalImpulseNs;
        public bool outOfPlane;
        public float noiseAccelRms;
        public readonly float[] noiseTauRms = new float[JointCount];
        public readonly float[] noiseTauMean = new float[JointCount];
        public float passiveResponseTime = -1f;
        public float activeResponseTime = -1f;
    }

    private readonly List<ThrowWindow> windows = new List<ThrowWindow>(16);

    public ImpactRecorder(Human human, ImpactThrower thrower, string label, string outputFolder,
        string sourceFingerprint, string buildGuid, float startY, float groundTopY,
        float windowSeconds = 4.5f)
    {
        this.human = human;
        this.thrower = thrower;
        this.label = label;
        this.outputFolder = outputFolder;
        this.sourceFingerprint = sourceFingerprint;
        this.buildGuid = buildGuid;
        this.startY = startY;
        this.groundTopY = groundTopY;
        this.windowSeconds = windowSeconds;
        fixedDelta = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.005f;
        int planned = thrower != null ? thrower.PlannedCount : 0;
        for (int i = 0; i < planned; i++)
        {
            ThrowWindow window = new ThrowWindow();
            window.index = i;
            windows.Add(window);
        }
    }

    public string CsvPath => System.IO.Path.Combine(outputFolder, label + "-impacts.csv");
    public string JsonPath => System.IO.Path.Combine(outputFolder, label + "-impacts.json");

    // ─── THE FRAME'S CAPTION (see ImpactCaption.cs) ───
    // The banner is composed here, where the numbers are, and read by the caption every
    // step; `BannerAgeSeconds` is how long ago the throw it describes landed (-1 before
    // the first one). Both are picture state: no code below reads them back, so a run
    // with the caption disabled would still measure the same thing.
    public string Banner { get; private set; } = string.Empty;
    public float BannerAgeSeconds { get; private set; } = -1f;
    public Vector2 BannerContactPoint { get; private set; }

    private ImpactCaption caption;
    private bool captionTried;
    private int lastBannerWindow = -1;
    private int closeLoggedWindow = -1;

    // ─── ONE FRAME ───
    // Called once per fixed step, where the stand calls recorder.Sample().
    public void Sample(float time)
    {
        Resolve();

        Vector2 comPosition = com != null ? com.GetCenterOfMass() : Vector2.zero;
        float comVX = 0f;
        if (hasLastCom && fixedDelta > 0f)
            comVX = (comPosition.x - lastComX) / fixedDelta;
        float comAX = hasLastCom ? (comVX - lastComVX) / fixedDelta : 0f;
        lastComX = comPosition.x;
        lastComVX = comVX;
        hasLastCom = true;

        float tilt = vestibular != null ? vestibular.GetBodyTilt() : 0f;
        float angVel = vestibular != null ? vestibular.GetBodyAngularVelocity() : 0f;
        float pelvisY = pelvis != null ? pelvis.position.y : 0f;
        float margin = state != null ? state.supportMargin : 0f;
        bool leftGrounded = state != null && state.leftFootGrounded;
        bool rightGrounded = state != null && state.rightFootGrounded;
        SampleTorques();
        float fzTotal = SoleVerticalForce();
        int steps = StepCount();
        bool hasSupport = leftGrounded || rightGrounded;
        if (!stanceMeasured)
        {
            // The stance line is measured on the live body, one step after the spawn
            // (the built rig needs one step to sit in its joints), not assumed.
            stanceMeasured = true;
            ResolveSoles();
            bool any = false;
            float lowest = float.MaxValue;
            for (int i = 0; i < sole.Length; i++)
            {
                if (sole[i] == null) continue;
                lowest = Mathf.Min(lowest, sole[i].bounds.min.y);
                any = true;
            }
            if (any) groundTopY = lowest;
        }

        float comHeight = Mathf.Max(0.05f, comPosition.y - groundTopY);
        int supportLevel = state != null
            ? BodySupportGeometry.Level(hasSupport, comPosition.x, comVX, comHeight,
                state.supportMinX, state.supportMaxX)
            : BodySupportGeometry.NoSupport;
        // The controller's OWN answer to the same question, on the same frame. The
        // card asks the throw record for "the shared FallCriterion AND the
        // controller's own state": the first is `fell` below, this is the second.
        // -1 = the rig has no controller in this run (the column then says so).
        int ctrlState = balance != null ? balance.GeometricFallState : -1;

        if (bodyWeightN <= 0f)
            bodyWeightN = TotalMass() * Mathf.Abs(Physics2D.gravity.y);

        // ─── FALL: the one project-wide criterion, same baseline convention as the stand ───
        if (time >= BaselineWindowStart && time < BaselineWindowEnd)
            standBaseline.Feed(pelvisY, tilt);
        else if (time >= BaselineWindowEnd)
            standBaseline.Freeze();
        float baselineHeight = standBaseline.valid ? standBaseline.height : float.NegativeInfinity;
        downNow = FallCriterion.Step(ref fallLatch, baselineHeight, pelvisY, tilt, fixedDelta, time);
        if (downNow && fallTime < 0f && fallLatch.fired)
            fallTime = fallLatch.fireTime;

        // The projectile of this frame: the oldest live one that has already hit.
        int activeThrow = -1;
        float projectileX = 0f;
        float projectileY = 0f;
        float projectileDistance = 0f;
        bool contacted = false;
        for (int i = 0; i < windows.Count; i++)
        {
            ImpactProjectile projectile = thrower != null ? thrower.ProjectileFor(i) : null;
            if (projectile == null || !projectile.ContactedHuman) continue;
            contacted = true;
            if (activeThrow < 0)
            {
                activeThrow = i;
                projectileX = projectile.transform.position.x;
                projectileY = projectile.transform.position.y;
                projectileDistance = Vector2.Distance(new Vector2(projectileX, projectileY),
                    comPosition);
            }
        }

        // ─── the per-throw windows ───
        for (int i = 0; i < windows.Count; i++)
        {
            ThrowWindow window = windows[i];
            ImpactProjectile projectile = thrower != null ? thrower.ProjectileFor(i) : null;
            if (projectile == null) continue;

            if (!window.contacted && projectile.ContactedHuman)
            {
                // The pre-impact noise snapshot is taken from the history BEFORE this
                // frame is pushed into it: this frame already carries the hit.
                SnapshotNoise(window);
                window.contacted = true;
                window.contactTime = projectile.ContactTime;
                window.contactPart = projectile.ContactPart;
                window.contactPoint = projectile.ContactPoint;
                window.aimPoint = projectile.AimPoint;
                window.referenceImpulseNs = projectile.ReferenceImpulseNs;
                window.closingSpeedMps = projectile.ClosingSpeedMps;
                window.deliveredImpulseAtEntryNs = projectile.DeliveredImpulseAtEntryNs;
                window.comXAtContact = comPosition.x;
                window.comYAtContact = comPosition.y;
                window.stepsAtContact = steps;
                window.ctrlStateAtContact = ctrlState;
            }

            if (window.contacted && time >= window.contactTime
                && time <= window.contactTime + windowSeconds)
            {
                Accumulate(window, time, comPosition, comAX, margin, fzTotal, steps,
                    projectile, ctrlState);
            }
        }

        // The row carries THIS frame's numbers, so it is written after the windows: the
        // three push columns are the live state of the throw this frame belongs to.
        ThrowWindow active = activeThrow >= 0 && activeThrow < windows.Count
            ? windows[activeThrow] : null;
        bool passiveReached = active != null && active.passiveReached;
        float pushMeanRatio = active != null ? active.pushMeanRatio : 0f;
        bool pushActive = active != null && passiveReached
            && active.pushMeanRatio >= 1f + PushThresholdRatio;
        if (!headerWritten)
        {
            WriteHeader();
            headerWritten = true;
        }
        AppendRow(time, comPosition, comVX, comAX, tilt, angVel, pelvisY, margin,
            leftGrounded, rightGrounded, fzTotal, steps, supportLevel, activeThrow,
            projectileX, projectileY, projectileDistance, contacted, passiveReached,
            pushMeanRatio, pushActive, ctrlState);
        samples++;

        PushHistory(comAX);
        UpdateCaption(time);
    }

    // ─── the caption on the frame ───
    // Built once, on the first step, so the harness has finished putting its camera in
    // place; refreshed every step after that. Read-only with respect to the simulation.
    private void UpdateCaption(float time)
    {
        if (!captionTried)
        {
            captionTried = true;
            caption = ImpactCaption.Create(HeaderText(), "label=" + label);
        }
        if (caption == null) return;

        int latest = -1;
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            if (windows[i].contacted) { latest = i; break; }
        }

        if (latest < 0)
        {
            Banner = string.Empty;
            BannerAgeSeconds = -1f;
            caption.Update(string.Empty, -1f, Vector2.zero);
            return;
        }

        ThrowWindow window = windows[latest];
        BannerAgeSeconds = time - window.contactTime;
        BannerContactPoint = window.contactPoint;
        Banner = BannerText(latest, window);

        if (lastBannerWindow != latest)
        {
            lastBannerWindow = latest;
            Debug.Log("IMPACT-BANNER " + Banner.Replace('\n', ' '));
        }
        if (closeLoggedWindow != latest && BannerAgeSeconds > windowSeconds)
        {
            // The window has closed: this is the throw's final text, the same numbers the
            // JSON carries, so the log and the frame can be checked against each other.
            closeLoggedWindow = latest;
            Debug.Log("IMPACT-BANNER-FINAL " + Banner.Replace('\n', ' '));
        }

        caption.Update(Banner, BannerAgeSeconds, BannerContactPoint);
    }

    // The parameters of the run, fixed for its whole length.
    private string HeaderText()
    {
        StringBuilder sb = new StringBuilder(220);
        int planned = thrower != null ? thrower.PlannedCount : 0;
        sb.Append("BALANCE-ARENA-1  |  ").Append(label).Append("  |  seed ")
            .Append(thrower != null ? thrower.seed.ToString(CultureInfo.InvariantCulture) : "n/a")
            .Append("  |  ").Append(planned.ToString(CultureInfo.InvariantCulture)).Append(" throw(s)");
        sb.Append('\n');
        sb.Append(thrower != null ? thrower.shapeText : "n/a");
        sb.Append(' ').Append(Fmt(thrower != null ? thrower.sizeMeters : 0f)).Append(" m  |  ")
            .Append(Fmt(thrower != null ? thrower.massKg : 0f)).Append(" kg  |  ")
            .Append(Fmt(thrower != null ? thrower.speedMps : 0f)).Append(" m/s  |  aim ")
            .Append(thrower != null ? thrower.aimPart : "n/a")
            .Append("  |  side ").Append(thrower != null ? thrower.sideText : "n/a");
        sb.Append('\n');
        sb.Append("step ").Append(Fmt(fixedDelta * 1000f)).Append(" ms  |  build ")
            .Append(ShortId(buildGuid)).Append("  |  source ").Append(ShortId(sourceFingerprint));
        return sb.ToString();
    }

    // The live text of one throw: what was thrown, what it hit, what it delivered to the
    // body, and what the body did about it.
    private string BannerText(int index, ThrowWindow window)
    {
        StringBuilder sb = new StringBuilder(260);
        sb.Append("THROW ").Append((index + 1).ToString(CultureInfo.InvariantCulture))
            .Append('/').Append(windows.Count.ToString(CultureInfo.InvariantCulture))
            .Append("  |  ").Append(thrower != null ? thrower.shapeText : "n/a")
            .Append(' ').Append(Fmt(thrower != null ? thrower.sizeMeters : 0f)).Append(" m  |  ")
            .Append(Fmt(thrower != null ? thrower.massKg : 0f)).Append(" kg  |  ")
            .Append(Fmt(thrower != null ? thrower.speedMps : 0f)).Append(" m/s  |  aim ")
            .Append(thrower != null ? thrower.aimPart : "n/a");
        sb.Append('\n');
        sb.Append("hit ").Append(window.contactPart.ToLowerInvariant())
            .Append(" @ ").Append(Fmt3(window.contactTime)).Append(" s  |  reference impulse ")
            .Append(Fmt(window.referenceImpulseNs)).Append(" N*s  |  closing ")
            .Append(Fmt(window.closingSpeedMps)).Append(" m/s");
        sb.Append('\n');
        float percent = window.referenceImpulseNs > 0f
            ? 100f * window.deliveredImpulseNs / window.referenceImpulseNs
            : 0f;
        sb.Append("delivered ").Append(Fmt(window.deliveredImpulseNs)).Append(" N*s (")
            .Append(percent.ToString("0", CultureInfo.InvariantCulture)).Append("%)  |  CoM ")
            .Append(Fmt(window.peakComExcursionM)).Append(" m  |  min margin ")
            .Append(Signed(window.minSupportMarginM)).Append(" m  |  steps ")
            .Append(Mathf.Max(0, window.lastSteps - window.stepsAtContact)
                .ToString(CultureInfo.InvariantCulture));
        sb.Append('\n');
        sb.Append(GroundText(window));
        sb.Append('\n');
        sb.Append(OutcomeText(window));
        return sb.ToString();
    }

    // The ground channel in one line: the passive window (the mechanical arrival of the
    // blow), the measured contact duration, and what the body did about it. These were
    // one column before the 19.09 audit (report 5c); the line is what the frame shows.
    private string GroundText(ThrowWindow window)
    {
        StringBuilder sb = new StringBuilder(170);
        sb.Append("passive 50 ms: peak ").Append(Fmt(window.passivePeakFzRatio)).Append(" W, ")
            .Append(Fmt(window.passiveExcessImpulseNs)).Append(" N*s");
        if (window.contactEndTime >= 0f)
        {
            sb.Append("  |  contact ")
                .Append(Fmt((window.contactEndTime - window.contactTime) * 1000f)).Append(" ms");
        }
        sb.Append("  |  push ");
        if (!window.passiveReached)
        {
            sb.Append("not measured yet (passive window)");
        }
        else if (!window.pushedBack)
        {
            sb.Append("none (peak 40 ms mean ")
                .Append(Fmt(window.pushMeanRatioPeak)).Append(" W)");
        }
        else
        {
            sb.Append(Fmt(window.pushBackPeakRatio)).Append(" W at +")
                .Append(Fmt((window.pushTime - window.contactTime) * 1000f)).Append(" ms, hold ")
                .Append(Fmt(window.pushBackHoldSeconds * 1000f)).Append(" ms, ")
                .Append(Fmt(window.pushBackImpulseNs)).Append(" N*s");
        }
        return sb.ToString();
    }

    private string OutcomeText(ThrowWindow window)
    {
        string outcome;
        if (window.alreadyDownAtContact)
            outcome = "already down when it arrived";
        else if (window.criterionFallTime > 0f)
            outcome = "down " + Fmt3(window.criterionFallTime - window.contactTime) + " s after contact";
        else if (window.fell)
            outcome = "down (criterion latched)";
        else
            outcome = "still standing +" + Fmt3(Mathf.Max(0f, BannerAgeSeconds)) + " s";
        return outcome + "  |  " + ControllerText(window);
    }

    // The controller's own reading of the same window (card: `fell` = criterion AND
    // controller state). Printed on the frame so a still cannot show only one verdict.
    private string ControllerText(ThrowWindow window)
    {
        if (window.ctrlStateAtContact < 0)
            return "controller n/a";
        StringBuilder sb = new StringBuilder(96);
        sb.Append("controller ").Append(window.ctrlStateAtContact
            .ToString(CultureInfo.InvariantCulture)).Append(" at contact");
        if (window.ctrlFallenAfterContactSeconds >= 0f)
        {
            sb.Append(", fallen +").Append(Fmt3(window.ctrlFallenAfterContactSeconds))
                .Append(" s");
        }
        else
        {
            sb.Append(", no fall");
        }
        sb.Append(" -> ").Append(FallVerdict(window));
        return sb.ToString();
    }

    private static string Fmt(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Fmt3(float value)
    {
        return value.ToString("0.000", CultureInfo.InvariantCulture);
    }

    private static string Signed(float value)
    {
        return value.ToString("+0.000;-0.000", CultureInfo.InvariantCulture);
    }

    private static string ShortId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "n/a";
        return id.Length > 12 ? id.Substring(0, 12) : id;
    }

    private void Accumulate(ThrowWindow window, float time, Vector2 comPosition, float comAX,
        float margin, float fzTotal, int steps, ImpactProjectile projectile, int ctrlState)
    {
        window.framesInWindow += 1f;
        window.lastSteps = steps;
        window.deliveredImpulseNs = projectile.DeliveredImpulseNs;
        window.deliveredVerticalImpulseNs = projectile.DeliveredVerticalImpulseNs;
        if (projectile.OutOfPlane) window.outOfPlane = true;

        float excursion = Mathf.Abs(comPosition.x - window.comXAtContact);
        if (excursion > window.peakComExcursionM) window.peakComExcursionM = excursion;
        window.comExcursionAtEndM = excursion;
        window.marginAtEndM = margin;
        float drop = window.comYAtContact - comPosition.y;
        if (drop > window.peakComDropM) window.peakComDropM = drop;
        if (margin < window.minSupportMarginM) window.minSupportMarginM = margin;
        if (margin < 0f)
        {
            window.wentOutsideSupport = true;
            window.insideRunSeconds = 0f;
        }
        else if (window.wentOutsideSupport && window.backInsideSupportSeconds < 0f)
        {
            window.insideRunSeconds += fixedDelta;
            if (window.insideRunSeconds >= 0.1f)
                window.backInsideSupportSeconds = time - window.contactTime - 0.1f;
        }

        for (int j = 0; j < JointCount; j++)
        {
            float magnitude = Mathf.Abs(torques[j]);
            if (magnitude > window.peakTau[j]) window.peakTau[j] = magnitude;
            if (ceilings[j] > 0f && magnitude >= 0.98f * ceilings[j])
                window.limitFrames[j] += 1f;
        }

        if (fzTotal > window.peakFzTotalN) window.peakFzTotalN = fzTotal;

        // ─── the blow, then the body's answer (see the header comment) ───
        // Contact duration: the projectile's delivered impulse grows while it is still
        // in contact with the body; ContactSettleFrames of no growth mean that contact
        // has ended. Recorded as geometry - it decides nothing below.
        float delivered = projectile.DeliveredImpulseNs;
        if (delivered > window.lastDeliveredImpulse + 1e-6f)
        {
            window.lastDeliveredImpulse = delivered;
            window.impulseStableFrames = 0;
        }
        else
        {
            window.impulseStableFrames++;
        }
        if (window.contactEndTime < 0f && window.impulseStableFrames >= ContactSettleFrames)
            window.contactEndTime = time - (ContactSettleFrames - 1) * fixedDelta;

        bool passive = time >= window.contactTime + PassiveWindowSeconds;
        if (!passive)
        {
            // No living controller can answer this early: the sequence here is the
            // mechanical arrival of the blow. This is what the old column counted.
            if (bodyWeightN > 0f)
                window.passivePeakFzRatio = Mathf.Max(window.passivePeakFzRatio,
                    fzTotal / bodyWeightN);
            window.passiveExcessImpulseNs += Mathf.Max(0f, fzTotal - bodyWeightN) * fixedDelta;
        }
        else
        {
            window.passiveReached = true;
            if (fzTotal > window.peakFzAfterPassiveN) window.peakFzAfterPassiveN = fzTotal;
            window.recentFz[window.recentIndex] = fzTotal;
            window.recentIndex = (window.recentIndex + 1) % PushMeanFrames;
            if (window.recentCount < PushMeanFrames) window.recentCount++;
            if (window.recentCount == PushMeanFrames && bodyWeightN > 0f)
            {
                float mean = 0f;
                for (int k = 0; k < PushMeanFrames; k++) mean += window.recentFz[k];
                mean /= PushMeanFrames;
                window.pushMeanRatio = mean / bodyWeightN;
                window.pushMeanRatioPeak = Mathf.Max(window.pushMeanRatioPeak,
                    window.pushMeanRatio);
                if (window.pushMeanRatio >= 1f + PushThresholdRatio)
                {
                    window.pushedBack = true;
                    if (window.pushFrame < 0)
                    {
                        window.pushTime = time;
                        window.pushFrame = Mathf.RoundToInt(window.framesInWindow);
                    }
                    window.pushBackHoldSeconds += fixedDelta;
                    window.pushBackImpulseNs += (mean - bodyWeightN) * fixedDelta;
                    window.groundPushImpulseNs = window.pushBackImpulseNs;
                    window.pushBackPeakRatio = Mathf.Max(window.pushBackPeakRatio,
                        window.pushMeanRatio);
                }
            }
        }
        if (fallTime > 0f)
        {
            // A fall before the contact means the body was already down when the object
            // arrived: "time to fall" is then meaningless for that throw and is reported
            // as -1 with the flag instead of a negative number.
            if (fallTime < window.contactTime) window.alreadyDownAtContact = true;
            else if (window.criterionFallTime < 0f) window.criterionFallTime = fallTime;
        }
        if (downNow && !window.fell)
        {
            window.fell = true;
        }

        // ─── the controller's own ladder, in the same window ───
        // The window opens at the contact, so ctrlStateAtContact is the posture the
        // blow found him in; both times below are measured from that contact, exactly
        // like the criterion's. NoSupport (3) is flight - no foot carries the body -
        // and is neither a fall nor an improvement, so it never sets the peak.
        if (ctrlState >= 0)
        {
            if (ctrlState != BodySupportGeometry.NoSupport
                && ctrlState > window.ctrlStatePeakAfterContact)
            {
                window.ctrlStatePeakAfterContact = ctrlState;
            }
            if (ctrlState == BodySupportGeometry.Critical)
            {
                window.ctrlCriticalFrames += 1f;
                if (window.ctrlCriticalAfterContactSeconds < 0f)
                    window.ctrlCriticalAfterContactSeconds = time - window.contactTime;
            }
            else if (ctrlState == BodySupportGeometry.Fallen)
            {
                window.ctrlFallenFrames += 1f;
                if (window.ctrlFallenAfterContactSeconds < 0f)
                    window.ctrlFallenAfterContactSeconds = time - window.contactTime;
            }
        }

        // ─── L1 latency, evaluated online against the pre-impact band ───
        float bandAccel = Mathf.Max(4f * window.noiseAccelRms, 0.30f);
        if (window.passiveResponseTime < 0f && Mathf.Abs(comAX) > bandAccel)
            window.passiveResponseTime = time;
        if (window.activeResponseTime < 0f)
        {
            for (int j = 0; j < JointCount; j++)
            {
                float band = Mathf.Max(4f * window.noiseTauRms[j], 2.0f);
                if (Mathf.Abs(torques[j] - window.noiseTauMean[j]) > band)
                {
                    window.activeResponseTime = time;
                    break;
                }
            }
        }
    }

    // ─── SOURCES ───
    private void Resolve()
    {
        if (human == null) return;
        if (balance == null) balance = human.GetComponent<BalanceController>();
        if (state == null) state = human.GetComponent<BodyStateEstimator>();
        if (com == null) com = human.GetComponent<CenterOfMassCalculator>();
        if (vestibular == null) vestibular = human.GetComponent<VestibularSystem>();
        if (pelvis == null) pelvis = human.transform.Find("Pelvis");
    }

    private void SampleTorques()
    {
        if (balance == null)
        {
            for (int j = 0; j < JointCount; j++)
            {
                torques[j] = 0f;
                ceilings[j] = 0f;
            }
            return;
        }
        torques[0] = AppliedTorque(balance.leftHipFlexor, balance.leftHipExtensor);
        torques[1] = AppliedTorque(balance.rightHipFlexor, balance.rightHipExtensor);
        torques[2] = AppliedTorque(balance.leftKneeFlexor, balance.leftKneeExtensor);
        torques[3] = AppliedTorque(balance.rightKneeFlexor, balance.rightKneeExtensor);
        torques[4] = AppliedTorque(balance.leftAnkleFlexor, balance.leftAnkleExtensor);
        torques[5] = AppliedTorque(balance.rightAnkleFlexor, balance.rightAnkleExtensor);
        ceilings[0] = Ceiling(balance.leftHipFlexor, balance.leftHipExtensor);
        ceilings[1] = Ceiling(balance.rightHipFlexor, balance.rightHipExtensor);
        ceilings[2] = Ceiling(balance.leftKneeFlexor, balance.leftKneeExtensor);
        ceilings[3] = Ceiling(balance.rightKneeFlexor, balance.rightKneeExtensor);
        ceilings[4] = Ceiling(balance.leftAnkleFlexor, balance.leftAnkleExtensor);
        ceilings[5] = Ceiling(balance.rightAnkleFlexor, balance.rightAnkleExtensor);
    }

    private static float AppliedTorque(Muscle flexor, Muscle extensor)
    {
        float torque = 0f;
        if (flexor != null) torque += flexor.activation * flexor.maxTorque * flexor.direction;
        if (extensor != null) torque += extensor.activation * extensor.maxTorque * extensor.direction;
        return torque;
    }

    private static float Ceiling(Muscle flexor, Muscle extensor)
    {
        float ceiling = 0f;
        if (flexor != null) ceiling = Mathf.Max(ceiling, flexor.maxTorque);
        if (extensor != null) ceiling = Mathf.Max(ceiling, extensor.maxTorque);
        return ceiling;
    }

    private int StepCount()
    {
        if (stepDriver == null && human != null)
            stepDriver = human.GetComponent<StepPhaseDriver>();
        int steps = stepDriver != null ? stepDriver.SwapCount : 0;
        if (balance != null && balance.useCaptureWalker != 0)
            steps = Mathf.Max(steps, balance.CaptureWalkStepCount);
        return steps;
    }

    private float TotalMass()
    {
        if (human == null) return 0f;
        float mass = 0f;
        Rigidbody2D[] bodies = human.GetComponentsInChildren<Rigidbody2D>();
        for (int i = 0; i < bodies.Length; i++)
            if (bodies[i] != null) mass += bodies[i].mass;
        return mass;
    }

    // The vertical load channel: the solver's own normal impulses through the two
    // soles, the upward part only, exactly like TrialRecorder's load meter.
    private float SoleVerticalForce()
    {
        if (!solesResolved) ResolveSoles();
        if (!groundFilterReady)
        {
            groundFilter = new ContactFilter2D();
            groundFilter.SetLayerMask(GroundLayers.Mask);
            groundFilter.useTriggers = false;
            groundFilterReady = true;
        }
        float fzL = 0f;
        float fzR = 0f;
        for (int i = 0; i < 4; i++)
        {
            Collider2D collider = sole[i];
            if (collider == null) continue;
            int count = Mathf.Min(collider.GetContacts(groundFilter, contactBuffer),
                contactBuffer.Length);
            for (int k = 0; k < count; k++)
            {
                ContactPoint2D cp = contactBuffer[k];
                float vertical = cp.normalImpulse * cp.normal.y;
                if (vertical <= 0f) continue;
                if (i < 2) fzL += vertical / fixedDelta;
                else fzR += vertical / fixedDelta;
            }
        }
        return fzL + fzR;
    }

    private void ResolveSoles()
    {
        if (human == null) return;
        sole[0] = SegmentCollider("LeftLegFoot");
        sole[1] = SegmentCollider("LeftLegToe");
        sole[2] = SegmentCollider("RightLegFoot");
        sole[3] = SegmentCollider("RightLegToe");
        if (sole[0] != null || sole[1] != null || sole[2] != null || sole[3] != null)
            solesResolved = true;
    }

    private Collider2D SegmentCollider(string name)
    {
        Transform t = human.transform.Find(name);
        return t != null ? t.GetComponent<Collider2D>() : null;
    }

    // ─── NOISE BANDS: the 0.5 s before the hit ───
    private void SnapshotNoise(ThrowWindow window)
    {
        int n = histFilled;
        if (n < 1) n = 1;
        if (n > HistoryFrames) n = HistoryFrames;
        double sumA = 0.0;
        double sumA2 = 0.0;
        for (int i = 0; i < n; i++)
        {
            float a = histAccel[i];
            sumA += a;
            sumA2 += (double)a * a;
        }
        float meanA = (float)(sumA / n);
        window.noiseAccelRms = Mathf.Sqrt(Mathf.Max(0f, (float)(sumA2 / n) - meanA * meanA));
        for (int j = 0; j < JointCount; j++)
        {
            double sum = 0.0;
            double sum2 = 0.0;
            for (int i = 0; i < n; i++)
            {
                float tau = histTau[j, i];
                sum += tau;
                sum2 += (double)tau * tau;
            }
            float mean = (float)(sum / n);
            window.noiseTauMean[j] = mean;
            window.noiseTauRms[j] = Mathf.Sqrt(Mathf.Max(0f, (float)(sum2 / n) - mean * mean));
        }
    }

    private void PushHistory(float comAX)
    {
        histAccel[histIndex] = comAX;
        for (int j = 0; j < JointCount; j++)
            histTau[j, histIndex] = torques[j];
        histIndex = (histIndex + 1) % HistoryFrames;
        if (histFilled < HistoryFrames) histFilled++;
    }

    // ─── CSV ───
    private void WriteHeader()
    {
        rows.Append("time").Append(';')
            .Append("comX").Append(';')
            .Append("comY").Append(';')
            .Append("comVX").Append(';')
            .Append("comAX").Append(';')
            .Append("tilt").Append(';')
            .Append("bodyAngVel").Append(';')
            .Append("pelvisY").Append(';')
            .Append("supportMargin").Append(';')
            .Append("leftGrounded").Append(';')
            .Append("rightGrounded").Append(';')
            .Append("fzTotal").Append(';')
            .Append("appTorqueHipL").Append(';')
            .Append("appTorqueHipR").Append(';')
            .Append("appTorqueKneeL").Append(';')
            .Append("appTorqueKneeR").Append(';')
            .Append("appTorqueAnkleL").Append(';')
            .Append("appTorqueAnkleR").Append(';')
            .Append("steps").Append(';')
            .Append("supportLevel").Append(';')
            .Append("throwIndex").Append(';')
            .Append("projectileX").Append(';')
            .Append("projectileY").Append(';')
            .Append("projectileDistanceM").Append(';')
            .Append("contacted").Append(';')
            .Append("down").Append(';')
            .Append("fell").Append(';')
            .Append("passiveReached").Append(';')
            .Append("pushMeanFzRatio").Append(';')
            .Append("pushActive").Append(';')
            .Append("ctrlState").Append('\n');
    }

    private void AppendRow(float time, Vector2 comPosition, float comVX, float comAX, float tilt,
        float angVel, float pelvisY, float margin, bool leftGrounded, bool rightGrounded,
        float fzTotal, int steps, int supportLevel, int activeThrow, float projectileX,
        float projectileY, float projectileDistance, bool contacted, bool passiveReached,
        float pushMeanRatio, bool pushActive, int ctrlState)
    {
        rows.Append(F(time)).Append(';')
            .Append(F(comPosition.x)).Append(';')
            .Append(F(comPosition.y)).Append(';')
            .Append(F(comVX)).Append(';')
            .Append(F(comAX)).Append(';')
            .Append(F(tilt)).Append(';')
            .Append(F(angVel)).Append(';')
            .Append(F(pelvisY)).Append(';')
            .Append(F(margin)).Append(';')
            .Append(leftGrounded ? "1" : "0").Append(';')
            .Append(rightGrounded ? "1" : "0").Append(';')
            .Append(F(fzTotal)).Append(';')
            .Append(F(torques[0])).Append(';')
            .Append(F(torques[1])).Append(';')
            .Append(F(torques[2])).Append(';')
            .Append(F(torques[3])).Append(';')
            .Append(F(torques[4])).Append(';')
            .Append(F(torques[5])).Append(';')
            .Append(steps.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(supportLevel.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(activeThrow.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(F(projectileX)).Append(';')
            .Append(F(projectileY)).Append(';')
            .Append(F(projectileDistance)).Append(';')
            .Append(contacted ? "1" : "0").Append(';')
            .Append(downNow ? "1" : "0").Append(';')
            .Append(fallLatch.fired ? "1" : "0").Append(';')
            .Append(passiveReached ? "1" : "0").Append(';')
            .Append(F(pushMeanRatio)).Append(';')
            .Append(pushActive ? "1" : "0").Append(';')
            .Append(ctrlState.ToString(CultureInfo.InvariantCulture)).Append('\n');
    }

    private static string F(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    public string BuildCsv()
    {
        return rows.ToString();
    }

    // ─── JSON ───
    public string BuildJson(float simulatedSeconds, float wallSeconds)
    {
        StringBuilder json = new StringBuilder(1 << 16);
        json.Append('{');
        json.Append("\"label\":\"").Append(Escape(label)).Append("\",");
        json.Append("\"harness\":\"BALANCE-ARENA-1\",");
        json.Append("\"sourceFingerprint\":\"").Append(Escape(sourceFingerprint)).Append("\",");
        json.Append("\"buildGuid\":\"").Append(Escape(buildGuid)).Append("\",");
        json.Append("\"unityVersion\":\"").Append(Escape(Application.unityVersion)).Append("\",");
        Num(json, "simulatedSeconds", simulatedSeconds);
        Num(json, "wallSeconds", wallSeconds);
        Num(json, "fixedDelta", fixedDelta);
        Num(json, "startY", startY);
        Num(json, "groundTopY", groundTopY);
        Num(json, "samples", samples);
        Num(json, "bodyMassKg", TotalMass());
        Num(json, "bodyWeightN", bodyWeightN);
        Num(json, "windowSeconds", windowSeconds);
        Num(json, "standBaselineHeight", standBaseline.valid ? standBaseline.height : -1f);
        json.Append("\"fallCriterionTime\":").Append(fallTime < 0f ? "-1" : F(fallTime)).Append(',');
        json.Append("\"fallCriterionFired\":").Append(fallLatch.fired ? "true" : "false").Append(',');
        Num(json, "fallCriterionDropMeters", FallCriterion.DropMeters);
        Num(json, "fallCriterionTiltDegrees", FallCriterion.TiltDegrees);
        Num(json, "fallCriterionSustainSeconds", FallCriterion.SustainSeconds);

        json.Append("\"switches\":{");
        if (thrower != null)
        {
            json.Append("\"massKg\":").Append(F(thrower.massKg)).Append(',');
            json.Append("\"shape\":\"").Append(Escape(thrower.shapeText)).Append("\",");
            json.Append("\"sizeMeters\":").Append(F(thrower.sizeMeters)).Append(',');
            json.Append("\"rectAspect\":").Append(F(thrower.rectAspect)).Append(',');
            json.Append("\"speedMps\":").Append(F(thrower.speedMps)).Append(',');
            json.Append("\"aim\":\"").Append(Escape(thrower.aimPart)).Append("\",");
            json.Append("\"side\":\"").Append(Escape(thrower.sideText)).Append("\",");
            json.Append("\"firstThrowAt\":").Append(F(thrower.firstThrowAt)).Append(',');
            Num(json, "count", thrower.throwCount);
            json.Append("\"spacingSeconds\":").Append(F(thrower.spacingSeconds)).Append(',');
            Num(json, "impactSeed", thrower.seed);
            json.Append("\"spawnDistance\":").Append(F(thrower.spawnDistance)).Append(',');
            json.Append("\"projectileGravity\":").Append(F(thrower.projectileGravity)).Append(',');
            json.Append("\"launch\":\"horizontal, at the aim height, inside the X-Y plane\"");
        }
        json.Append('}').Append(',');

        json.Append("\"throws\":[");
        for (int i = 0; i < windows.Count; i++)
        {
            if (i > 0) json.Append(',');
            AppendThrow(json, windows[i]);
        }
        json.Append(']').Append(',');

        int contactedCount = 0;
        int fellCount = 0;
        int pushCount = 0;
        int outOfPlaneCount = 0;
        int ctrlFallenCount = 0;
        int verdictMismatchCount = 0;
        float maxFz = 0f;
        float maxImpulse = 0f;
        float minActiveLatency = float.MaxValue;
        float maxActiveLatency = -1f;
        for (int i = 0; i < windows.Count; i++)
        {
            ThrowWindow window = windows[i];
            if (window.contacted) contactedCount++;
            if (window.fell) fellCount++;
            if (window.pushedBack) pushCount++;
            if (window.outOfPlane) outOfPlaneCount++;
            if (window.contacted)
            {
                // The card's `fell` = criterion AND controller: both are counted, and
                // the mismatch is its own number so a run cannot hide one behind the
                // other.
                bool ctrlFell = window.ctrlFallenAfterContactSeconds >= 0f;
                if (ctrlFell) ctrlFallenCount++;
                if (ctrlFell != window.fell) verdictMismatchCount++;
            }
            if (window.peakFzTotalN > maxFz) maxFz = window.peakFzTotalN;
            if (window.deliveredImpulseNs > maxImpulse) maxImpulse = window.deliveredImpulseNs;
            if (window.activeResponseTime > 0f)
            {
                float latency = (window.activeResponseTime - window.contactTime) * 1000f;
                if (latency < minActiveLatency) minActiveLatency = latency;
                if (latency > maxActiveLatency) maxActiveLatency = latency;
            }
        }

        json.Append("\"totals\":{");
        Num(json, "planned", windows.Count);
        Num(json, "fired", thrower != null ? thrower.FiredCount : 0);
        Num(json, "contacted", contactedCount);
        Num(json, "fellByCriterion", fellCount);
        Num(json, "pushBackCount", pushCount);
        json.Append("\"pushDefinition\":\"");
        json.Append("pushedBack = a ").Append(F(PushMeanFrames * fixedDelta * 1000f))
            .Append(" ms sliding mean of fzTotal >= ")
            .Append(F(1f + PushThresholdRatio))
            .Append(" * bodyWeightN, measured only after the passive window (contact + ")
            .Append(F(PassiveWindowSeconds * 1000f))
            .Append(" ms, the card's spinal-reflex floor: no living controller can answer earlier); the blow's own arrival is passivePeakFzRatio / passiveExcessImpulseNs and contactMs is how long the projectile stayed in contact with the body");
        json.Append("\",");
        Num(json, "outOfPlaneCount", outOfPlaneCount);
        Num(json, "maxPeakFzTotalN", maxFz);
        Num(json, "maxDeliveredImpulseNs", maxImpulse);
        // The two verdicts on the same windows: the shared FallCriterion and the
        // controller's own ladder. The mismatch count is the number to watch.
        Num(json, "ctrlFallenCount", ctrlFallenCount);
        Num(json, "verdictMismatchCount", verdictMismatchCount);
        Str(json, "fallVerdictDefinition", FallVerdictDefinitionText);
        json.Append("\"minActiveLatencyMs\":")
            .Append(minActiveLatency == float.MaxValue ? "-1" : F(minActiveLatency)).Append(',');
        json.Append("\"maxActiveLatencyMs\":")
            .Append(maxActiveLatency < 0f ? "-1" : F(maxActiveLatency));
        json.Append('}');

        json.Append('}');
        return json.ToString();
    }

    private void AppendThrow(StringBuilder json, ThrowWindow window)
    {
        ImpactProjectile projectile = thrower != null ? thrower.ProjectileFor(window.index) : null;
        ImpactThrower.ThrowPlan plan = thrower != null
            ? thrower.GetPlan(window.index) : default(ImpactThrower.ThrowPlan);

        json.Append('{');
        Num(json, "index", window.index);
        Num(json, "plannedTime", plan.time);
        json.Append("\"planFired\":").Append(plan.fired ? "true" : "false").Append(',');
        json.Append("\"massKg\":").Append(F(plan.mass)).Append(',');
        json.Append("\"shape\":\"").Append(Escape(plan.shape)).Append("\",");
        json.Append("\"sizeMeters\":").Append(F(plan.width)).Append(',');
        json.Append("\"sizeHeightMeters\":").Append(F(plan.height)).Append(',');
        json.Append("\"speedMps\":").Append(F(plan.speed)).Append(',');
        json.Append("\"launchSpeedMps\":").Append(F(plan.speed * plan.speedJitter)).Append(',');
        json.Append("\"aimFamily\":\"")
            .Append(Escape(thrower != null ? thrower.aimPart : string.Empty)).Append("\",");
        json.Append("\"aimPart\":\"").Append(Escape(plan.part)).Append("\",");
        json.Append("\"side\":").Append(plan.side.ToString(CultureInfo.InvariantCulture)).Append(',');
        json.Append("\"sideText\":").Append(plan.side < 0 ? "\"left\"" : "\"right\"").Append(',');
        // OUR reference number: ordered mass * ordered speed.
        json.Append("\"referenceImpulseNs\":").Append(F(plan.impulseNs)).Append(',');
        json.Append("\"aimJitterMeters\":[").Append(F(plan.jitterX)).Append(',')
            .Append(F(plan.jitterY)).Append("],");
        json.Append("\"aimWorld\":[").Append(F(window.aimPoint.x)).Append(',')
            .Append(F(window.aimPoint.y)).Append("],");
        json.Append("\"contacted\":").Append(window.contacted ? "true" : "false").Append(',');
        json.Append("\"contactTime\":")
            .Append(window.contactTime < 0f ? "-1" : F(window.contactTime)).Append(',');
        json.Append("\"contactFrame\":").Append(window.contactTime < 0f
            ? "-1"
            : Mathf.RoundToInt(window.contactTime / fixedDelta).ToString(CultureInfo.InvariantCulture))
            .Append(',');
        json.Append("\"contactPart\":\"").Append(Escape(window.contactPart)).Append("\",");
        json.Append("\"contactPoint\":[").Append(F(window.contactPoint.x)).Append(',')
            .Append(F(window.contactPoint.y)).Append("],");
        json.Append("\"aimMissMeters\":").Append(window.contacted
            ? F(Vector2.Distance(window.contactPoint, window.aimPoint)) : "-1").Append(',');
        json.Append("\"closingSpeedMps\":").Append(F(window.closingSpeedMps)).Append(',');
        json.Append("\"deliveredImpulseNs\":").Append(F(window.deliveredImpulseNs)).Append(',');
        json.Append("\"deliveredImpulseAtEntryNs\":")
            .Append(F(window.deliveredImpulseAtEntryNs)).Append(',');
        json.Append("\"deliveredVerticalImpulseNs\":")
            .Append(F(window.deliveredVerticalImpulseNs)).Append(',');
        json.Append("\"projectileGravityScale\":")
            .Append(F(projectile != null ? projectile.GravityScale : thrower.projectileGravity)).Append(',');
        json.Append("\"outOfPlane\":").Append(window.outOfPlane ? "true" : "false").Append(',');
        json.Append("\"maxAbsZMeters\":")
            .Append(F(projectile != null ? projectile.MaxAbsZ : 0f)).Append(',');

        float latencyPassive = window.passiveResponseTime > 0f
            ? (window.passiveResponseTime - window.contactTime) * 1000f : -1f;
        float latencyActive = window.activeResponseTime > 0f
            ? (window.activeResponseTime - window.contactTime) * 1000f : -1f;
        json.Append("\"latencyPassiveMs\":")
            .Append(latencyPassive < 0f ? "-1" : F(latencyPassive)).Append(',');
        json.Append("\"latencyActiveMs\":")
            .Append(latencyActive < 0f ? "-1" : F(latencyActive)).Append(',');
        json.Append("\"clairvoyanceFlag\":").Append(
            window.activeResponseTime > 0f && window.activeResponseTime <= window.contactTime
                ? "true" : "false").Append(',');
        json.Append("\"noiseAccelRms\":").Append(F(window.noiseAccelRms)).Append(',');
        json.Append("\"noiseTauRmsNm\":[");
        for (int j = 0; j < JointCount; j++)
        {
            if (j > 0) json.Append(',');
            json.Append(F(window.noiseTauRms[j]));
        }
        json.Append("],");

        json.Append("\"fell\":").Append(window.fell ? "true" : "false").Append(',');
        json.Append("\"criterionFallTime\":")
            .Append(window.criterionFallTime < 0f ? "-1" : F(window.criterionFallTime)).Append(',');
        json.Append("\"alreadyDownAtContact\":")
            .Append(window.alreadyDownAtContact ? "true" : "false").Append(',');
        json.Append("\"timeToFallSeconds\":")
            .Append(window.fell && window.criterionFallTime >= 0f
                ? F(window.criterionFallTime - window.contactTime) : "-1").Append(',');
        // ─── the controller's own verdict on the same window ───
        // The card asks the throw record for "the shared FallCriterion AND the
        // controller's own state". `fell` above is the criterion, unchanged and
        // comparable with every run already published; these are the controller's,
        // measured from the same contact by the same clock. A missing reading is -1;
        // fallVerdict is one word so a row cannot be quoted without saying which of
        // the two verdicts fired.
        Num(json, "ctrlStateAtContact", window.ctrlStateAtContact);
        Num(json, "ctrlStatePeakAfterContact", window.ctrlStatePeakAfterContact);
        Num(json, "ctrlCriticalAfterContactSeconds", window.ctrlCriticalAfterContactSeconds);
        Num(json, "ctrlFallenAfterContactSeconds", window.ctrlFallenAfterContactSeconds);
        BoolField(json, "ctrlFell", window.ctrlFallenAfterContactSeconds >= 0f);
        Num(json, "ctrlCriticalFraction",
            Fraction(window.ctrlCriticalFrames, window.framesInWindow));
        Num(json, "ctrlFallenFraction", Fraction(window.ctrlFallenFrames, window.framesInWindow));
        Str(json, "fallVerdict", FallVerdict(window));
        json.Append("\"peakComExcursionM\":").Append(F(window.peakComExcursionM)).Append(',');
        json.Append("\"comExcursionAtWindowEndM\":").Append(F(window.comExcursionAtEndM)).Append(',');
        json.Append("\"returnedWithin50mmAtWindowEnd\":")
            .Append(window.comExcursionAtEndM <= 0.05f && window.marginAtEndM >= 0f
                ? "true" : "false").Append(',');
        json.Append("\"peakComDropM\":").Append(F(window.peakComDropM)).Append(',');
        json.Append("\"minSupportMarginM\":")
            .Append(window.minSupportMarginM == float.MaxValue ? "-1"
                : F(window.minSupportMarginM)).Append(',');
        json.Append("\"wentOutsideSupport\":")
            .Append(window.wentOutsideSupport ? "true" : "false").Append(',');
        json.Append("\"backInsideSupportSeconds\":")
            .Append(window.wentOutsideSupport
                ? (window.backInsideSupportSeconds < 0f ? "-1"
                    : F(window.backInsideSupportSeconds))
                : "0").Append(',');
        json.Append("\"stepsAtContact\":")
            .Append(window.stepsAtContact.ToString(CultureInfo.InvariantCulture)).Append(',');
        json.Append("\"stepsInWindow\":")
            .Append(window.lastSteps.ToString(CultureInfo.InvariantCulture)).Append(',');
        json.Append("\"stepsAfterImpact\":")
            .Append((window.lastSteps - window.stepsAtContact).ToString(CultureInfo.InvariantCulture))
            .Append(',');
        json.Append("\"peakTorqueNm\":{\"hipL\":").Append(F(window.peakTau[0]))
            .Append(",\"hipR\":").Append(F(window.peakTau[1]))
            .Append(",\"kneeL\":").Append(F(window.peakTau[2]))
            .Append(",\"kneeR\":").Append(F(window.peakTau[3]))
            .Append(",\"ankleL\":").Append(F(window.peakTau[4]))
            .Append(",\"ankleR\":").Append(F(window.peakTau[5]))
            .Append('}').Append(',');
        json.Append("\"limitHitFraction\":{\"hipL\":")
            .Append(Ratio(window.limitFrames[0], window.framesInWindow))
            .Append(",\"hipR\":").Append(Ratio(window.limitFrames[1], window.framesInWindow))
            .Append(",\"kneeL\":").Append(Ratio(window.limitFrames[2], window.framesInWindow))
            .Append(",\"kneeR\":").Append(Ratio(window.limitFrames[3], window.framesInWindow))
            .Append(",\"ankleL\":").Append(Ratio(window.limitFrames[4], window.framesInWindow))
            .Append(",\"ankleR\":").Append(Ratio(window.limitFrames[5], window.framesInWindow))
            .Append('}').Append(',');
        json.Append("\"peakFzTotalN\":").Append(F(window.peakFzTotalN)).Append(',');
        json.Append("\"weightN\":").Append(F(bodyWeightN)).Append(',');
        json.Append("\"peakFzOverWeight\":").Append(bodyWeightN > 0f
            ? F(window.peakFzTotalN / bodyWeightN) : "-1").Append(',');
        // ─── the blow and the body's answer (see the header comment) ───
        // passive* is the mechanical arrival of the blow (the first 50 ms, before any
        // living controller can answer); contactMs is the measured duration of the
        // projectile's contact with the body; push* is the sustained rise of the
        // smoothed ground force after the passive window.
        json.Append("\"passiveWindowMs\":")
            .Append(F(PassiveWindowSeconds * 1000f)).Append(',');
        json.Append("\"passivePeakFzRatio\":").Append(F(window.passivePeakFzRatio)).Append(',');
        json.Append("\"passiveExcessImpulseNs\":")
            .Append(F(window.passiveExcessImpulseNs)).Append(',');
        json.Append("\"contactMs\":").Append(window.contactEndTime < 0f
            ? "-1" : F((window.contactEndTime - window.contactTime) * 1000f)).Append(',');
        json.Append("\"peakFzAfterPassiveN\":").Append(F(window.peakFzAfterPassiveN)).Append(',');
        json.Append("\"pushMeanWindowMs\":")
            .Append(F(PushMeanFrames * fixedDelta * 1000f)).Append(',');
        json.Append("\"pushMeanRatioPeak\":").Append(F(window.pushMeanRatioPeak)).Append(',');
        json.Append("\"pushThresholdRatio\":").Append(F(1f + PushThresholdRatio)).Append(',');
        json.Append("\"pushedBack\":").Append(window.pushedBack ? "true" : "false").Append(',');
        json.Append("\"pushTime\":").Append(window.pushTime < 0f ? "-1" : F(window.pushTime)).Append(',');
        json.Append("\"pushFrame\":").Append(window.pushTime < 0f
            ? "-1"
            : Mathf.RoundToInt(window.pushTime / fixedDelta).ToString(CultureInfo.InvariantCulture))
            .Append(',');
        json.Append("\"pushFrameAfterContact\":")
            .Append(window.pushFrame.ToString(CultureInfo.InvariantCulture)).Append(',');
        json.Append("\"pushBackHoldMs\":")
            .Append(F(window.pushBackHoldSeconds * 1000f)).Append(',');
        json.Append("\"pushBackPeakFzRatio\":").Append(F(window.pushBackPeakRatio)).Append(',');
        json.Append("\"pushBackImpulseNs\":").Append(F(window.pushBackImpulseNs)).Append(',');
        json.Append("\"groundPushImpulseNs\":").Append(F(window.groundPushImpulseNs)).Append(',');
        json.Append("\"windowFrames\":")
            .Append(window.framesInWindow.ToString(CultureInfo.InvariantCulture));
        json.Append('}');
    }

    private static string Ratio(float numerator, float denominator)
    {
        if (denominator <= 0f) return "0";
        return (numerator / denominator).ToString("0.####", CultureInfo.InvariantCulture);
    }

    // The float form of Ratio(): a JSON number instead of a JSON string.
    private static float Fraction(float numerator, float denominator)
    {
        return denominator <= 0f ? 0f : numerator / denominator;
    }

    // A JSON boolean field, written like Str() so no call site quotes a key by hand.
    private static void BoolField(StringBuilder json, string key, bool value)
    {
        json.Append('"').Append(key).Append('"').Append(':')
            .Append(value ? "true" : "false").Append(',');
    }

    // A string field, escaped the way the rest of the file escapes one. Written with
    // char literals so a key never has to be quoted by hand at the call site.
    private static void Str(StringBuilder json, string key, string value)
    {
        json.Append('"').Append(key).Append('"').Append(':').Append('"')
            .Append(Escape(value)).Append('"').Append(',');
    }

    // The card's per-throw `fell` is "the shared FallCriterion AND the controller's own
    // state". Both are published in the row; this is the one-word reading of the pair,
    // so a throw can never be quoted without saying which of the two fired. Both are
    // window-scoped: the window is the throw's own windowSeconds after its contact.
    private static string FallVerdict(ThrowWindow window)
    {
        if (!window.contacted) return "not-contacted";
        bool ctrlFell = window.ctrlFallenAfterContactSeconds >= 0f;
        if (window.fell && ctrlFell) return "both";
        if (window.fell) return "criterion-only";
        if (ctrlFell) return "controller-only";
        return "neither";
    }

    // The definition travels with the numbers, the way pushDefinition does.
    private const string FallVerdictDefinitionText =
        "ctrlState* is BalanceController.GeometricFallState, the ladder the controller " +
        "itself publishes (BodySupportGeometry against the estimator's live support span): " +
        "0 supported, 1 critical = the capture point has left the hull, 2 fallen = the CoM " +
        "itself has, 3 no support = no foot carries the body (flight, never counted as a " +
        "fall). ctrlFallenAfterContactSeconds / ctrlCriticalAfterContactSeconds are measured " +
        "from the contact, like timeToFallSeconds; fallVerdict compares ctrlFell with `fell` " +
        "(the shared FallCriterion, unchanged) over the same window: both | criterion-only | " +
        "controller-only | neither.";

    private static void Num(StringBuilder json, string key, float value)
    {
        json.Append('"').Append(key).Append("\":").Append(F(value)).Append(',');
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public void WriteFiles(float simulatedSeconds, float wallSeconds)
    {
        System.IO.Directory.CreateDirectory(outputFolder);
        string json = BuildJson(simulatedSeconds, wallSeconds);
        System.IO.File.WriteAllText(CsvPath, BuildCsv());
        System.IO.File.WriteAllText(JsonPath, json);
        Debug.Log("IMPACT_CSV " + CsvPath);
        Debug.Log("IMPACT_SUMMARY " + json);
    }
}
