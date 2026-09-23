using UnityEngine;

/// <summary>
/// 실제 투사체와 예측 표시가 동일하게 적용하는 최종 외형 값입니다.
/// </summary>
public readonly struct ProjectileVisualSnapshot
{
    public static ProjectileVisualSnapshot Default =>
        new(
            0,
            new Color32(
                255,
                255,
                255,
                255),
            0,
            1f,
            1f,
            1f,
            1f,
            0);

    public int VisualVariantId
    {
        get;
    }

    public Color32 Tint
    {
        get;
    }

    public int TrailVariantId
    {
        get;
    }

    public float TrailWidthMultiplier
    {
        get;
    }

    public float EmissionMultiplier
    {
        get;
    }

    public float AnimationSpeedMultiplier
    {
        get;
    }

    public float Scale
    {
        get;
    }

    public int VisualSeed
    {
        get;
    }


    public ProjectileVisualSnapshot(
        int visualVariantId,
        Color32 tint,
        int trailVariantId,
        float trailWidthMultiplier,
        float emissionMultiplier,
        float animationSpeedMultiplier,
        float scale,
        int visualSeed)
    {
        VisualVariantId =
            Mathf.Max(
                0,
                visualVariantId);

        Tint = tint;

        TrailVariantId =
            Mathf.Max(
                0,
                trailVariantId);

        TrailWidthMultiplier =
            Mathf.Max(
                0f,
                trailWidthMultiplier);

        EmissionMultiplier =
            Mathf.Max(
                0f,
                emissionMultiplier);

        AnimationSpeedMultiplier =
            Mathf.Max(
                0f,
                animationSpeedMultiplier);

        Scale =
            Mathf.Max(
                0.01f,
                scale);

        VisualSeed =
            visualSeed;
    }
}
