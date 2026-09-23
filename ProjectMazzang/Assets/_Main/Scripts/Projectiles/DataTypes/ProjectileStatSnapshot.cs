using UnityEngine;

/// <summary>
/// 발사 시점에 고정하는 투사체 관련 능력치 배율입니다.
/// </summary>
public readonly struct ProjectileStatSnapshot
{
    public static ProjectileStatSnapshot Identity =>
        new(
            1f,
            1f,
            1f,
            1f,
            1f,
            1f);

    public float DamageMultiplier
    {
        get;
    }

    public float SpeedMultiplier
    {
        get;
    }

    public float LifetimeMultiplier
    {
        get;
    }

    public float KnockbackMultiplier
    {
        get;
    }

    public float ScaleMultiplier
    {
        get;
    }

    public float GravityMultiplier
    {
        get;
    }


    public ProjectileStatSnapshot(
        float damageMultiplier,
        float speedMultiplier,
        float lifetimeMultiplier,
        float knockbackMultiplier,
        float scaleMultiplier,
        float gravityMultiplier)
    {
        DamageMultiplier =
            Mathf.Max(
                0f,
                damageMultiplier);

        SpeedMultiplier =
            Mathf.Max(
                0f,
                speedMultiplier);

        LifetimeMultiplier =
            Mathf.Max(
                0f,
                lifetimeMultiplier);

        KnockbackMultiplier =
            Mathf.Max(
                0f,
                knockbackMultiplier);

        ScaleMultiplier =
            Mathf.Max(
                0.01f,
                scaleMultiplier);

        GravityMultiplier =
            Mathf.Max(
                0f,
                gravityMultiplier);
    }


    public static ProjectileStatSnapshot
        FromDamageMultiplier(
            float damageMultiplier)
    {
        return new ProjectileStatSnapshot(
            damageMultiplier,
            1f,
            1f,
            1f,
            1f,
            1f);
    }


    public ProjectileStatSnapshot Combine(
        in ProjectileStatSnapshot other)
    {
        return new ProjectileStatSnapshot(
            DamageMultiplier *
            other.DamageMultiplier,

            SpeedMultiplier *
            other.SpeedMultiplier,

            LifetimeMultiplier *
            other.LifetimeMultiplier,

            KnockbackMultiplier *
            other.KnockbackMultiplier,

            ScaleMultiplier *
            other.ScaleMultiplier,

            GravityMultiplier *
            other.GravityMultiplier);
    }
}
