using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class CombatNoticeUI :
    MonoBehaviour
{
    private const int KillPriority = 0;
    private const int EliminatedPriority = 1;


    [Header("References")]
    [SerializeField]
    private RectTransform noticeRoot;

    [SerializeField]
    private CanvasGroup canvasGroup;

    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text detailText;


    [Header("Style")]
    [SerializeField]
    private Color killColor =
        Color.white;

    [SerializeField]
    private Color eliminatedColor =
        Color.white;


    [Header("Timing")]
    [Min(0f)]
    [SerializeField]
    private float enterDuration = 0.18f;

    [Min(0f)]
    [SerializeField]
    private float killHoldDuration = 1.1f;

    [Min(0f)]
    [SerializeField]
    private float eliminatedHoldDuration = 1.6f;

    [Min(0f)]
    [SerializeField]
    private float exitDuration = 0.25f;


    [Header("Motion")]
    [Min(0f)]
    [SerializeField]
    private float enterOffset = 28f;

    [Min(0f)]
    [SerializeField]
    private float exitOffset = 18f;

    [Range(0.5f, 1f)]
    [SerializeField]
    private float enterScale = 0.9f;


    private Sequence _sequence;

    private Vector2 _defaultAnchoredPosition;

    private int _currentPriority = -1;


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
        HideImmediate();
    }


    // =========================================================
    // Public
    // =========================================================

    public void ShowKill(
        string attackerName,
        string victimName)
    {
        Show(
            KillPriority,
            "K.O.",
            BuildDetail(
                attackerName,
                victimName),
            killColor,
            killHoldDuration);
    }


    public void ShowEliminated(
        string attackerName,
        string victimName)
    {
        Show(
            EliminatedPriority,
            "ELIMINATED",
            BuildDetail(
                attackerName,
                victimName),
            eliminatedColor,
            eliminatedHoldDuration);
    }


    public void HideImmediate()
    {
        _sequence?
            .Kill();

        _sequence =
            null;

        _currentPriority =
            -1;

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

            noticeRoot.localScale =
                Vector3.one;
        }
    }


    // =========================================================
    // Presentation
    // =========================================================

    private void Show(
        int priority,
        string title,
        string detail,
        Color titleColor,
        float holdDuration)
    {
        if (noticeRoot == null ||
            canvasGroup == null ||
            titleText == null)
        {
            return;
        }

        if (_currentPriority >
            priority)
        {
            return;
        }

        _currentPriority =
            priority;

        _sequence?
            .Kill();

        titleText.text =
            title;

        titleText.color =
            titleColor;

        if (detailText != null)
        {
            detailText.text =
                detail ?? string.Empty;
        }

        canvasGroup.alpha =
            0f;

        noticeRoot.anchoredPosition =
            _defaultAnchoredPosition +
            Vector2.down *
            enterOffset;

        noticeRoot.localScale =
            Vector3.one *
            enterScale;

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
            .Join(
                noticeRoot
                    .DOScale(
                        1f,
                        enterDuration)
                    .SetEase(
                        Ease.OutBack));

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


    private void HandleSequenceCompleted()
    {
        _sequence =
            null;

        _currentPriority =
            -1;

        if (canvasGroup != null)
        {
            canvasGroup.alpha =
                0f;
        }

        if (noticeRoot != null)
        {
            noticeRoot.anchoredPosition =
                _defaultAnchoredPosition;

            noticeRoot.localScale =
                Vector3.one;
        }
    }


    // =========================================================
    // Text
    // =========================================================

    private static string BuildDetail(
        string attackerName,
        string victimName)
    {
        bool hasAttacker =
            !string.IsNullOrWhiteSpace(
                attackerName);

        bool hasVictim =
            !string.IsNullOrWhiteSpace(
                victimName);

        if (hasAttacker &&
            hasVictim)
        {
            return
                $"{attackerName}  >  {victimName}";
        }

        if (hasVictim)
            return victimName;

        return string.Empty;
    }
}
