using Fusion;
using System.Collections.Generic;
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

    private int _visibleFireSequence;

    private GameObjectPool<PredictedProjectile>
        _predictionPool;

    private Transform _predictionPoolRoot;

    private readonly Dictionary<
        ProjectilePredictionKey,
        PredictedProjectile> _activePredictions =
            new();

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
        if (HasInputAuthority)
        {
            _visibleFireSequence =
                FireSequence;

            return;
        }

        if (_visibleFireSequence ==
            FireSequence)
        {
            return;
        }

        _visibleFireSequence =
            FireSequence;

        PlayFireEffects();
    }


    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        ClearPredictedProjectiles();

        base.Despawned(
            runner,
            hasState);
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

    protected ProjectilePredictionKey BuildPredictionKey(
        in ProjectileShotContext shot,
        int projectileIndex)
    {
        NetworkId emitterId =
            Object != null
                ? Object.Id
                : shot.Source.Id;

        return new ProjectilePredictionKey(
            emitterId,
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
        if ((!HasStateAuthority &&
             !HasInputAuthority) ||
            !CanFire())
        {
            return false;
        }

        if (projectilePrefab == null ||
            !projectilePrefab.TryGetComponent<Projectile>
                (out Projectile projectile))
        {
            return false;
        }

        int previousAmmo =
            Ammo;

        TickTimer previousFireCooldown =
            FireCooldown;

        int previousFireSequence =
            FireSequence;

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

        if (HasInputAuthority &&
            !HasStateAuthority &&
            Runner.IsForward)
        {
            PlayLocalFirePresentation(
                in launchPose);

            SpawnPredictedProjectiles(
                projectile,
                in launchPlan);
        }

        if (!HasStateAuthority)
            return true;

        if (!TrySpawnProjectilesAtOnce(
                shot,
                launchPlan))
        {
            RestoreFireStates(
                previousAmmo,
                previousFireCooldown,
                previousFireSequence);

            return false;
        }

        if (HasInputAuthority &&
            Runner.IsForward)
        {
            PlayLocalFirePresentation(
                in launchPose);
        }

        LastAuthoritativeFireOrigin =
            shot.FirePose.Origin;

        LastAuthoritativeFireDirection =
            shot.FirePose.Direction;

        return true;
    }


    private void RestoreFireStates(
        int ammo,
        TickTimer fireCooldown,
        int fireSequence)
    {
        Ammo =
            ammo;

        FireCooldown =
            fireCooldown;

        FireSequence =
            fireSequence;
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
        Vector2 launchOrigin =
            launch.Origin;

        Vector2 launchDirection =
            launch.Direction;

        ProjectileLaunchSettings launchSettings =
            launch.Settings;

        ProjectilePredictionKey predictionKey =
            launch.Key;

        NetworkObject spawned =
            Runner.Spawn(
                projectilePrefab,
                launchOrigin,
                ResolveProjectileRotation(
                    launchDirection),
                PlayerRef.None,
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
                            launchDirection,
                            predictionKey);

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

    protected virtual void PlayLocalFirePresentation(
        in ProjectileLaunchPose launchPose)
    {
        PlayFireEffects(
            launchPose.Origin);
    }

    /// <summary>
    /// 카메라 흔들림, 총구 이펙트, SFX 재생
    /// </summary>
    private void PlayFireEffects()
    {
        PlayFireEffects(
            LastAuthoritativeFireOrigin);
    }


    private void PlayFireEffects(
        Vector2 origin)
    {
        CameraShakeService.Play(
            fireShakeProfile,
            origin);

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


    private void SpawnPredictedProjectiles(
        Projectile projectileTemplate,
        in ProjectileLaunchPlan launchPlan)
    {
        EnsurePredictionPool(
            projectileTemplate);

        for (int i = 0;
             i < launchPlan.Count;
             i++)
        {
            ProjectileLaunch launch =
                launchPlan[i];

            if (!launch.Key.IsValid ||
                _activePredictions.ContainsKey(
                    launch.Key))
            {
                continue;
            }

            PredictedProjectile predicted =
                _predictionPool.Get();

            _activePredictions.Add(
                launch.Key,
                predicted);

            predicted.Play(
                this,
                in launch);
        }
    }


    private void EnsurePredictionPool(
        Projectile projectileTemplate)
    {
        if (_predictionPool != null)
            return;

        GameObject poolRoot =
            new GameObject(
                "[Local] Projectile Prediction Pool");

        _predictionPoolRoot =
            poolRoot.transform;

        _predictionPoolRoot.SetParent(
            transform,
            false);

        poolRoot.SetActive(
            false);

        _predictionPool =
            new GameObjectPool<
                PredictedProjectile>(
                    0,
                    () =>
                        PredictedProjectile.Create(
                            projectileTemplate,
                            _predictionPoolRoot),
                    OnTakePredictedProjectile,
                    OnReturnPredictedProjectile,
                    OnDestroyPredictedProjectile);
    }


    private void OnTakePredictedProjectile(
        PredictedProjectile predicted)
    {
        predicted.transform.SetParent(
            null,
            false);
    }


    private void OnReturnPredictedProjectile(
        PredictedProjectile predicted)
    {
        predicted.PrepareForPool();

        predicted.gameObject.SetActive(
            false);

        predicted.transform.SetParent(
            _predictionPoolRoot != null
                ? _predictionPoolRoot
                : transform,
            false);

        predicted.transform.localPosition =
            Vector3.zero;

        predicted.transform.localRotation =
            Quaternion.identity;

    }


    private static void OnDestroyPredictedProjectile(
        PredictedProjectile predicted)
    {
        if (predicted != null)
        {
            Destroy(
                predicted.gameObject);
        }
    }


    internal void ReleasePredictedProjectile(
        PredictedProjectile predicted)
    {
        if (predicted == null ||
            _predictionPool == null)
        {
            return;
        }

        ProjectilePredictionKey key =
            predicted.Key;

        if (!_activePredictions.TryGetValue(
                key,
                out PredictedProjectile active) ||
            active != predicted)
        {
            return;
        }

        _activePredictions.Remove(
            key);

        _predictionPool.Release(
            predicted);
    }


    internal bool TryBindPredictedProjectile(
        in ProjectilePredictionKey key,
        Projectile authoritativeProjectile,
        out PredictedProjectile predicted)
    {
        predicted =
            null;

        if (!key.IsValid ||
            authoritativeProjectile == null ||
            _predictionPool == null ||
            !_activePredictions.TryGetValue(
                key,
                out predicted))
        {
            return false;
        }

        return predicted.BindAuthority(
            authoritativeProjectile);
    }


    internal bool TryReleasePredictedProjectile(
        in ProjectilePredictionKey key)
    {
        if (!key.IsValid ||
            !_activePredictions.TryGetValue(
                key,
                out PredictedProjectile predicted))
        {
            return false;
        }

        ReleasePredictedProjectile(
            predicted);

        return true;
    }


    private void ClearPredictedProjectiles()
    {
        foreach (PredictedProjectile predicted
                 in _activePredictions.Values)
        {
            if (predicted == null)
                continue;

            predicted.PrepareForPool();

            Destroy(
                predicted.gameObject);
        }

        _activePredictions.Clear();

        if (_predictionPool != null)
        {
            _predictionPool.Clear();
            _predictionPool =
                null;
        }

        if (_predictionPoolRoot != null)
        {
            Destroy(
                _predictionPoolRoot.gameObject);

            _predictionPoolRoot =
                null;
        }
    }


    private void OnDestroy()
    {
        ClearPredictedProjectiles();
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
