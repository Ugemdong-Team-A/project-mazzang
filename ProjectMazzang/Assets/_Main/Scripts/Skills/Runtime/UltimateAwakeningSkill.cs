using UnityEngine.U2D.Animation;

public sealed class UltimateAwakeningSkill :
    Skill/*,
    IMeterSkill,
    IDurationSkill,
    IPlayerStatModifierSkill,
    IAppearanceModifierSkill*/
{
    private UltimateAwakeningSkillData
        UltimateAwakeningData =>
            (UltimateAwakeningSkillData)Data;


    public float MaxMeter =>
        UltimateAwakeningData.MaxMeter;

    public float MeterCost =>
        UltimateAwakeningData.MeterCost;

    public float PassiveGainPerSecond =>
        UltimateAwakeningData
            .PassiveGainPerSecond;

    public float DamageGainPerDamage =>
        UltimateAwakeningData
            .DamageGainPerDamage;

    public float Duration =>
        UltimateAwakeningData.Duration;

    public PlayerStatModifiers StatModifiers =>
        UltimateAwakeningData.StatModifiers;

    public SpriteLibraryAsset AppearanceLibraryAsset =>
        UltimateAwakeningData.AppearanceLibraryAsset;


    public override void Activate(
        in SkillUseContext useContext)
    {
        // Active phase를 읽는 플레이어 모듈들이 능력치 배율을 적용합니다.
    }
}
