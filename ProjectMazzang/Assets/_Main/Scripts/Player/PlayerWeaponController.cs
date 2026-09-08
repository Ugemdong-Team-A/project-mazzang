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

    [Networked]
    private TickTimer WeaponAnimationTimer
    {
        get;
        set;
    }

    [Networked]
    private TickTimer WeaponActionTimer
    {
        get;
        set;
    }

    [Networked]
    private NetworkBool LockedWeaponFacingRight
    {
        get;
        set;
    }

    [Networked]
    private WeaponMoveDirection ActiveWeaponMove
    {
        get;
        set;
    }

    [Networked]
    public byte WeaponAnimationSequence
    {
        get;
        private set;
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

        WeaponAnimationTimer =
            TickTimer.None;

        WeaponActionTimer =
            TickTimer.None;

        LockedWeaponFacingRight = true;
        ActiveWeaponMove =
            WeaponMoveDirection.Side;

        WeaponAnimationSequence = 0;
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
        if (WeaponAnimationTimer.IsRunning &&
            WeaponAnimationTimer.Expired(Runner))
        {
            WeaponAnimationTimer =
                TickTimer.None;
        }

        if (WeaponActionTimer.IsRunning &&
            WeaponActionTimer.Expired(Runner))
        {
            WeaponActionTimer =
                TickTimer.None;
        }

        TickPrepareAction(
            tick.State,
            false);
    }


    void IPlayerTickStateSource.CaptureTickState(
        PlayerTickState state)
    {
        state.HasEquippedWeapon = HasEquippedWeapon;
        state.IsWeaponFacingLocked =
            IsWeaponDirectionLocked;
        state.WeaponFacingRight =
            LockedWeaponFacingRight;
        state.WeaponAnimationSequence =
            WeaponAnimationSequence;
        state.IsWeaponAnimationActive =
            WeaponAnimationTimer.IsRunning &&
            !WeaponAnimationTimer.Expired(Runner);
        state.WeaponAnimation =
            EquippedWeapon?.GetPrimaryAnimation(
                ActiveWeaponMove);
    }


    bool IPlayerTickCommandSink.ResolveTickCommands(
        PlayerTickCommands commands,
        PlayerTickState state)
    {
        if (!commands.TryConsumeWeaponUse(
                out Vector2 moveInput,
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
            moveInput,
            aimDirection,
            !state.HasHealth ||
            state.IsAlive,
            !state.HasMovement ||
            state.FacingRight,
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
            hasInput &&
            !IsWeaponDirectionLocked)
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
        if (tickState.TryGetActiveActionClipData(
                out ActionAnimationClipData clipData))
        {
            ApplyActionHandIkPolicy(
                clipData.HandIkPolicy);
        }
        else
        {
            UpdateWeaponIkBinding();
        }

        if (weaponSocket == null ||
            !HasEquippedWeapon)
        {
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
    }


    // =========================================================
    // Weapon Aim
    // =========================================================

    private bool IsWeaponDirectionLocked =>
        WeaponActionTimer.IsRunning &&
        !WeaponActionTimer.Expired(Runner);

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

        WeaponAnimationTimer =
            TickTimer.None;

        WeaponActionTimer =
            TickTimer.None;

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


    private void ApplyActionHandIkPolicy(
        ActionHandIkPolicy policy)
    {
        switch (policy)
        {
            case ActionHandIkPolicy.AnimatedTargets:
                RestoreAnimationHandIk();
                break;

            case ActionHandIkPolicy.WeaponGrips:
                Weapon equippedWeapon =
                    EquippedWeapon;

                if (equippedWeapon?.HeldView != null)
                {
                    BindWeaponIk(
                        equippedWeapon.HeldView);
                }
                else
                {
                    RestoreAnimationHandIk();
                }
                break;

            default:
                UpdateWeaponIkBinding();
                break;
        }
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
        Vector2 moveInput,
        Vector2 aimDirection,
        bool isAlive,
        bool facingRight,
        Vector2 origin,
        float attackDamageMultiplier)
    {
        if (!HasStateAuthority)
            return false;

        if (!isAlive)
            return false;

        Weapon weapon =
            EquippedWeapon;

        if (weapon == null)
            return false;

        Vector2 useDirection =
            weapon.ResolvePrimaryDirection(
                moveInput,
                aimDirection,
                facingRight,
                out WeaponMoveDirection moveDirection);

        bool useFacingRight =
            Mathf.Abs(useDirection.x) > 0.0001f
                ? useDirection.x > 0f
                : facingRight;

        bool used = weapon.TryUse(
            origin,
            useDirection,
            !useFacingRight,
            attackDamageMultiplier);

        if (used)
        {
            WeaponAngle =
                DirectionToAngle(
                    useDirection);
            ActiveWeaponMove =
                moveDirection;

            ActionAnimationData animation =
                weapon.GetPrimaryAnimation(
                    moveDirection);

            ActionAnimationClipData clipData =
                animation != null
                    ? animation.GetClipData(
                        ActionAnimationPhase.Release)
                    : default;

            if (clipData.HasClip)
            {
                WeaponAnimationSequence++;
                WeaponAnimationTimer =
                    TickTimer.CreateFromSeconds(
                        Runner,
                        Mathf.Max(
                            clipData.Clip.length,
                            Runner.DeltaTime));
            }

            if (weapon.PrimaryDirection !=
                WeaponAttackDirection.Aim)
            {
                float actionDuration =
                    Mathf.Max(
                        weapon.PrimaryActionDuration,
                        clipData.HasClip
                            ? clipData.Clip.length
                            : 0f,
                        Runner.DeltaTime);

                LockedWeaponFacingRight =
                    useFacingRight;
                WeaponActionTimer =
                    TickTimer.CreateFromSeconds(
                        Runner,
                        actionDuration);
            }
        }

        return used;
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
