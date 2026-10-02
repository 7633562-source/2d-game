# BIRD-13 — hunt insects (concept)

Agent: `ai`. Writes `mode` / `SetFacing` / `Cry` only.
Play: `Bird` / `Chicken` when insects exist. Stay out of `SampleScene`.
Do not edit `BirdFlight`. Do not invent `Vision` on this card.
Do not take `BIRD-12-core-contact` (agent `bird`).

## One sentence

An insect is a new root faction, always Hostile to birds. A bird in
`Forage` that sees an insect switches to `Pursue`, then eats it with
the beak (`Peck`), not with a force.

## Contract

```
Forage + sees Insects (weaker Hostile)
    → Pursue that insect
    → close and on dirt → Peck
    → BeakStrike → Damageable.Hurt
    → insect dead → gone (eaten) → Forage
```

- Faction `Insects` (3). `FactionStamp.Insect` at spawn.
- `FactionTable.Rel(Bird, Insect) == Hostile`. Always. Not a Wildlife
  subfaction.
- Insect `ownStrength` is low — the bird Pursues, it does not Flee.
- Eat = mouth (`Beak` + `Peck`). The brain does not call `Hurt`.
- Wolf / Player vs Insects stay `Neutral` until a card.
- No insect ragdoll and no world scan on this card. Next: spawn +
  Vision reads `FactionMember`.

v1 prey marker may stand in as a weaker Hostile. A real insect carries
`FactionMember` + `Damageable`.

## Accept

Law in `ai.mdc`. `Rel(bird, insect) == Hostile`.
`Rel(player, insect) == Neutral`. `Rel(wolf, insect) == Neutral`.
No change to lift or walk gains. No insect crowd in `World` yet.
