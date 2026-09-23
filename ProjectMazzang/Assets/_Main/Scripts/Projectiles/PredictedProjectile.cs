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
        WeaponFirePose firePose,
        ProjectileLaunchSettings settings,
        Vector2? optionalDir = null
        )
    {
        Vector2 direction =
            ProjectileTrajectory
                .NormalizeDirection(
                    optionalDir ??
                    firePose.Direction);

        if (direction == Vector2.zero)
            direction = Vector2.right;

        transform.position =
            firePose.Origin;

        _settings =
            settings;

        _velocity =
            direction *
            settings.Speed;

        _elapsedTime =
            0f;

        _initialized =
            true;

        ApplyRotation();

        if (visual != null)
        {
            visual.Initialize();
        }

        if (trail != null)
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
