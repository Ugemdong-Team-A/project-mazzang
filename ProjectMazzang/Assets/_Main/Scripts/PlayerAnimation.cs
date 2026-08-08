using Fusion;
using UnityEngine;

public sealed class PlayerAnimation : NetworkBehaviour
{
    [SerializeField]
    private PlayerMovement movement;

    [SerializeField]
    private Animator animator;

    public override void Render()
    {
        Vector2 velocity = movement.Velocity;

        animator.SetFloat(
            "Speed",
            Mathf.Abs(velocity.x));

        animator.SetFloat(
            "VerticalSpeed",
            velocity.y);

        animator.SetBool(
            "Grounded",
            movement.IsGrounded);
    }
}