using UnityEngine;
using Fusion;

public class NetworkBootstrap4Test : MonoBehaviour
{
    [SerializeField] NetworkRunner runnerPrefab;
    [SerializeField] NetworkObject testPlayer;

    NetworkRunner _runner;

    async void StartTest()
    {
        await _runner.StartGame(
            new StartGameArgs
            {
                GameMode = GameMode.AutoHostOrClient,
                SessionName = "69¾¾¹ß74",
                IsVisible = false,
                IsOpen = true,
                PlayerCount = 3,
                SceneManager = _runner.AddBehaviour<NetworkSceneManagerDefault>()
            });

        _runner.Spawn(testPlayer, inputAuthority: _runner.LocalPlayer);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _runner = Instantiate(runnerPrefab);

        StartTest();
    }
}
