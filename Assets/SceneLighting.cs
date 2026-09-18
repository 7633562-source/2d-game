using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Night base: dim Global Light 2D (moon) + Bloom.
// The torch is a separate Point Light2D, not this class.
public static class SceneLighting
{
    public static readonly Color Moon = new Color(0.42f, 0.52f, 0.78f, 1f);
    public static readonly Color NightSky = new Color(0.07f, 0.09f, 0.16f, 1f);

    // Temporary: look at flat color boxes without moon, torch, or Bloom.
    // Lit without Light2D gives a black frame — SpriteLighting then hangs Unlit.
    public static bool Enabled = false;

    public static void Ensure(Camera camera)
    {
        Apply2DQuality();
        if (!Enabled)
        {
            if (camera != null)
            {
                camera.backgroundColor = NightSky;
                camera.allowHDR = false;
            }
            return;
        }
        EnsureGlobalMoon();
        DimSky();
        if (camera != null)
            SetupCamera(camera);
    }

    // 3D Ultra quality (shadows, pixel lights) is unused by 2D Light2D,
    // but atlases and GI are still computed. Do not touch VSync: without it
    // FPS flies off and the frame measurement lies.
    public static void Apply2DQuality()
    {
        QualitySettings.pixelLightCount = 0;
        QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.softParticles = false;
        QualitySettings.realtimeReflectionProbes = false;
    }

    private static void EnsureGlobalMoon()
    {
        if (GameObject.Find("GlobalMoon") != null)
            return;

        GameObject go = new GameObject("GlobalMoon");
        Light2D light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.color = Moon;
        light.intensity = 0.22f;
        light.blendStyleIndex = 0;
        light.shadowsEnabled = false;
    }

    private static void DimSky()
    {
        GameObject sky = GameObject.Find("Sky");
        if (sky == null)
            return;
        SpriteRenderer sr = sky.GetComponent<SpriteRenderer>();
        if (sr == null)
            return;
        SpriteLighting.ApplyUnlit(sr);
        sr.color = NightSky;
    }

    private static void SetupCamera(Camera camera)
    {
        camera.backgroundColor = NightSky;
        camera.allowHDR = true;
        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        EnsureBloomVolume();
    }

    private static void EnsureBloomVolume()
    {
        if (GameObject.Find("LightingVolume") != null)
            return;

        GameObject go = new GameObject("LightingVolume");
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "NightBloom";
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.01f);
        bloom.threshold.Override(1.4f);
        bloom.scatter.Override(0.7f);
        volume.sharedProfile = profile;
    }
}
