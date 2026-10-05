using UnityEngine;
using UnityEngine.AI;

namespace StealthGame
{
    /// <summary>
    /// Cửa khóa màu: có chìa đúng màu thì nhấn E (hoặc chạm vào) để mở.
    /// Cửa chìm xuống sàn rồi tắt; NavMeshObstacle (carve) bị gỡ để ma đi qua được.
    /// </summary>
    public class Door : MonoBehaviour, IInteractable
    {
        public string KeyName = "key1";
        public string displayName = "Cửa";
        public string keyDisplayName = "chìa khóa";

        bool m_Opening;
        float m_OpenTimer;
        Vector3 m_ClosedPos;

        string SaveId => "door_" + KeyName;

        void Start()
        {
            m_ClosedPos = transform.position;
            if (GameSession.Instance != null && GameSession.Instance.IsOpened(SaveId))
                gameObject.SetActive(false);
        }

        public bool CanInteract(PlayerInteractor interactor) => !m_Opening;

        public string GetPrompt(PlayerInteractor interactor) =>
            interactor.Inventory != null && interactor.Inventory.HasKey(KeyName)
                ? $"Mở {displayName}"
                : $"{displayName} bị khóa – cần {keyDisplayName}";

        public void Interact(PlayerInteractor interactor)
        {
            if (interactor.Inventory != null && interactor.Inventory.HasKey(KeyName)) Open();
            else
            {
                AudioManager.SfxAt(SoundId.Locked, transform.position);
                GameEvents.RaiseToast($"Cần {keyDisplayName}.");
            }
        }

        void OnCollisionEnter(Collision other)
        {
            if (m_Opening) return;
            var inventory = other.gameObject.GetComponentInParent<PlayerInventory>();
            if (inventory != null && inventory.HasKey(KeyName)) Open();
        }

        public void Open()
        {
            if (m_Opening) return;
            m_Opening = true;
            AudioManager.SfxAt(SoundId.DoorOpen, transform.position);
            GameEvents.RaiseToast($"Đã mở {displayName}.");
            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (GameSession.Instance != null) GameSession.Instance.MarkOpened(SaveId);
        }

        void Update()
        {
            if (!m_Opening) return;
            m_OpenTimer += Time.deltaTime;
            transform.position = m_ClosedPos + Vector3.down * (m_OpenTimer * 2.5f);
            if (m_OpenTimer > 1.2f) gameObject.SetActive(false);
        }
    }
}
