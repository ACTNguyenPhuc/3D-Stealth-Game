using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StealthGame
{
    /// <summary>Trainee (Tập sự) dùng giá trị 3 để không làm hỏng file lưu cũ.</summary>
    public enum Difficulty { Easy = 0, Normal = 1, Hard = 2, Trainee = 3 }

    /// <summary>Bảng cân bằng theo độ khó – mọi con số chỉnh độ khó tập trung tại đây.</summary>
    public readonly struct DifficultyProfile
    {
        public readonly string displayName;
        public readonly string description;
        public readonly float enemyDamage;
        public readonly float enemySpeed;
        public readonly float enemyVision;
        public readonly float detectionRate;
        public readonly float dawnMinutes;
        public readonly int startPotions;
        public readonly float scoreMultiplier;
        public readonly bool infiniteEnergy;    // Linh Quang / đèn soi không tốn năng lượng
        public readonly bool infinitePotions;   // bình máu dùng không giới hạn

        public DifficultyProfile(string name, string desc, float dmg, float speed, float vision, float detect, float minutes, int potions, float score,
            bool infiniteEnergy = false, bool infinitePotions = false)
        {
            displayName = name; description = desc; enemyDamage = dmg; enemySpeed = speed; enemyVision = vision;
            detectionRate = detect; dawnMinutes = minutes; startPotions = potions; scoreMultiplier = score;
            this.infiniteEnergy = infiniteEnergy; this.infinitePotions = infinitePotions;
        }

        /// <summary>Thứ tự hiển thị trong menu chọn độ khó.</summary>
        public static readonly Difficulty[] MenuOrder = { Difficulty.Trainee, Difficulty.Easy, Difficulty.Normal, Difficulty.Hard };

        public static DifficultyProfile Current => Get(GameSession.CurrentDifficulty);

        public static DifficultyProfile Get(Difficulty d) => d switch
        {
            Difficulty.Trainee => new DifficultyProfile("Tập sự", "Chế độ trình diễn: vô hạn Linh Quang, vô hạn bình máu. 30 phút.", 0.5f, 0.85f, 0.8f, 0.6f, 30f, 0, 0.5f, true, true),
            Difficulty.Easy => new DifficultyProfile("Dễ", "Ma chậm, gây ít sát thương. 20 phút trước bình minh.", 0.6f, 0.85f, 0.8f, 0.7f, 20f, 3, 0.75f),
            Difficulty.Hard => new DifficultyProfile("Khó", "Ma nhanh, nhìn xa, đánh đau. Chỉ 10 phút.", 1.5f, 1.15f, 1.2f, 1.4f, 10f, 0, 1.5f),
            _ => new DifficultyProfile("Thường", "Trải nghiệm cân bằng. 15 phút trước bình minh.", 1f, 1f, 1f, 1f, 15f, 1, 1f),
        };
    }

    /// <summary>Cài đặt người chơi, lưu bằng PlayerPrefs.</summary>
    public static class GameSettings
    {
        public static event Action Changed;

        public static float MasterVolume { get => Get("vol_master", 0.9f); set => Set("vol_master", value); }
        public static float MusicVolume { get => Get("vol_music", 0.6f); set => Set("vol_music", value); }
        public static float SfxVolume { get => Get("vol_sfx", 0.9f); set => Set("vol_sfx", value); }
        public static float MouseSensitivity { get => Get("mouse_sens", 1f); set => Set("mouse_sens", value); }
        public static bool InvertY { get => GetBool("invert_y", false); set => SetBool("invert_y", value); }
        public static bool CameraShake { get => GetBool("cam_shake", true); set => SetBool("cam_shake", value); }
        public static bool ShowFps { get => GetBool("show_fps", false); set => SetBool("show_fps", value); }
        public static bool Fullscreen { get => GetBool("fullscreen", Screen.fullScreen); set { SetBool("fullscreen", value); Apply(); } }
        public static readonly string[] GraphicsPresetNames = { "Thấp (tiết kiệm pin)", "Trung bình", "Cao" };

        /// <summary>0 = Thấp, 1 = Trung bình, 2 = Cao. Mặc định: điện thoại = Trung bình, PC = Cao.</summary>
        public static int GraphicsPreset
        {
            get => PlayerPrefs.GetInt("gfx_preset", Application.isMobilePlatform ? 1 : 2);
            set { PlayerPrefs.SetInt("gfx_preset", Mathf.Clamp(value, 0, 2)); Apply(); Changed?.Invoke(); }
        }

        /// <summary>Điều khiển cảm ứng: 0 = tự động (chỉ trên điện thoại), 1 = luôn bật, 2 = tắt.</summary>
        public static int TouchControlsMode
        {
            get => PlayerPrefs.GetInt("touch_mode", 0);
            set { PlayerPrefs.SetInt("touch_mode", value); Changed?.Invoke(); }
        }

        public static void Apply()
        {
            if (!Application.isEditor && !Application.isMobilePlatform && Screen.fullScreen != GetBool("fullscreen", Screen.fullScreen))
                Screen.fullScreen = GetBool("fullscreen", Screen.fullScreen);
            AudioListener.volume = Muted ? 0f : MasterVolume;
            ApplyGraphics();
        }

        static UniversalRenderPipelineAsset s_RuntimeAsset;

        /// <summary>
        /// Chỉnh URP theo preset lúc chạy: tỉ lệ render, khử răng cưa, bóng đổ, FPS.
        /// Dùng bản sao của asset để không ghi đè asset gốc khi chạy trong Editor.
        /// </summary>
        static void ApplyGraphics()
        {
            int p = GraphicsPreset;
            bool mobile = Application.isMobilePlatform;
            Application.targetFrameRate = mobile && p == 0 ? 30 : 60;
            QualitySettings.vSyncCount = 0;
            if (Application.isEditor) return;   // không đổi pipeline của project khi chạy trong Editor

            if (s_RuntimeAsset == null)
            {
                var src = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (src == null) return;
                s_RuntimeAsset = UnityEngine.Object.Instantiate(src);
                s_RuntimeAsset.name = src.name + " (Runtime)";
                QualitySettings.renderPipeline = s_RuntimeAsset;
            }

            var a = s_RuntimeAsset;
            a.renderScale = p switch { 0 => mobile ? 0.6f : 0.75f, 1 => mobile ? 0.8f : 0.9f, _ => 1f };
            a.msaaSampleCount = p == 2 && !mobile ? 4 : p == 2 ? 2 : 1;
            a.shadowDistance = p switch { 0 => 10f, 1 => 20f, _ => 35f };
            a.mainLightShadowmapResolution = p switch { 0 => 512, 1 => 1024, _ => 2048 };
            a.supportsHDR = p > 0;
        }

        public static void ResetDefaults()
        {
            foreach (var k in new[] { "vol_master", "vol_music", "vol_sfx", "mouse_sens", "invert_y", "cam_shake", "show_fps", "gfx_preset", "touch_mode" })
                PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        static float Get(string key, float def) => PlayerPrefs.GetFloat(key, def);
        static void Set(string key, float v) { PlayerPrefs.SetFloat(key, v); AudioListener.volume = Muted ? 0f : MasterVolume; Changed?.Invoke(); }

        /// <summary>Tham số dòng lệnh -mute: tắt toàn bộ âm thanh (dùng khi chạy kiểm thử tự động).</summary>
        static readonly bool Muted = Array.IndexOf(Environment.GetCommandLineArgs(), "-mute") >= 0;
        static bool GetBool(string key, bool def) => PlayerPrefs.GetInt(key, def ? 1 : 0) == 1;
        static void SetBool(string key, bool v) { PlayerPrefs.SetInt(key, v ? 1 : 0); Changed?.Invoke(); }
    }
}
