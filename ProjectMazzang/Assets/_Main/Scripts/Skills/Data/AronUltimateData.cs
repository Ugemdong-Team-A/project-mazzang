using UnityEngine;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Skill/Aron Ultimate",
    fileName = "AronUltimateSkillData")]

public class AronUltimateData : DashSkillData
{
    [Header("Meter")]
    [Min(0.01f)]
    [SerializeField]
    private float maxMeter = 100f;

    [Min(0f)]
    [SerializeField]
    private float meterCost = 100f;

    [Min(0f)]
    [SerializeField]
    private float passiveGainPerSecond = 2f;

    [Min(0f)]
    [SerializeField]
    private float damageGainPerDamage = 1f;


    public float MaxMeter => maxMeter;

    public float MeterCost => meterCost;

    public float PassiveGainPerSecond =>
        passiveGainPerSecond;

    public float DamageGainPerDamage =>
        damageGainPerDamage;

    public override Skill CreateSkill()
    {
        return new AronUltimate();
    }
}
