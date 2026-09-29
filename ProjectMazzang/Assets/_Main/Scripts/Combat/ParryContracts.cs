using Fusion;
using UnityEngine;

public readonly struct ParryHit
{
    public ParryHit(
        NetworkObject owner,
        Vector2 point,
        Vector2 direction,
        in ParryProjectileModifiers projectileModifiers)
    {
        Owner = owner;
        Point = point;
        Direction = direction;
        ProjectileModifiers = projectileModifiers;
    }

    public NetworkObject Owner { get; }
    public Vector2 Point { get; }
    public Vector2 Direction { get; }
    public ParryProjectileModifiers ProjectileModifiers { get; }
}

public interface IParryable
{
    Vector2 ParryVelocity { get; }

    NetworkObject ParrySource { get; }

    bool TryParry(in ParryHit hit);
}

public interface IParryVolume
{
    bool IsParryActive { get; }

    NetworkObject ParryOwner { get; }

    Vector2 ParryOrigin { get; }

    Vector2 ParryDirection { get; }

    float ParryRadius { get; }

    float ParryHalfAngle { get; }

    float ParryAimInfluence { get; }

    ParryProjectileModifiers ProjectileModifiers { get; }

    void OnParrySuccess(Vector2 point);
}

public static class ParryGeometry
{
    public static Vector2 ResolveOrigin(
        Vector2 rootPosition,
        bool facingRight,
        Vector2 centerOffset)
    {
        return rootPosition +
            new Vector2(
                centerOffset.x *
                (facingRight ? 1f : -1f),
                centerOffset.y);
    }

    public static Vector2 ResolveLocalOrigin(
        Vector2 origin,
        Vector2 forward,
        Vector2 localOffset,
        bool mirrored)
    {
        forward = forward.sqrMagnitude > 0.0001f
            ? forward.normalized
            : Vector2.right;

        if (mirrored)
            localOffset.y = -localOffset.y;

        Vector2 up =
            new(-forward.y, forward.x);

        return origin +
            forward * localOffset.x +
            up * localOffset.y;
    }
}
