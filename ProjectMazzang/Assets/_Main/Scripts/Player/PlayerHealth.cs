using Fusion;
using UnityEngine;

public sealed class PlayerHealth :
    NetworkBehaviour,
    IDamageable
{
    [Header("References")]
    [SerializeField]
    private PlayerMovement movement;

    [Header("Health")]
    [SerializeField]
    private int maxHealth = 100;

    [Header("Lives")]
    [SerializeField]
    private int startingLives = 3;

    [Header("Respawn")]
    [SerializeField]
    private float respawnDelay = 2f;


    [Networked]
    public int Health { get; private set; }

    [Networked]
    public int Lives { get; private set; }

    [Networked]
    public NetworkBool IsDead { get; private set; }

    [Networked]
    public byte DeathSequence { get; private set; }

    [Networked]
    private TickTimer RespawnTimer { get; set; }


    public int MaxHealth =>
        maxHealth;

    public int MaxLives =>
        startingLives;

    public bool IsAlive =>
        !IsDead &&
        Lives > 0;


    private void Awake()
    {
        if (movement == null)
        {
            movement =
                GetComponent<PlayerMovement>();
        }
    }


    public override void Spawned()
    {
        if (!HasStateAuthority)
            return;

        Health = maxHealth;
        Lives = startingLives;

        IsDead = false;
        RespawnTimer = TickTimer.None;
    }


    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!IsDead)
            return;

        if (Lives <= 0)
            return;

        if (!RespawnTimer.Expired(Runner))
            return;

        Respawn();
    }


    // =========================================================
    // Damage
    // =========================================================

    public void ApplyDamage(
        in DamageInfo info)
    {
        if (!HasStateAuthority)
            return;

        if (!IsAlive)
            return;

        Health =
            Mathf.Max(
                0,
                Health - info.Damage);

        if (info.Knockback.sqrMagnitude > 0f)
        {
            movement.ApplyKnockback(
                info.Knockback,
                info.KnockbackControlLock);
        }

        if (Health > 0)
            return;

        Die(
            info.Attacker);
    }


    // =========================================================
    // Life
    // =========================================================

    private void Die(
        PlayerRef attacker)
    {
        if (IsDead)
            return;

        IsDead = true;
        DeathSequence++;

        LoseLife();

        if (Lives <= 0)
        {
            HandleEliminated(
                attacker);

            return;
        }

        RespawnTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                respawnDelay);
    }


    private void LoseLife()
    {
        Lives =
            Mathf.Max(
                0,
                Lives - 1);
    }


    private void Respawn()
    {
        Health = maxHealth;

        IsDead = false;

        RespawnTimer =
            TickTimer.None;

        // 나중에:
        // - Spawn Point 이동
        // - Rigidbody 속도 초기화
        // - 무적 시간
        // - Combat 상태 초기화
        // - Movement 상태 초기화
    }


    private void HandleEliminated(
        PlayerRef attacker)
    {
        Health = 0;

        RespawnTimer =
            TickTimer.None;

        // 나중에:
        // NetworkGameManager 통보
        // 입력 차단
        // 캐릭터 제거
        // 승패 처리
    }
}