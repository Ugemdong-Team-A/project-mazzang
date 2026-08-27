public sealed class AwakeningSkill :
    Skill,
    IDurationSkill,
    IPlayerStatModifierSkill
{
    private AwakeningSkillData AwakeningData =>
        (AwakeningSkillData)Data;

    public float Duration =>
        AwakeningData.Duration;

    public PlayerStatModifiers StatModifiers =>
        AwakeningData.StatModifiers;

    public override void Activate(
        in SkillUseContext useContext)
    {
        // 효과는 Active phase를 읽는 플레이어 모듈들이 적용한다.
    }
}
