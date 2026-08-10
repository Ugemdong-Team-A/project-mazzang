using Fusion;
using UnityEngine;

public enum DeathCause : byte
{
    None = 0,
    Damage = 1,
    MapOut = 2
}

[DefaultExecutionOrder(-300)]
public sealed class PlayerHealth :
    PlayerModule,
    IPlayerHealthState,
    IPlayerDamageReceiver
{
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

    private IPlayerKnockbackReceiver
        _knockbackReceiver;

    private IPlayerCombatControl
        _combatControl;


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


    [Networked]
    public PlayerRef LastAttacker { get; private set; }


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


    bool IPlayerHealthState.IsDead =>
        IsDead;


    // =========================================================
    // Context
    // =========================================================

    protected override void RegisterContextUnits()
    {
        Context.Register<
            IPlayerHealthState>(
            this);

        Context.Register<
            IPlayerDamageReceiver>(
            this);
    }


    protected override void OnContextReady()
    {
        _knockbackReceiver =
            Context.Get<
                IPlayerKnockbackReceiver>();

        _combatControl =
            Context.Get<
                IPlayerCombatControl>();
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        _lastHealth =
            Health;

        BattleCameraController.Instance?
            .AddTarget(
                transform);

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
            .RemoveTarget(
                transform);
    }


    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!IsDead)
            return;

        if (Lives <= 0)
            return;

        if (!RespawnTimer
                .Expired(Runner))
        {
            return;
        }

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
                Health -
                info.Damage);

        // 유효한 피격이 들어오는 즉시 현재 공격을 끊는다.
        // 이후 Movement의 control lock 동안 새 공격도 차단된다.
        _combatControl?
            .CancelAttack();

        if (info.Knockback
                .sqrMagnitude > 0f)
        {
            _knockbackReceiver?
                .ApplyKnockback(
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
        if (attacker ==
            PlayerRef.None)
        {
            return;
        }

        if (attacker ==
            Object.InputAuthority)
        {
            return;
        }

        LastAttacker =
            attacker;

        if (lastAttackerCreditDuration <=
            0f)
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


    private PlayerRef
        GetValidLastAttacker()
    {
        if (LastAttacker ==
            PlayerRef.None)
        {
            return PlayerRef.None;
        }

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
        if (directAttacker !=
                PlayerRef.None &&
            directAttacker !=
                Object.InputAuthority)
        {
            return directAttacker;
        }

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

        // 사망한 틱에 이미 진행 중인 공격도 즉시 취소한다.
        _combatControl?
            .CancelAttack();

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
        if (_lastHealth >
                Health &&
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