using UnityEngine;

/// <summary>
/// 플레이어 모듈 사이의 제어 요청을 같은 네트워크 Tick 안에서 전달합니다.
/// 요청은 대상 모듈의 구체 타입이나 Context Unit을 참조하지 않습니다.
/// 요청 즉시 PlayerController가 처리하며, 처리 중 생긴 요청은
/// 현재 Pass의 남은 Sink 또는 다음 Resolve Pass에서 처리합니다.
/// Control Lock 복합 요청은 종류별로 나뉘며,
/// PlayerMovement, PlayerCombat, PlayerSkillController가
/// 자기 요청만 한 번씩 소비합니다.
/// </summary>
public sealed class PlayerTickCommands
{
    private IPlayerTickCommandDispatcher _dispatcher;

    private bool _cancelAttackRequested;

    private bool _knockbackRequested;
    private Vector2 _knockbackVelocity;

    private bool _aimCommandRequested;
    private bool _clearAimOverride;
    private PlayerAimOverride _aimOverride;
    private Vector2 _sourceAimDirection;

    private bool _facingRequested;
    private bool _facingRight;

    private bool _movementControlLockRequested;
    private float _movementControlLockDuration;

    private bool _attackControlLockRequested;
    private float _attackControlLockDuration;

    private bool _skillControlLockRequested;
    private float _skillControlLockDuration;

    private bool _movementVelocityRequested;
    private Vector2 _movementVelocity;

    private bool _weaponUseRequested;
    private Vector2 _weaponAimDirection;


    public bool HasPending =>
        _cancelAttackRequested ||
        _knockbackRequested ||
        _aimCommandRequested ||
        _facingRequested ||
        _movementControlLockRequested ||
        _attackControlLockRequested ||
        _skillControlLockRequested ||
        _movementVelocityRequested ||
        _weaponUseRequested;


    public void RequestCancelAttack()
    {
        _cancelAttackRequested = true;
        Dispatch();
    }


    public void RequestKnockback(
        Vector2 velocity)
    {
        _knockbackRequested = true;
        _knockbackVelocity = velocity;
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

    public void RequestControlLock(
        PlayerControlLock controls,
        float duration)
    {
        if (controls == PlayerControlLock.None ||
            duration <= 0f)
        {
            return;
        }

        if ((controls & PlayerControlLock.Movement) != 0)
        {
            QueueControlLock(
                ref _movementControlLockRequested,
                ref _movementControlLockDuration,
                duration);
        }

        if ((controls & PlayerControlLock.Attack) != 0)
        {
            QueueControlLock(
                ref _attackControlLockRequested,
                ref _attackControlLockDuration,
                duration);
        }

        if ((controls & PlayerControlLock.Skill) != 0)
        {
            QueueControlLock(
                ref _skillControlLockRequested,
                ref _skillControlLockDuration,
                duration);
        }

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

    internal bool TryConsumeMovementControlLock(
        out float duration)
    {
        return TryConsumeControlLock(
            ref _movementControlLockRequested,
            ref _movementControlLockDuration,
            out duration);
    }


    internal bool TryConsumeAttackControlLock(
        out float duration)
    {
        return TryConsumeControlLock(
            ref _attackControlLockRequested,
            ref _attackControlLockDuration,
            out duration);
    }


    internal bool TryConsumeSkillControlLock(
        out float duration)
    {
        return TryConsumeControlLock(
            ref _skillControlLockRequested,
            ref _skillControlLockDuration,
            out duration);
    }


    private static void QueueControlLock(
        ref bool requested,
        ref float queuedDuration,
        float duration)
    {
        requested = true;
        queuedDuration = Mathf.Max(
            queuedDuration,
            duration);
    }


    private static bool TryConsumeControlLock(
        ref bool requested,
        ref float queuedDuration,
        out float duration)
    {
        if (!requested)
        {
            duration = 0f;
            return false;
        }

        requested = false;
        duration = queuedDuration;
        queuedDuration = 0f;
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
        out Vector2 velocity)
    {
        if (!_knockbackRequested)
        {
            velocity = default;
            return false;
        }

        _knockbackRequested = false;
        velocity = _knockbackVelocity;
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
