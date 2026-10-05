using System;
using System.Collections.Generic;
using UnityEngine;

namespace StealthGame
{
    public enum VFXId
    {
        None,
        PickupSparkle,
        LanternBurst,
        EnemyHit,
        EnemyBanish,
        PlayerHit,
        Checkpoint,
        RelicCollect,
        SealBreak,
        LandDust,
        Heal,
    }

    [CreateAssetMenu(menuName = "Stealth Game/VFX Library")]
    public class VFXLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public VFXId id;
            public GameObject prefab;
            public float lifetime = 2f;
        }

        public List<Entry> entries = new();

        public Entry Get(VFXId id) => entries.Find(e => e.id == id);
    }
}
