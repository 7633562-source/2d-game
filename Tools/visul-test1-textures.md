# visul — Test1 “Yard” level textures

Layer: pictures only. Do not touch physics, `SampleScene`, colliders, or platform sizes.

Level: scene `World`, profile `Test1`. Code already reads both `Resources/Level/*` and the fallback path `Resources/Art/*`.

## Status

| File | Status |
|---|---|
| dirt | **present** `Assets/Resources/Art/dirt.png` — wired, tiles on platforms |
| grass | **present** `Assets/Resources/Art/grass.png` — strip on top of the platform, no collider |
| sky | **missing** — solid color for now, follows the camera |
| finish sign | **missing** — a red placeholder sits at x = 48 |

`skin` / `denim` / `fabric` are body, not level. I am not wiring them.

## Still to deliver

Put into `Assets/Resources/Level/` (preferred names) **or** into `Art/`:

| File | Why | Pixels | PPU |
|---|---|---|---|
| `sky.png` | sky, no characters, soft gradient | 256×256 or larger | 32 |
| `sign_finish.png` | finish sign, alpha, no ground in frame | 64×128 | 32 |

For dirt and grass, if you resave them for a side view:

- textures are **top-down** now. In the sagittal scene they sit on the side face of the platform — readable, but this is not a yard cross-section.
- ideal for a second pass: `ground_fill` as the **side** of dirt (soil layers), `ground_cap` a narrow **side grass strip** 128×32, not a square lawn from above.
- Sprite Mode **Single**, Mesh **Full Rect**, Generate Physics Shape **off**, Wrap Repeat, PPU 32. dirt/grass currently use Mode Multiple and PPU 100 — the code survived via `LoadAll`, but Single is simpler.

Do not scale objects. Size is set by `sprite.size`.

## “Yard” geometry (do not redraw the shape)

Spawn surface y = −2.00, human −0.82. Steps of 5 cm, no gaps. Tape to x = 96.

| Piece | X | Surface height |
|---|---|---|
| back yard + fence picture | −32 … 0 | −2.00 |
| spawn | 0 … 8 | −2.00 |
| entry curb / garden | 8 … 18 | −1.95 |
| drain dip | 18 … 22 | −2.00 |
| return + courtyard | 22 … 40 | −1.95 then −1.90 |
| gate dip | 40 … 42 | −1.95 |
| finish plaza, sign x=48 | 42 … 50 | −1.90 |
| east rise … lookout | 50 … 96 | ±5 cm, peak −1.80 |

## Do not

- do not change `SampleScene`
- do not hang a BoxCollider2D on sky, grass, sign, hills
- do not rename `dirt.png` / `grass.png` until `Level/ground_fill` exists

This card is done when World has its own finish sign and the sky is not a flat fill.
