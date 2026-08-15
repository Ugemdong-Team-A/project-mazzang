public interface IPlayerTickModule
{
    PlayerTickStage Stage
    {
        get;
    }

    void Simulate(
        in PlayerTick tick);
}
