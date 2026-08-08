using Fusion;
using UnityEngine;

public sealed class PlayerAnimation : NetworkBehaviour
{
    [SerializeField]
    private PlayerMovement movement;

    [SerializeField]
    private Animator animator;

    private byte lastJumpSequence;


    public override void Spawned()
    {
        lastJumpSequence =
            movement.JumpSequence;
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
}