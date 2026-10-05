using UnityEngine;
using UnityEngine.AI;

namespace StealthGame
{
    /// <summary>
    /// Phong ấn linh hồn bao quanh một Mảnh hồn. Cần chìa khóa đúng màu để phá.
    /// Tách việc "mở khóa" khỏi hình học căn phòng → dễ thiết kế level.
    /// </summary>
    public class KeySeal : MonoBehaviour, IInteractable
    {
        public string sealId = "seal_red";
        public string requiredKey = "keyRed";
        public string sealName = "Phong ấn Đỏ";
        public string keyDisplayName = "Chìa khóa đỏ";
        public Transform barrier;

        bool m_Opening;
        float m_Timer;
        Vector3 m_BaseScale;

        void Start()
        {
            if (barrier != null) m_BaseScale = barrier.localScale;
            if (GameSession.Instance != null && GameSession.Instance.IsOpened(sealId))
                SetOpenImmediate();
        }

        public bool CanInteract(PlayerInteractor interactor) => !m_Opening;

        public string GetPrompt(PlayerInteractor interactor) =>
            HasKey(interactor) ? $"Phá {sealName}" : $"{sealName} – cần {keyDisplayName}";

        bool HasKey(PlayerInteractor i) => i.Inventory != null && i.Inventory.HasKey(requiredKey);

        public void Interact(PlayerInteractor interactor)
        {
            if (!HasKey(interactor))
            {
                AudioManager.SfxAt(SoundId.Locked, transform.position);
                GameEvents.RaiseToast($"{sealName} cần {keyDisplayName}.");
                return;
            }
            m_Opening = true;
            AudioManager.SfxAt(SoundId.DoorOpen, transform.position);
            AudioManager.Sfx(SoundId.Relic, 0.5f);
            VFXManager.Play(VFXId.SealBreak, transform.position + Vector3.up);
            CameraShake.Shake(0.4f);
            GameEvents.RaiseToast($"{sealName} đã bị phá!");
            if (GameSession.Instance != null) GameSession.Instance.MarkOpened(sealId);
            GameEvents.RaiseSealOpened(sealId);
            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        void Update()
        {
            if (barrier == null) return;
            if (m_Opening)
            {
                m_Timer += Time.deltaTime;
                float k = Mathf.Clamp01(m_Timer / 0.8f);
                barrier.localScale = Vector3.Lerp(m_BaseScale, new Vector3(m_BaseScale.x * 1.4f, 0.01f, m_BaseScale.z * 1.4f), k);
                if (k >= 1f) SetOpenImmediate();
            }
            else
            {
                barrier.Rotate(Vector3.up, 20f * Time.deltaTime, Space.World);
            }
        }

        void SetOpenImmediate()
        {
            m_Opening = true;
            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (barrier != null) barrier.gameObject.SetActive(false);
            enabled = false;
        }
    }
}
