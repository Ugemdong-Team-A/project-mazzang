using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class MapWeaponSpawner :
    MonoBehaviour
{
    [SerializeField]
    private NetworkObject weaponPrefab;

    [SerializeField]
    private Transform spawnPoint;

    private NetworkObject _mapObject;

    private NetworkObject _spawnedWeapon;

    private NetworkRunner _runner;

    private bool _spawnAttempted;

    private bool _ownsSpawnedWeapon;


    private void Awake()
    {
        _mapObject =
            GetComponent<NetworkObject>();
    }


    private void Start()
    {
        TrySpawnWeapon();
    }


    private void OnDestroy()
    {
        if (_runner == null ||
            _spawnedWeapon == null ||
            !_runner.Exists(_spawnedWeapon))
        {
            return;
        }

        if (_ownsSpawnedWeapon)
        {
            _runner.Despawn(
                _spawnedWeapon);
        }
    }


    private void TrySpawnWeapon()
    {
        if (_spawnAttempted)
            return;

        _spawnAttempted = true;

        if (_mapObject == null ||
            !_mapObject.HasStateAuthority ||
            weaponPrefab == null ||
            spawnPoint == null)
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

        _spawnedWeapon =
            _runner.Spawn(
                weaponPrefab,
                spawnPoint.position,
                spawnPoint.rotation);

        _ownsSpawnedWeapon =
            _spawnedWeapon != null;
    }


#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        if (spawnPoint == null)
            return;

        Gizmos.color =
            new Color(
                1f,
                0.75f,
                0.1f,
                1f);

        Gizmos.DrawWireSphere(
            spawnPoint.position,
            0.35f);
    }

#endif
}
