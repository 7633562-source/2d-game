# DOG-4 — dog brain, no keyboard

This is AI. One sentence: the dog is an NPC wolf. A brain writes
what it wants; the body walks and leaps. The player keyboard does
not drive the dog.

Layer: `DogDrive` (intent). Body stays `DogStanceController`.
Shared life: `LifeState` / `OrganismLife`. Faction: Wildlife.Wolf.

## Contract

- Brain writes only `Walk` / `EndWalk` / `LeapAttack` / `Bark` /
  `OrganismVoice.Cry` in `Update`.
- No `AddForce`. No PD. No muscles. No `InputSystem` on the dog.
- Headless: `DogDrive` silent unless `allowHeadless`. CLI may still
  call `Walk()` / `LeapAttack()` on the controller.
- Do not invent `Vision` / a god brain. Prey mark is `DogPrey` v1
  (same style as `BirdPrey`).
- Life: Forage → walk; Pursue (weaker mark) → walk then leap;
  Flee later. Do not add `DogJumpBite` as a life root.

## Done when

Play scene `Dog` and yard wolves walk without Space / W / F / R.
Stand with `-dogWalk` still works. Keyboard on the dog is gone.
