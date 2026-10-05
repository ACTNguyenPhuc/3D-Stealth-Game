using UnityEngine;
using UnityEngine.AI;

namespace StealthGame
{
    /// <summary>Cửa chính bị phong ấn: cần đủ 3 Mảnh hồn để phá, sau đó bước vào vùng thoát để chiến thắng.</summary>
    public class ExitGate : MonoBehaviour, IInteractable
    {
        public const string GateId = "gate_main";

        public Transform barrier;
        public GameObject exitZone;

        bool m_Open;
        float m_Timer;
        Vector3 m_BaseScale;

        public bool IsOpen => m_Open;

        void Start()
        {
            if (barrier != null) m_BaseScale = barrier.localScale;
            if (exitZone != null) exitZone.SetActive(false);
            if (GameSession.Instance != null && GameSession.Instance.IsOpened(GateId))
                OpenImmediate();
        }

        public bool CanInteract(PlayerInteractor interactor) => !m_Open;

        public string GetPrompt(PlayerInteractor interactor)
        {
            int have = interactor.Inventory != null ? interactor.Inventory.RelicCount : 0;
            return have >= PlayerInventory.RelicsRequired
                ? "Dùng 3 Mảnh hồn phá phong ấn cửa chính"
                : $"Cửa chính bị phong ấn – cần {PlayerInventory.RelicsRequired} Mảnh hồn ({have}/{PlayerInventory.RelicsRequired})";
        }

        public void Interact(PlayerInteractor interactor)
        {
            int have = interactor.Inventory != null ? interactor.Inventory.RelicCount : 0;
            if (have < PlayerInventory.RelicsRequired)
            {
                AudioManager.SfxAt(SoundId.Locked, transform.position);
                GameEvents.RaiseToast($"Cần thêm {PlayerInventory.RelicsRequired - have} Mảnh hồn.");
                return;
            }
            m_Open = true;
            AudioManager.Sfx(SoundId.Relic);
            AudioManager.SfxAt(SoundId.DoorOpen, transform.position);
            VFXManager.Play(VFXId.SealBreak, transform.position + Vector3.up);
            CameraShake.Shake(0.8f);
            GameEvents.RaiseToast("Phong ấn đã vỡ! Chạy ra ngoài mau!");
            if (GameSession.Instance != null) GameSession.Instance.MarkOpened(GateId);
            GameEvents.RaiseSealOpened(GateId);
            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>())
                if (exitZone == null || !c.transform.IsChildOf(exitZone.transform)) c.enabled = false;
            if (exitZone != null) exitZone.SetActive(true);
        }

        void Update()
        {
            if (barrier == null || !barrier.gameObject.activeSelf) return;
            if (m_Open)
            {
                m_Timer += Time.deltaTime;
                float k = Mathf.Clamp01(m_Timer / 1f);
                barrier.localScale = Vector3.Lerp(m_BaseScale, new Vector3(m_BaseScale.x, 0.01f, m_BaseScale.z), k);
                if (k >= 1f) barrier.gameObject.SetActive(false);
            }
        }

        void OpenImmediate()
        {
            m_Open = true;
            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>())
                if (exitZone == null || !c.transform.IsChildOf(exitZone.transform)) c.enabled = false;
            if (barrier != null) barrier.gameObject.SetActive(false);
            if (exitZone != null) exitZone.SetActive(true);
        }
    }
}
