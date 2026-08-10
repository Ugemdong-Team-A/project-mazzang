using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 한 플레이어가 공유할 PlayerContext를 생성하고
/// 같은 NetworkObject 소속 PlayerModule들에게 동일한 Context를 연결합니다.
///
/// 개별 모듈의 구체 타입과 시뮬레이션 로직은 알지 않습니다.
/// FixedUpdateNetwork / Render 실행은 Fusion과 각 PlayerModule이 담당합니다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class PlayerController :
    NetworkBehaviour
{
    private PlayerContext _context;

    private PlayerModule[] _modules;

    private bool _initialized;


    public PlayerContext Context =>
        _context;


    private void Awake()
    {
        _context =
            new PlayerContext(
                gameObject);

        CollectModules();
    }


    public override void Spawned()
    {
        if (_initialized)
            return;

        InitializeModules();

        _initialized = true;
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
