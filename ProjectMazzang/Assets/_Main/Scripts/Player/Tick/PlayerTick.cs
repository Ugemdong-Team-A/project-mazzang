using Fusion;

public readonly struct PlayerTick
{
    public NetworkRunner Runner
    {
        get;
    }

    public Tick Number =>
        Runner.Tick;

    public float DeltaTime =>
        Runner.DeltaTime;

    public PlayerTickState State
    {
        get;
    }

    public PlayerTickCommands Commands
    {
        get;
    }


    public PlayerTick(
        NetworkRunner runner,
        PlayerTickState state,
        PlayerTickCommands commands)
    {
        Runner = runner;
        State = state;
        Commands = commands;
    }
}
