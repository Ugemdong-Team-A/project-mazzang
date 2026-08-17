using Fusion;
using UnityEngine;

/// <summary>
/// 기존 UI, Render, fallback 경로가 PlayerContext에 등록하는 호환 계약입니다.
/// 새 네트워크 Tick 모듈 통신에는 사용하지 않습니다.
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

    bool IsControlLocked { get; }

    byte JumpSequence { get; }

    JumpType LastJumpType { get; }
}

public interface IPlayerMovementControl :
    IPlayerContextUnit
{
    void SetVelocity(
        Vector2 velocity);

    void LockControl(
        float duration);
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

    int CurrentAttackId { get; }

    bool IsAttacking { get; }

    bool IsMovementLocked { get; }

    bool IsAttackOnCooldown { get; }

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

// =========================================================
// Aim
// =========================================================

public interface IPlayerAimState :
    IPlayerContextUnit
{
    Vector2 AimDirection { get; }

    float BodyAimAngle { get; }

    bool IsAimOverridden { get; }

    PlayerAimTrackingMode TrackingMode { get; }

    PlayerAimRigMode RigMode { get; }

    PlayerAimCardinalDirection CardinalDirection { get; }

    Vector2 ResolveDirectionTo(
        Vector2 worldTargetPosition);
}


public interface IPlayerAimControl :
    IPlayerContextUnit
{
    void ApplyOverride(
        in PlayerAimOverride aimOverride,
        Vector2 sourceAimDirection);

    void ClearOverride();
}


// =========================================================
// Facing
// =========================================================

public interface IPlayerFacingControl :
    IPlayerContextUnit
{
    void SetFacing(
        bool facingRight);
}

// =========================================================
// Weapon
// =========================================================

public interface IPlayerWeaponState :
    IPlayerContextUnit
{
    bool HasEquippedWeapon { get; }

    NetworkObject EquippedWeaponObject { get; }
}


public interface IPlayerWeaponControl :
    IPlayerContextUnit
{
    bool TryUseWeapon(
        Vector2 aimDirection);
}

// =========================================================
// Skill
// =========================================================

public interface IPlayerSkillAnimationState :
    IPlayerContextUnit
{
    byte SkillAnimationSequence { get; }

    PlayerSkillAnimationId LastSkillAnimation { get; }
}
