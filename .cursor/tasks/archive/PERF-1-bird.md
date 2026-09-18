# PERF-1 · Bird

One sentence: need the isolated cost of one bird; in the game it stays off until there is a separate GO.

Layer: bird (`Bird`, `BirdFlight`, `BirdController`, `BirdSensors`). Do not change the human, levels, or lighting.

## Contract

- `birdCount` in the game defaults to 0. Do not raise the default after the capture.
- The human headless stand does not spawn birds — a bird run only with explicit `-bird`.
- Lift only from the flap on the body, not a muscle and not a joint motor.

## Do

1. Take already captured `perf_bird` / `bird_stand*` as the start; if the fingerprint moved — one short headless `-bird 1` 30 s.
2. Record: rigidbodyCount, wall/sim, bird muscles/friction, how it is more/less expensive than Human.
3. Fork: bird in the same frame as the human — yes/no and why this is a second island of ~16 bodies. Do not implement joint spawn in World.

## Forbids

Do not leave a bird in World. Do not fix flight “while you’re here”. Do not touch Human PD.

## Acceptance

`Trials/Runs/perf_wave1_bird.json`. Table of numbers.
