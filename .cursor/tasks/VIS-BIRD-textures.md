# VIS-BIRD — crow and chicken parts

Picture only. Do not change `Bird` sizes, `BirdFlight`, or `BirdKind`
recipe numbers.

## What changed

Two folders: `Assets/Resources/Art/Crow/` and `Art/Chicken/`.
`Bird.Art(part)` picks the folder from `kind`. Play `Bird` / `BirdFlock50`
uses Crow. Play `Chicken` uses Chicken.

| File | Segment |
|---|---|
| `body.png` | Body |
| `neck.png` | Neck |
| `head.png` | Head |
| `beak.png` | Beak |
| `tail.png` | Tail |
| `wing.png` | humerus / ulna / hand |
| `thigh.png` | Thigh |
| `shank.png` | Shank |
| `foot.png` | Foot |

Sources: `ArtIncoming/Crow/gen/`, `ArtIncoming/Chicken/gen/`.
Re-import: `Tools/import-bird-art.ps1`.

## Contract

- Collider size unchanged. Flock still one `Rigidbody2D`.
- Far side is `DimFar(white)`.
- Headless placeholder 8×8 as before.
- Do not start a bird baseline for this look pass.
