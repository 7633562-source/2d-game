# CLEARING-1 - the first real game location: the forest clearing

Dispatched 21.09.2026 by the game line (Hermes) on H_Manager's order ("the level designer's first
real task on scenes/rooms"). Owner: the LEVEL DESIGNER stream (`ds-cycle.sh level`,
`h_2dgame_leveldesigner`).

## Goal

`Assets/Scenes/Clearing.unity` - the concept's forest clearing (`docs/CONCEPT.md`: trees, grass, a
campfire, strawberries, day/night) as ONE real, walkable location. Until now the concept exists only
as LABS: the yard tape keeps `treeCount` 0 in `World`/`Level1`, the trees live one species per
scene, the fire in its own lab. This scene is where the game's world is meant to be seen.

## What is inside (first slice)

- Ground + sky + the finish sign through the yard's own builders (`GroundBuilder` / `LevelArt`);
  the Test1 tape IS the level - do not author a new tape.
- The yard's Static grove plus the clearing's own tree(s): start from the Static grove; a dynamic
  species tree joins only if the frame budget holds it (measure, say so).
- The campfire at its drawn yard spot, the strawberries, the flower bed - the yard's own code and
  art, nothing repainted.
- `DayNightCycle` running (the yard's own clock).
- The human spawns in the yard's own spawn config and can walk (W / up arrow).
- No creatures in the first slice; one species per later cycle, as the yard did.

## Rule zero and boundaries

- No physics/control files: `BalanceController*`, `Human*`, `Dog*`, `Bird*`, `StepPhaseDriver`,
  `TrialRecorder` stay untouched; the reference trials stay bit-identical.
- `treeCount` on `World` and `Level1` stays 0; this is a NEW scene.
- The scene goes into the preview player's scene list (the way `BalanceArena` was added) so Yury can
  open it and captures can be taken.
- One change per cycle; builds from bash only; no commit (a commit needs Yury's word).

## Evidence (Yury judges by eye)

- 1920x1080 stills: the whole clearing, the campfire close, one tree close.
- A >= 15 s 1080p video of the clearing (a camera pan is acceptable if the capture path cannot
  drive a walk yet - say which was delivered).
- The buildGuid of the same build that made the frames; the report names what stands where.

## Acceptance

1. The scene opens in the player, the human stands on the yard's ground and walks; no physics
   numbers move anywhere else (rule zero runs).
2. The clearing contains the concept's set: ground/grass, trees, the campfire, strawberries,
   flowers, the sky and the day/night clock.
3. Stills + video from ONE build, with its buildGuid.
4. Nothing outside the level layer is edited.

## Out of scope

Creatures (later slices), new art, UI, physics changes, the Gallery/species scenes (the art lane's
and the catalogue's own work), `SampleScene`, `World`, `Level1`.
