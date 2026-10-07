using System;
using UnityEngine;

namespace StealthGame
{
    /// <summary>Máu, thể lực (chạy), năng lượng đèn; nhận sát thương và chết.</summary>
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        public static PlayerStats Instance { get; private set; }

        [Header("Máu")]
        public float maxHealth = 100f;
        public float invulnerableTime = 0.9f;
        public float potionHeal = 45f;

        [Header("Thể lực")]
        public float maxStamina = 100f;
        public float sprintCost = 22f;          // mỗi giây
        public float staminaRegen = 18f;
        public float staminaRegenDelay = 0.8f;

        [Header("Năng lượng đèn")]
        public float maxEnergy = 100f;
        public float passiveEnergyRegen = 1.2f;  // hồi chậm tới passiveRegenCap
        public float passiveRegenCap = 35f;

        public float Health { get; private set; }
        public float Stamina { get; private set; }
        public float Energy { get; private set; }
        public bool IsAlive => Health > 0f;
        public bool IsExhausted { get; private set; }
        public bool IsLowHealth => Health > 0f && Health <= maxHealth * 0.3f;

        public event Action<float> Damaged;
        public event Action Died;
        public event Action Healed;

        float m_InvulnerableUntil;
        float m_StaminaUsedAt;
        PlayerMovement m_Movement;
        PlayerInventory m_Inventory;

        void Awake()
        {
            Instance = this;
            Health = maxHealth;
            Stamina = maxStamina;
            Energy = maxEnergy;
            m_Movement = GetComponent<PlayerMovement>();
            m_Inventory = GetComponent<PlayerInventory>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetValues(float health, float energy)
        {
            Health = Mathf.Clamp(health, 1f, maxHealth);
            Energy = Mathf.Clamp(energy, 0f, maxEnergy);
            Stamina = maxStamina;
        }

        void Update()
        {
            if (!IsAlive) return;

            if (GameInput.Pressed(GameInput.UsePotion))
                UsePotion();

            if (Time.time - m_StaminaUsedAt > staminaRegenDelay)
                Stamina = Mathf.Min(maxStamina, Stamina + staminaRegen * Time.deltaTime);
            if (IsExhausted && Stamina >= maxStamina * 0.3f)
                IsExhausted = false;

            if (DifficultyProfile.Current.infiniteEnergy)
                Energy = maxEnergy;
            else if (Energy < passiveRegenCap)
                Energy = Mathf.Min(passiveRegenCap, Energy + passiveEnergyRegen * Time.deltaTime);
        }

        // ------------------------------------------------------------------ Thể lực / năng lượng

        /// <summary>Tiêu thể lực cho 1 bước FixedUpdate. Trả về false khi kiệt sức.</summary>
        public bool TryUseStamina(float dt)
        {
            if (IsExhausted) return false;
            Stamina -= sprintCost * dt;
            m_StaminaUsedAt = Time.time;
            if (Stamina <= 0f)
            {
                Stamina = 0f;
                IsExhausted = true;
                GameEvents.RaiseToast("Kiệt sức! Hãy dừng lại thở.");
                return false;
            }
            return true;
        }

        public bool TryUseEnergy(float amount)
        {
            if (DifficultyProfile.Current.infiniteEnergy) return true;
            if (Energy < amount) return false;
            Energy -= amount;
            return true;
        }

        public void DrainEnergy(float amount)
        {
            if (!DifficultyProfile.Current.infiniteEnergy) Energy = Mathf.Max(0f, Energy - amount);
        }
        public void AddEnergy(float amount) => Energy = Mathf.Min(maxEnergy, Energy + amount);

        // ------------------------------------------------------------------ Máu

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            Health = Mathf.Min(maxHealth, Health + amount);
            Healed?.Invoke();
            VFXManager.Play(VFXId.Heal, transform.position + Vector3.up * 0.6f);
        }

        public bool UsePotion()
        {
            bool infinite = DifficultyProfile.Current.infinitePotions;
            if (!infinite && (m_Inventory == null || m_Inventory.Potions <= 0))
            {
                GameEvents.RaiseToast("Không còn bình máu.");
                AudioManager.Sfx(SoundId.EnergyEmpty);
                return false;
            }
            if (Health >= maxHealth)
            {
                GameEvents.RaiseToast("Máu đã đầy.");
                return false;
            }
            if (!infinite) m_Inventory.UsePotion();
            Heal(potionHeal);
            AudioManager.Sfx(SoundId.Potion);
            GameEvents.RaiseToast($"+{potionHeal:0} máu");
            return true;
        }

        public void TakeDamage(float amount, Vector3 sourcePosition)
        {
            if (!IsAlive || Time.time < m_InvulnerableUntil || amount <= 0f) return;
            m_InvulnerableUntil = Time.time + invulnerableTime;

            Health = Mathf.Max(0f, Health - amount);
            Damaged?.Invoke(amount);
            GameEvents.RaisePlayerDamaged(amount, sourcePosition);
            AudioManager.SfxAt(SoundId.Hurt, transform.position);
            VFXManager.Play(VFXId.PlayerHit, transform.position + Vector3.up * 0.8f);
            CameraShake.Shake(0.6f);

            if (m_Movement != null)
                m_Movement.Knockback(transform.position - sourcePosition, 3.5f);

            if (Health <= 0f)
                Die();
        }

        public void Kill()
        {
            m_InvulnerableUntil = 0f;
            TakeDamage(Health + 1f, transform.position + transform.forward);
        }

        void Die()
        {
            AudioManager.SfxAt(SoundId.PlayerDeath, transform.position);
            CameraShake.Shake(1.2f);
            Died?.Invoke();
            GameEvents.RaisePlayerDied();
        }
    }
}
