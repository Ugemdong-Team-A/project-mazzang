using System;
using UnityEngine;

public enum PlayerAimTrackingMode : byte
{
    FollowInput = 0,
    LockedDirection,
    LockedFourWay
}

public enum PlayerAimFacingMode : byte
{
    FollowAim = 0,
    Locked
}

public enum PlayerAimRigMode : byte
{
    Procedural = 0,
    AnimationDriven
}

public enum PlayerAimCardinalDirection : byte
{
    None = 0,
    Right,
    Up,
    Left,
    Down
}


// =========================================================
// Runtime Override
// =========================================================

public readonly struct PlayerAimOverride
{
    public PlayerAimTrackingMode TrackingMode { get; }

    public PlayerAimFacingMode FacingMode { get; }

    public PlayerAimRigMode RigMode { get; }


    public PlayerAimOverride(
        PlayerAimTrackingMode trackingMode,
        PlayerAimFacingMode facingMode,
        PlayerAimRigMode rigMode)
    {
        TrackingMode =
            trackingMode;

        FacingMode =
            facingMode;

        RigMode =
            rigMode;
    }
}


// =========================================================
// Attack Definition
// =========================================================

public enum PlayerAttackAimMode : byte
{
    Free = 0,

    /// <summary>
    /// 공격 시작 당시의 자유 조준 방향을 그대로 고정합니다.
    /// </summary>
    DirectionLocked,

    /// <summary>
    /// 공격 시작 당시의 조준 방향을
    /// 상하좌우 중 하나로 양자화하여 고정합니다.
    /// </summary>
    FourWayLocked
}

public enum PlayerAttackPoseMode : byte
{
    /// <summary>
    /// 평상시처럼 PlayerAim의 CCD가 상체를 제어합니다.
    /// </summary>
    ProceduralAim = 0,

    /// <summary>
    /// 공격 애니메이션이 상체 포즈를 전적으로 제어합니다.
    /// </summary>
    Animation
}


[Serializable]
public struct PlayerAttackAimDefinition
{
    [SerializeField]
    private PlayerAttackAimMode aimMode;

    [SerializeField]
    private PlayerAttackPoseMode poseMode;


    public PlayerAttackAimMode AimMode =>
        aimMode;

    public PlayerAttackPoseMode PoseMode =>
        poseMode;


    public bool RequiresOverride =>
        aimMode !=
            PlayerAttackAimMode.Free ||
        poseMode !=
            PlayerAttackPoseMode.ProceduralAim;


    public PlayerAimOverride CreateOverride()
    {
        PlayerAimTrackingMode trackingMode =
            aimMode switch
            {
                PlayerAttackAimMode.DirectionLocked =>
                    PlayerAimTrackingMode.LockedDirection,

                PlayerAttackAimMode.FourWayLocked =>
                    PlayerAimTrackingMode.LockedFourWay,

                _ =>
                    PlayerAimTrackingMode.FollowInput
            };

        PlayerAimFacingMode facingMode =
            aimMode ==
            PlayerAttackAimMode.Free
                ? PlayerAimFacingMode.FollowAim
                : PlayerAimFacingMode.Locked;

        PlayerAimRigMode rigMode =
            poseMode ==
            PlayerAttackPoseMode.Animation
                ? PlayerAimRigMode.AnimationDriven
                : PlayerAimRigMode.Procedural;

        return new PlayerAimOverride(
            trackingMode,
            facingMode,
            rigMode);
    }
}