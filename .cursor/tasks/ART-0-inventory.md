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
   rig carries a painted canvas. The hip is THREE canvases since the 26th pass
   (`art-0-hip-26th-report.md`): `Art/Flower/hip.png` (round family), `hip_tall.png`
   (Tulip/Lily/Crocus/Bluebell), `hip_wide.png` (Poppy), chosen by the drawn box aspect through
   `ArtParts.HipKeyFor` / `HipFamilyTall` 0.906 / `HipFamilyWide` 1.268, and each canvas PAINTS its
   form pre-squashed for its own family (`gen-flower-spike-textures.py`'s `hip_form_scale` 1.000 /
   1.457 / 0.764 + `fitted_map`), so the world form the eye gets is the round hip's up to the family's
   own box spread: worst per-kind error 1.536x -> 1.116x (tall), 1.320x -> 1.010x (Poppy), the round
   family byte-identical at 1.077x. Left in the set: the knot pattern is one canvas for all ten stalk
   kinds, the round family's own 1.077x spread (Daisy/Sunflower, untouched by design - that canvas is
   the accepted reference), the corm canvas has gate evidence but no frame read yet, and the level's /
   yard's DECOR flowers (`YardAmbient.AddHousePart` builds its own petals) are flat cards of their
   own, not this rig.
4. Any Human clothing layer if the game shows one (skin/fabric/denim exist as materials but no cut
   garment sprites). OPEN.
5. UI/HUD icons - out of scope for this scene unless Yury asks. OPEN.
6. The human's `hand` canvas - DELIVERED (`Trials/Runs/art-0-hand-27th-report.md`): measured with
   `Tools/art0-part-ink-probe.py` (shipped bytes only), the hand filled **33 %** of its own
   `0.06 x 0.16 m` bone - the only part of the body under half - and its ink was centred, so the
   wrist cut sat **0.052 m below** the bone's proximal edge. `Tools/gen-human-hand-texture.py` now
   rewrites that one canvas from the hand's own accepted pixels (`LegacyArt/Human/hand.png`):
   `31,165 119x171 px / 0.037 x 0.053 m / wrist margin 0.052 m / 31 832 B` ->
   `6,0 180x261 px / 0.056 x 0.082 m / wrist margin 0.000 m / 63 865 B` (94 % of the box width is
   the proportional maximum: the art's aspect 0.70 against the box' 0.375). Frames `art0hand27` vs
   `art0hip29` (human still: 369 px in ONE cluster, the hand's box x 1067-1082 y 661-689; every
   other still identical or inside its own measured noise), 3 blind pairwise reads (one with the
   sides swapped) all correct. Still OPEN in this family: the assembled rig with real art has never
   been drawn (the `human rig` row is ART-1b, the walk stream's `HumanSegment`), the hand art itself
   is a PALM view on a side-view body (a procedural side-view redraw was attempted twice and
   rejected by the read - recorded in the generator), and the other nine parts' ink anchoring
   (torso 0.136 of 0.240 m in x, the limbs centred with 0.047-0.069 m of empty canvas at each end of
   the bone) is a rig-wide decision, not a hand fix. **Measured and REFUSED in the 28th pass**
   (`Trials/Runs/art-0-leaf-cover-28th-report.md` §5.2): the four limb inks are already at their
   maximum size (96-99 % of the box width, the bone's length unreachable without distortion), so the
   only freedom is which joint carries the slack - proximal anchoring doubles the wrist hole
   (0.069 -> 0.137 m) and worsens the ankle, i.e. it moves the same hole. Do not re-open it without a
   new instrument: the present centred placement is the minimax one.

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

## Update 20.09.2026 - what the pictures are NOW (ART-0 29th pass)

The pipeline step above is done and the loaders now find real pixels. Shipped, all under
`Assets/Resources/` and all read back off their own `.meta` by `Tools/art0-meta-audit.py`:

| key | file | what it is |
|---|---|---|
| `Art/Human/*` (10) | leg/foot crops of `LegacyArt/Human/*` | the body's parts, one PNG per bone |
| `Art/Dog/*` (10), `Art/Crow/*` (9) | crops of the legacy sheets | the animals' parts |
| `Art/skin`, `fabric`, `denim`, `dirt`, `grass`, `bark`, `leaf` | downscaled legacy materials | the seven tiled surfaces |
| `Art/Apple/{fruit,leaf}`, `Art/Strawberry/{berry,leaf,stem}` | legacy + painted leaves | the fruit |
| `Art/Campfire/{core,log_a,log_b,log_c}` | painted | the fire |
| `Art/Flower/*` | painted, luminance in alpha | the flower's foliage |
| `Level/{ground_fill,ground_cap,sky,sign_finish}` | painted | the four the README was waiting for |
| **`Art/Tree/leaf_card` (29th pass, 20.09)** | **painted, `Tools/gen-tree-leaf-textures.py`** | **one leaf, drawn Sliced in a leaf part's own box for parts of 0.02-0.12 m** |

The tree's leaf parts are the case the material could not cover: the rig's leaf parts are
0.002-0.5 m and the material's own leaves are 0.068-0.127 m, so a part of 0.02-0.12 m showed a
crop that is 87 % ink with a cut leaf on 100 % of the window borders
(`Tools/art0-leaf-window-probe.py`) - a green plate edged by the part's own rectangle, which is
what three reads of `art0leaf28_tree.png` named. From the 29th pass such a part draws the
painted leaf card (`ArtParts.TreeLeafCardKey`, one branch in `DressAll`), a part at or above
0.12 m keeps the material.

**CLOSED, 30th pass (20.09)** - `Trials/Runs/art-0-trunk-owner-30th-report.md`. The trunk chips
have a NAME now, and it is not what the 29th pass predicted. `Assets/Editor/ArtTrunkProbe.cs`
builds the gallery in edit mode, reproduces the shipped "oak close" lens (its own base frame is
0 px different from `art0leaf29_tree.png`), and then PAINTS OUT each of the 892 renderers of the
oak that reach the bole, one at a time. The owners are the rig's `Spur0_Tuft{0,1,2}_*` clusters
dressed with the `leaf` MATERIAL - the top single part owns 37 px at `0.016x0.035 m`, every
owner reads `cardBand=False`, and NOT ONE of them is a "leaf-material part at or above 0.12 m":
that hypothesis is refuted. Attribution of the bole's 7883 px of green: leaf material 5488 px
(69.6 %), leaf cards 1276 px (16.2 %), both leaf languages off leaves 0 px and the "neither"
set is 0 px, so the bole carries no unexplained green. The complaint's own word was tested too:
of the 36 green clusters on the bole, 0 pairs are byte-identical at native size (1 same-size
pair), and two 4x zooms of the shipped still both report organic jagged silhouettes with
leaflets and no texture-crop edge. Nothing was moved, on purpose. What is still open there: the
rig's own module seams and the base gap (the 17th pass' item), and the 1171 leaf parts under
0.02 m that keep the material - the owner list in the report is their population.

**The base gap is CLOSED (31st pass) and the foot is painted (32nd pass)** -
`Trials/Runs/art-0-bolefoot-31st-report.md`, `Trials/Runs/art-0-bolefoot-32nd-report.md`. The
17th pass' "base gap" was one row of pixels with a geometric cause (the trunk's own cards start
1.25 mm above the ground line and the rasterizer's centre rule paints a full row of backdrop
under the bole): the tree now draws a painted bole flare on the base part's own slot
(`ArtParts.DressTreeFoot`, `Art/Tree/bole_foot.png`, box 1.25x the base box wide, bottom
0.30 x height BELOW the ground). The 32nd pass repainted that canvas: the toe depths spread
4.0x -> 6.0x (measured by the new `Tools/art0-toe-depths.py`), and the toes are now cut by the
bark MATERIAL's own grain (`fissure_field`, `corr(fissure, depth) = +0.450`; `NOTCH_SHADE`
carries the same grooves below the soil line), which is what the 31st pass' read asked for.
Three blind reads (two canvases, one 16x frame) all chose the new canvas; one 8x frame read
missed the change and is recorded as a miss (`Tools/art0-crop-diff.py`: 138 of 1210 px of that
crop differ). Still open in this family: the bole's ~33 flat `Tiled` bark cards and its two
measured backdrop pockets (a single painted bole canvas would close both - a bigger art
decision), and the two shoulder runs the silhouette probe still reports (9 / 11 columns inside
a 2 px band; the instrument's own floor for a 4.8 px envelope is ~10-12, so no curve wins it).
