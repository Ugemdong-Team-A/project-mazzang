using Fusion;
using UnityEngine;
using static UnityEngine.UI.Image;

public class ProjectileWeapon :
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

    [Header("Test")]
    [SerializeField]
    bool testPredictedProjectile;

    [SerializeField]
    PredictedProjectile testPredictedPrefab;

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
            FireSequence || HasInputAuthority)
        {
            return;
        }

        _visibleFireSequence =
            FireSequence;

        PlayFireEffects();
    }


    // =========================================================
    // Fire
    // =========================================================

    protected virtual bool CanFire()
    {
        return
            IsEquipped &&
            Holder != null &&
            Ammo > 0 &&
            FireCooldown
                .ExpiredOrNotRunning(
                    Runner);
    }

    protected virtual void ApplyFireStates()
    {
        Ammo--;

        FireCooldown =
            fireInterval > 0f
                ? TickTimer.CreateFromSeconds(
                    Runner,
                    fireInterval)
                : TickTimer.None;

        // Host만 바꾸고, Peer가 사용할수도
        // FireSequence++;
    }

    // 받은 값으로 바로 Context를 만들지, Resolve 메서드들로 해석해서 만들지 고민인데 일단 후자로 ㄱㄱ
    protected virtual ProjectileShotContext ResolveShot(
        Vector2 origin,
        Vector2 direction,
        bool mirrored,
        float attackDamageMultiplier)
    {
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


        return new ProjectileShotContext(
            source,
            new WeaponFirePose(
                spawnPosition,
                direction),
            attackDamageMultiplier);
    }

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
        if(!CanFire()) 
            return false;

        ApplyFireStates();

        if (projectilePrefab == null ||
            !projectilePrefab.TryGetComponent<Projectile>
                (out Projectile projectile)) return false;

        ProjectileShotContext shot =
            ResolveShot(
                origin,
                direction,
                mirrored,
                attackDamageMultiplier);

        /*if (testPredictedProjectile)
        {
            TestPredictProjectile(
                shot,
                projectile.LaunchSettings);

            return true;
        }*/

        // 로컬 예측
        if (!HasStateAuthority &&
            HasInputAuthority && Runner.IsForward)
        {
            PlayLocalFirePresentation();
            return true;
        }

        // 호스트 판정
        if (!HasStateAuthority)
            return false;

        // 이친구는 예측 전에도 검사할까 고민중.. 서버에선 판정 안해도 예측 시도는 열어야할까
        /*if (projectilePrefab == null)
            return false; */    

        if (!TrySpawnProjectilesAtOnce(
                shot/*,
                projectile.LaunchSettings*/))
        {
            return false;
        }


        LastAuthoritativeFireOrigin =
            shot.FirePose.Origin;

        LastAuthoritativeFireDirection =
            shot.FirePose.Direction;

        FireSequence++;

        return true;
    }


    protected virtual bool TrySpawnProjectilesAtOnce(
        ProjectileShotContext shot)
    {
        return TrySpawnProjectile(
            shot,
            projectilePrefab.GetComponent<Projectile>().LaunchSettings);
    }


    protected bool TrySpawnProjectile(
        ProjectileShotContext shot,
        ProjectileLaunchSettings launchSettings,
        Vector2? optionalDir = null)
    {
        if (testPredictedProjectile)
        {
            TestPredictProjectile(
                shot,
                projectilePrefab.GetComponent<Projectile>().LaunchSettings,
                optionalDir);

            return true;
        }

        NetworkObject spawned =
            Runner.Spawn(
                projectilePrefab,
                shot.FirePose.Origin,
                ResolveProjectileRotation(
                    optionalDir.HasValue ?
                    optionalDir.Value :
                    shot.FirePose.Direction),
                shot.Source.InputAuthority,
                (runner, obj) =>
                {
                    Projectile projectile =
                        obj.GetComponent<Projectile>();

                    if (projectile == null)
                        return;

                    projectile.Initialize(
                            runner,
                            shot,
                            launchSettings);

                    /*if (launchSettings.HasValue)
                    {
                        projectile.Initialize(
                            runner,
                            shot.Source,
                            direction,
                            launchSettings.Value,
                            shot.AttackDamageMultiplier);
                    }
                    else
                    {
                        projectile.Initialize(
                            runner,
                            shot.Source,
                            direction,
                            shot.AttackDamageMultiplier);
                    }*/
                });

        return spawned != null;
    }


    private static Quaternion ResolveProjectileRotation(
        Vector2 direction)
    {
        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;

        return Quaternion.Euler(
            0f,
            0f,
            angle);
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

    protected virtual void PlayLocalFirePresentation()
    {
        PlayFireEffects();        
    }

    private void TestPredictProjectile(
        ProjectileShotContext shot,
        ProjectileLaunchSettings launchSettings,
        Vector2? optionalDir = null)
    {
        if (!testPredictedPrefab) return;

        PredictedProjectile predicted = Instantiate(testPredictedPrefab);

        predicted.Initialize(
            shot.FirePose,
            launchSettings,
            optionalDir);
    }

    /// <summary>
    /// 카메라 흔들림, 총구 이펙트, SFX 재생
    /// </summary>
    private void PlayFireEffects()
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
