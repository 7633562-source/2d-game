# Engine channel 6.6 — deferred upgrade record

Status: **SWITCHED 2026-09-30** — install done (29.09 23:57, H_SysAdmin), project switch executed overnight (30.09 00:37-00:45): build OK, control run clean; see "Switch executed". Owner: Hermes (orchestrator).
Lanes tracked here: 6.5 patch (6000.5.11f1 - branch closed), 6.6 (**current: 6000.6.4f1** - taken for the
game line 02.10, see the re-base note at the end; was `6000.6.3f1` since 30.09), 6.7 beta (6000.7.0b1/b2).
Everything below is verified against unity.com or the project files on the date named.
Created 2026-09-17 from the H_Scout "Unity 2D" digest (cron `58988890f9e7`, run 03:04).

## Why deferred

The Grok burn campaign (`grok-burn-campaign.py`, until 2026-09-29) owns the
workspace: the hourly trial tester, the 10-minute compile gate and the walk /
stance baselines in `Trials/Baselines/` are all bound to the current editor.
An upgrade swaps Physics 2D and URP underneath a running acceptance pipeline and
would invalidate every baseline mid-flight. Re-measure after the campaign.

## Our actual state (verified 2026-09-17, not from memory)

- Editor `6000.5.8f1` — `ProjectSettings/ProjectVersion.txt` (revision `5cb7df797b7d`).
- URP `17.5.0`, URP config `17.5.0`.
- Shipped 2D packages (`Library/PackageCache/*/package.json`):
  animation `15.1.0`, spriteshape `15.0.3`, psdimporter `14.0.3`, aseprite `5.0.3`,
  pixel-perfect `6.0.0`, tilemap.extras `8.0.3`, common `14.0.1`, tooling `3.0.1`.
- Corpus check: `Light2D` referenced in Assets, `ShadowCaster2D` and `Tilemap`
  have **0** references — ShadowCaster2D community pain does not touch us.

## Digest items (H_Scout, 2026-09-17) and the call on each

| Item | Source | Call |
|---|---|---|
| `6000.6.1f1` (16.09) — Physics 2D fix: dynamic body with rotation constraint no longer bounces at zero bounciness. Known 2D Renderer black screen with Bloom active (`6000.0.51f1+`, `6000.3.0a1+`). | https://unity.com/releases/editor/whats-new/6000.6.1 | Hold: editor upgrade, see above. Bloom + 2D Renderer warning matters for us — we use the 2D Renderer. |
| LTS patches `6000.0.84f1` (16.09), `6000.3.24f1` (10.09) | https://unity.com/releases/editor/archive | Not our channel — no action. |
| 2D packages at registry `latest` target `6000.7` (animation 17.0.1, spriteshape 17.0.0, aseprite 7.0.0, psdimporter 16.0.0, pixel-perfect 6.0.0, tilemap.extras 10.0.0) | https://packages.unity.com/com.unity.2d.animation | Rejected now: they do not install on 6.5/6.6. Revisit with the editor. |
| In 6.6: 2D Graphics Profiler + Tilemap Profiler modules, Light2D shader stripping + rendering layer mask, GPU-skinned ShadowCaster2D, Light2D/ShadowCaster2D providers, Wind/Buoyancy in Physics 2D, Unity Recorder with the 2D Renderer. WebGPU out of experimental + compat mode for older devices. | https://unity.com/releases/editor/whats-new/6000.6.0 | Evaluated after the campaign. Our target platform is WebGL, so WebGPU + compat is the highest-value line to test then; profilers are cheap wins on real scenes. |
| Official Unity plugin for Codex (16.09) and Claude Code (09.09): 31 Unity-engineer skills incl. 2D/tilemaps, plus a `unity-cli` skill (installs editors, creates and opens projects, manages packages). Unity 6+. | https://unity.com/blog/unity-plugin-codex | Interesting for us: `unity-cli` is a supported, machine-readable alternative to the fragile batch build in `Tools/run-trial.ps1` (two Unity instances racing for debugger port 3200 broke a build on 17.09). Evaluate after the campaign; do not swap the trial harness mid-flight. |
| Free 27-tile 16x16 interior set with ready `.meta` (16 PPU, Point filter). | Reddit r/Unity2D `1wia2dl` | Source **unverified from this host**: curl and the extract backend both get 403 from Reddit. If we want the art, fetch it through a browser session. Not blocking: our art path is `ArtIncoming/`. |

Coverage gaps named by the scout: `gamefromscratch.com` 403 to curl; Unity news via
`web_search` unusable; `status.unity.com` minor DevOps/UGS incident since 01.09 and
degradation in the US region — no local impact.

## Local checks done while logging this (2026-09-17)

- `ShadowCaster2D`: 0 usages in `Assets` — the community pain in the digest does
  not apply to us. `Light2D` is present, so the Light2D items are worth reading.
- Bloom hazard: `Assets/DefaultVolumeProfile.asset` is wired as the URP default
  volume profile in `Assets/UniversalRenderPipelineGlobalSettings.asset` and
  carries 9 enabled overrides. Bloom override state is now verified from the
  asset (file inspection is enough): `active: 1`, `intensity.m_Value: 0`,
  `threshold.m_Value: 0.9`. Bloom is enabled but visually inert at intensity 0,
  so the 2D Renderer + Bloom black screen (6000.0.51f1+) is a latent hazard,
  not a live one - it fires only if someone raises Bloom intensity. If a render
  goes black after no code change, check the volume profile before blaming our
  art. Re-check on the next engine.
- `com.unity.2d.tooling 3.0.1` is present in the package cache; it is not in the
  digest's baseline list — harmless, just noting the registry comparison is not
  exact.

## 6.5 patch lane (cheaper than 6.6): 6000.5.8f1 -> 6000.5.11f1

Verified against unity.com on 2026-09-17 (whats-new/6000.5.11 answers HTTP 200; 6000.5.12 and
6000.6.2 both 404, so no newer build exists in either branch):

- The 6.5 branch top is `6000.5.11f1` (2026-09-02); we are 3 patches behind our own branch.
- Fixes that could matter: Physics 2D - a very large wind force via `PhysicsBody.ApplyWind` or a very
  large impulse via `PhysicsWorld.Explode` could silently corrupt the whole physics world with NaN
  (UUM-150029); Rigidbody2D wakes when a child GameObject carrying a Collider2D is activated;
  Sprite Editor fast-playmode error message.
- Exposure check on our code (grep over `Assets`): `ApplyWind` in `PlantTree.cs:1559` and
  `TreeDriver.cs:74` is our own method, not the Unity 2D API, and nothing calls
  `PhysicsBody`/`PhysicsWorld` - UUM-150029 is not our bug. Runtime `SetActive` is confined to 10 calls
  in 3 files (`Campfire.cs`, `Torch.cs`, `YardAmbient.cs`), all ambient visuals, none of them toggling a
  Collider2D, so the Rigidbody2D wake fix is low value for us.
- Known issue UUM-149540 (`Resources.UnloadUnusedAssets()` much slower in a Player build, 6000.5.0b2+)
  does not reach us from our own code: neither `Resources.UnloadUnusedAssets` nor `GC.Collect` appears
  in `Assets`. Caveat: the engine still unloads on scene change, so keep it as a suspect for
  mobile/WebGL memory budgets - measure, do not assume.
- Call: **DEFERRED**, same reason as 6.6. A patch bump swaps the editor binary and with it Physics 2D,
  which invalidates the stance/walk baselines the burn campaign is measuring until 2026-09-29. It is
  the lower-risk lane of the two: same minor, URP and packages untouched, no asset upgrade pass.
  After the campaign: 6.5.11 first, re-run the baselines, then decide 6.6 on its own.

## 6.6 lane update (2026-09-19): use 6000.6.2f1, not 6.6.1

`6000.6.2f1` (2026-09-18, revision 770e33f6875c) is out and is now the pick inside the 6.6 branch.
Verified: whats-new/6000.6.2 = HTTP 200; 6000.6.3, 6000.7.0b2 and 6000.5.12 all 404.

- CORRECTION (2026-09-28): this entry originally claimed 6.6.2 ships the Light2D memory-leak fix
  (UUM-143650) - "2D: Fixed Light2D memory leak when unloading scenes". The issue tracker card lists
  `fixedInVersion: 6000.7.0b1` only, with no 6.6 backport, while the 6.6.2 release notes do mention the
  fix. Two Unity sources disagree, so treat it as *possibly* in 6.6.2 and verify at the upgrade; with
  9 scenes and 8 files referencing Light2D the effect is measurable either way.
- Still open in 6.6.2 per its own Known Issues: UUM-150232 (our Bloom + 2D Renderer black screen; root
  fix announced for 6000.7.0b2 only) and UUM-149540 (`Resources.UnloadUnusedAssets()` slow in Player
  builds, 6000.5.0b2+).
- Also in 6.6.2 and relevant to our WebGL/IL2CPP path: WebGPU render_attachment usage flags
  (UUM-150892), cube-texture shaders (UUM-150746), batchmode builds with NameFilesAsHashes
  (UUM-149003), IL2CPP direct codegen for ldind/stind restored (UUM-129900).
- No new patches in the 6.5 branch: 6000.5.11f1 is still the top there, so the 6.5 lane is unchanged.
- Decision unchanged: install waits for the burn campaign to close. Within 6.6, prefer 6.6.2.

## 6.6 lane update (2026-09-25): 6000.6.3f1 fixes OUR bug

`6000.6.3f1` (2026-09-24, SUPPORTED) is out, so the pick inside the 6.6 branch is 6.6.3, not 6.6.2.
Verified on the release page (HTTP 200; 6000.5.12 is still 404, so no 6.5 build carries these fixes):

- UUM-150232 is FIXED: "URP: Fixed the 2D Renderer creating a depth and stencil buffer in players when
  Disable Depth and Stencil Buffers is enabled in Player Settings." It is gone from the Known Issues
  list of 6.6.3. This is the root of our Bloom + 2D Renderer black screen, which this file and the
  scout's state had recorded as "6000.7.0b2 only" - that note is now obsolete: a SUPPORTED 6.6 patch
  carries the fix. Raising Bloom intensity stops being a landmine after the 6.6 upgrade.
- UUM-112909 - Web platform build fixed when output assets exceed 2 GB (with a clearer error above the
  4 GB limit). Directly on our shipping target.
- UUM-151103 - content-directory incremental builds: a Scene is rebuilt when a Prefab it uses changed.
  Matters if the content-directories pilot happens.
- Android: Target API Level 37 no longer fails with "Failed to find target with hash string android-37"
  (UUM-150216); AGP 9.0.0 -> 9.1.1.
- Still open in 6.6.3 per its Known Issues: UUM-149540 (Resources.UnloadUnusedAssets slow in Player
  builds - affects our 6000.5.8f1 too), UUM-152966 (Play Mode exit deadlock with Graphics Jobs),
  UUM-153744 (Vulkan swapchain crash). Our own code does not call UnloadUnusedAssets.
- 2026-09-26: UUM-150034 is also fixed in 6.6.3 - "Scripting: Fixed a false OperationCanceledException
  thrown when awaiting Task.Delay() inside a background-thread Awaitable method" (verified in the 6.6.3
  fix list). Exposure check on our code: `Awaitable`, `async`, `await` and `Task.Delay` appear 0 times
  in `Assets` (our async story is coroutines), so this bug is not ours and adds no weight to the
  decision - the Bloom fix remains the only measured reason for 6.6.3.
- Decision: timing unchanged (installs wait for the burn campaign to close); content changed - after
  29.09 the 6.6 lane means 6000.6.3f1, and the Bloom constraint can be lifted once we are on it.

## 6.7 beta lane (6000.7.0b1, 2026-09-17)

Verified on 2026-09-18 (whats-new/6000.7.0b1 = HTTP 200; 6000.7.0b2 and 6000.5.12 = 404):

- UUM-150232 (our Bloom + 2D Renderer black screen) is now root-caused on the official page:
  "URP: Fixed the 2D Renderer creating a depth and stencil buffer in players when Disable Depth and
  Stencil Buffers is enabled in Player Settings" - and it is marked *Fixed in 6000.7.0b2*, so it is in
  no 6.5 build (and, as of 2026-09-25, it also shipped in the SUPPORTED `6000.6.3f1` - see the 6.6
  lane update above). Exposure check on our project - both preconditions are absent:
  `ProjectSettings/ProjectSettings.asset:71` holds `disableDepthAndStencilBuffers: 0` (the option is
  OFF) and Bloom intensity is 0 (see above). Keep both as they are; if an art pass raises Bloom
  intensity, re-check the Player Settings flag first.
- UUM-138419: SpriteMask not rendering on Android/iOS/Web when Strip Engine Code is on and the Tilemap
  package is disabled in Package Manager. Exposure check: `stripEngineCode: 1`
  (`ProjectSettings.asset:186`, precondition present) but `SpriteMask` appears 0 times in `Assets`, so
  it is not our bug - and it stays that way while we do not adopt SpriteMask.
- Light2D memory leak on scene unload (mobile/WebGL memory): we have 9 scenes and 8 files referencing
  Light2D, so measure it together with UUM-149540 after the campaign.
- New in 6.7 and on our path: Physics Core 2D Profiler module, `EntityIDTile`/`Base` for Tilemap with
  Burst, Light2D and ShadowCaster2D ShaderGraph targets, custom Light2D materials, and
  `performance.measureUserAgentSpecificMemory` in the web diagnostics overlay.
- Learning (free, do it while the campaign runs): Unity Learn Monthly Update, September 2026 -
  https://discussions.unity.com/t/unity-learn-monthly-update-september-2026/1736869 - the Input System
  series is the relevant part. Note our actual exposure: only 2 files in `Assets` reference
  `UnityEngine.InputSystem` and there are 0 legacy `Input.*` calls, so this is reading material, not a
  migration task.

## Content directories (6.6+) - the first feature that hits our metrics

Verified 2026-09-23: Unity's article "Content directories: Beyond the AssetBundle" (George Ing,
2026-09-22) is live and describes them as the "more-performant, more-granular replacement for
AssetBundles" available in 6.6 for content that ships with the Player.

- Why it matters here: it targets exactly the metrics we judge the upgrade by - build size, build time,
  load time - and it covers our platforms (Player content, so WebGL and mobile included). Artefact
  names are content-addressed, so deduplication is built in; the build runs through the normal asset
  import framework (deterministic, cached, parallel) instead of bundling.
- The Addressables-to-content-directories conversion is claimed to be code-free; the recipe is in the
  Addressables 4.0 docs (convert-content-directories). The early-adopter Q&A (discussions 1732493)
  adds the trade-offs: a Build -> Register -> Load -> Unload API, an uncompressed directory that is a
  readable folder + manifest, and no CancellationToken on LoadAsync/LoadSceneAsync yet.
- Planning caveat: in 6.6 this is for STATIC Player-shipped content; remote/OTA/CDN and DLC/mods are
  promised for the Unity 7 generation (2027). We have no Addressables/AssetBundle path today, so there
  is nothing to port - it is an option for how art and tile content ship later.
- Action after the campaign and after the 6.6 decision: measure build time and build size with content
  directories against the current plain build on the same scene. Keep it out of `CURRENT.md` so no
  agent starts it mid-campaign.
- 2026-09-24, official manual page with root-asset design examples (verified HTTP 200, contains
  `BuildPipeline.BuildContentDirectory` and the `Loadable<T>` guidance):
  https://docs.unity3d.com/Manual/content-directories-examples.html . Read it BEFORE any pilot - it
  decides how code finds content at runtime and how much content registers into memory.
- 2026-09-24, pitfall from the community (verified thread):
  `SharedBundleSettingsCustomGroupIndex` in Addressables stores a group INDEX, not a GUID, and groups
  are re-sorted by GUID on serialize, so adding a group resolves the wrong one; the shared-bundle
  group also cannot reference a content-directory group. Do not use a custom "Shared Bundle Settings"
  group in a pilot.
- 2026-09-24, support status of our own channel - checked, not taken from a third party: Unity's own
  archive feed marks 6000.5.8f1 (2026-08-12, revision 5cb7df797b7d - the revision in our
  `ProjectVersion.txt`) and the newest 6000.5.11f1 (2026-09-02, revision 5c3a1087d8e4) as
  `stream: SUPPORTED`. endoflife.date claims 6.5 support ended 2026-08-31; that is a third-party page
  contradicted by Unity's archive and by the 02.09 patch itself - do not plan on it.
- 2026-09-28, and the support question resolved the other way: Unity's issue tracker now marks the 6.5
  ports as ended - issue 6820 (UUM-112909) carries `resolutionNotes: "6.5 has reached EOS"` on the
  6000.5.X port, and issue 24168 (UUM-150232) carries `"6.5 is EOL"` (both fetched first-hand today from
  issuetracker.unity.com/api/v1.0/issues/<id>, HTTP 200). The release archive still prints `SUPPORTED`
  for 6000.5.11f1, so two Unity systems disagree; the tracker is the one that talks about ports, and no
  further 6.5 patches are coming. The fixes we actually want exist only in 6000.6.3f1 (UUM-150232:
  fixedIn 6000.6.3f1 and 6000.7.0b2; UUM-112909: fixedIn 6000.6.3f1 and 6000.7.0a4). Reading: the upgrade
  is no longer optional - it is the only way this project receives engine fixes - while the timing (quiet
  window after the burn campaign, on 6000.6.3f1) stays as planned.
- 2026-09-26, Addressables version for a content-directories pilot - UPDATED 2026-09-29: the registry
  `latest` tag moved from 2.10.3 to `com.unity.addressables` **4.1.0** (published 2026-09-28 23:56 UTC,
  verified in the registry today), so 4.x is the default line now; 4.0.1 remains the first 4.x. 4.x needs
  content-directory support in the editor (6.6+), so the package must not be pulled before the editor
  upgrade. For the pilot, 4.1 adds Content Directory Archive Mode (None / Uncompressed / LZ4), applies
  Target Archive Size to uncompressed archives as well, and makes cache probing faster (Cache Probe Mode,
  default Cached Versions); measure build size and build time with Uncompressed against LZ4.
  Unity-confirmed bug to expect: a group converted to content directory disappears from the inspector
  group picker (the conversion itself is fine; change groups in the Addressables Groups window). In a
  mixed project the default shared-bundle group must stay on the Asset Bundle schema, or be overridden
  in `AddressableAssetSettings`.

## Platform specs and pipeline direction (2026-09-30)

- Android minimum spec: from 6.6 the minimum is OpenGLES 3.1 and the "Require ES 3.1" Player Setting is
  gone - Android players run on Vulkan or GLES 3.1/3.2 (Vulkan recommended, automatic fallback). Unity's
  thread quotes the reach cost of dropping older GLES: ~0.4% of players at 3.0, ~0.7% at 3.1. Not our
  problem yet (no Android build), recorded for the first mobile config; see perf-budget.mdc.
- Pipeline direction: Unity 7.0 makes the Built-In Render Pipeline obsolete and URP the single pipeline
  (official forum thread, 2026-09-29); 6.7 LTS promises GTAO, SSR, an AgX tonemapper, on-tile
  post-processing, upscalers and Surface Cache GI, and web gets WebGPU + WebAssembly64. We are already on
  URP with the 2D Renderer, so there is nothing to migrate - the practical rule is "do not add BIRP-era
  assets/shaders", which we already follow.
- WebGL light limits per Unity staff in that thread: web platforms receive the mobile shader limits, the
  per-camera additional-light limit can be raised with the URP Config package, and Forward+ removes the
  8-light-per-object cap (Deferred is unavailable on WebGL). For our 2D Renderer the binding number is
  `Renderer2D.asset: m_MaxLightRenderTextureCount: 4`; scenes hold 0 Light2D components and at most one
  runtime light per scene, so we sit far under it - numbers and the rule live in perf-budget.mdc.
- EDM4U is deprecated by Google on 2026-10-26; Unity's official EDM (`com.unity.external-dependency-manager`
  2.1.0, UPM, same XML declaration format) replaces it. We hold no EDM4U setup and no native dependencies,
  so there is nothing to migrate - the note exists so the first native Android/iOS SDK lands on EDM.

## Re-evaluation trigger

After the burn campaign closes (deadline file `%LOCALAPPDATA%\Temp\grok-burn\campaign.deadline`):

**Status 2026-09-28: approved.** Yury gave the go-ahead in H_Team («+» to the game line's 28.09 report,
14:09 MSK). The editor install is already requested from H_SysAdmin (`courier_admin_unity663.txt`):
6000.6.3f1 via Unity Hub, beside the current editor, nothing uninstalled, to run after the campaign
closes (`2026-09-29T22:35:37`). The project switch below waits for the editor to be present and for a
quiet window.

1. Re-read `ProjectSettings/ProjectVersion.txt` and `Packages/manifest.json` (not this file).
2. DONE (30.09): `6000.5.8f1` → `6000.6.3f1` (install 29.09, switch overnight, build OK, control run clean).
   The open decision is only *when* to take `6000.6.4f1` - criteria in its section above: a fix we measure
   on top of 6.6.3, or the first public WebGL build, whichever comes first.
3. Upgrade in a quiet window, re-bake shaders, re-run stance + walk baselines,
   compare against `Trials/Baselines/stable-pd/`, and only then let agents touch gameplay code.
4. Test a WebGL/WebGPU build against the current one for size and load time.
5. The 6.7 lane is no longer needed for our Bloom bug: UUM-150232 was fixed in `6000.6.3f1`
   (2026-09-24). Go to 6.7 only for its own gains (Tilemap on jobs/Burst, Physics Core 2D profiler,
   Light2D and ShadowCaster2D custom materials with ShaderGraph targets, the soft shadow generator,
   MSAA memory savings on mobile), and never before 6.6.
6. Pre-switch compile check of editor code (precedent 2026-09-20: SuperTiled2Unity, a third-party Tiled
   importer, stops compiling on `6000.6.2f1` with CS0619 `ProjectWindowUtil.CreateAssetWithContent`
   obsolete-as-error, and has no upstream fix - the same class already broke Meta XR SDK on 6.5;
   https://discussions.unity.com/t/1737055). Our exposure: `Packages/manifest.json` holds no
   third-party editor packages (Unity modules plus collab/rider/visualstudio only), there is no
   `.tmx`/`.aseprite`/`.psd` asset anywhere in `Assets`, and our own 10 scripts under `Assets/Editor`
   use only non-obsolete APIs (`AssetDatabase.CreateAsset`). So the check is cheap: compile
   `Assets/Editor` and the editor-side code first after the switch, before gameplay code is touched.
   Also check third-party managed DLLs: in 6.7.0b2 `UnityEngine.Hash128` moved from `CoreModule` to
   `ManagedKernelModule` with no type forwarder, so precompiled plugins referencing it fail with
   `TypeLoadException` at load (thread 1737635, no Unity answer yet - same family as the SuperTiled2Unity
   CS0619). Our exposure is zero: `Assets` holds 0 `.dll` files.
 7. Aseprite Importer has two independent community symptoms now: misaligned slices when frames are added
 (Unity accepted the bug report 18.09) and, new on 28.09, sprite components dropped and frames swapped
 after importing `.aseprite` (r/Unity2D `1ws14qz`, unanswered). We hold 0 `.aseprite` assets, so it
 cannot hit us today - re-check before making `.aseprite` the animation pipeline.

## First-hour checks after the switch (second opinion via the free-web queue, 2026-09-29)

Reviewed by a free channel (deepseek web, 36 s) against the plan above; kept only what survives
verification. Pre-switch snapshot, before the editor touches anything: record
`ProjectSettings/Physics2DSettings.asset` (`m_SimulationMode: 0` = FixedUpdate today, gravity -9.81,
velocity 8 / position 3), `GraphicsSettings.asset` and `QualitySettings.asset` - the editor rewrites
`ProjectVersion.txt`/`manifest.json` on first open, so those two say nothing about the past.

After the switch, in this order:
1. Full compile with `-batchmode -quit -logFile`, grep for `error CS` and `obsolete` (catches URP 17 API
   breaks; our only near-API reference is `WorldPerformanceMonitor.cs`, which looks up
   `UniversalRenderPipeline.RenderSingleCamera` / `RenderSingleCameraInternal` by name via reflection - a
   rename there does not break the build, it silently disables frame-time capture, so run the monitor once
   and confirm it still records).
2. Confirm `m_SimulationMode` is still 0 (FixedUpdate) and that gravity/iteration values did not move;
   then one stance and one walk trial against the frozen baselines.
3. WebGL development build: does it build, is the summed output under 2 GB, does the scene render with
   Light2D active.
4. `Renderer2DData`: check whether the new Light2D shader-stripping toggle defaults to on - if it does,
   verify Light2D still renders in the WebGL build (lost variants show in the player, not the editor).
5. Light2D leak measurement (UUM-143650) while the sources still disagree (release notes say 6.6.2, the
   issue tracker says 6000.7.0b1 only): 10 load/unload cycles of a scene with >= 4 Light2D objects,
   reading `Profiler.GetTotalAllocatedMemoryLong()` at the top of each cycle. A delta between cycle 1 and
   cycle 10 of <= 500 KB, or <= 50 KB per Light2D object per cycle, counts as fixed; >= 5 MB means the
   leak is still there.

Rejected from that review: "UUM-112909 may not be fixed in 6.6.3" - the 6000.6.3 release notes state it
verbatim ("Web: Fixed Web platform build when output asset sizes are larger than 2 GB ...") and the issue
card carries `fixedInVersion: 6000.6.3f1`. Unverified but noted: a reported Sprite Atlas size/memory
regression in early 6.6 builds - we hold 0 `.spriteatlas` assets, so no exposure today; re-check when the
art path starts packing atlases.

## 6000.6.4f1 (01.10.2026) - checked; TAKEN 02.10 (see the re-base note at the end)

Verified on the release page (HTTP 200): the newest SUPPORTED patch in 6.6. It carries what 6.6.3 has
(including our UUM-150232 Bloom fix - still absent from its Known Issues) plus:
- `Web: Added warnings when scripted change to maximum memory size results in memory size clamping or
  switch to WebAssembly 2023 target` (UUM-151511) - useful only as diagnostics against the >4 GB Wasm64
  trap we already documented;
- `Input: Removed a 17 B main-thread GC allocation on every input update tick in Mono players`
  (UUM-147633) - our WebGL player is IL2CPP, so the Mono path does not apply; desktop Mono builds would
  gain a little;
- `Shaders: fixed random crash during shader variant uploads in Player builds` (UUM-150584) and a
  thread-safety crash fix in the editor (UUM-146812);
- three cosmetic 2D editor-UI fixes (Tile Palette overlay width UUM-151417, Sprite Frame info box
  UUM-153407, Sync Active Palette button UUM-152530) and a profiler fix for the Physics 2D module
  (UUM-151943).
- Still open in 6.6.4: UUM-149540 (`Resources.UnloadUnusedAssets` slow in Player) - now open across every
  branch, so plan a workaround rather than a patch; UUM-153744 (Vulkan), UUM-142773/UUM-150566
  (bee_backend crashes), UUM-154645 (MPPM), UUM-142550 (fps drop with fast mouse).
- Take it when: a fix we measure lands on top of 6.6.3 (a WebGL/memory or 2D-physics one), or before the
  first public WebGL build if nothing newer exists by then - not for cosmetic editor-UI fixes alone. A
  switch costs a re-baseline, and we have just paid one for 6.6.3.
- Content directories, measured reality (thread 1738130, 02.10; confirmed by the Addressables 4.1
  changelog): **content directories are not compressed by default, AssetBundles are** - so a naive
  comparison shows the new pipeline as "bigger". The pilot must set compression explicitly (in
  Addressables 4.1: Content Directory Archive Mode = LZ4 / Uncompressed / None) and measure all three
  against the current AssetBundle build for both size and time.

## Switch executed (2026-09-30, 00:37-00:45)

- 2026-09-29 23:57 — H_SysAdmin completed the install of `6000.6.3f1` beside `6000.5.8f1`
  (`F:\6000.6.3f1\Editor\Unity.exe`; WebGL + Windows-standalone modules; Hub lists both editors;
  ref `reply_admin_unity663_install-done.txt`). Nothing uninstalled — rollback is a Hub switch away.
- 2026-09-30 00:37 — `ProjectVersion.txt` = `6000.6.3f1 (45d8eee7de74)`; harness scripts
  (`tilt-r15/r16-build.sh`, `tilt-csc-check.sh`) repointed to the new editor; git commit `3995a2ea`
  "Unity 6.6.3 upgrade (30.09, Hermes)" records the baseline.
- Editor auto-migration added serialized fields only (`URP-2D.asset`: `m_Light2DPrefilteringMode: 0`,
  `m_Light2DKeptVariantCombos: []`, shadow/prefilter additions; `GraphicsSettings.asset`:
  `m_DefaultLightBaker`, `compilerSettings`; `UniversalRenderPipelineGlobalSettings.asset` rewritten).
  No behavior flags changed.
- Check 1 (full compile) passed on the new editor (BUILD OK). Physics2D re-checked: gravity `-9.81`,
  velocity 8 / position 3, contact offset 0.01, `m_SimulationMode: 0` — identical to the pre-switch
  snapshot.
- Control trial run on 6.6.3 completed with exit 0, no errors; the line returned to normal operation
  the same night (trial runs continue, e.g. `tiltA_r33` on 6000.6.3f1).
- Still open from the first-hour list: WebGL dev build against 2 GB, Renderer2D Light2D stripping
  toggle check, Light2D leak measurement (UUM-143650) — next WebGL/art session.

## Note to H_Scout

The scout's stored project context claimed the project sits on the 6.6 channel.
It does not: we are on `6000.5.8f1`. Corrected in
`profiles/scout/state/unity2d-watch.json` on 2026-09-17 so the next digest does
not headline a version we are not on.

## 6.6.4 taken - the game line re-base (02.10.2026, KEEP)

- `ProjectSettings/ProjectVersion.txt` = `6000.6.4f1 (12bfff696524)` since ~02.10 14:38; the editor
  is installed at `F:/6000.6.4f1/Editor/Unity.exe` (beside `F:/6000.6.3f1`; the switch is OPS-1002).
- The walk line re-based in round c118 - the first fire on 6.6.4: all 16 anchors re-earned
  BYTE-IDENTICAL (player fp `8ce1a11f`, dll `a0a70046`, TRIAL_BUILD_OK, 0 CS) - the editor switch
  is behaviourally inert for the line (`Trials/Runs/walk-cycle118-unity664-hold.md`).
- The 02.10 reorg commit `8e9dd5d8` ("state after walk c124 + Unity 6.6.4") records the state.
