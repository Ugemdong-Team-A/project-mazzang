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


    public PlayerTick(
        NetworkRunner runner,
        PlayerTickState state)
    {
        Runner = runner;
        State = state;
    }
}
