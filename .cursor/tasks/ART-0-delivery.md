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
- The real frame, same camera: preview player rebuilt (`Logs/art0-tilelevel-build.log`, exit 0) and
  captured with the ART-1 args (`Logs/art0-tilelevel-gallery2.log`: `GALLERY-CAPTURE DONE
  label=gallery2 stills=7 panFrames=165 real=28 missing=13 dressed=5567`) into
  `Trials/Runs/gallery2_*.png`. All seven lenses framed identically (`orthoSize` equal to the
  digit: 33.986 / 1.968 / 1.304 / 5.300 / 3.577 / 8.347), so the A/B is the same camera.
  `gallery1_materials.png` vs `gallery2_materials.png` differ in exactly two column blocks,
  x 281-431 and x 485-634 - the skin and fabric swatches - 24119 px, and nowhere else in the
  1920x1080 frame. `art-0-tilewrap/frame_skin_blind.png` and `frame_fabric_blind.png` (3x,
  LEFT = shipped, RIGHT = fixed): the eye names the RIGHT as evenly lit on both.
- Control: the same build captured twice (`gallery2b`, `-panSeconds 0`) is **pixel-identical**
  (0 differing pixels in all seven frames), so the captures are deterministic and any difference
  between gallery1 and gallery2 is the build's, not the capture's.
- Frames that are NOT part of this A/B: `apple` (30403 px) and `level` (4350 px, x 1264-1415
  y 605-668, i.e. the same skin/fabric swatches seen in the level lens) differ because gallery1 and
  gallery2 are two player builds (`buildGuid` bdcea409 vs 481cb4c9) and the tree compiled in between
  carries the plant lane's uncommitted rig work. The apple tree cannot be affected by this change at
  all: it is dressed from `bark`, `leaf` and `Apple/fruit`, whose bytes are identical.
- Physics: no `.cs` touched. `ArtLibrary.Skin` / `ArtLibrary.Fabric` have exactly one caller,
  `GalleryScene.cs:72` (the swatch row) plus the two editor checks, so neither PNG is on any
  simulation path. This cycle's `git status`: `Tools/gen-level-textures.py`, the two PNGs, this card.
- Re-gated on the changed bytes (editor closed, no `-nographics`):
  `Unity.exe -batchmode -projectPath "E:/Games/2D Game/My project" -executeMethod
  ArtImportCheck.VerifyArt -logFile Logs/art-import-check-2026-09-19-tilelevel.log -quit` ->
  `UNITY EXIT=0`, 0 `error CS`, `failures=0`: all four `Level/*` via `Load<Sprite>` (1024x256 ppu=128,
  128x14 ppu=100, 512x128 ppu=32, 64x320 ppu=100), all seven `Art/<key>` `512x512 readable=True`,
  `TILE ground_cap ... REPEATS`, `TILE ground_fill box=32x2m ... colourDiffAtOneTile=0,0000 -> REPEATS`.
  The notes now read `ArtLibrary.Albedo(dirt) = texture` / `Tiled(dirt) = sprite`, not `null (stub)`:
  the tool's own "stubs today" comment was stale and is corrected in the same commit.

**Still open.** `bark`, `leaf`, `grass`, `denim`, `dirt` keep their wrap: no worse than their own
grain by the numbers, but `leaf` still shows a sliced vein at the wrap by eye, so the next step
there - if Yury wants it - is a cross-fade band of a few tens of px over the wrap, which is a
deliberate 2-3 % detail trade rather than this constant-per-row correction.

## Fourth pass 19.09 - gap 2: the campfire flame core is painted

Gap list item 2 ("Campfire (flame/embers) - currently procedural") is answered. New art:
`Assets/Resources/Art/Campfire/core.png` (81x192, RGBA32, readable, drawn by
`Tools/gen-fire-textures.py`) plus `Campfire.CoreSprite()` -> `ArtLibrary.Albedo("Campfire/core")` ->
`Sprite.Create`, handed to `MakeDisc` for the `FlameCore` renderer only (a missing PNG keeps today's
soft disc; the campfire has no collider and no Rigidbody2D, so no physics file is involved).

The three pooled-tongue canvases were painted, judged and **rejected**: three blind reviews of the same
moment and camera (`Trials/Runs/art-fire-2026-09-19/flame_variants_300pxm.png`) ranked the all-painted
fire last every time - 24 painted tongues read as separate spindles ("comb teeth", "glass fibres"),
while the soft discs merge into one mass. The core, one sprite drawn in front, is the case that wins:
the same review picked the core-painted fire as the best of four ("чёткое ядро с выразительной
верхушкой"). The tongues therefore keep `SoftTexture()` on purpose, and the generator prints them as
`rejected, evidence only, NOT written`.

Verified: `grok-compile-gate.sh` exit 0; `art1-gate.sh` -> `ART-1A-CHECK end failures=0`, exit 0, with
five new flame assertions (Albedo resolves, `81x192 readable=True format=RGBA32`, aspect 0,422 vs box
0,421, no tongue PNG, `Sprite.Create` succeeds -> `native=1x2,37m`). Inventory moved **66 real / 3
MISSING -> 67 / 2**; still missing: `flower` (plant lane) and `UI icons` (out of scope).

**CLOSED 19.09 (artist stream).** The stale row is gone: `GalleryScene.BuildFireRow` draws the painted
core as a real sprite (Sliced, `size` = Campfire's own `CoreBoxWidth x CoreBoxHeight`, tint `CoreTint` -
key, box and tint are read out of `Campfire.cs`, so the catalogue cannot drift from the fire the game
draws), `BuildMissingRow` keeps only flower + UI icons, and `GalleryCapture` takes a `<label>_fire.png`
still. The gallery inventory moved 28 real / 13 missing -> **29 / 11**, which now agrees with the gate
inventory (67 / 2). A Unity frame of the fire exists: `Trials/Runs/gallery4_fire.png` (1920x1080,
ortho 1.59) from the preview player, plus the whole-gallery still and the 16.5 s pan on the same build.
The core is drawn **static** on purpose - a live `Campfire` ticks a Perlin flicker and would break the
same-build capture control - so the row shows the shipped texture, not the assembled fire. The eye
reads the lone core as a light spindle rather than a living fire, which is recorded as the next look
item (a static stand-in of the assembled stack, or a repaint that carries more silhouette). Full
numbers: `Trials/Runs/art-0-fire-gallery-report.md`.

## Fifth pass 19.09 - the grass cap is drawn in tufts, and the root band stops being a slab

The ART-0 brief's five items were re-verified from scratch this cycle on today's bytes: two fresh Unity
batch runs, `art0-import-gate.sh` and `art1-gate.sh`, both `unity exit=0`, `failures=0` - four
`Level/*` via `Load<Sprite>` (1024x256 ppu=128, 128x14 ppu=100, 512x128 ppu=32, 64x320 ppu=100), the
seven `Art/<key>` `512x512 readable=True`, `TILE ground_cap ... colourDiffAtOneTile=0,0083 -> REPEATS`,
`TILE ground_fill box=32x2m ... 0,0000 -> REPEATS`, `ART-1A-CHECK end failures=0` with
`flower -> MISSING (procedural)` and `UI icons -> MISSING` as the only two gaps. Nothing about the
loader contract or the file set changed; what changed is one picture that did not survive the eye.

**The defect.** Judged at game scale on the frame the previous cap pass used as its own acceptance
(`Trials/Runs/ART-0-frames/cap-on-ground.png`, 10.24 m of ground at 200 px/m), the shipped strip read
as comb teeth with one thin spike standing up at the top of EVERY repeat - a ruler mark that gives the
tiling away. Measured on the old bytes: the top three rows held 9 / 10 / 56 columns (a 1 px sawtooth,
not grass), one bright blade measured 5.125 levels against the dimmer neighbour (7.7x the tile's own
grain of 0.665), and rows 10-12 of 14 were a flat slab - std 1.39 / 1.39 / 1.92 with 17 distinct
colours across 128 px.

**The repaint** (same 128x14 canvas, same PPU 100, same `.meta` guid - `git status` does not name the
meta). `Tools/gen-level-textures.py` now builds the silhouette from **tufts**: `_cap_clumps` cuts the
128 px ring into runs of 2..5 columns, each tuft gets one top, one light face and a bounded step
inside itself, and the widths wrap exactly so the pattern across the seam matches the pattern inside.
The de-tick guard now pulls BRIGHT columns toward their neighbours as well as dark ones
(`GRASS_TICK_LEVELS = 3.0`, both signs; it fired 0 times - the tuft construction removed the outliers
before the guard had to). The root rows mix a 2 px grain field plus a ±2 dither.

Measured after (same probe, both revisions reported by the same code):

| measurement | before | after |
|---|---|---|
| blade silhouette | 1 px needle sawtooth | tufts of 2..5 columns sharing a top |
| worst height spike (taller than BOTH neighbours) | 2 px | 1 px |
| tuft columns (top within 1 px of a neighbour) | 95 % | 99 % |
| worst bright stand-out vs the dimmer neighbour | 5.125 (7.7x grain) | 3.794 (3.4x grain) |
| worst stand-out vs BOTH neighbours | dark 1.778 / bright 2.083 (grain 0.665) | dark 2.413 / bright 2.095 (grain 1.112) |
| root rows y10-12 | std 1.39 / 1.39 / 1.92, 17 / 17 / 24 colours | std 2.39 / 2.32 / 2.83, 59 / 59 / 52 colours |
| wrap seam vs the tile's own grain | 0.08 vs 5.31 | 1.14 vs 5.09 (seamless) |

Stated plainly: the strict "above BOTH neighbours" metric did not improve (2.08 -> 2.10) and was never
the defect - grass has a grain by design. The silhouette, the periodic bright clump and the flat slab
are what moved, and the eye agrees: on `cap-before-after.png` (old row on top, new row below, same 8
repeats, same soil) the reviewer named the spike at every repeat in the old row and called the new row
"soft organic clumps - the defect is gone".

**Evidence.**
- `Trials/Runs/art-0-cap-tufts-report.md` (this cycle's full report: loader lines, the file table with
  original -> final sizes, the metrics, the commands).
- Pictures: `Trials/Runs/ART-0-frames/cap-before-after.png`, `cap-on-ground.png`, `cap-tiled-8x.png`.
- The real frame from a real build: `bash scripts/art-gallery-capture.sh art0cap` ->
  `PREVIEW_BUILD_OK ... scenes=12 errors=0`, `GALLERY-CAPTURE DONE label=art0cap stills=8
  panFrames=165 real=29 missing=11 dressed=5567`, capture failures 0. `art0cap_level.png` against
  `gallery4_level.png` (same still definition, same camera, `ortho=8,35`) differs in 3717 px of
  2 073 600, all of them inside rows 670-677 - the grass strip. Nothing else in the frame moved.
- Physics: no `.cs` touched, `Tools/` is outside `Assets/` and not compiled, and the cap hangs on a
  `SpriteRenderer` in `Tiled` mode with no collider, so the reference runs cannot move.

**Still open** (unchanged): `flower` and `UI icons` (labelled MISSING in the gallery), the next look
item on the finish sign (the red/cream chequer reads like a roadworks barrier at small scale; the
classic black/white chequer is the stronger read), a style mismatch between the flat-shaded grass cap
and the noisy soil fill (a second read of the WIDE gallery still picked exactly that, at 9 px of strip
it cannot judge the cap itself - recorded in the report, with the fill's wide-slab stretch bands, as
the next ground item), and the earlier passes' items - per-part binding of the creatures (ART-2/ART-3),
the flame core's spindle read.

## Sixth pass 19.09 (artist) - reachability re-verified on today's bytes, and the finish-sign item is closed

Two fresh Unity batch runs on the tree as it stands (16:50, editor closed): `art0-import-gate.sh` ->
`unity exit=0`, `failures=0`, four `Level/*` via `Load<Sprite>` (1024x256 ppu=128, 128x14 ppu=100,
512x128 ppu=32, 64x320 ppu=100), seven `Art/<key>` `512x512 readable=True`, `TILE ground_cap ...
colourDiffAtOneTile=0,0083 -> REPEATS`, `TILE ground_fill box=32x2m ... 0,0000 -> REPEATS`;
`art1-gate.sh` -> `ART-1A-CHECK end failures=0`, `55 part keys checked=0 missing`. Offline, PIL: 318
`.meta` files, 318 distinct guids, 0 duplicates; the originals are 1024x1024 and the shipped tiles
512x512 (3.85-6.42x smaller, 13.9 MB -> 3.73 MB in total). Nothing had to be added to the ART-0 file set.

The open look item from the fifth pass is done: the finish sign's cloth is graphite black + warm white
instead of red + cream. `python Tools/gen-level-textures.py sign_finish` (one key, guid reused) changed
exactly one file - the md5 of the four level PNGs, the seven material PNGs and all eleven `.meta` files
differs before/after on one line only, and the alpha channel is pixel-identical (11172 opaque px,
bbox 4,0,64,320 on both), so only RGB moved. Two eye checks at 64x320 / 36x180 / 16x81 / 8x40 / 5x25 px
and one on a real build frame read the new cloth as a finish and the old one as a checkpoint/hazard
marking; the flag still dies below 8x40, where the whole sprite is 3-4 px wide. From a real build
(`art-gallery-capture.sh art0sign`, `scenes=12 errors=0`, 8 stills, 16.5 s pan, `real=29 missing=11`):
`art0sign_level.png` against `art0cap_level.png` differs in 3298 of 2 073 600 pixels, all inside one
column run x 1054-1083 rows 468-590 - the banner cloth, nothing else. Commit `da788536`, two files.
Full numbers: `Trials/Runs/art-0-sign-report.md`.

Still open, unchanged: `flower` (procedural) and `UI icons` as labelled MISSING tiles, the per-part
binding of the creatures (ART-2/ART-3), the flame core's spindle read, the cap/soil style mismatch and
the wide-slab fill stretch (`LevelArt.UseTiledFill()` + `ArtLibrary.Tiled` is the declared path and the
fill is ready for it).

## Seventh pass 19.09 (artist) - the five brief items re-verified, and the finish sign is a finish now

Two fresh Unity batch runs on today's bytes (editor closed): `art0-import-gate.sh` -> `unity exit=0`,
`failures=0`, the four `Level/*` through `Load<Sprite>` (1024x256 ppu=128 RGB24, 128x14 ppu=100 RGBA32,
512x128 ppu=32 RGB24, 64x320 ppu=100 RGBA32), the seven `Art/<key>` `512x512 readable=True`,
`TILE ground_cap ... 0,0083 -> REPEATS`, `TILE ground_fill box=32x2m ... 0,0000 -> REPEATS`;
`art1-gate.sh` -> `part keys checked=55 missing=0`, `failures=0`. Offline on the same bytes: 318 `.meta`
files, 318 distinct guids, 0 duplicates; the seven material tiles are 1024x1024 (2003.7-3096.3 KB) ->
512x512 (334.6-664.6 KB), 17.41 MB -> 3.64 MB, and a fresh LANCZOS 512 of each source reproduces the
shipped file with maxdiff 0.0 for five of the seven (skin/fabric differ only by their tile-levelling).
Nothing had to be added or moved: the ART-0 file set is complete as it stands.

The one picture that did not survive a fresh eye read was the finish sign. Repainted within the same
canvas, PPU and `.meta` (guid unchanged, file 953 -> 959 B): 3 columns x 5 rows of 15 x 38 px on a metal
mast instead of 2 x 3 giant cells on brown wood. The grid was picked by a blind four-way read at native /
16x81 / 8x40 px - the previous picture came LAST ("a wooden post breaks the finish association, and 2x3
blocks read as a roadworks barrier"), 11 px cells came third ("grey mush at 8x40"), the winner is the
middle. `alpha byte-identical: True`, opaque 11172 -> 11172, bbox 4,0,64,320 unchanged: an RGB repaint,
no silhouette pixel moved. Real frame from a real build (`art-gallery-capture.sh art0sign3`,
`scenes=12 errors=0`, 8 stills, 16.5 s pan, `real=29 missing=11`, buildGuid `ea149f04cc5341da94f3bc39fd121cfe`)
against the previous pass's frame: 3836 of 2 073 600 px differ, all inside one region x 1045-1083
y 463-668 - the sign, nothing else. Video `Trials/Runs/art0sign3.mp4` (1920x1080, 165 frames, 16.5 s).
Full numbers and the residuals the same eye check found (sign cell aspect, the cap's visible period, the
fill's stamped stones and Y seam, the flame core's spindle read): `Trials/Runs/art-0-sign3-report.md`.

Physics: no simulation file touched this cycle - one PNG plus `Tools/gen-level-textures.py` (outside
`Assets/`, not compiled). The sign's box comes from the level data and `StaticPlatform` sets `sr.sprite`
only, so no collider, size or number can move.

Still open, unchanged: `flower` and `UI icons` (labelled MISSING), the per-part binding of the creatures
(ART-2/ART-3), the flame core's spindle read, the cap/soil style mismatch, the wide-slab fill stretch, and
now the two look items this pass recorded (the sign's elongated cells need a shorter banner to become
square; `filterMode` Point for the pixel art).

## Eighth pass 19.09 (artist) - the five brief items re-verified, and the 19.09f cap's import proof is closed

The ART-0 brief's five items were walked once more on today's bytes (HEAD `5972c095`), and the one thing
the previous cycle had to leave open - the engine proof for the repainted `ground_cap` - is now green.

**Item 1, the loader contract (quoted from the files, not guessed).** `Assets/LevelArt.cs`:

```
PathsFor:  FillKey  -> "Level/ground_fill"
           CapKey   -> "Level/ground_cap"
           SkyKey   -> "Level/sky",  "Art/sky"
           SignKey  -> "Level/sign_finish", "Art/sign_finish"
Load:      for (int i = 0; i < paths.Length && found == null; i++) found = LoadAt(paths[i]);
LoadAt:    Sprite single = Resources.Load<Sprite>(path);            if (single != null) return single;
           Sprite[] many = Resources.LoadAll<Sprite>(path);         if (many.Length > 0) return many[0];
Fill():    Load(FillKey) ?? ArtLibrary.Tiled(ArtLibrary.Dirt, ArtLibrary.GroundTileMeters) ?? White();
Cap():     Load(CapKey)  ?? ArtLibrary.Tiled(ArtLibrary.Grass, ArtLibrary.GrassTileMeters) ?? White();
```

So the four level pictures are **Sprite** assets under `Assets/Resources/Level/` with those exact
names - satisfied. `Assets/ArtLibrary.cs`, the material path:

```
Albedo(key):  Texture2D tex = Resources.Load<Texture2D>("Art/" + key);   // line 139
              // "bark" -> Art/bark.png, "Human/head" -> Art/Human/head.png
Tiled(key, metersPerTile): float ppu = tex.width / metersPerTile;        // line 165
              Sprite.Create(tex, full rect, pivot .5/.5, ppu, 0, SpriteMeshType.FullRect)
```

So the seven material keys (`skin`, `fabric`, `denim`, `dirt`, `grass`, `bark`, `leaf`) are **readable**
`Texture2D` assets under `Assets/Resources/Art/`, key = path fragment - satisfied. A key with no file
returns `null` on purpose, and the caller must then show the labelled MISSING-ART tile.

**Item 2, the materials, downscaled before bundling.** 1024x1024 sources in `Assets/LegacyArt/` (which is
not a Resources folder and still must not be bundled) -> 512x512 in `Assets/Resources/Art/`. 512 is not
taste: `Tiled` derives its own ppu from `tex.width`, and a tiled surface at this camera scale buys
nothing from 2048.

| file (key) | source | original | final | ratio |
|---|---|---|---|---|
| `Art/skin.png` | `LegacyArt/skin.png` | 1 024x1 024, 2003.7 KB | 512x512, 407.8 KB | 4.91x |
| `Art/fabric.png` | `LegacyArt/fabric.png` | 1 024x1 024, 2149.8 KB | 512x512, 334.6 KB | 6.42x |
| `Art/denim.png` | `LegacyArt/denim.png` | 1 024x1 024, 3096.3 KB | 512x512, 623.1 KB | 4.97x |
| `Art/dirt.png` | `LegacyArt/dirt.png` | 1 024x1 024, 2658.8 KB | 512x512, 550.6 KB | 4.83x |
| `Art/grass.png` | `LegacyArt/grass.png` | 1 024x1 024, 2920.5 KB | 512x512, 664.6 KB | 4.39x |
| `Art/bark.png` | `LegacyArt/bark.png` | 1 024x1 024, 2871.0 KB | 512x512, 597.5 KB | 4.81x |
| `Art/leaf.png` | `LegacyArt/leaf.png` | 1 024x1 024, 2126.8 KB | 512x512, 552.1 KB | 3.85x |
| total | | 17.41 MB | 3.64 MB | 4.78x |

**Item 3, the four level pictures the README asked for, with the exact names.** All four exist in
`Assets/Resources/Level/` (plus `README.txt`): `ground_fill` 1024x256 PPU 128, `ground_cap` 128x14 PPU 100,
`sky` 512x128 PPU 32, `sign_finish` 64x320 PPU 100; painted by `Tools/gen-level-textures.py`, seamless
where they tile. Nothing from the brief is left absent, so no MISSING-ART tile is needed for them.

**Item 4, the import, verified on the `.meta` bytes.** Every one of the eleven pictures carries
`textureType: 8` (Sprite), `spriteMode: 1` (Single), `spriteMeshType: 0` (Full Rect - what `Sliced` and
`Tiled` need), `wrapU/wrapV: 1`, `spriteGenerateFallbackPhysicsShape: 0` (art never carries a collider),
`isReadable: 1` on the seven material tiles (only they need CPU pixels for `Sample`) and `0` on the four
level pictures. PPU 128 / 100 / 32 / 100 on `Level/*`, 512 on the materials. `Assets/` holds 318 `.meta`
files with 318 distinct guids, 0 duplicates - the copied metas did not collide.
Command: `python Temp/grok-burn/art0-meta-audit.py` (offline, no Unity).

**Item 5, bash only, and the engine proof.** Both gates were run through bash on HEAD `5972c095` with the
shipped 19.09f `ground_cap.png` (`md5 9ce2387464b8135e1fd7ebb2873413d7`, identical before and after the
run):

```
bash $LOCALAPPDATA/hermes/scripts/art0-import-gate.sh  -> unity exit=0, failures=0, no "error CS"
  OK Level/ground_fill 1024x256 ppu=128 RGB24 | Level/ground_cap 128x14 ppu=100 RGBA32
  OK Level/sky 512x128 ppu=32 RGB24            | Level/sign_finish 64x320 ppu=100 RGBA32
  OK Art/skin .. Art/leaf  7 x 512x512 readable=True RGB24
  TILE ground_cap  box=8x0,14m native=1,28m period=164px rowStd near=0,0178 far=0,0177 colourDiffAtOneTile=0,0103 -> REPEATS
  TILE ground_fill box=32x2m   native=8m    period=256px rowStd near=0,0133 far=0,0133 colourDiffAtOneTile=0,0000 -> REPEATS
bash $LOCALAPPDATA/hermes/scripts/art1-gate.sh         -> unity exit=0, "ART-1A-CHECK end failures=0"
  55 part keys + 7 materials + the 4 level keys + the campfire flame core -> real; flower -> MISSING (procedural); UI icons -> MISSING
```

The cap's wrap cost (`0,0103`) is still below the tile's own grain (`0,0178`), so the tuft repaint did
not reintroduce the ruler mark as a seam. Full output kept at
`Temp/grok-burn/art0-cap1909f-close-gate.out`; the previous cycle's report
(`Trials/Runs/art-0-cap-rhythm-report.md` section 4) is updated from PENDING to CLOSED. The editor was
closed for both runs (no `Temp/UnityLockfile`), and the waiter held off until the other lane's
`trial.lock` cleared, so no trial was raced.

**Still open, unchanged.** `flower` (procedural; the caller is the plant lane) and `UI icons` (out of
scope) as labelled MISSING tiles; the per-part binding of the creatures (ART-2/ART-3); the flame core's
spindle read; the cap/soil style mismatch; the wide-slab fill stretch (`LevelArt.UseTiledFill()` +
`ArtLibrary.Tiled` is the declared path, the fill is ready); the sign's elongated cells; `filterMode`
Point for the pixel art.

**Physics.** No simulation file touched this pass - the change is one report card plus a report under
`Trials/Runs/` (gitignored), zero `.cs`, zero PNG, zero `.meta`. The reference runs cannot move.

## Ninth pass 19.09 (artist) - the five brief items re-verified, and the cap gets a silhouette the frame can carry

Both picture gates were run again through bash on today's bytes (editor closed, no
`Temp/UnityLockfile`, no `trial.lock`): `art0-import-gate.sh` -> `unity exit=0`, `failures=0`, the four
`Level/*` through `Load<Sprite>` (1024x256 ppu=128 RGB24, 128x14 ppu=100 RGBA32, 512x128 ppu=32 RGB24,
64x320 ppu=100 RGBA32), the seven `Art/<key>` `512x512 readable=True`, `TILE ground_cap 8x0,14m ...
colourDiffAtOneTile 0,0135 vs rowStd 0,0202 -> REPEATS`, `TILE ground_fill box=32x2m ... 0,0000 ->
REPEATS`; `art1-gate.sh` -> `ART-1A-CHECK end failures=0`, 55 part keys + 7 materials + 4 level keys +
the campfire flame core real, `flower -> MISSING (procedural)` and `UI icons -> MISSING` the only gaps.
`Tools/art0-meta-audit.py` (offline) passes all twelve shipped pictures on
`textureType 8 / spriteMode 1 / spriteMeshType 0 / wrapU-writeRepeat / fallbackPhysicsShape 0 /
isReadable` exactly where each loader needs it, and finds 318 `.meta` files with 318 distinct guids.
Nothing had to be added to the ART-0 file set.

The one thing that failed a fresh read was a PICTURE, and it was one the gates could never have
caught: the 19.09f cap had only ever reached them as a Tiled pixel test - no player build had shown
it. The first real frames of it (`art-gallery-capture.sh art0verify1909h`, buildGuid
`5a7c8f0e79854710a089ca87baf990cc`) put the strip's top edge at rows 17..19 with **std 0.34 px, 3
distinct rows over 1300 columns, tuft columns 100 %, 0 height spikes**: every tuft top sat within 1 px
of its neighbour, and at the level lens' 64.67 px/m (14 px canvas = 9 px on screen, 1 canvas px =
0.65 screen px) that averages out to a straight line. Two eye reads of that frame named the strip a
flat ribbon, and the teeth they could see sat on the SOIL line - the wrong end of the strip.

Repainted within the same canvas, PPU and `.meta` (guid `d818758150410510d3a9e73dae17dec1`, meta md5
unchanged, file 1342 -> 1519 B): `GRASS_TUFT_MIN 5 -> 3`, `GRASS_TUFT_MAX 8 -> 9` and the tuft height
field re-weighted `clump*0.50 + blade*0.50 -> clump*0.45 + blade*0.55` in
`Tools/gen-level-textures.py`. The tuft construction, the brightness fields, the de-tick guard, the
wrap-exact clump ring and the 4 root rows are untouched.

| measurement | 19.09f | 19.09h |
|---|---|---|
| canvas tip rows | 5..9, std 0.71, 5 distinct, max step 1 px | 4..9, std 1.18, 6 distinct, max step 2 px |
| same resampled to the frame's 83x9 px | tops 4..5, 2 distinct, std 0.49 | tops 3..6, 4 distinct, std 0.76 |
| frame top edge (level still) | rows 17..19, std 0.34 | rows 17..19, std 0.61 (79 % more variation) |
| generator seam / worst dark / worst bright | 1.14 vs 4.80 / 2.4 / 2.1 | 4.33 vs 6.08 / 1.2 / 2.2 |

Determinism: the rerun reproduces `md5 67f407c7122aece81fb239440709daea`, the other ten PNGs and all
eleven `.meta` files are byte-identical, and `git status` names one file
(`Assets/Resources/Level/ground_cap.png`).

**Evidence.** Real frames from a real build (`art-gallery-capture.sh art0caph`, `scenes=12 errors=0`,
8 stills, 16.5 s / 165-frame pan, `real=29 missing=11`, buildGuid `f17128ba30f2439d9915bc61a0c8ed95`):
all seven lenses framed identically to the previous capture (`ortho` 33,99 / 1,97 / 1,30 / 5,30 /
3,58 / 8,35 / 1,59) and the `dress geometry ... delta=0,0164 m` line is identical in both builds, so
nothing in the scene graph moved; every pixel that differs between the two builds lies inside a cap
strip (`level` 6161 px in y 671-677, 77358 px over the eight stills). Two blind eye reads of the two
canvases resampled to the frame's 83x9 px and tiled 4x both named the new strip the tufted one and the
old one "a flat band with 1 px teeth"; the on-ground A/B at the same camera
(`art0caph_level_640_660_ab.png`) reads "new = uneven grass, old = flat ribbon", "no ruler or comb
teeth in either", "the green/brown contact line stays ragged". Video `Trials/Runs/art0caph.mp4`
(1920x1080, 165 frames, 16.5 s). Full numbers and the loader lines quoted from the sources:
`Trials/Runs/art-0-cap-1909h-report.md`.

**Physics.** No simulation file touched: one PNG, `Tools/gen-level-textures.py` and offline probes
under `Tools/` (outside `Assets/`, not compiled). The cap is a child `SpriteRenderer` in Tiled mode
with no collider and no Rigidbody2D, and `StaticPlatform` writes only `sr.sprite`/`sr.size` on it, so
no collider, size or transform can move.

**Still open, unchanged.** `flower` (procedural; plant lane) and `UI icons` as labelled MISSING tiles;
the wide-slab fill's look (one read calls the soil "stretched with visible tiling", another calls it
painted with no hard seams - two reads disagree, so there is no measurement to justify a repaint yet);
`leaf`'s sliced vein at the wrap; the flame core's spindle read; the sign's elongated cells;
`filterMode Point` for the pixel art.


 

## Tenth pass 19.09 (artist) - the five brief items re-verified, and `leaf` stops showing a seam

Both picture gates were run again through bash on today's bytes (editor closed, no
`Temp/UnityLockfile`; the first attempt was refused with exit 4 while another lane held
`trial.lock`, and it was repeated after the lock cleared): `art0-import-gate.sh` -> `unity exit=0`,
0 `ART-IMPORT-CHECK FAIL`, the four `Level/*` through `Load<Sprite>` (1024x256 ppu=128, 128x14
ppu=100, 512x128 ppu=32, 64x320 ppu=100), the seven `Art/<key>` `512x512 readable=True`,
`TILE ground_cap ... colourDiffAtOneTile=0,0135 -> REPEATS`, `TILE ground_fill box=32x2m ...
0,0000 -> REPEATS`; `art1-gate.sh` -> `unity exit=0`, `ART-1A-CHECK end failures=0`, with
`material Art/leaf (Tiled 0.5 m)` and `Art/leaf samples finite`. `Tools/art0-meta-audit.py`:
`problems=0`, 318 `.meta` files with 318 distinct guids. Nothing had to be added to the ART-0 file
set, and the loader contract is unchanged (lines quoted in full in the ninth pass above).

The picture that failed a fresh read was `leaf`. The current built frame
(`art0caph_materials.png`) had its material swatch row read again: the `leaf` column came back as
*"a critical defect - a coarse 2x2 seam grid, the veins are cut off at the tile boundaries"*, while
dirt, grass and denim were called clean. That is not a swatch-only surface: `ArtParts` dressses the
tree crowns with it, `Tiled` at `LeafTileMeters = 0.5f`, so the seam was a line every 0.5 m on every
crown in the catalogue.

Measured on the shipped bytes: seam step 16.83 (X) / 17.07 (Y) levels against an own grain of
12.53 / 12.20, and - the metric that matters - the seam step ranked **74.8 % / 76.0 %** among every
step a 3x3 tiling shows, i.e. in the same place in every repeat. The first fix (tail faded into the
head at the same offset) moved nothing: 16.83 -> 16.18, 17.07 -> 17.26, because a B-px gap is still a
1-px jump of the same magnitude. The kept fix mirrors the head band so the fade ends exactly ON
`H[0]`: `Tools/gen-level-textures.py` `wrap_blend(out, 32)`.

| | before | after |
|---|---|---|
| seam step X / Y | 16.83 / 17.07 | 0.46 / 0.47 |
| seam rank X / Y | 74.8 % / 76.0 % | 0.9 % / 1.0 % |
| worst step inside the band vs outside | - | 96 vs 128 (X), 88 vs 119 (Y) |

`old vs new: 31155 px of 262144 (11.88 %) | outside the 32 px tail bands: 0 px` - only the two tail
bands moved, the interior is byte-identical. `md5sum` over 143 files names one changed PNG; all
eleven `.meta` files are byte-identical, so the guid `c5a18e66de5ed39bce58bb956cdd388d` did not move.
File 552.1 KB -> 548.6 KB.

**Evidence.** Real frames from a real build (`art-gallery-capture.sh art0leaf`,
`PREVIEW_BUILD_OK ... scenes=12 errors=0`, 8 stills, 165-frame / 16.5 s pan, `real=29 missing=11`,
capture failures 0, buildGuid `9345bbc560654530a3132ca50859f9ac`; video `Trials/Runs/art0leaf.mp4`).
Against `art0caph` with the same cameras (`ortho` equal to the digit): `fire` and `human`
**pixel-identical** (the two lenses that draw no `leaf` - the same-build control), `materials` 2910 px
inside one 150x150 box (the `leaf` swatch), `level` 528 px (the same swatch in the level lens),
`tree` 222916 px (the crowns), `apple` 18567 px, `photo` 4155 px; `placeholder` 60 px, maxdelta 5,
recorded as unexplained. Two blind magnified crops of the real frame (8x and 6x) named the previous
build as the one with the hard cut and this build as the one where the veins continue; the tile
tiled 12x5 at the drawn 64 px was read as *"RIGHT reads cleaner, LEFT has a regular grid of
horizontal seams"*. The 1:1 3x3 sheet was read as "identical" and is recorded that way - at 1:1 the
legacy leaf's vein node dominates the read and a 1-px step is below what it resolves.

**Physics.** One PNG, `Tools/gen-level-textures.py` (outside `Assets/`, not compiled) and two offline
tools under `Tools/`; no `.cs` touched. The material reaches the screen only through
`Albedo`/`Tiled` -> `ArtParts.Dress` -> `SpriteRenderer`, and `fire`/`human` are byte-identical
between builds. Full numbers: `Trials/Runs/art-0-leafwrap-report.md`.

**Still open, unchanged.** `flower` (procedural; plant lane) and `UI icons` as labelled MISSING tiles;
`leaf`'s tiling *rhythm* (the legacy source is one leaf close-up, so its vein node repeats every
0.5 m - `ArtParts.LeafTileMeters` is another lane's constant); `bark`/`grass`/`denim`/`dirt` keep
their wrap, measured no worse than their own grain and read clean; the wide-slab fill's look, the
flame core's spindle read, the sign's elongated cells, `filterMode Point` for the pixel art.

## Eleventh pass 19.09 (artist) - the flame core was drawn UPSIDE DOWN, and now it is a licked fire

The ART-0 brief's five items were re-verified on today's bytes (both gates re-run through bash, editor
closed, output kept): `art0-import-gate.sh` -> `unity exit=0`, `failures=0`, the four `Level/*` through
`Load<Sprite>` (1024x256 ppu=128, 128x14 ppu=100, 512x128 ppu=32, 64x320 ppu=100), the seven
`Art/<key>` `512x512 readable=True`, `TILE ground_cap ... 0,0135 vs rowStd 0,0202 -> REPEATS`,
`TILE ground_fill box=32x2m ... 0,0000 -> REPEATS`; `art1-gate.sh` -> `ART-1A-CHECK end failures=0` with
the flame assertions green (`Albedo` resolves, `81x192 readable=True`, `canvas aspect 0,422 vs box
0,421`, `Sprite.Create` succeeds). `Tools/art0-meta-audit.py`: `problems=0`, 318 `.meta` with 318
distinct guids. Nothing had to be added to the ART-0 file set, and the loader contract is unchanged
(lines quoted in full in the eighth pass above).

The picture that failed a fresh read was the campfire flame core, and the defect was not a matter of
taste: it was **drawn upside down**. The eye on today's still (`art0leaf_fire.png`) called it *"a smooth
glowing spindle / lamp, no flame tongues"*; measured on those pixels the core is 23x68 px whose width
rises to 23 px in rows 17..19 from the top and then falls to 5 px at its bottom row - the body sat at
the top and the point pointed at the ground. Cause: `flame_mask` measures `ys` as height ABOVE the
root, but a PIL array's row 0 is the TOP of the PNG, so every painted canvas was returned inverted
(only the core ships, so only the core could show it). Second, measured defect: one dome
(`peaks=1, valley=0.0 px`), no crown.

Change, in `Tools/gen-fire-textures.py` only (one PNG shipped): the field is flipped on the way out;
`CORE_BODY` + three `CORE_LICKS` (each in its own y band, at its own `x_off`, with its own lean, wobble
and dissolve, pasted through the same `flame_mask` by `lobe()`); fused with a p-norm soft union
(`SOFT_UNION_P = 8`) because a plain `np.maximum` leaves a corner where a lick leaves the body - the
first eye read named it *"rough flat cuts at the base of the side tongues"*; each lick is ramped in over
the lower third of its band, and the body's alpha ramps over `CORE_ROOT_FADE = 0.22` so its base
dissolves into the coal bed instead of ending in a chevron.

| measurement (81x192 canvas) | shipped | new |
|---|---|---|
| crown peaks / valley | 1 / 0.0 px | 2 / 32.2 px (the eye counts 3 tongues + a shoulder) |
| silhouette width, top third / bottom third | 36.9 / 18.4 px -> **UPSIDE DOWN** | 10.6 / 35.9 px -> **upright** |
| rows cut by the canvas border | 0 | 0 |
| coverage | 27.8 % | 29.7 % |
| same in a REAL frame (23x68 px -> 31x55 px), top/bottom third width | 17.5 / 11.4 px | 5.8 / 23.1 px |

**Evidence.** A real player build of the whole tree (`art-gallery-capture.sh art0flame1`,
`PREVIEW_BUILD_OK ... scenes=12 errors=0`, 8 stills, 165-frame / 16.5 s pan, `real=29 missing=11`,
capture failures 0, buildGuid `0114b556a23e4529aa49c9331438d87b`; video encoded separately with
`Tools/art0-pan-encode.py`, `art0flame1.mp4`). Against `art0leaf` the `fire` still differs in **5700 px
inside one 54x129 box** (x 933-986 y 683-811) and `human`, `level`, `materials`, `tree` are
**pixel-identical** - the same-build camera control; `apple` (1087 px) is the plant lane's rig compiled
between the two builds. Two A/B sheets rendered through the generator's own mirrored fire (identical
tongue pool, ages, flicker, logs, tint) and one real-frame 6x: the shipped core is read as *"a geometric
rhombus / crystal"* or *"a candle"*, the new one as *"an upright flame, three tongues, **Ship**"*. Both
differences are in `Trials/Runs/art-flamecore-2026-09-19/` (`art-0-flamecore-report.md` has the full
numbers). Residual recorded, not fixed: the tongues are thin and straight in a 16 cm box (no turbulence
in a still), and the coal bed/logs/sparks are the code's discs and particles, not art.

**Physics.** One PNG (`Art/Campfire/core.png`, RGBA32, readable, guid `3b4ac812...` unchanged, `.meta`
byte-identical) + `Tools/gen-fire-textures.py` + `Tools/art0-flamecore-probe.py` (both outside `Assets/`,
not compiled). The core reaches the screen through `Albedo` -> `Sprite.Create` -> `Campfire.MakeDisc`,
which writes `sr.sprite` only; box, `localScale`, tint and sorting order are the same constants, and the
campfire has no collider and no `Rigidbody2D`. No simulation file touched.

## Twelfth pass 19.09 (artist) - the finish chequer is square, and it is judged at the size the level draws

The ART-0 brief's five items were re-verified on today's bytes (both gates through bash, editor closed,
output kept): `art0-import-gate.sh` -> `unity exit=0`, `failures=0`, the four `Level/*` through
`Load<Sprite>` (1024x256 ppu=128 RGB24, 128x14 ppu=100 RGBA32, 512x128 ppu=32 RGB24, 64x320 ppu=100
RGBA32), the seven `Art/<key>` `512x512 readable=True`, `TILE ground_cap box=8x0,14m ... colourDiffAtOneTile
0,0135 -> REPEATS`, `TILE ground_fill box=32x2m ... 0,0000 -> REPEATS`; `art1-gate.sh` -> `unity exit=0`,
`ART-1A-CHECK end failures=0`, 55 part keys + 7 materials + 4 level keys + the flame core real,
`flower -> MISSING (procedural)` and `UI icons -> MISSING` the only gaps. `Tools/art0-meta-audit.py`:
`problems=0`, 318 `.meta` files with 318 distinct guids. Nothing had to be added to the ART-0 file set.

The open look item carried since the seventh pass - "the sign's elongated cells" - was closed, and
closing it moved the question to the scale the picture is judged at. `Assets/LevelTest1.cs:3059` places
the piece as `0.18 x 0.90 m` and `Assets/GameProcess.cs:939` runs the camera at ortho 4.6 (3.6 / 4.0 /
5.0 in the other branches), so at 1080p the sign is **21.1 x 105.7 px** and one 15 px cell is 5.0 x 5.0
screen px. The gallery's LEVEL row draws the sprite at its **native 0.64 x 3.2 m** (3.55x bigger) - that
is the view the elongation was ever seen in, and the label says so.

`Tools/art0-sign-cells.py` renders the shipping painter into the 0.18 x 0.90 m box at 117.39 px/m,
magnifies 4x NEAREST and writes blind two-panel sheets (`Trials/Runs/art-0-sign-cells/`, mapping in
`map.txt`). Four reads: B (15x15, 12 rows) beat the shipped 15x38 ("long vertical rectangles, 1:3-1:4,
not a chequer"); C (21x21) beat it too ("stretched along Y, an incorrect UV mapping"); D (11x11) LOST to
it ("dissolves into a grey mesh" - the too-fine end is rejected); and B beat C ("harlequin / hazard
marking"). So the repaint is "make the cells square", not "add detail".

`paint_sign_finish` defaults `cell_w=15, cell_h=38, rows=5` -> `cell_w=15, cell_h=15, rows=12`; the row
count now follows the cell size. Same canvas, PPU, palette and `.meta` (guid `b759f8b7...` did not move,
all eleven `.meta` files byte-identical). `python Tools/gen-level-textures.py sign_finish` changed exactly
one file: 959 -> 990 B, md5 `6bea0ce0...` -> `5e1b7827...`. On the shipped bytes
(`Tools/art0-sign-cells-verify.py`): **alpha byte-identical, opaque 11172 -> 11172, RGB changed 3576 of
20480 px (17.46 %)**, cell boundaries at pitch 15 px down and across the cloth (was 38 / 15), drawn cell
5.0 x 5.0 px, aspect h/w 1.00 (was 2.53).

**Evidence.** Real build (`art-gallery-capture.sh art0signc`, `scenes=12 errors=0`, 8 stills, 165 pan
frames / 16.5 s, `real=29 missing=11`, capture failures 0, buildGuid
`9b33c953507046558ae4e8d65b7ffc5f`, video `Trials/Runs/art0signc.mp4`). Against `art0flame1` (buildGuid
`0114b556...`): `fire`, `human`, `materials`, `tree` **pixel-identical** (the same-build control), `level`
1983 px differing inside x 1055-1083 y 477-589 (the cloth), `photo` and `placeholder` 231 px each at the
same cloth seen from the whole-gallery lens, `apple` 709 px (the plant lane's rig compiled between the two
builds). Blind read of the identical window in both LEVEL stills
(`frame_level_1000_430_art0flame1_vs_art0signc.png`, 3x NEAREST, mapping in `map_frame.txt`): the new
cloth is "a correct racing finish chequer, 1:1 cells, no digital noise", the shipped one "clearly
stretched along Y, cells 1:2 or worse". Full numbers: `Trials/Runs/art-0-sign-cells-report.md`.

**Physics.** One PNG + `Tools/gen-level-textures.py` + three offline probes under `Tools/` (outside
`Assets/`, never compiled). The sign is a `Decor` drawn by `StaticPlatform`, which writes `sr.sprite` on a
child `SpriteRenderer` with no collider and no `Rigidbody2D`; the piece size comes from the level data and
was not touched. No `.cs` file of the simulation is in `git status` for this pass.

**Still open.** `flower` (procedural; plant lane) and `UI icons` as labelled MISSING tiles; the LEVEL row
shows the sign at native size only (a gameplay-size instance next to it is the next step there, now that
the cells read square at every scale); `leaf`'s tiling rhythm; the wide-slab fill's look; the flame core's
spindle read and its thin straight tongues.

## Thirteenth pass 19.09 (artist) - the flame core stops being a pale carrier, and the loaders are re-proved

The ART-0 brief's five items were re-verified on today's bytes (HEAD `7dcf65e7`, editor closed): both
gates through bash, `art0-import-gate.sh` -> `unity exit=0`, `failures=0`, the four `Level/*` through
`Load<Sprite>` (1024x256 ppu=128 RGB24, 128x14 ppu=100 RGBA32, 512x128 ppu=32 RGB24, 64x320 ppu=100
RGBA32), the seven `Art/<key>` `512x512 readable=True`, `TILE ground_cap box=8x0,14m ...
colourDiffAtOneTile 0,0135 vs rowStd 0,0202 -> REPEATS`, `TILE ground_fill box=32x2m ... 0,0000 ->
REPEATS`; `art1-gate.sh` -> `unity exit=0`, `ART-1A-CHECK end failures=0`, 55 part keys + 7 materials +
4 level keys + the flame core real, `flower -> MISSING (procedural)` and `UI icons -> MISSING` the only
gaps. `Tools/art0-meta-audit.py`: `problems=0`, 12 pictures passing the meta contract, 318 `.meta` files
with 318 distinct guids. Nothing had to be added to the ART-0 file set; the loader contract is unchanged
and is quoted in full in the eighth pass above.

The picture that failed a fresh read was the campfire flame core. The read is a whole-one: the core is
drawn ALONE in the FIRE row (0.16 x 0.38 m, 40x121 px there), and the row read as *"a pale cream glowing
object - no orange or red shades at all"*, i.e. the original complaint in miniature. `paint()` had always
written RGB 255 into every canvas, so the core reached the screen as flat white modulated by
`Campfire.CoreTint` (1, 0.94, 0.72) - one pale cream root to crown. The canvas now carries the ramp:

| | shipped | this pass |
|---|---|---|
| md5 / bytes | `0e01dd29...` / 4 687 | `50c26d40...` / 11 342 |
| canvas, PPU, .meta | 81x192, 81 | unchanged; guid `3b4ac812` did not move, all 11 metas byte-identical |
| palette of the opaque px | sat mean 0.000, sat>0.40 on 0.0 %, mean rgb (255,255,255) | sat mean 0.768, sat>0.40 on 100 %, mean rgb (255,193,59) |
| ramp buckets (r-g) | white 100 % | yellow 68.5 % / amber 31.5 % |
| silhouette | valley 32.2 px, top third 10.6 px, edge rows 0 | valley 30.0 px, top third 10.5 px, edge rows 0 |
| ink in the fire row (real frame) | 40x121 px, rgb (213,202,159), sat 0.250 | 42x126 px, rgb (200,140,66), sat 0.638 |

`q = 1 - alpha**0.5 * (0.55 + 0.45 * root)` indexed into the same `flame_color` palette the procedural
tongues are tinted with, so canvas and discs cannot drift; alpha is untouched (opaque px 4 658 -> 4 654).
Also `flame_mask` gained an optional two-harmonic ripple on the half-width (`edge_amp`, zero by default,
so the rejected tongue canvases are unchanged): every flank used to be a clean arc, which a 4x read named
"mathematically smooth".

**The pass was nearly a revert, and that is the finding.** Two NAMED reads (assembled fire and 4x canvas,
LEFT shipped / RIGHT candidate) both preferred the flat white and would have thrown the ramp away. Blind
reads of the same moments in the assembly called it a tie, and a BLIND 5x window A/B of the fire row from
two real builds (`art0flame2` = flat white, `art0flame2h` = the ramp) read the ramp side as *"clearly the
more believable fire - gold -> orange repeats how wood burns"* and the shipped side as *"a cream-pale
glowing object, an overexposed silhouette that would look out of place next to painted grass, wood and
cloth"*. The mapping is in `frame_fire_blind_map.txt` and the lesson is in `Tools/gen-fire-textures.py`:
a named panel hands the reader the answer it is supposed to find.

**A tool bug fixed on the way.** `tinted()` WROTE the tint into RGB instead of MULTIPLYING it - exact
only while every canvas is flat white, which was true until today, and with a coloured canvas the A/B
sheets would have drawn both sides identically. Now `src = vertexColor * texture`, as Unity does it.

**Evidence.** `art-gallery-capture.sh art0flame2h` (`PREVIEW_BUILD_OK ... scenes=12 errors=0`, 8 stills,
165-frame / 16.5 s pan, `real=29 missing=11`, capture failures 0, buildGuid
`016bd4bffdc244139e4ce6800833a6d6`, video `Trials/Runs/art0flame2h.mp4`; the shipped canvas's md5 is
byte-identical inside that build). Against the A/B control build (`3942746521...`): `human`, `level`,
`materials`, `tree` **pixel-identical**, `fire` 4 414 px inside x 934-985 y 683-811, `photo`/
`placeholder` 17 px at x 1721-1723 y 572-577 (the same fire row from the whole-gallery lens), `apple`
1 253 px (the plant lane's rig compiled between the two builds). Against the previously shipped
`art0signc`: `fire` 4 455 px in the same box, `photo`/`placeholder` 17 px, the four control lenses
identical. Full numbers, the loader lines quoted from the sources and a caution about whole-still reads
inventing detail: `Trials/Runs/art-0-flame-1909m-report.md`.

**Physics.** Four files in `git status` for this pass - one PNG and three offline tools under `Tools/`
(outside `Assets/`, never compiled). No `.cs` touched; the core reaches the screen through
`Albedo` -> `Sprite.Create` -> `Campfire.MakeDisc`, which writes `sr.sprite` only, and the campfire has
no collider and no `Rigidbody2D`.

**Still open.** `flower` (procedural; plant lane) and `UI icons` as labelled MISSING tiles; the FIRE row
still shows the core alone rather than the assembled fire (a static stack needs the 24 tongues' geometry
in the scene, kept from drifting from `Campfire` - the next item here); the wide-slab fill's look; the
sign at native size only in the LEVEL row; `leaf`'s 0.5 m tiling rhythm; the cap/soil style mismatch.

## Fourteenth pass 19.09 (artist) - the grass cap reaches the ground surface, and the soil lip is gone

The ART-0 brief's five items were re-verified on today's bytes (editor closed, output kept): both gates
through bash, `art0-import-gate.sh` -> `unity exit=0`, `failures=0`, the four `Level/*` through
`Load<Sprite>` (1024x256 ppu=128 RGB24, 128x14 ppu=100 RGBA32, 512x128 ppu=32 RGB24, 64x320 ppu=100
RGBA32), the seven `Art/<key>` `512x512 readable=True`, `TILE ground_cap box=8x0,14m ... colourDiffAt
OneTile=0,0083 vs rowStd 0,0169 -> REPEATS`, `TILE ground_fill box=32x2m ... 0,0000 -> REPEATS`;
`art1-gate.sh` -> `ART-1A-CHECK end failures=0`, 55 part keys + 7 materials + 4 level keys + the flame
core real, `flower -> MISSING (procedural)` and `UI icons -> MISSING` the only gaps.
`Tools/art0-meta-audit.py`: `problems=0`, 318 `.meta` files with 318 distinct guids. Nothing had to be
added to the ART-0 file set; the loader contract is unchanged and is quoted in full in the eighth pass.

The picture that failed a fresh read was `ground_cap`, and the defect was geometric. `StaticPlatform
.SpawnSurfaceCap` puts the cap's box top exactly ON the ground surface and hangs the 0.14 m box INTO the
soil, so every row the canvas leaves empty at its top is a row of bare soil the player sees above the
grass. The shipped canvas left its top 4 rows empty (`tip rows 4..9`), and a real frame
(`Trials/Runs/gallery5_level.png`, level lens 64.67 px/m) showed exactly that: rows 669-672 soil, 673-677
green - **44 % of the 9 px drawn band was soil above the green**, and a blind read called it "a green band
sunk below the surface, with a layer of soil over it".

Change, in `Tools/gen-level-textures.py` only (one PNG shipped): `GRASS_TUFT_MIN/MAX` 3/9 -> 0/6 and
`paint_ground_cap` gained `tip_lo` / `tip_hi` / `min_top` (the old look is reproducible with 3, 9, 1;
`Tools/art0-cap-oldcheck.py` repaints it and prints the old md5).

| measurement (128x14 canvas, 9 drawn rows at 64.67 px/m) | shipped | this pass |
|---|---|---|
| tip rows | 4..9 std 1.18 | 1..6 std 1.18 (6 distinct) |
| soil above the green, drawn rows / share | 4 of 9 / 44 % | 2 of 9 / 22 % (and the first is a soil-grass blend) |
| green rows in the real frame | 673-677 (5) | 671-677 (7) |
| wrap seam vs own inner grain | 4.33 / 6.08 | 3.13 / 6.06 |
| worst dark / bright column stand-out (guard 3.0) | 1.2 / 2.2 | 2.4 / 1.5 |
| height spike / mean neighbour step / tuft columns | 1 px / 1.30 / 99 % | 1 px / 0.91 / 99 % |
| md5 / bytes | `67f407c7...` / 1519 B | `8d5f842e...` / 1746 B |
| `.meta` | guid `d8187581...` | byte-identical (`cmp`), guid did not move |

Candidates were painted and judged before the constant moved (`Tools/art0-cap-tips.py`, sheets and
mapping in `Trials/Runs/art-0-cap-tips/`): band 0..6 beat 3..9 in a blind 3-panel sheet ("ship") and in
the A/B against the shipped canvas; 0..9 was rejected (raggeder, worse seam 3.92 and a 2 px spike); and
band -1..6, which does put a blade on row 0, measured identically at draw scale (a single row is 0.65 px
of the 9) and was not taken. Recorded on the way: the numbers a vision read reports are not evidence -
one sheet read called two different canvases "pixel-identical" and another mis-placed the panels; only
the preference between two panels was stable, so the decision rests on that plus the measurement.

`Tools/art0-cap-canvas-diff.py`: **829 of 1792 px changed (46.26 %), rows 1..12**, row 0 untouched, the
root rows 10-13 keep their alpha and colour, alpha differs only because the silhouette moved 3 rows up.

**Evidence.** Real build (`art-gallery-capture.sh art0capb`, `PREVIEW_BUILD_OK ... scenes=12 errors=0`,
8 stills, 165-frame / 16.5 s pan, `real=29 missing=11`, capture failures 0, buildGuid
`8d0942d6007d422799c23be82687a30c`, video `Trials/Runs/art0capb.mp4`). Against `gallery5` (buildGuid
`dc09a872...`, the same tree but for this PNG and the tools), `Tools/art0-cap-frames-ab.py` shows every
still's differing rows form exactly ONE run whose height equals the cap's drawn height at that still's
own ortho - photo 2=2, human 37~38, apple 14=14, materials 21=21, level 9=9, fire 45~48, placeholder
2=2, tree 32 vs 58 (the run is clipped by the trunk, x 0-677 only); 153 638 px over the 8 stills and
nothing else on any frame moved. Two blind reads of identical windows from the two real builds
(mapping `Trials/Runs/art-0-cap-frames/map.txt`) picked this build both times - the LEVEL band ("the
grass reaches the surface line in the BOTTOM panel ... in the TOP panel there is a strip of bare soil
between the surface line and the tops of the grass") and the HUMAN close-up ("the green tufts come out
at the surface line right under the feet"; the other "looks like a layer-order mistake where the ground
hangs above the grass"). Full numbers and the quoted sources: `Trials/Runs/art-0-cap-tips-report.md`.

**Physics.** Two files in `git status` for this pass - one PNG and `Tools/gen-level-textures.py`
(outside `Assets/`, never compiled), plus the new offline probes under `Tools/`. No `.cs` touched; the
cap is a child `SpriteRenderer` with no collider and no `Rigidbody2D`, and its box (`size.x` x 0.14),
`localPosition`, tint and sorting order are constants in `StaticPlatform` that were not edited.

**Still open.** `flower` (procedural; plant lane) and `UI icons` as labelled MISSING tiles; the FIRE row
still shows the core alone rather than the assembled fire (the next item here); the wide-slab fill's
look; the sign at native size only in the LEVEL row; `leaf`'s 0.5 m tiling rhythm; `filterMode Point`
for the pixel art. **Closed by this pass: the cap/soil style mismatch** - it was the empty top of the
cap canvas, measured 44 % -> 22 % and judged blind on real frames.

## Fifteenth pass 19.09 (artist) - the gallery's FIRE row shows the assembled fire

The next item on this file's own list, and the one that had been waiting longest: the row drew the
painted core alone, on the ground line, and the yard draws three log boxes, an ember disc, 24 pooled
tongue discs and the core, at heights measured from the fire's own root. It now draws exactly that,
from `Campfire.FrozenStack()` - the pool's geometry, its sorting bands and its pose function, so the
catalogue cannot drift from the fire it catalogues.

To make that possible and safe, the geometry and the pose were **moved**, not copied:
`BuildTongues` and `TickTongues` now call `PoolRow` and `TonguePicture`, `BuildLogs` calls `LogRow`,
and the frozen stack calls the same three. `Tools/art0-fire-stack-check.py` is the guard: the
multiset of float literals of the whole file differs from the previous version by exactly the two
new frozen-stack constants (0.70f, 0.50f), nothing removed - so the move changed no band, no heat, no
life, no lean, no flicker weight, no log box, and `FrozenStack` retypes no literal at all.

The first build was rejected on its own still: the label ran over the logs, the ember and the flame
and was clipped by the frame's right edge (diff box reaching x = 1919). Third iteration measured
instead of judged - `Tools/art0-fire-label-gap.py` reports a 1 px (0.003 m) gap at 1.05 m and a
109 px (0.321 m) gap at 1.45 m, with no label ink on any frame edge; the blind read of that pair
(seed 7, map read after the pick) chose the 1.45 m one, which is what shipped (`art0firestack3`,
buildGuid `6e2836b71a1640a7aa2804c15f5b218c`). A second blind read, of the ground-line label against
the 1.05 m one, is kept in `Trials/Runs/art0stack-label-blind-pick.txt` **as a rejected read**: its
crop window excluded the ground-line label, so the panel whose label was out of frame looked like a
panel without one. Third lesson of this lane, same family as the first two: the frame decides, and a
window is only as good as its window.

Evidence: 8 stills + `art0firestack3.mp4` (165 frames, 16.5 s, 1080p) from one build
(`PREVIEW_BUILD_OK scenes=12 errors=0`, 0 capture failures, real=29 missing=11 unchanged). Against
the previous build: `human`, `level`, `materials`, `tree` pixel-identical, `fire` 67 887 px (the
stack + label), `photo`/`placeholder` 380 px (the label through the whole-strip lens), `apple`
235 px - the plant lane's berry placement, proved non-deterministic across two builds of the same
source (895 px between `art0firestack` and `art0firestack2`, which differ only in the label's
height). Both gates re-run after the last edit: `art0-import-gate.sh` unity exit=0, 0 failures, the
four `Level/*` through `Load<Sprite>` and the seven `Art/*` 512x512 readable, both tiles REPEAT;
`art1-gate.sh` `ART-1A-CHECK end failures=0`. Report `Trials/Runs/art-0-fire-stack-1909-report.md`.

**Physics.** Two compiled files (`Campfire.cs`, `GalleryScene.cs`) and four offline tools under
`Tools/`. Rule zero by construction, and the construction is quoted in the report: the trial player
is SampleScene only (`Assets/Editor/HeadlessBuild.cs:15`) and SampleScene contains **0** occurrences
of `Campfire` - the yard plants it at runtime (`LevelGenerator.cs:165`) and the fire lab through
`GameProcess.CreateCampfire` ("Scene Campfire only"), while `GalleryScene` exists only in the
gallery scene, which is in the preview player. The fire has no collider and no `Rigidbody2D`.
`Build/Headless` was neither rebuilt nor re-run.

**Still open.** `flower` (procedural; plant lane) and `UI icons` as labelled MISSING tiles; the
wide-slab fill's look; the sign at native size only in the LEVEL row; `leaf`'s 0.5 m tiling rhythm;
`filterMode Point` for the pixel art; sparks/smoke/embers and the night glow in the FIRE row (all
time-driven - they would make a catalogue frame unrepeatable). **Closed by this pass: the FIRE row
showing the core alone.**

## Sixteenth pass 19.09 (artist) - the leaf material is painted as a foliage field

Both picture gates were run again through bash on today's bytes (editor closed, no
`Temp/UnityLockfile`): `art0-import-gate.sh` -> `unity exit=0`, 0 `ART-IMPORT-CHECK FAIL`, the four
`Level/*` through `Load<Sprite>` (1024x256 ppu=128, 128x14 ppu=100, 512x128 ppu=32, 64x320 ppu=100),
`Art/leaf tex=leaf 512x512 readable=True format=RGB24`, the seven `Art/<key>` `512x512 readable=True`,
`TILE ground_cap ... 0,0083 -> REPEATS`, `TILE ground_fill box=32x2m ... 0,0000 -> REPEATS`;
`art1-gate.sh` -> `ART-1A-CHECK end failures=0` (55 part keys + 7 materials + 4 level keys). The five
brief items are re-proved on today's bytes and nothing had to be added to the ART-0 file set.

The next item on this file's own list, and the last one in the material set: **`leaf`'s 0.5 m tiling
rhythm**. `ArtParts.DressAll` dresses every crown with `ArtLibrary.Leaf` through `Tiled(key,
LeafTileMeters = 0.5f)`, and the legacy tile is a macro photograph of ONE leaf - its vein fan starts
at one node near an edge. The tenth pass took the seam out of it; what the eye then found was the
thing that was left: *"the same contours repeat, a regular carpet"* (read of `gallery6_tree.png`). No
blend and no downscale can fix that, because the picture contains one leaf.

`Assets/Resources/Art/leaf.png` is now **painted** by `Tools/gen-level-textures.py`
(`PAINTED_MATERIALS = {"leaf": paint_leaf}`; `bark`, `dirt`, `grass`, `denim`, `fabric`, `skin` stay
on the legacy path): 44 leaves of 0.033-0.127 m at the tile's 0.5 m, directions dealt one per leaf
from a full circle, a four-step depth ladder with per-leaf tone jitter, a per-leaf shadow direction
and alpha, and rim/vein lines at 110-150 of 255 alpha instead of 200-215. Every leaf is composited at
NINE positions of the tile lattice on a 2x canvas, so the tile is seamless **by construction** and
`WRAP_BLEND` is empty (a mirrored band would smear a composition that has no cut). `wrap_blend` is
what the tenth pass needed and what this pass retires for this key; the reason is written next to the
now-empty dict.

**The two traps this lane has already paid for, and how they were avoided.** (1) A blind read of the
first painting caught every leaf lying near-horizontal - "a woven mat" - because the directions were
drawn independently; they are now dealt one per leaf. (2) LANCZOS at the edge of an isolated crop
clamps to the edge pixel and left a 1.0-1.6 level step exactly AT the wrap; the 2:1 downscale is now a
plain 2x2 box average, which never reaches outside the tile. The tile is one window of the periodic
canvas, and the window start is the pair at the tile's **median** neighbour busyness - **not** the
quietest pair: the legacy tile scored 0.0 % of its own 511 edge pairs (the mirrored band had pinned
its wrap to the flattest pair in the picture), the painted one sits at 44.4 % / 50.3 %.

| | legacy photo | painted field |
|---|---|---|
| mean rgb | 66.7, 135.9, 3.9 | 66.8, 135.8, 4.5 |
| wrap step X / Y (levels, % of range) | 0.46 / 0.47 (0.18 / 0.19 %) | 1.06 / 1.10 (0.41 / 0.43 %) |
| wrap vs tile's own edge pairs | 0.0 % of 511 | 44.4 % / 50.3 % (median) |
| wrap / mean interior grain | 0.04 | 0.88 / 0.93 |
| rank max in a 3x3 repeat | 0.9 % | 88.0 % |
| distinct colours | 40 602 | 3 260 |
| file | 561 731 B | 161 855 B |

The 3x3 `rank max` is a percentile over every single pixel step, and a painted tile's interior is
nearly flat (grain 1.20 against the photo's 12.25), so a sub-level wrap step lands high in a
distribution of near-zero steps. **Read it with the grain or it inverts the truth**; the statistic
that answers "is the wrap special?" is the wrap against the tile's own edge pairs, which is now
printed as its own line by `Tools/art0-leaf-field-probe.py`. The palette is anchored on the tile it
replaces (mean rgb above, blue 4/255 kept) so the crowns keep the colour the tree lane and every
review knows.

**Evidence.** One build, one run, bash only (`art-gallery-capture.sh art0leaffield`):
`PREVIEW_BUILD_OK ... scenes=12 errors=0`, 8 stills + 165-frame / 16.5 s 1080p pan, 0 capture
failures, `real=29 missing=11 dressed=5567`, buildGuid `b2c011858b104923bc82a57262ea59f8`;
`Tools/gallery-refresh-check.py art0leaffield` -> `OK stills_bad=0 fields_moved=6`, row boxes 23 -> 23.
Against `gallery6` (the same player, leaf at `HEAD`): `fire` and `human` **pixel-identical** (the two
lenses that draw no `leaf` - the control), `tree` 751 665 px (the crowns), `photo` 10 179, `apple`
80 913, `materials` 22 391 px inside one 150x150 box (the `leaf` swatch), `level` 4 091 px inside
64x64 (the same swatch), `placeholder` 251 px. **Three blind reads** (`Tools/art0-leaf-frames-ab.py`,
zoom centred on the pixels that MOVED, mapping in `map.txt`, read after the pick) chose the painted
build each time, and the third was run with a seed that puts it on the RIGHT, so the verdict is not a
left-side bias. Two reads also flagged the crown GEOMETRY's cut planes - recorded for the plant lane
(`TREES-plan.md`), not acted on here. Full numbers: `Trials/Runs/art-0-leaf-field-report.md`.

**Physics.** One PNG, `Tools/gen-level-textures.py` (outside `Assets/`, never compiled) and three
offline tools under `Tools/`; no `.cs` under `Assets/` touched. The material reaches the screen only
through `Albedo` -> `Tiled` -> `ArtParts.Dress` -> `SpriteRenderer`, `RealArtEnabled` defaults to
false, and `fire`/`human` are byte-identical across two players and three other lanes' `.cs` edits.
`Build/Headless` was neither rebuilt nor re-run.

## Seventeenth pass 19.09 late (artist) - the five brief items re-proved, and the two tiles that still read a joint are closed

Editor closed, bash only, HEAD `8461733e`. Both picture gates were re-run on today's bytes before
anything was changed: `art0-import-gate.sh` -> `unity exit=0`, 0 `ART-IMPORT-CHECK FAIL`, the four
`Level/*` through `Load<Sprite>` (1024x256 ppu=128, 128x14 ppu=100, 512x128 ppu=32, 64x320 ppu=100),
the seven `Art/<key>` `512x512 readable=True`, `TILE ground_cap ... 0,0083 -> REPEATS`,
`TILE ground_fill box=32x2m ... 0,0000 -> REPEATS`; `art1-gate.sh` -> `ART-1A-CHECK end failures=0`,
`55 part keys checked=0 missing` + the 7 materials + the 4 level keys + the flame core, with `flower`
and `UI icons` the only two labelled gaps. Offline: `art0-meta-audit.py` -> `problems=0`, 318 `.meta`
files / 318 distinct guids; the materials are 1024x1024 originals -> 512x512 shipped, 17.41 MB -> 3.64
MB. Nothing was added or moved; the five brief items are re-proved on today's bytes.

**What was left open and is now measured.** The card's item 5 split the material set into
skin/fabric (ramp correction), leaf (painted) and "bark, denim, dirt, grass - a cut no worse than
their own grain by the numbers". That last clause was a ratio argument with no eye read behind it
since every fix landed, so all seven shipped tiles were repeated as 2 x 2 blocks at NATIVE size
(1024 px/m: a 512 tile covers 0.5 m; the oak close-up lens is 414 px/m, so native is the generous
case) in a shuffled order, one image per block, `map.txt` opened after the pick
(`Tools/art0-wrap-panels-17th.py`, `Trials/Runs/art-0-wrap-17th/`). The read named exactly two panels
as showing a joint - the vertical + horizontal line through the middle - and the map says `skin` and
`fabric`; `bark` ("единый непрерывный материал, видимых линий стыков нет"), `denim`, `dirt`, `grass`
and `leaf` all came back as one continuous material. So `bark` is closed by a read, not by a ratio,
and it was the last item item 5 was holding open.

**Why the earlier fix could not close skin/fabric.** `level_tile` removes a brightness RAMP and these
two have almost none left (skin 3.71 / 0.37 levels, fabric 0.01 / 0.01); what remains is DETAIL
(skin X 3.62 / Y 5.34 against grain 4.01 / 4.54, fabric 4.90 / 5.11 against 2.72 / 2.98), and the
generator's own threshold (`cut > 2x inner`) is what kept it out of reach. Full split for all seven
tiles in `Trials/Runs/art-0-wrap-17th-report.md` section 4. The rule the numbers give: on a NOISY tile
a coherent step of 1.4x the grain is invisible (bark 27.36 vs 19.03 - and the read agrees), on a
SMOOTH tile even ~1.2x reads as a drawn line, because a line is coherent and noise is not (grass has
88.7 % of its boundary pixels stepping more than 12 levels and reads seamless).

**The change.** One line in `Tools/gen-level-textures.py`: `WRAP_BLEND = {"skin": 32, "fabric": 32}`
(was empty since the leaf pass). The mirrored head band makes the junction step ~3 % of itself and
approaches it linearly over 32 px = 3 cm of the 0.5 m tile; nothing is resampled or relocated and the
band's own worst step stays below the step outside it (skin 23 vs 30 / 26 vs 31, fabric 13 vs 17 / 17
vs 19). After: skin cut X 5.90 -> **0.71** (grain 4.01), Y 5.60 -> **0.48**; fabric X 4.91 -> **0.45**
(grain 2.72), Y 4.96 -> **0.46**.

**Acceptance = the pairwise blind read**, the form this lane trusts (absolute verdicts are unstable;
recorded twice before). One image per material, HEAD bytes and this pass's bytes side by side, which
side is which random and withheld until after the pick (`Tools/art0-wrap-pair-17th.py`,
`Trials/Runs/art-0-wrap-17th-pair/`): `skin` HEAD=RIGHT/NEW=LEFT - "RIGHT ... резкая, хорошо
различимая вертикальная линия стыка ... перепад яркости", "LEFT ... стык практически незаметен ...
единое сплошное полотно" -> NEW; `fabric` HEAD=LEFT/NEW=RIGHT - "LEFT ... очень чёткий, резкий шов
... складки резко обрываются", "RIGHT ... вертикальный шов практически невидим, складки плавно
перетекают из одного блока в другой" -> NEW. The picks landed on OPPOSITE sides, so neither is a side
bias. A first, absolute seven-panel re-read was kept as the unstable form it is: it rated the new
`fabric` panel by the CONTENT of a 2 x 2 repeat ("стопроцентное повторение идентичных волн и складок в
каждом квадранте"), which no texture can hide.

**Determinism.** `python Tools/gen-level-textures.py skin fabric` rewrote exactly two files
(`Tools/art0-byte-snapshot-17th.py before|after`): `skin.png` 417537 -> 413751 B (md5
`36d51e73...` -> `a5f78f7e...`), `fabric.png` 342679 -> 340538 B (`52b47487...` -> `87706428...`).
The other nine pictures and all eleven `.meta` files are byte-identical - the guids did not move.
Both gates re-run after the last edit: `art0-import-gate.sh` unity exit=0, failures=0, `Art/skin` and
`Art/fabric` `512x512 readable=True`; `art1-gate.sh` `failures=0` with `Art/skin (Tiled 0.5 m)` and
`Art/fabric (Tiled 0.5 m)` OK.

**The frame.** One build, one run, bash only (`art-gallery-capture.sh art0wrap17`):
`PREVIEW_BUILD_OK ... scenes=12 errors=0`, 8 stills + 165-frame / 16.5 s 1080p pan, 0 capture
failures, `real=29 missing=11 dressed=5567`, buildGuid `b73a4a9f44784f73af3dd705427723b7`. Against
`art0leaffield` (`buildGuid b2c011858b104923bc82a57262ea59f8`, same 8 lenses): `fire`, `human` and
`tree` **pixel-identical** (the three controls), `materials` 5157 px in one box x 281-634 y 480-630,
`level` 942 px x 1264-1415 y 605-668, `photo`/`placeholder` 71 px each x 460-496 y 562-577 - and that
arithmetic names the cells: the materials row is `GalleryScene.cs:70-74` at 1.35 m pitch from x = 20,
1 x 1 m swatches `Tiled(key, 0.5f)`, so `skin` is 19.5-20.5 m and `fabric` 20.85-21.85 m = frame
x 281-634 at that still's 150.94 px/m (and x 460-493 at the wide lenses' 15.89 px/m). Nothing else in
any frame moved. `apple` 540 px is NOT this change (skin/fabric have one caller each, the swatch row,
and that lens does not show the materials row): it is the plant lane's berry-placement wobble across
two builds, already recorded in the 15th pass (895 px between two builds of the same source).

**Physics.** No `.cs` under `Assets/` touched: `Tools/gen-level-textures.py` is outside `Assets/` and
never compiled, and the two PNGs reach the screen only through `Albedo` -> `Tiled` -> `ArtParts.Dress`
-> `SpriteRenderer` with `RealArtEnabled` false by default. `ArtLibrary.Skin` / `ArtLibrary.Fabric`
have exactly one caller each (`GalleryScene.cs:70-74`, the material swatch row) plus the editor
checks. `Build/Headless` was neither rebuilt nor re-run.

**Still open.** `flower` and `UI icons` as labelled MISSING tiles; the wide-slab fill's look
(`LevelArt.UseTiledFill()` + `ArtLibrary.Tiled` is the declared path); the sign at native size only in
the LEVEL row; `filterMode Point` for the pixel art; sparks/smoke/embers and the night glow in the FIRE
row; the crowns' cut planes (plant lane). One measurement could not be made from the frame and is
recorded as such: `Tools/art0-bark-rhythm-probe.py` found no repeat signature at the 0.5 m tile in
`art0leaffield_tree.png` (MAD 34.1 at 207.10 px against 32-35 over the whole 180..240 px control range)
because that still's lower-middle region carries the crown's cut planes over the trunk - so the bark
verdict rests on the panel read above, not on that frame.



