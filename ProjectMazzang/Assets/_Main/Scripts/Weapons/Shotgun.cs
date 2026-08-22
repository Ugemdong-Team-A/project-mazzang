using Fusion;
using UnityEngine;

public sealed class Shotgun : Weapon
{
    [Header("Shotgun")]
    [Min(1)]
    [SerializeField]
    private int magazineSize = 6;

    [Min(0f)]
    [SerializeField]
    private float fireInterval = 0.8f;

    [Min(1)]
    [SerializeField]
    private int pelletCount = 4;

    [Min(0f)]
    [SerializeField]
    private float spreadAngle = 30f;


    [Header("Muzzle")]
    [SerializeField]
    private Transform muzzle;


    [Header("Projectile")]
    [SerializeField]
    private NetworkObject projectilePrefab;

    [Min(0.01f)]
    [SerializeField]
    private float projectileSpeed = 20f;

    [Min(0.01f)]
    [SerializeField]
    private float projectileLifetime = 1.5f;

    [Min(0)]
    [SerializeField]
    private int damage = 5;

    [SerializeField]
    private Vector2 knockback =
        new Vector2(5f, 1.5f);

    [Min(0f)]
    [SerializeField]
    private float knockbackControlLock = 0.08f;


    [Networked]
    public int Ammo
    {
        get;
        private set;
    }

    [Networked]
    private TickTimer FireCooldown
    {
        get;
        set;
    }


    public override void Spawned()
    {
        base.Spawned();

        if (!HasStateAuthority)
            return;

        Ammo = magazineSize;
        FireCooldown = TickTimer.None;
    }


    public override bool TryUse(
        Vector2 origin,
        Vector2 direction,
        bool mirrored)
    {
        if (!HasStateAuthority)
            return false;

        if (!IsEquipped)
            return false;

        if (Holder == null)
            return false;

        if (Ammo <= 0)
            return false;

        if (!FireCooldown.ExpiredOrNotRunning(Runner))
            return false;

        if (projectilePrefab == null)
            return false;


        direction = ResolveShotDirection(direction);

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;


        Vector2 spawnPosition =
            ResolveMuzzlePosition(
                origin,
                angle,
                mirrored);


        NetworkObject source = Holder;


        for (int i = 0; i < pelletCount; i++)
        {
            float pelletAngle =
                CalculatePelletAngle(i);

            Vector2 pelletDirection =
                RotateVector(
                    direction,
                    pelletAngle);


            Quaternion rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Atan2(
                        pelletDirection.y,
                        pelletDirection.x) *
                    Mathf.Rad2Deg);


            Vector2 projectileVelocity =
                pelletDirection *
                projectileSpeed;


            Vector2 projectileKnockback =
                ResolveKnockback(
                    pelletDirection);


            Runner.Spawn(
                projectilePrefab,
                spawnPosition,
                rotation,
                source.InputAuthority,
                (runner, obj) =>
                {
                    Projectile projectile =
                        obj.GetComponent<Projectile>();

                    if (projectile == null)
                        return;

                    projectile.Initialize(
                        runner,
                        source,
                        projectileVelocity,
                        projectileLifetime,
                        damage,
                        projectileKnockback,
                        knockbackControlLock);
                });
        }


        Ammo--;

        FireCooldown =
            fireInterval > 0f
                ? TickTimer.CreateFromSeconds(
                    Runner,
                    fireInterval)
                : TickTimer.None;


        return true;
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


    private Vector2 ResolveShotDirection(
        Vector2 direction)
    {
        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return Vector2.right;
        }

        return direction.normalized;
    }


    private Vector2 ResolveMuzzlePosition(
        Vector2 weaponPosition,
        float weaponAngle,
        bool mirrored)
    {
        if (muzzle == null)
            return weaponPosition;

        Vector2 muzzleOffset =
            muzzle.localPosition;

        if (mirrored)
        {
            muzzleOffset.y =
                -muzzleOffset.y;
        }

        return
            weaponPosition +
            RotateVector(
                muzzleOffset,
                weaponAngle);
    }


    private Vector2 ResolveKnockback(
        Vector2 direction)
    {
        Vector2 perpendicular =
            new Vector2(
                -direction.y,
                direction.x);

        return
            direction * knockback.x +
            perpendicular * knockback.y;
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