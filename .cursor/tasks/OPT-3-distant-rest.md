# OPT-3 · distant rest

One sentence: World NPCs that sit outside the camera must not pay the
200 Hz ragdoll bill; on screen they stay the same body.

Layer: boot (`NpcRest` on `GameProcess`). Hooks are flags on
`DogStanceController`, `ActuatorDriver`, and `BirdController`.
Do not edit human PD, muscles, signs, or `fixedDeltaTime`.

## Contract

- The player `Human` is never in the rest list.
- `HeadlessTrial.Active` never rests (no attach from `GameProcess.Start`,
  and `NpcRest.Tick` returns).
- Rest distance scales with `orthographicSize` so a zoomed flock overview
  does not freeze birds that are still in frame.
- Rest is `RigidbodyType2D.Kinematic` + `Interpolate.None`, not
  `enabled = false` on sensors/flight (PERF-5 measured 0% and reverted).
- Wake before the body enters the visible half-width (`wakeX` / view margin).
- Public field names of the human stand stay.

## Acceptance

- Scene `Dog` / `Bird` / `Chicken`: the followed body stays Dynamic
  (distance ≈ 0).
- World spawn (human at 0, dogs at 26, crows at 39): those packs rest
  immediately. Nearby hens around x=12 stay live.
- Walking the camera to the square wakes the dogs before they are on
  screen. Pose is the leftover sit, not a new gain.
- Human headless stand is unchanged (this code is not on that path).
- Do not open a new `stable-pd` baseline.

## Do not

- Cut 200 Hz. Retune PD for FPS. Sleep the hero.
- `enabled = false` on driven `BirdSensors` / `BirdFlight`.
- Plant `Sway` or wood RB on Static trees.
- Put more ragdoll dogs in World to “see FPS”.
