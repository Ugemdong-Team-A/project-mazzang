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

    public virtual void Present(
        in PlayerTickState tickState)
    {

    }

    public sealed override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
    }

    public sealed override void Render()
    {
        base.Render();
    }
}
