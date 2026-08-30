using Fusion;
using UnityEngine;

public sealed class DeployableSkill :
    Skill,
    ICastTimeSkill,
    IRecoverySkill,
    IActionLockSkill
{
    private bool _waitingToDeploy;


    private DeployableSkillData DeployableData =>
        (DeployableSkillData)Data;

    public float CastDuration =>
        DeployableData.CastDuration;

    public float RecoveryDuration =>
        DeployableData.RecoveryDuration;


    public override bool CanUse(
        in SkillUseContext useContext)
    {
        NetworkObject prefab =
            DeployableData.DeployablePrefab;

        if (DeployableData.RequiresGrounded &&
            (!Controller.TickState.HasMovement ||
             !Controller.TickState.IsGrounded))
        {
            return false;
        }

        return base.CanUse(in useContext) &&
               prefab != null &&
               prefab.GetComponent<Deployable>() != null;
    }


    public override void Activate(
        in SkillUseContext useContext)
    {
        _waitingToDeploy = true;
    }


    public override void FixedUpdateNetwork()
    {
        if (!_waitingToDeploy ||
            Controller.GetUsePhase(Slot) ==
                SkillUsePhase.Cast)
        {
            return;
        }

        _waitingToDeploy = false;

        if (Controller.HasStateAuthority)
        {
            SpawnDeployable();
        }
    }


    public override void Cancel()
    {
        _waitingToDeploy = false;
    }


    public bool IsActionLocked(
        SkillUsePhase phase)
    {
        return phase == SkillUsePhase.Cast;
    }


    private void SpawnDeployable()
    {
        NetworkObject prefab =
            DeployableData.DeployablePrefab;

        if (prefab == null)
            return;

        float facing =
            !Controller.TickState.HasMovement ||
            Controller.TickState.FacingRight
                ? 1f
                : -1f;

        Vector2 position =
            (Vector2)Controller.transform.position +
            Vector2.right *
            facing *
            DeployableData.SpawnForward +
            Vector2.up *
            DeployableData.SpawnUp;

        NetworkObject owner =
            Controller.Object;

        Controller.Runner.Spawn(
            prefab,
            position,
            Quaternion.identity,
            owner.InputAuthority,
            (runner, spawned) =>
            {
                Deployable deployable =
                    spawned.GetComponent<Deployable>();

                deployable?.Initialize(
                    owner,
                    0f,
                    Controller.TickState
                        .ActiveStatModifiers
                        .AttackDamage);
            });
    }
}
