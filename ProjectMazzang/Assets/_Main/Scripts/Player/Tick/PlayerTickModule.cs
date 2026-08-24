using Fusion;

public abstract class PlayerTickModule
    : NetworkBehaviour
{
    /// <summary>
    /// PlayerController가 같은 플레이어의 모듈에 연결한 쓰기 전용 요청 채널입니다.
    /// 실제 상태는 각 Command Sink만 변경합니다.
    /// </summary>
    protected PlayerTickCommands Commands
    {
        get;
        private set;
    }


    public abstract PlayerTickStage Stage
    {
        get;
    }


    internal void BindCommands(
        PlayerTickCommands commands)
    {
        Commands = commands;
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
