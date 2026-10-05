using System.Globalization;
using System.IO;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace StealthGame.EditorTools
{
    /// <summary>Debug: bake NavMesh tạm và xuất tam giác + kiểm tra đường đi giữa các điểm.</summary>
    public static class LevelProbe
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_3DStealthGame/Tutorial_Demo/Demo_Scenes/DemoScene.unity", OpenSceneMode.Single);
            // bỏ qua NPC, ma, người chơi khi bake
            foreach (var n in new[] { "NPCs", "Enemies", "Player", "Camera System", "Dialogue System" })
            {
                var g = GameObject.Find(n);
                if (g) g.SetActive(false);
            }
            var go = GameObject.Find("Level A");
            var s = go.AddComponent<NavMeshSurface>();
            s.collectObjects = CollectObjects.Children;
            s.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            s.BuildNavMesh();
            var tri = NavMesh.CalculateTriangulation();
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            for (int i = 0; i < tri.indices.Length; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    var v = tri.vertices[tri.indices[i + k]];
                    sb.Append(v.x.ToString("0.00", inv)).Append(',').Append(v.z.ToString("0.00", inv)).Append(k < 2 ? ';' : '\n');
                }
            }
            File.WriteAllText("Temp/navmesh.txt", sb.ToString());

            var outp = new StringBuilder();
            var lines = File.ReadAllLines("Temp/probe_points.txt");
            var start = new Vector3(-9.39f, 0f, -5.73f);
            foreach (var l in lines)
            {
                var p = l.Split('|');
                var pos = new Vector3(float.Parse(p[1], inv), 0f, float.Parse(p[2], inv));
                bool onMesh = NavMesh.SamplePosition(pos, out var hit, 1.0f, NavMesh.AllAreas);
                var path = new NavMeshPath();
                bool ok = onMesh && NavMesh.CalculatePath(start, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                outp.AppendLine($"{p[0]}|onMesh={onMesh}|reach={ok}|y={(onMesh ? hit.position.y : 0):0.00}");
            }
            File.WriteAllText("Temp/probe_result.txt", outp.ToString());
            Debug.Log("[LevelProbe] done");
        }
    }
}
