using Fusion;
using UnityEngine;

[DefaultExecutionOrder(-180)]
public sealed class PlayerWeaponController :
    PlayerModule,
    IPlayerWeaponState,
    IPlayerWeaponControl
{
    [Header("Weapon")]
    [SerializeField]
    private Transform weaponSocket;

    [Header("Drop")]
    [Min(0f)]
    [SerializeField]
    private float repickupBlockDuration = 0.35f;

    private IPlayerAimState
        _aimState;

    private IPlayerHealthState
        _healthState;

    private IPlayerMovementState
        _movementState;


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
        if (!HasStateAuthority)
            return;

        EquippedWeaponObject =
            null;

        PreviousButtons =
            default;
    }


    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        bool hasInput =
            GetInput(
                out PlayerInputData input);

        if (_healthState != null &&
            !_healthState.IsAlive)
        {
            if (HasEquippedWeapon)
            {
                Vector2 deathDropVelocity =
                    _movementState != null
                        ? _movementState.Velocity
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

        bool dropPressed =
            input.Buttons.WasPressed(
                PreviousButtons,
                PlayerButton.Drop);

        PreviousButtons =
            input.Buttons;

        if (!dropPressed)
            return;

        TryDropWeapon();
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

        // 현재는 "던지기"가 아니라 단순 버리기이므로
        // 의도적인 추가 속도를 주지 않는다.
        return DropWeapon(
            Vector2.zero);
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

        weapon.Drop(
            previousHolder,
            velocity,
            repickupBlockDuration);

        return true;
    }


    // =========================================================
    // Use
    // =========================================================

    public bool TryUseWeapon(
        Vector2 aimDirection)
    {
        // PlayerCombat은 InputAuthority에서도 예측 실행되지만,
        // 실제 Weapon 상태 변경 / Projectile Spawn은
        // StateAuthority만 확정한다.
        if (!HasStateAuthority)
            return false;

        if (_healthState != null &&
            !_healthState.IsAlive)
        {
            return false;
        }

        Weapon weapon =
            EquippedWeapon;

        if (weapon == null)
            return false;

        Vector2 direction =
            ResolveAimDirection(
                aimDirection);

        Vector2 origin =
            weaponSocket != null
                ? weaponSocket.position
                : transform.position;

        return weapon.TryUse(
            Object.InputAuthority,
            origin,
            direction);
    }


    // =========================================================
    // Pose
    // =========================================================

    public bool TryGetWeaponPose(
        out Vector2 position,
        out float angle)
    {
        position =
            weaponSocket != null
                ? weaponSocket.position
                : transform.position;

        Vector2 direction =
            ResolveAimDirection(
                Vector2.zero);

        angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;

        return true;
    }


    private Vector2 ResolveAimDirection(
        Vector2 sourceDirection)
    {
        if (sourceDirection.sqrMagnitude >
            0.0001f)
        {
            return sourceDirection.normalized;
        }

        if (_aimState != null &&
            _aimState.AimDirection.sqrMagnitude >
            0.0001f)
        {
            return _aimState.AimDirection.normalized;
        }

        if (_movementState != null &&
            !_movementState.FacingRight)
        {
            return Vector2.left;
        }

        return Vector2.right;
    }
}