using UnityEngine;
using UnityEngine.U2D.Animation;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Skill/Ultimate Awakening",
    fileName = "UltimateAwakeningSkillData")]
public sealed class UltimateAwakeningSkillData :
    SkillData
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


    [Header("Duration")]
    [Min(0.01f)]
    [SerializeField]
    private float duration = 8f;


    [Header("Stat Multipliers")]
    [Min(0f)]
    [SerializeField]
    private float moveSpeedMultiplier = 1.25f;

    [Min(0f)]
    [SerializeField]
    private float attackDamageMultiplier = 1.5f;

    [Min(0.01f)]
    [SerializeField]
    private float maxHealthMultiplier = 1.5f;

    [Tooltip("1보다 작으면 받는 피해가 감소합니다.")]
    [Min(0f)]
    [SerializeField]
    private float damageTakenMultiplier = 0.75f;


    [Header("Presentation")]
    [Min(0.01f)]
    [SerializeField]
    private float visualScaleMultiplier = 1.35f;

    [SerializeField]
    private SpriteLibraryAsset appearanceLibraryAsset;


    public float MaxMeter => maxMeter;

    public float MeterCost => meterCost;

    public float PassiveGainPerSecond =>
        passiveGainPerSecond;

    public float DamageGainPerDamage =>
        damageGainPerDamage;

    public float Duration => duration;

    public PlayerStatModifiers StatModifiers =>
        new(
            moveSpeedMultiplier,
            attackDamageMultiplier,
            maxHealthMultiplier,
            damageTakenMultiplier,
            visualScaleMultiplier);

    public SpriteLibraryAsset AppearanceLibraryAsset =>
        appearanceLibraryAsset;


    public override Skill CreateSkill()
    {
        return new UltimateAwakeningSkill();
    }
}
