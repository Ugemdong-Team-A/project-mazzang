using Fusion;
using UnityEngine;

public sealed class ProjectileGun :
    Weapon
{
    [Header("Primary Action")]
    [SerializeField]
    private WeaponActionData primaryAction =
        new();

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


    [Header("Presentation")]
    [SerializeField]
    private CameraShakeProfile fireShakeProfile;

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

    public override WeaponActionData GetAction(
        WeaponButton button,
        WeaponAttackSlot slot)
    {
        return button == WeaponButton.Primary
            ? primaryAction
            : null;
    }

    public override bool TryUse(
        Vector2 origin,
        Vector2 direction,
        WeaponAttackSlot slot,
        bool mirrored,
        float attackDamageMultiplier)
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

        WeaponFirePose firePose =
            ResolveMuzzlePose(
                origin,
                direction,
                mirrored);

        direction =
            firePose.Direction;

        Vector2 spawnPosition =
            firePose.Origin;

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;

        LastAuthoritativeFireOrigin =
            spawnPosition;

        LastAuthoritativeFireDirection =
            direction;

        Quaternion rotation =
            Quaternion.Euler(
                0f,
                0f,
                angle);

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
                        direction,
                        attackDamageMultiplier);
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


    // =========================================================
    // Presentation
    // =========================================================

    private void PlayFirePresentation()
    {
        CameraShakeService.Play(
            fireShakeProfile,
            LastAuthoritativeFireOrigin);

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
        Transform muzzle =
            HeldView != null &&
            HeldView.Muzzle != null
                ? HeldView.Muzzle
                : PresentationTemplate != null
                    ? PresentationTemplate.Muzzle
                    : null;

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
            (Vector3)(
                ResolveVisualForward(
                    muzzle) * 0.4f));

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
