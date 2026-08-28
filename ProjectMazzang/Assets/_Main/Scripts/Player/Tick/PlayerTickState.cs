using UnityEngine;

/// <summary>
/// 한 네트워크 Tick 안에서 단계 사이에 전달되는 플레이어 상태입니다.
/// Tick 시작 시 수집되고, 각 모듈 실행 직후 해당 모듈의 최신 값으로 갱신됩니다.
/// </summary>
public sealed class PlayerTickState
{
    public bool HasHealth { get; internal set; }

    public int Health { get; internal set; }

    public int MaxHealth { get; internal set; }

    public int Lives { get; internal set; }

    public bool IsInvulnerable { get; internal set; }

    public bool IsAlive { get; internal set; }

    public byte DeathSequence { get; internal set; }


    public bool HasMovement { get; internal set; }

    public bool FacingRight { get; internal set; }

    public bool IsGrounded { get; internal set; }

    public byte JumpSequence { get; internal set; }

    public JumpType LastJumpType { get; internal set; }

    public bool IsWallSliding { get; internal set; }

    public bool IsMovementControlLocked { get; internal set; }

    public Vector2 MovementVelocity { get; internal set; }


    public bool HasCombat { get; internal set; }

    public bool IsAttacking { get; internal set; }

    public byte AttackSequence { get; internal set; }

    public byte AttackId { get; internal set; }

    /// <summary>
    /// 외부 제어 효과로 기본 공격이 잠긴 상태입니다.
    /// </summary>
    public bool IsAttackControlLocked { get; internal set; }


    public bool HasSkill { get; internal set; }

    public byte SkillAnimationSequence { get; internal set; }

    public PlayerSkillAnimationId SkillAnimationId { get; internal set; }

    /// <summary>
    /// 외부 제어 효과로 새 스킬 사용이 잠긴 상태입니다.
    /// 이미 실행 중인 스킬의 진행 여부와는 무관합니다.
    /// </summary>
    public bool IsSkillControlLocked { get; internal set; }

    /// <summary>
    /// 실행 중인 스킬이 기본 공격과 무기 행동을 잠근 상태입니다.
    /// </summary>
    public bool IsSkillActionLocked { get; internal set; }

    /// <summary>
    /// 실행 중인 기본 공격이 이동을 잠근 상태입니다.
    /// </summary>
    public bool IsCombatMovementLocked { get; internal set; }


    public bool HasAim { get; internal set; }

    public bool HasAimOrigin { get; internal set; }

    public Vector2 AimOriginPosition { get; internal set; }

    public Vector2 AimDirection { get; internal set; }


    public bool HasEquippedWeapon { get; internal set; }


    internal void Reset()
    {
        HasHealth = false;
        Health = 0;
        MaxHealth = 0;
        Lives = 0;
        IsInvulnerable = false;
        IsAlive = false;
        DeathSequence = 0;

        HasMovement = false;
        FacingRight = true;
        IsGrounded = false;
        JumpSequence = 0;
        LastJumpType = default;
        IsWallSliding = false;
        IsMovementControlLocked = false;
        MovementVelocity = Vector2.zero;

        HasCombat = false;
        IsAttacking = false;
        AttackSequence = 0;
        AttackId = 0;
        IsAttackControlLocked = false;

        HasSkill = false;
        SkillAnimationSequence = 0;
        SkillAnimationId = PlayerSkillAnimationId.None;
        IsSkillControlLocked = false;
        IsSkillActionLocked = false;
        IsCombatMovementLocked = false;

        HasAim = false;
        HasAimOrigin = false;
        AimOriginPosition = Vector2.zero;
        AimDirection = Vector2.zero;

        HasEquippedWeapon = false;
    }


    public Vector2 ResolveAimDirectionTo(
        Vector2 worldTargetPosition)
    {
        if (!HasAim)
            return Vector2.zero;

        if (HasAimOrigin)
        {
            Vector2 direction =
                worldTargetPosition -
                AimOriginPosition;

            if (direction.sqrMagnitude > 0.0001f)
                return direction.normalized;
        }

        if (AimDirection.sqrMagnitude > 0.0001f)
            return AimDirection.normalized;

        return !HasMovement || FacingRight
            ? Vector2.right
            : Vector2.left;
    }


    public Vector2 ResolveAimOrigin(
        Vector2 fallbackPosition)
    {
        return HasAimOrigin
            ? AimOriginPosition
            : fallbackPosition;
    }
}
