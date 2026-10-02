# BIRD-8 — beak segment and strike

Agents: `bird` (segment) + `ai` (`Peck`). Play: `Bird` / `Chicken`.
Stay out of `World` / `SampleScene`. Do not retune crow lift.

## One sentence

Every bird has a beak segment. Hens peck the dirt with it. The beak
is the only collider that may hurt an enemy.

## Contract

- Crow and chicken, ragdoll and flock, grow a `Beak` if missing.
- Flock beak is look + trigger: still one `Rigidbody2D`.
- Brain writes `BirdMode.Peck` / `Attack`. `BeakStrike` calls
  `Damageable.Hurt`. No `AddForce` from the brain. No `FactionMember`.
- Chicken: Sit / Walk / Peck, `flyFraction` ≈ 0.

## Accept

Stand `Trials/Runs/bird_chicken_peck.json` (20 s, flock, seed 1):
`fell` false, sit 0.536 / walk 0.352 / peck **0.112** / fly 0,
`peckCount` 6, `hasBeak` true, `beakSizeX` 0.046, `rigidbodyCount` 1,
`hoverMean` 0.55. Crow `bird_beak_crow.json` (10 s): `hasBeak` true,
`peckFraction` 0, `beakSizeX` 0.032, still flies.

## Forbid

A second Rigidbody2D per flock bird. Damage from chest or wing.
Human PD. 200 Hz cut.
