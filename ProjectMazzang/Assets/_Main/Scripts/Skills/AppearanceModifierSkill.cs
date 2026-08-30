using UnityEngine.U2D.Animation;

/// <summary>
/// 활성 중 플레이어 외형에 적용할 Sprite Library Asset을 제공하는 스킬입니다.
/// 실제 적용 책임은 플레이어의 프레젠테이션 모듈에 있습니다.
/// </summary>
public interface IAppearanceModifierSkill
{
    SpriteLibraryAsset AppearanceLibraryAsset { get; }
}
