# VIS-FOOT — the foot is painted as a ski

Date: 2026-09-01. Dispatcher: this chat (human balance).
Owner: **2D visual**. Physics side is closed and must not move.

## One sentence

`Art/Human/foot.png` is stretched **2.4× along the bone** by `Sliced`, so
the human reads as standing on skis — the collider is correct, the picture
is not.

## Numbers, measured 01.09

Opaque rect of `foot.png` is 264×154 px → art aspect **1.71**.
`VisualSize` grows a painted part ×1.36 along the bone and ×1.22 across, so
the sprite is drawn 0.354 × 0.085 m → box aspect **4.14**.
4.14 / 1.71 = **×2.42**. `painted-parts.mdc` allows about a quarter.

Whole human for context (art aspect → drawn box aspect → distortion):

| part | art | box | stretch |
|---|---|---|---|
| head | 0.90 | 0.78 | ×0.86 |
| neck | 1.16 | 1.49 | ×1.28 |
| torso | 0.40 | 0.63 | ×1.58 |
| pelvis | 1.03 | 1.67 | ×1.63 |
| upper arm | 0.43 | 0.27 | ×0.63 |
| lower arm | 0.38 | 0.21 | ×0.56 |
| thigh | 0.45 | 0.33 | ×0.74 |
| shin | 0.34 | 0.23 | ×0.67 |
| **foot** | **1.71** | **4.14** | **×2.42** |

The hand is not in the table: `PartDrawSize` already exempts it by name and
draws it at PNG proportion. The foot is the worst part of the body; torso
and pelvis are the next two, and they are also past the quarter.

## Layer

Look only: sprite sizing in `HumanSegment` (`PartDrawSize` / `VisualSize`)
or a recrop of `Assets/Resources/Art/Human/foot.png`. Do not enter balance,
muscles, or the walk driver.

## Contract

- `Human.footSize` stays **0.26 × 0.07** and `ankleHeelOffset` stays
  **0.065**. They set the spawn point −0.82 and the ankle torque ceilings
  65 / 22 N·m (`balance-actuators.mdc`). Touching them invalidates the
  `stable-pd` baseline.
- Move the picture with `SetVisualOffset`, never the segment
  `localPosition` (`unity-2d-physics.mdc`).
- `PartDrawSize` exempts `Human/hand` by name today. Generalizing the
  exemption or adding the foot by name are both fine; do not change the
  hand's own numbers — see `VIS-HAND.md`, where a body-size edit folded
  walk at ~25 s.
- Do not touch mass fractions.

## Acceptance

- A purely visual change must give **bitwise the same CSV**. Run a default
  stance and compare SHA256 against a fresh default run, the way `vis_look`
  was accepted. A byte of difference means physics moved.
- Judge the assembled body in Play, not the PNG (`painted-parts.mdc`:
  done means assembled).

## Note for whoever takes it

The art aspect 1.71 is not a bad crop by itself: the painted foot includes
the instep up to the ankle, while the collider is only the sole slab
0.26 × 0.07. So there are two honest routes — recrop the art down to the
sole, or draw at PNG proportion and let the instep overhang the collider
(hand-style). Which one looks right is the owner's call; both keep the
collider untouched.

## Forbids

Do not "fix" it by changing `footSize`. Do not retune PD, cadence, or
contact friction: foot–ground µ was measured on 01.09 and stays 0.4
(`current-front.mdc`).
