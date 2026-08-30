using Fusion;
using UnityEngine;

public readonly struct DamageInfo
{
    public int Damage
    {
        get;
    }

    /// <summary>
    /// 이 Damage를 발생시킨 NetworkObject입니다.
    ///
    /// Player, Weapon, Projectile, Turret 등
    /// 구체적인 타입을 전제로 하지 않습니다.
    /// </summary>
    public NetworkObject Source
    {
        get;
    }

    public Vector2 Knockback
    {
        get;
    }

    public CrowdControlDefinition CrowdControl
    {
        get;
    }


    public DamageInfo(
        int damage,
        NetworkObject source,
        Vector2 knockback,
        CrowdControlDefinition crowdControl)
        : this(
            damage,
            1f,
            source,
            knockback,
            crowdControl)
    {
    }


    public DamageInfo(
        int baseDamage,
        float attackDamageMultiplier,
        NetworkObject source,
        Vector2 knockback,
        CrowdControlDefinition crowdControl)
    {
        Damage =
            ResolveAttackDamage(
                baseDamage,
                attackDamageMultiplier);

        Source =
            source;

        Knockback =
            knockback;

        CrowdControl =
            crowdControl;
    }


    public static int ResolveAttackDamage(
        int baseDamage,
        float attackDamageMultiplier)
    {
        return Mathf.Max(
            0,
            Mathf.RoundToInt(
                baseDamage *
                Mathf.Max(
                    0f,
                    attackDamageMultiplier)));
    }
}


public interface IDamageable
{
    bool IsAlive
    {
        get;
    }

    DamageResult ApplyDamage(
        in DamageInfo info);
}
