public enum PlayerTickStage : byte
{
    Begin = 0,
    SkillIntent,
    PrepareAction,
    Action,
    ControlResolve,
    Motion,
    Aim,
    LateAction,
    Finalize
}
