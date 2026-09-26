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
    private ProjectileVisual projectileVisual;

    private bool _presentationShown;

    [Tooltip(
        "충돌 시 카메라 진동 연출")]
    [SerializeField]
    private CameraShakeProfile impactShakeProfile;

    [SerializeField]
    private bool despawnOnImpact;

    [Min(0.05f)]
    [SerializeField]
    private float impactPresentationDuration = 0.15f;
    
    private int _visibleImpactSequence;

    private ProjectileLaunchSettings _runtimeLaunchSettings;

    private ProjectileCollision _projectileCollision;

    private ProjectileWeapon _predictionWeapon;
    private PredictedProjectile _localPrediction;

    public ProjectileBaseSettings BaseSettings =>
        new(
            initialSpeed,
            lifetime,
            gravityScale,
            gravityAcceleration,
            linearDrag,
            alignRotationToVelocity,
            collisionRadius);

    public ProjectileLaunchSettings LaunchSettings =>
        BaseSettings.Resolve(
            ProjectileStatSnapshot.Identity);

    public ProjectileVisual Visual =>
        projectileVisual;

    internal LayerMask CollisionMask =>
        collisionMask;

    internal float MaxSimulationStepDistance =>
        maxSimulationStepDistance;

    internal int MaxSimulationStepsPerTick =>
        maxSimulationStepsPerTick;

    internal bool DespawnOnImpact =>
        despawnOnImpact;

    internal float ImpactPresentationDuration =>
        impactPresentationDuration;

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
    protected CrowdControlType CrowdControlType
    {
        get;
        set;
    }

    [Networked]
    protected float CrowdControlDuration
    {
        get;
        set;
    }

    [Networked]
    protected float CrowdControlActivationDelay
    {
        get;
        set;
    }

    [Networked]
    protected NetworkBool StopMovementOnApply
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

    [Networked]
    private Vector2 PresentationOrigin
    {
        get;
        set;
    }

    [Networked]
    private float PresentationScaleMultiplier
    {
        get;
        set;
    }

    [Networked]
    private NetworkBool HasImpacted
    {
        get;
        set;
    }

    [Networked]
    private Vector2 LastImpactPosition
    {
        get;
        set;
    }

    [Networked]
    private int ImpactSequence
    {
        get;
        set;
    }

    [Networked]
    private TickTimer ImpactDespawnTimer
    {
        get;
        set;
    }

    [Networked]
    private NetworkId PredictionEmitterId
    {
        get;
        set;
    }

    [Networked]
    private int PredictionFireTick
    {
        get;
        set;
    }

    [Networked]
    private int PredictionFireSequence
    {
        get;
        set;
    }

    [Networked]
    private int PredictionProjectileIndex
    {
        get;
        set;
    }


    protected virtual void Awake()
    {
        _projectileCollision =
            GetComponent<ProjectileCollision>();

        if (_projectileCollision == null)
        {
            _projectileCollision =
                gameObject.AddComponent<
                    ProjectileCollision>();
        }

        if (projectileVisual == null)
        {
            projectileVisual =
                GetComponent<ProjectileVisual>();
        }

        if (projectileVisual == null)
        {
            projectileVisual =
                gameObject.AddComponent<
                    ProjectileVisual>();
        }

        projectileVisual.Initialize();

        if (Application.isPlaying)
        {
            projectileVisual.Hide();
        }
    }


    public override void Spawned()
    {
        _presentationShown =
            false;

        _predictionWeapon =
            null;

        _localPrediction =
            null;

        projectileVisual.Hide();

        TryResolvePresentation();

        _visibleImpactSequence =
            ImpactSequence;
    }


    public override void Render()
    {
        TryResolvePresentation();

        if (_visibleImpactSequence ==
            ImpactSequence)
        {
            return;
        }

        _visibleImpactSequence =
            ImpactSequence;

        CameraShakeService.Play(
            impactShakeProfile,
            LastImpactPosition);

        if (despawnOnImpact)
        {
            DespawnProjectile();
            return;
        }
    }


    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        if (_predictionWeapon != null &&
            _localPrediction != null)
        {
            _predictionWeapon
                .ReleasePredictedProjectile(
                    _localPrediction);
        }

        _predictionWeapon =
            null;

        _localPrediction =
            null;

        if (projectileVisual != null)
        {
            projectileVisual.Hide();
            projectileVisual.Complete();
        }
    }


    /*public virtual void Initialize(
        NetworkRunner runner,
        NetworkObject source,
        Vector2 direction,
        float attackDamageMultiplier = 1f)
    {
        Initialize(
            runner,
            source,
            direction,
            new ProjectileLaunchSettings(
                initialSpeed,
                lifetime),
            attackDamageMultiplier);
    }*/

    public virtual void Initialize(
        NetworkRunner runner,
        /*NetworkObject source,
        Vector2 direction,*/
        ProjectileShotContext shot,
        ProjectileLaunchSettings launchSettings,
        Vector2? optionalDir = null,
        ProjectilePredictionKey predictionKey = default)
    {
        if (!HasStateAuthority)
            return;

        Vector2 direction =
            ProjectileTrajectory.NormalizeDirection(
                optionalDir ??
                shot.LaunchPose.Direction);

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

        Source =
            shot.Source;

        PresentationOrigin =
            transform.position;

        PresentationScaleMultiplier =
            shot.Stats.ScaleMultiplier;

        Velocity =
            direction *
            launchSettings.Speed;

        _runtimeLaunchSettings =
            launchSettings;

        _projectileCollision.Initialize(
            transform,
            Object,
            Source,
            collisionMask,
            launchSettings.CollisionRadius);

        PredictionEmitterId =
            predictionKey.EmitterId;

        PredictionFireTick =
            predictionKey.FireTick;

        PredictionFireSequence =
            predictionKey.FireSequence;

        PredictionProjectileIndex =
            predictionKey.ProjectileIndex;

        Damage =
            attack != null
                ? DamageInfo.ResolveAttackDamage(
                    attack.Damage,
                    shot.AttackDamageMultiplier)
                : 0;

        LocalKnockback =
            ToLocalDirectionSpace(
                knockback,
                direction);

        CrowdControlDefinition crowdControl =
            attack != null
                ? attack.CrowdControl
                : default;

        CrowdControlType =
            crowdControl.Type;

        CrowdControlDuration =
            crowdControl.Duration;

        CrowdControlActivationDelay =
            crowdControl.ActivationDelay;

        StopMovementOnApply =
            crowdControl.StopMovementOnApply;

        LifeTimer =
            launchSettings.Lifetime > 0f
                ? TickTimer.CreateFromSeconds(
                    runner,
                    launchSettings.Lifetime)
                : TickTimer.None;

        IsInitialized =
            true;

        ApplyRotationFromVelocity();
    }


    private void TryResolvePresentation()
    {
        if (_presentationShown ||
            !IsInitialized ||
            projectileVisual == null ||
            !IsRenderInterpolationReady())
        {
            return;
        }

        if (TryBindLocalPrediction())
        {
            _presentationShown =
                true;

            return;
        }

        float scaleMultiplier =
            PresentationScaleMultiplier > 0f
                ? PresentationScaleMultiplier
                : 1f;

        ProjectileStatSnapshot stats =
            new(
                1f,
                1f,
                1f,
                1f,
                scaleMultiplier,
                1f);

        projectileVisual.Apply(
            in stats);

        _presentationShown =
            true;

        Vector3 trailOrigin =
            HasStateAuthority
                ? PresentationOrigin
                : transform.position;

        projectileVisual.Show(
            trailOrigin,
            transform);
    }


    private bool TryBindLocalPrediction()
    {
        if (HasStateAuthority ||
            Runner == null)
        {
            return false;
        }

        ProjectilePredictionKey key =
            new(
                PredictionEmitterId,
                PredictionFireTick,
                PredictionFireSequence,
                PredictionProjectileIndex);

        if (!key.IsValid ||
            !Runner.TryFindObject(
                key.EmitterId,
                out NetworkObject emitter) ||
            !emitter.TryGetComponent(
                out ProjectileWeapon weapon) ||
            !weapon.HasInputAuthority)
        {
            return false;
        }

        if (!weapon.TryBindPredictedProjectile(
                in key,
                this,
                out PredictedProjectile predicted))
        {
            return false;
        }

        _predictionWeapon =
            weapon;

        _localPrediction =
            predicted;

        return true;
    }


    private bool IsRenderInterpolationReady()
    {
        if (HasStateAuthority)
            return true;

        return TryGetSnapshotsBuffers(
            out _,
            out _,
            out _);
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

        if (HasImpacted && !despawnOnImpact)
        {
            if (ImpactDespawnTimer.Expired(
                    Runner))
            {
                DespawnProjectile();
            }

            return;
        }

        SimulateBallisticMovement();
    }


    private void SimulateBallisticMovement()
    {
        float tickDeltaTime =
            Runner.DeltaTime;

        int stepCount =
            ProjectileTrajectory
                .CalculateStepCount(
                    Velocity,
                    in _runtimeLaunchSettings,
                    tickDeltaTime,
                    maxSimulationStepDistance,
                    maxSimulationStepsPerTick);

        float stepDeltaTime =
            tickDeltaTime /
            stepCount;

        for (int i = 0;
             i < stepCount;
             i++)
        {
            Vector2 position =
                transform.position;

            Vector2 velocity =
                Velocity;

            ProjectileTrajectory.Step(
                ref position,
                ref velocity,
                in _runtimeLaunchSettings,
                stepDeltaTime);

            Velocity =
                velocity;

            if (!SimulateMovementStep(
                    position -
                    (Vector2)transform.position))
            {
                return;
            }
        }

        ApplyRotationFromVelocity();
    }


    private bool SimulateMovementStep(
        Vector2 displacement)
    {
        if (displacement.sqrMagnitude <=
            0.00000001f)
        {
            return true;
        }

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

        if (_projectileCollision.TrySweep(
                start,
                displacement,
                out RaycastHit2D hit))
        {
            Vector2 direction =
                displacement.normalized;

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


    protected virtual void OnImpact(
        RaycastHit2D hit)
    {
        TryApplyDamage(
            hit.collider);

        LastImpactPosition =
            transform.position;

        HasImpacted =
            true;

        ImpactSequence++;

        StopProjectile();

        ImpactDespawnTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                impactPresentationDuration);
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
                new CrowdControlDefinition(
                    CrowdControlType,
                    CrowdControlDuration,
                    CrowdControlActivationDelay,
                    StopMovementOnApply));

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
        if (!_runtimeLaunchSettings
                .AlignRotationToVelocity)
            return;

        Vector2 direction =
            ProjectileTrajectory.NormalizeDirection(
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
