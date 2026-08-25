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
    IPlayerCombatControl,
    IPlayerTickModule,
    IPlayerTickStateSource,
    IPlayerTickCommandSink
{
    private const int NoneAttackId = 0;


    [Header("Test Attacks")]
    [SerializeField]
    private PlayerAttackDefinition jabAttack;

    [SerializeField]
    private PlayerAttackDefinition counterAttack;


    [Header("Hit")]
    [SerializeField]
    private Transform attackSocket;

    [SerializeField]
    private LayerMask hurtboxLayer;


    private readonly HashSet<IDamageable>
        _hitTargets = new();


    private IPlayerMovementState
        _movementState;

    private IPlayerHealthState
        _healthState;

    private IPlayerAimState
        _aimState;

    private IPlayerAimControl
        _aimControl;

    private IPlayerWeaponState
        _weaponState;

    private IPlayerWeaponControl
        _weaponControl;


    // =========================================================
    // Network State
    // =========================================================

    [Networked]
    private NetworkButtons PreviousButtons
    {
        get;
        set;
    }


    [Networked]
    private TickTimer AttackPhaseTimer
    {
        get;
        set;
    }


    [Networked]
    private TickTimer AttackCooldownTimer
    {
        get;
        set;
    }


    [Networked]
    public PlayerAttackState AttackState
    {
        get;
        private set;
    }


    [Networked]
    public int CurrentAttackId
    {
        get;
        private set;
    }


    [Networked]
    public byte AttackSequence
    {
        get;
        private set;
    }


    // =========================================================
    // State
    // =========================================================

    public bool IsAttacking =>
        AttackState !=
        PlayerAttackState.None;


    public bool IsAttackOnCooldown =>
        !AttackCooldownTimer
            .ExpiredOrNotRunning(
                Runner);


    public bool IsMovementLocked
    {
        get
        {
            if (!IsAttacking)
                return false;

            if (!TryGetCurrentAttackDefinition(
                    out PlayerAttackDefinition definition))
            {
                return false;
            }

            return
                definition.MovementMode ==
                PlayerAttackMovementMode.Locked;
        }
    }


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

        _aimState =
            Context.Get<
                IPlayerAimState>();

        _aimControl =
            Context.Get<
                IPlayerAimControl>();

        _weaponState =
            Context.Get<
                IPlayerWeaponState>();

        _weaponControl =
            Context.Get<
                IPlayerWeaponControl>();
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        if (!HasStateAuthority)
            return;

        PreviousButtons =
            default;

        AttackState =
            PlayerAttackState.None;

        CurrentAttackId =
            NoneAttackId;

        AttackPhaseTimer =
            TickTimer.None;

        AttackCooldownTimer =
            TickTimer.None;
    }


    PlayerTickStage IPlayerTickModule.Stage =>
        PlayerTickStage.Action;


    void IPlayerTickModule.Simulate(
        in PlayerTick tick)
    {
        TickAction(
            tick.State.HasHealth &&
            tick.State.IsAlive,
            tick.State.HasMovement &&
            tick.State.IsMovementControlLocked,
            tick.State.HasSkill &&
            tick.State.IsSkillActionLocked,
            !tick.State.HasMovement ||
            tick.State.FacingRight,
            tick.State);
    }


    void IPlayerTickStateSource.CaptureTickState(
        PlayerTickState state)
    {
        state.HasCombat = true;
        state.IsCombatMovementLocked = IsMovementLocked;
    }


    bool IPlayerTickCommandSink.ResolveTickCommands(
        PlayerTickCommands commands,
        PlayerTickState state)
    {
        if (!commands.TryConsumeCancelAttack())
            return false;

        CancelAttack();
        return true;
    }


    public override void FixedUpdateNetwork()
    {
        if (IsTickControlled)
            return;

        TickAction();
    }


    internal void TickAction()
    {
        TickAction(
            _healthState != null &&
            _healthState.IsAlive,
            _movementState != null &&
            _movementState.IsControlLocked,
            false,
            ResolveFacingRight(),
            null);
    }


    private void TickAction(
        bool isAlive,
        bool isMovementControlLocked,
        bool isSkillActionLocked,
        bool facingRight,
        PlayerTickState tickState)
    {
        if (tickState == null &&
            !IsContextReady)
            return;

        bool hasInput =
            GetInput(
                out PlayerInputData input);


        // ==========================================
        // Dead
        // ==========================================

        if (!isAlive)
        {
            CancelAttack();

            if (hasInput)
            {
                PreviousButtons =
                    input.Buttons;
            }

            return;
        }


        // ==========================================
        // Control Lock
        // ==========================================

        if (isMovementControlLocked)
        {
            CancelAttack();

            if (hasInput)
            {
                PreviousButtons =
                    input.Buttons;
            }

            return;
        }


        if (isSkillActionLocked)
        {
            CancelAttack();

            if (hasInput)
            {
                PreviousButtons =
                    input.Buttons;
            }

            return;
        }


        // ==========================================
        // Attack State
        // ==========================================

        UpdateAttack(
            facingRight);


        if (!hasInput)
            return;


        // ==========================================
        // Attack Input
        // ==========================================

        bool attackPressed =
            input.Buttons.WasPressed(
                PreviousButtons,
                PlayerButton.Attack);

        PreviousButtons =
            input.Buttons;

        if (!attackPressed)
            return;


        TryAttack(
            in input,
            isMovementControlLocked,
            facingRight,
            tickState);
    }


    // =========================================================
    // Attack Request
    // =========================================================

    private void TryAttack(
        in PlayerInputData input,
        bool isMovementControlLocked,
        bool facingRight,
        PlayerTickState tickState)
    {
        if (IsAttacking)
            return;

        if (isMovementControlLocked)
        {
            return;
        }


        bool hasEquippedWeapon =
            tickState != null
                ? tickState.HasWeapon &&
                  tickState.HasEquippedWeapon
                : _weaponState != null &&
                  _weaponState.HasEquippedWeapon;

        if (hasEquippedWeapon)
        {
            Vector2 aimDirection =
                ResolveInputAimDirection(
                    input.AimWorldPosition,
                    facingRight,
                    tickState);

            if (TickCommands != null)
            {
                TickCommands.RequestWeaponUse(
                    aimDirection);
            }
            else
            {
                _weaponControl?
                    .TryUseWeapon(
                        aimDirection);
            }

            return;
        }


        if (IsAttackOnCooldown)
            return;


        PlayerAttackDefinition definition =
            SelectTestAttack(
                input.Move);

        if (!definition.IsValid)
            return;


        Vector2 sourceAimDirection =
            ResolveInputAimDirection(
                input.AimWorldPosition,
                facingRight,
                tickState);


        StartAttack(
            in definition,
            sourceAimDirection);
    }


    private PlayerAttackDefinition SelectTestAttack(
        Vector2 moveInput)
    {
        if (moveInput.y > 0.5f &&
            counterAttack.IsValid)
        {
            return counterAttack;
        }

        return jabAttack;
    }


    // =========================================================
    // Attack Start
    // =========================================================

    private void StartAttack(
        in PlayerAttackDefinition definition,
        Vector2 sourceAimDirection)
    {
        AttackData attack =
            definition.Attack;

        if (attack == null)
            return;


        CurrentAttackId =
            attack.AttackId;


        AttackState =
            PlayerAttackState.Startup;


        AttackPhaseTimer =
            CreateTimer(
                attack.StartupDuration);


        AttackCooldownTimer =
            CreateTimer(
                attack.Cooldown);


        ApplyAimRule(
            in definition,
            sourceAimDirection);


        AttackSequence++;
    }


    private void ApplyAimRule(
        in PlayerAttackDefinition definition,
        Vector2 sourceAimDirection)
    {
        PlayerAttackAimDefinition aimDefinition =
            definition.Aim;


        if (!aimDefinition.RequiresOverride)
        {
            RequestClearAimOverride();

            return;
        }


        PlayerAimOverride aimOverride =
            aimDefinition.CreateOverride();


        if (TickCommands != null)
        {
            TickCommands.RequestAimOverride(
                in aimOverride,
                sourceAimDirection);
        }
        else
        {
            _aimControl?
                .ApplyOverride(
                    in aimOverride,
                    sourceAimDirection);
        }
    }


    // =========================================================
    // Attack Update
    // =========================================================

    private void UpdateAttack(
        bool facingRight)
    {
        switch (AttackState)
        {
            case PlayerAttackState.None:
                break;


            case PlayerAttackState.Startup:
                UpdateStartup(
                    facingRight);
                break;


            case PlayerAttackState.Active:
                UpdateActive();
                break;


            case PlayerAttackState.Recovery:
                UpdateRecovery();
                break;
        }
    }


    private void UpdateStartup(
        bool facingRight)
    {
        if (!AttackPhaseTimer
                .Expired(Runner))
        {
            return;
        }


        BeginActive(
            facingRight);
    }


    private void BeginActive(
        bool facingRight)
    {
        if (!TryGetCurrentAttackDefinition(
                out PlayerAttackDefinition definition))
        {
            CancelAttack();

            return;
        }


        AttackData attack =
            definition.Attack;


        AttackState =
            PlayerAttackState.Active;


        if (attack is
            BoxAttackData boxAttack)
        {
            PerformBoxAttackHit(
                boxAttack,
                facingRight);
        }


        AttackPhaseTimer =
            CreateTimer(
                attack.ActiveDuration);
    }


    private void UpdateActive()
    {
        if (!AttackPhaseTimer
                .Expired(Runner))
        {
            return;
        }


        AttackData attack =
            GetCurrentAttack();

        if (attack == null)
        {
            CancelAttack();

            return;
        }


        AttackState =
            PlayerAttackState.Recovery;


        AttackPhaseTimer =
            CreateTimer(
                attack.RecoveryDuration);
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


    // =========================================================
    // Cancel
    // =========================================================

    public void CancelAttack()
    {
        if (AttackState ==
                PlayerAttackState.None &&
            CurrentAttackId ==
                NoneAttackId)
        {
            return;
        }


        AttackState =
            PlayerAttackState.None;


        AttackPhaseTimer =
            TickTimer.None;


        CurrentAttackId =
            NoneAttackId;


        RequestClearAimOverride();
    }


    private void RequestClearAimOverride()
    {
        if (TickCommands != null)
        {
            TickCommands.RequestClearAimOverride();
            return;
        }

        _aimControl?
            .ClearOverride();
    }


    // =========================================================
    // Attack Definition
    // =========================================================

    private bool TryGetCurrentAttackDefinition(
        out PlayerAttackDefinition definition)
    {
        if (CurrentAttackId !=
                NoneAttackId &&
            jabAttack.IsValid &&
            jabAttack.Attack.AttackId ==
                CurrentAttackId)
        {
            definition =
                jabAttack;

            return true;
        }


        if (CurrentAttackId !=
                NoneAttackId &&
            counterAttack.IsValid &&
            counterAttack.Attack.AttackId ==
                CurrentAttackId)
        {
            definition =
                counterAttack;

            return true;
        }


        definition =
            default;

        return false;
    }


    private AttackData GetCurrentAttack()
    {
        return
            TryGetCurrentAttackDefinition(
                out PlayerAttackDefinition definition)
                ? definition.Attack
                : null;
    }


    // =========================================================
    // Aim
    // =========================================================

    private Vector2 ResolveInputAimDirection(
        Vector2 aimWorldPosition,
        bool facingRight,
        PlayerTickState tickState)
    {
        if (tickState != null)
        {
            Vector2 direction =
                tickState.ResolveAimDirectionTo(
                    aimWorldPosition);

            if (direction.sqrMagnitude >
                0.0001f)
            {
                return direction.normalized;
            }
        }
        else if (_aimState != null)
        {
            Vector2 direction =
                _aimState.ResolveDirectionTo(
                    aimWorldPosition);


            if (direction.sqrMagnitude >
                0.0001f)
            {
                return
                    direction.normalized;
            }
        }


        return
            facingRight
                ? Vector2.right
                : Vector2.left;
    }


    // =========================================================
    // Box Hit
    // =========================================================

    private void PerformBoxAttackHit(
        BoxAttackData attack,
        bool facingRight)
    {
        if (attack == null)
            return;


        float facingSign =
            facingRight
                ? 1f
                : -1f;


        Vector2 center =
            CalculateBoxCenter(
                attack,
                facingRight);


        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                center,
                attack.HitboxSize,
                0f,
                hurtboxLayer);


        _hitTargets.Clear();


        foreach (Collider2D hit
                 in hits)
        {
            IDamageable damageable =
                hit.GetComponentInParent<
                    IDamageable>();


            if (damageable == null)
                continue;


            NetworkObject damageableObject =
                hit.GetComponentInParent<
                    NetworkObject>();

            if (damageableObject == Object)
                continue;


            if (!_hitTargets.Add(
                    damageable))
            {
                continue;
            }


            if (!damageable.IsAlive)
                continue;


            Vector2 knockback =
                new Vector2(
                    attack.KnockbackForward *
                    facingSign,
                    attack.KnockbackUp);


            DamageInfo info =
                new DamageInfo(
                    attack.Damage,
                    Object,
                    knockback,
                    attack.KnockbackControlLock);


            CombatDamageService.ApplyDamage(
                damageable,
                in info);
        }
    }


    private Vector2 CalculateBoxCenter(
        BoxAttackData attack,
        bool facingRight)
    {
        Vector2 offset =
            attack.HitboxOffset;


        if (!facingRight)
        {
            offset.x *=
                -1f;
        }


        return
            GetAttackOriginPosition() +
            offset;
    }


    private Vector2 GetAttackOriginPosition()
    {
        return
            attackSocket != null
                ? attackSocket.position
                : transform.position;
    }


    private bool ResolveFacingRight()
    {
        return
            _movementState == null ||
            _movementState.FacingRight;
    }


    // =========================================================
    // Timer
    // =========================================================

    private TickTimer CreateTimer(
        float duration)
    {
        return duration > 0f
            ? TickTimer.CreateFromSeconds(
                Runner,
                duration)
            : TickTimer.None;
    }


#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        Vector2 origin =
            GetAttackOriginPosition();


        Gizmos.DrawWireSphere(
            origin,
            0.06f);


        if (Application.isPlaying)
        {
            bool facingRight =
                ResolveFacingRight();


            DrawAttackGizmo(
                jabAttack,
                facingRight);


            DrawAttackGizmo(
                counterAttack,
                facingRight);


            return;
        }


        DrawAttackGizmo(
            jabAttack,
            true);


        DrawAttackGizmo(
            jabAttack,
            false);


        DrawAttackGizmo(
            counterAttack,
            true);


        DrawAttackGizmo(
            counterAttack,
            false);
    }


    private void DrawAttackGizmo(
        PlayerAttackDefinition definition,
        bool facingRight)
    {
        if (!definition.IsValid)
            return;


        if (definition.Attack is not
            BoxAttackData boxAttack)
        {
            return;
        }


        Vector2 center =
            CalculateBoxCenter(
                boxAttack,
                facingRight);


        Gizmos.DrawWireCube(
            center,
            boxAttack.HitboxSize);
    }

#endif
}
