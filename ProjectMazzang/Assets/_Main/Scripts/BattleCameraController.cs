using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public sealed class BattleCameraController : MonoBehaviour
{
    public static BattleCameraController Instance { get; private set; }

    [Header("Battle")]
    [SerializeField]
    private CinemachineTargetGroup targetGroup;

    [SerializeField]
    private CinemachineCamera battleCamera;

    [Header("Winner")]
    [SerializeField]
    private CinemachineCamera winnerCamera;

    [Header("Target")]
    [SerializeField]
    private float defaultTargetWeight = 1f;

    [SerializeField]
    private float defaultTargetRadius = 1f;

    [Header("Shake")]
    [SerializeField]
    private CinemachineImpulseSource impulseSource;

    [SerializeField]
    private float hitShakeForce = 0.2f;

    [SerializeField]
    private float deathShakeForce = 0.65f;

    [SerializeField]
    private float maxShakeDistance = 15f;

    [SerializeField]
    [Range(0f, 1f)]
    private float minimumDistanceFactor = 0.25f;


    private readonly HashSet<Transform> targets =
        new();


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Winner Camera는 평소에는 사용하지 않는다.
        if (winnerCamera != null)
        {
            winnerCamera.enabled = false;
        }
    }


    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


    // =========================================================
    // Target
    // =========================================================

    public void AddTarget(
        Transform target)
    {
        if (target == null ||
            targetGroup == null)
        {
            return;
        }

        if (!targets.Add(target))
            return;

        targetGroup.AddMember(
            target,
            defaultTargetWeight,
            defaultTargetRadius);
    }


    public void RemoveTarget(
        Transform target)
    {
        if (target == null ||
            targetGroup == null)
        {
            return;
        }

        if (!targets.Remove(target))
            return;

        targetGroup.RemoveMember(
            target);
    }


    public void ClearTargets()
    {
        foreach (Transform target in targets)
        {
            if (target != null)
            {
                targetGroup.RemoveMember(
                    target);
            }
        }

        targets.Clear();
    }


    // =========================================================
    // Shake
    // =========================================================

    public void PlayHitShake(
        Vector3 worldPosition,
        float multiplier = 1f)
    {
        PlayShake(
            worldPosition,
            hitShakeForce * multiplier);
    }


    public void PlayDeathShake(
        Vector3 worldPosition,
        float multiplier = 1f)
    {
        PlayShake(
            worldPosition,
            deathShakeForce * multiplier);
    }

    private void PlayShake(
        Vector3 worldPosition,
        float force)
    {
        if (impulseSource == null)
            return;

        float distanceFactor =
            CalculateDistanceFactor(
                worldPosition);

        impulseSource
            .GenerateImpulseWithForce(
                force * distanceFactor);
    }

    private float CalculateDistanceFactor(
        Vector3 worldPosition)
    {
        if (Camera.main == null ||
            maxShakeDistance <= 0f)
        {
            return 1f;
        }

        Vector2 cameraPosition =
            Camera.main.transform.position;

        Vector2 impactPosition =
            worldPosition;

        float distance =
            Vector2.Distance(
                cameraPosition,
                impactPosition);

        float factor =
            1f -
            Mathf.Clamp01(
                distance /
                maxShakeDistance);

        return Mathf.Lerp(
            minimumDistanceFactor,
            1f,
            factor);
    }


    // =========================================================
    // Winner
    // =========================================================

    public void FocusWinner(
        Transform winner)
    {
        if (winner == null ||
            winnerCamera == null)
        {
            return;
        }

        winnerCamera.Follow =
            winner;

        winnerCamera.LookAt =
            winner;

        winnerCamera.enabled =
            true;
    }


    public void RestoreBattleView()
    {
        if (winnerCamera == null)
            return;

        winnerCamera.enabled =
            false;

        winnerCamera.Follow =
            null;

        winnerCamera.LookAt =
            null;
    }
}