using Fusion;
using UnityEngine;

public sealed class FireballSkill :
    ProjectileSkill,
    IRecoverySkill,
    IActionLockSkill
{
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
        base.FixedUpdateNetwork();
    }

    public override void Render()
    {
        base.Render();
    }

    public override void Cancel()
    {
        base.Cancel();
    }

    public override void OnUseEnded()
    {
        DestroyPresentation();
    }
}
