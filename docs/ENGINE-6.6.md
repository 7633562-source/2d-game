# Engine channel 6.6 — deferred upgrade record

Status: **DEFERRED**. Do not start without Hermes/Yury. Owner: Hermes (orchestrator).
Lanes tracked here: 6.5 patch (6000.5.11f1), 6.6 (6000.6.2f1 - best in branch), 6.7 beta
(6000.7.0b1/b2). Everything below is verified against unity.com or the project files on the date named.
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

- It ships the item we would otherwise have waited for 6.7 to get: "2D: Fixed Light2D memory leak when
  unloading scenes - the runtime-created light mesh is now properly destroyed when the component is
  destroyed" (UUM-143650). With 9 scenes and 8 files referencing Light2D, that is the concrete
  measurable reason to prefer 6.6.2 over 6.6.1.
- Still open in 6.6.2 per its own Known Issues: UUM-150232 (our Bloom + 2D Renderer black screen; root
  fix announced for 6000.7.0b2 only) and UUM-149540 (`Resources.UnloadUnusedAssets()` slow in Player
  builds, 6000.5.0b2+).
- Also in 6.6.2 and relevant to our WebGL/IL2CPP path: WebGPU render_attachment usage flags
  (UUM-150892), cube-texture shaders (UUM-150746), batchmode builds with NameFilesAsHashes
  (UUM-149003), IL2CPP direct codegen for ldind/stind restored (UUM-129900).
- No new patches in the 6.5 branch: 6000.5.11f1 is still the top there, so the 6.5 lane is unchanged.
- Decision unchanged: install waits for the burn campaign to close. Within 6.6, prefer 6.6.2.

## 6.7 beta lane (6000.7.0b1, 2026-09-17)

Verified on 2026-09-18 (whats-new/6000.7.0b1 = HTTP 200; 6000.7.0b2 and 6000.5.12 = 404):

- UUM-150232 (our Bloom + 2D Renderer black screen) is now root-caused on the official page:
  "URP: Fixed the 2D Renderer creating a depth and stencil buffer in players when Disable Depth and
  Stencil Buffers is enabled in Player Settings" - and it is marked *Fixed in 6000.7.0b2*, so it is in
  no 6.5 and no 6.6 build. Exposure check on our project - both preconditions are absent:
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

## Re-evaluation trigger

After the burn campaign closes (deadline file `%LOCALAPPDATA%\Temp\grok-burn\campaign.deadline`):

1. Re-read `ProjectSettings/ProjectVersion.txt` and `Packages/manifest.json` (not this file).
2. Decide `6000.5.8f1` → `6000.6.1f1` on one axis only: does 6.6 fix anything we measure?
3. If yes: upgrade in a quiet window, re-bake shaders, re-run stance + walk baselines,
   compare against `Trials/Baselines/stable-pd/`, and only then let agents touch gameplay code.
4. Test a WebGL/WebGPU build against the current one for size and load time.
5. Then the 6.7 lane: our UUM-150232 fix ships in `6000.7.0b2` (17.09 b1 only carries the note), so
   wait for b2 before treating 6.7 as a fix for us.

## Note to H_Scout

The scout's stored project context claimed the project sits on the 6.6 channel.
It does not: we are on `6000.5.8f1`. Corrected in
`profiles/scout/state/unity2d-watch.json` on 2026-09-17 so the next digest does
not headline a version we are not on.
