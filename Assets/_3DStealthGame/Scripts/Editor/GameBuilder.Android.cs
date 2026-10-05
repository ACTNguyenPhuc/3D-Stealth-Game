using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace StealthGame.EditorTools
{
    /// <summary>Cấu hình & build bản Android (APK, IL2CPP ARM64, màn hình ngang).</summary>
    public static partial class GameBuilder
    {
        public const string AndroidPackage = "com.kmalearn.lemonsescape";
        const string ApkPath = "Builds/Android/LemonsEscape.apk";

        [MenuItem("Tools/Stealth Game/3. Build Android APK", priority = 2)]
        public static void BuildAndroidMenu() => BuildAndroid();

        /// <summary>Batch: -executeMethod StealthGame.EditorTools.GameBuilder.BatchBuildAndroid [-rebuild]</summary>
        public static void BatchBuildAndroid()
        {
            int code;
            try
            {
                if (Array.Exists(Environment.GetCommandLineArgs(), a => a == "-rebuild"))
                    BuildEverything();
                code = BuildAndroid() ? 0 : 2;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        static void ConfigureAndroid()
        {
            var t = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(t, AndroidPackage);
            PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(t, ManagedStrippingLevel.Minimal);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.renderOutsideSafeArea = true;   // UI tự chừa vùng tai thỏ (TouchControls)
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;

            // Chỉ chơi màn hình ngang
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            EditorUserBuildSettings.buildAppBundle = false;   // xuất APK để cài trực tiếp
        }

        public static bool BuildAndroid()
        {
            ConfigureAndroid();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MenuScenePath, GameScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            Debug.Log($"[GameBuilder] Android build: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, errors {report.summary.totalErrors}");
            return report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        }
    }
}
