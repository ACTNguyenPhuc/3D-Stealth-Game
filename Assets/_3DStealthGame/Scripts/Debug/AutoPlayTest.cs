using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using IKey = UnityEngine.InputSystem.Key;

namespace StealthGame
{
    /// <summary>
    /// Kiểm thử tự động toàn bộ luồng game (chỉ chạy khi có tham số dòng lệnh -autotest):
    /// Menu → Chơi mới → camera → di chuyển (bàn phím ảo) → hội thoại → nhật ký → pause → nhặt Mảnh hồn
    /// → chiến đấu → bị phát hiện → checkpoint → chết → hồi sinh (kiểm tra save) → phá phong ấn → chiến thắng → về menu.
    /// Chụp màn hình từng bước và ghi báo cáo PASS/FAIL + số lỗi runtime.
    /// </summary>
    public class AutoPlayTest : MonoBehaviour
    {
        string m_Out;
        readonly List<string> m_Report = new();
        readonly List<string> m_Errors = new();
        int m_Pass, m_Fail;
        Keyboard m_Keyboard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("-autotest")) return;
            var go = new GameObject("[AutoPlayTest]");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<AutoPlayTest>();
            int i = Array.IndexOf(args, "-autotestOut");
            t.m_Out = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.persistentDataPath, "autotest");
        }

        bool m_Finished;

        void Awake()
        {
            Application.logMessageReceived += OnLog;
            // Ghi lại ai yêu cầu thoát game giữa chừng (nút Thoát, đóng cửa sổ...)
            Application.wantsToQuit += () =>
            {
                if (!m_Finished)
                    Debug.LogWarning("[AutoTest] Có yêu cầu thoát game trước khi kiểm thử xong: " + Environment.StackTrace);
                return true;
            };
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                m_Errors.Add($"[{type}] {condition}\n{stack}");
        }

        void Start()
        {
            Directory.CreateDirectory(m_Out);
            SaveSystem.Delete();
            // Kiểm thử chạy cả khi cửa sổ không được focus (người dùng đang làm việc khác)
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            m_Keyboard = InputSystem.AddDevice<Keyboard>("AutoTestKeyboard");
            StartCoroutine(Run());
        }

        // ------------------------------------------------------------------ Tiện ích

        void Log(string msg)
        {
            Debug.Log("[AutoTest] " + msg);
            m_Report.Add(msg);
        }

        void Check(string name, bool ok)
        {
            if (ok) m_Pass++; else m_Fail++;
            Log((ok ? "PASS  " : "FAIL  ") + name);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(m_Out, name + ".png"));
            yield return null;
            yield return null;
            Log("screenshot " + name);
        }

        static IEnumerator Wait(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

        IEnumerator WaitForScene(string scene)
        {
            float t = 0f;
            while ((SceneManager.GetActiveScene().name != scene || GameServices.IsLoading) && t < 30f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return Wait(1f);
        }

        void Keys(params IKey[] keys) => InputSystem.QueueStateEvent(m_Keyboard, new KeyboardState(keys));

        static PlayerStats Player => PlayerStats.Instance;
        static PlayerInventory Inventory => Player != null ? Player.GetComponent<PlayerInventory>() : null;

        static void Teleport(Vector3 pos, float yaw = 0f)
        {
            var move = Player.GetComponent<PlayerMovement>();
            move.Teleport(pos, Quaternion.Euler(0f, yaw, 0f));
        }

        // ------------------------------------------------------------------ Kịch bản

        IEnumerator Run()
        {
            Log($"Bắt đầu kiểm thử – {DateTime.Now}");
            yield return WaitForScene(GameServices.MenuScene);
            yield return Wait(2f);
            Check("Menu chính hiển thị", MainMenuUI.Instance != null);
            yield return Shot("01_main_menu");

            // ---------- Chơi mới
            GameServices.StartNewGame(Difficulty.Normal);
            yield return WaitForScene(GameServices.GameScene);
            yield return Wait(2.5f);
            Check("GameSession khởi tạo", GameSession.Instance != null && GameSession.Instance.CurrentPhase == GameSession.Phase.Playing);
            Check("Có save checkpoint đầu", SaveSystem.HasSave);
            Check("Có kẻ địch trên NavMesh", EnemyAI.All.Count >= 5 && EnemyAI.All.All(e => e.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh));
            yield return Shot("02_game_start");

            // ---------- Điều khiển cảm ứng (khi chạy với -touch hoặc trên điện thoại)
            if (TouchInput.Enabled)
            {
                yield return Wait(0.5f);
                Check("Điều khiển cảm ứng hiển thị", TouchControls.Instance != null && TouchControls.Instance.IsVisible);
                Vector3 t0 = Player.transform.position;
                TouchControls.Instance.DebugSetStick(new Vector2(1f, 0f) * 0.6f);
                yield return Wait(1.2f);
                TouchControls.Instance.DebugSetStick(Vector2.zero);
                float moved = Vector3.Distance(t0, Player.transform.position);
                Check($"Joystick ảo di chuyển nhân vật ({moved:0.00} m)", moved > 0.4f);
                TouchInput.SetButton(TouchInput.Button.Crouch, true);
                TouchInput.Flush();
                yield return null; yield return null;
                TouchInput.SetButton(TouchInput.Button.Crouch, false);
                TouchInput.Flush();
                yield return Wait(0.3f);
                Check("Nút NGỒI cảm ứng", Player.GetComponent<PlayerMovement>().IsCrouching);
                TouchInput.SetButton(TouchInput.Button.Crouch, true); TouchInput.Flush();
                yield return null; yield return null;
                TouchInput.SetButton(TouchInput.Button.Crouch, false); TouchInput.Flush();
                yield return Shot("00_touch_controls");
            }

            // ---------- Camera
            var cams = CameraModeManager.Instance;
            for (int i = 0; i < cams.modes.Count; i++)
            {
                cams.SetMode(i);
                yield return Wait(1.6f);
                yield return Shot($"03_camera_{i + 1}");
            }
            cams.SetMode(0);
            yield return Wait(1f);

            // ---------- Di chuyển bằng bàn phím ảo
            Vector3 p0 = Player.transform.position;
            Keys(IKey.W);
            yield return Wait(1.2f);
            Keys();
            float walked = Vector3.Distance(p0, Player.transform.position);
            Check($"Đi bộ (W) – di chuyển {walked:0.00} m", walked > 0.5f);

            p0 = Player.transform.position;
            float stamina0 = Player.Stamina;
            Keys(IKey.S, IKey.LeftShift);
            yield return Wait(1.0f);
            Keys();
            Check($"Chạy (Shift) tốn thể lực ({stamina0:0} → {Player.Stamina:0})", Player.Stamina < stamina0);

            Keys(IKey.Space);
            yield return Wait(0.15f);
            Keys();
            yield return Wait(0.1f);
            Check("Nhảy (Space) rời mặt đất", !Player.GetComponent<PlayerMovement>().IsGrounded || Player.transform.position.y > 0.1f);
            yield return Wait(1f);

            Keys(IKey.C);
            yield return null; yield return null;
            Keys();
            yield return Wait(0.3f);
            Check("Ngồi (C)", Player.GetComponent<PlayerMovement>().IsCrouching);
            yield return Shot("04_crouch");
            Keys(IKey.C);
            yield return null; yield return null;
            Keys();

            // ---------- Hội thoại với Ivan
            var ivan = NPCDialogue.All.FirstOrDefault(n => n.name == "NPC_Ivan");
            Teleport(ivan.transform.position + ivan.transform.forward * 1.3f, ivan.transform.eulerAngles.y + 180f);
            yield return Wait(0.5f);
            DialogueManager.Instance.StartDialogue(ivan);
            yield return Wait(1.8f);
            Check("Hội thoại bắt đầu (thời gian dừng)", DialogueManager.IsDialogueActive && Time.timeScale == 0f);
            yield return Shot("05_dialogue");
            for (int i = 0; i < 8 && DialogueManager.IsDialogueActive; i++)
            {
                DialogueManager.Instance.Advance();   // hiện hết chữ
                DialogueManager.Instance.Advance();   // câu tiếp (nếu không có lựa chọn)
                DialogueManager.Instance.Choose(1);   // chọn "Làm sao để thoát ra?" / ...
                yield return Wait(0.3f);
            }
            yield return Shot("06_dialogue_menu");
            DialogueManager.Instance.EndDialogue();
            yield return Wait(0.5f);
            Check("Cờ ivan_met & nhiệm vụ mở", DialogueManager.Instance.HasFlag("ivan_met"));

            // ---------- Nhật ký & pause
            JournalUI.Instance.Open();
            yield return Wait(0.6f);
            Check("Nhật ký mở – game dừng", Time.timeScale == 0f);
            yield return Shot("07_journal");
            JournalUI.Instance.Close();
            yield return Wait(0.3f);
            GameSession.Instance.SetPaused(true);
            yield return Wait(0.6f);
            Check("Pause menu", PauseMenuUI.Instance.IsOpen && Time.timeScale == 0f);
            yield return Shot("08_pause");
            GameSession.Instance.SetPaused(false);
            yield return Wait(0.5f);
            Check("Tiếp tục sau pause", Time.timeScale == 1f);

            // ---------- Nhặt Mảnh hồn trong bếp
            var kitchenRelic = FindObjectsByType<Pickup>().FirstOrDefault(p => p.pickupId == "relic_kitchen");
            Teleport(kitchenRelic.transform.position + Vector3.back * 0.6f);
            yield return Wait(1.2f);
            Check("Nhặt Mảnh hồn Bình Minh", Inventory.RelicCount == 1);
            yield return Shot("09_relic");

            // ---------- Chiến đấu: Linh Quang vào hồn ma
            var ghost = EnemyAI.All.FirstOrDefault(e => e.enemyId == "ghost_corridor_w");
            if (ghost != null)
            {
                var gpos = ghost.transform.position;
                // Đứng sau lưng ma, cắt theo NavMesh để không lọt vào trong tường
                Vector3 back = -ghost.transform.forward;
                float dist = 2.2f;
                if (UnityEngine.AI.NavMesh.Raycast(gpos, gpos + back * dist, out var hitBack, UnityEngine.AI.NavMesh.AllAreas))
                {
                    if (hitBack.distance > 1.3f) dist = hitBack.distance - 0.3f;
                    else { back = -back; dist = 2f; }   // phía sau quá hẹp → đứng phía trước
                }
                Vector3 behind = gpos + back * dist;
                Teleport(behind, Quaternion.LookRotation(gpos - behind).eulerAngles.y);
                cams.SetMode(1);   // top-down: tấn công theo hướng nhân vật
                yield return Wait(0.3f);
                var combat = Player.GetComponent<PlayerCombat>();
                Player.AddEnergy(100f);
                float hp0 = ghost.Health;
                combat.TryBurst();
                yield return Wait(0.4f);
                yield return Shot("10_combat_burst");
                Check($"Linh Quang gây sát thương ({hp0:0} → {ghost.Health:0}) và làm choáng", ghost.Health < hp0);
                for (int i = 0; i < 4 && ghost.IsAlive; i++)
                {
                    yield return Wait(1.0f);
                    Player.AddEnergy(100f);
                    var to = ghost.transform.position - Player.transform.position;
                    Player.GetComponent<PlayerMovement>().FaceDirection(to);
                    combat.TryBurst();
                }
                yield return Wait(1.5f);
                Check("Trục xuất hồn ma (trạng thái Dead)", !ghost.IsAlive);
                yield return Shot("11_banished");
                cams.SetMode(0);
            }

            // ---------- Bị phát hiện
            var watcher = EnemyAI.All.FirstOrDefault(e => e.IsAlive && e.enemyId == "ghost_east");
            if (watcher != null)
            {
                // Đứng trước mặt ma, trong cùng phòng (không xuyên tường)
                Vector3 front = watcher.transform.position + watcher.transform.forward * 3f;
                if (UnityEngine.AI.NavMesh.Raycast(watcher.transform.position, front, out var navHit, UnityEngine.AI.NavMesh.AllAreas))
                    front = watcher.transform.position + watcher.transform.forward * Mathf.Max(1.3f, navHit.distance - 0.4f);
                Teleport(front, watcher.transform.eulerAngles.y + 180f);
                float t = 0f;
                while (!watcher.IsChasing && t < 4f) { t += Time.deltaTime; yield return null; }
                Check("Hồn ma phát hiện & truy đuổi", watcher.IsChasing);
                yield return Wait(0.6f);
                yield return Shot("12_detected");
            }

            // ---------- Checkpoint (lưu Mảnh hồn) rồi chết
            var cp = Checkpoint.All.FirstOrDefault(c => c.checkpointId == "cp_center");
            Teleport(cp.transform.position + cp.transform.forward * 0.6f);
            yield return Wait(1.0f);
            var save = SaveSystem.Load();
            Check("Checkpoint lưu tiến trình", save != null && save.checkpointId == "cp_center" && save.relics.Count == 1);
            yield return Shot("13_checkpoint");

            Player.TakeDamage(65f, Player.transform.position + Vector3.forward);
            yield return Wait(0.5f);
            yield return Shot("14_low_health");
            Player.Kill();
            yield return Wait(3.5f);
            Check("Màn hình Game Over", GameSession.Instance.CurrentPhase == GameSession.Phase.GameOver);
            yield return Shot("15_game_over");

            GameSession.Instance.RespawnAtCheckpoint();
            yield return WaitForScene(GameServices.GameScene);
            yield return Wait(2f);
            Check("Hồi sinh tại checkpoint giữ Mảnh hồn", Inventory.RelicCount == 1 && GameSession.Instance.Deaths == 1);
            Check("Vị trí hồi sinh gần cp_center",
                Vector3.Distance(Player.transform.position, Checkpoint.All.First(c => c.checkpointId == "cp_center").transform.position) < 3f);
            yield return Shot("16_respawn");

            // ---------- Hoàn thành màn: tắt AI để kiểm tra luồng chiến thắng ổn định
            foreach (var e in EnemyAI.All.ToList()) e.gameObject.SetActive(false);
            foreach (var s in SentryAI.All.ToList()) s.gameObject.SetActive(false);
            var interactor = Player.GetComponent<PlayerInteractor>();

            var keyRed = FindObjectsByType<Key>().FirstOrDefault(k => k.KeyName == "keyRed");
            if (keyRed != null)
            {
                Teleport(keyRed.transform.position);
                yield return Wait(1f);
            }
            Check("Nhặt chìa khóa đỏ", Inventory.HasKey("keyRed"));
            var keyBlue = FindObjectsByType<Key>().FirstOrDefault(k => k.KeyName == "keyBlue");
            if (keyBlue != null)
            {
                Teleport(keyBlue.transform.position);
                yield return Wait(1f);
            }
            Check("Nhặt chìa khóa xanh", Inventory.HasKey("keyBlue"));

            foreach (var seal in FindObjectsByType<KeySeal>())
            {
                Teleport(seal.transform.position + Vector3.back * 1.6f);
                yield return Wait(0.4f);
                Check($"{seal.sealName}: có thể tương tác", seal.CanInteract(interactor));
                seal.Interact(interactor);
                yield return Wait(1.2f);
                if (seal.sealId == "seal_blue") yield return Shot("17_seal_open");
                var relic = seal.transform.parent.GetComponentInChildren<Pickup>();
                if (relic != null)
                {
                    Teleport(seal.transform.position + Vector3.back * 0.5f);
                    yield return Wait(1.2f);
                }
            }
            Check("Đủ 3 Mảnh hồn", Inventory.RelicCount == 3);

            var gate = FindAnyObjectByType<ExitGate>();
            Teleport(gate.transform.position + Vector3.left * 1.5f, 90f);
            yield return Wait(0.5f);
            yield return Shot("18_exit_gate");
            gate.Interact(interactor);
            yield return Wait(1.5f);
            Check("Phong ấn cửa chính đã vỡ", gate.IsOpen);
            Teleport(gate.exitZone.transform.position, 90f);
            yield return Wait(3.5f);
            Check("Màn hình chiến thắng", GameSession.Instance.CurrentPhase == GameSession.Phase.Victory);
            Check($"Xếp hạng được tính (hạng {GameSession.Instance.LastResult.rank}, {GameSession.Instance.LastResult.total} điểm)",
                !string.IsNullOrEmpty(GameSession.Instance.LastResult.rank));
            yield return Shot("19_victory");

            // ---------- Về menu
            GameSession.Instance.QuitToMenu();
            yield return WaitForScene(GameServices.MenuScene);
            yield return Wait(1.5f);
            Check("Quay về menu chính", MainMenuUI.Instance != null);
            Check("Kỷ lục được lưu", SaveSystem.LoadRecord().wins >= 1);
            yield return Shot("20_menu_after");

            // ---------- Báo cáo
            Log($"KẾT QUẢ: {m_Pass} PASS, {m_Fail} FAIL, {m_Errors.Count} lỗi runtime");
            var lines = new List<string>(m_Report);
            if (m_Errors.Count > 0)
            {
                lines.Add("");
                lines.Add("===== LỖI RUNTIME =====");
                lines.AddRange(m_Errors.Take(50));
            }
            File.WriteAllLines(Path.Combine(m_Out, "report.txt"), lines);
            yield return Wait(0.5f);
            m_Finished = true;
            Application.Quit(m_Fail == 0 && m_Errors.Count == 0 ? 0 : 3);
        }
    }
}
