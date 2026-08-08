using UnityEngine;
using Fusion;

public class NetworkGameManager : NetworkBehaviour
{
    [SerializeField]
    private NetworkObject defaultPlayerPrefab;

    private MapRuntime currentMap;

    public override void Spawned()
    {
        if (!HasStateAuthority)
            return;

        InitializeGame();
    }

    private void InitializeGame()
    {
        NetworkGameSession gameSession =
            AppRoot.Instance.Network.GameSession;

        MapData mapData =
            gameSession.SelectedMapData;

        if (mapData == null ||
            mapData.MapPrefab == null)
        {
            Debug.LogError(
                "선택된 MapData가 올바르지 않습니다.",
                this);

            return;
        }

        NetworkObject mapObject =
            Runner.Spawn(mapData.MapPrefab);

        MapRuntime map =
            mapObject.GetComponent<MapRuntime>();

        SpawnPlayers(map);
    }

    private void SpawnPlayers(MapRuntime map)
    {
        currentMap = map;

        if (defaultPlayerPrefab == null)
        {
            Debug.LogError("[GM] 플레이어 프리팹이 null 입니다.");
            return;
        }

        int index = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            Transform spawnPoint =
                currentMap.GetSpawnPoint(index);

            if (spawnPoint == null)
            {
                Debug.LogError(
                    $"SpawnPoint가 부족합니다. Index: {index}",
                    this);

                break;
            }

            NetworkObject playerObj = Runner.Spawn(
                defaultPlayerPrefab,
                spawnPoint.position,
                spawnPoint.rotation,
                player);

            if (Runner.TryGetPlayerObject(player, out NetworkObject dataObj))
                if (dataObj.TryGetComponent(out NetworkPlayerData data))
                    data.SetPlayerCharacter(playerObj);

            index++;
        }
    }
}
