# PERF-3 · input debug polling

One sentence: agent debug flags must not perform file-system work on
every rendered frame.

Layer: intent (`PlayerInputSource`). Do not edit balance control,
physics, body geometry, level generation, or experiment semantics.

## Contract

- Keyboard state is still read every `Update`.
- Existing flag names and accepted values stay unchanged.
- `MotionIntent` and `StepPhaseDriver` behavior stay unchanged.
- The headless stand remains unaffected when no keyboard is present.

## Implemented 31.08

- Cached all four flag paths once in `Awake`.
- Poll lean, run, crouch, and stand-leg flags at 5 Hz.
- Cache parsed values between polls.
- Compile the IMGUI input hint only in Editor / Development builds.

## Verification

- Unity compiled the source in World cycle 33.
- IDE diagnostics report no errors.
- Cycle 33 completed with `valid = true`; this is a compile/runtime
  smoke check, not an input-latency benchmark.
