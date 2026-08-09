using UnityEngine;

/// <summary>
/// 한 플레이어가 공유할 PlayerContext를 생성하고,
/// PlayerModule들에게 동일한 Context를 연결합니다.
///
/// 개별 모듈의 타입이나 시뮬레이션 순서는 알지 않습니다.
/// FixedUpdateNetwork / Render 실행은 Fusion과 각 PlayerModule이 담당합니다.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class PlayerController :
    MonoBehaviour
{
    private PlayerContext _context;

    private PlayerModule[] _modules;


    public PlayerContext Context =>
        _context;


    private void Awake()
    {
        _context =
            new PlayerContext(
                gameObject);

        _modules =
            GetComponents<PlayerModule>();

        InitializeModules();
    }


    private void InitializeModules()
    {
        // 1차:
        // 모든 모듈에 동일 Context를 전달하고,
        // 각 모듈이 제공하는 Context Unit을 등록합니다.
        foreach (PlayerModule module
                 in _modules)
        {
            module.InitializeContext(
                _context);
        }

        // 2차:
        // 모든 Unit 등록이 완료된 뒤,
        // 각 모듈이 필요한 Unit을 안전하게 Resolve합니다.
        foreach (PlayerModule module
                 in _modules)
        {
            module
                .CompleteContextInitialization();
        }
    }
}
