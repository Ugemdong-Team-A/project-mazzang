public interface IMeterSkill
{
    float MaxMeter { get; }

    float MeterCost { get; }

    float PassiveGainPerSecond { get; }

    float DamageGainPerDamage { get; }
}
