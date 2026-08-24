using UnityEngine;

/// <summary>
/// 플레이어 모듈 사이의 제어 요청을 Tick 경계에서 전달합니다.
/// 요청은 대상 모듈의 구체 타입이나 Context Unit을 참조하지 않습니다.
/// </summary>
public sealed class PlayerTickCommands
{
    private IPlayerTickCommandDispatcher _dispatcher;

    private bool _cancelAttackRequested;

    private bool _knockbackRequested;
    private Vector2 _knockbackVelocity;
    private float _knockbackControlLock;

    private bool _aimCommandRequested;
    private bool _clearAimOverride;
    private PlayerAimOverride _aimOverride;
    private Vector2 _sourceAimDirection;

    private bool _facingRequested;
    private bool _facingRight;

    private bool _controlLockRequested;
    private float _controlLockDuration;

    private bool _movementVelocityRequested;
    private Vector2 _movementVelocity;

    private bool _weaponUseRequested;
    private Vector2 _weaponAimDirection;


    public bool HasPending =>
        _cancelAttackRequested ||
        _knockbackRequested ||
        _aimCommandRequested ||
        _facingRequested ||
        _controlLockRequested ||
        _movementVelocityRequested ||
        _weaponUseRequested;


    public void RequestCancelAttack()
    {
        _cancelAttackRequested = true;
        Dispatch();
    }


    public void RequestKnockback(
        Vector2 velocity,
        float controlLockDuration)
    {
        _knockbackRequested = true;
        _knockbackVelocity = velocity;
        _knockbackControlLock = controlLockDuration;
        Dispatch();
    }


    public void RequestAimOverride(
        in PlayerAimOverride aimOverride,
        Vector2 sourceAimDirection)
    {
        _aimCommandRequested = true;
        _clearAimOverride = false;
        _aimOverride = aimOverride;
        _sourceAimDirection = sourceAimDirection;
        Dispatch();
    }


    public void RequestClearAimOverride()
    {
        _aimCommandRequested = true;
        _clearAimOverride = true;
        _aimOverride = default;
        _sourceAimDirection = Vector2.zero;
        Dispatch();
    }

    public void RequestControlLock(float controlLockDuration)
    {
        _controlLockRequested = true;
        _controlLockDuration = controlLockDuration;

        Dispatch();
    }


    public void RequestSetMovementVelocity(
        Vector2 velocity)
    {
        _movementVelocityRequested = true;
        _movementVelocity = velocity;

        Dispatch();
    }

    public void RequestFacing(
        bool facingRight)
    {
        _facingRequested = true;
        _facingRight = facingRight;
        Dispatch();
    }


    public void RequestWeaponUse(
        Vector2 aimDirection)
    {
        _weaponUseRequested = true;
        _weaponAimDirection = aimDirection;
        Dispatch();
    }


    internal void SetDispatcher(
        IPlayerTickCommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }


    private void Dispatch()
    {
        _dispatcher?
            .DispatchTickCommands();
    }

    internal bool TryConsumeControlLock(out float duration)
    {
        if (!_controlLockRequested)
        {
            duration = 0f;
            return false;
        }

        _controlLockRequested = false;
        duration = _controlLockDuration;
        return true;
    }


    internal bool TryConsumeSetMovementVelocity(
        out Vector2 velocity)
    {
        if (!_movementVelocityRequested)
        {
            velocity = default;
            return false;
        }

        _movementVelocityRequested = false;
        velocity = _movementVelocity;
        return true;
    }

    internal bool TryConsumeCancelAttack()
    {
        if (!_cancelAttackRequested)
            return false;

        _cancelAttackRequested = false;
        return true;
    }


    internal bool TryConsumeKnockback(
        out Vector2 velocity,
        out float controlLockDuration)
    {
        if (!_knockbackRequested)
        {
            velocity = default;
            controlLockDuration = default;
            return false;
        }

        _knockbackRequested = false;
        velocity = _knockbackVelocity;
        controlLockDuration = _knockbackControlLock;
        return true;
    }


    internal bool TryConsumeAimCommand(
        out bool clearOverride,
        out PlayerAimOverride aimOverride,
        out Vector2 sourceAimDirection)
    {
        if (!_aimCommandRequested)
        {
            clearOverride = default;
            aimOverride = default;
            sourceAimDirection = default;
            return false;
        }

        _aimCommandRequested = false;
        clearOverride = _clearAimOverride;
        aimOverride = _aimOverride;
        sourceAimDirection = _sourceAimDirection;
        return true;
    }


    internal bool TryConsumeFacing(
        out bool facingRight)
    {
        if (!_facingRequested)
        {
            facingRight = default;
            return false;
        }

        _facingRequested = false;
        facingRight = _facingRight;
        return true;
    }


    internal bool TryConsumeWeaponUse(
        out Vector2 aimDirection)
    {
        if (!_weaponUseRequested)
        {
            aimDirection = default;
            return false;
        }

        _weaponUseRequested = false;
        aimDirection = _weaponAimDirection;
        return true;
    }
}
