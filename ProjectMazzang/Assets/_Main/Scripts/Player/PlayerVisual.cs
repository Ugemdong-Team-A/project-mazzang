using UnityEngine;

public sealed class PlayerVisual :
    PlayerModule
{
    [SerializeField]
    private Transform visualRoot;


    private IPlayerMovementState
        _movementState;

    private Vector3 _defaultScale;


    private void Awake()
    {
        if (visualRoot != null)
        {
            _defaultScale =
                visualRoot.localScale;
        }
    }


    protected override void OnContextReady()
    {
        _movementState =
            Context.Get<
                IPlayerMovementState>();
    }


    public override void Render()
    {
        if (_movementState == null ||
            visualRoot == null)
        {
            return;
        }

        Vector3 scale =
            _defaultScale;

        scale.x *=
            _movementState.FacingRight
                ? 1f
                : -1f;

        visualRoot.localScale =
            scale;
    }
}
