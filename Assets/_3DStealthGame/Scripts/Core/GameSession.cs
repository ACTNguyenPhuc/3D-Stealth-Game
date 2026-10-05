using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StealthGame
{
    /// <summary>
    /// Điều phối một lượt chơi trong scene gameplay:
    ///  - Khởi tạo từ "Chơi mới" hoặc "Tiếp tục" (nạp save, đặt người chơi ở checkpoint).
    ///  - Trạng thái: Playing / Paused / GameOver / Victory (timeScale, con trỏ chuột, input).
    ///  - Đồng hồ "trước bình minh", điểm số, thống kê, nhạc nền theo tình huống.
    ///  - Lưu tại checkpoint, hồi sinh, chiến thắng & xếp hạng.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GameSession : MonoBehaviour
    {
        public enum Phase { Playing, Paused, GameOver, Victory }
        public enum EndReason { Caught, Dawn }

        public static GameSession Instance { get; private set; }
        public static Difficulty CurrentDifficulty { get; private set; } = Difficulty.Normal;

        [Header("Tham chiếu")]
        public PlayerStats player;
        public Transform defaultSpawn;

        [Header("Điểm")]
        public int scorePerShard = 50;
        public int scorePerRelic = 1000;

        public Phase CurrentPhase { get; private set; } = Phase.Playing;
        public DifficultyProfile Profile { get; private set; }
        public float Elapsed { get; private set; }
        public float TimeLimit { get; private set; }
        public float TimeRemaining => Mathf.Max(0f, TimeLimit - Elapsed);
        public int Score { get; private set; }
        public int Deaths { get; private set; }
        public int Banished { get; private set; }
        public int Detections { get; private set; }
        public int ShardsCollected { get; private set; }
        public bool AnyEnemyChasing { get; private set; }
        public bool IsUIOpen => m_UIOpenCount > 0;
        public string CheckpointId => m_CheckpointId;

        readonly HashSet<string> m_Collected = new();
        readonly HashSet<string> m_Opened = new();
        readonly HashSet<string> m_Checkpoints = new();
        SaveData m_Loaded;
        string m_CheckpointId = "cp_start";
        bool m_IsNewGame;
        int m_UIOpenCount;
        float m_ChaseMusicUntil;
        float m_HeartbeatTimer;

        PlayerInventory m_Inventory;
        PlayerMovement m_Movement;

        // ------------------------------------------------------------------ Khởi tạo

        void Awake()
        {
            Instance = this;

            if (GameServices.LaunchContinue && SaveSystem.HasSave)
                m_Loaded = SaveSystem.Load();
            m_IsNewGame = m_Loaded == null;
            if (m_IsNewGame)
                m_Loaded = new SaveData { difficulty = GameServices.LaunchDifficulty };

            CurrentDifficulty = m_Loaded.difficulty;
            Profile = DifficultyProfile.Get(CurrentDifficulty);
            TimeLimit = Profile.dawnMinutes * 60f;

            foreach (var id in m_Loaded.collected) m_Collected.Add(id);
            foreach (var id in m_Loaded.opened) m_Opened.Add(id);
            foreach (var id in m_Loaded.checkpoints) m_Checkpoints.Add(id);
            m_CheckpointId = m_Loaded.checkpointId;
            Elapsed = m_Loaded.elapsedSeconds;
            Score = m_Loaded.score;
            Deaths = m_Loaded.deaths;
            Banished = m_Loaded.banished;
            Detections = m_Loaded.detections;
            ShardsCollected = m_Loaded.shardsCollected;

            if (player == null) player = FindAnyObjectByType<PlayerStats>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        void OnEnable()
        {
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.EnemyBanished += OnEnemyBanished;
            GameEvents.EnemyAlerted += OnEnemyAlerted;
            GameEvents.ItemCollected += OnItemCollected;
        }

        void OnDisable()
        {
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.EnemyBanished -= OnEnemyBanished;
            GameEvents.EnemyAlerted -= OnEnemyAlerted;
            GameEvents.ItemCollected -= OnItemCollected;
        }

        void Start()
        {
            m_Inventory = player.GetComponent<PlayerInventory>();
            m_Movement = player.GetComponent<PlayerMovement>();

            // Trạng thái nhân vật
            if (m_IsNewGame)
            {
                m_Inventory.Restore(new string[0], new string[0], Profile.startPotions, 0);
            }
            else
            {
                player.SetValues(m_Loaded.health, m_Loaded.energy);
                m_Inventory.Restore(m_Loaded.keys, m_Loaded.relics, m_Loaded.potions, m_Loaded.shards);
            }

            if (DialogueManager.Instance != null)
                foreach (var f in m_Loaded.flags) DialogueManager.Instance.SetFlag(f);

            // Vị trí xuất phát
            var cp = Checkpoint.All.FirstOrDefault(c => c.checkpointId == m_CheckpointId);
            Transform spawn = cp != null ? cp.SpawnPoint : defaultSpawn;
            if (spawn != null && m_Movement != null)
                m_Movement.Teleport(spawn.position, spawn.rotation);

            SetPhase(Phase.Playing);
            AudioManager.Music(SoundId.MusicExplore, 2f);

            if (m_IsNewGame)
            {
                // Lưu ngay để "Hồi sinh" luôn có điểm quay về
                var startCp = Checkpoint.All.FirstOrDefault(c => c.checkpointId == "cp_start");
                if (startCp != null) startCp.Activate(player, false);
                else SaveNow();
                GameEvents.RaiseToast("Tìm Ivan để biết cách thoát khỏi dinh thự. (Tab: Nhật ký)");
            }
            else
            {
                GameEvents.RaiseToast($"Tiếp tục từ checkpoint. Lần chết: {Deaths}");
            }
        }

        // ------------------------------------------------------------------ Update

        void Update()
        {
            if (CurrentPhase == Phase.Playing)
            {
                Elapsed += Time.deltaTime;
                if (Elapsed >= TimeLimit && player.IsAlive)
                {
                    GameOver(EndReason.Dawn);
                    return;
                }

                UpdateMusic();
                UpdateHeartbeat();

                if (!DialogueManager.IsBlockingInput && !GameServices.IsLoading)
                {
                    if (GameInput.Pause != null && GameInput.Pause.WasPressedThisFrame() && !IsUIOpen)
                        SetPaused(true);
                    else if (GameInput.Journal != null && GameInput.Journal.WasPressedThisFrame() && JournalUI.Instance != null)
                        JournalUI.Instance.Toggle();
                }
            }
            else if (CurrentPhase == Phase.Paused)
            {
                if (GameInput.Pause != null && GameInput.Pause.WasPressedThisFrame() && PauseMenuUI.Instance != null)
                    PauseMenuUI.Instance.Back();
            }
        }

        void LateUpdate()
        {
            // Chính sách con trỏ chuột tập trung một chỗ
            bool wantLock = CurrentPhase == Phase.Playing && !IsUIOpen && !DialogueManager.IsDialogueActive && player != null && player.IsAlive;
            var lockState = wantLock ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != lockState) Cursor.lockState = lockState;
            if (Cursor.visible == wantLock) Cursor.visible = !wantLock;
        }

        void UpdateMusic()
        {
            bool chasing = false;
            foreach (var e in EnemyAI.All)
            {
                if (e.IsChasing) { chasing = true; break; }
            }
            AnyEnemyChasing = chasing;
            if (chasing) m_ChaseMusicUntil = Time.time + 4f;
            AudioManager.Music(Time.time < m_ChaseMusicUntil ? SoundId.MusicChase : SoundId.MusicExplore, 1.2f);
        }

        void UpdateHeartbeat()
        {
            if (!player.IsLowHealth) return;
            m_HeartbeatTimer -= Time.deltaTime;
            if (m_HeartbeatTimer <= 0f)
            {
                m_HeartbeatTimer = 0.95f;
                AudioManager.Sfx(SoundId.Heartbeat, 0.8f);
            }
        }

        // ------------------------------------------------------------------ Trạng thái

        void SetPhase(Phase phase)
        {
            CurrentPhase = phase;
            bool playing = phase == Phase.Playing;
            GameInput.GameplayEnabled = playing && !IsUIOpen;
            Time.timeScale = playing && !IsUIOpen ? 1f : 0f;
            AudioListener.pause = phase == Phase.Paused;
        }

        public void SetPaused(bool paused)
        {
            if (CurrentPhase != Phase.Playing && CurrentPhase != Phase.Paused) return;
            SetPhase(paused ? Phase.Paused : Phase.Playing);
            if (PauseMenuUI.Instance != null)
            {
                if (paused) PauseMenuUI.Instance.Show();
                else PauseMenuUI.Instance.Hide();
            }
            AudioManager.Sfx(SoundId.UIOpen, 0.6f);
        }

        /// <summary>Mở/đóng một bảng UI chặn gameplay (Nhật ký...).</summary>
        public void PushUI()
        {
            m_UIOpenCount++;
            SetPhase(CurrentPhase);
        }

        public void PopUI()
        {
            m_UIOpenCount = Mathf.Max(0, m_UIOpenCount - 1);
            SetPhase(CurrentPhase);
        }

        // ------------------------------------------------------------------ Thế giới

        public bool IsCollected(string id) => !string.IsNullOrEmpty(id) && m_Collected.Contains(id);
        public void MarkCollected(string id) { if (!string.IsNullOrEmpty(id)) m_Collected.Add(id); }
        public bool IsOpened(string id) => m_Opened.Contains(id);
        public void MarkOpened(string id) => m_Opened.Add(id);
        public bool IsCheckpointActive(string id) => m_Checkpoints.Contains(id);

        public void AddScore(int amount) => Score += Mathf.RoundToInt(amount * Profile.scoreMultiplier);

        void OnItemCollected(PickupType type, string id, int amount)
        {
            if (type == PickupType.Shard)
            {
                ShardsCollected += amount;
                AddScore(scorePerShard * amount);
            }
            else if (type == PickupType.Relic) AddScore(scorePerRelic);
        }

        void OnEnemyBanished(EnemyAI enemy)
        {
            Banished++;
            AddScore(enemy.config != null ? enemy.config.scoreValue : 200);
            GameEvents.RaiseToast($"Đã trục xuất {enemy.DisplayName}!");
        }

        void OnEnemyAlerted(EnemyAI enemy) => Detections++;

        // ------------------------------------------------------------------ Lưu

        public void SaveAtCheckpoint(Checkpoint cp)
        {
            m_CheckpointId = cp.checkpointId;
            m_Checkpoints.Add(cp.checkpointId);
            SaveNow();
        }

        public SaveData BuildSaveData()
        {
            var data = new SaveData
            {
                difficulty = CurrentDifficulty,
                checkpointId = m_CheckpointId,
                health = Mathf.Max(player.Health, player.maxHealth * 0.5f),   // hồi sinh với tối thiểu 50% máu
                energy = Mathf.Max(player.Energy, 50f),
                potions = m_Inventory.Potions,
                shards = m_Inventory.Shards,
                shardsCollected = ShardsCollected,
                score = Score,
                elapsedSeconds = Elapsed,
                deaths = Deaths,
                banished = Banished,
                detections = Detections,
                keys = m_Inventory.Keys.ToList(),
                relics = m_Inventory.Relics.ToList(),
                collected = m_Collected.ToList(),
                opened = m_Opened.ToList(),
                checkpoints = m_Checkpoints.ToList(),
                flags = DialogueManager.Instance != null ? DialogueManager.Instance.Flags.ToList() : new List<string>(),
            };
            return data;
        }

        public void SaveNow() => SaveSystem.Save(BuildSaveData());

        // ------------------------------------------------------------------ Kết thúc

        void OnPlayerDied()
        {
            Deaths++;
            // Ghi số lần chết vào save hiện có (tiến trình vẫn quay về checkpoint)
            var save = SaveSystem.Load();
            if (save != null)
            {
                save.deaths = Deaths;
                SaveSystem.Save(save);
            }
            StartCoroutine(GameOverAfter(2.2f, EndReason.Caught));
        }

        IEnumerator GameOverAfter(float delay, EndReason reason)
        {
            GameInput.GameplayEnabled = false;
            yield return new WaitForSecondsRealtime(delay);
            GameOver(reason);
        }

        void GameOver(EndReason reason)
        {
            if (CurrentPhase == Phase.GameOver || CurrentPhase == Phase.Victory) return;
            if (reason == EndReason.Dawn)
                SaveSystem.Delete();  // hết giờ = thua cả lượt
            SetPhase(Phase.GameOver);
            AudioManager.Instance?.StopMusic(0.5f);
            AudioManager.Sfx(SoundId.GameOver);
            if (EndScreenUI.Instance != null) EndScreenUI.Instance.ShowGameOver(reason);
        }

        public struct RunResult
        {
            public int baseScore, timeBonus, stealthBonus, deathPenalty, total;
            public string rank;
            public bool newRecord;
        }

        public RunResult LastResult { get; private set; }

        public void Victory()
        {
            if (CurrentPhase == Phase.Victory || CurrentPhase == Phase.GameOver) return;

            var r = new RunResult { baseScore = Score };
            r.timeBonus = Mathf.RoundToInt(TimeRemaining * 3f * Profile.scoreMultiplier);
            r.stealthBonus = Mathf.RoundToInt(((Deaths == 0 ? 1500 : 0) + (Detections == 0 ? 1000 : Mathf.Max(0, 500 - Detections * 50))) * Profile.scoreMultiplier);
            r.deathPenalty = Deaths * 250;
            r.total = Mathf.Max(0, r.baseScore + r.timeBonus + r.stealthBonus - r.deathPenalty);
            r.rank = r.total >= 9500 ? "S" : r.total >= 7500 ? "A" : r.total >= 5500 ? "B" : r.total >= 3500 ? "C" : "D";

            var record = SaveSystem.LoadRecord();
            record.wins++;
            if (r.total > record.bestScore)
            {
                record.bestScore = r.total;
                record.bestRank = r.rank;
                r.newRecord = true;
            }
            if (record.bestTime <= 0f || Elapsed < record.bestTime) record.bestTime = Elapsed;
            SaveSystem.SaveRecord(record);
            SaveSystem.Delete();   // hoàn thành lượt chơi

            LastResult = r;
            SetPhase(Phase.Victory);
            AudioManager.Instance?.StopMusic(0.5f);
            AudioManager.Sfx(SoundId.Victory);
            if (EndScreenUI.Instance != null) EndScreenUI.Instance.ShowVictory(r);
        }

        // ------------------------------------------------------------------ Điều hướng từ UI

        public void RespawnAtCheckpoint()
        {
            if (SaveSystem.HasSave) GameServices.ContinueGame();
            else GameServices.StartNewGame(CurrentDifficulty);
        }

        public void RestartNewGame() => GameServices.StartNewGame(CurrentDifficulty);
        public void QuitToMenu() => GameServices.GoToMenu();

        /// <summary>Đồng hồ trong game: 00:00 → 06:00 (bình minh).</summary>
        public string ClockText
        {
            get
            {
                float k = TimeLimit > 0f ? Mathf.Clamp01(Elapsed / TimeLimit) : 0f;
                int minutes = Mathf.FloorToInt(k * 360f);
                return $"{minutes / 60:00}:{minutes % 60:00}";
            }
        }
    }
}
