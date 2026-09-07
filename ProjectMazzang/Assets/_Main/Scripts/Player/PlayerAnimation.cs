using UnityEngine;

public sealed class PlayerAnimation :
    PlayerTickModule
{
    private const string ActionCastPlaceholder =
        "ActionCastPlaceholder";

    private const string ActionReleasePlaceholder =
        "ActionReleasePlaceholder";

    private const string ActionRecoveryPlaceholder =
        "ActionRecoveryPlaceholder";

    private const string FullBodyLayer =
        "Action_FullBody";

    private const string UpperBodyLayer =
        "Action_UpperBody";

    private const string ArmsOnlyLayer =
        "Action_ArmsOnly";

    [SerializeField]
    private Animator animator;

    private AnimatorOverrideController
        _actionOverrideController;

    private byte _lastJumpSequence;

    private bool _jumpPresentationInitialized;

    private byte _lastAttackSequence;

    private bool _attackPresentationInitialized;

    private byte _lastAttackAnimationSequence;

    private bool _attackAnimationPresentationInitialized;

    private byte _lastDeathSequence;

    private bool _deathPresentationInitialized;

    private byte _lastSkillAnimationSequence;

    private bool _skillPresentationInitialized;

    private byte _lastWeaponAnimationSequence;

    private bool _weaponAnimationPresentationInitialized;

    public override PlayerTickStage Stage => PlayerTickStage.Finalize;


    public override void Spawned()
    {
        InitializeActionOverrideController();

        _jumpPresentationInitialized = false;
        _attackPresentationInitialized = false;
        _attackAnimationPresentationInitialized = false;
        _deathPresentationInitialized = false;
        _skillPresentationInitialized = false;
        _weaponAnimationPresentationInitialized = false;
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

        HandleActionAnimation(
            ref _lastWeaponAnimationSequence,
            ref _weaponAnimationPresentationInitialized,
            tickState.WeaponAnimationSequence,
            ActionAnimationPhase.Release,
            tickState.WeaponAnimation);

        if (tickState.HasCombat)
        {
            HandleActionAnimation(
                ref _lastAttackAnimationSequence,
                ref _attackAnimationPresentationInitialized,
                tickState.AttackAnimationSequence,
                tickState.AttackAnimationPhase,
                tickState.AttackAnimation);

            if (tickState.AttackAnimation == null)
            {
                HandleAttackAnimation(
                    tickState.AttackSequence,
                    tickState.AttackId);
            }
        }

        if (tickState.HasSkill)
        {
            HandleActionAnimation(
                ref _lastSkillAnimationSequence,
                ref _skillPresentationInitialized,
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


    private void HandleActionAnimation(
        ref byte previousSequence,
        ref bool initialized,
        byte skillAnimationSequence,
        ActionAnimationPhase phase,
        ActionAnimationData animation)
    {
        if (!HasSequenceChanged(
                ref previousSequence,
                ref initialized,
                skillAnimationSequence))
        {
            return;
        }

        ActionAnimationClipData clipData =
            animation != null
                ? animation.GetClipData(phase)
                : default;

        if (!clipData.HasClip ||
            !TryApplyActionOverride(
                phase,
                clipData))
        {
            return;
        }

        animator.SetInteger(
            "SkillPhase",
            (int)phase);

        animator.SetTrigger(
            "Skill");
    }


    private void InitializeActionOverrideController()
    {
        if (_actionOverrideController != null ||
            animator == null ||
            animator.runtimeAnimatorController == null)
        {
            return;
        }

        _actionOverrideController =
            new AnimatorOverrideController(
                animator.runtimeAnimatorController)
            {
                name =
                    $"{animator.runtimeAnimatorController.name} " +
                    "(Player Action Instance)"
            };

        animator.runtimeAnimatorController =
            _actionOverrideController;
    }


    private bool TryApplyActionOverride(
        ActionAnimationPhase phase,
        ActionAnimationClipData clipData)
    {
        if (_actionOverrideController == null)
            return false;

        string placeholder =
            phase switch
            {
                ActionAnimationPhase.Cast =>
                    ActionCastPlaceholder,
                ActionAnimationPhase.Release =>
                    ActionReleasePlaceholder,
                ActionAnimationPhase.Recovery =>
                    ActionRecoveryPlaceholder,
                _ =>
                    null
            };

        if (placeholder == null)
            return false;

        SelectActionLayer(
            clipData.BodyMask);

        _actionOverrideController[placeholder] =
            clipData.Clip;

        return _actionOverrideController[placeholder] ==
               clipData.Clip;
    }


    private void SelectActionLayer(
        ActionBodyMask bodyMask)
    {
        SetLayerWeight(
            FullBodyLayer,
            bodyMask == ActionBodyMask.FullBody);

        SetLayerWeight(
            UpperBodyLayer,
            bodyMask == ActionBodyMask.UpperBody);

        SetLayerWeight(
            ArmsOnlyLayer,
            bodyMask == ActionBodyMask.ArmsOnly);
    }


    private void SetLayerWeight(
        string layerName,
        bool active)
    {
        int layerIndex =
            animator.GetLayerIndex(
                layerName);

        if (layerIndex < 0)
            return;

        animator.SetLayerWeight(
            layerIndex,
            active ? 1f : 0f);
    }


    private void OnDestroy()
    {
        if (_actionOverrideController != null)
        {
            Destroy(
                _actionOverrideController);
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
