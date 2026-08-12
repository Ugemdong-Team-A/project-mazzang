using Fusion;
using UnityEngine;

public sealed class ProjectileGun :
    Weapon
{
    [Header("Gun")]
    [Min(1)]
    [SerializeField]
    private int magazineSize = 12;

    [Min(0f)]
    [SerializeField]
    private float fireInterval = 0.2f;


    [Header("Projectile")]
    [SerializeField]
    private NetworkObject projectilePrefab;

    [Min(0.01f)]
    [SerializeField]
    private float projectileSpeed = 24f;

    [Min(0.01f)]
    [SerializeField]
    private float projectileLifetime = 2f;

    [Min(0f)]
    [SerializeField]
    private float muzzleDistance = 0.55f;

    [Min(0)]
    [SerializeField]
    private int damage = 12;

    [SerializeField]
    private Vector2 knockback =
        new Vector2(5f, 1.5f);

    [Min(0f)]
    [SerializeField]
    private float knockbackControlLock = 0.08f;


    [Header("Presentation")]
    [SerializeField]
    private ParticleSystem muzzleFlash;

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip fireClip;

    private int _visibleFireSequence;


    // =========================================================
    // Network State
    // =========================================================

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

    [Networked]
    private int FireSequence
    {
        get;
        set;
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        base.Spawned();

        _visibleFireSequence =
            FireSequence;

        if (!HasStateAuthority)
            return;

        Ammo =
            magazineSize;

        FireCooldown =
            TickTimer.None;
    }


    public override void Render()
    {
        if (_visibleFireSequence ==
            FireSequence)
        {
            return;
        }

        _visibleFireSequence =
            FireSequence;

        PlayFirePresentation();
    }


    // =========================================================
    // Fire
    // =========================================================

    public override bool TryUse(
        PlayerRef attacker,
        Vector2 origin,
        Vector2 direction)
    {
        if (!HasStateAuthority)
            return false;

        if (!IsEquipped)
            return false;

        if (Ammo <= 0)
            return false;

        if (!FireCooldown
                .ExpiredOrNotRunning(
                    Runner))
        {
            return false;
        }

        if (projectilePrefab == null)
            return false;

        if (direction.sqrMagnitude <=
            0.0001f)
        {
            direction =
                Vector2.right;
        }
        else
        {
            direction.Normalize();
        }

        Vector2 spawnPosition =
            origin +
            direction *
            muzzleDistance;

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;

        Quaternion rotation =
            Quaternion.Euler(
                0f,
                0f,
                angle);

        Vector2 projectileVelocity =
            direction *
            projectileSpeed;

        Vector2 projectileKnockback =
            ResolveKnockback(
                direction);

        NetworkObject spawned =
            Runner.Spawn(
                projectilePrefab,
                spawnPosition,
                rotation,
                attacker,
                (runner, obj) =>
                {
                    BulletProjectile projectile =
                        obj.GetComponent<
                            BulletProjectile>();

                    if (projectile == null)
                        return;

                    projectile.Initialize(
                        runner,
                        projectileVelocity,
                        projectileLifetime,
                        damage,
                        projectileKnockback,
                        knockbackControlLock);
                });

        if (spawned == null)
            return false;

        Ammo--;

        FireCooldown =
            fireInterval > 0f
                ? TickTimer.CreateFromSeconds(
                    Runner,
                    fireInterval)
                : TickTimer.None;

        FireSequence++;

        return true;
    }


    private Vector2 ResolveKnockback(
        Vector2 direction)
    {
        Vector2 perpendicular =
            new Vector2(
                -direction.y,
                direction.x);

        return
            direction *
            knockback.x +
            perpendicular *
            knockback.y;
    }


    // =========================================================
    // Presentation
    // =========================================================

    private void PlayFirePresentation()
    {
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }

        if (audioSource != null &&
            fireClip != null)
        {
            audioSource.PlayOneShot(
                fireClip);
        }
    }
}
