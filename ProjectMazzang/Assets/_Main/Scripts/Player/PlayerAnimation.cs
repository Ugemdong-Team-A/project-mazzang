using UnityEngine;

public sealed class PlayerAnimation :
    PlayerTickModule
{
    private const string SkillCastPlaceholder =
        "SkillCastPlaceholder";

    private const string SkillReleasePlaceholder =
        "SkillReleasePlaceholder";

    private const string SkillRecoveryPlaceholder =
        "SkillRecoveryPlaceholder";

    [SerializeField]
    private Animator animator;

    private AnimatorOverrideController
        _skillOverrideController;

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
        InitializeSkillOverrideController();

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
                "MoveDirection",
                ResolveMoveDirection(
                    velocity.x,
                    tickState.FacingRight));

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
                tickState.SkillAnimationPhase,
                tickState.SkillAnimation);
        }

        if (tickState.HasHealth)
        {
            HandleDeathAnimation(
                tickState.DeathSequence);
        }
    }


    private static float ResolveMoveDirection(
        float horizontalVelocity,
        bool facingRight)
    {
        if (Mathf.Approximately(
                horizontalVelocity,
                0f))
        {
            return 0f;
        }

        float movementSign =
            Mathf.Sign(
                horizontalVelocity);

        float facingSign =
            facingRight
                ? 1f
                : -1f;

        return movementSign *
               facingSign;
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
        SkillAnimationPhase phase,
        SkillAnimationData animation)
    {
        if (!HasSequenceChanged(
                ref _lastSkillAnimationSequence,
                ref _skillPresentationInitialized,
                skillAnimationSequence))
        {
            return;
        }

        AnimationClip clip =
            animation?.GetClip(phase);

        if (clip == null ||
            !TryApplySkillOverride(
                phase,
                clip))
        {
            return;
        }

        animator.SetInteger(
            "SkillPhase",
            (int)phase);

        animator.SetTrigger(
            "Skill");
    }


    private void InitializeSkillOverrideController()
    {
        if (_skillOverrideController != null ||
            animator == null ||
            animator.runtimeAnimatorController == null)
        {
            return;
        }

        _skillOverrideController =
            new AnimatorOverrideController(
                animator.runtimeAnimatorController)
            {
                name =
                    $"{animator.runtimeAnimatorController.name} " +
                    "(Player Skill Instance)"
            };

        animator.runtimeAnimatorController =
            _skillOverrideController;
    }


    private bool TryApplySkillOverride(
        SkillAnimationPhase phase,
        AnimationClip clip)
    {
        if (_skillOverrideController == null)
            return false;

        string placeholder =
            phase switch
            {
                SkillAnimationPhase.Cast =>
                    SkillCastPlaceholder,
                SkillAnimationPhase.Release =>
                    SkillReleasePlaceholder,
                SkillAnimationPhase.Recovery =>
                    SkillRecoveryPlaceholder,
                _ =>
                    null
            };

        if (placeholder == null)
            return false;

        _skillOverrideController[placeholder] =
            clip;

        return _skillOverrideController[placeholder] ==
               clip;
    }


    private void OnDestroy()
    {
        if (_skillOverrideController != null)
        {
            Destroy(
                _skillOverrideController);
        }
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
