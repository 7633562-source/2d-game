# VIS-DOG — painted dog parts

Picture only. Do not change `Dog` sizes, masses, hinges, or
`DogStanceController`.

## What changed

Ten PNGs in `Assets/Resources/Art/Dog/`. `Dog.cs` passes
`ArtLibrary.Dog*` keys into `HumanSegment` the same way `Human` passes
`Human/*`. Far side is `DimFar(white)`. Headless still uses the 8×8
placeholder.

| File | Segment |
|---|---|
| `chest.png` | Chest |
| `pelvis.png` | Pelvis |
| `neck.png` | Neck |
| `head.png` | Head |
| `tail.png` | Tail (base on +X) |
| `front_upper.png` | front upper |
| `front_lower.png` | front forearm |
| `thigh.png` | rear thigh |
| `shin.png` | rear shin |
| `paw.png` | all four paws |

Sources: `ArtIncoming/Dog/gen/`. Re-import: `Tools/import-dog-art.ps1`.

## Contract

- Collider size unchanged. Sprite on child `Visual`.
- No `localScale` on a body with `Rigidbody2D`.
- Do not start a dog or human baseline for this look pass.

## Look verdict (01.09)

This pass is **not** a finished dog. Ten independent gens. Pelvis
reads as a limb. Thigh reads as a furred human arm. Front upper has a
painted knee on a straight bone. `Sliced` still forces collider
aspect (only the human hand is exempt). Crow / Chicken used the same
method — next paint follows `painted-parts.mdc`.

**01.09 gaps:** sky between chest/pelvis and chest/neck was PNG
padding under `Sliced`, not missing bones. Crop to opaque + painted
overlap 1.36. Colliders unchanged.
