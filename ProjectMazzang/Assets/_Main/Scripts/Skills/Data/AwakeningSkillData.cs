using UnityEngine;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Skill/Awakening",
    fileName = "AwakeningSkillData")]
public sealed class AwakeningSkillData : SkillData
{
    [Header("Duration")]
    [Min(0.01f)] [SerializeField] private float duration = 8f;

    [Header("Stat Multipliers")]
    [Min(0f)] [SerializeField] private float moveSpeedMultiplier = 1.25f;
    [Min(0f)] [SerializeField] private float attackDamageMultiplier = 1.5f;
    [Min(0.01f)] [SerializeField] private float maxHealthMultiplier = 1.5f;
    [Tooltip("1보다 작으면 받는 피해가 감소합니다.")]
    [Min(0f)] [SerializeField] private float damageTakenMultiplier = 0.75f;

    [Header("Presentation")]
    [Min(0.01f)] [SerializeField] private float visualScaleMultiplier = 1.35f;

    public float Duration => duration;

    public PlayerStatModifiers StatModifiers =>
        new(
            moveSpeedMultiplier,
            attackDamageMultiplier,
            maxHealthMultiplier,
            damageTakenMultiplier,
            visualScaleMultiplier);

    public override Skill CreateSkill()
    {
        return new AwakeningSkill();
    }
}
