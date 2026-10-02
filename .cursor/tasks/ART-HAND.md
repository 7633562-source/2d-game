# ART-HAND — hands are not shovels

Date: 2026-08-29. Dispatcher: TEAM LEAD.
Owner: **artist** (picture). The number lives in `Human.handSize` —
sprite and collider are the same size, not `localScale`.

## One sentence

A 0.32 m hand is almost the 0.34 m forearm — it reads as a third arm
link. Shorten it to Winter.

## Layer

Body/look: only the `Human.handSize` default. Do not touch PD, muscles,
levels, or bird.

## Contract

- Do not rename `handSize` (the stand writes it in `applied`).
- Keep width 0.06 (side view).
- Length: **0.19** (0.108 × 1.75 m per Winter, as in the comment above
  the sizes).
- Do not touch mass or `HAND_MASS_FRACTION`. Do not recompute spawn
  −0.82: the hand is not in the vertical leg chain.
- Two-foot CSV does **not** have to be bitwise: hand inertia changes.

## Acceptance

Stance 10 s: `fell: false`, `pelvisDrop` ≈ 0.0016, saturation 0.
`applied.handSize.y` = 0.19.

## Forbids

Do not scale `transform`. Do not amputate arms. Do not retune PD.

## Document status (2026-08-30)

In `Human.cs` now `handSize.y = 0.32`. Stance `vis_hand` at **0.19** was
accepted (`Trials/Runs/vis_hand_accept.md`). `current-front.mdc`: **0.19**
folded walk at ~25 s — physical length stays 0.32.

**03.09:** `PartDrawSize` / `handArtLength` are gone. The hand PNG is
stretched onto the 0.06 × 0.32 collider rectangle; a short palm is
empty pixels in that canvas, not a smaller `sprite.size`. Do not
close this card until the dispatcher drops the 0.19 requirement.

## Picture half — delivered 20.09 (ART-0 27th pass)

The collider is unchanged (`Human.handSize` 0.06 × 0.16), and the canvas still maps onto it
one-to-one (`.meta` ppu 3200, `Sliced`). What was wrong was the PICTURE, measured with
`python Tools/art0-part-ink-probe.py` on the shipped bytes: the hand filled **33 %** of its own box
(the only part of the body under half) and its ink was centred, so the wrist cut sat **0.052 m**
below the bone's proximal edge - in the rig the hand hung detached from the forearm that ends at
the wrist joint.

`Tools/gen-human-hand-texture.py` rewrites that one canvas from the hand's own accepted pixels:
`31,165 119x171 px (0.037 × 0.053 m), wrist margin 0.052 m, 31 832 B` became
`6,0 180x261 px (0.056 × 0.082 m), wrist margin 0.000 m, 63 865 B` - 94 % of the box width, which is
the proportional maximum for an art aspect of 0.70 in a box of 0.375. Nothing was repainted: the
accepted painted hand is scaled and anchored at the wrist.

Frames `art0hand27` against `art0hip29` (same lens, `ortho 1,97`; the human still moved in ONE
cluster, the hand's box x 1067-1082 y 661-689, every other still identical or inside its own
measured noise), three blind pairwise reads (seed 283 with the sides swapped) all correct. Report
`Trials/Runs/art-0-hand-27th-report.md`.

Two things this does NOT close: the PHYSICS length (the card's 0.19 against the code's 0.16 - the
dispatcher's call, untouched here), and the hand's orientation - the shipped art is a PALM view on a
side-view body. A procedural side-view hand was drawn twice this pass and read to a stop both times
("кухонная лопатка / варежка", then "гребень / грабли: все четыре пальца в одной плоскости"); the
record is in the generator's docstring so no later pass repeats it.

