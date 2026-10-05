using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StealthGame.EditorTools
{
    /// <summary>
    /// Dựng toàn bộ game từ code (chạy lại được nhiều lần):
    ///   1. Tài nguyên: vật liệu, hiệu ứng hạt, prefab vật phẩm / kẻ địch, thư viện âm thanh, theme UI, nhiệm vụ, hội thoại.
    ///   2. Scene gameplay (DemoScene): người chơi, camera, AI, NavMesh, vật phẩm, checkpoint, UI.
    ///   3. Scene menu chính.
    ///   4. Build Settings + Player Settings, build bản Windows.
    ///
    /// Menu: Tools ▸ Stealth Game ▸ ...   |   Batch: -executeMethod StealthGame.EditorTools.GameBuilder.BatchBuildAll
    /// </summary>
    public static partial class GameBuilder
    {
        const string Root = "Assets/_3DStealthGame";
        const string Gen = Root + "/Generated";
        const string GenMat = Gen + "/Materials";
        const string GenVfx = Gen + "/VFX";
        const string GenPrefab = Gen + "/Prefabs";
        const string GenData = Gen + "/Data";
        const string ResourcesDir = Root + "/Resources";
        const string GameScenePath = Root + "/Tutorial_Demo/Demo_Scenes/DemoScene.unity";
        const string MenuScenePath = Root + "/Scenes/MainMenu.unity";
        const string BuildPath = "Builds/Windows/LemonsEscape.exe";

        [MenuItem("Tools/Stealth Game/1. Build Everything (Assets + Scenes)", priority = 0)]
        public static void BuildEverything()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                AssetDatabase.StartAssetEditing();
                EnsureFolders();
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();

            ConfigureImporters();
            var assets = BuildAssets();
            BuildGameScene(assets);
            BuildMenuScene(assets);
            ConfigureProject();
            AssetDatabase.SaveAssets();
            Debug.Log("[GameBuilder] Hoàn tất dựng game. Mở scene MainMenu và bấm Play, hoặc dùng menu Build Windows Player.");
        }

        [MenuItem("Tools/Stealth Game/2. Build Windows Player", priority = 1)]
        public static void BuildPlayerMenu() => BuildPlayer();

        // ------------------------------------------------------------------ Batch

        public static void BatchBuildAll()
        {
            int code = 0;
            try
            {
                BuildEverything();
                if (!Array.Exists(Environment.GetCommandLineArgs(), a => a == "-skipPlayer"))
                    code = BuildPlayer() ? 0 : 2;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        public static void BatchBuildPlayerOnly()
        {
            int code;
            try { code = BuildPlayer() ? 0 : 2; }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static bool BuildPlayer()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MenuScenePath, GameScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[GameBuilder] Build result: {report.summary.result}, size {report.summary.totalSize / (1024 * 1024)} MB, errors {report.summary.totalErrors}");
            return report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        }

        // ------------------------------------------------------------------ Tiện ích

        static void EnsureFolders()
        {
            foreach (var f in new[] { Gen, GenMat, GenVfx, GenPrefab, GenData, ResourcesDir, Root + "/Scenes", Root + "/Dialogue" })
                EnsureFolder(f);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogWarning("[GameBuilder] Thiếu asset: " + path);
            return a;
        }

        static GameObject SavePrefab(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject Child(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            return go;
        }

        static Light AddLight(Transform parent, string name, Vector3 localPos, LightType type, Color color, float range, float intensity)
        {
            var go = Child(parent, name, localPos);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }
    }
}
