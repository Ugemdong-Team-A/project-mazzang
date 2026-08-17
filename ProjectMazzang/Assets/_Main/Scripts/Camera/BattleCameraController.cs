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

    [Header("Target Framing")]
    [SerializeField]
    private Transform targetWeightCenter;

    [SerializeField]
    private float fullWeightDistance = 12f;

    [SerializeField]
    private float farTargetDistance = 18f;

    [SerializeField]
    [Range(0f, 1f)]
    private float farTargetWeight;

    [SerializeField]
    private float targetWeightChangeSpeed = 3f;

    private readonly HashSet<Transform> targets =
        new();

    private Vector3 _fallbackTargetWeightCenter;

    private CinemachineGroupFraming _groupFraming;

    private CinemachineFollow _cameraFollow;


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

        ResolveBattleCameraComponents();

        if (targetWeightCenter != null)
        {
            _fallbackTargetWeightCenter =
                targetWeightCenter.position;
        }
        else if (targetGroup != null)
        {
            // TargetGroup이 플레이어를 따라 움직이기 전
            // 초기 위치를 전투 영역 중심으로 사용한다.
            _fallbackTargetWeightCenter =
                targetGroup.transform.position;
        }
        else
        {
            _fallbackTargetWeightCenter =
                transform.position;
        }

        // Winner Camera는 평소에는 사용하지 않는다.
        if (winnerCamera != null)
        {
            winnerCamera.enabled = false;
        }
    }


    private void Update()
    {
        UpdateTargetWeights();
    }


    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


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

        targetWeightChangeSpeed =
            Mathf.Max(
                0f,
                targetWeightChangeSpeed);
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


    public void SetTargetWeightCenter(
        Transform center)
    {
        targetWeightCenter = center;
    }


    public void ApplyMapSettings(
        MapRuntime map)
    {
        if (map == null)
            return;

        targetWeightCenter =
            map.CameraAnchor;

        _fallbackTargetWeightCenter =
            map.CameraAnchor.position;

        fullWeightDistance =
            map.FullWeightDistance;

        farTargetDistance =
            map.FarTargetDistance;

        farTargetWeight =
            map.FarTargetWeight;

        ResolveBattleCameraComponents();

        if (_groupFraming != null)
        {
            _groupFraming.OrthoSizeRange =
                new Vector2(
                    map.MinimumOrthoSize,
                    map.MaximumOrthoSize);
        }

        if (_cameraFollow != null)
        {
            Vector3 followOffset =
                _cameraFollow.FollowOffset;

            followOffset.x =
                map.CameraFollowOffset.x;

            followOffset.y =
                map.CameraFollowOffset.y;

            _cameraFollow.FollowOffset =
                followOffset;
        }
    }


    private void ResolveBattleCameraComponents()
    {
        if (battleCamera == null)
            return;

        _groupFraming ??=
            battleCamera.GetComponent<
                CinemachineGroupFraming>();

        _cameraFollow ??=
            battleCamera.GetComponent<
                CinemachineFollow>();
    }


    private void UpdateTargetWeights()
    {
        if (targetGroup == null ||
            targets.Count == 0)
        {
            return;
        }

        Vector2 center =
            GetTargetWeightCenter();

        foreach (Transform target in targets)
        {
            if (target == null)
                continue;

            int index =
                targetGroup.FindMember(
                    target);

            if (index < 0 ||
                index >= targetGroup.Targets.Count)
            {
                continue;
            }

            CinemachineTargetGroup.Target member =
                targetGroup.Targets[index];

            if (member == null)
                continue;

            float distance =
                Vector2.Distance(
                    center,
                    target.position);

            float desiredWeight =
                CalculateTargetWeight(
                    distance);

            member.Weight =
                Mathf.MoveTowards(
                    member.Weight,
                    desiredWeight,
                    targetWeightChangeSpeed *
                    Time.deltaTime);
        }
    }


    private float CalculateTargetWeight(
        float distance)
    {
        if (distance <= fullWeightDistance)
        {
            return defaultTargetWeight;
        }

        if (distance >= farTargetDistance)
        {
            return farTargetWeight;
        }

        float t =
            Mathf.InverseLerp(
                fullWeightDistance,
                farTargetDistance,
                distance);

        // 직선 보간보다 경계에서 조금 자연스럽게
        // Weight가 변하도록 한다.
        t =
            Mathf.SmoothStep(
                0f,
                1f,
                t);

        return Mathf.Lerp(
            defaultTargetWeight,
            farTargetWeight,
            t);
    }


    private Vector2 GetTargetWeightCenter()
    {
        if (targetWeightCenter != null)
        {
            return targetWeightCenter.position;
        }

        return _fallbackTargetWeightCenter;
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
