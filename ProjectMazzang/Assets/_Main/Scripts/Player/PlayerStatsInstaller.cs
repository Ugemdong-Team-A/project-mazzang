using UnityEngine;

/// <summary>
/// 같은 GameObject의 Stats 소비자에게 선택적인 기본 능력치 데이터를 전달합니다.
/// Tick을 소유하지 않으며, 데이터가 없으면 각 소비자의 기본값을 사용합니다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-900)]
public sealed class PlayerStatsInstaller :
    MonoBehaviour
{
    [SerializeField]
    private PlayerStatsData statsData;

    public PlayerStatsData StatsData =>
        statsData;

    private void Awake()
    {
        MonoBehaviour[] components =
            GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour component
                 in components)
        {
            if (component is IStatsConsumer consumer)
            {
                consumer.InitializeStats(
                    statsData);
            }
        }
    }
}
