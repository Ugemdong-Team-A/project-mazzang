using System.Collections.Generic;
using Fusion;
using UnityEngine;

public sealed class SwordWeapon :
    Weapon
{
    [Header("Attack")]
    [SerializeField]
    private AttackData attack;

    [SerializeField]
    private Vector2 hitboxSize =
        new Vector2(1.8f, 0.8f);

    [Min(0f)]
    [SerializeField]
    private float hitboxForwardOffset = 0.9f;

    [SerializeField]
    private LayerMask hurtboxLayer;

    [Header("Cooldown")]
    [Min(0f)]
    [SerializeField]
    private float cooldown = 0.5f;

    [Header("Attack Delay")]
    [Min(0f)]
    [SerializeField]
    private float attackDelay = 0.5f;

    [Networked]
    private TickTimer AttackDelayTimer
    {
        get;
        set;
    }
    private TickTimer CooldownTimer
    {
        get;
        set;
    }
    private Vector2 AttackDirection
    {
        get;
        set;
    }


    private readonly HashSet<IDamageable>
        _hitTargets = new();


    public override void Spawned()
    {
        base.Spawned();

        if (!HasStateAuthority)
            return;

        CooldownTimer =
            TickTimer.None;
    }


    public override bool TryUse(
     Vector2 origin,
     Vector2 direction,
     bool mirrored,
     float attackDamageMultiplier)
    {
        if (!CanAttack())
        {
            Debug.Log("[Sword] CanAttack 실패");
            return false;
        }

        AttackDelayTimer =
            attackDelay > 0f
                ? TickTimer.CreateFromSeconds(
                    Runner,
                    attackDelay)
                : TickTimer.None;

        StartCooldown();

        Debug.Log("[Sword] 공격 준비");

        return true;
    }


    private bool CanAttack()
    {
        return
            HasStateAuthority &&
            IsEquipped &&
            Holder != null &&
            CooldownTimer
                .ExpiredOrNotRunning(
                    Runner);
    }


    private void StartCooldown()
    {
        CooldownTimer =
            cooldown > 0f
                ? TickTimer.CreateFromSeconds(
                    Runner,
                    cooldown)
                : TickTimer.None;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!AttackDelayTimer.Expired(Runner))
            return;

        AttackDelayTimer =
            TickTimer.None;

        if (Holder == null)
            return;

        if (!Holder.TryGetComponent(
                out IWeaponHandler handler))
        {
            return;
        }

        Vector2 origin =
            handler.WeaponSocket != null
                ? handler.WeaponSocket.position
                : (Vector2)transform.position;

        Vector2 direction =
            NormalizeDirection(
                handler.WeaponDirection);

        PerformAttack(
            origin,
            direction);

        Debug.Log("[Sword] 0.5초 후 공격 판정");
    }

    private void PerformAttack(
        Vector2 origin,
        Vector2 direction)
    {
        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;


        Vector2 center =
            origin +
            direction *
            hitboxForwardOffset;


        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                center,
                hitboxSize,
                angle,
                hurtboxLayer);


        _hitTargets.Clear();


        foreach (Collider2D hit in hits)
        {
            IDamageable damageable =
                hit.GetComponentInParent<
                    IDamageable>();


            if (damageable == null)
                continue;


            NetworkObject target =
                hit.GetComponentInParent<
                    NetworkObject>();


            if (target == Holder)
                continue;


            if (!_hitTargets.Add(
                    damageable))
            {
                continue;
            }


            if (!damageable.IsAlive)
                continue;


            Vector2 knockback =
                direction * attack.KnockbackForward +
                Vector2.up * attack.KnockbackUp;


            DamageInfo info =
                new DamageInfo(
                    attack.Damage,
                    Holder,
                    knockback,
                    attack.CrowdControl);


            damageable.ApplyDamage(
                in info);
        }
    }


    private static Vector2 NormalizeDirection(
        Vector2 direction)
    {
        return
            direction.sqrMagnitude >
            0.0001f
                ? direction.normalized
                : Vector2.right;
    }


#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        Vector2 direction =
            Application.isPlaying
                ? NormalizeDirection(
                    transform.right)
                : Vector2.right;


        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;


        Vector2 center =
            (Vector2)transform.position +
            direction *
            hitboxForwardOffset;


        Gizmos.color =
            Color.white;


        Gizmos.matrix =
            Matrix4x4.TRS(
                center,
                Quaternion.Euler(
                    0f,
                    0f,
                    angle),
                Vector3.one);


        Gizmos.DrawWireCube(
            Vector3.zero,
            hitboxSize);
    }

#endif
}
