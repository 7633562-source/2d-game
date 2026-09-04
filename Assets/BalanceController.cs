using UnityEngine;

// Балансировщик на основе центра масс.
[DefaultExecutionOrder(0)]
public class BalanceController : MonoBehaviour
{
    public enum BalanceState
    {
        Balancing,
        Recovery,
        Falling
    }

    [Header("Состояние системы")]
    public BalanceState currentState = BalanceState.Balancing;

    [Header("Пороги конечного автомата")]
    public float recoveryCoMOffset = 0.15f;
    public float fallCoMOffset = 0.30f;

    [Header("Коэффициенты балансировки")]
    // Входы нормированы: 10 см смещения и 30 °/с наклона дают единицу.
    // Раньше складывали метры и град/с — скорость торса сразу забивала сигнал.
    public float comOffsetReference = 0.10f;
    public float tiltSpeedReference = 30f;
    public float comVelocityReference = 0.30f;
    public float comProportionalGain = 1.2f;
    public float comDerivativeGain = 0.35f;
    public float maxBalanceSignal = 1.0f;

    [Header("Целевые углы (градусы)")]
    // Для бедра это уже не угол сустава, а целевой мировой наклон таза:
    // отрицательное — вперёд (по часовой, человек смотрит вправо).
    // Колено по-прежнему держит jointAngle. Пара −3/6 оставлена как рабочая
    // стойка: лёгкий наклон таза вперёд, голень под тазом при колене +6°.
    public float hipBaseAngle = -3f;
    public float kneeBaseAngle = 6f;
    public float kneeRecoveryFlex = 12f;
    public float hipBalanceGain = 8f;

    [Header("Присед")]
    // intent.crouch — цель 0 или 1, не сглаженная величина.
    // Иначе стенд с мгновенным crouch=1 обогнал бы удержание клавиши.
    public float crouchRatePerSecond = 1.5f;
    // Сгибание колена положительное: диапазон 0…120, стойка +6°.
    // Переснято после разворота колена: 40/32 даёт просадку 6.8 см и держит
    // поясницу на 17.3° из 20. Дальше есть провал (50/40 роняет), хотя 70/55
    // снова стоит и даёт 20.4 см. Пока таз не управляется, за 40/32 не
    // заходить: разница между «стоит» и «падает» там не про глубину.
    public float crouchKneeFlex = 85f;
    // Больше не входит в цель бедра: угол сустава таз–ляжка не держим.
    // Поле оставлено, чтобы стенд и инспектор не потеряли имя.
    public float crouchHipFlex = 32f;
    // Положительное — целевой наклон таза вперёд при crouch=1.
    // Вперёд = по часовой = минус к мировой цели, как у crouchTorsoLean.
    // Глубину даёт колено; таз только задаёт, насколько наклониться.
    public float crouchPelvisTilt = 24f;
    // Положительное — добавка наклона груди вперёд при crouch=1.
    // Человек смотрит вправо, вперёд = по часовой = минус к цели.
    public float crouchTorsoLean = 16f;
    // Текущий уровень, которым пользуется поза. Для стенда и инспектора.
    public float crouchLevel;
    // Руки вперёд-вниз: кисть на земле расширяет опору (BodyState/CoM).
    [Tooltip("Плечо вперёд-вниз на приседе, град (минус у обеих рук).")]
    public float crouchArmShoulder = 18f;
    [Tooltip("Цель локтя: почти прямой, кисть к земле (не −130 — это сгиб вверх).")]
    public float crouchArmElbow = -6f;
    [Tooltip("Цель кисти на приседе, град.")]
    public float crouchArmWrist = -8f;
    [Tooltip("Мин. crouchLevel, чтобы кисти входили в опору.")]
    public float crouchHandSupportMin = 0.55f;
    [Tooltip("Допуск к y=−2.0: кисть считается на земле в приседе, м.")]
    public float crouchHandGroundSlop = 0.24f;
    [Tooltip("Crouch level where hand-support pose starts blending in.")]
    public float crouchHandPoseStart = 0.55f;
    [Tooltip("Deep crouch shoulder target (forward/down), degrees.")]
    public float crouchHandSupportShoulder = 52f;
    [Tooltip("Deep crouch elbow target, degrees.")]
    public float crouchHandSupportElbow = -18f;
    [Tooltip("Deep crouch wrist target, degrees.")]
    public float crouchHandSupportWrist = -20f;
    [Tooltip("Static shoulder split in deep crouch support pose, degrees.")]
    public float crouchHandSupportSpread = 10f;
    [Tooltip("Additional split from balance signal in deep crouch.")]
    public float crouchHandBalanceSpread = 8f;

    [Header("Наклон корпуса")]
    // intent.lean: +1 вперёд (− к мировой цели торса/таза), −1 назад.
    // Колени не трогаем — только грудь и таз; голеностоп держит CoM.
    [Tooltip("Скорость сглаживания leanLevel к intent.lean, 1/с.")]
    public float leanRatePerSecond = 1.5f;
    [Tooltip("Добавка наклона груди при lean=+1, град (вперёд = минус к цели).")]
    public float leanTorsoAngle = 12f;
    [Tooltip("Добавка наклона таза при lean=+1, град (вперёд = минус к цели).")]
    public float leanPelvisTilt = 8f;
    // Текущий уровень −1…+1 для позы и инспектора.
    public float leanLevel;

    [Header("Одноногая стойка")]
    // Сгиб свинга: сначала колено в плюс (укоротить ногу, оторвать стопу).
    // Бедро в минус только после отрыва и дальше по защёлке — цель должна
    // быть не меньше угла на отрыве (~37°), иначе ляжка разгибается и
    // стопа топает. 40° держит ногу в воздухе.
    public float swingHipFlex = 40f;
    public float swingKneeFlex = 55f;
    // Сгиб свинга на земле — доля swingKneeFlex: полный сгиб складывает цепь,
    // ноль не отрывает стопу. После latch — полный swingKneeFlex.
    public float swingKneeGroundedFraction = 0.25f;
    // Добавка к сигналу свинга в сторону flexor (уменьшает jointAngle).
    // Пока стопа на земле, то же PD таза на обоих бёдрах: лишний flexor
    // разгружает GRF свинга, и колено может оторвать стопу, а не сложить
    // замкнутую цепь. Снимать свинг с таза при grounded нельзя.
    public float swingHipUnloadBias = 0.25f;
    // Правая нога впереди (+X), левая сзади: при опоре слева свинг впереди
    // и 50% grounded knee его не снимает — нужны отдельные коэффициенты.
    public float forwardSwingHipUnloadScale = 1.3f;
    public float forwardSwingKneeGroundScale = 1.4f;
    // При опоре справа свинг — левая нога сзади (−X). Pelvis tilt там не
    // работает; разгрузку даём теми же идеями, что forward-свингу.
    public float backSwingHipUnloadScale = 1.3f;
    public float backSwingKneeGroundScale = 1.4f;
    // Доля standLegPelvisTilt назад при правой опоре. 1.0 = −10° роняет
    // обе стопы (swingFoot 1.0); включать дробью через CLI.
    public float backPelvisTiltFraction = 0f;
    // Plantarflex на grounded-свинге впереди. По умолчанию 0: +12° роняет
    // левую опору; включать только через CLI после отдельной проверки.
    public float swingAnkleGroundedToeOff = 0f;
    // Наклон таза при опоре слева: разгружает forward-свинг (правая нога).
    // На правой опоре не применять — мешает отрыву back-свинга.
    public float standLegPelvisTilt = 10f;
    // Во время walkActive при split — ослабить unload/knee/tilt свинга
    // (не только при обеих grounded: кадр отрыва иначе возвращает soft=1).
    // Контрперенос рук на walkActive выключен (иначе elbowLimit↑ и fold).
    [Tooltip("Доля unload/knee/tilt свинга при walk + split.")]
    public float dualSupportSwingScale = 0.45f;
    // Потолок standLegLevel на всём walkActive: если резать только при
    // обеих grounded, краткий отрыв даёт level→1 и fold после посадки.
    // Холодный oneg walkActive=false — без потолка.
    [Tooltip("Макс. standLegLevel пока walkActive. 0 = без потолка.")]
    public float dualSupportStandCap = 0.30f;
    // Множитель unload/knee свинга на walk-split поверх dualSoft. Tilt
    // остаётся на dualSoft — иначе fold после swap. 1 = как сейчас.
    [Tooltip("Доп. множитель unload/knee на walk-split (не для tilt). 1 = выкл.")]
    public float walkSwingLiftScale = 1f;
    // К концу фазы Stance — walkSingleSupport. Ранний stance-only роняет.
    [Tooltip("Секунд Stance до начала lift.")]
    public float walkStanceLiftDelay = 6f;
    [Tooltip("Секунд рампы liftAge после delay.")]
    public float walkStanceLiftRamp = 2f;
    // Отдельно от liftRamp: ускорение weight (1.5) ухудшило swing (0.57)
    // и sat (opt5). Дефолт = liftRamp; CLI -walkWeightRamp для перебора.
    [Tooltip("Секунд рампы weightBlend (unload/cap) в Stance при CoM на опоре.")]
    public float walkWeightRamp = 2f;
    // Бег: короче delay/ramp, иначе при runStance 4 с lift не успевает.
    [Tooltip("walkStanceLiftDelay при intent.run=1.")]
    public float runStanceLiftDelay = 2f;
    [Tooltip("walkWeightRamp при intent.run=1.")]
    public float runWeightRamp = 1.5f;
    // Подъём только когда CoM над опорной стопой, не только по таймеру:
    // иначе delay=4 + lift=2.5 всё равно fold (~22 с, walk_l25_d4).
    [Tooltip("|CoM−stance| ≤ этого на walk dual-support, чтобы включить lift.")]
    public float walkLiftComMax = 0.10f;
    // Toe-off свинга только на walk при высоком liftBlend; глобальный
    // swingAnkleGroundedToeOff на dual-support роняет (~17 с).
    [Tooltip("Макс. toe-off свинга на walk при liftBlend=1, град. Только forward-свинг. 0 = выкл.")]
    public float walkSwingToeOffMax = 0f;
    // Доп. unload и сгиб колена от liftAct — только через CLI; дефолт 0.
    [Tooltip("Доп. hip-unload при liftAct=1 на walk-split.")]
    public float walkLiftUnloadBias = 0.02f;
    [Tooltip("Доля swingKneeFlex на grounded-свинге при liftAct=1.")]
    public float walkKneePeelMax = 0.05f;
    // Толчок опорной стопы при полном weightBlend: CoM над опорой сам
    // не отрывает свинг (ankleBalance≈0). Не PD к углу — только сигнал
    // в ankleBalance. 0 = выкл. CLI -walkStancePush.
    [Tooltip("Доп. plantar на опорный голеностоп при weightBlend≈1. 0 = выкл.")]
    public float walkStancePush = 0f;
    // Выпрямление опоры при полном weightBlend: таз вверх → свинг теряет
    // GRF. Не сгиб свинга (holdknee/gk25 fold). 0 = выкл. CLI -walkStanceExtend.
    [Tooltip("Доля выпрямления опоры (колено/таз→0) при weightBlend=1. 0 = выкл.")]
    public float walkStanceExtend = 0f;
    // Ходьба — это управляемое падение вперёд, а не переступание. Пока целью
    // голеностопа остаётся «CoM над опорой», он возвращает тело назад, и смена
    // опоры даёт 0.21 м за 35 с. Здесь цель сдвинута на walkComLeadX вперёд
    // опорной стопы: разгон даёт тяжесть, свинг подставляется. Невидимой силы
    // нет. 0 = прежнее поведение. CLI -walkComLead.
    [Tooltip("Метров впереди опоры держать CoM при moveX≠0. 0 = топтание на месте.")]
    public float walkComLeadX = 0.02f;
    // Поза свинга включается только после отрыва (swingHipLatched), а отрыв
    // требует, чтобы ляжка уже ушла вперёд — замкнутый круг: оба бедра сидят
    // на «держать таз вертикально», ни одно не выносится, стопа не покидает
    // землю (swingFoot ≈ 1.0, travel 0.12 м за 35 с). Ножницы дают свингу позу
    // вперёд, пока он ещё на земле, как только вес перенесён на опору.
    // 0 = выкл, прежнее поведение. CLI -walkSwingScissor.
    [Tooltip("standLegLevel, с которого свинг выносится вперёд ещё на земле. 0 = выкл.")]
    public float walkSwingScissorLevel = 0.20f;
    // Вынос ляжки вперёд по третьему закону отбрасывает таз назад: полный
    // −swingHipFlex на земле даёт отрыв (swingFoot 0.50 вместо 0.999), но
    // человек уезжает назад и складывается за 7 с. Доля выноса на земле;
    // в воздухе поза остаётся полной. CLI -walkSwingScissorFlex.
    [Tooltip("Доля swingHipFlex в позе ножниц на земле. 1 = полный вынос.")]
    public float walkSwingScissorFlex = 0.25f;
    // После чирка hipair снова upright и сажает стопу. Держим flex ещё
    // hold секунд на grounded (не commit до первого воздуха). CLI, дефолт 0.
    [Tooltip("Секунд удержания swing-hip flex после !grounded. 0 = выкл.")]
    public float walkSwingAirHold = 0f;
    // После latch: сгиб бедра пока пятка не нагружена. Носок не сажает.
    // CLI -walkSwingHeelPlant 1; дефолт 0 = hipair (!grounded).
    [Tooltip("1 = поза свинга по !heelLoaded после latch. 0 = hipair.")]
    public float walkSwingHeelPlant = 0f;
    // После чирка hipair держит latch → grounded-колено выкл. Сброс latch
    // на посадке возвращает groundKnee без kneer (тот оставлял latch).
    // CLI -walkSwingUnlatchPlant 1; дефолт 0.
    [Tooltip("1 = сброс swingHipLatched при grounded свинга. 0 = sticky latch.")]
    public float walkSwingUnlatchPlant = 0f;
    // Latch с первого кадра воздуха гасит groundKnee на посадке.
    // Порог непрерывного !grounded (с) до latch. 0 = hipair (сразу).
    [Tooltip("Секунд непрерывного воздуха до latch. 0 = с первого кадра.")]
    public float walkSwingLatchAir = 0f;
    // В воздухе hipair держит ankle joint=0 (стопа ⊥ голени) → при сгибе
    // колена носок вниз чиркает. 1 = цель мировой горизонтали стопы.
    [Tooltip("1 = в воздухе после latch стопа к мировой горизонтали. 0 = joint 0.")]
    public float walkSwingAirLevel = 0f;
    // Пока liftBlend растёт, level догоняет cap быстрее — иначе unload
    // слабый при cap 0.4→1 и rate 0.5/с.
    [Tooltip("Множитель standLegRate при liftBlend=1 на walk. 1 = выкл.")]
    public float walkLiftLevelRateScale = 1f;
    // Отладка: фактический множитель dual-support в последнем FixedUpdate.
    [System.NonSerialized] public float lastDualSoft = 1f;
    [System.NonSerialized] public float lastLiftBlend;
    public float standLegRatePerSecond = 1.5f;
    [Tooltip("Сброс одноногой позы при standLeg=0 — быстрее, чем подъём, чтобы окно переноса не уплывало.")]
    public float standLegReleaseRatePerSecond = 4f;
    // 0 — две опоры, 1 — полная поза свинга. Сглаживание, как у crouchLevel.
    public float standLegLevel;

    // Ошибка и скорость делятся на опорные величины, поэтому вход PD
    // безразмерный, а коэффициенты имеют порядок единицы. До нормировки
    // ошибки в 0.03° хватало, чтобы активация упёрлась в 1: регулятор
    // работал выключателем и всегда на полной мощности.
    [Header("Нормировка входа PD")]
    public float errorReferenceDegrees = 10f;
    public float speedReferenceDegPerSec = 200f;

    [Header("PD-регуляторы суставов")]
    // hipP/hipD при двух опорах не кормят угол: таз держит мировая
    // вертикаль полями pelvisPGain/pelvisDGain. При одноногой стойке
    // ими пользуется свинг-бедро, и только после отрыва стопы.
    public float hipPGain = 1.5f;
    public float hipDGain = 0.4f;
    public float kneePGain = 1.2f;
    public float kneeDGain = 0.5f;
    public float neckPGain = 1.5f;
    public float neckDGain = 0.4f;
    // Цель шеи и головы — мировая вертикаль, а не угол к родителю.
    // Своя опорная ошибка, как у поясницы и таза: у ComputeSignal она 10°,
    // и при ней сустав уходил в насыщение от долей градуса.
    public float neckTargetTilt = 0f;
    public float neckErrorReferenceDegrees = 25f;
    // Устойчивый PD только для шеи и головы. Флаг оставлен, чтобы стенд мог
    // сравнить с прежним поведением тем же билдом: без него разница между
    // правкой и пересборкой неотличима. Руки на SPD всегда — см. блок ниже.
    public bool useStablePd = true;
    [Header("Руки")]
    // Суставная поза, не мировая вертикаль: иначе рука — маятник и машет.
    // Локоть гнётся в минус (−140…0). Ноль — упор «прямая рука»; без мышцы
    // гравитация держала сустав там ~98% времени. Цель чуть в минус.
    // SPD всегда: у локтя K·Δt/Ieff ≈ 11.8, у кисти ≈ 5.1, у плеча ≈ 3.4 —
    // явный PD неустойчив. useStablePd к рукам не относится.
    public float shoulderPGain = 1.5f;
    public float shoulderDGain = 0.4f;
    public float shoulderBaseAngle = 0f;
    public float shoulderErrorReferenceDegrees = 25f;
    public float elbowPGain = 1.2f;
    public float elbowDGain = 0.4f;
    public float elbowBaseAngle = -20f;
    public float elbowErrorReferenceDegrees = 25f;
    public float wristPGain = 1.0f;
    public float wristDGain = 0.4f;
    public float wristBaseAngle = 0f;
    public float wristErrorReferenceDegrees = 25f;
    // Смещение целей от balanceSignal (CoM + скорость наклона): CoM впереди —
    // плечи в плюс (кисть назад), локоть к нулю. Ноль — статичная поза.
    public float armShoulderBalanceGain = 25f;
    public float armElbowBalanceGain = 10f;
    public float armWristBalanceGain = 0f;
    // Мах рук на walk: противофаза ногам, sin по фазе Stance. Не CoM-balance —
    // тот давал elbowLimit и fold после swap.
    [Tooltip("Амплитуда плеча на walk, град (sin по фазе Stance).")]
    public float walkArmShoulderSwing = 15f;
    [Tooltip("Доп. сгиб локтя вперёд на walk, град.")]
    public float walkArmElbowSwing = 5f;
    [Tooltip("Доля амплитуды рук в Transfer (затухание). 0 = база.")]
    public float walkArmTransferCarry = 0.35f;
    [Header("Таз к мировой вертикали")]
    // Как поясница: ошибка и скорость только из VestibularSystem.
    // Сустав бедра живёт на ляжке, таз — connectedBody, поэтому знак
    // P/D как у ControlJointToAngle, а не как у поясницы: положительный
    // сигнал крутит таз против часовой (увеличивает jointAngle).
    // P и D плюсовые; минус передавать нельзя — контур перевернётся.
    public float pelvisPGain = 2.0f;
    public float pelvisDGain = 0.3f;
    public float pelvisErrorReferenceDegrees = 25f;
    [Header("Поясница")]
    // Опора — мировая вертикаль груди, не угол относительно таза.
    // Иначе при завале таза поясница везёт 35 кг верха вниз вместе с ним.
    // Ошибка и скорость только из VestibularSystem: смешивать мировой
    // наклон с joint.jointSpeed нельзя, это разные системы отсчёта.
    public float lumbarTargetTilt = 0f;
    // P и D плюсовые. В формуле ниже они входят как −P и −D, потому что
    // ошибка по-прежнему target − tilt: так сохраняется та же арифметика,
    // что у рабочей пары P=−2, D=−0.3, без смены знака демпфера.
    public float lumbarPGain = 2.0f;
    public float lumbarDGain = 0.3f;
    public float lumbarErrorReferenceDegrees = 25f;
    [Header("Голеностоп как маятник")]
    // Момент против смещения CoM, а не против угла сустава.
    // Опрокидывающий момент равен m*g*d ≈ 687*d Н·м. У тела ростом 1.75 м
    // центр масс ниже, инерция меньше: прежние P=7 и D=4 оставляли качку
    // торса и СКО смещения хуже эталона. P=14 держит CoM плотнее, D=8
    // гасит скорость после стартового приседания.
    public float ankleComP = 14f;
    public float ankleComD = 8f;
    public float ankleLimitMargin = 15f;

    [Header("Скорость активации мышц")]
    public float muscleActivationSpeed = 20f;

    [Header("Мышцы ног")]
    public Muscle leftHipFlexor;
    public Muscle leftHipExtensor;
    public Muscle rightHipFlexor;
    public Muscle rightHipExtensor;
    public Muscle leftKneeFlexor;
    public Muscle leftKneeExtensor;
    public Muscle rightKneeFlexor;
    public Muscle rightKneeExtensor;
    public Muscle leftAnkleFlexor;
    public Muscle leftAnkleExtensor;
    public Muscle rightAnkleFlexor;
    public Muscle rightAnkleExtensor;

    [Header("Мышцы поясницы")]
    public Muscle lumbarFlexor;
    public Muscle lumbarExtensor;

    [Header("Мышцы шеи и головы")]
    public Muscle neckFlexor;
    public Muscle neckExtensor;
    public Muscle headFlexor;
    public Muscle headExtensor;

    [Header("Мышцы рук")]
    public Muscle leftShoulderFlexor;
    public Muscle leftShoulderExtensor;
    public Muscle rightShoulderFlexor;
    public Muscle rightShoulderExtensor;
    public Muscle leftElbowFlexor;
    public Muscle leftElbowExtensor;
    public Muscle rightElbowFlexor;
    public Muscle rightElbowExtensor;
    public Muscle leftWristFlexor;
    public Muscle leftWristExtensor;
    public Muscle rightWristFlexor;
    public Muscle rightWristExtensor;

    private HingeJoint2D leftHipJoint;
    private HingeJoint2D rightHipJoint;
    private HingeJoint2D leftKneeJoint;
    private HingeJoint2D rightKneeJoint;
    private HingeJoint2D leftAnkleJoint;
    private HingeJoint2D rightAnkleJoint;
    private HingeJoint2D lumbarJoint;
    private HingeJoint2D neckJoint;
    private HingeJoint2D headJoint;
    private HingeJoint2D leftShoulderJoint;
    private HingeJoint2D rightShoulderJoint;
    private HingeJoint2D leftElbowJoint;
    private HingeJoint2D rightElbowJoint;
    private HingeJoint2D leftWristJoint;
    private HingeJoint2D rightWristJoint;

    // Парная инерция считается лениво, на первом FixedUpdate: в Awake
    // Rigidbody2D.inertia ещё не пересчитан по коллайдеру.
    private float neckEffectiveInertia;
    private float headEffectiveInertia;
    private float leftShoulderEffectiveInertia;
    private float rightShoulderEffectiveInertia;
    private float leftElbowEffectiveInertia;
    private float rightElbowEffectiveInertia;
    private float leftWristEffectiveInertia;
    private float rightWristEffectiveInertia;
    private float leftKneeEffectiveInertia;
    private float rightKneeEffectiveInertia;
    private float leftHipEffectiveInertia;
    private float rightHipEffectiveInertia;
    private float leftAnkleEffectiveInertia;
    private float rightAnkleEffectiveInertia;

    private VestibularSystem vestibularSystem;
    private CenterOfMassCalculator comCalculator;
    private BodyStateEstimator bodyState;
    private MotionIntent intent;
    private StepPhaseDriver stepPhaseDriver;
    // Искали один раз: у популяции driver нет, GetComponent каждый шаг 200 Гц
    // ничего не найдёт. Late-add — только Bind/Ensure, не FixedUpdate.
    private bool stepPhaseDriverSearched;
    // После первого отрыва свинга не возвращаем бедро к тазу из‑за чирканья
    // стопы: иначе ляжка разгибается и нога топает обратно.
    private bool swingHipLatched;
    // Остаток удержания flex после чирка (walkSwingAirHold).
    private float swingAirHoldRemain;
    // Непрерывный !grounded свинга для walkSwingLatchAir.
    private float swingAirStreak;
    // Последняя команда опоры: при смене знака сбрасываем уровень и защёлку.
    private float heldStandCmd;
    // Угол свинга в кадр защёлки. Цель после отрыва не слабее этого сгиба:
    // Min(angleAtLatch, −swingHipFlex) — более отрицательное = больше сгиб.
    private float swingHipAngleAtLatch;

    void Awake()
    {
        vestibularSystem = GetComponent<VestibularSystem>();
        comCalculator = GetComponent<CenterOfMassCalculator>();
        bodyState = GetComponent<BodyStateEstimator>();
        intent = GetComponent<MotionIntent>();
        CacheStepPhaseDriverOnce();

        leftHipJoint = GetJoint("LeftLegThigh");
        rightHipJoint = GetJoint("RightLegThigh");
        leftKneeJoint = GetJoint("LeftLegShin");
        rightKneeJoint = GetJoint("RightLegShin");
        leftAnkleJoint = GetJoint("LeftLegFoot");
        rightAnkleJoint = GetJoint("RightLegFoot");
        lumbarJoint = GetJoint("Torso");
        neckJoint = GetJoint("Neck");
        headJoint = GetJoint("Head");
        leftShoulderJoint = GetJoint("LeftArmUpper");
        rightShoulderJoint = GetJoint("RightArmUpper");
        leftElbowJoint = GetJoint("LeftArmLower");
        rightElbowJoint = GetJoint("RightArmLower");
        leftWristJoint = GetJoint("LeftArmHand");
        rightWristJoint = GetJoint("RightArmHand");
    }

    // Один поиск. HeadlessTrial вешает driver до BuildHuman, Awake его видит.
    // Если AddComponent сдвинется после Awake — явный Bind/Ensure снаружи.
    private void CacheStepPhaseDriverOnce()
    {
        if (stepPhaseDriverSearched) return;
        stepPhaseDriver = GetComponent<StepPhaseDriver>();
        stepPhaseDriverSearched = true;
    }

    public void BindStepPhaseDriver(StepPhaseDriver driver)
    {
        stepPhaseDriver = driver;
        stepPhaseDriverSearched = true;
    }

    // Повторный GetComponent только по явному вызову после late-add.
    public void EnsureStepPhaseDriver()
    {
        stepPhaseDriverSearched = false;
        CacheStepPhaseDriverOnce();
    }

    private HingeJoint2D GetJoint(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t.GetComponent<HingeJoint2D>() : null;
    }

    void FixedUpdate()
    {
        float comOffset = comCalculator != null ? comCalculator.GetCoMOffsetX() : 0f;
        float angularVelocity = vestibularSystem != null ? vestibularSystem.GetBodyAngularVelocity() : 0f;

        UpdateCrouchLevel();
        UpdateLeanLevel();
        UpdateStandLegLevel();

        bool walkSoft = stepPhaseDriver != null && stepPhaseDriver.walkActive;
        float standCmd = intent != null ? intent.standLeg : 0f;
        if (!walkSoft)
        {
            bool requestSplit = Mathf.Abs(standCmd) > 0.5f;
            if (requestSplit)
            {
                float requestSign = Mathf.Sign(standCmd);
                bool hadSplit = Mathf.Abs(heldStandCmd) > 0.5f;
                if (!hadSplit || Mathf.Sign(heldStandCmd) != requestSign)
                {
                    standLegLevel = 0f;
                    swingHipLatched = false;
                }
                heldStandCmd = requestSign;
            }
            else if (standLegLevel <= 0.001f)
            {
                heldStandCmd = 0f;
            }

            standCmd = heldStandCmd;
        }
        else
        {
            heldStandCmd = 0f;
        }

        bool splitLegs = standLegLevel > 0.001f && Mathf.Abs(standCmd) > 0.5f;
        bool leftIsSwing = splitLegs && standCmd > 0.5f;
        bool rightIsSwing = splitLegs && standCmd < -0.5f;
        bool leftGrounded = bodyState != null && bodyState.leftFootGrounded;
        bool rightGrounded = bodyState != null && bodyState.rightFootGrounded;
        bool walkDualSupport = walkSoft && leftGrounded && rightGrounded;

        float stanceComOff = 0f;
        if (splitLegs && bodyState != null && comCalculator != null)
        {
            Vector2 comSt = comCalculator.GetCenterOfMass();
            float stanceX = leftIsSwing ? bodyState.rightFootPosition.x
                : bodyState.leftFootPosition.x;
            stanceComOff = comSt.x - stanceX;
        }

        float liftAge = 0f;
        bool running = intent != null && intent.run > 0.5f;
        float activeLiftDelay = running ? runStanceLiftDelay : walkStanceLiftDelay;
        float activeWeightRamp = running && runWeightRamp > 0.01f
            ? runWeightRamp
            : (walkWeightRamp > 0.01f ? walkWeightRamp : walkStanceLiftRamp);
        if (walkSoft && splitLegs && stepPhaseDriver != null
            && stepPhaseDriver.PhaseCode == 0)
        {
            float age = stepPhaseDriver.PhaseAge();
            liftAge = Mathf.Clamp01(
                (age - activeLiftDelay) / Mathf.Max(0.05f, walkStanceLiftRamp));
        }

        float comLiftGate = 0f;
        if (walkSoft && splitLegs)
        {
            float absSt = Mathf.Abs(stanceComOff);
            float comMax = Mathf.Max(0.01f, walkLiftComMax);
            if (absSt <= comMax)
                comLiftGate = 1f;
            else if (absSt <= comMax * 2f)
                comLiftGate = 1f - (absSt - comMax) / comMax;
        }

        // Вес уже на опоре — разгрузка как одноопора, не ждать delay=6.
        // Иначе dualSoft=0.35 и cap=0.4 на всём Stance, стопа не отрывается
        // (walk1_lift swingFoot 0.9998). Рампа веса отдельна от liftRamp.
        // Tilt на dualSoft: полный tilt на правой опоре прижимает обе стопы.
        // Ease-out / мягкий gate (opt8*) — fold при том же swing ~0.42; линейный.
        float weightBlend = 0f;
        if (walkSoft && splitLegs && stepPhaseDriver != null
            && stepPhaseDriver.PhaseCode == 0 && comLiftGate >= 1f)
        {
            float age = stepPhaseDriver.PhaseAge();
            weightBlend = Mathf.Clamp01(age / Mathf.Max(0.05f, activeWeightRamp));
        }

        // Плавный подъём: cap, unload и ankle растут вместе, без скачка
        // cap 0.4→1 и liftMult 1→scale одновременно роняли (~21 с).
        float liftBlend = 0f;
        if (walkSoft && splitLegs && stepPhaseDriver != null
            && stepPhaseDriver.PhaseCode == 0)
            liftBlend = liftAge * comLiftGate;
        lastLiftBlend = liftBlend;
        // Unload/knee — квадрат: ранний dual-support не дёргается, конец Stance — резче.
        float liftAct = liftBlend * liftBlend;

        // Обе стопы на земле, CoM над опорой — контур как одноопора.
        bool walkSingleSupport = walkDualSupport && splitLegs && liftBlend >= 0.5f;

        // Потолок level рампой, не снятием: иначе unload не отрывает стопу.
        // capBlend берёт и таймерный lift, и вес на опоре — иначе cap=0.4
        // держит unload в нуле почти всё Stance.
        if (walkSoft && dualSupportStandCap > 0.01f)
        {
            float cap = dualSupportStandCap;
            float capBlend = Mathf.Max(liftBlend, weightBlend);
            if (capBlend > 0.001f)
                cap = Mathf.Lerp(dualSupportStandCap, 1f, capBlend);
            if (standLegLevel > cap)
                standLegLevel = cap;
        }

        // Падение от опорной стопы — true single-support или liftBlend.
        float fallOffset = comOffset;
        bool singleSupport = splitLegs && (
            (leftIsSwing && !leftGrounded && rightGrounded) ||
            (rightIsSwing && !rightGrounded && leftGrounded));
        if (singleSupport && splitLegs)
            fallOffset = stanceComOff;
        else if (walkDualSupport && splitLegs && liftBlend > 0.001f)
            fallOffset = Mathf.Lerp(comOffset, stanceComOff, liftBlend);
        UpdateState(fallOffset);

        if (currentState == BalanceState.Falling)
        {
            RelaxAllMuscles();
            return;
        }

        float offsetN = comOffset / Mathf.Max(0.01f, comOffsetReference);
        float tiltSpeedN = angularVelocity / Mathf.Max(1f, tiltSpeedReference);
        float balanceSignal = Mathf.Clamp(
            comProportionalGain * offsetN + comDerivativeGain * tiltSpeedN,
            -maxBalanceSignal, maxBalanceSignal);

        // Таз держит мировую вертикаль, а не угол к ляжке: иначе качка ноги
        // один в один уезжает в таз, и поясница выбирает весь ход ±20°.
        // hipBalanceGain по-прежнему чуть клонит цель навстречу CoM.
        // crouchHipFlex в цель не входит — присед задаёт наклон таза.
        float pelvisTarget = Mathf.Clamp(
            hipBaseAngle - hipBalanceGain * balanceSignal
                - crouchLevel * crouchPelvisTilt - leanLevel * leanPelvisTilt,
            -60f, 60f);
        // Выпрямление опоры: таз к вертикали при полном weightBlend.
        if (walkSoft && walkStanceExtend > 0.001f && weightBlend > 0.001f)
            pelvisTarget = Mathf.Lerp(pelvisTarget, 0f, Mathf.Clamp01(walkStanceExtend * weightBlend));
        float kneeTarget = kneeBaseAngle + crouchLevel * crouchKneeFlex;
        // Recovery-сгиб коленей при dual-support split складывает цепь
        // после transfer. На walkSingleSupport — как одноопора.
        if (currentState == BalanceState.Recovery
            && !(splitLegs && leftGrounded && rightGrounded && !walkSingleSupport))
            kneeTarget += kneeRecoveryFlex;
        kneeTarget = Mathf.Clamp(kneeTarget, 0f, 115f);

        float comVelX = bodyState != null ? bodyState.comVelocity.x : 0f;
        float velN = comVelX / Mathf.Max(0.05f, comVelocityReference);

        // Мягкий dual-support. Unload/knee — к одноноге по weightBlend;
        // tilt остаётся мягким (правая опора иначе прижимает обе стопы).
        float dualSoft = 1f;
        if (walkSoft && splitLegs)
            dualSoft = Mathf.Clamp01(dualSupportSwingScale);
        float tiltSoft = dualSoft;
        float unloadSoft = Mathf.Lerp(dualSoft, 1f, weightBlend);
        float liftMult = Mathf.Lerp(1f, Mathf.Max(1f, walkSwingLiftScale), liftAct);
        float liftSoft = unloadSoft * liftMult;
        lastDualSoft = dualSoft;

        // Разгрузка свинга наклоном таза:
        // (свинг справа). На правой опоре tilt обе стопы прижимает к земле.
        if (splitLegs && standLegPelvisTilt > 0.01f && standCmd < -0.5f)
        {
            pelvisTarget = Mathf.Clamp(
                pelvisTarget + standLegLevel * standLegPelvisTilt * tiltSoft,
                -60f, 60f);
        }
        else if (splitLegs && standLegPelvisTilt > 0.01f && standCmd > 0.5f
                 && backPelvisTiltFraction > 0.001f)
        {
            pelvisTarget = Mathf.Clamp(
                pelvisTarget - standLegLevel * standLegPelvisTilt * backPelvisTiltFraction * tiltSoft,
                -60f, 60f);
        }

        // CoM впереди крутит тело вперёд.
        // Walk Stance: опора от опорной стопы — иначе mid держит CoM между
        // стопами, comLiftGate/liftBlend не открываются, свинг не отрывается.
        // Transfer: mid (lerp по liftBlend), как walk_mid — иначе fold на swap.
        bool walkStancePhase = walkSoft && stepPhaseDriver != null
            && stepPhaseDriver.PhaseCode == 0;
        // Смещение цели вперёд опоры: голеностоп перестаёт возвращать тело
        // назад, и человек едет туда, куда просит moveX.
        float comLead = 0f;
        if (walkSoft && intent != null && walkComLeadX > 0.0001f)
            comLead = walkComLeadX * Mathf.Clamp(intent.moveX, -1f, 1f);
        float comRef = Mathf.Max(0.01f, comOffsetReference);
        float ankleOffsetN = (comOffset - comLead) / comRef;
        if (splitLegs && bodyState != null && comCalculator != null)
        {
            float stanceN = (stanceComOff - comLead) / comRef;
            if (walkDualSupport)
            {
                ankleOffsetN = walkStancePhase
                    ? stanceN
                    : Mathf.Lerp((comOffset - comLead) / comRef, stanceN, liftBlend);
            }
            else
                ankleOffsetN = stanceN;
        }
        float ankleBalance = Mathf.Clamp(
            ankleComP * ankleOffsetN + ankleComD * velN,
            -1f, 1f);

        if (!splitLegs)
        {
            swingHipLatched = false;
            swingAirHoldRemain = 0f;
            swingAirStreak = 0f;
        }
        else if ((leftIsSwing && !leftGrounded) || (rightIsSwing && !rightGrounded))
        {
            swingAirStreak += Time.fixedDeltaTime;
            // 0 = hipair: latch с первого кадра. Иначе — после streak.
            float needAir = walkSwingLatchAir > 0.001f ? walkSwingLatchAir : 0f;
            if (swingAirStreak >= needAir - 1e-6f)
            {
                if (!swingHipLatched)
                {
                    HingeJoint2D swingJoint = leftIsSwing ? leftHipJoint : rightHipJoint;
                    swingHipAngleAtLatch = swingJoint != null ? swingJoint.jointAngle : -swingHipFlex;
                }
                swingHipLatched = true;
                if (walkSwingAirHold > 0.001f)
                    swingAirHoldRemain = walkSwingAirHold;
            }
        }
        else
        {
            swingAirStreak = 0f;
            if (walkSwingUnlatchPlant > 0.5f && swingHipLatched
                && ((leftIsSwing && leftGrounded) || (rightIsSwing && rightGrounded)))
            {
                swingHipLatched = false;
                swingAirHoldRemain = 0f;
            }
            else if (swingAirHoldRemain > 0f)
                swingAirHoldRemain = Mathf.Max(0f, swingAirHoldRemain - Time.fixedDeltaTime);
        }

        // Поза −swingHipFlex: hipair = только воздух; heelPlant = пока
        // пятка не села (носок не снимает сгиб). airHold — таймер, отвергнут.
        bool heelPlant = walkSwingHeelPlant > 0.5f;
        bool leftHeel = bodyState != null && bodyState.leftFootHeelLoaded;
        bool rightHeel = bodyState != null && bodyState.rightFootHeelLoaded;
        bool airHold = swingAirHoldRemain > 0.001f;
        // Ножницы разрывают круг «поза только после отрыва».
        bool scissor = walkSoft && splitLegs && walkSwingScissorLevel > 0.001f
            && standLegLevel >= walkSwingScissorLevel;
        bool leftAir = swingHipLatched && (heelPlant ? !leftHeel : (!leftGrounded || airHold));
        bool rightAir = swingHipLatched && (heelPlant ? !rightHeel : (!rightGrounded || airHold));
        bool leftSwingHip = leftIsSwing && swingHipFlex > 0.5f && (leftAir || scissor);
        bool rightSwingHip = rightIsSwing && swingHipFlex > 0.5f && (rightAir || scissor);
        float swingUnload = 0f;
        if (splitLegs)
        {
            float unloadBase = swingHipUnloadBias * liftSoft;
            // peel/unload раньше сидели на liftAct (delay=6) — к концу Stance,
            // когда weightBlend уже полный с ~2 с. Цепляем к weightBlend.
            float earlyAct = Mathf.Max(liftAct, weightBlend);
            if (walkSoft && earlyAct > 0.001f && walkLiftUnloadBias > 0.001f)
                unloadBase += earlyAct * walkLiftUnloadBias;
            swingUnload = standLegLevel * unloadBase;
        }
        float leftSwingUnload = 0f;
        if (leftIsSwing && !leftSwingHip)
        {
            leftSwingUnload = swingUnload;
            if (standCmd > 0.5f)
                leftSwingUnload *= backSwingHipUnloadScale;
        }
        float rightSwingUnload = 0f;
        if (rightIsSwing && !rightSwingHip)
            rightSwingUnload = swingUnload * forwardSwingHipUnloadScale;

        ControlHipsToWorldUpright(
            pelvisTarget,
            !leftSwingHip,
            !rightSwingHip,
            leftSwingUnload,
            rightSwingUnload);

        // После отрыва цель не слабее угла защёлки: иначе ляжка разгибается
        // и 26 см стопы топают обратно.
        float latchedHipTarget = swingHipLatched
            ? Mathf.Min(swingHipAngleAtLatch, -swingHipFlex)
            : -swingHipFlex;
        // На земле вынос дозируется: реакция уходит в таз, а не в шаг.
        float groundedHipTarget = -swingHipFlex * Mathf.Clamp01(walkSwingScissorFlex);
        if (leftSwingHip)
        {
            float target = !leftAir && scissor ? groundedHipTarget : latchedHipTarget;
            ControlJointToAngleStable(leftHipJoint, target,
                hipPGain, hipDGain, errorReferenceDegrees,
                leftHipFlexor, leftHipExtensor, ref leftHipEffectiveInertia);
        }
        if (rightSwingHip)
        {
            float target = !rightAir && scissor ? groundedHipTarget : latchedHipTarget;
            ControlJointToAngleStable(rightHipJoint, target,
                hipPGain, hipDGain, errorReferenceDegrees,
                rightHipFlexor, rightHipExtensor, ref rightHipEffectiveInertia);
        }

        // Свинг-колено гнём только после отрыва: пока стопа на земле, цепь
        // замкнута, и swingKneeFlex складывает таз вместе с опорой.
        // После latch сгиб как у бедра — иначе нога не уйдёт с опоры.
        float leftKnee = kneeTarget;
        float rightKnee = kneeTarget;
        if (splitLegs)
        {
            if (leftIsSwing)
            {
                float groundKnee = swingKneeGroundedFraction * liftSoft;
                if (standCmd > 0.5f)
                    groundKnee *= backSwingKneeGroundScale;
                float earlyActL = Mathf.Max(liftAct, weightBlend);
                if (walkSoft && !swingHipLatched && earlyActL > 0.001f && walkKneePeelMax > 0.001f)
                    groundKnee = Mathf.Max(groundKnee, earlyActL * walkKneePeelMax);
                // Полный сгиб только в воздухе. На latched&&grounded — kneeTarget
                // (без groundKnee): rearm доли после чирка дал swing 0.62 / drop 0.05.
                if (swingHipLatched && !leftGrounded)
                    leftKnee = Mathf.Clamp(kneeBaseAngle + standLegLevel * swingKneeFlex, 0f, 115f);
                else if (!swingHipLatched)
                    leftKnee = Mathf.Clamp(
                        kneeTarget + standLegLevel * swingKneeFlex * groundKnee,
                        0f, 115f);
            }
            if (rightIsSwing)
            {
                float groundKnee = swingKneeGroundedFraction * forwardSwingKneeGroundScale * liftSoft;
                float earlyActR = Mathf.Max(liftAct, weightBlend);
                if (walkSoft && !swingHipLatched && earlyActR > 0.001f && walkKneePeelMax > 0.001f)
                    groundKnee = Mathf.Max(groundKnee, earlyActR * walkKneePeelMax);
                if (swingHipLatched && !rightGrounded)
                    rightKnee = Mathf.Clamp(kneeBaseAngle + standLegLevel * swingKneeFlex, 0f, 115f);
                else if (!swingHipLatched)
                    rightKnee = Mathf.Clamp(
                        kneeTarget + standLegLevel * swingKneeFlex * groundKnee,
                        0f, 115f);
            }
            // Опора: выпрямить колено к 0 при weightBlend — поднять таз.
            if (walkStanceExtend > 0.001f && weightBlend > 0.001f)
            {
                float ext = Mathf.Clamp01(walkStanceExtend * weightBlend);
                if (!leftIsSwing)
                    leftKnee = Mathf.Lerp(leftKnee, 0f, ext);
                if (!rightIsSwing)
                    rightKnee = Mathf.Lerp(rightKnee, 0f, ext);
            }
        }
        ControlJointToAngleStable(leftKneeJoint, leftKnee, kneePGain, kneeDGain,
            errorReferenceDegrees, leftKneeFlexor, leftKneeExtensor, ref leftKneeEffectiveInertia);
        ControlJointToAngleStable(rightKneeJoint, rightKnee, kneePGain, kneeDGain,
            errorReferenceDegrees, rightKneeFlexor, rightKneeExtensor, ref rightKneeEffectiveInertia);

        // Свинг на земле: на walk Stance — только опора (как cold oneg);
        // mid на Transfer dual; на liftBlend гасим ankle на свинге.
        float leftAnkleBalance;
        float rightAnkleBalance;
        if (walkDualSupport && walkStancePhase)
        {
            leftAnkleBalance = splitLegs && leftIsSwing && leftGrounded ? 0f : ankleBalance;
            rightAnkleBalance = splitLegs && rightIsSwing && rightGrounded ? 0f : ankleBalance;
        }
        else if (walkDualSupport && liftBlend < 0.001f)
        {
            leftAnkleBalance = ankleBalance;
            rightAnkleBalance = ankleBalance;
        }
        else if (walkDualSupport)
        {
            leftAnkleBalance = splitLegs && leftIsSwing && leftGrounded
                ? ankleBalance * (1f - liftAct) : ankleBalance;
            rightAnkleBalance = splitLegs && rightIsSwing && rightGrounded
                ? ankleBalance * (1f - liftAct) : ankleBalance;
        }
        else
        {
            leftAnkleBalance = splitLegs && leftIsSwing && leftGrounded ? 0f : ankleBalance;
            rightAnkleBalance = splitLegs && rightIsSwing && rightGrounded ? 0f : ankleBalance;
        }

        // Push-off опоры: при полном weightBlend и CoM над стопой добавить
        // plantar только на stance. Не против текущего ankleBalance.
        if (walkSoft && splitLegs && walkStancePush > 0.001f && weightBlend > 0.99f
            && Mathf.Abs(stanceComOff) <= Mathf.Max(0.01f, walkLiftComMax))
        {
            float push = walkStancePush * weightBlend;
            if (leftIsSwing && rightAnkleBalance >= -0.001f)
                rightAnkleBalance = Mathf.Clamp(rightAnkleBalance + push, -1f, 1f);
            else if (rightIsSwing && leftAnkleBalance >= -0.001f)
                leftAnkleBalance = Mathf.Clamp(leftAnkleBalance + push, -1f, 1f);
        }

        DriveSwingOrStanceAnkle(leftIsSwing, false, leftGrounded, leftAnkleJoint,
            leftAnkleBalance, leftAnkleFlexor, leftAnkleExtensor, ref leftAnkleEffectiveInertia);
        DriveSwingOrStanceAnkle(rightIsSwing, true, rightGrounded, rightAnkleJoint,
            rightAnkleBalance, rightAnkleFlexor, rightAnkleExtensor, ref rightAnkleEffectiveInertia);

        ControlLumbarToWorldUpright(
            lumbarTargetTilt - crouchLevel * crouchTorsoLean - leanLevel * leanTorsoAngle);

        ControlNeckAndHeadToWorldUpright();
        ControlArmsForBalance(balanceSignal);
    }

    private void UpdateState(float comOffset)
    {
        float absOffset = Mathf.Abs(comOffset);
        if (absOffset > fallCoMOffset) currentState = BalanceState.Falling;
        else if (absOffset > recoveryCoMOffset) currentState = BalanceState.Recovery;
        else currentState = BalanceState.Balancing;
    }

    // Цель приседа — ступенька, скорость спуска задаёт регулятор.
    // Нет MotionIntent — стоим, стенд и лишние люди не ломаются.
    private void UpdateCrouchLevel()
    {
        float desired = 0f;
        if (intent != null)
            desired = Mathf.Clamp01(intent.crouch);
        crouchLevel = Mathf.MoveTowards(
            crouchLevel, desired, Mathf.Max(0f, crouchRatePerSecond) * Time.fixedDeltaTime);
    }

    // Наклон — ступенька −1/0/+1 с клавиатуры; плавный ход задаёт регулятор.
    private void UpdateLeanLevel()
    {
        float desired = 0f;
        if (intent != null)
            desired = Mathf.Clamp(intent.lean, -1f, 1f);
        leanLevel = Mathf.MoveTowards(
            leanLevel, desired, Mathf.Max(0f, leanRatePerSecond) * Time.fixedDeltaTime);
    }

    // Намерение — ступенька −1/0/+1, скорость подъёма задаёт регулятор.
    private void UpdateStandLegLevel()
    {
        float desired = 0f;
        if (intent != null && Mathf.Abs(intent.standLeg) > 0.5f)
            desired = 1f;
        float rate = standLegRatePerSecond;
        if (desired < 0.5f && standLegLevel > desired)
            rate = standLegReleaseRatePerSecond;
        else if (desired > 0.5f && lastLiftBlend > 0.001f
                 && stepPhaseDriver != null && stepPhaseDriver.walkActive
                 && walkLiftLevelRateScale > 1.01f)
            rate *= 1f + lastLiftBlend * (walkLiftLevelRateScale - 1f);
        standLegLevel = Mathf.MoveTowards(
            standLegLevel, desired, Mathf.Max(0f, rate) * Time.fixedDeltaTime);
    }

    // Безразмерный сигнал PD в диапазоне [-1, 1] — прямо годится в активацию мышцы.
    private float ComputeSignal(HingeJoint2D joint, float targetAngle, float pGain, float dGain)
    {
        float error = Mathf.DeltaAngle(joint.jointAngle, targetAngle) / errorReferenceDegrees;
        float speed = joint.jointSpeed / speedReferenceDegPerSec;
        return Mathf.Clamp(pGain * error - dGain * speed, -1f, 1f);
    }

    // Единое соглашение для всех суставов: положительный сигнал означает
    // «увеличить угол сустава», и это работа extensor. Проверено на колене:
    // момент против часовой (flexor) уменьшает jointAngle.
    private void ApplySignal(float signal, Muscle flexor, Muscle extensor)
    {
        if (signal > 0f)
        {
            UpdateMuscle(extensor, signal);
            UpdateMuscle(flexor, 0f);
        }
        else
        {
            UpdateMuscle(extensor, 0f);
            UpdateMuscle(flexor, -signal);
        }
    }

    // Оставлен для совместимости; все вызовы переведены на Stable PD.
    private void ControlJointToAngle(HingeJoint2D joint, float targetAngle, float pGain, float dGain, Muscle flexor, Muscle extensor)
    {
        if (joint == null || flexor == null || extensor == null) return;
        ApplySignal(ComputeSignal(joint, targetAngle, pGain, dGain), flexor, extensor);
    }

    // Таз к мировой вертикали через бёдра. Ошибка и скорость — наклон
    // и ω таза, не jointAngle/jointSpeed: смешивать мир и сустав нельзя.
    // Знак не копирует поясницу. Поясничный сустав сидит на груди, бедренный
    // — на ляжке, таз получает момент со знаком минус. Положительный сигнал
    // (extensor) крутит таз против часовой и увеличивает jointAngle.
    // Пока свинг grounded, он тоже здесь: unload смещает его сигнал в flexor,
    // опора получает чистый PD. После защёлки свинг уходит в суставную позу.
    private void ControlHipsToWorldUpright(float targetTilt, bool driveLeft, bool driveRight,
                                          float leftUnload, float rightUnload)
    {
        if (vestibularSystem == null) return;

        float tilt = vestibularSystem.GetPelvisTilt();
        float angVel = vestibularSystem.GetPelvisAngularVelocity();
        float error = Mathf.DeltaAngle(tilt, targetTilt) / Mathf.Max(1f, pelvisErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float signal = Mathf.Clamp(pelvisPGain * error - pelvisDGain * speed, -1f, 1f);

        if (driveLeft)
            ApplySignal(WithHipUnload(signal, leftUnload), leftHipFlexor, leftHipExtensor);
        if (driveRight)
            ApplySignal(WithHipUnload(signal, rightUnload), rightHipFlexor, rightHipExtensor);
    }

    // unload=0 — тот же сигнал, без лишнего Clamp: путь двух опор побитово
    // совпадает с прежним. Минус unload тянет в flexor (уменьшает jointAngle).
    private static float WithHipUnload(float signal, float unload)
    {
        if (unload == 0f) return signal;
        return Mathf.Clamp(signal - unload, -1f, 1f);
    }

    // Свинг в воздухе: стопа перпендикулярна голени (ankle 0). На земле toe-off
    // и stance-only CoM задаются выше, до вызова этого метода.
    private void DriveSwingOrStanceAnkle(bool isSwing, bool isForwardSwing, bool grounded,
                                         HingeJoint2D joint, float ankleBalance,
                                         Muscle flexor, Muscle extensor, ref float ankleInertia)
    {
        if (isSwing && grounded && !swingHipLatched && isForwardSwing
            && stepPhaseDriver != null && stepPhaseDriver.walkActive
            && lastLiftBlend > 0.35f && walkSwingToeOffMax > 0.1f)
        {
            float toe = lastLiftBlend * lastLiftBlend * walkSwingToeOffMax;
            ControlJointToAngleStable(joint, toe, kneePGain, kneeDGain,
                errorReferenceDegrees, flexor, extensor, ref ankleInertia);
            return;
        }
        if (isSwing && grounded && !swingHipLatched && isForwardSwing
            && swingAnkleGroundedToeOff > 0.1f)
        {
            ControlJointToAngleStable(joint, swingAnkleGroundedToeOff, kneePGain, kneeDGain,
                errorReferenceDegrees, flexor, extensor, ref ankleInertia);
            return;
        }
        if (isSwing && swingHipLatched && !grounded)
        {
            // jointAngle = shin.rot − foot.rot. Горизонталь стопы (foot≈0) →
            // цель = угол голени; иначе 0 тянет носок вниз при сгибе колена.
            float airAnkle = 0f;
            if (walkSwingAirLevel > 0.5f && joint != null && joint.connectedBody != null)
                airAnkle = Mathf.DeltaAngle(0f, joint.connectedBody.rotation);
            ControlJointToAngleStable(joint, airAnkle, hipPGain, hipDGain,
                errorReferenceDegrees, flexor, extensor, ref ankleInertia);
            return;
        }
        ControlAnkleForBalance(joint, ankleBalance, flexor, extensor);
    }

    // Поясница держит грудь к мировой вертикали. Ошибка и скорость —
        // абсолютный наклон и угловая скорость торса, не угол сустава:
        // когда таз заваливается, грудь должна упереться, а не ехать вместе с ним.
        // Входы как у рабочей версии (error = target − tilt, скорость мировая).
        // Коэффициенты плюсовые, поэтому в P*e − D*ω они входят со знаком минус:
        // это ровно старые P=−2 и D=−0.3, а не новый демпфер.
    private void ControlLumbarToWorldUpright(float targetTilt)
    {
        if (lumbarFlexor == null || lumbarExtensor == null) return;
        if (vestibularSystem == null) return;

        float tilt = vestibularSystem.GetBodyTilt();
        float angVel = vestibularSystem.GetBodyAngularVelocity();
        float error = Mathf.DeltaAngle(tilt, targetTilt) / Mathf.Max(1f, lumbarErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float signal = Mathf.Clamp((-lumbarPGain) * error - (-lumbarDGain) * speed, -1f, 1f);
        ApplySignal(signal, lumbarFlexor, lumbarExtensor);
    }

    // Шея и голова к мировой вертикали. Раньше они держали угол к родителю
    // (ControlJointToAngle к нулю), то есть повторяли за торсом любой его завал
    // — та же ошибка, которую уже исправили у поясницы и у таза.
    //
    // Держал их не регулятор, а грубая сила: момент 15 Н·м на парной инерции
    // 0.0015 кг·м² даёт около 9900 рад/с², и PD разворачивал сустав каждый шаг
    // физики. Голова ходила на 9.8° размаха с частотой 25 Гц. Но эта долбёжка
    // была несущей: она приваривала 5.7 кг верха к груди, и на ней держался
    // толчок вперёд. Просто уменьшить момент нельзя — проверено, порог падает
    // с 21 Н·с ниже 18 (метки `nm*`). Сначала цель, потом момент.
    //
    // Оба сустава сидят на своём сегменте, как поясничный на груди, поэтому
    // знак копируется с поясницы, а не с таза: плюсовые P и D входят как −P и −D.
    private void ControlNeckAndHeadToWorldUpright()
    {
        if (vestibularSystem == null) return;

        ControlSegmentToWorldUpright(
            vestibularSystem.GetNeckTilt(), vestibularSystem.GetNeckAngularVelocity(),
            neckJoint, neckFlexor, neckExtensor, ref neckEffectiveInertia);
        ControlSegmentToWorldUpright(
            vestibularSystem.GetHeadTilt(), vestibularSystem.GetHeadAngularVelocity(),
            headJoint, headFlexor, headExtensor, ref headEffectiveInertia);
    }

    private void ControlSegmentToWorldUpright(float tilt, float angVel, HingeJoint2D joint,
                                              Muscle flexor, Muscle extensor, ref float cachedInertia)
    {
        if (flexor == null || extensor == null) return;

        // Устойчивый PD (Tan, Liu, Turk, 2011): ошибку берём по состоянию на
        // следующем шаге, а не на текущем, и делим сигнал на (1 + K·Δt/I).
        // Обычный явный PD расходится, когда K·Δt/I переваливает за 2 — то же
        // правило, что записано в balance-actuators для JointFriction. У шеи
        // это отношение равно 5.7 при инерции 0.0015 кг·м², отсюда и долбёжка
        // до 3.3° за шаг физики. Знаменатель гасит перелёт, а предсказанный
        // угол добавляет опережение по фазе, поэтому демпфер можно держать
        // сильным: он нужен, чтобы голова не улетала в упор при толчке.
        float tiltForError = tilt;
        float damping = 0f;

        if (useStablePd)
        {
            float dt = Time.fixedDeltaTime;
            tiltForError = tilt + angVel * dt;

            if (cachedInertia <= 0f)
                cachedInertia = EffectiveInertia(joint);

            if (cachedInertia > 0f)
            {
                // K в Н·м·с/рад: нормированный dGain × потолок мышцы, переведённый
                // из градусов в радианы. Иначе отношение не безразмерно.
                float k = neckDGain * flexor.maxTorque * Mathf.Rad2Deg
                          / Mathf.Max(1f, speedReferenceDegPerSec);
                damping = k * dt / cachedInertia;
            }
        }

        float error = Mathf.DeltaAngle(tiltForError, neckTargetTilt) / Mathf.Max(1f, neckErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float raw = (-neckPGain) * error - (-neckDGain) * speed;
        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        ApplySignal(signal, flexor, extensor);
    }

    // Базовая поза плюс контрперенос от CoM. На walk — противофазный мах
    // по фазе Stance; CoM-balance на walk выключен (fold после swap).
    private void ControlArmsForBalance(float balanceSignal)
    {
        float lSh, rSh, lEl, rEl, lWr, rWr;
        if (stepPhaseDriver != null && stepPhaseDriver.walkActive)
            ComputeWalkArmTargets(out lSh, out rSh, out lEl, out rEl, out lWr, out rWr);
        else if (crouchLevel > 0.001f && standLegLevel < 0.01f)
            ComputeCrouchArmTargets(balanceSignal, out lSh, out rSh, out lEl, out rEl, out lWr, out rWr);
        else
        {
            float armSignal = standLegLevel > 0.01f ? 0f : balanceSignal;
            lSh = rSh = Mathf.Clamp(
                shoulderBaseAngle + armShoulderBalanceGain * armSignal, -85f, 85f);
            lEl = rEl = Mathf.Clamp(
                elbowBaseAngle + armElbowBalanceGain * armSignal, -135f, -5f);
            lWr = rWr = Mathf.Clamp(
                wristBaseAngle + armWristBalanceGain * armSignal, -55f, 55f);
        }

        ApplyArmPose(lSh, rSh, lEl, rEl, lWr, rWr);
    }

    // Руки вниз к земле: минус у плеча — вперёд-вниз; локоть почти прямой.
    // Плюс у левого плеча (как на walk) уводил руку назад-вверх.
    private void ComputeCrouchArmTargets(float balanceSignal,
                                         out float leftShoulder, out float rightShoulder,
                                         out float leftElbow, out float rightElbow,
                                         out float leftWrist, out float rightWrist)
    {
        float t = Mathf.Clamp01(crouchLevel);
        float sh = crouchArmShoulder * t;
        float leftBaseShoulder = Mathf.Lerp(shoulderBaseAngle, shoulderBaseAngle - sh * 1.08f, t);
        float rightBaseShoulder = Mathf.Lerp(shoulderBaseAngle, shoulderBaseAngle - sh * 0.92f, t);
        float baseElbow = Mathf.Lerp(elbowBaseAngle, crouchArmElbow, t);
        float baseWrist = Mathf.Lerp(wristBaseAngle, crouchArmWrist, t);

        float supportStart = Mathf.Clamp01(crouchHandPoseStart);
        float supportBlend = Mathf.InverseLerp(supportStart, 1f, t);
        float balance = Mathf.Clamp(balanceSignal, -1f, 1f);
        float spread = supportBlend * (crouchHandSupportSpread + crouchHandBalanceSpread * Mathf.Abs(balance));
        float centerShoulder = Mathf.Lerp(
            0.5f * (leftBaseShoulder + rightBaseShoulder),
            -Mathf.Abs(crouchHandSupportShoulder),
            supportBlend);

        if (balance >= 0f)
        {
            leftShoulder = centerShoulder - spread;
            rightShoulder = centerShoulder + spread;
        }
        else
        {
            leftShoulder = centerShoulder + spread;
            rightShoulder = centerShoulder - spread;
        }

        leftElbow = Mathf.Lerp(baseElbow, crouchHandSupportElbow, supportBlend);
        rightElbow = Mathf.Lerp(baseElbow, crouchHandSupportElbow, supportBlend);
        leftWrist = Mathf.Lerp(baseWrist, crouchHandSupportWrist, supportBlend);
        rightWrist = Mathf.Lerp(baseWrist, crouchHandSupportWrist, supportBlend);

        leftShoulder = Mathf.Clamp(leftShoulder, -85f, 85f);
        rightShoulder = Mathf.Clamp(rightShoulder, -85f, 85f);
        leftElbow = Mathf.Clamp(leftElbow, -135f, -5f);
        rightElbow = Mathf.Clamp(rightElbow, -135f, -5f);
        leftWrist = Mathf.Clamp(leftWrist, -55f, 55f);
        rightWrist = Mathf.Clamp(rightWrist, -55f, 55f);
    }

    // Противофаза: левый свинг → правое плечо вперёд (− shoulder).
    private void ComputeWalkArmTargets(out float leftShoulder, out float rightShoulder,
                                       out float leftElbow, out float rightElbow,
                                       out float leftWrist, out float rightWrist)
    {
        leftShoulder = shoulderBaseAngle;
        rightShoulder = shoulderBaseAngle;
        leftElbow = elbowBaseAngle;
        rightElbow = elbowBaseAngle;
        leftWrist = wristBaseAngle;
        rightWrist = wristBaseAngle;

        if (stepPhaseDriver == null || walkArmShoulderSwing < 0.1f)
            return;

        float standSign;
        float swing;
        int phase = stepPhaseDriver.PhaseCode;
        float standCmd = intent != null ? intent.standLeg : 0f;

        if (phase == 0 && Mathf.Abs(standCmd) > 0.5f)
        {
            float age = stepPhaseDriver.PhaseAge();
            float dur = Mathf.Max(0.1f, stepPhaseDriver.stanceDuration);
            swing = Mathf.Sin(Mathf.Clamp01(age / dur) * Mathf.PI);
            standSign = standCmd;
        }
        else if (phase == 1 && walkArmTransferCarry > 0.001f
                 && Mathf.Abs(stepPhaseDriver.CurrentStanceSign) > 0.5f)
        {
            // Transfer: затухающий хвост маха, не обрыв к базе.
            float age = stepPhaseDriver.PhaseAge();
            float decayDur = Mathf.Min(1.5f, stepPhaseDriver.transferMaxDuration);
            float decay = 1f - Mathf.Clamp01(age / Mathf.Max(0.05f, decayDur));
            swing = walkArmTransferCarry * decay;
            standSign = stepPhaseDriver.CurrentStanceSign;
        }
        else
            return;

        ApplyWalkArmSwing(standSign, swing,
            out leftShoulder, out rightShoulder, out leftElbow, out rightElbow,
            out leftWrist, out rightWrist);
    }

    private void ApplyWalkArmSwing(float standSign, float swing,
                                   out float leftShoulder, out float rightShoulder,
                                   out float leftElbow, out float rightElbow,
                                   out float leftWrist, out float rightWrist)
    {
        leftShoulder = shoulderBaseAngle;
        rightShoulder = shoulderBaseAngle;
        leftElbow = elbowBaseAngle;
        rightElbow = elbowBaseAngle;
        leftWrist = wristBaseAngle;
        rightWrist = wristBaseAngle;

        if (swing < 0.001f)
            return;

        float amp = walkArmShoulderSwing * swing;
        float contra = -Mathf.Sign(standSign);
        float rightDelta = contra * amp;
        float leftDelta = -contra * amp;

        leftShoulder = Mathf.Clamp(shoulderBaseAngle + leftDelta, -85f, 85f);
        rightShoulder = Mathf.Clamp(shoulderBaseAngle + rightDelta, -85f, 85f);

        if (walkArmElbowSwing > 0.01f)
        {
            float elAmp = walkArmElbowSwing * swing;
            if (rightDelta < -0.01f)
                rightElbow = Mathf.Clamp(elbowBaseAngle - elAmp, -135f, -5f);
            if (leftDelta < -0.01f)
                leftElbow = Mathf.Clamp(elbowBaseAngle - elAmp, -135f, -5f);
        }

        leftWrist = Mathf.Clamp(wristBaseAngle + leftDelta * 0.15f, -55f, 55f);
        rightWrist = Mathf.Clamp(wristBaseAngle + rightDelta * 0.15f, -55f, 55f);
    }

    private void ApplyArmPose(float leftShoulder, float rightShoulder,
                              float leftElbow, float rightElbow,
                              float leftWrist, float rightWrist)
    {
        ControlJointToAngleStable(
            leftShoulderJoint, leftShoulder, shoulderPGain, shoulderDGain,
            shoulderErrorReferenceDegrees, leftShoulderFlexor, leftShoulderExtensor,
            ref leftShoulderEffectiveInertia);
        ControlJointToAngleStable(
            rightShoulderJoint, rightShoulder, shoulderPGain, shoulderDGain,
            shoulderErrorReferenceDegrees, rightShoulderFlexor, rightShoulderExtensor,
            ref rightShoulderEffectiveInertia);

        ControlJointToAngleStable(
            leftElbowJoint, leftElbow, elbowPGain, elbowDGain,
            elbowErrorReferenceDegrees, leftElbowFlexor, leftElbowExtensor,
            ref leftElbowEffectiveInertia);
        ControlJointToAngleStable(
            rightElbowJoint, rightElbow, elbowPGain, elbowDGain,
            elbowErrorReferenceDegrees, rightElbowFlexor, rightElbowExtensor,
            ref rightElbowEffectiveInertia);

        ControlJointToAngleStable(
            leftWristJoint, leftWrist, wristPGain, wristDGain,
            wristErrorReferenceDegrees, leftWristFlexor, leftWristExtensor,
            ref leftWristEffectiveInertia);
        ControlJointToAngleStable(
            rightWristJoint, rightWrist, wristPGain, wristDGain,
            wristErrorReferenceDegrees, rightWristFlexor, rightWristExtensor,
            ref rightWristEffectiveInertia);
    }

    // Устойчивый PD к углу сустава. Формула та же, что у шеи, знак — как у
    // ControlJointToAngle: P*error − D*speed. Знаменатель 1+K·Δt/I всегда,
    // без флага useStablePd: локоть и кисть иначе входят в предельный цикл.
    private void ControlJointToAngleStable(HingeJoint2D joint, float targetAngle,
                                           float pGain, float dGain, float errorRef,
                                           Muscle flexor, Muscle extensor, ref float cachedInertia)
    {
        if (joint == null || flexor == null || extensor == null) return;

        float dt = Time.fixedDeltaTime;
        float angleForError = joint.jointAngle + joint.jointSpeed * dt;
        float damping = 0f;

        if (cachedInertia <= 0f)
            cachedInertia = EffectiveInertia(joint);

        if (cachedInertia > 0f)
        {
            float k = dGain * flexor.maxTorque * Mathf.Rad2Deg
                      / Mathf.Max(1f, speedReferenceDegPerSec);
            damping = k * dt / cachedInertia;
        }

        float error = Mathf.DeltaAngle(angleForError, targetAngle) / Mathf.Max(1f, errorRef);
        float speed = joint.jointSpeed / speedReferenceDegPerSec;
        float raw = pGain * error - dGain * speed;
        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        ApplySignal(signal, flexor, extensor);
    }

    // Парная инерция сустава: оба тела крутятся навстречу, поэтому в знаменатель
    // идёт 1/(1/I₁ + 1/I₂), а не инерция одного сегмента. У головы связана лёгкая
    // шея, и парная инерция втрое меньше собственной — считать по одному телу
    // означает втрое занизить жёсткость.
    private static float EffectiveInertia(HingeJoint2D joint)
    {
        if (joint == null) return 0f;

        Rigidbody2D self = joint.attachedRigidbody;
        Rigidbody2D connected = joint.connectedBody;
        float a = self != null ? self.inertia : 0f;
        float b = connected != null ? connected.inertia : 0f;

        if (a <= 0f) return b;
        if (b <= 0f) return a;
        return 1f / (1f / a + 1f / b);
    }

    // Голеностоп — не позиционный сустав, а маятниковый привод: он получает
    // только момент против смещения центра масс. Пружину и вязкость к целевому
    // углу пришлось убрать: стопа прижата к земле, поэтому момент мышцы уходит
    // не в поворот сустава, а в разворот всего тела, и «демпфирование» угла
    // раскачивало человека до падения за 3 секунды. Вязкость даёт JointFriction.
    // Команда, которая вдавливает сустав в последние ankleLimitMargin градусов
    // до упора, обнуляется: иначе стопа клинит на ±45° и момента «назад» нет.
    private void ControlAnkleForBalance(HingeJoint2D joint, float balanceCommand, Muscle flexor, Muscle extensor)
    {
        if (joint == null || flexor == null || extensor == null) return;
        float signal = ProtectJointLimit(joint, Mathf.Clamp(balanceCommand, -1f, 1f));
        ApplySignal(signal, flexor, extensor);
    }

    private float ProtectJointLimit(HingeJoint2D joint, float signal)
    {
        if (!joint.useLimits) return signal;

        float angle = joint.jointAngle;
        float min = joint.limits.min;
        float max = joint.limits.max;
        // Положительный сигнал увеличивает угол, отрицательный уменьшает.
        if (signal > 0f && angle >= max - ankleLimitMargin) return 0f;
        if (signal < 0f && angle <= min + ankleLimitMargin) return 0f;
        return signal;
    }

    private void UpdateMuscle(Muscle muscle, float targetActivation)
    {
        if (muscle == null) return;
        // Мгновенное обнуление давало bang-bang: extensor→0 за шаг, flexor
        // включается с полным моментом — limit cycle на лёгких суставах.
        muscle.activation = Mathf.MoveTowards(
            muscle.activation, targetActivation,
            muscleActivationSpeed * Time.fixedDeltaTime);
    }

    private void RelaxAllMuscles()
    {
        UpdateMuscle(leftHipFlexor, 0f); UpdateMuscle(leftHipExtensor, 0f);
        UpdateMuscle(rightHipFlexor, 0f); UpdateMuscle(rightHipExtensor, 0f);
        UpdateMuscle(leftKneeFlexor, 0f); UpdateMuscle(leftKneeExtensor, 0f);
        UpdateMuscle(rightKneeFlexor, 0f); UpdateMuscle(rightKneeExtensor, 0f);
        UpdateMuscle(leftAnkleFlexor, 0f); UpdateMuscle(leftAnkleExtensor, 0f);
        UpdateMuscle(rightAnkleFlexor, 0f); UpdateMuscle(rightAnkleExtensor, 0f);
        UpdateMuscle(lumbarFlexor, 0f); UpdateMuscle(lumbarExtensor, 0f);
        UpdateMuscle(neckFlexor, 0f); UpdateMuscle(neckExtensor, 0f);
        UpdateMuscle(headFlexor, 0f); UpdateMuscle(headExtensor, 0f);
        UpdateMuscle(leftShoulderFlexor, 0f); UpdateMuscle(leftShoulderExtensor, 0f);
        UpdateMuscle(rightShoulderFlexor, 0f); UpdateMuscle(rightShoulderExtensor, 0f);
        UpdateMuscle(leftElbowFlexor, 0f); UpdateMuscle(leftElbowExtensor, 0f);
        UpdateMuscle(rightElbowFlexor, 0f); UpdateMuscle(rightElbowExtensor, 0f);
        UpdateMuscle(leftWristFlexor, 0f); UpdateMuscle(leftWristExtensor, 0f);
        UpdateMuscle(rightWristFlexor, 0f); UpdateMuscle(rightWristExtensor, 0f);
    }
}