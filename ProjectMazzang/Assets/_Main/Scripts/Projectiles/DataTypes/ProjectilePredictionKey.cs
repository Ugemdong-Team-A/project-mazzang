using Fusion;
using System;

/// <summary>
/// 로컬 예측 표시와 Host가 생성한 실제 투사체를 대응시키는 키입니다.
/// </summary>
public readonly struct ProjectilePredictionKey :
    IEquatable<ProjectilePredictionKey>
{
    public NetworkId EmitterId
    {
        get;
    }

    public int FireTick
    {
        get;
    }

    public int ShotOrdinal
    {
        get;
    }

    public int ProjectileIndex
    {
        get;
    }

    public bool IsValid =>
        !EmitterId.Equals(
            default(NetworkId)) &&
        FireTick >= 0 &&
        ShotOrdinal >= 0 &&
        ProjectileIndex >= 0;


    public ProjectilePredictionKey(
        NetworkId emitterId,
        int fireTick,
        int shotOrdinal,
        int projectileIndex)
    {
        EmitterId = emitterId;
        FireTick = fireTick;
        ShotOrdinal = shotOrdinal;
        ProjectileIndex = projectileIndex;
    }


    public bool Equals(
        ProjectilePredictionKey other)
    {
        return
            EmitterId.Equals(
                other.EmitterId) &&
            FireTick ==
            other.FireTick &&
            ShotOrdinal ==
            other.ShotOrdinal &&
            ProjectileIndex ==
            other.ProjectileIndex;
    }


    public override bool Equals(
        object obj)
    {
        return
            obj is ProjectilePredictionKey other &&
            Equals(
                other);
    }


    public override int GetHashCode()
    {
        unchecked
        {
            int hash =
                EmitterId.GetHashCode();

            hash =
                hash * 397 ^
                FireTick;

            hash =
                hash * 397 ^
                ShotOrdinal;

            hash =
                hash * 397 ^
                ProjectileIndex;

            return hash;
        }
    }


    public override string ToString()
    {
        return
            EmitterId +
            ":" +
            FireTick +
            ":" +
            ShotOrdinal +
            ":" +
            ProjectileIndex;
    }


    public static bool operator ==(
        ProjectilePredictionKey left,
        ProjectilePredictionKey right)
    {
        return left.Equals(
            right);
    }


    public static bool operator !=(
        ProjectilePredictionKey left,
        ProjectilePredictionKey right)
    {
        return !left.Equals(
            right);
    }
}
