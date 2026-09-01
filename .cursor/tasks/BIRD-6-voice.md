# BIRD-6 — vocalize (alarm cry)

Agent: `ai`. Writes `mode` / `SetFacing` / `OrganismVoice.Cry` only.
Play: `Bird` / `BirdFlock50`. Stay out of `World` / `SampleScene`.
Do not edit `BirdFlight`. Do not add `Vision` / `Hearing` / `FactionMember`.

## One sentence

A living organism can make a sound. A bird that sees a threat cries
`Alarm`, then `Fly` away. Others hear the fact and take off.

## Contract

`OrganismVoice.Cry` posts `SoundEvent` on `SoundBus`. That is not
`AudioSource` and not a force. Threat v1 is the `BirdThreat` marker
(position only), same style as `BirdPrey`. Ahead = `facing` along +X.
Default `threatOffsetX` / `-birdThreat` is 0 — wander stays unchanged.
Headless: `-bird 1 -birdRig flock -birdDrive wander -birdThreat 8`.

## Accept

Without `-birdThreat`: listed wander metrics unchanged, `cryCount` 0.
With `-birdThreat` ahead: `cryCount` ≥ 1, then `Fly` (not `Attack`).
Flock: one cry is enough for the others to leave Sit.
