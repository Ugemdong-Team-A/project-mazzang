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
    [InspectorName("게이지")]
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
            errors.Add("사용 횟수: '게이지가 차면 보충'을 사용하려면 게이지를 켜야 합니다.");

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
            errors.Add("게이지: 설정이 없습니다.");
            return;
        }

        if (!settings.Enabled)
            return;

        bool valid = usedForChargeRecharge
            ? settings.ValidateForChargeRecharge(out string meterError)
            : settings.Validate(out meterError);
        if (!valid)
            errors.Add("게이지: " + meterError);
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
    [InspectorName("게이지가 차면 보충")]
    Meter = 0,
    [InspectorName("시간이 지나면 보충")]
    Timed = 1
}

public enum SkillMeterRechargePolicy
{
    [InspectorName("전부 보충")]
    Full = 0,
    [InspectorName("1회씩 보충")]
    OneByOne = 1
}

public enum SkillChargeUseWindowMode
{
    [InspectorName("제한 없음")]
    Persistent = 0,
    [InspectorName("제한 시간 사용")]
    Timed = 1
}

[Serializable]
public sealed class ChargeSettings : SkillPatternOptions
{
    [InspectorName("최대 보유 횟수")]
    [SerializeField, Range(1, 255)] private int maxCharges = 2;
    [InspectorName("시작 횟수")]
    [Tooltip("시간으로 보충할 때 게임 시작 시 보유하는 횟수입니다. 게이지로 보충하면 항상 0부터 시작합니다.")]
    [SerializeField, Range(0, 255)] private int initialCharges = 2;
    [InspectorName("한 번에 쓰는 횟수")]
    [SerializeField, Range(1, 255)] private int costPerUse = 1;
    [Space]
    [InspectorName("횟수 보충 방식")]
    [SerializeField] private SkillChargeRechargeMode rechargeMode = SkillChargeRechargeMode.Timed;
    [FormerlySerializedAs("resetByMeterMode")]
    [InspectorName("게이지 완충 시")]
    [Tooltip("전부 보충은 사용할 수 있는 횟수가 없을 때 게이지를 채워 최대치까지 보충합니다. 1회씩 보충은 최대치에 도달할 때까지 완충할 때마다 한 번을 보충합니다.")]
    [SerializeField] private SkillMeterRechargePolicy meterRechargePolicy = SkillMeterRechargePolicy.Full;
    [InspectorName("1회 보충 시간")]
    [SerializeField, Min(0f)] private float rechargeDuration = 2f;
    [Space]
    [InspectorName("남은 횟수 사용 기한")]
    [Tooltip("제한 없음은 남은 횟수를 계속 보존합니다. 제한 시간 사용은 첫 사용부터 시간을 재고, 만료되면 남은 횟수를 없앱니다.")]
    [SerializeField] private SkillChargeUseWindowMode useWindowMode =
        SkillChargeUseWindowMode.Persistent;
    [InspectorName("사용 제한 시간 (초)")]
    [SerializeField, Min(0.01f)] private float useWindowDuration = 5f;

    public int MaxCharges => maxCharges;
    public int InitialCharges
        => RechargeMode == SkillChargeRechargeMode.Meter
        ? 0 : initialCharges;
    public int CostPerUse => costPerUse;
    public SkillChargeRechargeMode RechargeMode => rechargeMode;
    public SkillMeterRechargePolicy MeterRechargePolicy => meterRechargePolicy;
    public float RechargeDuration => rechargeDuration;
    public SkillChargeUseWindowMode UseWindowMode => useWindowMode;
    public float UseWindowDuration => useWindowDuration;

    public override bool Validate(out string error)
    {
        bool validRecharge = rechargeMode switch
        {
            SkillChargeRechargeMode.Meter =>
                Enum.IsDefined(
                    typeof(SkillMeterRechargePolicy),
                    meterRechargePolicy),
            SkillChargeRechargeMode.Timed =>
                initialCharges >= 0 &&
                initialCharges <= maxCharges &&
                NonNegative(rechargeDuration),
            _ => false
        };
        bool validUseWindow =
            useWindowMode == SkillChargeUseWindowMode.Persistent ||
            (useWindowMode == SkillChargeUseWindowMode.Timed &&
             NonNegative(useWindowDuration) &&
             useWindowDuration > 0f);

        error = maxCharges >= 1 && maxCharges <= byte.MaxValue && validRecharge &&
            costPerUse >= 1 && costPerUse <= maxCharges &&
            validUseWindow
            ? null : "현재 보이는 설정을 확인하세요. 횟수는 허용 범위 안이어야 하고, 시간으로 보충할 때의 보충 시간은 유한한 0 이상, 제한 시간은 유한한 양수여야 합니다.";
        return error == null;
    }
}

public enum SkillMeterConsumeMode
{
    [InspectorName("유지")]
    None,
    [InspectorName("지정량 차감")]
    Cost,
    [InspectorName("전부 소모")]
    Reset
}

[Serializable]
public sealed class MeterSettings : SkillPatternOptions
{
    [InspectorName("최대 게이지")]
    [SerializeField, Min(0.01f)] private float maxMeter = 100f;
    [InspectorName("시작 게이지")]
    [SerializeField, Min(0f)] private float initialMeter = 0f;
    [InspectorName("사용 가능 기준")]
    [SerializeField, Min(0f)] private float requiredMeter = 100f;
    [Space]
    [FormerlySerializedAs("comsumeMode")]
    [InspectorName("사용 후 처리")]
    [Tooltip("유지는 게이지를 확인만 하고, 지정량 차감은 입력한 만큼 빼며, 전부 소모는 게이지를 0으로 만듭니다.")]
    [SerializeField] private SkillMeterConsumeMode consumeMode = SkillMeterConsumeMode.Reset;
    [InspectorName("차감량")]
    [SerializeField, Min(0f)] private float cost = 100f;
    [InspectorName("초당 충전량")]
    [SerializeField, Min(0f)] private float passiveGainPerSecond = 2f;
    [InspectorName("피해 1당 충전량")]
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

        error = validCommon && validUseSettings
            ? null
            : "현재 보이는 설정을 확인하세요. 최대 게이지는 유한한 양수, 시작값과 사용값은 0~최대, 충전량은 유한한 0 이상이어야 합니다.";
        return error == null;
    }
}

[Serializable]
public sealed class SkillTimeSettings : SkillPatternOptions
{
    [InspectorName("시간 (초)")]
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
    [InspectorName("직접 입력")]
    Settings,
    [InspectorName("스킬 동작에서 결정")]
    Behavior
}

[Serializable]
public sealed class SkillDurationSettings : SkillPatternOptions
{
    [InspectorName("시간 결정 방식")]
    [Tooltip("스킬 동작에서 결정을 선택하면 대시 이동 시간처럼 실제 행동이 제공하는 시간을 사용합니다.")]
    [SerializeField] private SkillDurationSource source;
    [InspectorName("지속 시간 (초)")]
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
    [InspectorName("준비 중")]
    [SerializeField] private bool duringCast = true;
    [InspectorName("효과 지속 중")]
    [SerializeField] private bool duringActive = true;
    [InspectorName("마무리 중")]
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
    [InspectorName("이동 속도 배율")]
    [SerializeField, Min(0f)] private float moveSpeed = 1f;
    [InspectorName("공격력 배율")]
    [SerializeField, Min(0f)] private float attackDamage = 1f;
    [InspectorName("최대 체력 배율")]
    [SerializeField, Min(0.01f)] private float maxHealth = 1f;
    [InspectorName("받는 피해 배율")]
    [SerializeField, Min(0f)] private float damageTaken = 1f;
    [InspectorName("크기 배율")]
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
    [InspectorName("변경할 외형")]
    [Tooltip("비어 있으면 기본 외형을 유지합니다.")]
    [SerializeField] private SpriteLibraryAsset library;
    public SpriteLibraryAsset Library => library;
}
