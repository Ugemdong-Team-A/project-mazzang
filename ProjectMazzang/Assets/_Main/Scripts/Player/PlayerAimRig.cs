using Fusion;
using UnityEngine;

[DefaultExecutionOrder(-90)]
public sealed class PlayerAim :
    PlayerModule,
    IPlayerAimState
{
    [Header("Rig")]
    [SerializeField]
    private Transform aimPivot;

    [SerializeField]
    private Transform ccdTarget;

    [Min(0.01f)]
    [SerializeField]
    private float targetRadius = 3f;

    [Tooltip(
        "캐릭터의 실제 정면과 CCD Effector 본 방향의 각도 차이입니다.")]
    [SerializeField]
    private float rigAngleOffset = 90f;


    [Header("Aim")]
    [Range(0f, 89f)]
    [SerializeField]
    private float maxAimAngle = 80f;

    [Tooltip(
        "현재 Facing 기준으로 이 각도보다 뒤를 보면 Facing을 반전합니다.")]
    [Range(90f, 179f)]
    [SerializeField]
    private float facingFlipAngle = 100f;

    [Min(0f)]
    [SerializeField]
    private float aimSpeed = 540f;


    private IPlayerMovementState _movementState;

    private IPlayerFacingControl _facingControl;

    private IPlayerHealthState _healthState;


    // =========================================================
    // Network State
    // =========================================================

    /// <summary>
    /// 현재 Facing 기준 실제 상체 조준 각도입니다.
    ///
    /// 0도는 캐릭터 정면이며,
    /// 양수는 위쪽, 음수는 아래쪽입니다.
    /// </summary>
    [Networked]
    public float AimAngle
    {
        get;
        private set;
    }


    // =========================================================
    // Public State
    // =========================================================

    public Vector2 AimDirection =>
        GetWorldAimDirection(
            AimAngle);

    public bool FacingRight =>
        _movementState == null ||
        _movementState.FacingRight;


    // =========================================================
    // Context
    // =========================================================

    protected override void RegisterContextUnits()
    {
        Context.Register<
            IPlayerAimState>(
            this);
    }


    protected override void OnContextReady()
    {
        _movementState =
            Context.Get<
                IPlayerMovementState>();

        _facingControl =
            Context.Get<
                IPlayerFacingControl>();

        _healthState =
            Context.Get<
                IPlayerHealthState>();
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void FixedUpdateNetwork()
    {
        if (!IsContextReady)
            return;

        if (_healthState == null ||
            !_healthState.IsAlive)
        {
            return;
        }

        if (!GetInput(
                out PlayerInputData input))
        {
            return;
        }

        Vector2 desiredDirection =
            input.AimDirection;

        if (desiredDirection.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        desiredDirection.Normalize();

        UpdateFacing(
            desiredDirection);

        float targetAngle =
            CalculateLocalAimAngle(
                desiredDirection);

        targetAngle =
            Mathf.Clamp(
                targetAngle,
                -maxAimAngle,
                maxAimAngle);

        AimAngle =
            Mathf.MoveTowards(
                AimAngle,
                targetAngle,
                aimSpeed *
                Runner.DeltaTime);
    }


    public override void Render()
    {
        if (aimPivot == null ||
            ccdTarget == null)
        {
            return;
        }

        ApplyRigTarget();
    }


    // =========================================================
    // Facing
    // =========================================================

    private void UpdateFacing(
        Vector2 desiredDirection)
    {
        if (_movementState == null ||
            _facingControl == null)
        {
            return;
        }

        Vector2 facingDirection =
            _movementState.FacingRight
                ? Vector2.right
                : Vector2.left;

        float angleFromFacing =
            Vector2.Angle(
                facingDirection,
                desiredDirection);

        if (angleFromFacing <=
            facingFlipAngle)
        {
            return;
        }

        _facingControl.SetFacing(
            !_movementState.FacingRight);
    }


    // =========================================================
    // Aim
    // =========================================================

    private float CalculateLocalAimAngle(
        Vector2 worldDirection)
    {
        bool facingRight =
            _movementState == null ||
            _movementState.FacingRight;

        // 왼쪽을 볼 때는 World X를 뒤집어
        // 항상 캐릭터의 로컬 정면을 +X로 취급한다.
        Vector2 localDirection =
            facingRight
                ? worldDirection
                : new Vector2(
                    -worldDirection.x,
                    worldDirection.y);

        return
            Mathf.Atan2(
                localDirection.y,
                localDirection.x) *
            Mathf.Rad2Deg;
    }


    private Vector2 GetWorldAimDirection(
        float localAngle)
    {
        float radians =
            localAngle *
            Mathf.Deg2Rad;

        Vector2 localDirection =
            new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians));

        if (_movementState != null &&
            !_movementState.FacingRight)
        {
            localDirection.x *=
                -1f;
        }

        return localDirection.normalized;
    }


    // =========================================================
    // Presentation
    // =========================================================

    private void ApplyRigTarget()
    {
        float targetAngle =
            AimAngle +
            rigAngleOffset;

        float radians =
            targetAngle *
            Mathf.Deg2Rad;

        Vector3 localPosition =
            new Vector3(
                Mathf.Cos(radians),
                Mathf.Sin(radians),
                0f) *
            targetRadius;

        ccdTarget.position =
            aimPivot.TransformPoint(
                localPosition);
    }


#if UNITY_EDITOR

    private void OnValidate()
    {
        // Facing을 뒤집은 직후에도 Aim 제한 안쪽에
        // 들어올 수 있도록 최소 Flip 각도를 보장한다.
        float minimumFlipAngle =
            180f -
            maxAimAngle;

        facingFlipAngle =
            Mathf.Max(
                facingFlipAngle,
                minimumFlipAngle);
    }

#endif
}