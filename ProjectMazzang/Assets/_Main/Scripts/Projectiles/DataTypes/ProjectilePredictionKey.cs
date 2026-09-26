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

    public int FireSequence
    {
        get;
    }

    /// <summary>
    /// 한 번의 발사에서 파생된 투사체들의 순번입니다.
    /// 샷건뿐 아니라 다연장, 분열탄과 동시 발사 스킬에도 사용합니다.
    /// </summary>
    public int ProjectileIndex
    {
        get;
    }

    public bool IsValid =>
        !EmitterId.Equals(
            default(NetworkId)) &&
        FireTick >= 0 &&
        FireSequence >= 0 &&
        ProjectileIndex >= 0;


    public ProjectilePredictionKey(
        NetworkId emitterId,
        int fireTick,
        int fireSequence,
        int projectileIndex)
    {
        EmitterId = emitterId;
        FireTick = fireTick;
        FireSequence = fireSequence;
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
            FireSequence ==
            other.FireSequence &&
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
                FireSequence;

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
            FireSequence +
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
