using UnityEngine.U2D.Animation;

/// <summary>활성 공통 설정을 조회하고 행동 기반 지속시간을 해석합니다.</summary>
public sealed class SkillPatternView
{
    private readonly Skill skill;

    public SkillPatternView(Skill skill) { this.skill = skill; }

    private SkillPatternSettings Settings =>
        skill.Data.Patterns;

    public ChargeSettings Charge =>
        Settings.Charge.Enabled
            ? Settings.Charge
            : null;

    public MeterSettings Meter =>
        Settings.Meter.Enabled
            ? Settings.Meter
            : null;

    public SkillTimeSettings Cast =>
        Settings.Cast.Enabled
            ? Settings.Cast
            : null;

    public SkillDurationSettings DurationPattern =>
        Settings.Duration.Enabled
            ? Settings.Duration
            : null;

    public SkillTimeSettings Recovery =>
        Settings.Recovery.Enabled
            ? Settings.Recovery
            : null;


    public SkillStatSettings StatModifier =>
        Settings.StatModifier.Enabled 
        ? Settings.StatModifier 
        : null;

    public SkillAppearanceSettings Appearance 
        => Settings.Appearance.Enabled 
        ? Settings.Appearance 
        : null;

    public SkillActionLockSettings ActionLock 
        => Settings.ActionLock.Enabled 
        ? Settings.ActionLock 
        : null;

    // 런타임 해석이 필요한 값
    public bool UsesChargeWindow => Charge != null &&
        DurationPattern?.Mode == SkillDurationMode.ChargeWindow;

    public float ActiveDuration => UsesChargeWindow ? skill.BehaviorDuration : Duration;

    public bool UsesMeterRecharge => Charge?.RechargeMode == SkillChargeRechargeMode.Meter;

    public bool NeedsMeterPayment(bool windowOpen) => Meter != null &&
        !UsesMeterRecharge && !(UsesChargeWindow && windowOpen);

    public bool CanGainMeter(int charges, bool windowOpen)
    {
        if (Meter == null) return false;
        if (!UsesMeterRecharge) return true;
        return Charge.MeterRechargePolicy == SkillMeterRechargePolicy.Full
            ? charges < Charge.CostPerUse && !windowOpen
            : charges < Charge.MaxCharges;
    }

    public float Duration =>
        DurationPattern == null
            ? 0f
            : DurationPattern.Source ==
              SkillDurationSource.Behavior
                ? skill.BehaviorDuration
                : DurationPattern.Seconds;

    public float Cooldown =>
        skill.Data.Cooldown;

    public bool IsActionLocked(
        SkillUsePhase phase)
    {
        return Settings.ActionLock.Enabled &&
               Settings.ActionLock.IsLocked(phase);
    }
}
