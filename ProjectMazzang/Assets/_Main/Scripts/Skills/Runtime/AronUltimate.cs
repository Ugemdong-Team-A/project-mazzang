using UnityEngine;

public class AronUltimate : DashSkill
    , IMeterSkill
{
    public AronUltimate()
    {

    }

    private AronUltimateData
       AronUltimateData =>
           (AronUltimateData)Data;

    public float MaxMeter =>
        AronUltimateData.MaxMeter;

    public float MeterCost =>
        AronUltimateData.MeterCost;

    public float PassiveGainPerSecond =>
        AronUltimateData
            .PassiveGainPerSecond;

    public float DamageGainPerDamage =>
        AronUltimateData
            .DamageGainPerDamage;
}
