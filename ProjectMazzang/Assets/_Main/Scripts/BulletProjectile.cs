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

    [Min(0.001f)]
    [SerializeField]
    private float collisionRadius = 0.05f;

    [Tooltip(
        "한 번의 시뮬레이션 이동에서 최대 이동 거리입니다. " +
        "한 Tick 이동량이 이 값보다 크면 여러 단계로 나누어 이동합니다.")]
    [Min(0.005f)]
    [SerializeField]
    private float maxSimulationStepDistance = 0.08f;

    [Min(1)]
    [SerializeField]
    private int maxSimulationStepsPerTick = 12;


    // =========================================================
    // Network State
    // =========================================================

    [Networked]
    public Vector2 Velocity
    {
        get;
        private set;
    }

    [Networked]
    public PlayerRef Owner
    {
        get;
        private set;
    }

    [Networked]
    private int Damage
    {
        get;
        set;
    }

    [Networked]
    private Vector2 LocalKnockback
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

    [Networked]
    private NetworkBool IsInitialized
    {
        get;
        set;
    }


    // =========================================================
    // Initialize
    // =========================================================

    public void Initialize(
        NetworkRunner runner,
        Vector2 velocity,
        float lifetime,
        int damage,
        Vector2 knockback,
        float knockbackControlLock)
    {
        if (!HasStateAuthority)
            return;

        Vector2 direction =
            NormalizeDirection(
                velocity);

        if (direction ==
            Vector2.zero)
        {
            direction =
                transform.right;
        }

        float speed =
            velocity.magnitude;

        Velocity =
            direction *
            speed;

        Owner =
            Object.InputAuthority;

        Damage =
            damage;

        LocalKnockback =
            ToLocalDirectionSpace(
                knockback,
                direction);

        KnockbackControlLock =
            knockbackControlLock;

        LifeTimer =
            lifetime > 0f
                ? TickTimer.CreateFromSeconds(
                    runner,
                    lifetime)
                : TickTimer.None;

        IsInitialized =
            true;

        ApplyRotation(
            direction);
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority ||
            !IsInitialized)
        {
            return;
        }

        if (LifeTimer.Expired(
                Runner))
        {
            Runner.Despawn(
                Object);

            return;
        }

        SimulateMovement();
    }


    // =========================================================
    // Movement
    // =========================================================

    private void SimulateMovement()
    {
        float totalDistance =
            Velocity.magnitude *
            Runner.DeltaTime;

        if (totalDistance <=
            0.0001f)
        {
            return;
        }

        Vector2 direction =
            Velocity.normalized;

        int stepCount =
            CalculateStepCount(
                totalDistance);

        float stepDistance =
            totalDistance /
            stepCount;

        for (int i = 0;
             i < stepCount;
             i++)
        {
            Vector2 nextPosition =
                (Vector2)transform.position +
                direction *
                stepDistance;

            transform.position =
                nextPosition;

            if (!TryFindCollisionAt(
                    nextPosition,
                    out Collider2D hit))
            {
                continue;
            }

            ApplyHit(
                hit);

            Runner.Despawn(
                Object);

            return;
        }
    }


    private int CalculateStepCount(
        float totalDistance)
    {
        float stepDistance =
            Mathf.Max(
                0.005f,
                maxSimulationStepDistance);

        int stepCount =
            Mathf.CeilToInt(
                totalDistance /
                stepDistance);

        return Mathf.Clamp(
            stepCount,
            1,
            Mathf.Max(
                1,
                maxSimulationStepsPerTick));
    }


    // =========================================================
    // Collision
    // =========================================================

    private bool TryFindCollisionAt(
        Vector2 position,
        out Collider2D hit)
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                position,
                collisionRadius,
                collisionMask);

        for (int i = 0;
             i < hits.Length;
             i++)
        {
            Collider2D candidate =
                hits[i];

            if (candidate == null)
                continue;

            if (ShouldIgnoreCollider(
                    candidate))
            {
                continue;
            }

            hit =
                candidate;

            return true;
        }

        hit =
            null;

        return false;
    }


    private bool ShouldIgnoreCollider(
        Collider2D candidate)
    {
        if (candidate.transform == transform ||
            candidate.transform.IsChildOf(
                transform))
        {
            return true;
        }

        NetworkObject targetObject =
            candidate.GetComponentInParent<
                NetworkObject>();

        if (targetObject == null)
            return false;

        if (targetObject ==
            Object)
        {
            return true;
        }

        if (Owner != PlayerRef.None &&
            targetObject.InputAuthority ==
            Owner)
        {
            return true;
        }

        return false;
    }


    // =========================================================
    // Hit
    // =========================================================

    private void ApplyHit(
        Collider2D hit)
    {
        IDamageable damageable =
            hit.GetComponentInParent<
                IDamageable>();

        if (damageable == null ||
            !damageable.IsAlive)
        {
            return;
        }

        Vector2 direction =
            NormalizeDirection(
                Velocity);

        Vector2 knockback =
            FromLocalDirectionSpace(
                LocalKnockback,
                direction);

        DamageInfo info =
            new DamageInfo(
                Damage,
                Owner,
                knockback,
                KnockbackControlLock);

        damageable.ApplyDamage(
            in info);
    }


    // =========================================================
    // Redirect / Parry Ready
    // =========================================================

    public bool Reflect(
        PlayerRef newOwner,
        Vector2 newDirection)
    {
        if (!HasStateAuthority ||
            !IsInitialized)
        {
            return false;
        }

        newDirection =
            NormalizeDirection(
                newDirection);

        if (newDirection ==
            Vector2.zero)
        {
            return false;
        }

        float speed =
            Velocity.magnitude;

        Owner =
            newOwner;

        Velocity =
            newDirection *
            speed;

        ApplyRotation(
            newDirection);

        return true;
    }


    // =========================================================
    // Direction Utility
    // =========================================================

    private static Vector2 NormalizeDirection(
        Vector2 direction)
    {
        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return Vector2.zero;
        }

        return direction.normalized;
    }


    private static Vector2 ToLocalDirectionSpace(
        Vector2 worldVector,
        Vector2 forward)
    {
        Vector2 perpendicular =
            new Vector2(
                -forward.y,
                forward.x);

        return new Vector2(
            Vector2.Dot(
                worldVector,
                forward),

            Vector2.Dot(
                worldVector,
                perpendicular));
    }


    private static Vector2 FromLocalDirectionSpace(
        Vector2 localVector,
        Vector2 forward)
    {
        if (forward ==
            Vector2.zero)
        {
            return Vector2.zero;
        }

        Vector2 perpendicular =
            new Vector2(
                -forward.y,
                forward.x);

        return
            forward *
            localVector.x +
            perpendicular *
            localVector.y;
    }


    private void ApplyRotation(
        Vector2 direction)
    {
        if (direction ==
            Vector2.zero)
        {
            return;
        }

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;

        transform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                angle);
    }


#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            collisionRadius);
    }

#endif
}