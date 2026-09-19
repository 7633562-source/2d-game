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
