public interface IStatsConsumer
{
    /// <summary>
    /// null이면 소비자가 소유한 안전한 기본값을 사용합니다.
    /// </summary>
    void InitializeStats(
        PlayerStatsData statsData);
}
