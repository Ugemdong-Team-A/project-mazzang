using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using UnityEngine.U2D.IK;

[DefaultExecutionOrder(-210)]
public sealed class PlayerWeaponController :
    PlayerTickModule,
    IWeaponHandler,
    IPlayerTickCommandSink,
    IPlayerTickStateSource
{
    [Header("Weapon")]
    [SerializeField]
    private Transform weaponSocket;

    [Header("Weapon Presentation")]
    [SerializeField]
    private int weaponSortingOrder = 9;

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

    private Transform _leftHandAnimationTarget;

    private Transform _rightHandAnimationTarget;

    private bool _hasCapturedAnimationHandTargets;

    private Transform _presentationRoot;


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

    public Transform PresentationRoot =>
        _presentationRoot != null
            ? _presentationRoot
            : transform;

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
        NetworkRigidbody networkRigidbody =
            GetComponent<NetworkRigidbody>();

        _presentationRoot =
            networkRigidbody != null
                ? networkRigidbody.InterpolationTarget
                : null;

        CaptureAnimationHandIkTargets();
        RestoreAnimationHandIk();

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
        _boundIkView =
            null;
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
            !state.FacingRight,
            ResolveGameplayWeaponOrigin(
                state),
            state.ActiveStatModifiers.AttackDamage);

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
                    deathDropVelocity,
                    ResolveGameplayWeaponOrigin(
                        state));
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

        bool dropPressed =
            input.Buttons.WasPressed(
                PreviousButtons,
                PlayerButton.Drop);

        if (dropPressed)
        {
            PreviousButtons =
                input.Buttons;

            DropWeapon(
                CalculateDropVelocity(
                    state),
                ResolveGameplayWeaponOrigin(
                    state));

            return;
        }

        if (state.HasSkill &&
            state.IsSkillActionLocked)
        {
            PreviousButtons =
                input.Buttons;

            return;
        }

        bool secondaryPressed =
            input.Buttons.WasPressed(
                PreviousButtons,
                PlayerButton.Parry);

        PreviousButtons =
            input.Buttons;

        if (secondaryPressed &&
            ConsumesParryInput)
        {
            TryUseSecondaryWeapon(
                state.HasMovement &&
                !state.FacingRight,
                ResolveGameplayWeaponOrigin(
                    state));
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
                    false);
        }

        UpdateWeaponIkBinding();
    }


    // =========================================================
    // Weapon Aim
    // =========================================================

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

        direction =
            state.ResolveLimitedAimDirection(
                direction);

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

        return DropWeapon(
            Vector2.zero,
            transform.position);
    }


    private bool DropWeapon(
        Vector2 velocity,
        Vector2 origin)
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

        RestoreAnimationHandIk();

        weapon.Drop(
            previousHolder,
            origin,
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

        if (ReferenceEquals(
                _boundIkView,
                heldView))
        {
            return;
        }

        if (heldView != null)
        {
            BindWeaponIk(
                heldView);

            return;
        }

        RestoreAnimationHandIk();
    }


    private void BindWeaponIk(
        HeldWeaponView heldView)
    {
        if (heldView == null)
            return;

        _boundIkView =
            heldView;

        BindHandLimb(
            leftHandLimb,
            heldView.LeftHandGrip != null
                ? heldView.LeftHandGrip
                : _leftHandAnimationTarget);

        BindHandLimb(
            rightHandLimb,
            heldView.RightHandGrip != null
                ? heldView.RightHandGrip
                : _rightHandAnimationTarget);
    }


    private void RestoreAnimationHandIk()
    {
        _boundIkView =
            null;

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
        bool mirrored,
        Vector2 origin)
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

        return weapon.TryUseSecondary(
            origin,
            WeaponDirection,
            mirrored);
    }


    private bool TryUseWeapon(
        Vector2 aimDirection,
        bool isAlive,
        bool mirrored,
        Vector2 origin,
        float attackDamageMultiplier)
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

        return weapon.TryUse(
            origin,
            WeaponDirection,
            mirrored,
            attackDamageMultiplier);
    }


    private Vector2 ResolveGameplayWeaponOrigin(
        PlayerTickState state)
    {
        Vector2 fallbackPosition =
            transform.position;

        return state != null
            ? state.ResolveAimOrigin(
                fallbackPosition)
            : fallbackPosition;
    }
}
