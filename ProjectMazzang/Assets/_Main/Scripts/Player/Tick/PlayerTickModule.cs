using Fusion;

public abstract class PlayerTickModule
    : NetworkBehaviour
{
    public abstract PlayerTickStage Stage
    {
        get;
    }

    public abstract void Simulate(
        in PlayerTick tick);

    public sealed override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
    }
}
