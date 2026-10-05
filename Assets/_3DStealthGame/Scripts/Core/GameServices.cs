using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace StealthGame
{
    /// <summary>
    /// Đối tượng tồn tại suốt game (DontDestroyOnLoad): âm thanh, hiệu ứng, chuyển scene + màn hình loading.
    /// Tự khởi tạo trước scene đầu tiên – không cần đặt sẵn trong scene nào.
    /// </summary>
    public class GameServices : MonoBehaviour
    {
        public const string MenuScene = "MainMenu";
        public const string GameScene = "DemoScene";

        public static GameServices Instance { get; private set; }

        /// <summary>Yêu cầu khởi động màn chơi: chơi mới hay tiếp tục từ save.</summary>
        public static bool LaunchContinue { get; private set; }
        public static Difficulty LaunchDifficulty { get; private set; } = Difficulty.Normal;

        static readonly string[] k_Tips =
        {
            "Ngồi xuống (C) để di chuyển êm và khó bị phát hiện hơn.",
            "Chạy (Shift) gây tiếng động – lũ ma sẽ tới kiểm tra.",
            "Bật đèn (F) giúp nhìn rõ nhưng khiến ma thấy bạn từ xa hơn.",
            "Đòn Linh Quang (chuột trái) đánh từ phía sau gây sát thương gấp đôi.",
            "Tượng Gargoyle không thể bị tiêu diệt – hãy lẻn qua khi nó quay đi.",
            "Đốt ngọn nến checkpoint để lưu tiến trình.",
            "Đổi mảnh vụn linh hồn lấy bình máu ở chỗ Ivan.",
            "Nhấn Tab để xem hành trang và nhiệm vụ.",
        };

        VisualElement m_Overlay;
        VisualElement m_Bar;
        Label m_Tip;
        bool m_Loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[GameServices]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameServices>();
            Instance.Init();
        }

        void Init()
        {
            Application.targetFrameRate = 60;
            GameInput.EnsureCreated();
            GameSettings.Apply();

            gameObject.AddComponent<AudioManager>().Init(Resources.Load<SoundLibrary>("SoundLibrary"));
            gameObject.AddComponent<VFXManager>().Init(Resources.Load<VFXLibrary>("VFXLibrary"));

            var theme = UITheme.Current;
            if (theme.panelSettings != null)
            {
                var doc = gameObject.AddComponent<UIDocument>();
                doc.panelSettings = theme.panelSettings;
                doc.sortingOrder = 100;
            }
        }

        void Start() => BuildOverlay();

        void BuildOverlay()
        {
            var doc = GetComponent<UIDocument>();
            if (doc == null || doc.rootVisualElement == null) return;

            var theme = UITheme.Current;
            m_Overlay = UIBuild.Box("LoadingOverlay", doc.rootVisualElement);
            UIBuild.Fill(m_Overlay);
            UIBuild.ApplyDefaultFont(m_Overlay);
            m_Overlay.style.backgroundColor = new Color(0.02f, 0.02f, 0.04f, 1f);
            m_Overlay.style.justifyContent = Justify.Center;
            m_Overlay.style.alignItems = Align.Center;
            m_Overlay.pickingMode = PickingMode.Position;   // chặn click khi đang load

            UIBuild.Text("ĐANG TẢI...", 34, theme.gold, m_Overlay, bold: true);
            var track = UIBuild.Box("Track", m_Overlay);
            track.style.width = 420; track.style.height = 10; track.style.marginTop = 18;
            track.style.backgroundColor = new Color(1, 1, 1, 0.1f);
            UIBuild.Border(track, 0, Color.clear, 5);
            m_Bar = UIBuild.Box("Bar", track);
            m_Bar.style.height = Length.Percent(100);
            m_Bar.style.width = Length.Percent(0);
            m_Bar.style.backgroundColor = theme.gold;
            UIBuild.Border(m_Bar, 0, Color.clear, 5);
            m_Tip = UIBuild.Text("", 18, theme.muted, m_Overlay);
            m_Tip.style.marginTop = 22;
            m_Tip.style.maxWidth = 640;
            m_Tip.style.unityTextAlign = TextAnchor.MiddleCenter;
            m_Overlay.style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------------ Điều hướng

        public static void StartNewGame(Difficulty difficulty)
        {
            SaveSystem.Delete();
            LaunchContinue = false;
            LaunchDifficulty = difficulty;
            Load(GameScene);
        }

        public static void ContinueGame()
        {
            LaunchContinue = SaveSystem.HasSave;
            Load(GameScene);
        }

        public static void GoToMenu() => Load(MenuScene);

        public static void Quit()
        {
            Debug.Log("[GameServices] Quit được gọi từ UI");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public static void Load(string scene)
        {
            if (Instance == null) { SceneManager.LoadScene(scene); return; }
            if (Instance.m_Loading) return;
            Instance.StartCoroutine(Instance.LoadRoutine(scene));
        }

        IEnumerator LoadRoutine(string scene)
        {
            m_Loading = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            if (m_Overlay != null)
            {
                m_Tip.text = "Mẹo: " + k_Tips[Random.Range(0, k_Tips.Length)];
                m_Bar.style.width = Length.Percent(0);
                m_Overlay.style.display = DisplayStyle.Flex;
                for (float a = 0; a < 1f; a += Time.unscaledDeltaTime * 4f)
                {
                    m_Overlay.style.opacity = a;
                    yield return null;
                }
                m_Overlay.style.opacity = 1f;
            }

            VFXManager.Instance?.RecallAll();
            var op = SceneManager.LoadSceneAsync(scene);
            float shown = 0f;
            while (!op.isDone)
            {
                shown = Mathf.MoveTowards(shown, op.progress / 0.9f, Time.unscaledDeltaTime * 2f);
                if (m_Bar != null) m_Bar.style.width = Length.Percent(shown * 100f);
                yield return null;
            }
            if (m_Bar != null) m_Bar.style.width = Length.Percent(100);
            yield return new WaitForSecondsRealtime(0.35f);

            if (m_Overlay != null)
            {
                for (float a = 1f; a > 0f; a -= Time.unscaledDeltaTime * 2.5f)
                {
                    m_Overlay.style.opacity = a;
                    yield return null;
                }
                m_Overlay.style.display = DisplayStyle.None;
            }
            m_Loading = false;
        }

        public static bool IsLoading => Instance != null && Instance.m_Loading;
    }
}
