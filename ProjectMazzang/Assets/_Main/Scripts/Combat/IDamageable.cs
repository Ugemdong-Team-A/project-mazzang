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

    public float KnockbackControlLock
    {
        get;
    }


    public DamageInfo(
        int damage,
        NetworkObject source,
        Vector2 knockback,
        float knockbackControlLock)
    {
        Damage =
            damage;

        Source =
            source;

        Knockback =
            knockback;

        KnockbackControlLock =
            knockbackControlLock;
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