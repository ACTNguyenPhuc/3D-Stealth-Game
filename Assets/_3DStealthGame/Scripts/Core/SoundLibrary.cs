using System;
using System.Collections.Generic;
using UnityEngine;

namespace StealthGame
{
    [CreateAssetMenu(menuName = "Stealth Game/Sound Library")]
    public class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SoundId id;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
            [Range(0f, 0.5f)] public float pitchVariance = 0.05f;
        }

        public List<Entry> entries = new();

        Dictionary<SoundId, Entry> m_Map;

        public Entry Get(SoundId id)
        {
            if (m_Map == null)
            {
                m_Map = new Dictionary<SoundId, Entry>();
                foreach (var e in entries) m_Map[e.id] = e;
            }
            m_Map.TryGetValue(id, out var entry);
            return entry;
        }
    }
}
