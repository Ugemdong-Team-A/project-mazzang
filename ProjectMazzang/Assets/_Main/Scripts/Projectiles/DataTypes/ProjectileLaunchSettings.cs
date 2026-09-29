using UnityEngine;

/// <summary>
/// 발사 시점의 이동 설정과 논리적 스케일 적용 전 기본 판정 반경입니다.
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

    public float BaseCollisionRadius
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
        float baseCollisionRadius = 0.05f)
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

        BaseCollisionRadius =
            Mathf.Max(
                0.001f,
                baseCollisionRadius);
    }


    public float ResolveCollisionRadius(
        float scaleMultiplier)
    {
        float resolvedScale =
            scaleMultiplier > 0f
                ? scaleMultiplier
                : 1f;

        return BaseCollisionRadius *
               resolvedScale;
    }
}
