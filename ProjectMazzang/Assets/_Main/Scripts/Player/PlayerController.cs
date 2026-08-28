using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 같은 NetworkObject에 속한 PlayerTickModule을 수집하고
/// Stage와 Order 순서로 네트워크 Tick을 실행합니다.
/// 개별 모듈의 구체 타입과 시뮬레이션 로직은 알지 않습니다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class PlayerController :
    NetworkBehaviour,
    IPlayerTickCommandDispatcher
{
    private const int MaxCommandResolvePasses = 8;


    // private PlayerContext _context;

    [SerializeField]
    private PlayerTickModule[] _modules;

    private IPlayerTickStateSource[] _tickStateSources;

    private IPlayerTickCommandSink[] _tickCommandSinks;

    private readonly PlayerTickState _tickState =
        new();

    private readonly PlayerTickCommands _tickCommands =
        new();

    private bool _initialized;

    private bool _tickPipelineEnabled;

    private bool _resolvingCommands;


    public PlayerTickState TickState => _tickState;

    public PlayerTickCommands TickCommands => _tickCommands;


    /*public PlayerContext Context =>
        _context;*/


    private void Awake()
    {
        /*_context =
            new PlayerContext(
                gameObject);*/

        CollectModules();

        ConfigureTickPipeline();
    }


    public override void Spawned()
    {
        if (_initialized)
            return;

        InitializeModules();

        _initialized = true;
    }


    public override void FixedUpdateNetwork()
    {
        if (!_initialized ||
            !_tickPipelineEnabled)
        {
            return;
        }

        PlayerTick tick =
            new(
                Runner,
                _tickState,
                _tickCommands);

        CaptureInitialTickState();

        if (DispatchPendingCommands())
        {
            CaptureCurrentTickState();
        }

        foreach (PlayerTickModule module
                 in _modules)
        {
            module.Simulate(
                in tick);

            CaptureCurrentTickState();

            if (DispatchPendingCommands())
            {
                CaptureCurrentTickState();
            }
        }
    }

    public override void Render()
    {
        if (!_initialized ||
            !_tickPipelineEnabled)
        {
            return;
        }

        foreach (PlayerTickModule module in _modules)
            module.Present(in _tickState);
    }


    // =========================================================
    // Module Collection
    // =========================================================

    private void CollectModules()
    {
        NetworkObject ownerObject =
            GetComponent<NetworkObject>();

        PlayerTickModule[] candidates =
            GetComponentsInChildren<
                PlayerTickModule>(
                true);

        List<PlayerTickModule> modules =
            new(
                candidates.Length);

        foreach (PlayerTickModule module
                 in candidates)
        {
            NetworkObject moduleObject =
                module.GetComponentInParent<
                    NetworkObject>();

            // Nested NetworkObject의 모듈은
            // 별도의 PlayerContext 영역으로 취급한다.
            if (moduleObject !=
                ownerObject)
            {
                continue;
            }

            modules.Add(
                module);
        }

        _modules =
            modules.ToArray();
    }


    private void ConfigureTickPipeline()
    {
        _tickPipelineEnabled = false;

        List<PlayerTickModule> tickModules =
            new(
                _modules.Length);

        foreach (PlayerTickModule module
                 in _modules)
        {
            tickModules.Add(
                module);
        }

        tickModules.Sort(
            CompareTickModules);

        for (int i = 1;
             i < tickModules.Count;
             i++)
        {
            PlayerTickModule previous =
                tickModules[i - 1];

            PlayerTickModule current =
                tickModules[i];

            if (previous.Stage != current.Stage ||
                previous.Order != current.Order)
            {
                continue;
            }

            _modules =
                tickModules.ToArray();

            Debug.LogError(
                $"Player Tick 순서가 충돌했습니다. " +
                $"Stage: {current.Stage}, Order: {current.Order}, " +
                $"Modules: {previous.GetType().Name}, " +
                $"{current.GetType().Name}. " +
                "같은 Stage의 모듈에는 서로 다른 Order를 지정해야 합니다.",
                this);

            return;
        }

        _modules =
            tickModules.ToArray();

        List<IPlayerTickStateSource> stateSources =
            new();

        foreach (PlayerTickModule module
                 in _modules)
        {
            if (module is IPlayerTickStateSource stateSource)
            {
                stateSources.Add(
                    stateSource);
            }
        }

        _tickStateSources =
            stateSources.ToArray();

        List<IPlayerTickCommandSink> commandSinks =
            new();

        foreach (PlayerTickModule module
                 in _modules)
        {
            module.BindCommands(
                _tickCommands);

            if (module is IPlayerTickCommandSink commandSink)
            {
                commandSinks.Add(
                    commandSink);
            }
        }

        _tickCommandSinks =
            commandSinks.ToArray();

        _tickCommands.SetDispatcher(
            this);

        /*foreach (PlayerTickModule module
                 in _modules)
        {
            if (module is PlayerTickModule)
            {
                module.SetTickControlled(
                    true);
            }
        }*/

        _tickPipelineEnabled =
            true;
    }


    private void CaptureInitialTickState()
    {
        _tickState.Reset();

        CaptureCurrentTickState();
    }


    private void CaptureCurrentTickState()
    {
        foreach (IPlayerTickStateSource stateSource
                 in _tickStateSources)
        {
            stateSource.CaptureTickState(
                _tickState);
        }
    }


    private bool ResolvePendingCommands()
    {
        bool resolvedAny = false;

        for (int pass = 0;
             pass < MaxCommandResolvePasses &&
             _tickCommands.HasPending;
             pass++)
        {
            bool resolvedThisPass = false;

            foreach (IPlayerTickCommandSink commandSink
                     in _tickCommandSinks)
            {
                resolvedThisPass |=
                    commandSink.ResolveTickCommands(
                        _tickCommands,
                        _tickState);
            }

            if (!resolvedThisPass)
                break;

            resolvedAny = true;
        }

        if (_tickCommands.HasPending)
        {
            Debug.LogError(
                "처리되지 않은 Player Tick 명령이 있습니다.",
                this);
        }

        return resolvedAny;
    }


    private bool DispatchPendingCommands()
    {
        if (_resolvingCommands ||
            !_tickPipelineEnabled)
        {
            return false;
        }

        _resolvingCommands = true;

        try
        {
            return ResolvePendingCommands();
        }
        finally
        {
            _resolvingCommands = false;
        }
    }


    void IPlayerTickCommandDispatcher.DispatchTickCommands()
    {
        if (_resolvingCommands ||
            !_tickPipelineEnabled)
        {
            return;
        }

        CaptureCurrentTickState();
        DispatchPendingCommands();
        CaptureCurrentTickState();
    }


    private static int CompareTickModules(
        PlayerTickModule left,
        PlayerTickModule right)
    {
        int stageComparison =
            left.Stage.CompareTo(
                right.Stage);

        if (stageComparison != 0)
            return stageComparison;

        return left.Order.CompareTo(
            right.Order);
    }


    // =========================================================
    // Context Initialization
    // =========================================================

    private void InitializeModules()
    {
        // 1차:
        // 모든 모듈에 동일 Context를 전달하고,
        // 각 모듈이 제공하는 Context Unit을 등록한다.
        /*foreach (PlayerModule module
                 in _modules)
        {
            module.InitializeContext(
                _context);
        }*/

        // 2차:
        // 모든 Unit 등록이 완료된 뒤,
        // 각 모듈이 필요한 Unit을 Resolve한다.
        /*foreach (PlayerModule module
                 in _modules)
        {
            module
                .CompleteContextInitialization();
        }*/
    }
}
