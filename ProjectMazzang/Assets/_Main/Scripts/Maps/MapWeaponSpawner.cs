using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

[Serializable]
public sealed class MapWeaponSpawnEntry
{
    [SerializeField]
    private NetworkObject weaponPrefab;

    [SerializeField]
    private Transform spawnPoint;

    [Min(0f)]
    [SerializeField]
    private float spawnDelay;

    public NetworkObject WeaponPrefab =>
        weaponPrefab;

    public Transform SpawnPoint =>
        spawnPoint;

    public float SpawnDelay =>
        spawnDelay;
}


[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class MapWeaponSpawner :
    MonoBehaviour
{
    [Header("Primary Spawn")]
    [SerializeField]
    private NetworkObject weaponPrefab;

    [SerializeField]
    private Transform spawnPoint;

    [Min(0f)]
    [SerializeField]
    private float spawnDelay;

    [Header("Additional Weapon Spawns")]
    [SerializeField]
    private List<MapWeaponSpawnEntry>
        additionalSpawns = new();

    private readonly List<NetworkObject>
        _spawnedWeapons = new();

    private NetworkObject _mapObject;
    private NetworkRunner _runner;


    private void Awake()
    {
        _mapObject =
            GetComponent<NetworkObject>();
    }


    private void Start()
    {
        if (_mapObject == null ||
            !_mapObject.HasStateAuthority)
        {
            return;
        }

        _runner =
            FindFirstObjectByType<
                NetworkRunner>();

        if (_runner == null)
        {
            Debug.LogError(
                "[Map] NetworkRunner를 찾지 못해 " +
                "무기를 생성할 수 없습니다.",
                this);

            return;
        }

        ScheduleSpawn(
            weaponPrefab,
            spawnPoint,
            spawnDelay);

        if (additionalSpawns == null)
            return;

        foreach (MapWeaponSpawnEntry entry
                 in additionalSpawns)
        {
            if (entry == null)
                continue;

            ScheduleSpawn(
                entry.WeaponPrefab,
                entry.SpawnPoint,
                entry.SpawnDelay);
        }
    }


    private void ScheduleSpawn(
        NetworkObject prefab,
        Transform point,
        float delay)
    {
        if (prefab == null ||
            point == null)
        {
            return;
        }

        if (delay <= 0f)
        {
            SpawnWeapon(
                prefab,
                point);

            return;
        }

        StartCoroutine(
            SpawnWeaponAfterDelay(
                prefab,
                point,
                delay));
    }


    private IEnumerator SpawnWeaponAfterDelay(
        NetworkObject prefab,
        Transform point,
        float delay)
    {
        yield return new WaitForSeconds(
            delay);

        SpawnWeapon(
            prefab,
            point);
    }


    private void SpawnWeapon(
        NetworkObject prefab,
        Transform point)
    {
        if (_runner == null ||
            prefab == null ||
            point == null)
        {
            return;
        }

        Vector3 spawnPosition =
            point.position;

        Quaternion spawnRotation =
            point.rotation;

        NetworkObject spawned =
            _runner.Spawn(
                prefab,
                spawnPosition,
                spawnRotation,
                PlayerRef.None,
                (_, spawnedObject) =>
                {
                    spawnedObject.transform
                        .SetPositionAndRotation(
                            spawnPosition,
                            spawnRotation);

                    Rigidbody2D body =
                        spawnedObject.GetComponent<
                            Rigidbody2D>();

                    if (body == null)
                        return;

                    body.position =
                        spawnPosition;
                    body.rotation =
                        spawnRotation.eulerAngles.z;
                });

        if (spawned != null)
        {
            _spawnedWeapons.Add(
                spawned);
        }
    }


    private void OnDestroy()
    {
        if (_runner == null)
            return;

        foreach (NetworkObject weapon
                 in _spawnedWeapons)
        {
            if (weapon != null &&
                _runner.Exists(weapon))
            {
                _runner.Despawn(weapon);
            }
        }

        _spawnedWeapons.Clear();
    }


#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        DrawSpawnGizmo(
            spawnPoint,
            new Color(
                1f,
                0.75f,
                0.1f,
                1f));

        if (additionalSpawns == null)
            return;

        foreach (MapWeaponSpawnEntry entry
                 in additionalSpawns)
        {
            if (entry == null)
                continue;

            DrawSpawnGizmo(
                entry.SpawnPoint,
                new Color(
                    0.3f,
                    0.9f,
                    1f,
                    1f));
        }
    }


    private static void DrawSpawnGizmo(
        Transform point,
        Color color)
    {
        if (point == null)
            return;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(
            point.position,
            0.35f);
    }

#endif
}
