using System;
using UnityEngine;

namespace StealthGame
{
    public enum PickupType
    {
        Shard,      // mảnh vụn linh hồn: điểm + tiền tệ
        Potion,     // bình máu
        Energy,     // dầu đèn: hồi năng lượng
        Relic,      // Mảnh hồn – vật phẩm nhiệm vụ chính
        Key,
    }

    /// <summary>
    /// Kênh sự kiện toàn cục giúp các hệ thống không phụ thuộc trực tiếp vào nhau
    /// (UI, âm thanh, nhiệm vụ... chỉ cần lắng nghe).
    /// Mọi subscriber PHẢI hủy đăng ký trong OnDisable/OnDestroy.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<string> Toast;
        public static event Action<PickupType, string, int> ItemCollected;   // loại, id/tên, số lượng
        public static event Action<float, Vector3> PlayerDamaged;             // lượng máu mất, vị trí nguồn
        public static event Action PlayerDied;
        public static event Action<EnemyAI> EnemyAlerted;
        public static event Action<EnemyAI> EnemyBanished;
        public static event Action<Vector3> AlarmRaised;
        public static event Action<string> CheckpointReached;
        public static event Action<string> ObjectiveCompleted;
        public static event Action<string> SealOpened;

        public static void RaiseToast(string message) => Toast?.Invoke(message);
        public static void RaiseItemCollected(PickupType type, string id, int amount) => ItemCollected?.Invoke(type, id, amount);
        public static void RaisePlayerDamaged(float amount, Vector3 source) => PlayerDamaged?.Invoke(amount, source);
        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaiseEnemyAlerted(EnemyAI enemy) => EnemyAlerted?.Invoke(enemy);
        public static void RaiseEnemyBanished(EnemyAI enemy) => EnemyBanished?.Invoke(enemy);
        public static void RaiseAlarm(Vector3 position) => AlarmRaised?.Invoke(position);
        public static void RaiseCheckpoint(string id) => CheckpointReached?.Invoke(id);
        public static void RaiseObjectiveCompleted(string text) => ObjectiveCompleted?.Invoke(text);
        public static void RaiseSealOpened(string id) => SealOpened?.Invoke(id);

        /// <summary>Xóa toàn bộ listener khi load scene mới – tránh rò rỉ tham chiếu.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetAll()
        {
            Toast = null; ItemCollected = null; PlayerDamaged = null; PlayerDied = null;
            EnemyAlerted = null; EnemyBanished = null; AlarmRaised = null; CheckpointReached = null;
            ObjectiveCompleted = null; SealOpened = null;
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount, Vector3 sourcePosition);
    }

    public interface IInteractable
    {
        Transform transform { get; }
        bool CanInteract(PlayerInteractor interactor);
        string GetPrompt(PlayerInteractor interactor);
        void Interact(PlayerInteractor interactor);
    }
}
