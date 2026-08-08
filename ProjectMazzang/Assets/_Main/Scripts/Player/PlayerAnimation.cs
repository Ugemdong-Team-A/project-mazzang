using Fusion;
using UnityEngine;

public sealed class PlayerAnimation : NetworkBehaviour
{
    [SerializeField]
    private PlayerMovement movement;
    [SerializeField]
    private PlayerCombat combat;
    [SerializeField]
    private PlayerHealth health;

    [SerializeField]
    private Animator animator;

    private byte lastJumpSequence;
    private byte lastAttackSequence;
    private byte lastDeathSequence;

    public override void Spawned()
    {
        lastJumpSequence =
            movement.JumpSequence;

        lastDeathSequence =
        health.DeathSequence;
    }


    public override void Render()
    {
        Vector2 velocity =
            movement.Velocity;

        animator.SetFloat(
            "Speed",
            Mathf.Abs(velocity.x));

        animator.SetFloat(
            "VerticalSpeed",
            velocity.y);

        animator.SetBool(
            "Grounded",
            movement.IsGrounded);

        if (animator.GetBool("WallSliding") == !movement.IsWallSliding)
            animator.SetBool(
                "WallSliding",
                movement.IsWallSliding);

        HandleJumpAnimation();

        HandleAttackAnimation();

        HandleDeathAnimation();
    }


    private void HandleJumpAnimation()
    {
        if (lastJumpSequence ==
            movement.JumpSequence)
        {
            return;
        }

        lastJumpSequence =
            movement.JumpSequence;

        animator.SetInteger(
            "JumpType",
            (int)movement.LastJumpType);

        animator.SetTrigger(
            "Jump");
    }

    private void HandleAttackAnimation()
    {
        if (lastAttackSequence ==
            combat.AttackSequence)
        {
            return;
        }

        lastAttackSequence =
            combat.AttackSequence;

        animator.SetTrigger("Attack");
    }

    private void HandleDeathAnimation()
    {
        if (lastDeathSequence ==
            health.DeathSequence)
        {
            return;
        }

        lastDeathSequence =
            health.DeathSequence;

        animator.SetTrigger("Death");
    }
}