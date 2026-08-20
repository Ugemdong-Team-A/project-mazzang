using Fusion;
using UnityEngine;

public sealed class MachineGun : Weapon
{
    [Header("Gun")]
    [Min(1)]
    [SerializeField]
    private int magazineSize = 30;

    [Min(0.01f)]
    [SerializeField]
    private float fireInterval = 0.08f;


    [Header("Muzzle")]
    [SerializeField]
    private Transform muzzle;


    [Header("Projectile")]
    [SerializeField]
    private NetworkObject projectilePrefab;

    [Min(0.01f)]
    [SerializeField]
    private float projectileSpeed = 35f;

    [Min(0.01f)]
    [SerializeField]
    private float projectileLifetime = 2f;

    [Min(0)]
    [SerializeField]
    private int damage = 8;

    [SerializeField]
    private Vector2 knockback =
        new Vector2(3f, 1f);

    [Min(0f)]
    [SerializeField]
    private float knockbackControlLock = 0.05f;


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

        if (!FireCooldown.ExpiredOrNotRunning(Runner))
            return false;

        if (projectilePrefab == null)
            return false;


        if (direction.sqrMagnitude <=
            0.0001f)
        {
            direction =
                Vector2.right;
        }

        direction =
            direction.normalized;


        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;


        Vector2 spawnPosition =
            muzzle != null
                ? (Vector2)muzzle.position
                : origin;


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
            TickTimer.CreateFromSeconds(
                Runner,
                fireInterval);


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