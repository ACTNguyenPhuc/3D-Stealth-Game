using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StealthGame.EditorTools
{
    /// <summary>Chụp ảnh bản đồ từ trên xuống (dùng cho thiết kế level & tài liệu).</summary>
    public static class MapCapture
    {
        public const float MinX = -34f, MaxX = 22f, MinZ = -14f, MaxZ = 20f;

        [MenuItem("Tools/Stealth Game/Debug/Capture Top-Down Map")]
        public static void CaptureMenu() => Capture("Assets/_3DStealthGame/Tutorial_Demo/Demo_Scenes/DemoScene.unity", "Temp/map.png");

        public static void CaptureBatch()
        {
            var args = System.Environment.GetCommandLineArgs();
            string scene = "Assets/_3DStealthGame/Tutorial_Demo/Demo_Scenes/DemoScene.unity", output = "Temp/map.png";
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-mapScene") scene = args[i + 1];
                if (args[i] == "-mapOut") output = args[i + 1];
            }
            Capture(scene, output);
        }

        public static void Capture(string scenePath, string output)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var go = new GameObject("MapCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            float w = MaxX - MinX, h = MaxZ - MinZ;
            cam.orthographicSize = h / 2f;
            cam.aspect = w / h;
            cam.transform.position = new Vector3((MinX + MaxX) / 2f, 30f, (MinZ + MaxZ) / 2f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            int pw = 1840, ph = Mathf.RoundToInt(pw * h / w);
            var rt = new RenderTexture(pw, ph, 24);
            cam.targetTexture = rt;
            // tắt các vật che phía trên
            foreach (var r in Object.FindObjectsByType<Renderer>())
                if (r.bounds.min.y > 2.4f) r.enabled = false;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(pw, ph, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, pw, ph), 0, 0);
            tex.Apply();
            File.WriteAllBytes(output, tex.EncodeToPNG());
            Debug.Log("[MapCapture] wrote " + output);

            var sb = new StringBuilder();
            foreach (var c in Object.FindObjectsByType<Collider>())
            {
                if (c.isTrigger) continue;
                var b = c.bounds;
                sb.AppendLine($"{GetPath(c.transform)}|{b.min.x:0.00}|{b.min.y:0.00}|{b.min.z:0.00}|{b.max.x:0.00}|{b.max.y:0.00}|{b.max.z:0.00}");
            }
            File.WriteAllText(Path.ChangeExtension(output, ".colliders.txt"), sb.ToString());
        }

        static string GetPath(Transform t) => t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;
    }
}
