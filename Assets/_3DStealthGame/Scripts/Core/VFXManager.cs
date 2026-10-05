using System.Collections.Generic;
using UnityEngine;

namespace StealthGame
{
    /// <summary>Phát hiệu ứng hạt theo id, có object pool để tránh Instantiate/Destroy liên tục.</summary>
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        VFXLibrary m_Library;
        readonly Dictionary<VFXId, Queue<GameObject>> m_Pools = new();
        readonly List<(GameObject go, VFXId id, float until)> m_Active = new();

        public void Init(VFXLibrary library)
        {
            Instance = this;
            m_Library = library;
        }

        public static void Play(VFXId id, Vector3 position, Quaternion rotation = default, Color? tint = null)
        {
            if (Instance != null) Instance.Spawn(id, position, rotation == default ? Quaternion.identity : rotation, tint);
        }

        void Spawn(VFXId id, Vector3 position, Quaternion rotation, Color? tint)
        {
            var entry = m_Library != null ? m_Library.Get(id) : null;
            if (entry == null || entry.prefab == null) return;

            if (!m_Pools.TryGetValue(id, out var pool))
                m_Pools[id] = pool = new Queue<GameObject>();

            GameObject go = null;
            while (pool.Count > 0 && go == null) go = pool.Dequeue();
            if (go == null)
            {
                go = Instantiate(entry.prefab, transform);
                go.name = entry.prefab.name;
            }

            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
            {
                if (tint.HasValue)
                {
                    var main = ps.main;
                    main.startColor = tint.Value;
                }
                ps.Clear(true);
                ps.Play(true);
            }
            m_Active.Add((go, id, Time.unscaledTime + entry.lifetime));
        }

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = m_Active.Count - 1; i >= 0; i--)
            {
                var (go, id, until) = m_Active[i];
                if (go == null) { m_Active.RemoveAt(i); continue; }
                if (now < until) continue;
                go.SetActive(false);
                m_Pools[id].Enqueue(go);
                m_Active.RemoveAt(i);
            }
        }

        /// <summary>Thu hồi mọi hiệu ứng khi đổi scene.</summary>
        public void RecallAll()
        {
            foreach (var (go, id, _) in m_Active)
            {
                if (go == null) continue;
                go.SetActive(false);
                m_Pools[id].Enqueue(go);
            }
            m_Active.Clear();
        }
    }
}
