using Fusion;
using UnityEngine;

public enum DeathCause : byte
{
    None = 0,
    Damage = 1,
    MapOut = 2
}

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
    [SerializeField]
    private float respawnInvulnerabilityDuration = 2f;

    [Header("Kill Credit")]
    [Tooltip("마지막으로 공격한 플레이어에게 MapOut KO를 인정하는 시간입니다.")]
    [SerializeField]
    private float lastAttackerCreditDuration = 5f;


    private float _lastHealth;


    // =========================================================
    // Network State
    // =========================================================

    [Networked,
     OnChangedRender(nameof(OnHealthChanged))]
    public int Health { get; private set; }


    [Networked]
    public int Lives { get; private set; }


    [Networked,
     OnChangedRender(nameof(OnDeadChanged))]
    public NetworkBool IsDead { get; private set; }


    [Networked]
    public byte DeathSequence { get; private set; }


    // 가장 최근에 이 플레이어에게 유효한 공격을 가한 플레이어.
    [Networked]
    public PlayerRef LastAttacker { get; private set; }


    // 가장 최근 사망에서 실제로 Kill Credit을 받은 플레이어.
    // Death Feedback / Kill Feed에서 나중에 활용 가능.
    [Networked]
    public PlayerRef LastDeathAttacker { get; private set; }


    [Networked]
    public DeathCause LastDeathCause { get; private set; }


    [Networked]
    private TickTimer LastAttackerTimer { get; set; }


    [Networked]
    private TickTimer RespawnTimer { get; set; }

    [Networked]
    private TickTimer InvulnerabilityTimer { get; set; }

    // =========================================================
    // Public State
    // =========================================================

    public int MaxHealth =>
        maxHealth;


    public int MaxLives =>
        startingLives;


    public bool IsAlive =>
        !IsDead &&
        Lives > 0;

    public bool IsInvulnerable =>
    !InvulnerabilityTimer
        .ExpiredOrNotRunning(Runner);

    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (movement == null)
        {
            movement =
                GetComponent<PlayerMovement>();
        }
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        _lastHealth =
            Health;

        // Camera는 각 Peer의 로컬 Presentation이므로
        // StateAuthority 여부와 관계없이 등록한다.
        BattleCameraController.Instance?
            .AddTarget(transform);

        if (!HasStateAuthority)
            return;

        Health =
            maxHealth;

        Lives =
            startingLives;

        IsDead =
            false;

        DeathSequence =
            0;

        LastAttacker =
            PlayerRef.None;

        LastDeathAttacker =
            PlayerRef.None;

        LastDeathCause =
            DeathCause.None;

        LastAttackerTimer =
            TickTimer.None;

        RespawnTimer =
            TickTimer.None;

        InvulnerabilityTimer =
            TickTimer.None;
    }


    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        BattleCameraController.Instance?
            .RemoveTarget(transform);
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

        TryRespawn();
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

        if (IsInvulnerable)
            return;

        RegisterLastAttacker(
            info.Attacker);

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

        PlayerRef deathAttacker =
            ResolveDeathAttacker(
                info.Attacker);

        Die(
            deathAttacker,
            DeathCause.Damage);
    }


    // =========================================================
    // Map Out
    // =========================================================

    public void ApplyMapOut()
    {
        if (!HasStateAuthority)
            return;

        if (!IsAlive)
            return;

        PlayerRef attacker =
            GetValidLastAttacker();

        Die(
            attacker,
            DeathCause.MapOut);
    }


    // =========================================================
    // Last Attacker
    // =========================================================

    private void RegisterLastAttacker(
        PlayerRef attacker)
    {
        // 공격자가 없는 환경 피해 등.
        if (attacker == PlayerRef.None)
            return;

        // 자기 자신에게 발생한 피해는
        // 외부 Kill Credit 대상으로 기록하지 않는다.
        if (attacker == Object.InputAuthority)
            return;

        LastAttacker =
            attacker;

        if (lastAttackerCreditDuration <= 0f)
        {
            LastAttackerTimer =
                TickTimer.None;

            return;
        }

        LastAttackerTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                lastAttackerCreditDuration);
    }


    private PlayerRef GetValidLastAttacker()
    {
        if (LastAttacker == PlayerRef.None)
            return PlayerRef.None;

        if (LastAttackerTimer
            .ExpiredOrNotRunning(Runner))
        {
            return PlayerRef.None;
        }

        return LastAttacker;
    }


    private PlayerRef ResolveDeathAttacker(
        PlayerRef directAttacker)
    {
        // 이번 공격에 명확한 공격자가 있으면
        // 그 공격자를 최우선으로 사용.
        if (directAttacker != PlayerRef.None &&
            directAttacker != Object.InputAuthority)
        {
            return directAttacker;
        }

        // 환경 피해 등으로 직접 공격자가 없다면
        // 최근 공격자를 확인.
        return GetValidLastAttacker();
    }


    private void ClearLastAttacker()
    {
        LastAttacker =
            PlayerRef.None;

        LastAttackerTimer =
            TickTimer.None;
    }


    // =========================================================
    // Life
    // =========================================================

    private void Die(
        PlayerRef attacker,
        DeathCause cause)
    {
        if (IsDead)
            return;

        // State를 바꾸기 전에 이번 사망 정보를 먼저 확정.
        // 로컬 Presentation에서 IsDead 변경을 감지했을 때
        // 이미 Cause / Attacker를 읽을 수 있도록 한다.
        LastDeathAttacker =
            attacker;

        LastDeathCause =
            cause;

        InvulnerabilityTimer =
            TickTimer.None;

        Health =
            0;

        IsDead =
            true;

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


    private void TryRespawn()
    {
        NetworkGameManager gameManager =
            NetworkGameManager.Instance;

        if (gameManager == null)
            return;

        bool success =
            gameManager.TryRespawnPlayer(
                Object.InputAuthority);

        if (!success)
            return;

        CompleteRespawn();
    }

    private void CompleteRespawn()
    {
        Health =
            maxHealth;

        RespawnTimer =
            TickTimer.None;

        ClearLastAttacker();

        InvulnerabilityTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                respawnInvulnerabilityDuration);

        IsDead =
            false;
    }

    private void HandleEliminated(
        PlayerRef attacker)
    {
        RespawnTimer =
            TickTimer.None;

        NetworkGameManager.Instance?
            .ReportPlayerEliminated(
                Object.InputAuthority,
                attacker);
    }


    // =========================================================
    // Presentation
    // =========================================================

    private void OnHealthChanged()
    {
        // 죽음 자체에는 Death Shake가 있으므로
        // 치명타에서 Hit + Death Shake가 동시에 겹치는 것을 방지.
        if (_lastHealth > Health &&
            !IsDead)
        {
            BattleCameraController.Instance?
                .PlayHitShake(
                    transform.position);
        }

        _lastHealth =
            Health;
    }


    private void OnDeadChanged()
    {
        BattleCameraController bcc =
            BattleCameraController.Instance;

        if (IsDead)
        {
            bcc?.RemoveTarget(
                transform);

            bcc?.PlayDeathShake(
                transform.position);
        }
        else
        {
            bcc?.AddTarget(
                transform);
        }
    }
}