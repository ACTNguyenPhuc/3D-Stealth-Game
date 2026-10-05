using System;
using System.Collections.Generic;
using UnityEngine;

namespace StealthGame
{
    // ---------------------------------------------------------------------
    //  Dữ liệu của cây hội thoại (Dialogue Tree).
    //
    //  Một cây gồm nhiều Node. Mỗi Node là một câu thoại của một nhân vật,
    //  kèm theo danh sách lựa chọn (Choice) dẫn tới Node khác => tạo thành
    //  một đồ thị có hướng (thường là cây, nhưng cho phép vòng lặp để quay
    //  lại menu câu hỏi).
    //
    //  - Condition: điều kiện để một lựa chọn / điểm bắt đầu khả dụng
    //    (ví dụ: đã có chìa khóa, đã nói chuyện với NPC khác...).
    //  - Action: hiệu ứng lên thế giới game khi vào Node / chọn Choice
    //    (đặt cờ, trao chìa khóa, làm chậm ma, báo động...).
    // ---------------------------------------------------------------------

    public enum DialogueConditionType
    {
        FlagSet,
        FlagNotSet,
        HasKey,
        NotHasKey,
        NodeVisited,      // key = id của node trong cùng cây
        NodeNotVisited,
        ShardsAtLeast,    // amount = số mảnh vụn tối thiểu
        RelicsAtLeast,    // amount = số Mảnh hồn tối thiểu
        HealthBelowMax,
    }

    [Serializable]
    public class DialogueCondition
    {
        public DialogueConditionType type;
        public string key;
        public int amount;

        public DialogueCondition() { }
        public DialogueCondition(DialogueConditionType type, string key, int amount = 0)
        {
            this.type = type;
            this.key = key;
            this.amount = amount;
        }

        public override string ToString() => amount > 0 ? $"{type}({amount})" : $"{type}({key})";
    }

    public enum DialogueActionType
    {
        SetFlag,
        ClearFlag,
        GiveKey,        // key = tên chìa khóa (khớp Door.KeyName), value không dùng
        SlowEnemies,    // value = hệ số tốc độ (0.5 = chậm một nửa), duration = số giây
        AlertEnemies,   // kết thúc hội thoại: báo động + người chơi mất máu
        OpenDoor,       // key = tên chìa khóa của cửa cần mở
        ShowMessage,    // key = nội dung thông báo
        SpendShards,    // value = số mảnh vụn trả
        GivePotions,    // value = số bình máu
        Heal,           // value = lượng máu
        RestoreEnergy,  // value = năng lượng đèn
        AddScore,       // value = điểm
    }

    [Serializable]
    public class DialogueAction
    {
        public DialogueActionType type;
        public string key;
        public float value = 1f;
        public float duration;

        public DialogueAction() { }
        public DialogueAction(DialogueActionType type, string key = "", float value = 1f, float duration = 0f)
        {
            this.type = type;
            this.key = key;
            this.value = value;
            this.duration = duration;
        }

        public override string ToString() => string.IsNullOrEmpty(key) ? type.ToString() : $"{type}({key})";
    }

    [Serializable]
    public class DialogueChoice
    {
        [TextArea(1, 3)] public string text;
        [Tooltip("Node tiếp theo. Để trống = kết thúc hội thoại.")]
        public string targetNodeId;
        public List<DialogueCondition> conditions = new();
        [Tooltip("Nếu điều kiện chưa thỏa: TRUE = vẫn hiện nhưng bị khóa, FALSE = ẩn hẳn.")]
        public bool showWhenLocked = true;
        [Tooltip("Gợi ý hiển thị khi lựa chọn bị khóa.")]
        public string lockedHint;
        public List<DialogueAction> actions = new();
    }

    [Serializable]
    public class DialogueNode
    {
        public string id;
        [Tooltip("Tên người nói. Để trống = tên NPC. Dùng {player} cho nhân vật chính.")]
        public string speaker;
        [TextArea(2, 6)] public string text;
        public List<DialogueAction> onEnter = new();
        public List<DialogueChoice> choices = new();
        [Tooltip("Dùng khi node KHÔNG có lựa chọn: node tự chuyển tiếp. Trống = kết thúc.")]
        public string nextNodeId;

        public bool IsEnd => choices.Count == 0 && string.IsNullOrEmpty(nextNodeId);
    }

    /// <summary>Điểm vào có điều kiện: NPC chào khác nhau tùy trạng thái game.</summary>
    [Serializable]
    public class DialogueEntry
    {
        public string nodeId;
        public List<DialogueCondition> conditions = new();
    }

    [CreateAssetMenu(menuName = "Stealth Game/Dialogue Tree", fileName = "NewDialogueTree")]
    public class DialogueTree : ScriptableObject
    {
        public string npcName = "NPC";
        [Tooltip("Các điểm vào được kiểm tra theo thứ tự, điểm đầu tiên thỏa điều kiện sẽ được dùng.")]
        public List<DialogueEntry> entries = new();
        [Tooltip("Node bắt đầu mặc định khi không có entry nào thỏa.")]
        public string startNodeId;
        public List<DialogueNode> nodes = new();

        Dictionary<string, DialogueNode> m_Lookup;

        void OnEnable() => m_Lookup = null;
        void OnValidate() => m_Lookup = null;

        public DialogueNode GetNode(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            if (m_Lookup == null)
            {
                m_Lookup = new Dictionary<string, DialogueNode>();
                foreach (var node in nodes)
                {
                    if (node != null && !string.IsNullOrEmpty(node.id))
                        m_Lookup[node.id] = node;
                }
            }

            m_Lookup.TryGetValue(id, out var result);
            return result;
        }

        public string ResolveStartNode(Func<DialogueCondition, bool> evaluate)
        {
            foreach (var entry in entries)
            {
                if (entry.conditions.TrueForAll(c => evaluate(c)))
                    return entry.nodeId;
            }
            return startNodeId;
        }

        /// <summary>Kiểm tra lỗi thiết kế: id trùng, liên kết hỏng, node không thể tới.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var ids = new HashSet<string>();

            foreach (var node in nodes)
            {
                if (string.IsNullOrEmpty(node.id))
                    problems.Add("Có node không có id.");
                else if (!ids.Add(node.id))
                    problems.Add($"Id bị trùng: '{node.id}'.");
            }

            void CheckLink(string from, string to)
            {
                if (!string.IsNullOrEmpty(to) && !ids.Contains(to))
                    problems.Add($"'{from}' trỏ tới node không tồn tại '{to}'.");
            }

            CheckLink("startNodeId", startNodeId);
            foreach (var entry in entries)
                CheckLink("entry", entry.nodeId);

            foreach (var node in nodes)
            {
                CheckLink(node.id, node.nextNodeId);
                foreach (var choice in node.choices)
                    CheckLink(node.id, choice.targetNodeId);
            }

            // Duyệt BFS từ các điểm vào để tìm node "mồ côi"
            var reachable = new HashSet<string>();
            var queue = new Queue<string>();
            void Enqueue(string id)
            {
                if (!string.IsNullOrEmpty(id) && ids.Contains(id) && reachable.Add(id))
                    queue.Enqueue(id);
            }

            Enqueue(startNodeId);
            foreach (var entry in entries)
                Enqueue(entry.nodeId);

            while (queue.Count > 0)
            {
                var node = GetNode(queue.Dequeue());
                if (node == null) continue;
                Enqueue(node.nextNodeId);
                foreach (var choice in node.choices)
                    Enqueue(choice.targetNodeId);
            }

            foreach (var id in ids)
            {
                if (!reachable.Contains(id))
                    problems.Add($"Node '{id}' không thể tới được từ điểm bắt đầu.");
            }

            return problems;
        }
    }
}
