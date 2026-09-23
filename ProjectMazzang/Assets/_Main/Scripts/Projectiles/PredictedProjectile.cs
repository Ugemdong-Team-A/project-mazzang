using UnityEngine;

[DisallowMultipleComponent]
public sealed class PredictedProjectile : MonoBehaviour
{
    private ProjectileVisual visual;

    private ProjectileLaunchSettings _settings;
    private ProjectilePredictionKey _key;
    private ProjectileWeapon _owner;
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
            new GameObject(
                "[Local] " +
                projectileTemplate.name +
                " Prediction");

        instance.SetActive(
            false);

        if (poolRoot != null)
        {
            instance.transform.SetParent(
                poolRoot,
                false);
        }

        ProjectileVisual instanceVisual =
            ProjectileVisual.CreatePredictionCopy(
                projectileTemplate.gameObject,
                instance);

        PredictedProjectile predicted =
            instance.AddComponent<
                PredictedProjectile>();

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


    public void PrepareForPool()
    {
        _initialized =
            false;

        _owner =
            null;

        _key =
            default;

        _elapsedTime =
            0f;

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
}
