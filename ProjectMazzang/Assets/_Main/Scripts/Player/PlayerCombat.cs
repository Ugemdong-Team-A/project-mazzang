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
    private const int NoneAttackId = 0;


    [Header("Test Attacks")]
    [SerializeField]
    private BoxAttackData jabAttack;

    [SerializeField]
    private PlayerAttackAimDefinition jabAim;

    [SerializeField]
    private PlayerAttackMovementMode jabMovementMode;


    [Space]

    [SerializeField]
    private BoxAttackData counterAttack;

    [SerializeField]
    private PlayerAttackAimDefinition counterAim;

    [SerializeField]
    private PlayerAttackMovementMode counterMovementMode;


    [Header("Damage")]
    [SerializeField]
    private LayerMask hurtboxLayer;


    private readonly HashSet<IDamageable>
        _hitTargets = new();


    private IPlayerMovementState
        _movementState;

    private IPlayerHealthState
        _healthState;

    private IPlayerDamageReceiver
        _selfDamageReceiver;

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
            AttackData attack =
                GetCurrentAttack();

            if (!IsAttacking ||
                attack == null)
            {
                return false;
            }

            return GetMovementMode(
                    attack) ==
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

        _selfDamageReceiver =
            Context.Get<
                IPlayerDamageReceiver>();

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

        CurrentAttackId =
            NoneAttackId;
    }


    public override void FixedUpdateNetwork()
    {
        bool hasInput =
            GetInput(
                out PlayerInputData input);


        if (_healthState == null)
            return;


        // ==========================================
        // Dead
        // ==========================================

        if (!_healthState.IsAlive)
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

        if (_movementState != null &&
            _movementState.IsControlLocked)
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
        // Current Input Aim
        // ==========================================

        Vector2 currentInputAim =
            hasInput
                ? ResolveInputAimDirection(
                    input.AimWorldPosition)
                : Vector2.zero;


        // ==========================================
        // Attack State
        // ==========================================

        UpdateAttack(
            currentInputAim);


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
            currentInputAim);
    }


    // =========================================================
    // Attack Request
    // =========================================================

    private void TryAttack(
        in PlayerInputData input,
        Vector2 currentInputAim)
    {
        if (IsAttacking)
            return;

        if (_movementState != null &&
            _movementState.IsControlLocked)
        {
            return;
        }


        // 무기를 들고 있다면 Player 기본 공격 대신
        // 현재 장착 무기의 사용을 요청합니다.
        if (_weaponState != null &&
            _weaponState.HasEquippedWeapon)
        {
            _weaponControl?
                .TryUseWeapon(
                    currentInputAim);

            return;
        }


        if (IsAttackOnCooldown)
            return;


        BoxAttackData attack =
            SelectTestAttack(
                input.Move);

        if (attack == null)
            return;


        StartAttack(
            attack,
            currentInputAim);
    }


    /// <summary>
    /// 현재 테스트용 기본 공격 선택 규칙입니다.
    ///
    /// 위 입력 + 공격:
    /// Counter
    ///
    /// 그 외:
    /// Jab
    ///
    /// 이후 실제 공격 선택 시스템이 생기면
    /// 이 메서드만 교체합니다.
    /// </summary>
    private BoxAttackData SelectTestAttack(
        Vector2 moveInput)
    {
        if (moveInput.y > 0.5f &&
            counterAttack != null)
        {
            return counterAttack;
        }

        return jabAttack;
    }


    // =========================================================
    // Attack Start
    // =========================================================

    private void StartAttack(
        AttackData attack,
        Vector2 sourceAimDirection)
    {
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
            attack,
            sourceAimDirection);

        AttackSequence++;
    }


    private void ApplyAimRule(
        AttackData attack,
        Vector2 sourceAimDirection)
    {
        if (_aimControl == null)
            return;


        PlayerAttackAimDefinition aimDefinition =
            GetAimDefinition(
                attack);


        if (!aimDefinition.RequiresOverride)
        {
            _aimControl.ClearOverride();

            return;
        }


        PlayerAimOverride aimOverride =
            aimDefinition.CreateOverride();


        _aimControl.ApplyOverride(
            in aimOverride,
            sourceAimDirection);
    }


    // =========================================================
    // Attack Update
    // =========================================================

    private void UpdateAttack(
        Vector2 currentInputAim)
    {
        switch (AttackState)
        {
            case PlayerAttackState.None:
                break;


            case PlayerAttackState.Startup:
                UpdateStartup(
                    currentInputAim);
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
        Vector2 currentInputAim)
    {
        if (!AttackPhaseTimer
                .Expired(Runner))
        {
            return;
        }


        BeginActive(
            currentInputAim);
    }


    private void BeginActive(
        Vector2 currentInputAim)
    {
        AttackData attack =
            GetCurrentAttack();


        if (attack == null)
        {
            CancelAttack();

            return;
        }


        AttackState =
            PlayerAttackState.Active;


        // PlayerCombat은 현재 Box 공격을 직접 실행합니다.
        // AttackData 자체는 Player에 종속되지 않습니다.
        if (attack is
            BoxAttackData boxAttack)
        {
            Vector2 attackDirection =
                ResolveHitDirection(
                    attack,
                    currentInputAim);


            PerformBoxAttackHit(
                boxAttack,
                attackDirection);
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


        _aimControl?
            .ClearOverride();
    }


    // =========================================================
    // Attack Data
    // =========================================================

    private AttackData GetCurrentAttack()
    {
        if (CurrentAttackId ==
            NoneAttackId)
        {
            return null;
        }


        if (jabAttack != null &&
            jabAttack.AttackId ==
            CurrentAttackId)
        {
            return jabAttack;
        }


        if (counterAttack != null &&
            counterAttack.AttackId ==
            CurrentAttackId)
        {
            return counterAttack;
        }


        return null;
    }


    // =========================================================
    // Player Attack Rules
    // =========================================================

    private PlayerAttackAimDefinition
        GetAimDefinition(
            AttackData attack)
    {
        if (attack == jabAttack)
        {
            return jabAim;
        }


        if (attack == counterAttack)
        {
            return counterAim;
        }


        return default;
    }


    private PlayerAttackMovementMode
        GetMovementMode(
            AttackData attack)
    {
        if (attack == jabAttack)
        {
            return jabMovementMode;
        }


        if (attack == counterAttack)
        {
            return counterMovementMode;
        }


        return
            PlayerAttackMovementMode.Free;
    }


    // =========================================================
    // Aim
    // =========================================================

    private Vector2 ResolveInputAimDirection(
        Vector2 aimWorldPosition)
    {
        if (_aimState != null)
        {
            Vector2 direction =
                _aimState.ResolveDirectionTo(
                    aimWorldPosition);


            direction =
                NormalizeDirection(
                    direction);


            if (direction !=
                Vector2.zero)
            {
                return direction;
            }
        }


        if (_movementState != null &&
            !_movementState.FacingRight)
        {
            return Vector2.left;
        }


        return Vector2.right;
    }


    private Vector2 ResolveHitDirection(
        AttackData attack,
        Vector2 currentInputAim)
    {
        PlayerAttackAimDefinition aim =
            GetAimDefinition(
                attack);


        // Free Aim은 현재 Tick의 입력 방향을 사용합니다.
        if (aim.AimMode ==
            PlayerAttackAimMode.Free)
        {
            Vector2 freeDirection =
                NormalizeDirection(
                    currentInputAim);


            if (freeDirection !=
                Vector2.zero)
            {
                return freeDirection;
            }
        }


        // Locked / FourWay는 공격 시작 시
        // PlayerAim에 확정된 방향을 사용합니다.
        if (_aimState != null)
        {
            Vector2 aimDirection =
                NormalizeDirection(
                    _aimState.AimDirection);


            if (aimDirection !=
                Vector2.zero)
            {
                return aimDirection;
            }
        }


        if (_movementState != null &&
            !_movementState.FacingRight)
        {
            return Vector2.left;
        }


        return Vector2.right;
    }


    private static Vector2 NormalizeDirection(
        Vector2 direction)
    {
        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return Vector2.zero;
        }


        return direction.normalized;
    }


    // =========================================================
    // Box Damage
    // =========================================================

    private void PerformBoxAttackHit(
        BoxAttackData attack,
        Vector2 direction)
    {
        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return;
        }


        direction.Normalize();


        Vector2 perpendicular =
            new Vector2(
                -direction.y,
                direction.x);


        Vector2 localOffset =
            attack.HitboxOffset;


        Vector2 worldOffset =
            direction *
                localOffset.x +
            perpendicular *
                localOffset.y;


        Vector2 center =
            (Vector2)transform.position +
            worldOffset;


        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;


        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                center,
                attack.HitboxSize,
                angle,
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


            // 자기 자신 제외.
            if (ReferenceEquals(
                    damageable,
                    _selfDamageReceiver))
            {
                continue;
            }


            // 하나의 Damageable이 여러 Hurtbox Collider를
            // 가지고 있어도 한 공격에서는 한 번만 적용합니다.
            if (!_hitTargets.Add(
                    damageable))
            {
                continue;
            }


            if (!damageable.IsAlive)
                continue;


            Vector2 knockback =
                direction *
                    attack.KnockbackForward +
                Vector2.up *
                    attack.KnockbackUp;


            DamageInfo info =
                new DamageInfo(
                    attack.Damage,
                    Object,
                    knockback,
                    attack.KnockbackControlLock);


            damageable.ApplyDamage(
                in info);
        }
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
        DrawAttackGizmo(
            jabAttack);

        DrawAttackGizmo(
            counterAttack);
    }


    private void DrawAttackGizmo(
        BoxAttackData attack)
    {
        if (attack == null)
            return;


        Vector2 direction =
            Application.isPlaying &&
            _aimState != null &&
            _aimState.AimDirection.sqrMagnitude >
                0.0001f
                ? _aimState.AimDirection.normalized
                : Vector2.right;


        Vector2 perpendicular =
            new Vector2(
                -direction.y,
                direction.x);


        Vector2 offset =
            direction *
                attack.HitboxOffset.x +
            perpendicular *
                attack.HitboxOffset.y;


        Vector2 center =
            (Vector2)transform.position +
            offset;


        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;


        Matrix4x4 previousMatrix =
            Gizmos.matrix;


        Gizmos.matrix =
            Matrix4x4.TRS(
                center,
                Quaternion.Euler(
                    0f,
                    0f,
                    angle),
                Vector3.one);


        Gizmos.DrawWireCube(
            Vector3.zero,
            attack.HitboxSize);


        Gizmos.matrix =
            previousMatrix;
    }

#endif
}