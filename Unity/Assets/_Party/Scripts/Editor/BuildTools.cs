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

        /// <summary>
        /// Rebuild the scene AND build the player in ONE Unity invocation.
        ///
        /// Doing them as two separate launches produced an intermittently corrupt level0 -
        /// identical inputs gave corrupt/clean/corrupt across three cycles. The likely
        /// mechanism is the second process building against a scene the asset database
        /// has not finished settling after the first process saved it. One invocation
        /// removes the hand-off entirely.
        /// </summary>
        public static void RebuildAndBuildRedLight()
        {
            RedLightSetup.Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Run(BuildTarget.StandaloneOSX, "Build/MacRedLight/Party.app",
                "Assets/_Party/Scenes/RedLight.unity");
        }

        [MenuItem("Party/Build/macOS")]
        public static void BuildMac() => Run(BuildTarget.StandaloneOSX, "Build/Mac/Party.app");

        /// <summary>Red Light scene only - used by the headless round test.</summary>
        public static void BuildMacRedLight() =>
            Run(BuildTarget.StandaloneOSX, "Build/MacRedLight/Party.app",
                "Assets/_Party/Scenes/RedLight.unity");

        /// <summary>Say What He Says (#10) only - used by its headless round test.</summary>
        public static void BuildMacSayWhat() =>
            Run(BuildTarget.StandaloneOSX, "Build/MacSayWhat/Party.app",
                "Assets/_Party/Scenes/SayWhat.unity");

        [MenuItem("Party/Build/Windows x64")]
        public static void BuildWindows() => Run(BuildTarget.StandaloneWindows64, "Build/Windows/Party.exe");

        static void CopyAppId(string outPath, BuildTarget target)
        {
            const string src = "steam_appid.txt";
            if (!System.IO.File.Exists(src))
            {
                Debug.LogError("[Build] steam_appid.txt missing from the project root.");
                return;
            }

            // On macOS the player lives inside the .app bundle.
            string dir = target == BuildTarget.StandaloneOSX
                ? System.IO.Path.Combine(outPath, "Contents", "MacOS")
                : System.IO.Path.GetDirectoryName(outPath);

            if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir))
            {
                Debug.LogError($"[Build] cannot place steam_appid.txt - no directory at {dir}");
                return;
            }

            string dest = System.IO.Path.Combine(dir, "steam_appid.txt");
            System.IO.File.Copy(src, dest, true);
            Debug.Log($"[Build] steam_appid.txt -> {dest}");
        }

        static void Run(BuildTarget target, string outPath, string sceneOverride = null)
        {
            var opts = new BuildPlayerOptions
            {
                scenes           = new[] { sceneOverride ?? Scene },
                locationPathName = outPath,
                target           = target,
                // NOT a development build by default.
                //
                // BuildOptions.Development draws Unity's "Development Build" watermark and
                // the Development Console bar across the bottom of the screen - both were
                // burned into the captured frame the founder called bad, and neither is
                // part of the game. Logging via -logFile works identically without it, so
                // the regression suite is unaffected. Set PARTY_DEV_BUILD=1 to get the
                // profiler and the console back when actually debugging a player.
                options          = System.Environment.GetEnvironmentVariable("PARTY_DEV_BUILD") == "1"
                                   ? BuildOptions.Development : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary s = report.summary;

            // Steam looks for steam_appid.txt NEXT TO THE EXECUTABLE. Without it
            // SteamAPI.Init() returns false and every lobby silently does nothing -
            // so copy it as part of the build rather than leaving it to be forgotten.
            if (s.result == BuildResult.Succeeded) CopyAppId(outPath, target);

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
