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
if anything is missing. Run (bash, editor closed, **without** `-nographics` — the tiling check is a
render and skips itself silently when there is no graphics device):

```
"F:/6000.5.8f1/Editor/Unity.exe" -batchmode \
  -projectPath "E:/Games/2D Game/My project" \
  -executeMethod ArtImportCheck.VerifyArt \
  -logFile "E:/Games/2D Game/My project/Logs/art-import-check.log" -quit
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

## Re-verified 18.09 20:18 - independent run on the committed bytes

Same command as above (`-logFile Logs/art-import-check.log`, editor closed). Log kept as
`Logs/art-import-check-2026-09-18-2018.log`. Result `ART-IMPORT-CHECK end failures=0`, `ExitCode: 0`:

```
ART-IMPORT-CHECK OK Level/ground_fill via Load<Sprite> rect=1024x256 ppu=128 native=8x2m    format=RGB24
ART-IMPORT-CHECK OK Level/ground_cap  via Load<Sprite> rect=128x14  ppu=100 native=1,28x0,14m RGBA32
ART-IMPORT-CHECK OK Level/sky         via Load<Sprite> rect=512x128 ppu=32  native=16x4m   format=RGB24
ART-IMPORT-CHECK OK Level/sign_finish via Load<Sprite> rect=64x320  ppu=100 native=0,64x3,2m RGBA32
ART-IMPORT-CHECK OK Art/skin ... Art/leaf  tex=512x512 readable=True
ART-IMPORT-CHECK OK LevelArt.HasFill/HasCap/HasSign/Sky()/Fill()/Cap()/Sign()
ART-IMPORT-CHECK TILE ground_cap  box=8x0,14m period=164px colourDiffAtOneTile=0,0030 -> REPEATS
ART-IMPORT-CHECK TILE ground_fill box=32x2m   period=256px colourDiffAtOneTile=0,0000 -> REPEATS
```

`RGBA32` on `ground_cap` and `sign_finish` and `RGB24` on the two opaque pictures is the correct
split: Unity keeps alpha only where the art uses it. `readable=True` is what `ArtLibrary.SoftMap` /
`TileMap` need on the material path; `isReadable: 0` on the four level pictures is fine, `LevelArt`
only ever takes the Sprite.

Offline check of the same bytes (`PIL` + `numpy`, no Unity): every file decodes, every picture is
painted rather than blank (ground_fill 3430 distinct colours, sky 219, ground_cap 72, sign_finish 7 -
a flat sign), alpha sits where it belongs (fill/sky 100% opaque, cap 20.6% clear, sign 61.6% clear), and
none of the 222 `.meta` files in the project shares a guid (222 distinct - the metas were copied from
an existing sprite, so a duplicated guid was the real risk here).

Seam control experiment (source `LegacyArt/<key>.png` vs the shipped 512 vs a wrap-aware control
resize): the 512 LANCZOS downscale is **not** what makes a tile non-seamless - mean pixel distance to
the wrap-aware control is 0.00-0.02 of 255 and the seam figures barely move. Residual seams are in the
legacy source art.

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
5. **The material tiles tile only approximately.** Copy cost across the wrap seam vs the natural
   neighbour cost inside the tile (0-255, measured on the shipped 512s): `ground_fill` 6.23/6.25 and
   `ground_cap` 0.04/1.91 read seamless, `fabric` 4.29/2.04, `dirt` 10.36/8.49, `denim` 16.90/15.39,
   `grass` 22.63/19.65, `bark` 20.52/14.27, `leaf` 12.62/9.40, `skin` X 4.42/3.01 **Y 18.53/3.40**. A 2x2
   repeat of `skin`, `fabric`, `bark` and `leaf` shows a light cross through the boundary by eye. The
   legacy sources measure the same way (`skin` Y 19.22/4.35), so this is the incoming art, not the
   downscale - repairing it means a wrap blend on the tile edges, which is an art decision and is left
   open. Nothing draws these tiles yet (`ArtLibrary` is a stub), so no scene shows the seam today.
6. **`ground_fill` is seamless in X only** (Y 10.05/6.37). It matters only if a slab is drawn taller
   than the native 2 m; `ArtImportCheck` tiles the widest slab it has (32x2 m, horizontal only).

## Physics

No `.cs` file of the simulation was touched; the change is data plus one editor-only check tool.
`git status` shows only new files and this README/delivery card.

## Repaint 19.09 - the eye check found two pictures below the ship bar

ART-0's own tests passed, but the acceptance here is a PICTURE, so the four canvases were viewed at
native resolution and at gameplay scale (contact sheets in `Trials/Runs/art-0-repaint-2026-09-19/`).
Two defects came out that no byte, guid or seam test can see.

`ground_fill` - the root fibres were 6 straight bars of constant thickness with blunt squared ends,
52-130 px long and 5 rows tall. Measured before: 12+ rows in the top quarter carried a dark run
>= 20 px (rows 21-30 and 37-49, runs up to 84 px) - they read as rectangular smudges in the soil.
Now: 7 tapered fibres that meander (2 px thinning to 1 px, strength fading 0.30 -> 0.04 toward the
tip, drifting `+-1` px), cracks fade in and out (`0.26 + 0.34*sin(pi*t)` instead of a constant 0.70
mix) and fork, and stones get a 1-2 px contact shadow plus a jittered rim so the analytic ellipse
edge is gone. Measured after: 2 rows with a run >= 20 px, both 20-21 px, and the eye now reads
tapered meandering fibres instead of bars.

`sign_finish` - the old canvas read as a rigid slalom pole: a 16 px mast (almost half the 0.18 m box)
and a 28 px pennant tapering to a 1 px point, with one white stripe that cut the shape in two. Now:
an 11 px mast with a metal cap, a 46 px (0.13 m) banner of a red/cream chequer clipped by the
silhouette, a squared tag end (not a needle), regular integer steps on the taper, a 2 px mast-anchored
shadow on the cloth and two metal rings at the hoist. The chequer is the one finish motif that still
reads when the flag is 3 x 7 screen pixels: at 16x81 and 8x40 px on screen the chequer is legible, at
5x25 px it degrades to a two-tone mark (the flag is 2-3 px wide there - nothing more can survive).

Contracts kept: file names, sizes (`1024x256`, `64x320`), PPU (`128`, `100`), every `.meta` byte
unchanged, and the seamless X wrap of the tiled surfaces (fill seam 8.42 vs inner 8.68, cap 0.14 vs
3.30, sky 0.75 vs 0.63). Re-running `Tools/gen-level-textures.py` after the repaint changed exactly
two PNGs and left the other nine byte-identical (`git status` names only `ground_fill.png` and
`sign_finish.png`; the LANCZOS material path and the guids are deterministic).

Evidence: `Logs/art-import-check-2026-09-19-repaint.log` - `UNITY EXIT=0`, `failures=0`,
`Level/ground_fill via Load<Sprite> rect=1024x256 ppu=128`, `Level/sign_finish via Load<Sprite>
rect=64x320 ppu=100`, `LevelArt.HasFill/HasCap/HasSign/Sky()/Fill()/Cap()/Sign()` all OK, and
`TILE ground_cap ... -> REPEATS`, `TILE ground_fill box=32x2m period=256px colourDiffAtOneTile=0,0000
-> REPEATS`. Physics: no `.cs` touched, no collider or size involved.

Still open, unchanged by this cycle: the per-part sprites (`ART-1b`), the four `ArtLibrary` stubs
(the seven material tiles are reachable but nothing draws them), the 32 m slab stretching the fill
(`LevelArt.UseTiledFill()` + `ArtLibrary.Tiled` is the declared path and the fill is ready for it),
and the legacy material tiles' approximate tiling (item 5 above).

## Third pass 19.09 - the material tiles really tile now (skin, fabric)

Item 5 above stopped at "a wrap blend is an art decision and is left open". The gallery's 1 m
material swatch row (`Trials/Runs/gallery1_materials.png`, ART-1c) then showed it as a shipping
defect, so it was measured and fixed.

Measured on the shipped 512 bytes (`cut` = mean |first edge pixel - last edge pixel|, `inner` =
mean |neighbour|, both in 0-255 levels over RGB):

| material | cut X / Y | inner X / Y | ratio | verdict |
|---|---|---|---|---|
| skin | 5.90 / **24.71** | 4.01 / 4.54 | 1.5x / **5.4x** | defect: 94.6 % of the boundary pixels step more than 12 levels (p95 42) |
| fabric | 5.72 / 7.23 | 2.71 / 2.98 | 2.1x / 2.4x | defect: 7.1 % / 15.7 % step more than 12 levels |
| bark | 27.36 / 22.71 | 19.03 / 14.46 | 1.44x / 1.57x | noise texture: a cut is no worse than a groove |
| leaf | 16.83 / 17.07 | 12.53 / 12.20 | 1.34x / 1.40x | left alone |
| grass / denim / dirt | 30.17-22.54 | 26.20-18.68 | 1.13-1.23x | left alone |

So `skin` and `fabric` are the two that cannot tile; the other five have a cut no worse than their
own grain.

**What was tried and rejected.** Moving the crop origin to the least-discontinuous cut (a pure roll,
no resample) takes skin's boundary step from 24.71 to 4.09 and passes any boundary metric. A blind
2x2 A/B with neutral labels (`Trials/Runs/art-0-tilewrap/skin_blind.png`) had the eye pick the
*rolled* sheet as the one with a light band: a crop cannot remove a ramp, it only moves the step
inside the tile (16 px above the bottom edge, 24.5 levels). Kept as evidence of the trap - a
boundary metric cannot see a step that has moved inside.

**The fix.** `level_tile` in `Tools/gen-level-textures.py` adds one constant per row
(`c(y) = d * y / (h - 1)`, `d` = the row means' end-to-end difference) and one per column, and only
when `cut > 1.5` levels **and** `cut > 2x` the tile's own grain, with a 64-level cap. A constant per
row cannot change an interior difference: no blur, no resample, nothing relocated - the level simply
returns to itself at the wrap. It fired on skin (Y ramp 16.8..31.3) and fabric (Y 4.7..6.9,
X -4.0..-2.6), and on nothing else.

After: skin Y cut **24.71 -> 5.64** (its own grain 4.54), fabric Y 7.23 -> 5.12, X 5.72 -> 4.91. The
residual is the detail difference between the two edge rows, not a ramp.

**Determinism.** The rerun rewrites all 11 PNGs; `git status` names exactly two changed
(`skin.png`, `fabric.png`), and the four level PNGs plus all 11 `.meta` files are byte-identical
(`md5sum` before/after) - the guids did not move, so no scene reference changes and the sizes stay
407 KB / 334 KB.

**Evidence.**
- Blind 2x2 A/B, shipped bytes (`git show HEAD:Assets/Resources/Art/skin.png`) vs new, neutral
  labels: `art-0-tilewrap/skin_fixed_blind.png`, `fabric_fixed_blind.png`. Read: "the right sheet
  reads as one continuous surface; the left shows a clear brightness step."
- The real frame, same camera and same moment: preview player rebuilt
  (`Logs/art0-tilelevel-build.log`, exit 0) and captured with the ART-1 args
  (`Logs/art0-tilelevel-gallery2.log`: `GALLERY-CAPTURE DONE label=gallery2 stills=7
  panFrames=165 real=28 missing=13 dressed=5567`) into `Trials/Runs/gallery2_*.png`.
  `gallery1_materials.png` vs `gallery2_materials.png` differ in exactly two column blocks,
  x 281-431 and x 485-634 - the skin and fabric swatches - and nowhere else in the 1920x1080 frame.
  `art-0-tilewrap/frame_skin_blind.png` and `frame_fabric_blind.png` (3x, LEFT = shipped,
  RIGHT = fixed): the eye names the RIGHT as evenly lit on both.
- Physics: no `.cs` touched. `ArtLibrary.Skin` / `ArtLibrary.Fabric` have exactly one caller,
  `GalleryScene.cs:72` (the swatch row) plus the two editor checks, so neither PNG is on any
  simulation path. This cycle's `git status`: `Tools/gen-level-textures.py`, the two PNGs, this card.

**Still open.** `bark`, `leaf`, `grass`, `denim`, `dirt` keep their wrap: no worse than their own
grain by the numbers, but `leaf` still shows a sliced vein at the wrap by eye, so the next step
there - if Yury wants it - is a cross-fade band of a few tens of px over the wrap, which is a
deliberate 2-3 % detail trade rather than this constant-per-row correction.

