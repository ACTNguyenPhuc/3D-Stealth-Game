using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace StealthGame
{
    /// <summary>
    /// HUD chính: thanh máu / thể lực / năng lượng, đồng hồ bình minh, Mảnh hồn, chìa khóa,
    /// mảnh vụn, bình máu, điểm, nhiệm vụ hiện tại, trạng thái bị phát hiện,
    /// dấu "?"/"!" trên đầu kẻ địch, viền đỏ khi bị thương.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HUDController : MonoBehaviour
    {
        VisualElement m_Root;
        VisualElement m_HealthFill, m_StaminaFill, m_EnergyFill;
        Label m_HealthText, m_LanternState;
        Label m_Clock, m_Score, m_Shards, m_Potions;
        VisualElement[] m_RelicIcons;
        VisualElement m_KeyBlue, m_KeyRed;
        VisualElement m_Objectives;
        Label m_Status;
        VisualElement m_Crosshair;
        VisualElement m_Vignette;
        VisualElement m_Markers;
        Label m_Fps;

        readonly List<(VisualElement root, Label icon, VisualElement hpFill)> m_MarkerPool = new();
        readonly List<Label> m_ObjectiveLabels = new();

        PlayerStats m_Stats;
        PlayerInventory m_Inventory;
        PlayerCombat m_Combat;
        PlayerMovement m_Movement;
        UIDocument m_Doc;
        float m_DamageFlash;
        float m_FpsTimer;
        int m_FpsFrames;

        void OnEnable() => GameEvents.PlayerDamaged += OnDamaged;
        void OnDisable() => GameEvents.PlayerDamaged -= OnDamaged;

        void OnDamaged(float amount, Vector3 _) => m_DamageFlash = Mathf.Clamp01(m_DamageFlash + 0.5f + amount / 60f);

        void Start()
        {
            m_Doc = GetComponent<UIDocument>();
            Build(m_Doc.rootVisualElement);
        }

        void ResolvePlayer()
        {
            m_Stats = PlayerStats.Instance;
            if (m_Stats == null) return;
            m_Inventory = m_Stats.GetComponent<PlayerInventory>();
            m_Combat = m_Stats.GetComponent<PlayerCombat>();
            m_Movement = m_Stats.GetComponent<PlayerMovement>();
        }

        // ------------------------------------------------------------------ Dựng giao diện

        void Build(VisualElement docRoot)
        {
            var t = UITheme.Current;
            m_Root = UIBuild.Box("HUD", docRoot);
            UIBuild.Fill(m_Root);
            m_Root.pickingMode = PickingMode.Ignore;
            UIBuild.ApplyDefaultFont(m_Root);

            // Viền đỏ khi bị thương (dưới cùng)
            m_Vignette = UIBuild.Box("Vignette", m_Root);
            UIBuild.Fill(m_Vignette);
            if (t.vignette != null) m_Vignette.style.backgroundImage = new StyleBackground(t.vignette);
            m_Vignette.style.unityBackgroundImageTintColor = new Color(0.8f, 0f, 0f, 1f);
            m_Vignette.style.opacity = 0f;
            m_Vignette.pickingMode = PickingMode.Ignore;

            m_Markers = UIBuild.Box("Markers", m_Root);
            UIBuild.Fill(m_Markers);
            m_Markers.pickingMode = PickingMode.Ignore;

            // --- Góc trên trái: chỉ số
            var stats = UIBuild.Box("Stats", m_Root);
            UIBuild.Absolute(stats, 16, 14);
            stats.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            UIBuild.Border(stats, 1, new Color(1f, 1f, 1f, 0.12f), 10);
            UIBuild.Padding(stats, 8, 12);

            var hpRow = UIBuild.Row(stats);
            UIBuild.Icon(t.heart, 22, t.health, hpRow).style.marginRight = 8;
            UIBuild.Bar(hpRow, 230, 16, t.health, out m_HealthFill);
            m_HealthText = UIBuild.Text("100", 15, Color.white, hpRow, bold: true);
            m_HealthText.style.marginLeft = 8;

            var stRow = UIBuild.Row(stats);
            stRow.style.marginTop = 6;
            UIBuild.Icon(t.stamina, 18, t.staminaColor, stRow).style.marginRight = 10;
            UIBuild.Bar(stRow, 180, 9, t.staminaColor, out m_StaminaFill);

            var enRow = UIBuild.Row(stats);
            enRow.style.marginTop = 6;
            UIBuild.Icon(t.lantern, 18, t.energyColor, enRow).style.marginRight = 10;
            UIBuild.Bar(enRow, 180, 9, t.energyColor, out m_EnergyFill);
            m_LanternState = UIBuild.Text("", 13, t.energyColor, enRow, bold: true);
            m_LanternState.style.marginLeft = 8;

            // --- Góc trên phải: thời gian, vật phẩm, điểm
            var info = UIBuild.Box("Info", m_Root);
            UIBuild.Absolute(info, null, 14, 16);
            info.style.alignItems = Align.FlexEnd;
            info.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            UIBuild.Border(info, 1, new Color(1f, 1f, 1f, 0.12f), 10);
            UIBuild.Padding(info, 8, 12);

            var clockRow = UIBuild.Row(info);
            UIBuild.Icon(t.clock, 20, new Color(0.75f, 0.8f, 1f), clockRow).style.marginRight = 8;
            m_Clock = UIBuild.Text("00:00", 26, Color.white, clockRow, bold: true);
            var dawn = UIBuild.Text("  / 06:00 bình minh", 13, t.muted, clockRow);
            dawn.style.marginTop = 8;

            var relicRow = UIBuild.Row(info);
            relicRow.style.marginTop = 6;
            UIBuild.Text("Mảnh hồn ", 13, t.muted, relicRow);
            m_RelicIcons = new VisualElement[PlayerInventory.RelicsRequired];
            for (int i = 0; i < m_RelicIcons.Length; i++)
            {
                m_RelicIcons[i] = UIBuild.Icon(t.relic, 22, new Color(1f, 1f, 1f, 0.18f), relicRow);
                m_RelicIcons[i].style.marginLeft = 3;
            }
            UIBuild.Text("   ", 13, t.muted, relicRow);
            m_KeyBlue = UIBuild.Icon(t.key, 20, new Color(0.3f, 0.6f, 1f), relicRow);
            m_KeyRed = UIBuild.Icon(t.key, 20, new Color(1f, 0.3f, 0.3f), relicRow);
            m_KeyRed.style.marginLeft = 4;

            var itemRow = UIBuild.Row(info);
            itemRow.style.marginTop = 6;
            UIBuild.Icon(t.shard, 18, new Color(0.5f, 0.9f, 1f), itemRow);
            m_Shards = UIBuild.Text("0", 16, Color.white, itemRow, bold: true);
            m_Shards.style.marginLeft = 4;
            m_Shards.style.marginRight = 14;
            UIBuild.Icon(t.potion, 18, new Color(1f, 0.4f, 0.45f), itemRow);
            m_Potions = UIBuild.Text("0", 16, Color.white, itemRow, bold: true);
            m_Potions.style.marginLeft = 4;
            m_Potions.style.marginRight = 14;
            m_Score = UIBuild.Text("0", 16, t.gold, itemRow, bold: true);

            // --- Nhiệm vụ (bên phải)
            m_Objectives = UIBuild.Box("Objectives", m_Root);
            UIBuild.Absolute(m_Objectives, null, 140, 16);
            m_Objectives.style.maxWidth = 330;
            m_Objectives.style.alignItems = Align.FlexEnd;
            var qHead = UIBuild.Row(m_Objectives);
            UIBuild.Icon(t.quest, 16, t.gold, qHead).style.marginRight = 6;
            UIBuild.Text("NHIỆM VỤ  (Tab)", 14, t.gold, qHead, bold: true);

            // --- Trạng thái phát hiện (giữa trên)
            m_Status = UIBuild.Text("", 22, Color.white, m_Root, bold: true);
            UIBuild.Absolute(m_Status, null, 16);
            m_Status.style.alignSelf = Align.Center;
            UIBuild.Padding(m_Status, 4, 16);
            UIBuild.Border(m_Status, 0, Color.clear, 6);

            // --- Tâm ngắm
            m_Crosshair = UIBuild.Box("Crosshair", m_Root);
            UIBuild.Absolute(m_Crosshair);
            m_Crosshair.style.left = Length.Percent(50);
            m_Crosshair.style.top = Length.Percent(50);
            m_Crosshair.style.width = 8;
            m_Crosshair.style.height = 8;
            m_Crosshair.style.marginLeft = -4;
            m_Crosshair.style.marginTop = -4;
            UIBuild.Border(m_Crosshair, 2, new Color(1f, 1f, 1f, 0.8f), 4);

            m_Fps = UIBuild.Text("", 13, new Color(0.6f, 1f, 0.6f), m_Root);
            UIBuild.Absolute(m_Fps, null, null, 16, 14);
        }

        // ------------------------------------------------------------------ Cập nhật

        void Update()
        {
            if (m_Root == null) return;
            if (m_Stats == null) ResolvePlayer();
            if (m_Stats == null) return;

            var session = GameSession.Instance;
            bool hide = DialogueManager.IsDialogueActive || session == null
                        || session.CurrentPhase == GameSession.Phase.GameOver || session.CurrentPhase == GameSession.Phase.Victory;
            m_Root.style.display = hide ? DisplayStyle.None : DisplayStyle.Flex;
            if (hide) return;

            var t = UITheme.Current;
            m_HealthFill.style.width = Length.Percent(100f * m_Stats.Health / m_Stats.maxHealth);
            m_HealthText.text = Mathf.CeilToInt(m_Stats.Health).ToString();
            m_StaminaFill.style.width = Length.Percent(100f * m_Stats.Stamina / m_Stats.maxStamina);
            m_StaminaFill.style.backgroundColor = m_Stats.IsExhausted ? new Color(0.9f, 0.4f, 0.2f) : t.staminaColor;
            m_EnergyFill.style.width = Length.Percent(100f * m_Stats.Energy / m_Stats.maxEnergy);
            m_LanternState.text = m_Combat != null && m_Combat.IsLanternOn ? "ĐÈN BẬT" : "";

            m_Clock.text = session.ClockText;
            m_Clock.style.color = session.TimeRemaining < 120f
                ? Color.Lerp(Color.white, t.danger, Mathf.PingPong(Time.unscaledTime * 2f, 1f))
                : Color.white;
            m_Score.text = $"{session.Score} điểm";

            if (m_Inventory != null)
            {
                for (int i = 0; i < m_RelicIcons.Length; i++)
                    m_RelicIcons[i].style.unityBackgroundImageTintColor = i < m_Inventory.RelicCount ? t.gold : new Color(1f, 1f, 1f, 0.18f);
                m_KeyBlue.style.display = m_Inventory.HasKey("keyBlue") ? DisplayStyle.Flex : DisplayStyle.None;
                m_KeyRed.style.display = m_Inventory.HasKey("keyRed") ? DisplayStyle.Flex : DisplayStyle.None;
                m_Shards.text = m_Inventory.Shards.ToString();
                m_Potions.text = TouchInput.Enabled ? m_Inventory.PotionsLabel : $"{m_Inventory.PotionsLabel} (H)";
            }

            UpdateObjectives();
            UpdateStatus();
            UpdateMarkers();
            UpdateVignette();

            var cams = CameraModeManager.Instance;
            bool aiming = cams != null && cams.Current != null && cams.Current.moveBasis == MoveBasis.CameraYaw;
            m_Crosshair.style.display = aiming ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_Combat != null) m_Crosshair.style.opacity = 0.35f + 0.65f * m_Combat.CooldownRatio;

            if (GameSettings.ShowFps)
            {
                m_FpsFrames++;
                m_FpsTimer += Time.unscaledDeltaTime;
                if (m_FpsTimer >= 0.5f)
                {
                    m_Fps.text = $"{m_FpsFrames / m_FpsTimer:0} FPS";
                    m_FpsFrames = 0;
                    m_FpsTimer = 0f;
                }
            }
            else m_Fps.text = "";
        }

        void UpdateObjectives()
        {
            var quests = QuestManager.Instance;
            int shown = 0;
            if (quests != null)
            {
                foreach (var s in quests.States)
                {
                    if (!s.visible || s.complete) continue;
                    if (shown >= 4) break;
                    if (shown >= m_ObjectiveLabels.Count)
                    {
                        var l = UIBuild.Text("", 15, Color.white, m_Objectives);
                        l.style.unityTextAlign = TextAnchor.MiddleRight;
                        l.style.marginTop = 3;
                        l.style.backgroundColor = new Color(0f, 0f, 0f, 0.4f);
                        UIBuild.Padding(l, 3, 8);
                        UIBuild.Border(l, 0, Color.clear, 4);
                        m_ObjectiveLabels.Add(l);
                    }
                    var label = m_ObjectiveLabels[shown++];
                    label.text = (s.objective.optional ? "(Phụ) " : "• ") + s.Text;
                    label.style.color = s.objective.optional ? UITheme.Current.muted : Color.white;
                    label.style.display = DisplayStyle.Flex;
                }
            }
            for (int i = shown; i < m_ObjectiveLabels.Count; i++)
                m_ObjectiveLabels[i].style.display = DisplayStyle.None;
        }

        void UpdateStatus()
        {
            var session = GameSession.Instance;
            float maxAwareness = 0f;
            foreach (var e in EnemyAI.All) if (e.IsAlive) maxAwareness = Mathf.Max(maxAwareness, e.Awareness);
            foreach (var s in SentryAI.All) maxAwareness = Mathf.Max(maxAwareness, s.Awareness);

            if (session.AnyEnemyChasing)
            {
                m_Status.text = "BỊ PHÁT HIỆN!";
                m_Status.style.color = Color.white;
                m_Status.style.backgroundColor = new Color(0.75f, 0.1f, 0.08f, 0.6f + 0.3f * Mathf.PingPong(Time.unscaledTime * 3f, 1f));
            }
            else if (maxAwareness > 0.25f)
            {
                m_Status.text = "Có gì đó đang nghi ngờ...";
                m_Status.style.color = new Color(1f, 0.85f, 0.3f);
                m_Status.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            }
            else if (m_Movement != null && m_Movement.IsCrouching)
            {
                m_Status.text = "Đang ẩn nấp";
                m_Status.style.color = new Color(0.6f, 0.85f, 1f);
                m_Status.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
            }
            else
            {
                m_Status.text = "";
                m_Status.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
        }

        void UpdateMarkers()
        {
            var cam = Camera.main;
            var panel = m_Doc.rootVisualElement.panel;
            int used = 0;
            if (cam != null && panel != null)
            {
                Vector3 playerPos = m_Stats.transform.position;
                foreach (var e in EnemyAI.All)
                {
                    if (!e.IsAlive || (e.Awareness < 0.05f && !e.RecentlyDamaged && !e.IsChasing)) continue;
                    PlaceMarker(ref used, cam, panel, e.transform.position + Vector3.up * 1.9f, playerPos,
                        e.IsChasing ? "!" : "?", e.IsChasing ? 1f : e.Awareness, e.RecentlyDamaged ? e.HealthRatio : -1f);
                }
                foreach (var s in SentryAI.All)
                {
                    if (s.Awareness < 0.05f) continue;
                    PlaceMarker(ref used, cam, panel, s.transform.position + Vector3.up * 2.3f, playerPos,
                        s.Awareness >= 1f ? "!" : "?", s.Awareness, -1f);
                }
            }
            for (int i = used; i < m_MarkerPool.Count; i++)
                m_MarkerPool[i].root.style.display = DisplayStyle.None;
        }

        void PlaceMarker(ref int used, Camera cam, IPanel panel, Vector3 world, Vector3 playerPos, string symbol, float level, float hp)
        {
            if ((world - playerPos).sqrMagnitude > 30f * 30f) return;
            Vector3 vp = cam.WorldToViewportPoint(world);
            if (vp.z < 0.1f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) return;

            if (used >= m_MarkerPool.Count)
            {
                var root = UIBuild.Box("Marker", m_Markers);
                root.style.position = Position.Absolute;
                root.style.alignItems = Align.Center;
                root.style.width = 60;
                root.style.marginLeft = -30;
                var icon = UIBuild.Text("?", 30, Color.yellow, root, bold: true);
                icon.style.unityTextAlign = TextAnchor.MiddleCenter;
                var track = UIBuild.Bar(root, 46, 5, UITheme.Current.danger, out var fill);
                track.name = "HP";
                m_MarkerPool.Add((root, icon, fill));
            }

            var m = m_MarkerPool[used++];
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, cam);
            m.root.style.left = p.x;
            m.root.style.top = p.y - 40f;
            m.root.style.display = DisplayStyle.Flex;
            m.icon.text = symbol;
            m.icon.style.color = symbol == "!" ? new Color(1f, 0.2f, 0.15f) : new Color(1f, 0.85f, 0.2f);
            m.icon.style.fontSize = 22 + level * 12f;
            m.icon.style.opacity = 0.4f + 0.6f * level;
            var hpTrack = m.hpFill.parent;
            hpTrack.style.display = hp >= 0f ? DisplayStyle.Flex : DisplayStyle.None;
            if (hp >= 0f) m.hpFill.style.width = Length.Percent(100f * hp);
        }

        void UpdateVignette()
        {
            m_DamageFlash = Mathf.MoveTowards(m_DamageFlash, 0f, Time.unscaledDeltaTime * 1.5f);
            float low = m_Stats.IsLowHealth ? 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
            m_Vignette.style.opacity = Mathf.Clamp01(Mathf.Max(m_DamageFlash, low));
        }
    }
}
