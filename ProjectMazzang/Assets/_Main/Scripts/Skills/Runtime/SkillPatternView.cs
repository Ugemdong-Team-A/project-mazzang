using UnityEngine.U2D.Animation;

/// <summary>공통 설정을 읽고, 미전환 에셋만 기존 인터페이스 값으로 복구합니다.</summary>
public sealed class SkillPatternView
{
    private readonly Skill skill;
    private SkillPatternSettings Settings => skill.Data.Patterns;

    public SkillPatternView(Skill skill) { this.skill = skill; }

    public SkillPatternView Charge => Settings.Charge.Enabled ? this : null;
    public SkillPatternView Meter => Settings.Meter.Enabled ? this : null;
    public SkillPatternView Cast => Settings.Cast.Enabled ? this : null;
    public SkillPatternView DurationPattern => Settings.Duration.Enabled ? this : null;
    public SkillPatternView Recovery => Settings.Recovery.Enabled ? this : null;
    public SkillPatternView Stats => Settings.StatModifier.Enabled ? this : null;
    public SkillPatternView Appearance => Settings.Appearance.Enabled ? this : null;
    public SkillPatternView ActionLock => Settings.ActionLock.Enabled ? this : null;

    public int MaxCharges => Settings.Charge.MaxCharges;
    public float RechargeDuration => Settings.Charge.RechargeDuration;
    public float MaxMeter => Settings.Meter.MaxMeter;
    public float MeterCost => Settings.Meter.Cost;
    public float PassiveGainPerSecond => Settings.Meter.PassiveGainPerSecond;
    public float DamageGainPerDamage => Settings.Meter.DamageGainPerDamage;
    public float CastDuration => Settings.Cast.Seconds;
    public float RecoveryDuration => Settings.Recovery.Seconds;
    public float Duration => Settings.Duration.Source == SkillDurationSource.Behavior ? skill.BehaviorDuration : Settings.Duration.Seconds;
    public PlayerStatModifiers StatModifiers => Settings.StatModifier.Modifiers;
    public SpriteLibraryAsset AppearanceLibraryAsset => Settings.Appearance.Library;
    public bool IsActionLocked(SkillUsePhase phase) => Settings.ActionLock.IsLocked(phase);

    // 미전환 Meter 에셋은 이전의 쿨다운 무시 동작을 유지합니다.
    public float Cooldown => Meter != null ? 0f : skill.Data.Cooldown;
}
