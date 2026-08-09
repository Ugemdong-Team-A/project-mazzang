using Fusion;
using UnityEngine;

/// <summary>
/// PlayerContext에 등록할 수 있는 공통 단위입니다.
/// 상태 조회용 인터페이스와 기능 요청용 인터페이스 모두
/// 이 인터페이스를 상속합니다.
/// </summary>
public interface IPlayerContextUnit
{
}


// =========================================================
// Movement State
// =========================================================

public interface IPlayerMovementState :
    IPlayerContextUnit
{
    Vector2 Velocity { get; }

    bool IsGrounded { get; }

    bool IsTouchingWallLeft { get; }

    bool IsTouchingWallRight { get; }

    bool IsTouchingWall { get; }

    NetworkBool FacingRight { get; }

    NetworkBool IsWallSliding { get; }

    byte JumpSequence { get; }

    JumpType LastJumpType { get; }
}


// =========================================================
// Movement Commands
// =========================================================

public interface IPlayerKnockbackReceiver :
    IPlayerContextUnit
{
    void ApplyKnockback(
        Vector2 velocity,
        float controlLockDuration);
}


// =========================================================
// Combat State
// =========================================================

public interface IPlayerCombatState :
    IPlayerContextUnit
{
    PlayerAttackState AttackState { get; }

    bool IsAttacking { get; }

    byte AttackSequence { get; }
}


// =========================================================
// Health State
// =========================================================

public interface IPlayerHealthState :
    IPlayerContextUnit
{
    int Health { get; }

    int MaxHealth { get; }

    int Lives { get; }

    int MaxLives { get; }

    NetworkBool IsDead { get; }

    bool IsAlive { get; }

    bool IsInvulnerable { get; }

    byte DeathSequence { get; }

    PlayerRef LastDeathAttacker { get; }

    DeathCause LastDeathCause { get; }
}
