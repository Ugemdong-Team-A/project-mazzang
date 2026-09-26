using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PredictedProjectile : MonoBehaviour
{
    private ProjectileVisual visual;
    private ProjectileCollision collision;

    private ProjectileLaunchSettings _settings;
    private ProjectilePredictionKey _key;
    private ProjectileWeapon _owner;
    private Projectile _authoritativeProjectile;
    private LayerMask _collisionMask;
    private float _maxSimulationStepDistance;
    private int _maxSimulationStepsPerTick;
    private bool _despawnOnImpact;
    private float _impactPresentationDuration;
    private float _simulationDeltaTime;
    private float _fallbackSimulationAccumulator;
    private int _lastSimulationTick;
    private int _simulationExpirationTick;
    private Vector2 _simulationPosition;
    private Vector2 _velocity;
    private float _elapsedTime;
    private float _impactHideTime;
    private bool _hasPredictedImpact;
    private bool _impactVisualHidden;
    private bool _initialized;

    public ProjectilePredictionKey Key =>
        _key;


    public static PredictedProjectile Create(
        Projectile projectileTemplate,
        Transform poolRoot)
    {
        if (projectileTemplate == null)
        {
            throw new System.ArgumentNullException(
                nameof(projectileTemplate));
        }

        GameObject instance =
            Instantiate(
                projectileTemplate.gameObject,
                poolRoot,
                false);

        instance.name =
            "[Local] " +
            projectileTemplate.name +
            " Prediction";

        instance.SetActive(
            false);

        DisableAuthorityComponents(
            instance);

        ProjectileVisual instanceVisual =
            instance.GetComponentInChildren<
                ProjectileVisual>(
                    true);

        if (instanceVisual == null)
        {
            instanceVisual =
                instance.AddComponent<
                    ProjectileVisual>();
        }

        instanceVisual.Initialize();

        PredictedProjectile predicted =
            instance.GetComponent<
                PredictedProjectile>();

        if (predicted == null)
        {
            predicted =
                instance.AddComponent<
                    PredictedProjectile>();
        }

        predicted.visual =
            instanceVisual;

        predicted.collision =
            instance.GetComponent<
                ProjectileCollision>();

        if (predicted.collision == null)
        {
            predicted.collision =
                instance.AddComponent<
                    ProjectileCollision>();
        }

        predicted._collisionMask =
            projectileTemplate.CollisionMask;

        predicted._maxSimulationStepDistance =
            projectileTemplate
                .MaxSimulationStepDistance;

        predicted._maxSimulationStepsPerTick =
            projectileTemplate
                .MaxSimulationStepsPerTick;

        predicted._despawnOnImpact =
            projectileTemplate
                .DespawnOnImpact;

        predicted._impactPresentationDuration =
            projectileTemplate
                .ImpactPresentationDuration;

        return predicted;
    }


    public void Play(
        ProjectileWeapon owner,
        in ProjectileLaunch launch)
    {
        _owner =
            owner;

        _key =
            launch.Key;

        _authoritativeProjectile =
            null;

        Vector2 direction =
            ProjectileTrajectory
                .NormalizeDirection(
                    launch.Direction);

        if (direction == Vector2.zero)
            direction = Vector2.right;

        _simulationPosition =
            launch.Origin;

        transform.position =
            _simulationPosition;

        _settings =
            launch.Settings;

        _velocity =
            direction *
            launch.Settings.Speed;

        _simulationDeltaTime =
            ResolveSimulationDeltaTime(
                owner);

        NetworkRunner runner =
            owner != null
                ? owner.Runner
                : null;

        _lastSimulationTick =
            runner != null
                ? runner.Tick.Raw
                : launch.Key.FireTick;

        _simulationExpirationTick =
            ResolveSimulationExpirationTick(
                runner,
                launch.Settings.Lifetime);

        _fallbackSimulationAccumulator =
            0f;

        _impactHideTime =
            0f;

        _hasPredictedImpact =
            false;

        _impactVisualHidden =
            false;

        NetworkObject ownerObject =
            GetComponent<NetworkObject>();

        NetworkObject source =
            owner != null
                ? owner.Holder
                : null;

        collision.Initialize(
            transform,
            ownerObject,
            source,
            _collisionMask,
            launch.Settings.CollisionRadius);

        _elapsedTime =
            0f;

        _initialized =
            true;

        ApplyRotation();

        gameObject.SetActive(
            true);

        if (visual != null)
        {
            ProjectileStatSnapshot stats =
                launch.Stats;

            visual.Apply(
                in stats);

            visual.Show(
                transform.position,
                transform);
        }
    }


    private void Update()
    {
        if (!_initialized)
            return;

        float deltaTime =
            Time.deltaTime;

        _elapsedTime +=
            deltaTime;

        if (_hasPredictedImpact)
        {
            if (ShouldExpirePrediction())
            {
                ReturnToOwner();
                return;
            }

            TryHideImpactVisual();
            return;
        }

        NetworkRunner runner =
            _owner != null
                ? _owner.Runner
                : null;

        if (runner != null &&
            runner.DeltaTime > 0f)
        {
            int currentTick =
                runner.Tick.Raw;

            if (!AdvanceSimulationToTick(
                    currentTick))
            {
                return;
            }

            if (IsSimulationExpired(
                    currentTick))
            {
                if (_authoritativeProjectile == null)
                {
                    ReturnToOwner();
                }

                return;
            }

            float alpha =
                Mathf.Clamp01(
                    runner.LocalAlpha);

            if (_simulationExpirationTick >= 0 &&
                currentTick + 1 >=
                _simulationExpirationTick)
            {
                alpha = 0f;
            }

            ApplyRenderInterpolation(
                alpha);

            return;
        }

        if (_authoritativeProjectile == null &&
            _settings.Lifetime > 0f &&
            _elapsedTime >=
            _settings.Lifetime)
        {
            ReturnToOwner();

            return;
        }

        _fallbackSimulationAccumulator +=
            deltaTime;

        while (_fallbackSimulationAccumulator >=
               _simulationDeltaTime)
        {
            _fallbackSimulationAccumulator -=
                _simulationDeltaTime;

            if (!SimulateTick())
            {
                _fallbackSimulationAccumulator =
                    0f;

                return;
            }
        }

        ApplyRenderInterpolation(
            Mathf.Clamp01(
                _fallbackSimulationAccumulator /
                _simulationDeltaTime));
    }


    public bool BindAuthority(
        Projectile authoritativeProjectile)
    {
        if (!_initialized ||
            authoritativeProjectile == null)
        {
            return false;
        }

        if (_authoritativeProjectile != null &&
            _authoritativeProjectile !=
            authoritativeProjectile)
        {
            return false;
        }

        _authoritativeProjectile =
            authoritativeProjectile;

        return true;
    }


    public void PrepareForPool()
    {
        _initialized =
            false;

        _owner =
            null;

        _authoritativeProjectile =
            null;

        _key =
            default;

        _elapsedTime =
            0f;

        _fallbackSimulationAccumulator =
            0f;

        _lastSimulationTick =
            0;

        _simulationExpirationTick =
            -1;

        _simulationPosition =
            default;

        _velocity =
            default;

        _impactHideTime =
            0f;

        _hasPredictedImpact =
            false;

        _impactVisualHidden =
            false;

        if (visual != null)
        {
            visual.Hide();
            visual.ResetVisual();
        }
    }


    private void OnDestroy()
    {
        if (visual != null)
        {
            visual.Complete();
        }
    }


    private void ReturnToOwner()
    {
        ProjectileWeapon owner =
            _owner;

        if (owner != null)
        {
            owner.ReleasePredictedProjectile(
                this);

            return;
        }

        Destroy(
            gameObject);
    }


    private void ApplyRotation()
    {
        if (!_settings
                .AlignRotationToVelocity)
        {
            return;
        }

        transform.rotation =
            ProjectileTrajectory
                .ResolveRotation(
                    _velocity);
    }


    private bool AdvanceSimulationToTick(
        int currentTick)
    {
        if (currentTick <
            _lastSimulationTick)
        {
            _lastSimulationTick =
                currentTick;

            return true;
        }

        int lastTickToSimulate =
            currentTick;

        if (_simulationExpirationTick >= 0)
        {
            lastTickToSimulate =
                Mathf.Min(
                    lastTickToSimulate,
                    _simulationExpirationTick - 1);
        }

        while (_lastSimulationTick <
               lastTickToSimulate)
        {
            _lastSimulationTick++;

            if (!SimulateTick())
            {
                return false;
            }
        }

        return true;
    }


    private bool ShouldExpirePrediction()
    {
        if (_authoritativeProjectile != null)
            return false;

        NetworkRunner runner =
            _owner != null
                ? _owner.Runner
                : null;

        if (runner != null &&
            _simulationExpirationTick >= 0)
        {
            return IsSimulationExpired(
                runner.Tick.Raw);
        }

        return _settings.Lifetime > 0f &&
               _elapsedTime >=
               _settings.Lifetime;
    }


    private bool IsSimulationExpired(
        int currentTick)
    {
        return _simulationExpirationTick >= 0 &&
               currentTick >=
               _simulationExpirationTick;
    }


    private void ApplyRenderInterpolation(
        float alpha)
    {
        Vector2 nextPosition =
            _simulationPosition;

        Vector2 nextVelocity =
            _velocity;

        ProjectileTrajectory.Advance(
            ref nextPosition,
            ref nextVelocity,
            in _settings,
            _simulationDeltaTime,
            _maxSimulationStepDistance,
            _maxSimulationStepsPerTick);

        Vector2 renderPosition =
            Vector2.Lerp(
                _simulationPosition,
                nextPosition,
                alpha);

        if (TryResolvePredictedImpact(
                _simulationPosition,
                renderPosition -
                _simulationPosition))
        {
            return;
        }

        transform.position =
            renderPosition;

        if (!_settings
                .AlignRotationToVelocity)
        {
            return;
        }

        transform.rotation =
            ProjectileTrajectory
                .ResolveRotation(
                    Vector2.Lerp(
                        _velocity,
                        nextVelocity,
                        alpha));
    }


    private bool SimulateTick()
    {
        int stepCount =
            ProjectileTrajectory
                .CalculateStepCount(
                    _velocity,
                    in _settings,
                    _simulationDeltaTime,
                    _maxSimulationStepDistance,
                    _maxSimulationStepsPerTick);

        float stepDeltaTime =
            _simulationDeltaTime /
            stepCount;

        for (int i = 0;
             i < stepCount;
             i++)
        {
            Vector2 start =
                _simulationPosition;

            Vector2 nextPosition =
                start;

            Vector2 nextVelocity =
                _velocity;

            ProjectileTrajectory.Step(
                ref nextPosition,
                ref nextVelocity,
                in _settings,
                stepDeltaTime);

            _velocity =
                nextVelocity;

            Vector2 displacement =
                nextPosition -
                start;

            if (TryResolvePredictedImpact(
                    start,
                    displacement))
            {
                return false;
            }

            _simulationPosition =
                nextPosition;
        }

        return true;
    }


    private bool TryResolvePredictedImpact(
        Vector2 start,
        Vector2 displacement)
    {
        if (_hasPredictedImpact ||
            !collision.TrySweep(
                start,
                displacement,
                out RaycastHit2D hit))
        {
            return false;
        }

        Vector2 direction =
            displacement.normalized;

        _simulationPosition =
            start +
            direction *
            hit.distance;

        transform.position =
            _simulationPosition;

        if (_settings
                .AlignRotationToVelocity)
        {
            transform.rotation =
                ProjectileTrajectory
                    .ResolveRotation(
                        direction);
        }

        _velocity =
            Vector2.zero;

        _hasPredictedImpact =
            true;

        _impactHideTime =
            _elapsedTime +
            (_despawnOnImpact
                ? 0f
                : Mathf.Max(
                    0f,
                    _impactPresentationDuration));

        if (visual != null)
        {
            visual.Complete();
        }

        TryHideImpactVisual();

        return true;
    }


    private void TryHideImpactVisual()
    {
        if (_impactVisualHidden ||
            _elapsedTime <
            _impactHideTime)
        {
            return;
        }

        _impactVisualHidden =
            true;

        if (visual != null)
        {
            visual.Hide();
        }
    }


    private static float ResolveSimulationDeltaTime(
        ProjectileWeapon owner)
    {
        if (owner != null &&
            owner.Runner != null &&
            owner.Runner.DeltaTime > 0f)
        {
            return owner.Runner.DeltaTime;
        }

        return Mathf.Max(
            0.001f,
            Time.fixedDeltaTime);
    }


    private static int ResolveSimulationExpirationTick(
        NetworkRunner runner,
        float lifetime)
    {
        if (runner == null ||
            lifetime <= 0f)
        {
            return -1;
        }

        TickTimer timer =
            TickTimer.CreateFromSeconds(
                runner,
                lifetime);

        Tick? targetTick =
            timer.TargetTick;

        return targetTick.HasValue
            ? targetTick.Value.Raw
            : -1;
    }


    private static void DisableAuthorityComponents(
        GameObject instance)
    {
        NetworkBehaviour[] networkBehaviours =
            instance.GetComponentsInChildren<
                NetworkBehaviour>(
                    true);

        for (int i = 0;
             i < networkBehaviours.Length;
             i++)
        {
            networkBehaviours[i].enabled =
                false;
        }

        NetworkObject[] networkObjects =
            instance.GetComponentsInChildren<
                NetworkObject>(
                    true);

        for (int i = 0;
             i < networkObjects.Length;
             i++)
        {
            networkObjects[i].enabled =
                false;
        }

        Collider2D[] colliders =
            instance.GetComponentsInChildren<
                Collider2D>(
                    true);

        for (int i = 0;
             i < colliders.Length;
             i++)
        {
            colliders[i].enabled =
                false;
        }

        Rigidbody2D[] rigidbodies =
            instance.GetComponentsInChildren<
                Rigidbody2D>(
                    true);

        for (int i = 0;
             i < rigidbodies.Length;
             i++)
        {
            rigidbodies[i].simulated =
                false;
        }
    }
}
