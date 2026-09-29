/// <summary>
/// 활성 공통 Pattern을 조회하고 행동 기반 지속시간을 해석하는 단일 런타임 경로입니다.
/// </summary>
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

    public SkillAppearanceSettings Appearance =>
        Settings.Appearance.Enabled
            ? Settings.Appearance
            : null;

    public SkillActionLockSettings ActionLock =>
        Settings.ActionLock.Enabled
            ? Settings.ActionLock
            : null;

    // 런타임 해석이 필요한 값
    public bool UsesChargeWindow => Charge != null &&
        Charge.ChargeWindowMode == SkillChargeWindowMode.Timed;

    public float ChargeWindowDuration =>
        UsesChargeWindow
            ? Charge.ChargeWindowDuration
            : 0f;

    public bool RefreshesChargeWindowOnUse =>
        UsesChargeWindow &&
        Charge.ChargeWindowRefreshMode ==
            SkillChargeWindowRefreshMode.RefreshOnUse;

    public bool CanContinueChargeWindow(int remainingCharges) =>
        UsesChargeWindow &&
        remainingCharges >= Charge.CostPerUse;

    public bool ShouldRestartChargeWindow(bool windowOpen) =>
        UsesChargeWindow &&
        (!windowOpen || RefreshesChargeWindowOnUse);

    public float ActiveDuration =>
        DurationPattern == null
            ? 0f
            : DurationPattern.Source ==
              SkillDurationSource.Behavior
                ? skill.BehaviorDuration
                : DurationPattern.Seconds;

    /// <summary>
    /// 게임플레이의 단일 Cast → Active → Recovery 수명 주기에서
    /// 각 단계가 점유하는 시간을 반환합니다.
    /// </summary>
    public float GetPhaseDuration(
        SkillUsePhase phase)
    {
        return phase switch
        {
            SkillUsePhase.Cast =>
                Cast?.Seconds ?? 0f,
            SkillUsePhase.Active =>
                ActiveDuration,
            SkillUsePhase.Recovery =>
                Recovery?.Seconds ?? 0f,
            _ => 0f
        };
    }

    public float TotalUseDuration =>
        GetPhaseDuration(SkillUsePhase.Cast) +
        GetPhaseDuration(SkillUsePhase.Active) +
        GetPhaseDuration(SkillUsePhase.Recovery);

    public bool UsesMeterRecharge => Charge?.RechargeMode == SkillChargeRechargeMode.Meter;

    public bool NeedsMeterPayment(bool windowOpen) => Meter != null &&
        !UsesMeterRecharge && !(UsesChargeWindow && windowOpen);

    public bool CanGainMeter(int charges, bool windowOpen)
    {
        if (Meter == null) return false;
        if (!UsesMeterRecharge) return true;
        return Charge.MeterRefillMode == SkillChargeMeterRefillMode.Full
            ? charges < Charge.CostPerUse && !windowOpen
            : charges < Charge.MaxCharges;
    }

    public float Cooldown =>
        skill.Data.Cooldown;

    public bool IsActionLocked(
        SkillUsePhase phase)
    {
        return Settings.ActionLock.Enabled &&
               Settings.ActionLock.IsLocked(phase);
    }
}
