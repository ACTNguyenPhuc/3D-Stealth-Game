using UnityEditor;
using UnityEngine;

namespace StealthGame.EditorTools
{
    [CustomEditor(typeof(DialogueTree))]
    public class DialogueTreeInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            var tree = (DialogueTree)target;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Mở đồ thị", GUILayout.Height(26)))
                DialogueTreeGraphWindow.Open(tree);
            if (GUILayout.Button("Kiểm tra lỗi", GUILayout.Height(26)))
                LogValidation(tree);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox($"{tree.nodes.Count} node · {tree.entries.Count} điểm vào có điều kiện", MessageType.None);
            EditorGUILayout.Space();

            DrawDefaultInspector();
        }

        public static void LogValidation(DialogueTree tree)
        {
            var problems = tree.Validate();
            if (problems.Count == 0)
                Debug.Log($"[Dialogue] '{tree.name}': không có lỗi ({tree.nodes.Count} node).", tree);
            else
                foreach (var p in problems)
                    Debug.LogWarning($"[Dialogue] '{tree.name}': {p}", tree);
        }
    }
}
