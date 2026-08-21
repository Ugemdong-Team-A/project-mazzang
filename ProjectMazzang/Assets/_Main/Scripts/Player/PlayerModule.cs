using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어 내부 NetworkBehaviour의 공통 기반입니다.
///
/// 새 Tick 경로에는 명령 채널을 제공하고, 기존 UI와 Render를 위한
/// PlayerContext 호환 초기화는 별도 수명 주기로 유지합니다.
/// </summary>
public abstract class PlayerModule :
    NetworkBehaviour
{
    private bool _contextCompleted;

    private bool _tickControlled;


    /*protected PlayerContext Context
    {
        get;
        private set;
    }*/


    /*public bool IsContextReady =>
        Context != null &&
        _contextCompleted;*/


    protected bool IsTickControlled =>
        _tickControlled;

    protected PlayerTickCommands TickCommands
    {
        get;
        private set;
    }


    internal void SetTickControlled(
        bool controlled)
    {
        _tickControlled =
            controlled;
    }


    internal void SetTickCommands(
        PlayerTickCommands commands)
    {
        TickCommands = commands;
    }


    /*internal void InitializeContext(
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
    }*/


    /*internal void CompleteContextInitialization()
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
    }*/


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
