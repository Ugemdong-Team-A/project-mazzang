using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public sealed class BulletProjectile :
    NetworkBehaviour
{
    [Header("Collision")]
    [SerializeField]
    private LayerMask collisionMask;

    [Min(0f)]
    [SerializeField]
    private float castRadius = 0.05f;


    // =========================================================
    // Network State
    // =========================================================

    [Networked]
    private Vector2 Velocity
    {
        get;
        set;
    }

    [Networked]
    private int Damage
    {
        get;
        set;
    }

    [Networked]
    private Vector2 Knockback
    {
        get;
        set;
    }

    [Networked]
    private float KnockbackControlLock
    {
        get;
        set;
    }

    [Networked]
    private TickTimer LifeTimer
    {
        get;
        set;
    }


    // =========================================================
    // Spawn Initialization
    // =========================================================

    public void Initialize(
        NetworkRunner runner,
        Vector2 velocity,
        float lifetime,
        int damage,
        Vector2 knockback,
        float knockbackControlLock)
    {
        Velocity =
            velocity;

        Damage =
            damage;

        Knockback =
            knockback;

        KnockbackControlLock =
            knockbackControlLock;

        LifeTimer =
            lifetime > 0f
                ? TickTimer.CreateFromSeconds(
                    runner,
                    lifetime)
                : TickTimer.None;
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void FixedUpdateNetwork()
    {
        // 실제 충돌 / Damage / Transform 확정은 서버만 한다.
        if (!HasStateAuthority)
            return;

        if (LifeTimer.Expired(
                Runner))
        {
            Runner.Despawn(
                Object);

            return;
        }

        Vector2 start =
            transform.position;

        Vector2 displacement =
            Velocity *
            Runner.DeltaTime;

        float distance =
            displacement.magnitude;

        if (distance <= 0.0001f)
            return;

        Vector2 direction =
            displacement /
            distance;

        if (TryFindCollision(
                start,
                direction,
                distance,
                out RaycastHit2D hit))
        {
            transform.position =
                hit.point;

            ApplyHit(
                hit.collider);

            Runner.Despawn(
                Object);

            return;
        }

        transform.position =
            start +
            displacement;
    }


    // =========================================================
    // Collision
    // =========================================================

    private bool TryFindCollision(
        Vector2 origin,
        Vector2 direction,
        float distance,
        out RaycastHit2D bestHit)
    {
        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                origin,
                castRadius,
                direction,
                distance,
                collisionMask);

        bestHit =
            default;

        float bestDistance =
            float.PositiveInfinity;

        bool found =
            false;

        foreach (RaycastHit2D hit
                 in hits)
        {
            if (hit.collider == null)
                continue;

            NetworkObject hitObject =
                hit.collider.GetComponentInParent<
                    NetworkObject>();

            // 발사자 자신의 Player / Hurtbox는 통과한다.
            if (hitObject != null &&
                hitObject != Object &&
                hitObject.InputAuthority ==
                    Object.InputAuthority)
            {
                continue;
            }

            if (hit.distance >=
                bestDistance)
            {
                continue;
            }

            bestDistance =
                hit.distance;

            bestHit =
                hit;

            found =
                true;
        }

        return found;
    }


    private void ApplyHit(
        Collider2D hitCollider)
    {
        if (hitCollider == null)
            return;

        IDamageable damageable =
            hitCollider.GetComponentInParent<
                IDamageable>();

        if (damageable == null ||
            !damageable.IsAlive)
        {
            return;
        }

        DamageInfo info =
            new DamageInfo(
                Damage,
                Object.InputAuthority,
                Knockback,
                KnockbackControlLock);

        damageable.ApplyDamage(
            in info);
    }


#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            castRadius);
    }

#endif
}
