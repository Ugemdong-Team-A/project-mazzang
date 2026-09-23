using UnityEngine;

/// <summary>
/// 발사 시점의 능력치가 모두 적용된 최종 이동·판정 크기 설정입니다.
/// </summary>
public readonly struct ProjectileLaunchSettings
{
    public float Speed
    {
        get;
    }

    public float Lifetime
    {
        get;
    }

    public float GravityScale
    {
        get;
    }

    public float GravityAcceleration
    {
        get;
    }

    public float LinearDrag
    {
        get;
    }

    public bool AlignRotationToVelocity
    {
        get;
    }

    public float CollisionRadius
    {
        get;
    }

    public ProjectileLaunchSettings(
        float speed,
        float lifetime = 2.5f,
        float gravityScale = 0f,
        float gravityAcceleration = 9.81f,
        float linearDrag = 0f,
        bool alignRotationToVelocity = true,
        float collisionRadius = 0.05f)
    {
        Speed =
            Mathf.Max(
                0f,
                speed);

        Lifetime =
            Mathf.Max(
                0f,
                lifetime);

        GravityScale =
            Mathf.Max(
                0f,
                gravityScale);

        GravityAcceleration =
            Mathf.Max(
                0f,
                gravityAcceleration);

        LinearDrag =
            Mathf.Max(
                0f,
                linearDrag);

        AlignRotationToVelocity =
            alignRotationToVelocity;

        CollisionRadius =
            Mathf.Max(
                0.001f,
                collisionRadius);
    }
}
