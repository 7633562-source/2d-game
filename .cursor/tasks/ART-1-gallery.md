# ART-1 - the real-texture gallery scene (Yury, 18.09.2026)

One new scene where **every** game object is shown with the real texture that will ship, so Yury can
judge the look of the game before it is built everywhere. All other scenes keep their placeholder
rectangles - this is a showcase, not a global art pass.

Read `ART-0-inventory.md` first: the art exists (62 PNGs in `Assets/LegacyArt/`), nothing loads it, and
four level textures were never delivered.

## Rule zero - visual only
Touching visuals must not change physics. Acceptance of every cycle in this task: the physics numbers of
the reference runs stay BIT-IDENTICAL (`dog_art1` triple: comTravelX 5.53443003, comSpeedX 0.230613619,
35 cycles; walk reference run `walk1_load4`: fallTime 5.865, pelvisDrop 0.8723, comTravelX 2.0002).
Real art is enabled by a flag / by the gallery scene only; headless trials stay on placeholders unless a
flag says otherwise. This is the cosmetics-vs-physics boundary the dog review demanded - prove it.

## Step 1 - make the art reachable (loader)
- `LevelArt` and `ArtLibrary` must be able to find the real files. Keep `LegacyArt/` where it is (it is
  fine for the editor) and add a build/import step, or move the needed subset under
  `Assets/Resources/Art/` - whichever the implementer can make work in BOTH editor and headless player
  (the loader already has a `Resources.LoadAll<Sprite>` fallback for sliced atlases: reuse that pattern).
- Deliver the four missing level textures into `Resources/Level/` with the exact names the README
  demands: `ground_fill`, `ground_cap`, `sky`, `sign_finish`. Until the artist delivers them, the
  gallery must show the gap explicitly (a labelled "MISSING ART" tile), never a silent placeholder.

## Step 2 - per-part binding, behind a flag
- Human: bind the 10 parts (head, torso, pelvis, upper, lower, thigh, shin, foot, hand, neck) to the
  matching `HumanSegment` colliders. Size via `sprite.size` / renderer bounds so the drawn part matches
  the collider - **never** `localScale`. Preserve the part's aspect.
- Trees: trunk/limb segments use `bark`, crown/leaf layers use `leaf`; apple fruit uses `Apple/fruit`.
- Ground: `dirt` fill with `grass` cap, sky `sky`, finish `sign_finish`.
- Creatures (dog, crow, chicken, hedgehog) are owned by their own streams (ART-2/ART-3) - do not edit
  their files here.
- The flag must default to OFF for headless trials, so no existing measurement changes.

## Step 3 - the scene itself
- New scene `Assets/Scenes/Gallery.unity`, added to the build list.
- Layout: one row per family, objects at **real scale** with a 1 m scale bar, a label under each
  (name + source file), neutral background, no gameplay scripts running.
- Include: human (all variants we can pose), oak/tree kinds, apple tree with fruit, strawberry, flower,
  ground strip (dirt + grass cap), sky, sign, campfire, and reserved labelled slots for dog, crow,
  chicken, hedgehog marked "ART-3 pending".
- One comparison toggle: real art ON/OFF, so the difference is visible in two frames.
- Day/night: use the existing `DayNightCycle` if it can run without physics side effects; otherwise a
  fixed mid-day light.

## Evidence (Yury's standing requirement)
- 1080p frame of the whole gallery, plus a close 1080p frame of the human and of one tree.
- A short video panning the gallery (>= 15 s).
- The label + build hash of the same build that produced the frames.
- The gap list: every object with no real texture yet, named, plus the four level files' status.

## Acceptance
1. Gallery scene opens, contains only what is listed above, no placeholder rectangle anywhere except
   the explicitly labelled MISSING-ART tiles and the ART-3 pending slots.
2. Human parts and tree parts show real textures, fitted to their colliders (no stretched or squashed
   sprites; check against the collider bounds numerically, not by eye).
3. Frames + video delivered from one build, with the build hash.
4. Physics untouched: the reference runs above bit-identical.
5. A printed inventory in the report: object -> texture file -> real/missing.

## Order of work (one change per cycle, each with the acceptance above)
- ART-1a: loader reachability + the four level files' status + MISSING-ART tiles.
- ART-1b: human rig binding + gallery scene with the human.
- ART-1c: trees/apple/strawberry/flower + ground/sky/sign in the scene.
- ART-1d: comparison toggle, scale bars, labels, video.
- ART-2 (plant lane) and ART-3 (dog lane): creature bodies into the same scene.

## Out of scope
Making every gameplay scene use real art (later, after Yury approves the look); new character designs;
UI; changing physics to accommodate art.

## Pipeline note (read before Step 1)
The rectangle-equals-collider contract in `.cursor/rules/painted-parts.mdc` is not the enemy: keep it.
`HumanSegment` already stretches the full PNG onto the collider box (`Sliced`), so real art needs no
rig change - it needs the PNG to actually arrive at runtime. LegacyArt is marked "do not bundle", so the
missing link is the split/import step (`Tools/SplitSheet` -> crops), which must be run or reproduced.
Completing that step, for the parts the gallery needs, is part of ART-1a.
