using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Creates a URP Asset with a 2D Renderer and assigns it in Graphics/Quality.
// Without this Light2D does not exist in the Built-in pipeline.
[InitializeOnLoad]
public static class LightingPipelineSetup
{
    private const string Folder = "Assets/Settings";
    private const string RendererPath = "Assets/Settings/Renderer2D.asset";
    private const string PipelinePath = "Assets/Settings/URP-2D.asset";

    static LightingPipelineSetup()
    {
        EditorApplication.delayCall += Ensure;
    }

    [MenuItem("2D Game/Ensure URP 2D Lighting")]
    public static void EnsureMenu()
    {
        Ensure();
    }

    private static void Ensure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            AssetDatabase.CreateFolder("Assets", "Settings");

        Renderer2DData renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<Renderer2DData>();
            AssetDatabase.CreateAsset(renderer, RendererPath);
            SerializedObject so = new SerializedObject(renderer);
            so.FindProperty("m_HDREmulationScale").floatValue = 2f;
            so.FindProperty("m_LightRenderTextureScale").floatValue = 0.5f;
            SerializedProperty depth = so.FindProperty("m_UseDepthStencilBuffer");
            if (depth != null)
                depth.boolValue = false;
            SerializedProperty lightRt = so.FindProperty("m_MaxLightRenderTextureCount");
            if (lightRt != null)
                lightRt.intValue = 4;
            SerializedProperty shadowRt = so.FindProperty("m_MaxShadowRenderTextureCount");
            if (shadowRt != null)
                shadowRt.intValue = 0;
            SerializedProperty pp = so.FindProperty("m_PostProcessData");
            if (pp != null)
            {
                pp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            ResourceReloader.TryReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
        }

        UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsHDR = true;
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
        }

        if (GraphicsSettings.defaultRenderPipeline != pipeline)
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            Debug.Log("URP 2D Renderer assigned in GraphicsSettings.");
        }
    }
}
