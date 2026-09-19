using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene plumbing for the ART-1 gallery cycle.
//
//   GalleryBuild.CreateScenes        - create/fill Gallery.unity and TreeApple.unity, register both
//   GalleryBuild.CaptureGallery      - open Gallery.unity and play it (capture flags come from the command line)
//   GalleryBuild.CaptureTreeApple    - open TreeApple.unity and play it
//
// The capture methods do NOT use -quit: they hand the editor over to Play mode,
// and the scene's own capture component calls EditorApplication.Exit with the
// result. A watchdog exits 3 if that never happens.
public static class GalleryBuild
{
    public const string GalleryPath = "Assets/Scenes/Gallery.unity";
    public const string TreeApplePath = "Assets/Scenes/TreeApple.unity";
    private const string TreeTemplatePath = "Assets/Scenes/Tree.unity";

    private const float WatchdogSeconds = 300f;
    private static float playStartedAt;

    // ─── creation ─────────────────────────────────────────────────────────
    public static void CreateScenes()
    {
        CreateGalleryScene();
        CreateTreeAppleScene();
        RegisterScenes();
        Debug.Log("GALLERY-BUILD scenes created: " + GalleryPath + " and " + TreeApplePath);
        EditorApplication.Exit(0);
    }

    public static void CreateGalleryScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject camGo = new GameObject("Main Camera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.30f, 0.32f, 0.36f, 1f);
        camGo.transform.position = new Vector3(0f, 2f, -10f);
        camGo.tag = "MainCamera";

        GameObject manager = new GameObject("GalleryManager");
        manager.AddComponent<GalleryScene>();
        manager.AddComponent<GalleryCapture>();

        EditorSceneManager.MarkSceneDirty(scene);
        Directory.CreateDirectory(Path.GetDirectoryName(GalleryPath));
        EditorSceneManager.SaveScene(scene, GalleryPath);
        Debug.Log("GALLERY-BUILD wrote " + GalleryPath);
    }

    // Tree.unity is the plant gallery; this narrows the SAME spawn config to one
    // species. The original file is only read: SaveScene writes the new path.
    public static void CreateTreeAppleScene()
    {
        Scene tree = EditorSceneManager.OpenScene(TreeTemplatePath, OpenSceneMode.Single);

        GameProcess process = UnityEngine.Object.FindFirstObjectByType<GameProcess>();
        if (process == null)
        {
            Debug.LogError("GALLERY-BUILD FAIL " + TreeTemplatePath + " has no GameProcess");
            EditorApplication.Exit(1);
            return;
        }

        process.treeCount = 1;
        process.treeSpecies = "apple";
        process.treeAppleHeightM = 0f;      // the seed's own 2-5 m
        process.treeSeed = -1;
        process.treeOffsetX = 0f;
        process.treeSpacing = 0f;
        process.cameraFollowsTree = false;  // SpeciesScene owns the frame
        process.flowerCount = 0;
        process.birdCount = 0;
        process.dogCount = 0;
        process.humanCount = 0;
        process.spawnCampfire = false;
        process.enablePlayCapture = false;

        GameObject host = new GameObject("SpeciesScene");
        SpeciesScene species = host.AddComponent<SpeciesScene>();
        species.speciesName = "apple";
        species.label = "treeapple";

        EditorSceneManager.MarkSceneDirty(tree);
        EditorSceneManager.SaveScene(tree, TreeApplePath);
        Debug.Log("GALLERY-BUILD wrote " + TreeApplePath + " (treeCount=1, species=apple)");
    }

    public static void RegisterScenes()
    {
        // Species scenes beyond the apple are cloned from TreeApple.unity by
        // Tools/species-scenes.py, which registers them itself; this list keeps a
        // CreateScenes() re-run from dropping them.
        string[] wanted = { GalleryPath, TreeApplePath,
            "Assets/Scenes/TreeOak.unity", "Assets/Scenes/TreePine.unity" };
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < wanted.Length; i++)
        {
            bool present = false;
            for (int j = 0; j < list.Count; j++)
                present |= list[j].path == wanted[i];
            if (!present)
                list.Add(new EditorBuildSettingsScene(wanted[i], true));
        }
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log("GALLERY-BUILD build settings scenes: " + EditorBuildSettings.scenes.Length);
        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            Debug.Log("  " + (EditorBuildSettings.scenes[i].enabled ? "on  " : "off ")
                + EditorBuildSettings.scenes[i].path);
    }

    // ─── play-mode capture ────────────────────────────────────────────────
    public static void CaptureGallery()
    {
        PlayAndExit(GalleryPath);
    }

    public static void CaptureTreeApple()
    {
        PlayAndExit(TreeApplePath);
    }

    private static void PlayAndExit(string scenePath)
    {
        if (!File.Exists(scenePath))
        {
            Debug.LogError("GALLERY-BUILD FAIL no scene at " + scenePath);
            EditorApplication.Exit(1);
            return;
        }

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        WriteSidecar(scenePath);
        playStartedAt = (float)EditorApplication.timeSinceStartup;
        EditorApplication.update += Watchdog;
        EditorApplication.isPlaying = true;
        Debug.Log("GALLERY-BUILD playing " + scenePath);
    }

    // The command line of the frame producer, in the log next to the frames.
    private static void WriteSidecar(string scenePath)
    {
        Debug.Log("GALLERY-BUILD scene=" + scenePath + " unity=" + Application.unityVersion
            + " argv=" + string.Join(" ", Environment.GetCommandLineArgs()));
    }

    private static void Watchdog()
    {
        if (EditorApplication.timeSinceStartup - playStartedAt < WatchdogSeconds)
            return;
        Debug.LogError("GALLERY-BUILD FAIL capture did not finish within " + WatchdogSeconds + " s");
        EditorApplication.update -= Watchdog;
        EditorApplication.Exit(3);
    }
}
