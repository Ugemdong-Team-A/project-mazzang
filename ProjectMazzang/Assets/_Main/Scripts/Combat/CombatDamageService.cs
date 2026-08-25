/// <summary>
/// 피해 적용과 공격 성공 후 처리를 연결하는 공통 진입점입니다.
/// </summary>
public static class CombatDamageService
{
    public static DamageResult ApplyDamage(
        IDamageable target,
        in DamageInfo info)
    {
        if (target == null ||
            !target.IsAlive)
        {
            return DamageResult.Rejected;
        }

        return target.ApplyDamage(
            in info);
    }
}
