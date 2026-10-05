using System.Collections.Generic;
using UnityEngine;

namespace StealthGame
{
    /// <summary>Ngọn nến linh hồn: đi qua để thắp → lưu game, hồi một ít máu. Chết sẽ hồi sinh tại đây.</summary>
    public class Checkpoint : MonoBehaviour
    {
        public static readonly List<Checkpoint> All = new();

        public string checkpointId = "cp_start";
        public string displayName = "Phòng ngủ";
        public Transform spawnPoint;
        public GameObject flame;
        public Light flameLight;
        public float healAmount = 25f;

        public bool IsActive { get; private set; }
        public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            bool active = GameSession.Instance != null && GameSession.Instance.IsCheckpointActive(checkpointId);
            SetVisual(active);
            IsActive = active;
        }

        void SetVisual(bool on)
        {
            if (flame != null) flame.SetActive(on);
            if (flameLight != null) flameLight.enabled = on;
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsActive) return;
            var stats = other.GetComponentInParent<PlayerStats>();
            if (stats == null || !stats.IsAlive) return;
            Activate(stats, true);
        }

        public void Activate(PlayerStats stats, bool withFeedback)
        {
            IsActive = true;
            SetVisual(true);
            if (withFeedback)
            {
                VFXManager.Play(VFXId.Checkpoint, transform.position + Vector3.up * 0.8f);
                AudioManager.SfxAt(SoundId.Checkpoint, transform.position);
                if (stats != null && healAmount > 0f) stats.Heal(healAmount);
                GameEvents.RaiseToast($"Đã thắp nến – {displayName}. Tiến trình đã được lưu.");
            }
            if (GameSession.Instance != null) GameSession.Instance.SaveAtCheckpoint(this);
            GameEvents.RaiseCheckpoint(checkpointId);
        }
    }
}
