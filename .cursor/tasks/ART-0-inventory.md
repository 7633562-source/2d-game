# ART-0 - inventory: what real art exists, what loads, what is missing

Recon by Hermes, 18.09.2026. Facts only, each one verifiable by the command in brackets.

## The complaint (Yury)
"On every scene the textures are transparent rectangles matching the box colliders; only in one scene
(create it) should all objects appear with the real textures that will be in the game."

## Finding 1 - the real art EXISTS. 62 PNGs in `Assets/LegacyArt/`
[`find Assets/LegacyArt -name '*.png'`]

| group | files | notes |
|---|---|---|
| Human | foot, hand, head, lower, neck, pelvis, shin, thigh, torso, upper (10) | the whole rig, part by part |
| Dog | chest, front_lower, front_upper, head, neck, paw, pelvis, shin, tail, thigh (10) | |
| Crow | beak, body, foot, head, neck, shank, tail, thigh, wing (9) | |
| Chicken | beak, body, foot, head, neck, shank, tail, thigh, wing (9) | |
| Hedgehog | chest, front_lower, front_upper, head, jaw, neck, paw, pelvis, quill, shin, tail, thigh (12) | |
| Materials | bark, denim, dirt, fabric, grass, leaf, skin (7) | seamless/tiled surfaces |
| Apple | fruit, leaf (2) | |
| Strawberry | berry, leaf, stem (3) | |
| GeneratedArt | grok-bot/bark-1.png, bark-2.png (2) | |

All of them are imported as **Sprites** (`textureType: 8`, `spriteMode: 2`, `spritePixelsToUnits: 100`)
- see `Assets/LegacyArt/Human/head.png.meta`.

## Finding 2 - NOTHING loads them, and that is why everything is a rectangle
- `HumanSegment.cs` draws a code-generated 8x8 sprite (`_placeholderSprite`, line 235-248) sized to the
  collider. `HeadlessTrial.cs:98` says it plainly: "HumanSegment uses collider-sized sprites instead of
  1 m placeholders."
- `LevelArt.Load(key)` searches **only** `Resources/Level/<key>` and `Resources/Art/<key>`
  (`PathsFor`, then `LoadAt`). `Assets/Resources/` contains **one file** - `Level/README.txt`.
- `ArtLibrary.Albedo(key)` uses the same material keys (skin, fabric, denim, dirt, grass, bark, leaf).
- So both loaders look in a folder that has never had any art in it, while 62 usable PNGs sit next door
  in `LegacyArt/` (which is NOT a Resources folder, so `Resources.Load` cannot see it).

## Finding 3 - four level textures were requested and never delivered
`Assets/Resources/Level/README.txt` says, verbatim:
"Put PNGs from the brief Tools/visul-test1-textures.md here. Do not change file names: ground_fill,
ground_cap, sky, sign_finish"
Those four files do not exist anywhere in the project. That is the missing ground surface, ground cap,
sky and finish sign.

## Finding 4 - the resolution problem
The material PNGs are 2-3 MB each (bark 2871 KB, grass 2920 KB, dirt 2659 KB, skin 2004 KB, denim
3096 KB). For tiled materials at this scale they are almost certainly 2048x2048 or larger. Putting all
of them into `Resources/` unmodified would bloat the build for no visual gain. Tiled surfaces should be
downscaled (512x512 is plenty for grass/dirt/bark at this camera scale) - the artist stream owns that.

## Gap list (what has no art at all) - status at the 22nd pass, 20.09
0. Materials on the small parts - CLOSED (`Trials/Runs/art-0-period-22nd-report.md`): the 21st pass'
   open item 1. `ArtParts.Dress` now draws a MATERIAL (bark, leaf) `Tiled` at its authored 0.5 m period
   whatever the part's size, so a limb or a leaf pad shows a CROP of the canvas at the canvas' own
   scale instead of the whole 0.5 m sample squeezed into the part (5.00x on a 0.10 m limb, 4.17x on a
   0.12 m leaf pad). Painted parts (human, flower, campfire logs) keep `Sliced`.
1. `ground_fill`, `ground_cap`, `sky`, `sign_finish` - DELIVERED (`Trials/Runs/art-0-fill-18th-report.md`,
   `art-0-sign-report.md`, `art-0-cap-*.md`).
2. Campfire (flame/embers) - the three logs DELIVERED (`art-0-logs-19th-report.md`), the flame core
   painted (`art-0-flame-1909m-report.md`); sparks/smoke/embers stay procedural by decision.
3. Flowers (`PlantFlower`) - DELIVERED (`art-0-flower-20th-report.md`: `petal`, `petal_tip`, `cup`,
   `calyx`, `disk` in `Art/Flower/`). Gap 3b, the FOLIAGE - DELIVERED (`art-0-leaflet-23rd-report.md`:
   `Art/Flower/leaf.png` + `leaf_erect.png`, one painted leaf-pair canvas per leaflet family, the rig's
   16-card collage stepping aside; the family split 1.667-1.944 against 0.333-0.571 with
   `ArtParts.LeafFamilyWide` 0.8, worst per-kind aspect error 1.364x on Iris). Gap 3b CLOSED
   (`art-0-spike-24th-report.md`): the lavender spike (`Spike_i`) and the autumn hip (`Flower/hip.png`)
   are painted canvases now (`Art/Flower/spike.png` + `Art/Flower/hip.png`, one canvas taking the part's
   own box, the rig's cards stepping aside through `ArtParts.DressFlowerHeads`). The flower STEM is
   painted too (`art-0-stem-25th-report.md`): `Art/Flower/stem.png` + `stem_stout.png`, chosen by the
   drawn box aspect through `ArtParts.StemKeyFor` / `StemFamilyStout` 0.112, the rig's `Cone*` cards
   stepping aside, the winter `Frost*` / `WinterBud*` chips staying on the cane; worst per-kind aspect
   error 1.52x on Daffodil, 1.23x on the corm pair. **Gap 3b is now CLOSED**: every part of the flower
   rig carries a painted canvas. Left in the set: the knot pattern is one canvas for all ten stalk
   kinds, the hip canvas' aspect error on the tall kinds (1.35x Poppy / 1.37x Bluebell and Crocus), the
   corm canvas has gate evidence but no frame read yet, and the level's / yard's DECOR flowers
   (`YardAmbient.AddHousePart` builds its own petals) are flat cards of their own, not this rig.
4. Any Human clothing layer if the game shows one (skin/fabric/denim exist as materials but no cut
   garment sprites). OPEN.
5. UI/HUD icons - out of scope for this scene unless Yury asks. OPEN.

## Objects that must appear in the gallery scene
Human (all variants we can pose), dog, crow, chicken, hedgehog, oak/tree kinds, apple tree (+fruit),
strawberry, flower, campfire, ground + sky + sign, day/night sky, NPC, prey animal. Each at real size,
labelled, with a scale bar.

## Finding 5 - the placeholder look is BY DESIGN, and the pipeline step is missing
`.cursor/rules/painted-parts.mdc`: "The sprite is a transparent rectangle equal to the box collider.
`HumanSegment` sets `sprite.size = collider.size` and stretches the full PNG onto that box (Sliced)."
`.cursor/agents/artist.md`: "Assets/LegacyArt (**do not bundle**)".
So LegacyArt is the INCOMING art folder, deliberately outside the build, and the step that would turn it
into bundled per-part sprites (split sheet -> crops -> `Resources/Art/...`) was never finished. That is
why every scene shows transparent rectangles: the rectangles are the contract, the pixels behind them
never arrived. The gallery task therefore includes completing that pipeline step, not just building a
scene.
