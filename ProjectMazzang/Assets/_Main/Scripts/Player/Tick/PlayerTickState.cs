/// <summary>
/// 한 네트워크 Tick 안에서 단계 사이에 전달되는 플레이어 상태입니다.
/// Tick 시작 시 수집되고, 각 모듈 실행 직후 해당 모듈의 최신 값으로 갱신됩니다.
/// </summary>
public sealed class PlayerTickState
{
    public bool HasHealth { get; internal set; }

    public bool IsAlive { get; internal set; }


    internal void Reset()
    {
        HasHealth = false;
        IsAlive = false;
    }
}
