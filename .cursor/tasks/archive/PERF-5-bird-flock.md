# PERF-5 · bird flock capacity — closed

One sentence: measure the current one-body flock at 1/10/50 birds and
keep only an optimization that produces a repeatable throughput gain
without changing bird behavior.

Layer: performance plumbing in the Flock path. Bird sensors, flight,
AI intent, body recipe, art, and tree-perch semantics are contracts.

## Contract

- Use `BirdRig.Flock`: one `Rigidbody2D` per bird. Never benchmark a
  50-bird ragdoll flock.
- Keep `fixedDelta = 0.005`, solver 8/3, flight gains, flap frequency,
  AI timings, `aiStride = 1`, and body sizes unchanged.
- Keep `BirdController` as the owner of the Flock physics order:
  sensors, pose, then flight.
- Keep `BirdDrive` / `BirdFlockDrive` in `Update`; brains write only
  mode and facing.
- Do not change `NearPerch`, contact masks, `BirdPerch`, wood ghost,
  textures, or public CLI fields.
- Do not put birds in World.

## Measurement

The sequential series used flat Flock wander at 1/2/5/10 birds for
20 seconds, 50 birds for 30 seconds twice, a one-bird tree-perch run,
and a 50-bird tree-perch stress run.

The trial ground now follows the pack span:

```
packSpan = max(0, birdCount - 1) * 1.2
width    = max(48, packSpan + 16)
```

This gives a 74.8 m slab for 50 birds and keeps every spawn on ground.
The valid capacity curve for 1/2/5/10/50 birds is `wallPerSim`
0.51 / 0.55 / 0.73 / 0.96 / 1.00.

## Rejected candidate

Disabling the Unity behaviours for externally driven `BirdSensors` and
`BirdFlight` left both 50-bird repeats at `wallPerSim = 0.9996`.
The required improvement was at least 10%. The candidate was reverted;
`BirdController` retains the original `drivenExternally` flags.

## Recorder correction

`BirdTrialRecorder.enteredFlight` latches on Fly / Glide / Attack.
The grounded `BODY_DROP_FALL` check now applies only before flight, so a
normal return from a perch to dirt is not reported as a fall. Pitch and
CoM fall checks remain active. A no-flight ragdoll sit still fell at
0.58 s and reached 127.7° pitch, proving that collapse remains visible.

## Acceptance

Build fingerprint:
`4bb32153dd835e3fdc3ccde032b153a8bfecd20df4bf066b2341dd86513f7384`.

- Flat 50: `fell = false`, `bodyDrop = 0.0686`,
  `bothFeetGroundedFraction = 0.4073`, `probeCallCount = 5500`.
- BIRD-9 tree 1: `fell = false`, `perchLandCount = 5`,
  `dirtRejectCount = 7`.
- Tree 50 stress: `perchLandCount = 146`, `dirtRejectCount = 373`.
  Bird 0 dropped from a shared perch before its first flight, so its
  recorder flag is not the one-bird BIRD-9 acceptance result.
- The metric-only Flat 50 CSV is bitwise equal to the pre-gate CSV.
- Source and Headless player freshness matched at closure.

Full report: `Trials/Runs/perf_bird_flock_01.md`.
