using UnityEngine;

// Слой намерения: одноногая опора по таймеру, перенос — по условию (обе стопы,
// CoM по центру). BalanceController не трогаем.
[DefaultExecutionOrder(-50)]
public class StepPhaseDriver : MonoBehaviour
{
    private enum Phase
    {
        Stance,
        Transfer
    }

    [Header("Опора")]
    // Keep in sync with HeadlessTrial -walkStance default (8). Shorter Play
    // values (4) fold on the stand; do not lower without a trial.
    [Tooltip("Минимум секунд на одной ноге перед запросом переноса.")]
    public float stanceDuration = 6f;

    // Бег (intent.run): короче опора — чаще смена ног. Не SIMBICON.
    [Tooltip("Секунд Stance при intent.run=1.")]
    public float runStanceDuration = 4f;

    // После первого отрыва: повторный grounded → Transfer, не ждать таймер.
    // Один кадр воздуха — отскок (heel_w1 fold). Нужен streak воздуха.
    // 0 = выкл (только таймер stanceDuration). CLI -walkHeelMin / -walkHeelAir.
    [Tooltip("Мин. age Stance (с) после квалифицированного воздуха → Transfer.")]
    public float stanceHeelStrikeMinAge = 0f;
    [Tooltip("Мин. непрерывный !swingGrounded (с) до квалификации heel-strike. 0 = выкл.")]
    public float stanceHeelAirMin = 0f;

    // Ходьба — падение вперёд с подставлением ноги, а не переступание по
    // будильнику. Пока Stance держит таймер, тело при walkComLeadX успевает
    // уйти за опорную стопу и падает лицом (lead_003…lead_015: torso ~92°,
    // pelvisDrop 0.86). Смена опоры должна следовать за телом: CoM ушёл
    // вперёд опорной стопы на stanceComTrigger — пора ставить вторую ногу.
    // Это остаётся условием внутри драйвера фаз, а не отдельный конечный
    // автомат: SIMBICON не открываем. 0 = только таймер, прежнее поведение.
    [Tooltip("CoM впереди опорной стопы (м) → Transfer. 0 = только таймер.")]
    public float stanceComTrigger = 0.01f;

    [Tooltip("Мин. возраст Stance (с) до срабатывания по CoM.")]
    public float stanceComMinAge = 0.25f;

    [Tooltip("Первая фаза: +1 правая опора, −1 левая.")]
    public float firstStance = 1f;

    [Header("Перенос (event-gated)")]
    [Tooltip("Максимум секунд в двойной опоре — потом мягкий fallback.")]
    public float transferMaxDuration = 6f;

    [Tooltip("Макс. Transfer при intent.run=1.")]
    public float runTransferMaxDuration = 4f;

    [Tooltip("Не проверять готовность раньше — intent=0 должен дойти до BC.")]
    public float transferMinDuration = 0.7f;

    [Tooltip("Обе стопы grounded и |CoM offset| ≤ этого — жёсткая готовность.")]
    public float transferComOffsetMax = 0.10f;

    [Tooltip("Мягкий fallback после transferMaxDuration.")]
    public float transferFallbackComMax = 0.12f;

    [Tooltip("Legacy: в IsTransferReady больше не используется (отставание BC).")]
    public float transferStandLegLevelMax = 0.05f;

    public bool walkActive;

    // Счётчики для сводки: без них CSV «готов», а смена не видна.
    public int SwapCount { get; private set; }
    public int TransferReadyFrames { get; private set; }
    public int PhaseCode => phase == Phase.Transfer ? 1 : 0;
    public float CurrentStanceSign => currentStance;
    // Возраст в часах последнего Tick (стенд — simulated, Play — fixedTime).
    // Balance раньше брал Time.fixedTime при Tick(sim) — разные оси.
    public float PhaseAge() => lastTickTime - phaseEnterTime;

    private MotionIntent intent;
    private BodyStateEstimator bodyState;
    private CenterOfMassCalculator comCalculator;

    private float walkStartTime = -1f;
    private bool pendingBegin;
    private Phase phase;
    private float phaseEnterTime;
    private float lastTickTime;
    private float currentStance;
    // Stance: был ли хотя бы один кадр !grounded у свинга (для heel-strike).
    private bool swingWasAirborne;
    private float swingAirStreak;
    private bool swingAirQualified;
    private float prevTickTime = -1f;

    void Awake()
    {
        // BodyState / CoM появляются в BuildHuman после AddComponent —
        // здесь их ещё нет. Подхватываем лениво в EnsureRefs.
        intent = GetComponent<MotionIntent>();
    }

    private void EnsureRefs()
    {
        if (intent == null)
            intent = GetComponent<MotionIntent>();
        if (bodyState == null)
            bodyState = GetComponent<BodyStateEstimator>();
        if (comCalculator == null)
            comCalculator = GetComponent<CenterOfMassCalculator>();
    }

    // В headless Tick вызывает только HeadlessTrial (до физики).
    // FixedUpdate — для Play Mode / ArmWalk.
    public bool externalDrive;

    void FixedUpdate()
    {
        if (HeadlessTrial.Active || externalDrive) return;
        if (pendingBegin)
        {
            walkStartTime = Time.fixedTime;
            pendingBegin = false;
            lastTickTime = Time.fixedTime;
            BeginCycle(Time.fixedTime);
        }
        if (!walkActive || intent == null) return;
        Tick(Time.fixedTime);
    }

    public void ArmWalk()
    {
        walkActive = true;
        pendingBegin = true;
    }

    public void BeginWalk(float simTime)
    {
        walkActive = true;
        pendingBegin = false;
        walkStartTime = simTime;
        lastTickTime = simTime;
        BeginCycle(simTime);
    }

    public void EndWalk()
    {
        walkActive = false;
        pendingBegin = false;
        walkStartTime = -1f;
        if (intent != null)
            intent.standLeg = 0f;
    }

    private void BeginCycle(float simTime)
    {
        phase = Phase.Stance;
        phaseEnterTime = simTime;
        currentStance = firstStance;
        swingWasAirborne = false;
        swingAirStreak = 0f;
        swingAirQualified = false;
        prevTickTime = -1f;
        SwapCount = 0;
        TransferReadyFrames = 0;
    }

    public void Tick(float simTime)
    {
        EnsureRefs();
        if (!walkActive || intent == null || walkStartTime < 0f)
            return;

        lastTickTime = simTime;

        if (simTime < walkStartTime)
        {
            intent.standLeg = 0f;
            return;
        }

        float phaseAge = simTime - phaseEnterTime;

        if (phase == Phase.Stance)
        {
            intent.standLeg = currentStance;
            float dt = prevTickTime < 0f ? 0f : Mathf.Max(0f, simTime - prevTickTime);
            prevTickTime = simTime;

            bool swingGrounded = IsSwingFootGrounded();
            if (!swingGrounded)
            {
                swingWasAirborne = true;
                swingAirStreak += dt;
                if (stanceHeelAirMin > 0.001f && swingAirStreak >= stanceHeelAirMin)
                    swingAirQualified = true;
            }
            else
            {
                // Квалифицированный воздух + age ≥ liftDelay → Transfer на посадке.
                bool useHeel = stanceHeelAirMin > 0.001f && stanceHeelStrikeMinAge > 0.001f;
                if (useHeel && swingAirQualified && phaseAge >= stanceHeelStrikeMinAge)
                {
                    EnterTransfer(simTime);
                    intent.standLeg = 0f;
                    return;
                }
                swingAirStreak = 0f;
            }

            // Тело уже впереди опоры — шаг просрочен, ждать таймер нельзя.
            if (stanceComTrigger > 0.0001f
                && phaseAge >= Mathf.Max(0.05f, stanceComMinAge)
                && StanceComLead() >= stanceComTrigger)
            {
                EnterTransfer(simTime);
                intent.standLeg = 0f;
                return;
            }

            if (phaseAge >= Mathf.Max(0.1f, ActiveStanceDuration()))
            {
                EnterTransfer(simTime);
                intent.standLeg = 0f;
            }
            return;
        }

        intent.standLeg = 0f;
        bool ready = IsTransferReady();
        if (ready)
            TransferReadyFrames++;
        if (phaseAge >= transferMinDuration && ready)
        {
            EnterStance(simTime, -currentStance);
            return;
        }
        if (phaseAge >= ActiveTransferMaxDuration() && IsTransferFallbackReady())
        {
            EnterStance(simTime, -currentStance);
            return;
        }
        // Walk only: if the swing stays airborne, BothFeet never becomes true and
        // Transfer never ends — Play looks like one lifted leg forever.
        // Run keeps the strict gate (walk-only unstick folded run in walk_air_run).
        if (!IsRunning() && phaseAge >= ActiveTransferMaxDuration() * 1.25f)
            EnterStance(simTime, -currentStance);
    }

    private bool IsRunning()
    {
        return intent != null && intent.run > 0.5f;
    }

    private float ActiveStanceDuration()
    {
        return IsRunning() ? Mathf.Max(0.5f, runStanceDuration) : stanceDuration;
    }

    private float ActiveTransferMaxDuration()
    {
        return IsRunning() ? Mathf.Max(0.5f, runTransferMaxDuration) : transferMaxDuration;
    }

    private void EnterTransfer(float simTime)
    {
        phase = Phase.Transfer;
        phaseEnterTime = simTime;
        swingWasAirborne = false;
        swingAirStreak = 0f;
        swingAirQualified = false;
        prevTickTime = -1f;
    }

    private void EnterStance(float simTime, float stance)
    {
        currentStance = stance;
        phase = Phase.Stance;
        phaseEnterTime = simTime;
        swingWasAirborne = false;
        swingAirStreak = 0f;
        swingAirQualified = false;
        prevTickTime = -1f;
        SwapCount++;
        if (intent != null)
            intent.standLeg = currentStance;
    }

    // currentStance +1 = опора справа → свинг слева; −1 наоборот.
    private bool IsSwingFootGrounded()
    {
        if (bodyState == null) return true;
        return currentStance > 0.5f
            ? bodyState.leftFootGrounded
            : bodyState.rightFootGrounded;
    }

    // Насколько CoM ушёл вперёд опорной стопы вдоль направления ходьбы.
    private float StanceComLead()
    {
        if (bodyState == null || comCalculator == null) return 0f;
        Vector2 com = comCalculator.GetCenterOfMass();
        Vector2 stanceFoot = currentStance > 0.5f
            ? bodyState.rightFootPosition
            : bodyState.leftFootPosition;
        float dir = intent != null && Mathf.Abs(intent.moveX) > 0.01f
            ? Mathf.Sign(intent.moveX)
            : 1f;
        return (com.x - stanceFoot.x) * dir;
    }

    private bool BothFeetGrounded()
    {
        return bodyState != null
            && bodyState.leftFootGrounded
            && bodyState.rightFootGrounded;
    }

    private float AbsComOffset()
    {
        return comCalculator != null ? Mathf.Abs(comCalculator.GetCoMOffsetX()) : float.PositiveInfinity;
    }

    private bool IsTransferReady()
    {
        if (bodyState == null || comCalculator == null)
            return false;
        if (!BothFeetGrounded())
            return false;
        // Только mid-offset: порог «CoM над следующей стопой» на cold
        // first→left не выполнялся (walk_swap_soft2 swap=0). Мягкий вход
        // после EnterStance — dualSupportSwingScale в BalanceController.
        return AbsComOffset() <= transferComOffsetMax;
    }

    private bool IsTransferFallbackReady()
    {
        if (bodyState == null || comCalculator == null)
            return false;
        if (!BothFeetGrounded())
            return false;
        return AbsComOffset() <= transferFallbackComMax;
    }
}
