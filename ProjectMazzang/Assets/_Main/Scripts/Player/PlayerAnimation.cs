using Fusion;
using UnityEngine;

public sealed class PlayerAnimation :
    PlayerModule
{
    [SerializeField]
    private Animator animator;

    private byte _lastJumpSequence;

    private byte _lastAttackSequence;

    private byte _lastDeathSequence;

    private byte _lastSkillAnimationSequence;


    // =========================================================
    // Context
    // =========================================================

    /*protected override void OnContextReady()
    {
        _movementState =
            Context.Get<
                IPlayerMovementState>();

        _combatState =
            Context.Get<
                IPlayerCombatState>();

        _healthState =
            Context.Get<
                IPlayerHealthState>();

        _skillAnimationState =
            Context.Get<
                IPlayerSkillAnimationState>();
    }*/


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


    public override void Render()
    {
        /*if (_movementState == null ||
            _combatState == null ||
            _healthState == null)
        {
            return;
        }

        Vector2 velocity =
            _movementState.Velocity;

        animator.SetFloat(
            "Speed",
            Mathf.Abs(
                velocity.x));

        animator.SetFloat(
            "VerticalSpeed",
            velocity.y);

        animator.SetBool(
            "Grounded",
            _movementState.IsGrounded);

        if (animator.GetBool(
                "WallSliding") !=
            _movementState.IsWallSliding)
        {
            animator.SetBool(
                "WallSliding",
                _movementState.IsWallSliding);
        }*/

        HandleJumpAnimation();
        HandleAttackAnimation();
        HandleSkillAnimation();
        HandleDeathAnimation();
    }


    // =========================================================
    // Animation Events
    // =========================================================

    private void HandleJumpAnimation()
    {
        /*if (_lastJumpSequence ==
            _movementState.JumpSequence)
        {
            return;
        }

        _lastJumpSequence =
            _movementState.JumpSequence;

        animator.SetInteger(
            "JumpType",
            (int)_movementState
                .LastJumpType);*/

        animator.SetTrigger(
            "Jump");
    }


    private void HandleAttackAnimation()
    {
        /*if (_lastAttackSequence ==
            _combatState.AttackSequence)
        {
            return;
        }

        _lastAttackSequence =
            _combatState.AttackSequence;

        animator.SetInteger(
            "AttackId",
            _combatState.CurrentAttackId);*/

        animator.SetTrigger(
            "Attack");
    }


    private void HandleSkillAnimation()
    {
        /*if (_skillAnimationState == null ||
            _lastSkillAnimationSequence ==
            _skillAnimationState.SkillAnimationSequence)
        {
            return;
        }

        _lastSkillAnimationSequence =
            _skillAnimationState.SkillAnimationSequence;

        animator.SetInteger(
            "SkillId",
            (int)_skillAnimationState.LastSkillAnimation);

        animator.SetTrigger(
            "Skill");*/
    }


    private void HandleDeathAnimation()
    {
        /*if (_lastDeathSequence ==
            _healthState.DeathSequence)
        {
            return;
        }

        _lastDeathSequence =
            _healthState.DeathSequence;*/

        animator.SetTrigger(
            "Death");
    }
}
