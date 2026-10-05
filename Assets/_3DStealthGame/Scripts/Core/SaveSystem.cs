using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StealthGame
{
    /// <summary>Toàn bộ trạng thái cần lưu tại checkpoint.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public Difficulty difficulty = Difficulty.Normal;
        public string checkpointId = "cp_start";
        public string savedAt;

        public float health = 100f;
        public float energy = 100f;
        public int potions;
        public int shards;
        public int shardsCollected;
        public int score;

        public float elapsedSeconds;
        public int deaths;
        public int banished;
        public int detections;

        public List<string> keys = new();
        public List<string> relics = new();
        public List<string> collected = new();   // id các vật phẩm đã nhặt
        public List<string> opened = new();      // id cửa / phong ấn đã mở
        public List<string> flags = new();       // cờ hội thoại / cốt truyện
        public List<string> checkpoints = new(); // checkpoint đã kích hoạt
    }

    /// <summary>Kỷ lục tốt nhất (hiển thị ở menu).</summary>
    [Serializable]
    public class BestRecord
    {
        public int bestScore;
        public float bestTime;
        public string bestRank = "-";
        public int wins;
    }

    public static class SaveSystem
    {
        static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");
        const string k_RecordKey = "best_record";

        public static bool HasSave => File.Exists(SavePath);

        public static void Save(SaveData data)
        {
            try
            {
                data.savedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                string json = JsonUtility.ToJson(data, true);
                string tmp = SavePath + ".tmp";
                File.WriteAllText(tmp, json);           // ghi file tạm rồi thay thế – tránh hỏng save khi tắt đột ngột
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(tmp, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError("[Save] Không lưu được: " + e.Message);
            }
        }

        public static SaveData Load()
        {
            try
            {
                if (!HasSave) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                return data != null && data.version == 1 ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] File lưu bị lỗi, bỏ qua: " + e.Message);
                return null;
            }
        }

        public static void Delete()
        {
            try { if (HasSave) File.Delete(SavePath); }
            catch (Exception e) { Debug.LogWarning("[Save] " + e.Message); }
        }

        public static BestRecord LoadRecord()
        {
            string json = PlayerPrefs.GetString(k_RecordKey, "");
            return string.IsNullOrEmpty(json) ? new BestRecord() : JsonUtility.FromJson<BestRecord>(json) ?? new BestRecord();
        }

        public static void SaveRecord(BestRecord record)
        {
            PlayerPrefs.SetString(k_RecordKey, JsonUtility.ToJson(record));
            PlayerPrefs.Save();
        }
    }
}
