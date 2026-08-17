using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class NetworkMovingPlatform :
    MonoBehaviour
{
    [SerializeField]
    private Rigidbody2D body;

    [Header("Route (Parent Local Space)")]
    [SerializeField]
    private Vector2 pointA;

    [SerializeField]
    private Vector2 pointB =
        Vector2.up;

    [Min(0.1f)]
    [SerializeField]
    private float travelDuration = 2.5f;

    [Min(0f)]
    [SerializeField]
    private float endpointPause = 0.35f;

    [Min(0f)]
    [SerializeField]
    private float phaseOffset;

    private NetworkRunner _runner;


    private void Awake()
    {
        if (body == null)
        {
            body =
                GetComponent<Rigidbody2D>();
        }

        _runner =
            FindFirstObjectByType<
                NetworkRunner>();

        ApplyPosition(
            pointA,
            false);
    }


    private void FixedUpdate()
    {
        if (_runner == null)
        {
            _runner =
                FindFirstObjectByType<
                    NetworkRunner>();
        }

        if (_runner == null)
            return;

        float simulationTime =
            _runner.Tick.Raw *
            _runner.DeltaTime +
            phaseOffset;

        ApplyPosition(
            EvaluateLocalPosition(
                simulationTime),
            true);
    }


    private Vector2 EvaluateLocalPosition(
        float time)
    {
        float safeTravelDuration =
            Mathf.Max(
                0.1f,
                travelDuration);

        float halfCycle =
            safeTravelDuration +
            endpointPause;

        float cycleDuration =
            halfCycle * 2f;

        float cycleTime =
            Mathf.Repeat(
                time,
                cycleDuration);

        bool returning =
            cycleTime >= halfCycle;

        float legTime =
            returning
                ? cycleTime - halfCycle
                : cycleTime;

        float t =
            Mathf.Clamp01(
                legTime /
                safeTravelDuration);

        t =
            Mathf.SmoothStep(
                0f,
                1f,
                t);

        return returning
            ? Vector2.Lerp(
                pointB,
                pointA,
                t)
            : Vector2.Lerp(
                pointA,
                pointB,
                t);
    }


    private void ApplyPosition(
        Vector2 localPosition,
        bool usePhysics)
    {
        Transform parent =
            transform.parent;

        Vector2 worldPosition =
            parent != null
                ? parent.TransformPoint(
                    localPosition)
                : localPosition;

        if (usePhysics &&
            body != null)
        {
            body.MovePosition(
                worldPosition);

            return;
        }

        transform.position =
            worldPosition;
    }


#if UNITY_EDITOR

    private void OnValidate()
    {
        travelDuration =
            Mathf.Max(
                0.1f,
                travelDuration);

        endpointPause =
            Mathf.Max(
                0f,
                endpointPause);

        phaseOffset =
            Mathf.Max(
                0f,
                phaseOffset);
    }


    private void OnDrawGizmosSelected()
    {
        Transform parent =
            transform.parent;

        Vector3 worldA =
            parent != null
                ? parent.TransformPoint(
                    pointA)
                : pointA;

        Vector3 worldB =
            parent != null
                ? parent.TransformPoint(
                    pointB)
                : pointB;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            worldA,
            worldB);

        Gizmos.DrawWireSphere(
            worldA,
            0.2f);

        Gizmos.DrawWireSphere(
            worldB,
            0.2f);
    }

#endif
}
