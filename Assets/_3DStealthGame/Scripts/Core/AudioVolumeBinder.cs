using UnityEngine;

namespace StealthGame
{
    /// <summary>Gắn lên AudioSource có sẵn trong scene (ambient, tiếng bước chân...) để theo âm lượng cài đặt.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioVolumeBinder : MonoBehaviour
    {
        public SoundCategory category = SoundCategory.Ambient;
        float m_Base = -1f;
        AudioSource m_Source;

        void Awake()
        {
            m_Source = GetComponent<AudioSource>();
            m_Base = m_Source.volume;
            Apply();
        }

        void OnEnable() => GameSettings.Changed += Apply;
        void OnDisable() => GameSettings.Changed -= Apply;

        void Apply()
        {
            if (m_Source == null) return;
            float mul = category == SoundCategory.Music ? GameSettings.MusicVolume : GameSettings.SfxVolume;
            m_Source.volume = m_Base * mul;
        }
    }
}
