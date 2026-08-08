using UnityEngine;
using Fusion;

public enum PlayerButton
{
    Jump = 0,
}

public struct PlayerInputData : INetworkInput
{
    public Vector2 Move;
    public NetworkButtons Buttons;
}