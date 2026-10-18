# OPT-2 · bird

One sentence: there is no bird in the game (`birdCount = 0`) — do not invent a hot path.

Layer: bird. Do not touch the human.

## Do

1. Confirm that at `birdCount = 0` no `Bird` / `BirdFlight` / `BirdController` are created.
2. Do not change code if there is no hot path.
3. Write `Trials/Runs/opt2_bird.md`: why there are no edits, what would be more expensive (a second island of ~16 bodies).

## Forbids

Do not spawn a bird in World. Do not fix flight.
