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
    PlayerTickModule,
    IPlayerTickStateSource,
    IPlayerTickCommandSink
{
    private const int NoneAttackId = 0;


    [Header("Attack Data")]
    [SerializeField]
    private PlayerAttackData jabAttack;

    [SerializeField]
    private PlayerAttackData counterAttack;


    [Header("Hit")]
    [SerializeField]
    private LayerMask hurtboxLayer;


    private readonly HashSet<IDamageable>
        _hitTargets = new();

#if UNITY_EDITOR
    private Vector2 _attackGizmoOrigin;

    private bool _hasAttackGizmoOrigin;
#endif


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
    private byte ComboInputCount
    {
        get;
        set;
    }


    [Networked]
    private byte ComboDepth
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
    private TickTimer AttackControlLockTimer
    {
        get;
        set;
    }


    [Networked]
    private TickTimer AttackDashTimer
    {
        get;
        set;
    }


    [Networked]
    private Vector2 AttackDashDirection
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


    public bool IsAttackControlLocked =>
        !AttackControlLockTimer
            .ExpiredOrNotRunning(
                Runner);


    public bool IsMovementLocked
    {
        get
        {
            if (!IsAttacking)
                return false;

            if (!TryGetCurrentAttackData(
                    out PlayerAttackData attackData))
            {
                return false;
            }

            return
                attackData.MovementMode ==
                PlayerAttackMovementMode.Locked;
        }
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

        ComboInputCount = 0;
        ComboDepth = 0;

        AttackState =
            PlayerAttackState.None;

        CurrentAttackId =
            NoneAttackId;

        AttackPhaseTimer =
            TickTimer.None;

        AttackCooldownTimer =
            TickTimer.None;

        AttackControlLockTimer =
            TickTimer.None;

        AttackDashTimer =
            TickTimer.None;

        AttackDashDirection =
            Vector2.zero;
    }


    public override PlayerTickStage Stage =>
        PlayerTickStage.Action;


    public override void Simulate(
        in PlayerTick tick)
    {
#if UNITY_EDITOR
        _attackGizmoOrigin =
            tick.State.ResolveAimOrigin(
                transform.position);

        _hasAttackGizmoOrigin = true;
#endif

        TickAction(
            tick.State.HasHealth &&
            tick.State.IsAlive,
            tick.State.IsAttackControlLocked,
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
        state.IsAttacking = IsAttacking;
        state.AttackSequence = AttackSequence;
        state.AttackId = (byte)CurrentAttackId;
        state.IsAttackControlLocked =
            IsAttackControlLocked;
        state.IsCombatMovementLocked = IsMovementLocked;

        CaptureAttackDashState(
            state);
    }


    bool IPlayerTickCommandSink.ResolveTickCommands(
        PlayerTickCommands commands,
        PlayerTickState state)
    {
        bool resolved = false;

        if (commands.TryConsumeCancelAttack())
        {
            CancelAttack();
            resolved = true;
        }

        if (commands.TryConsumeAttackControlLock(
                out float duration))
        {
            LockAttackControl(
                duration);

            resolved = true;
        }

        return resolved;
    }


    /*public override void FixedUpdateNetwork()
    {
        if (IsTickControlled)
            return;

        TickAction();
    }*/


    private void TickAction(
        bool isAlive,
        bool isAttackControlLocked,
        bool isSkillActionLocked,
        bool facingRight,
        PlayerTickState tickState)
    {
        /*if (tickState == null &&
            !IsContextReady)
            return;*/

        bool hasInput =
            GetInput(
                out PlayerInputData input);

        bool attackPressed =
            hasInput &&
            input.Buttons.WasPressed(
                PreviousButtons,
                PlayerButton.Attack);

        if (hasInput)
        {
            PreviousButtons =
                input.Buttons;
        }


        // ==========================================
        // Dead
        // ==========================================

        if (!isAlive)
        {
            AttackControlLockTimer =
                TickTimer.None;

            CancelAttack();

            return;
        }


        // ==========================================
        // Active Skill Action Lock
        // ==========================================

        if (isSkillActionLocked)
        {
            CancelAttack();

            return;
        }


        // ==========================================
        // Attack State
        // ==========================================

        UpdateAttackDash();

        bool comboInputConsumed =
            UpdateAttack(
                facingRight,
                tickState,
                attackPressed &&
                !isAttackControlLocked);


        if (!hasInput)
            return;


        // ==========================================
        // Attack Control Lock
        // ==========================================

        if (isAttackControlLocked)
        {
            return;
        }


        // ==========================================
        // Attack Input
        // ==========================================

        if (!attackPressed ||
            comboInputConsumed)
            return;


        TryAttack(
            in input,
            facingRight,
            tickState);
    }


    // =========================================================
    // Attack Request
    // =========================================================

    private void TryAttack(
        in PlayerInputData input,
        bool facingRight,
        PlayerTickState tickState)
    {
        if (IsAttacking)
            return;


        bool hasEquippedWeapon =
            tickState != null
                ? tickState.HasEquippedWeapon : true;
                /*: _weaponState != null &&
                  _weaponState.HasEquippedWeapon*/

        if (hasEquippedWeapon)
        {
            Vector2 aimDirection =
                ResolveInputAimDirection(
                    input.AimWorldPosition,
                    facingRight,
                    tickState);

            if (Commands != null)
            {
                Commands.RequestWeaponUse(
                    aimDirection);
            }

            return;
        }


        if (IsAttackOnCooldown)
            return;


        PlayerAttackData attackData =
            SelectTestAttack(
                input.Move);

        if (attackData == null ||
            !attackData.IsValid)
            return;


        Vector2 sourceAimDirection =
            ResolveInputAimDirection(
                input.AimWorldPosition,
                facingRight,
                tickState);


        StartAttack(
            attackData,
            sourceAimDirection);
    }


    private PlayerAttackData SelectTestAttack(
        Vector2 moveInput)
    {
        if (moveInput.y > 0.5f &&
            counterAttack != null &&
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
        PlayerAttackData attackData,
        Vector2 sourceAimDirection,
        bool isComboFollowUp = false)
    {
        AttackData attack =
            attackData.Attack;

        if (attack == null)
            return;


        CurrentAttackId =
            attack.AttackId;

        ComboInputCount = 0;
        ComboDepth =
            isComboFollowUp
                ? (byte)1
                : (byte)0;


        AttackState =
            PlayerAttackState.Startup;


        AttackPhaseTimer =
            CreateTimer(
                attackData.StartupDuration);


        AttackCooldownTimer =
            CreateTimer(
                attackData.Cooldown);


        ApplyAimRule(
            attackData,
            sourceAimDirection);


        StartAttackDash(
            attackData.Dash,
            sourceAimDirection);


        AttackSequence++;
    }


    private void ApplyAimRule(
        PlayerAttackData attackData,
        Vector2 sourceAimDirection)
    {
        PlayerAttackAimData aimData =
            attackData.Aim;


        if (!aimData.RequiresOverride)
        {
            RequestClearAimOverride();

            return;
        }


        PlayerAimOverride aimOverride =
            aimData.CreateOverride();


        if (Commands != null)
        {
            Commands.RequestAimOverride(
                in aimOverride,
                sourceAimDirection);
        }
    }


    // =========================================================
    // Attack Dash
    // =========================================================

    private bool IsAttackDashActive =>
        AttackDashTimer.IsRunning &&
        !AttackDashTimer.Expired(Runner);


    private void StartAttackDash(
        DashData dash,
        Vector2 sourceAimDirection)
    {
        StopAttackDash();

        if (dash == null ||
            dash.Duration <= 0f ||
            dash.Speed <= 0f)
        {
            AttackDashTimer =
                TickTimer.None;

            AttackDashDirection =
                Vector2.zero;

            return;
        }

        AttackDashDirection =
            sourceAimDirection.sqrMagnitude > 0.0001f
                ? sourceAimDirection.normalized
                : Vector2.right;

        AttackDashTimer =
            CreateTimer(
                dash.Duration);
    }


    private void UpdateAttackDash()
    {
        if (!AttackDashTimer.IsRunning ||
            !AttackDashTimer.Expired(Runner))
        {
            return;
        }

        StopAttackDash();
    }


    private void StopAttackDash()
    {
        bool wasRunning =
            AttackDashTimer.IsRunning;

        AttackDashTimer =
            TickTimer.None;

        AttackDashDirection =
            Vector2.zero;

        if (wasRunning &&
            Commands != null)
        {
            Commands.RequestSetMovementVelocity(
                Vector2.zero);
        }
    }


    private void CaptureAttackDashState(
        PlayerTickState state)
    {
        PlayerAttackData attackData =
            null;

        bool hasDash =
            IsAttackDashActive &&
            TryGetCurrentAttackData(
                out attackData) &&
            attackData.Dash != null;

        state.HasCombatDash =
            hasDash;

        state.CombatDashVelocity =
            hasDash
                ? AttackDashDirection *
                  attackData.Dash.Speed
                : Vector2.zero;
    }


    // =========================================================
    // Attack Update
    // =========================================================

    private bool UpdateAttack(
        bool facingRight,
        PlayerTickState tickState,
        bool attackPressed)
    {
        switch (AttackState)
        {
            case PlayerAttackState.None:
                return false;


            case PlayerAttackState.Startup:
                return UpdateStartup(
                    facingRight,
                    tickState,
                    attackPressed);


            case PlayerAttackState.Active:
                return UpdateActive(
                    attackPressed);


            case PlayerAttackState.Recovery:
                return UpdateRecovery(
                    facingRight,
                    tickState,
                    attackPressed);


            default:
                return false;
        }
    }


    private bool UpdateStartup(
        bool facingRight,
        PlayerTickState tickState,
        bool attackPressed)
    {
        if (!AttackPhaseTimer
                .Expired(Runner))
        {
            return false;
        }


        BeginActive(
            facingRight,
            tickState);

        return TryBufferComboInput(
            attackPressed);
    }


    private void BeginActive(
        bool facingRight,
        PlayerTickState tickState)
    {
        if (!TryGetCurrentAttackData(
                out PlayerAttackData attackData))
        {
            CancelAttack();

            return;
        }


        AttackData attack =
            attackData.Attack;


        AttackState =
            PlayerAttackState.Active;


        if (attack is
            BoxAttackData boxAttack)
        {
            PerformBoxAttackHit(
                boxAttack,
                facingRight,
                ResolveGameplayAttackOrigin(
                    tickState),
                tickState.ActiveStatModifiers.AttackDamage);
        }


        AttackPhaseTimer =
            CreateTimer(
                attackData.ActiveDuration);
    }


    private bool UpdateActive(
        bool attackPressed)
    {
        bool comboInputConsumed =
            TryBufferComboInput(
                attackPressed);

        if (!AttackPhaseTimer
                .Expired(Runner))
        {
            return comboInputConsumed;
        }


        if (!TryGetCurrentAttackData(
                out PlayerAttackData attackData))
        {
            CancelAttack();

            return comboInputConsumed;
        }


        AttackState =
            PlayerAttackState.Recovery;


        AttackPhaseTimer =
            CreateTimer(
                attackData.RecoveryDuration);

        return comboInputConsumed;
    }


    private bool UpdateRecovery(
        bool facingRight,
        PlayerTickState tickState,
        bool attackPressed)
    {
        bool comboInputConsumed =
            TryBufferComboInput(
                attackPressed);

        if (!AttackPhaseTimer
                .Expired(Runner))
        {
            return comboInputConsumed;
        }


        if (TryStartComboFollowUp(
                facingRight,
                tickState))
        {
            return comboInputConsumed;
        }

        CancelAttack();

        return comboInputConsumed;
    }


    // =========================================================
    // Combo
    // =========================================================

    private bool TryBufferComboInput(
        bool attackPressed)
    {
        if (!attackPressed ||
            ComboDepth > 0 ||
            !TryGetCurrentAttackData(
                out PlayerAttackData attackData) ||
            ResolveComboFollowUp(
                attackData) == null)
        {
            return false;
        }

        if (attackData.AllowRepeatedComboInput)
        {
            ComboInputCount = 1;
        }
        else
        {
            ComboInputCount =
                (byte)Mathf.Min(
                    ComboInputCount + 1,
                    2);
        }

        return true;
    }


    private bool TryStartComboFollowUp(
        bool facingRight,
        PlayerTickState tickState)
    {
        if (ComboDepth > 0 ||
            !TryGetCurrentAttackData(
                out PlayerAttackData attackData) ||
            !IsComboInputSatisfied(
                ComboInputCount,
                attackData.AllowRepeatedComboInput))
        {
            return false;
        }

        PlayerAttackData followUp =
            ResolveComboFollowUp(
                attackData);

        if (followUp == null ||
            !followUp.IsValid)
        {
            return false;
        }

        Vector2 sourceAimDirection =
            tickState != null &&
            tickState.AimDirection.sqrMagnitude >
                0.0001f
                ? tickState.AimDirection.normalized
                : facingRight
                    ? Vector2.right
                    : Vector2.left;

        StartAttack(
            followUp,
            sourceAimDirection,
            true);

        return true;
    }


    private PlayerAttackData ResolveComboFollowUp(
        PlayerAttackData attackData)
    {
        PlayerAttackData configuredFollowUp =
            attackData != null
                ? attackData.ComboFollowUp
                : null;

        if (configuredFollowUp != null &&
            configuredFollowUp.IsValid)
        {
            return configuredFollowUp;
        }

        // 기존 프리팹과 SO를 다시 설정하지 않아도
        // 현재 기본 잽은 PlayerCombat의 Counter 슬롯으로 이어집니다.
        return attackData == jabAttack &&
               counterAttack != null &&
               counterAttack.IsValid
            ? counterAttack
            : null;
    }


    private static bool IsComboInputSatisfied(
        byte inputCount,
        bool allowRepeatedInput)
    {
        return allowRepeatedInput
            ? inputCount >= 1
            : inputCount == 1;
    }


    // =========================================================
    // Cancel
    // =========================================================

    public void CancelAttack()
    {
        StopAttackDash();

        if (AttackState ==
                PlayerAttackState.None &&
            CurrentAttackId ==
                NoneAttackId)
        {
            ComboInputCount = 0;
            ComboDepth = 0;

            return;
        }


        AttackState =
            PlayerAttackState.None;


        AttackPhaseTimer =
            TickTimer.None;


        CurrentAttackId =
            NoneAttackId;

        ComboInputCount = 0;
        ComboDepth = 0;


        RequestClearAimOverride();
    }


    private void RequestClearAimOverride()
    {
        if (Commands != null)
        {
            Commands.RequestClearAimOverride();
        }
    }


    // =========================================================
    // Attack Data
    // =========================================================

    private bool TryGetCurrentAttackData(
        out PlayerAttackData attackData)
    {
        if (MatchesCurrentAttack(
                jabAttack))
        {
            attackData =
                jabAttack;

            return true;
        }


        if (MatchesCurrentAttack(
                counterAttack))
        {
            attackData =
                counterAttack;

            return true;
        }


        PlayerAttackData jabFollowUp =
            jabAttack != null
                ? jabAttack.ComboFollowUp
                : null;

        if (MatchesCurrentAttack(
                jabFollowUp))
        {
            attackData =
                jabFollowUp;

            return true;
        }


        PlayerAttackData counterFollowUp =
            counterAttack != null
                ? counterAttack.ComboFollowUp
                : null;

        if (MatchesCurrentAttack(
                counterFollowUp))
        {
            attackData =
                counterFollowUp;

            return true;
        }


        attackData =
            null;

        return false;
    }


    private bool MatchesCurrentAttack(
        PlayerAttackData attackData)
    {
        return CurrentAttackId !=
                   NoneAttackId &&
               attackData != null &&
               attackData.IsValid &&
               attackData.Attack.AttackId ==
                   CurrentAttackId;
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
        /*else if (_aimState != null)
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
        }*/


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
        bool facingRight,
        Vector2 attackOrigin,
        float attackDamageMultiplier)
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
                facingRight,
                attackOrigin);


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
                    attackDamageMultiplier,
                    Object,
                    knockback,
                    attack.CrowdControl);


            CombatDamageService.ApplyDamage(
                damageable,
                in info);
        }
    }


    private Vector2 CalculateBoxCenter(
        BoxAttackData attack,
        bool facingRight,
        Vector2 attackOrigin)
    {
        Vector2 offset =
            attack.HitboxOffset;


        if (!facingRight)
        {
            offset.x *=
                -1f;
        }


        return
            attackOrigin +
            offset;
    }


    private Vector2 ResolveGameplayAttackOrigin(
        PlayerTickState tickState)
    {
        Vector2 fallbackPosition =
            transform.position;

        return tickState != null
            ? tickState.ResolveAimOrigin(
                fallbackPosition)
            : fallbackPosition;
    }


    private bool ResolveFacingRight()
    {
        return
            true/*_movementState == null ||
            _movementState.FacingRight*/;
    }


    // =========================================================
    // Timer
    // =========================================================

    private void LockAttackControl(
        float duration)
    {
        if (duration <= 0f)
            return;

        float remaining =
            AttackControlLockTimer
                .RemainingTime(Runner) ??
            0f;

        if (duration <= remaining)
            return;

        AttackControlLockTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                duration);
    }


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
            Application.isPlaying &&
            _hasAttackGizmoOrigin
                ? _attackGizmoOrigin
                : (Vector2)transform.position;


        Gizmos.DrawWireSphere(
            origin,
            0.06f);


        if (Application.isPlaying)
        {
            bool facingRight =
                ResolveFacingRight();


            DrawAttackGizmo(
                jabAttack,
                facingRight,
                origin);


            DrawAttackGizmo(
                counterAttack,
                facingRight,
                origin);


            return;
        }


        DrawAttackGizmo(
            jabAttack,
            true,
            origin);


        DrawAttackGizmo(
            jabAttack,
            false,
            origin);


        DrawAttackGizmo(
            counterAttack,
            true,
            origin);


        DrawAttackGizmo(
            counterAttack,
            false,
            origin);
    }


    private void DrawAttackGizmo(
        PlayerAttackData attackData,
        bool facingRight,
        Vector2 attackOrigin)
    {
        if (attackData == null ||
            !attackData.IsValid)
            return;


        if (attackData.Attack is not
            BoxAttackData boxAttack)
        {
            return;
        }


        Vector2 center =
            CalculateBoxCenter(
                boxAttack,
                facingRight,
                attackOrigin);


        Gizmos.DrawWireCube(
            center,
            boxAttack.HitboxSize);
    }

#endif
}
