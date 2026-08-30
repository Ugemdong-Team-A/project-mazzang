using Fusion;
using UnityEngine;

/// <summary>
/// 하나의 캐릭터를 식별하고 선택 화면에 표현하기 위한 데이터입니다.
///
/// 실제 전투 기능, 스킬, 비주얼 구성은 이 데이터가 조립하지 않습니다.
/// PlayerPrefab 자체가 해당 캐릭터의 완성된 구성을 소유합니다.
/// </summary>
[CreateAssetMenu(
    menuName = "Mazzang/Data/Character/Character",
    fileName = "CharacterData")]
public sealed class CharacterData :
    ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private int characterId;

    [SerializeField]
    private string displayName;

    [TextArea]
    [SerializeField]
    private string description;

    [Header("Lobby Presentation")]
    [SerializeField]
    private Sprite portrait;

    [Header("Gameplay")]
    [SerializeField]
    private NetworkObject playerPrefab;

    public int CharacterId =>
        characterId;

    public string DisplayName =>
        displayName;

    public string Description =>
        description;

    public Sprite Portrait =>
        portrait;

    public NetworkObject PlayerPrefab =>
        playerPrefab;
}
