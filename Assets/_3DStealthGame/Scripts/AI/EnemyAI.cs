using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace StealthGame
{
    /// <summary>
    /// AI hồn ma dùng NavMesh + máy trạng thái:
    ///
    ///   Idle ⇄ Patrol ──(thấy mờ / nghe tiếng)──► Investigate ──(cảnh giác đầy)──► Chase ──(trong tầm)──► Attack
    ///      ▲                                           │                             │  ▲                    │
    ///      └───────────────(hết thời gian tìm)─────────┘◄──────(mất dấu)─────────────┘  └────(hồi chiêu)─────┘
    ///   Bất kỳ ──(trúng Linh Quang)──► Stunned ──► Chase        Máu = 0 ──► Dead (tan biến, rơi mảnh vụn)
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAI : MonoBehaviour, IDamageable, IStunnable
    {
        public enum State { Idle, Patrol, Investigate, Chase, Attack, Stunned, Dead }

        public static readonly List<EnemyAI> All = new();

        public EnemyConfig config;
        public string enemyId;
        public Transform[] waypoints;
        public float eyeHeight = 1.1f;
        public VisionCone visionCone;
        public Animator animator;
        public Transform model;

        public State CurrentState { get; private set; } = State.Patrol;
        public float Awareness { get; private set; }
        public float Health { get; private set; }
        public float HealthRatio => config != null ? Health / config.maxHealth : 0f;
        public bool IsAlive => CurrentState != State.Dead;
        public bool IsAwareOfPlayer => CurrentState == State.Chase || CurrentState == State.Attack || Awareness >= 0.99f;
        public bool IsChasing => CurrentState == State.Chase || CurrentState == State.Attack;
        public bool RecentlyDamaged => Time.time - m_LastDamaged < 4f;
        public string DisplayName => config != null ? config.displayName : name;

        NavMeshAgent m_Agent;
        DifficultyProfile m_Diff;
        Transform m_Player;
        PlayerStats m_PlayerStats;
        PlayerMovement m_PlayerMove;
        PlayerCombat m_PlayerCombat;

        int m_WaypointIndex;
        float m_StateTimer;
        float m_LastSeen = -100f;
        float m_LastDamaged = -100f;
        float m_RepathTimer;
        Vector3 m_LastKnownPos;
        Vector3 m_InvestigatePos;
        bool m_AttackHit;
        float m_LookAroundYaw;
        Vector3 m_ModelBasePos;
        Vector3 m_ModelBaseScale = Vector3.one;
        float m_DeathTimer;

        // ------------------------------------------------------------------ Vòng đời

        void Awake()
        {
            m_Agent = GetComponent<NavMeshAgent>();
            m_Diff = DifficultyProfile.Get(GameSession.CurrentDifficulty);
            Health = config != null ? config.maxHealth : 100f;
            if (model != null)
            {
                m_ModelBasePos = model.localPosition;
                m_ModelBaseScale = model.localScale;
            }
        }

        void OnEnable()
        {
            All.Add(this);
            NoiseSystem.NoiseEmitted += OnNoise;
            GameEvents.AlarmRaised += OnAlarm;
        }

        void OnDisable()
        {
            All.Remove(this);
            NoiseSystem.NoiseEmitted -= OnNoise;
            GameEvents.AlarmRaised -= OnAlarm;
        }

        void Start()
        {
            ResolvePlayer();
            if (visionCone != null) visionCone.Configure(VisionRange, config.visionAngle);
            m_Agent.enabled = true;
            m_Agent.speed = PatrolSpeed;
            if (!m_Agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
                m_Agent.Warp(hit.position);
            SetState(waypoints != null && waypoints.Length > 0 ? State.Patrol : State.Idle);
        }

        void ResolvePlayer()
        {
            m_PlayerStats = PlayerStats.Instance;
            if (m_PlayerStats == null) return;
            m_Player = m_PlayerStats.transform;
            m_PlayerMove = m_PlayerStats.GetComponent<PlayerMovement>();
            m_PlayerCombat = m_PlayerStats.GetComponent<PlayerCombat>();
        }

        float PatrolSpeed => config.patrolSpeed * m_Diff.enemySpeed;
        float ChaseSpeed => config.chaseSpeed * m_Diff.enemySpeed;
        float VisionRange => config.visionRange * m_Diff.enemyVision;

        // ------------------------------------------------------------------ Update

        void Update()
        {
            if (CurrentState == State.Dead)
            {
                UpdateDeath();
                return;
            }
            if (m_Player == null) ResolvePlayer();
            if (m_Player == null || !m_Agent.isOnNavMesh) return;

            float dt = Time.deltaTime;
            bool playerAlive = m_PlayerStats.IsAlive;
            float dist = Vector3.Distance(transform.position, m_Player.position);
            bool canSee = playerAlive && CurrentState != State.Stunned && CanSeePlayer(out dist);

            UpdateAwareness(canSee, dist, dt);

            if (!playerAlive && IsChasing)
            {
                Awareness = 0f;
                SetState(State.Patrol);
            }

            m_StateTimer += dt;
            switch (CurrentState)
            {
                case State.Idle: UpdateIdle(); break;
                case State.Patrol: UpdatePatrol(); break;
                case State.Investigate: UpdateInvestigate(); break;
                case State.Chase: UpdateChase(canSee, dist); break;
                case State.Attack: UpdateAttack(dist); break;
                case State.Stunned: UpdateStunned(); break;
            }

            UpdateVisuals();
        }

        void UpdateAwareness(bool canSee, float dist, float dt)
        {
            if (canSee)
            {
                m_LastSeen = Time.time;
                m_LastKnownPos = m_Player.position;
                float closeness = 1f + (1f - Mathf.Clamp01(dist / VisionRange)) * 1.5f;
                float stealth = m_PlayerMove != null && m_PlayerMove.IsCrouching ? 0.45f : 1f;
                float lantern = m_PlayerCombat != null && m_PlayerCombat.IsLanternOn ? 1.5f : 1f;
                float motion = m_PlayerMove != null && m_PlayerMove.IsSprinting ? 1.4f : 1f;
                Awareness += config.detectionRate * m_Diff.detectionRate * closeness * stealth * lantern * motion * dt;
            }
            else if (!IsChasing)
            {
                Awareness -= config.awarenessDecay * dt;
            }
            Awareness = Mathf.Clamp01(Awareness);

            if (canSee && !IsChasing && CurrentState != State.Stunned)
            {
                if (Awareness >= 1f) StartChase();
                else if (Awareness >= 0.3f && CurrentState != State.Investigate) Investigate(m_Player.position, false);
            }
        }

        bool CanSeePlayer(out float dist)
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            bool crouch = m_PlayerMove != null && m_PlayerMove.IsCrouching;
            Vector3 target = m_Player.position + Vector3.up * (crouch ? 0.55f : 0.9f);
            Vector3 to = target - eye;
            dist = to.magnitude;

            float range = VisionRange * (crouch ? 0.7f : 1f) * (m_PlayerCombat != null && m_PlayerCombat.IsLanternOn ? 1.4f : 1f);
            if (dist > range) return false;

            // Ở rất gần thì "cảm nhận" được dù không nhìn thẳng
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            bool inAngle = Vector3.Angle(transform.forward, flat) <= config.visionAngle * 0.5f;
            if (!inAngle && dist > 1.1f) return false;

            if (Physics.Raycast(eye, to / dist, out var hit, dist + 0.2f, ~0, QueryTriggerInteraction.Ignore))
                return hit.transform.IsChildOf(m_Player);
            return true;
        }

        // ------------------------------------------------------------------ Trạng thái

        void SetState(State next)
        {
            CurrentState = next;
            m_StateTimer = 0f;

            switch (next)
            {
                case State.Idle:
                    m_Agent.isStopped = true;
                    break;
                case State.Patrol:
                    m_Agent.isStopped = false;
                    m_Agent.speed = PatrolSpeed;
                    GoToWaypoint();
                    break;
                case State.Investigate:
                    m_Agent.isStopped = false;
                    m_Agent.SetDestination(m_InvestigatePos);
                    m_LookAroundYaw = transform.eulerAngles.y;
                    break;
                case State.Chase:
                    m_Agent.isStopped = false;
                    m_Agent.speed = ChaseSpeed;
                    break;
                case State.Attack:
                    m_Agent.isStopped = true;
                    m_AttackHit = false;
                    AudioManager.SfxAt(SoundId.GhostAttack, transform.position, 0.8f);
                    break;
                case State.Stunned:
                    m_Agent.isStopped = true;
                    break;
            }
        }

        void UpdateIdle()
        {
            if (waypoints != null && waypoints.Length > 0 && m_StateTimer >= config.waitAtWaypoint)
            {
                m_WaypointIndex = (m_WaypointIndex + 1) % waypoints.Length;
                SetState(State.Patrol);
            }
        }

        void UpdatePatrol()
        {
            if (waypoints == null || waypoints.Length == 0) { SetState(State.Idle); return; }
            if (!m_Agent.pathPending && m_Agent.remainingDistance <= m_Agent.stoppingDistance + 0.1f)
                SetState(State.Idle);
        }

        void GoToWaypoint()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            var wp = waypoints[m_WaypointIndex % waypoints.Length];
            if (wp != null) m_Agent.SetDestination(wp.position);
        }

        public void Investigate(Vector3 position, bool hurry)
        {
            if (CurrentState == State.Dead || CurrentState == State.Stunned || IsChasing) return;
            m_InvestigatePos = position;
            if (CurrentState != State.Investigate) SetState(State.Investigate);
            else m_Agent.SetDestination(position);
            m_Agent.speed = hurry ? ChaseSpeed * 0.85f : PatrolSpeed * 1.2f;
            m_StateTimer = 0f;
        }

        void UpdateInvestigate()
        {
            bool arrived = !m_Agent.pathPending && m_Agent.remainingDistance <= m_Agent.stoppingDistance + 0.2f;
            if (arrived)
            {
                // Nhìn quanh tìm kiếm
                m_LookAroundYaw += Mathf.Sin(m_StateTimer * 2f) * 120f * Time.deltaTime;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, m_LookAroundYaw, 0f), Time.deltaTime * 3f);
            }
            if (m_StateTimer > config.investigateTime + (arrived ? 0f : 6f))
            {
                Awareness = Mathf.Min(Awareness, 0.2f);
                SetState(State.Patrol);
            }
        }

        void StartChase()
        {
            if (IsChasing) return;
            bool wasCalm = CurrentState == State.Patrol || CurrentState == State.Idle || CurrentState == State.Investigate;
            Awareness = 1f;
            SetState(State.Chase);
            if (wasCalm)
            {
                AudioManager.SfxAt(SoundId.GhostAlert, transform.position);
                GameEvents.RaiseEnemyAlerted(this);
                // Báo cho đồng bọn gần đó
                foreach (var other in All)
                {
                    if (other != this && other.IsAlive && Vector3.Distance(other.transform.position, transform.position) < 7f)
                        other.Investigate(m_Player.position, true);
                }
            }
        }

        void UpdateChase(bool canSee, float dist)
        {
            m_RepathTimer -= Time.deltaTime;
            if (m_RepathTimer <= 0f)
            {
                m_RepathTimer = 0.2f;
                m_Agent.SetDestination(canSee || Time.time - m_LastSeen < 1.2f ? m_Player.position : m_LastKnownPos);
            }

            if (canSee && dist <= config.attackRange + 0.35f)
            {
                SetState(State.Attack);
                return;
            }

            if (Time.time - m_LastSeen > config.loseSightTime)
            {
                Awareness = 0.6f;
                CurrentState = State.Investigate;    // không đi qua SetState để giữ đích
                m_InvestigatePos = m_LastKnownPos;
                m_StateTimer = 0f;
                m_Agent.speed = PatrolSpeed * 1.3f;
                m_Agent.SetDestination(m_LastKnownPos);
            }
        }

        void UpdateAttack(float dist)
        {
            Vector3 to = m_Player.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 10f);

            float windup = config.attackWindup;
            if (!m_AttackHit && m_StateTimer >= windup)
            {
                m_AttackHit = true;
                if (dist <= config.attackRange * 1.35f && m_PlayerStats.IsAlive)
                    m_PlayerStats.TakeDamage(config.attackDamage * m_Diff.enemyDamage, transform.position);
            }
            if (m_StateTimer >= windup + config.attackCooldown * 0.5f)
                SetState(State.Chase);
        }

        public void Stun(float seconds)
        {
            if (CurrentState == State.Dead) return;
            SetState(State.Stunned);
            m_StateTimer = -seconds;   // đếm lên tới 0
            Awareness = 1f;
            m_LastSeen = Time.time;
            if (m_Player != null) m_LastKnownPos = m_Player.position;
        }

        void UpdateStunned()
        {
            if (m_StateTimer >= 0f) StartChaseAfterStun();
        }

        void StartChaseAfterStun()
        {
            Awareness = 1f;
            SetState(State.Chase);
        }

        // ------------------------------------------------------------------ Giác quan bên ngoài

        void OnNoise(Vector3 pos, float radius)
        {
            if (!IsAlive || IsChasing || CurrentState == State.Stunned) return;
            float d = Vector3.Distance(pos, transform.position);
            if (d > radius * config.hearingMultiplier) return;
            Awareness = Mathf.Max(Awareness, 0.35f);
            Investigate(pos, radius >= 6f);
        }

        void OnAlarm(Vector3 pos)
        {
            if (!IsAlive || IsChasing) return;
            if (Vector3.Distance(pos, transform.position) > 22f) return;
            Awareness = Mathf.Max(Awareness, 0.8f);
            Investigate(pos, true);
        }

        // ------------------------------------------------------------------ Máu

        public void TakeDamage(float amount, Vector3 sourcePosition)
        {
            if (!IsAlive) return;
            Health -= amount;
            m_LastDamaged = Time.time;
            VFXManager.Play(VFXId.EnemyHit, transform.position + Vector3.up * 0.9f);
            AudioManager.SfxAt(SoundId.GhostHit, transform.position);
            if (Health <= 0f) Die();
        }

        void Die()
        {
            CurrentState = State.Dead;
            Awareness = 0f;
            m_DeathTimer = 0f;
            if (m_Agent.isOnNavMesh) m_Agent.isStopped = true;
            m_Agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (visionCone != null) visionCone.gameObject.SetActive(false);

            VFXManager.Play(VFXId.EnemyBanish, transform.position + Vector3.up * 0.8f);
            AudioManager.SfxAt(SoundId.GhostBanish, transform.position);
            CameraShake.Shake(0.3f);

            if (config.dropPrefab != null)
            {
                for (int i = 0; i < config.shardDrop; i++)
                {
                    Vector3 offset = Quaternion.Euler(0f, i * 360f / Mathf.Max(1, config.shardDrop), 0f) * Vector3.forward * 0.6f;
                    var drop = Instantiate(config.dropPrefab, transform.position + offset + Vector3.up * 0.4f, Quaternion.identity);
                    var pickup = drop.GetComponent<Pickup>();
                    if (pickup != null) pickup.pickupId = "";   // vật rơi không lưu id
                }
            }
            GameEvents.RaiseEnemyBanished(this);
        }

        void UpdateDeath()
        {
            m_DeathTimer += Time.deltaTime;
            float k = Mathf.Clamp01(m_DeathTimer / 1.2f);
            if (model != null)
            {
                model.localScale = m_ModelBaseScale * (1f - k);
                model.localPosition = m_ModelBasePos + Vector3.up * k * 0.8f;
                model.Rotate(Vector3.up, 540f * Time.deltaTime, Space.Self);
            }
            if (k >= 1f) gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Hiển thị

        void UpdateVisuals()
        {
            if (animator != null)
                animator.speed = CurrentState == State.Stunned ? 0f : Mathf.Clamp(m_Agent.velocity.magnitude / 1.2f, 0.4f, 2f);

            if (model != null)
            {
                Vector3 p = m_ModelBasePos;
                Vector3 s = m_ModelBaseScale;
                if (CurrentState == State.Stunned)
                    p += new Vector3(Mathf.Sin(Time.time * 40f) * 0.03f, 0f, 0f);
                else if (CurrentState == State.Attack)
                {
                    float w = Mathf.Clamp01(m_StateTimer / config.attackWindup);
                    s *= 1f + w * 0.15f;
                    p += Vector3.forward * (m_AttackHit ? 0.25f : -0.1f * w);
                }
                if (RecentlyDamaged && Time.time - m_LastDamaged < 0.15f) s *= 0.85f;
                model.localPosition = Vector3.Lerp(model.localPosition, p, 0.5f);
                model.localScale = Vector3.Lerp(model.localScale, s, 0.4f);
            }

            if (visionCone != null)
            {
                Color c = CurrentState switch
                {
                    State.Chase or State.Attack => new Color(1f, 0.15f, 0.1f, 0.32f),
                    State.Stunned => new Color(0.4f, 0.6f, 1f, 0.12f),
                    _ => Awareness > 0.05f
                        ? Color.Lerp(new Color(1f, 1f, 1f, 0.14f), new Color(1f, 0.8f, 0.1f, 0.32f), Awareness)
                        : new Color(1f, 1f, 1f, 0.14f),
                };
                visionCone.SetColor(c);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (config == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, config.visionRange);
            if (waypoints == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
                if (waypoints[i] != null && waypoints[(i + 1) % waypoints.Length] != null)
                    Gizmos.DrawLine(waypoints[i].position, waypoints[(i + 1) % waypoints.Length].position);
        }
    }
}
