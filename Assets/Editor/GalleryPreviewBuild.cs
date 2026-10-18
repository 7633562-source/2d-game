using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// A second player, for the art scenes only.
//
// Why not the trial player: Build/Headless/BalanceTrial.exe is single-scene by
// contract (the harness boots SampleScene and nothing else), so the ART-1
// catalogue gets its own small preview build:
//
//   Unity.exe -batchmode -nographics -projectPath <project>
//       -executeMethod GalleryPreviewBuild.Build -quit
//   Build/Preview/GalleryPreview.exe -batchmode -scene TreeApple -capture 1 ...
//
// SampleScene is not in this build, so no trial number can move.
public static class GalleryPreviewBuild
{
    private const string GalleryPath = "Assets/Scenes/Gallery.unity";
    private const string TreeApplePath = "Assets/Scenes/TreeApple.unity";
    // One species per scene (Yury 18.09): every other species scene is a clone of
    // TreeApple.unity written by Tools/species-scenes.py, which rewrites only the
    // three fields that name the species.
    //
    // Scene cull 27.09 (Yury: keep only what is really used): the nine species
    // scenes moved to <project>/_RemovedScenes_2026-09-27 (outside Assets) and are
    // not part of the build any more - a path in a build list that is not on disk
    // is what made the previous deploys exit(1). When Tools/species-scenes.py
    // recreates a species scene, add its path back here.
    private static readonly string[] SpeciesPaths = new string[0];
    // BALANCE-ARENA-1: the impact scene is editor-playable AND bootable in this
    // player, so "the scene runs" can be proved from a built player, not only from
    // the trial harness. It records into Trials/Runs from its own component.
    private const string BalanceArenaPath = "Assets/Scenes/BalanceArena.unity";
    // CLEARING-1: the first real location. Bootable here like BalanceArena, so
    // "the clearing runs" is proved from a built player, not the editor.
    private const string ClearingPath = "Assets/Scenes/Clearing.unity";

    public static void Build()
    {
        string folder = Arg("-buildOut", "Build/Preview");
        Directory.CreateDirectory(folder);
        string exePath = Path.Combine(folder, "GalleryPreview.exe");
        PlayerSettings.runInBackground = true;

        List<string> scenes = new List<string> { GalleryPath, TreeApplePath };
        scenes.AddRange(SpeciesPaths);
        scenes.Add(BalanceArenaPath);
        scenes.Add(ClearingPath);

        // A scene named here but absent on disk would silently cost a species its
        // scene: fail loudly instead, before the player is written.
        List<string> missing = scenes.FindAll(p => !File.Exists(p));
        if (missing.Count > 0)
        {
            Debug.LogError("PREVIEW_BUILD_FAILED missing scene(s): " + string.Join(", ", missing));
            EditorApplication.Exit(2);
            return;
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        bool ok = report.summary.result == BuildResult.Succeeded;
        Debug.Log((ok ? "PREVIEW_BUILD_OK " : "PREVIEW_BUILD_FAILED ") + exePath
            + " scenes=" + scenes.Count
            + " errors=" + report.summary.totalErrors + " bytes=" + report.summary.totalSize);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static string Arg(string key, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return fallback;
    }
}
