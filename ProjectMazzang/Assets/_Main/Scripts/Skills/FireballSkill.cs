using Fusion;
using UnityEngine;

public sealed class FireballSkill :
    Skill,
    ICastTimeSkill,
    IRecoverySkill,
    IActionLockSkill
{
    private Vector2 _aimDirection = Vector2.right;
    private Vector2 _aimWorldPosition;
    private bool _waitingToFire;
    private FireballCastPresentation _presentation;

    private FireballSkillData FireballData =>
        (FireballSkillData)Data;

    public float CastDuration => FireballData.CastDuration;
    public float RecoveryDuration => FireballData.RecoveryDuration;

    public bool IsActionLocked(
        SkillUsePhase phase)
    {
        return phase == SkillUsePhase.Cast;
    }

    public override bool CanUse(
        in SkillUseContext useContext)
    {
        return base.CanUse(in useContext) &&
               FireballData.ProjectilePrefab != null;
    }

    public override void Activate(
        in SkillUseContext useContext)
    {
        _waitingToFire = true;
        _aimWorldPosition =
            useContext.AimWorldPosition;
        UpdateAimDirection(
            useContext.AimWorldPosition);
    }

    public override void FixedUpdateNetwork()
    {
        if (!_waitingToFire)
            return;

        if (Controller.TryGetCurrentInput(
                out PlayerInputData input))
        {
            _aimWorldPosition =
                input.AimWorldPosition;
            UpdateAimDirection(
                input.AimWorldPosition);
        }

        if (Controller.GetUsePhase(Slot) ==
            SkillUsePhase.Cast)
        {
            return;
        }

        _waitingToFire = false;

        if (Controller.HasStateAuthority)
        {
            SpawnProjectile();
        }
    }

    public override void Render()
    {
        bool isCasting =
            Controller.GetUsePhase(Slot) ==
            SkillUsePhase.Cast;

        if (!isCasting)
        {
            DestroyPresentation();
            return;
        }

        EnsurePresentation();

        float progress =
            CastDuration <= 0f
                ? 1f
                : 1f -
                  Controller.GetPhaseRemaining(Slot) /
                  CastDuration;

        Vector2 direction =
            _aimDirection.sqrMagnitude > 0.0001f
                ? _aimDirection
                : (Vector2)Controller.transform.right;

        _presentation.SetPose(
            ResolveSpawnPosition(direction),
            Mathf.Clamp01(progress));
    }

    public override void Cancel()
    {
        _waitingToFire = false;
        DestroyPresentation();
    }

    public override void OnUseEnded()
    {
        DestroyPresentation();
    }

    private void UpdateAimDirection(
        Vector2 aimWorldPosition)
    {
        Vector2 pivot =
            (Vector2)Controller.transform.position +
            Vector2.up * FireballData.SpawnUp;

        Vector2 direction =
            aimWorldPosition -
            pivot;

        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();

            Vector2 origin =
                pivot +
                direction * FireballData.SpawnForward;

            Vector2 originToAim =
                aimWorldPosition - origin;

            _aimDirection =
                originToAim.sqrMagnitude > 0.0001f
                    ? originToAim.normalized
                    : direction;
        }
    }

    private void SpawnProjectile()
    {
        NetworkObject prefab =
            FireballData.ProjectilePrefab;

        if (prefab == null)
            return;

        Vector2 direction =
            _aimDirection.sqrMagnitude > 0.0001f
                ? _aimDirection.normalized
                : Vector2.right;

        UpdateAimDirection(
            _aimWorldPosition);

        direction =
            _aimDirection.sqrMagnitude > 0.0001f
                ? _aimDirection.normalized
                : direction;

        Vector2 origin =
            ResolveSpawnPosition(direction);

        float angle =
            Mathf.Atan2(direction.y, direction.x) *
            Mathf.Rad2Deg;

        Vector2 velocity =
            direction *
            FireballData.ProjectileSpeed;

        Controller.Runner.Spawn(
            prefab,
            origin,
            Quaternion.Euler(0f, 0f, angle),
            Controller.Object.InputAuthority,
            (runner, spawned) =>
            {
                Projectile projectile =
                    spawned.GetComponent<Projectile>();

                projectile?.Initialize(
                    runner,
                    Controller.Object,
                    velocity,
                    FireballData.ProjectileLifetime,
                    FireballData.Damage,
                    FireballData.Knockback,
                    FireballData.KnockbackControlLock);
            });
    }

    private Vector2 ResolveSpawnPosition(
        Vector2 direction)
    {
        return (Vector2)Controller.transform.position +
               direction * FireballData.SpawnForward +
               Vector2.up * FireballData.SpawnUp;
    }

    private void EnsurePresentation()
    {
        if (_presentation != null)
            return;

        _presentation =
            FireballCastPresentation.Create(
                FireballData.CastVfxPrefab);
    }

    private void DestroyPresentation()
    {
        if (_presentation == null)
            return;

        _presentation.Release();
        _presentation = null;
    }
}
