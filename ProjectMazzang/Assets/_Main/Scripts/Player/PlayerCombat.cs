using System.Collections.Generic;
using Fusion;
using UnityEngine;

public enum PlayerAttackState : byte
{
    None = 0,
    Startup,
    Active,
    Recovery
}

[DefaultExecutionOrder(-200)]
public sealed class PlayerCombat :
    PlayerModule,
    IPlayerCombatState,
    IPlayerCombatControl
{
    [Header("Basic Attack")]
    [SerializeField]
    private int basicDamage = 10;

    [SerializeField]
    private Vector2 basicAttackOffset =
        new Vector2(1f, 0f);

    [SerializeField]
    private Vector2 basicAttackSize =
        new Vector2(1.5f, 1f);

    [SerializeField]
    private LayerMask hurtboxLayer;

    [SerializeField]
    private Vector2 basicKnockback =
        new Vector2(6f, 4f);

    [SerializeField]
    private float basicKnockbackControlLock =
        0.12f;


    [Header("Basic Attack Timing")]
    [SerializeField]
    private float startupDuration = 0.08f;

    [SerializeField]
    private float activeDuration = 0.06f;

    [SerializeField]
    private float recoveryDuration = 0.2f;

    [SerializeField]
    [Min(0f)]
    private float basicAttackCooldown = 0.45f;


    private readonly HashSet<IDamageable>
        _hitTargets = new();

    private IPlayerMovementState
        _movementState;

    private IPlayerHealthState
        _healthState;

    private IPlayerDamageReceiver
        _selfDamageReceiver;


    // =========================================================
    // Network State
    // =========================================================

    [Networked]
    private NetworkButtons PreviousButtons { get; set; }

    [Networked]
    private TickTimer AttackPhaseTimer { get; set; }

    [Networked]
    private TickTimer AttackCooldownTimer { get; set; }

    [Networked]
    private NetworkBool AttackFacingRight { get; set; }


    [Networked]
    public PlayerAttackState AttackState { get; private set; }

    [Networked]
    public byte AttackSequence { get; private set; }


    public bool IsAttacking =>
        AttackState !=
        PlayerAttackState.None;


    public bool IsAttackOnCooldown =>
        !AttackCooldownTimer
            .ExpiredOrNotRunning(Runner);


    // =========================================================
    // Context
    // =========================================================

    protected override void RegisterContextUnits()
    {
        Context.Register<
            IPlayerCombatState>(
            this);

        Context.Register<
            IPlayerCombatControl>(
            this);
    }


    protected override void OnContextReady()
    {
        _movementState =
            Context.Get<
                IPlayerMovementState>();

        _healthState =
            Context.Get<
                IPlayerHealthState>();

        _selfDamageReceiver =
            Context.Get<
                IPlayerDamageReceiver>();
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void FixedUpdateNetwork()
    {
        if (_healthState == null)
            return;

        if (!_healthState.IsAlive)
        {
            CancelAttack();

            if (GetInput(
                    out PlayerInputData deadInput))
            {
                PreviousButtons =
                    deadInput.Buttons;
            }

            return;
        }

        // 피격으로 인한 제어락 중에는
        // 진행 중 공격을 즉시 취소하고 새 공격도 받지 않는다.
        if (_movementState != null &&
            _movementState.IsControlLocked)
        {
            CancelAttack();

            if (GetInput(
                    out PlayerInputData lockedInput))
            {
                PreviousButtons =
                    lockedInput.Buttons;
            }

            return;
        }

        UpdateAttack();

        if (!GetInput(
                out PlayerInputData input))
        {
            return;
        }

        bool attackPressed =
            input.Buttons.WasPressed(
                PreviousButtons,
                PlayerButton.Attack);

        PreviousButtons =
            input.Buttons;

        if (attackPressed)
        {
            TryAttack(
                input.Move);
        }
    }


    // =========================================================
    // Attack Request
    // =========================================================

    private void TryAttack(
        Vector2 moveInput)
    {
        if (IsAttacking)
            return;

        if (IsAttackOnCooldown)
            return;

        if (_movementState != null &&
            _movementState.IsControlLocked)
        {
            return;
        }

        /*
         * 나중에는 여기서만 분기한다.
         *
         * if (HasEquippedWeapon)
         * {
         *     StartWeaponAttack(...);
         *     return;
         * }
         */

        StartBasicAttack(
            moveInput);
    }


    // =========================================================
    // Basic Attack
    // =========================================================

    private void StartBasicAttack(
        Vector2 moveInput)
    {
        AttackState =
            PlayerAttackState.Startup;

        AttackPhaseTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                startupDuration);

        AttackCooldownTimer =
            basicAttackCooldown > 0f
                ? TickTimer.CreateFromSeconds(
                    Runner,
                    basicAttackCooldown)
                : TickTimer.None;

        if (moveInput.x > 0.01f)
        {
            AttackFacingRight =
                true;
        }
        else if (moveInput.x < -0.01f)
        {
            AttackFacingRight =
                false;
        }
        else
        {
            AttackFacingRight =
                _movementState != null
                    ? _movementState.FacingRight
                    : true;
        }

        AttackSequence++;
    }


    private void UpdateAttack()
    {
        switch (AttackState)
        {
            case PlayerAttackState.None:
                break;

            case PlayerAttackState.Startup:
                UpdateStartup();
                break;

            case PlayerAttackState.Active:
                UpdateActive();
                break;

            case PlayerAttackState.Recovery:
                UpdateRecovery();
                break;
        }
    }


    private void UpdateStartup()
    {
        if (!AttackPhaseTimer
                .Expired(Runner))
        {
            return;
        }

        BeginActive();
    }


    private void BeginActive()
    {
        AttackState =
            PlayerAttackState.Active;

        PerformBasicAttackHit();

        AttackPhaseTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                activeDuration);
    }


    private void UpdateActive()
    {
        if (!AttackPhaseTimer
                .Expired(Runner))
        {
            return;
        }

        AttackState =
            PlayerAttackState.Recovery;

        AttackPhaseTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                recoveryDuration);
    }


    private void UpdateRecovery()
    {
        if (!AttackPhaseTimer
                .Expired(Runner))
        {
            return;
        }

        CancelAttack();
    }


    public void CancelAttack()
    {
        AttackState =
            PlayerAttackState.None;

        AttackPhaseTimer =
            TickTimer.None;
    }


    // =========================================================
    // Hit
    // =========================================================

    private void PerformBasicAttackHit()
    {
        float direction =
            AttackFacingRight
                ? 1f
                : -1f;

        Vector2 offset =
            basicAttackOffset;

        offset.x *=
            direction;

        Vector2 center =
            (Vector2)transform.position +
            offset;

        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                center,
                basicAttackSize,
                0f,
                hurtboxLayer);

        _hitTargets.Clear();

        foreach (Collider2D hit in hits)
        {
            IDamageable damageable =
                hit.GetComponentInParent<
                    IDamageable>();

            if (damageable == null)
                continue;

            if (ReferenceEquals(
                    damageable,
                    _selfDamageReceiver))
            {
                continue;
            }

            if (!_hitTargets.Add(
                    damageable))
            {
                continue;
            }

            if (!damageable.IsAlive)
                continue;

            Vector2 knockback =
                basicKnockback;

            knockback.x *=
                direction;

            DamageInfo info =
                new DamageInfo(
                    basicDamage,
                    Object.InputAuthority,
                    knockback,
                    basicKnockbackControlLock);

            damageable.ApplyDamage(
                in info);
        }
    }


#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        Vector2 rightOffset =
            basicAttackOffset;

        Vector2 leftOffset =
            basicAttackOffset;

        leftOffset.x *=
            -1f;

        Vector2 rightCenter =
            (Vector2)transform.position +
            rightOffset;

        Vector2 leftCenter =
            (Vector2)transform.position +
            leftOffset;

        Gizmos.DrawWireCube(
            rightCenter,
            basicAttackSize);

        Gizmos.DrawWireCube(
            leftCenter,
            basicAttackSize);
    }

#endif
}