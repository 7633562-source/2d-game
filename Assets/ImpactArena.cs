using System;
using System.Globalization;
using UnityEngine;

// ─── BALANCE-ARENA-1: THE SCENE HOST ───
//
// The host component of `Assets/Scenes/BalanceArena.unity`. Built like WalkArena:
// the world is spawned at runtime and the scene holds nothing but a Main Camera and
// this component.
//
// Open the scene and press Play: the ground is built, one human stands on it, and at
// `firstThrowAt` the thrower sends the first of `throwCount` real Rigidbody2D bodies
// at the aimed body part. The run writes the same per-throw record as the headless
// harness (`Trials/Runs/<label>-impacts.json` / `.csv`), so the numbers Yury sees in
// the editor and the numbers in the report come from one implementation.
//
// The human is built with the rig defaults, exactly like the headless stand: no
// genome, no build override, no scripted force on any joint. The only agent acting on
// him is the solver's collision response to a thrown body.
public class ImpactArena : MonoBehaviour
{
    [Header("Run")]
    public bool runOnStart = true;
    public string label = "impact_play";
    public float durationSeconds = 24f;
    public float fixedDelta = 0.005f;
    public float startY = -0.82f;
    public float groundTopY = -2f;
    public float groundMeters = 40f;
    public string outputFolder = "E:/Games/2D Game/My project/Trials/Runs";
    public bool writeFiles = true;

    [Header("Camera (static: the whole man and the flight path stay in frame)")]
    public float cameraSize = 2.4f;
    public float cameraY = -1.0f;
    public Color clearColor = new Color(0.19f, 0.30f, 0.47f, 1f);

    [Header("Thrower")]
    public float impactMass = 4f;
    public string impactShape = "circle";
    public float impactSize = 0.25f;
    public float impactRectAspect = 2f;
    public float impactSpeed = 8f;
    public string impactAim = "torso";
    public string impactSide = "seed";
    public float impactAt = 8f;
    public int impactCount = 3;
    public float impactSpacing = 6f;
    public int impactSeed = 1;

    private Human human;
    private ImpactThrower thrower;
    private ImpactRecorder recorder;
    private bool begun;
    private float simulated;
    private bool written;

    private void Awake()
    {
        if (fixedDelta > 0f)
            Time.fixedDeltaTime = fixedDelta;
    }

    private void Start()
    {
        if (!runOnStart) return;
        Build();
    }

    public void Build()
    {
        if (begun) return;
        begun = true;

        GameObject groundHost = new GameObject("ArenaGround");
        GroundBuilder ground = groundHost.AddComponent<GroundBuilder>();
        ground.groundSize = new Vector2(Mathf.Max(20f, groundMeters), 2f);
        ground.groundPosition = new Vector2(0f, groundTopY - 1f);
        ground.BuildGround();

        GameObject humanObject = new GameObject("TrialHuman");
        humanObject.transform.position = new Vector2(0f, startY);
        human = humanObject.AddComponent<Human>();
        humanObject.AddComponent<MotionIntent>();
        StepPhaseDriver stepDriver = humanObject.AddComponent<StepPhaseDriver>();
        stepDriver.externalDrive = true;
        humanObject.AddComponent<PlayerInputSource>();
        human.BuildHuman();
        FactionStamp.Player(humanObject);

        GameObject throwerHost = new GameObject("ImpactThrower");
        thrower = throwerHost.AddComponent<ImpactThrower>();
        thrower.massKg = impactMass;
        thrower.shapeText = impactShape;
        thrower.sizeMeters = impactSize;
        thrower.rectAspect = impactRectAspect;
        thrower.speedMps = impactSpeed;
        thrower.aimPart = impactAim;
        thrower.sideText = impactSide;
        thrower.firstThrowAt = impactAt;
        thrower.throwCount = impactCount;
        thrower.spacingSeconds = impactSpacing;
        thrower.seed = impactSeed;
        thrower.Configure(human);

        recorder = new ImpactRecorder(human, thrower, label, outputFolder, string.Empty,
            Application.buildGUID, startY, groundTopY);

        FrameCamera();
        Debug.Log("IMPACT_ARENA begun: " + impactCount.ToString(CultureInfo.InvariantCulture)
            + " throw(s), aim " + impactAim + ", shape " + impactShape
            + " " + impactSize.ToString("0.###", CultureInfo.InvariantCulture) + " m, mass "
            + impactMass.ToString("0.###", CultureInfo.InvariantCulture) + " kg, speed "
            + impactSpeed.ToString("0.###", CultureInfo.InvariantCulture) + " m/s, seed "
            + impactSeed.ToString(CultureInfo.InvariantCulture));
    }

    private void FrameCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        if (cam == null) return;
        // A static wide lens: the man, the whole flight path and the fall all stay in
        // one frame, so the video can be judged without a moving camera.
        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow != null) follow.enabled = false;
        cam.orthographic = true;
        cam.orthographicSize = cameraSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = clearColor;
        cam.transform.position = new Vector3(0f, cameraY, -10f);
    }

    private void FixedUpdate()
    {
        if (!begun) return;
        simulated += Time.fixedDeltaTime;
        thrower.Advance(simulated);
        recorder.Sample(simulated);
        if (!written && simulated >= durationSeconds)
        {
            written = true;
            if (writeFiles)
                recorder.WriteFiles(simulated, Time.realtimeSinceStartup);
        }
    }

    // A hand-run still needs the files when the editor is stopped early.
    private void OnApplicationQuit()
    {
        if (!begun || written || !writeFiles) return;
        written = true;
        recorder.WriteFiles(simulated, Time.realtimeSinceStartup);
    }
}
