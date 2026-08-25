using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SkillSlotUI :
    MonoBehaviour
{
    [Header("Base")]
    [SerializeField]
    private GameObject contentRoot;

    [SerializeField]
    private Image iconImage;

    [SerializeField]
    private TMP_Text shortcutText;


    [Header("Cooldown")]
    [SerializeField]
    private GameObject cooldownRoot;

    [SerializeField]
    private Image cooldownFill;

    [SerializeField]
    private TMP_Text cooldownText;


    [Header("Charge")]
    [SerializeField]
    private GameObject chargeRoot;

    [SerializeField]
    private Transform chargePipRoot;

    [SerializeField]
    private Image chargePipPrefab;

    [SerializeField]
    private Color availableChargeColor =
        Color.white;

    [SerializeField]
    private Color emptyChargeColor =
        new(
            1f,
            1f,
            1f,
            0.25f);

    [SerializeField]
    private GameObject rechargeRoot;

    [SerializeField]
    private Image rechargeFill;

    [SerializeField]
    private TMP_Text rechargeText;


    [Header("Meter")]
    [SerializeField]
    private GameObject meterRoot;

    [SerializeField]
    private Image meterFill;

    [SerializeField]
    private TMP_Text meterText;


    [Header("Duration")]
    [SerializeField]
    private GameObject durationRoot;

    [SerializeField]
    private Image durationFill;

    [SerializeField]
    private TMP_Text durationText;


    private readonly List<Image>
        _chargePips =
            new();

    private PlayerSkillController
        _controller;

    private SkillSlot _slot;

    private Skill _skill;

    private SkillData _skillData;

    private int _builtMaxCharges;


    // =========================================================
    // Unity
    // =========================================================

    private void Update()
    {
        if (_controller == null)
            return;

        RefreshSkillReference();

        if (_skill == null)
            return;

        RefreshCooldown();
        RefreshCharge();
        RefreshMeter();
        RefreshDuration();
    }


    // =========================================================
    // Bind
    // =========================================================

    public void Bind(
        PlayerSkillController controller,
        SkillSlot slot)
    {
        if (_controller == controller &&
            _slot == slot)
        {
            return;
        }

        Unbind();

        _controller =
            controller;

        _slot =
            slot;

        RefreshShortcut();

        RefreshSkillReference(
            true);
    }


    public void Unbind()
    {
        _controller =
            null;

        _skill =
            null;

        _skillData =
            null;

        _builtMaxCharges =
            0;

        ClearChargePips();

        SetActive(
            contentRoot,
            false);

        SetActive(
            cooldownRoot,
            false);

        SetActive(
            chargeRoot,
            false);

        SetActive(
            meterRoot,
            false);

        SetActive(
            durationRoot,
            false);

        SetActive(
            shortcutText?.gameObject,
            false);
    }


    // =========================================================
    // Skill
    // =========================================================

    private void RefreshSkillReference(
        bool force = false)
    {
        Skill skill =
            _controller.GetSkill(
                _slot);

        if (!force &&
            ReferenceEquals(
                _skill,
                skill))
        {
            return;
        }

        _skill =
            skill;

        _skillData =
            _controller.GetSkillData(
                _slot);


        bool hasSkill =
            _skill != null &&
            _skillData != null;

        SetActive(
            contentRoot,
            hasSkill);

        SetActive(
            shortcutText?.gameObject,
            hasSkill);


        if (!hasSkill)
        {
            SetIcon(
                null);

            ClearChargePips();

            SetActive(
                cooldownRoot,
                false);

            SetActive(
                chargeRoot,
                false);

            SetActive(
                meterRoot,
                false);

            SetActive(
                durationRoot,
                false);

            return;
        }


        SetIcon(
            _skillData.Icon);

        RefreshChargeLayout();
    }


    private void SetIcon(
        Sprite icon)
    {
        if (iconImage == null)
            return;

        iconImage.sprite =
            icon;

        iconImage.enabled =
            icon != null;
    }


    // =========================================================
    // Cooldown
    // =========================================================

    private void RefreshCooldown()
    {
        float duration =
            _skillData.Cooldown;

        float remaining =
            _controller
                .GetCooldownRemaining(
                    _slot);


        bool active =
            duration > 0f &&
            remaining > 0f;

        SetActive(
            cooldownRoot,
            active);


        if (!active)
            return;


        if (cooldownFill != null)
        {
            cooldownFill.fillAmount =
                Mathf.Clamp01(
                    remaining /
                    duration);

            SetVerticalProgress(
                cooldownFill,
                cooldownFill.fillAmount);
        }


        SetTimeText(
            cooldownText,
            remaining);
    }


    // =========================================================
    // Charge
    // =========================================================

    private void RefreshChargeLayout()
    {
        if (_skill is not
            IChargeSkill)
        {
            _builtMaxCharges =
                0;

            ClearChargePips();

            SetActive(
                chargeRoot,
                false);

            return;
        }


        int maxCharges =
            _controller.GetMaxCharges(
                _slot);


        SetActive(
            chargeRoot,
            maxCharges > 1);


        if (maxCharges <= 1)
        {
            ClearChargePips();

            _builtMaxCharges =
                maxCharges;

            return;
        }


        EnsureChargePips(
            maxCharges);
    }


    private void RefreshCharge()
    {
        if (_skill is not
            IChargeSkill)
        {
            SetActive(
                chargeRoot,
                false);

            return;
        }


        int maximum =
            _controller.GetMaxCharges(
                _slot);

        int current =
            _controller.GetCurrentCharges(
                _slot);


        if (maximum !=
            _builtMaxCharges)
        {
            RefreshChargeLayout();
        }


        bool showCharges =
            maximum > 1;

        SetActive(
            chargeRoot,
            showCharges);


        if (!showCharges)
            return;


        for (int i = 0;
             i < _chargePips.Count;
             i++)
        {
            Image pip =
                _chargePips[i];

            if (pip == null)
                continue;

            pip.color =
                i < current
                    ? availableChargeColor
                    : emptyChargeColor;
        }


        bool recharging =
            current < maximum;


        SetActive(
            rechargeRoot,
            recharging);


        if (!recharging)
            return;


        if (rechargeFill != null)
        {
            rechargeFill.fillAmount =
                _controller
                    .GetRechargeNormalized(
                        _slot);

            SetHorizontalProgress(
                rechargeFill,
                rechargeFill.fillAmount);
        }


        float remaining =
            _controller
                .GetRechargeRemaining(
                    _slot);

        SetTimeText(
            rechargeText,
            remaining);
    }


    private void EnsureChargePips(
        int count)
    {
        if (_builtMaxCharges ==
                count &&
            _chargePips.Count ==
                count)
        {
            return;
        }


        ClearChargePips();

        _builtMaxCharges =
            count;


        if (chargePipRoot == null ||
            chargePipPrefab == null)
        {
            return;
        }


        for (int i = 0;
             i < count;
             i++)
        {
            Image pip =
                Instantiate(
                    chargePipPrefab,
                    chargePipRoot);

            pip.raycastTarget =
                false;

            pip.gameObject
                .SetActive(
                    true);

            _chargePips.Add(
                pip);
        }
    }


    private void ClearChargePips()
    {
        foreach (Image pip
                 in _chargePips)
        {
            if (pip == null)
                continue;

            Destroy(
                pip.gameObject);
        }

        _chargePips.Clear();
    }


    // =========================================================
    // Meter
    // =========================================================

    private void RefreshMeter()
    {
        if (_skill is not
                IMeterSkill meterSkill ||
            _controller.GetUsePhase(
                _slot) !=
            SkillUsePhase.None)
        {
            SetActive(
                meterRoot,
                false);

            return;
        }


        float maximum =
            _controller.GetMaxMeter(
                _slot);

        bool active =
            maximum > 0f;

        SetActive(
            meterRoot,
            active);

        if (!active)
            return;


        float current =
            _controller.GetCurrentMeter(
                _slot);

        float normalized =
            _controller.GetMeterNormalized(
                _slot);

        if (meterFill != null)
        {
            meterFill.fillAmount =
                normalized;

            SetHorizontalProgress(
                meterFill,
                normalized);
        }


        if (meterText == null)
            return;

        float cost =
            Mathf.Max(
                0f,
                meterSkill.MeterCost);

        meterText.text =
            current >= cost
                ? "READY"
                : $"{Mathf.FloorToInt(normalized * 100f)}%";
    }


    // =========================================================
    // Duration
    // =========================================================

    private void RefreshDuration()
    {
        if (_skill is not
                IDurationSkill durationSkill ||
            _controller.GetUsePhase(
                _slot) !=
            SkillUsePhase.Active)
        {
            SetActive(
                durationRoot,
                false);

            return;
        }


        float duration =
            durationSkill.Duration;

        float remaining =
            _controller.GetPhaseRemaining(
                _slot);


        bool active =
            duration > 0f &&
            remaining > 0f;

        SetActive(
            durationRoot,
            active);


        if (!active)
            return;


        if (durationFill != null)
        {
            durationFill.fillAmount =
                Mathf.Clamp01(
                    remaining /
                    duration);

            SetHorizontalProgress(
                durationFill,
                durationFill.fillAmount);
        }


        SetTimeText(
            durationText,
            remaining);
    }


    // =========================================================
    // Utility
    // =========================================================

    private void RefreshShortcut()
    {
        if (shortcutText == null)
            return;

        shortcutText.text =
            _slot == SkillSlot.Skill1
                ? "LSHIFT"
                : "Q";
    }


    private static void SetHorizontalProgress(
        Graphic graphic,
        float normalized)
    {
        SetProgressScale(
            graphic,
            Mathf.Clamp01(normalized),
            1f);
    }


    private static void SetVerticalProgress(
        Graphic graphic,
        float normalized)
    {
        SetProgressScale(
            graphic,
            1f,
            Mathf.Clamp01(normalized));
    }


    private static void SetProgressScale(
        Graphic graphic,
        float x,
        float y)
    {
        if (graphic == null)
            return;

        RectTransform rectTransform =
            graphic.rectTransform;

        rectTransform.localScale =
            new Vector3(
                x,
                y,
                1f);
    }

    private static void SetTimeText(
        TMP_Text text,
        float remaining)
    {
        if (text == null)
            return;

        if (remaining >= 1f)
        {
            text.text =
                Mathf.CeilToInt(
                    remaining)
                .ToString();

            return;
        }

        text.text =
            remaining.ToString(
                "0.0");
    }


    private static void SetActive(
        GameObject target,
        bool active)
    {
        if (target == null)
            return;

        if (target.activeSelf ==
            active)
        {
            return;
        }

        target.SetActive(
            active);
    }
}
