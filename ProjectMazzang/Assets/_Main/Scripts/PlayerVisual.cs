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

        scale.x *=
            movement.FacingRight
                ? 1f
                : -1f;

        visualRoot.localScale = scale;
    }
}