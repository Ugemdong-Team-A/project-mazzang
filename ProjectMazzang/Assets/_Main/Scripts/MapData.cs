using Fusion;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Game/Map Data",
    fileName = "MapData")]
public sealed class MapData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private int mapId;

    [SerializeField]
    private string displayName;

    [Header("Map")]
    [SerializeField]
    private NetworkObject mapPrefab;

    public int MapId => mapId;
    public string DisplayName => displayName;
    public NetworkObject MapPrefab => mapPrefab;
}