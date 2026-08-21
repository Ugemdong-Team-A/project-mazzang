/// <summary>
/// PlayerController가 구체 모듈 타입을 몰라도 Tick 상태를 수집할 수 있게 합니다.
/// </summary>
public interface IPlayerTickStateSource
{
    void CaptureTickState(
        PlayerTickState state);
}
