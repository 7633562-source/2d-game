# ART-0 delivery - the art reaches the loaders (18.09.2026, artist stream)

Every claim below is produced by a command that is written next to it. Read ART-0-inventory.md
first: this card is the delivery, not a second recon.

## What the loaders ask for (quoted, not guessed)

`Assets/LevelArt.cs`
- `PathsFor`: `"Level/ground_fill"`, `"Level/ground_cap"`, `"Level/sky"` (then `"Art/sky"`),
  `"Level/sign_finish"` (then `"Art/sign_finish"`).
- `LoadAt`: `Resources.Load<Sprite>(path)`, falling back to `Resources.LoadAll<Sprite>(path)[0]`.
  So all four are **Sprite** assets under `Assets/Resources/Level/`.

`Assets/ArtLibrary.cs` (last revision that loaded anything, `git show a13565ca:Assets/ArtLibrary.cs`)
- `LoadSource`: `Resources.Load<Texture2D>("Art/" + key)`, keys `skin`, `fabric`, `denim`, `dirt`,
  `grass`, `bark`, `leaf`.
- `SoftMap`/`TileMap` need `src.isReadable` (`GetPixelBilinear`), `TileSize = 512`, and `Tiled`
  computes its own `ppu = tex.width / metersPerTile` - the on-disk PPU is not used by that path.
  So the seven material tiles are **readable textures** under `Assets/Resources/Art/`.

## Files added (all painted/downscaled by `Tools/gen-level-textures.py`)

| file | purpose | source | original -> final |
|---|---|---|---|
| `Resources/Level/ground_fill.png` | slab fill, soil side view | painted | 1024x256, 435 KB |
| `Resources/Level/ground_cap.png` | 0.14 m grass cap strip | painted | 128x14, 709 B |
| `Resources/Level/sky.png` | sky colour ramp | painted | 512x128, 26 KB |
| `Resources/Level/sign_finish.png` | finish pennant on a post | painted | 64x320, 889 B |
| `Resources/Art/skin.png` | body material key `skin` | LegacyArt/skin.png | 1024x1024, 2003 KB -> 512x512, 407 KB |
| `Resources/Art/fabric.png` | key `fabric` | LegacyArt/fabric.png | 1024x1024, 2149 KB -> 512x512, 334 KB |
| `Resources/Art/denim.png` | key `denim` | LegacyArt/denim.png | 1024x1024, 3096 KB -> 512x512, 623 KB |
| `Resources/Art/dirt.png` | key `dirt` | LegacyArt/dirt.png | 1024x1024, 2658 KB -> 512x512, 550 KB |
| `Resources/Art/grass.png` | key `grass` | LegacyArt/grass.png | 1024x1024, 2920 KB -> 512x512, 664 KB |
| `Resources/Art/bark.png` | key `bark` | LegacyArt/bark.png | 1024x1024, 2871 KB -> 512x512, 597 KB |
| `Resources/Art/leaf.png` | key `leaf` | LegacyArt/leaf.png | 1024x1024, 2126 KB -> 512x512, 552 KB |

512 is not a taste decision: `ArtLibrary.TileMap` downscales to `TileSize = 512` at runtime anyway,
so a bigger source buys nothing for a tiled surface and costs build weight.

Each PNG has a hand-written `.meta` copied from the project's own Single-sprite shape
(`Assets/LegacyArt/Apple/fruit.png.meta`): `textureType: 8`, `spriteMode: 1`, `spriteMeshType: 0`
(Full Rect - required by Sliced and Tiled draw modes), `wrapU/wrapV: 1`,
`spriteGenerateFallbackPhysicsShape: 0` (art never carries a collider), `isReadable: 1` for the
materials only, PPU 128 / 100 / 32 / 512 as above.

## Proof that the loaders find them

`Assets/Editor/ArtImportCheck.cs` loads every path exactly the way the loaders do and exits non-zero
if anything is missing. Run (bash, editor closed):

```
"F:/6000.5.8f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "E:/Games/2D Game/My project" \
  -executeMethod ArtImportCheck.VerifyArt -logFile <logfile> -quit
```

Result 18.09: `failures=0`, and for each key
`Level/ground_fill via Load<Sprite> sprite=ground_fill rect=1024x256 ppu=128 ...`,
seven times `Art/skin ... Art/leaf readable=True`, and
`LevelArt.HasFill/HasCap/HasSign/Sky()/Fill()/Cap()/Sign()` all OK.

Tiled draw mode (the grass cap, and the tiled materials later) was checked by rendering, not by the
meta file: `ArtImportCheck` renders the cap in `Tiled`/`Continuous` at 8 x 0.14 m and compares the
pixels one native tile apart.

```
ART-IMPORT-CHECK TILE ground_cap  period=164px colourDiffAtOneTile=0.0030 -> REPEATS
ART-IMPORT-CHECK TILE ground_fill period=256px colourDiffAtOneTile=0.0000 -> REPEATS
```

The sprite's texture reports `wrapMode = Clamp` at runtime even though the import keeps
`wrapU/wrapV: 1`, so the pixel test is the only trustworthy answer - and it is positive.

## Left as a labelled gap (not delivered, deliberately)

1. **Per-part sprites for the creatures and the human.** The pre-stub `ArtLibrary` keys are
   `Human/head ... Dog/paw`, i.e. `Resources/Art/Human/<part>.png`; the 62 PNGs still sit in
   `LegacyArt` (not a Resources folder, so `Resources.Load` cannot see them) and they have to be
   split/cropped to the collider aspect before they can ship. That is the ART-1b step.
2. **`ArtLibrary.Albedo` / `ArtLibrary.Tiled` are stubs today** - they return `null` unconditionally,
   so the seven material tiles are reachable by the loader's path convention but no drawing code
   consumes them yet (`ART-IMPORT-CHECK note ArtLibrary.Tiled(dirt) = null (stub)`).
3. **Wide slabs stretch the fill.** `StaticPlatform.cs:38` draws the fill `Sliced`, which stretches
   the whole canvas: an 8 m slab shows 128 px/m, the 32 m "Behind" slab 32 px/m, and the art's
   inclusions flatten with it. The fix is the tiled path the code already declares
   (`LevelArt.UseTiledFill()` + `ArtLibrary.Tiled(GroundTileMeters = 2.5)`); the pixel test above
   shows `ground_fill` already tiles seamlessly at its 8 m period, so the art is ready for it.
4. **Sky/ground look in other scenes changes** as soon as these files exist (`LevelArt.Sky() != null`
   switches `DayNightCycle` to its textured branch). Pictures only - no code, no physics, no collider.

## Physics

No `.cs` file of the simulation was touched; the change is data plus one editor-only check tool.
`git status` shows only new files and this README/delivery card.
