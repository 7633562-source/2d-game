# VIS-HAND — hands are not shovels

Date: 2026-08-29. Dispatcher: TEAM LEAD.
Owner: **2D visual** (picture). The number lives in `Human.handSize` —
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
folded walk at ~25 s — physical length restored to 0.32; draw it shorter
only on `Visual`. Do not close this card until the dispatcher drops the
0.19 requirement or moves it to "picture only".
