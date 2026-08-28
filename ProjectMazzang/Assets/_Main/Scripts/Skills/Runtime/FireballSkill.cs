using Fusion;
using UnityEngine;

public sealed class FireballSkill :
    ProjectileSkill,
    IRecoverySkill,
    IActionLockSkill
{
    public float RecoveryDuration => ProjectileData.RecoveryDuration;

    public bool IsActionLocked(
        SkillUsePhase phase)
    {
        return phase == SkillUsePhase.Cast;
    }

}
