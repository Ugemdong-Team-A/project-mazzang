public enum PlayerTickStage : byte
{
    Begin = 0,
    SkillIntent,
    DefenseIntent,
    PrepareAction,
    Action,
    ControlResolve,
    Motion,
    Aim,
    LateAction,
    Finalize
}
