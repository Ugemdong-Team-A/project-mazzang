using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public enum BuildVersionState
{
    None,
    Checking,
    Available,
    Outdated,
    Failed
}

public sealed class BuildVersionChecker :
    MonoBehaviour
{
    [Header("Version Source")]
    [SerializeField]
    private string versionUrl;

    [Min(1)]
    [SerializeField]
    private int timeoutSeconds = 5;

    public BuildVersionState State
    {
        get;
        private set;
    }

    public string CurrentVersion =>
        Application.version;

    public string LatestVersion
    {
        get;
        private set;
    }

    public event Action<BuildVersionState>
        StateChanged;

    public void Check()
    {
        if (State ==
            BuildVersionState.Checking)
        {
            return;
        }

        StartCoroutine(
            CheckRoutine());
    }

    private IEnumerator CheckRoutine()
    {
        SetState(
            BuildVersionState.Checking);

        if (string.IsNullOrWhiteSpace(
                versionUrl))
        {
            SetState(
                BuildVersionState.Failed);

            yield break;
        }

        using (UnityWebRequest request =
               UnityWebRequest.Get(
                   versionUrl))
        {
            request.timeout =
                timeoutSeconds;

            yield return
                request.SendWebRequest();

            if (request.result !=
                UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    $"[Version] 버전 확인 실패: {request.error}",
                    this);

                SetState(
                    BuildVersionState.Failed);

                yield break;
            }

            LatestVersion =
                request.downloadHandler
                    .text
                    .Trim();
        }

        bool available =
            string.Equals(
                CurrentVersion,
                LatestVersion,
                StringComparison.Ordinal);

        SetState(
            available
                ? BuildVersionState.Available
                : BuildVersionState.Outdated);
    }

    private void SetState(
        BuildVersionState newState)
    {
        if (State ==
            newState)
        {
            return;
        }

        State =
            newState;

        StateChanged?.Invoke(
            State);
    }
}