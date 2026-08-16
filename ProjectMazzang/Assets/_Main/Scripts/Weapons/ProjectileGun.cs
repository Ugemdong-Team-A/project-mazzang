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


    [Header("Muzzle")]
    [Tooltip(
        "WeaponRoot 기준 실제 총구 위치입니다. " +
        "WeaponRoot의 직접 자식으로 두는 것을 권장합니다.")]
    [SerializeField]
    private Transform muzzle;


    [Header("Projectile")]
    [SerializeField]
    private NetworkObject projectilePrefab;

    [Min(0.01f)]
    [SerializeField]
    private float projectileSpeed = 24f;

    [Min(0.01f)]
    [SerializeField]
    private float projectileLifetime = 2f;

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

    public Vector2 PresentationMuzzlePosition =>
        muzzle != null
            ? muzzle.position
            : transform.position;


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

    [Networked]
    private Vector2 LastAuthoritativeFireOrigin
    {
        get;
        set;
    }

    [Networked]
    private Vector2 LastAuthoritativeFireDirection
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
    Vector2 origin,
    Vector2 direction)
    {
        if (!HasStateAuthority)
            return false;

        if (!IsEquipped)
            return false;

        if (Holder == null)
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


        NetworkObject source =
            Holder;


        direction =
            ResolveShotDirection(
                direction);

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;

        Vector2 spawnPosition =
            ResolveMuzzlePosition(
                origin,
                angle);

        LastAuthoritativeFireOrigin =
            spawnPosition;

        LastAuthoritativeFireDirection =
            direction;

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
                source.InputAuthority,
                (runner, obj) =>
                {
                    Projectile projectile =
                        obj.GetComponent<
                            Projectile>();

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
        float weaponAngle)
    {
        if (muzzle == null)
        {
            return weaponPosition;
        }

        // Muzzle은 WeaponRoot의 자식 Transform으로 직접 배치한다.
        // 현재 Weapon Transform의 월드 위치를 그대로 읽지 않고,
        // 에디터에서 설정한 로컬 위치만 사용한다.
        //
        // Combat이 Weapon의 FixedUpdateNetwork보다 먼저 실행되더라도
        // 이번 Tick의 weaponPosition / weaponAngle을 기준으로
        // 정확한 총구 위치를 얻기 위함이다.
        Vector2 muzzleOffset =
            muzzle.localPosition;

        return
            weaponPosition +
            RotateVector(
                muzzleOffset,
                weaponAngle);
    }


    private static Vector2 RotateVector(
        Vector2 value,
        float angle)
    {
        float radians =
            angle *
            Mathf.Deg2Rad;

        float cos =
            Mathf.Cos(
                radians);

        float sin =
            Mathf.Sin(
                radians);

        return new Vector2(
            value.x * cos -
            value.y * sin,

            value.x * sin +
            value.y * cos);
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


#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        if (muzzle == null)
            return;

        Gizmos.color =
            Color.cyan;

        Gizmos.DrawWireSphere(
            muzzle.position,
            0.05f);

        Gizmos.DrawLine(
            muzzle.position,
            muzzle.position +
            muzzle.right * 0.4f);

        if (!Application.isPlaying ||
            Object == null)
        {
            return;
        }

        if (LastAuthoritativeFireDirection.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        Gizmos.color =
            Color.red;

        Gizmos.DrawWireSphere(
            LastAuthoritativeFireOrigin,
            0.07f);

        Gizmos.DrawLine(
            LastAuthoritativeFireOrigin,
            LastAuthoritativeFireOrigin +
            LastAuthoritativeFireDirection * 0.6f);

        Gizmos.color =
            Color.yellow;

        Gizmos.DrawLine(
            muzzle.position,
            LastAuthoritativeFireOrigin);
    }

#endif
}