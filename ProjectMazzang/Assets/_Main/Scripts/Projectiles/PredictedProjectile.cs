using UnityEngine;

public class PredictedProjectile : MonoBehaviour
{
    [SerializeField]
    private ProjectileVisual visual;

    [SerializeField]
    private ProjectileTrail trail;

    private ProjectileLaunchSettings _settings;
    private Vector2 _velocity;
    private float _elapsedTime;
    private bool _initialized;

    public void Initialize(
        in ProjectileLaunch launch)
    {
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
        else if (trail != null)
        {
            trail.Begin(
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
            Destroy(
                gameObject);

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


    private void OnDestroy()
    {
        if (trail != null)
        {
            trail.Complete();
        }

        if (visual != null)
        {
            visual.Complete();
        }
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
