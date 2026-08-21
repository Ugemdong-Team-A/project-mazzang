using System.Collections.Generic;
using UnityEngine;

public sealed class MapRuntime : MonoBehaviour
{
    [Header("Spawns")]
    [SerializeField]
    private Transform[] spawnPoints;

    [Header("Arena")]
    [SerializeField]
    private Transform cameraAnchor;

    [SerializeField]
    private Rect playableBounds =
        new Rect(
            -12f,
            -3.5f,
            24f,
            10.5f);

    [SerializeField]
    private Rect outZoneBounds =
        new Rect(
            -16f,
            -11f,
            32f,
            23f);

    [Header("Camera")]
    [SerializeField]
    private Vector2 cameraFollowOffset =
        Vector2.up;

    [Min(0f)]
    [SerializeField]
    private float fullWeightDistance = 12f;

    [Min(0f)]
    [SerializeField]
    private float farTargetDistance = 18f;

    [Range(0f, 1f)]
    [SerializeField]
    private float farTargetWeight;

    [Min(0.01f)]
    [SerializeField]
    private float minimumOrthoSize = 6.5f;

    [Min(0.01f)]
    [SerializeField]
    private float maximumOrthoSize = 10.5f;

    private readonly List<Vector2>
        _threatPositions =
            new();

    public int SpawnPointCount =>
        spawnPoints?.Length ?? 0;

    public Transform CameraAnchor =>
        cameraAnchor != null
            ? cameraAnchor
            : transform;

    public Rect PlayableBounds =>
        playableBounds;

    public Rect OutZoneBounds =>
        outZoneBounds;

    public Vector2 CameraFollowOffset =>
        cameraFollowOffset;

    public float FullWeightDistance =>
        fullWeightDistance;

    public float FarTargetDistance =>
        farTargetDistance;

    public float FarTargetWeight =>
        farTargetWeight;

    public float MinimumOrthoSize =>
        minimumOrthoSize;

    public float MaximumOrthoSize =>
        maximumOrthoSize;


    private void Start()
    {
        BattleCameraController.Instance?
            .ApplyMapSettings(
                this);
    }

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

        CollectThreatPositions();

        Transform spawnPoint =
            GetSafestSpawnPoint(
                _threatPositions);

        if (spawnPoint != null)
        {
            return spawnPoint;
        }

        int spawnIndex =
            Random.Range(
                0, SpawnPointCount);

        return GetSpawnPoint(
            spawnIndex);
    }


    public Transform GetSafestSpawnPoint(
        IReadOnlyList<Vector2> threatPositions)
    {
        if (SpawnPointCount <= 0)
        {
            return null;
        }

        if (threatPositions == null ||
            threatPositions.Count == 0)
        {
            return null;
        }

        Transform safestPoint = null;
        float safestDistance =
            float.NegativeInfinity;

        foreach (Transform spawnPoint
                 in spawnPoints)
        {
            if (spawnPoint == null)
                continue;

            float nearestThreatDistance =
                float.PositiveInfinity;

            foreach (Vector2 threatPosition
                     in threatPositions)
            {
                float distance =
                    ((Vector2)spawnPoint.position -
                     threatPosition).sqrMagnitude;

                nearestThreatDistance =
                    Mathf.Min(
                        nearestThreatDistance,
                        distance);
            }

            if (nearestThreatDistance <=
                safestDistance)
            {
                continue;
            }

            safestPoint = spawnPoint;
            safestDistance =
                nearestThreatDistance;
        }

        return safestPoint;
    }


    private void CollectThreatPositions()
    {
        _threatPositions.Clear();

        PlayerHealth[] players =
            FindObjectsByType<PlayerHealth>(
                FindObjectsSortMode.None);

        foreach (PlayerHealth player
                 in players)
        {
            if (player == null ||
                !player.IsAlive)
            {
                continue;
            }

            _threatPositions.Add(
                player.transform.position);
        }
    }


#if UNITY_EDITOR

    private void OnValidate()
    {
        fullWeightDistance =
            Mathf.Max(
                0f,
                fullWeightDistance);

        farTargetDistance =
            Mathf.Max(
                fullWeightDistance + 0.01f,
                farTargetDistance);

        minimumOrthoSize =
            Mathf.Max(
                0.01f,
                minimumOrthoSize);

        maximumOrthoSize =
            Mathf.Max(
                minimumOrthoSize,
                maximumOrthoSize);

        playableBounds.width =
            Mathf.Max(
                0f,
                playableBounds.width);

        playableBounds.height =
            Mathf.Max(
                0f,
                playableBounds.height);

        outZoneBounds.width =
            Mathf.Max(
                playableBounds.width,
                outZoneBounds.width);

        outZoneBounds.height =
            Mathf.Max(
                playableBounds.height,
                outZoneBounds.height);
    }


    private void OnDrawGizmosSelected()
    {
        DrawBounds(
            playableBounds,
            Color.green);

        DrawBounds(
            outZoneBounds,
            Color.red);

        if (cameraAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                cameraAnchor.position,
                0.3f);
        }
    }


    private void DrawBounds(
        Rect bounds,
        Color color)
    {
        Gizmos.color = color;

        Vector3 center =
            transform.TransformPoint(
                new Vector3(
                    bounds.center.x,
                    bounds.center.y,
                    0f));

        Vector3 size =
            Vector3.Scale(
                new Vector3(
                    bounds.width,
                    bounds.height,
                    0f),
                transform.lossyScale);

        Gizmos.DrawWireCube(
            center,
            size);
    }

#endif
}
