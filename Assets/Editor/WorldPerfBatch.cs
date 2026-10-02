#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batchmode: World + Play ~35 s; stop via WorldPerformanceMonitor.TryAutoStop.
public static class WorldPerfBatch
{
    private const string ScenePath = "Assets/Scenes/World.unity";
    private const float RunSeconds = 35f;

    public static void Run()
    {
        string fullPath = Path.Combine(Application.dataPath, "..", ScenePath);
        if (!File.Exists(fullPath))
        {
            Debug.LogError("WORLD_PERF_BATCH_FAIL scene missing: " + ScenePath);
            EditorApplication.Exit(1);
            return;
        }

        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        Application.runInBackground = true;
        // EnterPlayMode reloads the domain — static Pending* is cleared.
        SessionState.SetFloat("WorldPerf.AutoStop", RunSeconds);
        EditorPrefs.SetFloat("WorldPerf.AutoStop", RunSeconds);
        WorldPerformanceMonitor.PendingAutoStopSeconds = RunSeconds;

        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.playModeStateChanged += OnBatchPlayEnd;
        EditorApplication.isPlaying = true;
    }

    private static void OnBatchPlayEnd(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= OnBatchPlayEnd;
        EditorApplication.Exit(0);
    }
}
#endif
