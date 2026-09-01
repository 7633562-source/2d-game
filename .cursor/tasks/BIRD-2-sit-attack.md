# BIRD-2 — Sit and Attack

Agent: `ai` owns `BirdDrive` (writes `mode` / `SetFacing` only).
Agent `bird` owns Sit/Attack **physics** (`BirdFlight`, pose, landing → Sit).
Play: `Bird`, `BirdFlock50`. Stay out of `World` / `SampleScene`.

## One sentence

The bird can **sit** (rest on the perch) and **attack** (stoop at prey).
Those are modes, not extra lift and not a hit.

## Contract

Brain / `BirdDrive` writes `BirdController.mode` and `SetFacing` in `Update`.
`BirdFlight` supplies forces. No `AddForce` in the drive. No damage layer.

| Mode | Body |
|---|---|
| `Sit` | grounded, wings folded, no auto-takeoff, no thrust |
| `Attack` | stoop: less lift, more forward thrust, dive pitch × facing |
| `Fly` / `Glide` / `Walk` / `Stand` | as before |

Landing contact may set `Sit` (rest). `takeoffDelay` stays a Stand demo timer.

## Circumstances (v1)

`BirdDrive` (one cheap `Update` per bird, not 200 Hz):

- Prey ahead and far → `Fly`
- Prey ahead, close, high enough → `Attack`
- Grounded and resting / arrived → `Sit`
- Passed prey in air → `Glide`

No `Vision` / `FactionMember` yet. Shared `BirdPrey` marker is the prey.

Handshake accepted 30.08: `ai` owns `BirdDrive`; `bird` owns Sit/Attack
physics. `noticeRange` default 40 m (flock prey 28 m + spacing). Next
AI step is overshoot → `Glide`, not Vision.

## Do not

Invisible up force, ragdoll-per-flock, `World` birds, hit/HP, SIMBICON.
