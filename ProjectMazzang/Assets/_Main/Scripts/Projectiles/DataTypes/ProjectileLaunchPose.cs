using UnityEngine;

/// <summary>
/// 무기와 스킬에 독립적인 투사체 생성 위치와 최종 기본 방향입니다.
/// </summary>
public readonly struct ProjectileLaunchPose
{
    public Vector2 Origin
    {
        get;
    }

    public Vector2 Direction
    {
        get;
    }


    public ProjectileLaunchPose(
        Vector2 origin,
        Vector2 direction)
    {
        Origin = origin;

        Direction =
            direction.sqrMagnitude >
            0.0001f
                ? direction.normalized
                : Vector2.right;
    }
}
