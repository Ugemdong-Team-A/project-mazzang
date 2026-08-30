using Fusion;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Map/Map",
    fileName = "MapData")]
public sealed class MapData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private int mapId;

    [SerializeField]
    private string displayName;

    [Header("Lobby Presentation")]
    [SerializeField]
    private Sprite previewImage;

    [Header("Map")]
    [SerializeField]
    private NetworkObject mapPrefab;

    public int MapId =>
        mapId;

    public string DisplayName =>
        displayName;

    public Sprite PreviewImage =>
        previewImage;

    public NetworkObject MapPrefab =>
        mapPrefab;
}
