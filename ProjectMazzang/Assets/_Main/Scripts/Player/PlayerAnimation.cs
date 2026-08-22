using Fusion;
using UnityEngine;

public sealed class PlayerAnimation :
    PlayerTickModule
{
    [SerializeField]
    private Animator animator;

    private byte _lastJumpSequence;

    private byte _lastAttackSequence;

    private byte _lastDeathSequence;

    private byte _lastSkillAnimationSequence;

    public override PlayerTickStage Stage => PlayerTickStage.Finalize;


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        /*if (_movementState == null ||
            _combatState == null ||
            _healthState == null)
        {
            return;
        }

        _lastJumpSequence =
            _movementState.JumpSequence;

        _lastAttackSequence =
            _combatState.AttackSequence;

        _lastDeathSequence =
            _healthState.DeathSequence;

        if (_skillAnimationState != null)
        {
            _lastSkillAnimationSequence =
                _skillAnimationState.SkillAnimationSequence;
        }*/
    }


    public override void Present(in PlayerTickState tickState)
    {
        Vector2 velocity =
            tickState.MovementVelocity;

        animator.SetFloat(
            "Speed",
            Mathf.Abs(
                velocity.x));

        animator.SetFloat(
            "VerticalSpeed",
            velocity.y);

        animator.SetBool(
            "Grounded",
            tickState.IsGrounded);

        if (animator.GetBool(
                "WallSliding") !=
            tickState.IsWallSliding)
        {
            animator.SetBool(
                "WallSliding",
                tickState.IsWallSliding);
        }

        HandleJumpAnimation(tickState.JumpSequence, tickState.LastJumpType);
        HandleAttackAnimation(tickState.AttackSequence, tickState.AttackId);
        HandleSkillAnimation(tickState.SkillAnimationSequence, tickState.SkillAnimationId);
        HandleDeathAnimation(tickState.DeathSequence);
    }


    // =========================================================
    // Animation Events
    // =========================================================

    private void HandleJumpAnimation(byte jumpSequence, JumpType jumpType)
    {
        if (_lastJumpSequence == jumpSequence)
        {
            return;
        }

        _lastJumpSequence = jumpSequence;

        animator.SetInteger(
            "JumpType", (int)jumpType);

        animator.SetTrigger(
            "Jump");
    }


    private void HandleAttackAnimation(byte attackSequence, byte AttackId)
    {
        if (_lastAttackSequence == attackSequence)
        {
            return;
        }

        _lastAttackSequence = attackSequence;

        animator.SetInteger(
            "AttackId", AttackId);

        animator.SetTrigger(
            "Attack");
    }


    private void HandleSkillAnimation(byte skillAnimationSequence, PlayerSkillAnimationId lastSkillAnimation)
    {
        if (_lastSkillAnimationSequence == skillAnimationSequence)
        {
            return;
        }

        _lastSkillAnimationSequence = skillAnimationSequence;

        animator.SetInteger(
            "SkillId", (int)lastSkillAnimation);

        animator.SetTrigger(
            "Skill");
    }


    private void HandleDeathAnimation(byte deathSequence)
    {
        if (_lastDeathSequence == deathSequence)
        {
            return;
        }

        _lastDeathSequence = deathSequence;

        animator.SetTrigger(
            "Death");
    }

    public override void Simulate(in PlayerTick tick)
    {
        
    }
}
