using UnityEngine;

public sealed class PlayerAnimation :
    PlayerTickModule
{
    [SerializeField]
    private Animator animator;

    private byte _lastJumpSequence;

    private bool _jumpPresentationInitialized;

    private byte _lastAttackSequence;

    private bool _attackPresentationInitialized;

    private byte _lastDeathSequence;

    private bool _deathPresentationInitialized;

    private byte _lastSkillAnimationSequence;

    private bool _skillPresentationInitialized;

    public override PlayerTickStage Stage => PlayerTickStage.Finalize;


    public override void Spawned()
    {
        _jumpPresentationInitialized = false;
        _attackPresentationInitialized = false;
        _deathPresentationInitialized = false;
        _skillPresentationInitialized = false;
    }


    public override void Present(in PlayerTickState tickState)
    {
        if (tickState.HasMovement)
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

            HandleJumpAnimation(
                tickState.JumpSequence,
                tickState.LastJumpType);
        }

        if (tickState.HasCombat)
        {
            HandleAttackAnimation(
                tickState.AttackSequence,
                tickState.AttackId);
        }

        if (tickState.HasSkill)
        {
            HandleSkillAnimation(
                tickState.SkillAnimationSequence,
                tickState.SkillAnimationId);
        }

        if (tickState.HasHealth)
        {
            HandleDeathAnimation(
                tickState.DeathSequence);
        }
    }


    // =========================================================
    // Animation Events
    // =========================================================

    private void HandleJumpAnimation(byte jumpSequence, JumpType jumpType)
    {
        if (!HasSequenceChanged(
                ref _lastJumpSequence,
                ref _jumpPresentationInitialized,
                jumpSequence))
        {
            return;
        }

        animator.SetInteger(
            "JumpType", (int)jumpType);

        animator.SetTrigger(
            "Jump");
    }


    private void HandleAttackAnimation(
        byte attackSequence,
        byte attackId)
    {
        if (!HasSequenceChanged(
                ref _lastAttackSequence,
                ref _attackPresentationInitialized,
                attackSequence))
        {
            return;
        }

        animator.SetInteger(
            "AttackId",
            attackId);

        animator.SetTrigger(
            "Attack");
    }


    private void HandleSkillAnimation(
        byte skillAnimationSequence,
        PlayerSkillAnimationId skillAnimationId)
    {
        if (!HasSequenceChanged(
                ref _lastSkillAnimationSequence,
                ref _skillPresentationInitialized,
                skillAnimationSequence))
        {
            return;
        }

        animator.SetInteger(
            "SkillId",
            (int)skillAnimationId);

        animator.SetTrigger(
            "Skill");
    }


    private void HandleDeathAnimation(byte deathSequence)
    {
        if (!HasSequenceChanged(
                ref _lastDeathSequence,
                ref _deathPresentationInitialized,
                deathSequence))
        {
            return;
        }

        animator.SetTrigger(
            "Death");
    }


    private static bool HasSequenceChanged(
        ref byte previousSequence,
        ref bool initialized,
        byte currentSequence)
    {
        if (!initialized)
        {
            initialized = true;
            previousSequence = currentSequence;

            return false;
        }

        if (previousSequence == currentSequence)
            return false;

        previousSequence = currentSequence;

        return true;
    }

    public override void Simulate(in PlayerTick tick)
    {
        
    }
}
