using UnityEngine.U2D.Animation;

public sealed class AwakeningSkill :
    Skill,
    IDurationSkill,
    IPlayerStatModifierSkill,
    IAppearanceModifierSkill
{
    private AwakeningSkillData AwakeningData =>
        (AwakeningSkillData)Data;

    public float Duration =>
        AwakeningData.Duration;

    public PlayerStatModifiers StatModifiers =>
        AwakeningData.StatModifiers;

    public SpriteLibraryAsset AppearanceLibraryAsset =>
        AwakeningData.AppearanceLibraryAsset;

    public override void Activate(
        in SkillUseContext useContext)
    {
        // 효과는 Active phase를 읽는 플레이어 모듈들이 적용한다.
    }
}
