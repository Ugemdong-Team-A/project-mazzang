using UnityEngine;

/// <summary>
/// 투사체 하나의 생성 위치, 이동 설정, 외형을 확정한 값입니다.
/// </summary>
public readonly struct ProjectileLaunch
{
    public ProjectilePredictionKey Key
    {
        get;
    }

    public ProjectileLaunchPose Pose
    {
        get;
    }

    public ProjectileLaunchSettings Settings
    {
        get;
    }

    public ProjectileVisualSnapshot Visual
    {
        get;
    }

    public Vector2 Origin =>
        Pose.Origin;

    public Vector2 Direction =>
        Pose.Direction;

    public int ProjectileIndex =>
        Key.ProjectileIndex;


    public ProjectileLaunch(
        ProjectilePredictionKey key,
        ProjectileLaunchPose pose,
        ProjectileLaunchSettings settings,
        ProjectileVisualSnapshot visual)
    {
        Key = key;
        Pose = pose;
        Settings = settings;
        Visual = visual;
    }
}
