using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>
/// 공유 에셋의 정적 설정입니다. 충전량과 타이머 등 플레이어 상태는 보관하지 않습니다.
/// 기존 실행 경로는 아직 이 설정을 소비하지 않습니다.
/// </summary>
[Serializable]
public sealed class SkillPatternSettings
{
    [SerializeField] private ChargeSettings charge = new();
    [SerializeField] private MeterSettings meter = new();
    [SerializeField] private SkillTimeSettings cast = new();
    [SerializeField] private SkillDurationSettings duration = new();
    [SerializeField] private SkillTimeSettings recovery = new();
    [SerializeField] private SkillActionLockSettings actionLock = new();
    [SerializeField] private SkillStatSettings statModifier = new();
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

        /*if (charge != null && meter != null && charge.Enabled && meter.Enabled)
            errors.Add("현재 Charge와 Meter의 동시 사용은 지원하지 않습니다.");*/

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
    Passive,
    Timed
}

[Serializable]
public sealed class ChargeSettings : SkillPatternOptions
{
    [SerializeField, Range(1, 255)] private int maxCharges = 2;
    [SerializeField, Range(0, 255)] private int initialCharges = 2;
    [SerializeField, Range(1, 255)] private int costPerUse = 1;
    [Space]
    [SerializeField] private SkillChargeRechargeMode rechargeMode = SkillChargeRechargeMode.Passive;
    [SerializeField, Min(0f)] private float rechargeDuration = 2f;

    public int MaxCharges => maxCharges;
    public int InitialCharges => initialCharges;
    public int CostPerUse => costPerUse;
    public SkillChargeRechargeMode RechargeMode => rechargeMode;
    public float RechargeDuration => rechargeDuration;

    public override bool Validate(out string error)
    {
        error = maxCharges >= 1 && maxCharges <= byte.MaxValue && NonNegative(rechargeDuration)
            ? null : "횟수는 1~255, 재충전 시간은 유한한 0 이상의 값이어야 합니다.";
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
    [SerializeField] private SkillMeterConsumeMode comsumeMode = SkillMeterConsumeMode.Reset;
    [SerializeField, Min(0f)] private float cost = 100f;
    [SerializeField, Min(0f)] private float passiveGainPerSecond = 2f;
    [SerializeField, Min(0f)] private float damageGainPerDamage = 1f;

    public float MaxMeter => maxMeter;
    public float InitialMeter => initialMeter;
    public float RequiredMeter => requiredMeter;
    public SkillMeterConsumeMode ConsumeMode => comsumeMode;
    public float Cost => cost;
    public float PassiveGainPerSecond => passiveGainPerSecond;
    public float DamageGainPerDamage => damageGainPerDamage;

    public override bool Validate(out string error)
    {
        error = NonNegative(maxMeter) && maxMeter > 0f &&
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
