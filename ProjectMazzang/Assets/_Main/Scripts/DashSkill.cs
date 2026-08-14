using UnityEngine;

public sealed class DashSkill :
    Skill
{
    private IPlayerMovementState
        _movementState;

    private IPlayerMovementControl
        _movementControl;

    private IPlayerCombatState
        _combatState;


    private DashSkillData DashData =>
        (DashSkillData)Data;


    protected override void OnInitialized()
    {
        _movementState =
            Context.Get<
                IPlayerMovementState>();

        _movementControl =
            Context.Get<
                IPlayerMovementControl>();

        _combatState =
            Context.Get<
                IPlayerCombatState>();
    }


    public override bool CanUse(
        in SkillUseContext useContext)
    {
        if (!base.CanUse(
                in useContext))
        {
            return false;
        }

        if (_movementState == null ||
            _movementControl == null)
        {
            return false;
        }

        if (_movementState.IsControlLocked)
            return false;

        if (_combatState != null &&
            _combatState.IsAttacking)
        {
            return false;
        }

        return true;
    }


    public override void Activate(
        in SkillUseContext useContext)
    {
        float direction;

        if (Mathf.Abs(
                useContext.MoveInput.x) >
            0.01f)
        {
            direction =
                Mathf.Sign(
                    useContext.MoveInput.x);
        }
        else
        {
            direction =
                _movementState.FacingRight
                    ? 1f
                    : -1f;
        }

        _movementControl
            .SetHorizontalVelocity(
                direction *
                DashData.DashSpeed,

                DashData.ControlLockDuration);
    }
}