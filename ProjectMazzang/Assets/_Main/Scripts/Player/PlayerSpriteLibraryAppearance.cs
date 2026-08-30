using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>
/// TickState가 요청한 Sprite Library Asset을 캐릭터의 SpriteLibrary에 적용합니다.
/// 이 모듈을 가진 플레이어만 Sprite Library 외형 교체를 지원합니다.
/// </summary>
public sealed class PlayerSpriteLibraryAppearance :
    PlayerTickModule
{
    [SerializeField]
    private SpriteLibrary spriteLibrary;

    private SpriteLibraryAsset _defaultLibraryAsset;
    private SpriteLibraryAsset _appliedLibraryAsset;


    public override PlayerTickStage Stage =>
        PlayerTickStage.Motion;

    public override int Order => 110;


    private void Awake()
    {
        if (spriteLibrary == null)
        {
            spriteLibrary =
                GetComponentInChildren<SpriteLibrary>(true);
        }

        if (spriteLibrary == null)
            return;

        _defaultLibraryAsset =
            spriteLibrary.spriteLibraryAsset;
        _appliedLibraryAsset =
            _defaultLibraryAsset;
    }


    public override void Present(
        in PlayerTickState tickState)
    {
        SpriteLibraryAsset requestedAsset =
            tickState.ActiveAppearanceLibraryAsset;

        Apply(
            requestedAsset != null
                ? requestedAsset
                : _defaultLibraryAsset);
    }


    private void Apply(
        SpriteLibraryAsset libraryAsset)
    {
        if (spriteLibrary == null ||
            ReferenceEquals(
                _appliedLibraryAsset,
                libraryAsset))
        {
            return;
        }

        _appliedLibraryAsset = libraryAsset;
        spriteLibrary.spriteLibraryAsset = libraryAsset;
    }


    public override void Simulate(
        in PlayerTick tick)
    {
    }
}
