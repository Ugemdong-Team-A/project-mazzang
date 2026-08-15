using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 한 플레이어가 공유할 PlayerContext를 생성하고
/// 같은 NetworkObject 소속 PlayerModule들에게 동일한 Context를 연결합니다.
///
/// 개별 모듈의 구체 타입과 시뮬레이션 로직은 알지 않습니다.
/// Tick 단계, 상태 제공자, 명령 소비자 계약만 해석해
/// 네트워크 Tick을 실행합니다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class PlayerController :
    NetworkBehaviour,
    IPlayerTickCommandDispatcher
{
    private const int MaxCommandResolvePasses = 8;


    private PlayerContext _context;

    private PlayerModule[] _modules;

    private IPlayerTickModule[] _tickModules;

    private IPlayerTickStateSource[] _tickStateSources;

    private IPlayerTickCommandSink[] _tickCommandSinks;

    private readonly PlayerTickState _tickState =
        new();

    private readonly PlayerTickCommands _tickCommands =
        new();

    private bool _initialized;

    private bool _tickPipelineEnabled;

    private bool _resolvingCommands;


    public PlayerContext Context =>
        _context;


    private void Awake()
    {
        _context =
            new PlayerContext(
                gameObject);

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

        foreach (IPlayerTickModule module
                 in _tickModules)
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


    // =========================================================
    // Module Collection
    // =========================================================

    private void CollectModules()
    {
        NetworkObject ownerObject =
            GetComponent<NetworkObject>();

        PlayerModule[] candidates =
            GetComponentsInChildren<
                PlayerModule>(
                true);

        List<PlayerModule> modules =
            new(
                candidates.Length);

        foreach (PlayerModule module
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
        List<IPlayerTickModule> tickModules =
            new();

        foreach (PlayerModule module
                 in _modules)
        {
            if (module is IPlayerTickModule tickModule)
            {
                tickModules.Add(
                    tickModule);
            }
        }

        tickModules.Sort(
            CompareTickModules);

        for (int i = 1;
             i < tickModules.Count;
             i++)
        {
            IPlayerTickModule previous =
                tickModules[i - 1];

            IPlayerTickModule current =
                tickModules[i];

            if (previous.Stage !=
                current.Stage)
            {
                continue;
            }

            Debug.LogError(
                $"Player Tick Stage {current.Stage}에 " +
                "둘 이상의 모듈이 등록되었습니다. " +
                "기존 실행 경로를 유지합니다.",
                this);

            _tickModules =
                tickModules.ToArray();

            return;
        }

        _tickModules =
            tickModules.ToArray();

        List<IPlayerTickStateSource> stateSources =
            new();

        foreach (PlayerModule module
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

        foreach (PlayerModule module
                 in _modules)
        {
            module.SetTickCommands(
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

        foreach (PlayerModule module
                 in _modules)
        {
            if (module is IPlayerTickModule)
            {
                module.SetTickControlled(
                    true);
            }
        }

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
        IPlayerTickModule left,
        IPlayerTickModule right)
    {
        return left.Stage.CompareTo(
            right.Stage);
    }


    // =========================================================
    // Context Initialization
    // =========================================================

    private void InitializeModules()
    {
        // 1차:
        // 모든 모듈에 동일 Context를 전달하고,
        // 각 모듈이 제공하는 Context Unit을 등록한다.
        foreach (PlayerModule module
                 in _modules)
        {
            module.InitializeContext(
                _context);
        }

        // 2차:
        // 모든 Unit 등록이 완료된 뒤,
        // 각 모듈이 필요한 Unit을 Resolve한다.
        foreach (PlayerModule module
                 in _modules)
        {
            module
                .CompleteContextInitialization();
        }
    }
}
