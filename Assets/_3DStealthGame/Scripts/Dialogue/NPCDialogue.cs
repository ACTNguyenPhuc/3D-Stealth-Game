using System.Collections.Generic;
using UnityEngine;

namespace StealthGame
{
    /// <summary>
    /// Gắn lên NPC để người chơi có thể bắt chuyện. DialogueManager sẽ tìm NPC
    /// gần nhất trong bán kính talkRadius và hiện gợi ý "[E] Nói chuyện".
    /// </summary>
    public class NPCDialogue : MonoBehaviour, IInteractable
    {
        public static readonly List<NPCDialogue> All = new();

        public string displayName = "NPC";
        public DialogueTree dialogueTree;
        public float talkRadius = 1.8f;

        [Tooltip("Điểm camera hội thoại nhìn vào (thường là đầu NPC).")]
        public Transform focusPoint;
        [Tooltip("Biểu tượng lơ lửng trên đầu, ẩn khi đang nói chuyện.")]
        public Transform indicator;

        public bool facePlayerWhileTalking = true;
        public float turnSpeed = 360f;

        Transform m_TalkingTo;
        Vector3 m_IndicatorBasePos;

        public Transform FocusPoint => focusPoint != null ? focusPoint : transform;
        public bool IsTalking => m_TalkingTo != null;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            if (indicator != null)
                m_IndicatorBasePos = indicator.localPosition;
        }

        public bool CanInteract(PlayerInteractor interactor) => dialogueTree != null && !IsTalking && DialogueManager.Instance != null;
        public string GetPrompt(PlayerInteractor interactor) => $"Nói chuyện với {displayName}";
        public void Interact(PlayerInteractor interactor) => DialogueManager.Instance.StartDialogue(this);

        public void BeginTalk(Transform player) => m_TalkingTo = player;
        public void EndTalk() => m_TalkingTo = null;

        void Update()
        {
            // Hội thoại tạm dừng thời gian (timeScale = 0) nên dùng unscaled time
            float dt = Time.unscaledDeltaTime;

            if (m_TalkingTo != null && facePlayerWhileTalking)
            {
                Vector3 toPlayer = m_TalkingTo.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.001f)
                {
                    Quaternion target = Quaternion.LookRotation(toPlayer);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
                }
            }

            if (indicator != null)
            {
                indicator.gameObject.SetActive(m_TalkingTo == null);
                indicator.localPosition = m_IndicatorBasePos + Vector3.up * (Mathf.Sin(Time.unscaledTime * 3f) * 0.05f);
                indicator.Rotate(Vector3.up, 90f * dt, Space.World);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, talkRadius);
        }
    }
}
