using UnityEngine;

public sealed class PlayerAnimation :
    PlayerTickModule
{
    private const float ActionPoseBlendDuration = 0.1f;

    private const string BaseIdlePlaceholder =
        "Idle";

    private const string ActionCastPlaceholder =
        "ActionCastPlaceholder";

    private const string ActionMainPlaceholder =
        "ActionMainPlaceholder";

    private const string ActionRecoveryPlaceholder =
        "ActionRecoveryPlaceholder";

    private const string FullBodyLayer =
        "Action_FullBody";

    private const string UpperBodyLayer =
        "Action_UpperBody";

    private const string ArmsOnlyLayer =
        "Action_ArmsOnly";

    private static readonly string[] ActionLayerNames =
    {
        FullBodyLayer,
        UpperBodyLayer,
        ArmsOnlyLayer
    };

    [SerializeField]
    private Animator animator;

    private AnimatorOverrideController
        _actionOverrideController;

    private AnimationClip _defaultIdleAnimation;

    private AnimationClip _appliedStanceAnimation;

    private readonly int[] _actionLayerIndices =
        { -1, -1, -1 };

    private readonly float[] _actionLayerTargets =
        new float[ActionLayerNames.Length];

    private bool _isBlendingActionLayers;

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
        InitializeActionLayers();

        _jumpPresentationInitialized = false;
        _attackPresentationInitialized = false;
        _attackAnimationPresentationInitialized = false;
        _deathPresentationInitialized = false;
        _skillPresentationInitialized = false;
        _weaponAnimationPresentationInitialized = false;
        _appliedStanceAnimation = null;
    }


    public override void Present(in PlayerTickState tickState)
    {
        HandleWeaponStance(
            tickState.HasEquippedWeapon
                ? tickState.WeaponStanceAnimation
                : null);

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
            tickState.IsWeaponAnimationActive
                ? ActionAnimationPhase.Main
                : ActionAnimationPhase.None,
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

        UpdateActionLayerWeights();
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

        if (phase == ActionAnimationPhase.None)
        {
            ClearActionLayers();
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

        BlendToActionPhase(phase);
    }


    private void ClearActionLayers()
    {
        animator.SetInteger(
            "SkillPhase",
            (int)ActionAnimationPhase.None);

        for (int index = 0;
             index < _actionLayerTargets.Length;
             index++)
        {
            _actionLayerTargets[index] = 0f;
        }

        _isBlendingActionLayers = true;
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

        _defaultIdleAnimation =
            _actionOverrideController[
                BaseIdlePlaceholder];
    }


    private void HandleWeaponStance(
        AnimationClip stanceAnimation)
    {
        if (_actionOverrideController == null)
            return;

        AnimationClip nextAnimation =
            stanceAnimation != null
                ? stanceAnimation
                : _defaultIdleAnimation;

        if (_appliedStanceAnimation ==
            nextAnimation)
        {
            return;
        }

        _actionOverrideController[
            BaseIdlePlaceholder] =
            nextAnimation;

        _appliedStanceAnimation =
            nextAnimation;
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
                ActionAnimationPhase.Main =>
                    ActionMainPlaceholder,
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
        SetActionLayerTarget(
            0,
            bodyMask == ActionBodyMask.FullBody);

        SetActionLayerTarget(
            1,
            bodyMask == ActionBodyMask.UpperBody);

        SetActionLayerTarget(
            2,
            bodyMask == ActionBodyMask.ArmsOnly);

        _isBlendingActionLayers = true;
    }


    private void SetActionLayerTarget(
        int actionLayer,
        bool active)
    {
        _actionLayerTargets[actionLayer] =
            active ? 1f : 0f;
    }


    private void InitializeActionLayers()
    {
        for (int index = 0;
             index < ActionLayerNames.Length;
             index++)
        {
            int layerIndex =
                animator.GetLayerIndex(
                    ActionLayerNames[index]);

            _actionLayerIndices[index] =
                layerIndex;

            _actionLayerTargets[index] =
                layerIndex >= 0
                    ? animator.GetLayerWeight(
                        layerIndex)
                    : 0f;
        }

        _isBlendingActionLayers = false;
    }


    private void BlendToActionPhase(
        ActionAnimationPhase phase)
    {
        string stateName =
            phase switch
            {
                ActionAnimationPhase.Cast =>
                    "Cast",
                ActionAnimationPhase.Main =>
                    "Main",
                ActionAnimationPhase.Recovery =>
                    "Recovery",
                _ =>
                    null
            };

        if (stateName == null)
            return;

        int sourceLayerIndex =
            _actionLayerIndices[0];

        if (sourceLayerIndex < 0)
        {
            animator.SetTrigger(
                "Skill");
            return;
        }

        int stateHash =
            Animator.StringToHash(
                FullBodyLayer +
                "." +
                stateName);

        if (!animator.HasState(
                sourceLayerIndex,
                stateHash))
        {
            animator.SetTrigger(
                "Skill");
            return;
        }

        // UpperBody와 ArmsOnly는 FullBody에 동기화된 레이어라
        // 원본 상태만 전환하면 같은 블렌드 시간을 공유한다.
        animator.CrossFadeInFixedTime(
            stateHash,
            ActionPoseBlendDuration,
            sourceLayerIndex,
            0f);
    }


    private void UpdateActionLayerWeights()
    {
        if (!_isBlendingActionLayers)
            return;

        float step =
            ActionPoseBlendDuration <= 0f
                ? 1f
                : Time.deltaTime /
                  ActionPoseBlendDuration;

        bool finished = true;

        for (int index = 0;
             index < _actionLayerIndices.Length;
             index++)
        {
            int layerIndex =
                _actionLayerIndices[index];

            if (layerIndex < 0)
                continue;

            float targetWeight =
                _actionLayerTargets[index];

            float nextWeight =
                Mathf.MoveTowards(
                    animator.GetLayerWeight(
                        layerIndex),
                    targetWeight,
                    step);

            animator.SetLayerWeight(
                layerIndex,
                nextWeight);

            finished &= Mathf.Approximately(
                nextWeight,
                targetWeight);
        }

        _isBlendingActionLayers = !finished;
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
