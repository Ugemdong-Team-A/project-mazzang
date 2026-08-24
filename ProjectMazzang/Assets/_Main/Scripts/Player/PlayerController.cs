using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 같은 NetworkObject에 속한 PlayerComponent를 수집합니다.
/// PlayerTickModule만 Stage와 Order 순서로 시뮬레이션하고,
/// 모든 PlayerComponent의 Present를 관리합니다.
/// 개별 모듈의 구체 타입과 시뮬레이션 로직은 알지 않습니다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class PlayerController :
    NetworkBehaviour,
    IPlayerTickCommandDispatcher
{
    private const int MaxCommandResolvePasses = 8;


    [SerializeField]
    private PlayerComponent[] _modules;

    private PlayerTickModule[] _tickModules;

    private IPlayerTickStateSource[] _tickStateSources;

    private IPlayerTickCommandSink[] _tickCommandSinks;

    private readonly PlayerTickState _tickState =
        new();

    private readonly PlayerTickCommands _tickCommands =
        new();

    private bool _initialized;

    private bool _tickPipelineEnabled;

    private bool _resolvingCommands;


    private void Awake()
    {
        CollectModules();

        ConfigureTickPipeline();
    }


    public override void Spawned()
    {
        if (_initialized)
            return;

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

    public override void Render()
    {
        if (!_initialized ||
            !_tickPipelineEnabled)
        {
            return;
        }

        foreach (PlayerComponent module in _modules)
            module.Present(in _tickState);
    }


    // =========================================================
    // Module Collection
    // =========================================================

    private void CollectModules()
    {
        NetworkObject ownerObject =
            GetComponent<NetworkObject>();

        PlayerComponent[] candidates =
            GetComponentsInChildren<
                PlayerComponent>(
                true);

        List<PlayerComponent> modules =
            new(
                candidates.Length);

        foreach (PlayerComponent module
                 in candidates)
        {
            NetworkObject moduleObject =
                module.GetComponentInParent<
                    NetworkObject>();

            // Nested NetworkObject는 별도의
            // PlayerController 관리 영역으로 취급한다.
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

        foreach (PlayerComponent module
                 in _modules)
        {
            if (module is PlayerTickModule tickModule)
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
            PlayerTickModule previous =
                tickModules[i - 1];

            PlayerTickModule current =
                tickModules[i];

            if (previous.Stage != current.Stage ||
                previous.Order != current.Order)
            {
                continue;
            }

            _tickModules =
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

        _tickModules =
            tickModules.ToArray();

        List<IPlayerTickStateSource> stateSources =
            new();

        foreach (PlayerTickModule module
                 in _tickModules)
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
                 in _tickModules)
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
}
