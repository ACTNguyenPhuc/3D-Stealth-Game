using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace StealthGame.EditorTools
{
    public static partial class GameBuilder
    {
        static void BuildMenuScene(BuiltAssets a)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            a = LoadBuilt();

            // Dinh thự làm phông nền
            var level = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Root + "/Prefabs/Levels/Level A.prefab"));
            level.transform.position = new Vector3(6.049f, 0f, 8.02f);
            DarkenOutside(level);

            // Ánh trăng + ánh sáng môi trường + sương mù
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 1f);
            moon.intensity = 0.9f;
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.04f, 0.04f, 0.09f);
            RenderSettings.fogDensity = 0.025f;

            var candles = new GameObject("Candle Lights").transform;
            foreach (var p in new[] { new Vector3(-9f, 1.2f, -5f), new Vector3(-4.5f, 1.2f, -3.5f), new Vector3(5f, 1.5f, 9f), new Vector3(12f, 1.2f, 1.5f), new Vector3(-1.5f, 1.2f, 2f) })
                AddLight(candles, "Candle", p, LightType.Point, new Color(1f, 0.6f, 0.3f), 6f, 2.5f);

            // Vài hồn ma lơ lửng cho không khí
            var ghostModel = Load<GameObject>(Root + "/Art/Models/Characters/Enemies/EyeGhost_Model.fbx");
            var ghostCtrl = Load<RuntimeAnimatorController>(Root + "/Tutorial_Demo/Demo_Animators/Demo_Ghost.controller");
            var ghostMat = Load<Material>(Root + "/Art/Materials/Characters/EyeGhost.mat");
            foreach (var (pos, yaw) in new[] { (new Vector3(-1.7f, 0f, 0.5f), 30f), (new Vector3(5f, 0f, 3f), 200f), (new Vector3(-9f, 0f, 1f), 120f) })
            {
                if (ghostModel == null) break;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(ghostModel);
                g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                var anim = GetOrAdd<Animator>(g);
                anim.runtimeAnimatorController = ghostCtrl;
                if (ghostMat != null)
                    foreach (var r in g.GetComponentsInChildren<Renderer>()) r.sharedMaterial = ghostMat;
            }

            // Camera bay quanh + hậu kỳ
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            cam.fieldOfView = 50f;
            cam.farClipPlane = 200f;
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            camGo.AddComponent<MenuCameraOrbit>();

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = Load<VolumeProfile>(Root + "/Art/PostProcessing/Scary Volume Profile.asset");

            var ui = new GameObject("Menu UI");
            var doc = ui.AddComponent<UIDocument>();
            doc.panelSettings = a.panelSettings;
            doc.sortingOrder = 10;
            ui.AddComponent<MainMenuUI>();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            Debug.Log("[GameBuilder] Scene menu chính đã dựng xong.");
        }

        static void ConfigureProject()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };
            PlayerSettings.productName = "Lemon's Escape";
            PlayerSettings.companyName = "KMALearn";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
        }
    }
}
