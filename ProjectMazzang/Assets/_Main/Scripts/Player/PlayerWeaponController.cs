using Fusion;
using UnityEngine;
using UnityEngine.U2D.IK;

[DefaultExecutionOrder(-210)]
public sealed class PlayerWeaponController :
    PlayerTickModule,
    IPlayerTickCommandSink,
    IPlayerTickStateSource
{
    [Header("Weapon")]
    [SerializeField]
    private Transform weaponSocket;

    [Tooltip(
        "PlayerAim이 허리 제한과 표시 보간을 적용해 회전시키는 피벗입니다.")]
    [SerializeField]
    private Transform resolvedAimPivot;

    [Header("Weapon Presentation")]
    [SerializeField]
    private int weaponSortingOrder = 24;

    [Header("Weapon IK")]
    [SerializeField]
    private LimbSolver2D leftHandLimb;

    [SerializeField]
    private LimbSolver2D rightHandLimb;

    [Header("Drop")]
    [SerializeField]
    private Vector2 dropVelocity =
        new Vector2(
            2.5f,
            1.5f);

    [Range(0f, 1f)]
    [SerializeField]
    private float inheritedVelocityFactor = 0.5f;

    [Min(0f)]
    [SerializeField]
    private float repickupBlockDuration = 0.35f;

    private HeldWeaponView
        _boundIkView;

    private PlayerAim _playerAim;

    private Transform _leftHandAnimationTarget;

    private Transform _rightHandAnimationTarget;

    private bool _hasCapturedAnimationHandTargets;

    private bool _animationHandIkAllowed;


    // =========================================================
    // Network State
    // =========================================================

    [Networked]
    public NetworkObject EquippedWeaponObject
    {
        get;
        private set;
    }

    [Networked]
    public float WeaponAngle
    {
        get;
        private set;
    }

    [Networked]
    private NetworkButtons PreviousButtons
    {
        get;
        set;
    }


    // =========================================================
    // State
    // =========================================================

    public bool HasEquippedWeapon =>
        EquippedWeaponObject != null;

    public bool ConsumesParryInput =>
        EquippedWeapon != null &&
        EquippedWeapon.ConsumesParryInput;

    public Weapon EquippedWeapon =>
        EquippedWeaponObject != null
            ? EquippedWeaponObject.GetComponent<Weapon>()
            : null;

    public Transform WeaponSocket =>
        weaponSocket;

    public int WeaponSortingOrder =>
        weaponSortingOrder;

    public Vector2 WeaponDirection =>
        AngleToDirection(
            WeaponAngle);

    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        _playerAim = GetComponent<PlayerAim>();

        ResolveStandardRigReferences();

        StabilizeWeaponSocket();

        CaptureAnimationHandIkTargets();

        // 평상시에는 손 Target이 Animator의 팔 자세를 덮지 않게 해제합니다.
        // 공격 클립이 손 Target을 사용하는 동안에는 Present에서 복원합니다.
        UnbindWeaponIk();

        if (!HasStateAuthority)
            return;

        EquippedWeaponObject =
            null;

        WeaponAngle =
            0f;

        PreviousButtons =
            default;
    }


    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        UnbindWeaponIk();
    }


    public override PlayerTickStage Stage =>
        PlayerTickStage.PrepareAction;


    public override void Simulate(
        in PlayerTick tick)
    {
        TickPrepareAction(
            tick.State,
            false);
    }


    void IPlayerTickStateSource.CaptureTickState(
        PlayerTickState state)
    {
        state.HasEquippedWeapon = HasEquippedWeapon;
    }


    bool IPlayerTickCommandSink.ResolveTickCommands(
        PlayerTickCommands commands,
        PlayerTickState state)
    {
        if (!commands.TryConsumeWeaponUse(
                out Vector2 aimDirection))
        {
            return false;
        }

        if (state.IsAttackControlLocked ||
            (state.HasSkill &&
             state.IsSkillActionLocked))
        {
            return true;
        }

        TryUseWeapon(
            aimDirection,
            !state.HasHealth ||
            state.IsAlive,
            state.HasMovement &&
            !state.FacingRight);

        return true;
    }


    private void TickPrepareAction(
        PlayerTickState state,
        bool useLegacyAim)
    {

        bool hasInput =
            GetInput(
                out PlayerInputData input);

        // 실제 게임플레이용 WeaponAngle은
        // StateAuthority가 현재 Tick 입력으로 확정한다.
        if (HasStateAuthority &&
            hasInput)
        {
            UpdateAuthoritativeWeaponAngle(
                input.AimWorldPosition,
                state,
                useLegacyAim);
        }

        if (!HasStateAuthority)
            return;

        if (state.HasHealth &&
            !state.IsAlive)
        {
            if (HasEquippedWeapon)
            {
                Vector2 deathDropVelocity =
                    state.HasMovement
                        ? state.MovementVelocity
                        : Vector2.zero;

                DropWeapon(
                    deathDropVelocity);
            }

            if (hasInput)
            {
                PreviousButtons =
                    input.Buttons;
            }

            return;
        }

        if (!hasInput)
            return;

        if (state.HasSkill &&
            state.IsSkillActionLocked)
        {
            PreviousButtons =
                input.Buttons;

            return;
        }

        bool dropPressed =
            input.Buttons.WasPressed(
                PreviousButtons,
                PlayerButton.Drop);

        bool secondaryPressed =
            input.Buttons.WasPressed(
                PreviousButtons,
                PlayerButton.Parry);

        PreviousButtons =
            input.Buttons;

        if (dropPressed)
        {
            DropWeapon(
                CalculateDropVelocity(
                    state));

            return;
        }

        if (secondaryPressed &&
            ConsumesParryInput)
        {
            TryUseSecondaryWeapon(
                state.HasMovement &&
                !state.FacingRight);
        }
    }


    public override void Present(in PlayerTickState tickState)
    {
        if (weaponSocket == null ||
            !HasEquippedWeapon)
        {
            UpdateWeaponIkBinding();
            return;
        }

        Weapon equippedWeapon =
            EquippedWeapon;

        if (equippedWeapon != null)
        {
            equippedWeapon
                .RefreshHeldPresentation(
                    resolvedAimPivot == null &&
                    !tickState.FacingRight);
        }

        UpdateWeaponIkBinding();
    }


    // =========================================================
    // Weapon Aim
    // =========================================================

    private void StabilizeWeaponSocket()
    {
        if (weaponSocket == null)
            return;

        Transform stableParent =
            resolvedAimPivot != null
                ? resolvedAimPivot
                : transform;

        if (weaponSocket.parent != stableParent)
        {
            weaponSocket.SetParent(
                stableParent,
                true);
        }

        if (resolvedAimPivot != null)
        {
            weaponSocket.localPosition =
                Vector3.zero;

            weaponSocket.localScale =
                Vector3.one;
        }

        weaponSocket.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                -90f);
    }


    private void UpdateAuthoritativeWeaponAngle(
        Vector2 aimWorldPosition,
        PlayerTickState state,
        bool useLegacyAim)
    {
        Vector2 direction = state.ResolveAimDirectionTo(
                    aimWorldPosition);
            /*useLegacyAim &&
            _aimState != null
                ? _aimState.ResolveDirectionTo(
                    aimWorldPosition)
                : state.ResolveAimDirectionTo(
                    aimWorldPosition);*/

        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        if (_playerAim != null)
        {
            direction =
                _playerAim.ResolveLimitedAimDirection(
                    direction,
                    !state.HasMovement ||
                    state.FacingRight);
        }

        WeaponAngle =
            DirectionToAngle(
                direction);
    }

    private static float DirectionToAngle(
        Vector2 direction)
    {
        return
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;
    }


    private static Vector2 AngleToDirection(
        float angle)
    {
        float radians =
            angle *
            Mathf.Deg2Rad;

        return new Vector2(
            Mathf.Cos(radians),
            Mathf.Sin(radians));
    }


    // =========================================================
    // Equip / Drop
    // =========================================================

    public bool TryEquipWeapon(
        Weapon weapon)
    {
        if (!HasStateAuthority)
            return false;

        if (weapon == null ||
            HasEquippedWeapon)
        {
            return false;
        }

        /*if (_healthState != null &&
            !_healthState.IsAlive)
        {
            return false;
        }*/

        if (!weapon.TryEquip(
                Object))
        {
            return false;
        }

        EquippedWeaponObject =
            weapon.Object;

        return true;
    }


    public bool TryDropWeapon()
    {
        if (!HasStateAuthority)
            return false;

        if (!HasEquippedWeapon)
            return false;

        return DropWeapon(Vector2.zero);
    }


    private bool DropWeapon(
        Vector2 velocity)
    {
        Weapon weapon =
            EquippedWeapon;

        if (weapon == null)
        {
            EquippedWeaponObject =
                null;

            return false;
        }

        PlayerRef previousHolder =
            Object.InputAuthority;

        EquippedWeaponObject =
            null;

        UnbindWeaponIk();

        weapon.Drop(
            previousHolder,
            weaponSocket != null
                ? (Vector2)weaponSocket.position
                : (Vector2)transform.position,
            WeaponAngle,
            velocity,
            repickupBlockDuration);

        return true;
    }


    // =========================================================
    // Weapon IK
    // =========================================================

    private void UpdateWeaponIkBinding()
    {
        Weapon equippedWeapon =
            EquippedWeapon;

        HeldWeaponView heldView =
            equippedWeapon != null
                ? equippedWeapon.HeldView
                : null;

        bool allowAnimationHandIk =
            _playerAim != null &&
            (_playerAim.RigMode ==
                PlayerAimRigMode.AnimationOnly ||
             _playerAim.RigMode ==
                PlayerAimRigMode.AnimationWithBodyAim);

        if (_boundIkView ==
                heldView &&
            _animationHandIkAllowed ==
                allowAnimationHandIk)
        {
            return;
        }

        UnbindWeaponIk();

        _animationHandIkAllowed =
            allowAnimationHandIk;

        if (heldView != null)
        {
            BindWeaponIk(
                heldView,
                allowAnimationHandIk);

            return;
        }

        if (allowAnimationHandIk)
        {
            BindAnimationHandIk();
        }
    }


    private void BindWeaponIk(
        HeldWeaponView heldView,
        bool allowAnimationFallback)
    {
        if (heldView == null)
            return;

        _boundIkView =
            heldView;

        BindHandLimb(
            leftHandLimb,
            heldView.LeftHandGrip != null
                ? heldView.LeftHandGrip
                : allowAnimationFallback
                    ? _leftHandAnimationTarget
                    : null);

        BindHandLimb(
            rightHandLimb,
            heldView.RightHandGrip != null
                ? heldView.RightHandGrip
                : allowAnimationFallback
                    ? _rightHandAnimationTarget
                    : null);
    }


    private void BindAnimationHandIk()
    {
        BindHandLimb(
            leftHandLimb,
            _leftHandAnimationTarget);

        BindHandLimb(
            rightHandLimb,
            _rightHandAnimationTarget);
    }


    private void CaptureAnimationHandIkTargets()
    {
        if (_hasCapturedAnimationHandTargets)
            return;

        _leftHandAnimationTarget =
            GetHandLimbTarget(
                leftHandLimb);

        _rightHandAnimationTarget =
            GetHandLimbTarget(
                rightHandLimb);

        _hasCapturedAnimationHandTargets =
            true;
    }


    private void UnbindWeaponIk()
    {
        UnbindHandLimb(
            leftHandLimb);

        UnbindHandLimb(
            rightHandLimb);

        _boundIkView =
            null;

        _animationHandIkAllowed =
            false;
    }


    private static void BindHandLimb(
        LimbSolver2D limb,
        Transform target)
    {
        if (limb == null)
            return;

        IKChain2D chain =
            limb.GetChain(
                0);

        if (chain == null)
            return;

        chain.target =
            target;

        limb.enabled =
            target != null;
    }


    private static Transform GetHandLimbTarget(
        LimbSolver2D limb)
    {
        if (limb == null)
            return null;

        IKChain2D chain =
            limb.GetChain(
                0);

        return chain != null
            ? chain.target
            : null;
    }


    private static void UnbindHandLimb(
        LimbSolver2D limb)
    {
        if (limb == null)
            return;

        IKChain2D chain =
            limb.GetChain(
                0);

        if (chain != null)
        {
            chain.target =
                null;
        }

        limb.enabled =
            false;
    }


    private void ResolveStandardRigReferences()
    {
        if (resolvedAimPivot == null &&
            _playerAim != null)
        {
            resolvedAimPivot =
                _playerAim.ResolvedAimPivot;
        }

        Standard2DRigIKSetup setup =
            GetComponent<Standard2DRigIKSetup>();

        if (setup == null)
            return;

        if (leftHandLimb == null)
        {
            leftHandLimb =
                setup.GeneratedLeftArmSolver;
        }

        if (rightHandLimb == null)
        {
            rightHandLimb =
                setup.GeneratedRightArmSolver;
        }
    }


    // =========================================================
    // Drop Velocity
    // =========================================================


    private Vector2 CalculateDropVelocity(
        PlayerTickState state)
    {
        float facingSign =
            !state.HasMovement ||
            state.FacingRight
                ? 1f
                : -1f;

        Vector2 tossVelocity =
            new Vector2(
                dropVelocity.x *
                facingSign,
                dropVelocity.y);

        return state.HasMovement
            ? tossVelocity +
              state.MovementVelocity *
              inheritedVelocityFactor
            : tossVelocity;
    }


    // =========================================================
    // Use
    // =========================================================


    private bool TryUseSecondaryWeapon(
        bool mirrored)
    {
        if (!HasStateAuthority)
            return false;

        Weapon weapon =
            EquippedWeapon;

        if (weapon == null ||
            !weapon.ConsumesParryInput)
        {
            return false;
        }

        Vector2 origin =
            weaponSocket != null
                ? weaponSocket.position
                : transform.position;

        return weapon.TryUseSecondary(
            origin,
            WeaponDirection,
            mirrored);
    }


    private bool TryUseWeapon(
        Vector2 aimDirection,
        bool isAlive,
        bool mirrored)
    {
        // 기존 aimDirection 인자는 호출 호환성을 위해 유지하지만,
        // 실제 판정 방향은 StateAuthority가 확정한 WeaponAngle을 사용한다.
        _ = aimDirection;

        if (!HasStateAuthority)
            return false;

        if (!isAlive)
            return false;

        Weapon weapon =
            EquippedWeapon;

        if (weapon == null)
            return false;

        Vector2 origin =
            weaponSocket != null
                ? weaponSocket.position
                : transform.position;

        return weapon.TryUse(
            origin,
            WeaponDirection,
            mirrored);
    }
}
