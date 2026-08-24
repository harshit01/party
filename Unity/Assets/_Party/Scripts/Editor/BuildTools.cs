using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Party.EditorTools
{
    /// <summary>
    /// Player builds, driven from the command line so both machines get the same thing.
    ///
    ///     Unity -batchmode -quit -executeMethod Party.EditorTools.BuildTools.BuildMac
    ///     Unity -batchmode -quit -executeMethod Party.EditorTools.BuildTools.BuildWindows
    ///
    /// NOTE: Windows builds from this Mac are Mono, not IL2CPP. Unity does not offer a
    /// Windows IL2CPP module on a macOS host (IL2CPP needs MSVC), so the shipping IL2CPP
    /// build must be produced on the Windows machine. Mono is fine for playtest builds.
    /// </summary>
    public static class BuildTools
    {
        const string Scene = "Assets/_Party/Scenes/NetTest.unity";

        [MenuItem("Party/Build/macOS")]
        public static void BuildMac() => Run(BuildTarget.StandaloneOSX, "Build/Mac/Party.app");

        [MenuItem("Party/Build/Windows x64")]
        public static void BuildWindows() => Run(BuildTarget.StandaloneWindows64, "Build/Windows/Party.exe");

        static void Run(BuildTarget target, string outPath)
        {
            var opts = new BuildPlayerOptions
            {
                scenes           = new[] { Scene },
                locationPathName = outPath,
                target           = target,
                options          = BuildOptions.Development,
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary s = report.summary;

            Debug.Log($"[Build] {target} -> {s.result} ({s.totalSize / 1048576} MB, {s.totalTime.TotalSeconds:F0}s)");

            // Fail LOUDLY. Unity's batchmode exits 0 even when a build fails, which has
            // already bitten this project twice (silent -importPackage no-op, and
            // -executeMethod returning 0 through compile errors).
            if (s.result != BuildResult.Succeeded)
            {
                foreach (var step in report.steps)
                    foreach (var msg in step.messages)
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                            Debug.LogError($"[Build] {msg.content}");
                EditorApplication.Exit(1);
            }
        }
    }
}
