using UnityEngine;

public sealed class MapRuntime : MonoBehaviour
{
    [SerializeField]
    private Transform[] spawnPoints;

    public int SpawnPointCount =>
        spawnPoints.Length;

    public Transform GetSpawnPoint(int index)
    {
        if (index < 0 ||
            index >= spawnPoints.Length)
        {
            return null;
        }

        return spawnPoints[index];
    }

    public Transform GetRandomSpawnPoint()
    {
        if (SpawnPointCount <= 0)
        {
            return null;
        }

        int spawnIndex =
            Random.Range(
                0, SpawnPointCount);

        Transform spawnPoint =
            GetSpawnPoint(
                spawnIndex);

        return spawnPoint;
    }
}