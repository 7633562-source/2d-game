# DOG-5 - get up from a fall (dog slot)

Requirement (Yury 18.09 + 21.09): the dog that is down must get back on four
paws, INCLUDING from its back. Today it does not: 21.09 the dog somersaults
onto its back at ~3.3 s and lies there to the end of the 25 s run
(21.24 s down, getUpSuccess False), while the righting cycle only rocks
+-30..45 deg and never crosses.

States to tell apart: Standing / Walking / Falling / Fallen (belly) / On the
back (|chestPitch| >= 100) / GettingUp (righting + rise).

Milestones, IN ORDER (visible in the frames): on the back, paws up -> the rock
GROWS (>= 3 successive rocks deeper) -> the trunk crosses the tipping line ->
belly down, paws under -> four paws, standing. A rise that skips a milestone
is rejected even if it ends standing.

Acceptance: within 10 s of the fall - fourFeetGroundedFraction back above 0.5
in the post window, trunkDrop within the DOG_UP_DROP bar of the stand,
dogGetUpAttempts >= 1, dogGetUpSuccess True, dogGetUpSeconds <= 10; the
sequence visible in the 5 fps video. Lying still after a fall is a failure.

Files: DogStanceController.cs (the get-up / righting block), DogTrialRecorder.cs
(append-only columns and keys). One writer at a time: the dog slot's brief
is F:/hermes-data/ds-dog-task.md.

Muscles only: no AddForce/AddTorque, no pose snap, no new 0/1 flag - a signal.
