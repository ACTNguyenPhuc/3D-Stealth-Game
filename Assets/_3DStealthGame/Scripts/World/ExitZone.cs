using UnityEngine;

namespace StealthGame
{
    /// <summary>Vùng thoát phía sau cửa chính – bước vào là chiến thắng.</summary>
    [RequireComponent(typeof(Collider))]
    public class ExitZone : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            var stats = other.GetComponentInParent<PlayerStats>();
            if (stats == null || !stats.IsAlive) return;
            if (GameSession.Instance != null) GameSession.Instance.Victory();
        }
    }
}
