using UnityEngine;

/// <summary>
/// 이전 플레이어 프리팹의 직렬화 참조를 보존하는 호환용 컴포넌트입니다.
/// 현재 런타임 능력치는 각 담당 컴포넌트가 직접 소유합니다.
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
}
