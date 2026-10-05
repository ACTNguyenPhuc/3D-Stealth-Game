using System;
using System.Collections.Generic;
using UnityEngine;

namespace StealthGame
{
    /// <summary>Hành trang: chìa khóa, bình máu, mảnh vụn linh hồn (tiền), Mảnh hồn (vật phẩm nhiệm vụ).</summary>
    public class PlayerInventory : MonoBehaviour
    {
        public const int RelicsRequired = 3;

        readonly HashSet<string> m_Keys = new();
        readonly HashSet<string> m_Relics = new();

        public int Potions { get; private set; }
        public int Shards { get; private set; }
        public int RelicCount => m_Relics.Count;
        public IReadOnlyCollection<string> Keys => m_Keys;
        public IReadOnlyCollection<string> Relics => m_Relics;

        public event Action Changed;

        public void AddKey(string key)
        {
            if (string.IsNullOrEmpty(key) || !m_Keys.Add(key)) return;
            Changed?.Invoke();
        }

        public bool HasKey(string key) => m_Keys.Contains(key);

        public void AddRelic(string id)
        {
            if (!m_Relics.Add(id)) return;
            Changed?.Invoke();
        }

        public bool HasRelic(string id) => m_Relics.Contains(id);

        public void AddPotions(int amount)
        {
            Potions = Mathf.Max(0, Potions + amount);
            Changed?.Invoke();
        }

        public void UsePotion()
        {
            if (Potions <= 0) return;
            Potions--;
            Changed?.Invoke();
        }

        public void AddShards(int amount)
        {
            Shards = Mathf.Max(0, Shards + amount);
            Changed?.Invoke();
        }

        public bool SpendShards(int amount)
        {
            if (Shards < amount) return false;
            Shards -= amount;
            Changed?.Invoke();
            return true;
        }

        public void Restore(IEnumerable<string> keys, IEnumerable<string> relics, int potions, int shards)
        {
            m_Keys.Clear();
            m_Relics.Clear();
            foreach (var k in keys) m_Keys.Add(k);
            foreach (var r in relics) m_Relics.Add(r);
            Potions = potions;
            Shards = shards;
            Changed?.Invoke();
        }
    }
}
