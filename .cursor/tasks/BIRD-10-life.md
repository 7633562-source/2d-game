# BIRD-10 — bird life switches

Agent: `ai`. Writes `mode` / `SetFacing` / `OrganismVoice.Cry` only.
Play: `Bird` / `BirdFlock50` / `Chicken`. Stay out of `World` / `SampleScene`.
Do not edit `BirdFlight`. Do not add `Vision` / `Hearing` / `FactionMember`.

## One sentence

Bird behaviour is a life-state machine. Ordinary living is `Wake / Forage`
(search for food). Seeing a stronger hostile goes to `Flee`; a weaker
hostile goes to `Pursue` (attack).

## Contract

Shared type `LifeState` (`Sleep`, `Forage`, `Flee`, `Pursue`) in
`LifeState.cs`. `OrganismLife.ChooseWake` is the transition. The bird
brain holds one `Life`, then writes body intent.

v1 stand-in (level factions come later):

- `BirdThreat` ahead = hostile stronger than `ownStrength` → `Flee`
- `BirdPrey` in `noticeRange` = hostile weaker → `Pursue`
- Hear foreign `Alarm` → `Flee`
- Else `Forage` (wander / hen Walk-Peck)

`FactionMember` exists (`FACTION-1`). This card still uses markers, not
a world scan. Strength fields
`ownStrength` / `threatStrength` / `preyStrength` are public so a later
faction card can feed the same compare.

## Accept

Default wander, no markers: `life` stays `Forage`, `lifeSwitchCount` 0,
listed wander metrics as before, `cryCount` 0.
`-birdThreat` ahead: switch to `Flee`, `cryCount` ≥ 1, Fly (hen: Walk),
not Attack.
`-birdDrive prey` with a prey marker: switch to `Pursue`, Attack when
close and high enough.
