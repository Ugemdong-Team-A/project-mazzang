using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.U2D.Animation;

/// <summary>
/// 공유 에셋의 정적 설정입니다. 충전량과 타이머 등 플레이어 상태는 보관하지 않습니다.
/// </summary>
[Serializable]
public sealed class SkillPatternSettings
{
    [InspectorName("사용 횟수")]
    [SerializeField] private ChargeSettings charge = new();
    [InspectorName("스킬 게이지")]
    [SerializeField] private MeterSettings meter = new();
    [InspectorName("준비 시간")]
    [SerializeField] private SkillTimeSettings cast = new();
    [InspectorName("효과 지속")]
    [SerializeField] private SkillDurationSettings duration = new();
    [InspectorName("마무리 시간")]
    [SerializeField] private SkillTimeSettings recovery = new();
    [InspectorName("사용 중 조작 제한")]
    [SerializeField] private SkillActionLockSettings actionLock = new();
    [InspectorName("능력치 변화")]
    [SerializeField] private SkillStatSettings statModifier = new();
    [InspectorName("외형 변경")]
    [SerializeField] private SkillAppearanceSettings appearance = new();

    public ChargeSettings Charge => charge;
    public MeterSettings Meter => meter;
    public SkillTimeSettings Cast => cast;
    public SkillDurationSettings Duration => duration;
    public SkillTimeSettings Recovery => recovery;
    public SkillActionLockSettings ActionLock => actionLock;
    public SkillStatSettings StatModifier => statModifier;
    public SkillAppearanceSettings Appearance => appearance;

    public bool Validate(out string error)
    {
        var errors = new List<string>();
        Check(charge, "사용 횟수", errors);

        bool usesMeterRecharge = charge != null && charge.Enabled &&
            charge.RechargeMode == SkillChargeRechargeMode.Meter;
        CheckMeter(meter, usesMeterRecharge, errors);

        Check(cast, "준비 시간", errors);
        Check(duration, "효과 지속", errors);
        Check(recovery, "마무리 시간", errors);
        Check(actionLock, "사용 중 조작 제한", errors);
        Check(statModifier, "능력치 변화", errors);
        Check(appearance, "외형 변경", errors);

        if (usesMeterRecharge && !(meter?.Enabled ?? false))
            errors.Add("사용 횟수: '스킬 게이지로 회복'을 사용하려면 스킬 게이지를 켜야 합니다.");

        bool hasDuration = duration?.Enabled ?? false;
        if ((statModifier?.Enabled ?? false) && !hasDuration)
            errors.Add("능력치 변화: 효과 지속을 함께 켜야 실제 적용 시간을 정할 수 있습니다.");
        if ((appearance?.Enabled ?? false) && !hasDuration)
            errors.Add("외형 변경: 효과 지속을 함께 켜야 실제 적용 시간을 정할 수 있습니다.");

        error = string.Join("\n", errors);
        return errors.Count == 0;
    }

    private static void CheckMeter(
        MeterSettings settings,
        bool usedForChargeRecharge,
        List<string> errors)
    {
        if (settings == null)
        {
            errors.Add("스킬 게이지: 설정이 없습니다.");
            return;
        }

        if (!settings.Enabled)
            return;

        bool valid = usedForChargeRecharge
            ? settings.ValidateForChargeRecharge(out string meterError)
            : settings.Validate(out meterError);
        if (!valid)
            errors.Add("스킬 게이지: " + meterError);
    }

    private static void Check(SkillPatternOptions options, string label, List<string> errors)
    {
        if (options == null)
            errors.Add(label + ": 설정이 없습니다.");
        else if (options.Enabled && !options.Validate(out string error))
            errors.Add(label + ": " + error);
    }
}

[Serializable]
public abstract class SkillPatternOptions
{
    [SerializeField] private bool enabled;
    public bool Enabled => enabled;
    public virtual bool Validate(out string error)
    {
        error = null;
        return true;
    }

    protected static bool NonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}

public enum SkillChargeRechargeMode
{
    [InspectorName("스킬 게이지로 회복")]
    Meter = 0,
    [InspectorName("시간으로 회복")]
    Timed = 1
}

public enum SkillChargeMeterRefillMode
{
    [InspectorName("모든 횟수 회복")]
    Full = 0,
    [InspectorName("1회 회복")]
    OneByOne = 1
}

public enum SkillChargeWindowMode
{
    [InspectorName("없음")]
    Persistent = 0,
    [InspectorName("있음")]
    Timed = 1
}

public enum SkillChargeWindowRefreshMode
{
    [InspectorName("남은 시간 유지")]
    Fixed = 0,
    [InspectorName("제한 시간 다시 시작")]
    RefreshOnUse = 1
}

[Serializable]
public sealed class ChargeSettings : SkillPatternOptions
{
    [InspectorName("최대 횟수")]
    [SerializeField, Range(1, 255)] private int maxCharges = 2;
    [InspectorName("시작 횟수")]
    [Tooltip("시간으로 회복할 때 게임 시작 시 보유하는 횟수입니다. 스킬 게이지로 회복하면 항상 0부터 시작합니다.")]
    [SerializeField, Range(0, 255)] private int initialCharges = 2;
    [InspectorName("사용 시 차감")]
    [SerializeField, Range(1, 255)] private int costPerUse = 1;
    [Space]
    [InspectorName("횟수 회복 방식")]
    [SerializeField] private SkillChargeRechargeMode rechargeMode = SkillChargeRechargeMode.Timed;
    [FormerlySerializedAs("resetByMeterMode")]
    [FormerlySerializedAs("meterRechargePolicy")]
    [InspectorName("게이지가 가득 차면")]
    [SerializeField] private SkillChargeMeterRefillMode meterRefillMode =
        SkillChargeMeterRefillMode.Full;
    [InspectorName("1회 회복 시간 (초)")]
    [SerializeField, Min(0f)] private float rechargeDuration = 2f;
    [Space]
    [FormerlySerializedAs("useWindowMode")]
    [InspectorName("남은 횟수 제한 시간")]
    [SerializeField] private SkillChargeWindowMode chargeWindowMode =
        SkillChargeWindowMode.Persistent;
    [FormerlySerializedAs("useWindowDuration")]
    [InspectorName("제한 시간 (초)")]
    [SerializeField, Min(0.01f)] private float chargeWindowDuration = 5f;
    [InspectorName("추가 사용 시")]
    [SerializeField] private SkillChargeWindowRefreshMode chargeWindowRefreshMode =
        SkillChargeWindowRefreshMode.Fixed;

    public int MaxCharges => maxCharges;
    public int InitialCharges
        => RechargeMode == SkillChargeRechargeMode.Meter
        ? 0 : initialCharges;
    public int CostPerUse => costPerUse;
    public SkillChargeRechargeMode RechargeMode => rechargeMode;
    public SkillChargeMeterRefillMode MeterRefillMode => meterRefillMode;
    public float RechargeDuration => rechargeDuration;
    public SkillChargeWindowMode ChargeWindowMode => chargeWindowMode;
    public float ChargeWindowDuration => chargeWindowDuration;
    public SkillChargeWindowRefreshMode ChargeWindowRefreshMode =>
        chargeWindowRefreshMode;

    public override bool Validate(out string error)
    {
        bool validRecharge = rechargeMode switch
        {
            SkillChargeRechargeMode.Meter =>
                Enum.IsDefined(
                    typeof(SkillChargeMeterRefillMode),
                    meterRefillMode),
            SkillChargeRechargeMode.Timed =>
                initialCharges >= 0 &&
                initialCharges <= maxCharges &&
                NonNegative(rechargeDuration),
            _ => false
        };
        bool validChargeWindow =
            chargeWindowMode == SkillChargeWindowMode.Persistent ||
            (chargeWindowMode == SkillChargeWindowMode.Timed &&
             NonNegative(chargeWindowDuration) &&
             chargeWindowDuration > 0f &&
             Enum.IsDefined(
                 typeof(SkillChargeWindowRefreshMode),
                 chargeWindowRefreshMode));

        error = maxCharges >= 1 && maxCharges <= byte.MaxValue && validRecharge &&
            costPerUse >= 1 && costPerUse <= maxCharges &&
            validChargeWindow
            ? null : "현재 보이는 설정을 확인하세요. 횟수는 허용 범위 안이어야 하고, 시간으로 회복할 때의 회복 시간은 유한한 0 이상, 제한 시간은 유한한 양수여야 합니다.";
        return error == null;
    }
}

public enum SkillMeterConsumeMode
{
    [InspectorName("유지")]
    None,
    [InspectorName("일부 차감")]
    Cost,
    [InspectorName("전부 소모")]
    Reset
}

[Serializable]
public sealed class MeterSettings : SkillPatternOptions
{
    [InspectorName("최대치")]
    [SerializeField, Min(0.01f)] private float maxMeter = 100f;
    [InspectorName("시작 게이지")]
    [SerializeField, Min(0f)] private float initialMeter = 0f;
    [InspectorName("사용 필요량")]
    [SerializeField, Min(0f)] private float requiredMeter = 100f;
    [Space]
    [FormerlySerializedAs("comsumeMode")]
    [InspectorName("사용 후 게이지")]
    [SerializeField] private SkillMeterConsumeMode consumeMode = SkillMeterConsumeMode.Reset;
    [InspectorName("차감량")]
    [SerializeField, Min(0f)] private float cost = 100f;
    [InspectorName("초당 자동 충전")]
    [SerializeField, Min(0f)] private float passiveGainPerSecond = 2f;
    [InspectorName("준 피해 1당 충전")]
    [SerializeField, Min(0f)] private float damageGainPerDamage = 1f;

    public float MaxMeter => maxMeter;
    public float InitialMeter => initialMeter;
    public float RequiredMeter => requiredMeter;
    public SkillMeterConsumeMode ConsumeMode => consumeMode;
    public float Cost => cost;
    public float PassiveGainPerSecond => passiveGainPerSecond;
    public float DamageGainPerDamage => damageGainPerDamage;

    public override bool Validate(out string error)
    {
        return Validate(usedForChargeRecharge: false, out error);
    }

    internal bool ValidateForChargeRecharge(out string error)
    {
        return Validate(usedForChargeRecharge: true, out error);
    }

    private bool Validate(bool usedForChargeRecharge, out string error)
    {
        bool validCommon = NonNegative(maxMeter) && maxMeter > 0f &&
            NonNegative(initialMeter) && initialMeter <= maxMeter &&
            NonNegative(passiveGainPerSecond) &&
            NonNegative(damageGainPerDamage);
        bool validUseSettings = usedForChargeRecharge ||
            (NonNegative(requiredMeter) &&
             requiredMeter <= maxMeter &&
             Enum.IsDefined(typeof(SkillMeterConsumeMode), consumeMode) &&
             (consumeMode != SkillMeterConsumeMode.Cost ||
              (NonNegative(cost) && cost <= maxMeter)));

        if (validCommon && validUseSettings)
        {
            error = null;
            return true;
        }

        error = usedForChargeRecharge
            ? "최대치는 유한한 양수, 시작 게이지는 0~최대치, 충전량은 유한한 0 이상이어야 합니다."
            : "현재 보이는 설정을 확인하세요. 최대치는 유한한 양수, 시작 게이지와 사용값은 0~최대치, 충전량은 유한한 0 이상이어야 합니다.";
        return false;
    }
}

[Serializable]
public sealed class SkillTimeSettings : SkillPatternOptions
{
    [SerializeField, Min(0f)] private float seconds;
    public float Seconds => seconds;
    public override bool Validate(out string error)
    {
        error = NonNegative(seconds) ? null : "시간은 유한한 0 이상의 값이어야 합니다.";
        return error == null;
    }
}

public enum SkillDurationSource
{
    Settings,
    Behavior
}

[Serializable]
public sealed class SkillDurationSettings : SkillPatternOptions
{
    [Tooltip("Behavior는 대시 이동 시간처럼 스킬 행동이 제공하는 시간을 사용합니다.")]
    [SerializeField] private SkillDurationSource source;
    [SerializeField, Min(0.01f)] private float seconds = 1f;
    public SkillDurationSource Source => source;
    public float Seconds => seconds;
    public override bool Validate(out string error)
    {
        error = source == SkillDurationSource.Behavior ||
            (source == SkillDurationSource.Settings && NonNegative(seconds) && seconds > 0f)
            ? null : "시간 출처가 유효해야 하며 직접 설정한 지속시간은 유한한 양수여야 합니다.";
        return error == null;
    }
}

[Serializable]
public sealed class SkillActionLockSettings : SkillPatternOptions
{
    [SerializeField] private bool duringCast = true;
    [SerializeField] private bool duringActive = true;
    [SerializeField] private bool duringRecovery = true;

    public bool IsLocked(SkillUsePhase phase) => Enabled && (phase switch
    {
        SkillUsePhase.Cast => duringCast,
        SkillUsePhase.Active => duringActive,
        SkillUsePhase.Recovery => duringRecovery,
        _ => false
    });
}

[Serializable]
public sealed class SkillStatSettings : SkillPatternOptions
{
    [SerializeField, Min(0f)] private float moveSpeed = 1f;
    [SerializeField, Min(0f)] private float attackDamage = 1f;
    [SerializeField, Min(0.01f)] private float maxHealth = 1f;
    [SerializeField, Min(0f)] private float damageTaken = 1f;
    [SerializeField, Min(0.01f)] private float visualScale = 1f;

    public PlayerStatModifiers Modifiers =>
        new(moveSpeed, attackDamage, maxHealth, damageTaken, visualScale);

    public override bool Validate(out string error)
    {
        error = NonNegative(moveSpeed) && NonNegative(attackDamage) &&
            NonNegative(maxHealth) && maxHealth > 0f && NonNegative(damageTaken) &&
            NonNegative(visualScale) && visualScale > 0f
            ? null : "배율은 유한한 0 이상의 값, 최대 체력과 크기 배율은 양수여야 합니다.";
        return error == null;
    }
}

[Serializable]
public sealed class SkillAppearanceSettings : SkillPatternOptions
{
    [Tooltip("비어 있으면 기본 외형을 유지합니다.")]
    [SerializeField] private SpriteLibraryAsset library;
    public SpriteLibraryAsset Library => library;
}
