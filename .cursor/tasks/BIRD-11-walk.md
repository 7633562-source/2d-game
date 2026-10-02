# BIRD-11 — crow walks while foraging

Agent: `ai`. Writes `mode` / `SetFacing` only.
Play: `Bird` / `BirdFlock50`. Stay out of `World` / `SampleScene`.
Do not edit `BirdFlight` or `walkThrust`.

## One sentence

A crow on dirt forages by walking. Short hops stay; they are not the
only ground life. A bound perch still takes off — dirt is not home.

## Contract

`BirdWander` gains phase `Walk`. After `Sit` on flat ground it usually
writes `BirdMode.Walk`, sometimes `Fly`. After a walk bout it sits.
`walkMin` / `walkMax` already on `BirdDrive`. No `AddForce`.
Hen walk is unchanged (`ChickenWander`).

Perch bound (`hasPerch`): Sit → Fly toward the slot, no Walk on dirt.

## Accept

Wander, no tree, no threat: `walkCount` ≥ 1 and `takeoffCount` ≥ 1
in 30 s. `life` stays `Forage`. Hen peck/walk fractions stay listed.
