public enum PlayerTickStage : byte
{
    Begin = 0,
    PrepareAction,
    Action,
    ControlResolve,
    Motion,
    Aim,
    LateAction,
    Finalize
}
