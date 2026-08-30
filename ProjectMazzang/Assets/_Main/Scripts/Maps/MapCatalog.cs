using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Map/Catalog",
    fileName = "MapCatalog")]
public sealed class MapCatalog : ScriptableObject
{
    [SerializeField]
    private MapData[] maps;

    public IReadOnlyList<MapData> Maps =>
        maps;

    public bool ContainsId(
        int mapId)
    {
        return TryGetById(
            mapId,
            out _);
    }

    public MapData GetById(
        int mapId)
    {
        TryGetById(
            mapId,
            out MapData data);

        return data;
    }

    public bool TryGetById(
        int mapId,
        out MapData data)
    {
        if (maps != null)
        {
            foreach (MapData map
                     in maps)
            {
                if (map == null)
                    continue;

                if (map.MapId !=
                    mapId)
                {
                    continue;
                }

                data = map;
                return true;
            }
        }

        data = null;
        return false;
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        if (maps == null)
            return;

        HashSet<int> ids = new();

        foreach (MapData map
                 in maps)
        {
            if (map == null)
                continue;

            if (ids.Add(
                    map.MapId))
            {
                continue;
            }

            Debug.LogError(
                $"MapCatalog에 중복 MapId가 있습니다: " +
                $"{map.MapId}",
                this);
        }
    }

#endif
}
