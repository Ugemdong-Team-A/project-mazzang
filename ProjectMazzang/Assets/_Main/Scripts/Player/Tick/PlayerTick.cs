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


    public PlayerTick(
        NetworkRunner runner)
    {
        Runner = runner;
    }
}
