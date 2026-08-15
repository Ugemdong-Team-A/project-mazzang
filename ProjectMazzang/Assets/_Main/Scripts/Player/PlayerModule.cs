using Fusion;
using UnityEngine;

/// <summary>
/// PlayerContext를 공유하는 플레이어 내부 NetworkBehaviour의 공통 기반입니다.
///
/// Fusion의 FixedUpdateNetwork / Render 실행은 각 파생 클래스가
/// 기존처럼 직접 담당하며, 이 클래스는 Context 연결만 책임집니다.
/// </summary>
public abstract class PlayerModule :
    NetworkBehaviour
{
    private bool _contextCompleted;

    private bool _tickControlled;


    protected PlayerContext Context
    {
        get;
        private set;
    }


    public bool IsContextReady =>
        Context != null &&
        _contextCompleted;


    protected bool IsTickControlled =>
        _tickControlled;


    internal void SetTickControlled(
        bool controlled)
    {
        _tickControlled =
            controlled;
    }


    internal void InitializeContext(
        PlayerContext context)
    {
        if (context == null)
        {
            Debug.LogError(
                $"{GetType().Name}에 전달된 " +
                "PlayerContext가 null입니다.",
                this);

            return;
        }

        if (Context != null)
        {
            if (ReferenceEquals(
                    Context,
                    context))
            {
                return;
            }

            Debug.LogError(
                $"{GetType().Name}은 이미 다른 " +
                "PlayerContext로 초기화되었습니다.",
                this);

            return;
        }

        Context = context;

        RegisterContextUnits();
    }


    internal void CompleteContextInitialization()
    {
        if (_contextCompleted)
            return;

        if (Context == null)
        {
            Debug.LogError(
                $"{GetType().Name}의 " +
                "PlayerContext가 초기화되지 않았습니다.",
                this);

            return;
        }

        OnContextReady();

        _contextCompleted = true;
    }


    // =========================================================
    // Context
    // =========================================================

    /// <summary>
    /// 이 모듈이 다른 플레이어 모듈에 제공하는
    /// Context Unit을 등록합니다.
    /// </summary>
    protected virtual void RegisterContextUnits()
    {
    }


    /// <summary>
    /// 모든 PlayerModule의 Context Unit 등록이 끝난 뒤 호출됩니다.
    /// 다른 Unit을 가져와 캐싱해야 한다면 여기서 처리합니다.
    /// </summary>
    protected virtual void OnContextReady()
    {
    }
}
