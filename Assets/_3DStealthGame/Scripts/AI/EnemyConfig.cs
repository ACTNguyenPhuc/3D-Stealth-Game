using UnityEngine;

namespace StealthGame
{
    /// <summary>Thông số một loại kẻ địch – tạo nhiều loại (Ma thường, Oan hồn...) chỉ bằng dữ liệu.</summary>
    [CreateAssetMenu(menuName = "Stealth Game/Enemy Config")]
    public class EnemyConfig : ScriptableObject
    {
        public string displayName = "Hồn ma";

        [Header("Máu & phần thưởng")]
        public float maxHealth = 100f;
        public int scoreValue = 200;
        public int shardDrop = 2;
        public GameObject dropPrefab;

        [Header("Di chuyển")]
        public float patrolSpeed = 1.1f;
        public float chaseSpeed = 2.6f;
        public float waitAtWaypoint = 1.5f;

        [Header("Giác quan")]
        public float visionRange = 7f;
        [Range(10f, 180f)] public float visionAngle = 100f;
        [Tooltip("Tốc độ tăng mức cảnh giác mỗi giây khi thấy người chơi.")]
        public float detectionRate = 1.4f;
        public float awarenessDecay = 0.35f;
        public float hearingMultiplier = 1f;
        public float loseSightTime = 4f;
        public float investigateTime = 4f;

        [Header("Tấn công")]
        public float attackRange = 1.25f;
        public float attackDamage = 20f;
        public float attackWindup = 0.45f;
        public float attackCooldown = 1.3f;

        [Header("Hiển thị")]
        public Color tint = Color.white;
        public float modelScale = 1f;
    }
}
