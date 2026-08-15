/// <summary>
/// 자신이 담당하는 Tick 명령만 소비하는 모듈 계약입니다.
/// </summary>
public interface IPlayerTickCommandSink
{
    bool ResolveTickCommands(
        PlayerTickCommands commands,
        PlayerTickState state);
}
