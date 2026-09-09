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
        Check(charge, "Charge", errors);
        Check(meter, "Meter", errors);
        Check(cast, "Cast", errors);
        Check(duration, "Duration", errors);
        Check(recovery, "Recovery", errors);
        Check(actionLock, "Action Lock", errors);
        Check(statModifier, "Stat Modifier", errors);
        Check(appearance, "Appearance", errors);

        if (charge != null && charge.Enabled &&
            charge.RechargeMode == SkillChargeRechargeMode.Meter && !(meter?.Enabled ?? false))
            errors.Add("Meter 방식 Charge에는 Meter가 필요합니다.");
        if (duration != null && duration.Enabled && duration.Mode == SkillDurationMode.ChargeWindow &&
            (!(charge?.Enabled ?? false) || duration.Source != SkillDurationSource.Settings))
            errors.Add("ChargeWindow에는 Charge와 Settings 출처의 제한 시간이 필요합니다.");

        error = string.Join("\n", errors);
        return errors.Count == 0;
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
    Meter = 0,
    Timed = 1
}

public enum SkillMeterRechargePolicy
{
    Full = 0,
    OneByOne = 1
}

[Serializable]
public sealed class ChargeSettings : SkillPatternOptions
{
    [SerializeField, Range(1, 255)] private int maxCharges = 2;
    [Tooltip("Timed의 시작 횟수입니다. Meter 방식은 항상 0부터 시작합니다.")]
    [SerializeField, Range(0, 255)] private int initialCharges = 2;
    [SerializeField, Range(1, 255)] private int costPerUse = 1;
    [Space]
    [SerializeField] private SkillChargeRechargeMode rechargeMode = SkillChargeRechargeMode.Timed;
    [FormerlySerializedAs("resetByMeterMode")]
    [Tooltip("Full은 사용 가능한 횟수가 없고 사용 구간이 닫힌 뒤 완충하여 전체 보충, OneByOne은 최대 횟수 미만에서 완충마다 하나씩 보충합니다.")]
    [SerializeField] private SkillMeterRechargePolicy meterRechargePolicy = SkillMeterRechargePolicy.Full;
    [SerializeField, Min(0f)] private float rechargeDuration = 2f;

    public int MaxCharges => maxCharges;
    public int InitialCharges
        => RechargeMode == SkillChargeRechargeMode.Meter
        ? 0 : initialCharges;
    public int CostPerUse => costPerUse;
    public SkillChargeRechargeMode RechargeMode => rechargeMode;
    public SkillMeterRechargePolicy MeterRechargePolicy => meterRechargePolicy;
    public float RechargeDuration => rechargeDuration;

    public override bool Validate(out string error)
    {
        error = maxCharges >= 1 && maxCharges <= byte.MaxValue && NonNegative(rechargeDuration) &&
            initialCharges >= 0 && initialCharges <= maxCharges && costPerUse >= 1 && costPerUse <= maxCharges
            ? null : "최대 횟수는 1~255, 초기 횟수는 0~최대, 비용은 1~최대, 재충전 시간은 유한한 0 이상이어야 합니다.";
        return error == null;
    }
}

public enum SkillMeterConsumeMode
{
    None,
    Cost,
    Reset
}

[Serializable]
public sealed class MeterSettings : SkillPatternOptions
{
    [SerializeField, Min(0.01f)] private float maxMeter = 100f;
    [SerializeField, Min(0.01f)] private float initialMeter = 0f;
    [SerializeField, Min(0.01f)] private float requiredMeter = 100f;
    [Space]
    [FormerlySerializedAs("comsumeMode")]
    [Tooltip("None은 요구량만 확인, Cost는 비용 차감, Reset은 0으로 초기화합니다. Meter 방식 Charge에서는 횟수 보충 시 Meter가 초기화됩니다.")]
    [SerializeField] private SkillMeterConsumeMode consumeMode = SkillMeterConsumeMode.Reset;
    [SerializeField, Min(0f)] private float cost = 100f;
    [SerializeField, Min(0f)] private float passiveGainPerSecond = 2f;
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
        if (!NonNegative(requiredMeter) || requiredMeter > maxMeter)
        {
            error = "RequiredMeter는 유한한 0 이상이며 MaxMeter 이하여야 합니다.";
            return false;
        }
        error = NonNegative(maxMeter) && maxMeter > 0f &&
            NonNegative(initialMeter) && initialMeter <= maxMeter &&
            NonNegative(requiredMeter) && requiredMeter <= maxMeter &&
            NonNegative(cost) && cost <= maxMeter &&
            NonNegative(passiveGainPerSecond) && NonNegative(damageGainPerDamage)
            ? null : "최대량은 유한한 양수, 비용은 0~최대량, 충전 비율은 유한한 0 이상의 값이어야 합니다.";
        return error == null;
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

public enum SkillDurationMode
{
    Active = 0,
    ChargeWindow = 1
}

[Serializable]
public sealed class SkillDurationSettings : SkillPatternOptions
{
    [Tooltip("Active는 개별 실행 시간, ChargeWindow는 첫 사용부터 남은 횟수를 사용할 수 있는 제한 시간입니다.")]
    [SerializeField] private SkillDurationMode mode;
    public SkillDurationMode Mode => mode;
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
