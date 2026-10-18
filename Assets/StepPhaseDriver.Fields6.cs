using UnityEngine;

public partial class StepPhaseDriver
{
    // The ladder above still cannot see two poses. (1) A fold that already
    // sits inside TiltSettled + signed lead ≤ plantComLeadMax — open defect 8:
    // CoM over the feet, trunk calm, pelvis on the ground. BeginCycle then
    // calls that a stand, CaptureStandHeight records the folded CoM as the
    // bar, and Fall starts from a body that has already collapsed. Arm the
    // sink guard from the measured spawn CoM (idle FixedUpdate after
    // BuildHuman, and BeginWalk before walkActive — Start runs before
    // BuildHuman). Do not floor with the pelvis spawn column: that sits
    // ~0.28 m above live CoM and reads quiet stance as collapsed.
    // Keep the measured floor under every later CaptureStandHeight, fail the gate,
    // and surrender cycle-0 Plant to two-foot quiet stance. (2) The last
    // rung required both feet down, so a single-support stall (air start,
    // one foot landed past CoM, the other still up) waited forever. Any
    // contact is enough to hand the quiet two-foot controller the airborne
    // leg; still no surrender in the air. Happy path never reaches either
    // rung: a quiet BeginWalk is not sunk (pelvisDrop 0.0017 vs
    // sinkAbortDrop 0.25) and opens the gate inside plantMinDuration.
    // (3) Fall abort / Reach stall / Catch bounce called EnterStandingPlant
    // with plantStandSurrender false, so a body already at fallAbortDeg
    // (30°) spent plantStallStandSeconds (3 s) in split Plant — that is
    // the fold, not a restore. Immediate two-foot surrender when the
    // pose is already a heap: abort tilt, sink, or cycle-0 |CoM−stance|
    // past RecoverComLostMeters (0.30, the same Fall band). (4) Cycle-0
    // PlantComReady is signed, and the forward bound is RecoverComLostMeters
    // not plantComLeadMax. 0.10 m still shut the gate on the ~10 cm
    // third-law shift from initiation, raised StandingRestore, and killed
    // rock / lean / geo-lift — walkSwap stayed 0. A cold lunge still
    // surrenders at 0.30 m. Cycle-0 tilt allows the commanded Plant lean
    // so 9° initiation cannot fail the 12° TiltSettled band; omega is not
    // in that gate (a 9° lean over plantMinDuration already makes ~18 °/s
    // and a flicker of 40 °/s latched the brake). (5) Cycle-0 FallLeadReady
    // does not wait for 1.5 cm of capture-point lead: stacked feet at rest
    // never grow that number, so Plant sat 1.5 s split and folded. Age is
    // plantMinDuration. Later cycles keep the trigger / plantLeadWait.
    // (6) Cycle-0 Reach/Catch plant is swing-ahead-of-stance, not
    // CoM-ahead: stacked feet fail catchFootLeadMin vs CoM. (7) Cycle-0
    // Fall/Reach abort at fallAbortDeg used to EnterStandingPlant: a 30°
    // lean IS the first fall, so restoring Plant cancelled Catch, Recover
    // never ran, walkSwap stayed 0, travelX 0, fold ~4 s. Cycle 0 Catchs
    // that lean. (8) Catch/Recover judged IsSwingFootPlanted (heel): geo
    // lift pitches the stacked sole onto the toe, so swingDown stayed
    // false and even stall-Catch had no Recover exit. Cycle 0 uses
    // grounded. Later cycles keep heel-first and abort-to-Plant.
    // (9) Those Catchs still left walkSwap=0. PlantTiltReady 17° and
    // RecoverPoseIsLost at fallAbortDeg shut Plant→Fall on the same
    // 30° lean Fall now Catchs — StandingRestore killed geo-lift
    // before the first Fall. Airborne Catch (geo clearance past the
    // 0.06 m probe) bounced Reach at catchMax because every Recover
    // exit needs swingDown; bounceMax then Planted. Grounded Catch
    // ran ApplyRecoverBrake + 0.15×catchKnee on stacked soles, which
    // is a landing snap under the hip, not a 4 cm plant. Cycle 0:
    // Plant tilt does not block Fall; abort tilt is not a lost pose;
    // Fall goes Catch (skip the 1.5 s Reach stall); airborne Catch
    // waits for the sole; Recover on span ≥ FootLeadDeadzone.
    // (10) firstStepPlace Catch then wrote reachKneeFlex 140° after
    // the geo lift. Geo assign needs both soles down, so the first
    // airborne frame of a successful lift took a 140° tuck, vault
    // yield dropped the stance strut, and sinkAbort Planted at ~4 s
    // with walkSwap still 0. Cycle 0 Catch keeps the Fall/geo knee,
    // holds the vault, and ThighHold so the 4 cm placement can land.
    // (11) That still left walkSwap=0. walkStepPlaceOnFold last-wrote
    // Asin(0.04/0.78)≈3° over ThighHold's 16–32° lift hip the moment
    // the knee was ≥20°, and grounded Catch (IsFirstStepPlanted) skipped
    // the air hipLift write, so the thigh dropped on Fall→Catch. µ=1.2
    // never grew FootLeadDeadzone. Cycle 0 Plant/Fall/Catch keeps
    // ThighHold (no 3° place) and Catch keeps hipLift 0.5 until Recover.
    // (12) That still left walkSwap=0. Geo ~50° for 8 cm is the lift;
    // Catch's 40° cap is 3 cm longer — the 0.06 m probe — so the first
    // airborne frame replanted and Recover never saw FootLeadDeadzone.
    // Cycle 0 Catch holds the last grounded geo fold (not 40° / 140°)
    // and skips landing-knee straighten until Recover.
    // (13) That still left walkSwap=0. 11:19 read lastFirstStepGeoKnee
    // from static ComputeFallCatchBalance (does not compile) and only
    // CAPPED 140° — landDeg 35° is below the cap, so the first air
    // frame without catchAirFold replanted; if the cap did hold 8 cm
    // the hovering sole never made swingDown and Catch had no exit.
    // Cycle 0 Catch ASSIGNS the latched standing geo until span ≥
    // FootLeadDeadzone, then landDeg so the 0.06 m probe reads
    // grounded and Recover can swap.
    // (14) That still left walkSwap=0. landDeg 35° with hipLift 0.5
    // still hovered ~8 cm (thigh 16–32° holds the ankle up);
    // Catch requires swingDown, so Recover never ran and sinkAbort
    // Planted at ~4 s. Cycle 0 Catch-ahead drops hipLift / ThighHold
    // / forceSwingFlex (same hipair clear as Recover) so gravity
    // plants the 2 cm span; vault stays. Until ahead, geo + lift hold.
    // (15) That still left walkSwap=0. ThighHold's hold term is 0 at
    // 16–32° (only hipLift floors the cap). Zeroing lift made the hip
    // limp: the sole retracted below FootLeadDeadzone before the 0.06 m
    // probe, and ending firstStepHoldLift on ahead re-armed the 3°
    // place unwind on the same frame. landDeg 35° at a 32° thigh is
    // still ~8 cm short. Cycle 0 Catch-ahead KEEPS hipLift / ThighHold
    // (span) and last-writes LandingKneeFlexDeg so the shin covers the
    // remaining clearance on that hip. firstStepHoldLift stays for the
    // whole Catch (no 3° place). Vault stays. Until ahead, geo + lift.
    // (16) That still left walkSwap=0. Geometry: at standing hip height
    // a 32° thigh cannot reach the ground (0.86·cos32° = 0.73 m, ~13 cm
    // hover). LandingKneeFlexDeg at that leftover asks for ~6° and
    // shortens vertical reach further. Catch has no cycle-0 air exit,
    // sinkAbort ~4 s. Cycle-0 Catch-air plants a matched 15°/15° pose
    // (hip flexion minus, knee plus): inside the 0.06 m probe with
    // ~7 cm span. firstStepHoldLift still blocks the 3° place. hipLift
    // drops only in Catch-air so ThighHold does not hold 32°; thighHold
    // stays so forceSwingFlex does not take −swingHipFlex. Grounded
    // Catch keeps geo + lift until the probe clears. Vault stays.
    // (17) That still left walkSwap=0. 15°/15° plants for one 5 ms
    // frame, then grounded Catch restored hipLift 0.5 (air-lift-hold
    // / Fall lift) and geo ~50°. The sole left the 0.06 m probe again;
    // Recover needs swingDown ∧ span ≥ FootLeadDeadzone for catchMin
    // (0.22 s); sinkAbort ~4 s. Cycle-0 Catch latches the plant after
    // the first airborne frame and holds hipLift 0 through contact.
    // (18) That still left walkSwap=0. 15°/15° matched is 1.5 cm short
    // of GetContacts at standing hip height (useGetContactsProbe, not
    // the 0.06 m box). Combined contactOffset 2 cm only fires on the
    // SPD overshoot through a longer chain. Recover then wrote
    // catchKnee / ApplyRecoverBrake and lifted that sole before
    // RecoverTouchdownMinAge. Cycle-0 plants 8°/6° (shortfall ~4 mm,
    // span ~7.5 cm), latches through Recover, skips catchMin (the
    // 0.05 s Recover hold is the plant). Until air, geo + lift. Vault
    // stays.
    // (19) That still left walkSwap=0. 8°/6° at standing hip height
    // sits ~4 mm above geometric overlap. GetContacts (not the 0.06 m
    // box) then never reports the sole, Catch never sees swingDown,
    // Recover never runs, sinkAbort ~4 s. Cycle 0 treats clearance
    // inside defaultContactOffset as planted — the 4 mm hover IS the
    // contact the offset exists for. Hip SPD to 8° wins over hipair
    // on that same Catch-air frame.
    // (20) That still left walkSwap=0. Geo lift aimed at 8 cm (the old
    // OverlapBox) is a ~50° squat; DualLoad never opens, the stance
    // sags with the fold, GetContacts stays true, Catch never latches
    // 8°/6°, Recover never sees ahead. Cycle 0 geo-lifts 2.5 cm (~28°,
    // past both contactOffsets), plants 10°/10° on a 10° stance-knee
    // hip drop (reach matches H, span ~4.5 cm), latches on geometric
    // separation not GetContacts-only, and PickStanceFromFeet counts
    // NearContact so the swap hands the front foot. Hip SPD to 10°
    // still wins over hipair. Vault / firstStepHoldLift stay.
    // (21) That still left walkSwap=0. Geo 2.5 cm on a loaded stacked
    // sole is still a squat: DualLoad never opens, the hip drops with
    // the fold, GetContacts stays true, Separated never latches, the
    // 10°/10° SPD never runs, sinkAbort ~4 s. Cycle 0 skips geo and
    // the 140° Fall pin. Fall commands the 10°/10° plant (hip SPD,
    // matching stance knee), Catch latches on entry, liftGateBlend = 1
    // so the assigned swing is trailing (unload / friction / cap).
    // Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (22) That still left walkSwap=0. 10°/10° on dirt cannot make
    // FootLeadDeadzone: both soles share the load, µ=1.2 friction
    // exceeds hip horizontal force, stance 10° keeps both chains the
    // same length. Cycle 0 Plant/Fall/Catch grounded: 55° swing fold
    // on a knee-0 stance strut (DrivePelvis stays). Latch 8°/6° on
    // the first airborne frame, not Catch entry. Recover still needs
    // span ≥ FootLeadDeadzone. Vault / firstStepHoldLift stay.
    // footFriction stays 1.2.
    // (23) That still left walkSwap=0. Comment 22 wrote DrivePelvis
    // stays, but Fall reachKneeFolding drops it the moment reachKnee
    // > 0.5°, and ApplyFirstStepCycle0Pose also wrote hipLift /
    // ThighHold / forceSwingFlex. Hip flexor on a loaded µ=1.2 sole
    // is sci_a: pelvis drops with the 55° fold, GetContacts never
    // clears, 8°/6° never latches, sinkAbort ~4 s. Cycle 0 grounded:
    // DrivePelvis stays (reachKneeFolding off, plantHipRelease 0),
    // knee-only 55°, stance vault, ankleLift unweight. Latch 8°/6°
    // on the first airborne frame. Recover still needs span ≥
    // FootLeadDeadzone. Vault / firstStepHoldLift stay. footFriction
    // stays 1.2.
    // (24) That still left walkSwap=0. Knee-only 55° on dirt is still
    // a squat: DrivePelvis holds thigh orientation, not hip height.
    // Quiet vault 6°→0 is ~1 mm; 55° shortens hip-to-ankle ~10 cm,
    // both soles stay loaded, µ=1.2 cannot slide, GetContacts never
    // clears. Plant walkPlantSwingKneeFirst also seeded lastPlant
    // 55°, so fold-hold restored the squat after ApplyFirstStep
    // zeroed it. Cycle 0 grounded: no 55° last-write (oneg
    // grounded-knee / unload / cap via liftGateBlend=1), DrivePelvis
    // stays, vault, ankleLift. Latch 8°/6° on the first airborne
    // frame. Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (25) That still left walkSwap=0. liftGateBlend=1 was a TIMER,
    // not a load: DualLoadMinSpan 0.04 sat above the 3 cm viewpoint
    // lunge (hipHalfSpacing·cos80°·2), FallLeadReady was auto-true,
    // so oneg 16° / unload 0.32 ran at 50/50. µN at half weight still
    // sticks. DualLoadMinSpan = FootLeadDeadzone (0.02). Fall waits
    // for CoM 1.5 cm past the already-leading right foot. Blend is
    // measured frontShare. 55° / unload only after frontShare ≥ 0.5
    // (N≈0, µ does not stick). Air latches 8°/6°. Recover still
    // needs span ≥ FootLeadDeadzone. Vault / firstStepHoldLift stay.
    // footFriction stays 1.2.
    // (26) That still left walkSwap=0. frontShare ≥ 0.5 on a 3 cm
    // lunge IS rest: (x_com − x_trail)/span = 0.5 at mid-stance, and
    // RMS CoM 5 mm flickers across that gate. Unload 0.32 / 55° ran
    // at 50/50; plantLeadWait then Fall'd the same squat; ApplyFirstStep
    // last-wrote reachKnee 0 on dirt so leftover never survived.
    // DualLoad cycle 0 ramps from StanceLead onto the leading foot
    // (N≈0), not the 3 cm share. FallLeadReady cycle 0 has no
    // deadline. ApplyFirstStep keeps leftover once CoM is on that
    // foot. Air latches 8°/6°. Recover still needs span ≥
    // FootLeadDeadzone. Vault / firstStepHoldLift stay. footFriction
    // stays 1.2.
    // (27) That still left walkSwap=0. FallLeadReady waited for
    // Capture ≥ 1.5 cm PAST a foot that already sits 1.5 cm ahead of
    // CoM. The ankle CoM loop holds RMS 5 mm at mid-stance and
    // cancels the 9° Plant lean, so that 3 cm roll never happens:
    // Plant never EnterFall, DualLoad/55° stay shut (StanceLead < 0),
    // sinkAbort ~4 s, walkSwap=0, travelX=0. Cycle 0: fade the ankle
    // hold until CoM is ON that foot; FallLeadReady is StanceLead ≥ 0
    // (not Capture ≥ trigger, not a deadline). DualLoad / 55° still
    // wait for that same event (trail N≈0). Air latches 8°/6°.
    // Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (28) That still left walkSwap=0. Comment 27's ankleScale fade
    // is the actuator of plantRockLead: at rest StanceLead ≈ −1.5 cm
    // so scale went to 0 on frame 1, the leading sole could not
    // dorsiflex, and the 9° trunk lean's third-law shoved the pelvis
    // back. DualLoad then used StanceLead / trigger, which is also 0
    // at the gate, so even a late arrival kept cap/unload shut.
    // Cycle 0: ankleScale stays 1 (rock + lean roll CoM onto the
    // leading foot). FallLeadReady is still StanceLead ≥ 0. DualLoad
    // is 1 at that same event, not StanceLead / trigger. Air latches
    // 8°/6°. Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (29) That still left walkSwap=0. Rock + 9° lean are multiplied
    // by calm² (~0.3 at 9°). Cycle 0 skipped trailing A2 / hip
    // ("no rear arm") while walkPlantStanceDriveFirst extended the
    // LEADING hip at StanceLead ≈ −1.5 cm — that hip is a brake
    // until CoM has arrived. The 3 cm lunge IS the rear arm. Cycle 0:
    // trailing A2 + hip on, leading-hip drive only once StanceLead ≥ 0,
    // trailWindow doses by the 3 cm span not 8 cm. FallLeadReady /
    // DualLoad still StanceLead ≥ 0. Air latches 8°/6°. Recover still
    // needs span ≥ FootLeadDeadzone. Vault / firstStepHoldLift stay.
    // footFriction stays 1.2.
    // (30) That still left walkSwap=0. RecoverBlend is raw² with
    // tiltN = tilt/(0.7·abort): at the 9° initiation, calm ≈ 0.33, so
    // plantRockLead 3 cm arrives as 1 cm and the mid-stance ankle loop
    // wins. Comment 29's trailing A2 on a 3 cm lunge / 26 cm foot puts
    // CoP at the toe, ahead of CoM — a brake, not a rear arm. Cycle 0:
    // rock not × calm (Plant and the Fall hold), trailing A2 / hip
    // later-cycle only, leading-hip drive still only once StanceLead ≥ 0.
    // FallLeadReady / DualLoad still StanceLead ≥ 0. Air latches 8°/6°.
    // Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (31) That still left walkSwap=0. Rock without calm is a couple on
    // a µ=1.2 stacked pair: the leading heel presses, the trailing sole
    // pins X, StanceLead stays −1.5 cm. FallLeadReady never fires. The
    // CoM translation that can close that gap is the geometric trunk-
    // mass shift from the 9° Plant lean (~2.7 cm at half-body / 0.35 m).
    // That lean was still × RecoverBlend (~3°, ~0.9 cm) and faded on
    // Capture, so any forward velocity from the couple killed it before
    // CoM reached the leading foot. Cycle 0: lean not × calm, leanRoom
    // 1 until EnterFall, Fall hold keeps that 9°. Rock stays not × calm.
    // Trailing A2 / hip later-cycle only. Leading-hip drive still only
    // once StanceLead ≥ 0. FallLeadReady / DualLoad still StanceLead ≥ 0.
    // Air latches 8°/6°. Recover still needs span ≥ FootLeadDeadzone.
    // Vault / firstStepHoldLift stay. footFriction stays 1.2.
    // (32) That still left walkSwap=0. The 9° lean about a free hip
    // assumes the hip is a fixed pivot. Two pinned soles (µ=1.2) make
    // it a bow: hips go back, trunk goes forward, whole-body CoM stays
    // at mid, StanceLead stays −1.5 cm. The translator is the trailing
    // CoM loop — both ankles share the leading-foot error, so the trail
    // dorsiflexes (heel, CoP behind CoM). walkPlantSwingAnkleReleaseFirst
    // faded that loop on the same plantMinDuration clock as rockClock,
    // product ≤ 0.25, then the pin returned. Cycle 0: keep the trail CoM
    // loop until StanceLead ≥ 0; clock fade is the unload at DualLoad,
    // not the initiation. Lean not × calm. Rock not × calm. Trailing A2
    // / hip later-cycle only. Leading-hip drive still only once
    // StanceLead ≥ 0. FallLeadReady / DualLoad still StanceLead ≥ 0.
    // Air latches 8°/6°. Recover still needs span ≥ FootLeadDeadzone.
    // Vault / firstStepHoldLift stay. footFriction stays 1.2.
    // (33) That still left walkSwap=0. Comment 32 kept the trail CoM
    // loop until StanceLead ≥ 0. DrivePelvis still holds the pelvis
    // world-upright on the stacked pair (plantHipRelease 0), so a CoP
    // behind CoM cannot rotate the inverted pendulum, and the same 9°
    // written into pelvisLean is a bow: hips back, CoM stays,
    // StanceLead stays −1.5 cm. walkPlantTiltFirst's 10° is a second
    // copy of that bow on the plantMinDuration clock. Plant never
    // EnterFall, 55° never legal, sinkAbort ~4 s, walkSwap=0,
    // travelX=0. Cycle 0 Plant while the trail is still loaded: 9° is
    // lumbar only (torsoLean); pelvisLean stays 0; walkPlantTiltFirst
    // waits for StanceLead ≥ 0. DrivePelvis is then a pivot, trunk mass
    // shifts forward, FallLeadReady can fire. Trail CoM keep / lean
    // not × calm / rock not × calm stay. Trailing A2 / hip later-cycle
    // only. Leading-hip drive still only once StanceLead ≥ 0.
    // FallLeadReady / DualLoad still StanceLead ≥ 0. Air latches 8°/6°.
    // Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (34) That still left walkSwap=0. Comment 33 made the 9° lumbar
    // only so DrivePelvis could be a pivot. The pivot still has a
    // P-to-vertical spring: lumbar third-law is absorbed (~0.9 cm of
    // chest mass), StanceLead stays −1.5 cm, Plant never EnterFall.
    // Cycle 0 Plant while the trail is loaded: hips lock to the stance
    // snapshot (not hipHoldAngle 0 — that brake is why hipHold is off
    // on a split) and pelvis P tracks current tilt, so the 9° is an
    // inverted-pendulum moment about the ankles. FallLeadReady can
    // fire when StanceLead ≥ 0. Trail CoM keep / lean not × calm /
    // rock not × calm stay. pelvisLean stays 0; walkPlantTiltFirst
    // waits for StanceLead ≥ 0. Trailing A2 / hip later-cycle only.
    // Leading-hip drive still only once StanceLead ≥ 0. FallLeadReady
    // / DualLoad still StanceLead ≥ 0. Air latches 8°/6°. Recover
    // still needs span ≥ FootLeadDeadzone. Vault / firstStepHoldLift
    // stay. footFriction stays 1.2.
    // (35) That still left walkSwap=0. Hip-lock + two planted µ=1.2
    // soles is overconstrained: the stack cannot rotate about the
    // ankles, MixHipHold D brakes pelvis ω, and lumbar 9° is only
    // ~0.9 cm of chest (need 1.5 cm). Revert the lock. Cycle 0 Plant
    // while the trail is loaded: trailing HIP extend (not A2 — toe
    // CoP on a 26 cm sole is a brake). Plus = thigh lower end back;
    // foot 1.5 cm behind CoM carries the pelvis onto the already-
    // leading foot. DrivePelvis stays world-vertical (hipHold off on
    // a split). trailWindow is the plantMinDuration clock, not 8 cm
    // (that arm never grows). FallLeadReady can fire when StanceLead
    // ≥ 0. Trail CoM keep / lean not × calm / rock not × calm stay.
    // pelvisLean stays 0; walkPlantTiltFirst waits for StanceLead ≥ 0.
    // Trailing A2 later-cycle only. Leading-hip drive still only once
    // StanceLead ≥ 0. FallLeadReady / DualLoad still StanceLead ≥ 0.
    // Air latches 8°/6°. Recover still needs span ≥ FootLeadDeadzone.
    // Vault / firstStepHoldLift stay. footFriction stays 1.2.
    // (36) That still left walkSwap=0. Comment 35's extend was a 0.15
    // DrivePelvis counter. Same-hip P-to-vertical cancels it at ~2°
    // of pelvis tilt; only the leading hip flexes (a sit). Cycle 0
    // Plant while the trail is loaded: trailing hip LEAVES DrivePelvis
    // and SPD-extends (plus pose, plantMinDuration clock, 8° ceiling).
    // Leading hip stays the world-vertical pivot: rear open + front
    // close is a lunge onto the already-leading foot. FallLeadReady
    // can fire when StanceLead ≥ 0. Trail CoM keep / lean not × calm /
    // rock not × calm stay. pelvisLean stays 0; walkPlantTiltFirst
    // waits for StanceLead ≥ 0. Trailing A2 later-cycle only.
    // Leading-hip drive still only once StanceLead ≥ 0. FallLeadReady
    // / DualLoad still StanceLead ≥ 0. Air latches 8°/6°. Recover
    // still needs span ≥ FootLeadDeadzone. Vault / firstStepHoldLift
    // stay. footFriction stays 1.2.
    // (37) That still left walkSwap=0. Comment 36's leading "pivot" is
    // DrivePelvis at signal 0 plus hipFriction 12 (a weld). Two equal
    // planted chains with a horizontal pelvis are a parallelogram:
    // CoM cannot walk onto the already-leading foot unless BOTH thighs
    // take the same plus angle. A welded lead is 0 DOF, trail SPD is
    // internal force, then the lead flexes (a sit). Cycle 0 Plant
    // while the trail is loaded: BOTH hips leave DrivePelvis and
    // SPD-extend to the same plus pose; leading-hip viscosity fades
    // with the trail. Stance knee / ankle keep the built damper.
    // FallLeadReady can fire when StanceLead ≥ 0. Trail CoM keep /
    // lean not × calm / rock not × calm stay. pelvisLean stays 0;
    // walkPlantTiltFirst waits for StanceLead ≥ 0. Trailing A2
    // later-cycle only. Leading-hip DrivePelvis only once StanceLead
    // ≥ 0. FallLeadReady / DualLoad still StanceLead ≥ 0. Air latches
    // 8°/6°. Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (38) That still left walkSwap=0. Comment 37 freed DrivePelvis
    // on both hips and faded only the leading hip. Remaining 0 DOF:
    // swingFade is clearance, so the trailing hip stays hipFriction 12
    // on dirt, and ankle K=3 on I/10 is a tighter weld than the hip —
    // two rigid struts, the parallelogram cannot change angle. torsoLean
    // 9° with DrivePelvis off is the comment-33 bow. Cycle 0 Plant
    // while the trail is loaded: both hip and ankle dampers snap to
    // tau K (knees keep the strut); torsoLean stays 0 with pelvisLean
    // until StanceLead ≥ 0. FallLeadReady can fire when StanceLead ≥ 0.
    // Trail CoM keep / rock not × calm stay. Trailing A2 later-cycle
    // only. Leading-hip DrivePelvis only once StanceLead ≥ 0.
    // FallLeadReady / DualLoad still StanceLead ≥ 0. Air latches
    // 8°/6°. Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (39) That still left walkSwap=0. Comment 38 snapped the dampers
    // so the struts could rotate, but both hips still targeted the
    // same +8° joint angle with DrivePelvis off. Two equilibria: shear
    // (thighs rotate about the ankles, CoM translates) vs sit (thighs
    // stay vertical, pelvis nods +8°, joint errors go to 0). Sit is
    // free; SPD takes it; StanceLead stays −1.5 cm; Plant never
    // EnterFall; 55° never legal; sinkAbort ~4 s; walkSwap=0;
    // travelX=0. Cycle 0 Plant while the trail is loaded: trail hip
    // leaves DrivePelvis and SPD-extends; leading hip STAYS the
    // world-vertical pivot (selects shear). Hip+ankle dampers still
    // snap to tau K. torsoLean 9° is legal again; pelvisLean stays 0
    // until StanceLead ≥ 0. Trail CoM keep / rock not × calm stay.
    // Trailing A2 later-cycle only. Leading DrivePelvis stays through
    // the trail-loaded window. FallLeadReady / DualLoad still
    // StanceLead ≥ 0. Air latches 8°/6°. Recover still needs span ≥
    // FootLeadDeadzone. Vault / firstStepHoldLift stay. footFriction
    // stays 1.2.
    // (40) That still left walkSwap=0. Comment 39's trail +8° is a
    // JOINT angle. jointAngle = pelvis − thigh, so sit (thighs stay
    // vertical, pelvis nods +8°) zeroes the error with no CoM travel;
    // DrivePelvis on the already-snapped leading hip then rotates the
    // lead thigh instead of un-nodding the pelvis. StanceLead stays
    // −1.5 cm; Plant never EnterFall; 55° never legal; sinkAbort ~4 s;
    // walkSwap=0; travelX=0. Cycle 0 Plant while the trail is loaded:
    // trail hip leaves DrivePelvis and SPD-holds WORLD thigh tilt
    // (knee back). Sit leaves the thigh at 0. Leading hip STAYS the
    // world-vertical pivot. Hip+ankle dampers still snap to tau K.
    // torsoLean 9° is legal; pelvisLean stays 0 until StanceLead ≥ 0.
    // Trail CoM keep / rock not × calm stay. Trailing A2 later-cycle
    // only. Leading DrivePelvis stays through the trail-loaded window.
    // FallLeadReady / DualLoad still StanceLead ≥ 0. Air latches
    // 8°/6°. Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (41) That still left walkSwap=0. Comment 40's trail WORLD tilt
    // is the right error (sit cannot zero it), but DrivePelvis on the
    // leading hip is the wrong pivot. The trail extensor is a couple:
    // clockwise trail thigh, CCW pelvis. DrivePelvis sees that nod
    // and flexes the lead thigh — a sit, not the equal-angle shear
    // comment 37 named. StanceLead stays −1.5 cm; Plant never
    // EnterFall; 55° never legal; sinkAbort ~4 s; walkSwap=0;
    // travelX=0. Cycle 0 Plant while the trail is loaded: BOTH hips
    // leave DrivePelvis and SPD-hold the SAME WORLD thigh tilt (knee
    // back). Sit leaves both thighs at 0. Equal world angles are the
    // parallelogram shear. Hip+ankle dampers still snap to tau K.
    // torsoLean stays 0 with pelvisLean until StanceLead ≥ 0.
    // Trail CoM keep / rock not × calm stay. Trailing A2 later-cycle
    // only. Leading DrivePelvis only once StanceLead ≥ 0.
    // FallLeadReady / DualLoad still StanceLead ≥ 0. Air latches
    // 8°/6°. Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (42) That still left walkSwap=0. Comment 41's equal WORLD thigh
    // tilt is the shear, but plantRockLead + dual CoM ankle still
    // rotate the shins (locked knees → thighs) in world frame. That
    // µ=1.2 couple wins at the sole; the hip pose is an internal
    // force; pelvis nods (sit); StanceLead stays −1.5 cm; Plant never
    // EnterFall; 55° never legal; sinkAbort ~4 s; walkSwap=0;
    // travelX=0. Cycle 0 Plant: BOTH hips leave DrivePelvis and
    // SPD-hold the SAME WORLD thigh tilt, not gated on trailLead /
    // plantHipExtend. Both ankle CoM loops (including plantRockLead)
    // stay at 0 so the snapped ankles are hinges. Ramp while the trail
    // is loaded, freeze that angle until EnterFall — otherwise
    // DrivePelvis returns at StanceLead ≥ 0 and unwinds the 1.5 cm
    // during plantMinDuration. Hip+ankle dampers still snap to tau K.
    // torsoLean stays 0 with pelvisLean until StanceLead ≥ 0.
    // Trailing A2 later-cycle only. Leading DrivePelvis only once
    // EnterFall. FallLeadReady / DualLoad still StanceLead ≥ 0.
    // Air latches 8°/6°. Recover still needs span ≥ FootLeadDeadzone.
    // Vault / firstStepHoldLift stay. footFriction stays 1.2.
    // (43) That still left walkSwap=0. Comments 37–42 drove shear with
    // hip WORLD/joint SPD and zeroed both ankle CoM loops. Hip muscle
    // is an internal couple (extensor clockwise on the thigh, CCW on
    // the pelvis) so the cheap equilibrium is a sit; StanceLead stays
    // −1.5 cm. Separately, plantRockLead never wrote on cycle 0
    // (`FallCatchCycleCount != 0` contradicted the comment that the
    // leading ankle already fired). No external CoP → Plant never
    // EnterFall; 55° never legal; sinkAbort ~4 s; walkSwap=0;
    // travelX=0. Cycle 0 Plant restores the ankle strategy:
    // plantRockLead writes (CoP behind CoM). DrivePelvis stays the
    // lumbar pivot. torsoLean 9° is back. Comment 44: pelvisLean is
    // the inverted-pendulum WORLD target DrivePelvis holds (minus =
    // forward), not a free-hip bow. Lumbar-only left ~1.35 cm of CoM,
    // short of StanceLead ≥ 0, and ankle K=3 froze the shin. Cycle 0
    // Plant writes the 9° pelvisLean and snaps only the ankle dampers
    // (hip K stays). The fulcrum is the ground, not the hinge. Comment
    // 45: that WORLD target is still a bow (SPD sees pelvis inertia).
    // Cycle 0 Plant SPD-holds the quiet hip snapshot (DrivePelvis off)
    // so the snapped ankles are the hinge; plantRockLead is the CoP.
    // Trunk lean waits (lumbar third-law on a locked stack is backward
    // about those ankles). Trail CoM keep / rock not × calm stay.
    // Trailing A2 / plantHipExtend later-cycle only. walkPlantTiltFirst
    // still waits (that +10 is the backward clearance tilt).
    // FallLeadReady / DualLoad still StanceLead ≥ 0. Air latches
    // 8°/6°. Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // Comment 46: quiet-joint lock + two soles is a table, not a
    // rocker. Cycle 0 Plant WORLD-holds both thighs at the same
    // clockwise tilt (shear). Snap hip+ankle dampers. Comment 47:
    // keep the shear, restore plantRockLead on the snapped ankles
    // (zeroing them left only an internal hip couple). Freeze that
    // world angle once StanceLead ≥ 0; DrivePelvis stays off until
    // EnterFall. Trunk lean waits. DualLoad / 55° still that
    // event, but only in Fall — Plant dirt must not fold 55°
    // in the freeze wait.
    // abort Plant waits the 3 s ladder).
    [Tooltip("1 = recover any starting pose: measured spawn CoM, TiltSettled + signed CoM-to-stance ≤ plantComLeadMax before Plant→Fall, one-foot surrender, abort Plant stands now. Cycle-0 Fall still uses RecoverComLostMeters 0.30 m (PlantComReady), not this standing band. 0 = former ladder.")]
    public float walkRecoverAnyPose = 1f;

    [Header("Transfer (event-gated)")]
    [Tooltip("Max seconds in dual support — then a soft fallback.")]
    public float transferMaxDuration = 6f;

    [Tooltip("Max Transfer when intent.run=1.")]
    public float runTransferMaxDuration = 4f;
    [Tooltip("Seconds reduction of walk transfer per 1 m/s target speed.")]
    public float walkCadenceTransferGain = 1.5f;
    [Tooltip("Minimum walk transfer duration under cadence scaling.")]
    public float walkCadenceMinTransfer = 1.0f;

    [Tooltip("Do not check ready earlier — intent=0 must reach BC.")]
    public float transferMinDuration = 0.7f;
    [Tooltip("If swing foot has landed, force leaving Transfer after this age, s.")]
    public float transferTouchdownMaxAge = 1.2f;

    [Tooltip("Both feet grounded and |CoM offset| ≤ this — hard ready.")]
    public float transferComOffsetMax = 0.10f;

    [Tooltip("Diagnostic: |CoM − catch foot| after a walking catch (~0.60 m). Not a Plant gate — that offset never enters 0.16 m.")]
    public float recoverComOffsetMax = 0.16f;

    [Tooltip("Soft fallback after transferMaxDuration.")]
    public float transferFallbackComMax = 0.12f;

    [Tooltip("Legacy: no longer used in IsTransferReady (BC lag).")]
    public float transferStandLegLevelMax = 0.05f;

    public bool walkActive;
    // Headless BeginWalk writes the simulated clock. Capture-walk
    // onset logging (first 3 s) measures age from this, not from a
    // physics-step counter that ResetCaptureWalk zeroes.
    public float WalkStartTime => walkStartTime;

    // Counters for the summary: without them the CSV looks "ready" but the swap is invisible.
    public int SwapCount { get; private set; }
    public int TransferReadyFrames { get; private set; }
    public int FallCatchCycleCount { get; private set; }
    // Failed Fall/Reach abort: Plant holds the same stance and the Recover
    // brake until TiltSettled + signed CoM lead, then the next Fall may start.
    // Not a completed cycle — FallCatchCycleCount stays put.
    public bool StandingRestore { get; private set; }
    // After Recover the new stance may still have CoM ahead of it
    // (AheadOfStance is a foot span, not a catch). Stay false until a
    // tick sees lead inside plantCaughtLeadMax: plantRock / lean must
    // not add forward CoP on that uncaught plant (that was the 90°
    // topple). FallLeadReady may still fire on the lead-wait deadline
    // so the latch cannot stall a completed swap until 93°.
    public bool PlantRolledFromBehind { get; private set; }
    // Recovery ladder is holding two-foot quiet stance instead of a split
    // Plant. Not a phase and not a cycle — the walk resumes from the same
    // Plant once the gate opens.
    public bool StandingSurrender => plantStandSurrender;
    public bool SpawnStandHeightValid => spawnStandHeightValid;
    public float SpawnStandHeight => spawnStandHeight;
    public bool StandComHeightValid => standComHeightValid;
    public float StandComHeight => standComHeight;
    public bool HasStandingComY => standComHeightValid || spawnStandHeightValid;
    public float StandingComY => standComHeightValid ? standComHeight : spawnStandHeight;
    // Live CoM drop below the settled standing bar (world Y). 0 when
    // no bar yet. Onset log / GETUP read this; not a force.
    public float StandingComDropMeters
    {
        get
        {
            if (comCalculator == null || !HasStandingComY)
                return 0f;
            if (comCalculator.TrackedBodyCount <= 0)
                return 0f;
            return StandingComY - comCalculator.GetCenterOfMass().y;
        }
    }
    public bool PelvisDroppedFromStand => !PelvisNearSpawnStand();
    // Failed catches since the last landing. Diagnostic for the ladder.
    public int FallCatchBounceCount => fallCatchBounceCount;
    public int FallCatchPhaseCode =>
        useFallCatchWalk != 0 && walkActive ? (int)fallCatchPhase : -1;
    public int PhaseCode
    {
        get
        {
            // Capture walker owns the gait: Tick returns without writing
            // fallCatchPhase or the legacy Stance/Transfer clock, so those
            // stay at Plant / Stance = 0 for the whole run. Report the
            // walker's own state so CSV walkPhase and the overlay move.
            if (balance == null)
                EnsureRefs();
            if (balance != null && balance.useCaptureWalker != 0 && walkActive)
                return balance.CaptureWalkStateCode;
            // WalkLift never EnterTransfer (that DualSupport is the dump).
            // Overlay Unload/Clear/Advance is the step; report it so
            // CSV walkPhase leaves Stance=0 once the swing knee lifts.
            if (balance != null && balance.useWalkLift != 0 && walkActive
                && balance.WalkLiftPhaseCode != 0)
                return balance.WalkLiftPhaseCode;
            return useFallCatchWalk != 0
                ? (fallCatchPhase <= FallCatchPhase.Reach ? 0 : 1)
                : (phase == Phase.Transfer ? 1 : 0);
        }
    }
    // Stance/Transfer (or Plant/Fall/Reach vs Catch) without the WalkLift
    // overlay. PhaseCode reports Unload as 1 so CSV leaves 0; that overlay
    // is still this Stance clock. Using PhaseCode==0 as "still Stance"
    // reset the lift on the next 5 ms step (knee act 0.43, walkPhase only
    // −1/0, no single support).
    public bool WalkStanceClock
    {
        get
        {
            if (!walkActive)
                return false;
            if (useFallCatchWalk != 0)
                return fallCatchPhase <= FallCatchPhase.Reach;
            return phase == Phase.Stance;
        }
    }
    public float CurrentStanceSign => currentStance;
    // WALK-2 diagnostics: read-only, for the trial CSV. A phase code alone
    // does not say whether the fall was long enough, whether the body had
    // really passed the stance foot at Reach, and whether the swing ever
    // cleared the ground before the catch.
    public float LastFallDuration => lastFallDuration;
    public float LastCatchDuration => lastCatchDuration;
    public float LastReachDuration => lastReachDuration;
    // Last airborne Reach extend (0–1), frozen on heel plant so Catch does
    // not read holdFold=0. Recover stance sign is the pre-swap Catch identity:
    // Plant decay must not reverse the arms when roles flip.
    public float LastReachExtendProgress => lastReachExtendProgress;
    public bool SwingFootIsPlanted => IsSwingFootPlanted();
    // Toe down, heel still up. CatchReachExtendProgress reads this so
}
