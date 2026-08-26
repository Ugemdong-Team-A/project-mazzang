internal interface IPlayerTickCommandDispatcher
{
    public PlayerTickState TickState { get; }

    public PlayerTickCommands TickCommands { get; }

    void DispatchTickCommands();
}
