using Fusion;
using UnityEngine;

public sealed class PlayerAnimation :
    PlayerModule
{
    [SerializeField]
    private Animator animator;


    private IPlayerMovementState
        _movementState;

    private IPlayerCombatState
        _combatState;

    private IPlayerHealthState
        _healthState;


    private byte _lastJumpSequence;

    private byte _lastAttackSequence;

    private byte _lastDeathSequence;


    // =========================================================
    // Context
    // =========================================================

    protected override void OnContextReady()
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
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        if (_movementState == null ||
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
    }


    public override void Render()
    {
        if (_movementState == null ||
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
        }

        HandleJumpAnimation();
        HandleAttackAnimation();
        HandleDeathAnimation();
    }


    // =========================================================
    // Animation Events
    // =========================================================

    private void HandleJumpAnimation()
    {
        if (_lastJumpSequence ==
            _movementState.JumpSequence)
        {
            return;
        }

        _lastJumpSequence =
            _movementState.JumpSequence;

        animator.SetInteger(
            "JumpType",
            (int)_movementState
                .LastJumpType);

        animator.SetTrigger(
            "Jump");
    }


    private void HandleAttackAnimation()
    {
        if (_lastAttackSequence ==
            _combatState.AttackSequence)
        {
            return;
        }

        _lastAttackSequence =
            _combatState.AttackSequence;

        animator.SetTrigger(
            "Attack");
    }


    private void HandleDeathAnimation()
    {
        if (_lastDeathSequence ==
            _healthState.DeathSequence)
        {
            return;
        }

        _lastDeathSequence =
            _healthState.DeathSequence;

        animator.SetTrigger(
            "Death");
    }
}
