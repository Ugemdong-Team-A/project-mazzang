using Fusion;
using System;
using UnityEngine;

public enum DeathCause : byte
{
    None = 0,
    Damage = 1,
    MapOut = 2
}

[DefaultExecutionOrder(-300)]
public sealed class PlayerHealth :
    PlayerTickModule,
    IDamageable,
    IPlayerTickStateSource,
    IStatsConsumer
{
    private const int PendingCrowdControlCapacity = 8;
    private const int DefaultMaxHealth = 100;


    [Header("Lives")]
    [SerializeField]
    private int startingLives = 3;


    [Header("Respawn")]
    [Min(0.01f)]
    [SerializeField]
    private float respawnDelay = 2f;

    [SerializeField]
    private float respawnInvulnerabilityDuration = 2f;


    [Header("Kill Credit")]
    [Tooltip("마지막으로 공격한 플레이어에게 MapOut KO를 인정하는 시간입니다.")]
    [SerializeField]
    private float lastAttackerCreditDuration = 5f;

    [Header("Presentation")]
    [SerializeField]
    private Transform cameraTarget;


    private float _lastHealth;

    private PlayerTickState _tickState;

    private PlayerStatsData _statsData;


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

    [Networked]
    private int AppliedMaxHealth { get; set; }


    [Networked, Capacity(PendingCrowdControlCapacity)]
    private NetworkArray<PendingCrowdControlState>
        PendingCrowdControls =>
            default;


    // =========================================================
    // Local Presentation Events
    // =========================================================

    public static event Action<PlayerHealth>
        LocalDeathOccurred;


    // =========================================================
    // Public State
    // =========================================================

    public int MaxHealth =>
        AppliedMaxHealth > 0
            ? AppliedMaxHealth
            : BaseMaxHealth;


    public int MaxLives =>
        startingLives;


    public bool IsAlive =>
        !IsDead &&
        Lives > 0;


    public bool IsInvulnerable =>
        !InvulnerabilityTimer
            .ExpiredOrNotRunning(Runner);

    public Transform CameraTarget =>
        cameraTarget != null
            ? cameraTarget
            : transform;


    void IStatsConsumer.InitializeStats(
        PlayerStatsData statsData)
    {
        _statsData = statsData;
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
                CameraTarget);

        if (!HasStateAuthority)
            return;

        AppliedMaxHealth =
            BaseMaxHealth;

        Health =
            MaxHealth;

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

        ClearPendingCrowdControls();
    }


    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        BattleCameraController.Instance?
            .RemoveTarget(
                CameraTarget);
    }


    public override PlayerTickStage Stage =>
        PlayerTickStage.Begin;


    public override void Simulate(
        in PlayerTick tick)
    {
        _tickState =
            tick.State;

        TickBegin(
            tick.State.ActiveStatModifiers.MaxHealth);
    }


    void IPlayerTickStateSource.CaptureTickState(
        PlayerTickState state)
    {
        state.HasHealth = true;
        state.Health = Health;
        state.MaxHealth = MaxHealth;
        state.Lives = Lives;
        state.IsInvulnerable = IsInvulnerable;
        state.IsAlive = IsAlive;
        state.DeathSequence = DeathSequence;
    }


    internal void TickBegin(
        float maxHealthMultiplier)
    {
        if (!HasStateAuthority)
            return;

        RefreshMaxHealthModifier(
            maxHealthMultiplier);

        ProcessPendingCrowdControls();

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

    public DamageResult ApplyDamage(
        in DamageInfo info)
    {
        if (!HasStateAuthority)
            return DamageResult.Rejected;

        if (!IsAlive)
            return DamageResult.Rejected;

        if (IsInvulnerable)
            return DamageResult.Rejected;

        int previousHealth =
            Health;

        RegisterLastAttacker(
            info.Source.InputAuthority);

        int effectiveDamage =
            ResolveEffectiveDamage(
                in info);

        Health =
            Mathf.Max(
                0,
                Health -
                effectiveDamage);

        int appliedDamage =
            previousHealth -
            Health;

        // 유효한 피격이 들어오는 즉시 현재 공격을 끊는다.
        // 새 공격 차단 시간은 아래의 Attack control lock이 담당한다.
        RequestCancelAttack();

        bool wasFatal =
            Health <= 0;

        if (info.CrowdControl.StopMovementOnApply)
        {
            RequestStopMovement();
        }

        if (!wasFatal)
        {
            ApplyOrQueueCrowdControl(
                info.CrowdControl);
        }

        if (info.Knockback
                .sqrMagnitude > 0f)
        {
            RequestKnockback(
                info.Knockback);
        }

        if (wasFatal)
        {
            PlayerRef deathAttacker =
                ResolveDeathAttacker(
                    info.Source.InputAuthority);

            Die(
                deathAttacker,
                DeathCause.Damage);
        }

        return new DamageResult(
            appliedDamage,
            wasFatal);
    }


    private void RequestCancelAttack()
    {
        if (Commands != null)
        {
            Commands.RequestCancelAttack();
        }
    }


    private void RequestCrowdControl(
        CrowdControlType type,
        float duration)
    {
        if (Commands == null)
            return;

        Commands.RequestControlLock(
            CrowdControlRules.ResolveLocks(
                type),
            duration);
    }


    private void RequestStopMovement()
    {
        if (Commands == null)
            return;

        Commands.RequestSetMovementVelocity(
            Vector2.zero);
    }


    private void ApplyOrQueueCrowdControl(
        CrowdControlDefinition definition)
    {
        if (definition.Type ==
                CrowdControlType.None ||
            definition.Duration <= 0f)
        {
            return;
        }

        if (definition.IsImmediate)
        {
            RequestCrowdControl(
                definition.Type,
                definition.Duration);

            return;
        }

        QueueCrowdControl(
            definition);
    }


    private void QueueCrowdControl(
        CrowdControlDefinition definition)
    {
        int targetIndex = -1;
        float latestRemaining =
            float.MinValue;

        for (int i = 0;
             i < PendingCrowdControlCapacity;
             i++)
        {
            PendingCrowdControlState state =
                PendingCrowdControls[i];

            if (!state.IsActive)
            {
                targetIndex = i;
                break;
            }

            float remaining =
                state.ActivationTimer
                    .RemainingTime(Runner) ??
                0f;

            if (remaining <= latestRemaining)
                continue;

            latestRemaining = remaining;
            targetIndex = i;
        }

        PendingCrowdControls.Set(
            targetIndex,
            new PendingCrowdControlState
            {
                IsActive = true,
                Type = definition.Type,
                Duration = definition.Duration,
                ActivationTimer =
                    TickTimer.CreateFromSeconds(
                        Runner,
                        definition.ActivationDelay)
            });
    }


    private void ProcessPendingCrowdControls()
    {
        for (int i = 0;
             i < PendingCrowdControlCapacity;
             i++)
        {
            PendingCrowdControlState state =
                PendingCrowdControls[i];

            if (!state.IsActive ||
                !state.ActivationTimer
                    .Expired(Runner))
            {
                continue;
            }

            RequestCrowdControl(
                state.Type,
                state.Duration);

            PendingCrowdControls.Set(
                i,
                default);
        }
    }


    private void ClearPendingCrowdControls()
    {
        for (int i = 0;
             i < PendingCrowdControlCapacity;
             i++)
        {
            PendingCrowdControls.Set(
                i,
                default);
        }
    }


    private void RequestKnockback(
        Vector2 velocity)
    {
        if (Commands == null)
            return;

        Commands.RequestKnockback(
            velocity);
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

        ClearPendingCrowdControls();

        // 사망한 틱에 이미 진행 중인 공격도 즉시 취소한다.
        RequestCancelAttack();

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
        ClearPendingCrowdControls();

        Health =
            MaxHealth;

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


    private void RefreshMaxHealthModifier(
        float multiplier)
    {
        int previousMaximum =
            MaxHealth;

        int nextMaximum =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    BaseMaxHealth * multiplier));

        if (previousMaximum == nextMaximum)
            return;

        AppliedMaxHealth =
            nextMaximum;

        if (!IsAlive)
            return;

        if (nextMaximum > previousMaximum)
        {
            Health =
                Mathf.Min(
                    nextMaximum,
                    Health +
                    nextMaximum -
                    previousMaximum);
        }
        else
        {
            Health =
                Mathf.Min(
                    Health,
                    nextMaximum);
        }
    }


    private int BaseMaxHealth =>
        ResolveBaseMaxHealth(
            _statsData);


    private static int ResolveBaseMaxHealth(
        PlayerStatsData statsData)
    {
        return statsData != null
            ? statsData.MaxHealth
            : DefaultMaxHealth;
    }


    private int ResolveEffectiveDamage(
        in DamageInfo info)
    {
        float damageTakenMultiplier =
            _tickState != null
                ? _tickState
                    .ActiveStatModifiers
                    .DamageTaken
                : 1f;

        return Mathf.Max(
            0,
            Mathf.RoundToInt(
                info.Damage *
                damageTakenMultiplier));
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
            CameraShakeService.PlayDefaultHit(
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
                CameraTarget);

            CameraShakeService.PlayDefaultDeath(
                CameraTarget.position);

            LocalDeathOccurred?
                .Invoke(this);
        }
        else
        {
            bcc?.AddTarget(
                CameraTarget);
        }
    }
}
