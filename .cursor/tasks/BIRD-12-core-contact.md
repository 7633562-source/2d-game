# BIRD-12 — birds touch only torso and head

Agent: `bird`. Layer: body / flock contact. Do not edit `BirdDrive`.
Play: `Bird` / `BirdFlock50` / `Chicken`. Do not retune PD or 200 Hz.

## One sentence

Birds generate solver contacts with each other only on torso and head.
Walk is `BirdMode.Walk`: horizontal `walkThrust` on the body, not a
paddle from the feet.

## Contract

`BirdCore` = Body + Head. `BirdLimb` = perch, wings, legs, neck, tail,
beak. Matrix: limb–limb and limb–core off. Limb–Ground stays.
`walkThrust` / `walkSpeed` defaults unchanged. No `AddForce` in `Update`.

## Accept

Flock pack still stands and walks (`bothGrounded`, `walkCount` when
wander). No new FixedUpdate probe. No `GetContacts` on the foot.

## Cost

One `IgnoreLayerCollision` pair at first build. One extra flock fixture
(head). Perch moved to a child so it can sit on `BirdLimb` (same body).
No N² `IgnoreCollision` per pair.
