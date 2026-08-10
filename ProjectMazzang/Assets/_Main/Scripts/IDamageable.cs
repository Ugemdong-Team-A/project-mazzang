using Fusion;
using UnityEngine;

public readonly struct DamageInfo
{
    public readonly int Damage;
    public readonly PlayerRef Attacker;
    public readonly Vector2 Knockback;
    public readonly float KnockbackControlLock;

    public DamageInfo(
        int damage,
        PlayerRef attacker,
        Vector2 knockback,
        float knockbackControlLock = 0.12f)
    {
        Damage = damage;
        Attacker = attacker;
        Knockback = knockback;
        KnockbackControlLock = knockbackControlLock;
    }
}

public interface IDamageable
{
    bool IsAlive { get; }

    void ApplyDamage(in DamageInfo info);
}
