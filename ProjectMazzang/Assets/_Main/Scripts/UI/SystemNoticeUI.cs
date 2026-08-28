using DG.Tweening;
using Fusion;
using TMPro;
using UnityEngine;

public sealed class SystemNoticeUI :
    MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private RectTransform noticeRoot;

    [SerializeField]
    private CanvasGroup canvasGroup;

    [SerializeField]
    private TMP_Text noticeText;


    [Header("Timing")]
    [Min(0f)]
    [SerializeField]
    private float enterDuration = 0.2f;

    [Min(0f)]
    [SerializeField]
    private float holdDuration = 1.8f;

    [Min(0f)]
    [SerializeField]
    private float exitDuration = 0.3f;


    [Header("Motion")]
    [Min(0f)]
    [SerializeField]
    private float enterOffset = 18f;

    [Min(0f)]
    [SerializeField]
    private float exitOffset = 12f;


    private FusionSessionController
        _network;

    private Sequence
        _sequence;

    private Vector2
        _defaultAnchoredPosition;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (noticeRoot != null)
        {
            _defaultAnchoredPosition =
                noticeRoot.anchoredPosition;
        }

        HideImmediate();
    }


    private void OnDisable()
    {
        _sequence?
            .Kill();

        _sequence =
            null;
    }


    // =========================================================
    // Bind
    // =========================================================

    public void Bind(
        FusionSessionController network)
    {
        if (_network == network)
            return;

        Unbind();

        _network =
            network;

        if (_network == null)
            return;

        _network.UnexpectedShutdown +=
            HandleUnexpectedShutdown;
    }


    public void Unbind()
    {
        if (_network != null)
        {
            _network.UnexpectedShutdown -=
                HandleUnexpectedShutdown;
        }

        _network =
            null;
    }


    // =========================================================
    // Public
    // =========================================================

    public void Show(
        string message)
    {
        if (string.IsNullOrWhiteSpace(
                message))
        {
            return;
        }

        if (noticeRoot == null ||
            canvasGroup == null ||
            noticeText == null)
        {
            return;
        }

        _sequence?
            .Kill();

        noticeText.text =
            $"- {message.Trim()} -";

        canvasGroup.alpha =
            0f;

        noticeRoot.anchoredPosition =
            _defaultAnchoredPosition +
            Vector2.down *
            enterOffset;

        _sequence =
            DOTween.Sequence()
                .SetUpdate(true);

        _sequence
            .Append(
                canvasGroup
                    .DOFade(
                        1f,
                        enterDuration));

        _sequence
            .Join(
                noticeRoot
                    .DOAnchorPos(
                        _defaultAnchoredPosition,
                        enterDuration)
                    .SetEase(
                        Ease.OutCubic));

        _sequence
            .AppendInterval(
                holdDuration);

        _sequence
            .Append(
                canvasGroup
                    .DOFade(
                        0f,
                        exitDuration));

        _sequence
            .Join(
                noticeRoot
                    .DOAnchorPos(
                        _defaultAnchoredPosition +
                        Vector2.up *
                        exitOffset,
                        exitDuration)
                    .SetEase(
                        Ease.InCubic));

        _sequence
            .OnComplete(
                HandleSequenceCompleted);
    }


    public void HideImmediate()
    {
        _sequence?
            .Kill();

        _sequence =
            null;

        if (canvasGroup != null)
        {
            canvasGroup.alpha =
                0f;

            canvasGroup.interactable =
                false;

            canvasGroup.blocksRaycasts =
                false;
        }

        if (noticeRoot != null)
        {
            noticeRoot.anchoredPosition =
                _defaultAnchoredPosition;
        }
    }


    // =========================================================
    // Network Notice
    // =========================================================

    private void HandleUnexpectedShutdown(
        ShutdownReason reason)
    {
        // Host Migration을 사용하지 않는 현재 구조에서는
        // Room 안에서 예상치 못한 Runner 종료가 발생하면
        // 남은 Client 입장에서는 Host와의 연결 종료로 취급한다.
        Show(
            "호스트와의 연결이 종료되었습니다.");
    }


    // =========================================================
    // Tween
    // =========================================================

    private void HandleSequenceCompleted()
    {
        _sequence =
            null;

        if (canvasGroup != null)
        {
            canvasGroup.alpha =
                0f;
        }

        if (noticeRoot != null)
        {
            noticeRoot.anchoredPosition =
                _defaultAnchoredPosition;
        }
    }
}