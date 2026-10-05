using System;
using System.Collections.Generic;
using UnityEngine;

namespace StealthGame
{
    public enum ObjectiveType
    {
        Flag,           // cờ hội thoại (key)
        RelicCount,     // số Mảnh hồn >= target
        HasKey,         // có chìa khóa (key)
        Opened,         // cửa / phong ấn đã mở (key = id)
        BanishCount,    // số ma đã trục xuất >= target
        ShardCount,     // tổng mảnh vụn đã nhặt >= target
        Escaped,        // đã thoát khỏi dinh thự
    }

    [Serializable]
    public class QuestObjective
    {
        public string id;
        [Tooltip("Có thể dùng {0} = tiến độ hiện tại, {1} = mục tiêu.")]
        public string text;
        public ObjectiveType type;
        public string key;
        public int target = 1;
        public bool optional;
        [Tooltip("Chỉ hiện khi objective này đã xong (để trống = hiện ngay).")]
        public string requires;
        [Tooltip("Gợi ý hiển thị trong Nhật ký.")]
        [TextArea] public string hint;
    }

    /// <summary>Danh sách nhiệm vụ của màn chơi – chỉnh nội dung không cần sửa code.</summary>
    [CreateAssetMenu(menuName = "Stealth Game/Quest Definition")]
    public class QuestDefinition : ScriptableObject
    {
        public string title = "Thoát khỏi dinh thự";
        [TextArea(3, 8)] public string story;
        public List<QuestObjective> objectives = new();
    }
}
