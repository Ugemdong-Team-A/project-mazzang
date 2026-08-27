using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class Projectile :
    NetworkBehaviour,
    IParryable
{
    [Header("Launch")]
    [Min(0.01f)]
    [SerializeField]
    private float initialSpeed = 16f;

    [Min(0.01f)]
    [SerializeField]
    private float lifetime = 2.5f;

    [Header("Attack")]
    [SerializeField]
    private AttackData attack;

    [Header("Collision")]
    [SerializeField]
    private LayerMask collisionMask;

    [Min(0.001f)]
    [SerializeField]
    private float collisionRadius = 0.05f;

    [Tooltip(
        "한 번의 Substep에서 허용할 최대 이동 거리입니다. " +
        "한 Tick의 이동량이 더 크면 여러 단계로 나누어 시뮬레이션합니다.")]
    [Min(0.005f)]
    [SerializeField]
    private float maxSimulationStepDistance = 0.08f;

    [Min(1)]
    [SerializeField]
    private int maxSimulationStepsPerTick = 16;


    [Header("Ballistics")]
    [Tooltip(
        "월드 아래 방향 중력 배율입니다. " +
        "0이면 직선 궤도, 1이면 gravityAcceleration을 그대로 사용합니다.")]
    [Min(0f)]
    [SerializeField]
    private float gravityScale = 0f;

    [Min(0f)]
    [SerializeField]
    private float gravityAcceleration = 9.81f;

    [Tooltip(
        "속도 감쇠 계수입니다. 0이면 공기 저항을 적용하지 않습니다.")]
    [Min(0f)]
    [SerializeField]
    private float linearDrag = 0f;

    [Tooltip(
        "진행 방향에 맞춰 Projectile의 +X 축을 회전시킵니다.")]
    [SerializeField]
    private bool alignRotationToVelocity = true;

    [Header("Presentation")]
    [SerializeField]
    private ProjectileTrail projectileTrail;

    [Tooltip(
        "Smooths NetworkTransform presentation on remote peers.")]
    [Min(0f)]
    [SerializeField]
    private float remotePresentationSharpness = 30f;

    [Tooltip(
        "Corrections beyond this distance snap immediately.")]
    [Min(0f)]
    [SerializeField]
    private float remotePresentationSnapDistance = 1.5f;

    private bool _trailStarted;
    private Vector3 _remotePresentationPosition;
    private Quaternion _remotePresentationRotation;
    private bool _hasRemotePresentationPose;


    public Vector2 ParryVelocity => Velocity;

    public NetworkObject ParrySource => Source;


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
        ResetRemotePresentationPose();
        TryStartTrailPresentation();
    }


    public override void Render()
    {
        SmoothRemotePresentation();
        TryStartTrailPresentation();
    }


    private void ResetRemotePresentationPose()
    {
        _remotePresentationPosition = transform.position;
        _remotePresentationRotation = transform.rotation;
        _hasRemotePresentationPose = true;
    }


    private void SmoothRemotePresentation()
    {
        if (HasStateAuthority)
        {
            ResetRemotePresentationPose();
            return;
        }

        Vector3 targetPosition = transform.position;
        Quaternion targetRotation = transform.rotation;

        if (!_hasRemotePresentationPose ||
            Vector3.Distance(
                _remotePresentationPosition,
                targetPosition) >= remotePresentationSnapDistance)
        {
            _remotePresentationPosition = targetPosition;
            _remotePresentationRotation = targetRotation;
            _hasRemotePresentationPose = true;
            return;
        }

        float blend = remotePresentationSharpness <= 0f
            ? 1f
            : 1f - Mathf.Exp(
                -remotePresentationSharpness * Time.deltaTime);

        _remotePresentationPosition = Vector3.Lerp(
            _remotePresentationPosition,
            targetPosition,
            blend);

        _remotePresentationRotation = Quaternion.Slerp(
            _remotePresentationRotation,
            targetRotation,
            blend);

        transform.SetPositionAndRotation(
            _remotePresentationPosition,
            _remotePresentationRotation);
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
        Vector2 direction)
    {
        direction =
            NormalizeDirection(
                direction);

        if (direction == Vector2.zero)
        {
            direction =
                transform.right;
        }

        Vector2 knockback =
            attack != null
                ? direction *
                  attack.KnockbackForward +
                  Vector2.up *
                  attack.KnockbackUp
                : Vector2.zero;

        Initialize(
            runner,
            source,
            direction * initialSpeed,
            lifetime,
            attack != null
                ? attack.Damage
                : 0,
            knockback,
            attack != null
                ? attack.KnockbackControlLock
                : 0f);
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

        if (ParryRegistry.TryParry(
                this,
                start,
                start + displacement))
        {
            ApplyRotationFromVelocity();
            return true;
        }

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

        DamageResult result =
            CombatDamageService.ApplyDamage(
                damageable,
                in info);

        return result.WasProcessed;
    }


    public bool TryParry(in ParryHit hit)
    {
        if (!HasStateAuthority ||
            !IsInitialized ||
            hit.Owner == null ||
            hit.Direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Source = hit.Owner;
        Velocity = hit.Direction.normalized *
                   Velocity.magnitude *
                   Mathf.Max(0f, hit.SpeedMultiplier);

        transform.position = hit.Point;
        ApplyRotationFromVelocity();
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
