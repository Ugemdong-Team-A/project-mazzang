using UnityEngine;
using Fusion;

public class NetworkBootstrap4Test : MonoBehaviour
{
    [SerializeField] NetworkRunner runnerPrefab;
    [Space]
    [SerializeField] NetworkObject testPlayer;
    [SerializeField] NetworkObject weapon4Test;

    NetworkRunner _runner;

    async void StartTest()
    {
        Debug.Log("테스트 메서드 시작!");

        await _runner.StartGame(
            new StartGameArgs
            {
                GameMode = GameMode.AutoHostOrClient,
                SessionName = "69씨발74",
                IsVisible = false,
                IsOpen = true,
                PlayerCount = 3,
                SceneManager = _runner.AddBehaviour<NetworkSceneManagerDefault>()
            });

        Debug.Log("러너 스폰됨, 테스트 오브젝트 생성 중..");

        if (testPlayer != null)
            _runner.Spawn(testPlayer, inputAuthority: _runner.LocalPlayer);

        if (weapon4Test != null)
            _runner.Spawn(weapon4Test, inputAuthority: _runner.LocalPlayer, position: new Vector3(-3f, 0f));
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _runner = Instantiate(runnerPrefab);

        StartTest();
    }
}
