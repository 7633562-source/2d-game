# PERF-4 · runtime cache cleanup

One sentence: remove repeated lookups and avoidable trial allocations
without changing physics, public input contracts, or CSV output.

Layer: shared utility, bird sensors/AI, level streaming, Dog control,
and experiment recorders. No balance coefficients or body geometry.

## Contract

- `GroundLayers.Index`, `Mask`, and `Apply` keep their behavior.
- Bird contact, perch, flight, and alarm semantics stay unchanged.
- `IgnoreHuman` remains on the Static World grove.
- Trial CSV content stays byte-for-byte compatible.
- Human physics, 200 Hz, solver 8/3, and production scenes stay fixed.

## Implemented 01.09

- Cache the resolved Ground layer.
- Compute `BirdSensors.nearPerch` once per physics tick and reuse it in
  controller, flight, and recorder.
- Collect one shared recent-sound snapshot per flock `Update`.
- Cache `OrganismVoice`'s Body transform.
- Stream level chunks only after the center chunk changes.
- Remove static tree-vs-static level collision-ignore pairing while
  preserving static tree-vs-human pairing.
- Skip velocity writes when `TreeRig.Static` has already made a tree
  body Static, removing the World warning flood.
- Append Bird/Dog CSV rows directly with reusable numeric buffers.
- Remove temporary activation arrays and duplicate Dog maximum scans.
- Remove the Dog Falling `Muscle[]` allocation.
- Reuse one stateless `WaitForFixedUpdate` in `HeadlessTrial`.

## Verification

- Build fingerprint:
  `3aa4985bbb470ff3c656e83dbed068da0f42ed552a27dd2db8bfee0cbeda7da6`.
- Human quiet stance `opt_p2_human_wait`: 30 s, `fell = false`,
  `pelvisDrop = 0.0016`, saturation 0, both feet 1.0.
- Chicken `opt_p2_wait_chicken`: 20 s, `fell = false`, airborne 0,
  `landedUpright = true`.
- Flock `opt_p2_flock`: 20 s, `fell = false`.
- Alarm flock `opt_p2_flock_alarm`: 10 s, `fell = false`,
  `cryCount = 10`.
- Dog `opt_p2_dog_wait`: 10 s, `fell = false`; forced
  `opt_p2_dog_fall` entered Falling at 1.175 s without an error.
- Bird, Human, and Dog before/after CSV hashes are bitwise equal; exact
  hashes are recorded in `Trials/Runs/opt_code_audit_31.md`.
- World+grove cycle 39: valid and clean, p95 16.75 ms, known work
  0.414 ms, `maxFixedSteps = 4`, GC marker 28.1 bytes/frame.

## Deferred

- Human recorder float-string formatting.
- Empty externally-driven `FixedUpdate` callbacks.
- The performance monitor's once-per-second file append.

These are experiment-only or tiny. They need a separate measured card,
not an unmeasured callback-lifecycle change.
