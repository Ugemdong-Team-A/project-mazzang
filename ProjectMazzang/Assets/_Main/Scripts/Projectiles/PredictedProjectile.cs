using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PredictedProjectile : MonoBehaviour
{
    private const float HandoffCorrectionDuration =
        0.1f;

    private ProjectileVisual visual;

    private ProjectileLaunchSettings _settings;
    private ProjectilePredictionKey _key;
    private ProjectileWeapon _owner;
    private Projectile _authoritativeProjectile;
    private Vector2 _velocity;
    private Vector2 _handoffOffset;
    private float _elapsedTime;
    private float _handoffElapsedTime;
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

        _handoffOffset =
            Vector2.zero;

        _handoffElapsedTime =
            0f;

        Vector2 direction =
            ProjectileTrajectory
                .NormalizeDirection(
                    launch.Direction);

        if (direction == Vector2.zero)
            direction = Vector2.right;

        transform.position =
            launch.Origin;

        _settings =
            launch.Settings;

        _velocity =
            direction *
            launch.Settings.Speed;

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

        if (_authoritativeProjectile != null)
        {
            FollowAuthoritativeProjectile(
                deltaTime);

            return;
        }

        _elapsedTime +=
            deltaTime;

        if (_settings.Lifetime > 0f &&
            _elapsedTime >=
            _settings.Lifetime)
        {
            ReturnToOwner();

            return;
        }

        Vector2 position =
            transform.position;

        ProjectileTrajectory.Step(
            ref position,
            ref _velocity,
            in _settings,
            deltaTime);

        transform.position =
            position;

        ApplyRotation();
    }


    public bool Follow(
        Projectile authoritativeProjectile)
    {
        if (!_initialized ||
            authoritativeProjectile == null)
        {
            return false;
        }

        _authoritativeProjectile =
            authoritativeProjectile;

        _handoffOffset =
            (Vector2)transform.position -
            (Vector2)authoritativeProjectile
                .transform.position;

        _handoffElapsedTime =
            0f;

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

        _handoffElapsedTime =
            0f;

        _handoffOffset =
            Vector2.zero;

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


    private void FollowAuthoritativeProjectile(
        float deltaTime)
    {
        if (_authoritativeProjectile == null)
        {
            ReturnToOwner();
            return;
        }

        _handoffElapsedTime +=
            deltaTime;

        float progress =
            HandoffCorrectionDuration > 0f
                ? Mathf.Clamp01(
                    _handoffElapsedTime /
                    HandoffCorrectionDuration)
                : 1f;

        float easedProgress =
            progress *
            progress *
            (3f - 2f * progress);

        Vector2 remainingOffset =
            Vector2.Lerp(
                _handoffOffset,
                Vector2.zero,
                easedProgress);

        transform.position =
            (Vector2)_authoritativeProjectile
                .transform.position +
            remainingOffset;

        transform.rotation =
            _authoritativeProjectile
                .transform.rotation;
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
