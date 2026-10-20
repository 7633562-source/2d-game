# GOAL (updated 2026-10-03 02:35)

Owner's focus (27.09, kept 02.10): the vestibular core of the human.

Acceptance scenario: the owner's door scene (the SampleScene harness; desk plan
`i1.5,w6,i2,w6,i2,s5,r2,w8`), default flags, 60 s, no fall.

This file is the single statement of the current goal; it is updated after every KEEP.
Rules: `F:/hermes-data/manager/inbox/scheme-opus-2026-10-02.md` (v1.2).
Brief (the door): `F:/hermes-data/ds-walk-brief.md` — rewritten each round.

## Checklist (status + last evidence; DONE = door numbers + the owner's eye)
- [ ] stands 60 s quietly — the plain stance fence `s_stance` holds 40 s (`78e044e6`, byte-stable
      since c121); the post-walk 60-s take `win60` `b7ae15b9` tails FLAT (last 16 s: pY span
      0.000, margin +0.77, |torso| <= 0.3; c122). No plain 60-s stand take yet.
- [ ] three full squats in a row, rises from each — harness: c84 KEEP, `squat_clean_reps` = 3
      (re-earned byte-identical every round since: ctl `29e90aab` / land `711f1a4a`).
      Door: 0 — the post-walk desk folds but does not rise yet (c124/c125).
- [ ] lean forward and back on the keys, returns upright — c82/c83 KEEPs on the always-on base:
      CoM out-and-returns +0.0264 m (fwd; max dev 0.0036) / -0.029 m (back); no fall; fences
      fwd `f3db714d` / back `13501f82` byte-stable. The owner's own key check in Unity: pending.
- [ ] stop after walking, settles — the plan stop holds 40/40 at defaults (c123; was a fall at
      8.405; release exit t=0.91, end margin +0.40, span 0.894, CoM inside 401/401); the door's
      stop 2 settles clean at defaults (c124: release exit v=0.193, margin 0.145).
- [ ] rises from the bottom of the squat — the fold holds the full crouch 2.05 s (21.31 -> 23.36;
      bottomKnee 109.70) then topples BACKWARD (real loss ~23.30). c125 closed the bottom-hold
      family as a lever (11 arms, 7 inert); c126 pinned the wall: the ankle balance command runs
      BANG-BANG (52% flexor-railed) and the push starts at 21.940 (raw -0.96 -> +0.03; the D term
      carries 66.5%), with a hull-flap defect (42/700 frames) slamming it rail-to-rail; the global
      D-derate is REFUTED (walk dies <= 3.22 s); c127 priced the crouch's own ankle gate:
      the rails clear (rail frac 0.713 -> 0.492 @0.25; flaps 42 -> 33; the loss +0.99 s to 24.285)
      and the fences hold byte-identical, but the squat law pays (reps 3 -> 2/1 @0.25, 0 @0.5/1.0)
      and the rise still does not run - REFUTED, reverted; the wall is the input (the refX flap)
      and the runaway's authority, not the gain; c128 priced the refX flap dwell: the flutter bridges
      (flaps 42 -> 6/8; offN smooth) but the rails go UP (0.713 -> 0.763 at t=0.20; the flutter was
      the extensor-crossing carrier) and the bottom's creep flips backward 1.4 s earlier (onset
      22.92 -> 21.54; forward peak 0.643 -> 0.331), the push fires late and weak and the fold still
      dives (23.355 at best; reps 0) while the squat law pays (reps 3 -> 2, new falls) - REFUTED,
      reverted; the wall is the runaway's own authority (the solver rides the flexor rail while the
      CoM slides back at -1.3..-1.5 m/s; the right ankle stop-protect zeroed 28-46% of the bottom
      frames while the left carries). c129 priced the runaway's own authority (the parked right
      ankle's stop-protect freed at the crouch, -poseAnkleCrouchFree): the mechanism fires
      (right-protect zeroed frames 245 -> 35; the bottom goes two-sided) and at full dose the
      backward runaway CANCELS (min comVelX -1.05 -> -0.22) but converts to a forward over-drive
      (+1.98 m/s; margin -0.96; forward fall 23.220 where the ref fell backward at 23.295) - the
      railed loop latches the other way; at 0.25/0.5 the backward story only delays (min -1.55/-0.83;
      falls 23.590/23.405) and the pre-registered rail drop did not come (0.693/0.715/0.765 vs 0.713).
      Fences byte-identical at dose and the squat law holds (reps 3/3, mask 7; f25 fall 6.88 = refs)
      - REFUTED-as-lever (no rise at any dose), reverted; the wall is the loop's LATCH at the bottom:
      freeing the parked press flips which way the saturated command runs away.
      Next single change: the pair's split, bounded (c130) - the command delivered two-sided with the
      pair's total bounded (the f100 cancel without the over-drive); second item: the bottom's own
      latch (the P term's authority while crouched).

## Tools in tree (flags kept for the next rounds; weekly prune: scheme 1.8)
- squat: -squatReps / -squatTaskTime / -squatTaskHold / -squatRise* / -squatPace /
  -squatBottomHold / -squatBottomSettle / -squatBottomMps / -squatBottomGate /
  -squatReleaseLoad / -squatReleaseFloor / -squatReleaseScale / -squatReleaseSink;
- walk / plan: -walk / -walkTime / -walkDuration / -captureWalk / -poseWalk / -walkPlan /
  -walkPlanBrake / -walkPlanBrakeCatch / -walkPlanBrakeFit;
- settle / lean: -poseWalkSettle / -poseWalkSettleBend / -poseWalkSettlePush /
  -poseWalkSettleGuard / -poseWalkSettleExit / -lean / -leanTime / -leanHold;
- kept-but-refuted (prune if unused for 3 rounds): -poseWalkSettleHold* (c118), the c125
  bottom-hold dose set. Full inventory: `HeadlessTrial.cs`.

## Last 5 KEEPs
- c124 — the brake fit: a brake never cuts mid-ramp; the door's stop 2 clean and the fold runs
  (bottomKnee 109.70, holds 2.05 s); `door_squat_desk_reps` still 0. fp `bad3088a` dll `f64a9191`.
- c123 — the settle's arming + the shipped stop: five defaults over two files; the plan stop
  holds 40/40 (was 8.405); the door survives to 24.5. fp `5447bdad` dll `b85c438f`.
- c122 — the state exit lands: the settle release becomes body-driven; quiet-tail margin
  -0.24 -> +0.77; the band 10/11. fp `6586470f` dll `a063b632`.
- c121 — the walk-on guard lands: the release deferral; holds 5 -> 10 of 11; defers 0-2, fires 0.
  fp `9fd985f4` dll `947d8326`.
- c118 — Unity 6000.6.4f1 re-base (housekeeping KEEP): 16/16 anchors byte-identical on the new
  editor (the round's feature change REFUTED). fp `8ce1a11f`.

## Claude cycles (temporary, owner's order 03.10; protocol F:/hermes-data/claude-lab/PROTOCOL.md)
- While the cycles are on (until `claude-lab/requests/END`): Claude writes the code; this line does
  NOT start own hypotheses and does NOT edit game code - it executes pending requests strictly per
  the protocol (apply patch, build, standard set + requested takes, measure, report, revert).
- Executor: the walk worker (h_2dgame_programmer), fired by the walk cycle script's Claude branch
  and by the 5-minute claude-lab trigger. Reports -> `F:/hermes-data/claude-lab/reports/rep-NNN.md`.
- Editor note (operator, 03.10): Unity upgraded 6.6.3 -> 6000.6.4f1 (project migrated, all fleet
  scripts repointed, commit 4e099340); builds run on 6.6.4f1 now.
