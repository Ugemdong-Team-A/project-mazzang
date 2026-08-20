using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public sealed class CameraShakeService :
    MonoBehaviour
{
    private readonly struct ShakeRequest
    {
        public ShakeRequest(
            CameraShakeProfile profile,
            Vector3 worldPosition,
            float multiplier)
        {
            Profile = profile;
            WorldPosition = worldPosition;
            Multiplier = multiplier;
        }

        public CameraShakeProfile Profile { get; }

        public Vector3 WorldPosition { get; }

        public float Multiplier { get; }
    }

    public static CameraShakeService Instance
    {
        get;
        private set;
    }

    [SerializeField]
    private CinemachineImpulseSource impulseSource;

    [Header("Defaults")]
    [SerializeField]
    private CameraShakeProfile defaultHitProfile;

    [SerializeField]
    private CameraShakeProfile defaultDeathProfile;

    [Header("Limits")]
    [Range(0f, 1f)]
    [SerializeField]
    private float globalIntensity = 1f;

    [Min(0f)]
    [SerializeField]
    private float maximumForce = 1.25f;

    private readonly List<ShakeRequest> _pendingRequests =
        new();

    private readonly Dictionary<int, float> _lastPlayedTimes =
        new();

    private readonly Dictionary<
        int,
        CinemachineImpulseDefinition> _profileDefinitions =
        new();


    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (impulseSource == null)
        {
            impulseSource =
                GetComponent<CinemachineImpulseSource>();
        }
    }


    private void LateUpdate()
    {
        FlushRequests();
    }


    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


    public static void Play(
        CameraShakeProfile profile,
        Vector3 worldPosition,
        float multiplier = 1f)
    {
        if (Instance == null ||
            profile == null ||
            multiplier <= 0f)
        {
            return;
        }

        Instance._pendingRequests.Add(
            new ShakeRequest(
                profile,
                worldPosition,
                multiplier));
    }


    public static void PlayDefaultHit(
        Vector3 worldPosition,
        float multiplier = 1f)
    {
        Play(
            Instance != null
                ? Instance.defaultHitProfile
                : null,
            worldPosition,
            multiplier);
    }


    public static void PlayDefaultDeath(
        Vector3 worldPosition,
        float multiplier = 1f)
    {
        Play(
            Instance != null
                ? Instance.defaultDeathProfile
                : null,
            worldPosition,
            multiplier);
    }


    private void FlushRequests()
    {
        if (_pendingRequests.Count == 0)
            return;

        Camera mainCamera =
            Camera.main;

        float now =
            Time.unscaledTime;

        CameraShakeProfile selectedProfile =
            null;

        float selectedForce =
            0f;

        Vector3 selectedWorldPosition =
            default;

        for (int i = 0;
             i < _pendingRequests.Count;
             i++)
        {
            ShakeRequest request =
                _pendingRequests[i];

            CameraShakeProfile profile =
                request.Profile;

            if (!CanPlay(
                    profile,
                    now))
            {
                continue;
            }

            float force =
                profile.Force *
                request.Multiplier *
                CalculateDistanceFactor(
                    profile,
                    request.WorldPosition,
                    mainCamera);

            if (force <= selectedForce)
                continue;

            selectedProfile =
                profile;

            selectedForce =
                force;

            selectedWorldPosition =
                request.WorldPosition;
        }

        _pendingRequests.Clear();

        if (selectedProfile == null ||
            impulseSource == null)
        {
            return;
        }

        float finalForce =
            Mathf.Min(
                selectedForce,
                maximumForce) *
            globalIntensity;

        if (finalForce <= 0f)
            return;

        CinemachineImpulseDefinition definition =
            GetDefinition(
                selectedProfile);

        if (definition == null)
            return;

        definition.CreateEvent(
            selectedWorldPosition,
            impulseSource.DefaultVelocity *
            finalForce);

        _lastPlayedTimes[
            selectedProfile.GetInstanceID()] =
            now;
    }


    private CinemachineImpulseDefinition GetDefinition(
        CameraShakeProfile profile)
    {
        int profileId =
            profile.GetInstanceID();

        if (_profileDefinitions.TryGetValue(
                profileId,
                out CinemachineImpulseDefinition definition))
        {
            return definition;
        }

        CinemachineImpulseDefinition template =
            impulseSource.ImpulseDefinition;

        if (template == null)
            return null;

        definition =
            new CinemachineImpulseDefinition
            {
                ImpulseChannel =
                    template.ImpulseChannel,

                ImpulseShape =
                    template.ImpulseShape,

                CustomImpulseShape =
                    template.CustomImpulseShape,

                ImpulseDuration =
                    profile.Duration,

                ImpulseType =
                    template.ImpulseType,

                DissipationRate =
                    template.DissipationRate,

                RawSignal =
                    template.RawSignal,

                AmplitudeGain =
                    template.AmplitudeGain,

                FrequencyGain =
                    template.FrequencyGain,

                RepeatMode =
                    template.RepeatMode,

                Randomize =
                    template.Randomize,

                TimeEnvelope =
                    template.TimeEnvelope,

                ImpactRadius =
                    template.ImpactRadius,

                DirectionMode =
                    template.DirectionMode,

                DissipationMode =
                    template.DissipationMode,

                DissipationDistance =
                    template.DissipationDistance,

                PropagationSpeed =
                    template.PropagationSpeed
            };

        _profileDefinitions.Add(
            profileId,
            definition);

        return definition;
    }


    private bool CanPlay(
        CameraShakeProfile profile,
        float now)
    {
        if (profile == null)
            return false;

        if (profile.MinimumInterval <= 0f)
            return true;

        return
            !_lastPlayedTimes.TryGetValue(
                profile.GetInstanceID(),
                out float lastPlayedTime) ||
            now - lastPlayedTime >=
            profile.MinimumInterval;
    }


    private static float CalculateDistanceFactor(
        CameraShakeProfile profile,
        Vector3 worldPosition,
        Camera mainCamera)
    {
        if (mainCamera == null ||
            profile.MaxDistance <= 0f)
        {
            return 1f;
        }

        float distance =
            Vector2.Distance(
                mainCamera.transform.position,
                worldPosition);

        float factor =
            1f -
            Mathf.Clamp01(
                distance /
                profile.MaxDistance);

        return Mathf.Lerp(
            profile.MinimumDistanceFactor,
            1f,
            factor);
    }
}
