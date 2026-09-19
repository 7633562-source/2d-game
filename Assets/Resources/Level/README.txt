Put PNGs from the brief Tools/visul-test1-textures.md here
Do not change file names: ground_fill, ground_cap, sky, sign_finish

Status 18.09.2026 (ART-0): all four are here. They are painted by
Tools/gen-level-textures.py (run it from the project root; it also writes the .meta files).

  ground_fill  1024x256  PPU 128  native 8x2 m   soil side view, seamless in X
  ground_cap    128x14   PPU 100  native 1.28x0.14 m  grass strip, tiles on the 0.14 m cap
  sky           512x128  PPU 32   colour ramp to horizon, drawn Sliced over 120x24 m
  sign_finish    64x320  PPU 100  finish pennant on a post, canvas 1:5 like the 0.18x0.90 m box

LevelArt reads Resources/Level/ground_fill, ground_cap, sky and sign_finish as Sprites
(spriteMode Single, mesh Full Rect, wrap Repeat). The drawing itself is not physics:
no collider, friction or size depends on these files.

Verification 19.09.2026 (ART-0 re-run, editor closed, bash only):
  "F:/6000.5.8f1/Editor/Unity.exe" -batchmode -projectPath "E:/Games/2D Game/My project" \
    -executeMethod ArtImportCheck.VerifyArt -logFile %LOCALAPPDATA%/Temp/grok-burn/art-import-check.log -quit
  -> exit 0, ART-IMPORT-CHECK end failures=0. All four keys load through
  Resources.Load<Sprite> (ground_fill 1024x256 ppu 128 = 8x2 m, ground_cap 128x14 ppu 100 =
  1.28x0.14 m, sky 512x128 ppu 32 = 16x4 m, sign_finish 64x320 ppu 100 = 0.64x3.2 m), and the
  Art/ keys skin fabric denim dirt grass bark leaf load as readable 512x512 textures.
  Tiled draw mode really repeats: ground_cap colourDiffAtOneTile 0.0030, ground_fill 0.0000.
  Wrap check on the committed bytes: every material tile's cut is within 1.81x its own grain,
  so no tile shows a visible step at the repeat.

Status 19.09.2026 late (ART-0, sixteenth pass): `leaf` is no longer a downscaled legacy photo.
The legacy leaf is a macro photograph of ONE leaf, so every crown drew the same vein node every
`ArtParts.LeafTileMeters` = 0.5 m. `Tools/gen-level-textures.py` now PAINTS it
(`PAINTED_MATERIALS = {"leaf": paint_leaf}`): 44 overlapping leaves of 0.033-0.127 m at that tile,
composited at the nine positions of the tile lattice, so it is seamless by construction and
`WRAP_BLEND` is empty. Mean colour kept (66.7, 135.9, 3.9 -> 66.8, 135.8, 4.5), guid and every
`.meta` field unchanged, 561 731 B -> 161 855 B. Measured wrap step 1.06/1.10 levels, at the median
of the tile's own 511 edge pairs; frames `art0leaffield` (buildGuid `b2c011858b104923bc82a57262ea59f8`),
report `Trials/Runs/art-0-leaf-field-report.md`.

Status 20.09.2026 (ART-0, eighteenth pass): `ground_fill` - the stone layer stops making the 8 m
repeat visible. The fill is drawn `Tiled` on every slab wider than one tile (LevelGenerator.chunkWidth
= 16 m -> the same pixels twice; the 32 m "Behind" plate -> four times), and the tile's own strongest
blobs stood 2.78-3.20x above its 13.1 level grain - every one of them a LIGHT stone (core
PEBBLE_LIGHT mixed at 0.85). Two independent eye reads of the tile repeated at 1:1 named the handles
and nothing else: a PAIR of light pebbles and two PAIRS of dark dots; no lone stone was named. So the
repaint changes the stone layer only: `FILL_STONE_CORE` 0.60 / `FILL_STONE_CORE_DARK` 0.72 (were
0.85), a weaker lit edge, `subtle` 0.22 -> 0.30, and `stone_paired()` refuses to place a strong stone
beside another strong one (gap 10 px). Strata, grain, roots, cracks, stone count and the mean soil
colour are untouched - mean rgb 93.1 67.3 36.5 on both sides, grain 13.14 -> 13.11, 447 090 ->
446 743 B, one file rewritten and eleven pictures + twelve `.meta` byte-identical
(`python Tools/art0-byte-snapshot-17th.py before18|after18`). Strongest blob 2.78x -> 2.19x grain and
0 blobs at >= 3x (was 4); the 32 m plate's max residual 7.13x -> 5.22x grain
(`python Tools/art0-fill-anchor-probe.py`). Blind pairwise reads (`Tools/art0-fill-strip-ab.py`, sides
shuffled, `map.txt` opened after each pick): no read ever named the new bytes, two of three on the
1:1 panel named the HEAD bytes, and the side-flipped sheet (seed 11, NEW on the RIGHT) picked
LEFT = HEAD, so the verdict is not a side bias; both control pairs (HEAD against HEAD) read "no
difference". Frames `art0fill18`, report `Trials/Runs/art-0-fill-18th-report.md`.
