# FACTION-1 — stamp factions at spawn

Agent: `ai`. Data only. No forces. No `Vision`.
Spawn writes the label. Brains may **read** `FactionTable` later.
Play: any scene that creates a body. Do not retune PD or `BirdFlight`.

## One sentence

Every intelligent body gets a faction when it is created. Faction 1 is
the player (one member). Wildlife holds subfactions Wolf and Bird;
those two are Hostile to each other.

## Contract

- `FactionMember` on the body root (not on a segment).
- `FactionTable.Rel` — pair → `Ally` / `Neutral` / `Hostile`.
- Stamp in `GameProcess` / `HeadlessTrial` after `Build*`, not in
  `FixedUpdate`.
- Player (human index 0) → `Player`, no subfaction.
- Crow / hen / flock → `Wildlife` / `Bird`.
- `Dog` body → `Wildlife` / `Wolf` (the wolf subfaction; no second
  ragdoll).
- Trees have no faction.
- Player vs Wildlife is `Neutral` until a later card.
- Wolf vs Bird is `Hostile`. Same subfaction is `Ally`.

Do not hard-code “bird fears wolf” inside `BirdController`.
Do not scan the world for members in this card (that is Vision).
v1 life still uses `BirdThreat` / `BirdPrey` markers.

## Accept

Spawned player has `FactionMember` `Player`. Each bird has `Wildlife` /
`Bird`. Each dog has `Wildlife` / `Wolf`.
`FactionTable.Rel(wolf, bird) == Hostile`.
`Rel(player, bird) == Neutral`. Same-subfaction `Ally`.
No change to lift, muscles, or walk gains.
