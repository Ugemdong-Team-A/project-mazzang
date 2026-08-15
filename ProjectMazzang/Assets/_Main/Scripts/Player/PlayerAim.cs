using Fusion;
using UnityEngine;

[DefaultExecutionOrder(-90)]
public sealed class PlayerAim :
    PlayerModule,
    IPlayerAimState,
    IPlayerAimControl,
    IPlayerTickModule
{
    [Header("Aim")]
    [Tooltip(
        "정확한 조준 방향의 기준점입니다. " +
        "Rig/Bone 계층 밖의 상체 위치에 둡니다.")]
    [SerializeField]
    private Transform aimOrigin;

    [SerializeField]
    private Transform ccdTarget;

    [Tooltip(
        "상체 조준을 담당하는 CCD 등의 Behaviour입니다. " +
        "AnimationDriven 상태에서는 비활성화됩니다.")]
    [SerializeField]
    private UnityEngine.Behaviour upperBodyAimRig;

    [Min(0.01f)]
    [SerializeField]
    private float ccdTargetRadius = 3f;

    [Tooltip(
        "캐릭터 정면축과 CCD Effector 본 축 사이의 각도 차이입니다.")]
    [SerializeField]
    private float rigAngleOffset = 90f;


    [Header("Body Aim")]
    [Range(0f, 89f)]
    [SerializeField]
    private float maxBodyAimAngle = 80f;

    [Range(90f, 179f)]
    [SerializeField]
    private float facingFlipAngle = 100f;

    [Min(0f)]
    [SerializeField]
    private float bodyAimSpeed = 540f;


    private IPlayerMovementState
        _movementState;

    private IPlayerFacingControl
        _facingControl;


    // =========================================================
    // Network State
    // =========================================================

    [Networked]
    public Vector2 AimDirection
    {
        get;
        private set;
    }


    [Networked]
    public float BodyAimAngle
    {
        get;
        private set;
    }


    [Networked]
    public PlayerAimTrackingMode TrackingMode
    {
        get;
        private set;
    }


    [Networked]
    public PlayerAimFacingMode FacingMode
    {
        get;
        private set;
    }


    [Networked]
    public PlayerAimRigMode RigMode
    {
        get;
        private set;
    }


    [Networked]
    public PlayerAimCardinalDirection CardinalDirection
    {
        get;
        private set;
    }


    [Networked]
    private NetworkBool LockedFacingRight
    {
        get;
        set;
    }


    // =========================================================
    // State
    // =========================================================

    public bool IsAimOverridden =>
        TrackingMode !=
            PlayerAimTrackingMode.FollowInput ||
        FacingMode !=
            PlayerAimFacingMode.FollowAim ||
        RigMode !=
            PlayerAimRigMode.Procedural;


    private bool FacingRight =>
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

        Context.Register<
            IPlayerAimControl>(
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
    }


    // =========================================================
    // Fusion
    // =========================================================

    PlayerTickStage IPlayerTickModule.Stage =>
        PlayerTickStage.Aim;


    void IPlayerTickModule.Simulate(
        in PlayerTick tick)
    {
        TickAim();
    }


    public override void FixedUpdateNetwork()
    {
        if (IsTickControlled)
            return;

        TickAim();
    }


    internal void TickAim()
    {
        if (!IsContextReady)
            return;

        if (!GetInput(
                out PlayerInputData input))
        {
            return;
        }

        Vector2 inputAimDirection =
            ResolveDirectionTo(
                input.AimWorldPosition);

        UpdateAimDirection(
            inputAimDirection);

        UpdateFacing();

        UpdateBodyAim();
    }


    public override void Render()
    {
        UpdateRigPresentation();
    }


    // =========================================================
    // Aim Override
    // =========================================================

    public void ApplyOverride(
        in PlayerAimOverride aimOverride,
        Vector2 sourceAimDirection)
    {
        Vector2 sourceDirection =
            ResolveSourceDirection(
                sourceAimDirection);

        // 방향을 고정하기 전에
        // 해당 공격 방향에 맞게 Facing을 한 번 확정한다.
        if (aimOverride.FacingMode ==
            PlayerAimFacingMode.Locked)
        {
            TryUpdateFacingFromDirection(
                sourceDirection);

            LockedFacingRight =
                FacingRight;
        }

        TrackingMode =
            aimOverride.TrackingMode;

        FacingMode =
            aimOverride.FacingMode;

        RigMode =
            aimOverride.RigMode;

        switch (TrackingMode)
        {
            case PlayerAimTrackingMode.FollowInput:
                AimDirection =
                    sourceDirection;

                CardinalDirection =
                    PlayerAimCardinalDirection.None;
                break;


            case PlayerAimTrackingMode.LockedDirection:
                AimDirection =
                    sourceDirection;

                CardinalDirection =
                    PlayerAimCardinalDirection.None;
                break;


            case PlayerAimTrackingMode.LockedFourWay:
                AimDirection =
                    SnapFourWay(
                        sourceDirection,
                        out PlayerAimCardinalDirection cardinal);

                CardinalDirection =
                    cardinal;
                break;
        }
    }


    public void ClearOverride()
    {
        TrackingMode =
            PlayerAimTrackingMode.FollowInput;

        FacingMode =
            PlayerAimFacingMode.FollowAim;

        RigMode =
            PlayerAimRigMode.Procedural;

        CardinalDirection =
            PlayerAimCardinalDirection.None;
    }


    // =========================================================
    // Input Aim
    // =========================================================

    /// <summary>
    /// AimOrigin에서 월드 타겟까지의 정확한 조준 방향을 계산합니다.
    /// Input은 월드 좌표만 제공하고, 조준 기준점의 의미는 PlayerAim이 소유합니다.
    /// </summary>
    public Vector2 ResolveDirectionTo(
        Vector2 worldTargetPosition)
    {
        if (aimOrigin == null)
        {
            return ResolveSourceDirection(
                Vector2.zero);
        }

        Vector2 direction =
            worldTargetPosition -
            (Vector2)aimOrigin.position;

        Vector2 normalized =
            NormalizeDirection(
                direction);

        if (normalized.sqrMagnitude >
            0.0001f)
        {
            return normalized;
        }

        // 커서가 AimOrigin에 거의 겹친 경우에는
        // 방향이 순간적으로 0이 되지 않도록 기존 방향을 유지한다.
        return ResolveSourceDirection(
            Vector2.zero);
    }


    private void UpdateAimDirection(
        Vector2 inputDirection)
    {
        if (TrackingMode !=
            PlayerAimTrackingMode.FollowInput)
        {
            return;
        }

        if (inputDirection.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        AimDirection =
            inputDirection;

        CardinalDirection =
            PlayerAimCardinalDirection.None;
    }


    private Vector2 ResolveSourceDirection(
        Vector2 sourceDirection)
    {
        Vector2 normalized =
            NormalizeDirection(
                sourceDirection);

        if (normalized.sqrMagnitude >
            0.0001f)
        {
            return normalized;
        }

        normalized =
            NormalizeDirection(
                AimDirection);

        if (normalized.sqrMagnitude >
            0.0001f)
        {
            return normalized;
        }

        return FacingRight
            ? Vector2.right
            : Vector2.left;
    }


    private static Vector2 NormalizeDirection(
        Vector2 direction)
    {
        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return Vector2.zero;
        }

        return direction.normalized;
    }


    // =========================================================
    // Facing
    // =========================================================

    private void UpdateFacing()
    {
        if (_facingControl == null ||
            _movementState == null)
        {
            return;
        }

        if (_movementState.IsWallSliding)
            return;

        if (FacingMode ==
            PlayerAimFacingMode.Locked)
        {
            _facingControl.SetFacing(
                LockedFacingRight);

            return;
        }

        TryUpdateFacingFromDirection(
            AimDirection);
    }


    private void TryUpdateFacingFromDirection(
        Vector2 direction)
    {
        if (_facingControl == null ||
            _movementState == null)
        {
            return;
        }

        if (_movementState.IsWallSliding)
            return;

        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        Vector2 facingDirection =
            FacingRight
                ? Vector2.right
                : Vector2.left;

        float angleFromFacing =
            Vector2.Angle(
                facingDirection,
                direction);

        if (angleFromFacing <=
            facingFlipAngle)
        {
            return;
        }

        _facingControl.SetFacing(
            !FacingRight);
    }


    // =========================================================
    // Body Aim
    // =========================================================

    private void UpdateBodyAim()
    {
        if (AimDirection.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        float targetAngle =
            CalculateLocalAimAngle(
                AimDirection);

        targetAngle =
            Mathf.Clamp(
                targetAngle,
                -maxBodyAimAngle,
                maxBodyAimAngle);

        BodyAimAngle =
            Mathf.MoveTowardsAngle(
                BodyAimAngle,
                targetAngle,
                bodyAimSpeed *
                Runner.DeltaTime);
    }


    private float CalculateLocalAimAngle(
        Vector2 worldDirection)
    {
        Vector2 localDirection =
            FacingRight
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


    private Vector2 GetWorldBodyDirection(
        float localAngle)
    {
        float radians =
            localAngle *
            Mathf.Deg2Rad;

        Vector2 direction =
            new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians));

        if (!FacingRight)
        {
            direction.x *=
                -1f;
        }

        return direction.normalized;
    }


    // =========================================================
    // Four Way
    // =========================================================

    private static Vector2 SnapFourWay(
        Vector2 direction,
        out PlayerAimCardinalDirection cardinal)
    {
        direction =
            NormalizeDirection(
                direction);

        if (direction.sqrMagnitude <=
            0.0001f)
        {
            cardinal =
                PlayerAimCardinalDirection.Right;

            return Vector2.right;
        }

        if (Mathf.Abs(direction.x) >=
            Mathf.Abs(direction.y))
        {
            if (direction.x >= 0f)
            {
                cardinal =
                    PlayerAimCardinalDirection.Right;

                return Vector2.right;
            }

            cardinal =
                PlayerAimCardinalDirection.Left;

            return Vector2.left;
        }

        if (direction.y >= 0f)
        {
            cardinal =
                PlayerAimCardinalDirection.Up;

            return Vector2.up;
        }

        cardinal =
            PlayerAimCardinalDirection.Down;

        return Vector2.down;
    }


    // =========================================================
    // Rig
    // =========================================================

    private void UpdateRigPresentation()
    {
        bool useProceduralRig =
            RigMode ==
            PlayerAimRigMode.Procedural;

        if (!useProceduralRig)
        {
            if (upperBodyAimRig != null &&
                upperBodyAimRig.enabled)
            {
                upperBodyAimRig.enabled =
                    false;
            }

            return;
        }

        if (aimOrigin == null ||
            ccdTarget == null)
        {
            return;
        }

        ApplyCcdTarget();

        if (upperBodyAimRig != null &&
            !upperBodyAimRig.enabled)
        {
            upperBodyAimRig.enabled =
                true;
        }
    }


    private void ApplyCcdTarget()
    {
        Vector2 bodyDirection =
            GetWorldBodyDirection(
                BodyAimAngle);

        float signedRigOffset =
            FacingRight
                ? rigAngleOffset
                : -rigAngleOffset;

        Vector2 rigDirection =
            RotateDirection(
                bodyDirection,
                signedRigOffset);

        ccdTarget.position =
            aimOrigin.position +
            (Vector3)(
                rigDirection *
                ccdTargetRadius);
    }


    private static Vector2 RotateDirection(
        Vector2 direction,
        float angle)
    {
        float radians =
            angle *
            Mathf.Deg2Rad;

        float cos =
            Mathf.Cos(radians);

        float sin =
            Mathf.Sin(radians);

        return new Vector2(
            direction.x * cos -
            direction.y * sin,

            direction.x * sin +
            direction.y * cos);
    }


#if UNITY_EDITOR

    private void OnValidate()
    {
        float minimumFlipAngle =
            180f -
            maxBodyAimAngle;

        facingFlipAngle =
            Mathf.Max(
                facingFlipAngle,
                minimumFlipAngle);
    }


    private void OnDrawGizmosSelected()
    {
        if (aimOrigin == null)
            return;

        Vector3 origin =
            aimOrigin.position;

        if (Application.isPlaying &&
            AimDirection.sqrMagnitude >
            0.0001f)
        {
            Gizmos.color =
                Color.yellow;

            Gizmos.DrawLine(
                origin,
                origin +
                (Vector3)(
                    AimDirection.normalized *
                    ccdTargetRadius));
        }

        if (ccdTarget != null)
        {
            Gizmos.color =
                Color.cyan;

            Gizmos.DrawLine(
                origin,
                ccdTarget.position);

            Gizmos.DrawWireSphere(
                ccdTarget.position,
                0.07f);
        }

        Gizmos.color =
            Color.white;
    }

#endif
}