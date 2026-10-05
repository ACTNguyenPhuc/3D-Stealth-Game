using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StealthGame
{
    /// <summary>
    /// Bộ điều khiển hội thoại: tìm NPC gần nhất, chạy cây hội thoại theo từng node,
    /// đánh giá điều kiện, thực thi hành động và lưu trạng thái (flags / node đã thăm).
    ///
    /// Vòng đời một cuộc hội thoại:
    ///   Idle --[E]--> StartDialogue --> EnterNode --(choice / continue)--> EnterNode ... --> EndDialogue --> Idle
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        [Serializable]
        public class KeyDisplayName
        {
            public string keyName;
            public string displayName;
        }

        public static DialogueManager Instance { get; private set; }
        public static bool IsDialogueActive => Instance != null && Instance.m_Active;

        /// <summary>True trong lúc hội thoại hoặc ngay frame vừa kết thúc (tránh phím 1-9 bị xử lý 2 lần).</summary>
        public static bool IsBlockingInput => IsDialogueActive || (Instance != null && Instance.m_LastEndFrame == Time.frameCount);

        [Header("References")]
        public PlayerMovement player;
        [Tooltip("Điểm trên người chơi để camera hội thoại nhìn vào (đầu).")]
        public Transform playerFocus;
        public DialogueUI dialogueUI;
        public GameHUD hud;

        [Header("Settings")]
        public string playerName = "John Lemon";
        [Tooltip("Dừng thời gian (ma đứng yên) trong lúc hội thoại.")]
        public bool pauseWorldDuringDialogue = true;
        public List<KeyDisplayName> keyDisplayNames = new();

        public event Action<NPCDialogue> DialogueStarted;
        public event Action<NPCDialogue> DialogueEnded;
        public event Action<DialogueNode> NodeEntered;

        public NPCDialogue CurrentNpc => m_Npc;
        public NPCDialogue NearbyNpc => m_NearbyNpc;

        readonly HashSet<string> m_Flags = new();
        readonly HashSet<string> m_VisitedNodes = new();
        readonly List<int> m_VisibleChoiceIndices = new();

        bool m_Active;
        bool m_AlertPending;
        bool m_TalkRequested;
        float m_PreviousTimeScale = 1f;
        float m_CooldownUntil;
        int m_LastEndFrame = -1;
        NPCDialogue m_Npc;
        NPCDialogue m_NearbyNpc;
        DialogueTree m_Tree;
        DialogueNode m_Node;

        void Awake()
        {
            Instance = this;
            if (player == null)
                player = FindAnyObjectByType<PlayerMovement>();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                if (m_Active && pauseWorldDuringDialogue)
                    Time.timeScale = m_PreviousTimeScale;
                Instance = null;
            }
        }

        void Start()
        {
            if (dialogueUI != null)
            {
                dialogueUI.ChoiceSelected += i => Choose(i);
                dialogueUI.AdvanceRequested += Advance;
            }
        }

        // ------------------------------------------------------------------ Flags

        public bool HasFlag(string flag) => m_Flags.Contains(flag);

        public void SetFlag(string flag, bool value = true)
        {
            if (value) m_Flags.Add(flag);
            else m_Flags.Remove(flag);
        }

        public IReadOnlyCollection<string> Flags => m_Flags;

        /// <summary>Gọi từ nút "Nói chuyện" trên màn hình cảm ứng.</summary>
        public void RequestTalk() => m_TalkRequested = true;

        // ------------------------------------------------------------------ Update

        void Update()
        {
            if (m_Active)
            {
                HandleDialogueInput();
                return;
            }

            if (m_TalkRequested)
            {
                m_TalkRequested = false;
                var npc = FindNearbyNpc();
                if (npc != null) StartDialogue(npc);
            }
        }

        /// <summary>NPC gần nhất trong bán kính nói chuyện (dùng cho nút cảm ứng).</summary>
        NPCDialogue FindNearbyNpc()
        {
            if (player == null) return null;
            NPCDialogue best = null;
            float bestDistance = float.MaxValue;
            foreach (var npc in NPCDialogue.All)
            {
                if (npc.dialogueTree == null) continue;
                float d = Vector3.Distance(npc.transform.position, player.transform.position);
                if (d <= npc.talkRadius && d < bestDistance) { bestDistance = d; best = npc; }
            }
            return best;
        }

        void HandleDialogueInput()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                    Advance();

                for (int i = 0; i < m_VisibleChoiceIndices.Count && i < 9; i++)
                {
                    if (kb[UnityEngine.InputSystem.Key.Digit1 + i].wasPressedThisFrame || kb[UnityEngine.InputSystem.Key.Numpad1 + i].wasPressedThisFrame)
                    {
                        Choose(i);
                        return;
                    }
                }

                if (kb.escapeKey.wasPressedThisFrame)
                    EndDialogue();
            }

            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                Advance();
        }

        // ------------------------------------------------------------------ Flow

        public void StartDialogue(NPCDialogue npc)
        {
            if (m_Active || npc == null || npc.dialogueTree == null || Time.unscaledTime < m_CooldownUntil)
                return;

            m_Active = true;
            m_AlertPending = false;
            m_Npc = npc;
            m_Tree = npc.dialogueTree;

            if (pauseWorldDuringDialogue)
            {
                m_PreviousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            // Người chơi quay mặt về phía NPC
            if (player != null)
            {
                Vector3 toNpc = npc.transform.position - player.transform.position;
                toNpc.y = 0f;
                if (toNpc.sqrMagnitude > 0.001f)
                    player.transform.rotation = Quaternion.LookRotation(toNpc);
            }

            npc.BeginTalk(player != null ? player.transform : null);

            if (CameraModeManager.Instance != null)
                CameraModeManager.Instance.BeginDialogueShot(npc.FocusPoint, playerFocus != null ? playerFocus : player.transform);

            if (hud != null)
            {
                hud.SetInteractionPrompt(null);
                hud.SetDialogueMode(true);
            }
            m_NearbyNpc = null;

            if (dialogueUI != null) dialogueUI.Open();
            DialogueStarted?.Invoke(npc);

            EnterNode(m_Tree.ResolveStartNode(Evaluate));
        }

        void EnterNode(string nodeId)
        {
            var node = m_Tree.GetNode(nodeId);
            if (node == null)
            {
                if (!string.IsNullOrEmpty(nodeId))
                    Debug.LogWarning($"[Dialogue] {m_Tree.name}: không tìm thấy node '{nodeId}'.", m_Tree);
                EndDialogue();
                return;
            }

            m_Node = node;
            m_VisitedNodes.Add(VisitKey(m_Tree, node.id));

            foreach (var action in node.onEnter)
                Execute(action);

            // Lọc các lựa chọn theo điều kiện
            m_VisibleChoiceIndices.Clear();
            var views = new List<DialogueUI.ChoiceView>();
            for (int i = 0; i < node.choices.Count; i++)
            {
                var choice = node.choices[i];
                bool available = choice.conditions.TrueForAll(Evaluate);
                if (!available && !choice.showWhenLocked)
                    continue;

                m_VisibleChoiceIndices.Add(i);
                views.Add(new DialogueUI.ChoiceView
                {
                    text = FormatText(choice.text),
                    locked = !available,
                    lockedHint = choice.lockedHint,
                });
            }

            bool isPlayerSpeaking = node.speaker == "{player}";
            string speaker = string.IsNullOrEmpty(node.speaker) ? m_Npc.displayName : FormatText(node.speaker);

            if (dialogueUI != null) dialogueUI.ShowLine(speaker, isPlayerSpeaking, FormatText(node.text), views, node.IsEnd);
            NodeEntered?.Invoke(node);
        }

        /// <summary>Chọn lựa chọn thứ visibleIndex (theo thứ tự hiển thị).</summary>
        public void Choose(int visibleIndex)
        {
            if (!m_Active || m_Node == null)
                return;

            if (dialogueUI != null && dialogueUI.IsTyping)
            {
                dialogueUI.FinishTyping();
                return;
            }

            if (visibleIndex < 0 || visibleIndex >= m_VisibleChoiceIndices.Count)
                return;

            var choice = m_Node.choices[m_VisibleChoiceIndices[visibleIndex]];
            if (!choice.conditions.TrueForAll(Evaluate))
            {
                Toast(string.IsNullOrEmpty(choice.lockedHint) ? "Lựa chọn này chưa khả dụng." : choice.lockedHint);
                return;
            }

            foreach (var action in choice.actions)
                Execute(action);

            if (string.IsNullOrEmpty(choice.targetNodeId))
                EndDialogue();
            else
                EnterNode(choice.targetNodeId);
        }

        /// <summary>Space / click: hiện hết chữ, hoặc sang câu tiếp theo nếu node không có lựa chọn.</summary>
        public void Advance()
        {
            if (!m_Active || m_Node == null)
                return;

            if (dialogueUI != null && dialogueUI.IsTyping)
            {
                dialogueUI.FinishTyping();
                return;
            }

            if (m_Node.choices.Count > 0)
                return;

            if (string.IsNullOrEmpty(m_Node.nextNodeId))
                EndDialogue();
            else
                EnterNode(m_Node.nextNodeId);
        }

        public void EndDialogue()
        {
            if (!m_Active)
                return;

            m_Active = false;
            m_Node = null;

            if (dialogueUI != null) dialogueUI.Close();
            if (hud != null) hud.SetDialogueMode(false);

            if (m_Npc != null)
                m_Npc.EndTalk();

            if (CameraModeManager.Instance != null)
                CameraModeManager.Instance.EndDialogueShot();

            if (pauseWorldDuringDialogue)
                Time.timeScale = m_PreviousTimeScale;

            m_CooldownUntil = Time.unscaledTime + 0.4f;
            m_LastEndFrame = Time.frameCount;

            var npc = m_Npc;
            m_Npc = null;
            DialogueEnded?.Invoke(npc);

            if (m_AlertPending)
            {
                m_AlertPending = false;
                if (player != null)
                {
                    GameEvents.RaiseAlarm(player.transform.position);
                    AudioManager.Sfx(SoundId.Alarm);
                    var stats = player.GetComponent<PlayerStats>();
                    if (stats != null) stats.TakeDamage(25f, npc != null ? npc.transform.position : player.transform.position);
                }
            }
        }

        // ------------------------------------------------------------------ Conditions & actions

        public bool Evaluate(DialogueCondition condition)
        {
            switch (condition.type)
            {
                case DialogueConditionType.FlagSet: return m_Flags.Contains(condition.key);
                case DialogueConditionType.FlagNotSet: return !m_Flags.Contains(condition.key);
                case DialogueConditionType.HasKey: return player != null && player.OwnKey(condition.key);
                case DialogueConditionType.NotHasKey: return player == null || !player.OwnKey(condition.key);
                case DialogueConditionType.NodeVisited: return m_VisitedNodes.Contains(VisitKey(m_Tree, condition.key));
                case DialogueConditionType.NodeNotVisited: return !m_VisitedNodes.Contains(VisitKey(m_Tree, condition.key));
                case DialogueConditionType.ShardsAtLeast: return Inventory != null && Inventory.Shards >= condition.amount;
                case DialogueConditionType.RelicsAtLeast: return Inventory != null && Inventory.RelicCount >= condition.amount;
                case DialogueConditionType.HealthBelowMax:
                    var st = player != null ? player.GetComponent<PlayerStats>() : null;
                    return st != null && st.Health < st.maxHealth;
                default: return true;
            }
        }

        void Execute(DialogueAction action)
        {
            switch (action.type)
            {
                case DialogueActionType.SetFlag:
                    m_Flags.Add(action.key);
                    break;

                case DialogueActionType.ClearFlag:
                    m_Flags.Remove(action.key);
                    break;

                case DialogueActionType.GiveKey:
                    if (player != null && !player.OwnKey(action.key))
                    {
                        player.AddKey(action.key);
                        Toast($"Nhận được: {GetKeyDisplayName(action.key)}");
                    }
                    break;

                case DialogueActionType.SlowEnemies:
                    StartCoroutine(SlowEnemiesRoutine(action.value, action.duration));
                    Toast($"Bọn ma bị chậm lại trong {action.duration:0} giây!");
                    break;

                case DialogueActionType.AlertEnemies:
                    m_AlertPending = true;
                    break;

                case DialogueActionType.OpenDoor:
                    foreach (var door in FindObjectsByType<Door>())
                    {
                        if (door.KeyName == action.key)
                            Destroy(door.gameObject);
                    }
                    Toast("Đâu đó có tiếng cửa mở ra...");
                    break;

                case DialogueActionType.ShowMessage:
                    Toast(action.key);
                    break;

                case DialogueActionType.SpendShards:
                    if (Inventory != null && Inventory.SpendShards(Mathf.RoundToInt(action.value)))
                        Toast($"-{action.value:0} mảnh vụn linh hồn");
                    break;

                case DialogueActionType.GivePotions:
                    if (Inventory != null)
                    {
                        Inventory.AddPotions(Mathf.RoundToInt(action.value));
                        AudioManager.Sfx(SoundId.Potion);
                        Toast($"+{action.value:0} Bình máu");
                    }
                    break;

                case DialogueActionType.Heal:
                    var hs = player != null ? player.GetComponent<PlayerStats>() : null;
                    if (hs != null) { hs.Heal(action.value); AudioManager.Sfx(SoundId.Potion); Toast($"+{action.value:0} máu"); }
                    break;

                case DialogueActionType.RestoreEnergy:
                    var es = player != null ? player.GetComponent<PlayerStats>() : null;
                    if (es != null) { es.AddEnergy(action.value); AudioManager.Sfx(SoundId.EnergyPickup); Toast($"+{action.value:0} năng lượng đèn"); }
                    break;

                case DialogueActionType.AddScore:
                    if (GameSession.Instance != null) GameSession.Instance.AddScore(Mathf.RoundToInt(action.value));
                    break;
            }
        }

        IEnumerator SlowEnemiesRoutine(float multiplier, float duration)
        {
            var enemies = FindObjectsByType<WaypointPatrol>();
            var original = new float[enemies.Length];
            for (int i = 0; i < enemies.Length; i++)
            {
                original[i] = enemies[i].moveSpeed;
                enemies[i].moveSpeed *= multiplier;
            }

            // Đếm theo thời gian game (không tính lúc đang hội thoại)
            yield return new WaitForSeconds(duration);

            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null)
                    enemies[i].moveSpeed = original[i];
            }
            Toast("Bọn ma đã trở lại tốc độ bình thường.");
        }

        void Toast(string message) => GameEvents.RaiseToast(message);

        PlayerInventory Inventory => player != null ? player.GetComponent<PlayerInventory>() : null;

        string GetKeyDisplayName(string keyName)
        {
            foreach (var entry in keyDisplayNames)
            {
                if (entry.keyName == keyName)
                    return entry.displayName;
            }
            return keyName;
        }

        string FormatText(string text) => string.IsNullOrEmpty(text) ? string.Empty : text.Replace("{player}", playerName);

        static string VisitKey(DialogueTree tree, string nodeId) => tree.name + "/" + nodeId;
    }
}
