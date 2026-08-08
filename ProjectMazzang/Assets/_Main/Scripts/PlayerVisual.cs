using Fusion;
using UnityEngine;

public sealed class PlayerVisual : NetworkBehaviour
{
    [SerializeField]
    private PlayerMovement movement;

    [SerializeField]
    private Transform visualRoot;

    private Vector3 defaultScale;

    private void Awake()
    {
        defaultScale = visualRoot.localScale;
    }

    public override void Render()
    {
        Vector3 scale = defaultScale;

        bool facingRight =
    movement.FacingRight;

        if (movement.IsWallSliding)
        {
            if (movement.IsTouchingWallLeft)
                facingRight = true;
            else if (movement.IsTouchingWallRight)
                facingRight = false;
        }

        scale.x *=
           facingRight
                ? 1f
                : -1f;

        visualRoot.localScale = scale;
    }
}