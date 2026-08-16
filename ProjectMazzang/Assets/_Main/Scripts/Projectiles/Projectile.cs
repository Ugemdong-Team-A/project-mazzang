using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class Projectile :
    NetworkBehaviour
{
    [Header("Collision")]
    [SerializeField]
    private LayerMask collisionMask;

    [Min(0.001f)]
    [SerializeField]
    private float collisionRadius = 0.05f;

    [Tooltip(
        "??Î≤àÏùò Substep?êÏÑú ?àÏö©??ÏµúÎ? ?¥Îèô Í±∞Î¶¨?ÖÎãà?? " +
        "??Tick???¥Îèô?âÏù¥ ???¨Î©¥ ?¨Îü¨ ?®Í≥ÑÎ°??òÎà† ?úÎ??àÏù¥?òÌï©?àÎã§.")]
    [Min(0.005f)]
    [SerializeField]
    private float maxSimulationStepDistance = 0.08f;

    [Min(1)]
    [SerializeField]
    private int maxSimulationStepsPerTick = 16;


    [Header("Ballistics")]
    [Tooltip(
        "?îÎìú ?ÑÎûò Î∞©Ìñ• Ï§ëÎ†• Î∞∞Ïú®?ÖÎãà?? " +
        "0?¥Î©¥ ÏßÅÏÑ† ?ÑÎèÑ, 1?¥Î©¥ gravityAcceleration??Í∑∏Î?Î°??¨Ïö©?©Îãà??")]
    [Min(0f)]
    [SerializeField]
    private float gravityScale = 0f;

    [Min(0f)]
    [SerializeField]
    private float gravityAcceleration = 9.81f;

    [Tooltip(
        "?çÎèÑ Í∞êÏá† Í≥ÑÏàò?ÖÎãà?? 0?¥Î©¥ Í≥µÍ∏∞ ?Ä??ùÑ ?ÅÏö©?òÏ? ?äÏäµ?àÎã§.")]
    [Min(0f)]
    [SerializeField]
    private float linearDrag = 0f;

    [Tooltip(
        "ÏßÑÌñâ Î∞©Ìñ•??ÎßûÏ∂∞ Projectile??+X Ï∂ïÏùÑ ?åÏ†Ñ?úÌÇµ?àÎã§.")]
    [SerializeField]
    private bool alignRotationToVelocity = true;

    [Header("Presentation")]
    [SerializeField]
    private ProjectileTrail projectileTrail;

    private bool _trailStarted;


    [Networked]
    public Vector2 Velocity
    {
        get;
        protected set;
    }

    [Networked]
    public NetworkObject Source
    {
        get;
        protected set;
    }

    [Networked]
    protected int Damage
    {
        get;
        set;
    }

    [Networked]
    protected Vector2 LocalKnockback
    {
        get;
        set;
    }

    [Networked]
    protected float KnockbackControlLock
    {
        get;
        set;
    }

    [Networked]
    protected TickTimer LifeTimer
    {
        get;
        set;
    }

    [Networked]
    protected NetworkBool IsInitialized
    {
        get;
        set;
    }


    protected virtual void Awake()
    {
        if (projectileTrail == null)
        {
            projectileTrail =
                GetComponent<ProjectileTrail>();
        }
    }


    public override void Spawned()
    {
        TryStartTrailPresentation();
    }


    public override void Render()
    {
        TryStartTrailPresentation();
    }


    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        if (projectileTrail != null)
        {
            projectileTrail.Complete();
        }
    }


    public virtual void Initialize(
    NetworkRunner runner,
    NetworkObject source,
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

        Source =
            source;

        Velocity =
            direction *
            velocity.magnitude;

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

        ApplyRotationFromVelocity();
        TryStartTrailPresentation();
    }


    private void TryStartTrailPresentation()
    {
        if (_trailStarted ||
            !IsInitialized ||
            projectileTrail == null)
        {
            return;
        }

        _trailStarted = true;

        projectileTrail.Begin(
            ResolvePresentationOrigin(),
            transform);
    }


    private Vector2 ResolvePresentationOrigin()
    {
        if (Source == null ||
            !Source.TryGetComponent(
                out PlayerWeaponController controller))
        {
            return transform.position;
        }

        if (controller.EquippedWeapon is
            ProjectileGun gun)
        {
            return gun.PresentationMuzzlePosition;
        }

        return controller.WeaponSocket != null
            ? controller.WeaponSocket.position
            : transform.position;
    }


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
            DespawnProjectile();
            return;
        }

        SimulateBallisticMovement();
    }


    private void SimulateBallisticMovement()
    {
        float tickDeltaTime =
            Runner.DeltaTime;

        Vector2 gravity =
            Vector2.down *
            gravityAcceleration *
            gravityScale;

        Vector2 estimatedEndVelocity =
            Velocity +
            gravity *
            tickDeltaTime;

        float estimatedMaxSpeed =
            Mathf.Max(
                Velocity.magnitude,
                estimatedEndVelocity.magnitude);

        float estimatedDistance =
            estimatedMaxSpeed *
            tickDeltaTime;

        int stepCount =
            CalculateStepCount(
                estimatedDistance);

        float stepDeltaTime =
            tickDeltaTime /
            stepCount;

        for (int i = 0;
             i < stepCount;
             i++)
        {
            ApplyBallistics(
                gravity,
                stepDeltaTime);

            if (!SimulateMovementStep(
                    stepDeltaTime))
            {
                return;
            }
        }

        ApplyRotationFromVelocity();
    }


    private void ApplyBallistics(
        Vector2 gravity,
        float deltaTime)
    {
        Velocity +=
            gravity *
            deltaTime;

        if (linearDrag <= 0f)
            return;

        Velocity /=
            1f +
            linearDrag *
            deltaTime;
    }


    private bool SimulateMovementStep(
        float deltaTime)
    {
        Vector2 displacement =
            Velocity *
            deltaTime;

        float distance =
            displacement.magnitude;

        if (distance <=
            0.0001f)
        {
            return true;
        }

        Vector2 direction =
            displacement /
            distance;

        Vector2 start =
            transform.position;

        if (TryFindCollision(
                start,
                direction,
                distance,
                out RaycastHit2D hit))
        {
            transform.position =
                start +
                direction *
                hit.distance;

            ApplyRotationFromVelocity();

            OnImpact(
                hit);

            return false;
        }

        transform.position =
            start +
            displacement;

        return true;
    }


    private int CalculateStepCount(
        float estimatedDistance)
    {
        float stepDistance =
            Mathf.Max(
                0.005f,
                maxSimulationStepDistance);

        int stepCount =
            Mathf.CeilToInt(
                estimatedDistance /
                stepDistance);

        return Mathf.Clamp(
            stepCount,
            1,
            Mathf.Max(
                1,
                maxSimulationStepsPerTick));
    }


    private bool TryFindCollision(
        Vector2 start,
        Vector2 direction,
        float distance,
        out RaycastHit2D nearestHit)
    {
        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                start,
                collisionRadius,
                direction,
                distance,
                collisionMask);

        bool found =
            false;

        nearestHit =
            default;

        float nearestDistance =
            float.MaxValue;

        for (int i = 0;
             i < hits.Length;
             i++)
        {
            RaycastHit2D hit =
                hits[i];

            Collider2D candidate =
                hit.collider;

            if (candidate == null)
                continue;

            if (ShouldIgnoreCollider(
                    candidate))
            {
                continue;
            }

            if (hit.distance >=
                nearestDistance)
            {
                continue;
            }

            nearestDistance =
                hit.distance;

            nearestHit =
                hit;

            found =
                true;
        }

        return found;
    }


    protected virtual bool ShouldIgnoreCollider(
        Collider2D candidate)
    {
        if (candidate.transform ==
            transform)
        {
            return true;
        }

        if (candidate.transform.IsChildOf(
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

        if (Source != null &&
    targetObject == Source)
        {
            return true;
        }

        return false;
    }


    protected virtual void OnImpact(
        RaycastHit2D hit)
    {
        TryApplyDamage(
            hit.collider);

        DespawnProjectile();
    }


    protected bool TryApplyDamage(
        Collider2D hit)
    {
        if (hit == null)
            return false;

        IDamageable damageable =
            hit.GetComponentInParent<
                IDamageable>();

        if (damageable == null ||
            !damageable.IsAlive)
        {
            return false;
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
                Source,
                knockback,
                KnockbackControlLock);

        damageable.ApplyDamage(
            in info);

        return true;
    }


    public virtual bool Reflect(
        PlayerRef newOwner,
        Vector2 newDirection,
        float speedMultiplier = 1f)
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
            Velocity.magnitude *
            Mathf.Max(
                0f,
                speedMultiplier);

        /*Source =
            newOwner;*/

        Velocity =
            newDirection *
            speed;

        ApplyRotationFromVelocity();

        return true;
    }


    protected void StopProjectile()
    {
        if (!HasStateAuthority)
            return;

        Velocity =
            Vector2.zero;
    }


    protected void DespawnProjectile()
    {
        if (!HasStateAuthority)
            return;

        Runner.Despawn(
            Object);
    }


    private void ApplyRotationFromVelocity()
    {
        if (!alignRotationToVelocity)
            return;

        Vector2 direction =
            NormalizeDirection(
                Velocity);

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


    protected static Vector2 NormalizeDirection(
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


#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            collisionRadius);
    }

#endif
}