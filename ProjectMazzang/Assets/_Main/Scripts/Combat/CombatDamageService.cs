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

        DamageResult result =
            target.ApplyDamage(
                in info);

        if (result.AppliedDamage <= 0 ||
            info.Source == null)
        {
            return result;
        }

        IDamageDealtReceiver receiver =
            info.Source.GetComponent<
                IDamageDealtReceiver>();

        receiver?.ReceiveDamageDealt(
            result.AppliedDamage);

        return result;
    }
}
