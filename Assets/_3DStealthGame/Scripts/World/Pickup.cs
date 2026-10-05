using UnityEngine;

namespace StealthGame
{
    /// <summary>Vật phẩm nhặt khi chạm vào: mảnh vụn linh hồn, bình máu, dầu đèn, Mảnh hồn.</summary>
    [RequireComponent(typeof(Collider))]
    public class Pickup : MonoBehaviour
    {
        public PickupType type = PickupType.Shard;
        public int amount = 1;
        [Tooltip("Id duy nhất để lưu trạng thái đã nhặt. Để trống = không lưu (vật rơi từ ma).")]
        public string pickupId;
        public string displayName;
        public Transform visual;
        public float bobHeight = 0.1f;
        public float spinSpeed = 90f;
        [Tooltip("Hút về phía người chơi khi ở gần (mảnh vụn).")]
        public float magnetRadius = 1.6f;

        Vector3 m_VisualBase;
        float m_Phase;
        bool m_Collected;

        void Start()
        {
            if (!string.IsNullOrEmpty(pickupId) && GameSession.Instance != null && GameSession.Instance.IsCollected(pickupId))
            {
                Destroy(gameObject);
                return;
            }
            if (visual != null) m_VisualBase = visual.localPosition;
            m_Phase = Random.value * 10f;
            GetComponent<Collider>().isTrigger = true;
        }

        void Update()
        {
            if (visual != null)
            {
                visual.localPosition = m_VisualBase + Vector3.up * (Mathf.Sin(Time.time * 2.2f + m_Phase) * bobHeight);
                visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            }

            if (type == PickupType.Shard && magnetRadius > 0f && PlayerStats.Instance != null && PlayerStats.Instance.IsAlive)
            {
                Vector3 target = PlayerStats.Instance.transform.position + Vector3.up * 0.6f;
                if ((target - transform.position).sqrMagnitude < magnetRadius * magnetRadius)
                    transform.position = Vector3.MoveTowards(transform.position, target, 4f * Time.deltaTime);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (m_Collected) return;
            var inventory = other.GetComponentInParent<PlayerInventory>();
            if (inventory == null) return;
            var stats = inventory.GetComponent<PlayerStats>();
            if (stats != null && !stats.IsAlive) return;
            Collect(inventory, stats);
        }

        void Collect(PlayerInventory inventory, PlayerStats stats)
        {
            m_Collected = true;
            Vector3 pos = transform.position;

            switch (type)
            {
                case PickupType.Shard:
                    inventory.AddShards(amount);
                    AudioManager.SfxAt(SoundId.Shard, pos, 0.7f);
                    VFXManager.Play(VFXId.PickupSparkle, pos, default, new Color(0.5f, 0.9f, 1f));
                    break;
                case PickupType.Potion:
                    inventory.AddPotions(amount);
                    AudioManager.SfxAt(SoundId.Potion, pos);
                    VFXManager.Play(VFXId.PickupSparkle, pos, default, new Color(1f, 0.35f, 0.4f));
                    GameEvents.RaiseToast($"+{amount} Bình máu  (nhấn H để dùng)");
                    break;
                case PickupType.Energy:
                    if (stats != null) stats.AddEnergy(40f * amount);
                    AudioManager.SfxAt(SoundId.EnergyPickup, pos);
                    VFXManager.Play(VFXId.PickupSparkle, pos, default, new Color(1f, 0.85f, 0.3f));
                    GameEvents.RaiseToast("+40 năng lượng đèn");
                    break;
                case PickupType.Relic:
                    inventory.AddRelic(pickupId);
                    AudioManager.Sfx(SoundId.Relic);
                    VFXManager.Play(VFXId.RelicCollect, pos);
                    CameraShake.Shake(0.35f);
                    GameEvents.RaiseToast($"Đã lấy {displayName}!  ({inventory.RelicCount}/{PlayerInventory.RelicsRequired})");
                    break;
                case PickupType.Key:
                    inventory.AddKey(pickupId);
                    AudioManager.SfxAt(SoundId.Key, pos);
                    VFXManager.Play(VFXId.PickupSparkle, pos);
                    GameEvents.RaiseToast("Nhận được: " + displayName);
                    break;
            }

            if (!string.IsNullOrEmpty(pickupId) && GameSession.Instance != null)
                GameSession.Instance.MarkCollected(pickupId);
            GameEvents.RaiseItemCollected(type, pickupId, amount);
            Destroy(gameObject);
        }
    }
}
