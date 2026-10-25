# req-draft — TREES: crow landing arrest + take-off push on the perch (`-treePerchKick`, default off)

Status: DRAFT by Claude lab (tree-1019), not numbered — re-list `requests/` before assigning req-NNN (fleet workers write req-NNN too).
Type: DELEGATION (code change + takes). Lane: main (trees). One Unity build per round. No commit without GOOD.
Base: req-287 BEST if it has landed by then; otherwise HEAD of the main tree (the change touches only `ApplyPerchLoad`, so it works on both the HEAD crown and the segmented tree).
Design + numbers: `D:\hermes-data\claude-lab\cache\tree-1019\tree-audit.md` §7, model `tree_core.py` (`crow_event`, `crow_force`, mode `kicks`), picture `D:\hermes-data\claude-lab\frames\tree-1019\tree.png` (bottom-left panels).

## Why

Today a crow on the tree is only its weight as a constant force while seated (`PlantTree.ApplyPerchLoad`, PlantTree.cs:2459-2499, set by `BirdTriggerPerch.SeatOnRing` l.1134 / `LeaveRing` l.1161, or by `-treePerchLoad`). The branch does not dip on touch-down and does not kick down when the bird pushes off. The lab model shows that two short half-sine pulses on the load edges, scaled by the inelastic share, reproduce the physical reference (bird mass riding the wood) within about 10-35 % on every metric; unscaled pulses overshoot 2.5x.

## Flags (all default OFF; with none of them the build is byte-identical)

| flag | meaning | default |
|---|---|---|
| `-treePerchKick 1` | add arrest + push impulses on the rising / falling edge of the effective perch load | 0 (off) |
| `-treePerchKickScale <f>` | multiplier on both impulses (for the sweep) | 1.0 |

Read once in the tree build path inside `if (HeadlessTrial.Active)` via `PlantTree.ReadPlantArg` (copy the `-treeRect` / `-treePerchLoad` pattern, HeadlessTrial.cs:3228 area), list them in the HeadlessTrial.cs flag list. Every hunk is an early return or a guarded call.

## Do

1. In `PlantTree`, add private state: `prevLoadOn` (bool), `kickT` (time since edge), `kickKind` (0 none / 1 arrest / 2 push), `kickImpulse` (Vector2), `kickDur` (s), and a `System.Random kickRng` seeded from the tree seed (`seed * 7919 + 101`), created only when the flag is on.
2. In `ApplyPerchLoad(deltaSeconds)`, BEFORE the early `if (perchLoadKg <= 0f) return;`, when the flag is on compute `loadOn = perchLoadKg > 0 && !(perchLoadUntilSeconds > 0 && perchLoadElapsed >= perchLoadUntilSeconds)` (the same condition the existing code uses to apply the weight) and detect edges against `prevLoadOn`:
   * rising edge: draw `vin = U(0.8, 1.8)` m/s, `ain = U(20, 60)` deg down, side = +-1 (sign of the bird's x velocity at seat if `BirdTriggerPerch` sets a public `tree.perchArriveVel`, else random), `Ta = U(0.03, 0.07)` s; impulse J = s * m * vin * (side cos ain, -sin ain).
   * falling edge: `vout = U(1.8, 2.8)` m/s, `aout = U(45, 75)` deg up, same side rule, `Tp = U(0.06, 0.10)` s; impulse J = -s * m * vout * (side cos aout, sin aout) (reaction of the push-off: down and backwards).
   * m = the load mass of that seat (perchLoadKg on the rising edge; remember it for the falling edge because perchLoadKg is 0 by then).
   * s = m_sub / (m_sub + m), m_sub = rigidbody mass of the perch wood + all its descendant wood + leaf masses attached under it (one walk of the node tree at bind time; cache per slot). Lab values: s = 0.49 twig, 0.75-0.83 branch, 0.93-0.98 limb.
3. While a pulse is active, every FixedUpdate add `F = J * (pi / (2 T)) * sin(pi * kickT / T) * PerchKickScale` with `AddForceAtPosition` at the same bound pad point (`perchLoadLocal`) - the half-sine integrates exactly to J. The falling-edge pulse must run even though `perchLoadKg` is 0 (so the push branch sits before the early return). After T, clear the pulse.
4. The existing weight force is unchanged. No new bodies, no change to the bird.
5. Log one line per pulse: `[TREEKICK] kind=arrest|push t=.. slot=.. m=.. s=.. J=(..,..) T=..` and put `treePerchKick`, `treePerchKickScale` and the per-seat values into the plant trial JSON (PlantTrialRecorder) only when the flag is on.
6. Optional (B, same round if trivial): in `BirdTriggerPerch.SeatOnRing` / `LeaveRing`, write `tree.perchArriveVel = body.velocity` / `tree.perchLeaveVel = body.velocity` (guarded by the flag) and use them instead of the random draws when their magnitude is in 0.3-4 m/s.

## Checks (in this order)

1. **S0 byte-identity**: std battery 11/11, md5 == c129a, with the new build and NO new flag.
2. **Off == base**: the req-287 (or HEAD) seat take `-treeWind 0 -treeTriggers -treeTriggerCap 16 -treeLoadOnTrigger -treeTriggerSlot <twig span> -treePerchLoad 0.45 -treePerchLoadUntil 28`, 30 s: joints CSV byte-identical to the base take.
3. **On**: the same seat take with `-treePerchKick 1`, seeds 1-3, on a twig slot and on a branch slot. Measure on the perch point (or the host joint angle if the point is not recorded):
   * fast dip (max drop within 0.3 s of the seat), twig band 2-6 cm (model 3.55; base step 3.17 in SEG24, much slower in P18);
   * take-off dip (drop within 0.3 s after release, relative to the seated level), twig 1-3 cm (model 1.92; base: none, the branch only rises);
   * landing speed of the perch point, twig 15-45 cm/s (model 38);
   * static seated drop unchanged vs check 2 within 1 mm (the pulses must not change the steady state);
   * no joint at its stop for > 0.1 s, no NaN, `UnguardedSolid 0`, census unchanged.
4. **Sweep** `-treePerchKickScale 0.5 / 1 / 1.5` on seed 1 twig: dips scale monotonically.
5. **Live crow**: one Level / probe take with a real `BirdTriggerPerch` seat (existing probe recipe), flag on: `[TREEKICK]` lines appear in pairs (arrest at SEAT, push at RELEASE), the bird's own seat metrics (seatHeldMax, guide) unchanged vs flag off.

## Report format

```
VERDICT: GOOD | NO-KEEP | BLOCKED  (one line)
S0: 11/11 md5 <hash> (== c129a ?)
off==base: <yes/no, which file differs>
on (twig, seeds 1-3): fastDip cm / takeoffDip cm / vLand cm/s / staticDrop delta mm / stops
on (branch, seed 1): same
sweep 0.5/1/1.5: fastDip / takeoffDip
live crow: TREEKICK pairs <n>, seatHeldMax off/on
diff: <path to patch>, build hash
```
