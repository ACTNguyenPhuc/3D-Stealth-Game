using UnityEngine;

namespace StealthGame
{
    /// <summary>Chìa khóa màu: chạm vào để nhặt. Dùng mở cửa / phong ấn cùng màu.</summary>
    public class Key : MonoBehaviour
    {
        public string KeyName = "key1";
        public string displayName = "Chìa khóa";

        Vector3 m_BasePos;
        bool m_Collected;

        string SaveId => "key_" + KeyName;

        void Start()
        {
            if (GameSession.Instance != null && GameSession.Instance.IsCollected(SaveId))
            {
                Destroy(gameObject);
                return;
            }
            m_BasePos = transform.position;
        }

        void Update()
        {
            transform.position = m_BasePos + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.08f);
            transform.Rotate(Vector3.up, 70f * Time.deltaTime, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            if (m_Collected) return;
            var inventory = other.GetComponentInParent<PlayerInventory>();
            if (inventory == null) return;

            m_Collected = true;
            inventory.AddKey(KeyName);
            AudioManager.SfxAt(SoundId.Key, transform.position);
            VFXManager.Play(VFXId.PickupSparkle, transform.position);
            GameEvents.RaiseToast("Nhận được: " + displayName);
            GameEvents.RaiseItemCollected(PickupType.Key, KeyName, 1);
            if (GameSession.Instance != null) GameSession.Instance.MarkCollected(SaveId);
            Destroy(gameObject);
        }
    }
}
