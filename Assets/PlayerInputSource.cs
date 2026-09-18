using UnityEngine;
using UnityEngine.InputSystem;

// The only intent source that reads the keyboard. Lives only on
// the human being played: the rest get intent from their own AI.
//
// Uses low-level Keyboard access, not an InputActions asset:
// the project has no prefabs and no hand-tuned assets; the whole scene
// is built in code. The project is on the new Input System
// (activeInputHandler = 1), so the old UnityEngine.Input would throw
// here.
//
// In a windowless run Keyboard.current is null. That is not enough to
// stay quiet any more: since this component drives StepPhaseDriver, the
// no-key branch writes zero intent and calls EndWalk, which on the stand
// wipes what HeadlessTrial just wrote. Update returns early whenever the
// walk clock belongs to someone else (HeadlessTrial.Active or
// StepPhaseDriver.externalDrive). Walk in the
// browser also has an on-screen Hold button: WebGL often reports
// Keyboard.current as null until the canvas is clicked, and isPressed
// can drop mid-hold, which used to ArmWalk then EndWalk on the next
// frame and look like a fall.
[RequireComponent(typeof(MotionIntent))]
[RequireComponent(typeof(StepPhaseDriver))]
public class PlayerInputSource : MonoBehaviour
{
    private MotionIntent intent;
    private StepPhaseDriver stepDriver;
    private BalanceController balance;

    private const float DebugFlagPollInterval = 0.2f;
    private string leanFlagPath;
    private string runFlagPath;
    private string crouchFlagPath;
    private string standFlagPath;
    private float nextDebugFlagPollTime;
    private bool leanFlagActive;
    private float leanFlagValue;
    private bool runFlagActive;
    private bool crouchFlagActive;
    private bool standFlagActive;
    private float standFlagValue;

    // Held walk from key-down / key-up edges. Trusting isPressed alone
    // in WebGL starts a step for one frame and then EndWalk, which
    // dumps the Fall/Catch automaton and the body folds.
    private bool keyboardWalkHeld;
    private bool uiWalkHeld;
    private GUIStyle walkButtonStyle;

    void Awake()
    {
        intent = GetComponent<MotionIntent>();
        stepDriver = GetComponent<StepPhaseDriver>();
        balance = GetComponent<BalanceController>();

#if !(UNITY_WEBGL && !UNITY_EDITOR)
        // Debug flags are an agent input channel; cache paths and keep disk I/O off the render-rate path.
        // The browser has no Tools/ folder; File.Exists there can throw and abort Update.
        string toolsPath = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(Application.dataPath, "..", "Tools"));
        leanFlagPath = System.IO.Path.Combine(toolsPath, "force-lean.flag");
        runFlagPath = System.IO.Path.Combine(toolsPath, "force-run.flag");
        crouchFlagPath = System.IO.Path.Combine(toolsPath, "force-crouch.flag");
        standFlagPath = System.IO.Path.Combine(toolsPath, "force-stand-leg.flag");
#endif
    }

    // Polling is in Update, not FixedUpdate: key state belongs to the
    // render frame. For a held key it does not matter, but at 200 Hz
    // physics the habit of reading input in FixedUpdate will one day
    // bite on "pressed this frame" events.
    void Update()
    {
        // The stand is the intent source in a trial: HeadlessTrial writes
        // crouch / lean / standLeg and owns BeginWalk + Tick. Keyboard.current
        // is null there, so every branch below would fall through to "no key":
        // EndWalk on the driver plus standLeg / moveX / crouch / lean = 0,
        // one render frame after the trial loop wrote them. walk2_sync lost
        // the whole gait that way (standLegLevel 0, 0 cycles) while the CSV
        // still showed standLegCmd 1, because the wipe lands between the
        // trial write and the next FixedUpdate.
        if (HeadlessTrial.Active || (stepDriver != null && stepDriver.externalDrive))
            return;

        Keyboard keyboard = Keyboard.current;
#if !(UNITY_WEBGL && !UNITY_EDITOR)
        if (keyboard != null)
            PollDebugFlags();
#endif

        UpdateKeyboardWalkLatch(keyboard);

        // ↑/W/Space: walk. Run requires Shift+W to avoid accidental run
        // latch when the player only tries to step. The on-screen Walk
        // button is the WebGL path when the keyboard device is missing.
        bool moveDown = keyboardWalkHeld || uiWalkHeld;
        bool sprintDown = keyboard != null
            && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);

        // The human faces right: ←/A — lean back, →/D — lean forward. This is
        // a torso lean, not a step: these keys used to arm walk via moveX.
        // Step takes priority over lean: the habitual "W together with D"
        // used to silently cancel walk, and the body only tilted without
        // ever lifting a leg.
        float lean = 0f;
        if (keyboard != null)
        {
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) lean += 1f;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) lean -= 1f;
        }
        if (leanFlagActive)
            lean = leanFlagValue;
        if (moveDown)
            lean = 0f;
        intent.lean = Mathf.Clamp(lean, -1f, 1f);

        float move = 0f;
        float run = 0f;
        if (moveDown)
        {
            move = 1f;
            if (sprintDown || runFlagActive)
                run = 1f;
        }
        intent.moveX = move;
        intent.run = run;

        bool crouching = keyboard != null
            && (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed);
        // Flag for a keyboard-free crouch check (agent / stand UI):
        // Tools/force-crouch.flag — hold the crouch while the file exists.
        if (crouchFlagActive)
            crouching = true;
        intent.crouch = crouching ? 1f : 0f;

        // Q/E — manual one-leg stance; takes priority over the step automaton.
        float stand = 0f;
        bool leftStance = keyboard != null && keyboard.qKey.isPressed;
        bool rightStance = keyboard != null && keyboard.eKey.isPressed;
        bool manualStand = false;
        if (leftStance && !rightStance)
        {
            stand = -1f;
            manualStand = true;
        }
        else if (rightStance && !leftStance)
        {
            stand = 1f;
            manualStand = true;
        }
        if (standFlagActive)
        {
            stand = standFlagValue;
            manualStand = true;
        }

        if (manualStand)
        {
            if (stepDriver != null)
                stepDriver.EndWalk();
            intent.standLeg = stand;
            intent.run = 0f;
        }
        else if (Mathf.Abs(intent.lean) > 0.01f)
        {
            // Lean without ↑/W: a leaning stance, no step requested.
            if (stepDriver != null)
                stepDriver.EndWalk();
            intent.standLeg = 0f;
            intent.moveX = 0f;
            intent.run = 0f;
        }
        else if (Mathf.Abs(move) > 0.01f && stepDriver != null)
        {
            // ArmWalk = BeginWalk + Tick, the same pair HeadlessTrial uses.
            if (!stepDriver.walkActive)
                stepDriver.ArmWalk();
        }
        else
        {
            if (stepDriver != null)
                stepDriver.EndWalk();
            intent.standLeg = 0f;
            intent.run = 0f;
        }
    }

    private void UpdateKeyboardWalkLatch(Keyboard keyboard)
    {
        if (keyboard == null)
        {
            keyboardWalkHeld = false;
            return;
        }

        bool pressed = WalkKeyPressed(keyboard);
        bool down = keyboard.wKey.wasPressedThisFrame
            || keyboard.upArrowKey.wasPressedThisFrame
            || keyboard.spaceKey.wasPressedThisFrame;
        bool up = keyboard.wKey.wasReleasedThisFrame
            || keyboard.upArrowKey.wasReleasedThisFrame
            || keyboard.spaceKey.wasReleasedThisFrame;

        if (down || pressed)
            keyboardWalkHeld = true;
        else if (up)
            keyboardWalkHeld = false;
    }

    private static bool WalkKeyPressed(Keyboard keyboard)
    {
        return keyboard.wKey.isPressed
            || keyboard.upArrowKey.isPressed
            || keyboard.spaceKey.isPressed;
    }

    private void PollDebugFlags()
    {
        if (string.IsNullOrEmpty(leanFlagPath)) return;

        float now = Time.unscaledTime;
        if (now < nextDebugFlagPollTime) return;
        nextDebugFlagPollTime = now + DebugFlagPollInterval;

        leanFlagActive = System.IO.File.Exists(leanFlagPath);
        if (leanFlagActive)
        {
            string raw = System.IO.File.ReadAllText(leanFlagPath).Trim().ToLowerInvariant();
            if (raw == "forward" || raw == "fwd" || raw == "right" || raw == "1" || raw == "+")
                leanFlagValue = 1f;
            else if (raw == "back" || raw == "backward" || raw == "left" || raw == "-1" || raw == "-")
                leanFlagValue = -1f;
            else
                leanFlagValue = 0f;
        }

        runFlagActive = System.IO.File.Exists(runFlagPath);
        crouchFlagActive = System.IO.File.Exists(crouchFlagPath);
        standFlagActive = System.IO.File.Exists(standFlagPath);
        if (standFlagActive)
        {
            string raw = System.IO.File.ReadAllText(standFlagPath).Trim().ToLowerInvariant();
            standFlagValue = raw == "right" || raw == "r" || raw == "1" ? 1f : -1f;
        }
    }

    void OnGUI()
    {
        if (HeadlessTrial.Active) return;
        DrawWalkButton();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DrawDebugOverlay();
#endif
    }

    private void DrawWalkButton()
    {
        if (walkButtonStyle == null)
        {
            walkButtonStyle = new GUIStyle(GUI.skin.button);
            walkButtonStyle.fontSize = 22;
            walkButtonStyle.fontStyle = FontStyle.Bold;
        }

        float btnW = 180f;
        float btnH = 56f;
        Rect walkRect = new Rect(
            Screen.width * 0.5f - btnW * 0.5f,
            Screen.height - btnH - 24f,
            btnW,
            btnH);
        bool pressed = GUI.RepeatButton(walkRect, uiWalkHeld ? "Walking" : "Walk", walkButtonStyle);
        // Layout events must not clear the hold: RepeatButton returns
        // false on Layout even while the mouse is down.
        if (Event.current.type != EventType.Layout)
            uiWalkHeld = pressed;
    }

    // Play-mode hint: shows that the key press reached lean / run / walk.
    private void DrawDebugOverlay()
    {
        if (intent == null) return;
        if (balance == null)
            balance = GetComponent<BalanceController>();
        float level = balance != null ? balance.leanLevel : 0f;
        string pose =
            intent.run > 0.5f ? "RUN" :
            intent.lean > 0.5f ? "FORWARD" :
            intent.lean < -0.5f ? "BACK" :
            intent.crouch > 0.5f ? "CROUCH" :
            Mathf.Abs(intent.moveX) > 0.01f ? "STEP" : "STAND";
        // Play walks the fall-catch FSM (Plant/Fall/Reach are PhaseCode 0).
        // Counting down walkStanceLiftDelay there is the legacy 8 s Stance
        // clock: Fall is already the lift, and "lift in 6.0 s" reads as a
        // lost key. The 8 s line stays for useFallCatchWalk=0 only.
        string walkLine = "walk off";
        if (stepDriver != null && stepDriver.walkActive)
        {
            float age = stepDriver.PhaseAge();
            float standLevel = balance != null ? balance.standLegLevel : 0f;
            string phaseText;
            int fc = stepDriver.FallCatchPhaseCode;
            if (balance != null
                && (balance.locomotionState == BalanceController.LocomotionState.GettingUp
                    || balance.locomotionState == BalanceController.LocomotionState.Fallen))
            {
                phaseText = balance.locomotionState
                    == BalanceController.LocomotionState.GettingUp
                    ? "getting up"
                    : "fallen";
            }
            else if (balance != null && balance.useCaptureWalker != 0)
            {
                switch (balance.CaptureWalkStateCode)
                {
                    case 1:
                        phaseText = "capture swing";
                        break;
                    case 2:
                        phaseText = "capture touchdown";
                        break;
                    case 3:
                        phaseText = "capture recovery";
                        break;
                    default:
                        phaseText = "capture double support";
                        break;
                }
                age = balance.CaptureWalkStateAge;
            }
            else if (stepDriver.useFallCatchWalk != 0 && fc >= 0)
            {
                switch ((StepPhaseDriver.FallCatchPhase)fc)
                {
                    case StepPhaseDriver.FallCatchPhase.Plant:
                        phaseText = "plant, double support";
                        break;
                    case StepPhaseDriver.FallCatchPhase.Fall:
                        phaseText = "fall, lifting the swing";
                        break;
                    case StepPhaseDriver.FallCatchPhase.Reach:
                        phaseText = "reach, placing the foot";
                        break;
                    case StepPhaseDriver.FallCatchPhase.Catch:
                        phaseText = "catch";
                        break;
                    default:
                        phaseText = "recover";
                        break;
                }
            }
            else
            {
                bool stance = stepDriver.PhaseCode == 0;
                float liftAt = balance == null ? 0f
                    : (intent.run > 0.5f ? balance.runStanceLiftDelay : balance.walkStanceLiftDelay);
                phaseText = stance
                    ? (age < liftAt
                        ? $"stance, weight transfer, lift in {Mathf.Max(0f, liftAt - age):0.0} s"
                        : "stance, leg unloading")
                    : "leg swap";
            }
            // Plant holds double support until the CoM has rolled over the
            // stance foot: releasing the swing earlier is a fall backward,
            // not a step. A deliberate wait must not read as a lost key press.
            // Only the fall-catch Plant sets this flag (legacy Stance does not).
            // Capture walker does not write these bits (Tick returns).
            bool gettingUp = balance != null
                && (balance.locomotionState
                    == BalanceController.LocomotionState.GettingUp
                    || balance.locomotionState
                    == BalanceController.LocomotionState.Fallen);
            if (!gettingUp && (balance == null || balance.useCaptureWalker == 0))
            {
                if (fc >= 0 && stepDriver.PlantWaitingForLead)
                    phaseText = "plant, rolling CoM over the foot";
                if (stepDriver.StandingSurrender)
                    phaseText = "recovering, standing on both feet";
                else if (stepDriver.StandingRestore)
                    phaseText = "recovering, holding the planted foot";
            }
            int captureSteps = balance != null && balance.useCaptureWalker != 0
                ? balance.CaptureWalkStepCount
                : stepDriver.SwapCount;
            walkLine = $"{phaseText}  phase={age:0.0} s  unload={standLevel:0.00}  swaps={captureSteps}";
        }

        GUI.Label(
            new Rect(12f, 12f, 720f, 82f),
            $"Hold W / ↑ / Space or Walk  ←/A back  →/D lean  Shift+W run  ↓/S crouch\n" +
            $"{pose}  lean={intent.lean:0}  level={level:0.00}  run={intent.run:0}\n" +
            walkLine);
    }
}
