using Fusion;

/// <summary>
/// PlayerController가 수명 주기와 Present 호출을 관리하는
/// 플레이어 컴포넌트의 공통 기반입니다.
/// </summary>
public abstract class PlayerComponent
    : NetworkBehaviour
{
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


/// <summary>
/// PlayerController의 결정론적 Tick 파이프라인에 참여하는
/// 시뮬레이션 모듈의 공통 기반입니다.
/// </summary>
public abstract class PlayerTickModule
    : PlayerComponent
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

    /// <summary>
    /// 같은 Stage 안에서의 실행 순서입니다.
    /// 값이 낮은 모듈이 먼저 실행되며,
    /// 같은 플레이어 안의 Stage와 Order 조합은 고유해야 합니다.
    /// </summary>
    public virtual int Order => 0;


    internal void BindCommands(
        PlayerTickCommands commands)
    {
        Commands = commands;
    }

    public abstract void Simulate(
        in PlayerTick tick);
}
