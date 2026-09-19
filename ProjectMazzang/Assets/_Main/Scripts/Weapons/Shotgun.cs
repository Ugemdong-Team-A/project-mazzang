using Fusion;
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


    protected override bool TrySpawnProjectiles(
        ProjectileShotContext shot)
    {
        bool spawnedAny =
            false;

        for (int i = 0;
             i < pelletCount;
             i++)
        {
            Vector2 pelletDirection =
                RotateVector(
                    shot.FirePose.Direction,
                    CalculatePelletAngle(i));

            Vector2 projectileVelocity =
                pelletDirection *
                projectileSpeed;

            NetworkObject spawned =
                Runner.Spawn(
                    ProjectilePrefab,
                    shot.FirePose.Origin,
                    ResolveProjectileRotation(
                        pelletDirection),
                    shot.Source.InputAuthority,
                    (runner, obj) =>
                    {
                        Projectile projectile =
                            obj.GetComponent<Projectile>();

                        projectile?.Initialize(
                            runner,
                            shot.Source,
                            projectileVelocity,
                            shot.AttackDamageMultiplier);
                    });

            spawnedAny |=
                spawned != null;
        }

        return spawnedAny;
    }


    private float CalculatePelletAngle(
        int index)
    {
        if (pelletCount <= 1)
            return 0f;

        float t =
            (float)index /
            (pelletCount - 1);

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
