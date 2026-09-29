using UnityEngine;

public readonly struct ParryProjectileModifiers
{
    public static ParryProjectileModifiers Identity =>
        new(
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

    public float KnockbackMultiplier
    {
        get;
    }

    public float ScaleMultiplier
    {
        get;
    }


    public ParryProjectileModifiers(
        float damageMultiplier,
        float speedMultiplier,
        float knockbackMultiplier,
        float scaleMultiplier)
    {
        DamageMultiplier =
            Mathf.Max(
                0f,
                damageMultiplier);

        SpeedMultiplier =
            Mathf.Max(
                0f,
                speedMultiplier);

        KnockbackMultiplier =
            Mathf.Max(
                0f,
                knockbackMultiplier);

        ScaleMultiplier =
            Mathf.Max(
                0.01f,
                scaleMultiplier);
    }
}
