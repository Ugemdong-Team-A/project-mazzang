using UnityEngine;

public sealed class PlayerVisual :
    PlayerModule
{
    [SerializeField]
    private GameObject characterVisualRoot;

    private IPlayerMovementState
        _movementState;

    private IPlayerHealthState
        _healthState;

    private Vector3 _defaultScale;


    private void Awake()
    {
        if (characterVisualRoot != null)
        {
            _defaultScale =
                characterVisualRoot
                    .transform
                    .localScale;
        }
    }


    protected override void OnContextReady()
    {
        _movementState =
            Context.Get<
                IPlayerMovementState>();

        _healthState =
            Context.Get<
                IPlayerHealthState>();
    }


    public override void Render()
    {
        if (characterVisualRoot == null)
            return;

        UpdateVisibility();
        UpdateFacing();
    }


    private void UpdateVisibility()
    {
        if (_healthState == null)
            return;

        bool visible =
            !_healthState.IsDead;

        if (characterVisualRoot.activeSelf ==
            visible)
        {
            return;
        }

        characterVisualRoot.SetActive(
            visible);
    }


    private void UpdateFacing()
    {
        if (_movementState == null)
            return;

        Vector3 scale =
            _defaultScale;

        scale.x *=
            _movementState.FacingRight
                ? 1f
                : -1f;

        characterVisualRoot
            .transform
            .localScale =
            scale;
    }
}