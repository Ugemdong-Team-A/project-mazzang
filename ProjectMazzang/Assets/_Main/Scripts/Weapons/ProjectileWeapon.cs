using Fusion;
using UnityEngine;

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

        FireSequence++;
    }

    private ProjectileLaunchPose ResolveLaunchPose(
        Vector2 origin,
        Vector2 direction,
        bool mirrored)
    {
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

        return new ProjectileLaunchPose(
            spawnPosition,
            direction);
    }

    private ProjectileStatSnapshot ResolveStatSnapshot(
        float damageMultiplier = 1f,
        float speedMultiplier = 1f,
        float lifetimeMultiplier = 1f,
        float knockbackMultiplier = 1f,
        float scaleMultiplier = 1f,
        float gravityMultiplier = 1f)
    {
        return new ProjectileStatSnapshot(
            damageMultiplier,
            speedMultiplier,
            lifetimeMultiplier,
            knockbackMultiplier,
            scaleMultiplier,
            gravityMultiplier);
    }

    private ProjectileShotContext BuildShotContext(
        in ProjectileLaunchPose launchPose,
        in ProjectileStatSnapshot stats)
    {
        NetworkObject source =
            Holder;

        return new ProjectileShotContext(
            source,
            launchPose,
            stats,
            Runner.Tick,
            FireSequence);
    }

    protected static ProjectilePredictionKey BuildPredictionKey(
        in ProjectileShotContext shot,
        int projectileIndex)
    {
        return new ProjectilePredictionKey(
            shot.Source.Id,
            shot.FireTick,
            shot.FireSequence,
            projectileIndex);
    }

    protected virtual ProjectileLaunchPlan BuildLaunchPlan(
        in ProjectileShotContext shot,
        in ProjectileBaseSettings projectileSettings)
    {
        ProjectileStatSnapshot stats =
            shot.Stats;

        ProjectileLaunchSettings settings =
            projectileSettings.Resolve(
                in stats);

        ProjectilePredictionKey key =
            BuildPredictionKey(
                in shot,
                0);

        ProjectileLaunch launch =
            new(
                key,
                shot.LaunchPose,
                settings,
                stats);

        return new ProjectileLaunchPlan(
            launch);
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

        if (projectilePrefab == null ||
            !projectilePrefab.TryGetComponent<Projectile>
                (out Projectile projectile))
        {
            return false;
        }

        ApplyFireStates();

        ProjectileLaunchPose launchPose =
            ResolveLaunchPose(
                origin,
                direction,
                mirrored);

        ProjectileStatSnapshot statSnapshot =
            ResolveStatSnapshot(
                attackDamageMultiplier);

        ProjectileShotContext shot =
            BuildShotContext(
                launchPose,
                statSnapshot);

        ProjectileBaseSettings projectileSettings =
            projectile.BaseSettings;

        ProjectileLaunchPlan launchPlan =
            BuildLaunchPlan(
                in shot,
                in projectileSettings);

        /*if (testPredictedProjectile)
        {
            TestPredictProjectile(
                shot,
                projectile.LaunchSettings);

            return true;
        }*/

        // 로컬 예측
        if (HasInputAuthority &&
            Runner.IsForward)
        {
            PlayLocalFirePresentation();
        }

        // 호스트 판정
        if (!HasStateAuthority)
            return true;

        // 이친구는 예측 전에도 검사할까 고민중.. 서버에선 판정 안해도 예측 시도는 열어야할까
        /*if (projectilePrefab == null)
            return false; */    

        if (!TrySpawnProjectilesAtOnce(
                shot,
                launchPlan))
        {
            return false;
        }


        LastAuthoritativeFireOrigin =
            shot.FirePose.Origin;

        LastAuthoritativeFireDirection =
            shot.FirePose.Direction;

        return true;
    }


    protected virtual bool TrySpawnProjectilesAtOnce(
        ProjectileShotContext shot,
        ProjectileLaunchPlan launchPlan)
    {
        bool spawnedAny =
            false;

        for (int i = 0;
             i < launchPlan.Count;
             i++)
        {
            ProjectileLaunch launch =
                launchPlan[i];

            spawnedAny |=
                TrySpawnProjectile(
                    shot,
                    in launch);
        }

        return spawnedAny;
    }


    protected bool TrySpawnProjectile(
        ProjectileShotContext shot,
        in ProjectileLaunch launch)
    {
        if (testPredictedProjectile)
        {
            TestPredictProjectile(
                in launch);

            return true;
        }

        Vector2 launchOrigin =
            launch.Origin;

        Vector2 launchDirection =
            launch.Direction;

        ProjectileLaunchSettings launchSettings =
            launch.Settings;

        NetworkObject spawned =
            Runner.Spawn(
                projectilePrefab,
                launchOrigin,
                ResolveProjectileRotation(
                    launchDirection),
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
                            launchSettings,
                            launchDirection);

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
        in ProjectileLaunch launch)
    {
        if (!testPredictedPrefab) return;

        PredictedProjectile predicted = Instantiate(testPredictedPrefab);

        predicted.Initialize(
            in launch);
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
