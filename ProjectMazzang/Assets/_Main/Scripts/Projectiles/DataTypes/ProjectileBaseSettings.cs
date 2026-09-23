using System;
using UnityEngine;

[Serializable]
public struct ProjectileBaseSettings
{
    [Min(0f)]
    [SerializeField]
    private float speed;

    [Min(0f)]
    [SerializeField]
    private float lifetime;

    [Min(0f)]
    [SerializeField]
    private float gravityScale;

    [Min(0f)]
    [SerializeField]
    private float gravityAcceleration;

    [Min(0f)]
    [SerializeField]
    private float linearDrag;

    [SerializeField]
    private bool alignRotationToVelocity;

    [Min(0.001f)]
    [SerializeField]
    private float collisionRadius;


    public static ProjectileBaseSettings Default =>
        new(
            16f,
            2.5f,
            0f,
            9.81f,
            0f,
            true,
            0.05f);

    public float Speed =>
        speed;

    public float Lifetime =>
        lifetime;

    public float GravityScale =>
        gravityScale;

    public float GravityAcceleration =>
        gravityAcceleration;

    public float LinearDrag =>
        linearDrag;

    public bool AlignRotationToVelocity =>
        alignRotationToVelocity;

    public float CollisionRadius =>
        collisionRadius;


    public ProjectileBaseSettings(
        float speed,
        float lifetime,
        float gravityScale,
        float gravityAcceleration,
        float linearDrag,
        bool alignRotationToVelocity,
        float collisionRadius)
    {
        this.speed =
            Mathf.Max(
                0f,
                speed);

        this.lifetime =
            Mathf.Max(
                0f,
                lifetime);

        this.gravityScale =
            Mathf.Max(
                0f,
                gravityScale);

        this.gravityAcceleration =
            Mathf.Max(
                0f,
                gravityAcceleration);

        this.linearDrag =
            Mathf.Max(
                0f,
                linearDrag);

        this.alignRotationToVelocity =
            alignRotationToVelocity;

        this.collisionRadius =
            Mathf.Max(
                0.001f,
                collisionRadius);
    }


    public ProjectileLaunchSettings Resolve(
        in ProjectileStatSnapshot stats)
    {
        return new ProjectileLaunchSettings(
            speed *
            stats.SpeedMultiplier,

            lifetime *
            stats.LifetimeMultiplier,

            gravityScale *
            stats.GravityMultiplier,

            gravityAcceleration,
            linearDrag,
            alignRotationToVelocity,

            collisionRadius *
            stats.ScaleMultiplier);
    }
}
