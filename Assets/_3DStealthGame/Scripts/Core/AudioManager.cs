using System;
using System.Collections.Generic;
using UnityEngine;

namespace StealthGame
{
    public enum SoundId
    {
        None,
        // Nhạc
        MusicMenu, MusicExplore, MusicChase,
        // Người chơi
        Jump, Land, Hurt, PlayerDeath, Footstep, Heartbeat,
        LanternBurst, LanternToggle, EnergyEmpty,
        // Kẻ địch
        GhostHit, GhostBanish, GhostAttack, GhostAlert, GhostMove, Alarm,
        // Vật phẩm & thế giới
        Shard, Potion, EnergyPickup, Key, Relic, Checkpoint, DoorOpen, Locked,
        // UI & sự kiện
        UIClick, UIHover, UIOpen, Victory, GameOver,
    }

    public enum SoundCategory { Sfx, Music, Ambient }

    /// <summary>
    /// Quản lý âm thanh tập trung: nhạc nền (crossfade 2 kênh), SFX 2D/3D dùng pool AudioSource.
    /// Âm lượng theo nhóm (Master / Music / SFX) lấy từ GameSettings.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        SoundLibrary m_Library;
        AudioSource[] m_Music = new AudioSource[2];
        int m_ActiveMusic;
        SoundId m_CurrentMusic = SoundId.None;
        float m_MusicFade = 1f, m_FadeSpeed = 1f;
        readonly List<AudioSource> m_Pool2D = new();
        readonly List<AudioSource> m_Pool3D = new();
        int m_Next2D, m_Next3D;

        public void Init(SoundLibrary library)
        {
            Instance = this;
            m_Library = library;

            for (int i = 0; i < 2; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.loop = true;
                src.playOnAwake = false;
                src.ignoreListenerPause = true;
                src.volume = 0f;
                m_Music[i] = src;
            }

            for (int i = 0; i < 10; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.ignoreListenerPause = true;   // UI vẫn kêu khi pause
                m_Pool2D.Add(s);
            }

            for (int i = 0; i < 16; i++)
            {
                var go = new GameObject("Sfx3D_" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 1.5f;
                s.maxDistance = 22f;
                m_Pool3D.Add(s);
            }
        }

        // ------------------------------------------------------------------ Music

        public void PlayMusic(SoundId id, float fadeTime = 1.5f)
        {
            if (id == m_CurrentMusic) return;
            m_CurrentMusic = id;
            var entry = m_Library != null ? m_Library.Get(id) : null;

            m_ActiveMusic = 1 - m_ActiveMusic;
            var src = m_Music[m_ActiveMusic];
            src.clip = entry != null && entry.clips.Length > 0 ? entry.clips[0] : null;
            src.volume = 0f;
            if (src.clip != null) src.Play();
            m_MusicFade = 0f;
            m_FadeSpeed = 1f / Mathf.Max(0.05f, fadeTime);
        }

        public void StopMusic(float fadeTime = 1f) => PlayMusic(SoundId.None, fadeTime);

        void Update()
        {
            m_MusicFade = Mathf.MoveTowards(m_MusicFade, 1f, m_FadeSpeed * Time.unscaledDeltaTime);
            float target = GameSettings.MusicVolume * MusicEntryVolume();
            var active = m_Music[m_ActiveMusic];
            var other = m_Music[1 - m_ActiveMusic];
            active.volume = target * m_MusicFade;
            other.volume = Mathf.MoveTowards(other.volume, 0f, m_FadeSpeed * Time.unscaledDeltaTime);
            if (other.volume <= 0f && other.isPlaying) other.Stop();
        }

        float MusicEntryVolume()
        {
            var e = m_Library != null ? m_Library.Get(m_CurrentMusic) : null;
            return e != null ? e.volume : 1f;
        }

        // ------------------------------------------------------------------ SFX

        public void Play(SoundId id, float volume = 1f)
        {
            var entry = Resolve(id, out var clip);
            if (clip == null) return;
            var src = m_Pool2D[m_Next2D];
            m_Next2D = (m_Next2D + 1) % m_Pool2D.Count;
            src.pitch = 1f + UnityEngine.Random.Range(-entry.pitchVariance, entry.pitchVariance);
            src.PlayOneShot(clip, entry.volume * volume * GameSettings.SfxVolume);
        }

        public void PlayAt(SoundId id, Vector3 position, float volume = 1f)
        {
            var entry = Resolve(id, out var clip);
            if (clip == null) return;
            var src = m_Pool3D[m_Next3D];
            m_Next3D = (m_Next3D + 1) % m_Pool3D.Count;
            src.transform.position = position;
            src.pitch = 1f + UnityEngine.Random.Range(-entry.pitchVariance, entry.pitchVariance);
            src.PlayOneShot(clip, entry.volume * volume * GameSettings.SfxVolume);
        }

        SoundLibrary.Entry Resolve(SoundId id, out AudioClip clip)
        {
            clip = null;
            if (m_Library == null || id == SoundId.None) return null;
            var entry = m_Library.Get(id);
            if (entry == null || entry.clips == null || entry.clips.Length == 0) return null;
            clip = entry.clips[UnityEngine.Random.Range(0, entry.clips.Length)];
            return entry;
        }

        public AudioClip GetClip(SoundId id)
        {
            Resolve(id, out var clip);
            return clip;
        }

        // Tiện gọi tĩnh – an toàn khi chưa có AudioManager (ví dụ test trong Editor)
        public static void Sfx(SoundId id, float volume = 1f) { if (Instance != null) Instance.Play(id, volume); }
        public static void SfxAt(SoundId id, Vector3 pos, float volume = 1f) { if (Instance != null) Instance.PlayAt(id, pos, volume); }
        public static void Music(SoundId id, float fade = 1.5f) { if (Instance != null) Instance.PlayMusic(id, fade); }
    }
}
