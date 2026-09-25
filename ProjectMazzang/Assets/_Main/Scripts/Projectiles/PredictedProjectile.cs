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
    private float _simulationDeltaTime;
    private float _simulationAccumulator;
    private Vector2 _simulationPosition;
    private Vector2 _velocity;
    private float _elapsedTime;
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

        _simulationAccumulator =
            0f;

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

        if (_authoritativeProjectile == null &&
            _settings.Lifetime > 0f &&
            _elapsedTime >=
            _settings.Lifetime)
        {
            ReturnToOwner();

            return;
        }

        _simulationAccumulator +=
            deltaTime;

        while (_simulationAccumulator >=
               _simulationDeltaTime)
        {
            ProjectileTrajectory.Advance(
                ref _simulationPosition,
                ref _velocity,
                in _settings,
                _simulationDeltaTime,
                _maxSimulationStepDistance,
                _maxSimulationStepsPerTick);

            _simulationAccumulator -=
                _simulationDeltaTime;
        }

        ApplyRenderInterpolation();
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

        _simulationAccumulator =
            0f;

        _simulationPosition =
            default;

        _velocity =
            default;

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


    private void ApplyRenderInterpolation()
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

        float alpha =
            Mathf.Clamp01(
                _simulationAccumulator /
                _simulationDeltaTime);

        transform.position =
            Vector2.Lerp(
                _simulationPosition,
                nextPosition,
                alpha);

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
