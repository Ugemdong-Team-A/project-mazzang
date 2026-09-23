using UnityEngine;

public sealed class Shotgun :
    ProjectileWeapon
{
    [Header("Shotgun")]
    [Min(1)]
    [SerializeField]
    private int pelletCount = 4;

    [Min(0f)]
    [SerializeField]
    private float spreadAngle = 30f;

    [Min(0.01f)]
    [SerializeField]
    private float projectileSpeed = 20f;

    [Min(0.01f)]
    [SerializeField]
    private float projectileLifetime = 1.5f;


    protected override ProjectileLaunchPlan BuildLaunchPlan(
        in ProjectileShotContext shot,
        in ProjectileBaseSettings projectileSettings)
    {
        ProjectileStatSnapshot stats =
            shot.Stats;

        ProjectileLaunchSettings baseLaunchSettings =
            projectileSettings.Resolve(
                in stats);

        ProjectileLaunchSettings launchSettings =
            new(
                projectileSpeed *
                stats.SpeedMultiplier,

                projectileLifetime *
                stats.LifetimeMultiplier,

                baseLaunchSettings.GravityScale,
                baseLaunchSettings.GravityAcceleration,
                baseLaunchSettings.LinearDrag,
                baseLaunchSettings.AlignRotationToVelocity,
                baseLaunchSettings.CollisionRadius);

        ProjectileVisualSnapshot visual =
            ResolveProjectileVisual(
                in stats);

        int count =
            Mathf.Max(
                1,
                pelletCount);

        ProjectileLaunch[] launches =
            new ProjectileLaunch[count];

        for (int i = 0;
             i < count;
             i++)
        {
            Vector2 pelletDirection =
                RotateVector(
                    shot.LaunchPose.Direction,
                    CalculatePelletAngle(
                        i,
                        count));

            ProjectileLaunchPose pose =
                new(
                    shot.LaunchPose.Origin,
                    pelletDirection);

            ProjectilePredictionKey key =
                BuildPredictionKey(
                    in shot,
                    i);

            launches[i] =
                new ProjectileLaunch(
                    key,
                    pose,
                    launchSettings,
                    visual);
        }

        return new ProjectileLaunchPlan(
            launches);
    }


    private float CalculatePelletAngle(
        int index,
        int count)
    {
        if (count <= 1)
            return 0f;

        float t =
            (float)index /
            (count - 1);

        return Mathf.Lerp(
            -spreadAngle * 0.5f,
             spreadAngle * 0.5f,
             t);
    }


    private static Vector2 RotateVector(
        Vector2 value,
        float angle)
    {
        float radians =
            angle * Mathf.Deg2Rad;

        float cos =
            Mathf.Cos(radians);

        float sin =
            Mathf.Sin(radians);

        return new Vector2(
            value.x * cos -
            value.y * sin,

            value.x * sin +
            value.y * cos);
    }
}
