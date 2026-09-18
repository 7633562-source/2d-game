using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Globalization;

// Play-mode frame capture to compare "in-game" behavior with headless trials.
// Saves PNG frames and a small metadata file to Trials/Captures/<session>.
public class PlayCaptureRecorder : MonoBehaviour
{
    [Header("Capture control")]
    [Tooltip("Enable capture by creating Tools/force-play-capture.flag.")]
    public bool armWithFlag = true;
    [Tooltip("Start one capture session automatically on the first walk input.")]
    public bool autoStartOnWalk = true;
    [Tooltip("Toggle capture with F8 in Play Mode.")]
    public bool allowKeyboardToggle = true;

    [Header("Capture sampling")]
    [Tooltip("Seconds between screenshots in Play Mode.")]
    public float captureInterval = 0.12f;
    [Tooltip("Safety cap for a single session.")]
    public int maxFrames = 900;
    [Tooltip("Save a cropped view around the human instead of full screen.")]
    public bool zoomHumanInCapture = true;
    [Tooltip("Capture crop width fraction of screen.")]
    [Range(0.25f, 1f)]
    public float captureWidthFraction = 0.55f;
    [Tooltip("Capture crop height fraction of screen.")]
    [Range(0.25f, 1f)]
    public float captureHeightFraction = 0.70f;
    [Tooltip("Vertical world offset for capture center.")]
    public float captureCenterYOffset = 0.35f;
    [Tooltip("Draw an on-screen timecode so each image has clear timing.")]
    public bool drawTimeOverlay = true;
    [Tooltip("Font size for capture timecode overlay.")]
    public int overlayFontSize = 20;

    private const string CaptureArmFlag = "force-play-capture.flag";
    private MotionIntent intent;
    private StepPhaseDriver stepDriver;
    private string capturesRoot;
    private string currentSessionDir;
    private float nextCaptureTime;
    private int frameIndex;
    private bool capturing;
    private bool walkSessionStarted;
    private float flagPollNextTime;
    private bool activeSessionNeedsFlag;
    private float captureStartTime;
    private float captureStartUnscaledTime;
    private GUIStyle overlayStyle;
    private readonly CultureInfo invariantCulture = CultureInfo.InvariantCulture;
    private bool captureQueued;
    private Camera captureCamera;
    private Transform captureTarget;
    private float captureStartX;
    private float prevTargetX;
    private float prevTargetTime;
    private float targetSpeedX;

    void Awake()
    {
        intent = FindFirstObjectByType<MotionIntent>();
        stepDriver = FindFirstObjectByType<StepPhaseDriver>();
        capturesRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Trials", "Captures"));
        captureCamera = Camera.main;
        captureTarget = intent != null ? intent.transform : null;
    }

    void Update()
    {
        if (HeadlessTrial.Active)
            return;

        PollFlag();
        PollWalkAutostart();
        PollKeyboard();
    }

    void LateUpdate()
    {
        if (!capturing)
            return;
        if (Time.unscaledTime < nextCaptureTime)
            return;
        if (captureQueued)
            return;
        StartCoroutine(CaptureFrameEndOfFrame());
    }

    void OnDisable()
    {
        if (capturing)
            StopCapture("component disabled");
    }

    private void PollFlag()
    {
        if (!armWithFlag)
            return;
        if (Time.unscaledTime < flagPollNextTime)
            return;
        flagPollNextTime = Time.unscaledTime + 0.25f;

        string flagPath = Path.Combine(Application.dataPath, "..", "Tools", CaptureArmFlag);
        bool armed = File.Exists(flagPath);
        if (armed && !capturing)
            StartCapture("flag", true);
        else if (!armed && capturing && activeSessionNeedsFlag)
            StopCapture("flag removed");
    }

    private void PollWalkAutostart()
    {
        if (!autoStartOnWalk || walkSessionStarted || capturing)
            return;
        EnsureRefs();
        if (intent == null || stepDriver == null)
            return;

        bool walkRequested = Mathf.Abs(intent.moveX) > 0.01f && stepDriver.walkActive;
        if (!walkRequested)
            return;

        walkSessionStarted = true;
        StartCapture("walk", false);
    }

    private void EnsureRefs()
    {
        if (intent == null)
            intent = FindFirstObjectByType<MotionIntent>();
        if (stepDriver == null)
            stepDriver = FindFirstObjectByType<StepPhaseDriver>();
        if (captureTarget == null && intent != null)
            captureTarget = intent.transform;
        if (captureCamera == null)
            captureCamera = Camera.main;
    }

    private void PollKeyboard()
    {
        if (!allowKeyboardToggle)
            return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;
        if (!keyboard.f8Key.wasPressedThisFrame)
            return;

        if (capturing)
            StopCapture("keyboard");
        else
            StartCapture("keyboard", false);
    }

    private void StartCapture(string reason, bool needsFlag)
    {
        Directory.CreateDirectory(capturesRoot);
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        currentSessionDir = Path.Combine(capturesRoot, $"{stamp}_{SceneManager.GetActiveScene().name}_{reason}");
        Directory.CreateDirectory(currentSessionDir);

        frameIndex = 0;
        capturing = true;
        activeSessionNeedsFlag = needsFlag;
        captureStartTime = Time.time;
        captureStartUnscaledTime = Time.unscaledTime;
        EnsureRefs();
        if (captureTarget != null)
        {
            captureStartX = captureTarget.position.x;
            prevTargetX = captureTarget.position.x;
            prevTargetTime = Time.time;
            targetSpeedX = 0f;
        }
        nextCaptureTime = Time.unscaledTime;
        WriteMeta("started", reason);
        Debug.Log($"PlayCaptureRecorder: start {currentSessionDir}");
    }

    private void StopCapture(string reason)
    {
        capturing = false;
        activeSessionNeedsFlag = false;
        WriteMeta("stopped", reason);
        Debug.Log($"PlayCaptureRecorder: stop ({reason}), frames={frameIndex}, dir={currentSessionDir}");
    }

    private IEnumerator CaptureFrameEndOfFrame()
    {
        captureQueued = true;
        yield return new WaitForEndOfFrame();
        if (capturing)
            CaptureFrame();
        captureQueued = false;
    }

    private void CaptureFrame()
    {
        if (frameIndex >= Mathf.Max(1, maxFrames))
        {
            StopCapture("maxFrames");
            return;
        }

        EnsureRefs();
        RectInt captureRect = ResolveCaptureRect();
        Texture2D tex = new Texture2D(captureRect.width, captureRect.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(captureRect.x, captureRect.y, captureRect.width, captureRect.height), 0, 0, false);
        tex.Apply(false, false);
        byte[] png = tex.EncodeToPNG();
        Destroy(tex);

        string tStamp = Time.time.ToString("0000.000", invariantCulture);
        string path = Path.Combine(currentSessionDir, $"frame_{frameIndex:0000}_t{tStamp}.png");
        File.WriteAllBytes(path, png);
        frameIndex++;
        nextCaptureTime = Time.unscaledTime + Mathf.Max(0.01f, captureInterval);
    }

    private RectInt ResolveCaptureRect()
    {
        int fullW = Screen.width;
        int fullH = Screen.height;
        if (!zoomHumanInCapture || captureCamera == null || captureTarget == null)
            return new RectInt(0, 0, fullW, fullH);

        int w = Mathf.Clamp(Mathf.RoundToInt(fullW * Mathf.Clamp01(captureWidthFraction)), 256, fullW);
        int h = Mathf.Clamp(Mathf.RoundToInt(fullH * Mathf.Clamp01(captureHeightFraction)), 180, fullH);

        Vector3 worldCenter = captureTarget.position + new Vector3(0f, captureCenterYOffset, 0f);
        Vector3 screenPoint = captureCamera.WorldToScreenPoint(worldCenter);
        float cx = Mathf.Clamp(screenPoint.x, 0f, fullW);
        float cy = Mathf.Clamp(screenPoint.y, 0f, fullH);

        int x = Mathf.RoundToInt(Mathf.Clamp(cx - w * 0.5f, 0f, fullW - w));
        int y = Mathf.RoundToInt(Mathf.Clamp(cy - h * 0.5f, 0f, fullH - h));
        return new RectInt(x, y, w, h);
    }

    private void WriteMeta(string state, string reason)
    {
        if (string.IsNullOrEmpty(currentSessionDir))
            return;

        string metaPath = Path.Combine(currentSessionDir, "meta.txt");
        string text =
            $"state={state}\n" +
            $"reason={reason}\n" +
            $"capturedUtc={DateTime.UtcNow:O}\n" +
            $"scene={SceneManager.GetActiveScene().name}\n" +
            $"resolution={Screen.width}x{Screen.height}\n" +
            $"zoomHumanInCapture={zoomHumanInCapture}\n" +
            $"captureWidthFraction={captureWidthFraction.ToString("0.###", invariantCulture)}\n" +
            $"captureHeightFraction={captureHeightFraction.ToString("0.###", invariantCulture)}\n" +
            $"fixedDelta={Time.fixedDeltaTime:0.000###}\n" +
            $"targetFrameRate={Application.targetFrameRate}\n" +
            $"frames={frameIndex}\n";
        File.WriteAllText(metaPath, text);
    }

    void OnGUI()
    {
        if (!drawTimeOverlay || !capturing)
            return;
        EnsureRefs();

        if (overlayStyle == null)
        {
            overlayStyle = new GUIStyle(GUI.skin.label);
            overlayStyle.normal.textColor = Color.white;
            overlayStyle.fontStyle = FontStyle.Bold;
        }
        overlayStyle.fontSize = Mathf.Clamp(overlayFontSize, 12, 40);

        float t = Time.time;
        float dtFromStart = t - captureStartTime;
        float unscaled = Time.unscaledTime - captureStartUnscaledTime;
        float rootX = 0f;
        float travelX = 0f;
        if (captureTarget != null)
        {
            rootX = captureTarget.position.x;
            travelX = rootX - captureStartX;
            float dt = Mathf.Max(0.0001f, t - prevTargetTime);
            targetSpeedX = (rootX - prevTargetX) / dt;
            prevTargetX = rootX;
            prevTargetTime = t;
        }
        string phaseText = "n/a";
        string swapText = "n/a";
        if (stepDriver != null)
        {
            phaseText = stepDriver.PhaseCode == 0 ? "stance" : "transfer";
            swapText = stepDriver.SwapCount.ToString(invariantCulture);
        }

        string text =
            "CAPTURE TIMECODE\n" +
            $"sim={t.ToString("0.000", invariantCulture)}s  fromStart={dtFromStart.ToString("0.000", invariantCulture)}s  unscaled={unscaled.ToString("0.000", invariantCulture)}s\n" +
            $"frame={frameIndex.ToString(invariantCulture)}  phase={phaseText}  swaps={swapText}\n" +
            $"rootX={rootX.ToString("0.###", invariantCulture)}  travelX={travelX.ToString("0.###", invariantCulture)}  speedX={targetSpeedX.ToString("0.###", invariantCulture)} m/s";

        Rect rect = new Rect(12f, 48f, Screen.width - 24f, 118f);
        DrawOutlinedLabel(rect, text, overlayStyle);
    }

    private static void DrawOutlinedLabel(Rect rect, string text, GUIStyle style)
    {
        Color old = GUI.color;
        GUI.color = Color.black;
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x - 1f, rect.y - 1f, rect.width, rect.height), text, style);
        GUI.color = old;
        GUI.Label(rect, text, style);
    }
}
