using Fusion;
using UnityEngine;

/// <summary>
/// PlayerContext에 등록할 수 있는 플레이어 내부 계약의 공통 단위입니다.
/// 상태 조회와 즉시 명령 모두 이 인터페이스를 기반으로 등록합니다.
/// </summary>
public interface IPlayerContextUnit
{
}


// =========================================================
// Movement
// =========================================================

public interface IPlayerMovementState :
    IPlayerContextUnit
{
    Vector2 Velocity { get; }

    bool IsGrounded { get; }

    bool FacingRight { get; }

    bool IsWallSliding { get; }

    byte JumpSequence { get; }

    JumpType LastJumpType { get; }
}


public interface IPlayerKnockbackReceiver :
    IPlayerContextUnit
{
    void ApplyKnockback(
        Vector2 velocity,
        float controlLockDuration);
}


// =========================================================
// Combat
// =========================================================

public interface IPlayerCombatState :
    IPlayerContextUnit
{
    PlayerAttackState AttackState { get; }

    bool IsAttacking { get; }

    byte AttackSequence { get; }
}


public interface IPlayerCombatControl :
    IPlayerContextUnit
{
    void CancelAttack();
}


// =========================================================
// Health
// =========================================================

public interface IPlayerHealthState :
    IPlayerContextUnit
{
    int Health { get; }

    int MaxHealth { get; }

    int Lives { get; }

    int MaxLives { get; }

    bool IsDead { get; }

    bool IsAlive { get; }

    bool IsInvulnerable { get; }

    byte DeathSequence { get; }

    PlayerRef LastDeathAttacker { get; }

    DeathCause LastDeathCause { get; }
}


public interface IPlayerDamageReceiver :
    IPlayerContextUnit,
    IDamageable
{
}
