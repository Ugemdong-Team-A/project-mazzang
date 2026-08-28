using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.IK;

[DefaultExecutionOrder(-90)]
public sealed class PlayerAim :
    PlayerTickModule,
    IPlayerTickCommandSink,
    IPlayerTickStateSource
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
        "ResolvedAimPivot이 위치와 회전을 상속할 기준 척추 본입니다.")]
    [SerializeField]
    private Transform resolvedAimReferenceBone;

    [Tooltip(
        "기준 척추 본의 로컬 원점에서 최종 상체 자세를 따라가는 피벗입니다.")]
    [SerializeField]
    private Transform resolvedAimPivot;

    [Tooltip(
        "상체 조준을 담당하는 CCD입니다. " +
        "ProceduralAim은 이 CCD를 사용하고, " +
        "AnimationWithBodyAim은 같은 Effector를 방향 기준으로 사용합니다.")]
    [SerializeField]
    private CCDSolver2D upperBodyAimRig;

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

    [Tooltip(
        "Smooths the networked body aim angle between render frames.")]
    [Min(0f)]
    [SerializeField]
    private float bodyAimPresentationSharpness = 24f;
    
    private float _presentedBodyAimAngle;
    private bool _hasPresentedBodyAimAngle;
    private bool _presentedFacingRight;

    private bool _applyAnimationBodyAim;
    private bool _hasAppliedAnimationBodyAim;
    private Quaternion _animationBodyAimBaseRotation;

    private readonly List<AnimatedIkTargetPose>
        _animatedIkTargetPoses = new();

    private readonly struct AnimatedIkTargetPose
    {
        public readonly Transform Target;
        public readonly Vector3 LocalPosition;
        public readonly Quaternion LocalRotation;

        public AnimatedIkTargetPose(
            Transform target)
        {
            Target = target;
            LocalPosition = target.localPosition;
            LocalRotation = target.localRotation;
        }
    }

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

    public Transform ResolvedAimPivot =>
        resolvedAimPivot;


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        ResolveStandardRigReferences();
    }


    public override PlayerTickStage Stage =>
        PlayerTickStage.Aim;


    public override void Simulate(
        in PlayerTick tick)
    {
        TickAim(tick);
    }


    void IPlayerTickStateSource.CaptureTickState(
        PlayerTickState state)
    {
        state.HasAim = true;
        state.HasAimOrigin = aimOrigin != null;
        state.AimOriginPosition =
            aimOrigin != null
                ? (Vector2)aimOrigin.position
                : Vector2.zero;
        state.AimDirection = AimDirection;
    }


    bool IPlayerTickCommandSink.ResolveTickCommands(
        PlayerTickCommands commands,
        PlayerTickState state)
    {
        if (!commands.TryConsumeAimCommand(
                out bool clearOverride,
                out PlayerAimOverride aimOverride,
                out Vector2 sourceAimDirection))
        {
            return false;
        }

        if (clearOverride)
        {
            ClearOverride();
        }
        else
        {
            ApplyOverride(
                commands,
                in aimOverride,
                sourceAimDirection,
                !state.HasMovement ||
                state.FacingRight,
                state.HasMovement &&
                state.IsWallSliding);
        }

        return true;
    }


    private void TickAim(
        PlayerTick tick)
    {
        if (!GetInput(
                out PlayerInputData input))
        {
            return;
        }

        bool facingRight = !tick.State.HasMovement ||
            tick.State.FacingRight;
        bool isWallSliding =
            tick.State.HasMovement &&
            tick.State.IsWallSliding;

        Vector2 inputAimDirection =
            ResolveDirectionTo(
                input.AimWorldPosition,
                facingRight);

        UpdateAimDirection(
            inputAimDirection);

        UpdateFacing(
            tick.Commands,
            isWallSliding,
            facingRight);

        UpdateBodyAim(facingRight);
    }


    public override void Present(in PlayerTickState tickState)
    {
        bool facingRight = tickState.FacingRight;

        UpdateBodyAimPresentation(facingRight);
        UpdateRigPresentation(facingRight);
    }


    private void Update()
    {
        // Animator가 이번 프레임의 클립 포즈를 계산하기 전에
        // 이전 프레임에 더했던 표현 전용 회전을 제거합니다.
        RestoreAnimationBodyAimPose();
    }


    private void LateUpdate()
    {
        if (!_applyAnimationBodyAim)
            return;

        ApplyAnimationBodyAimPose();
    }


    private void OnDisable()
    {
        RestoreAnimationBodyAimPose();
    }


    private void UpdateBodyAimPresentation(bool facingRight)
    {
        if (!_hasPresentedBodyAimAngle ||
            _presentedFacingRight != facingRight)
        {
            _presentedBodyAimAngle = BodyAimAngle;
            _hasPresentedBodyAimAngle = true;
            _presentedFacingRight = facingRight;
            return;
        }

        float blend = bodyAimPresentationSharpness <= 0f
            ? 1f
            : 1f - Mathf.Exp(
                -bodyAimPresentationSharpness * Time.deltaTime);

        _presentedBodyAimAngle = Mathf.LerpAngle(
            _presentedBodyAimAngle,
            BodyAimAngle,
            blend);
    }


    // =========================================================
    // Aim Override
    // =========================================================


    private void ApplyOverride(
        PlayerTickCommands tickCommands,
        in PlayerAimOverride aimOverride,
        Vector2 sourceAimDirection,
        bool facingRight,
        bool isWallSliding)
    {
        Vector2 sourceDirection =
            ResolveSourceDirection(
                sourceAimDirection, facingRight);

        // 방향을 고정하기 전에
        // 해당 공격 방향에 맞게 Facing을 한 번 확정한다.
        if (aimOverride.FacingMode ==
            PlayerAimFacingMode.Locked)
        {
            LockedFacingRight =
                TryUpdateFacingFromDirection(
                    tickCommands,
                    sourceDirection,
                    isWallSliding,
                    facingRight);
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

    private Vector2 ResolveDirectionTo(
        Vector2 worldTargetPosition,
        bool facingRight)
    {
        if (aimOrigin == null)
        {
            return ResolveSourceDirection(
                Vector2.zero,
                facingRight);
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

        return ResolveSourceDirection(
            Vector2.zero,
            facingRight);
    }


    private Vector2 ResolveSourceDirection(
        Vector2 sourceDirection,
        bool facingRight)
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

        return facingRight
            ? Vector2.right
            : Vector2.left;
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

    private void UpdateFacing(
        PlayerTickCommands commands,
        bool isWallSliding,
        bool facingRight)
    {        
        if (isWallSliding)
            return;

        if (FacingMode ==
            PlayerAimFacingMode.Locked)
        {
            RequestFacing(
                commands,
                LockedFacingRight);

            return;
        }

        TryUpdateFacingFromDirection(
            commands,
            AimDirection,
            isWallSliding,
            facingRight);
    }


    private bool TryUpdateFacingFromDirection(
        PlayerTickCommands tickCommands,
        Vector2 direction,
        bool isWallSliding,
        bool facingRight)
    {
        if (isWallSliding)
            return facingRight;

        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return facingRight;
        }

        Vector2 facingDirection =
            facingRight
                ? Vector2.right
                : Vector2.left;

        float angleFromFacing =
            Vector2.Angle(
                facingDirection,
                direction);

        if (angleFromFacing <=
            facingFlipAngle)
        {
            return facingRight;
        }

        bool nextFacingRight =
            !facingRight;

        RequestFacing(
            tickCommands,
            nextFacingRight);

        return nextFacingRight;
    }


    private void RequestFacing(
        PlayerTickCommands tickCommands,
        bool facingRight)
    {
        if (tickCommands != null)
        {
            tickCommands.RequestFacing(
                facingRight);

            return;
        }
    }


    // =========================================================
    // Body Aim
    // =========================================================

    private void UpdateBodyAim(bool facingRight)
    {
        if (AimDirection.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        float targetAngle =
            CalculateLocalAimAngle(
                AimDirection, facingRight);

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
        Vector2 worldDirection, bool facingFight)
    {
        Vector2 localDirection =
            facingFight
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
        float localAngle, bool facingRight)
    {
        float radians =
            localAngle *
            Mathf.Deg2Rad;

        Vector2 direction =
            new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians));

        if (!facingRight)
        {
            direction.x *=
                -1f;
        }

        return direction.normalized;
    }


    public Vector2 ResolveLimitedAimDirection(
        Vector2 direction,
        bool facingRight)
    {
        direction = NormalizeDirection(direction);

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return GetWorldBodyDirection(
                BodyAimAngle,
                facingRight);
        }

        float localAngle = Mathf.Clamp(
            CalculateLocalAimAngle(
                direction,
                facingRight),
            -maxBodyAimAngle,
            maxBodyAimAngle);

        return GetWorldBodyDirection(
            localAngle,
            facingRight);
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

    private void UpdateRigPresentation(bool facingRight)
    {
        _applyAnimationBodyAim =
            RigMode ==
            PlayerAimRigMode.AnimationWithBodyAim;

        if (RigMode ==
            PlayerAimRigMode.AnimationOnly)
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

        ApplyCcdTarget(facingRight);

        if (upperBodyAimRig == null)
            return;

        if (_applyAnimationBodyAim)
        {
            // 4본 CCD는 클립의 허리/목/머리 비율을 다시 분배하므로
            // 합성 모드에서는 끄고 LateUpdate에서 포즈 전체를 돌립니다.
            upperBodyAimRig.enabled = false;
            return;
        }

        upperBodyAimRig.solveFromDefaultPose = true;

        // Target 회전이 아니라 Effector 위치로 상체 방향을 풉니다.
        upperBodyAimRig.constrainRotation = false;
        upperBodyAimRig.weight = 1f;

        if (!upperBodyAimRig.enabled)
        {
            upperBodyAimRig.enabled =
                true;
        }
    }


    private void ApplyAnimationBodyAimPose()
    {
        if (resolvedAimReferenceBone == null ||
            ccdTarget == null ||
            upperBodyAimRig == null)
        {
            return;
        }

        IKChain2D bodyChain =
            upperBodyAimRig.GetChain(0);

        Transform bodyEffector =
            bodyChain?.effector;

        if (bodyEffector == null)
            return;

        Vector2 animatedDirection =
            bodyEffector.position -
            resolvedAimReferenceBone.position;

        Vector2 targetDirection =
            ccdTarget.position -
            resolvedAimReferenceBone.position;

        if (animatedDirection.sqrMagnitude <= 0.0001f ||
            targetDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float worldDelta =
            Vector2.SignedAngle(
                animatedDirection,
                targetDirection);

        _animationBodyAimBaseRotation =
            resolvedAimReferenceBone.localRotation;

        CaptureAnimatedUpperBodyIkTargets();

        float parentHandedness =
            GetParentHandedness(
                resolvedAimReferenceBone);

        resolvedAimReferenceBone.localRotation *=
            Quaternion.Euler(
                0f,
                0f,
                worldDelta * parentHandedness);

        // 클립이 팔 IK Target을 키로 저장한 경우에도 상체와 함께 돌려
        // 팔 자세가 원래 클립과 같은 상대 배치를 유지하게 합니다.
        foreach (AnimatedIkTargetPose pose
                 in _animatedIkTargetPoses)
        {
            if (pose.Target == null)
                continue;

            pose.Target.RotateAround(
                resolvedAimReferenceBone.position,
                Vector3.forward,
                worldDelta);
        }

        _hasAppliedAnimationBodyAim = true;
    }


    private void CaptureAnimatedUpperBodyIkTargets()
    {
        _animatedIkTargetPoses.Clear();

        IKManager2D manager =
            upperBodyAimRig.GetComponentInParent<IKManager2D>();

        if (manager == null)
            return;

        foreach (Solver2D solver in manager.solvers)
        {
            if (solver == null ||
                solver == upperBodyAimRig ||
                !solver.isActiveAndEnabled ||
                solver.weight <= 0f)
            {
                continue;
            }

            for (int index = 0;
                 index < solver.chainCount;
                 index++)
            {
                IKChain2D chain =
                    solver.GetChain(index);

                Transform chainRoot =
                    chain?.rootTransform;

                Transform target =
                    chain?.target;

                if (chainRoot == null ||
                    target == null ||
                    !chainRoot.IsChildOf(
                        resolvedAimReferenceBone) ||
                    target.IsChildOf(
                        resolvedAimReferenceBone))
                {
                    continue;
                }

                _animatedIkTargetPoses.Add(
                    new AnimatedIkTargetPose(
                        target));
            }
        }
    }


    private void RestoreAnimationBodyAimPose()
    {
        if (!_hasAppliedAnimationBodyAim)
            return;

        if (resolvedAimReferenceBone != null)
        {
            resolvedAimReferenceBone.localRotation =
                _animationBodyAimBaseRotation;
        }

        foreach (AnimatedIkTargetPose pose
                 in _animatedIkTargetPoses)
        {
            if (pose.Target == null)
                continue;

            pose.Target.localPosition =
                pose.LocalPosition;

            pose.Target.localRotation =
                pose.LocalRotation;
        }

        _animatedIkTargetPoses.Clear();
        _hasAppliedAnimationBodyAim = false;
    }


    private static float GetParentHandedness(
        Transform target)
    {
        if (target.parent == null)
            return 1f;

        Vector3 parentScale =
            target.parent.lossyScale;

        return parentScale.x * parentScale.y < 0f
            ? -1f
            : 1f;
    }


    private void ResolveStandardRigReferences()
    {
        Standard2DRigIKSetup setup =
            GetComponent<Standard2DRigIKSetup>();

        if (setup == null)
            return;

        if (ccdTarget == null)
        {
            ccdTarget =
                setup.GeneratedBodyAimTarget;
        }

        if (upperBodyAimRig == null)
        {
            upperBodyAimRig =
                setup.GeneratedBodyAimSolver;
        }
    }


    private void ApplyCcdTarget(bool facingRight)
    {
        Vector2 bodyDirection =
            GetPresentedBodyDirection(facingRight);

        float signedRigOffset =
            facingRight
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


    private Vector2 GetPresentedBodyDirection(
        bool facingRight)
    {
        return GetWorldBodyDirection(
            _hasPresentedBodyAimAngle
                ? _presentedBodyAimAngle
                : BodyAimAngle,
            facingRight);
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

        if (resolvedAimReferenceBone != null &&
            resolvedAimPivot != null &&
            resolvedAimPivot.parent !=
                resolvedAimReferenceBone)
        {
            Debug.LogWarning(
                "ResolvedAimPivot은 지정한 기준 척추 본의 " +
                "직접 자식이어야 합니다.",
                this);
        }
    }


    private void OnDrawGizmosSelected()
    {
        if (aimOrigin == null)
            return;

        Vector3 origin =
            aimOrigin.position;

        if (Application.isPlaying &&
            Object != null &&
            Object.IsValid &&
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
