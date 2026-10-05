using System.Collections.Generic;
using UnityEngine;

namespace StealthGame
{
    /// <summary>
    /// Theo dõi tiến độ nhiệm vụ. Trạng thái được suy ra từ dữ liệu game (hành trang, cờ, cửa đã mở...)
    /// nên không cần lưu riêng – load save là nhiệm vụ tự khớp.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        public QuestDefinition quest;

        public struct ObjectiveState
        {
            public QuestObjective objective;
            public int progress;
            public bool complete;
            public bool visible;
            public string Text => string.Format(objective.text ?? "", progress, objective.target);
        }

        readonly List<ObjectiveState> m_States = new();
        readonly HashSet<string> m_Completed = new();
        bool m_Initialized;
        float m_NextEval;

        public IReadOnlyList<ObjectiveState> States => m_States;
        public string CurrentMainObjective { get; private set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (quest == null || Time.unscaledTime < m_NextEval) return;
            m_NextEval = Time.unscaledTime + 0.25f;
            Evaluate();
        }

        public void Evaluate()
        {
            if (quest == null) return;
            m_States.Clear();
            CurrentMainObjective = null;

            foreach (var obj in quest.objectives)
            {
                int progress = GetProgress(obj);
                bool complete = m_Completed.Contains(obj.id) || progress >= obj.target;
                if (complete && m_Completed.Add(obj.id) && m_Initialized)
                {
                    AudioManager.Sfx(SoundId.Checkpoint, 0.5f);
                    GameEvents.RaiseToast("Hoàn thành: " + string.Format(obj.text, Mathf.Min(progress, obj.target), obj.target));
                    GameEvents.RaiseObjectiveCompleted(obj.id);
                }
                bool visible = string.IsNullOrEmpty(obj.requires) || m_Completed.Contains(obj.requires);
                var state = new ObjectiveState { objective = obj, progress = Mathf.Min(progress, obj.target), complete = complete, visible = visible };
                m_States.Add(state);
                if (CurrentMainObjective == null && visible && !complete && !obj.optional)
                    CurrentMainObjective = state.Text;
            }
            // Lần đánh giá đầu (sau khi load save) không phát thông báo
            m_Initialized = true;
        }

        int GetProgress(QuestObjective obj)
        {
            var session = GameSession.Instance;
            var player = PlayerStats.Instance;
            var inventory = player != null ? player.GetComponent<PlayerInventory>() : null;

            switch (obj.type)
            {
                case ObjectiveType.Flag:
                    return DialogueManager.Instance != null && DialogueManager.Instance.HasFlag(obj.key) ? obj.target : 0;
                case ObjectiveType.RelicCount:
                    return inventory != null ? inventory.RelicCount : 0;
                case ObjectiveType.HasKey:
                    return inventory != null && inventory.HasKey(obj.key) ? obj.target : 0;
                case ObjectiveType.Opened:
                    return session != null && session.IsOpened(obj.key) ? obj.target : 0;
                case ObjectiveType.BanishCount:
                    return session != null ? session.Banished : 0;
                case ObjectiveType.ShardCount:
                    return session != null ? session.ShardsCollected : 0;
                case ObjectiveType.Escaped:
                    return session != null && session.CurrentPhase == GameSession.Phase.Victory ? obj.target : 0;
            }
            return 0;
        }
    }
}
