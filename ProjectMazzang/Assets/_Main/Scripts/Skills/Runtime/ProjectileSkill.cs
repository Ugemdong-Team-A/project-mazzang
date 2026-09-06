using Fusion;
using UnityEngine;

public class ProjectileSkill : 
    Skill/*,
    ICastTimeSkill*/
{
    protected Vector2 _aimDirection = Vector2.right;
    protected Vector2 _aimWorldPosition;
    protected bool _waitingToFire;
    protected FireballCastPresentation _presentation;

    protected ProjectileSkillData ProjectileData =>
        (ProjectileSkillData)Data;

    public float CastDuration => ProjectileData.CastDuration;

    public override bool CanUse(
        in SkillUseContext useContext)
    {
        return base.CanUse(in useContext) &&
               ProjectileData.ProjectilePrefab != null;
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
            Controller.GetSkillAimDirection(Slot);

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction =
                _aimDirection.sqrMagnitude > 0.0001f
                    ? _aimDirection
                    : (Vector2)Controller.transform.right;
        }

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

    protected virtual void UpdateAimDirection(
        Vector2 aimWorldPosition)
    {
        Vector2 pivot =
            (Vector2)Controller.transform.position +
            Vector2.up * ProjectileData.SpawnUp;

        Vector2 direction =
            aimWorldPosition -
            pivot;

        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();

            Vector2 origin =
                pivot +
                direction * ProjectileData.SpawnForward;

            Vector2 originToAim =
                aimWorldPosition - origin;

            _aimDirection =
                originToAim.sqrMagnitude > 0.0001f
                    ? originToAim.normalized
                    : direction;

            Controller.SetSkillAimDirection(
                Slot,
                _aimDirection);
        }
    }

    protected virtual void SpawnProjectile()
    {
        NetworkObject prefab =
            ProjectileData.ProjectilePrefab;

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
                    direction,
                    Controller.TickState
                        .ActiveStatModifiers
                        .AttackDamage);
            });
    }

    protected Vector2 ResolveSpawnPosition(
        Vector2 direction)
    {
        return (Vector2)Controller.transform.position +
               direction * ProjectileData.SpawnForward +
               Vector2.up * ProjectileData.SpawnUp;
    }

    protected void EnsurePresentation()
    {
        if (_presentation != null)
            return;

        _presentation =
            FireballCastPresentation.Create(
                ProjectileData.CastVfxPrefab);
    }

    protected void DestroyPresentation()
    {
        if (_presentation == null)
            return;

        _presentation.Release();
        _presentation = null;
    } 
}
