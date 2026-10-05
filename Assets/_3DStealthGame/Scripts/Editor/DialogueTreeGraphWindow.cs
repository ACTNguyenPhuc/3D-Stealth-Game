using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StealthGame.EditorTools
{
    /// <summary>
    /// Cửa sổ xem trực quan cây hội thoại dưới dạng đồ thị (node + mũi tên).
    /// Bố cục tự động theo độ sâu BFS từ node bắt đầu. Kéo chuột để di chuyển khung nhìn.
    /// Tools > Stealth Demo > Dialogue Tree Viewer, hoặc nút "Mở đồ thị" trong Inspector.
    /// </summary>
    public class DialogueTreeGraphWindow : EditorWindow
    {
        const float k_NodeWidth = 260f;
        const float k_ColumnGap = 90f;
        const float k_RowGap = 24f;
        const float k_LineHeight = 18f;

        DialogueTree m_Tree;
        Vector2 m_Pan = new(30f, 40f);
        readonly Dictionary<string, Rect> m_Rects = new();
        readonly Dictionary<string, int> m_Depth = new();
        string m_Selected;

        static readonly Color k_Start = new(0.25f, 0.6f, 0.3f);
        static readonly Color k_End = new(0.6f, 0.25f, 0.25f);
        static readonly Color k_Normal = new(0.22f, 0.22f, 0.27f);
        static readonly Color k_Player = new(0.2f, 0.35f, 0.55f);

        [MenuItem("Tools/Stealth Demo/Dialogue Tree Viewer", priority = 40)]
        public static void OpenEmpty() => Open(Selection.activeObject as DialogueTree);

        public static void Open(DialogueTree tree)
        {
            var window = GetWindow<DialogueTreeGraphWindow>("Dialogue Tree");
            window.m_Tree = tree;
            window.m_Pan = new Vector2(30f, 40f);
            window.Repaint();
        }

        void OnSelectionChange()
        {
            if (Selection.activeObject is DialogueTree tree && tree != m_Tree)
            {
                m_Tree = tree;
                Repaint();
            }
        }

        void OnGUI()
        {
            DrawToolbar();
            if (m_Tree == null)
            {
                EditorGUILayout.HelpBox("Chọn một DialogueTree asset (Project window) để xem đồ thị.", MessageType.Info);
                return;
            }

            Layout();

            var area = new Rect(0, 22, position.width, position.height - 22);
            EditorGUI.DrawRect(area, new Color(0.13f, 0.13f, 0.15f));
            GUI.BeginClip(area);
            DrawEdges();
            DrawNodes();
            GUI.EndClip();

            HandlePan(area);
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            m_Tree = (DialogueTree)EditorGUILayout.ObjectField(m_Tree, typeof(DialogueTree), false, GUILayout.Width(260));
            if (GUILayout.Button("Về giữa", EditorStyles.toolbarButton, GUILayout.Width(70)))
                m_Pan = new Vector2(30f, 40f);
            if (m_Tree != null && GUILayout.Button("Kiểm tra lỗi", EditorStyles.toolbarButton, GUILayout.Width(90)))
                DialogueTreeInspector.LogValidation(m_Tree);
            GUILayout.FlexibleSpace();
            GUILayout.Label("■ Bắt đầu  ■ Kết thúc  ■ Người chơi nói   — [?] / mũi tên cam: lựa chọn có điều kiện", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ Layout

        static IEnumerable<string> Targets(DialogueNode n)
        {
            if (!string.IsNullOrEmpty(n.nextNodeId)) yield return n.nextNodeId;
            foreach (var c in n.choices)
                if (!string.IsNullOrEmpty(c.targetNodeId)) yield return c.targetNodeId;
        }

        void Layout()
        {
            m_Rects.Clear();
            m_Depth.Clear();

            var queue = new Queue<string>();
            void Seed(string id)
            {
                if (!string.IsNullOrEmpty(id) && m_Tree.GetNode(id) != null && !m_Depth.ContainsKey(id))
                {
                    m_Depth[id] = 0;
                    queue.Enqueue(id);
                }
            }

            Seed(m_Tree.startNodeId);
            foreach (var e in m_Tree.entries) Seed(e.nodeId);

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                var node = m_Tree.GetNode(id);
                foreach (var target in Targets(node))
                {
                    if (m_Tree.GetNode(target) == null || m_Depth.ContainsKey(target)) continue;
                    m_Depth[target] = m_Depth[id] + 1;
                    queue.Enqueue(target);
                }
            }

            // Node không tới được: đặt ở cột cuối
            int maxDepth = m_Depth.Count > 0 ? m_Depth.Values.Max() : 0;
            foreach (var n in m_Tree.nodes)
                if (!string.IsNullOrEmpty(n.id) && !m_Depth.ContainsKey(n.id))
                    m_Depth[n.id] = maxDepth + 1;

            var columnY = new Dictionary<int, float>();
            foreach (var node in m_Tree.nodes)
            {
                if (string.IsNullOrEmpty(node.id) || m_Rects.ContainsKey(node.id)) continue;
                int d = m_Depth[node.id];
                columnY.TryGetValue(d, out float y);
                float height = NodeHeight(node);
                m_Rects[node.id] = new Rect(m_Pan.x + d * (k_NodeWidth + k_ColumnGap), m_Pan.y + y, k_NodeWidth, height);
                columnY[d] = y + height + k_RowGap;
            }
        }

        static float NodeHeight(DialogueNode n)
        {
            int textLines = Mathf.Clamp(Mathf.CeilToInt((n.text?.Length ?? 0) / 38f), 1, 4);
            return 22f + textLines * 15f + 8f + n.choices.Count * k_LineHeight + (n.onEnter.Count > 0 ? k_LineHeight : 0f) + 8f;
        }

        // ------------------------------------------------------------------ Drawing

        void DrawEdges()
        {
            foreach (var node in m_Tree.nodes)
            {
                if (string.IsNullOrEmpty(node.id) || !m_Rects.TryGetValue(node.id, out var from)) continue;

                if (!string.IsNullOrEmpty(node.nextNodeId))
                    DrawEdge(new Vector2(from.xMax, from.y + 14f), node.nextNodeId, new Color(0.8f, 0.8f, 0.8f));

                float choiceStart = ChoiceStartY(node, from);
                for (int i = 0; i < node.choices.Count; i++)
                {
                    var c = node.choices[i];
                    if (string.IsNullOrEmpty(c.targetNodeId)) continue;
                    var color = c.conditions.Count > 0 ? new Color(1f, 0.6f, 0.2f) : new Color(0.55f, 0.8f, 1f);
                    DrawEdge(new Vector2(from.xMax, choiceStart + i * k_LineHeight + k_LineHeight * 0.5f), c.targetNodeId, color);
                }
            }
        }

        void DrawEdge(Vector2 start, string targetId, Color color)
        {
            if (!m_Rects.TryGetValue(targetId, out var to)) return;
            var end = new Vector2(to.xMin, to.y + 14f);
            float tangent = Mathf.Max(50f, Mathf.Abs(end.x - start.x) * 0.5f);
            bool backwards = end.x <= start.x;
            if (backwards) color.a = 0.55f;  // vòng lặp quay lại
            Handles.DrawBezier(start, end, start + Vector2.right * tangent, end + Vector2.left * tangent, color, null, 2.5f);
            var arrow = new Rect(end.x - 7f, end.y - 4f, 8f, 8f);
            EditorGUI.DrawRect(arrow, color);
        }

        float ChoiceStartY(DialogueNode node, Rect r)
        {
            int textLines = Mathf.Clamp(Mathf.CeilToInt((node.text?.Length ?? 0) / 38f), 1, 4);
            return r.y + 22f + textLines * 15f + 8f + (node.onEnter.Count > 0 ? k_LineHeight : 0f);
        }

        void DrawNodes()
        {
            var title = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
            var body = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } };
            var choice = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.75f, 0.9f, 1f) }, clipping = TextClipping.Clip };
            var action = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.85f, 0.4f) }, clipping = TextClipping.Clip };

            var entryIds = new HashSet<string>(m_Tree.entries.Select(e => e.nodeId)) { m_Tree.startNodeId };

            foreach (var node in m_Tree.nodes)
            {
                if (string.IsNullOrEmpty(node.id) || !m_Rects.TryGetValue(node.id, out var r)) continue;

                Color header = node.speaker == "{player}" ? k_Player : node.IsEnd ? k_End : entryIds.Contains(node.id) ? k_Start : k_Normal;
                EditorGUI.DrawRect(r, new Color(0.18f, 0.18f, 0.21f));
                EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 20f), header);
                if (m_Selected == node.id)
                    DrawOutline(r, Color.yellow);

                string speaker = string.IsNullOrEmpty(node.speaker) ? m_Tree.npcName : node.speaker;
                GUI.Label(new Rect(r.x + 4, r.y + 1, r.width - 8, 18), $"{node.id}   ({speaker})", title);

                float y = r.y + 22f;
                int textLines = Mathf.Clamp(Mathf.CeilToInt((node.text?.Length ?? 0) / 38f), 1, 4);
                GUI.Label(new Rect(r.x + 4, y, r.width - 8, textLines * 15f + 4f), node.text, body);
                y += textLines * 15f + 8f;

                if (node.onEnter.Count > 0)
                {
                    GUI.Label(new Rect(r.x + 4, y, r.width - 8, k_LineHeight), "On enter: " + string.Join(", ", node.onEnter), action);
                    y += k_LineHeight;
                }

                for (int i = 0; i < node.choices.Count; i++)
                {
                    var c = node.choices[i];
                    string label = $"{i + 1}. {c.text}";
                    if (c.conditions.Count > 0) label = "[?] " + label;
                    if (string.IsNullOrEmpty(c.targetNodeId)) label += "  [END]";
                    GUI.Label(new Rect(r.x + 4, y, r.width - 8, k_LineHeight), new GUIContent(label, Tooltip(c)), choice);
                    y += k_LineHeight;
                }

                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    m_Selected = node.id;
                    Repaint();
                }
            }
        }

        static string Tooltip(DialogueChoice c)
        {
            var parts = new List<string> { "→ " + (string.IsNullOrEmpty(c.targetNodeId) ? "kết thúc" : c.targetNodeId) };
            if (c.conditions.Count > 0) parts.Add("Điều kiện: " + string.Join(" AND ", c.conditions));
            if (c.actions.Count > 0) parts.Add("Hành động: " + string.Join(", ", c.actions));
            return string.Join("\n", parts);
        }

        static void DrawOutline(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, 2), c);
            EditorGUI.DrawRect(new Rect(r.x - 2, r.yMax, r.width + 4, 2), c);
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y, 2, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax, r.y, 2, r.height), c);
        }

        void HandlePan(Rect area)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDrag && area.Contains(e.mousePosition) && (e.button == 0 || e.button == 2))
            {
                m_Pan += e.delta;
                e.Use();
                Repaint();
            }
        }
    }
}
