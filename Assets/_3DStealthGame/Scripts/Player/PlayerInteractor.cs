using UnityEngine;

namespace StealthGame
{
    /// <summary>
    /// Tìm vật tương tác gần nhất phía trước (NPC, cửa, phong ấn, checkpoint, cổng thoát)
    /// và hiển thị gợi ý "[E] ...". Nhấn E để tương tác.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public float radius = 2.0f;
        public float scanInterval = 0.1f;

        public PlayerMovement Movement { get; private set; }
        public PlayerInventory Inventory { get; private set; }
        public PlayerStats Stats { get; private set; }
        public IInteractable Current { get; private set; }

        readonly Collider[] m_Buffer = new Collider[32];
        float m_NextScan;
        string m_LastPrompt;

        void Awake()
        {
            Movement = GetComponent<PlayerMovement>();
            Inventory = GetComponent<PlayerInventory>();
            Stats = GetComponent<PlayerStats>();
        }

        void Update()
        {
            bool active = GameInput.GameplayEnabled && !DialogueManager.IsBlockingInput && (Stats == null || Stats.IsAlive);
            if (!active)
            {
                SetPrompt(null);
                Current = null;
                return;
            }

            if (Time.time >= m_NextScan)
            {
                m_NextScan = Time.time + scanInterval;
                Current = FindBest();
            }

            SetPrompt(Current != null ? (TouchInput.Enabled ? "[DÙNG]  " : "[E]  ") + Current.GetPrompt(this) : null);

            if (Current != null && GameInput.Pressed(GameInput.Interact))
            {
                var target = Current;
                Current = null;
                target.Interact(this);
                m_NextScan = Time.time + 0.25f;
            }
        }

        IInteractable FindBest()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.8f, radius, m_Buffer, ~0, QueryTriggerInteraction.Collide);
            IInteractable best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var interactable = m_Buffer[i].GetComponentInParent<IInteractable>();
                if (interactable == null || !interactable.CanInteract(this)) continue;

                Vector3 to = interactable.transform.position - transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                // Ưu tiên vật ở phía trước mặt
                float facing = dist > 0.01f ? Vector3.Dot(transform.forward, to / dist) : 1f;
                float score = dist - facing * 0.6f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = interactable;
                }
            }
            return best;
        }

        void SetPrompt(string prompt)
        {
            if (prompt == m_LastPrompt) return;
            m_LastPrompt = prompt;
            if (GameHUD.Instance != null)
                GameHUD.Instance.SetInteractionPrompt(prompt);
        }
    }
}
