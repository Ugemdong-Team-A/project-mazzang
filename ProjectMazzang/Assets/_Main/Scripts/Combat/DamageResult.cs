using System;

public readonly struct DamageResult
{
    public static DamageResult Rejected =>
        default;


    /// <summary>
    /// 대상이 피해 요청을 수락해 피격 효과까지 처리했는지 나타냅니다.
    /// 실제 체력 감소량은 0일 수 있습니다.
    /// </summary>
    public bool WasProcessed
    {
        get;
    }

    /// <summary>
    /// 방어 및 체력 범위를 반영한 실제 체력 감소량입니다.
    /// </summary>
    public int AppliedDamage
    {
        get;
    }

    public bool WasFatal
    {
        get;
    }


    public DamageResult(
        int appliedDamage,
        bool wasFatal)
    {
        WasProcessed =
            true;

        AppliedDamage =
            Math.Max(
                0,
                appliedDamage);

        WasFatal =
            wasFatal;
    }
}
