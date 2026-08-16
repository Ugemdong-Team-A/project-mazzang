using Fusion;
using UnityEngine;
using UnityEngine.U2D.IK;

[DefaultExecutionOrder(-210)]
public sealed class PlayerWeaponController :
    PlayerModule,
    IPlayerWeaponState,
    IPlayerWeaponControl,
    IPlayerTickModule,
    IPlayerTickCommandSink,
    IPlayerTickStateSource
{
    [Header("Weapon")]
    [SerializeField]
    private Transform weaponSocket;

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


    private IPlayerAimState
        _aimState;

    private IPlayerHealthState
        _healthState;

    private IPlayerMovementState
        _movementState;

    private HeldWeaponView
        _boundIkView;


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
    // Context
    // =========================================================

    protected override void RegisterContextUnits()
    {
        Context.Register<
            IPlayerWeaponState>(
            this);

        Context.Register<
            IPlayerWeaponControl>(
            this);
    }


    protected override void OnContextReady()
    {
        _aimState =
            Context.Get<
                IPlayerAimState>();

        _healthState =
            Context.Get<
                IPlayerHealthState>();

        _movementState =
            Context.Get<
                IPlayerMovementState>();
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        StabilizeWeaponSocket();

        // Prefab에 저장된 IK target/enable 상태가 첫 Render까지 팔을
        // 고정하지 않도록 권한과 관계없이 초기 바인딩을 해제한다.
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


    PlayerTickStage IPlayerTickModule.Stage =>
        PlayerTickStage.PrepareAction;


    void IPlayerTickModule.Simulate(
        in PlayerTick tick)
    {
        TickPrepareAction(
            tick.State,
            false);
    }


    void IPlayerTickStateSource.CaptureTickState(
        PlayerTickState state)
    {
        state.HasWeapon = true;
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

        TryUseWeapon(
            aimDirection,
            !state.HasHealth ||
            state.IsAlive,
            state.HasMovement &&
            !state.FacingRight);

        return true;
    }


    public override void FixedUpdateNetwork()
    {
        if (IsTickControlled)
            return;

        TickPrepareAction();
    }


    internal void TickPrepareAction()
    {
        PlayerTickState fallbackState =
            new();

        fallbackState.HasHealth =
            _healthState != null;
        fallbackState.IsAlive =
            _healthState != null &&
            _healthState.IsAlive;
        fallbackState.HasMovement =
            _movementState != null;
        fallbackState.MovementVelocity =
            _movementState != null
                ? _movementState.Velocity
                : Vector2.zero;
        fallbackState.FacingRight =
            _movementState == null ||
            _movementState.FacingRight;
        fallbackState.HasAim =
            _aimState != null;
        fallbackState.AimDirection =
            _aimState != null
                ? _aimState.AimDirection
                : Vector2.zero;

        TickPrepareAction(
            fallbackState,
            true);
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

            ApplyWeaponSocketRotation(
                WeaponAngle);
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

        PreviousButtons =
            input.Buttons;

        if (dropPressed)
        {
            DropWeapon(
                CalculateDropVelocity(
                    state));
        }
    }


    public override void Render()
    {
        if (weaponSocket == null ||
            !HasEquippedWeapon)
        {
            UpdateWeaponIkBinding();
            return;
        }

        float visualAngle =
            WeaponAngle;

        if (HasInputAuthority &&
            _aimState != null &&
            _aimState.AimDirection.sqrMagnitude >
                0.0001f)
        {
            visualAngle =
                DirectionToAngle(
                    _aimState.AimDirection);
        }

        ApplyWeaponSocketRotation(
            visualAngle);

        Weapon equippedWeapon =
            EquippedWeapon;

        if (equippedWeapon != null)
        {
            equippedWeapon
                .RefreshHeldPresentation(
                    _movementState != null &&
                    !_movementState.FacingRight);
        }

        UpdateWeaponIkBinding();
    }


    // =========================================================
    // Weapon Aim
    // =========================================================

    private void StabilizeWeaponSocket()
    {
        if (weaponSocket == null ||
            weaponSocket.parent == transform)
        {
            return;
        }

        weaponSocket.SetParent(
            transform,
            true);
    }


    private void UpdateAuthoritativeWeaponAngle(
        Vector2 aimWorldPosition,
        PlayerTickState state,
        bool useLegacyAim)
    {
        Vector2 direction =
            useLegacyAim &&
            _aimState != null
                ? _aimState.ResolveDirectionTo(
                    aimWorldPosition)
                : state.ResolveAimDirectionTo(
                    aimWorldPosition);

        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        WeaponAngle =
            DirectionToAngle(
                direction);
    }

    private void ApplyWeaponSocketRotation(
        float worldAngle)
    {
        Vector2 worldDirection =
            AngleToDirection(
                worldAngle);

        Transform parent =
            weaponSocket.parent;

        if (parent == null)
        {
            weaponSocket.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    worldAngle);

            return;
        }

        Vector3 localDirection3 =
            parent.InverseTransformVector(
                worldDirection);

        Vector2 localDirection =
            new Vector2(
                localDirection3.x,
                localDirection3.y);

        if (localDirection.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        float localAngle =
            Mathf.Atan2(
                localDirection.y,
                localDirection.x) *
            Mathf.Rad2Deg;

        weaponSocket.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                localAngle);
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

        if (_healthState != null &&
            !_healthState.IsAlive)
        {
            return false;
        }

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
            CalculateDropVelocity());
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

        if (_boundIkView ==
            heldView)
        {
            return;
        }

        UnbindWeaponIk();

        if (heldView == null)
            return;

        BindWeaponIk(
            heldView);
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
            heldView.LeftHandGrip);

        BindHandLimb(
            rightHandLimb,
            heldView.RightHandGrip);
    }


    private void UnbindWeaponIk()
    {
        UnbindHandLimb(
            leftHandLimb);

        UnbindHandLimb(
            rightHandLimb);

        _boundIkView =
            null;
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


    // =========================================================
    // Drop Velocity
    // =========================================================

    private Vector2 CalculateDropVelocity()
    {
        float facingSign =
            _movementState == null ||
            _movementState.FacingRight
                ? 1f
                : -1f;

        Vector2 tossVelocity =
            new Vector2(
                dropVelocity.x *
                facingSign,
                dropVelocity.y);

        if (_movementState == null)
        {
            return tossVelocity;
        }

        return
            tossVelocity +
            _movementState.Velocity *
            inheritedVelocityFactor;
    }


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

    public bool TryUseWeapon(
        Vector2 aimDirection)
    {
        return TryUseWeapon(
            aimDirection,
            _healthState == null ||
            _healthState.IsAlive,
            _movementState != null &&
            !_movementState.FacingRight);
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
